using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public sealed class InternalCommandCreatedEventArgs
	{
		public bool handledExternally = false;
		public string command = null;
		public Encoding encoding = null;
		internal InternalCommandCreatedEventArgs(string commandA, Encoding encodingA = null)
		{
			command = commandA;
			encoding = encodingA;
		}
	}
}
