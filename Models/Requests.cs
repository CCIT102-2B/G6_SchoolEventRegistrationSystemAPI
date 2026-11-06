namespace EventRegistrationAPI.Models;

// "Request" classes describe the JSON the web page SENDS to us.
// Using them means the page can only set the fields we allow (not Id, not Code).

/// <summary>Sent when adding an event (POST /api/events).</summary>
public class CreateEventRequest
{
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public string Venue { get; set; } = "";
    public int Capacity { get; set; }               // used by "open" mode
    public string Description { get; set; } = "";

    // NEW (all optional, so the old request body still works):
    public string? Audience { get; set; }                  // college | department | inter
    public List<string>? InvitedDepartments { get; set; }  // department codes, e.g. ["CCIT", "COE"]
    public string? CapacityMode { get; set; }              // open | quota
    public List<Quota>? Quotas { get; set; }               // used by "quota" mode
}

/// <summary>Sent when a student registers (POST /api/registrations): the Registration Form.</summary>
public class RegisterRequest
{
    public int EventId { get; set; }
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string GradeSection { get; set; } = "";
    public string Email { get; set; } = "";
}

/// <summary>Sent when someone logs in (POST /api/auth/login).</summary>
public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

/// <summary>Sent when an organizer checks a student in (PUT /api/attendance/{code}/status).</summary>
public class StatusRequest
{
    public string? Status { get; set; }   // registered | checked_in
}

/// <summary>Sent when a new student creates an account (POST /api/auth/register): the Sign-up form.</summary>
public class SignUpRequest
{
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }          // optional
    public string LastName { get; set; } = "";
    public string StudentNumber { get; set; } = "";  // e.g. 2026-1001
    public string Email { get; set; } = "";          // the student email
    public string ContactNumber { get; set; } = "";  // mobile number
    public string Department { get; set; } = "";     // e.g. CCIT
    public string Program { get; set; } = "";        // e.g. BSIT
    public int YearLevel { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
    public bool AgreeToTerms { get; set; }
}
