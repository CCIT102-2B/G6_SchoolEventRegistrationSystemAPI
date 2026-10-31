namespace EventRegistrationAPI.Models;

public class Student
{
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string GradeSection { get; set; } = "";   // e.g. "Grade 10 - Rizal"
    public string Email { get; set; } = "";
}
