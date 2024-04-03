﻿using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	public sealed class NetAddress : IComparable {
		private readonly int hashCode;
		public byte []IP { get; init; }
		public ushort Port { get; init; }
		public NetAddress(NetAddress address) {
			this.IP = new byte[address.IP.Length];
			Array.Copy(address.IP, this.IP, address.IP.Length);
			this.Port = address.Port;
			this.hashCode = this.GenerateHashCode();
		}
		public NetAddress(byte []ip, ushort port) {
			this.IP = ip;
			this.Port = port;
			this.hashCode = this.GenerateHashCode();
		}
		private int GenerateHashCode() {
			return (this.IP[0], this.IP[1], this.IP[2], this.IP[3], this.Port).GetHashCode();
		}
		public static bool operator ==(NetAddress address1, NetAddress address2) {
			if (address1 is null && address2 is null)
				return true;
			if (address1 is null || address2 is null)
				return false;
			if (address1.Port != address2.Port)
				return false;
			if (address1.IP == null && address2.IP == null)
				return true;
			if (address1.IP == null || address2.IP == null)
				return false;
			if (address1.IP.Length != address2.IP.Length)
				return false;
			for (int i = 0; i < address1.IP.Length; i++) {
				if (address1.IP[i] != address2.IP[i])
					return false;
			}
			return true;
		}
		public static bool operator !=(NetAddress address1, NetAddress address2) {
			return (address1 == address2) != true;
		}
		public override bool Equals(object obj) {
			return base.Equals(obj);
		}
		public override int GetHashCode() {
			return this.hashCode;
		}
		public int IPAsLong() {
			return (this.IP == null || this.IP.Length < 4) ? 0 : BitConverter.ToInt32(this.IP,0);
		}
		public override string ToString() {
			var builder = new StringBuilder();
			for (int i = 0; i < this.IP.Length; i++) {
				if (i != 0) {
					builder.Append('.');
				}
				builder.Append(this.IP[i]);
			}
			builder.Append(':').Append(this.Port);
			return builder.ToString();
		}
		public static NetAddress FromString(string address, ushort port = 0, bool doDNSLookup = true) {
			return NetSystem.StringToAddress(address, port, doDNSLookup);
		}

        public int CompareTo(object obj) // For sorting
        {
			if (obj == null) return 1;

			NetAddress otherAddress = obj as NetAddress;
			if(otherAddress == null) return 1;
			for (int i = 0; i < this.IP.Length; i++) { // compare numbers from front to back until we find a difference, then apply sorting to that difference
				if(this.IP[i] != otherAddress.IP[i])
                {
					return this.IP[i].CompareTo(otherAddress.IP[i]);
                }
			}
			return this.Port.CompareTo(otherAddress.Port);

        }
    }
	public sealed class NetAddressComparer : EqualityComparer<NetAddress> {
		public override bool Equals(NetAddress x, NetAddress y) {
			return x == y;
		}
		public override int GetHashCode(NetAddress obj) {
			return obj.GetHashCode();
		}
	}
}
