using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
    internal class ClientCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Argon2Hash { get; set; } = string.Empty;
        public string PseudoRandomConstant { get => Username + "project-flare.net"; }
        public string SecureRandom { get; set; } = string.Empty;
        public int MemoryCostBytes { get; private set; }
        public int TimeCost { get; private set; }
        public string PasswordHash 
        {
            get => Argon2Hash.Split('$').Last();
        }
        public string AuthToken { get; set; } = string.Empty;

        public ClientCredentials(int memoryCostBytes, int timeCost) 
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
