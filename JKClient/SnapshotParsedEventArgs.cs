using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public sealed class SnapshotParsedEventArgs
    {
		public Snapshot snap;
		internal SnapshotParsedEventArgs(Snapshot snapA)
		{
			snap = snapA;
		}
	}
}
