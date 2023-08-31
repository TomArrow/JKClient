using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct GameState {
		private const int MaxConfigstrings = 2736;//2200; // MOH Limit now.
		public const int MaxGameStateChars = 41952;//16000; // MOH Limit now. But we check more precisely by client handler where needed
		public const int ServerInfo = 0;
		public const int SystemInfo = 1;
		public unsafe fixed int StringOffsets[GameState.MaxConfigstrings];
		public unsafe fixed sbyte StringData[GameState.MaxGameStateChars];
		public int DataCount;
	}
}
