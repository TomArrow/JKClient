namespace JKClient {
	public enum ConnectionStatus {
		Disconnected,
		Authorizing, // MOH
		Connecting,
		Challenging,
		Connected,
		Primed,
		Active
	}
}