namespace JKClient {
	public abstract class Q3NetHandler : NetHandler {
		public override int MaxMessageLength => 16384;
		public override int MaxGameStateChars => 16000;
		public Q3NetHandler(ProtocolVersion protocol) : base((int)protocol) {}
	}
}
