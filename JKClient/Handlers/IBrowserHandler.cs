using System.Collections.Generic;

namespace JKClient {
	public interface IBrowserHandler : INetHandler {
		int[] AdditionalProtocols { get; }
		bool NeedStatus { get; }
		IEnumerable<ServerBrowser.ServerAddress> GetMasterServers();
		void HandleInfoPacket(in ServerInfo serverInfo, in InfoString info);
		void HandleStatusResponse(in ServerInfo serverInfo, in InfoString info);
	}
}
