using Isopoh.Cryptography.Argon2;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace flare_csharp
{
    public class Credentials
    {
        /// <summary>
        /// 64 MB
        /// </summary>
        public const int MIN_MEMORY_COST_BYTES = 65_536;

        /// <summary>
        /// 128 MB
        /// </summary>
        public const int DEFAULT_MEMORY_COST_BYTES = 131_072;

		/// <summary>
		/// Amount of computation realized and impacts the execution time (3)
		/// </summary>
		public const int MIN_TIME_COST = 3;

		/// <summary>
		/// Amount of computation realized and impacts the execution time (3)
		/// </summary>
		public const int DEFAULT_TIME_COST = 3;

        /// <summary>
        /// 31-bit entropy
        /// </summary>
        public const int MIN_SALT_ENTROPY = 31;

		/// <summary>
		/// Client's username.
		/// </summary>
		public string Username { get; set; }

        /// <summary>
        /// Client's password, most likely will be 8-digit PIN.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Full generated argon2 hash in <see cref="Crypto.HashPasswordArgon2i(ref Credentials)"/> method.
        /// </summary>
        public string Argon2Hash { get; set; }

        /// <summary>
        /// Pseudo random constant used to create a salt for argon2 hash.
        /// </summary>
        public string PseudoRandomConstant { get => Username + "project-flare.net"; }

        /// <summary>
        /// Random constant that is provided by the device, used to create a salt for more secure argon2 hash.
        /// </summary>
        public string SecureRandom { get; set; }

        /// <summary>
        /// How much bytes are used to hash <see cref="Password"/> in argon2 hash function.
        /// </summary>
        public int MemoryCostBytes { get; set; }

        /// <summary>
        /// Also the argon2 hash parameter, must be saved.
        /// </summary>
        public int TimeCost { get; set; }

        /// <summary>
        /// Only hash of the <see cref="Password"/>
        /// </summary>
        public string PasswordHash 
        {
            get => Argon2Hash.Split('$').Last();
        }
        public string Salt { get; set; }

        /// <summary>
        /// Received authentication token from the server when logging in or registering.
        /// </summary>
        public string AuthToken { get; set; }

        /// <summary>
        /// EC Diffie-Hellman Key Pair
        /// </summary>
        public AsymmetricCipherKeyPair? AsymmetricCipherKeyPair { get; set; }

        /// <summary>
        /// Used to send as user's public key to the server
        /// </summary>
        public string? IdentityPublicKey 
        {
            get
            {
                if (AsymmetricCipherKeyPair is not null)
                    return 
                        SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(AsymmetricCipherKeyPair!.Public)
                            .GetDerEncoded().ToB64String();
                return null;
            }
        }

        /// <summary>
        /// Initializing with default values.
        /// </summary>
        public Credentials() 
        {
            Argon2Hash = string.Empty;
            AuthToken = string.Empty;
            MemoryCostBytes = DEFAULT_MEMORY_COST_BYTES;
            Password = string.Empty;
            Salt = string.Empty;
            SecureRandom = string.Empty;
            TimeCost = DEFAULT_TIME_COST;
            Username = string.Empty;
        }



        public override string ToString()
        {
            return new string($"{Username}\n{Password}\n{Argon2Hash}\n{PseudoRandomConstant}\n{SecureRandom}\n" +
                $"{MemoryCostBytes}\n{TimeCost}\n{PasswordHash}\n{AuthToken}");
        }
    }
}
