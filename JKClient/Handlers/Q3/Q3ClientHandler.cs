using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace JKClient {
	public class Q3ClientHandler : Q3NetHandler, IClientHandler {
		private NetAddress authorizeServer;
		private GameMod gameMod = GameMod.Base;
		public virtual ClientVersion Version => ClientVersion.Q3_v1_32;
		public virtual int MaxReliableCommands => 64;
		public virtual int MaxConfigstrings => 1024;
		public virtual int MaxClients => 64;
		public virtual bool CanParseRMG => false;
		public virtual bool CanParseVehicle => false;
		public virtual string GuidKey => "cl_guid";
		public virtual bool FullByteEncoding => false;
		public Q3ClientHandler(ProtocolVersion protocol) : base(protocol) {}
		public void RequestAuthorization(string CDKey, Action<NetAddress, string> authorize) {
			if (this.authorizeServer == null) {
				this.authorizeServer = NetSystem.StringToAddress("authorize.quake3arena.com", 27952);
				if (this.authorizeServer == null) {
					Debug.WriteLine("Couldn't resolve authorize address");
					return;
				}
			}
			string nums = Regex.Replace(CDKey, "[^a-zA-Z0-9]", string.Empty);
			authorize(this.authorizeServer, $"getKeyAuthorize {0} {nums}");
		}
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd) {
			//Q3 doesn't have setgame and mapchange commands, the rest commands match
			if (cmd == ServerCommandOperations.SetGame) {
				cmd = ServerCommandOperations.EOF;
			}
		}
		public virtual void AdjustGameStateConfigstring(int i, string csStr) {
			if (i == (int)Q3ClientGame.ConfigstringQ3.GameVersion) {
				if (csStr.Contains("cpma")) {
					this.gameMod = GameMod.CPMA;
				} else if (csStr.Contains("defrag")) {
					this.gameMod = GameMod.DeFRaG;
				}
			} else if (i == 12 && csStr.Contains("RA3")) {
				this.gameMod = GameMod.RocketArena3;
			} else if (i == 872 && csStr.Length > 0 && csStr[0] != '\0') {
				this.gameMod = GameMod.OSP;
			}
		}
		public virtual ClientGame CreateClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			return new Q3ClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
		}
		public virtual bool CanParseSnapshot() {
			return true;
		}
		public virtual IList<NetField> GetEntityStateFields() {
			return Q3ClientHandler.entityStateFields68;
		}
		public virtual IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot) {
			return Q3ClientHandler.playerStateFields68;
		}
		public virtual void ClearState() {
			this.gameMod = GameMod.Base;
		}
		public virtual void SetExtraConfigstringInfo(in ServerInfo serverInfo, in InfoString info) {
			serverInfo.Version = ClientVersion.Q3_v1_32;
			if (info.Count <= 0) {
				return;
			}
			serverInfo.GameType = Q3ClientHandler.GetGameType(info["g_gametype"].Atoi());
		}
		private static GameType GetGameType(int gameType) {
			switch (gameType) {
			case 0:
				return GameType.FFA;
			case 1:
				return GameType.Duel;
			case 2:
				return GameType.SinglePlayer;
			case 3:
				return GameType.Team;
			case 4:
				return GameType.CTF;
			default:
				return (GameType)(gameType+5);
			}
		}
		private enum GameMod {
			Base,
			DeFRaG,
			CPMA,
			OSP,
			RocketArena3
		}
		private static readonly unsafe NetFieldsArray entityStateFields68 = new NetFieldsArray(typeof(EntityState)) {
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32(), 32  }, 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32(), 0  }, 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1, 0  }, 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32(), 0  }, 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*1, 0  }, 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*2, 0  }, 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1, 0  }, 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*2, 0  }, 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32(), 0  }, 
			{   nameof(EntityState.Event)	,	10	},
			{ nameof(EntityState.Angles2),  sizeof(float)*1, 0  },
			{	nameof(EntityState.EntityType)	,	8	},
			{ nameof(EntityState.TorsoAnimation), 8  },
			{   nameof(EntityState.EventParm)   ,	8	},
			{ nameof(EntityState.LegsAnimation), 8  },
			{	nameof(EntityState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Type)).ToInt32(), 8, ( value) => {
				if (Enum.IsDefined(typeof(TrajectoryType), *value)) {
					var trType = (TrajectoryType)(*value);
					//Q3 doesn't have non-linear stop trajectory type, the rest types match
					if (trType >= TrajectoryType.TR_NONLINEAR_STOP) {
						(* value)++;
					}
				}
			}	 }, 
			{   nameof(EntityState.EntityFlags)	,	19	},
			{	nameof(EntityState.OtherEntityNum)	,	Common.GEntitynumBits	},
			{ nameof(EntityState.Weapon), 8  },
			{	nameof(EntityState.ClientNum)	,	8	},
			{ nameof(EntityState.Angles),  sizeof(float)*1, 0  },
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Duration)).ToInt32(), 32  }, 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Type)).ToInt32(), 8, ( value) => {
				if (Enum.IsDefined(typeof(TrajectoryType), *value)) {
					var trType = (TrajectoryType)(*value);
					//Q3 doesn't have non-linear stop trajectory type, the rest types match
					if (trType >= TrajectoryType.TR_NONLINEAR_STOP) {
						(* value)++;
					}
				}
			}  }, 
			{ nameof(EntityState.Origin), 0  },
			{ nameof(EntityState.Origin),  sizeof(float)*1, 0  },
			{ nameof(EntityState.Origin),  sizeof(float)*2, 0  },
			{ nameof(EntityState.Solid), 24  },
			{ nameof(EntityState.Powerups), 16  },
			{ nameof(EntityState.ModelIndex), 8  },
			{ nameof(EntityState.OtherEntityNum2), Common.GEntitynumBits  },
			{ nameof(EntityState.LoopSound), 8  },
			{ nameof(EntityState.Generic1), 8  },
			{ nameof(EntityState.Origin2),  sizeof(float)*2, 0  },
			{ nameof(EntityState.Origin2), 0  },
			{ nameof(EntityState.Origin2),  sizeof(float)*1, 0  },
			{ nameof(EntityState.ModelIndex2), 8  },
			{ nameof(EntityState.Angles), 0  },
			{ nameof(EntityState.Time), 32  },
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32(), 32  }, 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Duration)).ToInt32(), 32  }, 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*2, 0  }, 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32(), 0  }, 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*1, 0  }, 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*2, 0  }, 
			{ nameof(EntityState.Time2), 32  },
			{ nameof(EntityState.Angles),  sizeof(float)*2, 0  },
			{ nameof(EntityState.Angles2), 0  },
			{ nameof(EntityState.Angles2),  sizeof(float)*2, 0  },
			{ nameof(EntityState.ConstantLight), 32  },
			{ nameof(EntityState.Frame), 16  },
		};
		private static unsafe readonly NetFieldsArray playerStateFields68 = new NetFieldsArray(typeof(PlayerState)) {
			{ nameof(PlayerState.CommandTime), 32  },
			{ nameof(PlayerState.Origin), 0  },
			{ nameof(PlayerState.Origin),  sizeof(float)*1, 0  },
			{ nameof(PlayerState.BobCycle), 8  },
			{ nameof(PlayerState.Velocity), 0  },
			{ nameof(PlayerState.Velocity),  sizeof(float)*1, 0  },
			{ nameof(PlayerState.ViewAngles),  sizeof(float)*1, 0  },
			{ nameof(PlayerState.ViewAngles), 0  },
			{ nameof(PlayerState.WeaponTime), -16  },
			{ nameof(PlayerState.Origin),  sizeof(float)*2, 0  },
			{ nameof(PlayerState.Velocity),  sizeof(float)*2, 0  },
			{ nameof(PlayerState.LegsTimer), 8  },
			{ nameof(PlayerState.PlayerMoveTime), -16  },
			{   nameof(PlayerState.EventSequence) ,	16	},
			{ nameof(PlayerState.TorsoAnim), 8  },
			{ nameof(PlayerState.MovementDirection), 4  },
			{	nameof(PlayerState.Events)  ,   sizeof(int)*0	,   8	},
			{ nameof(PlayerState.LegsAnimation), 8  },
			{	nameof(PlayerState.Events)	,	sizeof(int)*1	,	8	},
			{   nameof(PlayerState.PlayerMoveFlags) ,	16	},
			{	nameof(PlayerState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{ nameof(PlayerState.Weaponstate), 4  },
			{   nameof(PlayerState.EntityFlags) ,	16	},
			{   nameof(PlayerState.ExternalEvent) ,	10	},
			{ nameof(PlayerState.Gravity), 16  },
			{ nameof(PlayerState.Speed), 16  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(int)*1, 16  },
			{	nameof(PlayerState.ExternalEventParm)	,	8	},
			{ nameof(PlayerState.ViewHeight), -8  },
			{ nameof(PlayerState.DamageEvent), 8  },
			{ nameof(PlayerState.DamageYaw), 8  },
			{ nameof(PlayerState.DamagePitch), 8  },
			{ nameof(PlayerState.DamageCount), 8  },
			{ nameof(PlayerState.Generic1), 8  },
			{	nameof(PlayerState.PlayerMoveType)	,	8	, (value) => {
				if (Enum.IsDefined(typeof(PlayerMoveType), *value)) {
					var pmType = (PlayerMoveType)(*value);
					//Q3 doesn't have jetpack and float player movements, the rest movements match
					if (pmType >= PlayerMoveType.Jetpack) {
						(*value)+=2;
					}
				}
			}	},
			{ nameof(PlayerState.DeltaAngles), 16  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(int)*2, 16  },
			{ nameof(PlayerState.TorsoTimer), 12  },
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*1   ,   8	},
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*0   ,   8	},
			{   nameof(PlayerState.ClientNum) ,	8	},
			{ nameof(PlayerState.Weapon), 5  },
			{ nameof(PlayerState.ViewAngles),  sizeof(float)*2, 0  },
			{ nameof(PlayerState.GrapplePoint), 0  },
			{ nameof(PlayerState.GrapplePoint),  sizeof(float)*1, 0  },
			{ nameof(PlayerState.GrapplePoint),  sizeof(float)*2, 0  },
			{ nameof(PlayerState.JumpPadEntity), 10  },
			{ nameof(PlayerState.LoopSound), 16  },
		};
	}
}
