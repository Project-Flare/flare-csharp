using Flare.V1;
using flare_csharp.Services;
using Grpc.Net.Client;
using static Flare.V1.Auth;
using Zxcvbn;
using System.Text.RegularExpressions;
using System.Text;

namespace flare_csharp
{
	public enum ASState { Initialized, Connecting, FetchingCredReq, SettingCreds, Registering, Reconnecting, Aborted }
	public enum ASCommand { Success, Fail, Abort }
	public class AuthorizationService : Service<ASState, ASCommand, GrpcChannel>
	{
		private AuthClient authClient;
		public string ServerUrl { get; set; }
		public CredentialRequirements UserCredentialRequirements { get; set; }
		private Credentials credentials;
		public string Username { get => credentials.Username; }
		public string Password { get => credentials.Password; }
		public AuthorizationService(string serverUrl, GrpcChannel channel) : base(new Process<ASState, ASCommand>(ASState.Initialized), channel)
		{
			ServerUrl = serverUrl;
			UserCredentialRequirements = new CredentialRequirements();
			credentials = new Credentials();
			authClient = new AuthClient(Channel);
		}
		public override void EndService()
		{
			throw new NotImplementedException();
		}
		public override void StartService()
		{
			try
			{
				Process.ProcessThread = new Thread(RunServiceAsync)
				{
					Name = "AUTH_SERVICE_THREAD",
					IsBackground = true
				};
				Process.MoveToNextState(ASCommand.Success);
				Process.ProcessThread.Start();
			}
			catch (Exception ex)
			{
				//[TODO]: learn string formatting and use proper logger for gods sake. https://learn.microsoft.com/en-us/dotnet/api/system.string.format?view=net-8.0
				Console.WriteLine(string.Format($"[ERROR]: Failed to initialize services. Inner exception:\n{ex}"));
				Process.MoveToNextState(ASCommand.Abort);
			}
		}
		protected override async void RunServiceAsync()
		{
			while (!ServiceEnded())
			{
				switch (State)
				{
					case ASState.Connecting:
						try
						{
							CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
							await Channel.ConnectAsync(cancellationTokenSource.Token);
							Process.MoveToNextState(ASCommand.Success);
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
							Process.MoveToNextState(ASCommand.Success);
						}
						catch
						{
							Process.MoveToNextState(command: ASCommand.Fail);
						}
						break;
					case ASState.SettingCreds:
						try
						{
							OnCredentialRequirementsReceived(new AuthServiceEventArgs(UserCredentialRequirements));
						}
						catch
						{
							Process.MoveToNextState(ASCommand.Abort);
						}
						if (Username != string.Empty && Password != string.Empty)
						{
							Process.MoveToNextState(ASCommand.Success);
						}
						break;
					default:
						break;
				}
			}
		}
		protected override void DefineWorkflow()
		{
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Initialized, command: ASCommand.Success), processState: ASState.Connecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Initialized, command: ASCommand.Abort), processState: ASState.Aborted);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Connecting, command: ASCommand.Success), processState: ASState.FetchingCredReq);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Connecting, command: ASCommand.Fail), processState: ASState.Reconnecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.FetchingCredReq, command: ASCommand.Success), processState: ASState.SettingCreds);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.FetchingCredReq, command: ASCommand.Fail), processState: ASState.Reconnecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.SettingCreds, command: ASCommand.Abort), processState: ASState.Aborted);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.SettingCreds, command: ASCommand.Fail), processState: ASState.FetchingCredReq);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.SettingCreds, command: ASCommand.Success), processState: ASState.Registering);
		}

		private async Task FetchCredentialRequirements(AuthClient authClient)
		{
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			RequirementsResponse requirementsResponse = await authClient.GetCredentialRequirementsAsync(
				new RequirementsRequest { }, headers: null, deadline: null, cancellationTokenSource.Token);
			UserCredentialRequirements.ValidUsernameRules = new CredentialRequirements.UsernameRequirements(requirementsResponse);
			UserCredentialRequirements.ValidPasswordRules = new CredentialRequirements.PasswordRequirements(requirementsResponse);
		}

		public enum PasswordState { NA, IsBlank, TooLong, NotAllAscii, TooWeak, NotAlphanumerical, VeryWeak, Decent, Good, Great, Excellent }

		public PasswordState EvaluatePassword(string? password)
		{
			if (State != ASState.SettingCreds)
				throw new InvalidOperationException($"{System.Reflection.MethodBase.GetCurrentMethod()!.Name} can be only used when the requirements of " +
					$"user credentials are received: {this.GetType().Name} is in {ASState.SettingCreds} state.");

			if (password is null || password == string.Empty)
				return PasswordState.IsBlank;

			if (password.Length > (int)UserCredentialRequirements.ValidPasswordRules.MaxLength)
				return PasswordState.TooLong;

			if (UserCredentialRequirements.ValidPasswordRules.Encoding == CredentialRequirements.Encoding.Ascii)
				if (!CredentialRequirements.ContainsOnlyAscii(password))
					return PasswordState.NotAllAscii;

			//[NOTE]: I think that the string object in C# is in Unicode by default
			const int MIN_ENTROPY = 35;
			int passwordEntropy = CredentialRequirements.GetBitEntropy(password);

			if (passwordEntropy < Math.Max((int)UserCredentialRequirements.ValidPasswordRules.BitEntropy, MIN_ENTROPY))
				return PasswordState.TooWeak;

			const int VERY_WEAK_ENTROPY = 50;
			if (passwordEntropy <= VERY_WEAK_ENTROPY)
				return PasswordState.VeryWeak;

			const int DECENT_ENTROPY = 65;
			if (passwordEntropy <= DECENT_ENTROPY)
				return PasswordState.Decent;

			const int GOOD_ENTROPY = 75;
			if (passwordEntropy <= GOOD_ENTROPY)
				return PasswordState.Good;

			const int GREAT_ENTROPY = 90;
			if (passwordEntropy <= GREAT_ENTROPY)
				return PasswordState.Great;

			return PasswordState.Excellent;
		}

		public enum UsernameState { NA, IsBlank, NotAllAscii, IsTaken, NonCompliant, Ok, NotAlphanumeric, TooShort, TooLong }
		public UsernameState EvaluateUsername(string? username)
		{
			if (State != ASState.SettingCreds)
				throw new InvalidOperationException($"{System.Reflection.MethodBase.GetCurrentMethod()!.Name} can be only used when the requirements of " +
					$"user credentials are received: {this.GetType().Name} is in {ASState.SettingCreds} state.");

			if (username is null || username == string.Empty)
				return UsernameState.IsBlank;

			if (username.Length < (int)UserCredentialRequirements.ValidUsernameRules.MinLength)
				return UsernameState.TooShort;

			if (username.Length > (int)UserCredentialRequirements.ValidUsernameRules.MaxLength)
				return UsernameState.TooLong;

			Regex regex = new Regex(@"^[\d\w]{1,32}$", RegexOptions.IgnoreCase);
			if (!regex.IsMatch(username) && UserCredentialRequirements.ValidUsernameRules.StringFormatType == CredentialRequirements.StringFormatType.Alphanumeric)
				return UsernameState.NotAlphanumeric;

			try
			{
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
				UsernameOpinionResponse opinionResponse = authClient.GetUsernameOpinion(new UsernameOpinionRequest { Username = username }, headers: null, deadline: null, cancellationTokenSource.Token);
				switch (opinionResponse.Opinion)
				{
					case UsernameOpinionResponse.Types.UsernameOpinion.Taken:
						return UsernameState.IsTaken;
					case UsernameOpinionResponse.Types.UsernameOpinion.Bad:
						return UsernameState.NonCompliant;
					case UsernameOpinionResponse.Types.UsernameOpinion.Ok:
						return UsernameState.Ok;
					default:
						return UsernameState.NA;
				}
			}
			catch (Exception)
			{
				Process.MoveToNextState(ASCommand.Fail);
			}
			return UsernameState.NA;
		}

		public bool UsernameValid(string? username) => (EvaluateUsername(username) == UsernameState.Ok) ? true : false;

		public bool PasswordValid(string? password) =>
			new PasswordState[] { PasswordState.IsBlank, PasswordState.NA, PasswordState.TooLong, PasswordState.NotAllAscii, PasswordState.TooWeak, PasswordState.NotAlphanumerical }
			.Contains(EvaluatePassword(password)) ? false : true;


		public bool TrySetUsername(string? username)
		{
			if (!UsernameValid(username))
				return false;
			credentials.Username = username!;
			return true;
		}

		public bool TrySetPassword(string? password)
		{
			if (!PasswordValid(password))
				return false;
			credentials.Password = password!;
			return true;
		}

		protected override bool ServiceEnded()
		{
			return Channel.State == Grpc.Core.ConnectivityState.Shutdown
				|| Channel.State == Grpc.Core.ConnectivityState.TransientFailure
				|| State == ASState.Aborted;
		}

		public delegate void CredentialRequirementsReceived(AuthServiceEventArgs eventArgs);
		public class AuthServiceEventArgs : EventArgs
		{
			public CredentialRequirements CredentialRequirements { get; private set; }
			public AuthServiceEventArgs(CredentialRequirements credentialRequirements)
			{
				CredentialRequirements = credentialRequirements;
			}
		}
		public event CredentialRequirementsReceived? ReceivedCredentialRequirements;
		public void OnCredentialRequirementsReceived(AuthServiceEventArgs eventArgs)
		{
			ReceivedCredentialRequirements?.Invoke(eventArgs);
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
					switch (requirementsResponse.UsernameRequirements.Encoding)
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
				public ulong MaxLength;
				public Encoding Encoding;
				public ulong BitEntropy;
				public PasswordRequirements(RequirementsResponse requirementsResponse)
				{
					MaxLength = requirementsResponse.PasswordRequirements.MaxLength;
					switch (requirementsResponse.PasswordRequirements.Encoding)
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
			public static int GetBitEntropy(string passwordToEvaluate)
			{
				return (int)Math.Log2(Core.EvaluatePassword(passwordToEvaluate).Guesses);
			}
			public static bool ContainsOnlyAscii(string str)
			{
				foreach (char c in str)
					if (!char.IsAscii(c))
						return false;
				return true;
			}
		}
	}
}
