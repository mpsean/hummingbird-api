using Hummingbird.API.DTOs;
using Hummingbird.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hummingbird.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimeAttendanceController : ControllerBase
{
    private readonly TimeAttendanceService _service;

    public TimeAttendanceController(TimeAttendanceService service)
    {
        _service = service;
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only CSV files are accepted." });

        using var stream = file.OpenReadStream();
        var result = await _service.ImportCsvAsync(stream);

        return Ok(result);
    }

    [HttpGet("{year}/{month}")]
    public async Task<IActionResult> GetByMonth(int year, int month)
    {
        var records = await _service.GetByMonthAsync(year, month);
        return Ok(records);
    }

    [HttpGet("{year}/{month}/summary")]
    public async Task<IActionResult> GetSummary(int year, int month)
    {
        var summary = await _service.GetSummaryByMonthAsync(year, month);
        return Ok(summary);
    }

    [HttpGet("months")]
    public async Task<IActionResult> GetAvailableMonths()
    {
        var months = await _service.GetAvailableMonthsAsync();
        return Ok(months.Select(m => new { year = m.Item1, month = m.Item2 }));
    }

    [HttpDelete("{year}/{month}")]
    public async Task<IActionResult> DeleteMonth(int year, int month)
    {
        await _service.DeleteByMonthAsync(year, month);
        return Ok(new { message = $"Deleted attendance records for {month}/{year}." });
    }
}
