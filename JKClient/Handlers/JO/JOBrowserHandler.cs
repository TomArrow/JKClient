using System.Collections.Generic;

namespace JKClient {
	public class JOBrowserHandler : JONetHandler, IBrowserHandler {
		private const string MasterJK2RavenSoftware = "masterjk2.ravensoft.com";
		private const string MasterJKHub = "master.jkhub.org";
		private const string MasterJK2MV = "master.jk2mv.org";
		private const ushort PortMasterJO = 28060;
		public virtual bool NeedStatus { get; private set; }
		public int[] AdditionalProtocols { get; private set; } = null;
		public JOBrowserHandler(ProtocolVersion protocol, bool allProtocols = false) : base(protocol) {
            if (allProtocols)
            {
				if(protocol == ProtocolVersion.Protocol16)
                {
					AdditionalProtocols = new int[] { (int)ProtocolVersion.Protocol15};
                } else if(protocol == ProtocolVersion.Protocol15)
                {
					AdditionalProtocols = new int[] { (int)ProtocolVersion.Protocol16};
                }
            }
		}
		public virtual IEnumerable<ServerBrowser.ServerAddress> GetMasterServers() {
			return new ServerBrowser.ServerAddress[] {
				new ServerBrowser.ServerAddress(JOBrowserHandler.MasterJK2RavenSoftware, JOBrowserHandler.PortMasterJO),
				new ServerBrowser.ServerAddress(JOBrowserHandler.MasterJKHub, JOBrowserHandler.PortMasterJO),
				new ServerBrowser.ServerAddress(JOBrowserHandler.MasterJK2MV, JOBrowserHandler.PortMasterJO)
			};
		}
		public virtual void HandleInfoPacket(in ServerInfo serverInfo, in InfoString info) {
			// We often use the same serverbrowser for both jka and jo. switch here.
            if (serverInfo.Protocol >= ProtocolVersion.Protocol25 && serverInfo.Protocol <= ProtocolVersion.Protocol26 )
            {
				this.NeedStatus = JABrowserHandler.HandleInfoPacketJA(serverInfo, info);
			} else
            {
				this.NeedStatus = HandleInfoPacketJO(serverInfo, info);
			}
		}
		public static bool HandleInfoPacketJO(in ServerInfo serverInfo, in InfoString info) {
			bool needStatus = true;
			switch (serverInfo.Protocol) {
			case ProtocolVersion.Protocol15:
				serverInfo.Version = ClientVersion.JO_v1_02;
				break;
			case ProtocolVersion.Protocol16:
				serverInfo.Version = ClientVersion.JO_v1_04;
				break;
			}
			if (info.Count <= 0) {
				return needStatus;
			}
			int gameType = info["gametype"].Atoi();
			//JO doesn't have Power Duel, the rest game types match
			if (gameType >= (int)GameType.PowerDuel) {
				gameType++;
			}
			serverInfo.GameType = (GameType)gameType;
			serverInfo.NeedPassword = info["needpass"].Atoi() != 0;
			serverInfo.TrueJedi = info["truejedi"].Atoi() != 0;
			serverInfo.WeaponDisable = info["wdisable"].Atoi() != 0;
			serverInfo.ForceDisable = info["fdisable"].Atoi() != 0;
			return needStatus;
		}
		public virtual void HandleStatusResponse(in ServerInfo serverInfo, in InfoString info) {
			// We often use the same serverbrowser for both jka and jo. switch here.
			if (serverInfo.Protocol >= ProtocolVersion.Protocol25 && serverInfo.Protocol <= ProtocolVersion.Protocol26)
			{
				JABrowserHandler.HandleStatusResponseJA(serverInfo, info);
			}
			else
			{
				HandleStatusResponseJO(serverInfo, info);
			}
			this.NeedStatus = false;
		}
		public static void HandleStatusResponseJO(in ServerInfo serverInfo, in InfoString info)
        {
			if (info["version"].Contains("v1.03"))
			{
				serverInfo.Version = ClientVersion.JO_v1_03;
			}
			int gameType = info["g_gametype"].Atoi();
			//JO doesn't have Power Duel, the rest game types match
			if (gameType >= (int)GameType.PowerDuel)
			{
				gameType++;
			}
			serverInfo.GameType = (GameType)gameType;
		}
	}
}
