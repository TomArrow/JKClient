using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct UserCommand {
		public const int CommandBackup = 64;
		public const int CommandMask = (UserCommand.CommandBackup - 1);
		public int ServerTime;

		// Added so user of JKClient can manipulate buttons etc.
		public unsafe fixed int Angles[3];
		public int Buttons;
		public byte Weapon;           // weapon
		public byte ForceSelection;
		public byte InventorySelection;
		public byte GenericCmd;

		public sbyte ForwardMove, RightMove, Upmove;

		public byte Milliseconds; // MOHAA

		public enum Button // I commented out some that are JK2 specific and appended "JK2" to some that I kept. The remaining should be universal
        {
			Attack				=1,
			Talk				=2,			// displays talk balloon and disables actions
			UseHoldable			=4,
			UseMOHAA			=8,
			Gesture				=8,
			Walking				=16,			// walking can't just be infered from MOVE_RUN
												// because a key pressed late in the frame will
												// only generate a small move value for that frame
												// walking will use different animations and
												// won't generate footsteps
			//Use					=32,			// the ol' use key returns!
			ForceGripJK2			=64,			// 
			AltAttackJK2			=128,

			AnyJK2					=256,           // any key whatsoever

			//ForcePower			=512,			// use the "active" force power

			//ForceLightning		=1024,

			ForceDrainJK2 = 2048,
			MouseMOH = 1<<15
		}

		public QuakeBoolean forceWriteThisCmd; // If not true, this one can be skipped to reduce traffic.

		internal unsafe bool IdenticalTo(UserCommand otherCmd)
        {
			return this.ForwardMove == otherCmd.ForwardMove
				&& this.RightMove == otherCmd.RightMove
				&& this.Upmove == otherCmd.Upmove
				&& this.GenericCmd == otherCmd.GenericCmd
				&& this.InventorySelection == otherCmd.InventorySelection
				&& this.ForceSelection == otherCmd.ForceSelection
				&& this.Weapon == otherCmd.Weapon
				&& this.Buttons == otherCmd.Buttons
				&& this.Angles[0] == otherCmd.Angles[0]
				&& this.Angles[1] == otherCmd.Angles[1]
				&& this.Angles[2] == otherCmd.Angles[2]
				&& this.Buttons == otherCmd.Buttons
				;

		} 
	}
}
