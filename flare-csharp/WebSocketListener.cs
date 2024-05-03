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
		public async void RunService()
		{
			using (var webSocket = new ClientWebSocket())
			{
				try
				{
					var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
					await webSocket.ConnectAsync(new Uri("wss://ws.f2.project-flare.net/"), cts.Token);
					await webSocket.SendAsync(new SubscribeRequest { Token = AuthToken, BeginTimestamp = "0" }.ToByteArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
					while (webSocket.State == WebSocketState.Open)
					{
						byte[] buffer = new byte[1024];
						int byteCount = 0;
						int free = buffer.Length;
						while (true)
						{
							WebSocketReceiveResult response = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, byteCount, free), cts.Token);
							if (response.EndOfMessage)
							{
								byteCount += response.Count;
								break;
							}
							if (free.Equals(response.Count))
							{
								int newSize = buffer.Length * 2;
								if (newSize > 2_000_000)
									break;
								byte[] newBuffer = new byte[newSize];
								Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
								free = newBuffer.Length - buffer.Length;
								byteCount = buffer.Length;
								buffer = newBuffer;
							}
						}
						cts.TryReset();
						messageQueue.Enqueue(InboundUserMessage.Parser.ParseFrom(buffer, offset: 0, length: byteCount));
						await webSocket.SendAsync(Encoding.ASCII.GetBytes("PING ME"), WebSocketMessageType.Binary, endOfMessage: true, cts.Token);
						cts.TryReset();
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[ERROR_LISTENER]: {ex.Message}");
				}
			}
		}
	}
}
