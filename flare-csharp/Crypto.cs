using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;
using System.Security.Cryptography;
using System.Text;

namespace flare_csharp
{
    public static class Crypto
    {
        /// <summary>
        /// User PIN must contain only ASCII digits [0-9].
        /// <example>
        /// For example:
        /// <c>string pin = "88890785"</c>
        /// </example>
        /// </summary>
        public class PinNotAllAsciiDigitsException : Exception { }
        /// <summary>
        /// The user PIN by the protocol must contain only 8 digits total.
        /// </summary>
        public class PinNotEightDigitsException : Exception { }
        
        /// <summary>
        /// Derived argon2 hash from a user pin.
        /// Hash is used as server-submittable password for proving ownership of the username.
        /// </summary>
        /// <param name="password">8 digit string.</param>
        /// <returns></returns>
        /// <exception cref="PinNotAllAsciiDigitsException"></exception>
        /// <exception cref="PinNotEightDigitsException"></exception>
        public static (string hash, string secureRandom) HashArgon2iOld(string password, string host, string username)
        {
            // PIN must contain only digits
           /* if (!password.All(x => Char.IsAsciiDigit(x)))
                throw new PinNotAllAsciiDigitsException();*/

            // PIN must contain only 8 digits in total
            const int EIGHT_DIGITS = 8;
            if (password.Length != EIGHT_DIGITS)
                throw new PinNotEightDigitsException();

            string c1 = host + username;
            string c2 = RandomNumberGenerator.GetBytes(16).ToB64String();
            string salt = c1 + c2;

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
                Password = Encoding.ASCII.GetBytes(password),
                // You cannot have an output without a salt, must be >= 8 bytes TODO-get timestamp from the server, add "project" 
                Salt = Encoding.ASCII.GetBytes(salt),
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

            return new (hashString, c2);
        }

        public static (string argonHash, string c2) HashArgon2i(string password, string host, string username, int memoryCostBytes, int timeCost)
        {
            string c1 = host + username;
            string c2 = RandomNumberGenerator.GetBytes(16).ToB64String();
            string salt = c1 + c2;

            // Set the appropriate configuration for hashing PIN code
            Argon2Config argonConfig = new Argon2Config
            {
                // Slightly slower, but is safe from side-channel attacks
                Type = Argon2Type.DataIndependentAddressing,
                Version = Argon2Version.Nineteen,
                // Amount of computation realized and impacts the execution time
                TimeCost = timeCost,
                // Memory needed for the hash
                MemoryCost = memoryCostBytes,
                Lanes = 4,
                // Higher that "Lanes" does not hurt
                Threads = Environment.ProcessorCount,
                // Convert pin to bytes
                Password = Encoding.ASCII.GetBytes(password),
                // You cannot have an output without a salt, must be >= 8 bytes TODO-get timestamp from the server, add "project" 
                Salt = Encoding.ASCII.GetBytes(salt),
                Secret = Encoding.ASCII.GetBytes("Flare App Csharp"),
                HashLength = 32
            };

            var argon2 = new Argon2(argonConfig);
            string argonHashString;
            // Use an array that can hold secret information to extract the hash
            using (SecureArray<byte> hash = argon2.Hash())
            {
                argonHashString = argonConfig.EncodeString(hash.Buffer);
            }

            return new(argonHashString, c2);
        }
    }
}
