using Grpc.Net.Client;
using Flare.V1;
using Grpc.Core;
using Org.BouncyCastle.Math.EC.Rfc8032;
using System.Runtime.CompilerServices;

namespace flare_csharp
{
    public class ClientManager
    {
        /// <summary>
        /// Sending request to the server failed.
        /// </summary>
        public class GrpcCallFailureException : Exception
        {
            public GrpcCallFailureException() : base() { }
            public GrpcCallFailureException(string mess, Exception innerEx) : base(mess, innerEx) { }
        }

        /// <summary>
        /// The request to remove all data from the server failed.
        /// </summary>
        public class FailedToWipeUserDataFromServerException : Exception { }

        /// <summary>
        /// Login credentials of the user are invalid.
        /// </summary>
        public class LoginFailureException : Exception
        {
            /// <summary>
            /// Failure of the login request with the reason why login failed.
            /// </summary>
            /// <param name="reason">The reason why the login request was denied by the server.</param>
            public LoginFailureException(string reason) : base(reason) { }
        }

        /// <summary>
        /// The registration of new user operation failed, specifically used in <see cref="RegisterToServerAsync"/>
        /// </summary>
        public class RegistrationFailedException : Exception
        {
            /// <summary>
            /// Failure to register the user to the server.
            /// </summary>
            /// <param name="reason">Reason of why the registration attempt of the client was refused.</param>
            public RegistrationFailedException(string reason) : base(reason) { }
        }

        /// <summary>
        /// Failed to generate correct argon2 hash using <see cref="Crypto.HashPasswordArgon2i(ref ClientCredentials)"/> hash method.
        /// </summary>
        public class WrongCredentialsForArgon2Hash : Exception { }

        /// <summary>
        /// Server domain URL.
        /// </summary>
        public string ServerUrl { get; private set; }

        /// <summary>
        /// Client's username.
        /// </summary>
        public string Username { get => clientCredentials.Username; set => clientCredentials.Username = value; }

        /// <summary>
        /// Client's password, used to generate password hash.
        /// </summary>
        public string Password { get => clientCredentials.Password; set => clientCredentials.Password = value; }

        /// <summary>
        /// This holds important credential information of the client.
        /// </summary>
        private ClientCredentials clientCredentials;

        /// <summary>
        /// gRPC channel through communication between client and server happens.
        /// </summary>
        private GrpcChannel channel;

        /// <summary>
        /// Authentication client service.
        /// </summary>
        private Auth.AuthClient authClient;

        /// <summary>
        /// Creates new ClientManager instance with clean new parameters.
        /// </summary>
        /// <param name="serverUrl">The server URL needed to connect to.</param>
        public ClientManager(string serverUrl)
        {
            const int MEM_COST_BYTES = 1024 * 512; // 512MB
            const int TIME_COST = 3;
            clientCredentials = new ClientCredentials(MEM_COST_BYTES, TIME_COST);

            ServerUrl = new string(serverUrl);
            Password = string.Empty;

            channel = GrpcChannel.ForAddress(ServerUrl);
            authClient = new Auth.AuthClient(channel);
        }

        /// <summary>
        /// Simple call to the server to check if the set username is acceptable for registration.
        /// </summary>
        /// <returns>
        /// <c>Unspecified</c> treat as a server-error, <c>Taken</c> username is already taken, <c>Bad</c> username does not follow the requirements, <c>Ok</c> this username can be used to register a new user.
        /// </returns>
        /// <exception cref="GrpcCallFailureException">
        /// The process of sending-receiving failed.
        /// </exception>
        public async Task<string> CheckUsernameStatusAsync()
        {
            try
            {
                UsernameOpinionResponse resp =
                    await ServerCall<UsernameOpinionResponse>.FulfilUnaryCallAsync(
                            authClient.GetUsernameOpinionAsync(
                                new UsernameOpinionRequest { Username = this.Username }));
                return resp.Opinion.ToString();
            }
            catch (Exception ex)
            {
                throw new GrpcCallFailureException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Use to get credential requirements from the server, use "flare-proto/rpc_auth.proto" file's "RequirementResponse" section.
        /// </summary>
        /// <returns>
        /// Formatted string where the requirements of user password and username are specified
        /// <example>
        /// For example:
        /// <code>
        /// { "usernameRequirements": { "minLength": "2", "maxLength": "32", "encoding": "ENCODING_ASCII", "formatType": "STRING_FORMAT_TYPE_ALPHANUMERIC" }, "passwordRequirements": { "maxLength": "128", "encoding": "ENCODING_UNICODE", "bitsEntropy": "50" } }
        /// </code>
        /// </example>
        /// </returns>
        /// <exception cref="GrpcCallFailureException"></exception>
        public async Task<string> GetCredentialRequirementsAsync()
        {
            try
            {
                RequirementsResponse resp =
                    await ServerCall<RequirementsResponse>.FulfilUnaryCallAsync(
                        authClient.GetCredentialRequirementsAsync(
                            new RequirementsRequest { }));
                return resp.ToString();
            }
            catch (Exception ex)
            {
                throw new GrpcCallFailureException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Correctly specified user credentials will be hashed by secure argon2i, <see cref="Password"/> must be set and will be used as password to generate password hash. 
        /// </summary>
        /// <exception cref="WrongCredentialsForArgon2Hash">
        /// Thrown when the credential parameters or password do not match the requirements to securely hash password using <see cref="Crypto.HashPasswordArgon2i(ref ClientCredentials)"/>
        /// </exception>
        private void HashPassword()
        {
            try
            {
                Crypto.HashPasswordArgon2i(ref clientCredentials);
            }
            catch (Exception)
            {
                throw new WrongCredentialsForArgon2Hash();
            }
        }

        /// <summary>
        /// Registration of the new user to the server, user receives authentication token. 
        /// To successfully complete the registration request client must define its unique username and check it's status <see cref="CheckUsernameStatusAsync"/> if it fulfils all the requirements.
        /// When password or username are not set, expect errors. If no exceptions are thrown, the registration process is successful, client credentials that contain vital information MUST be stored securely on local device.
        /// </summary>
        /// <exception cref="GrpcCallFailureException">
        /// Thrown when there is an error when sending request to the server.
        /// </exception>
        /// <exception cref="RegistrationFailedException">
        /// Thrown when server refused to accept new registration with set credentials.
        /// </exception>
        public async Task RegisterToServerAsync()
        {
            HashPassword();

            RegisterResponse resp;

            try
            {

                resp =
                    await ServerCall<RegisterResponse>.FulfilUnaryCallAsync(
                        authClient.RegisterAsync(
                            new RegisterRequest
                            {
                                Username = clientCredentials.Username,
                                PasswordHash = clientCredentials.PasswordHash,
                                HashParams = new HashParams
                                {
                                    MemoryCost = (ulong)clientCredentials.MemoryCostBytes,
                                    TimeCost = (ulong)clientCredentials.TimeCost,
                                    Salt = clientCredentials.SecureRandom,
                                }
                            }));
            }
            catch (Exception ex)
            {
                throw new GrpcCallFailureException(ex.Message, ex);
            }

            if (resp.HasFailure)
                throw new RegistrationFailedException(resp.Failure.ToString());

            clientCredentials.AuthToken = resp.Token;
            SaveData();
        }

        /// <summary>
        /// Logs user to the server if the user is already registered (aka exists).
        /// </summary>
        /// <returns></returns>
        /// <exception cref="GrpcCallFailureException">
        /// Throws when the gRPC call process aborted.
        /// </exception>
        /// <exception cref="LoginFailureException">
        /// Throws when the login request with current client's credentials has been denied.
        /// </exception>
        public async Task LoginToServerAsync()
        {
            LoginResponse resp;

            try
            {
                resp =
                    await ServerCall<LoginResponse>.FulfilUnaryCallAsync(
                        authClient.LoginAsync(new LoginRequest
                        {
                            Username = clientCredentials.Username,
                            Password = clientCredentials.Password
                        })
                    );
            }
            catch (Exception ex)
            {
                throw new GrpcCallFailureException(ex.Message, ex);
            }

            if (resp.HasFailure)
            {
                throw new LoginFailureException(resp.Failure.ToString());
            }

            if (resp.HasToken)
            {
                clientCredentials.AuthToken = resp.Token;
            }

            SaveData();
        }

        /// <summary>
        /// Checks user's token health.
        /// </summary>
        /// <returns>
        /// <c>Unspecified</c> unknown error, <c>Dead</c> the token is expired or invalid, <c>Ok</c> token is correct.
        /// </returns>
        /// <exception cref="GrpcCallFailureException">
        /// Throws when an error occurs on the gRPC call.
        /// </exception>
        public async Task<string> GetTokenHealthAsync()
        {
            TokenHealthResponse resp;

            try
            {
                var metadata = new Metadata
                {
                    { "flare-auth", clientCredentials.AuthToken }
                };
                resp = await ServerCall<TokenHealthResponse>.FulfilUnaryCallAsync(
                    authClient.GetTokenHealthAsync(
                        new TokenHealthRequest { }, headers: metadata));
            }
            catch (Exception ex)
            {
                throw new GrpcCallFailureException(ex.Message, ex);
            }

            return resp.Health.ToString();
        }

        /// <summary>
        /// Send request to server to renew the server-issued token for client.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="GrpcCallFailureException">The gRPC call of request failed.</exception>
        public async Task RenewTokenAsync()
        {
            RenewTokenResponse resp;

            try
            {
                resp = await ServerCall<RenewTokenResponse>.FulfilUnaryCallAsync(
                    authClient.RenewTokenAsync(
                        new RenewTokenRequest { },
                        headers: new Metadata
                        {
                            { "flare-auth", clientCredentials.AuthToken }
                        }));
            }
            catch (Exception ex)
            {
                throw new GrpcCallFailureException(ex.Message, ex);
            }

            clientCredentials.AuthToken = resp.Token;
        }

        /// <summary>
        /// Removes all data of the user from the server database and the client's username can be reused.
        /// </summary>
        /// <param name="lockUsername">
        /// <c>true</c> if username can't be used (locking other registrations that have the same username), <c>false</c> if other
        /// user can be registered to server with the same username.
        /// </param>
        /// <returns></returns>
        /// <exception cref="FailedToWipeUserDataFromServerException">
        /// Failed to wipe user data from the server.
        /// </exception>
        public async Task RemoveUserFromServerAsync(bool lockUsername)
        {
            var resp = await ServerCall<ObliviateResponse>.FulfilUnaryCallAsync(
                authClient.ObliviateAsync(
                    new ObliviateRequest { Lockdown = lockUsername },
                    headers: new Metadata { { "flare-auth", clientCredentials.AuthToken } }
                ));

            if (resp.Result != ObliviateResponse.Types.ObliviateResult.OkUnlocked)
                throw new FailedToWipeUserDataFromServerException();
        }

        // Just simple way of saving creds on .txt file (temporary)
        public void SaveData()
        {
            var writer = new StreamWriter(".\\Data.txt");
            writer.WriteLine(clientCredentials.ToString());
            writer.Close();
        }
    }
}
