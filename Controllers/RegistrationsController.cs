using EventRegistrationAPI.Data;
using EventRegistrationAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistrationAPI.Controllers;

/// <summary>
/// Everything about REGISTRATIONS: Register for Event (Registration Form)
/// and the Registration Confirmation. URLs start with /api/registrations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RegistrationsController : ControllerBase
{
    private readonly DataStore _db;

    public RegistrationsController(DataStore db)
    {
        _db = db;
    }

    // The confirmation the student sees after registering
    private static object Confirmation(Registration r, Student s, SchoolEvent e) => new
    {
        r.Code, s.StudentId, s.FullName, s.GradeSection, s.Email,
        eventTitle = e.Title, e.Date, e.Time, e.Venue, r.RegisteredAt,
        r.Department, r.Program, r.YearLevel, r.Status
    };

    /// <summary>
    /// API 5: POST /api/registrations      (used by: Register for Event)
    /// Registers the LOGGED-IN student for one event and returns a confirmation code.
    /// Input:  { eventId, studentId, fullName, gradeSection, email }   (header  Authorization: Bearer TOKEN)
    /// Checks: you are logged in as a student (the student ID always comes from your account),
    ///         all fields filled in, valid email, the event exists and has not passed,
    ///         the event is open to your department (and has a quota for your program and year level),
    ///         it is not full (the overall limit in "open" mode, your program and year slots in "quota" mode),
    ///         and the same student ID cannot register twice for the same event.
    /// Also:   the student's information is saved (or updated if the student already exists).
    /// Output: the confirmation with its code (status 201), 401 if not logged in, 403 if not allowed to join.
    /// </summary>
    [HttpPost]
    public IActionResult Register(RegisterRequest req)
    {
        var me = Auth.GetAccount(Request, _db);
        if (me == null) return Unauthorized(new { error = "Please log in with your student account first" });
        if (me.Role != Roles.Student) return StatusCode(403, new { error = "Only student accounts can register for events" });

        string studentId = me.StudentId.ToUpper();   // always the logged-in student, whatever the form sent
        string fullName = DataStore.Clean(req.FullName, 60);
        string gradeSection = DataStore.Clean(req.GradeSection, 30);
        string email = DataStore.Clean(req.Email, 80).ToLower();

        // anything left blank is filled in from the student's account
        if (fullName == "") fullName = me.DisplayName;
        if (gradeSection == "") gradeSection = $"{me.Program} - {DataStore.Ordinal(me.YearLevel)} year";
        if (email == "") email = me.Email;

        if (studentId == "" || fullName.Length < 2 || gradeSection == "")
            return BadRequest(new { error = "Please fill in all the student information" });
        if (!DataStore.IsEmail(email))
            return BadRequest(new { error = "Please enter a valid email address" });

        lock (_db)
        {
            var ev = _db.Events.FirstOrDefault(e => e.Id == req.EventId);
            if (ev == null) return NotFound(new { error = "Event not found" });
            if (ev.Date < DataStore.Today) return Conflict(new { error = "This event has already passed" });

            // NEW: is this event open to the student's department, and does it have slots for their program and year?
            string? why = EventRules.WhyNotEligible(ev, me);
            if (why != null) return StatusCode(403, new { error = why });

            if (ev.CapacityMode == CapacityModes.Quota)
            {
                // Mode A: count only the students of the same program AND year level
                var quota = EventRules.FindQuota(ev, me.Program, me.YearLevel)!;   // WhyNotEligible already made sure it exists
                int used = _db.Registrations.Count(r => r.EventId == ev.Id && r.Program == me.Program && r.YearLevel == me.YearLevel);
                if (used >= quota.Slots)
                    return Conflict(new { error = $"Sorry, all {quota.Slots} slots for {me.Program} {DataStore.Ordinal(me.YearLevel)} year are already taken" });
            }
            else
            {
                // Mode B: one overall limit (this is the original check)
                int taken = _db.Registrations.Count(r => r.EventId == ev.Id);
                if (taken >= ev.Capacity) return Conflict(new { error = "Sorry, this event is already full" });
            }

            if (_db.Registrations.Any(r => r.EventId == ev.Id && r.StudentId == studentId))
                return Conflict(new { error = $"Student {studentId} is already registered for this event" });

            // STUDENT INFORMATION: save the student once, update the details if they already exist
            var student = _db.Students.FirstOrDefault(s => s.StudentId == studentId);
            if (student == null)
            {
                student = new Student { StudentId = studentId };
                _db.Students.Add(student);
            }
            student.FullName = fullName;
            student.GradeSection = gradeSection;
            student.Email = email;
            if (me.ContactNumber != "") student.ContactNumber = me.ContactNumber;   // NEW: so organizers can reach attendees
            student.Department = me.Department;
            student.Program = me.Program;
            student.YearLevel = me.YearLevel;

            var reg = new Registration
            {
                Id = _db.Registrations.Select(r => r.Id).DefaultIfEmpty(0).Max() + 1,
                Code = DataStore.RandomCode(),
                EventId = ev.Id,
                StudentId = studentId,
                RegisteredAt = DateTime.Now,
                Department = me.Department,
                Program = me.Program,
                YearLevel = me.YearLevel,
                Status = RegStatus.Registered
            };
            _db.Registrations.Add(reg);
            _db.Save();

            return CreatedAtAction(nameof(GetByCode), new { code = reg.Code }, Confirmation(reg, student, ev));
        }
    }

    /// <summary>
    /// API 6: GET /api/registrations/K7M2QX      (used by: Registration Confirmation lookup)
    /// Finds a registration by its confirmation code and returns the same confirmation again.
    /// Output: the confirmation, or 404 "No registration found for that code"
    /// </summary>
    [HttpGet("{code}")]
    public IActionResult GetByCode(string code)
    {
        lock (_db)
        {
            var reg = _db.Registrations.FirstOrDefault(r => r.Code == code.ToUpper());
            if (reg == null) return NotFound(new { error = "No registration found for that code" });

            var student = _db.Students.First(s => s.StudentId == reg.StudentId);
            var ev = _db.Events.First(e => e.Id == reg.EventId);
            return Ok(Confirmation(reg, student, ev));
        }
    }
}
