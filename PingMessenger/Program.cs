using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
	static async Task Main(string[] args)
	{
		Console.WriteLine("Ping Messenger");
		Console.WriteLine("What is your local ip:");
		string localIpString = Console.ReadLine().Trim().ToLower();
		if(long.TryParse(localIpString, out long localIp))
		{
			Console.WriteLine("Invalid IP address.");
			return;
		}

		// Start receiving messages in a separate task
		CancellationTokenSource cts = new CancellationTokenSource();
		Task receiveTask = Task.Run(() => ReceiveMessages(cts.Token, localIp));

		// Main loop for sending messages
		while (true)
		{
			Console.WriteLine("Enter 'send' to send a message or 'exit' to quit:");
			string command = Console.ReadLine().Trim().ToLower();

			if (command == "send")
			{
				await SendMessage();
			}
			else if (command == "exit")
			{
				cts.Cancel(); // Stop the receive task
				await receiveTask; // Wait for the receive task to complete
				break;
			}
			else
			{
				Console.WriteLine("Invalid command.");
			}
		}
	}

	static async Task SendMessage()
	{
		Console.Write("Enter the IP address to ping: ");
		string ipAddress = Console.ReadLine();

		Console.Write("Enter your message: ");
		string message = Console.ReadLine();

		using (Ping ping = new Ping())
		{
			PingOptions options = new PingOptions
			{
				DontFragment = true
			};

			byte[] buffer = Encoding.ASCII.GetBytes(message);
			int timeout = 120;

			try
			{
				PingReply reply = await ping.SendPingAsync(ipAddress, timeout, buffer, options);
				if (reply.Status == IPStatus.Success)
				{
					Console.WriteLine($"Message sent successfully to {ipAddress}. Roundtrip time: {reply.RoundtripTime} ms");
				}
				else
				{
					Console.WriteLine($"Failed to send message: {reply.Status}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error: {ex.Message}");
			}
		}
	}


	//static async Task ReceiveMessages(CancellationToken token)
	//{
	//	Console.WriteLine("Receiving messages...");

	//	using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp))
	//	{
	//		socket.Bind(new IPEndPoint(IPAddress.Any, 0));
	//		byte[] buffer = new byte[1024];

	//		while (!token.IsCancellationRequested)
	//		{
	//			try
	//			{
	//				socket.ReceiveTimeout = 5000; // 5 seconds timeout
	//				EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
	//				int receivedLength = socket.ReceiveFrom(buffer, ref remoteEndPoint);

	//				if (receivedLength > 0)
	//				{
	//					// Check for ICMP Echo Request
	//					if (buffer[0] == 8) // ICMP Echo Request
	//					{
	//						// ICMP packets start with an 8-byte header
	//						// Type (1 byte), Code (1 byte), Checksum (2 bytes), Identifier (2 bytes), Sequence Number (2 bytes)
	//						// The message starts after these 8 bytes
	//						string receivedMessage = Encoding.ASCII.GetString(buffer, 8, receivedLength - 8);
	//						Console.WriteLine("\nReceived message: {0}\n", receivedMessage);
	//					}
	//				}
	//			}
	//			catch (SocketException ex)
	//			{
	//				if (ex.SocketErrorCode == SocketError.TimedOut)
	//				{
	//					// Timeout, no data received, continue to next iteration
	//					continue;
	//				}
	//				Console.WriteLine("Socket error: {0}", ex.Message);
	//			}

	//			await Task.Delay(100); // Slight delay to prevent tight loop
	//		}
	//	}

	//	Console.WriteLine("Stopped receiving messages.");
	//}

	static async Task ReceiveMessages(CancellationToken token, long localIP)
	{
		//Console.WriteLine("Receiving messages...");

		using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP))
		{
			socket.Bind(new IPEndPoint(localIP, 0));
			socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);

			byte[] buffer = new byte[4096];

			while (!token.IsCancellationRequested)
			{
				try
				{
					if (socket.Available == 0)
					{
						await Task.Delay(100); // Slight delay to prevent tight loop
						continue;
					}

					int bytesRead = socket.Receive(buffer);
					var icmpPacket = new ICMPEchoRequest(buffer, bytesRead);

					if (icmpPacket != null)
					{
						Console.WriteLine($"Received ICMP Echo Request from {icmpPacket.SourceAddress}");
						// Here you can process the ICMP Echo Request further if needed
						//string receivedMessage = Encoding.ASCII.GetString(buffer, 8, receivedLength - 8);
						//Console.WriteLine("\nReceived message: {0}\n", receivedMessage);
						string receivedMessage = Encoding.ASCII.GetString(icmpPacket.Data);
						Console.WriteLine($"Message: {receivedMessage}");//receivedMessage}");
					}
				}
				catch (SocketException ex)
				{
					Console.WriteLine("Socket error: {0}", ex.Message);
				}
			}
		}

		Console.WriteLine("Stopped receiving messages.");
	}

	class ICMPEchoRequest
	{
		public IPAddress SourceAddress { get; private set; }
		public byte[] Data { get; private set; }

		public ICMPEchoRequest(byte[] buffer, int bytesRead)
		{
			// Assuming IPv4 and no IP options, the source address starts at byte 12
			SourceAddress = new IPAddress(BitConverter.ToUInt32(buffer, 12));
			// The data starts after the IP header (20 bytes for basic IPv4 header) and ICMP header (8 bytes)
			int headerOffset = 20 + 8; // Adjust if your IP header length varies
			Data = new byte[bytesRead - headerOffset];
			Array.Copy(buffer, headerOffset, Data, 0, bytesRead - headerOffset);
		}
	}


}
