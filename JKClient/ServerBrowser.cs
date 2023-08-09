using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace JKClient {
	public sealed class ServerBrowser : NetClient {
		public long RefreshTimeout { get; init; } = 3000L;
		public bool ForceStatus { get; init; } = false;
		private readonly List<ServerAddress> masterServers;
		private readonly ConcurrentDictionary<NetAddress, ServerInfo> globalServers;
		private TaskCompletionSource<IEnumerable<ServerInfo>> getListTCS, refreshListTCS;
		private static List<NetAddress> hiddenServers = new List<NetAddress>();
		public static void SetHiddenServers(IEnumerable<NetAddress> hiddenServersA)
        {
            lock (hiddenServers)
            {
				hiddenServers.Clear();
				foreach(NetAddress theAddress in hiddenServersA)
                {
					hiddenServers.Add(theAddress);
				}
			}
        }
		private long serverRefreshTimeout = 0L;
		private readonly ConcurrentDictionary<NetAddress, FullServerInfoTask> fullServerInfoTasks;
		private readonly HashSet<NetAddress> fullServerInfoTasksToRemove;
		private readonly ConcurrentDictionary<NetAddress, ServerInfoTask> serverInfoTasks;
		private readonly HashSet<NetAddress> serverInfoTasksToRemove;
		private readonly ConcurrentDictionary<NetAddress, ServerInfoInfoTask> serverInfoInfoTasks;
		private readonly HashSet<NetAddress> serverInfoInfoTasksToRemove;
		private IBrowserHandler BrowserHandler => this.NetHandler as IBrowserHandler;
		public ServerBrowser(IBrowserHandler browserHandler, IEnumerable<ServerAddress> customMasterServers = null, bool customOnly = false)
			: base(browserHandler) {
			if (customOnly && customMasterServers == null) {
				throw new JKClientException(new ArgumentNullException(nameof(customMasterServers)));
			}
			if (customOnly) {
				this.masterServers = new List<ServerAddress>(customMasterServers);
			} else {
				this.masterServers = new List<ServerAddress>(this.BrowserHandler.GetMasterServers());
				if (customMasterServers != null) {
					this.masterServers.AddRange(customMasterServers);
				}
			}
			this.globalServers = new ConcurrentDictionary<NetAddress, ServerInfo>(new NetAddressComparer());
			this.fullServerInfoTasks = new ConcurrentDictionary<NetAddress, FullServerInfoTask>(new NetAddressComparer());
			this.fullServerInfoTasksToRemove = new HashSet<NetAddress>(new NetAddressComparer());
			this.serverInfoTasks = new ConcurrentDictionary<NetAddress, ServerInfoTask>(new NetAddressComparer());
			this.serverInfoTasksToRemove = new HashSet<NetAddress>(new NetAddressComparer());
			this.serverInfoInfoTasks = new ConcurrentDictionary<NetAddress, ServerInfoInfoTask>(new NetAddressComparer());
			this.serverInfoInfoTasksToRemove = new HashSet<NetAddress>(new NetAddressComparer());
		}
		private protected override void OnStop(bool afterFailure) {
			this.getListTCS?.TrySetCanceled();
			this.refreshListTCS?.TrySetCanceled();
			this.serverRefreshTimeout = 0L;
			base.OnStop(afterFailure);
		}
		private protected override async Task Run() {
			const int frameTime = 8;
			while (true) {
				this.GetPacket();
				this.HandleServersList();
				this.HandleFullServerInfoTasks();
				this.HandleServerInfoTasks();
				this.HandleServerInfoInfoTasks();
				await Task.Delay(frameTime);
			}
		}
		private void HandleServersList() {
			if (this.serverRefreshTimeout != 0L && this.serverRefreshTimeout < Common.Milliseconds) {
				this.getListTCS?.TrySetResult(this.globalServers.Values);
				this.refreshListTCS?.TrySetResult(this.globalServers.Values);
				this.serverRefreshTimeout = 0L;
			}
		}
		private void HandleFullServerInfoTasks() {
			foreach (var fullServerInfoTask in this.fullServerInfoTasks) {
				if (fullServerInfoTask.Value.Timeout < Common.Milliseconds) {
					fullServerInfoTask.Value.TrySetCanceled();
					this.fullServerInfoTasksToRemove.Add(fullServerInfoTask.Key);
				}
			}
			foreach (var fullServerInfoTaskToRemove in this.fullServerInfoTasksToRemove) {
				this.fullServerInfoTasks.TryRemove(fullServerInfoTaskToRemove, out _);
			}
			this.fullServerInfoTasksToRemove.Clear();
		}
		private void HandleServerInfoTasks() {
			foreach (var serverInfoTask in this.serverInfoTasks) {
				if (serverInfoTask.Value.Timeout < Common.Milliseconds) {
					serverInfoTask.Value.TrySetCanceled();
					this.serverInfoTasksToRemove.Add(serverInfoTask.Key);
				}
			}
			foreach (var serverInfoTaskToRemove in this.serverInfoTasksToRemove) {
				this.serverInfoTasks.TryRemove(serverInfoTaskToRemove, out _);
			}
			this.serverInfoTasksToRemove.Clear();
		}
		private void HandleServerInfoInfoTasks() {
			foreach (var serverInfoInfoTask in this.serverInfoInfoTasks) {
				if (serverInfoInfoTask.Value.Timeout < Common.Milliseconds) {
					serverInfoInfoTask.Value.TrySetCanceled();
					this.serverInfoInfoTasksToRemove.Add(serverInfoInfoTask.Key);
				}
			}
			foreach (var serverInfoInfoTaskToRemove in this.serverInfoInfoTasksToRemove) {
				this.serverInfoInfoTasks.TryRemove(serverInfoInfoTaskToRemove, out _);
			}
			this.serverInfoInfoTasksToRemove.Clear();
		}
		public async Task<IEnumerable<ServerInfo>> GetNewList() {
			this.getListTCS?.TrySetCanceled();
			this.getListTCS = new TaskCompletionSource<IEnumerable<ServerInfo>>();
			this.globalServers.Clear();
			this.serverRefreshTimeout = Common.Milliseconds + this.RefreshTimeout;
			lock (hiddenServers) // For servers that aren't reported to the master server. Set via SetHiddenServers();
			{
				foreach (NetAddress hiddenServer in hiddenServers)
				{
					var serverInfo = new ServerInfo()
					{
						Address = hiddenServer,
						Start = Common.Milliseconds
					};
					this.globalServers[serverInfo.Address] = serverInfo;
					this.OutOfBandPrint(serverInfo.Address, "getinfo xxx");
				}
			}
			this.serverRefreshTimeout = Common.Milliseconds + this.RefreshTimeout;
			foreach (var masterServer in this.masterServers) {
				var address = await NetSystem.StringToAddressAsync(masterServer.Name, masterServer.Port);
				if (address == null) {
					continue;
				}
				this.OutOfBandPrint(address, $"getservers {this.Protocol}");
				if((this.NetHandler as IBrowserHandler).AdditionalProtocols != null)
                {
					foreach(int additionalProtocol in (this.NetHandler as IBrowserHandler).AdditionalProtocols)
                    {
						this.OutOfBandPrint(address, $"getservers {additionalProtocol}");
					}
                }
			}
			return await this.getListTCS.Task;
		}
		public async Task<IEnumerable<ServerInfo>> RefreshList() {
			if (this.globalServers.Count <= 0) {
				return await this.GetNewList();
			}
			this.refreshListTCS?.TrySetCanceled();
			this.refreshListTCS = new TaskCompletionSource<IEnumerable<ServerInfo>>();
			this.serverRefreshTimeout = Common.Milliseconds + this.RefreshTimeout;
			foreach (var server in this.globalServers) {
				var serverInfo = server.Value;
				serverInfo.InfoSet = false;
				serverInfo.Start = Common.Milliseconds;
				this.OutOfBandPrint(serverInfo.Address, "getinfo xxx");
			}
			return await this.refreshListTCS.Task;
		}
		public async Task<InfoString> GetServerInfo(NetAddress address) {
			if (this.serverInfoTasks.ContainsKey(address)) {
				this.serverInfoTasks[address].TrySetCanceled();
			}
			var serverInfoTCS = this.serverInfoTasks[address] = new ServerInfoTask();
			this.OutOfBandPrint(address, "getstatus");
			return await serverInfoTCS.Task;
		}
		public async Task<ServerInfo> GetFullServerInfo(NetAddress address, bool status=true, bool info=true) {
			if (this.fullServerInfoTasks.ContainsKey(address)) {
				this.fullServerInfoTasks[address].TrySetCanceled();
			}
			var fullServerInfoTCS = this.fullServerInfoTasks[address] = new FullServerInfoTask() { needsInfo=info,needsStatus=status };
            if (!this.globalServers.ContainsKey(address)) // TODO Hm ugly? Is there a nicer way?
            {
				var serverInfo = new ServerInfo()
				{
					Address = address,
					Start = Common.Milliseconds
				};
				this.globalServers[serverInfo.Address] = serverInfo;
			}
			if (info) this.OutOfBandPrint(address, "getinfo xxx");
			if (status) this.OutOfBandPrint(address, "getstatus");
			return await fullServerInfoTCS.Task;
		}
		public async Task<InfoString> GetServerInfo(string address, ushort port = 0) {
			var netAddress = await NetSystem.StringToAddressAsync(address, port);
			if (netAddress == null) {
				return null;
			}
			return await this.GetServerInfo(netAddress);
		}
		// For actual "getinfo" packet since GetServerInfo actually gives you "getstatus"
		public async Task<InfoString> GetServerInfoInfo(NetAddress address) {
			if (this.serverInfoInfoTasks.ContainsKey(address)) {
				this.serverInfoInfoTasks[address].TrySetCanceled();
			}
			var serverInfoInfoTCS = this.serverInfoInfoTasks[address] = new ServerInfoInfoTask();
			this.OutOfBandPrint(address, "getinfo xxx");
			return await serverInfoInfoTCS.Task;
		}
		// For actual "getinfo" packet since GetServerInfo actually gives you "getstatus"
		public async Task<InfoString> GetServerInfoInfo(string address, ushort port = 0) {
			var netAddress = await NetSystem.StringToAddressAsync(address, port);
			if (netAddress == null) {
				return null;
			}
			return await this.GetServerInfoInfo(netAddress);
		}
		private protected override unsafe void PacketEvent(in NetAddress address, in Message msg) {
			fixed (byte *b = msg.Data) {
				if (msg.CurSize >= 4 && *(int*)b == -1) {
					msg.BeginReading(true);
					msg.ReadLong();
					string s = msg.ReadStringLineAsString();
					var command = new Command(s);
					string c = command.Argv(0);
					if (string.Compare(c, "infoResponse", StringComparison.OrdinalIgnoreCase) == 0) {
						this.ServerInfoPacket(address, msg);
					} else if (string.Compare(c, "statusResponse", StringComparison.OrdinalIgnoreCase) == 0) {
						this.ServerStatusResponse(address, msg);
					} else if (string.Compare(c, 0, "getserversResponse", 0, 18, StringComparison.Ordinal) == 0) {
						this.ServersResponsePacket(address, msg);
					}
				}
			}
		}
		private unsafe void ServersResponsePacket(in NetAddress address, in Message msg) {
			fixed (byte *b = msg.Data) {
				byte *buffptr = b;
				byte *buffend = buffptr + msg.CurSize;
				do {
					if (*buffptr == 92) { //'\\'
						break;
					}
					buffptr++;
				} while (buffptr < buffend);
				while (buffptr + 1 < buffend) {
					if (*buffptr != 92) { //'\\'
						break;
					}
					buffptr++;
					byte []ip = new byte[4];
					if (buffend - buffptr < ip.Length + sizeof(ushort) + 1) {
						break;
					}
					for (int i = 0; i < ip.Length; i++) {
						ip[i] = *buffptr++;
					}
					int port = (*buffptr++) << 8;
					port += *buffptr++;
					var serverInfo = new ServerInfo() {
						Address = new NetAddress(ip, (ushort)port),
						Start = Common.Milliseconds
					};
					this.globalServers[serverInfo.Address] = serverInfo;
					this.OutOfBandPrint(serverInfo.Address, "getinfo xxx");
					if (*buffptr != 92 && *buffptr != 47) { //'\\' '/'
						break;
					}
				}
			}
		}
		private void ServerStatusResponse(in NetAddress address, in Message msg) {
			var info = new InfoString(msg.ReadStringLineAsString());
			if (this.serverInfoTasks.ContainsKey(address)) {
				this.serverInfoTasks[address].TrySetResult(info);
				this.serverInfoTasks.TryRemove(address, out _);
			}
			if (this.globalServers.ContainsKey(address)) {
				var serverInfo = this.globalServers[address];
				int playersCount = 0;
				serverInfo.players.Clear();
				for (string s = msg.ReadStringLineAsString(); !string.IsNullOrEmpty(s); s = msg.ReadStringLineAsString()) {
					var command = new Command(s);
					int score = command.Argv(0).Atoi();
					int ping = command.Argv(1).Atoi();
					string name = command.Argv(2);
					serverInfo.players.Add(new Player() {name=name,ping=ping,score=score,isBot=ping<=0 });
					if (ping > 0) {
						playersCount++;
					}
				}
				serverInfo.SetStatusInfo(info);
				serverInfo.RealClients = serverInfo.Clients = playersCount;
				this.BrowserHandler.HandleStatusResponse(serverInfo, info);
				serverInfo.NoBots = info["_nobots"].Atoi() > 0 || info["_noBots"].Atoi() > 0;
				serverInfo.StatusResponseReceived = true;
				serverInfo.StatusResponseReceivedTime = DateTime.Now;
				this.serverRefreshTimeout = Common.Milliseconds + this.RefreshTimeout;

				if (this.fullServerInfoTasks.ContainsKey(address)
					&& (!this.fullServerInfoTasks[address].needsInfo || serverInfo.InfoPacketReceived)
					&& (!this.fullServerInfoTasks[address].needsStatus || serverInfo.StatusResponseReceived)
					)
				{
					this.fullServerInfoTasks[address].TrySetResult(serverInfo);
					this.fullServerInfoTasks.TryRemove(address, out _);
				}
			}
		}
		private void ServerInfoPacket(in NetAddress address, in Message msg) {
			var info = new InfoString(msg.ReadStringAsString());
			if (this.serverInfoInfoTasks.ContainsKey(address))
			{
				this.serverInfoInfoTasks[address].TrySetResult(info);
				this.serverInfoInfoTasks.TryRemove(address, out _);
			}
			if (this.globalServers.ContainsKey(address)) {
				var serverInfo = this.globalServers[address];
				if (serverInfo.InfoSet) {
					return;
				}
				serverInfo.Ping = (int)(Common.Milliseconds - serverInfo.Start);
				serverInfo.SetInfo(info);
				this.BrowserHandler.HandleInfoPacket(serverInfo, info);
				if (this.BrowserHandler.NeedStatus || this.ForceStatus) {
					this.OutOfBandPrint(serverInfo.Address, "getstatus");
				}
				serverInfo.InfoPacketReceived = true;
				serverInfo.InfoPacketReceivedTime = DateTime.Now;
				this.serverRefreshTimeout = Common.Milliseconds + this.RefreshTimeout;

				if (this.fullServerInfoTasks.ContainsKey(address)
					&& (!this.fullServerInfoTasks[address].needsInfo || serverInfo.InfoPacketReceived)
					&& (!this.fullServerInfoTasks[address].needsStatus || serverInfo.StatusResponseReceived)
					)
				{
					this.fullServerInfoTasks[address].TrySetResult(serverInfo);
					this.fullServerInfoTasks.TryRemove(address, out _);
				}
			}
		}
		private class FullServerInfoTask : TaskCompletionSource<ServerInfo> {
			private const long CancelTimeout = 3000L;
			public bool needsStatus = false;
			public bool needsInfo = false;
			public long Timeout { get; init; } = Common.Milliseconds + FullServerInfoTask.CancelTimeout;
		}
		private class ServerInfoTask : TaskCompletionSource<InfoString> {
			private const long CancelTimeout = 3000L;
			public long Timeout { get; init; } = Common.Milliseconds + ServerInfoTask.CancelTimeout;
		}
		private class ServerInfoInfoTask : TaskCompletionSource<InfoString> {
			private const long CancelTimeout = 3000L;
			public long Timeout { get; init; } = Common.Milliseconds + ServerInfoInfoTask.CancelTimeout;
		}
		public sealed class ServerAddress {
			public string Name { get; init; }
			public ushort Port { get; init; }
			public ServerAddress(string name, ushort port) {
				this.Name = name;
				this.Port = port;
			}
		}
		public static IBrowserHandler GetKnownBrowserHandler(ProtocolVersion protocol) {
			switch (protocol) {
			case ProtocolVersion.Protocol25:
			case ProtocolVersion.Protocol26:
				return new JABrowserHandler(protocol);
			case ProtocolVersion.Protocol15:
			case ProtocolVersion.Protocol16:
				return new JOBrowserHandler(protocol);
			case ProtocolVersion.Protocol68:
			case ProtocolVersion.Protocol71:
				return new Q3BrowserHandler(protocol);
			}
			throw new JKClientException($"There isn't any known server browser handler for given protocol: {protocol}");
		}
	}
}
