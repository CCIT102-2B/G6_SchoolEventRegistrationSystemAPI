namespace EventRegistrationAPI.Models;


public class Registration
{
    public int Id { get; set; }
    public string Code { get; set; } = "";           // the confirmation code, e.g. K7M2QX
    public int EventId { get; set; }                 // points to SchoolEvent.Id
    public string StudentId { get; set; } = "";      // points to Student.StudentId
    public DateTime RegisteredAt { get; set; }
}
