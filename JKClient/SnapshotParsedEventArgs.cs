using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public sealed class SnapshotParsedEventArgs
    {
		public Snapshot snap;
		public int snapNum;
		internal SnapshotParsedEventArgs(Snapshot snapA, int snapNumA)
		{
			snap = snapA;
			snapNum = snapNumA;
		}
	}
}
