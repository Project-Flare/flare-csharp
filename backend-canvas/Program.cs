using flare_csharp;

namespace backend_canvas
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            System.Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT", "1");

            var clientManager = new ClientManager("https://rpc.f2.project-flare.net");
            clientManager.Credentials.Username = "testing_user_0";
            clientManager.Credentials.Password = "190438934";
            clientManager.Credentials.Argon2Hash = "$argon2i$v=19$m=524288,t=3,p=4$dGVzdGluZ191c2VyXzBwcm9qZWN0LWZsYXJlLm5ldGVNVEhJaWl0NlNTcWZKdWg2UEovM3c$tHhA3AmlEH8ao3vypVV36eyzbKfuX2b5a+5OCdD0kJI";
            await clientManager.LoginToServerAsync();
        }
    }
}
