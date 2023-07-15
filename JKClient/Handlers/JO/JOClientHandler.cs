using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace JKClient {
	public class JOClientHandler : JONetHandler, IClientHandler {
		public virtual new ProtocolVersion Protocol => (ProtocolVersion)base.Protocol;
		public virtual ClientVersion Version { get; private set; }
		public virtual int MaxReliableCommands => 128;
		public virtual int MaxConfigstrings => 1400;
		public virtual int MaxClients => 32;
		public virtual bool CanParseRMG => false;
		public virtual bool CanParseVehicle => false;
		public virtual string GuidKey => "";//throw new NotImplementedException(); // Don't throw here, it breaks stuff wtf.
		public virtual bool FullByteEncoding => false;
		public JOClientHandler(ProtocolVersion protocol, ClientVersion version) : base(protocol) {
			this.Version = version;
		}
		public void RequestAuthorization(string CDKey, Action<NetAddress, string> authorize) {}
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd) {
			//JO doesn't have setgame command, the rest commands match
			if (cmd >= ServerCommandOperations.SetGame) {
				cmd++;
			}
		}
		public virtual void AdjustGameStateConfigstring(int i, string csStr) {
			if (i == GameState.ServerInfo) {
				var info = new InfoString(csStr);
				if (info["version"].Contains("v1.03")) {
					this.Version = ClientVersion.JO_v1_03;
				}
			}
		}
		public virtual ClientGame CreateClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			return new JOClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
		}
		public virtual bool CanParseSnapshot() {
			return true;
		}
		public virtual IList<NetField> GetEntityStateFields() {
			switch (this.Protocol) {
			default:
				throw new JKClientException("Protocol not supported");
			case ProtocolVersion.Protocol15 when this.Version == ClientVersion.JO_v1_03:
			case ProtocolVersion.Protocol16:
				return JOClientHandler.entityStateFields16;
			case ProtocolVersion.Protocol15:
				return JOClientHandler.entityStateFields15;
			}
		}
		public virtual IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot) {
			switch (this.Protocol) {
			default:
				throw new JKClientException("Protocol not supported");
			case ProtocolVersion.Protocol15 when this.Version == ClientVersion.JO_v1_03:
			case ProtocolVersion.Protocol16:
				return JOClientHandler.playerStateFields16;
			case ProtocolVersion.Protocol15:
				return JOClientHandler.playerStateFields15;
			}
		}
		public virtual void ClearState() {}
		public virtual void SetExtraConfigstringInfo(in ServerInfo serverInfo, in InfoString info) {
			switch (serverInfo.Protocol) {
			case ProtocolVersion.Protocol15 when info["version"].Contains("v1.03"):
				serverInfo.Version = ClientVersion.JO_v1_03;
				break;
			case ProtocolVersion.Protocol15:
				serverInfo.Version = ClientVersion.JO_v1_02;
				break;
			case ProtocolVersion.Protocol16:
				serverInfo.Version = ClientVersion.JO_v1_04;
				break;
			}
			if (info.Count <= 0) {
				return;
			}
			int gameType = info["g_gametype"].Atoi();
			//JO doesn't have Power Duel, the rest game types match
			if (gameType >= (int)GameType.PowerDuel) {
				gameType++;
			}
			serverInfo.GameType = (GameType)gameType;
			serverInfo.NeedPassword = info["g_needpass"].Atoi() != 0;
			serverInfo.TrueJedi = info["g_jediVmerc"].Atoi() != 0;
			if (serverInfo.GameType == GameType.Duel) {
				serverInfo.WeaponDisable = info["g_duelWeaponDisable"].Atoi() != 0;
			} else {
				serverInfo.WeaponDisable = info["g_weaponDisable"].Atoi() != 0;
			}
			serverInfo.ForceDisable = info["g_forcePowerDisable"].Atoi() != 0;
		}
		private static readonly NetFieldsArray entityStateFields15 = new NetFieldsArray(typeof(EntityState)) {
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32()    ,   32  },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() ,  0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1, 0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() , 0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*1,    0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*2  ,   0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1    ,  0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*2 ,   0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32()   , 0   },
			{   nameof(EntityState.Event)   ,   10  }, // There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
			{   nameof(EntityState.Angles2), sizeof(float)*1   ,    0   },
			{   nameof(EntityState.EntityType)  ,   8   },
			{   nameof(EntityState.TorsoAnimation)  ,   16  }, // Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
			{   nameof(EntityState.ForceFrame)  ,   16  },
			{   nameof(EntityState.EventParm)   ,   8   },
			{   nameof(EntityState.LegsAnimation)  ,    16  },
			{   nameof(EntityState.GroundEntityNum) ,   Common.GEntitynumBits   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Type)).ToInt32() ,  8   },
			{   nameof(EntityState.EntityFlags) ,   32  },
			{   nameof(EntityState.Bolt1) , 8   },
			{   nameof(EntityState.Bolt2) , Common.GEntitynumBits   },
			{   nameof(EntityState.TrickedEntityIndex)   ,  16  }, //See note in PSF
			{   nameof(EntityState.TrickedEntityIndex2)  ,  16  },
			{   nameof(EntityState.TrickedEntityIndex3)  ,  16  },
			{   nameof(EntityState.TrickedEntityIndex4)  ,  16  },
			{   nameof(EntityState.Speed) , 0   },
			{   nameof(EntityState.FireFlag)  , 2   },
			{   nameof(EntityState.GenericEnemyIndex)    ,  32  },
			{   nameof(EntityState.ActiveForcePass)   , 6   },
			{   nameof(EntityState.EmplacedOwner) , 32  },
			{   nameof(EntityState.OtherEntityNum)  ,   Common.GEntitynumBits   },
			{   nameof(EntityState.Weapon)   ,  8   },
			{   nameof(EntityState.ClientNum)   ,   8   },
			{   nameof(EntityState.Angles), sizeof(float)*1  ,  0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Duration)).ToInt32()    ,   32  },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Type)).ToInt32()    ,    8   },
			{   nameof(EntityState.Origin) ,    0   },
			{   nameof(EntityState.Origin), sizeof(float)*1 ,   0   },
			{   nameof(EntityState.Origin), sizeof(float)*2 ,   0   },
			{   nameof(EntityState.Solid)  ,    24  },
			{   nameof(EntityState.Owner)   ,   Common.GEntitynumBits   },
			{   nameof(EntityState.TeamOwner)   ,   8   },
			{   nameof(EntityState.ShouldTarget)   ,    1   },
			{   nameof(EntityState.Powerups)    ,   16  },
			{   nameof(EntityState.ModelGhoul2)    ,    4   },
			{   nameof(EntityState.G2Radius)    ,   8   },
			{   nameof(EntityState.ModelIndex)    , -8  },
			{   nameof(EntityState.OtherEntityNum2)  ,  Common.GEntitynumBits   },
			{   nameof(EntityState.LoopSound) , 8   },
			{   nameof(EntityState.Generic1) ,  8   },
			{   nameof(EntityState.Origin2), sizeof(float)*2    ,   0   },
			{   nameof(EntityState.Origin2), sizeof(float)*0    ,   0   },
			{   nameof(EntityState.Origin2), sizeof(float)*1    ,   0   },
			{   nameof(EntityState.ModelIndex2) ,   8   },
			{   nameof(EntityState.Angles)    , 0   },
			{   nameof(EntityState.Time)  , 32  },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32(),    32  },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Duration)).ToInt32() ,   32  },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32()+sizeof(float)*2 ,   0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() ,  0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32()+sizeof(float)*1    ,   0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32()+sizeof(float)*2    ,   0   },
			{   nameof(EntityState.Time2)    ,  32  },
			{   nameof(EntityState.Angles), sizeof(float)*2   , 0   },
			{   nameof(EntityState.Angles2) ,   0   },
			{   nameof(EntityState.Angles2),sizeof(float)*2 ,   0   },
			{   nameof(EntityState.ConstantLight) , 32  },
			{   nameof(EntityState.Frame)   ,   16  },
			{   nameof(EntityState.SaberInFlight)   ,   1   },
			{   nameof(EntityState.SaberEntityNum)   ,  Common.GEntitynumBits   },
			{   nameof(EntityState.SaberMove)  ,    8   },
			{   nameof(EntityState.ForcePowersActive)   ,   32  },
			{   nameof(EntityState.IsJediMaster)   ,    1   }
		};
		private static readonly NetFieldsArray entityStateFields16 = new NetFieldsArray(typeof(EntityState)) {
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32()    ,   32  },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() ,  0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1, 0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() , 0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*1,    0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*2  ,   0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1    ,  0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*2 ,   0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32()   , 0   },
			{   nameof(EntityState.Event)   ,   10  }, // There is a maximum of 256 events (8 bits transmission, 2 high bits for uniqueness)
			{   nameof(EntityState.Angles2), sizeof(float)*1   ,    0   },
			{   nameof(EntityState.EntityType)  ,   8   },
			{   nameof(EntityState.TorsoAnimation)  ,   16  }, // Maximum number of animation sequences is 2048.  Top bit is reserved for the togglebit
			{   nameof(EntityState.ForceFrame)  ,   16  },
			{   nameof(EntityState.EventParm)   ,   8   },
			{   nameof(EntityState.LegsAnimation)  ,    16  },
			{   nameof(EntityState.GroundEntityNum) ,   Common.GEntitynumBits   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Type)).ToInt32() ,  8   },
			{   nameof(EntityState.EntityFlags) ,   32  },
			{   nameof(EntityState.Bolt1) , 8   },
			{   nameof(EntityState.Bolt2) , Common.GEntitynumBits   },
			{   nameof(EntityState.TrickedEntityIndex)   ,  16  }, //See note in PSF
			{   nameof(EntityState.TrickedEntityIndex2)  ,  16  },
			{   nameof(EntityState.TrickedEntityIndex3)  ,  16  },
			{   nameof(EntityState.TrickedEntityIndex4)  ,  16  },
			{   nameof(EntityState.Speed) , 0   },
			{   nameof(EntityState.FireFlag)  , 2   },
			{   nameof(EntityState.GenericEnemyIndex)    ,  32  },
			{   nameof(EntityState.ActiveForcePass)   , 6   },
			{   nameof(EntityState.EmplacedOwner) , 32  },
			{   nameof(EntityState.OtherEntityNum)  ,   Common.GEntitynumBits   },
			{   nameof(EntityState.Weapon)   ,  8   },
			{   nameof(EntityState.ClientNum)   ,   8   },
			{   nameof(EntityState.Angles), sizeof(float)*1  ,  0   },
			{   nameof(EntityState.Position), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Duration)).ToInt32()    ,   32  },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Type)).ToInt32()    ,    8   },
			{   nameof(EntityState.Origin) ,    0   },
			{   nameof(EntityState.Origin), sizeof(float)*1 ,   0   },
			{   nameof(EntityState.Origin), sizeof(float)*2 ,   0   },
			{   nameof(EntityState.Solid)  ,    24  },
			{   nameof(EntityState.Owner)   ,   Common.GEntitynumBits   },
			{   nameof(EntityState.TeamOwner)   ,   8   },
			{   nameof(EntityState.ShouldTarget)   ,    1   },
			{   nameof(EntityState.Powerups)    ,   16  },
			{   nameof(EntityState.ModelGhoul2)    ,    5   },
			{   nameof(EntityState.G2Radius)    ,   8   },
			{   nameof(EntityState.ModelIndex)    , -8  },
			{   nameof(EntityState.OtherEntityNum2)  ,  Common.GEntitynumBits   },
			{   nameof(EntityState.LoopSound) , 8   },
			{   nameof(EntityState.Generic1) ,  8   },
			{   nameof(EntityState.Origin2), sizeof(float)*2    ,   0   },
			{   nameof(EntityState.Origin2), sizeof(float)*0    ,   0   },
			{   nameof(EntityState.Origin2), sizeof(float)*1    ,   0   },
			{   nameof(EntityState.ModelIndex2) ,   8   },
			{   nameof(EntityState.Angles)    , 0   },
			{   nameof(EntityState.Time)  , 32  },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32(),    32  },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Duration)).ToInt32() ,   32  },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32()+sizeof(float)*2 ,   0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() ,  0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32()+sizeof(float)*1    ,   0   },
			{   nameof(EntityState.AngularPosition), Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32()+sizeof(float)*2    ,   0   },
			{   nameof(EntityState.Time2)    ,  32  },
			{   nameof(EntityState.Angles), sizeof(float)*2   , 0   },
			{   nameof(EntityState.Angles2) ,   0   },
			{   nameof(EntityState.Angles2),sizeof(float)*2 ,   0   },
			{   nameof(EntityState.ConstantLight) , 32  },
			{   nameof(EntityState.Frame)   ,   16  },
			{   nameof(EntityState.SaberInFlight)   ,   1   },
			{   nameof(EntityState.SaberEntityNum)   ,  Common.GEntitynumBits   },
			{   nameof(EntityState.SaberMove)  ,    8   },
			{   nameof(EntityState.ForcePowersActive)   ,   32  },
			{   nameof(EntityState.IsJediMaster)   ,    1   }
		};
		private static unsafe readonly NetFieldsArray playerStateFields15 = new NetFieldsArray(typeof(PlayerState)) {
			{   nameof(PlayerState.CommandTime), 32  },
			{   nameof(PlayerState.Origin), 0  },
			{   nameof(PlayerState.Origin),  sizeof(float)*1, 0  },
			{   nameof(PlayerState.BobCycle), 8  },
			{   nameof(PlayerState.Velocity), 0  },
			{   nameof(PlayerState.Velocity),  sizeof(float)*1, 0  },
			{   nameof(PlayerState.ViewAngles),  sizeof(float)*1, 0  },
			{   nameof(PlayerState.ViewAngles), 0  },
			{   nameof(PlayerState.WeaponTime), -16  },
			{   nameof(PlayerState.WeaponChargeTime), 32  },
			{   nameof(PlayerState.WeaponChargeSubtractTime), 32  },
			{   nameof(PlayerState.Origin),  sizeof(float)*2, 0  },
			{   nameof(PlayerState.Velocity),  sizeof(float)*2, 0  },
			{   nameof(PlayerState.PlayerMoveTime), -16  },
			{   nameof(PlayerState.EventSequence) , 16  },
			{   nameof(PlayerState.TorsoAnim), 16  },
			{   nameof(PlayerState.TorsoTimer), 16  },
			{   nameof(PlayerState.LegsAnimation), 16  },
			{   nameof(PlayerState.LegsTimer), 16  },
			{   nameof(PlayerState.MovementDirection), 4  },
			{   nameof(PlayerState.Events)  ,   sizeof(int)*0   ,   10  },
			{   nameof(PlayerState.Events)  ,   sizeof(int)*1   ,   10  },
			{ nameof(PlayerState.PlayerMoveFlags), 16  },
			{   nameof(PlayerState.GroundEntityNum) ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.Weaponstate), 4  },
			{   nameof(PlayerState.EntityFlags) ,   32  },
			{   nameof(PlayerState.ExternalEvent) , 10  },
			{ nameof(PlayerState.Gravity), 16  },
			{ nameof(PlayerState.Speed), 16  },
			{ nameof(PlayerState.Basespeed), 16  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(float)*1, 16  }, // Replace sizeof type.
			{   nameof(PlayerState.ExternalEventParm)   ,   8   },
			{ nameof(PlayerState.ViewHeight), -8  },
			{ nameof(PlayerState.DamageEvent), 8  },
			{ nameof(PlayerState.DamageYaw), 8  },
			{ nameof(PlayerState.DamagePitch), 8  },
			{ nameof(PlayerState.DamageCount), 8  },
			{ nameof(PlayerState.DamageType), 2  },
			{ nameof(PlayerState.Generic1), 8  },
			{   nameof(PlayerState.PlayerMoveType)  ,   8   , (value) => {
				if (Enum.IsDefined(typeof(PlayerMoveType), *value)) {
					var pmType = (PlayerMoveType)(*value);
					//JO doesn't have jetpack player movement, the rest movements match
					if (pmType >= PlayerMoveType.Jetpack) {
						(*value)++;
					}
				}
			}   },
			{ nameof(PlayerState.DeltaAngles), 16  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(float)*2, 16  }, // Replace sizeof type. 
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*0   ,   -16 },
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*1   ,   8   },
			{   nameof(PlayerState.ClientNum) , 8   },
			{   nameof(PlayerState.Weapon), 5  },
			{   nameof(PlayerState.ViewAngles),  sizeof(float)*2, 0  },
			{ nameof(PlayerState.JumpPadEntity), 10  },
			{ nameof(PlayerState.LoopSound), 16  },
			{ nameof(PlayerState.ZoomMode), 2  },
			{ nameof(PlayerState.ZoomTime), 32  },
			{ nameof(PlayerState.ZoomLocked), 1  },
			{ nameof(PlayerState.ZoomFov), 8  },
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowersActive)).ToInt32(), 32  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceMindtrickTargetIndex)).ToInt32(), 16  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceMindtrickTargetIndex2)).ToInt32(), 16  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceMindtrickTargetIndex3)).ToInt32(), 16  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceMindtrickTargetIndex4)).ToInt32(), 16  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceJumpZStart)).ToInt32(), 0  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowerSelected)).ToInt32(), 8  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowersKnown)).ToInt32(), 32  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePower)).ToInt32(), 8  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceSide)).ToInt32(), 2  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.SentryDeployed)).ToInt32(), 1  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowerLevel)).ToInt32(), 2  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowerLevel)).ToInt32(), 2  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.GenericEnemyIndex), 32  },
			{ nameof(PlayerState.ActiveForcePass), 6  },
			{ nameof(PlayerState.HasDetPackPlanted), 1  },
			{   nameof(PlayerState.EmplacedIndex)    ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceRageRecoveryTime)).ToInt32(), 32  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.RocketLockIndex), 8  },
			{ nameof(PlayerState.RocketLockTime), 32  },
			{ nameof(PlayerState.RocketTargetTime), 32  },
			{ nameof(PlayerState.HolocronBits), 32  },
			{ nameof(PlayerState.IsJediMaster), 1  },
			{ nameof(PlayerState.FallingToDeath), 32  },
			{ nameof(PlayerState.ElectrifyTime), 32  },
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowerDebounce)).ToInt32(), 32  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.SaberMove), 32  },
			{ nameof(PlayerState.SaberActive), 1  },
			{ nameof(PlayerState.SaberInFlight), 1  },
			{ nameof(PlayerState.SaberBlocked), 8  },
			{   nameof(PlayerState.SaberEntityNum)   ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.SaberCanThrow), 1  },
			{ nameof(PlayerState.ForceHandExtend), 8  },
			{ nameof(PlayerState.ForceDodgeAnim), 16  },
			{  nameof(PlayerState.forceData), Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.SaberAnimLevel)).ToInt32()   ,   2   },
			{   nameof(PlayerState.forceData), Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.SaberDrawAnimLevel)).ToInt32()   ,   2   },
			{ nameof(PlayerState.SaberAttackChainCount), 4  },
			{ nameof(PlayerState.SaberHolstered), 1  },
			{ nameof(PlayerState.UsingATST), 1  },
			{ nameof(PlayerState.AtstAltFire), 1  },
			{   nameof(PlayerState.DuelIndex)   ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.DuelTime), 32  },
			{ nameof(PlayerState.DuelInProgress), 1  },
			{ nameof(PlayerState.SaberLockTime), 32  },
			{   nameof(PlayerState.SaberLockEnemy)   ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.SaberLockFrame), 16  },
			{ nameof(PlayerState.SaberLockAdvance), 1  },
			{ nameof(PlayerState.InAirAnim), 1  },
			{ nameof(PlayerState.DualBlade), 1  },
			{ nameof(PlayerState.lastHitLoc),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.lastHitLoc), 0  },
			{ nameof(PlayerState.lastHitLoc),  sizeof(float)*1, 0  }, // Replace sizeof type. 
		};
		private static unsafe readonly NetFieldsArray playerStateFields16 = new NetFieldsArray(typeof(PlayerState)) {
			{   nameof(PlayerState.CommandTime), 32  },
			{   nameof(PlayerState.Origin), 0  },
			{   nameof(PlayerState.Origin),  sizeof(float)*1, 0  },
			{   nameof(PlayerState.BobCycle), 8  },
			{   nameof(PlayerState.Velocity), 0  },
			{   nameof(PlayerState.Velocity),  sizeof(float)*1, 0  },
			{   nameof(PlayerState.ViewAngles),  sizeof(float)*1, 0  },
			{   nameof(PlayerState.ViewAngles), 0  },
			{   nameof(PlayerState.WeaponTime), -16  },
			{   nameof(PlayerState.WeaponChargeTime), 32  },
			{   nameof(PlayerState.WeaponChargeSubtractTime), 32  },
			{   nameof(PlayerState.Origin),  sizeof(float)*2, 0  },
			{   nameof(PlayerState.Velocity),  sizeof(float)*2, 0  },
			{	nameof(PlayerState.PlayerMoveTime), -16  },
			{   nameof(PlayerState.EventSequence) , 16  },
			{	nameof(PlayerState.TorsoAnim), 16  },
			{	nameof(PlayerState.TorsoTimer), 16  },
			{	nameof(PlayerState.LegsAnimation), 16  },
			{	nameof(PlayerState.LegsTimer), 16  },
			{	nameof(PlayerState.MovementDirection), 4  },
			{   nameof(PlayerState.Events)  ,   sizeof(int)*0   ,   10  },
			{   nameof(PlayerState.Events)  ,   sizeof(int)*1   ,   10  },
			{ nameof(PlayerState.PlayerMoveFlags), 16  },
			{   nameof(PlayerState.GroundEntityNum) ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.Weaponstate), 4  },
			{   nameof(PlayerState.EntityFlags) ,   32  },
			{   nameof(PlayerState.ExternalEvent) , 10  },
			{ nameof(PlayerState.Gravity), 16  },
			{ nameof(PlayerState.Speed), 16  },
			{ nameof(PlayerState.Basespeed), 16  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(float)*1, 16  }, // Replace sizeof type. 
			{   nameof(PlayerState.ExternalEventParm)   ,   8   },
			{ nameof(PlayerState.ViewHeight), -8  },
			{ nameof(PlayerState.DamageEvent), 8  },
			{ nameof(PlayerState.DamageYaw), 8  },
			{ nameof(PlayerState.DamagePitch), 8  },
			{ nameof(PlayerState.DamageCount), 8  },
			{ nameof(PlayerState.DamageType), 2  },
			{ nameof(PlayerState.Generic1), 8  },
			{   nameof(PlayerState.PlayerMoveType)  ,   8   , (value) => {
				if (Enum.IsDefined(typeof(PlayerMoveType), *value)) {
					var pmType = (PlayerMoveType)(*value);
					//JO doesn't have jetpack player movement, the rest movements match
					if (pmType >= PlayerMoveType.Jetpack) {
						(*value)++;
					}
				}
			}   },
			{ nameof(PlayerState.DeltaAngles), 16  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(float)*2, 16  }, // Replace sizeof type. 
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*0   ,   -16 },
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*1   ,   8   },
			{   nameof(PlayerState.ClientNum) , 8   },
			{   nameof(PlayerState.Weapon), 5  },
			{   nameof(PlayerState.ViewAngles),  sizeof(float)*2, 0  },
			{ nameof(PlayerState.JumpPadEntity), 10  },
			{ nameof(PlayerState.LoopSound), 16  },
			{ nameof(PlayerState.ZoomMode), 2  },
			{ nameof(PlayerState.ZoomTime), 32  },
			{ nameof(PlayerState.ZoomLocked), 1  },
			{ nameof(PlayerState.ZoomFov), 8  },
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowersActive)).ToInt32(), 32  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceMindtrickTargetIndex)).ToInt32(), 16  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceMindtrickTargetIndex2)).ToInt32(), 16  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceMindtrickTargetIndex3)).ToInt32(), 16  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceMindtrickTargetIndex4)).ToInt32(), 16  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceJumpZStart)).ToInt32(), 0  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowerSelected)).ToInt32(), 8  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowersKnown)).ToInt32(), 32  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePower)).ToInt32(), 8  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceSide)).ToInt32(), 2  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.SentryDeployed)).ToInt32(), 1  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowerLevel)).ToInt32(), 2  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowerLevel)).ToInt32(), 2  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.GenericEnemyIndex), 32  },
			{ nameof(PlayerState.ActiveForcePass), 6  },
			{ nameof(PlayerState.HasDetPackPlanted), 1  },
			{   nameof(PlayerState.EmplacedIndex)   ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForceRageRecoveryTime)).ToInt32(), 32  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.RocketLockIndex), 8  },
			{ nameof(PlayerState.RocketLockTime), 32  },
			{ nameof(PlayerState.RocketTargetTime), 32  },
			{ nameof(PlayerState.HolocronBits), 32  },
			{ nameof(PlayerState.IsJediMaster), 1  },
			{ nameof(PlayerState.ForceRestricted), 1  },
			{ nameof(PlayerState.TrueJedi), 1  },
			{ nameof(PlayerState.TrueNonJedi), 1  },
			{ nameof(PlayerState.FallingToDeath), 32  },
			{ nameof(PlayerState.ElectrifyTime), 32  },
			{ nameof(PlayerState.forceData),  Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.ForcePowerDebounce)).ToInt32(), 32  }, //  Replace ForceData with real type and double check. 
			{ nameof(PlayerState.SaberMove), 32  },
			{ nameof(PlayerState.SaberActive), 1  },
			{ nameof(PlayerState.SaberInFlight), 1  },
			{ nameof(PlayerState.SaberBlocked), 8  },
			{   nameof(PlayerState.SaberEntityNum)   ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.SaberCanThrow), 1  },
			{ nameof(PlayerState.ForceHandExtend), 8  },
			{ nameof(PlayerState.ForceDodgeAnim), 16  },
			{   nameof(PlayerState.forceData), Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.SaberAnimLevel)).ToInt32()   ,   2   },
			{  nameof(PlayerState.forceData), Marshal.OffsetOf(typeof(ForceData),nameof(ForceData.SaberDrawAnimLevel)).ToInt32()   ,   2   },
			{ nameof(PlayerState.SaberAttackChainCount), 4  },
			{ nameof(PlayerState.SaberHolstered), 1  },
			{ nameof(PlayerState.UsingATST), 1  },
			{ nameof(PlayerState.AtstAltFire), 1  },
			{   nameof(PlayerState.DuelIndex)   ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.DuelTime), 32  },
			{ nameof(PlayerState.DuelInProgress), 1  },
			{ nameof(PlayerState.SaberLockTime), 32  },
			{   nameof(PlayerState.SaberLockEnemy)   ,   Common.GEntitynumBits   },
			{ nameof(PlayerState.SaberLockFrame), 16  },
			{ nameof(PlayerState.SaberLockAdvance), 1  },
			{ nameof(PlayerState.InAirAnim), 1  },
			{ nameof(PlayerState.DualBlade), 1  },
			{ nameof(PlayerState.lastHitLoc),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.lastHitLoc), 0  },
			{ nameof(PlayerState.lastHitLoc),  sizeof(float)*1, 0  }, // Replace sizeof type. 
		};
	}
}
