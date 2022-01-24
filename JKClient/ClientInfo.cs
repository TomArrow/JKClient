namespace JKClient {
	public struct ClientInfo {
		public int ClientNum { get; set; }
		public bool InfoValid { get; set; }
		public string Name { get; set; }
		internal Team Team;
		internal void Clear() {
			this.ClientNum = 0;
			this.InfoValid = false;
			this.Name = null;
			this.Team = Team.Free;
		}
	}
}
