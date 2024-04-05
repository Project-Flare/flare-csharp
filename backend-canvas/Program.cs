using flare_csharp;
using System.Security.Cryptography;
using System.Text;
using Isopoh.Cryptography.Argon2;
using Flare.V1;
using Grpc.Net.Client.Web;
using Grpc.Net.Client;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace backend_canvas
{
    internal class Program
    {
        static void Main(string[] args)
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

            // Hash for session authentication
            string argon2Hash = Crypto.HashArgon2i(PIN);
            Console.WriteLine($"Full argon2 hash: {argon2Hash}\n");

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

            var channel = GrpcChannel.ForAddress("https://rpc.f2.project-flare.net", new GrpcChannelOptions
            {
                HttpHandler = new GrpcWebHandler(clientHandler),
            });

            var authClient = new Auth.AuthClient(channel);
            var request = new RequirementsRequest();
            var authResponse = authClient.GetCredentialRequirements(request);
            Console.WriteLine($"{authResponse}");
            Console.ReadKey();
        }
    }
}
