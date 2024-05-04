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
        public const int MEMORY_COST_BYTES = 131_072;
        public const int MIN_TIME_COST = 3;
        public const int TIME_COST = 3;
        public const int MIN_SALT_ENTROPY = 31;
		/// <summary>
		/// Client's username.
		/// </summary>
		public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Client's password, most likely will be 8-digit PIN.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Full generated argon2 hash in <see cref="Crypto.HashPasswordArgon2i(ref Credentials)"/> method.
        /// </summary>
        public string Argon2Hash { get; set; } = string.Empty;

        /// <summary>
        /// Pseudo random constant used to create a salt for argon2 hash.
        /// </summary>
        public string PseudoRandomConstant { get => Username + "project-flare.net"; }

        /// <summary>
        /// Random constant that is provided by the device, used to create a salt for more secure argon2 hash.
        /// </summary>
        public string SecureRandom { get; set; } = string.Empty;

        /// <summary>
        /// How much bytes are used to hash <see cref="Password"/> in argon2 hash function.
        /// </summary>
        public int MemoryCostBytes { get; set; } = -1;

        /// <summary>
        /// Also the argon2 hash parameter, must be saved.
        /// </summary>
        public int TimeCost { get; set; } = -1;

        /// <summary>
        /// Only hash of the <see cref="Password"/>
        /// </summary>
        public string PasswordHash 
        {
            get => Argon2Hash.Split('$').Last();
        }
        public string Salt { get; set; } = string.Empty;
        /// <summary>
        /// Received authentication token from the server when logging in or registering. Thread safe (source - kinda trust me bro)
        /// </summary>
        public string AuthToken { get; set; } = string.Empty;

        public Credentials() { }

        public Credentials(int memoryCostBytes, int timeCost) 
        {
            MemoryCostBytes = memoryCostBytes;
            TimeCost = timeCost;
        }


        public override string ToString()
        {
            return new string($"{Username}\n{Password}\n{Argon2Hash}\n{PseudoRandomConstant}\n{SecureRandom}\n" +
                $"{MemoryCostBytes}\n{TimeCost}\n{PasswordHash}\n{AuthToken}");
        }
    }
}
