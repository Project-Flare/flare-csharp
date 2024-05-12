using Flare.V1;
using flare_csharp.Services;
using Google.Protobuf;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace flare_csharp
{
	public enum MRSState { Initialized, Connecting, Listening, Receiving, Aborted, Reconnecting };
	public enum MRSCommand { Run, Connected, Reconnect, Receive, Abort, End, Fail }
	public class MessageReceivingService : Service<MRSState, MRSCommand, ClientWebSocket>
	{
		public Credentials Credentials { get; set; }
		public string ServerUrl { get; private set; }
		private ConcurrentQueue<Message> receivedMessageQueue;
		public MessageReceivingService(Process<MRSState, MRSCommand> process, string serverUrl, Credentials credentials) : base(process, new ClientWebSocket())
		{
			Credentials = credentials;
			ServerUrl = serverUrl;
			receivedMessageQueue = new ConcurrentQueue<Message>();
		}

		protected override void DefineWorkflow()
		{
			Process.AddStateTransition(transition: new Process<MRSState, MRSCommand>.StateTransition(currentState: MRSState.Initialized, command: MRSCommand.Run), processState: MRSState.Connecting);
			Process.AddStateTransition(transition: new Process<MRSState, MRSCommand>.StateTransition(currentState: MRSState.Connecting, command: MRSCommand.Connected), processState: MRSState.Listening);
			Process.AddStateTransition(transition: new Process<MRSState, MRSCommand>.StateTransition(currentState: MRSState.Connecting, command: MRSCommand.Fail), processState: MRSState.Reconnecting);
			Process.AddStateTransition(transition: new Process<MRSState, MRSCommand>.StateTransition(currentState: MRSState.Listening, command: MRSCommand.Receive), processState: MRSState.Receiving);
			Process.AddStateTransition(transition: new Process<MRSState, MRSCommand>.StateTransition(currentState: MRSState.Receiving, command: MRSCommand.Reconnect), processState: MRSState.Reconnecting);
			Process.AddStateTransition(transition: new Process<MRSState, MRSCommand>.StateTransition(currentState: MRSState.Receiving, command: MRSCommand.End), processState: MRSState.Listening);
			Process.AddStateTransition(transition: new Process<MRSState, MRSCommand>.StateTransition(currentState: MRSState.Reconnecting, command: MRSCommand.End), processState: MRSState.Listening);
			Process.AddStateTransition(transition: new Process<MRSState, MRSCommand>.StateTransition(currentState: MRSState.Reconnecting, command: MRSCommand.Abort), processState: MRSState.Aborted);
		}
		public override void EndService()
		{
			throw new NotImplementedException();
		}

		public override void StartService()
		{
			if (State == MRSState.Initialized)
				Process.MoveToNextState(MRSCommand.Run);
		}

		private async Task SendSubscribeRequestAsync()
		{
			try
			{
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
				SubscribeRequest subscribeRequest = new SubscribeRequest
				{
					Token = Credentials.AuthToken,
					BeginTimestamp = string.Empty // [TODO]: do when you implement time stamping
				};
				await Channel.SendAsync(
					buffer: subscribeRequest.ToByteArray(),
					messageType: WebSocketMessageType.Binary,
					endOfMessage: true,
					cancellationToken: cancellationTokenSource.Token);
			}
			catch // [NOTE]: you probably should check what exception was thrown
			{
				Process.GoTo(MRSState.Reconnecting); // [WARNING][TODO]: never use goto, this MUST be temporary
			}
		}

		public override async void RunServiceAsync()
		{
			//[DEV_NOTES]: pinging should be simple async task that will be awaited at the end of the loop
			while (!ServiceEnded())
			{
				var pingChannelTask = PingChannel();
				switch (State)
				{
					case MRSState.Initialized:
						// Wait for the service to be started
						break;
					case MRSState.Connecting:
						try
						{
							if (Channel.State == WebSocketState.Open)
							{
								Process.MoveToNextState(MRSCommand.Connected);
							}
							else
							{
								CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
								await Channel.ConnectAsync(uri: new Uri(ServerUrl), cancellationToken: cancellationTokenSource.Token);
								await SendSubscribeRequestAsync();
								Process.MoveToNextState(MRSCommand.Connected);
							}
						}
						catch
						{
							Process.MoveToNextState(MRSCommand.Fail);
						}
						break;
					case MRSState.Listening:
						if (Channel.State == WebSocketState.Open) // [NOTE]: I think this is dumb logic, but ok
						{
							Process.MoveToNextState(MRSCommand.Receive);
						}
						break;
					case MRSState.Receiving:
						try
						{
							(byte[] data, int offset, int length) receivedData = await ReceiveMessageAsync(5);
							Message receivedMessage = new Message
							{
								InboundUserMessage = InboundUserMessage.Parser.ParseFrom(receivedData.data, receivedData.offset, receivedData.length)
							};
							receivedMessageQueue.Enqueue(receivedMessage);
							Process.MoveToNextState(MRSCommand.End);
						}
						catch //[TODO]: I guess I should check if I am connected or not? We shall see
						{
							Process.GoTo(MRSState.Reconnecting); // [WARNING]: JUST STOP PLEASE FIX THIS
						}
						break;
					case MRSState.Reconnecting:
						// [NOTE]: just try to reconnect several times
						int reconnectionAttempts = 0;
						while(reconnectionAttempts < 3)
						{
							try
							{
								Channel.Dispose();
								Channel = new ClientWebSocket();
								CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
								await Channel.ConnectAsync(new Uri(ServerUrl), cancellationTokenSource.Token);
								if (Channel.State == WebSocketState.Connecting || Channel.State == WebSocketState.Open)
								{
									await SendSubscribeRequestAsync();
									Process.MoveToNextState(MRSCommand.End);
									// [TODO]: this should't be so oogaa booga
									reconnectionAttempts = int.MaxValue;
								}
							}
							catch
							{
								Thread.Sleep(TimeSpan.FromSeconds(2));
								reconnectionAttempts++;
							}
							// [TODO]: handle abort situation
						}
						break;
					default:
						break;
				}
				await pingChannelTask;
			}
		}

		protected override bool ServiceEnded()
		{
			return State == MRSState.Aborted;
		}

		private async Task<(byte[] data, int offset, int length)> ReceiveMessageAsync(int timeoutSeconds)
		{
			const int KILOBYTE = 1024;
			byte[] buffer = new byte[KILOBYTE];
			int byteCount = 0;
			int free = buffer.Length;
			var ct = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

			while (true)
			{
				WebSocketReceiveResult response = await Channel.ReceiveAsync(new ArraySegment<byte>(buffer, byteCount, free), ct.Token);
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
			return new(buffer, 0, byteCount);
		}

		private async Task PingChannel()
		{
			try
			{
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				await Channel.SendAsync(buffer: Encoding.ASCII.GetBytes("Ping me!"), messageType: WebSocketMessageType.Binary, endOfMessage: true, cancellationToken: cancellationTokenSource.Token);
			}
			catch (Exception) //[NOTE]: this is just that the task won't throw the exception when the connection is lost on the web socket channel
			{

			}
		}

		public List<Message> FetchReceivedMessages()
		{
			var messageList = new List<Message>();
			foreach(var message in receivedMessageQueue)
			{
				messageList.Add(message);
			}
			return messageList;
		}

		public sealed class Message
		{
			public InboundUserMessage InboundUserMessage { get; set; }
			public Message()
			{
				InboundUserMessage = new InboundUserMessage();
			}
		}
	}
}
