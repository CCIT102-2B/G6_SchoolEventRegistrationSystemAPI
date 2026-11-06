using EventRegistrationAPI.Data;
using EventRegistrationAPI.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistrationAPI.Controllers;

/// <summary>
/// LOGIN for students, department organizers and the Master Admin. URLs start with /api/auth
/// See Data/Auth.cs for how the token works.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DataStore _db;

    public AuthController(DataStore db)
    {
        _db = db;
    }

    /// <summary>
    /// API 8: POST /api/auth/login      (used by: the Log in page)
    /// Input:  { username, password }   (the username is not case-sensitive)
    /// Output: { token, user: { username, displayName, role, department, program, yearLevel, studentId, email } }
    ///         or 401 "Wrong username or password"
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login(LoginRequest req)
    {
        string username = DataStore.Clean(req.Username, 40);
        var account = _db.Accounts.FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (account == null || !Auth.Verify(account, req.Password ?? ""))
            return Unauthorized(new { error = "Wrong username or password" });

        string token = Auth.NewToken();
        _db.Sessions[token] = account.Username;
        return Ok(new { token, user = Auth.Profile(account) });
    }

    /// <summary>API 9: POST /api/auth/logout      Forgets the token. Output: { ok: true }</summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        string header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            _db.Sessions.TryRemove(header[7..].Trim(), out _);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// API 10: GET /api/auth/me      (used when the page opens, to remember who is logged in)
    /// Output: the logged-in user (same as "user" in the login output), or 401
    /// </summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        var me = Auth.GetAccount(Request, _db);
        if (me == null) return Unauthorized(new { error = "Not logged in" });
        return Ok(Auth.Profile(me));
    }

    /// <summary>
    /// API 14: POST /api/auth/register      (used by: the Sign up page)
    /// A new student creates their own account. Every field is required except the middle name.
    /// Input:  { firstName, middleName, lastName, studentNumber, email, contactNumber, department, program, yearLevel,
    ///           username, password, confirmPassword, agreeToTerms }
    /// Checks: names use letters only; student number is 4 to 20 letters, numbers or dashes; valid email;
    ///         valid Philippine mobile number (09171234567 or +639171234567); the program must belong to the department
    ///         and the year level must exist for that program; username is 4 to 20 letters, numbers, dots or underscores;
    ///         password is at least 8 characters with a letter and a number, is not the username, and matches confirmPassword;
    ///         the box is ticked. The username, student number and email must not belong to another account (409).
    /// Only STUDENT accounts can be created here. The role is never taken from the page,
    /// so nobody can make themselves an organizer or the Master Admin.
    /// Output: { token, user } with status 201, so the new student is logged in straight away.
    /// </summary>
    [HttpPost("register")]
    public IActionResult Register(SignUpRequest req)
    {
        string first = DataStore.Clean(req.FirstName, 40);
        string middle = DataStore.Clean(req.MiddleName, 40);
        string last = DataStore.Clean(req.LastName, 40);
        string studentNumber = DataStore.Clean(req.StudentNumber, 20).ToUpper();
        string email = DataStore.Clean(req.Email, 80).ToLower();
        string? phone = DataStore.NormalizePhone(req.ContactNumber);
        string username = DataStore.Clean(req.Username, 20);
        string password = req.Password ?? "";

        const string namePattern = @"^\p{L}[\p{L} .'\-]{1,39}$";
        if (!Regex.IsMatch(first, namePattern)) return BadRequest(new { error = "Please enter your first name (letters only)" });
        if (middle != "" && !Regex.IsMatch(middle, namePattern)) return BadRequest(new { error = "The middle name can only have letters" });
        if (!Regex.IsMatch(last, namePattern)) return BadRequest(new { error = "Please enter your last name (letters only)" });
        if (!Regex.IsMatch(studentNumber, @"^[A-Z0-9\-]{4,20}$"))
            return BadRequest(new { error = "Student number must be 4 to 20 letters, numbers or dashes, like 2026-1001" });
        if (!DataStore.IsEmail(email)) return BadRequest(new { error = "Please enter a valid student email address" });
        if (phone == null) return BadRequest(new { error = "Enter a valid mobile number, like 09171234567 or +639171234567" });

        var dept = Catalog.FindDepartment(req.Department);
        if (dept == null) return BadRequest(new { error = "Please choose your department" });
        var prog = dept.Programs.FirstOrDefault(p => p.Code.Equals((req.Program ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        if (prog == null) return BadRequest(new { error = $"That program does not belong to {dept.Code}" });
        if (req.YearLevel < 1 || req.YearLevel > prog.Years)
            return BadRequest(new { error = $"{prog.Code} has year levels 1 to {prog.Years}" });

        if (!Regex.IsMatch(username, @"^[A-Za-z0-9_.]{4,20}$"))
            return BadRequest(new { error = "Username must be 4 to 20 letters, numbers, dots or underscores" });
        if (password.Length < 8 || password.Length > 64 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return BadRequest(new { error = "Password must be at least 8 characters with a letter and a number" });
        if (password.Equals(username, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Password cannot be the same as your username" });
        if (password != req.ConfirmPassword) return BadRequest(new { error = "The two passwords do not match" });
        if (!req.AgreeToTerms) return BadRequest(new { error = "Please tick the box to agree before creating your account" });

        // "Juan M. Dela Cruz": first name, middle initial, last name
        string displayName = middle == "" ? $"{first} {last}" : $"{first} {char.ToUpper(middle[0])}. {last}";

        lock (_db)
        {
            // one account per username, student number and email
            if (_db.Accounts.Any(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return Conflict(new { error = "That username is already taken. Please choose another one" });
            if (_db.Accounts.Any(a => a.StudentId.Equals(studentNumber, StringComparison.OrdinalIgnoreCase)))
                return Conflict(new { error = "An account already exists for that student number" });
            if (_db.Accounts.Any(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                return Conflict(new { error = "An account already exists for that email address" });

            var account = Auth.NewAccount(username, password, displayName, Roles.Student,   // always a student
                dept.Code, prog.Code, req.YearLevel, studentNumber, email, phone);
            account.FirstName = first;
            account.MiddleName = middle;
            account.LastName = last;
            _db.Accounts.Add(account);
            _db.Save();

            string token = Auth.NewToken();
            _db.Sessions[token] = account.Username;
            return StatusCode(StatusCodes.Status201Created, new { token, user = Auth.Profile(account) });
        }
    }
}
