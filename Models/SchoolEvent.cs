namespace EventRegistrationAPI.Models;

/// <summary>
/// One school event, for example "Science Fair".
/// A Model describes the shape of our data. It is saved in data.json
/// and sent to the web page as JSON.
/// </summary>
public class SchoolEvent
{
    // ----- the original fields (unchanged) -----
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; }      // e.g. 2026-11-05
    public TimeOnly Time { get; set; }      // e.g. 08:00
    public string Venue { get; set; } = "";
    public int Capacity { get; set; }       // maximum number of participants (in "quota" mode: the total of all quota slots)
    public string Description { get; set; } = "";

    // ----- NEW: who can join, who owns the event, and how the seats are shared -----
    // The default values below describe an OLD event from before these fields existed:
    // college-wide, created by the Master Admin, first-come-first-served. So old data.json files still work.
    public string Audience { get; set; } = Audiences.College;                 // college | department | inter
    public List<string> AllowedDepartments { get; set; } = new() { "ALL" };   // "ALL" or department codes like "CCIT"
    public string OwnerDepartment { get; set; } = "";                         // who created it. "" = Master Admin
    public string CapacityMode { get; set; } = CapacityModes.Open;            // open | quota
    public List<Quota> Quotas { get; set; } = new();                          // used when CapacityMode is "quota"
}

/// <summary>Mode A: reserved slots for one program and one year level, e.g. BSIT 1st year: 30 slots.</summary>
public class Quota
{
    public string Program { get; set; } = "";   // e.g. "BSIT"
    public int YearLevel { get; set; }          // 1 to 5
    public int Slots { get; set; }              // how many students of that program and year may register
}

/// <summary>Who may join an event.</summary>
public static class Audiences
{
    public const string College = "college";        // every department (Master Admin only)
    public const string Department = "department";  // one department only
    public const string Inter = "inter";            // the owner's department plus invited departments
}

/// <summary>How the seats are shared.</summary>
public static class CapacityModes
{
    public const string Open = "open";     // Mode B: one overall limit, first come first served
    public const string Quota = "quota";   // Mode A: a slot limit per program and year level
}
