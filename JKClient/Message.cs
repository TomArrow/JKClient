﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {

	

	internal class BufferedDemoMessageContainer
    {
		public Message msg;
		public DateTime time;
		public bool containsFullSnapshot;
		public int serverTime;
	}

	internal sealed class Message {
		private const int FloatIntBits = 13;
		private const int FloatIntBias = (1<<(Message.FloatIntBits-1));
		private int bit = 0;
		private int bitSaved = 0;
		private bool oobSaved = false;
		private int readCountSaved = 0;
		public bool Overflowed { get; private set; }
		public bool OOB { get; private set; }
		public byte []Data { get; init; }
		public int MaxSize { get; init; }
		public int CurSize { get; set; } = 0;
		public int ReadCount { get; private set; } = 0;
		public int Bit {
			get => this.bit;
			private set => this.bit = value;
		}
		private static readonly Huffman compressor, decompressor;
		static Message() {
			Message.compressor = new Huffman();
			Message.decompressor = new Huffman(true);
			for (int i = 0; i < 256; i++) {
				for (int j = 0; j < Message.hData[i]; j++) {
					Message.compressor.AddReference((byte)i);
					Message.decompressor.AddReference((byte)i);
				}
			}
		}
		public Message() {}
		public Message(byte []data, int length, bool oob = false) {
			this.Data = data;
			this.MaxSize = length;
			this.OOB = oob;
		}
		public Message Clone()
        {
			Message retVal = new Message() { MaxSize= this.MaxSize, Data= (byte[])this.Data.Clone() };
			retVal.Overflowed = this.Overflowed;
			retVal.OOB = this.OOB;
			//retVal.MaxSize = this.MaxSize;
			retVal.CurSize = this.CurSize;
			retVal.ReadCount = this.ReadCount;
			retVal.Bit = this.Bit;
			//retVal.Data = (byte[])this.Data.Clone();
			return retVal;
        }

		public void Bitstream() {
			this.OOB = false;
		}
		public void SaveState() {
			this.bitSaved = this.bit;
			this.oobSaved = this.OOB;
			this.readCountSaved = this.ReadCount;
		}
		public void RestoreState() {
			this.bit = this.bitSaved;
			this.OOB = this.oobSaved;
			this.ReadCount = this.readCountSaved;
		}
		public unsafe void WriteBits(int value, int bits) {
			if (this.MaxSize - this.CurSize < 4) {
				this.Overflowed = true;
				return;
			}
			if (bits == 0 || bits < -31 || bits > 32) {
				throw new JKClientException($"WriteBits: bad bits {bits}");
			}
			if (bits < 0) {
				bits = -bits;
			}
			if (this.OOB) {
				if (bits == 8) {
					this.Data[this.CurSize] = (byte)value;
					this.CurSize += 1;
					this.bit += 8;
				} else if (bits == 16) {
					byte []temp = BitConverter.GetBytes((short)value);
					Array.Copy(temp, 0, this.Data, this.CurSize, 2);
					this.CurSize += 2;
					this.bit += 16;
				} else if (bits == 32) {
					byte []temp = BitConverter.GetBytes(value);
					Array.Copy(temp, 0, this.Data, this.CurSize, 4);
					this.CurSize += 4;
					this.bit += 32;
				}
			} else {
				lock (Message.compressor) {
					value &= (int)(0xffffffff>>(32-bits));
					if ((bits&7) != 0) {
						int nbits = bits&7;
						for (int i = 0; i < nbits; i++) {
							Message.compressor.PutBit((value&1), this.Data, ref this.bit);
							value >>= 1;
						}
						bits -= nbits;
					}
					if (bits != 0) {
						for (int i = 0; i < bits; i+=8) {
							Message.compressor.OffsetTransmit((value&0xff), this.Data, ref this.bit);
							value >>= 8;
						}
					}
					this.CurSize = (this.bit>>3)+1;
				}
			}
		}
		public void WriteByte(int c) {
			this.WriteBits(c, 8);
		}
		public unsafe void WriteData(byte []data, int length) {
			fixed (byte *d = data) {
				this.WriteData(d, length);
			}
		}
		public unsafe void WriteData(byte *data, int length) {
			for (int i = 0; i < length; i++) {
				this.WriteByte(data[i]);
			}
		}
		public void WriteShort(int c) {
			this.WriteBits(c, 16);
		}
		public void WriteLong(int c) {
			this.WriteBits(c, 32);
		}
		public unsafe void WriteString(sbyte []s) {
			if (s == null || s.Length <= 0) {
				this.WriteByte(0);
			} else {
				int l = Common.StrLen(s);
				if (l >= Common.MaxStringChars) {
					this.WriteByte(0);
					return;
				}
				byte []b = new byte[l+1];
				fixed (sbyte *ss = s) {
					Marshal.Copy((IntPtr)ss, b, 0, l);
				}
				this.WriteData(b, l+1);
			}
		}

		public unsafe void WriteBigString(sbyte []s) {
			if (s == null || s.Length <= 0) {
				this.WriteByte(0);
			} else {
				int l = Common.StrLen(s);
				if (l >= Common.BigInfoString) {
					this.WriteByte(0);
					return;
				}
				byte []b = new byte[l+1];
				fixed (sbyte *ss = s) {
					Marshal.Copy((IntPtr)ss, b, 0, l);
				}
				this.WriteData(b, l+1);
			}
		}

		public void WriteDeltaKey(int key, int oldV, int newV, int bits)
		{
			if (oldV == newV)
			{
				this.WriteBits(0, 1);
				return;
			}
			this.WriteBits(1, 1);
			this.WriteBits((newV ^ key) & ((1 << bits) - 1), bits);
		}

		public unsafe void WriteDeltaUsercmdKey(int key, ref UserCommand from, ref UserCommand to) {
			if (to.ServerTime - from.ServerTime < 256) {
				this.WriteBits(1, 1);
				this.WriteBits(to.ServerTime - from.ServerTime, 8);
			} else {
				this.WriteBits(0, 1);
				this.WriteBits(to.ServerTime, 32);
			}
			if (from.Angles[0] == to.Angles[0] &&
				from.Angles[1] == to.Angles[1] &&
				from.Angles[2] == to.Angles[2] &&
				from.ForwardMove == to.ForwardMove &&
				from.RightMove == to.RightMove &&
				from.Upmove == to.Upmove &&
				from.Buttons == to.Buttons &&
				from.Weapon == to.Weapon &&
				from.ForceSelection == to.ForceSelection &&
				from.InventorySelection == to.InventorySelection &&
				from.GenericCmd == to.GenericCmd)
			{
				this.WriteBits(0, 1);               // no change
				return;
			}
			key ^= to.ServerTime;
			this.WriteBits(1, 1);
			this.WriteDeltaKey(key, from.Angles[0], to.Angles[0], 16);
			this.WriteDeltaKey(key, from.Angles[1], to.Angles[1], 16);
			this.WriteDeltaKey(key, from.Angles[2], to.Angles[2], 16);
			this.WriteDeltaKey(key, from.ForwardMove, to.ForwardMove, 8);
			this.WriteDeltaKey(key, from.RightMove, to.RightMove, 8);
			this.WriteDeltaKey(key, from.Upmove, to.Upmove, 8);
			this.WriteDeltaKey(key, from.Buttons, to.Buttons, 16);
			this.WriteDeltaKey(key, from.Weapon, to.Weapon, 8);

			this.WriteDeltaKey(key, from.ForceSelection, to.ForceSelection, 8);
			this.WriteDeltaKey(key, from.InventorySelection, to.InventorySelection, 8);

			this.WriteDeltaKey(key, from.GenericCmd, to.GenericCmd, 8);
		}
		public void BeginReading(bool oob = false) {
			this.ReadCount = 0;
			this.bit = 0;
			this.OOB = oob;
		}
		public unsafe int ReadBits(int bits) {
			int value = 0;
			bool sgn;
			if (bits < 0) {
				bits = -bits;
				sgn = true;
			} else {
				sgn = false;
			}
			if (this.OOB) {
				if (bits == 8) {
					value = this.Data[this.ReadCount];
					this.ReadCount += 1;
					this.bit += 8;
				} else if (bits == 16) {
					fixed (byte *b = &this.Data[this.ReadCount]) {
						value = *(short*)b;
					}
					this.ReadCount += 2;
					this.bit += 16;
				} else if (bits == 32) {
					fixed (byte *b = &this.Data[this.ReadCount]) {
						value = *(int*)b;
					}
					this.ReadCount += 4;
					this.bit += 32;
				}
			} else {
				lock (Message.decompressor) {
					int nbits = 0;
					if ((bits&7) != 0) {
						nbits = bits&7;
						for (int i = 0; i < nbits; i++) {
							value |= (Message.decompressor.GetBit(this.Data, ref this.bit)<<i);
						}
						bits -= nbits;
					}
					if (bits != 0) {
						for (int i = 0; i < bits; i+=8) {
							int get = 0;
							Message.decompressor.OffsetReceive(ref get, this.Data, ref this.bit);
							value |= (get<<(i+nbits));
						}
					}
					this.ReadCount = (this.bit>>3)+1;
				}
			}
			if (sgn) {
				if ((value & (1 << (bits - 1))) != 0) {
					value |= -1 ^ ((1 << bits) - 1);
				}
			}
			return value;
		}
		public int ReadByte() {
			int c = (byte)this.ReadBits(8);
			if (this.ReadCount > this.CurSize) {
				c = -1;
			}
			return c;
		}
		public int ReadShort() {
			int c = (short)this.ReadBits(16);
			if (this.ReadCount > this.CurSize) {
				c = -1;
			}
			return c;
		}
		public int ReadLong() {
			int c = this.ReadBits(32);
			if (this.ReadCount > this.CurSize) {
				c = -1;
			}
			return c;
		}
		public sbyte []ReadString() {
			sbyte []str = new sbyte[Common.MaxStringChars];
			int l, c;
			l = 0;
			do {
				c = this.ReadByte();
				if (c == -1 || c == 0) {
					break;
				}
				if (c == 37) { //'%'
					c = 46; //'.'
				}
				str[l] = (sbyte)c;
				l++;
			} while (l < sizeof(sbyte)*Common.MaxStringChars-1);
			if (l <= sizeof(sbyte)*Common.MaxStringChars) {
				str[l] = 0;
			} else {
				str[sizeof(sbyte)*Common.MaxStringChars-1] = 0;
			}
			return str;
		}
		public string ReadStringAsString() {
			sbyte []str = this.ReadString();
			return Common.ToString(str);
		}
		public sbyte []ReadBigString() {
			sbyte []str = new sbyte[Common.BigInfoString];
			int l, c;
			l = 0;
			do {
				c = this.ReadByte();
				if (c == -1 || c == 0) {
					break;
				}
				if (c == 37) { //'%'
					c = 46; //'.'
				}
				str[l] = (sbyte)c;
				l++;
			} while (l < sizeof(sbyte)*Common.BigInfoString-1);
			str[l] = 0;
			return str;
		}
		public string ReadBigStringAsString() {
			sbyte []str = this.ReadBigString();
			return Common.ToString(str);
		}
		public sbyte []ReadStringLine() {
			sbyte []str = new sbyte[Common.MaxStringChars];
			int l, c;
			l = 0;
			do {
				c = this.ReadByte();
				if (c == -1 || c == 0 || c == 10) { //'\n'
					break;
				}
				if (c == 37) { //'%'
					c = 46; //'.'
				}
				str[l] = (sbyte)c;
				l++;
			} while (l < sizeof(sbyte)*Common.MaxStringChars-1);
			str[l] = 0;
			return str;
		}
		public string ReadStringLineAsString() {
			sbyte []str = this.ReadStringLine();
			return Common.ToString(str);
		}
		//we don't really need any Data in assetsless client
		public void ReadData(byte []data, int len) {
			for (int i = 0; i < len; i++) {
				/*data[i] = (byte)*/this.ReadByte();
			}
		}

		// TODO: This must be adapted to the new Q3 stuff I think, see ReadDeltaEntity.
		public unsafe void WriteDeltaEntity(EntityState* from, EntityState* to,bool force, ClientVersion version, IClientHandler clientHandler)
        {

			// a NULL to is a delta remove message
			if (to == null)
			{
				if (from == null)
				{
					return;
				}
				this.WriteBits(from->Number, Common.GEntitynumBits);
				this.WriteBits(1, 1);
				return;
			}

			if (to->Number < 0 || to->Number >= Common.MaxGEntities)
			{
				throw new JKClientException($"Bad delta entity number: {to->Number}");
			}

			int lc = 0;
			// build the change vector as bytes so it is endien independent

			var fields = clientHandler.GetEntityStateFields();
			if(clientHandler.Protocol != (int)ProtocolVersion.Protocol15 && clientHandler.Protocol != (int)ProtocolVersion.Protocol16 && clientHandler.Protocol != (int)ProtocolVersion.Protocol26)
            {
				throw new JKClientException($"WriteDeltaEntity: Only protocols 15 and 16 (Jedi Outcast) and protocol 26 (Jedi Academy) supported right now.");
			}

			int numFields = fields.Count;
			int* fromF, toF;
			for (int i = 0; i < numFields; i++)
			{
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				if (*fromF != *toF)
				{
					lc = i + 1;
				}
			}

			if (lc == 0)
			{
				// nothing at all changed
				if (!force)
				{
					return;     // nothing at all
				}
				// write two bits for no change
				this.WriteBits(to->Number, Common.GEntitynumBits);
				this.WriteBits(0, 1);       // not removed
				this.WriteBits(0, 1);       // no delta
				return;
			}

			this.WriteBits(to->Number, Common.GEntitynumBits);
			this.WriteBits(0, 1);           // not removed
			this.WriteBits(1, 1);           // we have a delta

			this.WriteByte(lc); // # of changes

			//oldsize += numFields;  // ?!?!

			float fullFloat;
			int trunc;

			for (int i = 0; i < lc; i++)
			{
				//gLastField = field;
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);

				if (*fromF == *toF)
				{
					this.WriteBits( 0, 1);   // no change
					continue;
				}

				this.WriteBits( 1, 1);   // changed

				if (fields[i].Bits == 0)
				{
					// float
					fullFloat = *(float*)toF;
					trunc = (int)fullFloat;

					if (fullFloat == 0.0f)
					{
						this.WriteBits(0, 1);
						//oldsize += FLOAT_INT_BITS;  // ??
					}
					else
					{
						this.WriteBits(1, 1);
						if (trunc == fullFloat && trunc + FloatIntBias >= 0 &&
							trunc + FloatIntBias < (1 << FloatIntBits))
						{
							// send as small integer
							this.WriteBits( 0, 1);
							this.WriteBits( trunc + FloatIntBias, FloatIntBits);
						}
						else
						{
							// send as full floating point value
							this.WriteBits( 1, 1);
							this.WriteBits( *toF, 32);
						}
					}
				}
				else
				{
					if (*toF == 0)
					{
						this.WriteBits( 0, 1);
					}
					else
					{
						this.WriteBits( 1, 1);
						// integer
						this.WriteBits( *toF, fields[i].Bits);
					}
				}
			}

			//gLastField = &noField; // ?

		}

		public unsafe void ReadDeltaEntity(EntityState *from, EntityState *to, int number, IClientHandler clientHandler, StringBuilder debugString = null) {
			if (number < 0 || number >= Common.MaxGEntities) {
				throw new JKClientException($"Bad delta entity number: {number}");
			}

			bool print = debugString != null;

			if (this.ReadBits(1) == 1) {
				Common.MemSet(to, 0, sizeof(EntityState));
				to->Number = Common.MaxGEntities - 1;
                if (print)
                {
					debugString.Append($"{this.ReadCount}: #{number} remove\n");
                }
				return;
			}
			if (this.ReadBits(1) == 0) {
				*to = *from;
				to->Number = number;
				return;
			}


			var fields = clientHandler.GetEntityStateFields();
			int lc = this.ReadByte();

            if (print)
            {
				debugString.Append($"{this.ReadCount}: #{number} ");
			}

			to->Number = number;
			int* fromF, toF;
			int trunc;
			for (int i = 0; i < lc; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				if (this.ReadBits(1) == 0) {
					*toF = *fromF;
				} else {
					int bits = fields[i].Bits;
					if (bits == 0) {
						if (this.ReadBits(1) == 0) {
							*(float*)toF = 0.0f;
						} else {
							if (this.ReadBits(1) == 0) {
								trunc = this.ReadBits(Message.FloatIntBits);
								trunc -= Message.FloatIntBias;
								*(float*)toF = trunc;
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{trunc} ");
								}
							} else {
								*toF = this.ReadBits(32);
								if (print)
								{
									debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
								}
							}
						}
					} else {
						if (this.ReadBits(1) == 0) {
							*toF = 0;
						} else {
							*toF = this.ReadBits(bits);
							if (print)
							{
								debugString.Append($"{fields[i].Name}:{*toF} ");
							}
						}
					}
					fields[i].Adjust?.Invoke(toF);
				}
			}
			for (int i = lc; i < fields.Count; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				*toF = *fromF;
				//fields[i].Adjust?.Invoke(toF);
			}
			if (print)
			{
				debugString.Append($"\n");
			}
		}
		public unsafe void ReadDeltaPlayerstate(PlayerState *from, PlayerState *to, bool isVehicle, IClientHandler clientHandler, StringBuilder debugString = null) {
			GCHandle fromHandle;
			if (from == null) {
				fromHandle = GCHandle.Alloc(PlayerState.Null, GCHandleType.Pinned);
				from = (PlayerState *)fromHandle.AddrOfPinnedObject();
			} else {
				fromHandle = new GCHandle();
			}
			*to = *from;
			bool isPilot() {
				return this.ReadBits(1) != 0;
			}

			bool print = debugString != null;
            if (print)
            {
				debugString.Append($"{this.ReadCount}: playerstate ");
			}

			var fields = clientHandler.GetPlayerStateFields(isVehicle, isPilot);
			int lc = this.ReadByte();
			int* fromF, toF;
			int trunc;
			for (int i = 0; i < lc; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				if (this.ReadBits(1) == 0) {
					*toF = *fromF;
				} else {
					int bits = fields[i].Bits;
					if (bits == 0) {
						if (this.ReadBits(1) == 0) {
							trunc = this.ReadBits(Message.FloatIntBits);
							trunc -= Message.FloatIntBias;
							*(float*)toF = trunc;
							if (print)
							{
								debugString.Append($"{fields[i].Name}:{trunc} ");
							}
						} else {
							*toF = this.ReadBits(32);
							if (print)
							{
								debugString.Append($"{fields[i].Name}:{*(float*)toF} ");
							}
						}
					} else {
						*toF = this.ReadBits(bits); 
						if (print)
						{
							debugString.Append($"{fields[i].Name}:{*toF} ");
						}
					}
					fields[i].Adjust?.Invoke(toF);
				}
			}
			for (int i = lc; i < fields.Count; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				*toF = *fromF;
				//fields[i].Adjust?.Invoke(toF);
			}
			if (this.ReadBits(1) != 0) {
				if (this.ReadBits(1) != 0) {
					int bits = this.ReadShort();
					for (int i = 0; i < 16; i++) {
						if ((bits & (1<<i)) != 0) {
							if (i == 4
								&& (clientHandler.Protocol == (int)ProtocolVersion.Protocol25
								|| clientHandler.Protocol == (int)ProtocolVersion.Protocol26)) {
								to->Stats[i] = this.ReadBits(19);
							} else {
								to->Stats[i] = this.ReadShort();
							}
						}
					}
				}
				if (this.ReadBits(1) != 0) {
					int bits = this.ReadShort();
					for (int i = 0; i < 16; i++) {
						if ((bits & (1<<i)) != 0) {
							to->Persistant[i] = this.ReadShort();
						}
					}
				}
				if (this.ReadBits(1) != 0) {
					int bits = this.ReadShort();
					for (int i = 0; i < 16; i++) {
						if ((bits & (1<<i)) != 0) {
							to->Ammo[i] = this.ReadShort();
						}
					}
				}
				if (this.ReadBits(1) != 0) {
					int bits = this.ReadShort();
					for (int i = 0; i < 16; i++) {
						if ((bits & (1<<i)) != 0) {
							to->PowerUps[i] = this.ReadLong();
						}
					}
				}
			}
			if (fromHandle.IsAllocated) {
				fromHandle.Free();
			}

			if (print)
			{
				debugString.Append("\n");
			}
		}

		private static readonly int []hData = new int[256]{
			250315,			// 0
			41193,			// 1
			6292,			// 2
			7106,			// 3
			3730,			// 4
			3750,			// 5
			6110,			// 6
			23283,			// 7
			33317,			// 8
			6950,			// 9
			7838,			// 10
			9714,			// 11
			9257,			// 12
			17259,			// 13
			3949,			// 14
			1778,			// 15
			8288,			// 16
			1604,			// 17
			1590,			// 18
			1663,			// 19
			1100,			// 20
			1213,			// 21
			1238,			// 22
			1134,			// 23
			1749,			// 24
			1059,			// 25
			1246,			// 26
			1149,			// 27
			1273,			// 28
			4486,			// 29
			2805,			// 30
			3472,			// 31
			21819,			// 32
			1159,			// 33
			1670,			// 34
			1066,			// 35
			1043,			// 36
			1012,			// 37
			1053,			// 38
			1070,			// 39
			1726,			// 40
			888,			// 41
			1180,			// 42
			850,			// 43
			960,			// 44
			780,			// 45
			1752,			// 46
			3296,			// 47
			10630,			// 48
			4514,			// 49
			5881,			// 50
			2685,			// 51
			4650,			// 52
			3837,			// 53
			2093,			// 54
			1867,			// 55
			2584,			// 56
			1949,			// 57
			1972,			// 58
			940,			// 59
			1134,			// 60
			1788,			// 61
			1670,			// 62
			1206,			// 63
			5719,			// 64
			6128,			// 65
			7222,			// 66
			6654,			// 67
			3710,			// 68
			3795,			// 69
			1492,			// 70
			1524,			// 71
			2215,			// 72
			1140,			// 73
			1355,			// 74
			971,			// 75
			2180,			// 76
			1248,			// 77
			1328,			// 78
			1195,			// 79
			1770,			// 80
			1078,			// 81
			1264,			// 82
			1266,			// 83
			1168,			// 84
			965,			// 85
			1155,			// 86
			1186,			// 87
			1347,			// 88
			1228,			// 89
			1529,			// 90
			1600,			// 91
			2617,			// 92
			2048,			// 93
			2546,			// 94
			3275,			// 95
			2410,			// 96
			3585,			// 97
			2504,			// 98
			2800,			// 99
			2675,			// 100
			6146,			// 101
			3663,			// 102
			2840,			// 103
			14253,			// 104
			3164,			// 105
			2221,			// 106
			1687,			// 107
			3208,			// 108
			2739,			// 109
			3512,			// 110
			4796,			// 111
			4091,			// 112
			3515,			// 113
			5288,			// 114
			4016,			// 115
			7937,			// 116
			6031,			// 117
			5360,			// 118
			3924,			// 119
			4892,			// 120
			3743,			// 121
			4566,			// 122
			4807,			// 123
			5852,			// 124
			6400,			// 125
			6225,			// 126
			8291,			// 127
			23243,			// 128
			7838,			// 129
			7073,			// 130
			8935,			// 131
			5437,			// 132
			4483,			// 133
			3641,			// 134
			5256,			// 135
			5312,			// 136
			5328,			// 137
			5370,			// 138
			3492,			// 139
			2458,			// 140
			1694,			// 141
			1821,			// 142
			2121,			// 143
			1916,			// 144
			1149,			// 145
			1516,			// 146
			1367,			// 147
			1236,			// 148
			1029,			// 149
			1258,			// 150
			1104,			// 151
			1245,			// 152
			1006,			// 153
			1149,			// 154
			1025,			// 155
			1241,			// 156
			952,			// 157
			1287,			// 158
			997,			// 159
			1713,			// 160
			1009,			// 161
			1187,			// 162
			879,			// 163
			1099,			// 164
			929,			// 165
			1078,			// 166
			951,			// 167
			1656,			// 168
			930,			// 169
			1153,			// 170
			1030,			// 171
			1262,			// 172
			1062,			// 173
			1214,			// 174
			1060,			// 175
			1621,			// 176
			930,			// 177
			1106,			// 178
			912,			// 179
			1034,			// 180
			892,			// 181
			1158,			// 182
			990,			// 183
			1175,			// 184
			850,			// 185
			1121,			// 186
			903,			// 187
			1087,			// 188
			920,			// 189
			1144,			// 190
			1056,			// 191
			3462,			// 192
			2240,			// 193
			4397,			// 194
			12136,			// 195
			7758,			// 196
			1345,			// 197
			1307,			// 198
			3278,			// 199
			1950,			// 200
			886,			// 201
			1023,			// 202
			1112,			// 203
			1077,			// 204
			1042,			// 205
			1061,			// 206
			1071,			// 207
			1484,			// 208
			1001,			// 209
			1096,			// 210
			915,			// 211
			1052,			// 212
			995,			// 213
			1070,			// 214
			876,			// 215
			1111,			// 216
			851,			// 217
			1059,			// 218
			805,			// 219
			1112,			// 220
			923,			// 221
			1103,			// 222
			817,			// 223
			1899,			// 224
			1872,			// 225
			976,			// 226
			841,			// 227
			1127,			// 228
			956,			// 229
			1159,			// 230
			950,			// 231
			7791,			// 232
			954,			// 233
			1289,			// 234
			933,			// 235
			1127,			// 236
			3207,			// 237
			1020,			// 238
			927,			// 239
			1355,			// 240
			768,			// 241
			1040,			// 242
			745,			// 243
			952,			// 244
			805,			// 245
			1073,			// 246
			740,			// 247
			1013,			// 248
			805,			// 249
			1008,			// 250
			796,			// 251
			996,			// 252
			1057,			// 253
			11457,			// 254
			13504,			// 255
		};
	}
}
