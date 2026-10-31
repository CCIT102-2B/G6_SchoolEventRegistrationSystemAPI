namespace EventRegistrationAPI.Models;


public class SchoolEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; }      // e.g. 2026-11-05
    public TimeOnly Time { get; set; }      // e.g. 08:00
    public string Venue { get; set; } = "";
    public int Capacity { get; set; }       // maximum number of participants
    public string Description { get; set; } = "";
}
