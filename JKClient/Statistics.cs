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
        public Int64 demoCurrentTime { get; internal set; }
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



        public event PropertyChangedEventHandler PropertyChanged;
    }
}
