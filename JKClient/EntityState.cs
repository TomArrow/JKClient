using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct EntityState {
		//Dummy is used as any value parsed in ReadDeltaEntity, as being offset by 0
		public int Dummy;
		public int Number;
		public int EntityType;
		public int EntityFlags;

		public Trajectory Position;        // for calculating position
		public Trajectory AngularPosition; // for calculating angles

		public int Time;
		public int Time2;

		public unsafe fixed float Origin[3];
		public unsafe fixed float Origin2[3];

		public unsafe fixed float Angles[3];
		public unsafe fixed float Angles2[3];

		public int Bolt1;
		public int Bolt2;

		//rww - this is necessary for determining player visibility during a jedi mindtrick
		public int TrickedEntityIndex; //0-15
		public int TrickedEntityIndex2; //16-32
		public int TrickedEntityIndex3; //33-48
		public int TrickedEntityIndex4; //49-64

		public float Speed;

		public int FireFlag;

		public int GenericEnemyIndex;

		public int ActiveForcePass;

		public int EmplacedOwner;

		public int OtherEntityNum; // shotgun sources, etc
		public int OtherEntityNum2;

		public int GroundEntityNum;    // -1 = in air

		public int ConstantLight;  // r + (g<<8) + (b<<16) + (intensity<<24)
		public int LoopSound;      // constantly loop this sound

		public int ModelGhoul2;
		public int G2Radius;
		public int ModelIndex;
		public int ModelIndex2;
		public int ClientNum;      // 0 to (MAX_CLIENTS - 1), for players and corpses
		public int Frame;

		public QuakeBoolean SaberInFlight;
		public int SaberEntityNum;
		public int SaberMove;
		public int ForcePowersActive;

		public QuakeBoolean IsJediMaster;

		public int Solid;          // for client side prediction, trap_linkentity sets this properly

		public int Event;          // impulse events -- muzzle flashes, footsteps, etc
		public int EventParm;

		// so crosshair knows what it's looking at
		public int Owner;
		public int TeamOwner;
		public QuakeBoolean ShouldTarget;

		// for players
		public int Powerups;       // bit flags
		public int Weapon;         // determines weapon and flash model, etc
		public int LegsAnimation;       // mask off ANIM_TOGGLEBIT
		public int TorsoAnimation;      // mask off ANIM_TOGGLEBIT

		public int ForceFrame;     //if non-zero, force the anim frame

		public int Generic1;

		public QuakeBoolean FilledFromPlayerState; // That way we can know the data in this is valid aside from CurrentValid

		//IMPORTANT: update all entityStateFields in Message after adding new fields
		public static readonly EntityState Null = new EntityState();
	}



	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct Trajectory
	{
		public TrajectoryType Type;
		public int Time;
		public int Duration;         // if non 0, trTime + trDuration = stop time
		public unsafe fixed float Base[3];
		public unsafe fixed float Delta[3];
	}

	public enum TrajectoryType :int
	{
		TR_STATIONARY,
		TR_INTERPOLATE,             // non-parametric, but interpolate between snapshots
		TR_LINEAR,
		TR_LINEAR_STOP,
		TR_SINE,                    // value = base + sin( time / duration ) * delta
		TR_GRAVITY
	}
}
