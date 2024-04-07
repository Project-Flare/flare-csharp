using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;
using System.Security.Cryptography;
using System.Text;

namespace flare_csharp
{
    public static class Crypto
    {
        /// <summary>
        /// Used to generate password hash with set parameters in <see cref="ClientCredentials"/> class.
        /// </summary>
        /// <param name="cred">Credentials to be used to generate argon2 hash for these specific credentials.</param>
        public static void HashPasswordArgon2i(ref ClientCredentials cred)
        {
            cred.SecureRandom = RandomNumberGenerator.GetBytes(16).ToB64String();
            string salt = cred.PseudoRandomConstant + cred.SecureRandom;

            // Set the appropriate configuration for hashing PIN code
            Argon2Config argonConfig = new Argon2Config
            {
                // Slightly slower, but is safe from side-channel attacks
                Type = Argon2Type.DataIndependentAddressing,
                Version = Argon2Version.Nineteen,
                // Amount of computation realized and impacts the execution time
                TimeCost = cred.TimeCost,
                // Memory needed for the hash
                MemoryCost = cred.MemoryCostBytes,
                Lanes = 4,
                // Higher that "Lanes" does not hurt
                Threads = Environment.ProcessorCount,
                // Convert pin to bytes
                Password = Encoding.ASCII.GetBytes(cred.Password),
                // You cannot have an output without a salt, must be >= 8 bytes TODO-get timestamp from the server, add "project" 
                Salt = Encoding.ASCII.GetBytes(salt),
                Secret = Encoding.ASCII.GetBytes("Flare App Csharp"),
                HashLength = 32
            };

            var argon2 = new Argon2(argonConfig);
            // Use an array that can hold secret information to extract the hash
            using (SecureArray<byte> hash = argon2.Hash())
            {
                cred.Argon2Hash = argonConfig.EncodeString(hash.Buffer);
            }
        }
    }
}
