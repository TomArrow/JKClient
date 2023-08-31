using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct ClientSnapshot {
		public const int NotActive = 2;
		public QuakeBoolean Valid;
		public int Flags;
		public int ServerTime;
		public int ServerTimeResidual;
		public int MessageNum;
		public int DeltaNum;
		public PlayerState PlayerState;
		public PlayerState VehiclePlayerState;
		public int NumEntities;
		public int ParseEntitiesNum;
		public int ServerCommandNum;
		public int ping;

		public int numberOfSounds; // MOH
		//public ServerSound[] sounds; // MOH
	}
}
