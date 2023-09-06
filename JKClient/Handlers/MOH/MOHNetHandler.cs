using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public abstract class MOHNetHandler : NetHandler
	{
		public override int MaxMessageLength => 49152;
		public override int MaxGameStateChars => 41952;
		public override ushort DefaultPort => 12203;
		public MOHNetHandler(ProtocolVersion protocol) : base((int)protocol) { }
	}
}
