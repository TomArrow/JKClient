using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace JKClient {
	public sealed class InfoString : ConcurrentDictionary<string, string> {
		private const char Delimiter = '\\';
		private List<string> parameterOrderList = new List<string>();
		private HashSet<string> parameterOrderListExistChecker = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		public new string this[string key] {
			get => this.ContainsKey(key) ? base[key] : string.Empty;
			internal set
			{
                lock (parameterOrderList)
                {
					if(parameterOrderList.Count > 0)
                    {
                        if (!parameterOrderListExistChecker.Contains(key))
                        {
							parameterOrderListExistChecker.Add(key);
							parameterOrderList.Add(key);
						}
                    }
				}
				base[key] = value;
			}
		}
		private InfoString() {}
		public InfoString(string infoString, IEnumerable<string> parameterOrderListA = null) : base(new InfoStringComparer()) {
            
			if(parameterOrderListA != null)
			{
				lock (parameterOrderList)
				{
					foreach (string param in parameterOrderListA)
					{
						if (!parameterOrderListExistChecker.Contains(param))
						{
							parameterOrderListExistChecker.Add(param);
							parameterOrderList.Add(param);
						}
					}
				}
			}
			if (string.IsNullOrEmpty(infoString)) {
				return;
			}
			int index = infoString.IndexOf(InfoString.Delimiter);
			if (index < 0) {
				return;
			}
			string []keyValuePairs = infoString.Split(InfoString.Delimiter);
			int i = index != 0 ? 0 : 1;
			int length = (keyValuePairs.Length - i) & ~1;
			for (; i < length; i+=2) {
				this[keyValuePairs[i]] = keyValuePairs[i+1];
			}
		}
		public override string ToString() {
			if (this.Count <= 0) {
				return string.Empty;
			}
			var builder = new StringBuilder();
			if(parameterOrderList.Count == 0)
            {
				foreach (var keyValuePair in this)
				{
					builder
						.Append(InfoString.Delimiter)
						.Append(keyValuePair.Key)
						.Append(InfoString.Delimiter)
						.Append(keyValuePair.Value);
				}
			} 
			else
            {
				// Order the values according to list we have.
				// TODO Optimize this a bit.
				var valuesHere = this.ToArray();
				HashSet<string> usedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				lock (parameterOrderList)
				{
                    foreach (string parameter in parameterOrderList)
                    {
                        if (this.ContainsKey(parameter))
                        {
                            if (!usedParameters.Contains(parameter))
                            {
								usedParameters.Add(parameter);
								builder
								.Append(InfoString.Delimiter)
								.Append(parameter)
								.Append(InfoString.Delimiter)
								.Append(this[parameter]);
							}
						}
                    }
				}
				foreach (var keyValuePair in this) 
				{
					if (!usedParameters.Contains(keyValuePair.Key))
					{
						Debug.WriteLine($"InfoString.ToString(): WEIRD, {keyValuePair.Key} not caught in parameterOrderList.");
						builder
						.Append(InfoString.Delimiter)
						.Append(keyValuePair.Key)
						.Append(InfoString.Delimiter)
						.Append(keyValuePair.Value);
					}
				}
			}
			return builder.ToString();
		}
		public KeyValuePair<string,string>[] ToArray() {
			if (this.Count <= 0) {
				return new KeyValuePair<string, string>[0];
			}
			KeyValuePair<string, string>[] kvpa = new KeyValuePair<string, string>[this.Count];
			int index = 0;
			foreach (var keyValuePair in this) {
				kvpa[index++] = keyValuePair;
			}
			return kvpa;
		}
		private class InfoStringComparer : EqualityComparer<string> {
			public override bool Equals(string x, string y) {
				return x.Equals(y, StringComparison.OrdinalIgnoreCase);
			}
			public override int GetHashCode(string obj) {
				return obj.GetHashCode();
			}
		}
	}


}
