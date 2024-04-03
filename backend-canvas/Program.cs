using flare_csharp;
using System.Security.Cryptography;
using System.Text;
using Isopoh.Cryptography.Argon2;

namespace backend_canvas
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // User pin
            string PIN = "12345678";
            Console.WriteLine($"PIN: {PIN}\n");

            // Hashing PIN in argon2
            string argon2Hash = Crypto.HashArgon2i(PIN);
            Console.WriteLine($"Full argon2 hash: {argon2Hash}\n");

            // Get the stretched key of the PIN
            string stretchedKey = argon2Hash.Split('$', StringSplitOptions.RemoveEmptyEntries).Last();
            Console.WriteLine($"Stretched key: {stretchedKey}\n");

            // Left half will become the authorization key
            string leftSide = stretchedKey.Substring(0, stretchedKey.Length / 2);
            string authKey = HMACSHA256.HashData(
                Encoding.ASCII.GetBytes(leftSide),
                Encoding.ASCII.GetBytes("Authorization Key Encryption")
                ).ToB64String();
            Console.WriteLine($"Authorization key: {leftSide} => HMACSHA256 -> {authKey}\n");

            // Right half will become the genesis key
            string rightSide = stretchedKey.Substring(stretchedKey.Length / 2, stretchedKey.Length / 2);
            string genesisKey = HMACSHA256.HashData(
                Encoding.ASCII.GetBytes(rightSide),
                Encoding.ASCII.GetBytes("Genesis Key Encryption")
                ).ToB64String();
            // "Genesis key: " + rightSide + " => " + genesisKey
            Console.WriteLine($"Genesis key: {rightSide} => HMACSHA256 -> {genesisKey}\n");

            // Genesis key will be used to hash master key
            string c = RandomNumberGenerator.GetBytes(32).ToB64String();
            string masterKey = HMACSHA256.HashData(
                Encoding.ASCII.GetBytes(genesisKey),
                Encoding.ASCII.GetBytes(c)
                ).ToB64String();
            Console.WriteLine($"Master key: {genesisKey} + {c} => HMACSHA256 -> {masterKey}\n");
        }
    }
}
