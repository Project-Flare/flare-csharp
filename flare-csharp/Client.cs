using Flare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Flare.AuthResponse.Types;

namespace flare_csharp
{

    public class ClientOperationFailedException : Exception
    {
        public ClientOperationFailedException() : base() { }
        public ClientOperationFailedException(string message) : base(message) { }
        public ClientOperationFailedException(string message, Exception innerException) : base(message, innerException) { }
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
        public static List<User> UserDiscoveryList { get; private set; } = new List<User>();

        /// <summary>
        /// Connects to server via Web Socket
        /// </summary>
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
            catch (MessageServiceOperationException)
            {
                State = ClientState.NotConnected;
            }
        }

        /// <summary>
        /// Register to server with the set credentials of the Client singleton
        /// </summary>
        /// <exception cref="ClientOperationFailedException">Failed to register to server</exception>
        public static async Task RegisterToServer()
        {
            if (!UsernameEvaluation.Equals(UsernameValidity.Correct))
                throw new ClientOperationFailedException("Client username: " + Username + " is not valid");

            if (PasswordStrength <= PasswordStrength.Weak)
                throw new ClientOperationFailedException("Client password: " + Password + " is not valid");
            
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
                throw new ClientOperationFailedException();

            if (!response.ServerMessageTypeCase.Equals(ServerMessage.ServerMessageTypeOneofCase.RegisterResponse))
                throw new ClientOperationFailedException();

            if (response.RegisterResponse.DenyReason.Equals(RegisterResponse.Types.RegisterDenyReason.RdrUsernameTaken))
                throw new ClientOperationFailedException("Username is taken");

            if (!response.RegisterResponse.HasAuthToken)
                throw new ClientOperationFailedException();

            AuthToken = response.RegisterResponse.AuthToken;
            State = ClientState.LoggedIn;
        }

        /// <summary>
        /// Log in to server with set Client singleton credentials
        /// </summary>
        /// <exception cref="ClientOperationFailedException">Client login operation failed</exception>
        public static async Task LoginToServer()
        {
            if (!State.Equals(ClientState.Connected))
                throw new ClientOperationFailedException("Client is not connected to the server");

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
                throw new ClientOperationFailedException("Failed to get server response");

            if (!response.ServerMessageTypeCase.Equals(ServerMessage.ServerMessageTypeOneofCase.LoginResponse))
                throw new ClientOperationFailedException();

            if (response.LoginResponse.HasDenyReason)
                throw new ClientOperationFailedException("User login failed: " + response.LoginResponse.DenyReason);

            if (!response.LoginResponse.HasAuthToken)
                throw new ClientOperationFailedException("Failed to get authorization token");

            AuthToken = response.LoginResponse.AuthToken;
            State = ClientState.LoggedIn;
        }

        /// <summary>
        /// Fills user discovery list with other users of the app
        /// </summary>
        /// <exception cref="ClientOperationFailedException">Failed to fill user discovery list with users</exception>
        public static async Task FillUserDiscovery()
        {
            if (State.Equals(ClientState.NotConnected))
                throw new ClientOperationFailedException("Client is not connected to the server");

            if (!State.Equals(ClientState.LoggedIn))
                throw new ClientOperationFailedException("Client is not logged in to the server");

            MessageService.AddMessage(new ClientMessage
            {
                AuthRequest = new AuthRequest
                {
                    SessionToken = AuthToken
                }
            });

            MessageService.AddMessage(new ClientMessage
            {
                UserListRequest = new UserListRequest()
            });

            await MessageService.SendMessageAsync(2);

            ServerMessage? response = MessageService.GetServerResponse();

            if (response is null
                || !response.ServerMessageTypeCase.Equals(ServerMessage.ServerMessageTypeOneofCase.AuthResponse)
                || !response.AuthResponse.Result.Equals(AuthResult.ArOk))
            {
                throw new ClientOperationFailedException("Failed to establish secure session");
            }

            AuthToken = (response.AuthResponse.HasNewAuthToken) ? response.AuthResponse.NewAuthToken : AuthToken;

            response = MessageService.GetServerResponse();

            if (response is null)
                throw new ClientOperationFailedException();

            if (!response.ServerMessageTypeCase.Equals(ServerMessage.ServerMessageTypeOneofCase.UserListResponse))
                throw new ClientOperationFailedException();
            
            foreach (User user in response.UserListResponse.Users)
                UserDiscoveryList.Add(user);
        }
    }
}
