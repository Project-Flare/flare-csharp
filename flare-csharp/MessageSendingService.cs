using Flare.V1;
using flare_csharp.Services;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace flare_csharp
{
	public enum MSSState { Initialized, Connecting, Connected, SendingMessage, Reconnecting, Aborted, Exited };
	public enum MSSCommand { StartService, Success, Failure, SendEnqueuedMessage, MessageSent, Reconnect, Reconnected, Abort }
	public sealed class MessageSendingService : Service<MSSState, MSSCommand, GrpcChannel>
	{
		private IdentityStore _identityStore; 
		public Credentials Credentials { get; set; }
		public string ServerUrl { get; set; }
		private ConcurrentQueue<OutboundMessage> sendMessagesQueue;
		private ConcurrentQueue<OutboundMessage> sentMessagesQueue;
		public MessageSendingService(Process<MSSState, MSSCommand> process, string serverUrl, Credentials credentials, GrpcChannel channel, IdentityStore identityStore) : base(process, channel)
		{
			Credentials = credentials;
			ServerUrl = serverUrl;
			sendMessagesQueue = new ConcurrentQueue<OutboundMessage>();
			sentMessagesQueue = new ConcurrentQueue<OutboundMessage>();

			_identityStore = identityStore;
		}
		public override async void RunServiceAsync()
		{
			Messaging.MessagingClient messagingClient = new Messaging.MessagingClient(Channel);
			while (!ServiceEnded())
			{
				switch (State)
				{
					case MSSState.Initialized:
						// Do nothing, can be moved to next state only when the service is manually started
						break;
					case MSSState.Connecting:
						try
						{
							CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
							await Channel.ConnectAsync(cancellationTokenSource.Token);
							Process.MoveToNextState(MSSCommand.Success);
						}
						catch
						{
							Process.MoveToNextState(MSSCommand.Failure);
						}
						break;
					case MSSState.Connected:
						if (!sendMessagesQueue.IsEmpty)
						{
							Process.MoveToNextState(command: MSSCommand.SendEnqueuedMessage);
						}
						break;
					case MSSState.SendingMessage:
						OutboundMessage? message;
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
							Metadata headers = new Metadata { { "flare-auth", Credentials.AuthToken } };
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
								await Channel.ConnectAsync(cts.Token);
								if (Channel.State == ConnectivityState.Ready)
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
		public void SendMessage(OutboundMessage message)
		{
			sendMessagesQueue.Enqueue(message);
		}
		protected override void DefineWorkflow()
		{
			Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.Initialized, MSSCommand.StartService), processState: MSSState.Connecting);
			Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.Connecting, MSSCommand.Success), processState: MSSState.Connected);
			Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.Connecting, MSSCommand.Failure), processState: MSSState.Reconnecting);
			Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.Connected, MSSCommand.SendEnqueuedMessage), processState: MSSState.SendingMessage);
			Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.SendingMessage, MSSCommand.MessageSent), processState: MSSState.Connected);
			Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.SendingMessage, MSSCommand.Reconnect), processState: MSSState.Reconnecting);
			Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.Reconnecting, MSSCommand.Reconnected), processState: MSSState.Connected);
			Process.AddStateTransition(transition: new Process<MSSState, MSSCommand>.StateTransition(MSSState.Reconnecting, MSSCommand.Abort), processState: MSSState.Aborted);
		}

		protected override bool ServiceEnded()
		{
			return Channel.State == ConnectivityState.TransientFailure
				|| Channel.State == ConnectivityState.Shutdown;
		}

		public override void StartService()
		{
			if (State == MSSState.Initialized)
				Process.MoveToNextState(MSSCommand.StartService);
			if (State == MSSState.Aborted) // [TODO]: throw better Exception
				throw new InvalidOperationException("The service is aborted");
		}

		public override void EndService()
		{
			throw new NotImplementedException();
		}

		public sealed class OutboundMessage         // this may change later, now I just need a simple wrapper class for sending messages
		{
			public string RecipientUsername { get; set; }
			public string MessageText { get; set; }
			public bool IsSentSuccessfully { get; set; }
			public OutboundMessage(string recipientUsername, string messageText)
			{
				RecipientUsername = recipientUsername;
				MessageText = messageText;
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
