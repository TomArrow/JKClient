﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace JKClient {
	public abstract class NetClient : IDisposable {
		private protected readonly NetSystem net;
		private readonly byte []packetReceived;
		private CancellationTokenSource cts;
		public bool Started { get; private set; }
		private readonly protected INetHandler NetHandler;
		private protected ProtocolVersion Protocol => this.NetHandler.Protocol;
		internal NetClient(INetHandler netHandler) {
			if (netHandler == null) {
				throw new JKClientException(new ArgumentNullException(nameof(netHandler)));
			}
			this.net = new NetSystem();
			this.NetHandler = netHandler;
			this.packetReceived = new byte[this.NetHandler.MaxMessageLength];
		}

		public void Start(Func<JKClientException, Task> exceptionCallback) {
			if (this.Started) {
				return;
//				throw new JKClientException("NetClient is already started");
			}
			this.Started = true;
			this.OnStart();
			this.cts = new CancellationTokenSource();

			Task.Factory.StartNew(this.Run, this.cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap().ContinueWith((t) => {
					this.Stop();
					exceptionCallback?.Invoke(new JKClientException(t.Exception));
			}, TaskContinuationOptions.OnlyOnFaulted); // Don't use OnlyOnFaulted. It's buggy and won't catch exceptions thrown inside event handlers.
		}
		public void Stop() {
			if (!this.Started) {
				return;
//				throw new JKClientException("Cannot stop NetClient when it's not started");
			}
			this.Started = false;
			this.OnStop();
			if (this.cts != null) {
				this.cts.Cancel();
				this.cts = null;
			}
		}
		private protected void GetPacket() {
			var netmsg = new Message(packetReceived, sizeof(byte)*this.NetHandler.MaxMessageLength);
			NetAddress address = null;
			while (this.net.GetPacket(ref address, netmsg)) {
				if ((uint)netmsg.CurSize <= netmsg.MaxSize) {
					this.PacketEvent(address, netmsg);
				}
				Common.MemSet(netmsg.Data, 0, sizeof(byte)*netmsg.MaxSize);
			}
		}
		internal void OutOfBandPrint(NetAddress address, string data) {
			byte []msg = new byte[this.NetHandler.MaxMessageLength];
			msg[0] = unchecked((byte)-1);
			msg[1] = unchecked((byte)-1);
			msg[2] = unchecked((byte)-1);
			msg[3] = unchecked((byte)-1);
			byte []dataMsg = Common.Encoding.GetBytes(data);
			dataMsg.CopyTo(msg, 4);
			this.net.SendPacket(dataMsg.Length+4, msg, address);
		}
		internal void OutOfBandData(NetAddress address, string data, int length) {
			byte []msg = new byte[this.NetHandler.MaxMessageLength*2];
			msg[0] = 0xff;
			msg[1] = 0xff;
			msg[2] = 0xff;
			msg[3] = 0xff;
			byte []dataMsg = Common.Encoding.GetBytes(data);
			dataMsg.CopyTo(msg, 4);
			var mbuf = new Message(msg, msg.Length) {
				CurSize = length+4
			};
			Huffman.Compress(mbuf, 12);
			this.net.SendPacket(mbuf.CurSize, mbuf.Data, address);
		}
		private protected abstract void PacketEvent(NetAddress address, Message msg);
		private protected abstract Task Run();
		private protected virtual void OnStart() {}
		private protected virtual void OnStop() {}
		public void Dispose() {
			this.net?.Dispose();
		}
	}
}
