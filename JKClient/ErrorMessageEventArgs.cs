using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{
	public sealed class ErrorMessageEventArgs
	{
		public string errorMessage;
		public string errorMessageDetail;
		public MessageCopy possibleRelatedMessage;
		internal ErrorMessageEventArgs(string errorMessageA, string errorMessageDetailA, MessageCopy possibleRelatedMessageA)
		{
			errorMessage = errorMessageA;
			errorMessageDetail = errorMessageDetailA;
			possibleRelatedMessage = possibleRelatedMessageA;
		}
	}
}
