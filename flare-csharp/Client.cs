namespace flare_csharp
{
    public class Client
    {
        public enum State
        {
            Connected, Disconnected
        }
        public static string ServerUrl { get; } = "https://rpc.f2.project-flare.net";
        private ClientManager clientManager;
        public Client()
        {
            clientManager = new ClientManager(ServerUrl);
        }

        public void Thread()
        {
            while (true)
            {

            }
        }
    }
}
