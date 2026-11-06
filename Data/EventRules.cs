using EventRegistrationAPI.Models;

namespace EventRegistrationAPI.Data;

/// <summary>Small rules about events that more than one controller needs.</summary>
public static class EventRules
{
    /// <summary>The Master Admin can manage every event. A department organizer can manage the events their department created.</summary>
    public static bool CanManage(SchoolEvent e, Account? me) =>
        me != null && (me.Role == Roles.Master ||
                       (me.Role == Roles.Organizer && e.OwnerDepartment != "" && e.OwnerDepartment == me.Department));

    public static bool DeptAllowed(SchoolEvent e, string department) =>
        e.AllowedDepartments.Contains("ALL") || e.AllowedDepartments.Contains(department);

    public static Quota? FindQuota(SchoolEvent e, string program, int yearLevel) =>
        e.Quotas.FirstOrDefault(q => q.Program == program && q.YearLevel == yearLevel);

    /// <summary>
    /// Can this student join this event at all (ignoring "is it full")?
    /// Returns null when yes, or a message that says why not.
    /// </summary>
    public static string? WhyNotEligible(SchoolEvent e, Account student)
    {
        if (!DeptAllowed(e, student.Department))
            return $"This event is not open to {student.Department} students. It is for: {string.Join(", ", e.AllowedDepartments)}.";

        if (e.CapacityMode == CapacityModes.Quota && FindQuota(e, student.Program, student.YearLevel) == null)
            return $"No slots are reserved for {student.Program} {DataStore.Ordinal(student.YearLevel)} year students.";

        return null;
    }
}
