using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;
using System.Security.Cryptography;
using System.Text;

namespace flare_csharp
{
    public static class Crypto
    {
        public static string HashArgon2i(string pin)
        {
            // Set the appropriate configuration for hashing PIN code
            Argon2Config argonConfig = new Argon2Config
            {
                // Slightly slower, but is safe from side-channel attacks
                Type = Argon2Type.DataIndependentAddressing,
                Version = Argon2Version.Nineteen,
                // Amount of computation realized and impacts the execution time
                TimeCost = 3,
                // Memory needed for the hash
                MemoryCost = 512_000,
                Lanes = 4,
                // Higher that "Lanes" does not hurt
                Threads = Environment.ProcessorCount,
                // Convert pin to bytes
                Password = Encoding.ASCII.GetBytes(pin),
                // You cannot have an output without a salt, must be >= 8 bytes
                Salt = RandomNumberGenerator.GetBytes(16),
                Secret = Encoding.ASCII.GetBytes("Flare App Csharp"),
                HashLength = 32
            };

            var argon2 = new Argon2(argonConfig);
            string hashString;
            // Use an array that can hold secret information to extract the hash
            using (SecureArray<byte> hash = argon2.Hash())
            {
                hashString = argonConfig.EncodeString(hash.Buffer);
            }

            return hashString;
        }
    }
}
