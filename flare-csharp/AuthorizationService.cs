using Flare.V1;
using flare_csharp.Services;
using Grpc.Net.Client;
using static Flare.V1.Auth;
using Zxcvbn;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Authentication;
using System.Security;
using System.Threading;
using Isopoh.Cryptography.Argon2;
using Grpc.Core;

namespace flare_csharp
{
	public enum ASState { Initialized, Connecting, ReceivingCredentialRequirements, SettingCreds, Registering, LoggingIn, Reconnecting, Aborted, EndedSuccessfully, Ended }
	public enum ASCommand { Success, Fail, Abort, UserHasAccount, Reconnect, Retry, End }
	public class AuthorizationService : Service<ASState, ASCommand, GrpcChannel>
	{
		private AuthClient authClient;
		public string ServerUrl { get; set; }
		public CredentialRequirements UserCredentialRequirements { get; set; }
		private Credentials credentials;
		public IdentityStore? identityStore;
		public string Username { get => credentials.Username; }
		public string Password { get => credentials.Password; }
		public AuthorizationService(string serverUrl, GrpcChannel channel, Credentials? credentials, IdentityStore identityStore) : base(new Process<ASState, ASCommand>(ASState.Initialized), channel)
		{
			ServerUrl = serverUrl;
			UserCredentialRequirements = new CredentialRequirements();
			this.credentials = (credentials is null) ? new Credentials() : credentials;
			this.identityStore = identityStore;
			authClient = new AuthClient(Channel);
		}
		public override void EndService()
		{
			if (State == ASState.EndedSuccessfully || State == ASState.Ended)
				return;
			else
				Process.MoveToNextState(ASCommand.End);
		}
		public override void StartService()
		{
			if (State == ASState.Aborted)
			{
				Process.MoveToNextState(ASCommand.Retry);
			}
			else if (State == ASState.Initialized)
			{
				Process.MoveToNextState(ASCommand.Success);
			}
			else
			{
				Process.GoTo(ASState.Connecting);
			}
		}
		public Thread? GiveServiceThread() => Process.ProcessThread;
		public void LoadUserCredentials(Credentials credentials)
		{
			this.credentials = credentials;
		}
		public Credentials GetAcquiredCredentials() => this.credentials;
		public IdentityStore GetAcquiredIdentityStore() => this.identityStore;
		public override async void RunServiceAsync()
		{
			while (!ServiceEnded())
			{
				switch (State)
				{
					case ASState.Connecting:
						try
						{
							CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
							await Channel.ConnectAsync(cancellationTokenSource.Token);
							if (credentials.Username == string.Empty && credentials.Password == string.Empty)
							{
								Process.MoveToNextState(ASCommand.Success);
							}
							else if (!string.IsNullOrEmpty(credentials.AuthToken))
							{
								bool tokenIsOk = await CheckTokenHealth();
								if (tokenIsOk)
								{
									On_LoggedInToServer(new LoggedInEventArgs(success: true, failureReason: null));
								}
								On_LoggedInToServer(new LoggedInEventArgs(success: false, failureReason: LoggedInEventArgs.FailureReason.AuthTokenExpired));
								Process.MoveToNextState(ASCommand.End);
							}
							else if (!string.IsNullOrEmpty(credentials.Password)) // todo: infinite login loop
							{
								Process.MoveToNextState(ASCommand.UserHasAccount);
							}
						}
						catch (KeyNotFoundException)
						{
							Process.MoveToNextState(ASCommand.Abort);
						}
						catch (Exception ex)
						{
							Process.MoveToNextState(ASCommand.Fail);
						}
						break;
					case ASState.ReceivingCredentialRequirements:
						try
						{
							await ReceiveCredentialRequirements();
							On_CredentialRequirementsReceived(new ReceivedRequirementsEventArgs(UserCredentialRequirements));
							Process.MoveToNextState(ASCommand.Success);
						}
						catch
						{
							Process.MoveToNextState(command: ASCommand.Fail);
						}
						break;
					case ASState.SettingCreds:
						if (Username != string.Empty && Password != string.Empty)
						{
							Process.MoveToNextState(ASCommand.Success);
						}
						break;
					case ASState.Registering:
						try
						{
							RegisterResponse registerResponse = await RegisterToServerAsync();
							RegistrationToServerEvent += SetAuthToken;
							On_RegistrationToServer(new RegistrationToServerEventArgs(registerResponse));
							RegistrationToServerEvent -= SetAuthToken;
						}
						catch (Exception ex){ }
						break;
					case ASState.LoggingIn:
						try
						{
							LoginToServer();
							Process.MoveToNextState(ASCommand.Success);
						}
						catch (AuthenticationException)
						{
							Process.MoveToNextState(ASCommand.Abort);
						}
						catch (SecurityException)
						{
							Process.MoveToNextState(ASCommand.Abort);
						}
						catch (AbandonedMutexException)
						{
							Process.MoveToNextState(ASCommand.Abort);
						}
						catch (Exception ex)
						{
							Process.MoveToNextState(ASCommand.Reconnect);
						}
						break;
					case ASState.Reconnecting:
						// TODO: resolve infinite loop problem when reconnecting
						try
						{
							CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
							await Channel.ConnectAsync(cancellationTokenSource.Token);
							Process.MoveToNextState(ASCommand.Success);
						}
						catch (Exception)
						{
							On_LoggedInToServer(new LoggedInEventArgs(success: false, failureReason: LoggedInEventArgs.FailureReason.ConnectionFailure));
							On_RegistrationToServer(new RegistrationToServerEventArgs());
							Process.MoveToNextState(ASCommand.End);
						}
						break;
					default:
						break;
				}
			}
		}
		public delegate void LoggedInToServerDelegate(LoggedInEventArgs loggedInEventArgs);
		public event LoggedInToServerDelegate? LoggedInToServerEvent;
		public class LoggedInEventArgs : EventArgs
		{
			public bool LoggedInSuccessfully { get; private set; }
			public enum FailureReason { None, UntrustworthyServer,  PasswordInvalid, UsernameInvalid, ServerError, UsernameNotExist, UserDoesNotExits, AuthTokenExpired, ConnectionFailure, Unknown }
			public FailureReason LoginFailureReason { get; private set; }
			public LoggedInEventArgs(bool success, FailureReason? failureReason)
			{
				LoggedInSuccessfully = success;
				LoginFailureReason = (FailureReason)((failureReason is null) ? FailureReason.None : failureReason!);
			}
		}
		private void On_LoggedInToServer(LoggedInEventArgs loggedInEventArgs)
		{
			LoggedInToServerEvent?.Invoke(loggedInEventArgs);
		}
		private void LoginToServer()
		{
			GetClientHashParamsRequest getClientHashParamsRequest = new GetClientHashParamsRequest
			{
				Username = credentials.Username
			};
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			GetClientHashParamsResponse getClientHashParamsResponse = authClient.GetClientHashParams(getClientHashParamsRequest, headers: null, deadline: null, cancellationTokenSource.Token);
			if (getClientHashParamsResponse.HasError)
			{
				LoggedInEventArgs.FailureReason failureReason = LoggedInEventArgs.FailureReason.Unknown;
				switch (getClientHashParamsResponse.Error)
				{
					case GetClientHashParamsResponse.Types.GetClientHashParamsError.UserNotFound:
						failureReason = LoggedInEventArgs.FailureReason.UsernameNotExist;
						break;
					case GetClientHashParamsResponse.Types.GetClientHashParamsError.Missing:
						failureReason = LoggedInEventArgs.FailureReason.UserDoesNotExits;
						break;
					default:
						failureReason = LoggedInEventArgs.FailureReason.Unknown;
						break;
				}
				On_LoggedInToServer(new LoggedInEventArgs(success: false, failureReason));
				throw new AuthenticationException($"Failed to login to server with {credentials.Username} because {getClientHashParamsResponse.GetClientHashParamsResultCase}");
			}
			HashParams hashParams = getClientHashParamsResponse.Params;
			if (hashParams.MemoryCost < Credentials.MIN_MEMORY_COST_BYTES || hashParams.TimeCost < Credentials.MIN_TIME_COST || CredentialRequirements.GetBitEntropy(hashParams.Salt) < Credentials.MIN_SALT_ENTROPY) // the server is untrustworthy!
			{
				On_LoggedInToServer(new LoggedInEventArgs(success: false, LoggedInEventArgs.FailureReason.UntrustworthyServer));
				throw new SecurityException($"Server sent less than minimum requirements to {hashParams.GetType().Name}");
			}
			LoginRequest loginRequest = new LoginRequest();
			if (identityStore.Identity is null)
			{
				identityStore.Identity = Crypto.GenerateECDHKeyPair();
			}

            credentials.MemoryCostBytes = (int)hashParams.MemoryCost;
            credentials.TimeCost = (int)hashParams.TimeCost;
            credentials.Salt = hashParams.Salt;

			loginRequest.IdentityPublicKey = Crypto.GetDerEncodedPublicKey(
				identityStore.Identity.Public
			);
            loginRequest.Username = credentials.Username;
            Crypto.HashPasswordArgon2i(credentials);
            loginRequest.PasswordHash = credentials.PasswordHash;
			
			cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			LoginResponse loginResponse = authClient.Login(request: loginRequest, headers: null, deadline: null, cancellationTokenSource.Token);
			if (loginResponse.HasFailure)
			{
				LoggedInEventArgs.FailureReason failureReason = LoggedInEventArgs.FailureReason.Unknown;
				switch(loginResponse.Failure)
				{
					case LoginResponse.Types.LoginFailure.UsernameInvalid:
						failureReason = LoggedInEventArgs.FailureReason.UsernameInvalid;
						break;
					case LoginResponse.Types.LoginFailure.PasswordInvalid:
						failureReason = LoggedInEventArgs.FailureReason.PasswordInvalid;
						break;
					default:
						failureReason = LoggedInEventArgs.FailureReason.Unknown;
						break;
				}
				On_LoggedInToServer(new LoggedInEventArgs(success: false, failureReason));
				throw new AbandonedMutexException($"Failed to login to server with {credentials.Username} because {loginResponse.Failure}");
			}
			credentials.AuthToken = loginResponse.Token;
			On_LoggedInToServer(new LoggedInEventArgs(success: true, failureReason: null));
		}
		protected override void DefineWorkflow()
		{
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Initialized, command: ASCommand.Success), processState: ASState.Connecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Initialized, command: ASCommand.Abort), processState: ASState.Aborted);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Initialized, command: ASCommand.End), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Connecting, command: ASCommand.UserHasAccount), processState: ASState.LoggingIn);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Connecting, command: ASCommand.End), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Connecting, command: ASCommand.Success), processState: ASState.ReceivingCredentialRequirements);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Connecting, command: ASCommand.Fail), processState: ASState.Reconnecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.ReceivingCredentialRequirements, command: ASCommand.Success), processState: ASState.SettingCreds);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.ReceivingCredentialRequirements, command: ASCommand.End), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.ReceivingCredentialRequirements, command: ASCommand.Fail), processState: ASState.Reconnecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.SettingCreds, command: ASCommand.Abort), processState: ASState.Aborted);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.SettingCreds, command: ASCommand.End), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.SettingCreds, command: ASCommand.Fail), processState: ASState.ReceivingCredentialRequirements);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.SettingCreds, command: ASCommand.Success), processState: ASState.Registering);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Registering, command: ASCommand.Abort), processState: ASState.Reconnecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Registering, command: ASCommand.End), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Registering, command: ASCommand.Success), processState: ASState.EndedSuccessfully);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.LoggingIn, command: ASCommand.Abort), processState: ASState.Aborted);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.LoggingIn, command: ASCommand.Fail), processState: ASState.ReceivingCredentialRequirements);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.LoggingIn, command: ASCommand.Reconnect), processState: ASState.Reconnecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.LoggingIn, command: ASCommand.Success), processState: ASState.EndedSuccessfully);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.LoggingIn, command: ASCommand.End), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Reconnecting, command: ASCommand.Success), processState: ASState.Connecting);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Reconnecting, command: ASCommand.Abort), processState: ASState.Aborted);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Reconnecting, command: ASCommand.End), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Aborted, command: ASCommand.Retry), processState: ASState.Initialized);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Aborted, command: ASCommand.End), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Ended, command: ASCommand.End), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Ended, command: ASCommand.Success), processState: ASState.Ended);
			Process.AddStateTransition(transition: new Process<ASState, ASCommand>.StateTransition(currentState: ASState.Ended, command: ASCommand.Retry), processState: ASState.Initialized);
		}
		/// <summary>
		/// Sets <see cref="credentials"/> <see cref="Credentials.AuthToken"/> property if the registration was successful.
		/// </summary>
		/// <param name="eventArgs">Registration to server event arguments.</param>
		private void SetAuthToken(RegistrationToServerEventArgs eventArgs)
		{
			if (eventArgs.RegistrationForm.UserRegisteredSuccessfully)
				credentials.AuthToken = eventArgs.RegistrationForm.AuthToken;
		}
		private async Task<RegisterResponse> RegisterToServerAsync()
		{
			credentials.MemoryCostBytes = Credentials.DEFAULT_MEMORY_COST_BYTES;
			credentials.TimeCost = 3;
			Crypto.HashPasswordArgon2i(credentials);
			HashParams hashParams = new HashParams
			{
				MemoryCost = (ulong)credentials.MemoryCostBytes,
				TimeCost = (ulong)credentials.TimeCost,
				Salt = credentials.PseudoRandomConstant + credentials.SecureRandom
			};
			if (identityStore is null)
				identityStore = new();
			identityStore.Identity = Crypto.GenerateECDHKeyPair();
			RegisterRequest registerRequest = new RegisterRequest
			{
				Username = this.Username,
				HashParams = hashParams,
				IdentityPublicKey = Crypto.GetDerEncodedPublicKey(identityStore.Identity.Public),
				PasswordHash = credentials.PasswordHash
			};
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			RegisterResponse registerResponse = await authClient.RegisterAsync(registerRequest, headers: null, deadline: null, cancellationTokenSource.Token);
			return registerResponse;
		}
		private async Task<bool> CheckTokenHealth()
		{
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			Metadata headers = new Metadata { { "flare-auth", credentials.AuthToken } };
			TokenHealthRequest tokenHealthRequest = new TokenHealthRequest();
			TokenHealthResponse tokenHealthResponse = await authClient.GetTokenHealthAsync(tokenHealthRequest, headers, deadline: null, cancellationTokenSource.Token);
			if (tokenHealthResponse.Health == TokenHealthResponse.Types.TokenHealth.Ok)
				return true;
			return false;
		}

		public delegate void RegistrationToServerDelegate(RegistrationToServerEventArgs eventArgs);
		public event RegistrationToServerDelegate? RegistrationToServerEvent;
		public class RegistrationToServerEventArgs : EventArgs
		{
			public RegistrationResponse RegistrationForm { get; private set; }
			public RegistrationToServerEventArgs(RegisterResponse registerResponse)
			{
				RegistrationForm = new RegistrationResponse(registerResponse);
			}
			public RegistrationToServerEventArgs()
			{
				RegistrationForm = new();
			}
			public class RegistrationResponse
			{
				public enum FailureReason { None, UsernameIsTaken, BadUsername, BadPassword, ConnectionFailure, Unknown }
				public FailureReason RegistrationFailureReason 
				{ 
					get
					{
						if (!registerResponse.HasFailure)
							return FailureReason.None;

						switch (registerResponse.Failure)
						{
							case RegisterResponse.Types.RegisterFailure.UsernameTaken:
								return FailureReason.UsernameIsTaken;
							case RegisterResponse.Types.RegisterFailure.UsernameBad:
								return FailureReason.BadUsername;
							case RegisterResponse.Types.RegisterFailure.PasswordBad:
								return FailureReason.BadPassword;
							default:
								return FailureReason.Unknown;
						}
					} 
				}
				public bool UserRegisteredSuccessfully { get => registerResponse.HasToken; }
				public string AuthToken { get => registerResponse.HasToken ? registerResponse.Token : string.Empty; }
				private RegisterResponse registerResponse { get; set; }
				public RegistrationResponse(RegisterResponse registerResponse)
				{
					this.registerResponse = registerResponse;
				}
				public RegistrationResponse()
				{
					registerResponse = new();
				}
			}
		}
		public void On_RegistrationToServer(RegistrationToServerEventArgs eventArgs)
		{
			RegistrationToServerEvent?.Invoke(eventArgs);
		}
		private async Task ReceiveCredentialRequirements()
		{
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			RequirementsResponse requirementsResponse = await authClient.GetCredentialRequirementsAsync(
				new RequirementsRequest { }, headers: null, deadline: null, cancellationTokenSource.Token);
			UserCredentialRequirements.ValidUsernameRules = new CredentialRequirements.UsernameRequirements(requirementsResponse);
			UserCredentialRequirements.ValidPasswordRules = new CredentialRequirements.PasswordRequirements(requirementsResponse);
		}

		public delegate void CredentialRequirementsReceivedDelegate(ReceivedRequirementsEventArgs eventArgs);
		public class ReceivedRequirementsEventArgs : EventArgs
		{
			public CredentialRequirements CredentialRequirements { get; private set; }
			public ReceivedRequirementsEventArgs(CredentialRequirements credentialRequirements)
			{
				CredentialRequirements = credentialRequirements;
			}
		}
		public event CredentialRequirementsReceivedDelegate? ReceivedCredentialRequirements;
		public void On_CredentialRequirementsReceived(ReceivedRequirementsEventArgs eventArgs)
		{
			ReceivedCredentialRequirements?.Invoke(eventArgs);
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
			if (!regex.IsMatch(username))
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
				|| State == ASState.Aborted
				|| State == ASState.EndedSuccessfully
				|| State == ASState.Ended;
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
