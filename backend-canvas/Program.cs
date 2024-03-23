using Flare;
using flare_csharp;

namespace backend_canvas
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Just the sandbox
            Console.WriteLine(Client.State);

            await Client.ConnectToServer();

            Console.WriteLine(Client.State);

            await Client.DisconnectFromServer();

            Console.WriteLine(Client.State);

            Client.Username = "herkus_leon_kaselis_3";
            Client.Password = "n:+l@/~t}E:~\\7:N}\"ELR.8<9";

            Console.WriteLine("Username is " + Client.UsernameEvaluation);
            Console.WriteLine("Password is " + Client.PasswordStrength);

            Client.Username = "Skibidi";
            Client.Password = "scndjakcn((201328::aksdl]{";

            Console.WriteLine("Registration of " + Client.Username + " started...");
            try
            {
                await Client.RegisterToServer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to register client to server: " + ex.Message);
            }

            try
            {
                await Client.LoginToServer();
            }
            catch(Exception ex)
            {
                Console.WriteLine("Failed to log in to server: " + ex.Message);
            }

            Console.WriteLine("Login successful: " + Client.AuthToken);

            try
            {
                await Client.FillUserDiscovery();
            }
            catch(Exception)
            {
                Console.WriteLine("Failed to get user list");
            }

            foreach(var user in Client.UserDiscoveryList)
            {
                Console.WriteLine(user.ToString());
            }
        }
    }
}
