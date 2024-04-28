using Flare.V1;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using flare_csharp;

namespace backend_canvas
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
			System.Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT", "1");

			var clientManager = new ClientManager("https://rpc.f2.project-flare.net");
			clientManager.Credentials.Username = "testing_user_0";
			clientManager.Credentials.Password = "190438934";
			clientManager.Credentials.Argon2Hash = "$argon2i$v=19$m=524288,t=3,p=4$dGVzdGluZ191c2VyXzBwcm9qZWN0LWZsYXJlLm5ldGVNVEhJaWl0NlNTcWZKdWg2UEovM3c$tHhA3AmlEH8ao3vypVV36eyzbKfuX2b5a+5OCdD0kJI";
			await clientManager.LoginToServerAsync();
			await clientManager.SendMessageAsync(string.Empty, clientManager.Username);
			/*await clientManager.ConnectWebSocket();
			Thread pingThread = new Thread(clientManager.Ping);
			pingThread.Start();
			Task<Flare.V1.InboundUserMessage> receiveMessageTask = clientManager.ReceiveMessagesTask();
			Console.WriteLine("Receiving message...");
			Console.WriteLine("Received message: " + receiveMessageTask.Result.ToString());
			Thread.Sleep(500000);*/
			using (var webSocket = new ClientWebSocket())
			{

				// Connecting to server
				await webSocket.ConnectAsync(new Uri("wss://ws.f2.project-flare.net/"), CancellationToken.None);

				while (webSocket.State == WebSocketState.Open)
				{
					Console.WriteLine($"[WS_STATE]: {webSocket.State}");
					// Subscribing to server
					var request = new SubscribeRequest
					{
						Token = clientManager.Credentials.AuthToken
					};
					await webSocket.SendAsync(request.ToByteArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
					// Checking token health
					string tokenHealth = await clientManager.GetTokenHealthAsync();
					Console.WriteLine("[TOKEN_HEALTH]: " + tokenHealth);
					Console.WriteLine($"[WS_STATE]: {webSocket.State}");
					// Receiving my sent message to myself from the server
					var buffer = new byte[1024];
					Console.WriteLine($"[WS_STATE]: {webSocket.State}");
					var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
					try
					{
						//var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
						var message = await ReceiveMessageAsync(webSocket, cts);
						Console.WriteLine($"[MESSAGE]: {message.ToString()}");
						cts.TryReset();
					}
					catch (Exception ex)
					{
						Console.WriteLine($"[WS_STATE]: {webSocket.State}");
						Console.WriteLine("[ERROR]: failed to receive message from the server:\n\t" + ex.Message);
					}
				}	
			}
		}

		public static async Task<InboundUserMessage> ReceiveMessageAsync(ClientWebSocket webSocket, CancellationTokenSource cts)
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

			return InboundUserMessage.Parser.ParseFrom(buffer, 0, offset);
		}
	}
}
