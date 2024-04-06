using flare_csharp;
using System.Security.Cryptography;
using System.Text;
using Isopoh.Cryptography.Argon2;
using Flare.V1;
using Grpc.Net.Client.Web;
using Grpc.Net.Client;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Google.Protobuf.Reflection;

namespace backend_canvas
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            /*// User pin
             string PIN = "12345678";
             Console.WriteLine($"PIN: {PIN}\n");

             // Hash for session authentication
             string argon2Hash = Crypto.HashArgon2i(PIN);
             Console.WriteLine($"Full argon2 hash: {argon2Hash}\n");

             Client client = new Client();
             try
             {
                 client.TryEstablishChannel();
             }
             catch(FailedToEstablishClientChannelException)
             {
                 Console.WriteLine($"Failed to establish channel to the server: {client.ServerUrl}");
             }
             finally
             {
                 Console.WriteLine($"Client is connected: {client.ChannelEstablished}");
             }
             client.GetUsernameRequirements();*/

            // User pin

            string PIN = "12345678";
            Console.WriteLine($"PIN: {PIN}\n");

            var clientHandler = new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback =
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
                            "041edefed4e89575f318071a7177e2af696004dfa3d266d0586e30e02b76" +
                            "c3171cb87cf12e69f1903fe55aeed708b480e5bfe4db7512fa63c52a708b" +
                            "7ad615241f078db87a9f24c9913749d0ab1765c5dea6c56a5b1527ec721d" +
                            "64ac2e19a29adf";

                        return certificate.GetPublicKeyString().ToLower().Equals(pub_key_pin);
                    }
                }
            };

            var channel = GrpcChannel.ForAddress("http://77.221.72.56:50051", new GrpcChannelOptions
            {
                DisposeHttpClient = true,
                ThrowOperationCanceledOnCancellation = true,
                HttpClient = new HttpClient(new SocketsHttpHandler
                {
                    ConnectTimeout = TimeSpan.FromSeconds(3)
                })
            });

            var authClient = new Auth.AuthClient(channel);
            var requirementsRequest = new RequirementsRequest();
            var credentialRequirementRequest = authClient.GetCredentialRequirements(requirementsRequest);
            Console.WriteLine($"{credentialRequirementRequest}");
            authClient = new Auth.AuthClient(channel);

            try
            {
                var usernameOpinionRequest = new UsernameOpinionRequest();
                usernameOpinionRequest.Username = "manfredas_lamsargis_2003";
                var usernameOpinionResponse = authClient.GetUsernameOpinionAsync(usernameOpinionRequest);
                await usernameOpinionResponse;
                Console.WriteLine($"{usernameOpinionResponse.ResponseAsync.Result.Opinion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Failed to get username opinion response because:\n\n {ex.Message}");
            }

            var result = Crypto.HashArgon2i(PIN, "project-flare.net", "manfredas_lamsargis_2003");
            Console.WriteLine(result.hash + "\n" + result.secureRandom);
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(120));
            authClient = new Auth.AuthClient(channel);
            try
            {
                var registerRequest = new RegisterRequest();
                registerRequest.Username = "manfredas_lamsargis_2020";
                registerRequest.PasswordHash = result.hash.Split('$').Last();
                registerRequest.HashParams = new HashParams
                {
                    Salt = result.secureRandom,
                    MemoryCost = 512_000,
                    TimeCost = 3
                };
                registerRequest.IdentityPublicKey = string.Empty;

                var header = new Metadata();
                header.Add("flare-auth", "6+3YkyU047cskNhD6UG9E4ucnje/IGcIsH5rpnaVQ+FGH8vblq2/CB7Qwsw8wsy4f/KKJ84ElFSZmsMGYsXAY1KrjaiAUsKKN7m6Z8XbUnxwdg2a4wwCWP75+yAsZjgp9OlP6g4vAlcR22o0JmHP6u0OAy2NAgKCkbU/Y4g1dwjhbJalwPQgdS9yEp3CPPJSTf/CAW0=|xGrvv0bfMpEkhR4F");
                
                var registerResponse = await authClient.RegisterAsync(registerRequest, cancellationToken: cts.Token);
                Console.WriteLine(registerResponse.ToString());

                var getClientHashParamsReq = new GetClientHashParamsRequest();
                getClientHashParamsReq.Username = "manfredas_lamsargis_2007";
                var getClientHashParamsResp = await authClient.GetClientHashParamsAsync(getClientHashParamsReq, headers: header);
                Console.WriteLine(getClientHashParamsReq.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            authClient = new Auth.AuthClient(channel);
            try
            {
                var registerRequest = new RegisterRequest();
                registerRequest.Username = "manfredas_lamsargis_2020";
                registerRequest.PasswordHash = result.hash.Split('$').Last();
                registerRequest.HashParams = new HashParams
                {
                    Salt = result.secureRandom,
                    MemoryCost = 512_000,
                    TimeCost = 3
                };
                registerRequest.IdentityPublicKey = string.Empty;

                var header = new Metadata();
                header.Add("flare-auth", "6+3YkyU047cskNhD6UG9E4ucnje/IGcIsH5rpnaVQ+FGH8vblq2/CB7Qwsw8wsy4f/KKJ84ElFSZmsMGYsXAY1KrjaiAUsKKN7m6Z8XbUnxwdg2a4wwCWP75+yAsZjgp9OlP6g4vAlcR22o0JmHP6u0OAy2NAgKCkbU/Y4g1dwjhbJalwPQgdS9yEp3CPPJSTf/CAW0=|xGrvv0bfMpEkhR4F");

                var registerResponse = await authClient.RegisterAsync(registerRequest, cancellationToken: cts.Token);
                Console.WriteLine(registerResponse.ToString());

                /* var getClientHashParamsReq = new GetClientHashParamsRequest();
                 getClientHashParamsReq.Username = "manfredas_lamsargis_2007";
                 var getClientHashParamsResp = await authClient.GetClientHashParamsAsync(getClientHashParamsReq, headers: header);
                 Console.WriteLine(getClientHashParamsReq.ToString());*/
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.ReadKey();
        }
    }
}
