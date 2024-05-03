using Flare.V1;
using Grpc.Net.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static flare_csharp.Services.SetLoginCreds;

namespace flare_csharp
{
	namespace Services
	{
		public abstract class Service<TState, TChannel> where TState : Enum
		{
			public virtual TState State { get; protected set; }
			public virtual Process<TState> Process { get; protected set; }
			protected Service(TState initialState, Process<TState> process)
			{
				State = initialState;
				Process = process;
			}
			public abstract Task RunServiceAsync(TChannel channel, Process<TState> process);
			protected abstract void DefineWorkflow();
			protected abstract bool ServiceEnded(TChannel channel);
		}

		public enum MSSState { Connected, Disconnected, SendingMessage };
		public sealed class MessagingSendingService : Service<MSSState, GrpcChannel>
		{
			private ConcurrentQueue<Message> messageQueue;
			public MessagingSendingService(MSSState initialState, Process<MSSState> process) : base(initialState, process)
			{
				messageQueue = new ConcurrentQueue<InboundUserMessage>();
			}
			public override async Task RunServiceAsync(GrpcChannel channel, Process<MSSState> process)
			{
				Messaging.MessagingClient messagingClient = new Messaging.MessagingClient(channel);
				while (!ServiceEnded(channel))
				{

				}
			}
			public void SendMessage(Message message) 
			{
				messageQueue.Enqueue(message);
			}
			protected override void DefineWorkflow()
			{
				throw new NotImplementedException();
			}

			protected override bool ServiceEnded(GrpcChannel channel)
			{
				return channel.State == Grpc.Core.ConnectivityState.TransientFailure
					|| channel.State == Grpc.Core.ConnectivityState.Shutdown
					|| channel.State == Grpc.Core.ConnectivityState.Shutdown;
			}
			public sealed class Message			// this may change later, now I just need a simple wrapper class for sending messages
			{
				public string RecipientUsername { get; set; }
				public string MessageText { get; set; }
				public Message(string recipientUsername, string messageText)
				{
					RecipientUsername = recipientUsername;
					MessageText = messageText;
				}
			}
		}

		public enum SetLoginCredsServiceState { FetchRequirements, SetUsername, SetPassword, FetchOpinion, EndSuccess, TryRecconect, EndFailure }
		public class SetLoginCredsService : Service<SetLoginCredsServiceState, GrpcChannel>
		{
			public SetLoginCredsService(SetLoginCredsServiceState initialState, Process<SetLoginCredsServiceState> process) : base(initialState, process)
			{
				DefineWorkflow();
			}
			public override async Task RunServiceAsync(GrpcChannel channel, Process<SetLoginCredsServiceState> process)
			{
				RequirementsResponse? serverRequirements;
				var authClientService = new Auth.AuthClient(channel);
				while (!ServiceEnded(channel))
				{
					switch (process.CurrentState)
					{
						case SetLoginCredsServiceState.FetchRequirements:
							try
							{
								serverRequirements = await authClientService.GetCredentialRequirementsAsync(
									request: new RequirementsRequest { });
								if (serverRequirements is not null)
								{
									Process.MoveToNextState(Process<SetLoginCredsServiceState>.Command.Success);
								}
							}
							catch (Exception)
							{
								Process.GoTo(SetLoginCredsServiceState.TryRecconect);
							}
							break;
						case SetLoginCredsServiceState.SetUsername:
							// TODO
							break;
						case SetLoginCredsServiceState.SetPassword:
							//TODO
							break;
						case SetLoginCredsServiceState.FetchOpinion:
							// TODO
							break;
						case SetLoginCredsServiceState.EndSuccess:
							//TODO
							break;
						case SetLoginCredsServiceState.TryRecconect:
							//TODO
							break;
						case SetLoginCredsServiceState.EndFailure:
							//TODO
							break;
						default:
							// TODO
							break;
					}
				}

			}

			protected override bool ServiceEnded(GrpcChannel channel)
			{
				return Process.CurrentState == SetLoginCredsServiceState.EndSuccess
					|| Process.CurrentState == SetLoginCredsServiceState.EndSuccess
					|| channel.State == Grpc.Core.ConnectivityState.Idle
					|| channel.State == Grpc.Core.ConnectivityState.TransientFailure
					|| channel.State == Grpc.Core.ConnectivityState.Shutdown;
			}

			protected override void DefineWorkflow()
			{
				Process.AddStateTransition(new Process<SetLoginCredsServiceState>.StateTransition(
					currentState: SetLoginCredsServiceState.FetchRequirements,
					command: Process<SetLoginCredsServiceState>.Command.Success), SetLoginCredsServiceState.SetUsername);
				Process.AddStateTransition(new Process<SetLoginCredsServiceState>.StateTransition(
					currentState: SetLoginCredsServiceState.SetUsername,
					command: Process<SetLoginCredsServiceState>.Command.Success), SetLoginCredsServiceState.SetPassword);
				Process.AddStateTransition(new Process<SetLoginCredsServiceState>.StateTransition(
					currentState: SetLoginCredsServiceState.SetPassword,
					command: Process<SetLoginCredsServiceState>.Command.Success), SetLoginCredsServiceState.FetchOpinion);
				Process.AddStateTransition(new Process<SetLoginCredsServiceState>.StateTransition(
					currentState: SetLoginCredsServiceState.FetchOpinion,
					command: Process<SetLoginCredsServiceState>.Command.Success), SetLoginCredsServiceState.EndSuccess);
				Process.AddStateTransition(new Process<SetLoginCredsServiceState>.StateTransition(
					currentState: SetLoginCredsServiceState.FetchOpinion,
					command: Process<SetLoginCredsServiceState>.Command.Failure), SetLoginCredsServiceState.SetUsername);
				Process.AddStateTransition(new Process<SetLoginCredsServiceState>.StateTransition(
					currentState: SetLoginCredsServiceState.FetchRequirements,
					command: Process<SetLoginCredsServiceState>.Command.Failure), SetLoginCredsServiceState.TryRecconect);
				Process.AddStateTransition(new Process<SetLoginCredsServiceState>.StateTransition(
					currentState: SetLoginCredsServiceState.TryRecconect,
					command: Process<SetLoginCredsServiceState>.Command.Success), SetLoginCredsServiceState.FetchRequirements);
				Process.AddStateTransition(new Process<SetLoginCredsServiceState>.StateTransition(
					currentState: SetLoginCredsServiceState.TryRecconect,
					command: Process<SetLoginCredsServiceState>.Command.Failure), SetLoginCredsServiceState.EndFailure);
			}
		}

		public class SetLoginCreds
		{
			public enum ProcessState { FetchRequirements, SetUsername, SetPassword, FetchOpinion, EndSuccess, TryRecconect, EndFailure }

			public delegate void RunServiceDelegate(GrpcChannel channel, Auth.AuthClient authClientService, Process<ProcessState> process);
			public async void RunService(GrpcChannel channel, Auth.AuthClient authClientService, Process<ProcessState> process)
			{
				RequirementsResponse? serverRequirements;
				DefineProcessFlow(process);
				while (process.CurrentState != ProcessState.EndSuccess)
				{
					switch (process.CurrentState)
					{
						case ProcessState.FetchRequirements:
							serverRequirements =
								await authClientService.GetCredentialRequirementsAsync(
										request: new RequirementsRequest { });
							if (serverRequirements is not null)
							{
								process.MoveToNextState(Process<ProcessState>.Command.Success);
							}
							break;
						case ProcessState.SetUsername:
							// TODO
							break;
						case ProcessState.SetPassword:
							//TODO
							break;
						case ProcessState.FetchOpinion:
							// TODO
							break;
						case ProcessState.EndSuccess:
							//TODO
							break;
						default:
							Console.WriteLine("[NOT IMPLEMENTED]");
							break;
					}
				}
			}
			public static void DefineProcessFlow(Process<ProcessState> process)
			{
				// VERY TODO
				process.AddStateTransition(new Process<ProcessState>.StateTransition(
					currentState: ProcessState.FetchRequirements,
					command: Process<ProcessState>.Command.Success), ProcessState.SetUsername);
				process.AddStateTransition(new Process<ProcessState>.StateTransition(
					currentState: ProcessState.SetUsername,
					command: Process<ProcessState>.Command.Success), ProcessState.SetPassword);
				process.AddStateTransition(new Process<ProcessState>.StateTransition(
					currentState: ProcessState.SetPassword,
					command: Process<ProcessState>.Command.Success), ProcessState.FetchOpinion);
				process.AddStateTransition(new Process<ProcessState>.StateTransition(
					currentState: ProcessState.FetchOpinion,
					command: Process<ProcessState>.Command.Success), ProcessState.SetUsername);
				process.AddStateTransition(new Process<ProcessState>.StateTransition(
					currentState: ProcessState.FetchOpinion,
					command: Process<ProcessState>.Command.Success), ProcessState.EndSuccess);
			}
		}
	}
}
