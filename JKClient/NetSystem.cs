using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace JKClient {
	internal sealed class NetSystem : IDisposable {
		private const ushort PortServerDefault = 29070;
		private ushort portServer = 29070;
		private Socket ipSocket;
		private Socket socksSocket;
		private IPEndPoint endPoint;
		private bool disposed = false;

		private SocksProxy? proxy = null;

		public NetSystem(ushort portServerA, SocksProxy? proxyA = null) {
			portServer = portServerA;
			proxy = proxyA;
			this.InitSocket();
		}

		private void OpenSocks(short port)
        {
			if (!proxy.HasValue) return;
			if (proxy.Value.address == null) return;

			byte[] buf = new byte[64];
			int len;

			SocksProxy socks = proxy.Value;
			IPEndPoint socksEndpoint;
			//Socket socksSocket;

			socks.active = false;
			Debug.WriteLine("Opening connection to SOCKS server.\n");

            try {
				bool rfc1929 = false;
				socksSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				socksEndpoint = new IPEndPoint(socks.address.IPAsLong(), socks.address.Port);
				//socksSocket.Connect(socksEndpoint);
				socksSocket.ConnectAsync(socksEndpoint).Wait(1000);

                if (!socksSocket.Connected)
                {
					Debug.WriteLine("OpenSocks: Connecting failed.\n");
					goto OpenSocksReturn;
				}

				if(!string.IsNullOrEmpty(socks.username) || !string.IsNullOrEmpty(socks.password))
                {
					// send socks authentication handshake
					rfc1929 = true;
				}

				buf[0] = 5;     // SOCKS version
								// method count
				if (rfc1929)
				{
					buf[1] = 2;
					len = 4;
				}
				else
				{
					buf[1] = 1;
					len = 3;
				}
				buf[2] = 0;     // method #1 - method id #00: no authentication
				if (rfc1929)
				{
					buf[2] = 2;     // method #2 - method id #02: username/password
				}

				socksSocket.Send(buf, len, SocketFlags.None);

				// get the response
				len = socksSocket.Receive(buf); 
				
				if (len != 2 || buf[0] != 5)
				{
					Debug.WriteLine("OpenSocks: bad response\n");
					goto OpenSocksReturn;
				}
				switch (buf[1])
				{
					case 0: // no authentication
						break;
					case 2: // username/password authentication
						break;
					default:
						Debug.WriteLine("OpenSocks: request denied\n");
						goto OpenSocksReturn;
				}

				// do username/password authentication if needed
				if (buf[1] == 2)
				{
					int ulen;
					int plen;

					// build the request
					ulen = (int)(string.IsNullOrEmpty(socks.username)?0:socks.username.Length);
					plen = (int)(string.IsNullOrEmpty(socks.password) ? 0 : socks.password.Length);

					buf[0] = 1;     // username/password authentication version
					buf[1] = (byte)ulen;
					if (ulen > 0)
					{
						Array.Copy(Encoding.ASCII.GetBytes(socks.username),0,buf,2,ulen);
					}
					buf[2 + ulen] = (byte)plen;
					if (plen > 0)
					{
						Array.Copy(Encoding.ASCII.GetBytes(socks.password), 0, buf, 3+ulen, plen);
					}

					// send it
					socksSocket.Send(buf,3+ulen+plen,SocketFlags.None);

					// get the response
					len = socksSocket.Receive(buf); //len = recv(socks_socket, (char*)buf, 64, 0);
					if (len != 2 || buf[0] != 1)
					{
						Debug.WriteLine("OpenSocks: bad response\n");
						goto OpenSocksReturn;
					}
					if (buf[1] != 0)
					{
						Debug.WriteLine("OpenSocks: authentication failed\n");
						goto OpenSocksReturn;
					}
				}

				// send the UDP associate request
				buf[0] = 5;     // SOCKS version
				buf[1] = 3;     // command: UDP associate
				buf[2] = 0;     // reserved
				buf[3] = 1;     // address type: IPV4
				{
					buf[4] = 0;
					buf[5] = 0;
					buf[6] = 0;
					buf[7] = 0;
				}
				{
					Int16 networkOrderPort = IPAddress.HostToNetworkOrder(port);// port
					Array.Copy(BitConverter.GetBytes(networkOrderPort), 0, buf, 8, 2);
				}
				socksSocket.Send(buf, 10, SocketFlags.None);

				// get the response
				len = socksSocket.Receive(buf);
				if (len < 2 || buf[0] != 5)
				{
					Debug.WriteLine("OpenSocks: bad response\n");
					goto OpenSocksReturn;
				}
				// check completion code
				if (buf[1] != 0)
				{
					Debug.WriteLine("OpenSocks: request denied: %i\n", buf[1]);
					goto OpenSocksReturn;
				}
				if (buf[3] != 1)
				{
					Debug.WriteLine("OpenSocks: relay address is not IPV4: %i\n", buf[3]);
					goto OpenSocksReturn;
				}
				byte[] ip = new byte[4];
				Array.Copy(buf,4,ip,0,4);
				short relayPort = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buf, 8));
				NetAddress udpRelayAddr = new NetAddress(ip, (ushort)relayPort);

				socks.udpRelayAddress = udpRelayAddr;
				socks.active = true;


			} catch(Exception e)
            {
				Debug.WriteLine($"WARNING: OpenSocks: {e.ToString()}");
			}
			OpenSocksReturn:
			proxy = socks;
			return;
		}

		private void InitSocket(bool reinit = false) {
			try {
				this.ipSocket?.Close();
			} catch {}
			try {
				this.socksSocket?.Close();
			} catch {}
			this.ipSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
				Blocking = false,
				EnableBroadcast = true
			};
			this.ipSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
			int i;
			bool tryToReuse = false;
			if (reinit) {
				i = this.endPoint.Port - portServer;
				if (i < 0) {
					i = 0;
					tryToReuse = false;
				} else {
					this.ipSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
					tryToReuse = true;
				}
			} else {
				i = 0;
			}
			bool triedToReuse = false;
			for (; i < 256; i++) {
				try {
					this.endPoint = new IPEndPoint(IPAddress.Any, portServer + i);
					if (tryToReuse && triedToReuse) {
						this.ipSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
						tryToReuse = false;
					}
					if (tryToReuse && !triedToReuse) {
						triedToReuse = true;
						i = -1;
					}
					this.ipSocket.Bind(this.endPoint);
                    if (proxy.HasValue)
                    {
						OpenSocks((short)(ushort)this.endPoint.Port);
					}
				} catch (SocketException exception) {
					switch (exception.SocketErrorCode) {
					case SocketError.AddressAlreadyInUse:
//					case SocketError.AddressFamilyNotSupported:
						break;
					default:
						throw;
					}
					Debug.WriteLine(exception);
					continue;
				}
				break;
			}
			if (reinit) {
				this.ipSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
			}
		}

		byte[] socksBuf = new byte[4096];
		public void SendPacket(int length, byte []data, NetAddress address) {
			if (this.disposed) {
				return;
			}
			lock (this.ipSocket) {
				try {
					if(proxy.HasValue && proxy.Value.active)
					{
						socksBuf[0] = 0;    // reserved
						socksBuf[1] = 0;
						socksBuf[2] = 0;    // fragment (not fragmented)
						socksBuf[3] = 1;    // address type: IPV4
						Array.Copy(address.IP, 0, socksBuf, 4, 4);
						Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)address.Port)), 0, socksBuf, 8, 2);
						Array.Copy(data, 0, socksBuf, 10, length);
						this.ipSocket.SendTo(socksBuf, length+10, SocketFlags.None, proxy.Value.udpRelayAddress.ToIPEndPoint());
					} else
					{
						this.ipSocket.SendTo(data, length, SocketFlags.None, address.ToIPEndPoint());
					}
				} catch (SocketException exception) {
					switch (exception.SocketErrorCode) {
					case SocketError.WouldBlock:
						break;
					case SocketError.NotConnected:
					case SocketError.Shutdown:
						this.InitSocket(true);
						goto default;
					default:
						Debug.WriteLine("SocketException:");
						Debug.WriteLine(exception);
						break;
					}
				}
			}
		}
		public bool GetPacket(ref NetAddress address, Message msg) {
			if (this.disposed) {
				return false;
			}
			EndPoint endPoint = new IPEndPoint(0, 0);
			try {
                if (!this.ipSocket.Poll(0,SelectMode.SelectRead))
                {
					return false;
                }
				int ret = this.ipSocket.ReceiveFrom(msg.Data, msg.MaxSize, SocketFlags.None, ref endPoint);
#if STRONGREADDEBUG
				msg.doDebugLogExt($"GetPacket: received {ret} bytes");
#endif
				if (ret == msg.MaxSize) {
#if STRONGREADDEBUG
					msg.doDebugLogExt("GetPacket: ret == msg.MaxSize");
#endif
					return false;
				}
				var ipEndPoint = endPoint as IPEndPoint;
				address = new NetAddress(ipEndPoint.Address.GetAddressBytes(), (ushort)ipEndPoint.Port);
				if (proxy.HasValue && proxy.Value.active && proxy.Value.udpRelayAddress == address) // Receiving via SOCKS proxy
                {
                    if (ret < 10 || msg.Data[0] != 0 || msg.Data[1] != 0 || msg.Data[2] != 0 || msg.Data[3] != 1)
                    {
						return false;
					}
					byte[] realIp = new byte[4];
					Array.Copy(msg.Data, 4, realIp, 0, 4);
					ushort realPort = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(msg.Data, 8));
					address = new NetAddress(realIp, realPort);
					byte[] dataCopy = (byte[])msg.Data.Clone();
					ret -= 10;
					Array.Copy(dataCopy, 10, msg.Data, 0, ret);
#if STRONGREADDEBUG
					msg.doDebugLogExt("GetPacket: copied from proxy message");
#endif
				}
				msg.CurSize = ret;
				return true;
			} catch (SocketException exception) {
				switch (exception.SocketErrorCode) {
				case SocketError.WouldBlock:
				case SocketError.ConnectionReset:
					break;
				case SocketError.NotConnected:
				case SocketError.Shutdown:
					this.InitSocket(true);
					goto default;
				default:
					Debug.WriteLine("SocketException:");
					Debug.WriteLine(exception);
					break;
				}
				return false;
			}
		}
		public static async Task<NetAddress> StringToAddressAsync(string address, ushort port = 0, bool doDNSLookup = true) {
			byte []ip;
			int index = address.IndexOf(':');
			if (port <= 0) {
				port = index >= 0 && ushort.TryParse(address.Substring(index+1), out ushort p) ? p : NetSystem.PortServerDefault;
			}
			if (index < 0) {
				index = address.Length;
			}
			ip = IPAddress.TryParse(address.Substring(0, index), out IPAddress ipAddress) ? ipAddress.GetAddressBytes() : null;
			if (ip == null) {
                if (doDNSLookup)
				{
					try
					{
						var hostEntry = await Dns.GetHostEntryAsync(address);
						ip = hostEntry.AddressList.FirstOrDefault(adr => adr.AddressFamily == AddressFamily.InterNetwork)?.GetAddressBytes();
					}
					catch (SocketException exception)
					{
						if (exception.SocketErrorCode == SocketError.HostNotFound)
						{
							return null;
						}
						else
						{
							throw;
						}
					}
				} else
                {
					return null;
                }
			}
			return new NetAddress(ip, port);
		}
		public static NetAddress StringToAddress(string address, ushort port = 0, bool doDNSLookup = true) {
			return NetSystem.StringToAddressAsync(address, port, doDNSLookup).Result;
		}
		public void Dispose() {
			this.disposed = true;
			this.ipSocket?.Close(5);
			this.socksSocket?.Close(5);
		}
	}
	public static class NetSystemExtensions {
		public static IPEndPoint ToIPEndPoint(this NetAddress address) {
			return new IPEndPoint(new IPAddress(address.IP), address.Port);
		}
	}
}
