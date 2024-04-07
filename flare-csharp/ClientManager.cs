using Grpc.Net.Client;
using Flare.V1;

namespace flare_csharp
{
    public class ClientManager
    {
        /// <summary>
        /// Sending request to the server failed.
        /// </summary>
        public class ServerRequestFailureException : Exception { }

        /// <summary>
        /// Response is not received or received in bad format.
        /// </summary>
        public class ReceiveServerResponseFailException : Exception { }

        /// <summary>
        /// The registration of new user operation failed, specifically used in <see cref="RegisterToServer"/>
        /// </summary>
        public class RegistrationFailedException : Exception
        {
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
        /// Client's PIN code, used to generate as password.
        /// </summary>
        public string PIN { get => clientCredentials.Password; set => clientCredentials.Password = value; }

        /// <summary>
        /// This holds important credential information of the client.
        /// </summary>
        private ClientCredentials clientCredentials;

        /// <summary>
        /// Authorization token received from the server that is used to authenticate the session between the client and server.
        /// </summary>
        private string authToken;

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
            PIN = string.Empty;

            authToken = string.Empty;

            channel = GrpcChannel.ForAddress(ServerUrl);
            authClient = new Auth.AuthClient(channel);
        }

        /// <summary>
        /// Simple call to the server to check if the set username is acceptable for registration.
        /// </summary>
        /// <returns>
        /// <c>Unspecified</c> treat as a server-error, <c>Taken</c> username is already taken, <c>Bad</c> username does not follow the requirements, <c>Ok</c> this username can be used to register a new user.
        /// </returns>
        /// <exception cref="ServerRequestFailureException">
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
            catch (Exception)
            {
                throw new ServerRequestFailureException();
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
        /// <exception cref="ServerRequestFailureException"></exception>
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
            catch (Exception)
            {
                throw new ServerRequestFailureException();
            }
        }

        /// <summary>
        /// Correctly specified user credentials will be hashed by secure argon2i, <see cref="PIN"/> must be set and will be used as password to generate password hash. 
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
        /// <exception cref="ServerRequestFailureException">
        /// Thrown when there is an error when sending request to the server.
        /// </exception>
        /// <exception cref="ReceiveServerResponseFailException">
        /// Thrown when there is an error on the receiving response from the server.
        /// </exception>
        /// <exception cref="RegistrationFailedException">
        /// Thrown when server refused to accept new registration with set credentials.
        /// </exception>
        public async Task RegisterToServer()
        {
            HashPassword();

            RegisterResponse? resp = null;

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
                                    Hash = clientCredentials.PasswordHash
                                }
                            }));
            }
            catch (Exception)
            {
                throw new ServerRequestFailureException();
            }

            if (resp is null)
                throw new ReceiveServerResponseFailException();

            if (resp.HasFailure)
                throw new RegistrationFailedException(resp.Failure.ToString());

            clientCredentials.AuthToken = resp.Token;
            SaveData();
        }

        // TODO - not implemented yet
        public async Task LoginToServer()
        {
            var logReq = new LoginRequest
            {
                // hardcoded for now
                Username = "manfredas_lamsargis_2004",
                Password = "WXYSO1o7wPLNkFk8pnHUENEyyCtV7ehrXCd/t6hyNus"
            };

            var call = authClient.LoginAsync(logReq);

            var task = call.ResponseAsync;
            await task;

            var response = (task.IsCompletedSuccessfully) ? task.Result : null;

            if (response is null)
                return;

            if (response.HasFailure)
                return;

            this.authToken = response.Token;
            Console.WriteLine($"Login successful, received token: {authToken}");
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
