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
		public int lastKnownMessageNum;
		internal SnapshotParsedEventArgs(Snapshot snapA, int snapNumA, int lastKnownServerTimeA, int lastKnownMessageNumA)
		{
			snap = snapA;
			snapNum = snapNumA;
			lastKnownServerTime = lastKnownServerTimeA;
			lastKnownMessageNum = lastKnownMessageNumA;
		}
	}
}
