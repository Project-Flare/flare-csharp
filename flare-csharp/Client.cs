using Flare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Flare.AuthResponse.Types;

namespace flare_csharp
{
    /// <summary>
    /// Everything that goes wrong in <see cref="Client"/> singleton or not according to plan, this exception is thrown.
    /// </summary>
    public class ClientOperationFailedException : Exception
    {
        public ClientOperationFailedException() : base() { }
        public ClientOperationFailedException(string message) : base(message) { }
        public ClientOperationFailedException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// This class manages communication between server and the client app.
    /// </summary>
    public static class Client
    {
        /// <summary>
        /// Describes the current state of the client.
        /// </summary>
        public enum ClientState
        {
            NotConnected,
            Connected,
            LoggedIn
        }

        /// <value>
        /// Used as credentials when logging to server. Username must be set correctly. Use <c>UserRegistration</c> class to check username syntax correctness.
        /// Default value is empty string.
        /// <example>
        /// Example of correct setting of username property:
        /// <code>
        /// Client.Username = (UserRegistration.ValidifyUsername(string newUsername) == UsernameValidity.Correct) ? username : Client.Username;
        /// </code>
        /// </example>
        /// </value>
        public static string Username { get; set; } = string.Empty;

        /// <value>
        /// Password is used when logging in or registering to server. It's evaluation must be good or excellent to be passed as valid credentials to server. Default value is empty string.
        /// <example>
        /// Example:
        /// <code>
        /// Client.Password = (UserRegistration.EvaluatePassword(string newPassword) >= PasswordStrength.Good) newPassword : Client.Password;
        /// </code>
        /// </example>
        /// </value>
        public static string Password { get; set; } = string.Empty;

        /// <value>
        /// Each session is authenticated with a server-issued key, which may change if the server issues a new session key. The session authentication token is obtained at login or at registration as a new user.
        /// </value>
        public static string AuthToken { get; private set; } = string.Empty;

        /// <value>
        /// Use this to check <c>Username</c> property syntax evaluation.
        /// </value>
        public static UsernameValidity UsernameEvaluation { get => UserRegistration.ValidifyUsername(Username); }

        /// <value>
        /// Use this to check <c>Password</c> property strength. An acceptable password is a <c>PasswordStrength.Good</c> or <c>PasswordStrength.Excellent</c> rating.
        /// </value>
        public static PasswordStrength PasswordStrength { get => UserRegistration.EvaluatePassword(Password); }

        /// <value>
        /// The user may not yet be connected to the server, in which case all operations related to server communication will cause errors or exceptions. Always check if the client is connected to the server before using <c>Client</c> singleton opearations.
        /// If the client is logged in successfully, then instead of <c>Connected</c> the state will be <c>LoggedIn</c>.
        /// </value>
        public static ClientState State { get; private set; } = ClientState.NotConnected;

        /// <value>
        /// List of all users (except the client itself) that are registered to the server, can be used to search user by its <c>Username</c> property value.
        /// </value>
        public static List<User> UserDiscoveryList { get; private set; } = new List<User>();

        /// <summary>
        /// Tries to connect to the server via Web Socket. This method won't throw any exceptions if the connection operation fails, to check if the connection was successful, check <c>State</c> property.
        /// <example>
        /// Example how the operation should be used:
        /// </example>
        /// <code>
        /// if (Client.State.Equals(ClientState.NotConnected))
        ///     await Client.ConnectToServer();
        ///     
        /// if (!Client.State.Equals(ClientState.Connected))
        ///     Console.WriteLine("Failed to connect the server...")
        /// </code>
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
        /// If <see cref="Username"/> and <see cref="Password"/> properties set in accordance with the requirements of the Protocol and <see cref="State"/> property is <see cref="ClientState.Connected"/>,
        /// then the attempt to register client to the server will be made. When registration succeeds, the <see cref="State"/> property changes to <see cref="ClientState.LoggedIn"/>.
        /// </summary>
        /// <exception cref="ClientOperationFailedException">
        /// Throw when the requirements are not met or registration to the server failed.
        /// </exception>
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
        /// Log in to server with set client singleton <see cref="Username"/> and <see cref="Password"/> credentials.
        /// </summary>
        /// <exception cref="ClientOperationFailedException">
        /// Throw when login operation failed or the requirements were not met.
        /// </exception>
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
        /// Populates <see cref="UserDiscoveryList"/> property with other users that are registered (not necessarily currently logged in) to server.
        /// </summary>
        /// <exception cref="ClientOperationFailedException">
        /// Throw when client is not connected to server, logged in or sending/receiving operations failed.
        /// </exception>
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
