using System;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {
	public sealed partial class JKClient {
		private const int PacketBackup = 32;
		private const int PacketMask = (JKClient.PacketBackup-1);
		private const int MaxParseEntities = 2048;
#region ClientActive
		private ClientSnapshot snap = new ClientSnapshot();
		private int serverTime = 0;
		private int levelStartTime = 0;
		private int oldFrameServerTime = 0;
		private bool newSnapshots = false;
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
		private void ParseServerMessage(in Message msg) {
			msg.Bitstream();
			this.reliableAcknowledge = msg.ReadLong();
			if (this.reliableAcknowledge < this.reliableSequence - this.MaxReliableCommands) {
				this.reliableAcknowledge = this.reliableSequence;
			}
			bool eof = false;
			ServerCommandOperations cmd;
			while (true) {
				if (msg.ReadCount > msg.CurSize) {
					throw new JKClientException("ParseServerMessage: read past end of server message");
				}
				cmd = (ServerCommandOperations)msg.ReadByte();
				this.ClientHandler.AdjustServerCommandOperations(ref cmd);
				if (cmd == ServerCommandOperations.EOF) {
					break;
				}
				switch (cmd) {
				default:
					throw new JKClientException("ParseServerMessage: Illegible server message");
				case ServerCommandOperations.Nop:
					break;
				case ServerCommandOperations.ServerCommand:
					this.ParseCommandString(in msg);
					break;
				case ServerCommandOperations.Gamestate:
					this.ParseGamestate(in msg);
					break;
				case ServerCommandOperations.Snapshot:
					this.ParseSnapshot(in msg);
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
				if (eof) {
					break;
				}
			}
		}
		private unsafe void ParseGamestate(in Message msg) {
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
					sbyte []s = msg.ReadBigString();
					int len = Common.StrLen(s);
					if (len + 1 + this.gameState.DataCount > GameState.MaxGameStateChars) {
						throw new JKClientException("MaxGameStateChars exceeded");
					}
					string csStr = Common.ToString(s);
					this.ClientHandler.AdjustGameStateConfigstring(i, csStr);
					this.gameState.StringOffsets[i] = this.gameState.DataCount;
					fixed (sbyte *stringData = this.gameState.StringData) {
						Marshal.Copy((byte[])(Array)s, 0, (IntPtr)(stringData+this.gameState.DataCount), len+1);
					}
					this.gameState.DataCount += len + 1;
				} else if (cmd == ServerCommandOperations.Baseline) {
					int newnum = msg.ReadBits(Common.GEntitynumBits);
					if (newnum < 0 || newnum >= Common.MaxGEntities) {
						throw new JKClientException($"Baseline number out of range: {newnum}");
					}
					fixed (EntityState *nes = &EntityState.Null) {
						fixed (EntityState *bl = &this.entityBaselines[newnum]) {
							msg.ReadDeltaEntity(nes, bl, newnum, this.ClientHandler);
						}
					}
				} else {
					throw new JKClientException("ParseGamestate: bad command byte");
				}
			}
			this.clientNum = msg.ReadLong();
			this.checksumFeed = msg.ReadLong();
			if (this.ClientHandler.CanParseRMG) {
				this.ParseRMG(msg);
			}
			this.SystemInfoChanged();
			this.clientGame = this.InitClientGame();
			this.ServerInfoChanged?.Invoke(this.ServerInfo);
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
				throw new JKClientException("Cannot connect to a pure server without assets");
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
			int index = this.clientGame.GetConfigstringIndex(indexA);
			if (index < 0 || index >= this.MaxConfigstrings) {
				throw new JKClientException($"Configstring: bad index: {index}");
			}
			fixed (sbyte* s = this.gameState.StringData) {
				sbyte* cs = s + this.gameState.StringOffsets[index];
				return Common.ToString(cs, Common.StrLen(cs));
			}
		}
		private unsafe void ClearState() {
			this.snap = new ClientSnapshot();
			this.serverTime = 0;
			this.levelStartTime = 0;
			this.oldFrameServerTime = 0;
			this.serverTimeOlderThanPreviousCount = 0;
			this.newSnapshots = false;
			fixed (GameState *gs = &this.gameState) {
				Common.MemSet(gs, 0, sizeof(GameState));
			}
			this.parseEntitiesNum = 0;
			Common.MemSet(this.cmds, 0, sizeof(UserCommand)*UserCommand.CommandBackup);
			this.cmdNumber = 0;
			Common.MemSet(this.outPackets, 0, sizeof(OutPacket)*JKClient.PacketBackup);
			this.serverId = 0;
			Common.MemSet(this.snapshots, 0, sizeof(ClientSnapshot)*JKClient.PacketBackup);
			Common.MemSet(this.entityBaselines, 0, sizeof(EntityState)*Common.MaxGEntities);
			Common.MemSet(this.parseEntities, 0, sizeof(EntityState)*JKClient.MaxParseEntities);
			this.clientGame = null;
			this.ClientHandler.ClearState();
		}
		private void ClearConnection() {
			for (int i = 0; i < this.ClientHandler.MaxReliableCommands; i++) {
				Common.MemSet(this.serverCommands[i], 0, sizeof(sbyte)*Common.MaxStringChars);
				Common.MemSet(this.reliableCommands[i], 0, sizeof(sbyte)*Common.MaxStringChars);
			}
			this.clientNum = -1;
			this.lastPacketSentTime = 0;
			this.lastPacketTime = 0;
			this.serverAddress = null;
			this.connectTime = 0;
			this.connectPacketCount = 0;
			this.challenge = 0;
			this.checksumFeed = 0;
			this.reliableSequence = 0;
			this.reliableAcknowledge = 0;
			this.serverMessageSequence = 0;
			this.serverCommandSequence = 0;
			this.lastExecutedServerCommand = 0;
			this.netChannel = null;

			this.Demowaiting = 0;
			this.Demorecording = false;
			this.bufferedDemoMessages.Clear();
			this.DemoLastWrittenSequenceNumber = -1;
			this.DemoName = "";
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
			sbyte []s = msg.ReadString();
			if (this.serverCommandSequence >= seq) {
				return;
			}
			this.serverCommandSequence = seq;
			int index = seq & (this.MaxReliableCommands-1);
			Array.Copy(s, 0, this.serverCommands[index], 0, Common.MaxStringChars);
		}


		int serverTimeOlderThanPreviousCount = 0; // Count of snaps received with a lower servertime than the old snap we have. Should be a static function variable but that doesn't exist in C#

		private unsafe void ParseSnapshot(in Message msg) {
			ClientSnapshot *oldSnap;
			var oldSnapHandle = GCHandle.Alloc(this.snapshots, GCHandleType.Pinned);
			var newSnap = new ClientSnapshot() {
				ServerCommandNum = this.serverCommandSequence,
				ServerTime = msg.ReadLong(),
				MessageNum = this.serverMessageSequence
			};

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
			if (deltaNum == 0) {
				newSnap.DeltaNum = -1;
			} else {
				newSnap.DeltaNum = newSnap.MessageNum - deltaNum;
			}
			newSnap.Flags = msg.ReadByte();
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

			//if (len > sizeof(byte)*32) {
				//oldSnapHandle.Free();
				//throw new JKClientException("ParseSnapshot: Invalid size %d for areamask");
			//}

			msg.ReadData(null, len);
			if (this.ClientHandler.CanParseSnapshot()) {
				msg.ReadDeltaPlayerstate(oldSnap != null ? &oldSnap->PlayerState : null, &newSnap.PlayerState, false, this.ClientHandler);
				if (this.ClientHandler.CanParseVehicle && newSnap.PlayerState.VehicleNum != 0) {
					msg.ReadDeltaPlayerstate(oldSnap != null ? &oldSnap->VehiclePlayerState : null, &newSnap.VehiclePlayerState, true, this.ClientHandler);
				}
				this.ParsePacketEntities(in msg, in oldSnap, &newSnap);
			}
			oldSnapHandle.Free();
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
			this.snap = newSnap;
			this.snapshots[this.snap.MessageNum & JKClient.PacketMask] = this.snap;
			this.newSnapshots = true;

			this.OnSnapshotParsed(EventArgs.Empty);
		}
		private unsafe void ParsePacketEntities(in Message msg, in ClientSnapshot *oldSnap, in ClientSnapshot *newSnap) {
			newSnap->ParseEntitiesNum = this.parseEntitiesNum;
			newSnap->NumEntities = 0;
			EntityState *oldstate;
			var oldstateHandle = GCHandle.Alloc(this.parseEntities, GCHandleType.Pinned);
			int oldindex = 0;
			int oldnum;
			int newnum = msg.ReadBits(Common.GEntitynumBits);
			while (true) {
				if (oldSnap != null && oldindex < oldSnap->NumEntities) {
					oldstate = ((EntityState *)oldstateHandle.AddrOfPinnedObject()) + ((oldSnap->ParseEntitiesNum + oldindex) & (JKClient.MaxParseEntities-1));
					oldnum = oldstate->Number;
				} else {
					oldstate = null;
					oldnum = 99999;
				}
				fixed (EntityState *newstate = &this.parseEntities[this.parseEntitiesNum]) {
					if (oldstate == null && (newnum == (Common.MaxGEntities-1))) {
						break;
					} else if (oldnum < newnum) {
						*newstate = *oldstate;
						oldindex++;
					} else if (oldnum == newnum) {
						oldindex++;
						msg.ReadDeltaEntity(oldstate, newstate, newnum, this.ClientHandler);
						newnum = msg.ReadBits(Common.GEntitynumBits);
					} else if (oldnum > newnum) {
						fixed (EntityState *bl = &this.entityBaselines[newnum]) {
							msg.ReadDeltaEntity(bl, newstate, newnum, this.ClientHandler);
						}
						newnum = msg.ReadBits(Common.GEntitynumBits);
					}
					if (newstate->Number == Common.MaxGEntities-1)
						continue;
					this.parseEntitiesNum++;
					this.parseEntitiesNum &= (JKClient.MaxParseEntities-1);
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
					fixed (sbyte* s = msg.ReadString()) {
						byte* ss = (byte*)s;
						throw new JKClientException($"{Common.ToString(ss, sizeof(sbyte)*Common.MaxStringChars)}");
					}
				}
			}
			int size = msg.ReadShort();
			if (size < 0 || size > sizeof(byte)*this.ClientHandler.MaxMessageLength) {
				throw new JKClientException($"ParseDownload: Invalid size {size} for download chunk");
			}
			msg.ReadData(null, size);
		}
	}
}
