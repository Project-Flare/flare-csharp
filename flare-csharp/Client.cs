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
    public static class Client
    {
        public enum ClientState
        {
            NotConnected,
            ConnectionFailed,
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
            try
            {
                MessageService.CancelOperationAfter(120);
                await MessageService.Connect();
                State = ClientState.Connected;
            }
            catch (ConnectionFailedException)
            {
                State = ClientState.ConnectionFailed;
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

            await MessageService.SendAllAddedMessagesAsync();

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

    }
}
