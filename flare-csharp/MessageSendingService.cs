using Flare.V1;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
	public class MessageSendingService
	{
		public GrpcChannel Channel { get; set; }
		public ClientCredentials Credentials { get; set; }
		public Messaging.MessagingClient MessagingClient { get; set; }
		private ConcurrentQueue<MessageRequest> messageQueue;
		private Thread messageSendingThread;

		public MessageSendingService(GrpcChannel channel, ClientCredentials credentials)
		{
			Channel = channel;
			Credentials = credentials;
			MessagingClient = new Messaging.MessagingClient(channel);
			messageQueue = new ConcurrentQueue<MessageRequest>();
			messageSendingThread = new Thread(RunService);
		}

		public void StartService()
		{
			messageSendingThread.Start();
		}

		public void EnqueueMessage(MessageRequest messageRequest)
		{
			messageQueue.Enqueue(messageRequest);
		}

		private async void RunService()
		{
			while (true)
			{
				try
				{
					await SendEnqueuedMessageAsync(5);
				}
				catch
				{
					// todo
				}
			}
		}

		private async Task SendEnqueuedMessageAsync(int timeoutSeconds)
		{
			MessageRequest? messageRequest;
			messageQueue.TryPeek(out messageRequest);
			if (messageRequest is not null)
			{
				var ct = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
				var metadata = new Metadata { { "flare-auth", ClientCredentials.AuthToken } };
				MessageResponse response = await MessagingClient.MessageAsync(messageRequest, headers: metadata, deadline: DateTime.UtcNow.AddSeconds(timeoutSeconds));
				if (response.HasSuccess)
				{
					messageQueue.TryDequeue(out _);
				}
			}
		}
	}
}
