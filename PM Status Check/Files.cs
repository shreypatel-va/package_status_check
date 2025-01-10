using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;

namespace PM_Status_Check
{
    internal class Files
    {


        private static string? _DataDirectory { get; set; } = null;

        internal static string GetDataDirectory()
        {

            if (!string.IsNullOrEmpty(_DataDirectory)) return _DataDirectory;

            _DataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Log.Information("Data Directory Set to {_DataDirectory}", _DataDirectory);
            return _DataDirectory;
        }        
        internal static string GetAppDir()
        {
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IDT");
            System.IO.Directory.CreateDirectory(filePath);
            return filePath;
        }

        internal static string GetDocumentCache()
        {
            var filePath = GetAppDir();
            filePath = Path.Combine(filePath, "DocumentCache");
            System.IO.Directory.CreateDirectory(filePath);
            return filePath;
        }

        internal static bool CleanupEfolderDocs(IProgress<string>? progress = null)
        {
            var fileLocation = Path.Combine(GetDocumentCache(), "efolder");
            DateTime cutoff = DateTime.Now.AddDays(-30);

            return DeleteOldItems(fileLocation, cutoff, progress);
        }
        internal static bool DeleteOldItems(string filePath, DateTime cutoff, IProgress<string>? progress = null)
        {
            bool ok = true;

            foreach (FileInfo file in new DirectoryInfo(filePath).GetFiles("*.*"))
            {
                if (progress != null) progress.Report($"Deleting file {file.Name}");
                if (!file.Exists) continue;
                if (file.LastAccessTime < cutoff)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        ok = false;
                    }
                }
            }

            return ok;
        }
    }

    public class Crc32
    {
        private const UInt32 s_generator = 0xED888320;
        private readonly UInt32[] m_checksumTable;

        public Crc32()
        {
            m_checksumTable = Enumerable.Range(0, 256).Select(i =>
            {
                var tableEntry = (uint)i;
                for (var j = 0; j < 8; j++)
                {
                    tableEntry = ((tableEntry & 1) != 0)
                        ? (s_generator ^ (tableEntry >> 1))
                        : (tableEntry >> 1);
                }
                return tableEntry;
            }).ToArray();
        }

        public UInt32 Get<T>(IEnumerable<T> byteStream)
        {

            return ~byteStream.Aggregate(0xFFFFFFFF, (checksumRegister, currentByte) =>
                    (m_checksumTable[(checksumRegister & 0xFF) ^ Convert.ToByte(currentByte)] ^ (checksumRegister >> 8)));

        }
    }

    public class Encryption
    {
        public static void TestEncryption()
        {
            string plaintext = "Hi this is a test";
            string password = "Pablo";
            var testEnc = new Encryption(password);
            //Log.Debug("Before");
            //Log.Debug(plaintext);
            testEnc.EncryptToFile(plaintext, Files.GetAppDir() + @"\TestEnc.enc");
            var outText = testEnc.DecryptFromFile(Files.GetAppDir() + @"\TestEnc.enc");
            //Log.Debug("After");
            //Log.Debug(outText);

        }
        public static string GetRegistry(string valueName)
        {
            var enc = new Encryption();
            return enc.DecryptFromRegistry(valueName);
        }
        public static void SetRegistry(string valueName, string value)
        {
            var enc = new Encryption();
            enc.EncryptToRegistry(value, valueName);
        }

        private static string UserKey { get; set; } = null;

        private static string GetKey()
        {
            if (UserKey == null)
            {
                RegistryKey rootKey = Registry.CurrentUser;
                using (RegistryKey rk = rootKey.OpenSubKey(@"Software\BVA\IDT\Tokens", false))
                {
                    if (rk != null)
                    {
                        var res = rk.GetValue("LocalKey");
                        if (res != null)
                        {
                            UserKey = res.ToString();
                            return UserKey;
                        }
                    }
                }
                Random rnd = new Random();
                char[] chars = new char[64];
                while (UserKey == null || UserKey.Length < 32)
                {
                    for (var i = 0; i < 64; i++)
                    {
                        chars[i] = (char)rnd.Next(0, 128);
                    }
                    UserKey = Regex.Replace(new string(chars), @"\W", "");
                }
                
                using (RegistryKey rk = rootKey.CreateSubKey(@"Software\BVA\IDT\Tokens"))
                {
                    rk.SetValue("LocalKey", UserKey, RegistryValueKind.String);
                }
            }
            return UserKey;
        }


        public string Password { get; set; }
        private byte[] PasswordSalt { get; set; } = new byte[] { 147, 119, 135, 23, 24, 79, 115, 208 };
        private Rfc2898DeriveBytes PasswordBytes { get; set; }

        public Encryption()
        {
            Password = GetKey();
            PasswordBytes = DeriveKey(Password, PasswordSalt);
        }

        public Encryption(string password)
        {
            Password = password;
            PasswordBytes = DeriveKey(password, PasswordSalt);
        }

        public Rfc2898DeriveBytes DeriveKey(string password, byte[] salt)
            => new Rfc2898DeriveBytes(password, salt, 1000, HashAlgorithmName.SHA256);

        public byte[] Encrypt(string plaintext)
        {
            PasswordBytes.Reset();
            var aes = new AesCng();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Key = PasswordBytes.GetBytes(32);
            aes.IV = PasswordBytes.GetBytes(16);
            var encryptor = aes.CreateEncryptor();
            byte[] encrypted;
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plaintext);
                    }
                    encrypted = msEncrypt.ToArray();
                }
            }
            return encrypted;
        }

        public void EncryptToRegistry(string plaintext, string valueName)
        {
            var bytes = Encrypt(plaintext);
            var base64Bytes = Convert.ToBase64String(bytes);
            RegistryKey rootKey = Registry.CurrentUser;
            using (RegistryKey rk = rootKey.CreateSubKey(@"Software\BVA\IDT\Encrypted"))
            {
                rk.SetValue(valueName, base64Bytes, RegistryValueKind.String);
            }
        }

        public string EncryptToBase64(string plaintext)
        {
            var bytes = Encrypt(plaintext);
            return Convert.ToBase64String(bytes);
        }

        public void EncryptToFile(string plaintext, string filename)
        {
            PasswordBytes.Reset();
            var aes = new AesCng();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Key = PasswordBytes.GetBytes(32);
            aes.IV = PasswordBytes.GetBytes(16);
            var encryptor = aes.CreateEncryptor();
            using (FileStream fsEncrypt = File.Create(filename))
            {
                using (CryptoStream csEncrypt = new CryptoStream(fsEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plaintext);
                    }
                }
            }
        }

        public async Task EncryptToFileAsync(string plaintext, string filename)
        {
            PasswordBytes.Reset();
            var aes = new AesCng();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Key = PasswordBytes.GetBytes(32);
            aes.IV = PasswordBytes.GetBytes(16);
            var encryptor = aes.CreateEncryptor();
            using (FileStream fsEncrypt = File.Create(filename))
            {
                using (CryptoStream csEncrypt = new CryptoStream(fsEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        await swEncrypt.WriteAsync(plaintext);
                    }
                }
            }
        }

        public string Decrypt(byte[] cipherText)
        {
            PasswordBytes.Reset();
            var aes = new AesCng();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Key = PasswordBytes.GetBytes(32);
            aes.IV = PasswordBytes.GetBytes(16);
            var decryptor = aes.CreateDecryptor();
            string decrypted;
            using (MemoryStream msDecrypt = new MemoryStream(cipherText))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        decrypted = srDecrypt.ReadToEnd();
                    }
                }
            }
            return decrypted;
        }

        public string DecryptFromRegistry(string valueName)
        {
            RegistryKey rootKey = Registry.CurrentUser;
            using (RegistryKey rk = rootKey.OpenSubKey(@"Software\BVA\IDT\Encrypted", false))
            {
                if (rk != null)
                {
                    var res = rk.GetValue(valueName);
                    if (res != null)
                    {
                        var bytes = Convert.FromBase64String(res.ToString());
                        return Decrypt(bytes);
                    }
                }
            }
            return null;
        }

        public string DecryptFromBase64(string encrypted)
        {
            var bytes = Convert.FromBase64String(encrypted);
            return Decrypt(bytes);
        }


        public string DecryptFromFile(string filename)
        {
            PasswordBytes.Reset();
            var aes = new AesCng();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Key = PasswordBytes.GetBytes(32);
            aes.IV = PasswordBytes.GetBytes(16);
            var decryptor = aes.CreateDecryptor();
            string decrypted;
            using (FileStream fsDecrypt = File.Open(filename, FileMode.Open))
            {
                using (CryptoStream csDecrypt = new CryptoStream(fsDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        decrypted = srDecrypt.ReadToEnd();
                    }
                }
            }
            return decrypted;
        }

        public async Task<string> DecryptFromFileAsync(string filename)
        {
            PasswordBytes.Reset();
            var aes = new AesCng();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Key = PasswordBytes.GetBytes(32);
            aes.IV = PasswordBytes.GetBytes(16);
            var decryptor = aes.CreateDecryptor();
            string decrypted;
            using (FileStream fsDecrypt = File.Open(filename, FileMode.Open))
            {
                using (CryptoStream csDecrypt = new CryptoStream(fsDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        decrypted = await srDecrypt.ReadToEndAsync();
                    }
                }
            }
            return decrypted;
        }

    }

    public class ImageEdit
    {
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            Rectangle destRect = new(0, 0, width, height);
            Bitmap destBmp = new(width, height);

            destBmp.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destBmp))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destBmp;
        }
    }

}
