using Grpc.Net.Client.Web;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;
using Flare.V1;

namespace flare_csharp
{
    public class FailedToEstablishClientChannelException() : Exception() { }
    public class Client
    {
        public readonly string ServerUrl = "https://rpc.f2.project-flare.net";
        public bool ChannelEstablished { get; private set; } = false;

        private GrpcChannel? channel;
        private Auth.AuthClient? authClient;
        public Client() { }
        public void TryEstablishChannel()
        {
            if (ChannelEstablished)
                return;

            try
            {
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

                this.channel = GrpcChannel.ForAddress(ServerUrl, new GrpcChannelOptions
                {
                    HttpHandler = new GrpcWebHandler(clientHandler),
                });

                this.authClient = new Auth.AuthClient(this.channel);
            }
            catch (Exception)
            {
                throw new FailedToEstablishClientChannelException();
            }
        }

        public void GetUsernameRequirements()
        {
            if (!ChannelEstablished)
                return;
            var request = new RequirementsRequest();
            var authResponse = authClient!.GetCredentialRequirements(request);
            Console.WriteLine($"{authResponse}");
        }
    }
}
