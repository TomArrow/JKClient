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
		internal DownloadFinishedEventArgs(string localNameA, string remoteNameA, int checksumA, byte[] dataA)
		{
			localName = localNameA;
			remoteName = remoteNameA;
			data = dataA;
			checksum = checksumA;
		}
	}

	public sealed partial class JKClient
    {

		public event EventHandler<DownloadFinishedEventArgs> DownloadFinished;
		protected void OnDownloadFinished(string localName, string remoteName, int checksum, byte[] data)
		{
			DownloadFinished?.Invoke(this, new DownloadFinishedEventArgs(localName, remoteName, checksum, data));
		}

		class queuedDownload {
			public string remoteName;
			public string localName;
			public int checksum;
		}


        string downloadTempName = null;
		string downloadName = null;
		int downloadBlock=0;  // block we are waiting for
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
				downloadBlockConfirmed = 0;
				downloadBlockLastAcked = 0;
				downloadBlockLastSuccessful = 0;
				downloadCount = 0;
				//ExecuteCommandInternal($"download {currentDownload.remoteName}");
				AddReliableCommand($"download {currentDownload.remoteName}");
			}
			if((this.downloadName != null) != desiredSnapsDownloadOverride)
            {
				this.DesiredSnaps = this.desiredSnaps; // reset?
            }
        }

		public void EnqueueDownload(string remoteName, string localName, int checksum)
        {
			queuedDownload newDl = new queuedDownload() { checksum = checksum, localName = localName, remoteName = remoteName };
			if(queuedDownloads.Count > 0)
            {
				//somehoow mayabe check for dupes?
            }
			queuedDownloads.Enqueue(newDl);
        }

		public void ResetDownloads()
        {
			if(downloadName != null)
            {
				AddReliableCommand("stopdl");
			}
			KillCurrentDownload();
			queuedDownloads.Clear();
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
			ushort block = (ushort)msg.ReadShort();
			if (block == 0)// && downloadBlock == 0)
			{
				downloadSize = msg.ReadLong();
				if (downloadSize < 0)
				{
					fixed (sbyte* s = msg.ReadString((ProtocolVersion)this.Protocol))
					{
						byte* ss = (byte*)s;
						var cmd = new Command(new string[] { "print", Common.ToString(ss, sizeof(sbyte) * Common.MaxStringCharsMOH) });
						this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd, -1));
						KillCurrentDownload();
						AddReliableCommand("stopdl");
						return true;
						//throw new JKClientException($"{Common.ToString(ss, sizeof(sbyte)*Common.MaxStringCharsMOH)}");
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
					else if (this.realTime - this.downloadBlockLastAcked > 1000)
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

            if (size == 0)// A zero length block means EOF
			{
				if(download != null)
                {
					byte[] fileContents = download.ToArray();
					download.Dispose();
					download = null;


					// i should verify the .zip checksum here but.... im too lazy to port all that logic?
					OnDownloadFinished(currentDownload.localName,currentDownload.remoteName,currentDownload.checksum, fileContents);
                }
				downloadTempName = downloadName = null;

				CheckDownloads();
            }

			return true;
		}
	}
}
