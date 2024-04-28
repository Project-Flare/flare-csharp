using Flare.V1;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
			//System.Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT", "1");

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
				Token = ClientCredentials.AuthToken
			};

			await webSocket.SendAsync(request.ToByteArray(), WebSocketMessageType.Binary, true, CancellationToken.None);

			// Running constant ping thread
			var pingThread = new Thread(() =>
			{
				Thread.CurrentThread.IsBackground = true;
                Console.WriteLine("[PING]: sending");
                webSocket.SendAsync(
				Encoding.ASCII.GetBytes("PING ME"), WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);
                Console.WriteLine("[PING]: sent");
			});
			pingThread.Start();

			// Receiving my sent message to myself from the server
			var buffer = new byte[1024];

			await webSocket.ReceiveAsync(buffer, CancellationToken.None);

			await webSocket.CloseAsync(
				WebSocketCloseStatus.NormalClosure, 
				statusDescription: null, 
				CancellationToken.None);

			Thread.Sleep(500000);
		}
	}
}
