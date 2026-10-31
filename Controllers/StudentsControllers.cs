using EventRegistrationAPI.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistrationAPI.Controllers;

/// <summary>
/// Everything about STUDENTS (Student Information). URLs start with /api/students
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly DataStore _db;

    public StudentsController(DataStore db)
    {
        _db = db;
    }

    /// <summary>
    /// API 7: GET /api/students/2026-0123      (used by: the Registration Form)
    /// Finds a student by student ID and lists the events they registered for.
    /// The form uses it to fill in the name, grade and email of returning students.
    /// Output: { studentId, fullName, gradeSection, email, registrations: [ { code, eventTitle, date, time, venue, registeredAt } ] }
    ///         or 404 "Student not found"
    /// </summary>
    [HttpGet("{studentId}")]
    public IActionResult GetByStudentId(string studentId)
    {
        string id = DataStore.Clean(studentId, 20).ToUpper();

        lock (_db)
        {
            var student = _db.Students.FirstOrDefault(s => s.StudentId == id);
            if (student == null) return NotFound(new { error = "Student not found" });

            var registrations = (
                from r in _db.Registrations.Where(r => r.StudentId == id)
                join e in _db.Events on r.EventId equals e.Id
                select new { r.Code, eventTitle = e.Title, e.Date, e.Time, e.Venue, r.RegisteredAt }
            ).ToList();

            return Ok(new { student.StudentId, student.FullName, student.GradeSection, student.Email, registrations });
        }
    }
}
