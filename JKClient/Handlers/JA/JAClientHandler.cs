using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace JKClient {
	public class JAClientHandler : JANetHandler, IClientHandler {
		private const int MaxConfigstringsBase = 1700;
		private const int MaxConfigstringsOJP = 2200;
		private GameMod gameMod = GameMod.Undefined;
		public virtual ClientVersion Version { get; private set; }
		public virtual int MaxReliableCommands => 128;
		public virtual int MaxConfigstrings { get; private set; } = JAClientHandler.MaxConfigstringsBase;
		public virtual int MaxClients => 32;
		public virtual bool CanParseRMG => true;
		public virtual bool CanParseVehicle => true;
		public virtual string GuidKey => "ja_guid";
		public virtual bool FullByteEncoding => true;
		public JAClientHandler(ProtocolVersion protocol, ClientVersion version) : base(protocol) {
			this.Version = version;
		}
		public void RequestAuthorization(string CDKey, Action<NetAddress, string> authorize) {}
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd) {}
		public virtual void AdjustGameStateConfigstring(int i, string csStr) {
			if (i == GameState.ServerInfo) {
				var info = new InfoString(csStr);
				string gamename = info["gamename"];
				//TODO: add mod handlers
				if (gamename.Contains("Szlakiem Jedi RPE")
					|| gamename.Contains("Open Jedi Project")
					|| gamename.Contains("OJP Enhanced")
					|| gamename.Contains("OJP Basic")
					|| gamename.Contains("OJRP")) {
					this.gameMod = GameMod.OJP;
					this.MaxConfigstrings = JAClientHandler.MaxConfigstringsOJP;
				} else if (gamename.Contains("Movie Battles II")) {
					this.gameMod = GameMod.MBII;
				} else {
					this.gameMod = GameMod.Base;
				}
			}
		}
		public virtual ClientGame CreateClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			return new JAClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
		}
		public virtual bool CanParseSnapshot() {
			switch (this.gameMod) {
			default:
				return true;
			case GameMod.Undefined:
			case GameMod.MBII:
				return false;
			}
		}
		public virtual IList<NetField> GetEntityStateFields() {
			switch (this.gameMod) {
			default:
				return JAClientHandler.entityStateFields26;
			case GameMod.MBII:
				return JAClientHandler.entityStateFieldsMBII;
			case GameMod.OJP:
				return JAClientHandler.entityStateFieldsOJP;
			}
		}
		public virtual IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot) {
			if (isVehicle) {
				return JAClientHandler.vehPlayerStateFields26;
			} else {
				if (isPilot()) {
					return JAClientHandler.pilotPlayerStateFields26;
				} else {
					switch (this.gameMod) {
					default:
						return JAClientHandler.playerStateFields26;
					case GameMod.MBII:
						return JAClientHandler.playerStateFieldsMBII;
					case GameMod.OJP:
						return JAClientHandler.playerStateFieldsOJP;
					}
				}
			}
		}
		public virtual void ClearState() {
			this.gameMod = GameMod.Undefined;
			this.MaxConfigstrings = JAClientHandler.MaxConfigstringsBase;
		}
		public virtual void SetExtraConfigstringInfo(in ServerInfo serverInfo, in InfoString info) {
			switch (serverInfo.Protocol) {
			case ProtocolVersion.Protocol25:
				serverInfo.Version = ClientVersion.JA_v1_00;
				break;
			case ProtocolVersion.Protocol26:
				serverInfo.Version = ClientVersion.JA_v1_01;
				break;
			}
			if (info.Count <= 0) {
				return;
			}
			serverInfo.GameType = (GameType)info["g_gametype"].Atoi();
			serverInfo.NeedPassword = info["g_needpass"].Atoi() != 0;
			serverInfo.TrueJedi = info["g_jediVmerc"].Atoi() != 0;
			if (serverInfo.GameType == GameType.Duel || serverInfo.GameType == GameType.PowerDuel) {
				serverInfo.WeaponDisable = info["g_duelWeaponDisable"].Atoi() != 0;
			} else {
				serverInfo.WeaponDisable = info["g_weaponDisable"].Atoi() != 0;
			}
			serverInfo.ForceDisable = info["g_forcePowerDisable"].Atoi() != 0;
		}
		private enum GameMod {
			Undefined,
			Base,
			MBII,
			OJP
		}
		private static readonly NetFieldsArray entityStateFields26 = new NetFieldsArray(typeof(EntityState)) {
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32(), 32  }, //  Replace PosType with real type and double check. 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1, 0  }, //  Replace PosType with real type and double check. Also replace sizeof type near end. 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32(), 0  }, //  Replace PosType with real type and double check. 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*1, 0  }, //  Replace AposType with real type and double check. Also replace sizeof type near end. 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*2, 0  }, //  Replace PosType with real type and double check. Also replace sizeof type near end. 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32(), 0  }, //  Replace AposType with real type and double check. 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32(), 0  }, //  Replace PosType with real type and double check. 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*1, 0  }, //  Replace PosType with real type and double check. Also replace sizeof type near end. 
			{	nameof(EntityState.EntityType)	,	8	},
			{ nameof(EntityState.Angles),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*2, 0  }, //  Replace PosType with real type and double check. Also replace sizeof type near end. 
			{ nameof(EntityState.Origin), 0  },
			{ nameof(EntityState.Origin),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(EntityState.Origin),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			// does this need to be 8 bits? // Not detected
			{ nameof(EntityState.Weapon), 8  },
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Type)).ToInt32(), 8  }, //  Replace AposType with real type and double check. 
			// changed from 12 to 16 // Not detected
			{ nameof(EntityState.LegsAnimation), 16  },
			// suspicious // Not detected
			{ nameof(EntityState.TorsoAnimation), 16  },
			// large use beyond Common.GEntitynumBits - should use generic1 insead // Not detected
			{ nameof(EntityState.GenericEnemyIndex), 32  },
			{	nameof(EntityState.EntityFlags)	,	32	},
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Duration)).ToInt32(), 32  }, //  Replace PosType with real type and double check. 
			// might be able to reduce // Not detected
			{ nameof(EntityState.TeamOwner), 8  },
			{	nameof(EntityState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{ nameof(EntityState.Position),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Type)).ToInt32(), 8  }, //  Replace PosType with real type and double check. 
			{ nameof(EntityState.Angles),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(EntityState.Angles), 0  },
			{ nameof(EntityState.Solid), 24  },
		// flag states barely used - could be moved elsewhere // Not detected
			{ nameof(EntityState.FireFlag), 2  },
			{	nameof(EntityState.Event)	,	10	},
			{ nameof(EntityState.customRGBA),  sizeof(int)*3, 8  }, // Replace sizeof type. 
		// used mostly for players and npcs - appears to be static / never changing // Not detected
			{ nameof(EntityState.customRGBA), 8  },
		// only used in fx system (which rick did) and chunks // Not detected
			{ nameof(EntityState.Speed), 0  },
		// why are npc's clientnum's that big? // Not detected
			{   nameof(EntityState.ClientNum)	,	Common.GEntitynumBits	},
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Base)).ToInt32() + sizeof(float)*2, 0  }, //  Replace AposType with real type and double check. Also replace sizeof type near end. 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Time)).ToInt32(), 32  }, //  Replace AposType with real type and double check. 
		// used mostly for players and npcs - appears to be static / never changing // Not detected
			{ nameof(EntityState.customRGBA),  sizeof(int)*1, 8  }, // Replace sizeof type. 
		// used mostly for players and npcs - appears to be static / never changing // Not detected
			{ nameof(EntityState.customRGBA),  sizeof(int)*2, 8  }, // Replace sizeof type. 
		// multiple meanings // Not detected
			{ nameof(EntityState.SaberEntityNum), Common.GEntitynumBits  },
		// could probably just eliminate and assume a big number // Not detected
			{ nameof(EntityState.G2Radius), 8  },
			{ nameof(EntityState.OtherEntityNum2), Common.GEntitynumBits  },
		// used all over the place // Not detected
			{ nameof(EntityState.Owner), Common.GEntitynumBits  },
			{ nameof(EntityState.ModelIndex2), 8  },
// why was this changed from 0 to 8 ? // Not detected
			{   nameof(EntityState.EventParm)   ,	8	},
			// unknown about size? // Not detected
			{ nameof(EntityState.SaberMove), 8  },
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*1, 0  }, //  Replace AposType with real type and double check. Also replace sizeof type near end. 
			{ nameof(EntityState.boneAngles1),  sizeof(float)*1, 0  }, // Replace sizeof type. 
		// why raised from 8 to -16? // Not detected
			{ nameof(EntityState.ModelIndex), -16  },
		// barely used, could probably be replaced // Not detected
			{ nameof(EntityState.EmplacedOwner), 32  },
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32(), 0  }, //  Replace AposType with real type and double check. 
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Delta)).ToInt32() + sizeof(float)*2, 0  }, //  Replace AposType with real type and double check. Also replace sizeof type near end. 
		// shouldn't these be better off as flags?  otherwise, they may consume more bits this way // Not detected
			{ nameof(EntityState.torsoFlip), 1  },
			{ nameof(EntityState.Angles2),  sizeof(float)*1, 0  }, // Replace sizeof type. 
		// used mostly in saber and npc // Not detected
			{ nameof(EntityState.lookTarget), Common.GEntitynumBits  },
			{ nameof(EntityState.Origin2),  sizeof(float)*2, 0  }, // Replace sizeof type. 
		// randomly used, not sure why this was used instead of svc_noclient // Not detected
		//	if (cent->currentState.modelGhoul2 == 127) // Not detected
		//	{ //not ready to be drawn or initialized.. // Not detected
		//		return; // Not detected
		//	} // Not detected
			{ nameof(EntityState.ModelGhoul2), 8  },
			{ nameof(EntityState.LoopSound), 8  },
			{ nameof(EntityState.Origin2), 0  },
		// multiple purpose bit flag // Not detected
			{ nameof(EntityState.ShouldTarget), 1  },
		// widely used, does not appear that they have to be 16 bits // Not detected
			{ nameof(EntityState.TrickedEntityIndex), 16  },
			{	nameof(EntityState.OtherEntityNum)	,	Common.GEntitynumBits	},
			{ nameof(EntityState.Origin2),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(EntityState.Time2), 32  },
			{ nameof(EntityState.legsFlip), 1  },
		// fully used // Not detected
			{ nameof(EntityState.Bolt2), Common.GEntitynumBits  },
			{ nameof(EntityState.ConstantLight), 32  },
			{ nameof(EntityState.Time), 32  },
		// why doesn't lookTarget just indicate this? // Not detected
			{ nameof(EntityState.hasLookTarget), 1  },
			{ nameof(EntityState.boneAngles1),  sizeof(float)*2, 0  }, // Replace sizeof type. 
		// used for both force pass and an emplaced gun - gun is just a flag indicator // Not detected
			{ nameof(EntityState.ActiveForcePass), 6  },
		// used to indicate health // Not detected
			{ nameof(EntityState.health), 10  },
		// appears to have multiple means, could be eliminated by indicating a sound set differently // Not detected
			{ nameof(EntityState.loopIsSoundset), 1  },
			{ nameof(EntityState.saberHolstered), 2  },
		//NPC-SPECIFIC: // Not detected
		// both are used for NPCs sabers, though limited // Not detected
			{ nameof(EntityState.npcSaber1), 9  },
			{ nameof(EntityState.maxhealth), 10  },
			{ nameof(EntityState.TrickedEntityIndex2), 16  },
		// appear to only be 18 powers? // Not detected
			{ nameof(EntityState.ForcePowersActive), 32  },
		// used, doesn't appear to be flexible // Not detected
			{ nameof(EntityState.iModelScale), 10  },
		// full bits used // Not detected
			{ nameof(EntityState.Powerups), 16  },
		// can this be reduced? // Not detected
			{ nameof(EntityState.soundSetIndex), 8  },
		// looks like this can be reduced to 4? (ship parts = 4, people parts = 2) // Not detected
			{ nameof(EntityState.brokenLimbs), 8  },
			{ nameof(EntityState.csSounds_Std), 8  },
		// used extensively // Not detected
			{ nameof(EntityState.SaberInFlight), 1  },
			{ nameof(EntityState.Angles2), 0  },
			{ nameof(EntityState.Frame), 16  },
			{ nameof(EntityState.Angles2),  sizeof(float)*2, 0  }, // Replace sizeof type. 
		// why not use torsoAnim and set a flag to do the same thing as forceFrame (saberLockFrame) // Not detected
			{ nameof(EntityState.ForceFrame), 16  },
			{ nameof(EntityState.Generic1), 8  },
		// do we really need 4 indexes? // Not detected
			{ nameof(EntityState.boneIndex1), 6  },
		// only 54 classes, could cut down 2 bits // Not detected
			{ nameof(EntityState.NPC_class), 8  },
			{ nameof(EntityState.AngularPosition),  Marshal.OffsetOf(typeof(Trajectory),nameof(Trajectory.Duration)).ToInt32(), 32  }, //  Replace AposType with real type and double check. 
		// there appears to be only 2 different version of parms passed - a flag would better be suited // Not detected
			{ nameof(EntityState.boneOrient), 9  },
		// this looks to be a single bit flag // Not detected
			{ nameof(EntityState.Bolt1), 8  },
			{ nameof(EntityState.TrickedEntityIndex3), 16  },
		// in use for vehicles // Not detected
			{ nameof(EntityState.m_iVehicleNum), Common.GEntitynumBits  },
			{ nameof(EntityState.TrickedEntityIndex4), 16  },
		// but why is there an opposite state of surfaces field? // Not detected
			{ nameof(EntityState.surfacesOff), 32  },
			{ nameof(EntityState.eFlags2), 10  },
		// should be bit field // Not detected
			{ nameof(EntityState.IsJediMaster), 1  },
		// should be bit field // Not detected
			{ nameof(EntityState.isPortalEnt), 1  },
		// possible multiple definitions // Not detected
			{ nameof(EntityState.heldByClient), 6  },
		// this does not appear to be used in any production or non-cheat fashion - REMOVE // Not detected
			{ nameof(EntityState.ragAttach), Common.GEntitynumBits  },
		// used only in one spot for seige // Not detected
			{ nameof(EntityState.boltToPlayer), 6  },
			{ nameof(EntityState.npcSaber2), 9  },
			{ nameof(EntityState.csSounds_Combat), 8  },
			{ nameof(EntityState.csSounds_Extra), 8  },
			{ nameof(EntityState.csSounds_Jedi), 8  },
		// used only for surfaces on NPCs // Not detected
			{ nameof(EntityState.surfacesOn), 32  },
			{ nameof(EntityState.boneIndex2), 6  },
			{ nameof(EntityState.boneIndex3), 6  },
			{ nameof(EntityState.boneIndex4), 6  },
			{ nameof(EntityState.boneAngles1), 0  },
			{ nameof(EntityState.boneAngles2), 0  },
			{ nameof(EntityState.boneAngles2),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(EntityState.boneAngles2),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(EntityState.boneAngles3), 0  },
			{ nameof(EntityState.boneAngles3),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(EntityState.boneAngles3),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(EntityState.boneAngles4), 0  },
			{ nameof(EntityState.boneAngles4),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(EntityState.boneAngles4),  sizeof(float)*2, 0  }, // Replace sizeof type. 
		 // Not detected
		//rww - for use by mod authors only // Not detected
		//#ifndef _XBOX // Not detected
			{ nameof(EntityState.userInt1), 1  },
			{ nameof(EntityState.userInt2), 1  },
			{ nameof(EntityState.userInt3), 1  },
			{ nameof(EntityState.userFloat1), 1  },
			{ nameof(EntityState.userFloat2), 1  },
			{ nameof(EntityState.userFloat3), 1  },
			{ nameof(EntityState.userVec1), 1  },
			{ nameof(EntityState.userVec1),  sizeof(float)*1, 1  }, // Replace sizeof type. 
			{ nameof(EntityState.userVec1),  sizeof(float)*2, 1  }, // Replace sizeof type. 
			{ nameof(EntityState.userVec2), 1  },
			{ nameof(EntityState.userVec2),  sizeof(float)*1, 1  }, // Replace sizeof type. 
			{ nameof(EntityState.userVec2),  sizeof(float)*2, 1  }, // Replace sizeof type. 
		//#endif // Not detected
		};
		private static readonly NetFieldsArray entityStateFieldsMBII = new NetFieldsArray(JAClientHandler.entityStateFields26);
		private static readonly NetFieldsArray entityStateFieldsOJP = new NetFieldsArray(JAClientHandler.entityStateFields26)
			.Override(120, 32)
			.Override(122, 32);
		private static readonly NetFieldsArray playerStateFields26 = new NetFieldsArray(typeof(PlayerState)) {
			{ nameof(PlayerState.CommandTime), 32  },
			{ nameof(PlayerState.Origin),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.Origin), 0  },
			{ nameof(PlayerState.ViewAngles),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.ViewAngles), 0  },
			{ nameof(PlayerState.Origin),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.Velocity), 0  },
			{ nameof(PlayerState.Velocity),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.Velocity),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.BobCycle), 8  },
			{ nameof(PlayerState.WeaponTime), -16  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(int)*1, 16  }, // Replace sizeof type. 
			{ nameof(PlayerState.Speed), 0  },
			{ nameof(PlayerState.LegsAnimation), 16  },
			{ nameof(PlayerState.DeltaAngles), 16  },
			{ nameof(PlayerState.TorsoAnim), 16  },
			{	nameof(PlayerState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{   nameof(PlayerState.EntityFlags) ,	32	},
			{	0	,	8	},
			{   nameof(PlayerState.EventSequence) ,	16	},
			{ nameof(PlayerState.TorsoTimer), 16  },
			{ nameof(PlayerState.LegsTimer), 16  },
			{ nameof(PlayerState.ViewHeight), -8  },
			{	0	,	4	},
			{ nameof(PlayerState.RocketLockIndex), Common.GEntitynumBits  },
			{ 0, 4  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.GenericEnemyIndex), 32  },
			{	nameof(PlayerState.Events)  ,   sizeof(int)*0	,   10	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*1	,	10	},
			{ nameof(PlayerState.customRGBA), 8  },
			{ nameof(PlayerState.MovementDirection), 4  },
			{ nameof(PlayerState.SaberEntityNum), Common.GEntitynumBits  },
			{ nameof(PlayerState.customRGBA),  sizeof(int)*3, 8  }, // Replace sizeof type. 
			{ nameof(PlayerState.Weaponstate), 4  },
			{ nameof(PlayerState.SaberMove), 32  },
			{ nameof(PlayerState.standheight), 10  },
			{ nameof(PlayerState.crouchheight), 10  },
			{ nameof(PlayerState.Basespeed), -16  },
			{   nameof(PlayerState.PlayerMoveFlags) ,	16	},
			{ nameof(PlayerState.jetpackFuel), 8  },
			{ nameof(PlayerState.cloakFuel), 8  },
			{ nameof(PlayerState.PlayerMoveTime), -16  },
			{ nameof(PlayerState.customRGBA),  sizeof(int)*1, 8  }, // Replace sizeof type. 
			{   nameof(PlayerState.ClientNum) ,	Common.GEntitynumBits	},
			{ nameof(PlayerState.DuelIndex), Common.GEntitynumBits  },
			{ nameof(PlayerState.customRGBA),  sizeof(int)*2, 8  }, // Replace sizeof type. 
			{ nameof(PlayerState.Gravity), 16  },
			{ nameof(PlayerState.Weapon), 8  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(int)*2, 16  }, // Replace sizeof type. 
			{ nameof(PlayerState.SaberCanThrow), 1  },
			{ nameof(PlayerState.ViewAngles),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ 0, 32  }, //  Replace FdType with real type and double check. 
			{ 0, 2  }, //  Replace FdType with real type and double check. 
			{ 0, 32  }, //  Replace FdType with real type and double check. 
			{ 0, 8  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.torsoFlip), 1  },
			{   nameof(PlayerState.ExternalEvent) ,	10	},
			{ nameof(PlayerState.DamageYaw), 8  },
			{ nameof(PlayerState.DamageCount), 8  },
			{ nameof(PlayerState.InAirAnim), 1  },
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*1   ,   8	},
			{	0	,	2	},
			{ nameof(PlayerState.SaberAttackChainCount), 4  },
			{	nameof(PlayerState.PlayerMoveType)	,	8	},
			{	nameof(PlayerState.ExternalEventParm)	,	8	},
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*0   ,   -16	},
			{ nameof(PlayerState.lookTarget), Common.GEntitynumBits  },
			{ nameof(PlayerState.WeaponChargeSubtractTime), 32  },
			{ nameof(PlayerState.WeaponChargeTime), 32  },
			{ nameof(PlayerState.legsFlip), 1  },
			{ nameof(PlayerState.DamageEvent), 8  },
			{ nameof(PlayerState.RocketTargetTime), 32  },
			{ nameof(PlayerState.ActiveForcePass), 6  },
			{ nameof(PlayerState.ElectrifyTime), 32  },
			{ 0, 0  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.LoopSound), 16  },
			{ nameof(PlayerState.hasLookTarget), 1  },
			{ nameof(PlayerState.SaberBlocked), 8  },
			{ nameof(PlayerState.DamageType), 2  },
			{ nameof(PlayerState.RocketLockTime), 32  },
			{ nameof(PlayerState.ForceHandExtend), 8  },
			{ nameof(PlayerState.SaberHolstered), 2  },
			{ 0, 32  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.DamagePitch), 8  },
			{   nameof(PlayerState.VehicleNum) ,	Common.GEntitynumBits	},
			{ nameof(PlayerState.Generic1), 8  },
			{ nameof(PlayerState.JumpPadEntity), 10  },
			{ nameof(PlayerState.HasDetPackPlanted), 1  },
			{ nameof(PlayerState.SaberInFlight), 1  },
			{ nameof(PlayerState.ForceDodgeAnim), 16  },
			{ nameof(PlayerState.ZoomMode), 2  },
			{ nameof(PlayerState.hackingTime), 32  },
			{ nameof(PlayerState.ZoomTime), 32  },
			{ nameof(PlayerState.brokenLimbs), 8  },
			{ nameof(PlayerState.ZoomLocked), 1  },
			{ nameof(PlayerState.ZoomFov), 0  },
			{ 0, 32  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.FallingToDeath), 32  },
			{0, 16  }, //  Replace FdType with real type and double check. 
			{ 0, 16  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.lastHitLoc),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ 0, 16  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.lastHitLoc), 0  },
			{ nameof(PlayerState.eFlags2), 10  },
			{ 0, 16  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.lastHitLoc),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ 0, 1  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.SaberLockTime), 32  },
			{ nameof(PlayerState.SaberLockFrame), 16  },
			{ 0, 2  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.SaberLockEnemy), Common.GEntitynumBits  },
			{ 0, 1  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.EmplacedIndex), Common.GEntitynumBits  },
			{ nameof(PlayerState.HolocronBits), 32  },
			{ nameof(PlayerState.IsJediMaster), 1  },
			{ nameof(PlayerState.ForceRestricted), 1  },
			{ nameof(PlayerState.TrueJedi), 1  },
			{ nameof(PlayerState.TrueNonJedi), 1  },
			{ nameof(PlayerState.DuelTime), 32  },
			{ nameof(PlayerState.DuelInProgress), 1  },
			{ nameof(PlayerState.SaberLockAdvance), 1  },
			{ nameof(PlayerState.heldByClient), 6  },
			{ nameof(PlayerState.ragAttach), Common.GEntitynumBits  },
			{ nameof(PlayerState.iModelScale), 10  },
			{ nameof(PlayerState.hackingBaseTime), 16  },
		 // Not detected
		//rww - for use by mod authors only // Not detected
		//#ifndef _XBOX // Not detected
			{ nameof(PlayerState.userInt1), 1  },
			{ nameof(PlayerState.userInt2), 1  },
			{ nameof(PlayerState.userInt3), 1  },
			{ nameof(PlayerState.userFloat1), 1  },
			{ nameof(PlayerState.userFloat2), 1  },
			{ nameof(PlayerState.userFloat3), 1  },
			{ nameof(PlayerState.userVec1), 1  },
			{ nameof(PlayerState.userVec1),  sizeof(float)*1, 1  }, // Replace sizeof type. 
			{ nameof(PlayerState.userVec1),  sizeof(float)*2, 1  }, // Replace sizeof type. 
			{ nameof(PlayerState.userVec2), 1  },
			{ nameof(PlayerState.userVec2),  sizeof(float)*1, 1  }, // Replace sizeof type. 
			{ nameof(PlayerState.userVec2),  sizeof(float)*2, 1  }, // Replace sizeof type. 
		//#endif // Not detected
		};
		private static readonly NetFieldsArray playerStateFieldsMBII = new NetFieldsArray(JAClientHandler.playerStateFields26);
		private static readonly NetFieldsArray playerStateFieldsOJP = new NetFieldsArray(JAClientHandler.playerStateFields26)
			.Override(125, 10)
			.Override(127, 32);
		private static readonly NetFieldsArray pilotPlayerStateFields26 = new NetFieldsArray(typeof(PlayerState)) {
			{ nameof(PlayerState.CommandTime), 32  },
			{ nameof(PlayerState.Origin),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.Origin), 0  },
			{ nameof(PlayerState.ViewAngles),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.ViewAngles), 0  },
			{ nameof(PlayerState.Origin),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.WeaponTime), -16  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(int)*1, 16  }, // Replace sizeof type. 
			{ nameof(PlayerState.DeltaAngles), 16  },
			{ nameof(PlayerState.EntityFlags), 32  },
			{ nameof(PlayerState.EventSequence), 16  },
			{ nameof(PlayerState.RocketLockIndex), Common.GEntitynumBits  },
			{ nameof(PlayerState.Events), 10  },
			{ nameof(PlayerState.Events),  sizeof(int)*1, 10  }, // Replace sizeof type. 
			{ nameof(PlayerState.Weaponstate), 4  },
			{ nameof(PlayerState.PlayerMoveFlags), 16  },
			{ nameof(PlayerState.PlayerMoveTime), -16  },
			{ nameof(PlayerState.ClientNum), Common.GEntitynumBits  },
			{ nameof(PlayerState.Weapon), 8  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(int)*2, 16  }, // Replace sizeof type. 
			{ nameof(PlayerState.ViewAngles),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.ExternalEvent), 10  },
			{ nameof(PlayerState.EventParms),  sizeof(int)*1, 8  }, // Replace sizeof type. 
			{ nameof(PlayerState.PlayerMoveType), 8  },
			{ nameof(PlayerState.ExternalEventParm), 8  },
			{ nameof(PlayerState.EventParms), -16  },
			{ nameof(PlayerState.WeaponChargeSubtractTime), 32  },
			{ nameof(PlayerState.WeaponChargeTime), 32  },
			{ nameof(PlayerState.RocketTargetTime), 32  },
			{0, 0  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.RocketLockTime), 32  },
			{ nameof(PlayerState.VehicleNum), Common.GEntitynumBits  },
			{ nameof(PlayerState.Generic1), 8  },
			{ nameof(PlayerState.eFlags2), 10  },
		 // Not detected
		//===THESE SHOULD NOT BE CHANGING OFTEN==================================================================== // Not detected
			{ nameof(PlayerState.LegsAnimation), 16  },
			{ nameof(PlayerState.TorsoAnim), 16  },
			{ nameof(PlayerState.TorsoTimer), 16  },
			{ nameof(PlayerState.LegsTimer), 16  },
			{ nameof(PlayerState.jetpackFuel), 8  },
			{ nameof(PlayerState.cloakFuel), 8  },
			{ nameof(PlayerState.SaberCanThrow), 1  },
			{ 0, 32  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.torsoFlip), 1  },
			{ nameof(PlayerState.legsFlip), 1  },
			{0, 32  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.HasDetPackPlanted), 1  },
			{ 0, 32  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.SaberInFlight), 1  },
			{ 0, 16  }, //  Replace FdType with real type and double check. 
			{ 0, 16  }, //  Replace FdType with real type and double check. 
			{ 0, 16  }, //  Replace FdType with real type and double check. 
			{ 0, 16  }, //  Replace FdType with real type and double check. 
			{ 0, 1  }, //  Replace FdType with real type and double check. 
			{0, 2  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.HolocronBits), 32  },
			{ 0, 8  }, //  Replace FdType with real type and double check. 
		 // Not detected
		//===THE REST OF THESE SHOULD NOT BE RELEVANT, BUT, FOR SAFETY, INCLUDE THEM ANYWAY, JUST AT THE BOTTOM=============================================================== // Not detected
			{ nameof(PlayerState.Velocity), 0  },
			{ nameof(PlayerState.Velocity),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.Velocity),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.BobCycle), 8  },
			{ nameof(PlayerState.Speed), 0  },
			{ nameof(PlayerState.GroundEntityNum), Common.GEntitynumBits  },
			{ nameof(PlayerState.ViewHeight), -8  },
			{ 0, 4  }, //  Replace FdType with real type and double check. 
			{ 0, 4  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.GenericEnemyIndex), 32  },
			{ nameof(PlayerState.customRGBA), 8  },
			{ nameof(PlayerState.MovementDirection), 4  },
			{ nameof(PlayerState.SaberEntityNum), Common.GEntitynumBits  },
			{ nameof(PlayerState.customRGBA),  sizeof(int)*3, 8  }, // Replace sizeof type. 
			{ nameof(PlayerState.SaberMove), 32  },
			{ nameof(PlayerState.standheight), 10  },
			{ nameof(PlayerState.crouchheight), 10  },
			{ nameof(PlayerState.Basespeed), -16  },
			{ nameof(PlayerState.customRGBA),  sizeof(int)*1, 8  }, // Replace sizeof type. 
			{ nameof(PlayerState.DuelIndex), Common.GEntitynumBits  },
			{ nameof(PlayerState.customRGBA),  sizeof(int)*2, 8  }, // Replace sizeof type. 
			{ nameof(PlayerState.Gravity), 16  },
			{ 0, 32  }, //  Replace FdType with real type and double check. 
			{ 0, 2  }, //  Replace FdType with real type and double check. 
			{ 0, 8  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.DamageYaw), 8  },
			{ nameof(PlayerState.DamageCount), 8  },
			{ nameof(PlayerState.InAirAnim), 1  },
			{ 0, 2  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.SaberAttackChainCount), 4  },
			{ nameof(PlayerState.lookTarget), Common.GEntitynumBits  },
			{ nameof(PlayerState.moveDir),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.moveDir), 0  },
			{ nameof(PlayerState.DamageEvent), 8  },
			{ nameof(PlayerState.moveDir),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.ActiveForcePass), 6  },
			{ nameof(PlayerState.ElectrifyTime), 32  },
			{ nameof(PlayerState.DamageType), 2  },
			{ nameof(PlayerState.LoopSound), 16  },
			{ nameof(PlayerState.hasLookTarget), 1  },
			{ nameof(PlayerState.SaberBlocked), 8  },
			{ nameof(PlayerState.ForceHandExtend), 8  },
			{ nameof(PlayerState.SaberHolstered), 2  },
			{ nameof(PlayerState.DamagePitch), 8  },
			{ nameof(PlayerState.JumpPadEntity), 10  },
			{ nameof(PlayerState.ForceDodgeAnim), 16  },
			{ nameof(PlayerState.ZoomMode), 2  },
			{ nameof(PlayerState.hackingTime), 32  },
			{ nameof(PlayerState.ZoomTime), 32  },
			{ nameof(PlayerState.brokenLimbs), 8  },
			{ nameof(PlayerState.ZoomLocked), 1  },
			{ nameof(PlayerState.ZoomFov), 0  },
			{ nameof(PlayerState.FallingToDeath), 32  },
			{ nameof(PlayerState.lastHitLoc),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.lastHitLoc), 0  },
			{ nameof(PlayerState.lastHitLoc),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.SaberLockTime), 32  },
			{ nameof(PlayerState.SaberLockFrame), 16  },
			{ nameof(PlayerState.SaberLockEnemy), Common.GEntitynumBits  },
			{0, 1  }, //  Replace FdType with real type and double check. 
			{ nameof(PlayerState.EmplacedIndex), Common.GEntitynumBits  },
			{ nameof(PlayerState.IsJediMaster), 1  },
			{ nameof(PlayerState.ForceRestricted), 1  },
			{ nameof(PlayerState.TrueJedi), 1  },
			{ nameof(PlayerState.TrueNonJedi), 1  },
			{ nameof(PlayerState.DuelTime), 32  },
			{ nameof(PlayerState.DuelInProgress), 1  },
			{ nameof(PlayerState.SaberLockAdvance), 1  },
			{ nameof(PlayerState.heldByClient), 6  },
			{ nameof(PlayerState.ragAttach), Common.GEntitynumBits  },
			{ nameof(PlayerState.iModelScale), 10  },
			{ nameof(PlayerState.hackingBaseTime), 16  },
		//===NEVER SEND THESE, ONLY USED BY VEHICLES============================================================== // Not detected
		//rww - for use by mod authors only // Not detected
		//#ifndef _XBOX // Not detected
			{ nameof(PlayerState.userInt1), 1  },
			{ nameof(PlayerState.userInt2), 1  },
			{ nameof(PlayerState.userInt3), 1  },
			{ nameof(PlayerState.userFloat1), 1  },
			{ nameof(PlayerState.userFloat2), 1  },
			{ nameof(PlayerState.userFloat3), 1  },
			{ nameof(PlayerState.userVec1), 1  },
			{ nameof(PlayerState.userVec1),  sizeof(float)*1, 1  }, // Replace sizeof type. 
			{ nameof(PlayerState.userVec1),  sizeof(float)*2, 1  }, // Replace sizeof type. 
			{ nameof(PlayerState.userVec2), 1  },
			{ nameof(PlayerState.userVec2),  sizeof(float)*1, 1  }, // Replace sizeof type. 
			{ nameof(PlayerState.userVec2),  sizeof(float)*2, 1  }, // Replace sizeof type. 
		//#endif // Not detected
		};
		private static readonly NetFieldsArray vehPlayerStateFields26 = new NetFieldsArray(typeof(PlayerState)) {
			{ nameof(PlayerState.CommandTime), 32  },
			{ nameof(PlayerState.Origin),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.Origin), 0  },
			{ nameof(PlayerState.ViewAngles),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.ViewAngles), 0  },
			{ nameof(PlayerState.Origin),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.Velocity), 0  },
			{ nameof(PlayerState.Velocity),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.Velocity),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.WeaponTime), -16  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(int)*1, 16  }, // Replace sizeof type. 
			{ nameof(PlayerState.Speed), 0  },
			{ nameof(PlayerState.LegsAnimation), 16  },
			{ nameof(PlayerState.DeltaAngles), 16  },
			{ nameof(PlayerState.GroundEntityNum), Common.GEntitynumBits  },
			{ nameof(PlayerState.EntityFlags), 32  },
			{ nameof(PlayerState.EventSequence), 16  },
			{ nameof(PlayerState.LegsTimer), 16  },
			{ nameof(PlayerState.RocketLockIndex), Common.GEntitynumBits  },
			{ nameof(PlayerState.Events), 10  },
			{ nameof(PlayerState.Events),  sizeof(int)*1, 10  }, // Replace sizeof type. 
			{ nameof(PlayerState.Weaponstate), 4  },
			{ nameof(PlayerState.PlayerMoveFlags), 16  },
			{ nameof(PlayerState.PlayerMoveTime), -16  },
			{ nameof(PlayerState.ClientNum), Common.GEntitynumBits  },
			{ nameof(PlayerState.Gravity), 16  },
			{ nameof(PlayerState.Weapon), 8  },
			{ nameof(PlayerState.DeltaAngles),  sizeof(int)*2, 16  }, // Replace sizeof type. 
			{ nameof(PlayerState.ViewAngles),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.ExternalEvent), 10  },
			{ nameof(PlayerState.EventParms),  sizeof(int)*1, 8  }, // Replace sizeof type. 
			{ nameof(PlayerState.PlayerMoveType), 8  },
			{ nameof(PlayerState.ExternalEventParm), 8  },
			{ nameof(PlayerState.EventParms), -16  },
			{ nameof(PlayerState.vehOrientation), 0  },
			{ nameof(PlayerState.vehOrientation),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.moveDir),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.moveDir), 0  },
			{ nameof(PlayerState.vehOrientation),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.moveDir),  sizeof(float)*2, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.RocketTargetTime), 32  },
			{ nameof(PlayerState.ElectrifyTime), 32  },
			{ nameof(PlayerState.LoopSound), 16  },
			{ nameof(PlayerState.RocketLockTime), 32  },
			{ nameof(PlayerState.VehicleNum), Common.GEntitynumBits  },
			{ nameof(PlayerState.vehTurnaroundTime), 32  },
			{ nameof(PlayerState.hackingTime), 32  },
			{ nameof(PlayerState.brokenLimbs), 8  },
			{ nameof(PlayerState.vehWeaponsLinked), 1  },
			{ nameof(PlayerState.hyperSpaceTime), 32  },
			{ nameof(PlayerState.eFlags2), 10  },
			{ nameof(PlayerState.hyperSpaceAngles),  sizeof(float)*1, 0  }, // Replace sizeof type. 
			{ nameof(PlayerState.vehBoarding), 1  },
			{ nameof(PlayerState.vehTurnaroundIndex), Common.GEntitynumBits  },
			{ nameof(PlayerState.vehSurfaces), 16  },
			{ nameof(PlayerState.hyperSpaceAngles), 0  },
			{ nameof(PlayerState.hyperSpaceAngles),  sizeof(float)*2, 0  }, // Replace sizeof type. 
		 // Not detected
		//rww - for use by mod authors only // Not detected
		//#ifndef _XBOX // Not detected
			{ nameof(PlayerState.userInt1), 1  },
			{ nameof(PlayerState.userInt2), 1  },
			{ nameof(PlayerState.userInt3), 1  },
			{ nameof(PlayerState.userFloat1), 1  },
			{ nameof(PlayerState.userFloat2), 1  },
			{ nameof(PlayerState.userFloat3), 1  },
			{ nameof(PlayerState.userVec1), 1  },
			{ nameof(PlayerState.userVec1),  sizeof(float)*1, 1  }, // Replace sizeof type. 
			{ nameof(PlayerState.userVec1),  sizeof(float)*2, 1  }, // Replace sizeof type. 
			{ nameof(PlayerState.userVec2), 1  },
			{ nameof(PlayerState.userVec2),  sizeof(float)*1, 1  }, // Replace sizeof type. 
			{ nameof(PlayerState.userVec2),  sizeof(float)*2, 1  }, // Replace sizeof type. 
		//#endif // Not detected
		};
	}
}
