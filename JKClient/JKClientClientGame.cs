using System;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {

	public struct ConfigStringMismatch
	{
		public string intendedString;
		public string actualString;
		public byte[] oldGsStringData;
		public byte[] newGsStringData;
	}
	public struct NetDebug
	{
		public string debugString;
	}

	public sealed partial class JKClient : IJKClientImport {
		public event EventHandler<EntityEventArgs> EntityEvent;
		void IJKClientImport.OnEntityEvent(EntityEventArgs entityEventArgs)
		{
			this.EntityEvent?.Invoke(this, entityEventArgs);
		}

		private readonly StringBuilder bigInfoString = new StringBuilder(Common.BigInfoString, Common.BigInfoString);


		private void FirstSnapshot()
        {
			this.clServerTimeDelta = this.snap.ServerTime - this.realTime;
			this.clOldServerTime = this.snap.ServerTime;
		}
		private void AdjustTimeDelta()
        {
			const int RESET_TIME = 500;
			this.newSnapshots = false;
			int newDelta = this.snap.ServerTime - this.realTime;
			int deltaDelta = Math.Abs(newDelta - this.clServerTimeDelta);
			if (deltaDelta > RESET_TIME)
			{
				this.clServerTimeDelta = newDelta;
				this.clOldServerTime = this.snap.ServerTime;  // FIXME: is this a problem for cgame?
				this.clServerTime = this.snap.ServerTime;
			}
			else if (deltaDelta > 100)
			{
				// fast adjust, cut the difference in half
				this.clServerTimeDelta = (this.clServerTimeDelta + newDelta) >> 1;
			}
			else
			{
				// Don't do this for now, not sure what it's about -TA

				// slow drift adjust, only move 1 or 2 msec

				// if any of the frames between this and the previous snapshot
				// had to be extrapolated, nudge our sense of time back a little
				// the granularity of +1 / -2 is too high for timescale modified frametimes
				//if (com_timescale->value == 0 || com_timescale->value == 1)
				{
					if (this.clExtrapolatedSnapshot)
					{
						this.clExtrapolatedSnapshot = false;
						this.clServerTimeDelta -= 2;
					}
					else
					{
						// otherwise, move our sense of time forward to minimize total latency
						this.clServerTimeDelta++;
					}
				}
			}
			this.Stats.deltaDelta = deltaDelta;
		}
		private void SetTime() {
			if (this.Status != ConnectionStatus.Active) {
				if (this.Status != ConnectionStatus.Primed) {
					return;
				}
				if (this.newSnapshots) {
					this.newSnapshots = false;
					if ((this.snap.Flags & ClientSnapshot.NotActive) != 0) {
						return;
					}
					this.FirstSnapshot();
					this.Status = ConnectionStatus.Active;
					this.connectTCS?.TrySetResult(true);
				}
				if (this.Status != ConnectionStatus.Active) {
					return;
				}
			}
			this.serverTime = this.snap.ServerTime;
			this.oldFrameServerTime = this.snap.ServerTime;

			// Need this for proper command timing.
			// For sake of simplicity, we forgo timenudge.
			// We're not really playing after all, only spectating.
			this.clServerTime = this.realTime + this.clServerTimeDelta;
			// guarantee that time will never flow backwards, even if
			// serverTimeDelta made an adjustment or cl_timeNudge was changed
			if (this.clServerTime < this.clOldServerTime)
			{
				this.clServerTime = this.clOldServerTime;
			}
			this.clOldServerTime = this.clServerTime;

			if(this.realTime + this.clServerTimeDelta >= this.snap.ServerTime - 5)
            {
				this.clExtrapolatedSnapshot = true;
            }

            if (this.newSnapshots)
            {
				this.AdjustTimeDelta();
			}
		}
		private ClientGame InitClientGame() {
			this.Status = ConnectionStatus.Primed;
			var clientGame = this.ClientHandler.CreateClientGame(this, this.serverMessageSequence, this.serverCommandSequence, this.clientNum);
			if (clientGame == null) {
				throw new JKClientException("Failed to create client game for unknown client");
			}
			return clientGame;
		}


        private unsafe void ConfigstringModified(in Command command, in sbyte []s) {
			int index = command.Argv(1).Atoi();
			if (index < 0 || index >= this.MaxConfigstrings) {
				throw new JKClientException($"ConfigstringModified: bad index {index}");
			}
			int start = 4 + command.Argv(1).Length;
			if (s[start] == 34) { //'\"'
				start++;
			}
			int blen = command.Argv(2).Length;
			//if (blen == 0) { //Wtf?
			//	blen = 1;
			//}
			sbyte []b = new sbyte[blen+1];
			Array.Copy(s, start, b, 0, b.Length-1);
#if DEBUG
			bool oldGsExists = false;
			GameState oldGs;
#endif
			fixed (sbyte *old = &this.gameState.StringData[this.gameState.StringOffsets[index]],
				sb = b) {
				if (Common.StriCmp(old, sb) == 0) {
					return;
				}

#if DEBUG
				oldGs = this.gameState;
				oldGsExists = true;
#else
				var oldGs = this.gameState;
#endif
				fixed (GameState *gs = &this.gameState) {
					Common.MemSet(gs, 0, sizeof(GameState));
				}
				this.gameState.DataCount = 1;
				int len;
				for (int i = 0; i < this.MaxConfigstrings; i++) {
					//byte []dup = new byte[GameState.MaxGameStateChars]; // Don't allocate this much useless stuff omg!
					sbyte *dup = null;
					sbyte* bdup = &oldGs.StringData[oldGs.StringOffsets[i]];
					if (i == index) {
						//len = Common.StrLen(sb);
						//Marshal.Copy((IntPtr)sb, dup, 0, len);
						dup = sb;
					} else {
						if (bdup[0] == 0) {
							continue;
						}
						//len = Common.StrLen(bdup);
						dup = bdup;
						//Marshal.Copy((IntPtr)bdup, dup, 0, len);
					}

					len = Common.StrLen(dup);
					if (len + 1 + this.gameState.DataCount > this.ClientHandler.MaxGameStateChars) {
						throw new JKClientException("MaxGameStateChars exceeded");
					}
					this.gameState.StringOffsets[i] = this.gameState.DataCount;
					fixed (sbyte *stringData = this.gameState.StringData) {
						//Marshal.Copy((IntPtr)dup, 0, (IntPtr)(stringData+this.gameState.DataCount), len+1);
						Buffer.MemoryCopy(dup, stringData + this.gameState.DataCount, GameState.MaxGameStateChars- this.gameState.DataCount, len+1);
					}
					this.gameState.DataCount += len + 1;
				}
			}
			if (index == GameState.SystemInfo) {
				this.SystemInfoChanged();
			} else if (index == GameState.ServerInfo) {
				this.ServerInfoChanged?.Invoke(this.ServerInfo,false);
			}
#if DEBUG
			if(this.DebugConfigStrings && this.DebugEventHappened.GetInvocationList().Length > 0)
            {
				// Check back if the configstring was correctly written.
				string shouldString = command.Argv(2);
				string isString = this.GetConfigstring(index);
				if(shouldString != isString)
                {

					byte[] oldArray = oldGsExists ? new byte[GameState.MaxGameStateChars] : null;
					byte[] newArray = new byte[GameState.MaxGameStateChars];
                    if (oldGsExists)
					{
						Marshal.Copy((IntPtr)oldGs.StringData, oldArray, 0,  GameState.MaxGameStateChars);
					}

					fixed (sbyte* newGameStateData = this.gameState.StringData)
                    {
						Marshal.Copy((IntPtr)newGameStateData, newArray, 0, GameState.MaxGameStateChars);
					}
					this.OnDebugEventHappened(new ConfigStringMismatch() { intendedString=shouldString, actualString=isString,oldGsStringData=oldArray,newGsStringData=newArray});
                }
            }
#endif
		}

		int IJKClientImport.MaxClients => this.ClientHandler.MaxClients;
		public event Action<CommandEventArgs> ServerCommandExecuted;
		void IJKClientImport.ExecuteServerCommand(CommandEventArgs eventArgs) {
			this.ServerCommandExecuted?.Invoke(eventArgs);
		}
		void IJKClientImport.NotifyClientInfoChanged() {
			this.ServerInfoChanged?.Invoke(this.ServerInfo,false);
		}
		void IJKClientImport.GetCurrentSnapshotNumber(out int snapshotNumber, out int serverTime) {
			snapshotNumber = this.snap.MessageNum;
			serverTime = this.snap.ServerTime;
		}
		bool IJKClientImport.GetSnapshot(in int snapshotNumber, ref Snapshot snapshot) {
			if (snapshotNumber > this.snap.MessageNum) {
				throw new JKClientException("GetSnapshot: snapshotNumber > this.snapshot.messageNum");
			}
			if (this.snap.MessageNum - snapshotNumber >= JKClient.PacketBackup) {
				return false;
			}
			ref var clSnapshot = ref this.snapshots[snapshotNumber & JKClient.PacketMask];
			if (!clSnapshot.Valid) {
				return false;
			}
			if (this.parseEntitiesNum - clSnapshot.ParseEntitiesNum > JKClient.MaxParseEntities) {
				return false;
			}
			snapshot.Flags = clSnapshot.Flags;
			snapshot.ServerCommandSequence = clSnapshot.ServerCommandNum;
			snapshot.ServerTime = clSnapshot.ServerTime;
			snapshot.PlayerState = clSnapshot.PlayerState;
			snapshot.VehiclePlayerState = clSnapshot.VehiclePlayerState;
			snapshot.ping = clSnapshot.ping;
			snapshot.NumEntities = Math.Min(clSnapshot.NumEntities, Snapshot.MaxEntities);
			for (int i = 0; i < snapshot.NumEntities; i++) {
				int entNum = (clSnapshot.ParseEntitiesNum + i) & (JKClient.MaxParseEntities-1);
				snapshot.Entities[i] = this.parseEntities[entNum];
			}
			return true;
		}
		unsafe bool IJKClientImport.GetServerCommand(in int serverCommandNumber, out Command command) {
			if (serverCommandNumber <= this.serverCommandSequence - this.MaxReliableCommands) {
				throw new JKClientException("GetServerCommand: a reliable command was cycled out");
			}
			if (serverCommandNumber > this.serverCommandSequence) {
				throw new JKClientException("GetServerCommand: requested a command not received");
			}
			this.lastExecutedServerCommand = serverCommandNumber;
			sbyte []sc = this.serverCommands[serverCommandNumber & (this.MaxReliableCommands - 1)];
			int commandMessageNumber = this.serverCommandMessagenums[serverCommandNumber & (this.MaxReliableCommands - 1)];
rescan:
			string s = Common.ToString(sc);
			command = new Command(s);
			s = Common.ToString(sc, Encoding.UTF8);
			var utf8Command = new Command(s);
			string cmd = command.Argv(0);
			this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, commandMessageNumber, utf8Command));
			if (string.Compare(cmd, "disconnect", StringComparison.Ordinal) == 0) {
				this.Disconnect();
				return true;
			} else if (string.Compare(cmd, "bcs0", StringComparison.Ordinal) == 0) {
				this.bigInfoString
					.Clear()
					.Append("cs ")
					.Append(command.Argv(1))
					.Append(" \"")
					.Append(command.Argv(2));
				return false;
			} else if (string.Compare(cmd, "bcs1", StringComparison.Ordinal) == 0) {
				this.bigInfoString
					.Append(command.Argv(2));
				return false;
			} else if (string.Compare(cmd, "bcs2", StringComparison.Ordinal) == 0) {
				this.bigInfoString
					.Append(command.Argv(2))
					.Append("\"");
				s = this.bigInfoString.ToString();
				sc = new sbyte[Common.BigInfoString];
				var bsc = Common.Encoding.GetBytes(s);
				fixed (sbyte *psc = sc) {
					Marshal.Copy(bsc, 0, (IntPtr)psc, bsc.Length);
				}
				goto rescan;
			} else if (string.Compare(cmd, "cs", StringComparison.Ordinal) == 0) {
				this.ConfigstringModified(command, sc);
				return true;
			}
			return true;
		}
		unsafe string IJKClientImport.GetConfigstring(in int index) => this.GetConfigstring(index);

    }
}
