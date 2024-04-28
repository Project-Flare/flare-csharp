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
			var webSocket = new ClientWebSocket();

			// Connecting to server
			await webSocket.ConnectAsync(new Uri("wss://ws.f2.project-flare.net/"), CancellationToken.None);

			// Subscribing to server
			var request = new SubscribeRequest
			{
				Token = clientManager.Credentials.AuthToken
			};

			await webSocket.SendAsync(request.ToByteArray(), WebSocketMessageType.Binary, true, CancellationToken.None);

			/*// Running constant ping thread
			var pingThread = new Thread(() =>
			{
				Console.WriteLine($"[WS_STATE]: {webSocket.State}");
				Thread.CurrentThread.IsBackground = true;
                var pingTask = webSocket.SendAsync(
				Encoding.ASCII.GetBytes("PING ME"), WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);
				pingTask.Wait();
                Console.WriteLine("[PING]: sent");
				Thread.Sleep(1000);
			});
			pingThread.Name = "PING_THREAD";
			pingThread.Start();*/

			string tokenHealth = await clientManager.GetTokenHealthAsync();
			Console.WriteLine("[TOKEN_HEALTH]: " + tokenHealth);

			Console.WriteLine($"[WS_STATE]: {webSocket.State}");
			// Receiving my sent message to myself from the server
			var buf = new byte[1024];
			Console.WriteLine($"[WS_STATE]: {webSocket.State}");
			var buffer = new ArraySegment<byte>(buf);
			Console.WriteLine($"[WS_STATE]: {webSocket.State}");

			try
			{

				var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
			}
			catch
			{
				webSocket.Abort();
				webSocket = new ClientWebSocket();
				await webSocket.ConnectAsync(new Uri("wss://ws.f2.project-flare.net/"), CancellationToken.None);
			}

			Console.WriteLine($"[WS_STATE]: {webSocket.State}");

			Thread.Sleep(500000);
		}
	}
}
