namespace JKClient {
	public struct ClientInfo {
		public int ClientNum { get; internal set; }
		public bool InfoValid { get; internal set; }
		public string Name { get; internal set; }
		public Team Team { get; internal set; }
		internal void Clear() {
			this.ClientNum = 0;
			this.InfoValid = false;
			this.Name = null;
			this.Team = Team.Free;
		}
	}
}
