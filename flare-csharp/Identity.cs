using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
    public class Identity
    {
        public string Username { get; set; }
        public ECPublicKeyParameters PublicKey { get; set; }
        public byte[]? SharedSecret { get; set; }
    }
}
