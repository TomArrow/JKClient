
﻿using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Explicit, Size = 4)]
	public struct QuakeBoolean {
		[FieldOffset(0)]
		private readonly int value;
		private QuakeBoolean(bool value) {
			this.value = value ? 1 : 0;
		}
		private QuakeBoolean(int value) {
			this.value = value;
		}
		public static implicit operator bool(QuakeBoolean b) => b.value != 0;
		public static implicit operator int(QuakeBoolean b) => b.value;
		public static implicit operator QuakeBoolean(bool b) => new QuakeBoolean(b);
		public static implicit operator QuakeBoolean(int i) => new QuakeBoolean(i);
		public override string ToString() => ((bool)this).ToString();
	}
}
