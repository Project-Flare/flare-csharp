using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using System.Numerics;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Math;

namespace flare_csharp
{
    public static class Crypto
    {
        /// <summary>
        /// Used to generate password hash with set parameters in <see cref="Credentials"/> class.
        /// </summary>
        /// <param name="cred">Credentials to be used to generate argon2 hash for these specific credentials.</param>
        public static void HashPasswordArgon2i(Credentials cred)
        {
            string salt;
            if (cred.Salt == string.Empty)
            {
				cred.SecureRandom = RandomNumberGenerator.GetBytes(16).ToB64String();
				salt = cred.PseudoRandomConstant + cred.SecureRandom;
			}
            else
            {
                salt = cred.Salt;
            }

            // Set the appropriate configuration for hashing PIN code
            Argon2Config argonConfig = new()
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

        /// Generating Built In EC GF(p) Parameters using SEC_secp521r1 elliptic curve
        public static ECDomainParameters ECBuiltInDomainParams()
        {
            const string SEC_SECP521R1 = "secp521r1";
			X9ECParameters ecParams = ECNamedCurveTable.GetByName(SEC_SECP521R1);
            return new ECDomainParameters(
                ecParams.Curve, ecParams.G, ecParams.N, ecParams.H, ecParams.GetSeed());
        }

        /// EC Diffie-Hellman Key Pair Generation
        public static AsymmetricCipherKeyPair GenerateECDHKeyPair(ECDomainParameters ecParams)
        {
            ECKeyGenerationParameters ecKeyGenParams =
                new ECKeyGenerationParameters(ecParams, new SecureRandom());
            ECKeyPairGenerator ecKeyPairGen = new ECKeyPairGenerator();
            ecKeyPairGen.Init(ecKeyGenParams);
            AsymmetricCipherKeyPair ecKeyPair = ecKeyPairGen.GenerateKeyPair();
            return ecKeyPair;
        }

        /// EC Diffie-Hellman Key Agreement
        public static Org.BouncyCastle.Math.BigInteger PartyABasicAgreement(ECPrivateKeyParameters privateKeyPartyA, ECPrivateKeyParameters publicKeyPartyB)
        {
            ECDHCBasicAgreement keyAgreement = new ECDHCBasicAgreement();
            keyAgreement.Init(privateKeyPartyA);

            Org.BouncyCastle.Math.BigInteger secret = keyAgreement.CalculateAgreement(publicKeyPartyB);

            return secret;
        }

        /// EC Diffie-Hellman Key Agreement
        public static Org.BouncyCastle.Math.BigInteger PartyBBasicAgreement(ECPrivateKeyParameters privateKeyPartyB, ECPrivateKeyParameters publicKeyPartyA)
        {
            ECDHCBasicAgreement keyAgreementParty = new ECDHCBasicAgreement();
            keyAgreementParty.Init(privateKeyPartyB);

            Org.BouncyCastle.Math.BigInteger secret = keyAgreementParty.CalculateAgreement(publicKeyPartyA);

            return secret;
        }
    }
}
