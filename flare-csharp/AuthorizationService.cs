using Flare.V1;
using flare_csharp.Services;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Flare.V1.Auth;

namespace flare_csharp
{
	/// <summary>
	/// <list type="number">
	/// <item><see cref="ASState.Connecting"/> - connecting to the server via gRPC (initial state).</item>
	/// <item><see cref="ASState.FetchingCredReq"/>- fetching credential requirements from the server.</item>
	/// <item><see cref="ASState.SettingCreds"/>- credential requirements are set in <see cref="AuthorizationService.ValidCredentialRules"/></item>
	/// <item><see cref="ASState.Reconnecting"/>- tries to reconnect to server via gRPC channel 3 times.</item>
	/// </list>
	/// </summary>
	public enum ASState { Connecting, FetchingCredReq, SettingCreds, Reconnecting} //[TODO]: you know what
	public enum ASCommand { Proceed, Fail, Abort }
	public class AuthorizationService : Service<ASState, ASCommand, GrpcChannel>
	{
		public string ServerUrl { get; set; }
		public CredentialRequirements ValidCredentialRules { get; set; }
		public Credentials Credentials { get; set; }
		public AuthorizationService(Process<ASState, ASCommand> process, string serverUrl, GrpcChannel channel) : base(process, channel)
		{
			ServerUrl = serverUrl;
			ValidCredentialRules = new CredentialRequirements();
			Credentials = new Credentials();
		}

		public override void EndService()
		{
			throw new NotImplementedException();
		}

		public override void StartService()
		{
			Process.ProcessThread = new Thread(RunServiceAsync)
			{
				Name = "AUTH_SERVICE_THREAD",
				IsBackground = true
			};
			Process.ProcessThread.Start();
		}
		protected override async void RunServiceAsync()
		{
			AuthClient authClient = new AuthClient(Channel);
			while(!ServiceEnded())
			{
				switch(State)
				{
					case ASState.Connecting:
						try
						{
							CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
							await Channel.ConnectAsync(cancellationTokenSource.Token);
							Process.MoveToNextState(ASCommand.Proceed);
						}
						catch
						{
							Process.MoveToNextState(ASCommand.Fail);
						}
						break;
					case ASState.FetchingCredReq:
						try
						{
							await FetchCredentialRequirements(authClient);
							Process.MoveToNextState(ASCommand.Proceed);
						}
						catch
						{
							Process.MoveToNextState(command: ASCommand.Fail);
						}
						break;
					case ASState.SettingCreds:
						break;
					default:
						break;
				}
			}
		}
		protected override void DefineWorkflow()
		{
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Connecting, command: ASCommand.Proceed), processState: ASState.FetchingCredReq);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Connecting, command: ASCommand.Fail), processState: ASState.Reconnecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.FetchingCredReq, command: ASCommand.Proceed), processState: ASState.SettingCreds);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.FetchingCredReq, command: ASCommand.Fail), processState: ASState.Reconnecting);
		}

		private async Task FetchCredentialRequirements(AuthClient authClient)
		{
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			RequirementsResponse requirementsResponse = await authClient.GetCredentialRequirementsAsync(
				new RequirementsRequest { }, headers: null, deadline: null, cancellationTokenSource.Token);
			ValidCredentialRules.ValidUsernameRules = new CredentialRequirements.UsernameRequirements(requirementsResponse);
			ValidCredentialRules.ValidPasswordRules = new CredentialRequirements.PasswordRequirements(requirementsResponse);
		}



		protected override bool ServiceEnded()
		{
			return Channel.State == Grpc.Core.ConnectivityState.Shutdown
				|| Channel.State == Grpc.Core.ConnectivityState.TransientFailure;
		}

		public class CredentialRequirements
		{
			public enum Encoding { NA, Ascii, Unicode };
			public enum StringFormatType { NA, LettersOnly, LettersNumbers, Alphanumeric };
			public struct UsernameRequirements
			{
				public ulong MinLength;
				public ulong MaxLength;
				public Encoding Encoding;
				public StringFormatType StringFormatType;
				public UsernameRequirements(RequirementsResponse requirementsResponse)
				{
					MinLength = requirementsResponse.UsernameRequirements.MinLength;
					MaxLength = requirementsResponse.UsernameRequirements.MaxLength;
					switch(requirementsResponse.UsernameRequirements.Encoding)
					{
						case RequirementsResponse.Types.Encoding.Ascii:
							Encoding = Encoding.Ascii;
							break;
						case RequirementsResponse.Types.Encoding.Unicode:
							Encoding = Encoding.Unicode;
							break;
						default:
							Encoding = Encoding.NA;
							break;
					}
					switch (requirementsResponse.UsernameRequirements.FormatType)
					{
						case RequirementsResponse.Types.StringFormatType.LettersOnly:
							StringFormatType = StringFormatType.LettersOnly;
							break;
						case RequirementsResponse.Types.StringFormatType.LettersNumbers:
							StringFormatType = StringFormatType.LettersNumbers;
							break;
						case RequirementsResponse.Types.StringFormatType.Alphanumeric:
							StringFormatType = StringFormatType.Alphanumeric;
							break;
						default:
							StringFormatType = StringFormatType.NA;
							break;
					}
				}
			}
			public struct PasswordRequirements
			{
				ulong MaxLength;
				Encoding Encoding;
				ulong BitEntropy;
				public PasswordRequirements(RequirementsResponse requirementsResponse)
				{
					MaxLength = requirementsResponse.PasswordRequirements.MaxLength;
					switch(requirementsResponse.PasswordRequirements.Encoding)
					{
						case RequirementsResponse.Types.Encoding.Ascii:
							Encoding = Encoding.Ascii;
							break;
						case RequirementsResponse.Types.Encoding.Unicode:
							Encoding = Encoding.Unicode;
							break;
						default:
							Encoding = Encoding.NA;
							break;
					}
					BitEntropy = requirementsResponse.PasswordRequirements.BitsEntropy;
				}
			}
			public UsernameRequirements ValidUsernameRules { get; set; }
			public PasswordRequirements ValidPasswordRules { get; set; }
			public CredentialRequirements()
			{
				ValidUsernameRules = new UsernameRequirements();
				ValidPasswordRules = new PasswordRequirements();
			}
		}
	}
}
