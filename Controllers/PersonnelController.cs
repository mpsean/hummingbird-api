using CsvHelper;
using CsvHelper.Configuration;
using Hummingbird.API.Data;
using Hummingbird.API.DTOs;
using Hummingbird.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Hummingbird.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonnelController : ControllerBase
{
    private readonly AppDbContext _db;

    public PersonnelController(AppDbContext db)
    {
        _db = db;
    }

    // ── Employees ────────────────────────────────────────────────────────────

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees([FromQuery] string? status)
    {
        var query = _db.Employees.Include(e => e.Position).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<EmployeeStatus>(status, true, out var s))
        {
            query = query.Where(e => e.Status == s);
        }

        var list = await query.OrderBy(e => e.EmployeeCode).ToListAsync();
        return Ok(list.Select(MapEmployeeToDto));
    }

    [HttpPost("employees/import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportEmployees(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only CSV files are accepted." });

        var positions = await _db.Positions.ToListAsync();

        using var stream = file.OpenReadStream();
        var result = await ImportEmployeesCsvAsync(stream, positions);

        return Ok(result);
    }

    [HttpGet("employees/{id}")]
    public async Task<IActionResult> GetEmployee(int id)
    {
        var emp = await _db.Employees.Include(e => e.Position).FirstOrDefaultAsync(e => e.Id == id);
        if (emp == null) return NotFound();
        return Ok(MapEmployeeToDto(emp));
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeDto dto)
    {
        if (await _db.Employees.AnyAsync(e => e.EmployeeCode == dto.EmployeeCode))
            return Conflict(new { message = $"Employee code '{dto.EmployeeCode}' already exists." });

        if (!await _db.Positions.AnyAsync(p => p.Id == dto.PositionId))
            return BadRequest(new { message = "Invalid position ID." });

        if (!Enum.TryParse<EmployeeStatus>(dto.Status, true, out var status))
            status = EmployeeStatus.Active;

        var emp = new Employee
        {
            EmployeeCode = dto.EmployeeCode,
            Name = dto.Name,
            Surname = dto.Surname,
            PositionId = dto.PositionId,
            Salary = dto.Salary,
            DateJoined = dto.DateJoined,
            Status = status
        };

        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();

        var created = await _db.Employees.Include(e => e.Position).FirstAsync(e => e.Id == emp.Id);
        return CreatedAtAction(nameof(GetEmployee), new { id = emp.Id }, MapEmployeeToDto(created));
    }

    [HttpPut("employees/{id}")]
    public async Task<IActionResult> UpdateEmployee(int id, [FromBody] UpdateEmployeeDto dto)
    {
        var emp = await _db.Employees.FindAsync(id);
        if (emp == null) return NotFound();

        if (emp.EmployeeCode != dto.EmployeeCode &&
            await _db.Employees.AnyAsync(e => e.EmployeeCode == dto.EmployeeCode && e.Id != id))
            return Conflict(new { message = $"Employee code '{dto.EmployeeCode}' already in use." });

        if (!await _db.Positions.AnyAsync(p => p.Id == dto.PositionId))
            return BadRequest(new { message = "Invalid position ID." });

        if (!Enum.TryParse<EmployeeStatus>(dto.Status, true, out var status))
            return BadRequest(new { message = "Invalid status." });

        emp.EmployeeCode = dto.EmployeeCode;
        emp.Name = dto.Name;
        emp.Surname = dto.Surname;
        emp.PositionId = dto.PositionId;
        emp.Salary = dto.Salary;
        emp.DateJoined = dto.DateJoined;
        emp.Status = status;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Updated." });
    }

    [HttpDelete("employees/{id}")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var emp = await _db.Employees.FindAsync(id);
        if (emp == null) return NotFound();

        _db.Employees.Remove(emp);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Deleted." });
    }

    // ── Positions ─────────────────────────────────────────────────────────────

    [HttpGet("positions")]
    public async Task<IActionResult> GetPositions()
    {
        var positions = await _db.Positions
            .Include(p => p.Employees)
            .OrderBy(p => p.Id)
            .ToListAsync();

        return Ok(positions.Select(p => new PositionDto
        {
            Id = p.Id,
            Name = p.Name,
            DefaultSalary = p.DefaultSalary,
            ShiftType = p.ShiftType.ToString(),
            ClockInTime = p.ClockInTime.ToString(@"hh\:mm"),
            ClockOutTime = p.ClockOutTime.ToString(@"hh\:mm"),
            TotalHours = p.TotalHours,
            ServiceChargePercentage = p.ServiceChargePercentage,
            EmployeeCount = p.Employees.Count(e => e.Status == EmployeeStatus.Active)
        }));
    }

    [HttpGet("positions/{id}")]
    public async Task<IActionResult> GetPosition(int id)
    {
        var p = await _db.Positions.Include(x => x.Employees).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        return Ok(new PositionDto
        {
            Id = p.Id,
            Name = p.Name,
            DefaultSalary = p.DefaultSalary,
            ShiftType = p.ShiftType.ToString(),
            ClockInTime = p.ClockInTime.ToString(@"hh\:mm"),
            ClockOutTime = p.ClockOutTime.ToString(@"hh\:mm"),
            TotalHours = p.TotalHours,
            ServiceChargePercentage = p.ServiceChargePercentage,
            EmployeeCount = p.Employees.Count(e => e.Status == EmployeeStatus.Active)
        });
    }

    [HttpPost("positions")]
    public async Task<IActionResult> CreatePosition([FromBody] CreatePositionDto dto)
    {
        if (!Enum.TryParse<ShiftType>(dto.ShiftType, true, out var shift))
            return BadRequest(new { message = "Invalid shift type." });

        if (!TimeSpan.TryParse(dto.ClockInTime, out var clockIn) ||
            !TimeSpan.TryParse(dto.ClockOutTime, out var clockOut))
            return BadRequest(new { message = "Invalid time format. Use HH:mm." });

        var position = new Position
        {
            Name = dto.Name,
            DefaultSalary = dto.DefaultSalary,
            ShiftType = shift,
            ClockInTime = clockIn,
            ClockOutTime = clockOut,
            TotalHours = dto.TotalHours,
            ServiceChargePercentage = dto.ServiceChargePercentage
        };

        _db.Positions.Add(position);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPosition), new { id = position.Id }, new { id = position.Id });
    }

    [HttpPut("positions/{id}")]
    public async Task<IActionResult> UpdatePosition(int id, [FromBody] UpdatePositionDto dto)
    {
        var pos = await _db.Positions.FindAsync(id);
        if (pos == null) return NotFound();

        if (!Enum.TryParse<ShiftType>(dto.ShiftType, true, out var shift))
            return BadRequest(new { message = "Invalid shift type." });

        if (!TimeSpan.TryParse(dto.ClockInTime, out var clockIn) ||
            !TimeSpan.TryParse(dto.ClockOutTime, out var clockOut))
            return BadRequest(new { message = "Invalid time format. Use HH:mm." });

        pos.Name = dto.Name;
        pos.DefaultSalary = dto.DefaultSalary;
        pos.ShiftType = shift;
        pos.ClockInTime = clockIn;
        pos.ClockOutTime = clockOut;
        pos.TotalHours = dto.TotalHours;
        pos.ServiceChargePercentage = dto.ServiceChargePercentage;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Updated." });
    }

    [HttpDelete("positions/{id}")]
    public async Task<IActionResult> DeletePosition(int id)
    {
        var pos = await _db.Positions.FindAsync(id);
        if (pos == null) return NotFound();

        bool hasEmployees = await _db.Employees.AnyAsync(e => e.PositionId == id);
        if (hasEmployees)
            return BadRequest(new { message = "Cannot delete a position that has active employees." });

        _db.Positions.Remove(pos);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Deleted." });
    }

    private async Task<ImportResultDto> ImportEmployeesCsvAsync(Stream stream, List<Position> positions)
    {
        var result = new ImportResultDto();
        var existingCodes = (await _db.Employees.Select(e => e.EmployeeCode).ToListAsync())
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var positionMap = positions.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
        };

        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        int row = 1;
        while (csv.Read())
        {
            row++;
            try
            {
                var code      = csv.GetField("Employee_ID")?.Trim() ?? "";
                var name      = csv.GetField("Name")?.Trim() ?? "";
                var surname   = csv.GetField("Surname")?.Trim() ?? "";
                var posName   = csv.GetField("Position")?.Trim() ?? "";
                var salaryStr = csv.GetField("Salary")?.Trim() ?? "";
                var dateStr   = csv.GetField("Date_Joined")?.Trim() ?? "";
                var statusStr = csv.GetField("Status")?.Trim() ?? "Active";

                if (string.IsNullOrWhiteSpace(code))
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"Row {row}: Employee_ID is empty.");
                    continue;
                }

                // Duplicate check
                if (existingCodes.Contains(code))
                {
                    result.Skipped++;
                    continue;
                }

                // Salary (parsed early so new positions can use it as DefaultSalary)
                if (!decimal.TryParse(salaryStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var salary))
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"Row {row}: Invalid salary '{salaryStr}'.");
                    continue;
                }

                // Position — auto-create with defaults if not found
                if (!positionMap.TryGetValue(posName, out var position))
                {
                    if (string.IsNullOrWhiteSpace(posName))
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Row {row}: Position is empty.");
                        continue;
                    }
                    position = new Position
                    {
                        Name = posName,
                        DefaultSalary = salary,
                        ShiftType = ShiftType.Morning,
                        ClockInTime = TimeSpan.FromHours(8),
                        ClockOutTime = TimeSpan.FromHours(17),
                        TotalHours = 9,
                        ServiceChargePercentage = 0
                    };
                    _db.Positions.Add(position);
                    positionMap[posName] = position;
                    result.PositionsCreated++;
                }

                // Date — force UTC to avoid timezone shift on save
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateRaw))
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"Row {row}: Invalid Date_Joined '{dateStr}'.");
                    continue;
                }
                var dateJoined = DateTime.SpecifyKind(dateRaw.Date, DateTimeKind.Utc);

                // Status
                if (!Enum.TryParse<EmployeeStatus>(statusStr, true, out var empStatus))
                    empStatus = EmployeeStatus.Active;

                var emp = new Employee
                {
                    EmployeeCode = code,
                    Name = name,
                    Surname = surname,
                    Salary = salary,
                    DateJoined = dateJoined,
                    Status = empStatus
                };

                // New positions have Id=0 until saved — use navigation property so EF resolves the FK
                if (position.Id == 0)
                    emp.Position = position;
                else
                    emp.PositionId = position.Id;

                _db.Employees.Add(emp);

                existingCodes.Add(code); // prevent intra-file duplicates
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors++;
                result.ErrorMessages.Add($"Row {row}: {ex.Message}");
            }
        }

        if (result.Imported > 0 || result.PositionsCreated > 0)
            await _db.SaveChangesAsync();

        return result;
    }

    private static EmployeeDto MapEmployeeToDto(Employee e) => new()
    {
        Id = e.Id,
        EmployeeCode = e.EmployeeCode,
        Name = e.Name,
        Surname = e.Surname,
        PositionId = e.PositionId,
        PositionName = e.Position?.Name ?? "",
        Salary = e.Salary,
        DateJoined = e.DateJoined,
        Status = e.Status.ToString()
    };
}
