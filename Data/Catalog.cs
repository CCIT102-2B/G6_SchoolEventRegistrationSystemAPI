namespace EventRegistrationAPI.Data;

public record ProgramInfo(string Code, string Name, int Years = 4);
public record DepartmentInfo(string Code, string Name, ProgramInfo[] Programs);

/// <summary>
/// The list of departments and their academic programs (First Asia Institute of Technology and Humanities).
/// It never changes while the app runs, so it lives in code and is NOT saved in data.json.
/// "Years" is how many year levels the program has (used to check the year level of a quota).
/// </summary>
public static class Catalog
{
    public static readonly DepartmentInfo[] Departments =
    {
        new("CCIT", "College of Computing and Information Technology", new[]
        {
            new ProgramInfo("BSCS", "BS Computer Science"),
            new ProgramInfo("BSIT", "BS Information Technology"),
            new ProgramInfo("BSEMC", "BS Entertainment and Multimedia Computing", 2),
        }),
        new("CBMA", "College of Business Administration, Management, and Accountancy", new[]
        {
            new ProgramInfo("BSBA", "BS Business Administration (Financial/Marketing Management)"),
            new ProgramInfo("BSA", "BS Accountancy"),
            new ProgramInfo("BSHM", "BS Hospitality Management"),
            new ProgramInfo("BSTM", "BS Tourism Management"),
        }),
        new("COE", "College of Engineering", new[]
        {
            new ProgramInfo("BSCPE", "BS Computer Engineering"),
            new ProgramInfo("BSEE", "BS Electrical Engineering"),
            new ProgramInfo("BSECE", "BS Electronics Engineering"),
            new ProgramInfo("BSIE", "BS Industrial Engineering"),
        }),
        new("CASE", "College of Arts, Sciences and Education", new[]
        {
            new ProgramInfo("BAC", "Bachelor of Arts in Communication"),
            new ProgramInfo("BSP", "Bachelor of Science in Psychology"),
            new ProgramInfo("BEEd", "Bachelor of Elementary Education"),
            new ProgramInfo("BSEd", "Bachelor of Secondary Education"),
        }),
        new("CHC", "College of Health Care", new[]
        {
            new ProgramInfo("BSN", "BS Nursing"),
            new ProgramInfo("BSMT", "BS Medical Technology"),
        }),
        new("CCJ", "College of Criminal Justice", new[]
        {
            new ProgramInfo("BSCrim", "BS Criminology"),
            new ProgramInfo("BSFS", "BS Forensic Science"),
        }),
    };

    /// <summary>Finds a department by code (not case-sensitive). Returns null if there is none.</summary>
    public static DepartmentInfo? FindDepartment(string? code) =>
        Departments.FirstOrDefault(d => d.Code.Equals((code ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds a program by code (not case-sensitive). The result has the official spelling, e.g. "BSEd".</summary>
    public static ProgramInfo? FindProgram(string? code) =>
        Departments.SelectMany(d => d.Programs)
                   .FirstOrDefault(p => p.Code.Equals((code ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Which department a program belongs to, e.g. "BSIT" gives "CCIT".</summary>
    public static string? DepartmentOfProgram(string programCode) =>
        Departments.FirstOrDefault(d => d.Programs.Any(p => p.Code == programCode))?.Code;
}
