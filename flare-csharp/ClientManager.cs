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
        public string ServerUrl { get; private set; }
        public string Username { get; set; }
        public string PIN { get; set; }

        private (string fullHash, string c2) argon2iHash;
        private string authToken;
        private GrpcChannel channel;
        private Auth.AuthClient authClient;

        public ClientManager(string serverUrl)
        {
            ServerUrl = new string(serverUrl);
            Username = string.Empty;
            PIN = string.Empty;

            argon2iHash = (string.Empty, string.Empty);
            authToken = string.Empty;

            channel = GrpcChannel.ForAddress(ServerUrl);
            authClient = new Auth.AuthClient(channel);
        }

        public async Task<string> CheckUsernameStatusAsync()
        {
            string status = string.Empty;
            try
            {
                AsyncUnaryCall<UsernameOpinionResponse> call = 
                    authClient.GetUsernameOpinionAsync(
                        new UsernameOpinionRequest { Username = this.Username });

                Task<UsernameOpinionResponse> task = call.ResponseAsync;
                await task;

                status = (task.IsCompletedSuccessfully) ? task.Result.Opinion.ToString() : string.Empty;
            }
            catch (Exception) 
            {
                Console.WriteLine("[ERROR]: failed to get username status.");
            }

            return status;
        }

        public async Task<string> GetCredentialRequirementsAsync()
        {
            string req = string.Empty;

            try
            {
                AsyncUnaryCall<RequirementsResponse> call =
                    authClient.GetCredentialRequirementsAsync(
                        new RequirementsRequest { });

                Task<RequirementsResponse> task = call.ResponseAsync;
                await task;

                req = (task.IsCompletedSuccessfully) ? task.Result.ToString() : string.Empty;
            }
            catch (Exception)
            {
                Console.WriteLine("[ERROR]: failed to get credential requirements.");
            }

            return req;
        }

        public async Task RegisterToServer()
        {
            try
            {
                const int MEM_COST_BYTES = 1024 * 512; // 512MB
                const int TIME_COST = 3;

                this.argon2iHash = Crypto.HashArgon2i(this.PIN, this.ServerUrl, this.Username, MEM_COST_BYTES, TIME_COST);

                var regReq = new RegisterRequest
                {
                    Username = this.Username,
                    PasswordHash = this.argon2iHash.fullHash.Split('$').Last(),
                    HashParams = new HashParams
                    {
                        MemoryCost = MEM_COST_BYTES,
                        TimeCost = TIME_COST,
                        Salt = this.argon2iHash.c2,
                        Hash = this.argon2iHash.fullHash.Split('$').Last(),
                    },
                    IdentityPublicKey = string.Empty
                };

                var call = authClient.RegisterAsync(regReq);

                var task = call.ResponseAsync;
                await task;

                var response = (task.IsCompletedSuccessfully) ? task.Result : null;

                if (response is null)
                    return;

                if (response.HasFailure)
                {
                    Console.WriteLine($"Registration failed because {response.Failure}");
                    return;
                }
                else
                {
                    this.authToken = response.Token;
                    Console.WriteLine(argon2iHash);
                    Console.WriteLine(authToken);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("[ERROR]: failed to register to server.");
            }
        }

        public void SaveData()
        {
            throw new NotImplementedException();
        }
    }
}
