namespace JKClient {
	public interface INetHandler {
		int Protocol { get; }
		int MaxMessageLength { get; }
		int MaxGameStateChars { get; }
	}
	public abstract class NetHandler : INetHandler {
		public virtual int Protocol { get; protected set; }
		public abstract int MaxMessageLength { get; }
		public abstract int MaxGameStateChars { get; }
		public NetHandler(int protocol) {
			this.Protocol = protocol;
		}
	}
}
