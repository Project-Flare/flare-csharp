using Flare.V1;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
	public class WebSocketListener
	{
		public string AuthToken { get; set; } = string.Empty;
		public ConcurrentQueue<InboundUserMessage> messageQueue { get; set; } = new ConcurrentQueue<InboundUserMessage>();
		private async void RunService()
		{
			using (var webSocket = new ClientWebSocket())
			{
				var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
				await webSocket.ConnectAsync(new Uri("wss://ws.f2.project-flare.net/"), cts.Token);
				await webSocket.SendAsync(new SubscribeRequest { Token = AuthToken }.ToByteArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
				while(webSocket.State == WebSocketState.Open)
				{
					byte[] buffer = new byte[1024];
					int offset = 0;
					int free = buffer.Length;
					while (true)
					{
						WebSocketReceiveResult response = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free), cts.Token);
						if (response.EndOfMessage)
							break;
						if (free.Equals(response.Count))
						{
							int newSize = buffer.Length * 2;
							if (newSize > 2_000_000)
								break;
							byte[] newBuffer = new byte[newSize];
							Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
							free = newBuffer.Length - buffer.Length;
							offset = buffer.Length;
							buffer = newBuffer;
						}
					}
					cts.TryReset();
					messageQueue.Enqueue(InboundUserMessage.Parser.ParseFrom(buffer, offset: 0, length: offset));
					await webSocket.SendAsync(Encoding.ASCII.GetBytes("PING ME"), WebSocketMessageType.Binary, endOfMessage: true, cts.Token);
					cts.TryReset();
				}
			}
		}

		private async Task<InboundUserMessage> ReceiveMessageAsync(ClientWebSocket webSocket, CancellationTokenSource cts)
		{
			const int KILOBYTE = 1024;
			byte[] buffer = new byte[KILOBYTE];
			int offset = 0;
			int free = buffer.Length;

			while (true)
			{
				WebSocketReceiveResult response = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free), cts.Token);

				if (response.EndOfMessage)
				{
					offset += response.Count;
					break;
				}

				// Enlarge if the received message is bigger than the buffer
				if (free.Equals(response.Count))
				{
					int newSize = buffer.Length * 2;

					if (newSize > 2_000_000)
						break;

					byte[] newBuffer = new byte[newSize];
					Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);

					free = newBuffer.Length - buffer.Length;
					offset = buffer.Length;
					buffer = newBuffer;
				}

			}

			return InboundUserMessage.Parser.ParseFrom(buffer);
		}
	}
}
