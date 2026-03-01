using System;
using System.Collections.Generic;
using System.IO;
using System.Text;



namespace JKClient
{
	public sealed class DownloadFinishedEventArgs
	{
		public string localName;
		public string remoteName;
		public int checksum;
		public byte[] data;
		public bool anyLeftInQueue;
		internal DownloadFinishedEventArgs(string localNameA, string remoteNameA, int checksumA, byte[] dataA, bool anyLeftInQueueA)
		{
			localName = localNameA;
			remoteName = remoteNameA;
			data = dataA;
			checksum = checksumA;
			anyLeftInQueue = anyLeftInQueueA;
		}
	}

	public sealed partial class JKClient
    {

		public event EventHandler<DownloadFinishedEventArgs> DownloadFinished;
		protected void OnDownloadFinished(string localName, string remoteName, int checksum, byte[] data, bool anyLeftInQueue)
		{
			DownloadFinished?.Invoke(this, new DownloadFinishedEventArgs(localName, remoteName, checksum, data, anyLeftInQueue));
		}

		class queuedDownload {
			public string remoteName;
			public string localName;
			public int checksum;
			public byte[] partialData;
		}


        string downloadTempName = null;
		string downloadName = null;
		int downloadBlock=0;  // block we are waiting for
		//int downloadBlockBase=0;  // when block index starts wrapping with small blocksizes like 1024
		int downloadBlockConfirmed=0;  // block we have
		int downloadBlockLastAcked = 0;
		int downloadBlockLastSuccessful = 0;
		int downloadCount=0;  // how many bytes we got
		int downloadSize=0;   // how many bytes we got
		Queue<queuedDownload> queuedDownloads = new Queue<queuedDownload>();
		queuedDownload currentDownload = null;
		MemoryStream download = null;

		//int downloadNumber=0;
		//int downloadIndex=0;  // current index in downloadChksums
		//string downloadList; // list of paks we need to download
		//int downloadChksums[64]; // contains checksums of the currently requested paks

		private void CheckDownloads()
        {
            if (queuedDownloads.Count > 0 && downloadName == null)
            {
				currentDownload = queuedDownloads.Dequeue();
				downloadName = currentDownload.localName;
				downloadTempName = $"{currentDownload.localName}.tmp";
				downloadBlock = 0;
				//downloadBlockBase = 0;
				downloadBlockConfirmed = 0;
				downloadBlockLastAcked = 0;
				downloadBlockLastSuccessful = 0;
				downloadCount = 0;
				//ExecuteCommandInternal($"download {currentDownload.remoteName}");
				AddReliableCommand($"download \"{currentDownload.remoteName}\""); // todo some kind of failsafe for when this cmd gets lost in transmission (since reliable commands... arent really reliable anymore *rage*)
			}
			if((this.downloadName != null) != desiredSnapsDownloadOverride)
            {
				this.DesiredSnaps = this.desiredSnaps; // reset?
            }
        }

		public void EnqueueDownload(string remoteName, string localName, int checksum, byte[] existingPartialData = null)
        {

			void enqueueDownload()
			{
				queuedDownload newDl = new queuedDownload() { checksum = checksum, localName = localName, remoteName = remoteName, partialData = existingPartialData };
				//if (queuedDownloads.Count > 0)
				//{
					//somehoow mayabe check for dupes?
				//}
				queuedDownloads.Enqueue(newDl);
			}
			this.actionsQueue.Enqueue(enqueueDownload);

        }

		private void ResetDownloads()
        {
			if(downloadName != null)
            {
				AddReliableCommand("stopdl");
			}
			KillCurrentDownload();
			queuedDownloads.Clear();
		}

		public void EndDownloads()
        {
			// public api
			void resetDownloadsAction()
			{
				ResetDownloads();
			}
			this.actionsQueue.Enqueue(resetDownloadsAction);
		}

		private void KillCurrentDownload()
		{
			if (download != null)
			{
				download.Dispose();
				download = null;
			}
			downloadTempName = null;
			downloadName = null;
			// downloadIndex = 0;
			//downloadNumber = 0;
			downloadBlock = 0;  // block we are waiting for
			//downloadBlockBase = 0;  // block we are waiting for
			downloadBlockConfirmed = 0;  // block we are waiting for
			downloadBlockLastAcked = 0; 
			downloadBlockLastSuccessful = 0; 
			downloadCount = 0;  // how many bytes we got
			downloadSize = 0;   // how many bytes we got
			currentDownload = null;
		}

		// TODO what if we are stuck waiting for a download that was never approved?
		private unsafe bool ParseDownload(in Message msg)
		{
            if (downloadTempName == null)
			{
				var cmd = new Command(new string[] { "print", $"^3WARNING: Server sending download, but no download was requested\n" });
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
				AddReliableCommand("stopdl");
				return false;
			}
			int blockClosestUShortMultiple = ((downloadBlock + 32768) / 65536) * 65536; // not perfect math? idk close enough
			int block = (ushort)msg.ReadShort();
			if(Math.Abs(block - blockClosestUShortMultiple) > 32768) // we use our own downloadblock tracking to fix the possibly wrapped sent number. kinda cringe but what can ya do. otherwise e.g. with a MAX_DOWNLOAD_BLKSIZE of 1024 we will crap out at around or above 67107840 bytes (~64MB)
			{
				if(block < 32768)
                {
					block += blockClosestUShortMultiple;
                }
                else
                {
					block += blockClosestUShortMultiple- 65536;
				}
            }
			if (block == 0)// && downloadBlock == 0)
			{

				if (downloadBlock > 1024 && Math.Abs(blockClosestUShortMultiple - downloadBlock) < 1024 ) // HACK. block index wrapped. 1024 chosen randomly. just to see if we're close
				{
					var cmd = new Command(new string[] { "print", $"Download block index wrap detected. Attempting fix." });
					//downloadBlockBase = blockClosestUShortMultiple;
                }
                else { 
					downloadSize = msg.ReadLong();
					if (downloadSize < 0)
					{
						fixed (sbyte* s = msg.ReadString((ProtocolVersion)this.Protocol))
						{
							byte* ss = (byte*)s;
							var cmd = new Command(new string[] { "print", $"Download failure for some reason. Download size {downloadSize}. print attempt following." });
							this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
							cmd = new Command(new string[] { "print", Common.ToString(ss, sizeof(sbyte) * Common.MaxStringCharsMOH) });
							this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
							KillCurrentDownload();
							AddReliableCommand("stopdl");
							return true;
							//throw new JKClientException($"{Common.ToString(ss, sizeof(sbyte)*Common.MaxStringCharsMOH)}");
						}
					}
				}
			} /*else if (downloadBlock > 1 && block == 0)
			{ // TA: My own addition. If server cancels our dl.
				var cmd = new Command(new string[] { "print", $"^3WARNING: Server starting download, but we are in the middle of another download\n" });
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
				KillCurrentDownload();
				AddReliableCommand("stopdl");
				return false;
			}*/

			//block += downloadBlockBase;

			ushort size = (ushort)msg.ReadShort();
			if (size < 0 || size > sizeof(byte) * this.ClientHandler.MaxMessageLength)
			{
				var cmd = new Command(new string[] { "print", $"^1ParseDownload: Invalid size {size} for download chunk" });
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
				KillCurrentDownload();
				AddReliableCommand("stopdl");
				return false;
				//throw new JKClientException($"ParseDownload: Invalid size {size} for download chunk");
			}
			byte[] newData = new byte[size];
			msg.ReadData(newData, size);

            if (downloadBlock != block)
			{
				// this will actually happen a lot probably (since blocks are sent repeatedly), so lets not spam it maybe
				/*if (downloadBlock != block + 1)
				{
					// it can happen we're getting the previous one cuz that one isn't acked yet.
					// but otherwise better cancel.
					var cmd = new Command(new string[] { "print", $"CL_ParseDownload: Expected block {downloadBlock}, got {block}. CANCELING.\n" });
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
					KillCurrentDownload();
					AddReliableCommand("stopdl");
				}
                else*/
				{
					bool dontPrint = downloadBlockLastSuccessful > 0 && this.realTime > downloadBlockLastSuccessful && (this.realTime - downloadBlockLastSuccessful) > 10000;
					bool giveUp = downloadBlockLastSuccessful > 0 && this.realTime > downloadBlockLastSuccessful && (this.realTime - downloadBlockLastSuccessful) > 60000;
                    if (giveUp)
					{
						var cmd = new Command(new string[] { "print", $"^1ParseDownload: Havent gotten the requested block {downloadBlock} for over 1 minute. Giving up on this download.\n" });
						this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
						KillCurrentDownload();
						AddReliableCommand("stopdl");
						return true;
					} 
					else if (this.downloadBlockLastAcked > 0 && downloadBlock > 0 && downloadBlock > block && this.realTime - this.downloadBlockLastAcked > 5000) // cant just keep reacking cuz server needs all nextdl to come in perfect order
					{
                        if (!dontPrint)
						{
							var cmd = new Command(new string[] { "print", $"CL_ParseDownload: Expected block {downloadBlock}, got {block}. FORCING REACK.\n" });
							this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
						}
						AddReliableCommand($"nextdl {downloadBlockConfirmed}");
						this.downloadBlockLastAcked = this.realTime;
					} else
					{
						if (!dontPrint)
						{
							var cmd = new Command(new string[] { "print", $"CL_ParseDownload: Expected block {downloadBlock}, got {block}\n" });
							this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
						}
					}
				}
				return true;
			}
			if(download == null)
            {
				download = new MemoryStream();
				// error check here in vanilla, but theres no reason this should fail.
            }
            if (size > 0)
            {
				download.Write(newData,0,size);
            }
			downloadBlockConfirmed = downloadBlock;
			AddReliableCommand($"nextdl {downloadBlockConfirmed}");
			downloadBlockLastAcked = this.realTime;
			downloadBlockLastSuccessful = this.realTime;
			downloadBlock++;
			downloadCount += size;
            if (downloadSize > 0 && 10 * downloadCount/downloadSize > 10 * (downloadCount-size) / downloadSize)
			{
				var cmd = new Command(new string[] { "print", $"^3CL_ParseDownload: File {downloadName} at {100UL * (UInt64)downloadCount / (UInt64)downloadSize}%\n" });
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
			}

            if (size == 0)// A zero length block means EOF
			{
				if(download != null)
                {
					byte[] fileContents = download.ToArray();
					download.Dispose();
					download = null;


					// i should verify the .zip checksum here but.... im too lazy to port all that logic?
					OnDownloadFinished(currentDownload.localName,currentDownload.remoteName,currentDownload.checksum, fileContents, queuedDownloads.Count > 0);
                }
				downloadTempName = downloadName = null;

				CheckDownloads();
            }

			return true;
		}
	}
}
