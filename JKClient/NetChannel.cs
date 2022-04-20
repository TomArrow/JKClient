using System;
using System.Collections.Generic;

namespace JKClient {

	internal class FragmentAssemblyBuffer
	{
		public const double FragmentBuffersTimeout = 10;

		public byte[] data;//[MAX_MSGLEN]; // actual data
		public bool[] fragmentsReceived;//[MAX_MSGLEN / FRAGMENT_SIZE + 1]; // array indicating if a particular fragment has been received
		public int lastFragment; // index of the last fragment. 0 means we don't know yet.
		public int totalLength; // length of the entire message
		public DateTime time; // when was this fragment buffer last accessed? we want to clean up old unfinished fragment buffers.
		public FragmentAssemblyBuffer(int maxMessageLength)
		{
			data = new byte[maxMessageLength];
			fragmentsReceived = new bool[maxMessageLength / NetChannel.FragmentSize + 1];
		}
	}

	internal sealed class NetChannel {
		private const int MaxPacketLen = 1400;
		internal const int FragmentSize = NetChannel.MaxPacketLen - 100;
		private const int FragmentBit = 1<<31;
		private readonly NetSystem net;
		private readonly int qport;

		private SortedDictionary<int, FragmentAssemblyBuffer> fragmentBuffers = new SortedDictionary<int, FragmentAssemblyBuffer>(); // New arbitrary order fragment assembly ported from my eternaljk2mv fork - TA

		//private readonly byte []fragmentBuffer;
		private readonly byte []unsentBuffer;
		private readonly int maxMessageLength;
		private int dropped = 0;
		private int incomingSequence = 0;
		//private int fragmentSequence = 0;
		//private int fragmentLength = 0;
		private int unsentFragmentStart = 0;
		private int unsentLength = 0;
		public int OutgoingSequence { get; private set; } = 1;
		public bool UnsentFragments { get; private set; } = false;
		public NetAddress Address { get; private set; }
		public NetChannel(NetSystem net, NetAddress address, int qport, int maxMessageLength) {
			this.net = net;
			this.Address = address;
			this.qport = qport;
			this.maxMessageLength = maxMessageLength;
			//this.fragmentBuffer = new byte[this.maxMessageLength];
			this.unsentBuffer = new byte[this.maxMessageLength];
		}
		public unsafe bool Process(Message msg, ref int sequenceNumber, ref bool validButOutOfOrder) {
			msg.BeginReading(true);
			int sequence = msg.ReadLong();
			
			validButOutOfOrder = false;

			msg.SaveState();
			bool fragmented;
			if ((sequence & NetChannel.FragmentBit) != 0) {
				sequence &= ~NetChannel.FragmentBit;
				fragmented = true;
			} else {
				fragmented = false;
			}

			sequenceNumber = sequence;

			int fragmentStart, fragmentLength;
			if (fragmented) {
				fragmentStart = (ushort)msg.ReadShort();
				fragmentLength = (ushort)msg.ReadShort();
			} else {
				fragmentStart = 0;
				fragmentLength = 0;
			}
			bool isOutOfOrder = false;
			if (sequence <= this.incomingSequence) {
				//return false;
				isOutOfOrder = true;// We still want to assemble fragmented messages, even if out of order
			}
			this.dropped = sequence - (this.incomingSequence+1);
			if (fragmented) {

				// changes here by TA: Arbitrary order fragment assembly. And even possibility to assemble multiple fragment buffers at the same time, in case they all 
				// the fragments from different messages come mixed at varying out of order times.

				// First, some maintenance. Remove too old fragment buffers.
				List<int> toErase = new List<int>();
				foreach(KeyValuePair<int,FragmentAssemblyBuffer> fab in fragmentBuffers)
                {
					//if (fab.Value.time + FragmentAssemblyBuffer.FragmentBuffersTimeout < Com_RealTime(NULL))
					if (  (DateTime.Now - fab.Value.time).TotalSeconds > FragmentAssemblyBuffer.FragmentBuffersTimeout)
					{
						toErase.Add(fab.Key);
					}
				}
				foreach(int key in toErase)
                {
					fragmentBuffers.Remove(key);
                }

                if (!fragmentBuffers.ContainsKey(sequence))
				{
					fragmentBuffers.Add(sequence, new FragmentAssemblyBuffer(this.maxMessageLength));
				}
				bool isNewBuffer = !fragmentBuffers.ContainsKey(sequence);

				FragmentAssemblyBuffer thisFragmentBuffer = fragmentBuffers[sequence]; // This will either find or insert the element.

				/*if (sequence != this.fragmentSequence) {
					this.fragmentSequence = sequence;
					this.fragmentLength = 0;
				}
				if (fragmentStart != this.fragmentLength) {
					return false;
				}*/

				// old sanity check for fragment size adapted to new code
				if (fragmentLength < 0 || (msg.ReadCount + fragmentLength) > msg.CurSize ||
					(fragmentStart + fragmentLength) > sizeof(byte)*this.maxMessageLength) {
					return false;
				}

				// Additional sanity check now since we need to track the individual pieces precisely
				if (fragmentStart % NetChannel.FragmentSize > 0)
				{ // Not a correct multiple of fragment size. Should never happen.
					return false;
				}

				// copy to buffer 
				int currentFragment = fragmentStart / NetChannel.FragmentSize;
				bool isLastFragment = fragmentLength != NetChannel.FragmentSize;
				Array.Copy(msg.Data, msg.ReadCount, thisFragmentBuffer.data,fragmentStart, fragmentLength);
				//Com_Memcpy(thisFragmentBuffer->data + fragmentStart,msg->data + msg->readcount, fragmentLength);
				thisFragmentBuffer.fragmentsReceived[currentFragment] = true;
				thisFragmentBuffer.time = DateTime.Now;
				if (isLastFragment)
				{
					thisFragmentBuffer.lastFragment = currentFragment;
					thisFragmentBuffer.totalLength = fragmentStart + fragmentLength;
				}

				// Any fragments missing?
				if (thisFragmentBuffer.lastFragment == 0)
				{
					return false; // last fragment is not received, no need to even check
				}
				else
				{
					for (int i = thisFragmentBuffer.lastFragment; i >= 0; i--)
					{
						if (!thisFragmentBuffer.fragmentsReceived[i])
						{
							return false; // If any fragment is missing, there's no point in continuing here.
						}
					}
				}

				/*Array.Copy(msg.Data, msg.ReadCount, this.fragmentBuffer, this.fragmentLength, fragmentLength);
				this.fragmentLength += fragmentLength;
				if (fragmentLength == NetChannel.FragmentSize) {
					return false;
				}*/
				if (thisFragmentBuffer.totalLength + 4 > msg.MaxSize) {
					return false;
				}
				fixed (byte* b = msg.Data) {
					*(int*)b = sequence;
				}

				//Com_Memcpy(msg->data + 4, thisFragmentBuffer->data, thisFragmentBuffer->totalLength);
				Array.Copy(thisFragmentBuffer.data,0,msg.Data,4,thisFragmentBuffer.totalLength);
				msg.CurSize = thisFragmentBuffer.totalLength + 4;
				/*Array.Copy(this.fragmentBuffer, 0, msg.Data, 4, this.fragmentLength);
				msg.CurSize = this.fragmentLength + 4;
				this.fragmentLength = 0;*/
				msg.RestoreState();

				thisFragmentBuffer = null;
				fragmentBuffers.Remove(sequence); // Now that the message is fully assembled, we can discard the fragment buffer

				if (!isOutOfOrder)
				{
					this.incomingSequence = sequence;   // lets not accept any more with this sequence number -gil
					return true;
				}
				else
				{
					validButOutOfOrder = true;
					return false;
				}
			}

			if (!isOutOfOrder)
			{
				this.incomingSequence = sequence;
				return true;
			}
			else
			{
				validButOutOfOrder = true;
				return false;
			}
		}
		public void Transmit(int length, byte []data) {
			if (length > this.maxMessageLength) {
				throw new JKClientException($"Transmit: length = {length}");
			}
			this.unsentFragmentStart = 0;
			if (length >= NetChannel.FragmentSize) {
				this.UnsentFragments = true;
				this.unsentLength = length;
				Array.Copy(data, 0, this.unsentBuffer, 0, length);
				this.TransmitNextFragment();
				return;
			}
			byte []buf = new byte[NetChannel.MaxPacketLen];
			var msg = new Message(buf, sizeof(byte)*NetChannel.MaxPacketLen, true);
			msg.WriteLong(this.OutgoingSequence);
			this.OutgoingSequence++;
			msg.WriteShort(this.qport);
			msg.WriteData(data, length);
			this.net.SendPacket(msg.CurSize, msg.Data, this.Address);
		}
		public unsafe void TransmitNextFragment() {
			byte []buf = new byte[NetChannel.MaxPacketLen];
			var msg = new Message(buf, sizeof(byte)*NetChannel.MaxPacketLen, true);
			msg.WriteLong(this.OutgoingSequence | NetChannel.FragmentBit);
			msg.WriteShort(this.qport);
			int fragmentLength = NetChannel.FragmentSize;
			if (this.unsentFragmentStart + fragmentLength > this.unsentLength) {
				fragmentLength = this.unsentLength - this.unsentFragmentStart;
			}
			msg.WriteShort(this.unsentFragmentStart);
			msg.WriteShort(fragmentLength);
			fixed (byte *b = this.unsentBuffer) {
				msg.WriteData(b + this.unsentFragmentStart, fragmentLength);
			}
			this.net.SendPacket(msg.CurSize, msg.Data, this.Address);
			this.unsentFragmentStart += fragmentLength;
			if (this.unsentFragmentStart == this.unsentLength && fragmentLength != NetChannel.FragmentSize) {
				this.OutgoingSequence++;
				this.UnsentFragments = false;
			}
		}
	}
}
