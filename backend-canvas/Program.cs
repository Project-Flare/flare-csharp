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
			var clientManager = new ClientManager("https://rpc.f2.project-flare.net");
			clientManager.Credentials.Username = "testing_user_0";
			clientManager.Credentials.Password = "190438934";
			clientManager.Credentials.Argon2Hash = "$argon2i$v=19$m=524288,t=3,p=4$dGVzdGluZ191c2VyXzBwcm9qZWN0LWZsYXJlLm5ldGVNVEhJaWl0NlNTcWZKdWg2UEovM3c$tHhA3AmlEH8ao3vypVV36eyzbKfuX2b5a+5OCdD0kJI";
			await clientManager.LoginToServerAsync();
			await clientManager.SendMessageAsync("UwU", clientManager.Username);
			WebSocketListener wsl = new WebSocketListener();
			wsl.AuthToken = clientManager.Credentials.AuthToken;
			Thread receiver = new Thread(wsl.RunService);
			receiver.Name = "RECEIVER";
			receiver.IsBackground = true;
			receiver.Start();
			await clientManager.SendMessageAsync("UwU_2", clientManager.Username);
			await clientManager.SendMessageAsync("UwU_3", clientManager.Username);
			while (true) 
			{
				Console.WriteLine("[INFO_MAIN_THREAD]: Received messages:");
				wsl.messageQueue.ToList().ForEach(message => Console.WriteLine($"\t{message.SenderUsername} : {message.ServerTime}\n"));
				wsl.messageQueue = new System.Collections.Concurrent.ConcurrentQueue<InboundUserMessage>();
				Thread.Sleep(10000);
			}
		}
	}
}
