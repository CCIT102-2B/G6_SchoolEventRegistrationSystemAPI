using EventRegistrationAPI.Data;
using EventRegistrationAPI.Models;
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
    /// API 7: GET /api/students/2026-0123      (used by: "My registrations" on the Register page)
    /// Finds a student by student ID and lists the events they registered for.
    /// NEW: you must be logged in. A student can only look up their own ID; organizers and the Master Admin can look up anyone.
    /// Output: { studentId, fullName, gradeSection, email, contactNumber, department, program, yearLevel,
    ///           registrations: [ { code, eventTitle, date, time, venue, registeredAt, status } ] }
    ///         or 404 "Student not found"
    /// </summary>
    [HttpGet("{studentId}")]
    public IActionResult GetByStudentId(string studentId)
    {
        var me = Auth.GetAccount(Request, _db);
        if (me == null) return Unauthorized(new { error = "Please log in first" });

        string id = DataStore.Clean(studentId, 20).ToUpper();
        if (me.Role == Roles.Student && !me.StudentId.Equals(id, StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, new { error = "You can only look up your own student information" });

        lock (_db)
        {
            var student = _db.Students.FirstOrDefault(s => s.StudentId == id);
            if (student == null) return NotFound(new { error = "Student not found" });

            var registrations = (
                from r in _db.Registrations.Where(r => r.StudentId == id)
                join e in _db.Events on r.EventId equals e.Id
                select new { r.Code, eventTitle = e.Title, e.Date, e.Time, e.Venue, r.RegisteredAt, r.Status }
            ).ToList();

            return Ok(new { student.StudentId, student.FullName, student.GradeSection, student.Email,
                            student.ContactNumber, student.Department, student.Program, student.YearLevel, registrations });
        }
    }
}
