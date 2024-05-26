using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using System.Security.Principal;

namespace flare_csharp
{
    public class FlareAeadCiphertext(byte[] ciphertext, byte[] nonce)
    {
        // Combined AEAD authentication tag and AES output
        public byte[] Ciphertext { get; set; } = ciphertext;
        public byte[] Nonce { get; set; } = nonce;
    }
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

        /// <summary>
        /// Generate a 32-byte from non-uniform randomness, intended for use with EC Diffie-Hellman blobs
        /// </summary>
        /// <param name="input_secret"></param>
        /// <returns></returns>
        public static byte[] DeriveBlake3(byte[] input_secret)
        {
            byte[] output = new byte[32];
            var digest = new Blake3Digest();
            digest.Init(new Blake3Parameters());

            digest.BlockUpdate(input_secret);
            digest.DoFinal(output);

            return output;
        }

        /// <summary>
        /// Encrypts a byte array plaintext to the Flare protocol's transmission format
        /// </summary>
        /// <param name="key">32-byte encryption key</param>
        /// <param name="plaintext">Variable-length input plaintext</param>
        public static FlareAeadCiphertext FlareAeadEncrypt(byte[] key, byte[] plaintext)
        {
            KeyParameter keyParam = new(key);

            IBlockCipher cipher = new AesEngine();
            int macSize = 8 * cipher.GetBlockSize();
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] associatedText = [];
            AeadParameters keyParamAead = new(keyParam, macSize, nonce, associatedText);
            GcmSivBlockCipher cipherMode = new(cipher);
            cipherMode.Init(true, keyParamAead);

            int outputSize = cipherMode.GetOutputSize(plaintext.Length);
            byte[] ciphertext = new byte[outputSize];
            int result = cipherMode.ProcessBytes(plaintext, 0, plaintext.Length, ciphertext, 0);
            cipherMode.DoFinal(ciphertext, result);
            return new FlareAeadCiphertext(ciphertext, nonce);
        }

        public static byte[] FlareAeadDecrypt(byte[] key, FlareAeadCiphertext ciphertextPackage)
        {
            KeyParameter keyParam = new(key);
            byte[] ciphertext = ciphertextPackage.Ciphertext;

            IBlockCipher cipher = new AesEngine();
            int macSize = 8 * cipher.GetBlockSize();
            byte[] nonce = ciphertextPackage.Nonce;
            byte[] associatedText = [];
            AeadParameters keyParamAead = new(keyParam, macSize, nonce, associatedText);
            GcmSivBlockCipher cipherMode = new(cipher);
            cipherMode.Init(false, keyParamAead);

            int outputSize = cipherMode.GetOutputSize(ciphertext.Length);
            byte[] plainTextData = new byte[outputSize];
            int result = cipherMode.ProcessBytes(ciphertext, 0, ciphertext.Length, plainTextData, 0);
            cipherMode.DoFinal(plainTextData, result);
            return plainTextData;
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
        public static AsymmetricCipherKeyPair GenerateECDHKeyPair()
        {
            ECDomainParameters ecParams = ECBuiltInDomainParams();
            ECKeyGenerationParameters ecKeyGenParams =
                new ECKeyGenerationParameters(ecParams, new SecureRandom());
            ECKeyPairGenerator ecKeyPairGen = new ECKeyPairGenerator();
            ecKeyPairGen.Init(ecKeyGenParams);
            AsymmetricCipherKeyPair ecKeyPair = ecKeyPairGen.GenerateKeyPair();
            return ecKeyPair;
        }

        /// EC Diffie-Hellman Key Agreement
        public static Org.BouncyCastle.Math.BigInteger PartyBasicAgreement(ECPrivateKeyParameters privateKeyPartyA, ECPublicKeyParameters publicKeyPartyB)
        {
            ECDHCBasicAgreement keyAgreement = new ECDHCBasicAgreement();
            keyAgreement.Init(privateKeyPartyA);

            Org.BouncyCastle.Math.BigInteger secret = keyAgreement.CalculateAgreement(publicKeyPartyB);

            return secret;
        }

        public static string GetDerEncodedPublicKey(AsymmetricKeyParameter publicKey)
        {
            return Convert.ToBase64String(
                SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey).GetDerEncoded()
            );
        }

        public static AsymmetricKeyParameter GetPublicKeyFromDer(string publicKey)
        {
            return PublicKeyFactory.CreateKey(
                Convert.FromBase64String(publicKey)
            );
        }

        public static string GetDerEncodedPrivateKey(AsymmetricKeyParameter privateKey)
        {
            return Convert.ToBase64String(
                PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey).GetDerEncoded()
            );
        }

        public static AsymmetricKeyParameter GetPrivateKeyFromDer(string privateKey)
        {
            return PrivateKeyFactory.CreateKey(
                Convert.FromBase64String(privateKey)
            );
        }
    }
}