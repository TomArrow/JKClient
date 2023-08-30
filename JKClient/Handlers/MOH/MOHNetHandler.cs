using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public abstract class MOHNetHandler : NetHandler
	{
		public override int MaxMessageLength => 49152;
		public MOHNetHandler(ProtocolVersion protocol) : base((int)protocol) { }
	}
}
