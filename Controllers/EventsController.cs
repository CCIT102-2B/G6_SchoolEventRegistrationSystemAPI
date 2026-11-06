using EventRegistrationAPI.Data;
using EventRegistrationAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistrationAPI.Controllers;

/// <summary>
/// Everything about EVENTS: View Events, Event Details, Add Event, View Participants.
/// [Route("api/[controller]")] means every URL here starts with /api/events
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly DataStore _db;

    // ASP.NET gives us the DataStore that Program.cs created
    public EventsController(DataStore db)
    {
        _db = db;
    }

    // Adds "registered", "seatsLeft" and "isPast" to an event before we send it to the page.
    // NEW: it also adds the audience/quota details, and (when someone is logged in) what THEY may do with it.
    private object WithCounts(SchoolEvent e, Account? me)
    {
        int registered = _db.Registrations.Count(r => r.EventId == e.Id);
        bool isStudent = me?.Role == Roles.Student;
        string? note = isStudent ? EventRules.WhyNotEligible(e, me!) : null;

        // how many slots of each quota are used (only used by Mode A events)
        var quotas = e.Quotas.Select(q =>
        {
            int used = _db.Registrations.Count(r => r.EventId == e.Id && r.Program == q.Program && r.YearLevel == q.YearLevel);
            return new { q.Program, q.YearLevel, q.Slots, used, left = q.Slots - used };
        }).ToList();

        // for a logged-in student on a quota event: how many slots are left for THEIR program and year
        int? yourSlotsLeft = null;
        if (isStudent && note == null && e.CapacityMode == CapacityModes.Quota)
            yourSlotsLeft = quotas.First(q => q.Program == me!.Program && q.YearLevel == me.YearLevel).left;

        return new
        {
            e.Id, e.Title, e.Date, e.Time, e.Venue, e.Capacity, e.Description,
            registered,
            seatsLeft = e.Capacity - registered,
            isPast = e.Date < DataStore.Today,
            // new fields
            e.Audience, e.AllowedDepartments, e.OwnerDepartment, e.CapacityMode, quotas,
            canManage = EventRules.CanManage(e, me),
            eligible = isStudent ? note == null : (bool?)null,
            eligibilityNote = note,
            yourSlotsLeft
        };
    }

    /// <summary>
    /// API 1: GET /api/events        (used by: View Events)
    /// Gets ALL events, soonest first. Each one also says how many students
    /// registered and how many seats are left. Anyone can call it (no login needed).
    /// If a student is logged in, each event also says whether it is open to them.
    /// Input:  nothing (optional header  Authorization: Bearer TOKEN)
    /// Output: [ { id, title, date, time, venue, capacity, description, registered, seatsLeft, isPast,
    ///             audience, allowedDepartments, ownerDepartment, capacityMode, quotas, canManage, eligible, ... }, ... ]
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        var me = Auth.GetAccount(Request, _db);
        lock (_db)
        {
            var events = _db.Events
                .OrderBy(e => e.Date).ThenBy(e => e.Time)
                .Select(e => WithCounts(e, me))
                .ToList();
            return Ok(events);
        }
    }

    /// <summary>
    /// API 2: GET /api/events/2      (used by: Event Details)
    /// Gets ONE event by its id (the number in the URL).
    /// Output: the event, or 404 "Event not found"
    /// </summary>
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var me = Auth.GetAccount(Request, _db);
        lock (_db)
        {
            var ev = _db.Events.FirstOrDefault(e => e.Id == id);
            if (ev == null) return NotFound(new { error = "Event not found" });
            return Ok(WithCounts(ev, me));
        }
    }

    /// <summary>
    /// API 3: POST /api/events       (used by: Add Event)
    /// Creates a new event after checking the details.  NEW: you must be logged in as an organizer or the Master Admin.
    /// Input:  { title, date, time, venue, capacity, description,
    ///           audience, invitedDepartments, capacityMode, quotas }      (the last four are new and optional)
    /// Who can join (audience):
    ///   Master Admin:  "college" (all departments, the default), "department" (exactly one department in invitedDepartments),
    ///                  or "inter" (two or more departments in invitedDepartments).
    ///   Organizer:     "department" (their own department, the default) or "inter" (their department plus
    ///                  the departments in invitedDepartments). Organizers cannot create college-wide events.
    /// How seats are shared (capacityMode):
    ///   "open"  (default) Mode B: one overall limit, "capacity" must be 1 to 1000.
    ///   "quota"           Mode A: "quotas" is a list of { program, yearLevel, slots }. The capacity becomes the total of all slots.
    /// Other checks (same as before): title and venue are required, the date cannot be in the past.
    /// Output: the new event (status 201 = "created"), 401 if not logged in, 403 if not allowed.
    /// </summary>
    [HttpPost]
    public IActionResult Create(CreateEventRequest req)
    {
        var me = Auth.GetAccount(Request, _db);
        if (me == null) return Unauthorized(new { error = "Please log in first" });
        if (me.Role == Roles.Student) return StatusCode(403, new { error = "Only organizers and the Master Admin can add events" });

        string title = DataStore.Clean(req.Title, 80);
        string venue = DataStore.Clean(req.Venue, 80);

        if (title.Length < 3) return BadRequest(new { error = "Please enter an event title" });
        if (venue.Length < 2) return BadRequest(new { error = "Please enter the venue" });
        if (req.Date < DataStore.Today) return BadRequest(new { error = "The event date cannot be in the past" });

        // ---- WHO CAN JOIN: work out audience, allowed departments and owner ----
        string audience = (req.Audience ?? "").Trim().ToLower();
        if (audience == "") audience = me.Role == Roles.Master ? Audiences.College : Audiences.Department;

        var picked = new List<string>();   // the department codes the page sent (cleaned, no duplicates)
        foreach (var code in req.InvitedDepartments ?? new List<string>())
        {
            var dept = Catalog.FindDepartment(code);
            if (dept == null) return BadRequest(new { error = $"Unknown department: {code}" });
            if (!picked.Contains(dept.Code)) picked.Add(dept.Code);
        }

        List<string> allowed;
        string ownerDept;
        if (me.Role == Roles.Master)
        {
            ownerDept = "";   // owned by the Master Admin
            if (audience == Audiences.College) allowed = new() { "ALL" };
            else if (audience == Audiences.Department)
            {
                if (picked.Count != 1) return BadRequest(new { error = "Choose exactly one department for a department event" });
                allowed = picked;
            }
            else if (audience == Audiences.Inter)
            {
                if (picked.Count < 2) return BadRequest(new { error = "Choose at least two departments for an inter-departmental event" });
                allowed = picked;
            }
            else return BadRequest(new { error = "Audience must be college, department or inter" });
        }
        else
        {
            ownerDept = me.Department;
            if (audience == Audiences.College)
                return StatusCode(403, new { error = "Only the Master Admin can create college-wide events" });
            if (audience == Audiences.Department) allowed = new() { me.Department };
            else if (audience == Audiences.Inter)
            {
                picked.Remove(me.Department);   // their own department is always included
                if (picked.Count < 1) return BadRequest(new { error = "Choose at least one other department to invite" });
                allowed = new List<string> { me.Department };
                allowed.AddRange(picked);
            }
            else return BadRequest(new { error = "Audience must be department or inter" });
        }

        // ---- HOW SEATS ARE SHARED: Mode B (open) or Mode A (quota) ----
        string mode = (req.CapacityMode ?? "").Trim().ToLower();
        if (mode == "") mode = CapacityModes.Open;

        int capacity;
        var quotas = new List<Quota>();
        if (mode == CapacityModes.Open)
        {
            if (req.Capacity < 1 || req.Capacity > 1000) return BadRequest(new { error = "Maximum participants must be from 1 to 1000" });
            capacity = req.Capacity;
        }
        else if (mode == CapacityModes.Quota)
        {
            if (req.Quotas == null || req.Quotas.Count == 0)
                return BadRequest(new { error = "Add at least one program and year-level quota" });

            foreach (var q in req.Quotas)
            {
                var prog = Catalog.FindProgram(q.Program);
                if (prog == null) return BadRequest(new { error = $"Unknown program: {q.Program}" });

                string progDept = Catalog.DepartmentOfProgram(prog.Code)!;
                if (!allowed.Contains("ALL") && !allowed.Contains(progDept))
                    return BadRequest(new { error = $"{prog.Code} belongs to {progDept}, which is not invited to this event" });
                if (q.YearLevel < 1 || q.YearLevel > prog.Years)
                    return BadRequest(new { error = $"{prog.Code} has year levels 1 to {prog.Years}" });
                if (q.Slots < 1 || q.Slots > 1000)
                    return BadRequest(new { error = "Slots must be from 1 to 1000" });
                if (quotas.Any(x => x.Program == prog.Code && x.YearLevel == q.YearLevel))
                    return BadRequest(new { error = $"{prog.Code} {DataStore.Ordinal(q.YearLevel)} year is listed more than once" });

                quotas.Add(new Quota { Program = prog.Code, YearLevel = q.YearLevel, Slots = q.Slots });
            }
            capacity = quotas.Sum(q => q.Slots);   // the overall capacity is simply the total of the quotas
        }
        else return BadRequest(new { error = "Capacity mode must be open or quota" });

        lock (_db)
        {
            var ev = new SchoolEvent
            {
                Id = _db.Events.Select(e => e.Id).DefaultIfEmpty(0).Max() + 1,
                Title = title,
                Date = req.Date,
                Time = req.Time,
                Venue = venue,
                Capacity = capacity,
                Description = DataStore.Clean(req.Description, 500),
                Audience = audience,
                AllowedDepartments = allowed,
                OwnerDepartment = ownerDept,
                CapacityMode = mode,
                Quotas = quotas
            };
            _db.Events.Add(ev);
            _db.Save();   // writes everything to data.json
            return CreatedAtAction(nameof(GetById), new { id = ev.Id }, WithCounts(ev, me));
        }
    }

    /// <summary>
    /// API 4: GET /api/events/2/participants      (used by: View Participants / Participant list)
    /// Gets the event and the students registered for it, in the order they signed up.
    /// Output: { event: {...}, participants: [ { studentId, fullName, gradeSection, email, registeredAt, code,
    ///                                           department, program, yearLevel, status } ] }
    /// </summary>
    [HttpGet("{id:int}/participants")]
    public IActionResult GetParticipants(int id)
    {
        var me = Auth.GetAccount(Request, _db);
        lock (_db)
        {
            var ev = _db.Events.FirstOrDefault(e => e.Id == id);
            if (ev == null) return NotFound(new { error = "Event not found" });

            // join = match each Registration with its Student by StudentId
            var participants = (
                from r in _db.Registrations.Where(r => r.EventId == id).OrderBy(r => r.RegisteredAt)
                join s in _db.Students on r.StudentId equals s.StudentId
                select new { s.StudentId, s.FullName, s.GradeSection, s.Email, r.RegisteredAt, r.Code,
                             r.Department, r.Program, r.YearLevel, r.Status }
            ).ToList();

            return Ok(new { @event = WithCounts(ev, me), participants });   // @event because "event" is a C# keyword
        }
    }
}
