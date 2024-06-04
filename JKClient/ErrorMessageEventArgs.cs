using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public sealed class ErrorMessageEventArgs
	{
		public string errorMessage;
		internal ErrorMessageEventArgs(string errorMessageA)
		{
			errorMessage = errorMessageA;
		}
	}
}
