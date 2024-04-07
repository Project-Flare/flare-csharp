using Flare.V1;
using Grpc.Net.Client.Web;
using Grpc.Net.Client;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Crypto.Agreement;
using flare_csharp;

namespace backend_canvas
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            System.Environment.SetEnvironmentVariable(
                "DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT", "1");

            var clientManager = new ClientManager("https://rpc.f2.project-flare.net");
            clientManager.Username = "manfredas_lamsargis_2004";
            clientManager.PIN = "12345678";
            string requirements = await clientManager.GetCredentialRequirementsAsync();
            Console.WriteLine(requirements);
            string status = await clientManager.CheckUsernameStatusAsync();
            Console.WriteLine(status);
            //await clientManager.RegisterToServer();
            //await clientManager.LoginToServer();
        }

        static List<string> ReadFromFile(string path)
        {
            var lines = new List<string>();
            try
            {
                var reader = new StreamReader(path);
                string? line = reader.ReadLine();
                while (line is not null)
                {
                    lines.Add(line);
                    line = reader.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return lines;
        }
    }
}
