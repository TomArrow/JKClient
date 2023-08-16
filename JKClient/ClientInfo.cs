namespace JKClient {
	public struct ClientInfo {
		public int ClientNum { get; internal set; }
		public bool InfoValid { get; internal set; }
		public string Name { get; internal set; }
		public string Model { get; internal set; }
		public string Color1 { get; internal set; }
		public string Color2 { get; internal set; }
		public string GRedTeam { get; internal set; }
		public string GBlueTeam { get; internal set; }
		public Team Team { get; internal set; }
		public float BotSkill { get; internal set; }
		internal void Clear() {
			this.ClientNum = 0;
			this.InfoValid = false;
			this.Name = null;
			this.Model = null;
			this.Color1 = null;
			this.Color2 = null;
			this.GRedTeam = null;
			this.GBlueTeam = null;
			this.Team = Team.Free;
			this.BotSkill = -1.0f;
		}
	}
}
