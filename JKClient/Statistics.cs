using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace JKClient
{
    public class Statistics : INotifyPropertyChanged
    {
        public Int64 demoSize { get; internal set; }
        [DependsOn("demoSize")]
        public string demoSizeKiB { get {
                return $"{((double)demoSize / 1024.0).ToString("#,#.000",CultureInfo.InvariantCulture)} KiB";
            } 
        }
        public Int64 demoSizeFullFlushed { get; internal set; }
        [DependsOn("demoSizeFullFlushed")]
        public string demoSizeFullFlushedKiB
        {
            get
            {
                return $"{((double)demoSizeFullFlushed / 1024.0).ToString("#,#.000", CultureInfo.InvariantCulture)} KiB";
            }
        }
        public int deltaSnapMaxDelay { get; internal set; } // Amount of milliseconds we have to delay to guarantee we keep getting delta snaps. This is live adjusted all the time automatically.
        public Int64 deltaSnaps { get; internal set; }
        public Int64 nonDeltaSnaps { get; internal set; }
        public Int64 totalMessages { get; internal set; }
        public Int64 messagesSkipped { get; internal set; }
        public Int64 messagesOutOfOrder { get; internal set; }
        public Int64 messagesSkippable { get; internal set; }
        public Int64 messagesSuperSkippable { get; internal set; }
        public Int64 messagesUnskippableSvc { get; internal set; }
        public Int64 messagesUnskippableNewCommands { get; internal set; }
        public Int64 messagesUnskippableNonDelta { get; internal set; }
        public Int64 messagesNotSkippedTime { get; internal set; }
        public Int64 demoCurrentTimeSyncFix { get; internal set; }
        public Int64 demoCurrentTime { get; internal set; }
        public Int64 demoCurrentTimeWritten { get; internal set; }
        public Int64 messagesDropped { get; internal set; }

        public Int64 lastFrameDelta { get; internal set; }
        [DependsOn("lastFrameDelta")]
        public int lastFrameDeltaFPS { get {
                return lastFrameDelta == 0 ? 0 : (int)(1000 / lastFrameDelta);
            } }

        public Int64 lastUserCommandDelta { get; internal set; }
        [DependsOn("lastUserCommandDelta")]
        public int lastUserCommandDeltaFPS
        {
            get
            {
                return lastUserCommandDelta ==  0 ? 0 :(int)(1000 / lastUserCommandDelta);
            }
        }
        public Int64 lastUserPacketDelta { get; internal set; }
        [DependsOn("lastUserPacketDelta")]
        public int lastUserPacketDeltaFPS
        {
            get
            {
                return lastUserPacketDelta == 0 ? 0 : (int)(1000 / lastUserPacketDelta);
            }
        }
        public int deltaDelta { get; internal set; }

        public string lastCommand { get; internal set; }
        public bool keyActiveW { get; internal set; }
        public bool keyActiveA { get; internal set; }
        public bool keyActiveS { get; internal set; }
        public bool keyActiveD { get; internal set; }
        public bool keyActiveJump { get; internal set; }
        public bool keyActiveCrouch { get; internal set; }
        public bool keyActive0 { get; internal set; }
        public bool keyActive1 { get; internal set; }
        public bool keyActive2 { get; internal set; }
        public bool keyActive3 { get; internal set; }
        public bool keyActive4 { get; internal set; }
        public bool keyActive5 { get; internal set; }
        public bool keyActive6 { get; internal set; }
        public bool keyActive7 { get; internal set; }
        public bool keyActive8 { get; internal set; }
        public bool keyActive9 { get; internal set; }
        public bool keyActive10 { get; internal set; }
        public bool keyActive11 { get; internal set; }
        public bool keyActive12 { get; internal set; }
        public bool keyActive13 { get; internal set; }
        public bool keyActive14 { get; internal set; }
        public bool keyActive15 { get; internal set; }
        public bool keyActive16 { get; internal set; }
        public bool keyActive17 { get; internal set; }
        public bool keyActive18 { get; internal set; }
        public bool keyActive19 { get; internal set; }
        public bool keyActive20 { get; internal set; }


        public event PropertyChangedEventHandler PropertyChanged;
    }
}
