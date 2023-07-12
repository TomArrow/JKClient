using System;

namespace JKClient {
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
