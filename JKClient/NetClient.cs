using System;
using System.Threading;
using System.Threading.Tasks;

namespace JKClient {

	public delegate void InternalTaskStartedEventHandler(object sender, in Task task, string description);
	public abstract class NetClient : IDisposable {

		private protected readonly NetSystem net;
		private readonly byte []packetReceived;
		private CancellationTokenSource cts;
		public bool Started { get; private set; }
		private readonly protected INetHandler NetHandler;
		public int Protocol => this.NetHandler.Protocol;

		public event InternalTaskStartedEventHandler InternalTaskStarted;
		internal NetClient(INetHandler netHandler) {
			if (netHandler == null) {
				throw new JKClientException(new ArgumentNullException(nameof(netHandler)));
			}
			this.net = new NetSystem(netHandler.DefaultPort);
			this.NetHandler = netHandler;
			this.packetReceived = new byte[this.NetHandler.MaxMessageLength];
		}

		protected void OnInternalTaskStarted(Task task, string description)
        {
			InternalTaskStarted?.Invoke(this, task, description);
        }

		public void Start(Func<JKClientException, Task> exceptionCallback) {
			if (this.Started) {
				return;
//				throw new JKClientException("NetClient is already started");
			}
			this.Started = true;
			this.OnStart();
			this.cts = new CancellationTokenSource();

			Task backgroundTask = Task.Factory.StartNew(this.Run, this.cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap().ContinueWith((t) => {

					this.Stop(true);
					exceptionCallback?.Invoke(new JKClientException(t.Exception));
			}, TaskContinuationOptions.OnlyOnFaulted); // Don't use OnlyOnFaulted. It's buggy and won't catch exceptions thrown inside event handlers.

			this.OnInternalTaskStarted(backgroundTask,$"{this.GetType().ToString()} Loop");
		}
		public void Stop(bool afterFailure = false) {
			if (!this.Started) {
				return;
//				throw new JKClientException("Cannot stop NetClient when it's not started");
			}
			this.Started = false;
			this.OnStop(afterFailure);
			if (this.cts != null) {
				this.cts.Cancel();
				this.cts = null;
			}
		}
		private protected void GetPacket() {
			var netmsg = new Message(this.packetReceived, sizeof(byte)*this.NetHandler.MaxMessageLength);
			NetAddress address = null;
			while (this.net.GetPacket(ref address, netmsg)) {
				if ((uint)netmsg.CurSize <= netmsg.MaxSize) {
					this.PacketEvent(address, netmsg);
				}
				Common.MemSet(netmsg.Data, 0, sizeof(byte)*netmsg.MaxSize);
			}
		}
		internal void OutOfBandPrint(in NetAddress address, in string data) {

			bool isMOH = Common.ProtocolIsMOH((ProtocolVersion)this.Protocol);

			byte []msg = new byte[this.NetHandler.MaxMessageLength];
			msg[0] = unchecked((byte)-1);
			msg[1] = unchecked((byte)-1);
			msg[2] = unchecked((byte)-1);
			msg[3] = unchecked((byte)-1);
            if (isMOH)
            {
				msg[4] = unchecked((byte)2); // Client->Server. MOH thing. Server->Client is 1.
			}
			byte []dataMsg = Common.Encoding.GetBytes(data);
			dataMsg.CopyTo(msg, isMOH ? 5 : 4);
			this.net.SendPacket(dataMsg.Length+(isMOH ? 5: 4), msg, address);
		}
		internal void OutOfBandData(in NetAddress address, in string data, in int length)
		{
			bool isMOH = Common.ProtocolIsMOH((ProtocolVersion)this.Protocol);

			byte []msg = new byte[this.NetHandler.MaxMessageLength*2];
			msg[0] = 0xff;
			msg[1] = 0xff;
			msg[2] = 0xff;
			msg[3] = 0xff; 
			if (isMOH)
			{
				msg[4] = unchecked((byte)2); // Client->Server. MOH thing. Server->Client is 1.
			}
			byte []dataMsg = Common.Encoding.GetBytes(data);
			dataMsg.CopyTo(msg, isMOH ? 5 : 4);
			var mbuf = new Message(msg, msg.Length) {
				CurSize = length+ (isMOH ? 5 : 4)
			};
			Huffman.Compress(mbuf, isMOH ? 13 : 12);
			this.net.SendPacket(mbuf.CurSize, mbuf.Data, address);
		}
		private protected abstract void PacketEvent(in NetAddress address, in Message msg);
		private protected abstract Task Run();
		private protected virtual void OnStart() {}
		private protected virtual void OnStop(bool afterFailure) {}
		public void Dispose() {
			this.Stop();
			this.net?.Dispose();
		}
	}
}
