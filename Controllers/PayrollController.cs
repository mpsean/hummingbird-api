using Hummingbird.API.DTOs;
using Hummingbird.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hummingbird.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PayrollController : ControllerBase
{
    private readonly PayrollService _service;

    public PayrollController(PayrollService service)
    {
        _service = service;
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] CalculatePayrollDto dto)
    {
        if (dto.Month < 1 || dto.Month > 12)
            return BadRequest(new { message = "Month must be 1–12." });
        if (dto.Year < 2000)
            return BadRequest(new { message = "Invalid year." });
        if (dto.ServiceChargeTotal < 0)
            return BadRequest(new { message = "Service charge cannot be negative." });

        var result = await _service.CalculatePayrollAsync(dto);
        return Ok(result);
    }

    [HttpGet("{year}/{month}")]
    public async Task<IActionResult> GetPayroll(int year, int month)
    {
        var result = await _service.GetPayrollAsync(year, month);
        if (result == null)
            return NotFound(new { message = $"No payroll calculated for {month}/{year}." });
        return Ok(result);
    }

    [HttpGet("months")]
    public async Task<IActionResult> GetCalculatedMonths()
    {
        var months = await _service.GetCalculatedMonthsAsync();
        return Ok(months.Select(m => new { year = m.Item1, month = m.Item2 }));
    }
}
