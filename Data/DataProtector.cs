using System.Security.Cryptography;
using System.Text;

namespace EventRegistrationAPI.Data;

/// <summary>
/// Encrypts and decrypts everything saved in data.json (usernames, names, emails, contact numbers, events, registrations...).
/// It uses AES-256-GCM: the file is unreadable without the secret key, and any change to the file by someone else is detected.
///
/// Where the secret key comes from:
///   1. the DATA_KEY setting (an environment variable) if it is set: any long random text, at least 20 characters. Best for hosting.
///   2. otherwise a file called data.key, next to data.json. It is created the first time the app runs.
///
/// KEEP THE KEY SAFE: never upload data.key to GitHub or share it. If the key is lost, data.json can no longer be opened
/// (delete data.json to start again with fresh sample data).
///
/// Passwords are a separate matter: they are never stored in a form that can be turned back into the password.
/// Only a salted one-way hash is saved (see Auth.cs). They end up inside this encrypted file as well.
/// </summary>
public class DataProtector
{
    /// <summary>An encrypted data.json starts with this text. An old, readable data.json starts with "{".</summary>
    public const string Prefix = "ENCv1:";

    private static readonly byte[] Context = Encoding.UTF8.GetBytes("school-events-data-v1");
    private readonly byte[] _key;

    private DataProtector(byte[] key) { _key = key; }

    public static bool LooksEncrypted(string fileText) => fileText.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Finds (or, the very first time, creates) the secret key for this data file.</summary>
    public static DataProtector ForFile(string dataFile)
    {
        var fromSetting = Environment.GetEnvironmentVariable("DATA_KEY")?.Trim();
        if (!string.IsNullOrEmpty(fromSetting))
        {
            if (fromSetting.Length < 20)
                throw new InvalidOperationException("DATA_KEY is too short. Use at least 20 characters of random text.");
            // turns the text into a 256-bit key
            return new DataProtector(Rfc2898DeriveBytes.Pbkdf2(fromSetting, Encoding.UTF8.GetBytes("school-events-key-salt-v1"),
                                                               100_000, HashAlgorithmName.SHA256, 32));
        }

        string keyFile = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dataFile))!, "data.key");
        if (File.Exists(keyFile))
        {
            try
            {
                var key = Convert.FromBase64String(File.ReadAllText(keyFile).Trim());
                if (key.Length == 32) return new DataProtector(key);
            }
            catch (FormatException) { /* handled below */ }
            throw new InvalidOperationException($"The key file {keyFile} is damaged. Put back your original data.key (or set DATA_KEY). " +
                                                "To start again with fresh data, delete data.json and data.key.");
        }

        // No key yet. Never invent a NEW key for data that is already encrypted: it could never be opened with it.
        if (File.Exists(dataFile) && LooksEncrypted(File.ReadAllText(dataFile)))
            throw new InvalidOperationException("data.json is encrypted but its key is missing. Put the original data.key back next to data.json " +
                                                "(or set DATA_KEY to the original key). To start again with fresh data, delete data.json.");

        var newKey = RandomNumberGenerator.GetBytes(32);
        File.WriteAllText(keyFile, Convert.ToBase64String(newKey));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(keyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);   // only the owner may read it
        return new DataProtector(newKey);
    }

    /// <summary>Turns readable text into  ENCv1:  followed by the encrypted text.</summary>
    public string Encrypt(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(12);          // a new random value every time we save
        var cipher = new byte[plain.Length];
        var tag = new byte[16];                                    // the "seal" that detects tampering
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag, Context);
        return Prefix + Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    /// <summary>The opposite of Encrypt. Throws a clear error when the key is wrong or the file was changed.</summary>
    public string Decrypt(string fileText)
    {
        try
        {
            var all = Convert.FromBase64String(fileText[Prefix.Length..].Trim());
            var nonce = all[..12];
            var tag = all[12..28];
            var cipher = all[28..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(nonce, cipher, tag, plain, Context);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception e) when (e is CryptographicException or FormatException or ArgumentException)
        {
            throw new InvalidOperationException("data.json could not be decrypted: the key (data.key or DATA_KEY) is not the one it was saved with, " +
                                                "or the file was changed. Put back the original key. To start again with fresh data, delete data.json.");
        }
    }
}
