using Isopoh.Cryptography.Argon2;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
    public class IdentityStore
    {
        public AsymmetricCipherKeyPair? Identity;
        public Dictionary<string, Identity> Contacts = new();
    }
}