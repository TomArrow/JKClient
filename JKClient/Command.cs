﻿using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JKClient {
	public sealed class Command {
		private readonly string []argv = null;
		private readonly string rawString = null;
		public int Argc => this.argv != null ? this.argv.Length : 0;
		public string Argv(int arg) => arg >= this.Argc || arg < 0 ? string.Empty : this.argv[arg];
		private Command() {}
		internal Command(string text) {
			this.argv = Command.TokenizeString(text).ToArray();
			this.rawString = text;
		}
		internal Command(string []argv) {
			this.argv = argv;
		}
		public string RawString()
        {
			return this.rawString;
        }
		public string RawStringOrConcatenated()
        {
			return string.IsNullOrWhiteSpace(this.rawString) ? "\""+string.Join("\" \"",this.argv)+"\"" : this.rawString;
        }
		private static IEnumerable<string> TokenizeString(string line) {
			const char delimiter = ' ', textQualifier = '"';
			if (line == null) {
				yield break;
			} else {
				char prevChar = '\0';
				char nextChar = '\0';
				char currentChar = '\0';
				bool inString = false;
				bool wasInString = false;
				var token = new StringBuilder();
				for (int i = 0; i < line.Length; i++) {
					currentChar = line[i];
					if (i > 0)
						prevChar = line[i - 1];
					else
						prevChar = '\0';
					if (i + 1 < line.Length)
						nextChar = line[i + 1];
					else
						nextChar = '\0';
					if (currentChar == textQualifier && (prevChar == '\0' || prevChar == delimiter || prevChar == '\n') && !inString) {
						inString = true;
						continue;
					}
					if (currentChar == textQualifier && (nextChar == '\0' || nextChar == delimiter || nextChar == '\n') && inString) {
						inString = false;
						wasInString = true;
						continue;
					}
					if ((currentChar == delimiter || currentChar == '\n') && !inString) {
						if (token.Length <= 0 && !wasInString) {
							continue;
						}
						wasInString = false;
						yield return token.ToString();
						token = token.Remove(0, token.Length);
						continue;
					}
					token = token.Append(currentChar);
				}
				yield return token.ToString();
			}
		}
	}
}
