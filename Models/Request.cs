namespace EventRegistrationAPI.Models;


public class CreateEventRequest
{
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public string Venue { get; set; } = "";
    public int Capacity { get; set; }
    public string Description { get; set; } = "";
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
