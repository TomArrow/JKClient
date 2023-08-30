using System;
using System.Runtime.InteropServices;

namespace JKClient {

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	struct ServerSound
	{ // MOHAA
		public unsafe fixed float origin[3];
		public int entity_number;
		public int channel;
		public short sound_index;
		public float volume;
		public float min_dist;
		public float maxDist;
		public float pitch;
		public QuakeBoolean stop_flag;
		public QuakeBoolean streamed;
	}

	public class Snapshot {
		public const int MaxEntities = 256;
		public int Flags;
		public int ServerTime;
		public PlayerState PlayerState;
		public PlayerState VehiclePlayerState;
		public int NumEntities;
		public int ping;
		public EntityState []Entities { get; init; } = new EntityState[Snapshot.MaxEntities];
		public int ServerCommandSequence;
	}
}
