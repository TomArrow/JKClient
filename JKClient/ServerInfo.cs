using System;

namespace JKClient {
	//TODO: remake to struct?
	public sealed class ServerInfo {
		public NetAddress Address { get; internal set; }
		public string HostName { get; internal set; }
		public string MapName { get; internal set; }
		public string Game { get; internal set; }
		public string GameName { get; internal set; }
		public GameType GameType { get; internal set; }
		public int Clients { get; internal set; }
		public int MaxClients { get; internal set; }
		public int MinPing { get; internal set; }
		public int MaxPing { get; internal set; }
		public int Ping { get; internal set; }
		public bool Visibile { get; internal set; }
		public bool NeedPassword { get; internal set; }
		public bool TrueJedi { get; internal set; }
		public bool WeaponDisable { get; internal set; }
		public bool ForceDisable { get; internal set; }
		public ProtocolVersion Protocol { get; internal set; }
		public ClientVersion Version { get; internal set; }
		public string ServerGameVersionString { get; internal set; }
		public string Location { get; internal set; }
		public bool NWH { get; internal set; } // NWH mod detection
		public int FloodProtect { get; internal set; } = -1; // -1 if not yet set, -2 if server does not send it at all
		public bool Pure { get; internal set; }
		internal bool InfoSet;
		internal long Start;
		internal void SetInfo(InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			this.Protocol = (ProtocolVersion)info["protocol"].Atoi();
			this.Version = JKClient.GetVersion(this.Protocol);
			this.Clients = info["clients"].Atoi();
			this.HostName = info["hostname"];
			this.NWH = info.ContainsKey("nwh") ? (info["nwh"].Atoi()==0?false:true) : false;
			this.MapName = info["mapname"];
			this.MaxClients = info["sv_maxclients"].Atoi();
			this.Game = info["game"];
			this.GameType = ServerInfo.GetGameType(info["gametype"].Atoi(), this.Protocol);
			this.MinPing = info["minping"].Atoi();
			this.MaxPing = info["maxping"].Atoi();
			this.NeedPassword = info["needpass"].Atoi() != 0;
			this.TrueJedi = info["truejedi"].Atoi() != 0;
			this.WeaponDisable = info["wdisable"].Atoi() != 0;
			this.ForceDisable = info["fdisable"].Atoi() != 0;
			//JO doesn't have Power Duel, the rest game types match
			if (JKClient.IsJO(this.Protocol) && this.GameType >= GameType.PowerDuel) {
				this.GameType++;
			}
			this.Pure = info["pure"].Atoi() != 0;
			this.InfoSet = true;
		}
		internal void SetConfigstringInfo(InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			this.Protocol = (ProtocolVersion)info["protocol"].Atoi();
			if (this.Protocol == ProtocolVersion.Protocol15 && info["version"].Contains("v1.03")) {
				this.Version = ClientVersion.JO_v1_03;
			} else {
				this.Version = JKClient.GetVersion(this.Protocol);
			}
			
			this.FloodProtect = info["sv_floodProtect"] == String.Empty ? -2 : info["sv_floodProtect"].Atoi();
			this.HostName = info["sv_hostname"];
			this.MapName = info["mapname"];
			this.MaxClients = info["sv_maxclients"].Atoi();
			this.GameType = ServerInfo.GetGameType(info["g_gametype"].Atoi(), this.Protocol);
			this.GameName = info["gamename"];
			this.ServerGameVersionString = info["version"];
			this.Location = info["Location"];
			this.MinPing = info["sv_minping"].Atoi();
			this.MaxPing = info["sv_maxping"].Atoi();
			this.NeedPassword = info["g_needpass"].Atoi() != 0;
			this.TrueJedi = info["g_jediVmerc"].Atoi() != 0;
			if (this.GameType == GameType.Duel || this.GameType == GameType.PowerDuel) {
				this.WeaponDisable = info["g_duelWeaponDisable"].Atoi() != 0;
			} else {
				this.WeaponDisable = info["g_weaponDisable"].Atoi() != 0;
			}
			this.ForceDisable = info["g_forcePowerDisable"].Atoi() != 0;
			this.InfoSet = true;
		}
		private static GameType GetGameType(int gameType, ProtocolVersion protocol) {
			if (JKClient.IsQ3(protocol)) {
				switch (gameType) {
				case 0:
					return GameType.FFA;
				case 1:
					return GameType.Duel;
				case 2:
					return GameType.SinglePlayer;
				case 3:
					return GameType.Team;
				case 4:
					return GameType.CTF;
				default:
					return (GameType)(gameType+5);
				}
			//JO doesn't have Power Duel, the rest game types match
			} else if (JKClient.IsJO(protocol) && gameType >= (int)GameType.PowerDuel) {
				gameType++;
			}
			return (GameType)gameType;
		}
	}
}
