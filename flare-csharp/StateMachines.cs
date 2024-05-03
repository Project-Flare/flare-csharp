using Flare.V1;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace flare_csharp
{
	namespace Services
	{
		public abstract class Service<TState, TCommand, TChannel> where TState : Enum where TCommand : Enum
		{
			public virtual TState State { get => Process.CurrentState; }
			public virtual Process<TState, TCommand> Process { get; protected set; }
			protected Service(Process<TState, TCommand> process)
			{
				Process = process;
				DefineWorkflow();
			}
			protected abstract void RunServiceAsync(TChannel channel, Process<TState, TCommand> process);
			protected abstract void DefineWorkflow();
			protected abstract bool ServiceEnded(TChannel channel);
			public abstract void RunService(TChannel channel);
			public abstract void EndService();
		}

		public enum MSSState { Connected, SendingMessage, Reconnecting, Aborted, Exited };
		public enum MSSCommand { SendEnqueuedMessage, MessageSent, Reconnect, Reconnected, Abort }
		public sealed class MessagingSendingService : Service<MSSState, MSSCommand, GrpcChannel>
		{
			public string ServerUrl { get; set; }
			private ConcurrentQueue<Message> sendMessagesQueue;
			private ConcurrentQueue<Message> sentMessagesQueue;
			public MessagingSendingService(Process<MSSState, MSSCommand> process, string serverUrl) : base(process)
			{
				ServerUrl = serverUrl;
				sendMessagesQueue = new ConcurrentQueue<Message>();
				sentMessagesQueue = new ConcurrentQueue<Message>();
			}
			protected override async void RunServiceAsync(GrpcChannel channel, Process<MSSState, MSSCommand> process)
			{
				Messaging.MessagingClient messagingClient = new Messaging.MessagingClient(channel);
				while (!ServiceEnded(channel))
				{
					switch (State)
					{
						case MSSState.Connected:
							if (!sendMessagesQueue.IsEmpty)
							{
								Process.MoveToNextState(command: MSSCommand.SendEnqueuedMessage);
							}
							break;
						case MSSState.SendingMessage:
							Message? message;
							sendMessagesQueue.TryPeek(out message); // won't dequeue message until I make sure I sent it
							if (message is null)
							{
								Process.MoveToNextState(command: MSSCommand.MessageSent);
								break;
							}
							try
							{
								MessageRequest messageRequest = new MessageRequest
								{
									EncryptedMessage = message.EncryptMessage(),
									RecipientUsername = message.RecipientUsername
								};
								Metadata headers = new Metadata { { "flare-auth", message.CurrentAuthToken } };
								DateTime deadline = DateTime.UtcNow.AddSeconds(5); // [NOTE]: this shouldn't be hardcoded
								MessageResponse response = await messagingClient.MessageAsync(messageRequest, headers, deadline);
								message.IsSentSuccessfully = (response.HasSuccess) ? true : false; // [TODO]: this should be handled properly
								sendMessagesQueue.TryDequeue(out _); // [NOTE]: should specify if it was sent directly or enqueued to the recipient's mailbox
								sentMessagesQueue.Enqueue(message);
								Process.MoveToNextState(MSSCommand.MessageSent);
							}
							catch // [NOTE]: maybe handle different exceptions accordingly (for example: message failed to send not because of connection issues
							{
								Process.MoveToNextState(MSSCommand.Reconnect);
							}
							break;
						case MSSState.Reconnecting:
							// [NOTE]: I guess there's a better way, but I like this approach too much
							bool reconnected = false;
							int reconnectionAttempts = 0;
							while (reconnectionAttempts < 3)
							{
								try
								{
									CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
									await channel.ConnectAsync(cts.Token);
									if (channel.State == ConnectivityState.Ready)
									{
										Process.MoveToNextState(MSSCommand.Reconnected);
										reconnected = true;
										break;
									}
								}
								catch
								{
									reconnectionAttempts++;
								}
							}
							if (!reconnected) // if all attempts of reconnection failed abort the process
							{
								Process.MoveToNextState(MSSCommand.Abort);
							}
							break;
						case MSSState.Aborted:
							// [TODO]: idk, should really ask for Herkus here, maybe raise an event?
							break;
						default:
							break;
					}
				}
			}
			public void SendMessage(Message message)
			{
				sendMessagesQueue.Enqueue(message);
			}
			protected override void DefineWorkflow()
			{
				Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.Connected, MSSCommand.SendEnqueuedMessage), processState: MSSState.SendingMessage);
				Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.SendingMessage, MSSCommand.MessageSent), processState: MSSState.Connected);
				Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.SendingMessage, MSSCommand.Reconnect), processState: MSSState.Reconnecting);
				Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.Reconnecting, MSSCommand.Reconnected), processState: MSSState.Connected);
				Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.Reconnecting, MSSCommand.Abort), processState: MSSState.Aborted);
			}

			protected override bool ServiceEnded(GrpcChannel channel)
			{
				return channel.State == Grpc.Core.ConnectivityState.TransientFailure
					|| channel.State == Grpc.Core.ConnectivityState.Shutdown
					|| channel.State == Grpc.Core.ConnectivityState.Shutdown;
			}

			public override void RunService(GrpcChannel channel)
			{
				Process.ProcessThread = new Thread(() => RunServiceAsync(channel, Process))
				{
					Name = "MESSAGE_SENDING_SERVICE_THREAD",
					IsBackground = true
				};
				Process.ProcessThread.Start();
			}

			public override void EndService()
			{
				throw new NotImplementedException();
			}

			public sealed class Message         // this may change later, now I just need a simple wrapper class for sending messages
			{
				public string RecipientUsername { get; set; }
				public string MessageText { get; set; }
				public string CurrentAuthToken { get; set; }
				public bool IsSentSuccessfully { get; set; }
				public Message(string recipientUsername, string messageText, string currentAuthToken)
				{
					RecipientUsername = recipientUsername;
					MessageText = messageText;
					CurrentAuthToken = currentAuthToken;
					IsSentSuccessfully = false;
				}
				public DiffieHellmanMessage EncryptMessage()
				{
					var encryptedMessage = new DiffieHellmanMessage
					{
						Ciphertext = ByteString.CopyFromUtf8(MessageText),
						Nonce = ByteString.CopyFromUtf8("LOL"),
						SenderIdentityPublicKey = "public_key_lol"
					};
					return encryptedMessage;
				}
			}
		}
	}
}
