using flare_csharp;

namespace backend_canvas
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            System.Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT", "1");

            var clientManager = new ClientManager("https://rpc.f2.project-flare.net");
            await clientManager.LoginToServer();
            await clientManager.GetTokenHealth();
        }
    }
}
