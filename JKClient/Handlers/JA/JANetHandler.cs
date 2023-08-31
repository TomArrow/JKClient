namespace JKClient {
	public abstract class JANetHandler : NetHandler {
		public override int MaxMessageLength => 49152;
		public override int MaxGameStateChars => 16000;
		public JANetHandler(ProtocolVersion protocol) : base((int)protocol) {}
	}
}
