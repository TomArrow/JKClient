using System.Runtime.InteropServices;

namespace JKClient {


	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct FrameInfo
	{
		public int Index;
		public float Time;
		public float Weight;

		public static int ASSUMEDSIZE = sizeof(int)+sizeof(float)+sizeof(float); // Cringe but can't use sizeof for structs... sigh.
	}

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

		//public QuakeBoolean FilledFromPlayerState; // That way we can know the data in this is valid aside from CurrentValid



		//JKA:
		public int eFlags2;        // EF2_??? used much less frequently
		public QuakeBoolean loopIsSoundset; //qtrue if the loopSound index is actually a soundset index
		public int soundSetIndex;
		public int saberHolstered;//sent in only only 2 bits - should be 0, 1 or 2
		public QuakeBoolean isPortalEnt; //this needs to be seperate for all entities I guess, which is why I couldn't reuse another value.
		public QuakeBoolean legsFlip; //set to opposite when the same anim needs restarting, sent over in only 1 bit. Cleaner and makes porting easier than having that god forsaken ANIM_TOGGLEBIT.
		public QuakeBoolean torsoFlip;
		public int heldByClient; //can only be a client index - this client should be holding onto my arm using IK stuff.
		public int ragAttach; //attach to ent while ragging
		public int iModelScale; //rww - transfer a percentage of the normal scale in a single int instead of 3 x-y-z scale values
		public int brokenLimbs;
		public int boltToPlayer; //set to index of a real client+1 to bolt the ent to that client. Must be a real client, NOT an NPC.
								 //for looking at an entity's origin (NPCs and players)
		public QuakeBoolean hasLookTarget;
		public int lookTarget;
		public unsafe fixed int customRGBA[4];
		//I didn't want to do this, but I.. have no choice. However, we aren't setting this for all ents or anything,
		//only ones we want health knowledge about on cgame (like siege objective breakables) -rww
		public int health;
		public int maxhealth; //so I know how to draw the stupid health bar
							  //NPC-SPECIFIC FIELDS
							  //------------------------------------------------------------
		public int npcSaber1;
		public int npcSaber2;
		//index values for each type of sound, gets the folder the sounds
		//are in. I wish there were a better way to do this,
		public int csSounds_Std;
		public int csSounds_Combat;
		public int csSounds_Extra;
		public int csSounds_Jedi;
		public int surfacesOn; //a bitflag of corresponding surfaces from a lookup table. These surfaces will be forced on.
		public int surfacesOff; //same as above, but forced off instead.
								//Allow up to 4 PCJ lookup values to be stored here.
								//The resolve to configstrings which contain the name of the
								//desired bone.
		public int boneIndex1;
		public int boneIndex2;
		public int boneIndex3;
		public int boneIndex4;
		//packed with x, y, z orientations for bone angles
		public int boneOrient;
		//I.. feel bad for doing this, but NPCs really just need to
		//be able to control this sort of thing from the server sometimes.
		//At least it's at the end so this stuff is never going to get sent
		//over for anything that isn't an NPC.
		public unsafe fixed float boneAngles1[3]; //angles of boneIndex1
		public unsafe fixed float boneAngles2[3]; //angles of boneIndex2
		public unsafe fixed float boneAngles3[3]; //angles of boneIndex3
		public unsafe fixed float boneAngles4[3]; //angles of boneIndex4
		public int NPC_class; //we need to see what it is on the client for a few effects.
							  //If non-0, this is the index of the vehicle a player/NPC is riding.
		public int m_iVehicleNum;
		//rww - spare values specifically for use by mod authors.
		//See netf_overrides.txt if you want to increase the send
		//amount of any of these above 1 bit.
		public int userInt1;
		public int userInt2;
		public int userInt3;
		public float userFloat1;
		public float userFloat2;
		public float userFloat3;
		public unsafe fixed float userVec1[3];
		public unsafe fixed float userVec2[3];

		// MOHAA
		public unsafe fixed byte frameInfo[(sizeof(int)+sizeof(float)*2)*16]; // This is cringe but the fixed size buffers can only use primitive types. We don't really need the data anyway so this will simply be used for proper parsing and baselines and that's it.
		public unsafe fixed float BoneAngles[5*3];
		public unsafe fixed int BoneTag[5];
		public float ActionWeight;
		public int Parent;
		public int RenderFx;
		public float Scale;
		public float Alpha;
		public int UsageIndex;
		public int TagNum;
		public QuakeBoolean AttachUseAngles;
		public unsafe fixed byte Surfaces[32];
		public float LoopSoundVolume;
		public float LoopSoundMinDist;
		public float LoopSoundMaxDist;
		public float LoopSoundPitch;
		public int LoopSoundFlags;
		public unsafe fixed float AttachOffset[3];
		public int BeamEntnum;
		public int SkinNum;
		public int WasFrame;
		public unsafe fixed float ShaderData[2];
		public float ShaderTime;
		public unsafe fixed float EyeVector[3];

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
