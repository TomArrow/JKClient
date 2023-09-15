using System;
using System.Collections.Generic;
using System.Text;

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

}
