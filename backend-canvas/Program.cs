using Flare;
using flare_csharp;

namespace backend_canvas
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            Console.WriteLine(Client.State);

            await Client.ConnectToServer();

            Console.WriteLine(Client.State);

            Client.Username = "herkus_leon_kaselis_3";
            Client.Password = "n:+l@/~t}E:~\\7:N}\"ELR.8<9";

            Console.WriteLine("Username is " + Client.UsernameEvaluation);
            Console.WriteLine("Password is " + Client.PasswordStrength);

            Console.WriteLine("Registration of " + Client.Username + " started...");
            try
            {
                await Client.RegisterToServer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to register client to server: " + ex.Message);
                return;
            }

            Console.WriteLine("Registration successful: " + Client.AuthToken);
        }
    }
}
