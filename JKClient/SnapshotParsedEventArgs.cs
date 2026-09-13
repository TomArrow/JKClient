using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public sealed class SnapshotParsedEventArgs
    {
		public Snapshot snap;
		public int snapNum;
		public int lastKnownServerTime;
		internal SnapshotParsedEventArgs(Snapshot snapA, int snapNumA, int lastKnownServerTimeA)
		{
			snap = snapA;
			snapNum = snapNumA;
			lastKnownServerTime = lastKnownServerTimeA;
		}
	}
}
