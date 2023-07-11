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
using System.Collections.Generic;
using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	class IsExternalInit { }
}

namespace JKClient {

	public class DemoName_t {
		public string name;
		public DateTime time; // For name shenanigans
		public override string ToString()
        {
			return name;
        }
    }

	enum jaPlusClientSupportFlags_t
	{
		CSF_GRAPPLE_SWING = (int)0x00000001u, // Can correctly predict movement when using the grapple hook
		CSF_SCOREBOARD_LARGE = (int)0x00000002u, // Can correctly parse scoreboard messages with information for 32 clients
		CSF_SCOREBOARD_KD = (int)0x00000004u, // Can correctly parse scoreboard messages with extra K/D information
		CSF_CHAT_FILTERS = (int)0x00000008u, // Can correctly parse chat messages with proper delimiters
		CSF_FIXED_WEAPON_ANIMS = (int)0x00000010u, // Fixes the missing concussion rifle animations
		CSF_WEAPONDUEL = (int)0x00000020u,
	}

	public delegate void UserCommandGeneratedEventHandler(object sender, ref UserCommand modifiableCommand);
	public sealed partial class JKClient : NetClient {
		public volatile int SnapOrderTolerance = 100;
		public volatile bool SnapOrderToleranceDemoSkipPackets = false;
		private const int LastPacketTimeOut = 5 * 60000;
		private const int RetransmitTimeOut = 3000;
		private const int MaxPacketUserCmds = 32;
		private const string DefaultName = "AssetslessClient";
		private readonly string jaPlusClientSupportFlagsExplanation = ((int)(jaPlusClientSupportFlags_t.CSF_SCOREBOARD_KD | jaPlusClientSupportFlags_t.CSF_SCOREBOARD_LARGE | jaPlusClientSupportFlags_t.CSF_GRAPPLE_SWING)).ToString("X"); // This is what jaPlusClientSupportFlags is, but I can't do that because ToString() can't be assigned to constant
		private const string jaPlusClientSupportFlags = "7";
		private const string forcePowersJKClientDefault = "7-1-032330000000001333";
		private const string forcePowersAllDark = "200-2-033330333000033333";
		private const string UserInfo = "\\name\\" + JKClient.DefaultName + "\\rate\\200000\\snaps\\1000\\model\\kyle/default\\forcepowers\\"+ forcePowersAllDark + "\\color1\\4\\color2\\4\\handicap\\100\\teamtask\\0\\sex\\male\\password\\\\cg_predictItems\\1\\saber1\\single_1\\saber2\\none\\char_color_red\\255\\char_color_green\\255\\char_color_blue\\255\\engine\\jkclient_demoRec\\cjp_client\\1.4JAPRO\\csf\\"+ jaPlusClientSupportFlags + "\\assets\\0"; // cjp_client: Pretend to be jaPRO for more scoreboard stats
		private readonly Random random = new Random();
		private readonly int port;
		private readonly InfoString userInfo = new InfoString(UserInfo);
		private readonly ConcurrentQueue<Action> actionsQueue = new ConcurrentQueue<Action>();
		private ClientGame clientGame;
		private TaskCompletionSource<bool> connectTCS;

		public Statistics Stats { get; init; } = new Statistics();
        public ClientEntity[] Entities => clientGame != null? clientGame.Entities : null;
        public int playerStateClientNum => snap.PlayerState.ClientNum;
        public PlayerState PlayerState => snap.PlayerState;
        public bool IsInterMission => snap.PlayerState.PlayerMoveType == PlayerMoveType.Intermission;
        public PlayerMoveType PlayerMoveType => snap.PlayerState.PlayerMoveType;
        public int SnapNum => snap.MessageNum;
        public int ServerTime => snap.ServerTime;
		public int gameTime => this.clientGame == null ? 0: this.clientGame.GetGameTime();
        //public PlayerState CurrentPlayerState => clientGame != null? clientGame. : null;
        #region ClientConnection
        public int clientNum { get; private set; } = -1;
		private int lastPacketSentTime = 0;
		private int lastPacketTime = 0;
		private NetAddress serverAddress;
		private int connectTime = -9999;
		private int infoRequestTime = -9999;
		private int connectPacketCount = 0;
		private int challenge = 0;
		private int checksumFeed = 0;
		private int reliableSequence = 0;
		private int reliableAcknowledge = 0;
		private sbyte [][]reliableCommands;
		private int serverMessageSequence = 0;
		private int serverCommandSequence = 0;
		private int lastExecutedServerCommand = 0;
		private int desiredSnaps = 1000;
		private bool clientForceSnaps = false;
		private sbyte [][]serverCommands;
		private NetChannel netChannel;
		#endregion
		#region DemoWriting
		// demo information
		DemoName_t DemoName;
		public string AbsoluteDemoName { get; private set; } = null;
		//bool SpDemoRecording;
		public bool Demorecording { get; private set; }
		private SortedDictionary<int, BufferedDemoMessageContainer> bufferedDemoMessages = new SortedDictionary<int, BufferedDemoMessageContainer>();
		//bool Demoplaying;
		int Demowaiting;   // don't record until a non-delta message is received. Changed to int. 0=not waiting. 1=waiting for delta message with correct deltanum. 2= waiting for full snapshot
		const double DemoRecordBufferedReorderTimeout = 10;
		int DemoLastWrittenSequenceNumber = -1;

		BufferedDemoMessageContainer DemoAfkSnapsDropLastDroppedMessage = null; // With afk snap skipping, we wanna always keep the last one and write it when a change is detected, so that playing the demo doesn't result in unnatural movement from longer interpolation times.
		int DemoAfkSnapsDropLastDroppedMessageNumber = -1;
		bool LastMessageWasDemoAFKDrop = false;
		
		bool DemoSkipPacket;
		bool FirstDemoFrameSkipped;
		TaskCompletionSource<bool> demoRecordingStartPromise = null;
		TaskCompletionSource<bool> demoFirstPacketRecordedPromise = null;
		Mutex DemofileLock = new Mutex();
		FileStream Demofile;
		Int64 DemoLastFullFlush = 0;
		public Int64 DemoFlushInterval = 200 * 1000; // 200 KB. At the very least every 200 KB a write to disk is forced.
#endregion
#region ClientStatic
		private int realTime = 0;
		private string servername;
		public ConnectionStatus Status { get; private set; }
		public IClientHandler ClientHandler => this.NetHandler as IClientHandler;

		public ClientVersion Version => this.ClientHandler.Version;
		
		public event EventHandler<SnapshotParsedEventArgs> SnapshotParsed;
		internal void OnSnapshotParsed(SnapshotParsedEventArgs eventArgs)
        {
			this.SnapshotParsed?.Invoke(this, eventArgs);
		}
		public event UserCommandGeneratedEventHandler UserCommandGenerated;
		internal void OnUserCommandGenerated(ref UserCommand cmd)
        {
			this.UserCommandGenerated?.Invoke(this, ref cmd);
		}
		public event EventHandler<object> DebugEventHappened; // Has nothing to do with q3 engine events. It's just for some special cases to comfortably debug stuff.
		internal void OnDebugEventHappened(object o)
        {
			this.DebugEventHappened?.Invoke(this, o);
		}
		public bool DebugConfigStrings { get; set; } = false;
		public bool DebugNet { get; set; } = false;
		// set to true if you want this client to get stuck as "Connecting"
		public bool GhostPeer { get; init; } = false;
		private StringBuilder showNetString = null;

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
				}/* else if (name.Length > 31) { // Let me choose pls :)
					name = name.Substring(0, 31);
				}*/
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
		public int DesiredSnaps {
			get => this.desiredSnaps;
			set {
				if(this.desiredSnaps != value) { 
					this.desiredSnaps = value;
					this.userInfo["snaps"] = value.ToString();
					this.UpdateUserInfo();
				}
			}
		}
		// Force discard packets that don't respect our DesiredSnaps setting?
		// Many servers don't respect it these days.
		public bool ClientForceSnaps {
			get => this.clientForceSnaps;
			set {
				this.clientForceSnaps = value;
			}
		}

		public bool AfkDropSnaps { get; set; } = false;
		public int AfkDropSnapsMinFPS { get; set; } = 2;
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
				string systemInfoCSStr = this.GetConfigstring(GameState.SystemInfo);
				var info2 = new InfoString(systemInfoCSStr);
				this.serverInfo.Address = this.serverAddress;
				this.serverInfo.Clients = this.serverInfo.ClientsIncludingBots = this.ClientInfo?.Count(ci => ci.InfoValid) ?? 0;
				this.serverInfo.SetConfigstringInfo(info);
				this.serverInfo.SetSystemConfigstringInfo(info2);
				this.ClientHandler.SetExtraConfigstringInfo(this.serverInfo, info);
				return this.serverInfo;
			}
		}
		public event Action<ServerInfo> ServerInfoChanged;
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
		private protected override void OnStop(bool afterFailure) {
			this.connectTCS?.TrySetCanceled();
			this.connectTCS = null;
			this.Status = ConnectionStatus.Disconnected;
			if (afterFailure) {
				this.DequeueActions();
				this.ClearState();
				this.ClearConnection();
			}
			base.OnStop(afterFailure);
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
				this.Stats.lastFrameDelta = msec;
				this.SendCommand();
				this.CheckForResend();
				this.SetTime();
				if (this.Status >= ConnectionStatus.Primed) {
					this.clientGame.Frame(this.serverTime);
				}
				//await Task.Delay(6); // This is taking WAY longer than 3 ms. More like 20-40ms, it's insane
				System.Threading.Thread.Sleep(6); // This ALSO isn't precise (except when debugging ironically?!?! the moment i detach debugger it becomes imprecise and takes forever)
				// Actually it's fine now. I had to increase the timer resolution with the thingie thang
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
				this.ClientHandler.RequestAuthorization(this.CDKey, (address, data2) => {
					this.OutOfBandPrint(address, data2);
				});
				this.OutOfBandPrint(this.serverAddress, "getinfo xxx");
				this.infoRequestTime = this.realTime;
				this.OutOfBandPrint(this.serverAddress, $"getchallenge {this.challenge}");
				break;
			case ConnectionStatus.Challenging:
                if (this.serverInfo.InfoPacketReceived) // Don't challenge until we have serverInfo.
                {
                    if (this.serverInfo.NWH)
                    {
						this.userInfo["engine"] = "demoBot"; // Try not to get influenced by servers blocking JKChat
                    }

					string data = $"connect \"{this.userInfo}\\protocol\\{this.Protocol}\\qport\\{this.port}\\challenge\\{this.challenge}\"";
					this.OutOfBandData(this.serverAddress, data, data.Length);
				} else if (this.realTime - this.infoRequestTime >= JKClient.RetransmitTimeOut) // Maybe the request or answer to the request got lost somewhere... let's ask again.
                {
					this.OutOfBandPrint(this.serverAddress, "getinfo xxx");
					this.infoRequestTime = this.realTime;
				}
				break;
			}
		}
		private unsafe void Encode(in Message msg) {
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
		private unsafe void Decode(in Message msg) {
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


		private unsafe bool MessageCheckSuperSkippable(Message msg, int deltaNum)
        {

			bool isPilot()
			{
				return msg.ReadBits(1) != 0;
			}

			// Do more investigation. See if the delta snapshot contains any new info, or if all entities have just remained exactly same. If there is not a single changed entity or playerstate thingie,
			// mark superSkippable
			int snapFlags = msg.ReadByte();

			// Areabytess
			int len = msg.ReadByte();
			msg.ReadData(null, len);

			// If any fields changed, not super skippable
			var fields = this.ClientHandler.GetPlayerStateFields(false, isPilot);
			int lc = msg.ReadByte();
			if (lc > 0) {
				for (int i = 0; i < lc; i++)
				{
					if (msg.ReadBits(1) != 0)
					{
						if (fields[i].Name != nameof(PlayerState.CommandTime))
						{
							return false;
						} else
                        {
							int bits = fields[i].Bits;
							msg.ReadBits( bits == 0 ? (msg.ReadBits(1) == 0 ? Message.FloatIntBits : 32) : bits); // Very short form of reading a field and discarding it the result.
						}

					}
				}
			}

			// If any additional values/stats changed, not super skippable
			if (msg.ReadBits(1) != 0) return false;

			// Sad, since nothing changed we have to check if the PS we'd be deltaing from has vehiclenum for JKA
			if (this.ClientHandler.CanParseVehicle) 
            {
				var oldSnapHandle = GCHandle.Alloc(this.snapshots, GCHandleType.Pinned);
				var oldSnap = ((ClientSnapshot*)oldSnapHandle.AddrOfPinnedObject()) + (deltaNum & JKClient.PacketMask);
				if (oldSnap->PlayerState.VehicleNum != 0) {
					// If any fields changed, not super skippable
					lc = msg.ReadByte();
					if (lc > 0) {
						var vehFields = this.ClientHandler.GetPlayerStateFields(true, isPilot);
						// Some fields changed
						// We will allow a change for "CommandTime" because that one is updated even if someone is afk.
						for (int i = 0; i < lc; i++)
                        {
							if (msg.ReadBits(1) != 0)
							{
								if (vehFields[i].Name != nameof(PlayerState.CommandTime))
								{
									return false;
								}
								else
								{
									int bits = vehFields[i].Bits;
									msg.ReadBits(bits == 0 ? (msg.ReadBits(1) == 0 ? Message.FloatIntBits : 32) : bits); // Very short form of reading a field and discarding it the result.
								}

							}
						}
					}

					// If any additional values/stats changed, not super skippable
					if (msg.ReadBits(1) != 0) return false;
				}
			}

			// Entities
			int safetyIndex = -1; // Safety index should NEVER be needed but a malformed message could result in this and cause an infinite loop perhaps? Though at that point we likely have other problems...
			var eFields = this.ClientHandler.GetEntityStateFields();
			int entityStateCommandTimeOffset = Marshal.OffsetOf<EntityState>(nameof(EntityState.Position)).ToInt32() + Marshal.OffsetOf<Trajectory>(nameof(Trajectory.Time)).ToInt32();
			while (true && ++safetyIndex <= Common.MaxGEntities)
            {
				int newnum = msg.ReadBits(Common.GEntitynumBits);
				if (newnum == Common.MaxGEntities - 1) break;
				if (msg.ReadBits(1) == 1) return false; // It's a removal, hence a change.
				if (msg.ReadBits(1) == 1) {
					// It has chnaged fields, hence a change.
					// But we might allow commandtime change.
					lc = msg.ReadByte();for (int i = 0; i < lc; i++)
					{
						if (msg.ReadBits(1) != 0)
						{
							if (eFields[i].Offset != entityStateCommandTimeOffset)
							{
								return false;
							}
							else
							{
								int bits = eFields[i].Bits;
								if(msg.ReadBits(1) != 0)
								{
									msg.ReadBits(bits == 0 ? (msg.ReadBits(1) == 0 ? Message.FloatIntBits : 32) : bits);
                                }
							}

						}
					}
				}

			}
			return safetyIndex <= Common.MaxGEntities; // Uh. Hm. If safetyIndex is > Common.MaxGEntities (or anywhere close really) we have some messed up message to deal with, so would be better to discard anyway ig? lol whatever we're not determining that here.

		}

		// Basically: Is this message's first server command a snapshot (indicating no gamestate/commands in this message)?
		// And: Is the snapshot delta? 
		// We don't ever wanna skip messasges with gamestate or commands, or with non-delta messages.
		// But we can skip delta messages to artificially limit snaps.
		// Superskippable: delta snapshot with no changes.
		private bool MessageIsSkippable(in Message msg, int newSnapNum, ref int serverTimeHere, ref bool superSkippable)
        {
			superSkippable = false;
			bool canSkip = false;
			// Pre-parse a bit of the messsage to see if it contains anything but snapshot as first thing
			msg.SaveState();
			msg.Bitstream();
			_ = msg.ReadLong(); // Reliable acknowledge, don't care.
			if (msg.ReadCount > msg.CurSize)
			{
				throw new JKClientException("ParseServerMessage (pre): read past end of server message");
			}
			ServerCommandOperations cmd = (ServerCommandOperations)msg.ReadByte();
			this.ClientHandler.AdjustServerCommandOperations(ref cmd);
			bool newCommands = false;
			if(cmd == ServerCommandOperations.ServerCommand)
            {
				int seq = msg.ReadLong();
				_ = msg.ReadString();
				if (this.serverCommandSequence < seq)
				{
					newCommands = true;
					Stats.messagesUnskippableNewCommands++;
				}
				cmd = (ServerCommandOperations)msg.ReadByte();
			}
            if (!newCommands)
            {
				if (cmd == ServerCommandOperations.Snapshot)
				{
					serverTimeHere = msg.ReadLong();
					int deltaNum = msg.ReadByte();
					int theDeltaNum = 0;
					if (deltaNum == 0)
					{
						theDeltaNum = -1;
					}
					else
					{
						theDeltaNum = newSnapNum - deltaNum;
					}
					if (theDeltaNum > 0)
					{
						Stats.messagesSkippable++;
						canSkip = true; // This is the only situation where we wanna skip. Message contains no gamestate or commands, only snapshot, and it's a delta snapshot.

						superSkippable = MessageCheckSuperSkippable(msg, theDeltaNum);
                        if (superSkippable)
                        {
							Stats.messagesSuperSkippable++;
						}
					}
					else
					{
						Stats.messagesUnskippableNonDelta++;
					}
				}
				else
				{
					Stats.messagesUnskippableSvc++;
				}
			}
			msg.RestoreState();
			return canSkip;
		}

		private protected override unsafe void PacketEvent(in NetAddress address, in Message msg) {
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
				int sequenceNumber =0;
				bool validButOutOfOrder=false;
				bool process = this.netChannel.Process(msg, ref sequenceNumber, ref validButOutOfOrder);
				bool detectSuperSkippable = true;

				// Save to demo queue
				if(process || validButOutOfOrder)
                {
					bool LastMessageWasDemoAFKDropRemember = LastMessageWasDemoAFKDrop;
					LastMessageWasDemoAFKDrop = false;

                    if (!LastMessageWasDemoAFKDropRemember)
                    {
						DemoAfkSnapsDropLastDroppedMessage = null;
						DemoAfkSnapsDropLastDroppedMessageNumber = -1;
                    }

					this.Decode(msg);

					Stats.totalMessages++;
                    if (validButOutOfOrder)
                    {
						Stats.messagesOutOfOrder++;
					}

					// Clientside snaps limiting if requested
					if (process && (clientForceSnaps || AfkDropSnaps))
					{
						int newSnapNum = *(int*)b;
						int newServerTime = 0;

						bool didWeSkipThis = false;
						bool superSkippable = false;
						if (MessageIsSkippable(in msg, newSnapNum, ref newServerTime, ref superSkippable))
						{
							// TODO Measure round trip from client to server and back 
							// Then subtract that from measured PACKET_BACKUP(32)/sv snaps
							// Then give a bit of safety delay (50ms?)
							// and make that the maximum time that is skipped
							// Reason: If we go past the server's PACKET_BACKUP, we start getting non-delta snaps. Bad, those are big and unskippabble.
							// Roundtrip because it seems that just measuring my own ping to Australia for example and duplicating it doesn't yield the correct
							// value to prevent non-delta snaps. The actual fps value can be lower than that number would suggest.
							// How to measure roundtrip? Send a reliable message, remember when it was sent, and see when it is acked.
							bool wasExplicitlyNotSkipped = false;
							int oldServerTime = this.snap.ServerTime;
							int timeDelta = newServerTime - oldServerTime;
							if (clientForceSnaps)
                            {
								int minDelta = 1000 / this.desiredSnaps;

								// Give it a bit of tolerance. 5 percent
								// Because for example snaps 2 will result in time distances like 493 instead of 500 ms.
								// That's technically only 2 percent. But whatever, let's give it 5 percent tolerance.
								// And it's rounded anyway. So it'd only apply with minDelta 20+ ms anyway, which would be 50 fps. So if we requesst 50fps, it might allow 52 fps.
								minDelta -= minDelta / 20;

								if (timeDelta < minDelta)
								{
									didWeSkipThis = true;
									Stats.messagesSkipped++;
									return; // We're skipping this one.
								}
								else
								{
									//Stats.messagesNotSkippedTime++;
									wasExplicitlyNotSkipped = true;
								}
							}
							if(!didWeSkipThis && AfkDropSnaps)
                            {
                                if (superSkippable) { 

									int maxDelta = 1000 / this.AfkDropSnapsMinFPS;
									// Afk works with min fps instead of maxfps. Because we'd love to skip ALL afk messages ofc. 
									// But if we skip too many, we start getting non-delta packs. So find a compromise that makes sense.
									// It also depends on ping and sv_fps/server snaps. Server keeps a certain amount of PACKET_BACKUP.
									// Once server runs out of PACKET_BACKUP, we start getting non-delta snaps, which take up more space.
									// For now, we leave it up to the user to choose a reasonable setting. But we could maybe automate it at some point.
									if(timeDelta <= maxDelta)
									{
										DemoAfkSnapsDropLastDroppedMessage = new BufferedDemoMessageContainer()
										{
											msg = msg.Clone(),
											time = DateTime.Now,
											containsFullSnapshot = false // To be determined
										};
										DemoAfkSnapsDropLastDroppedMessageNumber = sequenceNumber;
										LastMessageWasDemoAFKDrop = true;
										didWeSkipThis = true;
										Stats.messagesSkipped++;
										return; // We're skipping this one.
									}
									else
									{
										//Stats.messagesNotSkippedTime++;
										wasExplicitlyNotSkipped = true;
									}
                                }
								
							}
                            if (wasExplicitlyNotSkipped)
                            {
								Stats.messagesNotSkippedTime++;
							}
						}


						if (LastMessageWasDemoAFKDropRemember && Demorecording && !didWeSkipThis && DemoAfkSnapsDropLastDroppedMessageNumber != -1 && DemoAfkSnapsDropLastDroppedMessage != null)
						{
							lock (bufferedDemoMessages)
							{
								if (bufferedDemoMessages.ContainsKey(DemoAfkSnapsDropLastDroppedMessageNumber))
								{
									// VERY WEIRD. 
								}
								else
								{
									bufferedDemoMessages.Add(DemoAfkSnapsDropLastDroppedMessageNumber, DemoAfkSnapsDropLastDroppedMessage);
									/*if (validButOutOfOrder)
									{
										this.Stats.messagesDropped--;
									}*/ // Hmm might need some handling for better stats when using this afk dropping stuff? Oh well.
								}
							}
						}
					}
					

					if (Demorecording)
					{
						lock (bufferedDemoMessages)
						{
							if (bufferedDemoMessages.ContainsKey(sequenceNumber))
							{
								// VERY WEIRD. 
							}
							else
							{
								bufferedDemoMessages.Add(sequenceNumber, new BufferedDemoMessageContainer()
								{
									msg = msg.Clone(),
									time = DateTime.Now,
									containsFullSnapshot = false // To be determined
								});
                                if (validButOutOfOrder)
                                {
									this.Stats.messagesDropped--;
								}
							}
						}
						
					}
				}


				if (!process)
				{
					return;
				} else
                {
					this.Stats.messagesDropped += this.netChannel.dropped;
				}


				// the header is different lengths for reliable and unreliable messages
				headerBytes = msg.ReadCount;

				this.serverMessageSequence = *(int*)b;
				this.lastPacketTime = this.realTime;
				this.ParseServerMessage(msg);


				//
				// we don't know if it is ok to save a demo message until
				// after we have parsed the frame
				//
				if (Demorecording && Demowaiting==0 && !DemoSkipPacket)
				{
					//WriteDemoMessage(msg, headerBytes);
					WriteBufferedDemoMessages();
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
			if (string.Compare(c, "infoResponse", StringComparison.OrdinalIgnoreCase) == 0)
			{
				// Minimalistic handling. We only need some basic info.
				var info = new InfoString(msg.ReadStringAsString());
				this.serverInfo.Ping = (int)(Common.Milliseconds - serverInfo.Start);
				this.serverInfo.SetInfo(info);
				this.serverInfo.InfoPacketReceived = true;
				this.serverInfo.InfoPacketReceivedTime = DateTime.Now;
			}
			else if (string.Compare(c, "challengeResponse", StringComparison.OrdinalIgnoreCase) == 0) {
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
                if (!this.GhostPeer) {  // Stay in perpetual challenging mode
					if (this.Status != ConnectionStatus.Challenging) {
						return;
					}
					if (address != this.serverAddress) {
						return;
					}
					this.netChannel = new NetChannel(this.net, address, this.port, this.ClientHandler.MaxMessageLength);
					this.Status = ConnectionStatus.Connected;
					this.lastPacketSentTime = -9999;
				}
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
		private void CreateNewCommand()
		{
			if (this.Status < ConnectionStatus.Primed) {
				return;
			}
			int userCmdDelta = this.clServerTime - this.cmds[this.cmdNumber & UserCommand.CommandMask].ServerTime;
			/*if (userCmdDelta==0 && this.clServerTime > 0) // I commented this block out but originally it was userCmdDelta<1 and caused issues. ==0 should be ok but we don't really need this anyway.
            {
				return; // Never let time flow backwards.
			}*/ // actually this seems to do more harm than good. we're already limited with thread.sleep and sometimes servers reset time and we can accidentally get sstuck in MoveNoDelta and record HUGE files.
			this.Stats.lastUserCommandDelta = userCmdDelta;
			this.cmdNumber++;
			this.cmds[this.cmdNumber & UserCommand.CommandMask] = default;
			this.cmds[this.cmdNumber & UserCommand.CommandMask].ServerTime = this.clServerTime;

			OnUserCommandGenerated(ref this.cmds[this.cmdNumber & UserCommand.CommandMask]);
		}
		private void SendCommand() {
			if (this.Status < ConnectionStatus.Connected) {
				return;
			}
			this.CreateNewCommand();
			int oldPacketNum = (this.netChannel.OutgoingSequence - 1) & JKClient.PacketMask;
			int delta = this.realTime - this.outPackets[oldPacketNum].RealTime;
			//if (delta < 10) { // Don't limit this. We're already limiting the main loop.
			if (delta < 1) { // Ok let's not be ridiculous.
				return;
			}
			this.Stats.lastUserPacketDelta = delta;
			this.WritePacket();
		}
		private void WritePacket() {
			if (this.netChannel == null) {
				return;
			}
			lock (this.netChannel) {
				var oldcmd = new UserCommand();
				byte[] data = new byte[this.ClientHandler.MaxMessageLength];
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
					if (!this.snap.Valid || this.serverMessageSequence != this.snap.MessageNum || Demowaiting == 2) {
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
                if (!this.GhostPeer) // As a ghost peer we want to get stuck in CON_CONNECTING forever. Hack for rare circumstances
                {
					this.netChannel.Transmit(msg.CurSize, msg.Data);
					while (this.netChannel.UnsentFragments)
					{
						this.netChannel.TransmitNextFragment();
					}
				}
			}
		}
		private unsafe void AddReliableCommand(string cmd, bool disconnect = false, Encoding encoding = null) {
			int unacknowledged = this.reliableSequence - this.reliableAcknowledge;
			fixed (sbyte *reliableCommand = this.reliableCommands[++this.reliableSequence & (this.MaxReliableCommands-1)]) {
				encoding = encoding ?? Common.Encoding;
				Marshal.Copy(encoding.GetBytes(cmd+'\0'), 0, (IntPtr)(reliableCommand), encoding.GetByteCount(cmd)+1);
			}
		}
		public int GetUnacknowledgedReliableCommandCount()
        {
			return this.reliableSequence - this.reliableAcknowledge;

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
		public Task Connect(in ServerInfo serverInfo) {
			if (serverInfo == null) {
				throw new JKClientException(new ArgumentNullException(nameof(serverInfo)));
			}
			return this.Connect(serverInfo.Address.ToString());
		}
		public async Task Connect(string address) {
			this.connectTCS?.TrySetCanceled();
			this.connectTCS = null;
			var serverAddress = await NetSystem.StringToAddressAsync(address);
			if (serverAddress == null) {
				throw new JKClientException("Bad server address");
			}
			this.connectTCS = new TaskCompletionSource<bool>();
			void connect() {
				this.servername = address;
				this.serverAddress = serverAddress;
				this.challenge = ((random.Next() << 16) ^ random.Next()) ^ (int)Common.Milliseconds;
				this.connectTime = -9999;
				this.infoRequestTime = -9999;
				this.connectPacketCount = 0;
				this.Status = ConnectionStatus.Connecting;
			}
			this.actionsQueue.Enqueue(connect);
			await this.connectTCS.Task;
		}
		public void Disconnect() {
			var status = this.Status;
			this.Status = ConnectionStatus.Disconnected;
			void disconnect() {
				this.StopRecord_f();
				this.connectTCS?.TrySetCanceled();
				this.connectTCS = null;
				if (status >= ConnectionStatus.Connected) {
					this.AddReliableCommand("disconnect", true);
					this.WritePacket();
					this.WritePacket();
					this.WritePacket();
				}
				this.ClearState();
				this.ClearConnection();
				OnDisconnected(EventArgs.Empty);
			}
			this.actionsQueue.Enqueue(disconnect);
		}
		public static IClientHandler GetKnownClientHandler(in ServerInfo serverInfo) {
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
		void WriteDemoMessage(Message msg, int headerBytes,int sequenceNumber)
		{
			int len, swlen;

            lock (DemofileLock) {
				if (!Demorecording)
				{
					//Com_Printf("Not recording a demo.\n");
					return;
				}

				// write the packet sequence
				//len = serverMessageSequence;
				len = sequenceNumber;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));

				// skip the packet sequencing information
				len = msg.CurSize - headerBytes;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Write(msg.Data, headerBytes, len);


				if (demoFirstPacketRecordedPromise != null)
				{
					demoFirstPacketRecordedPromise.SetResult(true); // Just in case the outside code wants to do something particular once actual packets are being recorded.
					demoFirstPacketRecordedPromise = null;
				}
                if ((DemoLastFullFlush + DemoFlushInterval) < Demofile.Position)
                {
					Demofile.Flush(true);
					DemoLastFullFlush = Demofile.Position;
					Stats.demoSizeFullFlushed = DemoLastFullFlush;
				}
				Stats.demoSize = Demofile.Position;
			}
		}

		/*
		====================
		WriteBufferedDemoMessages
		Writes messages from the buffered demo packets map into the demo if they are either 
		follow ups to a previously written messages without a gap or if they are at least the timeout age.
		If called with qtrue parameter, timeout will be ignored and all messages will be flushed and written
		into the demo file.
		====================
		*/
		void WriteBufferedDemoMessages(bool forceWriteAll = false)
		{
            lock (bufferedDemoMessages) { 
				//static msg_t tmpMsg;
				//static byte tmpMsgData[MAX_MSGLEN];
				//tmpMsg.data = tmpMsgData;

				// First write messages that exist without a gap.
				//while (bufferedDemoMessages.find(clc.demoLastWrittenSequenceNumber + 1) != bufferedDemoMessages.end())
				while (bufferedDemoMessages.ContainsKey(DemoLastWrittenSequenceNumber + 1))
				{
					// While we have all the messages without any gaps, we can just dump them all into the demo file.
					Message tmpMsg = bufferedDemoMessages[DemoLastWrittenSequenceNumber + 1].msg;
					WriteDemoMessage(tmpMsg, tmpMsg.ReadCount, DemoLastWrittenSequenceNumber + 1);
					DemoLastWrittenSequenceNumber = DemoLastWrittenSequenceNumber + 1;
					bufferedDemoMessages.Remove(DemoLastWrittenSequenceNumber);
				}

				// Now write messages that are older than the timeout. Also do a bit of cleanup while we're at it.
				// bufferedDemoMessages is a map and maps are ordered, so the key (sequence number) should be incrementing.
				List<int> itemsToErase = new List<int>();
				foreach (KeyValuePair<int,BufferedDemoMessageContainer> tmpMsg in bufferedDemoMessages)
				{
					if (tmpMsg.Key <= DemoLastWrittenSequenceNumber)
					{ // Older or identical number to stuff we already wrote. Discard.
						itemsToErase.Add(tmpMsg.Key);
						continue;
					}
					// First potential candidate.
					//if (forceWriteAll || tmpIt->second.time + cl_demoRecordBufferedReorderTimeout->integer < Com_RealTime(NULL)) {
					if (forceWriteAll || ((DateTime.Now - tmpMsg.Value.time).TotalSeconds) > DemoRecordBufferedReorderTimeout)
					{
						WriteDemoMessage(tmpMsg.Value.msg, tmpMsg.Value.msg.ReadCount, tmpMsg.Key);
						DemoLastWrittenSequenceNumber = tmpMsg.Key;
						itemsToErase.Add(tmpMsg.Key);
					}
					else
					{
						// Not old enough. When there are gaps we want to wait X amount of seconds before writing a new
						// message so that older ones can still arrive.
						break; // Since the messages in the map are ordered, if we're not writing this one, no need to continue.
					}
				}
				foreach(int itemToErase in itemsToErase)
				{
					bufferedDemoMessages.Remove(itemToErase);
				}
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

				WriteBufferedDemoMessages(true); // Flush all messages into the demo file.

				// finish up
				len = -1;
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
				Demofile.Flush(true);
				DemoLastFullFlush = Demofile.Position;
				Demofile.Close();
				Demofile.Dispose();
				Demofile = null;
				DemoLastFullFlush = 0;
				Demorecording = false;
				Demowaiting = 0;
				//Com_Printf("Stopped demo.\n");
				Stats.demoSize = 0;
				Stats.demoSizeFullFlushed = DemoLastFullFlush;

				UpdateDemoTime();
			}
		}


		/*
		==================
		DemoFilename
		==================
		*/
		DemoName_t DemoFilename()
		{
			DateTime now = DateTime.Now;
			return new DemoName_t { name = "demo" + now.ToString("yyyy-MM-dd_HH-mm-ss"), time=now };
		}

		// firstPacketRecordedTCS in case you want to do anything once the first packet has recorded, like send some command that you want the response recorded of
		public async Task<bool> Record_f(DemoName_t demoName,TaskCompletionSource<bool> firstPacketRecordedTCS = null)
        {
			if(demoRecordingStartPromise != null)
            {
				firstPacketRecordedTCS.TrySetResult(false);
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

		public DemoName_t getDemoName()
        {
			return DemoName;
        }

		Message constructMetaMessage()
        {
			byte[] data = new byte[ClientHandler.MaxMessageLength];
			var msg = new Message(data, sizeof(byte) * ClientHandler.MaxMessageLength);
			msg.Bitstream(); 
			msg.WriteLong(reliableSequence);
			StringBuilder sb = new StringBuilder();
			sb.Append("{"); // original filename
			//sb.Append("\"of\":\""); // original filename // Maybe don't do this as someone might use this to record demos and then rename, then it doesn't count as original filename anymore? And we have time after all.
			//sb.Append(this.AbsoluteDemoName);
			//sb.Append("\","); // original start time
			sb.Append("\"wr\":\""); // writer
			sb.Append("jkclient_demoRec"); // jkClient_demoRec
			sb.Append("\","); // original start time
			sb.Append("\"ost\":"); // original start time
			sb.Append(((DateTimeOffset)this.DemoName.time.ToUniversalTime()).ToUnixTimeSeconds());
			if(this.serverAddress != null)
            {
				sb.Append(",\"oip\":\""); // original IP
				sb.Append(this.serverAddress.ToString());
			}
			sb.Append("\"}");
			string metaData = sb.ToString();
			int eofOperation = ClientHandler is JOClientHandler ? (int)ServerCommandOperations.EOF - 1 : (int)ServerCommandOperations.EOF;
			HiddenMetaStuff.createMetaMessage(msg, metaData, eofOperation);
			return msg;
		}


		static Mutex demoUniqueFilenameMutex = new Mutex();

		// Demo recording
		private unsafe bool StartRecording(DemoName_t demoName,bool timeStampDemoname=false)
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

            lock (demoUniqueFilenameMutex) { // Make sure we don't accidentally try writing to an identical filename twice from different instances of the client. It can happen when the timing is really tight on a reconnect and then you get a serious error thrown your way.

				string name = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JKWatcher", "demos/" + demoName + ".dm_" + ((int)Protocol).ToString());
				int filenameIncrement = 2;
				while (File.Exists(name))
				{
					name = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demos/" + demoName + $" ({filenameIncrement++})" + ".dm_" + ((int)Protocol).ToString());

					//Com_Printf("Record: Couldn't create a file\n");
					//return false;
				}

				lock (DemofileLock) {

					// open the demo file
					//Com_Printf("recording to %s.\n", name);
					Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "demos"));
					DemoLastFullFlush = 0;
					Demofile = new FileStream(name,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
					/*if (!Demofile)
					{
						Com_Printf("ERROR: couldn't open.\n");
						return;
					}*/
					Demorecording = true;

					this.AbsoluteDemoName = name;
					this.DemoName = demoName;

					Demowaiting = 2; // request non-delta message with value 2.
					 //DemoSkipPacket = false;
					DemoLastWrittenSequenceNumber = 0;

					int len;

					// Metadata
					Message metaMsg = constructMetaMessage();
					len = this.serverMessageSequence - 2;
					Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
					len = metaMsg.CurSize;
					Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
					Demofile.Write(metaMsg.Data, 0, metaMsg.CurSize);

					//byte[] data = new byte[Message.MaxLength];
					byte[] data = new byte[ClientHandler.MaxMessageLength];

					// write out the gamestate message
					var msg = new Message(data, sizeof(byte) * ClientHandler.MaxMessageLength);

					msg.Bitstream();

					// NOTE, MRE: all server->client messages now acknowledge
					msg.WriteLong(reliableSequence);

					msg.WriteByte((int)ServerCommandOperations.Gamestate);
					msg.WriteLong(serverCommandSequence);


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

                    if (this.ClientHandler is JAClientHandler) // RMG nonsense. Take the easy way for now. Maybe do nicer someday.
					{
						/*// RMG stuff
						if (clc.rmgHeightMapSize)
						{
							int i;

							// Height map
							MSG_WriteShort(&buf, (unsigned short)clc.rmgHeightMapSize );
							MSG_WriteBits(&buf, 0, 1);
							MSG_WriteData(&buf, clc.rmgHeightMap, clc.rmgHeightMapSize);

							// Flatten map
							MSG_WriteShort(&buf, (unsigned short)clc.rmgHeightMapSize );
							MSG_WriteBits(&buf, 0, 1);
							MSG_WriteData(&buf, clc.rmgFlattenMap, clc.rmgHeightMapSize);

							// Seed 
							MSG_WriteLong(&buf, clc.rmgSeed);

							// Automap symbols
							MSG_WriteShort(&buf, (unsigned short)clc.rmgAutomapSymbolCount );
							for (i = 0; i < clc.rmgAutomapSymbolCount; i++)
							{
								MSG_WriteByte(&buf, (unsigned char)clc.rmgAutomapSymbols[i].mType );
								MSG_WriteByte(&buf, (unsigned char)clc.rmgAutomapSymbols[i].mSide );
								MSG_WriteLong(&buf, (long)clc.rmgAutomapSymbols[i].mOrigin[0]);
								MSG_WriteLong(&buf, (long)clc.rmgAutomapSymbols[i].mOrigin[1]);
							}
						}
						else*/
						{

							msg.WriteShort(0);
						}
					}

					// finished writing the client packet
					msg.WriteByte(eofOperation);

					// write it to the demo file
					len = this.serverMessageSequence - 1;

					Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));

					len = msg.CurSize;
					Demofile.Write(BitConverter.GetBytes(len), 0, sizeof(int));
					Demofile.Write(msg.Data, 0, msg.CurSize);

					// the rest of the demo file will be copied from net messages

					Demofile.Flush(true);
					DemoLastFullFlush = Demofile.Position;
					Stats.demoSize = Demofile.Position;
					Stats.demoSizeFullFlushed = DemoLastFullFlush;

					return true;

				}
			}

		}

		
	}
}
