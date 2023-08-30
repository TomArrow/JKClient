using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace JKClient
{
    // MOH related message functions.
    internal  sealed partial class Message
    {

		//
		//
		//
		// MOHAA functions start
		//
		//


		const int MAX_PACKED_COORD = 65536;// TA stuff
		const int MAX_PACKED_COORD_HALF = MAX_PACKED_COORD / 2;// TA stuff
		const int MAX_PACKED_COORD_EXTRA = 262144;// TA stuff
		const int MAX_PACKED_COORD_EXTRA_HALF = MAX_PACKED_COORD_EXTRA / 2;// TA stuff

		unsafe float ReadFloat()
		{
			int l;
			float f;

			l = ReadBits(32);
			if (ReadCount > CurSize)
			{
				f = -1;
			} else
            {
				f = *(float*)&l;
            }

			return f;
		}
		unsafe void WriteFloat( float f)
		{
			int l = *(int*)&f;

			WriteBits(l, 32);
		}


		float ReadCoord()
		{
			float sign = 1.0f;
			int read;
			float rtn;

			read = ReadBits( 19);
			if ((read & 262144) > 0)  // the 19th bit is the sign
				sign = -1.0f;
			read &= ~262144; //  uint=4294705151
			rtn = sign * read / 16.0f;

			return rtn;
		}

		unsafe void ReadDir( float* dir)
		{
			int b;

			b = ReadByte();
			Common.ByteToDir(b, dir);
		}

		float ReadServerFrameTime_ver_15()
		{
			return ReadFloat();
		}


		float ReadServerFrameTime_ver_6(string serverInfo)
		{
			InfoString serverInfoParsed = new InfoString(serverInfo);
			return 1.0f/ serverInfoParsed["sv_fps"].Atof();
			/*int stringOffset = demo.cut.Cl.gameState.stringOffsets[CS_SERVERINFO]; // OpenMohaa has CS_SYSTEMINFO but sv_fps is in CS_SERVERINFO
			int maxLength = sizeof(demo.cut.Cl.gameState.stringData) - stringOffset;
			return 1.0f / atof(Info_ValueForKey(clCut->gameState.stringData + clCut->gameState.stringOffsets[CS_SERVERINFO], maxLength, "sv_fps")); // OpenMohaa has CS_SYSTEMINFO but sv_fps is in CS_SERVERINFO*/
		}

		float ReadServerFrameTime( ProtocolVersion protocol, bool forceConfigStringMethod, string serverInfo)
		{
			if ((protocol < ProtocolVersion.Protocol6 || protocol > ProtocolVersion.Protocol8) && !forceConfigStringMethod)
			{
				return ReadServerFrameTime_ver_15();
			}
			else
			{
				// smaller below version 15
				return ReadServerFrameTime_ver_6(serverInfo);
			}
		}

		unsafe EntityState GetNullEntityState()
		{
			EntityState nullState = new EntityState();
			nullState.Alpha = 1.0f;
			nullState.Scale = 1.0f;
			nullState.Parent = Common.MaxGEntities-1;
			nullState.ConstantLight = 0xffffff;
			nullState.RenderFx = 16;
			nullState.BoneTag[4] = -1;
			nullState.BoneTag[3] = -1;
			nullState.BoneTag[2] = -1;
			nullState.BoneTag[1] = -1;
			nullState.BoneTag[0] = -1;
			return nullState;
		}

		float UnpackAngle(int value, int bits)
		{
			int maxValue;
			float neg;
			float calc;

			neg = 1.0f;
			if (bits < 0)
			{
				bits = -1 - bits;
				maxValue = 1 << bits;
				if ((maxValue & value) != 0)
				{
					neg = -1.0f;
					value &= ~maxValue;
				}
			}

			switch (bits)
			{
				case 8:
					return neg * 360.0f / 256.0f;
				case 12:
					return neg * value * 360.0f / 4096.0f;
				case 16:
					calc = value * 360.0f / 65536.0f;
					break;
				default:
					calc = 360.0f / (1 << bits) * value;
					break;
			}
			return neg * calc;
		}

		float UnpackAnimTime(int packed)
		{
			return packed / 100.0f;
		}

		float UnpackAnimWeight(int result, int bits)
		{
			int max = (1 << bits) - 1;
			float tmp = (float)result / (float)max;

			if (tmp < 0.0f) return 0.0f;
			else if (tmp > 1.0f) return 1.0f;
			else return tmp;
		}

		float UnpackScale(int packed)
		{
			return packed / 100.0f;
		}

		float UnpackAlpha(int packed, int bits)
		{
			return (float)packed / (float)((1 << bits) - 1);
		}

		float UnpackCoord(int packed, int bits)
		{
			return (float)(packed - MAX_PACKED_COORD_HALF) / 4.0f;
		}

		float UnpackCoordExtra(int packed, int bits)
		{
			return (float)(packed - MAX_PACKED_COORD_EXTRA_HALF) / 16.0f;
		}

		void WriteDeltaCoord( int from, int to)
		{
			int delta = to - from;
			int deltaAbs = Math.Abs(delta);

			if (deltaAbs <= 0 || deltaAbs > 128)
			{
				// high delta, direct value
				WriteBits( 0, 1);
				WriteBits( to, 16);
			}
			else
			{
				WriteBits( 1, 1);

				if (delta < 0)
				{
					WriteBits( 1 + ((deltaAbs - 1) << 1), 8);
				}
				else
				{
					WriteBits( (deltaAbs - 1) << 1, 8);
				}
			}
		}

		int ReadDeltaCoord( int from)
		{
			int value;
			int delta;

			if (0 == ReadBits( 1))
			{
				// no delta
				return ReadBits( 16);
			}

			value = ReadBits( 8);
			delta = (value >> 1) + 1;

			if ((value & 1) != 0)
			{
				delta = -delta;
			}

			return delta + from;
		}

		void WriteDeltaCoordExtra( int from, int to)
		{
			int delta = to - from;
			int deltaAbs = Math.Abs(delta);

			if (deltaAbs <= 0 || deltaAbs > 512)
			{
				// high delta, direct value
				WriteBits( 0, 1);
				WriteBits( to, 18);
			}
			else
			{
				WriteBits( 1, 1);

				if (delta < 0)
				{
					WriteBits( 1 + ((deltaAbs - 1) << 1), 10);
				}
				else
				{
					WriteBits( (deltaAbs - 1) << 1, 10);
				}
			}
		}

		int ReadDeltaCoordExtra( int from)
		{
			int value;
			int delta;

			if (ReadBits( 1) == 0)
			{
				// no delta
				return ReadBits( 18);
			}

			value = ReadBits( 10);
			delta = (value >> 1) + 1;

			if ((value & 1) != 0)
			{
				delta = -delta;
			}

			return delta + from;
		}

		unsafe void ReadRegular_ver_15( int bits, void* toF)
		{
			if (bits == 0)
			{
				// float
				if (0 == ReadBits(1))
				{
					// float
					*(float*)toF = 0.0f;
				}
				else
				{
					if (0 == ReadBits(1))
					{
						// integral float
						*(float*)toF = ReadBits( -Message.FloatIntBits);
					}
					else
					{
						// full floating point value
						uint v = (uint)ReadBits( 32);
						if ((v & 1)>0)
						{
							*(int*)toF = (int)(((v + 0x7A000000) >> 1) | 0x80000000);
						}
						else
						{
							*(int*)toF = (int)((v + 0x7A000000) >> 1);
						}
					}
				}
			}
			else
			{
				if (ReadBits( 1) > 0)
				{
					*(int*)toF = ReadBits( bits);
				}
				else
				{
					*(int*)toF = 0;
				}
			}
		}

		unsafe void ReadRegularSimple_ver_15(int bits, void* toF)
		{
			ReadRegular_ver_15( bits, toF);
		}

		unsafe void WriteRegular_ver_15(int bits, void* toF)
		{
			float fullFloat;
			int trunc;

			if (bits == 0)
			{
				// float
				fullFloat = *(float*)toF;
				trunc = (int)fullFloat;

				if (fullFloat == 0.0f)
				{
					WriteBits(0, 1);
					//oldsize += Message.FloatIntBits;
				}
				else
				{
					WriteBits(1, 1);
					if (trunc == fullFloat && trunc >= -4096 && trunc < 4096)
					{
						// send as small integer
						WriteBits(0, 1);
						WriteBits(trunc, -Message.FloatIntBits);
					}
					else
					{
						int newvalue = *(int*)toF * 2 - 0x7A000000;
						if (*(int*)toF < 0)
						{
							newvalue |= 1;
						}
						WriteBits(1, 1);
						// send as full floating point value
						WriteBits(newvalue, 32);
					}
				}
			}
			else
			{
				if (0 == *(int*)toF)
				{
					WriteBits(0, 1);
				}
				else
				{
					WriteBits(1, 1);
					// integer
					WriteBits(*(int*)toF, bits);
				}
			}
		}

		unsafe void WriteRegularSimple_ver_15(int bits, void* toF)
		{
			WriteRegular_ver_15(bits, toF);
		}

		void WriteEntityNum_ver_15(short number)
		{
			// protocols version 15 and above adds 1 to the entity number
			WriteBits((number + 1) % Common.MaxGEntities, Common.GEntitynumBits);
		}

		ushort ReadEntityNum_ver_15()
		{
			return (ushort)((ReadBits(Common.GEntitynumBits) - 1) % Common.MaxGEntities);
		}

		unsafe void ReadRegular_ver_6(int bits, void* toF)
		{
			if (bits == 0)
			{
				if (0 == ReadBits(1))
				{
					// float
					*(float*)toF = 0.0f;
				}
				else
				{
					if (0 == ReadBits(1))
					{
						*(float*)toF = (int)ReadBits(Message.FloatIntBits) - Message.FloatIntBias;
					}
					else
					{
						// full floating point value
						*(float*)toF = ReadFloat();
					}
				}
			}
			else
			{
				if (0 < ReadBits(1))
				{
					*(int*)toF = ReadBits(bits);
				}
				else
				{
					*(int*)toF = 0;
				}
			}
		}

		unsafe void ReadRegularSimple_ver_6(int bits, void* toF)
		{
			if (bits == 0)
			{
				// float
				if (ReadBits(1) == 0)
				{
					// integral float
					int trunc = ReadBits(Message.FloatIntBits);
					// bias to allow equal parts positive and negative
					trunc -= Message.FloatIntBias;
					*(float*)toF = trunc;
				}
				else
				{
					// full floating point value
					*(int*)toF = ReadBits(32);
				}
			}
			else
			{
				// integer
				*(int*)toF = ReadBits(bits);
			}
		}

		unsafe void WriteRegular_ver_6(int bits, void* toF)
		{
			float fullFloat;
			int trunc;

			if (bits == 0)
			{
				// float
				fullFloat = *(float*)toF;
				trunc = (int)fullFloat;

				if (fullFloat == 0.0f)
				{
					WriteBits(0, 1);
					//oldsize += Message.FloatIntBits;
				}
				else
				{
					WriteBits(1, 1);
					if (trunc == fullFloat && trunc + Message.FloatIntBias >= 0 &&
						trunc + Message.FloatIntBias < (1 << Message.FloatIntBits))
					{
						// send as small integer
						WriteBits(0, 1);
						WriteBits(trunc + Message.FloatIntBias, -Message.FloatIntBits);
					}
					else
					{
						// send as full floating point value
						WriteBits(1, 1);
						WriteBits(*(int*)toF, 32);
					}
				}
			}
			else
			{
				if (0 == *(int*)toF)
				{
					WriteBits(0, 1);
				}
				else
				{
					WriteBits(1, 1);
					// integer
					WriteBits(*(int*)toF, bits);
				}
			}
		}

		unsafe void WriteRegularSimple_ver_6(int bits, void* toF)
		{
			float fullFloat;
			int trunc;

			if (bits == 0)
			{
				// float
				fullFloat = *(float*)toF;
				trunc = (int)fullFloat;

				if (trunc == fullFloat && trunc + Message.FloatIntBias >= 0 &&
					trunc + Message.FloatIntBias < (1 << Message.FloatIntBits))
				{
					// send as small integer
					WriteBits(0, 1);
					WriteBits(trunc + Message.FloatIntBias, Message.FloatIntBits);
				}
				else
				{
					// send as full floating point value
					WriteBits(1, 1);
					WriteBits(*(int*)toF, 32);
				}
			}
			else
			{
				// integer
				WriteBits(*(int*)toF, bits);
			}
		}

		void WriteEntityNum_ver_6(short number)
		{
			WriteBits(number % Common.MaxGEntities, Common.GEntitynumBits);
		}

		ushort ReadEntityNum_ver_6()
		{
			return (ushort)(ReadBits(Common.GEntitynumBits) % Common.MaxGEntities);
		}

		unsafe void ReadRegular(int bits, void* toF, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				ReadRegular_ver_15(bits, toF);
			}
			else
			{
				ReadRegular_ver_6(bits, toF);
			}
		}

		unsafe void ReadRegularSimple(int bits, void* toF, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				ReadRegularSimple_ver_15(bits, toF);
			}
			else
			{
				ReadRegularSimple_ver_6(bits, toF);
			}
		}

		unsafe void WriteRegular(int bits, void* toF, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WriteRegular_ver_15(bits, toF);
			}
			else
			{
				WriteRegular_ver_6(bits, toF);
			}
		}

		unsafe void WriteRegularSimple(int bits, void* toF, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WriteRegularSimple_ver_15(bits, toF);
			}
			else
			{
				WriteRegularSimple_ver_6(bits, toF);
			}
		}

		void WriteEntityNum(short number, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WriteEntityNum_ver_15(number);
			}
			else
			{
				WriteEntityNum_ver_6(number);
			}
		}

		ushort ReadEntityNum(ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				return ReadEntityNum_ver_15();
			}
			else
			{
				return ReadEntityNum_ver_6();
			}
		}


		int PackAngle(float angle, int bits)
		{
			int bit;
			float calc;

			bit = 0;
			if (bits < 0)
			{
				bits = ~bits;
				if (angle < 0.0)
				{
					angle = -angle;
					bit = 1 << bits;
				}
			}

			switch (bits)
			{
				case 8:
					calc = angle * 256.0f / 360.0f;
					return bit | ((int)calc & 0xFF);
				case 12:
					calc = angle * 4096.0f / 360.0f;
					return bit | ((int)calc & 0xFFF);
				case 16:
					calc = angle * 65536.0f / 360.0f;
					return bit | ((int)calc & 0xFFFF);
				default:
					calc = (1 << bits) * angle / 360.0f;
					return bit | ((int)calc & ((1 << bits) - 1));
			}
		}

		int PackAnimTime(float time, int bits)
		{
			int maxValue;
			int packed;

			maxValue = (1 << bits) - 1;
			packed = (int)(time * 100.0f);
			if (packed >= 0)
			{
				if (packed > maxValue)
				{
					packed = maxValue;
				}
			}
			else
			{
				packed = 0;
			}

			//timestats[packed]++;

			return packed;
		}

		int PackAnimWeight(float weight, int bits)
		{
			int maxValue;
			int packed;

			maxValue = (1 << bits) - 1;
			packed = (int)(maxValue * weight);
			if (packed >= 0)
			{
				if (packed > maxValue)
				{
					packed = maxValue;
				}
			}
			else
			{
				packed = 0;
			}

			//weightstats[packed]++;

			return packed;
		}

		int PackScale(float scale, int bits)
		{
			int maxValue;
			int packed;

			maxValue = (1 << bits) - 1;
			packed = (int)(scale * 100.0f);
			if (packed >= 0)
			{
				if (packed > maxValue)
				{
					packed = maxValue;
				}
			}
			else
			{
				packed = 0;
			}

			//scalestats[packed]++;

			return packed;
		}

		int PackAlpha(float alpha, int bits)
		{
			int maxValue;
			int packed;

			maxValue = (1 << bits) - 1;
			packed = (int)(maxValue * alpha);
			if (packed >= 0)
			{
				if (packed > maxValue)
				{
					packed = maxValue;
				}
			}
			else
			{
				packed = 0;
			}

			//alphastats[packed]++;

			return packed;
		}

		int PackCoord(float coord)
		{
			uint packed = (uint)Math.Round(coord * 4.0 + MAX_PACKED_COORD_HALF);
			//coordstats[packed]++;

			return (int)packed;
		}

		int PackCoordExtra(float coord)
		{
			uint packed = (uint)Math.Round(coord * 16.0 + MAX_PACKED_COORD_EXTRA_HALF);
			if (packed >= MAX_PACKED_COORD_EXTRA)
			{
				Debug.WriteLine("Illegal XYZ coordinates for an entity, information lost in transmission\n");
			}
			else
			{
				// This check wasn't added in >= 2.0
				// which means a player could crash a server when out of bounds
				//++coordextrastats[packed];
			}

			return (int)packed;
		}

		float ReadPackedAngle_ver_15(int bits)
		{
			int packed = ReadBits(Math.Abs(bits));
			return UnpackAngle(packed, bits);
		}

		float ReadPackedAnimTime_ver_15(int bits, float fromValue, float frameTime)
		{
			int packed;
			if (0 == ReadBits(1))
			{
				return fromValue + frameTime;
			}

			packed = ReadBits(bits);
			return UnpackAnimTime(packed);
		}

		float ReadPackedAnimWeight_ver_15(int bits)
		{
			int packed = ReadBits(bits);
			return UnpackAnimWeight(packed, bits);
		}

		float ReadPackedScale_ver_15(int bits)
		{
			int packed = ReadBits(bits);
			return UnpackScale(packed);
		}

		float ReadPackedAlpha_ver_15(int bits)
		{
			int packed = ReadBits(bits);
			return UnpackAlpha(packed, bits);
		}

		float ReadPackedCoord_ver_15(float fromValue, int bits)
		{
			int packedFrom = PackCoord(fromValue);
			int packedTo = ReadDeltaCoord(packedFrom);
			return UnpackCoord(packedTo, bits);
		}

		float ReadPackedCoordExtra_ver_15(float fromValue, int bits)
		{
			int packedFrom = PackCoordExtra(fromValue);
			int packedTo = ReadDeltaCoordExtra(packedFrom);
			return UnpackCoordExtra(packedTo, bits);
		}

		void WritePackedAngle_ver_15(float value, int bits)
		{
			int packed = PackAngle(value, bits);
			WriteBits(packed, Math.Abs(bits));
		}

		void WritePackedAnimTime_ver_15(float fromValue, float toValue, float frameTime, int bits)
		{
			int packed;

			if (Math.Abs(fromValue - toValue) < frameTime)
			{
				// below the frame time, don't send
				WriteBits(0, 1);
				return;
			}

			WriteBits(1, 1);
			packed = PackAnimTime(toValue, bits);
			WriteBits(packed, bits);
		}

		void WritePackedAnimWeight_ver_15(float value, int bits)
		{
			int packed = PackAnimWeight(value, bits);
			WriteBits(packed, bits);
		}

		void WritePackedScale_ver_15(float value, int bits)
		{
			int packed = PackScale(value, bits);
			WriteBits(packed, bits);
		}

		void WritePackedAlpha_ver_15(float value, int bits)
		{
			int packed = PackAlpha(value, bits);
			WriteBits(packed, bits);
		}

		void WritePackedCoord_ver_15(float fromValue, float toValue, int bits)
		{
			int packedFrom = PackCoord(fromValue);
			int packedTo = PackCoord(toValue);
			WriteDeltaCoord(packedFrom, packedTo);
		}

		void WritePackedCoordExtra_ver_15(float fromValue, float toValue, int bits)
		{
			int packedFrom = PackCoordExtra(fromValue);
			int packedTo = PackCoordExtra(toValue);
			WriteDeltaCoordExtra(packedFrom, packedTo);
		}

		unsafe bool DeltaNeeded_ver_15(void* fromField, void* toField, NetFieldType fieldType, int bits)
		{
			int packedFrom;
			int packedTo;
			int maxValue;

			if (*(int*)fromField == *(int*)toField)
			{
				return false;
			}

			switch (fieldType)
			{
				case NetFieldType.regular:
					if (0 == bits || bits == 32)
					{
						return true;
					}

					maxValue = (1 << Math.Abs(bits)) - 1;
					return (((*(int*)fromField ^ *(int*)toField) & maxValue) != 0);
				case NetFieldType.angle:
					packedFrom = PackAngle(*(float*)fromField, bits);
					packedTo = PackAngle(*(float*)toField, bits);
					return (packedFrom != packedTo);
				case NetFieldType.animTime:
					packedFrom = PackAnimTime(*(float*)fromField, bits);
					packedTo = PackAnimTime(*(float*)toField, bits);
					return (packedFrom != packedTo);
				case NetFieldType.animWeight:
					packedFrom = PackAnimWeight(*(float*)fromField, bits);
					packedTo = PackAnimWeight(*(float*)toField, bits);
					return (packedFrom != packedTo);
				case NetFieldType.scale:
					packedFrom = PackScale(*(float*)fromField, bits);
					packedTo = PackScale(*(float*)toField, bits);
					return (packedFrom != packedTo);
				case NetFieldType.alpha:
					packedFrom = PackAlpha(*(float*)fromField, bits);
					packedTo = PackAlpha(*(float*)toField, bits);
					return (packedFrom != packedTo);
				case NetFieldType.coord:
					packedFrom = PackCoord(*(float*)fromField);
					packedTo = PackCoord(*(float*)toField);
					return (packedFrom != packedTo);
				case NetFieldType.coordExtra:
					packedFrom = PackCoordExtra(*(float*)fromField);
					packedTo = PackCoordExtra(*(float*)toField);
					return (packedFrom != packedTo);
				case NetFieldType.velocity:
					return true;
				case NetFieldType.simple:
					return true;
				default:
					return true;
			}
		}

		float ReadPackedAngle_ver_6(int bits)
		{
			int result;
			float tmp = 1.0f;
			if (bits < 0)
			{
				if (0 < ReadBits(1))
					tmp = -1.0f;
				bits = ~bits;
			}

			result = ReadBits(bits);
			switch (bits)
			{
				case 8:
					return tmp * 360.0f / 256.0f;
				case 12:
					return tmp * result * 360.0f / 4096.0f;
				case 16:
					return tmp * result * 360.0f / 65536.0f;
				default:
					return tmp * 360.0f / (1 << bits) * result;
			}
		}

		float ReadPackedAnimTime_ver_6(int bits, float fromValue, float frameTime)
		{
			return ReadBits(15) / 100.0f;
		}

		float ReadPackedAnimWeight_ver_6(int bits)
		{
			float tmp = ReadBits(8) / 255.0f;
			if (tmp < 0.0f)
				return 0.0f;
			else if (tmp > 1.0f)
				return 1.0f;
			else
				return tmp;
		}

		float ReadPackedScale_ver_6(int bits)
		{
			return ReadBits(10) / 100.0f;
		}

		float ReadPackedAlpha_ver_6(int bits)
		{
			float tmp = ReadBits(8) / 255.0f;
			if (tmp < 0.0f)
				return 0.0f;
			else if (tmp > 1.0f)
				return 1.0f;
			else
				return tmp;
		}

		float ReadPackedCoord_ver_6(float fromValue, int bits)
		{
			float tmp = 1.0f;
			int value = ReadBits(19);
			if ((value & 262144)>0) // test for 19th bit
				tmp = -1.0f;
			value &= ~262144;   // remove that bit
			return tmp * value / 16.0f;
		}

		float ReadPackedCoordExtra_ver_6(float fromValue, int bits)
		{
			int packedFrom = PackCoordExtra(fromValue);
			int packedTo = ReadDeltaCoordExtra(packedFrom);
			return UnpackCoordExtra(packedTo, bits);
		}


		void WritePackedAngle_ver_6(float value, int bits)
		{
			// angles, what a mess! it wouldnt surprise me if something goes wrong here ;)

			float tmp = value;

			if (bits < 0)
			{
				if (tmp < 0.0f)
				{
					WriteBits(1, 1);
					tmp = -tmp;
				}
				else
				{
					WriteBits(0, 1);
				}

				bits = ~bits;
			}
			else
			{
				bits = bits;
			}

			if (bits == 12)
			{
				tmp = tmp * 4096.0f / 360.0f;
				WriteBits(((int)tmp) & 4095, 12);
			}
			else if (bits == 8)
			{
				tmp = tmp * 256.0f / 360.0f;
				WriteBits(((int)tmp) & 255, 8);
			}
			else if (bits == 16)
			{
				tmp = tmp * 65536.0f / 360.0f;
				WriteBits(((int)tmp) & 65535, 16);
			}
			else
			{
				tmp = tmp * (1 << (byte)bits) / 360.0f;
				WriteBits(((int)tmp) & ((1 << (byte)bits) - 1), bits);
			}
		}

		void WritePackedAnimTime_ver_6(float fromValue, float toValue, float frameTime, int bits)
		{
			int packed = (int)(toValue * 100.0f);
			if (packed < 0)
			{
				packed = 0;
			}
			else if (packed >= (1 << 15))
			{
				packed = (1 << 15);
			}

			WriteBits(packed, 15);
		}

		void WritePackedAnimWeight_ver_6(float value, int bits)
		{
			int packed = (int)((value * 255.0f) + 0.5f);

			if (packed < 0)
			{
				packed = 0;
			}
			else if (packed > 255)
			{
				packed = 255;
			}

			WriteBits(packed, 8);
		}

		void WritePackedScale_ver_6(float value, int bits)
		{
			int packed = (int)(value * 100.0f);
			if (packed < 0)
			{
				packed = 0;
			}
			else if (packed > 1023)
			{
				packed = 1023;
			}

			WriteBits(packed, 10);
		}

		void WritePackedAlpha_ver_6(float value, int bits)
		{
			int packed = (int)((value * 255.0f) + 0.5f);

			if (packed < 0)
			{
				packed = 0;
			}
			else if (packed > 255)
			{
				packed = 255;
			}

			WriteBits(packed, 8);
		}

		void WritePackedCoord_ver_6(float fromValue, float toValue, int bits)
		{
			int packed = (int)(toValue * 16.0f);

			if (toValue < 0)
			{
				packed = ((-packed) & 262143) | 262144;
			}
			else
			{
				packed = packed & 262143;
			}

			WriteBits(packed, 19);
		}

		void WritePackedCoordExtra_ver_6(float fromValue, float toValue, int bits)
		{
			// Don't implement
		}

		unsafe bool DeltaNeeded_ver_6(void* fromField, void* toField, int fieldType, int bits)
		{
			// Unoptimized in base game
			// Doesn't compare packed values
			return (*(int*)fromField != *(int*)toField);
		}

		float ReadPackedAngle(int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				return ReadPackedAngle_ver_15(bits);
			}
			else
			{
				return ReadPackedAngle_ver_6(bits);
			}
		}

		float ReadPackedAnimTime(int bits, float fromValue, float frameTime, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				return ReadPackedAnimTime_ver_15(bits, fromValue, frameTime);
			}
			else
			{
				return ReadPackedAnimTime_ver_6(bits, fromValue, frameTime);
			}
		}

		float ReadPackedAnimWeight(int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				return ReadPackedAnimWeight_ver_15(bits);
			}
			else
			{
				return ReadPackedAnimWeight_ver_6(bits);
			}
		}

		float ReadPackedScale(int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				return ReadPackedScale_ver_15(bits);
			}
			else
			{
				return ReadPackedScale_ver_6(bits);
			}
		}

		float ReadPackedAlpha(int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				return ReadPackedAlpha_ver_15(bits);
			}
			else
			{
				return ReadPackedAlpha_ver_6(bits);
			}
		}

		float ReadPackedCoord(float fromValue, int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				return ReadPackedCoord_ver_15(fromValue, bits);
			}
			else
			{
				return ReadPackedCoord_ver_6(fromValue, bits);
			}
		}

		float ReadPackedCoordExtra(float fromValue, int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				return ReadPackedCoordExtra_ver_15(fromValue, bits);
			}
			else
			{
				return ReadPackedCoordExtra_ver_6(fromValue, bits);
			}
		}


		void WritePackedAngle(float value, int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WritePackedAngle_ver_15(value, bits);
			}
			else
			{
				WritePackedAngle_ver_6(value, bits);
			}
		}

		void WritePackedAnimTime(float fromValue, float toValue, float frameTime, int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WritePackedAnimTime_ver_15(fromValue, toValue, frameTime, bits);
			}
			else
			{
				WritePackedAnimTime_ver_6(fromValue, toValue, frameTime, bits);
			}
		}

		void WritePackedAnimWeight(float value, int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WritePackedAnimWeight_ver_15(value, bits);
			}
			else
			{
				WritePackedAnimWeight_ver_6(value, bits);
			}
		}

		void WritePackedScale(float value, int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WritePackedScale_ver_15(value, bits);
			}
			else
			{
				WritePackedScale_ver_6(value, bits);
			}
		}

		void WritePackedAlpha(float value, int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WritePackedAlpha_ver_15(value, bits);
			}
			else
			{
				WritePackedAlpha_ver_6(value, bits);
			}
		}

		void WritePackedCoord(float fromValue, float toValue, int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WritePackedCoord_ver_15(fromValue, toValue, bits);
			}
			else
			{
				WritePackedCoord_ver_6(fromValue, toValue, bits);
			}
		}

		void WritePackedCoordExtra(float fromValue, float toValue, int bits, ProtocolVersion protocol)
		{
			if (protocol > ProtocolVersion.Protocol8)
			{
				WritePackedCoordExtra_ver_15(fromValue, toValue, bits);
			}
			else
			{
				WritePackedCoordExtra_ver_6(fromValue, toValue, bits);
			}
		}

		unsafe bool DeltaNeeded(void* fromField, void* toField, int fieldType, int bits)
		{
			// Unoptimized in base game
			// Doesn't compare packed values
			return (*(int*)fromField != *(int*)toField);
		}

		float ReadPackedVelocity(int bits)
		{
			float tmp = 1.0f;
			int value = ReadBits(17);
			if ((value & 65536)>0) // test for 17th bit
				tmp = -1.0f;
			value &= ~65536; // remove that bit
			return tmp * value / 8.0f;
		}

		int ReadPackedSimple(int fromValue, int bits)
		{
			if (0 == ReadBits(1))
			{
				return fromValue;
			}

			return ReadBits(bits);
		}

		void WritePackedVelocity(float value, int bits)
		{
			int packed = (int)(uint)(value * 8.0f); // TODO Not sure about this?
			if (value < 0)
			{
				packed = ((-packed) & 65535) | 65536;
			}
			else
			{
				packed = packed & 65535;
			}

			WriteBits(packed, 17);
		}

		void WritePackedSimple(int value, int bits)
		{
			byte packed = (byte)value;
			if (0 == packed)
			{
				WriteBits(0, 1);
			}

			WriteBits(1, 1);
			WriteBits(packed, bits);
		}

		/*
		==================
		WriteSounds

		write the sounds to the snapshot...
		1:1 translated from assembly code
		==================
		*/
		unsafe void WriteSounds(ServerSound* sounds, int snapshot_number_of_sounds)
		{

			int i;

			if (0 == snapshot_number_of_sounds)
			{
				WriteBits(0, 1);
			}
			else
			{
				WriteBits(1, 1);
				WriteBits(snapshot_number_of_sounds, 7);

				for (i = 0; i < snapshot_number_of_sounds; i++)
				{
					if (!sounds[i].stop_flag)
					{
						WriteBits(0, 1);
						WriteBits(sounds[i].streamed, 1);

						if (sounds[i].origin[0] == 0.0f && sounds[i].origin[1] == 0.0f && sounds[i].origin[2] == 0.0f)
							WriteBits(0, 1);
						else
						{
							WriteBits(1, 1);
							WriteFloat(sounds[i].origin[0]);
							WriteFloat(sounds[i].origin[1]);
							WriteFloat(sounds[i].origin[2]);
						}
						WriteBits(sounds[i].entity_number, 11);
						WriteBits(sounds[i].channel, 7);
						WriteBits(sounds[i].sound_index, 9);

						if (sounds[i].volume != -1.0f)
						{
							WriteBits(1, 1);
							WriteFloat(sounds[i].volume);
						}
						else
						{
							WriteBits(0, 1);
						}

						if (sounds[i].min_dist != -1.0f)
						{
							WriteBits(1, 1);
							WriteFloat(sounds[i].min_dist);
						}
						else
						{
							WriteBits(0, 1);
						}

						if (sounds[i].pitch != -1.0f)
						{
							WriteBits(1, 1);
							WriteFloat(sounds[i].pitch);
						}
						else
						{
							WriteBits(0, 1);
						}

						WriteFloat(sounds[i].maxDist);
					}
					else
					{
						WriteBits(1, 1);
						WriteBits(sounds[i].entity_number, 10);
						WriteBits(sounds[i].channel, 7);
					}
				}
			}
		}

		/*
		==================
		ReadSounds

		read the sounds from the snapshot...
		1:1 translated from assembly code
		==================
		*/
		unsafe void ReadSounds(ServerSound* sounds, int* snapshot_number_of_sounds)
		{

			int fubar;
			int i;

			if (0 < ReadBits(1))
			{
				fubar = ReadBits(7);

				if (fubar <= 64)
				{
					*snapshot_number_of_sounds = fubar;
					for (i = 0; i < fubar; i++)
					{
						if (ReadBits(1) == 1)
						{
							sounds[i].entity_number = ReadBits(10);
							sounds[i].channel = ReadBits(7);
							sounds[i].stop_flag = true; // su44 was here
						}
						else
						{
							sounds[i].stop_flag = false;
							sounds[i].streamed = (QuakeBoolean)ReadBits(1);
							if (ReadBits(1) == 1)
							{
								sounds[i].origin[0] = ReadFloat();
								sounds[i].origin[1] = ReadFloat();
								sounds[i].origin[2] = ReadFloat();
							}
							else
							{
								sounds[i].origin[0] = 0;
								sounds[i].origin[1] = 0;
								sounds[i].origin[2] = 0;
							}
							sounds[i].entity_number = ReadBits(11);
							sounds[i].channel = ReadBits(7);
							sounds[i].sound_index = (short)ReadBits(9);

							if (ReadBits(1) == 1)
							{
								sounds[i].volume = ReadFloat();
							}
							else
							{
								sounds[i].volume = -1.0f;
							}

							if (ReadBits(1) == 1)
							{
								sounds[i].min_dist = ReadFloat();
							}
							else
							{
								sounds[i].min_dist = -1.0f;
							}

							if (ReadBits(1) == 1)
							{
								sounds[i].pitch = ReadFloat();
							}
							else
							{
								sounds[i].pitch = 1.0f; // su44 was here
							}

							sounds[i].maxDist = ReadFloat();
						}
					}

				}
			}
		}


		//
		// MOHAA functions end
		//
		//
	}
}
