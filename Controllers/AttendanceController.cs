using EventRegistrationAPI.Data;
using EventRegistrationAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistrationAPI.Controllers;

/// <summary>
/// The EVENT ATTENDANCE MANAGER for department organizers and the Master Admin. URLs start with /api/attendance
/// What each person can see:
///   Master Admin:  every registration of every event.
///   Organizer:     registrations for events their department created, plus registrations of their own
///                  department's students in any event (for example an event another department invited them to).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly DataStore _db;

    public AttendanceController(DataStore db)
    {
        _db = db;
    }

    // One line of the attendance table
    private record Row(string Code, int EventId, string EventTitle, DateOnly EventDate, string StudentId, string FullName,
                       string Department, string Program, int YearLevel, string Email, string ContactNumber, string Status,
                       DateTime RegisteredAt, DateTime? CheckedInAt, bool CanManage);

    /// <summary>
    /// API 12: GET /api/attendance      (used by: Attendance Manager)
    /// Lists registered students. Every filter is optional and they can be combined:
    ///   ?eventId=4 &amp;department=CCIT &amp;program=BSIT &amp;yearLevel=1 &amp;status=checked_in &amp;search=juan
    ///   &amp;sort=name|studentId|department|program|year|status|event|registered &amp;dir=asc|desc
    /// Output: { summary: { total, checkedIn, notYetCheckedIn, allInScope },
    ///           events: [ { id, title, date } ]   (the events this person can pick in the filter),
    ///           rows:   [ { code, eventId, eventTitle, studentId, fullName, department, program, yearLevel, email, contactNumber,
    ///                       status, registeredAt, checkedInAt, canManage } ] }
    ///         or 401 (not logged in) / 403 (students cannot open this)
    /// </summary>
    [HttpGet]
    public IActionResult GetAttendance(int? eventId, string? department, string? program, int? yearLevel,
                                       string? status, string? search, string? sort, string? dir)
    {
        var me = Auth.GetAccount(Request, _db);
        if (me == null) return Unauthorized(new { error = "Please log in first" });
        if (me.Role == Roles.Student) return StatusCode(403, new { error = "Only organizers can open the Attendance Manager" });

        lock (_db)
        {
            bool master = me.Role == Roles.Master;

            // 1) everything this person is allowed to see
            var scope = (
                from r in _db.Registrations
                join e in _db.Events on r.EventId equals e.Id
                join s in _db.Students on r.StudentId equals s.StudentId
                where master || e.OwnerDepartment == me.Department || r.Department == me.Department
                select new Row(r.Code, e.Id, e.Title, e.Date, s.StudentId, s.FullName, r.Department, r.Program, r.YearLevel,
                               s.Email, s.ContactNumber, r.Status, r.RegisteredAt, r.CheckedInAt, EventRules.CanManage(e, me))
            ).ToList();

            // 2) the events to show in the "Event" filter
            var events = _db.Events
                .Where(e => master || e.OwnerDepartment == me.Department || scope.Any(r => r.EventId == e.Id))
                .OrderBy(e => e.Date).ThenBy(e => e.Time)
                .Select(e => new { e.Id, e.Title, e.Date })
                .ToList();

            // 3) apply the filters the page sent
            IEnumerable<Row> rows = scope;
            if (eventId.HasValue) rows = rows.Where(r => r.EventId == eventId.Value);
            if (!string.IsNullOrWhiteSpace(department)) rows = rows.Where(r => r.Department.Equals(department.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(program)) rows = rows.Where(r => r.Program.Equals(program.Trim(), StringComparison.OrdinalIgnoreCase));
            if (yearLevel.HasValue && yearLevel > 0) rows = rows.Where(r => r.YearLevel == yearLevel.Value);
            if (!string.IsNullOrWhiteSpace(status)) rows = rows.Where(r => r.Status == status.Trim().ToLower());
            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = search.Trim();
                rows = rows.Where(r => r.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || r.StudentId.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || r.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || r.Email.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || r.ContactNumber.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            // 4) sorting
            Func<Row, object> key = (sort ?? "").ToLower() switch
            {
                "name" => r => r.FullName,
                "studentid" => r => r.StudentId,
                "department" => r => r.Department,
                "program" => r => r.Program,
                "year" => r => r.YearLevel,
                "status" => r => r.Status,
                "event" => r => r.EventTitle,
                _ => r => r.RegisteredAt
            };
            var sorted = (dir ?? "").ToLower() == "asc"
                ? rows.OrderBy(key).ThenBy(r => r.RegisteredAt)
                : rows.OrderByDescending(key).ThenByDescending(r => r.RegisteredAt);
            var list = sorted.ToList();

            int checkedIn = list.Count(r => r.Status == RegStatus.CheckedIn);
            return Ok(new
            {
                summary = new { total = list.Count, checkedIn, notYetCheckedIn = list.Count - checkedIn, allInScope = scope.Count },
                events,
                rows = list
            });
        }
    }

    /// <summary>
    /// API 13: PUT /api/attendance/K7M2QX/status      (used by: the Check in / Undo buttons)
    /// Marks one registration as checked in (or back to registered).
    /// Input:  { status }   "checked_in" or "registered"
    /// Only the Master Admin or the organizer of THAT event can do this (403 for anyone else).
    /// Output: { code, status, checkedInAt }
    /// </summary>
    [HttpPut("{code}/status")]
    public IActionResult SetStatus(string code, StatusRequest req)
    {
        var me = Auth.GetAccount(Request, _db);
        if (me == null) return Unauthorized(new { error = "Please log in first" });
        if (me.Role == Roles.Student) return StatusCode(403, new { error = "Only organizers can change attendance" });

        string status = (req.Status ?? "").Trim().ToLower();
        if (status != RegStatus.Registered && status != RegStatus.CheckedIn)
            return BadRequest(new { error = "Status must be registered or checked_in" });

        lock (_db)
        {
            var reg = _db.Registrations.FirstOrDefault(r => r.Code == code.ToUpper());
            if (reg == null) return NotFound(new { error = "No registration found for that code" });

            var ev = _db.Events.First(e => e.Id == reg.EventId);
            if (!EventRules.CanManage(ev, me))
                return StatusCode(403, new { error = "Only the organizer of this event (or the Master Admin) can change its attendance" });

            reg.Status = status;
            reg.CheckedInAt = status == RegStatus.CheckedIn ? DateTime.Now : null;
            _db.Save();
            return Ok(new { reg.Code, reg.Status, reg.CheckedInAt });
        }
    }
}
