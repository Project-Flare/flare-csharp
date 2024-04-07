using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
    internal class Argon2iHash
    {
        public string Password { get; set; }
        public string Argon2Hash { get; set; }
        public string SecureRandom { get; set; }
        public int MemoryCost { get; set; }
        public int TimeCost { get; set; }
        public string PasswordHash 
        {
            get => Argon2Hash.Split('$').Last();
        }
    }
}
