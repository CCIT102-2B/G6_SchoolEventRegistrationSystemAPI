namespace G6_SchoolEventRegistrationSystemAPI.Models;

public class Registration
{
    public int Id { get; set; }
    public string Code { get; set; } = ""; 
    public int EventId { get; set; }
    public string StudentId { get; set; } = "";
    public DateTime RegisteredAt { get; set; }
}