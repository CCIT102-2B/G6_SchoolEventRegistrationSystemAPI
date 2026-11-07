# CampusPulse — Local API

The back end. It only provides the API. The web page is in the FrontEnd folder.

## Run it
1. Install the .NET SDK (8 or newer): https://dotnet.microsoft.com/download
2. Open a terminal in THIS folder (the one that contains `EventRegistrationAPI.csproj`) and run: `dotnet run`
3. Wait for: `Now listening on: http://localhost:5005`
4. Leave it running, then open `FrontEnd/index.html` (see the FrontEnd README).

You can test the API alone in the browser: http://localhost:5005/api/events

If you get a "framework not found" error, open `EventRegistrationAPI.csproj` and change `net8.0` to match your SDK (for example `net10.0`).
Data is saved in `data.json`, **encrypted**, together with a secret key file `data.key` (both are created on first run). Delete BOTH files to reset to the sample data. See "Data encryption" below.

## Demo accounts (the password of EVERY account is `password123`)
| Username | Role | Department / program |
|---|---|---|
| `FAITH` | Master Admin: college-wide events, can manage every event | all |
| `cbma_admin` | Organizer (department head) | CBMA |
| `ccit_admin` | Organizer | CCIT |
| `coe_admin` | Organizer | COE |
| `case_admin` | Organizer | CASE |
| `chc_admin` | Organizer | CHC |
| `ccj_admin` | Organizer | CCJ |
| `student_ccit_1` | Student: Juan Dela Cruz, ID 2026-1001 | CCIT, BSIT, 1st year |
| `student_ccit_3` | Student: Maria Santos, ID 2024-1003 | CCIT, BSCS, 3rd year |
| `student_cabea_3` | Student: Ana Reyes, ID 2024-2003 | CBMA, BSA, 3rd year |

These demo accounts are only created when `data.json` does not exist yet. If you renamed departments after your first run, delete `data.json` and run again so the accounts match your catalog.

## Creating a new account (sign up)
New students can create their own account on the **Sign up** page (API: `POST /api/auth/register`). Every field is required except the middle name:

| Field | Rule |
|---|---|
| First name, last name (middle name optional) | letters, spaces, dots, apostrophes and dashes |
| Student number | 4 to 20 letters, numbers or dashes (example `2026-1001`), saved in capital letters, one account per number |
| Student email | a valid email address, one account per email |
| Contact number | a Philippine mobile number: `09171234567`, `0917 123 4567` or `+639171234567` (saved as `09171234567`) |
| Department, program, year level | the program must belong to the department and the year level must exist for that program (`years` in `Data/Catalog.cs`) |
| Username | 4 to 20 letters, numbers, dots or underscores, not case-sensitive, one account per username |
| Password and confirm password | at least 8 characters with a letter and a number, not the same as the username, both must match |
| Agreement box | must be ticked |

- Sign-up only ever creates **student** accounts. The role is never read from the page, so nobody can make themselves an organizer or the Master Admin. Organizer accounts are still created in `DataStore.SeedAccounts()`.
- The new student is logged in straight away.
- The contact number is copied to the student record when they register for an event. Organizers and the Master Admin see it in the Attendance Manager (and the CSV export, and can search by it). The public participant list does not show it.
- There is no email or SMS verification, so the system cannot prove that a student really owns the student number they type. A real system would add a verification step or an approval by the registrar.

Passwords are never stored as plain text: `data.json` keeps a salt and a PBKDF2 hash.

## What can each role do?
| | Visitor | Student | Organizer (department head) | Master Admin |
|---|---|---|---|---|
| Create a new account (sign up) | yes (creates a student account) | n/a | n/a | n/a |
| View events and participants | yes | yes | yes | yes |
| Register for an event | no | yes, only if the event is open to their department (and has slots for their program and year) | no | no |
| Add an event | no | no | own department, or own department + invited departments | college-wide, one department, or selected departments |
| Attendance Manager | no | no | events their department created + their department's students in any event; can check in students for their own events | everything |

## Event audience and capacity
- **Audience** (`audience`): `college` (all departments, Master Admin only), `department` (one department), `inter` (the owner's department plus invited departments). Saved as `allowedDepartments` (`["ALL"]` or department codes).
- **Capacity mode** (`capacityMode`):
  - `open` (Mode B): one overall limit (`capacity`), first come first served.
  - `quota` (Mode A): `quotas` is a list of `{ program, yearLevel, slots }`, for example BSIT 1st year: 30 and BSCS 3rd year: 15. `capacity` becomes the total of all slots (45 in that example). A student can only register if their program and year have a slot left.

Example body for `POST /api/events` (as `ccit_admin`):
```json
{
  "title": "CCIT Tech Summit", "date": "2026-12-01", "time": "09:00:00", "venue": "Lab 1", "description": "Talks and demos",
  "audience": "inter", "invitedDepartments": ["COE"],
  "capacityMode": "quota",
  "quotas": [ { "program": "BSIT", "yearLevel": 1, "slots": 30 }, { "program": "BSCS", "yearLevel": 3, "slots": 15 } ]
}
```
The old body (title, date, time, venue, capacity, description) still works: it makes an open event for the organizer's department (or a college-wide one for the Master Admin).

## How to test with Scalar (or Postman)
Open `http://localhost:5005/scalar/v1` to browse the API documentation.
The OpenAPI document is available at `http://localhost:5005/swagger/v1/swagger.json`.

1. `POST /api/auth/login` with `{ "username": "ccit_admin", "password": "password123" }` and copy the `token` from the response.
2. For every other request, add a header: `Authorization` = `Bearer PASTE_THE_TOKEN_HERE`. In Scalar, add it in the Headers table of the request.
3. Tokens are kept in memory, so restarting the API logs everyone out. Just log in again.

## Structure
```
API/
  Controllers/
    EventsController.cs          add / view events, event details, participants
    RegistrationsController.cs   register + confirmation
    StudentsController.cs        student information
    AuthController.cs            NEW  log in, log out, who am I, sign up
    CatalogController.cs         NEW  departments and programs
    AttendanceController.cs      NEW  Attendance Manager: list, filter, sort, check in
  Models/
    SchoolEvent.cs  Student.cs  Registration.cs   the data (updated with audience, quotas, program, year, status)
    Account.cs                   NEW  login accounts and roles
    Requests.cs                  the JSON the page sends
  Data/
    DataStore.cs                 keeps the lists in memory and saves them to data.json (now also accounts, saved encrypted)
    Catalog.cs                   NEW  the departments and programs (edit this file to change them)
    Auth.cs                      NEW  password hashing, tokens, "who is asking?"
    DataProtector.cs             NEW  encrypts and decrypts data.json
    EventRules.cs                NEW  small shared rules (who may join, who may manage)
  Program.cs                     starts the app, turns on controllers and CORS (unchanged)
  appsettings.json               settings (the address it runs on)
  EventRegistrationAPI.csproj    project file
```

## The APIs (each one is explained in a comment above its method in the controller)
| # | Method and URL | Who | What it does |
|---|---|---|---|
| 1 | GET /api/events | anyone | List all events with seats left (a logged-in student also gets `eligible` and `yourSlotsLeft`) |
| 2 | GET /api/events/{id} | anyone | Get one event |
| 3 | POST /api/events | organizer, master | Add a new event (audience + capacity mode) |
| 4 | GET /api/events/{id}/participants | anyone | The participant list of an event |
| 5 | POST /api/registrations | student | Register the logged-in student, returns a confirmation code |
| 6 | GET /api/registrations/{code} | anyone with the code | Look up a confirmation by its code |
| 7 | GET /api/students/{studentId} | logged in (students: own ID only) | Student information and their registrations |
| 8 | POST /api/auth/login | anyone | Log in, returns a token and the user |
| 9 | POST /api/auth/logout | anyone | Forget the token |
| 10 | GET /api/auth/me | logged in | Who is logged in |
| 11 | GET /api/catalog | anyone | Departments and their programs |
| 12 | GET /api/attendance | organizer, master | Attendees with filters: eventId, department, program, yearLevel, status, search, sort, dir |
| 13 | PUT /api/attendance/{code}/status | organizer of that event, master | Check a student in (`checked_in`) or undo (`registered`) |
| 14 | POST /api/auth/register | anyone | Sign up: create a new student account (returns a token and the user) |

## Data encryption
`data.json` is encrypted (AES-256-GCM). Usernames, names, emails, contact numbers, events and registrations cannot be read by opening the file, and any change made to the file by someone else is detected. Instead of readable JSON you will see text starting with `ENCv1:`.

- **The key** is `data.key`, created next to `data.json` the first time the app runs. On a host you can set the `DATA_KEY` setting instead (any random text, at least 20 characters), so the key is not stored next to the data.
- **Keep the key safe.** Never upload `data.key` or your `DATA_KEY` to GitHub or share them (the `.gitignore` files already leave `data.key` out). Keep a copy of the key together with any backup of `data.json`. Without the right key the file cannot be opened. The app then refuses to start, and it never overwrites your data. To start again with sample data, delete `data.json` and `data.key`.
- **Passwords** are never stored in a form that can be turned back into the password. Only a salted one-way hash is saved (PBKDF2-SHA256 with a different salt for every account, see `Data/Auth.cs`). That is stronger than encryption, and the hashes are inside the encrypted file as well.
- **Upgrading:** an old readable `data.json` is accepted and turned into an encrypted file the first time the new version starts.
- To look at the data, use the app (Attendance Manager and CSV export) or the API. The file itself is unreadable on purpose.

## Hosting settings (optional)
Everything works without these. A host such as Render lets you set them as environment variables:

| Variable | What it does |
|---|---|
| `PORT` | The port to listen on (the host sets it; this replaces the `Urls` line in `appsettings.json`) |
| `ALLOWED_ORIGINS` | The website address(es) allowed to call the API, separated by commas, for example `https://school-events.vercel.app`. Empty means any website. |
| `DATA_FILE` | Where `data.json` is saved (the key file `data.key` goes in the same folder) |
| `DATA_KEY` | The secret key for encrypting `data.json`: random text, at least 20 characters. If you do not set it, a `data.key` file is used. |

`GET /health` answers `{"status":"ok"}` so the host can check that the API is alive. `Dockerfile` and `.dockerignore` are what Render uses to build the API. See `DEPLOY.md` at the top of the project.

## Upgrading from the earlier version
Your old `data.json` still works: old events become college-wide, open events owned by the Master Admin, and the mock accounts are added automatically. The new sample events (CCIT Tech Summit and CABEA Business Fair) only appear on a fresh start, so delete `data.json` if you want them.

## Why CORS is in Program.cs
The page (FrontEnd) and the API run at different addresses, so the browser blocks the page from calling the API unless the API says it is allowed. `AddCors` and `UseCors` in `Program.cs` do that. `AllowAnyHeader` also lets the page send the `Authorization` header.


## Local-only mode

The API is hosted together with the FrontEnd by `dotnet run`. Start the app from this API folder, then visit `http://localhost:5005`. No cloud hosting or CORS configuration is used.
