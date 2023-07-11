using System.Runtime.InteropServices;

namespace JKClient {

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct ForceData
    {
		
		public unsafe fixed int ForcePowerDebounce[18];   //for effects that must have an interval
		public int ForcePowersKnown;
		public int ForcePowersActive;
		public int ForcePowerSelected;
		public int ForceButtonNeedRelease;
		public unsafe fixed int ForcePowerDuration[18];
		public int ForcePower;
		public int ForcePowerMax;
		public int ForcePowerRegenDebounceTime;
		public unsafe fixed int ForcePowerLevel[18];      //so we know the max forceJump power you have
		public unsafe fixed int ForcePowerBaseLevel[18];
		public int ForceUsingAdded;
		public float ForceJumpZStart;                    //So when you land, you don't get hurt as much
		public float ForceJumpCharge;                    //you're current forceJump charge-up level, increases the longer you hold the force jump button down
		public int ForceJumpSound;
		public int ForceJumpAddTime;
		public int ForceGripEntityNum;                 //what entity I'm gripping
		public int ForceGripDamageDebounceTime;        //debounce for grip damage
		public float ForceGripBeingGripped;              //if > level.time then client is in someone's grip
		public int ForceGripCripple;                   //if != 0 then make it so this client can't move quickly (he's being gripped)
		public int ForceGripUseTime;                   //can't use if > level.time
		public float ForceGripSoundTime;
		public float ForceGripStarted;                   //level.time when the grip was activated
		public float ForceSpeedSmash;
		public float ForceSpeedDoDamage;
		public int ForceSpeedHitIndex;                 //if we hit another player and got hurt, hurt them too
		public int ForceHealTime;
		public int ForceHealAmount;

		//This hurts me somewhat to do, but there's no other real way to allow completely "dynamic" mindtricking.
		public int ForceMindtrickTargetIndex; //0-15
		public int ForceMindtrickTargetIndex2; //16-32
		public int ForceMindtrickTargetIndex3; //33-48
		public int ForceMindtrickTargetIndex4; //49-64

		public int ForceRageRecoveryTime;
		public int ForceDrainEntNum;
		public float ForceDrainTime;

		public int ForceDoInit;

		public int ForceSide;
		public int ForceRank;

		public int ForceDeactivateAll;

		public unsafe fixed int KillSoundEntIndex[6]; //this goes here so it doesn't get wiped over respawn

		public QuakeBoolean SentryDeployed;
		
		public int SaberAnimLevel;
		public int SaberDrawAnimLevel;

		public int Suicides;

		public int PrivateDuelTime;

    }

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct PlayerState {
		public int Dummy;
		public PlayerMoveType PlayerMoveType;
		public int PlayerMoveFlags;
		public int GroundEntityNum;
		public int EntityFlags;
		public int EventSequence;
		public unsafe fixed int Events[PlayerState.MaxEvents];
		public unsafe fixed int EventParms[PlayerState.MaxEvents];
		public int ExternalEvent;
		public int ExternalEventParm;
		public int ClientNum;
		public int EntityEventSequence;
		public int VehicleNum;
		public unsafe fixed int Stats[(int)Stat.Max];
		//IMPORTANT: update all playerStateFields in Message after adding new fields
		public static readonly PlayerState Null = new PlayerState();
		public const int MaxEvents = 2;

		// This is not complete. If you need a particular field to be there in the converted entity, make sure to add it here
		public unsafe void ToEntityState(ref EntityState es) {
			if (this.PlayerMoveType == PlayerMoveType.Intermission || this.PlayerMoveType == PlayerMoveType.Spectator) {
				es.EntityType = (int)ClientGame.EntityType.Invisible;
			} else if (this.Stats[(int)Stat.Health] <= Common.GibHealth) {
				es.EntityType = (int)ClientGame.EntityType.Invisible;
			} else {
				es.EntityType = (int)ClientGame.EntityType.Player;
			}
			es.Number = es.ClientNum = this.ClientNum;

			fixed(float* tmp = Origin)
            {
				fixed(float* tmp2 = es.Position.Base) {
					Common.VectorCopy(tmp,tmp2);
				}
			}
			/*if (snap)
			{
				SnapVector(s->pos.trBase);
			}*/
			// set the trDelta for flag direction
			es.Position.Type = TrajectoryType.TR_INTERPOLATE;
			fixed (float* tmp = Velocity)
			{
				fixed (float* tmp2 = es.Position.Delta)
				{
					Common.VectorCopy(tmp, tmp2);
				}
			}

			es.AngularPosition.Type = TrajectoryType.TR_INTERPOLATE;
			fixed (float* tmp = ViewAngles)
			{
				fixed (float* tmp2 = es.AngularPosition.Base)
				{
					Common.VectorCopy(tmp, tmp2);
				}
			}
			/*if (snap)
			{
				SnapVector(s->apos.trBase);
			}*/

			if (this.ExternalEvent != 0) {
				es.Event = this.ExternalEvent;
				es.EventParm = this.ExternalEventParm;
			} else if (this.EntityEventSequence < this.EventSequence) {
				if (this.EntityEventSequence < this.EventSequence - PlayerState.MaxEvents) {
					this.EntityEventSequence = this.EventSequence - PlayerState.MaxEvents;
				}
				int sequence = this.EntityEventSequence & (PlayerState.MaxEvents-1);
				es.Event = this.Events[sequence] | ((this.EntityEventSequence & 3) << 8);
				es.EventParm = this.EventParms[sequence];
				this.EntityEventSequence++;
			}
			es.GroundEntityNum = this.GroundEntityNum;

			es.Powerups = 0;
			for (int i = 0; i < Common.MaxPowerUps; i++)
			{
				if (PowerUps[i] != 0)
				{
					es.Powerups |= 1 << i;
				}
			}

			es.EntityFlags = this.EntityFlags;

			//es.FilledFromPlayerState = true;
		}




		// From eternaljk2mv:
		// Not all of these are used yet and need to be filled in playerstatefields
		public int CommandTime;    // cmd->serverTime of last executed command
		public int BobCycle;       // for view bobbing and footstep generation
		public int PlayerMoveTime;

		public unsafe fixed float Origin[3];
		public unsafe fixed float Velocity[3];
		public int WeaponTime;
		public int WeaponChargeTime;
		public int WeaponChargeSubtractTime;
		public int Gravity;
		public int Speed;
		public int Basespeed; //used in prediction to know base server g_speed value when modifying speed between updates
		public unsafe fixed int DeltaAngles[3];    // add to command angles to get view direction
								// changed by spawns, rotating objects, and teleporters

		public int UseTime;

		public int LegsTimer;      // don't change low priority animations until this runs out
		public int LegsAnimation;       // mask off ANIM_TOGGLEBIT

		public int TorsoTimer;     // don't change low priority animations until this runs out
		public int TorsoAnim;      // mask off ANIM_TOGGLEBIT

		public int MovementDirection;    // a number 0 to 7 that represents the reletive angle
							// of movement to the view angle (axial and diagonals)
							// when at rest, the value will remain unchanged
							// used to twist the legs during strafing

		public int ExternalEventTime;

		public int Weapon;         // copied to entityState_t->weapon
		public int Weaponstate;

		public unsafe fixed float ViewAngles[3];      // for fixed views
		public int ViewHeight;

		// damage feedback
		public int DamageEvent;    // when it changes, latch the other parms
		public int DamageYaw;
		public int DamagePitch;
		public int DamageCount;
		public int DamageType;

		public int Paintime;       // used for both game and client side to process the pain twitch - NOT sent across the network
		public int PainDirection;  // NOT sent across the network
		float YawAngle;     // NOT sent across the network
		public QuakeBoolean Yawing;            // NOT sent across the network
		float PitchAngle;       // NOT sent across the network
		public QuakeBoolean Pitching;      // NOT sent across the network

		// these also need the constants ported:
		public unsafe fixed int Persistant[Common.MaxPersistant]; // stats that aren't cleared on death
		public unsafe fixed int PowerUps[Common.MaxPowerUps]; // level.time that the powerup runs out
		public unsafe fixed int Ammo[Common.MaxWeapons];

		public int Generic1;
		public int LoopSound;
		public int JumpPadEntity;    // jumppad entity hit this frame

		// not communicated over the net at all
		public int Ping;           // server to game info for scoreboard
		public int PlayerMoveFrameCount;   // FIXME: don't transmit over the network
		public int JumpPadFrame;

		public int LastOnGround;   //last time you were on the ground

		public QuakeBoolean SaberInFlight;
		public QuakeBoolean SaberActive;

		public int SaberMove;
		public int SaberBlocking;
		public int SaberBlocked;

		public int SaberLockTime;
		public int SaberLockEnemy;
		public int SaberLockFrame; //since we don't actually have the ability to get the current anim frame
		public int SaberLockHits; //every x number of buttons hits, allow one push forward in a saber lock (server only)
		public QuakeBoolean SaberLockAdvance; //do an advance (sent across net as 1 bit)

		public int SaberEntityNum;
		public float SaberEntityDist;
		public int SaberEntityState;
		public int SaberThrowDelay;
		public QuakeBoolean SaberCanThrow;
		public int SaberDidThrowTime;
		public int SaberDamageDebounceTime;
		public int SaberHitWallSoundDebounceTime;
		public int SaberEventFlags;

		public int RocketLockIndex;
		public float RocketLastValidTime;
		public float RocketLockTime;
		public float RocketTargetTime;

		public int EmplacedIndex;
		public float EmplacedTime;

		public QuakeBoolean IsJediMaster;
		public QuakeBoolean ForceRestricted;
		public QuakeBoolean TrueJedi;
		public QuakeBoolean TrueNonJedi;
		public int SaberIndex;

		public int GenericEnemyIndex;
		public float DroneFireTime;
		public float DroneExistTime;

		public int ActiveForcePass;

		public QuakeBoolean HasDetPackPlanted; //better than taking up an eFlag isn't it?

		//float HolocronsCarried[NUM_FORCE_POWERS];
		public int HolocronCantTouch;
		public float HolocronCantTouchTime; //for keeping track of the last holocron that just popped out of me (if any)
		public int HolocronBits;

		public int LegsAnimExecute;
		public int TorsoAnimExecute;
		public int FullAnimExecute;

		public int ElectrifyTime;

		public int SaberAttackSequence;
		public int SaberIdleWound;
		public int SaberAttackWound;
		public int SaberBlockTime;

		public int OtherKiller;
		public int OtherKillerTime;
		public int OtherKillerDebounceTime;

		// Needs porting the forcedata stuff...
		//forcedata_t fd;
		public ForceData forceData;
		public QuakeBoolean ForceJumpFlip;
		public int ForceHandExtend;
		public int ForceHandExtendTime;

		public int ForceRageDraintime;

		public int ForceDodgeAnim;
		public QuakeBoolean QuickerGetup;

		public int GroundTime;     // time when first left ground

		public int FootstepTime;

		public int OtherSoundTime;
		public float OtherSoundLen;

		public int ForceGripMoveInterval;
		public int ForceGripChangeMovetype;

		public int ForceKickFlip;

		public int DuelIndex;
		public int DuelTime;
		public QuakeBoolean DuelInProgress;

		public int SaberAttackChainCount;

		public QuakeBoolean SaberHolstered;

		public QuakeBoolean UsingATST;
		public QuakeBoolean AtstAltFire;
		public int HoldMoveTime;

		public int ForceAllowDeactivateTime;

		// zoom key
		public int ZoomMode;       // 0 - not zoomed, 1 - disruptor weapon
		public int ZoomTime;
		public QuakeBoolean ZoomLocked;
		public float ZoomFov;
		public int ZoomLockTime;

		public int FallingToDeath;

		public int UseDelay;

		public QuakeBoolean InAirAnim;

		public QuakeBoolean DualBlade;

		public unsafe fixed float lastHitLoc[3];



		// JKA:
		public unsafe fixed float moveDir[3]; //NOT sent over the net - nor should it be.
		public float speedJKA;
		public int slopeRecalcTime; //this is NOT sent across the net and is maintained seperately on game and cgame in pmove code.
		public QuakeBoolean legsFlip; //set to opposite when the same anim needs restarting, sent over in only 1 bit. Cleaner and makes porting easier than having that god forsaken ANIM_TOGGLEBIT.
		public QuakeBoolean torsoFlip;
		public int eFlags2;        // copied to entityState_t->eFlags2, EF2_??? used much less frequently
		public int saberLockHitCheckTime; //so we don't allow more than 1 push per server frame
		public int saberLockHitIncrementTime; //so we don't add a hit per attack button press more than once per server frame
		public int saberHolsteredJKA;
		public int heldByClient; //can only be a client index - this client should be holding onto my arm using IK stuff.
		public int ragAttach; //attach to ent while ragging
		public int iModelScale;
		public int brokenLimbs;
		//for looking at an entity's origin (NPCs and players)
		public QuakeBoolean hasLookTarget;
		public int lookTarget;
		public unsafe fixed int customRGBA[4];
		public int standheight;
		public int crouchheight;
		//If non-0, this is the index of the vehicle a player/NPC is riding.
		//public int m_iVehicleNum;
		//lovely hack for keeping vehicle orientation in sync with prediction
		public unsafe fixed float vehOrientation[3];
		public QuakeBoolean vehBoarding;
		public int vehSurfaces;
		//vehicle turnaround stuff (need this in ps so it doesn't jerk too much in prediction)
		public int vehTurnaroundIndex;
		public int vehTurnaroundTime;
		//vehicle has weapons linked
		public QuakeBoolean vehWeaponsLinked;
		//when hyperspacing, you just go forward really fast for HYPERSPACE_TIME
		public int hyperSpaceTime;
		public unsafe fixed float hyperSpaceAngles[3];
		//hacking when > time
		public int hackingTime;
		//actual hack amount - only for the proper percentage display when
		//drawing progress bar (is there a less bandwidth-eating way to do
		//this without a lot of hassle?)
		public int hackingBaseTime;
		//keeps track of jetpack fuel
		public int jetpackFuel;
		//keeps track of cloak fuel
		public int cloakFuel;
		//rww - spare values specifically for use by mod authors.
		//See psf_overrides.txt if you want to increase the send
		//amount of any of these above 1 bit.
		public int userInt1;
		public int userInt2;
		public int userInt3;
		public float userFloat1;
		public float userFloat2;
		public float userFloat3;
		public unsafe fixed float userVec1[3];
		public unsafe fixed float userVec2[3];

	}
}
