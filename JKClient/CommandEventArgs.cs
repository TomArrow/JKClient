using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient
{

    public enum CommandSpamType {
		None,
		SpamSame, // might spam the same text over and over
		SpamKind, // might spam very similar text over and over
	}

    
	public sealed class CommandEventArgs {
		public Command Command { get; init; }
		public Command UTF8Command { get; init; }
		public int MessageNum { get; init; }
		public CommandSpamType SpamType { get; init; }
		public string SpamKind { get; init; } = null; // if SpamType is SpamKind, have some kind of identifier here that lets us recognize that not entirely identical messages with this same identifier belong to the same spam group and should be debounced together
		private CommandEventArgs() {}
		internal CommandEventArgs(Command command, int messageNum, Command utf8Command = null, CommandSpamType spamType = CommandSpamType.None, string spamKind = null) {
			this.Command = command;
			this.UTF8Command = utf8Command;
			this.MessageNum = messageNum;
			this.SpamType = spamType;
			this.SpamKind = spamKind;
		}
	}
}
