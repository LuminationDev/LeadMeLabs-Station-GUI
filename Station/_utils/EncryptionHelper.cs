using System;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Linq;

namespace Station
{
    public static class EncryptionHelper
    {
        // This key is used for decrypting the nodejs encryption only
        private static readonly string secretKey = "VMALkZE0qYMuPZN4N6QbJOZxQL22Rvzf";

        // This constant is used to determine the keysize of the encryption algorithm in bits.
        // We divide this by 8 within the code below to get the equivalent number of bytes.
        private const int Keysize = 128;

        // This constant determines the number of iterations for the password bytes generation function.
        private const int DerivationIterations = 1000;

        public static string Encrypt(string plainText, string passPhrase)
        {
            if(string.IsNullOrEmpty(plainText)) throw new ArgumentException(nameof(plainText));
            if(string.IsNullOrEmpty(passPhrase)) throw new ArgumentException(nameof(passPhrase));

            string encrypted = "";
            if (plainText.Length % 32 != 0)
            {
                int requiredPadding = 32 - (plainText.Length % 32);
                for (int i = 0; i < requiredPadding; i++)
                {
                    plainText += "_";
                }
            }
            for (int i = 0; i < plainText.Length; i += 32)
            {
                int substringLength = 32;
                if (plainText.Length < i + 32)
                {
                    substringLength = plainText.Length - i;
                }
                encrypted += Encrypt32(plainText.Substring(i, substringLength), passPhrase);
            }

            return encrypted;
        }

        private static string? Encrypt32(string plainText, string passPhrase)
        {
            // Salt and IV is randomly generated each time, but is preprended to encrypted cipher text
            // so that the same Salt and IV values can be used when decrypting.  
            var saltStringBytes = Generate128BitsOfRandomEntropy();
            var ivStringBytes = Generate128BitsOfRandomEntropy();
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = Aes.Create("AesManaged"))
                {
                    if (symmetricKey == null) return null;

                    symmetricKey.BlockSize = 128;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cryptoStream.FlushFinalBlock();
                                // Create the final bytes as a concatenation of the random salt bytes, the random iv bytes and the cipher bytes.
                                var cipherTextBytes = saltStringBytes;
                                cipherTextBytes = cipherTextBytes.Concat(ivStringBytes).ToArray();
                                cipherTextBytes = cipherTextBytes.Concat(memoryStream.ToArray()).ToArray();
                                memoryStream.Close();
                                cryptoStream.Close();
                                return Convert.ToBase64String(cipherTextBytes);
                            }
                        }
                    }
                }
            }
        }

        public static string Decrypt(string cipherText, string passPhrase)
        {
            if (string.IsNullOrEmpty(cipherText)) throw new ArgumentException(nameof(cipherText));
            if (string.IsNullOrEmpty(passPhrase)) throw new ArgumentException(nameof(passPhrase));

            string decrypted = "";
            for (int i = 0; i < cipherText.Length; i += 108)
            {
                int substringLength = 108;
                if (cipherText.Length < i + 108)
                {
                    substringLength = cipherText.Length - i;
                }

                decrypted += EncryptionHelper.Decrypt108(cipherText.Substring(i, substringLength), passPhrase);
            }

            return decrypted.Trim('_');
        }

        private static string? Decrypt108(string cipherText, string passPhrase)
        {
            // Get the complete stream of bytes that represent:
            // [32 bytes of Salt] + [32 bytes of IV] + [n bytes of CipherText]
            var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
            // Get the saltbytes by extracting the first 32 bytes from the supplied cipherText bytes.
            var saltStringBytes = cipherTextBytesWithSaltAndIv.Take(Keysize / 8).ToArray();
            // Get the IV bytes by extracting the next 32 bytes from the supplied cipherText bytes.
            var ivStringBytes = cipherTextBytesWithSaltAndIv.Skip(Keysize / 8).Take(Keysize / 8).ToArray();
            // Get the actual cipher text bytes by removing the first 64 bytes from the cipherText string.
            var cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip((Keysize / 8) * 2).Take(cipherTextBytesWithSaltAndIv.Length - ((Keysize / 8) * 2)).ToArray();

            using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = Aes.Create("AesManaged"))
                {
                    if (symmetricKey == null) return null;

                    symmetricKey.BlockSize = 128;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream(cipherTextBytes))
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                var plainTextBytes = new byte[cipherTextBytes.Length];
                                var decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                                memoryStream.Close();
                                cryptoStream.Close();
                                return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generate a random byte array of characters of length 16.
        /// </summary>
        /// <returns>A randomised byte array of length 16</returns>
        public static byte[] Generate128BitsOfRandomEntropy()
        {
            var randomBytes = new byte[16]; // 16 Bytes will give us 128 bits.
            using (var rngCsp = RandomNumberGenerator.Create())
            {
                // Fill the array with cryptographically secure random bytes.
                rngCsp.GetBytes(randomBytes);
            }
            return randomBytes;
        }

        /// <summary>
        /// Encrypt data that can be read by the nodejs launcher program.
        /// </summary>
        /// <param name="data">A string of data that is to be encrypted.</param>
        /// <returns>An encrypted string that can be written to a file.</returns>
        public static string EncryptNode(string data)
        {
            byte[] key = Encoding.UTF8.GetBytes(secretKey);
            byte[] iv = Generate128BitsOfRandomEntropy(); // generate a random initialization vector

            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] encryptedBytes;

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(dataBytes, 0, dataBytes.Length);
                        cs.FlushFinalBlock();
                        encryptedBytes = ms.ToArray();
                    }
                }
            }

            string encryptedData = BitConverter.ToString(iv).Replace("-", "") + BitConverter.ToString(encryptedBytes).Replace("-", "");
            return encryptedData;
        }

        /// <summary>
        /// Decrypt the data when encrypted through the nodejs launcher program.
        /// </summary>
        /// <param name="encryptedData">A string of encrypted data to decipher.</param>
        /// <returns>A decrypted string.</returns>
        public static string DecryptNode(string encryptedData)
        {
            byte[] key = Encoding.UTF8.GetBytes(secretKey);
            byte[] iv = HexStringToByteArray(encryptedData.Substring(0, 32));

            string encrypted = encryptedData.Substring(32);

            byte[] encryptedBytes = HexStringToByteArray(encrypted);
            byte[] decryptedBytes;

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream(encryptedBytes))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader reader = new StreamReader(cs))
                        {
                            decryptedBytes = Encoding.UTF8.GetBytes(reader.ReadToEnd());
                        }
                    }
                }
            }

            string decrypted = Encoding.UTF8.GetString(decryptedBytes);
            return decrypted;
        }

        /// <summary>
        /// Convert a string of hex characters into a byte array.
        /// </summary>
        /// <param name="hexString">A hexadecimal string</param>
        /// <returns>A byte array of characters</returns>
        private static byte[] HexStringToByteArray(string hexString)
        {
            int numBytes = hexString.Length / 2;
            byte[] bytes = new byte[numBytes];
            for (int i = 0; i < numBytes; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}
