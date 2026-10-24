namespace  G6_SchoolEventRegistrationSystemAPI.Models;


public class SchoolEvent
{
    public  int Id { get; set; }
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; } 
    public TimeOnly Time { get; set; } 
    public string Venue { get; set; } = "";
    public int Capacity { get; set; } 
    public string Description { get; set; } = "";
}
