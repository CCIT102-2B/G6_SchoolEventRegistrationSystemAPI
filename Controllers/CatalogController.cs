using EventRegistrationAPI.Data;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistrationAPI.Controllers;

/// <summary>The list of departments and programs. URL: /api/catalog</summary>
[ApiController]
[Route("api/[controller]")]
public class CatalogController : ControllerBase
{
    /// <summary>
    /// API 11: GET /api/catalog      (used by: Add Event form and the Attendance Manager filters)
    /// Output: [ { code, name, programs: [ { code, name, years } ] }, ... ]   (anyone can call it)
    /// </summary>
    [HttpGet]
    public IActionResult GetAll() =>
        Ok(Catalog.Departments.Select(d => new
        {
            d.Code, d.Name,
            programs = d.Programs.Select(p => new { p.Code, p.Name, years = p.Years })
        }));
}
