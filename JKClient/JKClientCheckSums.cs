using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace JKClient
{
    public class CheckSumFile {
        public string name { get; init; } = null;
        public bool hasCgame { get; init; } = false;
        public bool hasUI { get; init; } = false;
        public byte[] headerLongData { get; init; } = null;
        internal int pureChecksum = 0;
    }

    public sealed partial class JKClient
    {

        int lastCheckSumFeedCalculated = 0;

        CheckSumFile[] checkSumFiles = null;

        string pureCheckSumCommand = null;

        bool serverIsPure = false;

        public void SetAssetChecksumFiles(CheckSumFile[] files)
        {
            checkSumFiles = files;

            if(this.checksumFeed != 0)
            {
                CalculatePureChecksums();
            }
        }

        void CalculatePureChecksums()
        {
            if(this.checkSumFiles == null || this.checkSumFiles.Length == 0)
            {
                pureCheckSumCommand = null;
                return;
            }

            int checksum = this.checksumFeed;
            int numPaks = 0;
            int key = this.checksumFeed;
            byte[] keyBytes = BitConverter.GetBytes(key);
            CheckSumFile cgameFile = null;
            CheckSumFile uiFile = null;
            foreach (CheckSumFile file in checkSumFiles)
            {
                byte[] allBytes = new byte[keyBytes.Length + file.headerLongData.Length];
                Array.Copy(keyBytes, allBytes, keyBytes.Length);
                Array.Copy(file.headerLongData,0, allBytes,keyBytes.Length, file.headerLongData.Length);
                acryptohashnet.MD4 hashAlgo = new acryptohashnet.MD4();
                byte[] md4Hash = hashAlgo.ComputeHash(allBytes);
                int[] digest = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    digest[i] = BitConverter.ToInt32(md4Hash, i * 4);
                }
                int val = digest[0] ^ digest[1] ^ digest[2] ^ digest[3];
                file.pureChecksum = val;
                if (file.hasCgame)
                {
                    cgameFile = file;
                }
                if (file.hasUI)
                {
                    uiFile = file;
                }
                checksum ^= file.pureChecksum;
                numPaks++;
            }
            lastCheckSumFeedCalculated = this.checksumFeed;


            StringBuilder checkSumCommand = new StringBuilder();
            checkSumCommand.Append("cp ");
            if(cgameFile != null)
            {
                checkSumCommand.Append(cgameFile.pureChecksum);
                checkSumCommand.Append(" ");
            }
            if(uiFile != null)
            {
                checkSumCommand.Append(uiFile.pureChecksum);
                checkSumCommand.Append(" ");
            }
            checkSumCommand.Append("@ ");
            foreach (CheckSumFile file in checkSumFiles)
            {
                checkSumCommand.Append(file.pureChecksum);
                checkSumCommand.Append(" ");
            }
            checksum ^= numPaks;
            checkSumCommand.Append(checksum);
            checkSumCommand.Append(" ");
            pureCheckSumCommand = checkSumCommand.ToString();
            Debug.WriteLine($"checksumFeed {this.checksumFeed}, cmd: {pureCheckSumCommand}");
        }

        void SendPureChecksums()
        {
            if (!serverIsPure)
            {
                return;
            }
            if(this.checksumFeed != lastCheckSumFeedCalculated)
            {
                CalculatePureChecksums();
            }
            if(pureCheckSumCommand != null)
            {
                this.ExecuteCommandInternal(pureCheckSumCommand);
            }
        }
    }
}
