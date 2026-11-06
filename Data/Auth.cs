using System.Security.Cryptography;
using EventRegistrationAPI.Models;

namespace EventRegistrationAPI.Data;

/// <summary>
/// Login helpers. How logging in works in this project:
///   1. The page sends the username and password to POST /api/auth/login.
///   2. If they are right, the API makes a random TOKEN, remembers "this token = this user"
///      (DataStore.Sessions) and sends the token back.
///   3. The page sends that token in every later request, in the header  Authorization: Bearer TOKEN.
///   4. Controllers call Auth.GetAccount(Request, _db) to find out who is asking.
/// (Sessions live in memory, so everyone is logged out when the API restarts.)
/// </summary>
public static class Auth
{
    /// <summary>Scrambles a password with its salt. The same password and salt always give the same hash.</summary>
    public static string Hash(string password, string salt) =>
        Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), 100_000, HashAlgorithmName.SHA256, 32));

    public static Account NewAccount(string username, string password, string displayName, string role,
        string department = "", string program = "", int yearLevel = 0, string studentId = "", string email = "", string contactNumber = "")
    {
        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        return new Account
        {
            Username = username, DisplayName = displayName, Role = role, Department = department,
            Program = program, YearLevel = yearLevel, StudentId = studentId, Email = email, ContactNumber = contactNumber,
            Salt = salt, PasswordHash = Hash(password, salt)
        };
    }

    public static bool Verify(Account account, string password) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(Hash(password, account.Salt)),
            Convert.FromBase64String(account.PasswordHash));

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    /// <summary>Reads "Authorization: Bearer TOKEN" from the request and returns who it belongs to (or null).</summary>
    public static Account? GetAccount(HttpRequest request, DataStore db)
    {
        string header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        string token = header[7..].Trim();
        if (!db.Sessions.TryGetValue(token, out var username)) return null;
        return db.Accounts.FirstOrDefault(a => a.Username == username);
    }

    /// <summary>The safe part of an account that may be sent to the page (never the password hash).</summary>
    public static object Profile(Account a) => new
    {
        a.Username, a.DisplayName, a.Role, a.Department,
        departmentName = Catalog.FindDepartment(a.Department)?.Name ?? "",
        a.Program, a.YearLevel, a.StudentId, a.Email, a.ContactNumber, a.FirstName, a.MiddleName, a.LastName
    };
}
