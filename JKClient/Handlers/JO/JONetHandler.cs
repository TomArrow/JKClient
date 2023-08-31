namespace JKClient {
	public abstract class JONetHandler : NetHandler {
		public override int MaxMessageLength => 16384;
		public override int MaxGameStateChars => 16000;
		public JONetHandler(ProtocolVersion protocol) : base((int)protocol) {}
	}
}
