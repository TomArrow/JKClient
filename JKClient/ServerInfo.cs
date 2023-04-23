using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace JKClient
{
	public struct Player
    {
		public int score;
		public int ping;
		public string name;
		public bool isBot;
		public override string ToString()
        {
			string botStringPart = isBot ? ",bot" : "";
			return $"\"{name}\"({score},{ping}{botStringPart})";
        }
    }

	//TODO: remake to struct?
	public sealed class ServerInfo {
		internal List<Player> players = new List<Player> ();
		public ReadOnlyCollection<Player> Players { get {
				return players.AsReadOnly();
			} }
		public DateTime? InfoPacketReceivedTime { get; internal set; } = null;
		public DateTime? StatusResponseReceivedTime { get; internal set; } = null;
		public bool InfoPacketReceived { get; internal set; } = false;
		public bool StatusResponseReceived { get; internal set; } = false; // If this is true, the Clients count is the actual count of clients excluding bots
		public NetAddress Address { get; internal set; }
		public string HostName { get; internal set; }
		public string MapName { get; internal set; }
		public string Game { get; internal set; }
		public string GameName { get; internal set; }
		public GameType GameType { get; internal set; }
		public int Clients { get; internal set; }
		public int ClientsIncludingBots { get; internal set; }
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
		internal void SetInfo(in InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			this.Protocol = (ProtocolVersion)info["protocol"].Atoi();
			this.Clients = this.ClientsIncludingBots = info["clients"].Atoi();
			this.HostName = info["hostname"];
			this.NWH = info.ContainsKey("nwh") ? (info["nwh"].Atoi()==0?false:true) : false;
			this.MapName = info["mapname"];
			this.MaxClients = info["sv_maxclients"].Atoi();
			this.Game = info["game"];
			this.MinPing = info["minPing"].Atoi();
			this.MaxPing = info["maxPing"].Atoi();
			this.InfoSet = true;
		}
		internal void SetConfigstringInfo(in InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			this.Protocol = (ProtocolVersion)info["protocol"].Atoi();
			
			this.FloodProtect = info["sv_floodProtect"] == String.Empty ? -2 : info["sv_floodProtect"].Atoi();
			this.GameName = info["gamename"];
			this.ServerGameVersionString = info["version"];
			this.Location = info["Location"];

			this.HostName = info["sv_hostname"];
			this.MapName = info["mapname"];
			this.MaxClients = info["sv_maxclients"].Atoi();
			this.MinPing = info["sv_minping"].Atoi();
			this.MaxPing = info["sv_maxping"].Atoi();
			this.InfoSet = true;
		}
		public static bool operator ==(in ServerInfo serverInfo1, in ServerInfo serverInfo2) {
			return serverInfo1?.Address == serverInfo2?.Address;
		}
		public static bool operator !=(in ServerInfo serverInfo1, in ServerInfo serverInfo2) {
			return (serverInfo1 == serverInfo2) != true;
		}
		public override bool Equals(object obj) {
			return base.Equals(obj);
		}
		public override int GetHashCode() {
			return this.Address.GetHashCode();
		}
	}
	public sealed class ServerInfoComparer : EqualityComparer<ServerInfo> {
		public override bool Equals(ServerInfo x, ServerInfo y) {
			return x.Address == y.Address;
		}
		public override int GetHashCode(ServerInfo obj) {
			return obj.Address.GetHashCode();
		}
	}
}
