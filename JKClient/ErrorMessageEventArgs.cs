using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public sealed class ErrorMessageEventArgs
	{
		public string errorMessage;
		public string errorMessageDetail;
		internal ErrorMessageEventArgs(string errorMessageA, string errorMessageDetailA)
		{
			errorMessage = errorMessageA;
			errorMessageDetail = errorMessageDetailA;
		}
	}
}
