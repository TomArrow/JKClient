using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {

	internal class DemoTimeTracker
    {
		public int DemoCurrentTime = 0;
		public int DemoBaseTime = 0;
		public int DemoStartTime = 0;
		public int LastKnownTime = 0;
	}

	public sealed partial class JKClient {
		private const int PacketBackup = 256;
		private const int PacketMask = (JKClient.PacketBackup-1);
		private const int MaxParseEntities = JKClient.PacketBackup * 64; // this assumes no more than 64 entities per packet on average. is it really reliable? idk. packets can theoretically have even 1024 ents? //2048;
#region ClientActive
		private ClientSnapshot snap = new ClientSnapshot();
		private int serverTime = 0;

		// Need this for proper command timing. Or server will discard commands we send because they have duplicated servertimes
		private int clServerTime = 0; // What we wanna send in commands
		private int clOldServerTime = 0; // What we wanna send in commands
		private int clServerTimeDelta = 0; // What we wanna send in commands
		private bool clExtrapolatedSnapshot = false;
		public int DemoCurrentTimeApproximate => this.demoTimeTrackerApproximate.DemoCurrentTime;
		public int DemoCurrentTimeRealDelayed => this.demoTimeTrackerRealDelayed.DemoCurrentTime; // Due to delayed writing, this value might be a bit old.

		private int? currentDemoWrittenServerTime = null;
		private int? currentDemoWrittenSequenceNumber = null;
		private int? currentDemoMaxSequenceNumber = null;
		DemoTimeTracker demoTimeTrackerApproximate = new DemoTimeTracker();
		DemoTimeTracker demoTimeTrackerRealDelayed = new DemoTimeTracker();


		private long lastServerTimeUpdateTime = 0;
		private int levelStartTime = 0;
		private int oldFrameServerTime = 0;
		private bool newSnapshots = false;

		// delta snap minimum delay related stuff
		private int deltaSnapMaxDelay = 1000; // Maximum amount of milliseconds we can skip messages until we start getting nondeltas. This depends on ping and a lot of other things, so we just automatically adjust it all the time.
		private UInt64 nonDeltaSnapsBitmask = 0;
		private Int64 nonDeltaSnapsBitmaskIndex = 0;
		private DateTime lastDeltaSnapMaxDelayAdjustment = DateTime.Now;
		private bool lastDeltaSnapMaxDelayAdjustmentWasUp = false;

		private GameState gameState = new GameState();
		private int parseEntitiesNum = 0;
		private UserCommand []cmds = new UserCommand[UserCommand.CommandBackup];
		private int cmdNumber = 0;
		private OutPacket []outPackets = new OutPacket[JKClient.PacketBackup];
		private int serverId = 0;
		private ClientSnapshot []snapshots = new ClientSnapshot[JKClient.PacketBackup];
		private EntityState []entityBaselines = new EntityState[Common.MaxGEntities];
		private EntityState []parseEntities = new EntityState[JKClient.MaxParseEntities];
#endregion
		private int MaxConfigstrings => this.ClientHandler.MaxConfigstrings;


		private void UpdateDemoTime()
        {
			if(!this.Demorecording || this.Demowaiting > 0)
            {
				this.currentDemoWrittenServerTime = null;
				this.currentDemoWrittenSequenceNumber = null;
				this.currentDemoMaxSequenceNumber = null;
				this.demoTimeTrackerApproximate.DemoCurrentTime = 0;
				this.demoTimeTrackerApproximate.DemoBaseTime = 0;
				this.demoTimeTrackerApproximate.DemoStartTime = 0;
				this.demoTimeTrackerApproximate.LastKnownTime = this.snap.ServerTime;
				this.demoTimeTrackerRealDelayed.DemoCurrentTime = 0;
				this.demoTimeTrackerRealDelayed.DemoBaseTime = 0;
				this.demoTimeTrackerRealDelayed.DemoStartTime = 0;
				this.demoTimeTrackerRealDelayed.LastKnownTime = this.snap.ServerTime;
				this.Stats.demoCurrentTime = 0;
				return;
            }


			// This is tracking the approximate time based on parsed snapshots, 
			if (this.snap.ServerTime < this.demoTimeTrackerApproximate.LastKnownTime && this.maxSequenceNum == this.serverMessageSequence /*&& this.snap.ServerTime < 10000*/)
			{ // Assume a servertime reset (new serverTime is under 10 secs). 
				this.demoTimeTrackerApproximate.DemoBaseTime = this.demoTimeTrackerApproximate.DemoCurrentTime; // Remember fixed offset into demo time.
				this.demoTimeTrackerApproximate.DemoStartTime = this.snap.ServerTime;
			}

			// This is tracking the real current demotime of messages in the demo, but it's delayed because we don't write messages to the demo immediately, in case we receive them out of order.
			if (this.currentDemoWrittenServerTime.HasValue)
            {
				if (this.currentDemoWrittenServerTime.Value < this.demoTimeTrackerRealDelayed.LastKnownTime && this.currentDemoMaxSequenceNumber == this.currentDemoWrittenSequenceNumber /*&& this.currentDemoWrittenServerTime.Value < 10000*/)
				{ // Assume a servertime reset (new serverTime is under 10 secs). (outdated, instead check that the demo packets are in sequence. They really always should be.)
					this.demoTimeTrackerRealDelayed.DemoBaseTime = this.demoTimeTrackerRealDelayed.DemoCurrentTime; // Remember fixed offset into demo time.
					this.demoTimeTrackerRealDelayed.DemoStartTime = this.currentDemoWrittenServerTime.Value;

					// We set it for the approximate as well! Since it's actually the accurate value for the demo. 
					// Basically the idea is: We do the afk snap skipping. But once we go back to recording, we're dumping the last skipped afk messages as well.
					// Due to this, the approximate tracking down there can get out of sync with the real demo if the server does a map_restart for example, thus putting serverTime back to 0.
					// Hence, we put it back in sync here.
					// This SHOULD always happen AFTER the approximate handling because it's based on messages being written to the demo,
					// which happens (at the earliest) after each packet is parsed
					// Whereas the approximate handling happens after each snapshot parsed.
					// Outside tools trying to find the current demo time for cutting should use the approximate value since it isn't delayed and will likely
					// give a more precise value bassed on wanting to get the demo time at the time of call, not the demo time actually written to the file.
					// So this RealDelayed tracking is simply a help to correct a possible shift in sync for the approximate demo time.
					this.Stats.demoCurrentTimeSyncFix += Math.Abs(this.demoTimeTrackerApproximate.DemoBaseTime - this.demoTimeTrackerRealDelayed.DemoBaseTime);
					this.demoTimeTrackerApproximate.DemoBaseTime = this.demoTimeTrackerRealDelayed.DemoBaseTime;
					this.demoTimeTrackerApproximate.DemoStartTime = this.demoTimeTrackerRealDelayed.DemoStartTime;
				}
				this.demoTimeTrackerRealDelayed.DemoCurrentTime = this.demoTimeTrackerRealDelayed.DemoBaseTime + this.currentDemoWrittenServerTime.Value - this.demoTimeTrackerRealDelayed.DemoStartTime;
				this.demoTimeTrackerRealDelayed.LastKnownTime = this.currentDemoWrittenServerTime.Value;
			}

			// This is tracking the approximate time based on parsed snapshots, 
			this.demoTimeTrackerApproximate.DemoCurrentTime = this.demoTimeTrackerApproximate.DemoBaseTime + this.snap.ServerTime - this.demoTimeTrackerApproximate.DemoStartTime;
			this.demoTimeTrackerApproximate.LastKnownTime = this.snap.ServerTime;
			this.Stats.demoCurrentTime = this.demoTimeTrackerApproximate.DemoCurrentTime;
			this.Stats.demoCurrentTimeWritten = this.demoTimeTrackerRealDelayed.DemoCurrentTime;
		}

		private void ParseServerMessage(in Message msg) {
			bool isMOH = this.ClientHandler is MOHClientHandler;

			if (this.DebugNet && showNetString == null)
            {
				showNetString = new StringBuilder();
            } else if(!this.DebugNet && showNetString!= null)
            {
				showNetString = null;
			} else if(showNetString != null)
            {
				showNetString.Clear();
				showNetString.Append("------------------\n");
            }

			msg.Bitstream();
			this.reliableAcknowledge = msg.ReadLong();
			if (this.reliableAcknowledge < this.reliableSequence - this.MaxReliableCommands) {
				this.reliableAcknowledge = this.reliableSequence;
			}
			bool eof = false;
			ServerCommandOperations cmd;
			ServerCommandOperations oldCmd;
			while (true) {
				if (msg.ReadCount > msg.CurSize) {
					throw new JKClientException("ParseServerMessage: read past end of server message");
				}
				cmd = (ServerCommandOperations)msg.ReadByte();
				this.ClientHandler.AdjustServerCommandOperations(ref cmd);
				if (cmd == ServerCommandOperations.EOF) {
					break;
				}
				//Debug.WriteLine(cmd);
				switch (cmd) {
				case ServerCommandOperations.LocPrint:
				case ServerCommandOperations.CenterPrint:
				case ServerCommandOperations.CGameMessage:
                    if (isMOH)
                    {
						demoCutParseMOHAASVCReal(msg,(ProtocolVersion)this.Protocol,cmd);
					} else
                    {
						throw new JKClientException("ParseServerMessage: Illegible server message");
					}
					break;
				default:
					throw new JKClientException("ParseServerMessage: Illegible server message");
				case ServerCommandOperations.Nop:
					Debug.WriteLine("svc_nop");
					break;
				case ServerCommandOperations.ServerCommand:
					this.ParseCommandString(in msg);
					break;
				case ServerCommandOperations.Gamestate:
					this.ParseGamestate(in msg);
					break;
				case ServerCommandOperations.Snapshot:
					this.ParseSnapshot(in msg);
					this.UpdateDemoTime();
					eof = true;
					break;
				case ServerCommandOperations.SetGame:
//					this.ParseSetGame(in msg);
					eof = true;
					break;
				case ServerCommandOperations.Download:
//					this.ParseDownload(in msg);
					eof = true;
					break;
				case ServerCommandOperations.MapChange:
					break;
				}
				oldCmd = cmd;
				if (eof) {
					break;
				}
			}

			if(showNetString != null)
            {
				this.OnDebugEventHappened(new NetDebug() { debugString = showNetString.ToString() });
            }
		}
		private unsafe void ParseGamestate(in Message msg) {
			bool isMOH = this.ClientHandler is MOHClientHandler;

			this.connectPacketCount = 0;
			this.ClearState();
			this.serverCommandSequence = msg.ReadLong();
			this.gameState.DataCount = 1;
			ServerCommandOperations cmd;
			while (true) {
				cmd = (ServerCommandOperations)msg.ReadByte();
				this.ClientHandler.AdjustServerCommandOperations(ref cmd);
				if (cmd == ServerCommandOperations.EOF) {
					break;
				} else if (cmd == ServerCommandOperations.Configstring) {
					int i = msg.ReadShort();
					if (i < 0 || i > this.MaxConfigstrings) {
						throw new JKClientException("configstring > MaxConfigStrings");
					}
					sbyte []s = msg.ReadBigString(isMOH && this.Protocol > (int)ProtocolVersion.Protocol8);
					int len = Common.StrLen(s);
					if (len + 1 + this.gameState.DataCount > this.ClientHandler.MaxGameStateChars) {
						throw new JKClientException("MaxGameStateChars exceeded");
					}
					string csStr = Common.ToString(s);
					if(showNetString != null)
                    {
						showNetString.Append($"{i}:{csStr}\n");
                    }
					this.ClientHandler.AdjustGameStateConfigstring(i, csStr);
					this.gameState.StringOffsets[i] = this.gameState.DataCount;
					fixed (sbyte *stringData = this.gameState.StringData) {
						Marshal.Copy((byte[])(Array)s, 0, (IntPtr)(stringData+this.gameState.DataCount), len+1);
					}
					this.gameState.DataCount += len + 1;
				} else if (cmd == ServerCommandOperations.Baseline) {
					int newnum = 0;
                    if (isMOH)
                    {
						newnum = msg.ReadEntityNum((ProtocolVersion)Protocol);
					} else
                    {
						newnum = msg.ReadBits(Common.GEntitynumBits);
					}
					if (newnum < 0 || newnum >= Common.MaxGEntities) {
						throw new JKClientException($"Baseline number out of range: {newnum}");
					}
					bool fakeNonDelta = false;
					fixed (EntityState* nesMoh = &EntityState.NullMOH)
					{
						fixed (EntityState* nes = &EntityState.Null)
						{
							fixed (EntityState* bl = &this.entityBaselines[newnum])
							{
								msg.ReadDeltaEntity(isMOH ? nesMoh : nes, bl, newnum, this.ClientHandler,this.serverFrameTime, ref fakeNonDelta, showNetString);
							}
						}
					}
				} else {
					throw new JKClientException("ParseGamestate: bad command byte");
				}
			}
			this.clientNum = msg.ReadLong();
			this.checksumFeed = msg.ReadLong();

            if (isMOH)
            {
				this.serverFrameTime = msg.ReadServerFrameTime((ProtocolVersion)this.Protocol,false,this.GetConfigstring((int)ClientGame.Configstring.ServerInfo));
			}

			if (this.ClientHandler.CanParseRMG) {
				this.ParseRMG(msg);
			}
			this.SystemInfoChanged();

			this.SendPureChecksums();

			this.clientGame = this.InitClientGame();
			this.ServerInfoChanged?.Invoke(this.ServerInfo,true);
		}
		private void ParseRMG(in Message msg) {
			ushort rmgHeightMapSize = (ushort)msg.ReadShort();
			if (rmgHeightMapSize == 0) {
				return;
			}
			if (msg.ReadBits(1) != 0) {
				msg.ReadData(null, rmgHeightMapSize);
			} else {
				msg.ReadData(null, rmgHeightMapSize);
			}
			ushort size = (ushort)msg.ReadShort();
			if (msg.ReadBits(1) != 0) {
				msg.ReadData(null, size);
			} else {
				msg.ReadData(null, size);
			}
			int rmgSeed = msg.ReadLong();
			ushort rmgAutomapSymbolCount = (ushort)msg.ReadShort();
			for (int i = 0; i < rmgAutomapSymbolCount; i++) {
				msg.ReadByte();
				msg.ReadByte();
				msg.ReadLong();
				msg.ReadLong();
			}
		}
		private void SystemInfoChanged() {
			string systemInfo = this.GetConfigstring(GameState.SystemInfo);
			var info = new InfoString(systemInfo);
			this.serverId = info["sv_serverid"].Atoi();
			if (info["sv_pure"].Atoi() != 0) {
				serverIsPure = true;
				//throw new JKClientException("Cannot connect to a pure server without assets");
			}  else
            {
				serverIsPure = false;
            }
		}

		internal unsafe string GetConfigstring(in int index) {
			if (index < 0 || index >= this.MaxConfigstrings) {
				throw new JKClientException($"Configstring: bad index: {index}");
			}
			fixed (sbyte* s = this.gameState.StringData) {
				sbyte* cs = s + this.gameState.StringOffsets[index];
				return Common.ToString(cs, Common.StrLen(cs));
			}
		}
		public unsafe string GetMappedConfigstring(ClientGame.Configstring indexA) {
			if (this.clientGame == null) return "";
			int index = (int)indexA < 2 ? (int)indexA : this.clientGame.GetConfigstringIndex(indexA); // Values under 2 (ServerInfo, SystemInfo) don't need mapping.
			if (index < 0 || index >= this.MaxConfigstrings) {
				throw new JKClientException($"Configstring: bad index: {index}");
			}
			fixed (sbyte* s = this.gameState.StringData) {
				sbyte* cs = s + this.gameState.StringOffsets[index];
				return Common.ToString(cs, Common.StrLen(cs));
			}
		}
		private unsafe void ClearState()
		{
			
			this.snap = new ClientSnapshot();
			this.serverTime = 0;
			this.clServerTime = 0;
			this.clOldServerTime = 0;
			this.clServerTimeDelta = 0;
			this.clExtrapolatedSnapshot = false;
			//this.DemoCurrentTime = 0;
			//this.DemoBaseTime = 0;
			//this.DemoStartTime = 0;
			//this.LastKnownTime = 0;
			this.lastServerTimeUpdateTime = 0;
			this.levelStartTime = 0;
			this.oldFrameServerTime = 0;
			this.serverTimeOlderThanPreviousCount = 0;
			this.newSnapshots = false;
			this.nonDeltaSnapsBitmask = 0;
			this.deltaSnapMaxDelay = 1000;
			this.nonDeltaSnapsBitmaskIndex = 0;
			this.lastDeltaSnapMaxDelayAdjustmentWasUp = false;
			fixed (GameState* gs = &this.gameState)
			{
				Common.MemSet(gs, 0, sizeof(GameState));
			}
			if(!(this.ClientHandler is MOHClientHandler)) {
				// CRINGE
				// I'm doing something wrong I think
				// MOHAA demos just keep referencing old snaps, idk why. So I need to keep that info
				// until I figure out wtf is going on.
				this.parseEntitiesNum = 0;
				Common.MemSet(this.cmds, 0, sizeof(UserCommand) * UserCommand.CommandBackup);
				this.cmdNumber = 0;
				Common.MemSet(this.outPackets, 0, sizeof(OutPacket) * JKClient.PacketBackup);
				this.serverId = 0;
				Common.MemSet(this.snapshots, 0, sizeof(ClientSnapshot) * JKClient.PacketBackup);
				Common.MemSet(this.entityBaselines, 0, sizeof(EntityState) * Common.MaxGEntities);
				Common.MemSet(this.parseEntities, 0, sizeof(EntityState) * JKClient.MaxParseEntities);
				this.clientGame = null;
				this.ClientHandler.ClearState();
			}
			
		}
		private void ClearConnection() {
			this.StopRecord_f();
			for (int i = 0; i < this.ClientHandler.MaxReliableCommands; i++) {
				Common.MemSet(this.serverCommands[i], 0, sizeof(sbyte)*Common.MaxStringCharsMOH);
				this.serverCommandMessagenums[i] = 0;
				Common.MemSet(this.reliableCommands[i], 0, sizeof(sbyte)*Common.MaxStringCharsMOH);
			}
			this.clientNum = -1;
			this.lastPacketSentTime = 0;
			this.lastPacketTime = 0;
			this.serverAddress = null;
			this.connectTime = 0;
			this.mohConnectTimeExtraDelay = 0;
			this.infoRequestTime = 0;
			this.connectPacketCount = 0;
			this.challenge = 0;
			this.checksumFeed = 0;
			this.serverFrameTime = 0;
			this.reliableSequence = 0;
			this.reliableAcknowledge = 0;
			this.serverMessageSequence = 0;
			this.maxSequenceNum = 0;
			this.serverCommandSequence = 0;
			this.lastExecutedServerCommand = 0;
			this.netChannel = null;

			this.messageIntervals = new int[messageIntervalsMeasureCount];
			this.lastMessageReceivedTime = 0;
			this.messageIntervalMeasurementIndex = 0;
			this.messageIntervalAverage = 1000;

			this.Demowaiting = 0;
			this.Demorecording = false;
			this.bufferedDemoMessages.Clear();
			this.DemoLastWrittenSequenceNumber = -1;
			this.DemoAfkSnapsDropLastDroppedMessage = null;
			this.DemoAfkSnapsDropLastDroppedMessageNumber = -1;
			this.LastMessageWasDemoAFKDrop = false;
			this.DemoName = null;
			this.AbsoluteDemoName = null;
			this.Demofile = null;
			this.DemoSkipPacket = false;
			if(this.demoRecordingStartPromise != null)
            {
				this.demoRecordingStartPromise.TrySetResult(false);
				this.demoRecordingStartPromise = null;
			}
			if (this.demoFirstPacketRecordedPromise != null)
			{
				this.demoFirstPacketRecordedPromise.TrySetResult(false);
				this.demoFirstPacketRecordedPromise = null;
			}
		}
		private void ParseCommandString(in Message msg) {
			int seq = msg.ReadLong();
			sbyte []s = msg.ReadString((ProtocolVersion)this.Protocol);
			if (this.serverCommandSequence >= seq) {
				return;
			}
			if(showNetString != null)
            {
				showNetString.Append($"{seq}: {Common.ToString(s)}\n");
            }
			this.serverCommandSequence = seq;
			int index = seq & (this.MaxReliableCommands-1);
			Array.Copy(s, 0, this.serverCommands[index], 0, Common.MaxStringCharsMOH);
			this.serverCommandMessagenums[index] = this.serverMessageSequence;
		}


		int serverTimeOlderThanPreviousCount = 0; // Count of snaps received with a lower servertime than the old snap we have. Should be a static function variable but that doesn't exist in C#

		private unsafe void ParseSnapshot(in Message msg) {

			bool isMOH = this.ClientHandler is MOHClientHandler;

			bool isFakeNonDelta = false;

			ClientSnapshot *oldSnap;
			var oldSnapHandle = GCHandle.Alloc(this.snapshots, GCHandleType.Pinned);
			var newSnap = new ClientSnapshot() {
				ServerCommandNum = this.serverCommandSequence,
				ServerTime = msg.ReadLong(),
				ServerTimeResidual = isMOH ? msg.ReadByte() : 0, // MOH thing. I hope this maintains the order.
				MessageNum = this.serverMessageSequence
			};

			//Debug.WriteLine(newSnap.ServerTime);
			//Debug.WriteLine(newSnap.ServerTimeResidual);

			lock (bufferedDemoMessages)
			{
				if (bufferedDemoMessages.ContainsKey(this.serverMessageSequence))
				{
					bufferedDemoMessages[this.serverMessageSequence].serverTime = newSnap.ServerTime;
				}
			}

			// Sometimes packets arrive out of order. We want to tolerate this a bit to tolerate bad internet connections.
			// However if it happens a large amount of times in a row, it might indicate a game restart/map chance I guess?
			// So let the cvar cl_snapOrderTolerance decide how many times we allow it.
			if (newSnap.ServerTime < this.oldFrameServerTime)
			{
				//Com_Printf("WARNING: newSnap.serverTime < cl.oldFrameServerTime.\n");
				serverTimeOlderThanPreviousCount++;
			}
			else
			{
				serverTimeOlderThanPreviousCount = 0;
			}

			int deltaNum = msg.ReadByte();

			if(deltaNum > 127)
            {
				if (PingAdjust != 0)
				{
					int originaldeltanum = deltaNum;
					/*// With pingadjust we can sometimes accidentally confirm a packet that wasn't even sent yet, resulting a wrap around,
					// which can result in a deltaNum like 255 (actually -1). 
					deltaNum = (sbyte)(byte)deltaNum;
					// First let's get the actual true signed deltaNum by casting to sbyte
					// We must then consider that any server usually has a PACKET_BACKUP of 32 max
					// Our own rotary array has a higher PACKET_BACKUP so we cannot blindly apply the same deltaNum.
					// Ok examples:
					// deltaNum is -1: So we assume that the source message num is the current snap + 1.
					// So in our rotary array we would go +1. 
					int tmpSourceSnapNum = newSnap.MessageNum - deltaNum;
					// But what we really need to ask is: What number would it be in the rotary array on the server?
					// Let's say the number is 33. With our PACKET_BACKUP of 256, we actually have an array index of 33.
					// But on the server, 33 wraps around to 1. 
					// So while on our client we would be trying to actually reference snapshot 33, the server is referencing snapshot 1.
					// So let's now figure out the array index that the server likely used:
					int serverArrayIndexDelta = tmpSourceSnapNum & 31;
					// And now we can try to guess which snapshot num was stored on the server in that index.
					// We can make a relatively safe assumption that it is the last number smaller than newSnap.MessageNum which fits into that index.
					// Let's figure out the array index in which THIS current snapshot is stored on the server:
					int serverArrayIndexCurrent = newSnap.MessageNum & 31;
					int referencedMsgNum = 0;
					if (serverArrayIndexDelta <= serverArrayIndexCurrent)
					{
						// If the current array index is higher than the referenced one, we can simply subtract the difference.
						referencedMsgNum = newSnap.MessageNum - serverArrayIndexCurrent + serverArrayIndexDelta;
					}
                    else
					{
						// If it is lower, we must go back to index 0 by subtracting the current array index, and then subtract the amount of frames we'd have to go back to reach the desired index.
						// Let's say the current index is 0 already, and the desired index is 31.
						// Then we have to subtract the current index (0), aka subtract nothing.
						// And then we must subtract 1. We arrive at 1 by saying 32-desiredIndex(31) = 1.
						referencedMsgNum = newSnap.MessageNum - serverArrayIndexCurrent - (32- serverArrayIndexDelta);
					}
					// Now we know the referenced message num and can correct the deltaNum.
					deltaNum = newSnap.MessageNum - referencedMsgNum;*/

					// All of the above is nice and all but it can be simplified a lot actually...
					deltaNum = deltaNum & 31; // Gives the same results and is much simpler.

					if(deltaNum == 0)
                    {
						Debug.WriteLine($"ParseSnapshot: deltaNum > 127 with pingadjust: {originaldeltanum}, fixing to {deltaNum} which is ZERO, YIKES!!!!");
					} else
					{
						Debug.WriteLine($"ParseSnapshot: deltaNum > 127 with pingadjust: {originaldeltanum}, fixing to {deltaNum}");
					}
				}
				else
                {

					Debug.WriteLine($"ParseSnapshot: deltaNum > 127: {deltaNum}");
				}
			}

			//Debug.WriteLine(deltaNum);
			if (deltaNum == 0) {
				newSnap.DeltaNum = -1;
			} else {
				newSnap.DeltaNum = newSnap.MessageNum - deltaNum;
			}
			newSnap.Flags = msg.ReadByte();
			//Debug.WriteLine(newSnap.Flags);
			if (newSnap.DeltaNum <= 0) {
				newSnap.Valid = true;
				oldSnap = null;
				lock (bufferedDemoMessages)
				{
					if (bufferedDemoMessages.ContainsKey(this.serverMessageSequence))
					{
						bufferedDemoMessages[this.serverMessageSequence].containsFullSnapshot = true;
					}
				}
				if (Demowaiting == 2)
				{
					Demowaiting = 1;    // now we wait for a delta snapshot that references this or another buffered full snapshot.
				}
				/*if (Demowaiting)
				{
					// This is in case we use the buffered reordering of packets for demos. We want to remember the last sequenceNum we wrote to the demo.
					// Here we just save a fake number of the message before this so that *this* message does get saved.
					DemoLastWrittenSequenceNumber = clc.serverMessageSequence - 1;
				}
				Demowaiting = false;   // we can start recording now*/
			}
			else {
				oldSnap = ((ClientSnapshot *)oldSnapHandle.AddrOfPinnedObject()) + (newSnap.DeltaNum & JKClient.PacketMask);
				if (!oldSnap->Valid) {

				} else if (oldSnap->MessageNum != newSnap.DeltaNum) {

				} else if (this.parseEntitiesNum - oldSnap->ParseEntitiesNum > JKClient.MaxParseEntities-128) {

				} else {
					newSnap.Valid = true;
				}

				// Demo recording stuff.
				if (Demowaiting == 1 && newSnap.Valid)
				{
					lock (bufferedDemoMessages)
					{
						if (bufferedDemoMessages.ContainsKey(newSnap.DeltaNum))
						{
							if (bufferedDemoMessages[newSnap.DeltaNum].containsFullSnapshot)
							{
								// Okay NOW we can start recording the demo.
								Demowaiting = 0;
								// This is in case we use the buffered reordering of packets for demos. We want to remember the last sequenceNum we wrote to the demo.
								// Here we just save a fake number of the message before the referenced full snapshot so that saving begins at that full snapshot that is being correctly referenced by the server.
								//
								DemoLastWrittenSequenceNumber = newSnap.DeltaNum - 1;
								// Short explanation: 
								// The old system merely waited for a full snapshot to start writing the demo.
								// However, at that point the server has not yet received an ack for that full snapshot we received.
								// Sometimes the server does not receive this ack (in time?) and as a result it keeps referencing
								// older snapshots including delta snapshots that are not part of our demo file.
								// So instead, we do a two tier system. First we request a full snapshot. Then we wait for a delta
								// snapshot that correctly references the full snapshot. THEN we start recording the demo, starting
								// exactly at the snapshot that we finally know the server knows we received.

								this.demoTimeTrackerRealDelayed.DemoStartTime = this.demoTimeTrackerApproximate.DemoStartTime = bufferedDemoMessages[newSnap.DeltaNum].serverTime.Value;
							}
							else
							{
								Demowaiting = 2; // Nah. It's referencing a delta snapshot. We need it to reference a full one. Request another full one.
							}
						}
						else
						{
							// We do not have this referenced snapshot buffered. Request a new full snapshot.
							Demowaiting = 2;
						}
					}
				}


			}

			// Ironically, to be more tolerant of bad internet, we set the (possibly) out of order snap to invalid. 
			// That way it will not be saved to cl.snap and cause a catastrophic failure/disconnect unless it happens
			// at least cl_snapOrderTolerance times in a row.
			if (serverTimeOlderThanPreviousCount > 0 && serverTimeOlderThanPreviousCount <= SnapOrderTolerance)
			{
				// TODO handle demowaiting better?
				newSnap.Valid = false; 
				if (SnapOrderToleranceDemoSkipPackets)
				{
					DemoSkipPacket = true;
				}
				//Debug.Print("WARNING: Snapshot servertime lower than previous snap. Ignoring %d/%d.\n", serverTimeOlderThanPreviousCount, cl_snapOrderTolerance->integer);
			}

			int len = msg.ReadByte();

			//Debug.WriteLine(len);
			//if (len > sizeof(byte)*32) {
			//oldSnapHandle.Free();
			//throw new JKClientException("ParseSnapshot: Invalid size %d for areamask");
			//}

			msg.ReadData(null, len);
			if (this.ClientHandler.CanParseSnapshot()) {
				msg.ReadDeltaPlayerstate(oldSnap != null ? &oldSnap->PlayerState : null, &newSnap.PlayerState, false, this.ClientHandler,ref isFakeNonDelta, showNetString);
				if (this.ClientHandler.CanParseVehicle && newSnap.PlayerState.VehicleNum != 0) {
					msg.ReadDeltaPlayerstate(oldSnap != null ? &oldSnap->VehiclePlayerState : null, &newSnap.VehiclePlayerState, true, this.ClientHandler, ref isFakeNonDelta, showNetString);
				}
				this.ParsePacketEntities(in msg, in oldSnap, &newSnap, ref isFakeNonDelta);

                if (isMOH)
                {
					ServerSound[] sounds = new ServerSound[64];
					fixed(ServerSound* soundP = sounds)
                    {
						msg.ReadSounds(soundP,&newSnap.numberOfSounds);
					}
                }
			}
			oldSnapHandle.Free();
            if (isFakeNonDelta)
            {
				if(deltaNum == 0)
				{
					Stats.fakeNonDeltaSnaps++;
				} else
                {
					Stats.corruptDeltaSnaps++;
                }
            }
			if (!newSnap.Valid) {
				return;
			}
			int oldMessageNum = this.snap.MessageNum + 1;
			if (newSnap.MessageNum - oldMessageNum >= JKClient.PacketBackup) {
				oldMessageNum = newSnap.MessageNum - JKClient.PacketMask;
			}
			for (;oldMessageNum < newSnap.MessageNum; oldMessageNum++) {
				this.snapshots[oldMessageNum & JKClient.PacketMask].Valid = false;
			}


			newSnap.ping = 999;
			// calculate ping time
			for (int i = 0; i < JKClient.PacketBackup; i++)
			{
				int packetNum = (this.netChannel.OutgoingSequence - 1 - i) & (JKClient.PacketBackup - 1);
				if (this.snap.PlayerState.CommandTime >= this.outPackets[packetNum].ServerTime)
				{
					newSnap.ping = this.realTime - this.outPackets[packetNum].RealTime;
					break;
				}
			}

			this.snap = newSnap;
			this.snapshots[this.snap.MessageNum & JKClient.PacketMask] = this.snap;
			this.newSnapshots = true;
			this.lastServerTimeUpdateTime = Common.Milliseconds;

			this.nonDeltaSnapsBitmaskIndex++;
			if (oldSnap == null)
            {
				this.nonDeltaSnapsBitmask |= (1UL << (int)(this.nonDeltaSnapsBitmaskIndex % 64));
				Stats.nonDeltaSnaps++;
			} else
            {
				this.nonDeltaSnapsBitmask &= ~(1UL << (int)(this.nonDeltaSnapsBitmaskIndex % 64));
				Stats.deltaSnaps++;
			}


			if (this.SnapshotParsed?.GetInvocationList().Length > 0)
            {
				Snapshot eventSnapsshot = new Snapshot();
				(this as IJKClientImport).GetSnapshot(this.snap.MessageNum, ref eventSnapsshot);
				this.OnSnapshotParsed(new SnapshotParsedEventArgs(eventSnapsshot, this.snap.MessageNum));
			}
		}
		private unsafe void ParsePacketEntities(in Message msg, in ClientSnapshot *oldSnap, in ClientSnapshot *newSnap, ref bool fakeNonDelta) {
			bool mohEntityNumSubtract = this.ClientHandler is MOHClientHandler && this.Protocol > (int)ProtocolVersion.Protocol8;
			newSnap->ParseEntitiesNum = this.parseEntitiesNum;
			newSnap->NumEntities = 0;
			EntityState *oldstate;
			var oldstateHandle = GCHandle.Alloc(this.parseEntities, GCHandleType.Pinned);
			IntPtr debugAddress = oldstateHandle.AddrOfPinnedObject();

			int oldindex = 0;
			int oldnum;
			int newnum = msg.ReadBits(Common.GEntitynumBits);
            if (mohEntityNumSubtract)
            {
				newnum = (ushort)(newnum - 1) % Common.MaxGEntities;
			}
			while (true) {
				if (oldSnap != null && oldindex < oldSnap->NumEntities) {
					oldstate = ((EntityState *)oldstateHandle.AddrOfPinnedObject()) + ((oldSnap->ParseEntitiesNum + oldindex) & (JKClient.MaxParseEntities-1));
					oldnum = oldstate->Number;
				} else {
					oldstate = null;
					oldnum = 99999;
				}
				fixed (EntityState *newstate = &this.parseEntities[this.parseEntitiesNum & (JKClient.MaxParseEntities - 1)]) {
					if (oldstate == null && (newnum == (Common.MaxGEntities-1))) {
						break;
					} else if (oldnum < newnum) {
						*newstate = *oldstate;
						oldindex++;
					} else if (oldnum == newnum) {
						oldindex++;
						if(newnum == (Common.MaxGEntities - 1))
                        { // debugging. apparently one of these can happen.
							IntPtr debugAddress2 = oldstateHandle.AddrOfPinnedObject();
							IntPtr debugAddress3 = (IntPtr)newstate;
							OnErrorMessageCreated($"Wtf, I'm oldnum == newnum but also newnum == (Common.MaxGEntities - 1); oldindex {oldindex}, oldnum {oldnum}, newnum {newnum}, newState->number {newstate->Number}, oldSnap->ParseEntitiesNum {(oldSnap != null ? oldSnap->ParseEntitiesNum : -9999999)}, oldindex {oldindex}, this.parseEntitiesNum {this.parseEntitiesNum}, newSnap->NumEntities {newSnap->NumEntities}, oldSnap->MessageNum {(oldSnap is null ? -99999 : oldSnap->MessageNum)}, oldSnap->DeltaNum {(oldSnap is null ? -99999 : oldSnap->DeltaNum)}, newSnap->MessageNum {newSnap->MessageNum}, newSnap->DeltaNum {newSnap->DeltaNum}, debugAddress {debugAddress}, debugAddress2 {debugAddress2}, debugAddress3 {debugAddress3}",null,msg.MakePublicCopy());
                        }
						msg.ReadDeltaEntity(oldstate, newstate, newnum, this.ClientHandler, this.serverFrameTime,ref fakeNonDelta, showNetString);
						newnum = msg.ReadBits(Common.GEntitynumBits);
						if (mohEntityNumSubtract)
						{
							newnum = (ushort)(newnum - 1) % Common.MaxGEntities;
						}
					} else if (oldnum > newnum) {
						if(newnum == (Common.MaxGEntities - 1))
						{ // debugging. apparently one of these can happen.
							IntPtr debugAddress2 = oldstateHandle.AddrOfPinnedObject();
							IntPtr debugAddress3 = (IntPtr)newstate;
							OnErrorMessageCreated($"Wtf, I'm oldnum > newnum but also newnum == (Common.MaxGEntities - 1); oldindex {oldindex}, oldnum {oldnum}, newnum {newnum}, newState->number {newstate->Number}, oldSnap->ParseEntitiesNum {(oldSnap != null ? oldSnap->ParseEntitiesNum : -9999999)}, oldindex {oldindex}, this.parseEntitiesNum {this.parseEntitiesNum}, newSnap->NumEntities {newSnap->NumEntities}, oldSnap->MessageNum {(oldSnap is null ? -99999 : oldSnap->MessageNum)}, oldSnap->DeltaNum {(oldSnap is null ? -99999 : oldSnap->DeltaNum)}, newSnap->MessageNum {newSnap->MessageNum}, newSnap->DeltaNum {newSnap->DeltaNum}, debugAddress {debugAddress}, debugAddress2 {debugAddress2}, debugAddress3 {debugAddress3}",null, msg.MakePublicCopy());
                        }
						fixed (EntityState *bl = &this.entityBaselines[newnum]) {
							msg.ReadDeltaEntity(bl, newstate, newnum, this.ClientHandler, this.serverFrameTime, ref fakeNonDelta, showNetString);
						}
						newnum = msg.ReadBits(Common.GEntitynumBits);
						if (mohEntityNumSubtract)
						{
							newnum = (ushort)(newnum - 1) % Common.MaxGEntities;
						}
					}

					if (msg.ReadCount > msg.CurSize)
					{
						msg.CreateErrorMessage($"ParsePacketEntities: end of message; oldindex {oldindex}, oldnum {oldnum}, newnum {newnum}, newState->number {newstate->Number}");
						throw new JKClientException($"ParsePacketEntities: end of message; oldindex {oldindex}, oldnum {oldnum}, newnum {newnum}, newState->number {newstate->Number}");
					}

					if (newstate->Number == Common.MaxGEntities-1)
						continue;

					this.parseEntitiesNum++;
					//this.parseEntitiesNum &= (JKClient.MaxParseEntities-1);
					newSnap->NumEntities++;
				}
			}
			oldstateHandle.Free();
		}
		private void ParseSetGame(in Message msg) {
			int i = 0;
			while (i < 64) {
				int next = msg.ReadByte();
				if (next != 0) {

				} else {
					break;
				}
				i++;
			}
		}
		private unsafe void ParseDownload(in Message msg) {
			ushort block = (ushort)msg.ReadShort();
			if (block == 0) {
				int downloadSize = msg.ReadLong();
				if (downloadSize < 0) {
					fixed (sbyte* s = msg.ReadString((ProtocolVersion)this.Protocol)) {
						byte* ss = (byte*)s;
						throw new JKClientException($"{Common.ToString(ss, sizeof(sbyte)*Common.MaxStringCharsMOH)}");
					}
				}
			}
			int size = msg.ReadShort();
			if (size < 0 || size > sizeof(byte)*this.ClientHandler.MaxMessageLength) {
				throw new JKClientException($"ParseDownload: Invalid size {size} for download chunk");
			}
			msg.ReadData(null, size);
		}
























		// MOHAA stuff. Really ugly. Just pretend it's not here, we just need it to get through the demo message. Not even doing anything with this data.

		// MOHAA: 
		// TODO: Do something with these values so they don't get lost. (Important?)
		void CL_ParseLocationprint(Message msg, ProtocolVersion protocol)
		{
			int x, y;

			x = msg.ReadShort();
			y = msg.ReadShort();
			//string = msg.ReadScrambledString();
			msg.ReadString(protocol);

			//UI_UpdateLocationPrint(x, y, string, 1.0);
		}

		void CL_ParseCenterprint(Message msg, ProtocolVersion protocol)
		{
			//char* string;

			//string = msg.ReadScrambledString();
			msg.ReadString(protocol);

			// FIXME
			//UI_UpdateCenterPrint(string, 1.0);
		}
		const int MAX_IMPACTS = 64;
		unsafe void CG_ParseCGMessage_ver_15(Message msg, ProtocolVersion protocol)
		{
			int i;
			int iType;
			int iLarge;
			int iInfo;
			int iCount;
			//char* szTmp;
			float[] vStart = new float[3];
			float[] vEnd = new float[3];
			float[] vTmp = new float[3];
			float[,] vEndArray = new float[MAX_IMPACTS,3];
			//vec3_t vStart, vEnd, vTmp;
			//vec3_t vEndArray[MAX_IMPACTS];
			float alpha;

			bool bMoreCGameMessages = true;
			while (bMoreCGameMessages)
			{
				iType = msg.ReadBits( 6);

				switch (iType)
				{
					case 1:
					case 2:
					case 5:
						if (iType == 1)
						{
							vTmp[0] = msg.ReadCoord();
							vTmp[1] = msg.ReadCoord();
							vTmp[2] = msg.ReadCoord();
						}
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();

						if (iType != 1)
						{
							vTmp[0] = vStart[0];
							vTmp[1] = vStart[1];
							vTmp[2] = vStart[2];
						}

						vEndArray[0,0] = msg.ReadCoord();
						vEndArray[0,1] = msg.ReadCoord();
						vEndArray[0,2] = msg.ReadCoord();
						iLarge = msg.ReadBits( 2);
						if (msg.ReadBits( 1) > 0)
						{
							int iAlpha = msg.ReadBits( 10);
							alpha = (float)iAlpha / 512.0f;
							if (alpha < 0.002f)
							{
								alpha = 0.002f;
							}
						}
						else
						{
							alpha = 1.0f;
						}

						if (iType == 1)
						{
							//CG_MakeBulletTracer(vTmp, vStart, vEndArray, 1, iLarge, qfalse, qtrue, alpha);
						}
						else if (iType == 2)
						{
							//CG_MakeBulletTracer(vTmp, vStart, vEndArray, 1, iLarge, qfalse, qtrue, alpha);
						}
						else
						{
							//CG_MakeBubbleTrail(vStart, vEndArray[0], iLarge, alpha);
						}

						break;
					case 3:
					case 4:
						if (iType == 3)
						{
							vTmp[0] = msg.ReadCoord();
							vTmp[1] = msg.ReadCoord();
							vTmp[2] = msg.ReadCoord();
							iInfo = msg.ReadBits( 6);
						}
						else
						{
							iInfo = 0;
						}

						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						iLarge = msg.ReadBits( 2);
						if (0 < msg.ReadBits( 1))
						{
							int iAlpha = msg.ReadBits( 10);
							alpha = (float)iAlpha / 512.0f;
							if (alpha < 0.002f)
							{
								alpha = 0.002f;
							}
						}
						else
						{
							alpha = 1.0f;
						}

						iCount = msg.ReadBits( 6);
						for (i = 0; i < iCount; ++i)
						{
							vEndArray[i,0] = msg.ReadCoord();
							vEndArray[i,1] = msg.ReadCoord();
							vEndArray[i,2] = msg.ReadCoord();
						}

						if (iCount != 0)
						{
							//CG_MakeBulletTracer(vTmp, vStart, vEndArray, iCount, iLarge, iInfo, qtrue, alpha);
						}
						break;
					case 6:
					case 7:
					case 8:
					case 9:
					case 10:
					case 11:
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						fixed(float* p = vEnd)
                        {
							msg.ReadDir(p);
						}
						iLarge = msg.ReadBits( 2);

						/*switch (iType) {
						case 6:
							if (wall_impact_count < MAX_IMPACTS) {
								VectorCopy(vStart, wall_impact_pos[wall_impact_count]);
								VectorCopy(vEnd, wall_impact_norm[wall_impact_count]);
								wall_impact_large[wall_impact_count] = iLarge;
								wall_impact_type[wall_impact_count] = -1;
								wall_impact_count++;
							}
							break;
						case 7:
							if (wall_impact_count < MAX_IMPACTS) {
								VectorCopy(vStart, wall_impact_pos[wall_impact_count]);
								VectorCopy(vEnd, wall_impact_norm[wall_impact_count]);
								wall_impact_large[wall_impact_count] = iLarge;
								wall_impact_type[wall_impact_count] = 6;
								wall_impact_count++;
							}
							break;
						case 8:
							if (flesh_impact_count < MAX_IMPACTS) {
								// negative
								VectorNegate(vEnd, vEnd);
								VectorCopy(vStart, flesh_impact_pos[flesh_impact_count]);
								VectorCopy(vEnd, flesh_impact_norm[flesh_impact_count]);
								flesh_impact_large[flesh_impact_count] = iLarge;
								flesh_impact_count++;
							}
							break;
						case 9:
							if (flesh_impact_count < MAX_IMPACTS) {
								// negative
								VectorNegate(vEnd, vEnd);
								VectorCopy(vStart, flesh_impact_pos[flesh_impact_count]);
								VectorCopy(vEnd, flesh_impact_norm[flesh_impact_count]);
								flesh_impact_large[flesh_impact_count] = iLarge;
								flesh_impact_count++;
							}
							break;
						case 10:
							if (wall_impact_count < MAX_IMPACTS) {
								VectorCopy(vStart, wall_impact_pos[wall_impact_count]);
								VectorCopy(vEnd, wall_impact_norm[wall_impact_count]);
								wall_impact_large[wall_impact_count] = iLarge;
								wall_impact_type[wall_impact_count] = 2;
								wall_impact_count++;
							}
							break;
						case 11:
							if (wall_impact_count < MAX_IMPACTS) {
								VectorCopy(vStart, wall_impact_pos[wall_impact_count]);
								VectorCopy(vEnd, wall_impact_norm[wall_impact_count]);
								wall_impact_large[wall_impact_count] = iLarge;
								wall_impact_type[wall_impact_count] = 4;
								wall_impact_count++;
							}
							break;
						default:
							continue;
						}*/
						break;

					case 12:
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						vEnd[0] = msg.ReadCoord();
						vEnd[1] = msg.ReadCoord();
						vEnd[2] = msg.ReadCoord();
						//CG_MeleeImpact(vStart, vEnd);
						break;
					case 13:
					case 14:
					case 15:
					case 16:
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						//CG_MakeExplosionEffect(vStart, iType);
						break;
					case 18:
					case 19:
					case 20:
					case 21:
					case 22:
					case 23:
					case 24:
					case 25:
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
                        fixed (float *p = vEnd)
                        {
							msg.ReadDir(p);
						}

						//sfxManager.MakeEffect_Normal(iType + SFX_EXP_GREN_PUDDLE, vStart, vEnd);
						break;

					case 26:
					case 27:
						{
							//str    sEffect;
							//char cTmp[8];
							//vec3_t axis[3];

							vStart[0] = msg.ReadCoord();
							vStart[1] = msg.ReadCoord();
							vStart[2] = msg.ReadCoord();
							iLarge = msg.ReadByte();
							// get the integer as string
							//snprintf(cTmp, sizeof(cTmp), "%d", iLarge);

							if (iType == 26)
							{
								//sEffect = "models/fx/crates/debris_";
							}
							else
							{
								//sEffect = "models/fx/windows/debris_";
							}

							//sEffect += cTmp;
							//sEffect += ".tik";

							//VectorSet(axis[0], 0, 0, 1);
							//VectorSet(axis[1], 0, 1, 0);
							//VectorSet(axis[2], 1, 0, 0);

							//cgi.R_SpawnEffectModel(sEffect.c_str(), vStart, axis);
						}
						break;

					case 28:
						vTmp[0] = msg.ReadCoord();
						vTmp[1] = msg.ReadCoord();
						vTmp[2] = msg.ReadCoord();
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						vEndArray[0,0] = msg.ReadCoord();
						vEndArray[0,1] = msg.ReadCoord();
						vEndArray[0,2] = msg.ReadCoord();
						iLarge = msg.ReadBits( 2);
						if (0 != msg.ReadBits( 1))
						{
							int iAlpha = msg.ReadBits( 10);
							alpha = (float)iAlpha / 512.0f;
							if (alpha < 0.002f)
							{
								alpha = 0.002f;
							}
						}
						else
						{
							alpha = 1.0f;
						}

						//CG_MakeBulletTracer(vTmp, vStart, vEndArray, 1, iLarge, qtrue, qtrue, alpha);
						break;

					case 29:
						//memset(vTmp, 0, sizeof(vTmp));
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						vEndArray[0,0] = msg.ReadCoord();
						vEndArray[0,1] = msg.ReadCoord();
						vEndArray[0,2] = msg.ReadCoord();
						iLarge = msg.ReadBits( 1);
						if (0!=msg.ReadBits( 1))
						{
							int iAlpha = msg.ReadBits( 10);
							alpha = (float)iAlpha / 512.0f;
							if (alpha < 0.002f)
							{
								alpha = 0.002f;
							}
						}
						else
						{
							alpha = 1.0f;
						}

						//CG_MakeBulletTracer(vTmp, vStart, vEndArray, 1, iLarge, qfalse, qtrue, alpha);
						break;

					case 30:
						iInfo = msg.ReadByte();
						msg.ReadString( protocol,true);//strcpy(cgi.HudDrawElements[iInfo].shaderName, msg.ReadString());
													  //cgi.HudDrawElements[iInfo].string[0] = 0;
													  //cgi.HudDrawElements[iInfo].pFont = NULL;
													  //cgi.HudDrawElements[iInfo].fontName[0] = 0;
													  // set the shader
													  //CG_HudDrawShader(iInfo);
						break;

					case 31:
						iInfo = msg.ReadByte();
						msg.ReadBits( 2);//cgi.HudDrawElements[iInfo].iHorizontalAlign = msg.ReadBits(2);
						msg.ReadBits( 2);//cgi.HudDrawElements[iInfo].iVerticalAlign = msg.ReadBits(2);
						break;

					case 32:
						iInfo = msg.ReadByte();
						msg.ReadShort();//cgi.HudDrawElements[iInfo].iX = msg.ReadShort();
						msg.ReadShort();//cgi.HudDrawElements[iInfo].iY = msg.ReadShort();
						msg.ReadShort();//cgi.HudDrawElements[iInfo].iWidth = msg.ReadShort();
						msg.ReadShort();//cgi.HudDrawElements[iInfo].iHeight = msg.ReadShort();
						break;

					case 33:
						iInfo = msg.ReadByte();
						msg.ReadBits( 1);//cgi.HudDrawElements[iInfo].bVirtualScreen = msg.ReadBits(1);
						break;

					case 34:
						iInfo = msg.ReadByte();
						msg.ReadByte();//cgi.HudDrawElements[iInfo].vColor[0] = msg.ReadByte() / 255.0;
						msg.ReadByte();//cgi.HudDrawElements[iInfo].vColor[1] = msg.ReadByte() / 255.0;
						msg.ReadByte();//cgi.HudDrawElements[iInfo].vColor[2] = msg.ReadByte() / 255.0;
						break;

					case 35:
						iInfo = msg.ReadByte();
						msg.ReadByte();//cgi.HudDrawElements[iInfo].vColor[3] = msg.ReadByte() / 255.0;
						break;

					case 36:
						iInfo = msg.ReadByte();
						//cgi.HudDrawElements[iInfo].hShader = 0;
						msg.ReadString(protocol, true);//strcpy(cgi.HudDrawElements[iInfo].string, msg.ReadString());
						break;

					case 37:
						iInfo = msg.ReadByte();
						msg.ReadString(protocol, true);//strcpy(cgi.HudDrawElements[iInfo].fontName, msg.ReadString());
													  //cgi.HudDrawElements[iInfo].hShader = 0;
													  //cgi.HudDrawElements[iInfo].shaderName[0] = 0;
													  // load the font
													  //CG_HudDrawFont(iInfo);
						break;

					case 38:
					case 39:
						{
							int iOldEnt;

							//iOldEnt = current_entity_number;
							//current_entity_number = cg.snap->ps.clientNum;
							//if (iType == 36) {
							//	commandManager.PlaySound("dm_kill_notify", NULL, CHAN_LOCAL, 2.0, -1, -1, 1);
							//}
							//else {
							//	commandManager.PlaySound("dm_hit_notify", NULL, CHAN_LOCAL, 2.0, -1, -1, 1);
							//}

							//current_entity_number = iOldEnt;
						}
						break;

					case 40:
						{
							int iOldEnt;

							vStart[0] = msg.ReadCoord();
							vStart[1] = msg.ReadCoord();
							vStart[2] = msg.ReadCoord();
							iLarge = msg.ReadBits( 1);
							iInfo = msg.ReadBits( 6);
							//szTmp = msg.ReadString(protocol);
							msg.ReadString(protocol, true);

							//iOldEnt = current_entity_number;

							//if (iLarge) {
							//	current_entity_number = iInfo;
							//
							//	commandManager.PlaySound(szTmp, vStart, CHAN_LOCAL, -1, -1, -1, 0);
							//}
							//else {
							//	current_entity_number = cg.snap->ps.clientNum;
							//
							//	commandManager.PlaySound(szTmp, vStart, CHAN_AUTO, -1, -1, -1, 1);
							//}

							//current_entity_number = iOldEnt;
						}
						break;
					case 41:
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						vEnd[0] = msg.ReadCoord();
						vEnd[1] = msg.ReadCoord();
						vEnd[2] = msg.ReadCoord();
						msg.ReadByte();
						msg.ReadByte();
						//VectorSubtract(vEnd, vStart, vTmp);

						// FIXME: unimplemented
						// ?? can't figure out what is this
						break;
					default:
						//cgi.Error(ERR_DROP, "CG_ParseCGMessage: Unknown CGM message type");
						break;
				}

				bMoreCGameMessages = 0!=msg.ReadBits( 1);
			}
		}

		unsafe void CG_ParseCGMessage_ver_6(Message msg, ProtocolVersion protocol)
		{
			int i;
			int iType;
			int iLarge;
			int iInfo;
			int iCount;
			//char* szTmp;
			float[] vStart = new float[3];
			float[] vEnd = new float[3];
			float[] vTmp = new float[3];
			float[,] vEndArray = new float[MAX_IMPACTS, 3];
			//vec3_t vStart, vEnd, vTmp;
			//vec3_t vEndArray[MAX_IMPACTS];

			bool bMoreCGameMessages = true;
			while (bMoreCGameMessages)
			{
				iType = msg.ReadBits( 6);

				switch (iType)
				{
					case 1:
					case 2:
					case 5:
						if (iType == 1)
						{
							vTmp[0] = msg.ReadCoord();
							vTmp[1] = msg.ReadCoord();
							vTmp[2] = msg.ReadCoord();
						}
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();

						if (iType != 1)
						{
							vTmp[0] = vStart[0];
							vTmp[1] = vStart[1];
							vTmp[2] = vStart[2];
						}

						vEndArray[0,0] = msg.ReadCoord();
						vEndArray[0,1] = msg.ReadCoord();
						vEndArray[0,2] = msg.ReadCoord();
						iLarge = msg.ReadBits( 1);

						if (iType == 1)
						{
							//CG_MakeBulletTracer(vTmp, vStart, vEndArray, 1, iLarge, qfalse, qtrue);
						}
						else if (iType == 2)
						{
							//CG_MakeBulletTracer(vTmp, vStart, vEndArray, 1, iLarge, qfalse, qtrue);
						}
						else
						{
							//CG_MakeBubbleTrail(vStart, vEndArray[0], iLarge);
						}

						break;
					case 3:
					case 4:
						if (iType == 3)
						{
							vTmp[0] = msg.ReadCoord();
							vTmp[1] = msg.ReadCoord();
							vTmp[2] = msg.ReadCoord();
							iInfo = msg.ReadBits( 6);
						}
						else
						{
							iInfo = 0;
						}

						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						iLarge = msg.ReadBits( 1);
						iCount = msg.ReadBits( 6);
						for (i = 0; i < iCount; ++i)
						{
							vEndArray[i,0] = msg.ReadCoord();
							vEndArray[i,1] = msg.ReadCoord();
							vEndArray[i,2] = msg.ReadCoord();
						}

						//if (iCount)
						{
							//CG_MakeBulletTracer(vTmp, vStart, vEndArray, iCount, iLarge, iInfo, qtrue);
						}
						break;
					case 6:
					case 7:
					case 8:
					case 9:
					case 10:
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						fixed(float* p = vEnd)
                        {

							msg.ReadDir(p);
						}
						iLarge = msg.ReadBits( 1);

						switch (iType)
						{
							case 6:
								//if (wall_impact_count < MAX_IMPACTS) {
								//	VectorCopy(vStart, wall_impact_pos[wall_impact_count]);
								//	VectorCopy(vEnd, wall_impact_norm[wall_impact_count]);
								//	wall_impact_large[wall_impact_count] = iLarge;
								//	wall_impact_type[wall_impact_count] = 0;
								//	wall_impact_count++;
								//}
								break;
							case 7:
								//if (flesh_impact_count < MAX_IMPACTS) {
								// negative
								//	VectorNegate(vEnd, vEnd);
								//	VectorCopy(vStart, flesh_impact_pos[flesh_impact_count]);
								//	VectorCopy(vEnd, flesh_impact_norm[flesh_impact_count]);
								//	flesh_impact_large[flesh_impact_count] = iLarge;
								//	flesh_impact_count++;
								//}
								break;
							case 8:
								//if (flesh_impact_count < MAX_IMPACTS) {
								// negative
								//	VectorNegate(vEnd, vEnd);
								//	VectorCopy(vStart, flesh_impact_pos[flesh_impact_count]);
								//	VectorCopy(vEnd, flesh_impact_norm[flesh_impact_count]);
								//	flesh_impact_large[flesh_impact_count] = iLarge;
								//	flesh_impact_count++;
								//}
								break;
							case 9:
								//if (wall_impact_count < MAX_IMPACTS) {
								//	VectorCopy(vStart, wall_impact_pos[wall_impact_count]);
								//	VectorCopy(vEnd, wall_impact_norm[wall_impact_count]);
								//	wall_impact_large[wall_impact_count] = iLarge;
								//	wall_impact_type[wall_impact_count] = (iLarge != 0) + 2;
								//	wall_impact_count++;
								//}
								break;
							case 10:
								//if (wall_impact_count < MAX_IMPACTS) {
								//	VectorCopy(vStart, wall_impact_pos[wall_impact_count]);
								//	VectorCopy(vEnd, wall_impact_norm[wall_impact_count]);
								//	wall_impact_large[wall_impact_count] = iLarge;
								//	wall_impact_type[wall_impact_count] = (iLarge != 0) + 4;
								//	wall_impact_count++;
								//}
								break;
							default:
								continue;
						}
						break;

					case 11:
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						vEnd[0] = msg.ReadCoord();
						vEnd[1] = msg.ReadCoord();
						vEnd[2] = msg.ReadCoord();
						//CG_MeleeImpact(vStart, vEnd);
						break;
					case 12:
					case 13:
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						//CG_MakeExplosionEffect(vStart, iType);
						break;
					case 15:
					case 16:
					case 17:
					case 18:
					case 19:
					case 20:
					case 21:
					case 22:
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						fixed(float* p= vEnd)
                        {
							msg.ReadDir(p);
						}

						//sfxManager.MakeEffect_Normal(iType + SFX_EXP_GREN_PUDDLE, vStart, vEnd);
						break;

					case 23:
					case 24:
						{
							//str    sEffect;
							//char cTmp[8];
							//vec3_t axis[3];

							vStart[0] = msg.ReadCoord();
							vStart[1] = msg.ReadCoord();
							vStart[2] = msg.ReadCoord();
							iLarge = msg.ReadByte();
							// get the integer as string
							//snprintf(cTmp, sizeof(cTmp), "%d", iLarge);

							if (iType == 23)
							{
								//sEffect = "models/fx/crates/debris_";
							}
							else
							{
								//sEffect = "models/fx/windows/debris_";
							}

							//sEffect += cTmp;
							//sEffect += ".tik";

							//VectorSet(axis[0], 0, 0, 1);
							//VectorSet(axis[1], 0, 1, 0);
							//VectorSet(axis[2], 1, 0, 0);

							//cgi.R_SpawnEffectModel(sEffect.c_str(), vStart, axis);
						}
						break;

					case 25:
						vTmp[0] = msg.ReadCoord();
						vTmp[1] = msg.ReadCoord();
						vTmp[2] = msg.ReadCoord();
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						vEndArray[0,0] = msg.ReadCoord();
						vEndArray[0,1] = msg.ReadCoord();
						vEndArray[0,2] = msg.ReadCoord();
						iLarge = msg.ReadBits( 1);

						//CG_MakeBulletTracer(vTmp, vStart, vEndArray, 1, iLarge, qtrue, qtrue);
						break;

					case 26:
						//memset(vTmp, 0, sizeof(vTmp));
						vStart[0] = msg.ReadCoord();
						vStart[1] = msg.ReadCoord();
						vStart[2] = msg.ReadCoord();
						vEndArray[0,0] = msg.ReadCoord();
						vEndArray[0,1] = msg.ReadCoord();
						vEndArray[0,2] = msg.ReadCoord();
						iLarge = msg.ReadBits( 1);

						//CG_MakeBulletTracer(vTmp, vStart, vEndArray, 1, iLarge, qfalse, qtrue);
						break;

					case 27:
						iInfo = msg.ReadByte();
						msg.ReadString( protocol);//strcpy(cgi.HudDrawElements[iInfo].shaderName, msg.ReadString());
													  //cgi.HudDrawElements[iInfo].string[0] = 0;
													  //cgi.HudDrawElements[iInfo].pFont = NULL;
													  //cgi.HudDrawElements[iInfo].fontName[0] = 0;
													  // set the shader
													  //CG_HudDrawShader(iInfo);
						break;

					case 28:
						iInfo = msg.ReadByte();
						msg.ReadBits( 2); //cgi.HudDrawElements[iInfo].iHorizontalAlign = msg.ReadBits(2);
						msg.ReadBits( 2);  // cgi.HudDrawElements[iInfo].iVerticalAlign = msg.ReadBits(2);
						break;

					case 29:
						iInfo = msg.ReadByte();
						msg.ReadShort();//cgi.HudDrawElements[iInfo].iX = msg.ReadShort();
						msg.ReadShort();//cgi.HudDrawElements[iInfo].iY = msg.ReadShort();
						msg.ReadShort();//cgi.HudDrawElements[iInfo].iWidth = msg.ReadShort();
						msg.ReadShort();//cgi.HudDrawElements[iInfo].iHeight = msg.ReadShort();
						break;

					case 30:
						iInfo = msg.ReadByte();
						msg.ReadBits( 1);//cgi.HudDrawElements[iInfo].bVirtualScreen = msg.ReadBits(1);
						break;

					case 31:
						iInfo = msg.ReadByte();
						msg.ReadByte();//cgi.HudDrawElements[iInfo].vColor[0] = msg.ReadByte() / 255.0;
						msg.ReadByte();//cgi.HudDrawElements[iInfo].vColor[1] = msg.ReadByte() / 255.0;
						msg.ReadByte();//cgi.HudDrawElements[iInfo].vColor[2] = msg.ReadByte() / 255.0;
						break;

					case 32:
						iInfo = msg.ReadByte();
						msg.ReadByte();//cgi.HudDrawElements[iInfo].vColor[3] = msg.ReadByte() / 255.0;
						break;

					case 33:
						iInfo = msg.ReadByte();
						//cgi.HudDrawElements[iInfo].hShader = 0;
						msg.ReadString(protocol);//strcpy(cgi.HudDrawElements[iInfo].string, msg.ReadString());
						break;

					case 34:
						iInfo = msg.ReadByte();
						msg.ReadString(protocol);//strcpy(cgi.HudDrawElements[iInfo].fontName, msg.ReadString());
													  //cgi.HudDrawElements[iInfo].hShader = 0;
													  //cgi.HudDrawElements[iInfo].shaderName[0] = 0;
													  // load the font
													  //CG_HudDrawFont(iInfo);
						break;

					case 35:
					case 36:
						{
							//int iOldEnt;

							//iOldEnt = current_entity_number;
							//current_entity_number = cg.snap->ps.clientNum;
							//if (iType == 36) {
							//	commandManager.PlaySound("dm_kill_notify", NULL, CHAN_LOCAL, 2.0, -1, -1, 1);
							//}
							//else {
							//	commandManager.PlaySound("dm_hit_notify", NULL, CHAN_LOCAL, 2.0, -1, -1, 1);
							//}

							//current_entity_number = iOldEnt;
						}
						break;

					case 37:
						{
							int iOldEnt;

							vStart[0] = msg.ReadCoord();
							vStart[1] = msg.ReadCoord();
							vStart[2] = msg.ReadCoord();
							iLarge = msg.ReadBits( 1);
							iInfo = msg.ReadBits( 6);
							//szTmp = msg.ReadString(protocol);
							msg.ReadString(protocol);

							//iOldEnt = current_entity_number;

							//if (iLarge) {
							//	current_entity_number = iInfo;
							//
							//	commandManager.PlaySound(szTmp, vStart, CHAN_LOCAL, -1, -1, -1, 0);
							//}
							//else {
							//	current_entity_number = cg.snap->ps.clientNum;
							//
							//	commandManager.PlaySound(szTmp, vStart, CHAN_AUTO, -1, -1, -1, 1);
							//}

							//current_entity_number = iOldEnt;
						}
						break;
					default:
						//cgi.Error(ERR_DROP, "CG_ParseCGMessage: Unknown CGM message type");
						break;
				}

				bMoreCGameMessages = 0 != msg.ReadBits( 1);
			}
		}

		void CL_ParseCGMessageMOHAA(Message msg, ProtocolVersion protocol)
		{
			//cl_currentMSG = msg;
			//cge->CG_ParseCGMessage();
			if (protocol> ProtocolVersion.Protocol8)
			{
				CG_ParseCGMessage_ver_15(msg,protocol);
			}
			else
			{
				CG_ParseCGMessage_ver_6(msg,protocol);
			}

		}

		bool demoCutParseMOHAASVCReal(Message msg, ProtocolVersion protocol, ServerCommandOperations cmd)
		{
			switch (cmd)
			{

				case ServerCommandOperations.CenterPrint:
					CL_ParseCenterprint(msg,protocol);
					break;
				case ServerCommandOperations.LocPrint:
					CL_ParseLocationprint(msg, protocol);
					break;
				case ServerCommandOperations.CGameMessage:
					CL_ParseCGMessageMOHAA(msg, protocol);
					break;
			}
			return true;
		}



	}
}
