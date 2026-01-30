using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace JKClient
{
    // This is all kinda scuffed, ignore it. We just hardcode this elsewhere, sigh.
    public class MOHBrowserHandler : MOHNetHandler, IBrowserHandler
    {
        private const string MasterMOHOldMasterGamespy = "master.gamespy.com"; // Can't query this even if we wanted to :)
        private const string MasterMOHOldMaster = "master.2015.com";
        private const string MasterXNull = "master.x-null.net";
        private const ushort PortMasterMOH = 27950;
        private const ushort PortMasterMOHSocketService = 8080;
        public virtual bool NeedStatus { get; private set; }
        public int[] AdditionalProtocols { get; private set; } = null;
        public MOHBrowserHandler(ProtocolVersion protocol, bool allProtocols = false) : base(protocol)
        {
            if (allProtocols)
            {
                AdditionalProtocols = new int[] { (int)ProtocolVersion.Protocol6, (int)ProtocolVersion.Protocol7,(int)ProtocolVersion.Protocol15, (int)ProtocolVersion.Protocol16, (int)ProtocolVersion.Protocol17 };
            }
        }
        public virtual IEnumerable<ServerBrowser.ServerAddress> GetMasterServers()
        {
            return new ServerBrowser.ServerAddress[] {
				//new ServerBrowser.ServerAddress(MOHBrowserHandler.MasterMOHOldMaster, MOHBrowserHandler.PortMasterMOH), // It's dead, who cares.
				new ServerBrowser.ServerAddress(MOHBrowserHandler.MasterXNull, MOHBrowserHandler.PortMasterMOHSocketService),
            };
        }
        public virtual void HandleInfoPacket(in ServerInfo serverInfo, in InfoString info)
        {
            this.NeedStatus = true;
            if (info.Count <= 0)
            {
                return;
            }
            GameTypeMOH gameTypeMOH = (GameTypeMOH)info["gametype"].Atoi();
            switch (gameTypeMOH)
            {
                case GameTypeMOH.GT_SINGLE_PLAYER:
                    serverInfo.GameType = GameType.SinglePlayer;
                    break;
                default:
                case GameTypeMOH.GT_MAX_GAME_TYPE:
                case GameTypeMOH.GT_FFA:
                    serverInfo.GameType = GameType.FFA;
                    break;
                case GameTypeMOH.GT_TEAM:
                    serverInfo.GameType = GameType.Team;
                    break;
                case GameTypeMOH.GT_TEAM_ROUNDS:
                    serverInfo.GameType = GameType.TeamRounds;
                    break;
                case GameTypeMOH.GT_OBJECTIVE:
                    serverInfo.GameType = GameType.Objective;
                    break;
                case GameTypeMOH.GT_TOW:
                    serverInfo.GameType = GameType.TOW;
                    break;
                case GameTypeMOH.GT_LIBERATION:
                    serverInfo.GameType = GameType.Liberation;
                    break;
            }
            //serverInfo.NeedPassword = info["needpass"].Atoi() != 0;
            serverInfo.GameName = info["gametypestring"]; // Ofc MOH is special and can't just use normal gamename :)
        }
        public virtual void HandleStatusResponse(in ServerInfo serverInfo, in InfoString info)
        {
            GameTypeMOH gameTypeMOH = (GameTypeMOH)info["g_gametype"].Atoi();
            switch (gameTypeMOH)
            {
                case GameTypeMOH.GT_SINGLE_PLAYER:
                    serverInfo.GameType = GameType.SinglePlayer;
                    break;
                default:
                case GameTypeMOH.GT_MAX_GAME_TYPE:
                case GameTypeMOH.GT_FFA:
                    serverInfo.GameType = GameType.FFA;
                    break;
                case GameTypeMOH.GT_TEAM:
                    serverInfo.GameType = GameType.Team;
                    break;
                case GameTypeMOH.GT_TEAM_ROUNDS:
                    serverInfo.GameType = GameType.TeamRounds;
                    break;
                case GameTypeMOH.GT_OBJECTIVE:
                    serverInfo.GameType = GameType.Objective;
                    break;
                case GameTypeMOH.GT_TOW:
                    serverInfo.GameType = GameType.TOW;
                    break;
                case GameTypeMOH.GT_LIBERATION:
                    serverInfo.GameType = GameType.Liberation;
                    break;
            }
            serverInfo.GameName = info["g_gametypestring"]; // Ofc MOH is special and can't just use normal gamename :)
            serverInfo.ServerSVInfoString = info["sv_info"]; 
            serverInfo.Protocol = (ProtocolVersion)info["protocol"].Atoi();
            this.NeedStatus = false;
        }

        public static Response333Networks parse333NetworksResponse(string data)
        {
            JsonSerializerOptions opts = new JsonSerializerOptions() {  NumberHandling= System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals | System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };

            //byte[] byteData = Encoding.UTF8.GetBytes(data);
            //Utf8JsonReader jsonReader = new Utf8JsonReader(byteData, new JsonReaderOptions() { AllowTrailingCommas=true,CommentHandling= JsonCommentHandling.Skip, MaxDepth=10 });
            //jsonReader.Read();
            var stuff = JsonSerializer.Deserialize<JsonElement>(data);

            if (stuff.GetArrayLength() < 2) return null;
            ServerItem333Networks[] deserialized = JsonSerializer.Deserialize<ServerItem333Networks[]>(stuff[0], opts);
            //jsonReader.Read();
            OverviewItem333Networks overview = JsonSerializer.Deserialize<OverviewItem333Networks>(stuff[1], opts);
            //jsonReader.Read();

            if(overview != null && deserialized != null)
            {
                return new Response333Networks() { overview = overview, items = deserialized };
            }

            return null;
        }
    }





    public class XNullServerListWebSocketResponse
    {
        public XNullServerData[] Servers { get; set; }
    }

    public class XNullServerData
    {
        public string ip { get; set; }
        public int port { get; set; }
        public int queryport { get; set; }
        public string gameid { get; set; }
        public string status { get; set; }
        public string gsstatus { get; set; }
        public string alive { get; set; }
        public string gamever { get; set; }
    }

    public class Response333Networks {
        public ServerItem333Networks[] items;
        public OverviewItem333Networks overview;
    }


    public class OverviewItem333Networks
    {
        public int players { get; set; }
        public int total { get; set; }
    }

    public class ServerItem333Networks
    {
        public string country { get; set; }
        public int dt_added { get; set; }
        public int dt_updated { get; set; }
        public string gamename { get; set; }
        public string gametype { get; set; }
        public string hostname { get; set; }
        public int hostport { get; set; }
        public int id { get; set; }
        public string ip { get; set; }
        public string label { get; set; }
        public string mapname { get; set; }
        public object maptitle { get; set; }
        public int maxplayers { get; set; }
        public int numplayers { get; set; }
    }


}