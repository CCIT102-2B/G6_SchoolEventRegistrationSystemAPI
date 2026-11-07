namespace EventRegistrationAPI.Models;

/// <summary>
/// STUDENT INFORMATION. A student is saved once and can register for many events.
/// StudentId is the school ID number (for example 2026-0123) and acts as the key.
/// </summary>
public class Student
{
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string GradeSection { get; set; } = "";   // e.g. "BSIT - 1st year"
    public string Email { get; set; } = "";
    public string ContactNumber { get; set; } = "";   // NEW: copied from the student's account when they register

    // NEW: filled in from the student's login account when they register
    public string Department { get; set; } = "";
    public string Program { get; set; } = "";
    public int YearLevel { get; set; }
}
