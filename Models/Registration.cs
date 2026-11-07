namespace EventRegistrationAPI.Models;

/// <summary>
/// One student signing up for one event. It only stores the IDs;
/// the event and student details are looked up when needed.
/// </summary>
public class Registration
{
    public int Id { get; set; }
    public string Code { get; set; } = "";           // the confirmation code, e.g. K7M2QX
    public int EventId { get; set; }                 // points to SchoolEvent.Id
    public string StudentId { get; set; } = "";      // points to Student.StudentId
    public DateTime RegisteredAt { get; set; }

    // NEW: a copy of the student's department, program and year AT THE TIME they registered,
    // so quotas and the Attendance Manager can count and filter without extra lookups.
    public string Department { get; set; } = "";
    public string Program { get; set; } = "";
    public int YearLevel { get; set; }

    // NEW: attendance. Starts as "registered"; an organizer changes it to "checked_in" at the entrance.
    public string Status { get; set; } = RegStatus.Registered;
    public DateTime? CheckedInAt { get; set; }
}

public static class RegStatus
{
    public const string Registered = "registered";
    public const string CheckedIn = "checked_in";
}
