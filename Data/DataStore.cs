using System.Text.Json;
using System.Text.RegularExpressions;
using EventRegistrationAPI.Models;

namespace EventRegistrationAPI.Data;

/// <summary>
/// Our simple "database". The three lists live in memory and Save() writes them to data.json.
/// (Later you can replace this class with Entity Framework and a real database.)
/// Controllers use  lock (_db) { ... }  so two requests never change the lists at the same moment.
/// </summary>
public class DataStore
{
    private readonly string _file;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<SchoolEvent> Events { get; private set; } = new();
    public List<Student> Students { get; private set; } = new();
    public List<Registration> Registrations { get; private set; } = new();

    public DataStore(string filePath)
    {
        _file = filePath;

        // Load what was saved last time, if data.json exists
        if (File.Exists(_file))
        {
            try
            {
                var saved = JsonSerializer.Deserialize<SavedData>(File.ReadAllText(_file));
                if (saved != null)
                {
                    Events = saved.Events;
                    Students = saved.Students;
                    Registrations = saved.Registrations;
                    return;
                }
            }
            catch (JsonException)
            {
                // the file was unreadable: fall through and start fresh
            }
        }

        // First run: start with 3 sample events
        Events = SampleEvents();
        Save();
    }

    /// <summary>Writes everything to data.json. Call it after every change.</summary>
    public void Save()
    {
        var data = new SavedData { Events = Events, Students = Students, Registrations = Registrations };
        File.WriteAllText(_file, JsonSerializer.Serialize(data, JsonOptions));
    }

    // The shape of data.json
    private class SavedData
    {
        public List<SchoolEvent> Events { get; set; } = new();
        public List<Student> Students { get; set; } = new();
        public List<Registration> Registrations { get; set; } = new();
    }

    private static List<SchoolEvent> SampleEvents()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new List<SchoolEvent>
        {
            new() { Id = 1, Title = "Intramurals Opening Ceremony", Date = today.AddDays(7), Time = new TimeOnly(8, 0),
                    Venue = "School gymnasium", Capacity = 200,
                    Description = "Parade of teams and opening program. Wear your team color." },
            new() { Id = 2, Title = "Science Fair", Date = today.AddDays(14), Time = new TimeOnly(9, 0),
                    Venue = "Science building", Capacity = 40,
                    Description = "Present your project to the judges. Bring your display board." },
            new() { Id = 3, Title = "Career Orientation Talk", Date = today.AddDays(30), Time = new TimeOnly(13, 30),
                    Venue = "Auditorium", Capacity = 5,
                    Description = "Guest speakers share what it is like to work in different careers." },
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

    /// <summary>Makes a random confirmation code like K7M2QX.</summary>
    public static string RandomCode(int length = 6)
    {
        const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, length).Select(_ => letters[Random.Shared.Next(letters.Length)]).ToArray());
    }
}
