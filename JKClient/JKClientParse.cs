﻿using System;
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
		private GameMod gameMod = GameMod.Undefined;
		private int MaxConfigstrings => GameState.MaxConfigstrings(this.Protocol);
		private void ParseServerMessage(Message msg) {
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
				//JO doesn't have setgame command, the rest commands match
				if (this.IsJO() && cmd >= ServerCommandOperations.SetGame) {
					cmd++;
				//Q3 doesn't have setgame and mapchange commands, the rest commands match
				} else if (this.IsQ3() && cmd == ServerCommandOperations.SetGame) {
					cmd = ServerCommandOperations.EOF; //OPTIMIZE: just break?
				}
				if (cmd == ServerCommandOperations.EOF) {
					break;
				}
				switch (cmd) {
				default:
					throw new JKClientException("ParseServerMessage: Illegible server message");
				case ServerCommandOperations.Nop:
					break;
				case ServerCommandOperations.ServerCommand:
					this.ParseCommandString(msg);
					break;
				case ServerCommandOperations.Gamestate:
					this.ParseGamestate(msg);
					break;
				case ServerCommandOperations.Snapshot:
					this.ParseSnapshot(msg);
					eof = true;
					break;
				case ServerCommandOperations.SetGame:
//					this.ParseSetGame(msg);
					eof = true;
					break;
				case ServerCommandOperations.Download:
//					this.ParseDownload(msg);
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
		private unsafe void ParseGamestate(Message msg) {
			this.connectPacketCount = 0;
			this.ClearState();
			this.serverCommandSequence = msg.ReadLong();
			this.gameState.DataCount = 1;
			ServerCommandOperations cmd;
			while (true) {
				cmd = (ServerCommandOperations)msg.ReadByte();
				//JO doesn't have setgame command, the rest commands match
				if (this.IsJO() && cmd >= ServerCommandOperations.SetGame) {
					cmd++;
				//Q3 doesn't have setgame and mapchange commands, the rest commands match
				} else if (this.IsQ3() && cmd == ServerCommandOperations.SetGame) {
					cmd = ServerCommandOperations.EOF; //OPTIMIZE: just break?
				}
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
					if (i == GameState.ServerInfo) {
						string serverInfoCSStr = Common.ToString(s);
						var infoString = new InfoString(serverInfoCSStr);
						if (this.Protocol == ProtocolVersion.Protocol15 && infoString["version"].Contains("v1.03")) {
							this.Version = ClientVersion.JO_v1_03;
						}
						string gamename = infoString["gamename"];
						if (gamename.Contains("Szlakiem Jedi RPE")
							|| gamename.Contains("Open Jedi Project")
							|| gamename.Contains("OJP Enhanced")
							|| gamename.Contains("OJP Basic")
							|| gamename.Contains("OJRP")) {
							this.gameMod = GameMod.OJP;
						} else if (gamename.Contains("Movie Battles II")) {
							this.gameMod = GameMod.MBII;
						} else {
							this.gameMod = GameMod.Base;
						}
					}
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
							msg.ReadDeltaEntity(nes, bl, newnum, this.Protocol, this.gameMod);
						}
					}
				} else {
					throw new JKClientException("ParseGamestate: bad command byte");
				}
			}
			this.clientNum = msg.ReadLong();
			this.checksumFeed = msg.ReadLong();
			if (this.IsJA()) {
				this.ParseRMG(msg);
			}
			this.SystemInfoChanged();
			this.clientGame = this.InitClientGame();
			this.ServerInfoChanged?.Invoke(this.ServerInfo);
		}
		private void ParseRMG(Message msg) {
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
			var infoString = new InfoString(systemInfo);
			this.serverId = infoString["sv_serverid"].Atoi();
			if (infoString["sv_pure"].Atoi() != 0) {
				throw new JKClientException("Cannot connect to a pure server without assets");
			}
		}
		internal unsafe string GetConfigstring(int index) {
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
			this.gameMod = GameMod.Undefined;
			this.clientGame = null;
		}
		private void ClearConnection() {
			for (int i = 0; i < JKClient.MaxReliableCommandsJK; i++) {
				Common.MemSet(this.serverCommands[i], 0, sizeof(sbyte)*Common.MaxStringChars);
				Common.MemSet(this.reliableCommands[i], 0, sizeof(sbyte)*Common.MaxStringChars);
			}
			this.clientNum = 0;
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

			this.Demowaiting = false;
			this.Demorecording = false;
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
		private void ParseCommandString(Message msg) {
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
		private unsafe void ParseSnapshot(Message msg) {
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
				Demowaiting = false;   // we can start recording now
			}
			else {
				oldSnap = ((ClientSnapshot *)oldSnapHandle.AddrOfPinnedObject()) + (newSnap.DeltaNum & JKClient.PacketMask);
				if (!oldSnap->Valid) {

				} else if (oldSnap->MessageNum != newSnap.DeltaNum) {

				} else if (this.parseEntitiesNum - oldSnap->ParseEntitiesNum > JKClient.MaxParseEntities-128) {

				} else {
					newSnap.Valid = true;
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
			if (len > sizeof(byte)*32) {
				throw new JKClientException("ParseSnapshot: Invalid size %d for areamask");
			}
			msg.ReadData(null, len);
			if (this.CanParseSnapshot()) {
				msg.ReadDeltaPlayerstate(oldSnap != null ? &oldSnap->PlayerState : null, &newSnap.PlayerState, false, this.Protocol, this.gameMod);
				if (this.IsJA() && newSnap.PlayerState.VehicleNum != 0) {
					msg.ReadDeltaPlayerstate(oldSnap != null ? &oldSnap->VehiclePlayerState : null, &newSnap.VehiclePlayerState, true, this.Protocol, this.gameMod);
				}
				this.ParsePacketEntities(msg, oldSnap, &newSnap);
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
		private unsafe void ParsePacketEntities(Message msg, ClientSnapshot *oldSnap, ClientSnapshot *newSnap) {
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
						msg.ReadDeltaEntity(oldstate, newstate, newnum, this.Protocol, this.gameMod);
						newnum = msg.ReadBits(Common.GEntitynumBits);
					} else if (oldnum > newnum) {
						fixed (EntityState *bl = &this.entityBaselines[newnum]) {
							msg.ReadDeltaEntity(bl, newstate, newnum, this.Protocol, this.gameMod);
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
		private bool CanParseSnapshot() {
			switch (this.gameMod) {
			default:
				return true;
			case GameMod.Undefined:
			case GameMod.MBII:
				return false;
			}
		}
		private void ParseSetGame(Message msg) {
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
		private unsafe void ParseDownload(Message msg) {
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
			if (size < 0 || size > sizeof(byte)*Message.MaxLength(this.Protocol)) {
				throw new JKClientException($"ParseDownload: Invalid size {size} for download chunk");
			}
			msg.ReadData(null, size);
		}
	}
}
