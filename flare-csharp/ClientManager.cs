using Grpc.Net.Client;
using Flare.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;

namespace flare_csharp
{
    public class ClientManager
    {
        public class ServerRequestFailureException : Exception { }
        public class ReceiveServerResponseFailException : Exception { }
        public class RegistrationFailedException : Exception
        {
            public RegistrationFailedException(string reason) : base(reason) { }
        }
        public string ServerUrl { get; private set; }
        public string Username { get => clientCredentials.Username; set => clientCredentials.Username = value; }
        public string PIN { get => clientCredentials.Password; set => clientCredentials.Password = value; }

        private ClientCredentials clientCredentials;
        private string authToken;
        private GrpcChannel channel;
        private Auth.AuthClient authClient;

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

        public async Task RegisterToServer()
        {
            RegisterResponse? resp = null;
            Crypto.HashPasswordArgon2i(ref clientCredentials);

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

        // TODO
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

        public void SaveData()
        {
            var writer = new StreamWriter(".\\Data.txt");
            writer.WriteLine(clientCredentials.ToString());
            writer.Close();
        }
    }
}
