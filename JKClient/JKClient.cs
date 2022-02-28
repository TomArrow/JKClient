using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JKClient {
	public sealed partial class JKClient : NetClient/*, IJKClientImport*/ {

		public volatile int SnapOrderTolerance = 100;
		public volatile bool SnapOrderToleranceDemoSkipPackets = false;

		private const int LastPacketTimeOut = 5 * 60000;
		private const int RetransmitTimeOut = 3000;
		private const int MaxPacketUserCmds = 32;
		private const string DefaultName = "AssetslessClient";
		private const string UserInfo = "\\name\\" + JKClient.DefaultName + "\\rate\\25000\\snaps\\40\\model\\kyle/default\\forcepowers\\7-1-032330000000001333\\color1\\4\\color2\\4\\handicap\\100\\teamtask\\0\\sex\\male\\password\\\\cg_predictItems\\1\\saber1\\single_1\\saber2\\none\\char_color_red\\255\\char_color_green\\255\\char_color_blue\\255\\engine\\jkclient\\assets\\0";
		private readonly Random random = new Random();
		private readonly int port;
		private readonly InfoString userInfo = new InfoString(UserInfo);
		private readonly ConcurrentQueue<Action> actionsQueue = new ConcurrentQueue<Action>();
		private ClientGame clientGame;
		private TaskCompletionSource<bool> connectTCS;

        public ClientEntity[] Entities => clientGame != null? clientGame.Entities : null;
        public int playerStateClientNum => snap.PlayerState.ClientNum;
        public bool IsInterMission => snap.PlayerState.PlayerMoveType == PlayerMoveType.Intermission;
        public PlayerMoveType PlayerMoveType => snap.PlayerState.PlayerMoveType;
		public int gameTime => this.clientGame == null ? 0: this.clientGame.GameTime;
        //public PlayerState CurrentPlayerState => clientGame != null? clientGame. : null;
        #region ClientConnection
        public int clientNum { get; private set; } = 0;
		private int lastPacketSentTime = 0;
		private int lastPacketTime = 0;
		private NetAddress serverAddress;
		private int connectTime = -9999;
		private int connectPacketCount = 0;
		private int challenge = 0;
		private int checksumFeed = 0;
		private int reliableSequence = 0;
		private int reliableAcknowledge = 0;
		private sbyte [][]reliableCommands;
		private int serverMessageSequence = 0;
		private int serverCommandSequence = 0;
		private int lastExecutedServerCommand = 0;
		private sbyte [][]serverCommands;
		private NetChannel netChannel;
#endregion
#region DemoWriting
		// demo information
		string DemoName;
		//bool SpDemoRecording;
		public bool Demorecording { get; private set; }
		//bool Demoplaying;
		bool Demowaiting;   // don't record until a non-delta message is received
		bool DemoSkipPacket;
		bool FirstDemoFrameSkipped;
		TaskCompletionSource<bool> demoRecordingStartPromise = null;
		TaskCompletionSource<bool> demoFirstPacketRecordedPromise = null;
		Mutex DemofileLock = new Mutex();
		FileStream Demofile;
#endregion
#region ClientStatic
		private int realTime = 0;
		private string servername;
		private NetAddress authorizeServer;
		public ConnectionStatus Status { get; private set; }
		public IClientHandler ClientHandler => this.NetHandler as IClientHandler;
		internal ClientVersion Version => this.ClientHandler.Version;
		public event EventHandler<EntityEventArgs> EntityEvent;
		internal void OnEntityEvent(EntityEventArgs entityEventArgs)
        {
			this.EntityEvent?.Invoke(this,entityEventArgs);
		}
		public event EventHandler SnapshotParsed;
		internal void OnSnapshotParsed(EventArgs eventArgs)
        {
			this.SnapshotParsed?.Invoke(this, eventArgs);
		}
		public event EventHandler Disconnected;
		internal void OnDisconnected(EventArgs eventArgs)
        {
			this.Disconnected?.Invoke(this, eventArgs);
		}
		private int MaxReliableCommands => this.ClientHandler.MaxReliableCommands;
		private string GuidKey => this.ClientHandler.GuidKey;
		#endregion
		public string Name {
			get => this.userInfo["name"];
			set {
				string name = value;
				if (string.IsNullOrEmpty(name)) {
					name = JKClient.DefaultName;
				} else if (name.Length > 31) {
					name = name.Substring(0, 31);
				}
				this.userInfo["name"] = name;
				this.UpdateUserInfo();
			}
		}
		public string Password {
			get => this.userInfo["password"];
			set {
				this.userInfo["password"] = value;
				this.UpdateUserInfo();
			}
		}
		public Guid Guid {
			get => Guid.TryParse(this.userInfo[this.GuidKey], out Guid guid) ? guid : Guid.Empty;
			set {
				this.userInfo[this.GuidKey] = value.ToString();
				this.UpdateUserInfo();
			}
		}
		public string CDKey { get; set; } = string.Empty;
		public ClientInfo []ClientInfo => this.clientGame?.ClientInfo;
		private readonly ServerInfo serverInfo = new ServerInfo();
		public ServerInfo ServerInfo {
			get {
				string serverInfoCSStr = this.GetConfigstring(GameState.ServerInfo);
				var info = new InfoString(serverInfoCSStr);
				this.serverInfo.Address = this.serverAddress;
				this.serverInfo.Clients = this.ClientInfo?.Count(ci => ci.InfoValid) ?? 0;
				this.serverInfo.SetConfigstringInfo(info);
				this.ClientHandler.SetExtraConfigstringInfo(this.serverInfo, info);
				return this.serverInfo;
			}
		}
		public event Action<ServerInfo> ServerInfoChanged;
		internal void NotifyServerInfoChanged() {
			this.ServerInfoChanged?.Invoke(this.ServerInfo);
		}
		public JKClient(IClientHandler clientHandler) : base(clientHandler) {
			this.Status = ConnectionStatus.Disconnected;
			this.port = random.Next(1, 0xffff) & 0xffff;
			this.reliableCommands = new sbyte[this.MaxReliableCommands][];
			this.serverCommands = new sbyte[this.MaxReliableCommands][];
			for (int i = 0; i < this.MaxReliableCommands; i++) {
				this.serverCommands[i] = new sbyte[Common.MaxStringChars];
				this.reliableCommands[i] = new sbyte[Common.MaxStringChars];
			}
		}
		private protected override void OnStart() {
			//don't start with any pending actions
			this.DequeueActions(false);
			base.OnStart();
		}
		private protected override async Task Run() {
			long frameTime, lastTime = Common.Milliseconds;
			int msec;
			this.realTime = 0;
			while (true) {
				if (!this.Started) {
					break;
				}
				if (this.realTime - this.lastPacketTime > JKClient.LastPacketTimeOut && this.Status == ConnectionStatus.Active) {
					var cmd = new Command(new string []{ "disconnect", "Last packet from server was too long ago" });
					this.Disconnect();
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd));
				}
				this.GetPacket();
				frameTime = Common.Milliseconds;
				msec = (int)(frameTime - lastTime);
				if (msec > 5000) {
					msec = 5000;
				}
				this.DequeueActions();
				lastTime = frameTime;
				this.realTime += msec;
				this.SendCmd();
				this.CheckForResend();
				this.SetTime();
				if (this.Status >= ConnectionStatus.Primed) {
					this.clientGame.Frame(this.serverTime);
				}
				await Task.Delay(8);
			}
			//complete all actions after stop
			this.DequeueActions();
		}
		private void DequeueActions(bool invoke = true) {
#if NETSTANDARD2_1
			if (!invoke) {
				this.actionsQueue.Clear();
				return;
			}
#endif
			while (this.actionsQueue.TryDequeue(out var action)) {
				if (invoke) {
					action?.Invoke();
				}
			}
		}
		public void SetUserInfoKeyValue(string key, string value) {
			key = key.ToLower();
			if (key == "name") {
				this.Name = value;
			} else if (key == "password") {
				this.Password = value;
			} else if (key == this.GuidKey) {
				this.Guid = Guid.TryParse(value, out Guid guid) ? guid : Guid.Empty;
			} else {
				this.userInfo[key] = value;
				this.UpdateUserInfo();
			}
		}
		private void UpdateUserInfo() {
			if (this.Status < ConnectionStatus.Challenging) {
				return;
			}
			this.ExecuteCommand($"userinfo \"{userInfo}\"");
		}
		private void CheckForResend() {
			if (this.Status != ConnectionStatus.Connecting && this.Status != ConnectionStatus.Challenging) {
				return;
			}
			if (this.realTime - this.connectTime < JKClient.RetransmitTimeOut) {
				return;
			}
			this.connectTime = this.realTime;
			this.connectPacketCount++;
			switch (this.Status) {
			case ConnectionStatus.Connecting:
				this.RequestAuthorization();
				this.OutOfBandPrint(this.serverAddress, $"getchallenge {this.challenge}");
				break;
			case ConnectionStatus.Challenging:
				string data = $"connect \"{this.userInfo}\\protocol\\{this.Protocol.ToString("d")}\\qport\\{this.port}\\challenge\\{this.challenge}\"";
				this.OutOfBandData(this.serverAddress, data, data.Length);
				break;
			}
		}
		private void RequestAuthorization() {
			if (!this.ClientHandler.RequiresAuthorization) {
				return;
			}
			if (this.authorizeServer == null) {
				this.authorizeServer = NetSystem.StringToAddress("authorize.quake3arena.com", 27952);
				if (this.authorizeServer == null) {
					Debug.WriteLine("Couldn't resolve authorize address");
					return;
				}
			}
			var nums = Regex.Replace(CDKey, "[^a-zA-Z0-9]", string.Empty);
			this.OutOfBandPrint(this.authorizeServer, $"getKeyAuthorize {0} {nums}");
		}
		private unsafe void Encode(Message msg) {
			if (msg.CurSize <= 12) {
				return;
			}
			msg.SaveState();
			msg.BeginReading();
			int serverId = msg.ReadLong();
			int messageAcknowledge = msg.ReadLong();
			int reliableAcknowledge = msg.ReadLong();
			msg.RestoreState();
			fixed (sbyte *b = this.serverCommands[reliableAcknowledge & (this.MaxReliableCommands-1)]) {
				fixed (byte *d = msg.Data) {
					byte *str = (byte*)b;
					int index = 0;
					byte key = (byte)(this.challenge ^ serverId ^ messageAcknowledge);
					for (int i = 12; i < msg.CurSize; i++) {
						if (str[index] == 0)
							index = 0;
						if ((!this.ClientHandler.FullByteEncoding && str[index] > 127) || str[index] == 37) { //'%'
							key ^= (byte)(46 << (i & 1)); //'.'
						} else {
							key ^= (byte)(str[index] << (i & 1));
						}
						index++;
						*(d + i) = (byte)(*(d + i) ^ key);
					}
				}
			}
		}
		private unsafe void Decode(Message msg) {
			msg.SaveState();
			msg.Bitstream();
			int reliableAcknowledge = msg.ReadLong();
			msg.RestoreState();
			fixed (sbyte *b = this.reliableCommands[reliableAcknowledge & (this.MaxReliableCommands-1)]) {
				fixed (byte *d = msg.Data) {
					byte *str = (byte*)b;
					int index = 0;
					byte key = (byte)(this.challenge ^ *(uint*)d);
					for (int i = msg.ReadCount + 4; i < msg.CurSize; i++) {
						if (str[index] == 0)
							index = 0;
						if ((!this.ClientHandler.FullByteEncoding && str[index] > 127) || str[index] == 37) { //'%'
							key ^= (byte)(46 << (i & 1)); //'.'
						} else {
							key ^= (byte)(str[index] << (i & 1));
						}
						index++;
						*(d + i) = (byte)(*(d + i) ^ key);
					}
				}
			}
		}
		private protected override unsafe void PacketEvent(NetAddress address, Message msg) {
			//			this.lastPacketTime = this.realTime;
			int headerBytes;
			fixed (byte *b = msg.Data) {
				if (msg.CurSize >= 4 && *(int*)b == -1) {
					this.ConnectionlessPacket(address, msg);
					return;
				}
				if (this.Status < ConnectionStatus.Connected) {
					return;
				}
				if (msg.CurSize < 4) {
					return;
				}
				if (address != this.netChannel.Address) {
					return;
				}
				if (!this.netChannel.Process(msg)) {
					return;
				}
				this.Decode(msg);

				// the header is different lengths for reliable and unreliable messages
				headerBytes = msg.ReadCount;

				this.serverMessageSequence = *(int*)b;
				this.lastPacketTime = this.realTime;
				this.ParseServerMessage(msg);


				//
				// we don't know if it is ok to save a demo message until
				// after we have parsed the frame
				//
				if (Demorecording && !Demowaiting && !DemoSkipPacket)
				{
					WriteDemoMessage(msg, headerBytes);
					if(demoFirstPacketRecordedPromise != null)
                    {
						demoFirstPacketRecordedPromise.SetResult(true); // Just in case the outside code wants to do something particular once actual packets are being recorded.
						demoFirstPacketRecordedPromise = null;
					}
				}
				//DemoSkipPacket = false; // Reset again for next message
											 // TODO Maybe instead make a queue of packages to be written to the demo file.
											 // Then just read them in the correct order. That way we can integrate even packages out of order.
											 // However it's low priority bc this error is relatively rare.
			}
		}
		private void ConnectionlessPacket(NetAddress address, Message msg) {
			msg.BeginReading(true);
			msg.ReadLong();
			string s = msg.ReadStringLineAsString();
			var command = new Command(s);
			string c = command.Argv(0);
			if (string.Compare(c, "challengeResponse", StringComparison.OrdinalIgnoreCase) == 0) {
				if (this.Status != ConnectionStatus.Connecting) {
					return;
				}
				c = command.Argv(2);
				if (address != this.serverAddress) {
					if (string.IsNullOrEmpty(c) || c.Atoi() != this.challenge)
						return;
				}
				this.challenge = command.Argv(1).Atoi();
				this.Status = ConnectionStatus.Challenging;
				this.connectPacketCount = 0;
				this.connectTime = -99999;
				this.serverAddress = address;
			} else if (string.Compare(c, "connectResponse", StringComparison.OrdinalIgnoreCase) == 0) {
				if (this.Status != ConnectionStatus.Challenging) {
					return;
				}
				if (address != this.serverAddress) {
					return;
				}
				this.netChannel = new NetChannel(this.net, address, this.port, this.ClientHandler.MaxMessageLength);
				this.Status = ConnectionStatus.Connected;
				this.lastPacketSentTime = -9999;
			} else if (string.Compare(c, "disconnect", StringComparison.OrdinalIgnoreCase) == 0) {
				if (this.netChannel == null) {
					return;
				}
				if (address != this.netChannel.Address) {
					return;
				}
				if (this.realTime - this.lastPacketTime < 3000) {
					return;
				}
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command));
				this.Disconnect();
			} else if (string.Compare(c, "echo", StringComparison.OrdinalIgnoreCase) == 0) {
				this.OutOfBandPrint(address, command.Argv(1));
			} else if (string.Compare(c, "print", StringComparison.OrdinalIgnoreCase) == 0) {
				if (address == this.serverAddress) {
					s = msg.ReadStringAsString();
					var cmd = new Command(new string []{ "print", s });
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd));
					Debug.WriteLine(s);
				}
			} else {
				Debug.WriteLine(c);
			}
		}
		private void CreateNewCommand() {
			if (this.Status < ConnectionStatus.Primed) {
				return;
			}
			this.cmdNumber++;
			this.cmds[this.cmdNumber & UserCommand.CommandMask].ServerTime = this.serverTime;
		}
		private void SendCmd() {
			if (this.Status < ConnectionStatus.Connected) {
				return;
			}
			this.CreateNewCommand();
			int oldPacketNum = (this.netChannel.OutgoingSequence - 1) & JKClient.PacketMask;
			int delta = this.realTime - this.outPackets[oldPacketNum].RealTime;
			if (delta < 10) {
				return;
			}
			this.WritePacket();
		}
		private void WritePacket() {
			var oldcmd = new UserCommand();
			byte []data = new byte[this.ClientHandler.MaxMessageLength];
			var msg = new Message(data, sizeof(byte)*this.ClientHandler.MaxMessageLength);
			msg.Bitstream();
			msg.WriteLong(this.serverId);
			msg.WriteLong(this.serverMessageSequence);
			msg.WriteLong(this.serverCommandSequence);
			for (int i = this.reliableAcknowledge + 1; i <= this.reliableSequence; i++) {
				msg.WriteByte((int)ClientCommandOperations.ClientCommand);
				msg.WriteLong(i);
				msg.WriteString(this.reliableCommands[i & (this.MaxReliableCommands-1)]);
			}
			int oldPacketNum = (this.netChannel.OutgoingSequence - 1 - 1) & JKClient.PacketMask;
			int count = this.cmdNumber - this.outPackets[oldPacketNum].CommandNumber;
			if (count > JKClient.MaxPacketUserCmds) {
				count = JKClient.MaxPacketUserCmds;
			}
			if (count >= 1) {
				if (!this.snap.Valid || this.serverMessageSequence != this.snap.MessageNum || Demowaiting) {
					msg.WriteByte((int)ClientCommandOperations.MoveNoDelta);
				} else {
					msg.WriteByte((int)ClientCommandOperations.Move);
				}
				msg.WriteByte(count);
				int key = this.checksumFeed;
				key ^= this.serverMessageSequence;
				key ^= Common.HashKey(this.serverCommands[this.serverCommandSequence & (this.MaxReliableCommands-1)], 32);
				for (int i = 0; i < count; i++) {
					int j = (this.cmdNumber - count + i + 1) & UserCommand.CommandMask;
					msg.WriteDeltaUsercmdKey(key, ref oldcmd, ref this.cmds[j]);
					oldcmd = this.cmds[j];
				}
			}
			int packetNum = this.netChannel.OutgoingSequence & JKClient.PacketMask;
			this.outPackets[packetNum].RealTime = this.realTime;
			this.outPackets[packetNum].ServerTime = oldcmd.ServerTime;
			this.outPackets[packetNum].CommandNumber = this.cmdNumber;
			msg.WriteByte((int)ClientCommandOperations.EOF);
			this.Encode(msg);
			this.netChannel.Transmit(msg.CurSize, msg.Data);
			while (this.netChannel.UnsentFragments) {
				this.netChannel.TransmitNextFragment();
			}
		}
		private unsafe void AddReliableCommand(string cmd, bool disconnect = false, Encoding encoding = null) {
			int unacknowledged = this.reliableSequence - this.reliableAcknowledge;
			fixed (sbyte *reliableCommand = this.reliableCommands[++this.reliableSequence & (this.MaxReliableCommands-1)]) {
				encoding = encoding ?? Common.Encoding;
				Marshal.Copy(encoding.GetBytes(cmd+'\0'), 0, (IntPtr)(reliableCommand), encoding.GetByteCount(cmd)+1);
			}
		}
		public void ExecuteCommand(string cmd, Encoding encoding = null) {
			void executeCommand() {
				if (cmd.StartsWith("rcon ", StringComparison.OrdinalIgnoreCase)) {
					this.ExecuteCommandDirectly(cmd, encoding);
				} else {
					this.AddReliableCommand(cmd, encoding: encoding);
				}
			}
			this.actionsQueue.Enqueue(executeCommand);
		}
		private void ExecuteCommandDirectly(string cmd, Encoding encoding) {
			this.OutOfBandPrint(this.serverAddress, cmd);
		}
		public async Task Connect(ServerInfo serverInfo) {
			if (serverInfo == null) {
				throw new JKClientException(new ArgumentNullException(nameof(serverInfo)));
			}
			await this.Connect(serverInfo.Address.ToString(), serverInfo.Protocol);
		}
		public async Task Connect(string address, ProtocolVersion protocol = ProtocolVersion.Unknown) {
			this.connectTCS?.TrySetCanceled();
			var serverAddress = NetSystem.StringToAddress(address);
			if (serverAddress == null) {
				throw new JKClientException("Bad server address");
			}
			if (this.Protocol != protocol) {
				throw new JKClientException("Protocol mismatch on connect");
			}
			this.connectTCS = new TaskCompletionSource<bool>();
			void connect() {
				this.servername = address;
				this.serverAddress = serverAddress;
				this.challenge = ((random.Next() << 16) ^ random.Next()) ^ (int)Common.Milliseconds;
				this.connectTime = -9999;
				this.connectPacketCount = 0;
				this.Status = ConnectionStatus.Connecting;
			}
			this.actionsQueue.Enqueue(connect);
			await this.connectTCS.Task;
		}
		public void Disconnect() {
			void disconnect() {
				this.StopRecord_f();
				this.connectTCS?.TrySetCanceled();
				if (this.Status >= ConnectionStatus.Connected) {
					this.AddReliableCommand("disconnect", true);
					this.WritePacket();
					this.WritePacket();
					this.WritePacket();
				}
				this.Status = ConnectionStatus.Disconnected;
				this.ClearState();
				this.ClearConnection();
				OnDisconnected(EventArgs.Empty);
			}
			this.actionsQueue.Enqueue(disconnect);
		}
		public static IClientHandler GetKnownClientHandler(ServerInfo serverInfo) {
			if (serverInfo == null) {
				throw new JKClientException(new ArgumentNullException(nameof(serverInfo)));
			}
			return JKClient.GetKnownClientHandler(serverInfo.Protocol, serverInfo.Version);
		}
		public static IClientHandler GetKnownClientHandler(ProtocolVersion protocol, ClientVersion version) {
			switch (protocol) {
			case ProtocolVersion.Protocol25:
			case ProtocolVersion.Protocol26:
				return new JAClientHandler(protocol, version);
			case ProtocolVersion.Protocol15:
			case ProtocolVersion.Protocol16:
				return new JOClientHandler(protocol, version);
			case ProtocolVersion.Protocol68:
			case ProtocolVersion.Protocol71:
				return new Q3ClientHandler(protocol);
			}
			throw new JKClientException($"There isn't any known client handler for given protocol: {protocol}");
		}


		/*
		====================
		WriteDemoMessage

		Dumps the current net message, prefixed by the length
		====================
		*/
		void WriteDemoMessage(Message msg, int headerBytes)
		{
			int len, swlen;

            lock (DemofileLock) {
				if (!Demorecording)
				{
					//Com_Printf("Not recording a demo.\n");
					return;
				}

				// write the packet sequence
				len = serverMessageSequence;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));

				// skip the packet sequencing information
				len = msg.CurSize - headerBytes;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Write(msg.Data, headerBytes, len);
			}
		}

		/*
		====================
		StopRecording_f

		stop recording a demo
		====================
		*/
		public void StopRecord_f()
		{
			int len;

            lock (DemofileLock) {


				if (!Demorecording)
				{
					//Com_Printf("Not recording a demo.\n");
					return;
				}

				// finish up
				len = -1;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Close();
				Demofile.Dispose();
				Demofile = null;
				Demorecording = false;
				//Com_Printf("Stopped demo.\n");
			}
		}


		/*
		==================
		DemoFilename
		==================
		*/
		string DemoFilename()
		{
			return "demo" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
		}

		// firstPacketRecordedTCS in case you want to do anything once the first packet has recorded, like send some command that you want the response recorded of
		public async Task<bool> Record_f(string demoName,TaskCompletionSource<bool> firstPacketRecordedTCS = null)
        {
			if(demoRecordingStartPromise != null)
            {
				return false;
            }

			DemoName = demoName;

			demoRecordingStartPromise = new TaskCompletionSource<bool>();
			if(firstPacketRecordedTCS != null)
            {
				demoFirstPacketRecordedPromise = firstPacketRecordedTCS;
			}

			actionsQueue.Enqueue(()=> {
				demoRecordingStartPromise.TrySetResult(StartRecording(DemoName));
				demoRecordingStartPromise = null;
			});

			return await demoRecordingStartPromise.Task;
		}

		// Demo recording
		private unsafe bool StartRecording(string demoName,bool timeStampDemoname=false)
        {

			if (Demorecording)
			{
				return false;
			}

			if (Status != ConnectionStatus.Active)
			{
				//Com_Printf("You must be in a level to record.\n");
				return false;
			}


            if (timeStampDemoname)
            {
				demoName = DemoFilename();
            }
			string name = "demos/" + demoName + ".dm_" + ((int)Protocol).ToString();
			if (File.Exists(name))
			{
				//Com_Printf("Record: Couldn't create a file\n");
				return false;
			}

            lock (DemofileLock) {

				// open the demo file
				//Com_Printf("recording to %s.\n", name);
				Directory.CreateDirectory("demos");
				Demofile = new FileStream(name,FileMode.CreateNew,FileAccess.Write,FileShare.None);
				/*if (!Demofile)
				{
					Com_Printf("ERROR: couldn't open.\n");
					return;
				}*/
				Demorecording = true;

				this.DemoName = demoName;

				Demowaiting = true;
				//DemoSkipPacket = false;
			

				//byte[] data = new byte[Message.MaxLength];
				byte[] data = new byte[ClientHandler.MaxMessageLength];


				// write out the gamestate message
				var msg = new Message(data, sizeof(byte) * ClientHandler.MaxMessageLength);

				msg.Bitstream();

				// NOTE, MRE: all server->client messages now acknowledge
				msg.WriteLong(reliableSequence);

				msg.WriteByte((int)ServerCommandOperations.Gamestate);
				msg.WriteLong(serverCommandSequence);

				int len;

				// configstrings
				for (int i = 0; i < ClientHandler.MaxConfigstrings; i++)
				{
					if (0 == gameState.StringOffsets[i])
					{
						continue;
					}
					fixed (sbyte* s = this.gameState.StringData)
					{
						sbyte* cs = s + gameState.StringOffsets[i];
						msg.WriteByte((int)ServerCommandOperations.Configstring);
						msg.WriteShort(i);
						len = Common.StrLen(cs);
						byte[] bytes = new byte[len];
						Marshal.Copy((IntPtr)cs, bytes, 0, len);
						msg.WriteBigString((sbyte[])(Array)bytes);
					}
				}

				// baselines
				EntityState nullstate;
				for (int i = 0; i < Common.MaxGEntities; i++)
				{

					fixed(EntityState* ent = &entityBaselines[i])
					{
						if (0 == ent->Number)
						{
							continue;
						}
						msg.WriteByte((int)ServerCommandOperations.Baseline);
						msg.WriteDeltaEntity(&nullstate, ent, true,this.Version,this.ClientHandler);
					}
				}

				int eofOperation = ClientHandler is JOClientHandler ? (int)ServerCommandOperations.EOF -1 : (int)ServerCommandOperations.EOF;
				msg.WriteByte(eofOperation);

				// finished writing the gamestate stuff

				// write the client num
				msg.WriteLong(this.clientNum);
				// write the checksum feed
				msg.WriteLong(this.checksumFeed);

				// finished writing the client packet
				msg.WriteByte(eofOperation);

				// write it to the demo file
				len = this.serverMessageSequence - 1;

				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));

				len = msg.CurSize;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Write(msg.Data, 0, msg.CurSize);

				// the rest of the demo file will be copied from net messages

				return true;

			}

		}

		
	}
}
