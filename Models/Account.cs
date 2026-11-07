namespace EventRegistrationAPI.Models;

/// <summary>The three kinds of users. They are saved as text in data.json.</summary>
public static class Roles
{
    public const string Student = "student";
    public const string Organizer = "organizer";   // a department head / department admin
    public const string Master = "master";         // the Master Admin (username FAITH)
}

/// <summary>
/// A LOGIN ACCOUNT. Students, department organizers and the Master Admin all use this class;
/// the Role field says which one it is. The password is never stored as plain text:
/// we keep a random Salt and the PasswordHash (see Data/Auth.cs).
/// </summary>
public class Account
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = Roles.Student;
    public string Department { get; set; } = "";     // e.g. "CCIT". Empty for the Master Admin.
    public string Program { get; set; } = "";        // students only, e.g. "BSIT"
    public int YearLevel { get; set; }               // students only, 1 to 5
    public string StudentId { get; set; } = "";      // students only, e.g. 2026-1001
    public string Email { get; set; } = "";          // students only (the student email)

    // NEW: filled in when a student creates their own account (sign-up). Seeded demo accounts may leave them empty.
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string ContactNumber { get; set; } = "";  // mobile number, saved like 09171234567
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
