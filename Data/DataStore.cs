using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using EventRegistrationAPI.Models;

namespace EventRegistrationAPI.Data;

/// <summary>
/// Our simple "database". The four lists live in memory and Save() writes them to data.json (encrypted).
/// (Later you can replace this class with Entity Framework and a real database.)
/// Controllers use  lock (_db) { ... }  so two requests never change the lists at the same moment.
/// </summary>
public class DataStore
{
    private readonly string _file;
    private readonly DataProtector _protector;   // NEW: encrypts what we write to data.json
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<SchoolEvent> Events { get; private set; } = new();
    public List<Student> Students { get; private set; } = new();
    public List<Registration> Registrations { get; private set; } = new();
    public List<Account> Accounts { get; private set; } = new();     // NEW: logins for students, organizers, master

    // NEW: who is logged in right now. token -> username. Kept in memory only (not saved in data.json).
    public ConcurrentDictionary<string, string> Sessions { get; } = new();

    public DataStore(string filePath)
    {
        _file = filePath;
        _protector = DataProtector.ForFile(filePath);   // the secret key (see Data/DataProtector.cs)
        bool loaded = false;

        // Load what was saved last time, if data.json exists
        if (File.Exists(_file))
        {
            try
            {
                string text = File.ReadAllText(_file);
                // An encrypted file is decrypted first. If the key is wrong this throws and the app refuses to start,
                // so your data is never overwritten by mistake. An OLD readable data.json is accepted too:
                // the Save() at the end of this constructor turns it into an encrypted file.
                if (DataProtector.LooksEncrypted(text)) text = _protector.Decrypt(text);
                var saved = JsonSerializer.Deserialize<SavedData>(text);
                if (saved != null)
                {
                    Events = saved.Events;
                    Students = saved.Students;
                    Registrations = saved.Registrations;
                    Accounts = saved.Accounts;
                    loaded = true;
                }
            }
            catch (JsonException)
            {
                // the file was unreadable: fall through and start fresh
            }
        }

        // First run: start with sample events
        if (!loaded) Events = SampleEvents();

        // An OLD data.json has no accounts yet. Create the mock accounts so login works.
        // (Old events and registrations are kept as they are; the new fields get safe defaults.)
        if (Accounts.Count == 0) Accounts = SeedAccounts();

        Save();
    }

    /// <summary>Writes everything to data.json. Call it after every change.</summary>
    public void Save()
    {
        var data = new SavedData { Events = Events, Students = Students, Registrations = Registrations, Accounts = Accounts };
        string encrypted = _protector.Encrypt(JsonSerializer.Serialize(data, JsonOptions));
        // write to a temporary file first, so a crash in the middle of saving can never leave a half-written data.json
        string temp = _file + ".tmp";
        File.WriteAllText(temp, encrypted);
        File.Move(temp, _file, overwrite: true);
    }

    // The shape of data.json
    private class SavedData
    {
        public List<SchoolEvent> Events { get; set; } = new();
        public List<Student> Students { get; set; } = new();
        public List<Registration> Registrations { get; set; } = new();
        public List<Account> Accounts { get; set; } = new();
    }

    private static List<SchoolEvent> SampleEvents()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new List<SchoolEvent>
        {
            // Events 1 to 3: the original samples. Notice they only set the original fields,
            // so they are college-wide and first-come-first-served (the new defaults).
            new() { Id = 1, Title = "Intramurals Opening Ceremony", Date = today.AddDays(7), Time = new TimeOnly(8, 0),
                    Venue = "School gymnasium", Capacity = 200,
                    Description = "Parade of teams and opening program. Wear your team color." },
            new() { Id = 2, Title = "Science Fair", Date = today.AddDays(14), Time = new TimeOnly(9, 0),
                    Venue = "Science building", Capacity = 40,
                    Description = "Present your project to the judges. Bring your display board." },
            new() { Id = 3, Title = "Career Orientation Talk", Date = today.AddDays(30), Time = new TimeOnly(13, 30),
                    Venue = "Auditorium", Capacity = 5,
                    Description = "Guest speakers share what it is like to work in different careers." },

            // Events 4 and 5 show the new features.
            // CCIT invites COE too, and shares the seats with quotas per program and year level (Mode A).
            new() { Id = 4, Title = "CCIT Tech Summit", Date = today.AddDays(10), Time = new TimeOnly(9, 0),
                    Venue = "CCIT Laboratory 1", Capacity = 7,
                    Description = "Talks and demos for computing and engineering students.",
                    Audience = Audiences.Inter, AllowedDepartments = new() { "CCIT", "COE" }, OwnerDepartment = "CCIT",
                    CapacityMode = CapacityModes.Quota,
                    Quotas = new()
                    {
                        new Quota { Program = "BSIT", YearLevel = 1, Slots = 3 },
                        new Quota { Program = "BSCS", YearLevel = 3, Slots = 2 },
                        new Quota { Program = "BSCPE", YearLevel = 2, Slots = 2 },
                    } },
            // CBMA only, one overall limit (Mode B).
            new() { Id = 5, Title = "CBMA Business Fair", Date = today.AddDays(12), Time = new TimeOnly(10, 0),
                    Venue = "Function hall", Capacity = 60,
                    Description = "Booths and mock pitches by business and accountancy students.",
                    Audience = Audiences.Department, AllowedDepartments = new() { "CBMA" }, OwnerDepartment = "CBMA",
                    CapacityMode = CapacityModes.Open },
        };
    }

    /// <summary>The mock accounts for testing. EVERY account uses the password:  password123</summary>
    private static List<Account> SeedAccounts()
    {
        const string pw = "password123";
        return new List<Account>
        {
            // Master Admin: global control over all events and departments
            Auth.NewAccount("FAITH", pw, "FAITH (Master Admin)", Roles.Master),

            // Department heads / organizers: one per college
            Auth.NewAccount("cbma_admin", pw, "CBMA Department Head", Roles.Organizer, "CBMA"),
            Auth.NewAccount("ccit_admin", pw, "CCIT Department Head", Roles.Organizer, "CCIT"),
            Auth.NewAccount("coe_admin", pw, "COE Department Head", Roles.Organizer, "COE"),
            Auth.NewAccount("case_admin", pw, "CASE Department Head", Roles.Organizer, "CASE"),
            Auth.NewAccount("chc_admin", pw, "CHC Department Head", Roles.Organizer, "CHC"),
            Auth.NewAccount("ccj_admin", pw, "CCJ Department Head", Roles.Organizer, "CCJ"),

            // Students: each tied to a department, program and year level
            Auth.NewAccount("student_ccit_1", pw, "Juan Dela Cruz", Roles.Student, "CCIT", "BSIT", 1, "2026-1001", "juan.delacruz@example.com", "09171234501"),
            Auth.NewAccount("student_ccit_3", pw, "Maria Santos", Roles.Student, "CCIT", "BSCS", 3, "2024-1003", "maria.santos@example.com", "09171234503"),
            Auth.NewAccount("student_cabea_3", pw, "Ana Reyes", Roles.Student, "CBMA", "BSA", 3, "2024-2003", "ana.reyes@example.com", "09171234533"),
        };
    }

    // ---------- small helpers used by the controllers ----------

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Trims spaces and cuts the text to a maximum length.</summary>
    public static string Clean(string? text, int max = 200)
    {
        var t = (text ?? "").Trim();
        return t.Length > max ? t[..max] : t;
    }

    public static bool IsEmail(string email) => Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

    /// <summary>
    /// Checks a Philippine mobile number and returns it in one format (09171234567), or null if it is not valid.
    /// Accepts 09171234567, 0917 123 4567, 0917-123-4567 and +639171234567.
    /// </summary>
    public static string? NormalizePhone(string? raw)
    {
        var d = Regex.Replace(raw ?? "", @"[\s\-()]", "");
        if (d.StartsWith("+63")) d = "0" + d[3..];
        return Regex.IsMatch(d, @"^09\d{9}$") ? d : null;
    }

    /// <summary>1 gives "1st", 2 gives "2nd", and so on.</summary>
    public static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => n + "th" };

    /// <summary>Makes a random confirmation code like K7M2QX.</summary>
    public static string RandomCode(int length = 6)
    {
        const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, length).Select(_ => letters[Random.Shared.Next(letters.Length)]).ToArray());
    }
}
