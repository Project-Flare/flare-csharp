using Flare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
    public class ClientRegisterFailedException : Exception
    {
        public ClientRegisterFailedException() : base() { }
        public ClientRegisterFailedException(string message) : base(message) { }
        public ClientRegisterFailedException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ClientLoginFailedException : Exception
    {
        public ClientLoginFailedException() : base() { }
        public ClientLoginFailedException(string message) : base(message) { }
        public ClientLoginFailedException(string message, Exception innerException) : base (message, innerException) { }
    }
    public static class Client
    {
        public enum ClientState
        {
            NotConnected,
            Connected,
            LoggedIn
        }

        public static string Username { get; set; } = string.Empty;
        public static string Password { get; set; } = string.Empty;
        public static string AuthToken { get; private set; } = string.Empty;
        public static UsernameValidity UsernameEvaluation { get => UserRegistration.ValidifyUsername(Username); }
        public static PasswordStrength PasswordStrength { get => UserRegistration.EvaluatePassword(Password); }
        public static ClientState State { get; private set; } = ClientState.NotConnected;

        public static async Task ConnectToServer()
        {
            if (State.Equals(ClientState.Connected))
                return;
            
            try
            {
                MessageService.CancelOperationAfter(120);
                await MessageService.Connect();

                State = (MessageService.Connected) ? ClientState.Connected : ClientState.NotConnected;
            }
            catch (ConnectionFailedException)
            {
                State = ClientState.NotConnected;
            }
        }

        public static async Task RegisterToServer()
        {
            if (!UsernameEvaluation.Equals(UsernameValidity.Correct))
                throw new ClientRegisterFailedException("Client username: " + Username + " is not valid");

            if (PasswordStrength <= PasswordStrength.Weak)
                throw new ClientRegisterFailedException("Client password: " + Password + " is not valid");
            
            MessageService.AddMessage(new ClientMessage
            {
                RegisterRequest = new RegisterRequest
                {
                    Username = Username,
                    Password = Password
                }
            });

            await MessageService.SendMessageAsync(MessageService.QueuedMessageCount);

            ServerMessage? response = MessageService.GetServerResponse();

            if (response is null)
                throw new ClientRegisterFailedException();

            if (!response.ServerMessageTypeCase.Equals(ServerMessage.ServerMessageTypeOneofCase.RegisterResponse))
                throw new ClientRegisterFailedException();

            if (response.RegisterResponse.DenyReason.Equals(RegisterResponse.Types.RegisterDenyReason.RdrUsernameTaken))
                throw new ClientRegisterFailedException("Username is taken");

            if (!response.RegisterResponse.HasAuthToken)
                throw new ClientRegisterFailedException();

            AuthToken = response.RegisterResponse.AuthToken;
            State = ClientState.LoggedIn;
        }

        public static async Task LoginToServer()
        {
            MessageService.AddMessage(new ClientMessage
            {
                LoginRequest = new LoginRequest
                {
                    Username = Username,
                    Password = Password
                }
            });

            await MessageService.SendMessageAsync(1);

            ServerMessage? response = MessageService.GetServerResponse();

            if (response is null)
                throw new ClientLoginFailedException("Failed to get server response");

            if (!response.ServerMessageTypeCase.Equals(ServerMessage.ServerMessageTypeOneofCase.LoginResponse))
                throw new ClientLoginFailedException();

            if (response.LoginResponse.HasDenyReason)
                throw new ClientLoginFailedException("User login failed: " + response.LoginResponse.DenyReason);

            if (!response.LoginResponse.HasAuthToken)
                throw new ClientLoginFailedException("Failed to get authorization token");

            AuthToken = response.LoginResponse.AuthToken;
            State = ClientState.LoggedIn;
        }

    }
}
