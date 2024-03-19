using Flare;
using Google.Protobuf;
using System.Net.Security;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Zxcvbn;

namespace Flare
{
    public class Client
    {
        public enum RegistrationResponse
        {
            ClientIsNotConnectedToTheServer,
            SubmittedUserRegistrationNotValid,
            FailedToFormRegisterRequest,
            FailedToGetServerResponse,
            ServerRegisterResponseInvalid,
            ServerDenyReasonUsernameTaken,
            ServerDenyReasonUsernameInvalidSyntax,
            ServerDenyReasonUsernameInvalidLength,
            ServerDenyReasonPasswordIsBlank,
            ServerDenyReasonPasswordIsWeak,
            ServerDenyReasonUnknown,
            ServerDenyInterpretationUnknown,
            NewUserRegistrationSucceeded,
            NewUserRegistrationFailed
        }

        public enum LoginResponse
        {
            UserCredentialsNotSet,
            FailedToGetServerLoginResponse,
            FailedToGetServerResponse,
            ServerLoginResponseInvalid,
            ServerDenyReasonInvalidUsername,
            ServerDenyReasonInvalidPassword,
            ServerDenyReasonUnknown,
            ServerDenyInterpretationUnknown,
            UserLoginSucceeded,
            UserLoginFailed
        }

        public enum AuthenticationResponse
        {
            UserCredentialsAuthTokenNotFilled,
            FailedToGetServerAuthResponse,
            ServerAuthResponseInvalid,
            NewSessionTokenNotReceived,
            ServerResponseCurrentUserAuthTokenIsOk,
            UserAuthTokenIsRenewed,
            ServerDenyUserAuthTokenIsInvalid,
            ServerDenyUserAuthTokenExpired,
            ServerDenyUnknown,
            ServerNewTokenNotReceived,
            NewTokenAuthFailed
        }

        public enum UserListResponse
        {
            FailedToSendUserListRequest,
            FailedToReceiveUserListResponse,
            UserListIsFilledSuccessfully
        }

        public sealed class Credentials
        {
            private string _username;
            private string _password;
            private string _authToken;

            public string Username { get => _username; set => _username = value; }
            public string Password { get => _password; set => _password = value; }
            public string AuthToken { get => _authToken; set => _authToken = value; }
            public bool Filled
            {
                get
                {
                    return !(string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password) || string.IsNullOrEmpty(_authToken));
                }
            }

            public bool CredentialsFilled
            {
                get
                {
                    return !(string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password));
                }
            }

            public Credentials()
            {
                _username = string.Empty;
                _password = string.Empty;
                _authToken = string.Empty;
            }
            public Credentials(string username, string password, string authToken)
            {
                _username = username;
                _password = password;
                _authToken = authToken;
            }
        }

        // Server URL (DO NOT TOUCH)
        const string _serverUrl = "wss://ws.project-flare.net/";

        // Client contacts the server only through this channel
        private ClientWebSocket _webSocket;

        // Required to specify a maximum time period for contact tasks
        private CancellationTokenSource _ctSource;

        // Whether the client has successfully connected to the server
        private bool _connected;

        // Storing user credentials (loaded or new registration)
        private Credentials _usrCredentials;

        // Message buffer
        private const int KILOBYTE = 1024;
        private byte[] _buffer = new byte[KILOBYTE];

        // List of all users of the DC
        public List<string> UserList { get; private set; }

        public bool IsConnected { get => _connected; }
        public Credentials UserCredentials
        {
            get => _usrCredentials;
            set
            {
                if (value.CredentialsFilled)
                    _usrCredentials = value;
            }
        }

        public Client()
        {
            _webSocket = new ClientWebSocket();
            // [FOR EDIT]
            _ctSource = new CancellationTokenSource();
            _ctSource.CancelAfter(TimeSpan.FromSeconds(60));
            _connected = false;
            // TODO - import if the user is already registered
            _usrCredentials = new Credentials();
            UserList = new List<string>();
        }

        public async Task ConnectToServer()
        {
            _webSocket.Options.RemoteCertificateValidationCallback =
            (
                object sender,
                X509Certificate? certificate,
                X509Chain? chain,
                SslPolicyErrors sslPolicyErrors
            ) =>
            {
                if ((sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0)
                    return false;

                if (certificate is null)
                    return false;

                const string pub_key_pin =
                    "04447327fe093b0450bbae0346cf85" +
                    "fb60491ea04adc1c7d10a49c3397bf" +
                    "1a2539e7eea6a6b4109a5c62b2df55" +
                    "003c998b4afb1f103b883f1f649b3b" +
                    "6530ce8dd7";

                return certificate.GetPublicKeyString().ToLower().Equals(pub_key_pin);
            };

            await _webSocket.ConnectAsync(new Uri(_serverUrl), _ctSource.Token);

            ServerMessage serverMessage = await ReceiveServerMessageAsync();

            _connected = serverMessage.ServerMessageTypeCase.Equals(Flare.ServerMessage.ServerMessageTypeOneofCase.Hello);
        }

        public async Task<RegistrationResponse> RegisterToServer(UserRegistration registration)
        {
            if (!_connected)
                return RegistrationResponse.ClientIsNotConnectedToTheServer;

            if (!registration.IsValid)
                return RegistrationResponse.SubmittedUserRegistrationNotValid;

            var registerRequest = registration.FormRegistrationRequest();

            if (registerRequest is null)
                return RegistrationResponse.FailedToFormRegisterRequest;

            // Send request to server
            var clientMessage = new Flare.ClientMessage();
            clientMessage.RegisterRequest = registerRequest;
            await SendClientMessageAsync(clientMessage, true);

            // Get server response
            Flare.ServerMessage serverMessage = await ReceiveServerMessageAsync();

            if (serverMessage.ServerMessageTypeCase != Flare.ServerMessage.ServerMessageTypeOneofCase.RegisterResponse)
                return RegistrationResponse.ServerRegisterResponseInvalid;

            Flare.RegisterResponse registerResponse = serverMessage.RegisterResponse;

            if (registerResponse.HasDenyReason)
            {
                switch (registerResponse.DenyReason)
                {
                    case Flare.RegisterResponse.Types.RegisterDenyReason.RdrUsernameTaken:
                        return RegistrationResponse.ServerDenyReasonUsernameTaken;
                    case Flare.RegisterResponse.Types.RegisterDenyReason.RdrUsernameInvalidSymbols:
                        return RegistrationResponse.ServerDenyReasonUsernameInvalidSyntax;
                    case Flare.RegisterResponse.Types.RegisterDenyReason.RdrUsernameInvalidLength:
                        return RegistrationResponse.ServerDenyReasonUsernameInvalidLength;
                    case Flare.RegisterResponse.Types.RegisterDenyReason.RdrPasswordBlank:
                        return RegistrationResponse.ServerDenyReasonPasswordIsBlank;
                    case Flare.RegisterResponse.Types.RegisterDenyReason.RdrPasswordWeak:
                        return RegistrationResponse.ServerDenyReasonPasswordIsWeak;
                    case Flare.RegisterResponse.Types.RegisterDenyReason.RdrUnknown:
                        return RegistrationResponse.ServerDenyReasonUnknown;
                    default:
                        return RegistrationResponse.ServerDenyInterpretationUnknown;
                }
            }

            if (registerResponse.HasAuthToken)
            {
                _usrCredentials.Username = registration.Username;
                _usrCredentials.Password = registration.Password;
                _usrCredentials.AuthToken = registerResponse.AuthToken;
                return RegistrationResponse.NewUserRegistrationSucceeded;
            }

            return RegistrationResponse.NewUserRegistrationFailed;
        }

        public async Task<LoginResponse> LoginToServer()
        {
            if (!_usrCredentials.CredentialsFilled)
                return LoginResponse.UserCredentialsNotSet;

            var loginRequest = new Flare.LoginRequest();
            loginRequest.Username = _usrCredentials.Username;
            loginRequest.Password = _usrCredentials.Password;

            var clientMessage = new Flare.ClientMessage();
            clientMessage.LoginRequest = loginRequest;
            await SendClientMessageAsync(clientMessage, true);

            ServerMessage serverMessage = await ReceiveServerMessageAsync();

            if (serverMessage.ServerMessageTypeCase != Flare.ServerMessage.ServerMessageTypeOneofCase.LoginResponse)
                return LoginResponse.ServerLoginResponseInvalid;

            Flare.LoginResponse loginResponse = serverMessage.LoginResponse;

            if (loginResponse.HasDenyReason)
            {
                switch (loginResponse.DenyReason)
                {
                    case Flare.LoginResponse.Types.LoginDenyReason.LdrUsernameInvalid:
                        return LoginResponse.ServerDenyReasonInvalidUsername;
                    case Flare.LoginResponse.Types.LoginDenyReason.LdrPasswordInvalid:
                        return LoginResponse.ServerDenyReasonInvalidPassword;
                    case Flare.LoginResponse.Types.LoginDenyReason.LdrUnknown:
                        return LoginResponse.ServerDenyReasonUnknown;
                    default:
                        return LoginResponse.ServerDenyInterpretationUnknown;
                }
            }

            if (loginResponse.HasAuthToken)
            {
                _usrCredentials.AuthToken = loginResponse.AuthToken;
                return LoginResponse.UserLoginSucceeded;
            }

            return LoginResponse.UserLoginFailed;
        }

        public async Task<AuthenticationResponse> TryAuthNewSession()
        {
            if (!_usrCredentials.Filled)
                return AuthenticationResponse.UserCredentialsAuthTokenNotFilled;

            var authRequest = new Flare.AuthRequest();
            authRequest.SessionToken = _usrCredentials.AuthToken;

            var clientMessage = new Flare.ClientMessage();
            clientMessage.AuthRequest = authRequest;

            await SendClientMessageAsync(clientMessage, true);

            Flare.ServerMessage serverMessage = await ReceiveServerMessageAsync();

            if (serverMessage.ServerMessageTypeCase != Flare.ServerMessage.ServerMessageTypeOneofCase.AuthResponse)
                return AuthenticationResponse.ServerAuthResponseInvalid;

            Flare.AuthResponse? authResponse = serverMessage.AuthResponse;

            if (authResponse is null)
            {
                return AuthenticationResponse.NewTokenAuthFailed;
            }

            if (!authResponse.HasNewAuthToken)
            {
                switch (authResponse.Result)
                {
                    case Flare.AuthResponse.Types.AuthResult.ArOk:
                        if (authResponse.HasNewAuthToken)
                        {
                            _usrCredentials.AuthToken = authResponse.NewAuthToken;
                            return AuthenticationResponse.UserAuthTokenIsRenewed;
                        }
                        return AuthenticationResponse.ServerResponseCurrentUserAuthTokenIsOk;
                    case Flare.AuthResponse.Types.AuthResult.ArSessionInvalid:
                        return AuthenticationResponse.ServerDenyUserAuthTokenIsInvalid;
                    case Flare.AuthResponse.Types.AuthResult.ArSessionExpired:
                        return AuthenticationResponse.ServerDenyUserAuthTokenExpired;
                    case Flare.AuthResponse.Types.AuthResult.ArUnknown:
                        return AuthenticationResponse.ServerDenyUnknown;
                    default:
                        return AuthenticationResponse.ServerNewTokenNotReceived;
                }
            }

            return AuthenticationResponse.NewTokenAuthFailed;
        }

        public async Task<UserListResponse> FillUserList()
        {
            var isSent = await SendClientMessageAsync(new Flare.ClientMessage
            {
                UserListRequest = new Flare.UserListRequest()
            }, true);

            if (!isSent)
                return UserListResponse.FailedToSendUserListRequest;

            var serverMessage = await ReceiveServerMessageAsync();

            if (serverMessage is null)
                return UserListResponse.FailedToReceiveUserListResponse;

            var userList = serverMessage.UserListResponse;

            foreach (var user in userList.Users)
                UserList.Add(user.Username);

            return UserListResponse.UserListIsFilledSuccessfully;
        }

        private async Task<bool> SendClientMessageAsync(Flare.ClientMessage message, bool endOfMessage)
        {
            // Don't send if the web socket is not open
            if (!_webSocket.State.Equals(WebSocketState.Open))
                return false;

            // Convert given message to byte array in protobuf encoding
            _buffer = message.ToByteArray();

            // Send protobuf byte array through the web socket
            await _webSocket.SendAsync(_buffer, WebSocketMessageType.Binary, endOfMessage, _ctSource.Token);

            // Message sent successfully
            return true;
        }

        private async Task<ServerMessage> ReceiveServerMessageAsync()
        {
            _buffer = new byte[KILOBYTE];
            int offset = 0;
            int free = _buffer.Length;

            while (true)
            {
                _ctSource.TryReset();
                var response = await _webSocket.ReceiveAsync(new ArraySegment<byte>(_buffer, offset, free), _ctSource.Token);

                if (response.EndOfMessage)
                {
                    offset += response.Count;
                    break;
                }

                // Enlarge if the received message is bigger than the buffer
                if (free.Equals(response.Count))
                {
                    int newSize = _buffer.Length * 2;

                    if (newSize > 2_000_000)
                        break;

                    byte[] newBuffer = new byte[newSize];
                    Array.Copy(_buffer, 0, newBuffer, 0, _buffer.Length);

                    free = newBuffer.Length - _buffer.Length;
                    offset = _buffer.Length;
                    _buffer = newBuffer;
                }

            }

            return ServerMessage.Parser.ParseFrom(_buffer, 0, offset);
        }
    }
}
