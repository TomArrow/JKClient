﻿using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	public sealed class CommandEventArgs {
		public Command Command { get; init; }
		public Command UTF8Command { get; init; }
		public int MessageNum { get; init; }
		private CommandEventArgs() {}
		internal CommandEventArgs(Command command, int messageNum, Command utf8Command = null) {
			this.Command = command;
			this.UTF8Command = utf8Command;
			this.MessageNum = messageNum;
		}
	}
}
