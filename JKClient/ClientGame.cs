using System;

namespace JKClient {


	public enum CtfMessageType : int {
		FraggedFlagCarrier,
		FlagReturned,
		PlayerReturnedFlag,
		PlayerCapturedFlag,
		PlayerGotFlag
	}

	public interface IJKClientImport {
		internal void OnEntityEvent(EntityEventArgs entityEventArgs);
		internal int MaxClients { get; }
		public int Protocol { get; }
		public void GetCurrentSnapshotNumber(out int snapshotNumber, out int serverTime);
		internal bool GetSnapshot(in int snapshotNumber, ref Snapshot snapshot);
		internal bool GetServerCommand(in int serverCommandNumber, out Command command);
		internal string GetConfigstring(in int index);
		internal void ExecuteServerCommand(CommandEventArgs eventArgs);
		internal void NotifyClientInfoChanged();
		internal bool TellBotSkill(int clientNum, string value);
		internal void ResetExternalBotConfirmation(int clientNum);
    }
	public abstract class ClientGame {
		protected readonly bool Initialized = false;
		protected readonly int ClientNum;
		private protected int LatestSnapshotNum = 0;
		private protected int ProcessedSnapshotNum = 0;
		private protected Snapshot Snap = null, NextSnap = null;
		private protected int ServerCommandSequence = 0;
		internal protected readonly ClientEntity []Entities = new ClientEntity[Common.MaxGEntities];
		private protected readonly IJKClientImport Client;
		private protected int ServerTime;
		//private protected int LevelStartTime;
		//internal int GameTime => ServerTime - LevelStartTime;
		
		private protected readonly Snapshot []ActiveSnapshots = new Snapshot[2] {
			new Snapshot(),
			new Snapshot()
		};
		internal ClientInfo []ClientInfo {
			get;
			private protected set;
		}
		internal unsafe ClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			this.Client = client;
			this.ClientNum = clientNum;
			this.ProcessedSnapshotNum = serverMessageNum;
			this.ServerCommandSequence = serverCommandSequence;
			this.LatestSnapshotNum = 0;
			this.Snap = null;
			this.NextSnap = null;
			Common.MemSet(this.Entities, 0, sizeof(ClientEntity)*Common.MaxGEntities);
			this.ClientInfo = new ClientInfo[this.Client.MaxClients];
			for (int i = 0; i < this.Client.MaxClients; i++) {
				this.NewClientInfo(i);
			}
			this.Initialized = true;
		}
		internal virtual void Frame(int serverTime) {
			this.ServerTime = serverTime;
			this.ProcessSnapshots();
		}
		internal int GetGameTime()
		{
			int configstringLevelStartTime = this.GetConfigstringIndex(Configstring.LevelStartTime);
			int levelStartTime = Client.GetConfigstring(configstringLevelStartTime).Atoi();
			return ServerTime - levelStartTime;
		}

		private protected virtual void ProcessSnapshots() {
			this.Client.GetCurrentSnapshotNumber(out int n, out int _);
			if (n != this.LatestSnapshotNum) {
				if (n < this.LatestSnapshotNum) {
					this.Snap = null;
					this.NextSnap = null;
					this.ProcessedSnapshotNum = -2;
				}
				this.LatestSnapshotNum = n;
			}
			Snapshot snap;
			while (this.Snap == null) {
				snap = this.ReadNextSnapshot();
				if (snap == null) {
					return;
				}
				if ((snap.Flags & ClientSnapshot.NotActive) == 0) {
					this.SetInitialSnapshot(snap);
				}
			}
			do {
				if (this.NextSnap == null) {
					snap = this.ReadNextSnapshot();
					if (snap == null) {
						break;
					}
					this.SetNextSnap(snap);
					if (this.NextSnap.ServerTime < this.Snap.ServerTime) {
						throw new JKClientException("ProcessSnapshots: Server time went backwards");
					}
				}
				if (this.ServerTime >= this.Snap.ServerTime && this.ServerTime < this.NextSnap.ServerTime) {
					break;
				}
				this.TransitionSnapshot();
			} while (true);
		}
		private protected virtual Snapshot ReadNextSnapshot() {
			Snapshot dest;
			while (this.ProcessedSnapshotNum < this.LatestSnapshotNum) {
				if (this.Snap == this.ActiveSnapshots[0]) {
					dest = this.ActiveSnapshots[1];
				} else {
					dest = this.ActiveSnapshots[0];
				}
				this.ProcessedSnapshotNum++;
				if (this.Client.GetSnapshot(this.ProcessedSnapshotNum, ref dest)) {
					return dest;
				}
			}
			return null;
		}
		private protected virtual void SetInitialSnapshot(in Snapshot snap) {
			this.Snap = snap;
			this.Snap.PlayerState.ToEntityState(ref this.Entities[snap.PlayerState.ClientNum].CurrentState);
			this.Entities[snap.PlayerState.ClientNum].CurrentFilledFromPlayerState = true;
			this.ExecuteNewServerCommands(snap.ServerCommandSequence);
			int count = this.Snap.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.Snap.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.CurrentState = es;
				cent.Interpolate = false;
				cent.CurrentValid = true;
				this.ResetEntity(ref cent);
				this.CheckEvents(ref cent);
			}
		}
		private protected virtual void SetNextSnap(in Snapshot snap) {
			this.NextSnap = snap;
			this.NextSnap.PlayerState.ToEntityState(ref this.Entities[snap.PlayerState.ClientNum].NextState);
			int count = this.NextSnap.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.NextSnap.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.NextState = es;
				if (!cent.CurrentValid || (((cent.CurrentState.EntityFlags ^ es.EntityFlags) & this.GetEntityFlag(EntityFlag.TeleportBit)) != 0)) {
					cent.Interpolate = false;
				} else {
					cent.Interpolate = true;
				}
			}
		}
		private protected virtual void TransitionSnapshot() {
			this.ExecuteNewServerCommands(this.NextSnap.ServerCommandSequence);
			int count = this.Snap.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.Snap.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.CurrentValid = false;
			}
			this.Entities[this.Snap.PlayerState.ClientNum].CurrentFilledFromPlayerState = false;

			var oldFrame = this.Snap;
			this.Snap = this.NextSnap;
			this.Snap.PlayerState.ToEntityState(ref this.Entities[this.Snap.PlayerState.ClientNum].CurrentState);
			this.Entities[this.Snap.PlayerState.ClientNum].CurrentFilledFromPlayerState = true;
			this.Entities[this.Snap.PlayerState.ClientNum].Interpolate = false;
			count = this.Snap.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.Snap.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.CurrentState = cent.NextState;
				cent.CurrentValid = true;
				if (!cent.Interpolate) {
					this.ResetEntity(ref cent);
				}
				cent.Interpolate = false;
				this.CheckEvents(ref cent);
				cent.SnapshotTime = this.Snap.ServerTime;
			}
			this.NextSnap = null;
			this.TransitionPlayerState(ref this.Snap.PlayerState, ref oldFrame.PlayerState);
		}
		private protected virtual void ResetEntity(ref ClientEntity cent) {
			if (cent.SnapshotTime < this.ServerTime - ClientEntity.EventValidMsec) {
				cent.PreviousEvent = 0;
			}
		}
		private protected virtual unsafe void TransitionPlayerState(ref PlayerState ps, ref PlayerState ops) {
			if (ps.ClientNum != ops.ClientNum) {
				ops = ps;
			}
			if (ps.ExternalEvent != 0 && ps.ExternalEvent != ops.ExternalEvent) {
				ref var cent = ref this.Entities[ps.ClientNum];
				ref var es = ref cent.CurrentState;
				es.Event = ps.ExternalEvent;
				es.EventParm = ps.ExternalEventParm;
				this.HandleEvent(new EntityEventData(in cent));
			}
			for (int i = ps.EventSequence - PlayerState.MaxEvents; i < ps.EventSequence; i++) {
				if (i >= ops.EventSequence
					|| (i > ops.EventSequence - PlayerState.MaxEvents && ps.Events[i & (PlayerState.MaxEvents-1)] != ops.Events[i & (PlayerState.MaxEvents-1)])) {
					ref var cent = ref this.Entities[ps.ClientNum];
					ref var es = ref cent.CurrentState;
					es.Event = ps.Events[i & (PlayerState.MaxEvents-1)];
					es.EventParm = ps.EventParms[i & (PlayerState.MaxEvents-1)];
					this.HandleEvent(new EntityEventData(in cent));
				}
			}
			if (ps.ClientNum != ops.ClientNum) {
				for (int i = 0; i < this.Client.MaxClients; i++) {
					this.NewClientInfo(i);
				}
			}
		}
		protected virtual void ExecuteNewServerCommands(int latestSequence) {
			while (this.ServerCommandSequence < latestSequence) {
				if (this.Client.GetServerCommand(++this.ServerCommandSequence, out var command)) {
					this.ServerCommand(command);
				}
			}
		}
		protected virtual void ServerCommand(Command command) {
			string cmd = command.Argv(0);
			if (string.Compare(cmd, "cs", StringComparison.OrdinalIgnoreCase) == 0) {
				this.ConfigstringModified(command);
			}
		}
		protected virtual void ConfigstringModified(Command command) {
			int num = command.Argv(1).Atoi();
			int configstringPlayers = this.GetConfigstringIndex(Configstring.Players);
			if (num >= configstringPlayers && num < configstringPlayers+this.Client.MaxClients) {
				this.NewClientInfo(num - configstringPlayers);
			}
			//int configstringLevelStartTime = this.GetConfigstringIndex(Configstring.LevelStartTime);
			//if(num == configstringLevelStartTime)
            //{
			//	this.LevelStartTime = command.Argv(2).Atoi();
            //}
		}
		protected virtual void NewClientInfo(int clientNum) {
			bool isMOH = this is MOHClientGame;

			string configstring = this.Client.GetConfigstring(clientNum + this.GetConfigstringIndex(Configstring.Players));
			if (string.IsNullOrEmpty(configstring) || configstring[0] == '\0'
				|| !configstring.Contains(isMOH ? "name" : "n")) {
				this.ClientInfo[clientNum].Clear();
				this.Client.ResetExternalBotConfirmation(clientNum); // make sure to reset this when players connect/disconnect to be safe
			} else {
				var info = new InfoString(configstring);
				this.ClientInfo[clientNum].ClientNum = clientNum;
                if (isMOH)
                {
					this.ClientInfo[clientNum].Team = Team.Spectator;
					this.ClientInfo[clientNum].IsActiveMOH = true;
                    if (!string.IsNullOrEmpty(info["team"])) // MOH expansions actually do this :)
                    {
						int mohTeam = info["team"].Atoi();
                        switch (mohTeam)
                        {
							case 0: // TEAM_NONE
								this.ClientInfo[clientNum].IsActiveMOH = false;
								this.ClientInfo[clientNum].Team = Team.Spectator;
								break;
							default:
							case 1: // TEAM_SPECTATOR
								this.ClientInfo[clientNum].Team = Team.Spectator;
								break;
							case 2: // TEAM_FREEFORALL
								this.ClientInfo[clientNum].Team = Team.Free;
								break;
							case 3: // TEAM_ALLIES
								this.ClientInfo[clientNum].Team = Team.Blue;
								break;
							case 4: // TEAM_AXIS
								this.ClientInfo[clientNum].Team = Team.Red;
								break;
						}

					}
				} else
                {
					this.ClientInfo[clientNum].Team = (Team)info["t"].Atoi();
				}
				if (info.ContainsKey("skill"))
				{
					this.ClientInfo[clientNum].IsBotBySkill = this.Client.TellBotSkill(clientNum, info["skill"]);
					this.ClientInfo[clientNum].BotSkill = info["skill"].Atof(); // Only bots have this set
				} else
				{
					this.ClientInfo[clientNum].IsBotBySkill = this.Client.TellBotSkill(clientNum, "");
					this.ClientInfo[clientNum].BotSkill = -1.0f;
				}
				this.ClientInfo[clientNum].Name = info[isMOH ? "name":"n"];
				this.ClientInfo[clientNum].Model = info["model"];
				this.ClientInfo[clientNum].Color1 = info["c1"];
				this.ClientInfo[clientNum].Color2 = info["c2"];
				this.ClientInfo[clientNum].GRedTeam = info["g_redteam"];
				this.ClientInfo[clientNum].GBlueTeam = info["g_blueteam"];
                if (!this.ClientInfo[clientNum].InfoValid)
				{
					this.Client.ResetExternalBotConfirmation(clientNum); // make sure to reset this when players connect/disconnect to be safe
				}
				this.ClientInfo[clientNum].InfoValid = true;
			}
			if (this.Initialized) {
				this.Client.NotifyClientInfoChanged();
			}
		}
		private protected virtual void CheckEvents(ref ClientEntity cent) {
			ref var es = ref cent.CurrentState;
			if (es.EntityType > this.GetEntityType(EntityType.Events)) {
				if (cent.PreviousEvent != 0) {
					return;
				}
				if ((es.EntityFlags & this.GetEntityFlag(EntityFlag.PlayerEvent)) != 0) {
					es.Number = es.OtherEntityNum;
				}
				cent.PreviousEvent = 1;
				es.Event = (es.EntityType - this.GetEntityType(EntityType.Events));
			} else {
				if (es.Event == cent.PreviousEvent) {
					return;
				}
				cent.PreviousEvent = es.Event;
				if ((es.Event & ~(int)EntityEvent.Bits) == 0) {
					return;
				}
			}
			this.HandleEvent(new EntityEventData(in cent));
		}
		protected virtual EntityEvent HandleEvent(EntityEventData eventData) {
			ref var es = ref eventData.Cent.CurrentState;
			int entityEvent = es.Event & ~(int)EntityEvent.Bits;
			var ev = this.GetEntityEvent(entityEvent);
			if (ev == EntityEvent.None) {
				return EntityEvent.None;
			}
			int clientNum = es.ClientNum;
			if (clientNum < 0 || clientNum >= this.Client.MaxClients) {
				clientNum = 0;
			}
			if (es.EntityType == this.GetEntityType(EntityType.Player)) {
				if (!this.ClientInfo[clientNum].InfoValid) {
					return EntityEvent.None;
				}
			}
			this.Client.OnEntityEvent(new EntityEventArgs(ev, eventData.Cent));
			return ev;
		}
		internal abstract int GetConfigstringIndex(Configstring index);
		protected abstract EntityEvent GetEntityEvent(int entityEvent);
		protected abstract int GetEntityType(EntityType entityType);
		protected abstract int GetEntityFlag(EntityFlag entityFlag);
		public enum Configstring {
			ServerInfo =0,
			SystemInfo =1,
			GameVersion,
			Scores1,
			Scores2,
			Sounds,
			Players,
			LevelStartTime,
			FlagStatus,
			Intermission
		}
		public enum EntityFlag : int {
			TeleportBit,
			PlayerEvent
		}
		public enum EntityEvent : int {
			None,
			VoiceCommandSound,
			Obituary,
			ForceDrained,
			CtfMessage,
			Jump,
			Bits = 0x300
		}
		public enum EntityType : int
		{
			General,
			Player,
			Item,
			Missile,
			Special,
			Holocron,
			Mover,
			Beam,
			Portal,
			Speaker,
			PushTrigger,
			TeleportTrigger,
			Invisible,
			NPC,
			Team,
			Body,
			Terrain,
			FX,
			Grapple,
			Events
		}
		public sealed class EntityEventData {
			internal ClientEntity Cent;
			public int Event => this.Cent.CurrentState.Event;
			public int EventParm => this.Cent.CurrentState.EventParm;
			public int EntityNum => this.Cent.CurrentState.Number;
			public int ClientNum => this.Cent.CurrentState.ClientNum;
			public int OtherEntityNum => this.Cent.CurrentState.OtherEntityNum;
			public int GroundEntityNum => this.Cent.CurrentState.GroundEntityNum;
			private EntityEventData() {}
			internal EntityEventData(in ClientEntity cent) {
				this.Cent = cent;
			}
		}
	}
	
	
}
