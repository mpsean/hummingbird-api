using CsvHelper;
using CsvHelper.Configuration;
using Hummingbird.API.Data;
using Hummingbird.API.DTOs;
using Hummingbird.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Hummingbird.API.Services;

public class TimeAttendanceService
{
    private readonly AppDbContext _db;

    public TimeAttendanceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ImportResultDto> ImportCsvAsync(Stream fileStream)
    {
        var result = new ImportResultDto();
        var positions = await _db.Positions.ToListAsync();
        var employees = await _db.Employees.Include(e => e.Position).ToListAsync();

        var existingKeys = await _db.TimeAttendanceRecords
            .Select(t => new { t.EmployeeCode, t.Date })
            .ToListAsync();
        var seenKeys = new HashSet<(string, DateTime)>(
            existingKeys.Select(k => (k.EmployeeCode, k.Date)));

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
        };

        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            try
            {
                var dateStr = csv.GetField("Date") ?? "";
                var employeeCode = csv.GetField("Employee_ID") ?? "";
                var name = csv.GetField("Name") ?? "";
                var department = csv.GetField("Department") ?? "";
                var shiftType = csv.GetField("Shift_Type") ?? "";
                var clockInStr = csv.GetField("Clock_In") ?? "";
                var clockOutStr = csv.GetField("Clock_Out") ?? "";
                var totalHoursStr = csv.GetField("Total_Hours") ?? "0";

                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
                {
                    result.Errors++;
                    result.ErrorMessages.Add($"Invalid date: {dateStr}");
                    continue;
                }

                // Normalize to UTC date-only (Kind=Utc midnight)
                var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

                if (!seenKeys.Add((employeeCode, dateUtc)))
                {
                    result.Skipped++;
                    continue;
                }

                bool isMissing = string.IsNullOrWhiteSpace(clockInStr);
                TimeSpan? clockIn = null;
                TimeSpan? clockOut = null;

                if (!isMissing && TimeSpan.TryParse(clockInStr, out var ci))
                    clockIn = ci;

                if (!string.IsNullOrWhiteSpace(clockOutStr) && TimeSpan.TryParse(clockOutStr, out var co))
                    clockOut = co;

                decimal.TryParse(totalHoursStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var totalHours);

                // Determine penalty: late > 30 min from position clock-in
                bool isPenalty = false;
                if (!isMissing && clockIn.HasValue)
                {
                    var employee = employees.FirstOrDefault(e =>
                        e.EmployeeCode.Equals(employeeCode, StringComparison.OrdinalIgnoreCase));

                    if (employee != null)
                    {
                        var posClockIn = employee.Position.ClockInTime;
                        var lateThreshold = posClockIn.Add(TimeSpan.FromMinutes(30));
                        if (clockIn.Value > lateThreshold)
                            isPenalty = true;
                    }
                    else
                    {
                        // Try to match by department/position name
                        var pos = positions.FirstOrDefault(p =>
                            p.Name.Equals(department, StringComparison.OrdinalIgnoreCase));
                        if (pos != null)
                        {
                            var lateThreshold = pos.ClockInTime.Add(TimeSpan.FromMinutes(30));
                            if (clockIn.Value > lateThreshold)
                                isPenalty = true;
                        }
                    }
                }

                var record = new TimeAttendanceRecord
                {
                    EmployeeCode = employeeCode,
                    EmployeeName = name,
                    Department = department,
                    Date = dateUtc,
                    ShiftType = shiftType,
                    ClockIn = clockIn,
                    ClockOut = clockOut,
                    TotalHours = isMissing ? 0 : totalHours,
                    IsMissing = isMissing,
                    IsPenalty = isPenalty && !isMissing,
                    Month = dateUtc.Month,
                    Year = dateUtc.Year
                };

                _db.TimeAttendanceRecords.Add(record);
                result.Imported++;

                if (result.Imported % 1000 == 0)
                {
                    await _db.SaveChangesAsync();
                    _db.ChangeTracker.Clear();
                }
            }
            catch (Exception ex)
            {
                result.Errors++;
                result.ErrorMessages.Add(ex.Message);
            }
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync();

        // Set month/year from first imported record
        var firstRecord = await _db.TimeAttendanceRecords
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();
        if (firstRecord != null)
        {
            result.Month = firstRecord.Month;
            result.Year = firstRecord.Year;
        }

        return result;
    }

    public async Task<List<TimeAttendanceRecordDto>> GetByMonthAsync(int year, int month)
    {
        var records = await _db.TimeAttendanceRecords
            .Where(t => t.Year == year && t.Month == month)
            .OrderBy(t => t.EmployeeCode)
            .ThenBy(t => t.Date)
            .ToListAsync();

        return records.Select(MapToDto).ToList();
    }

    public async Task<List<TimeAttendanceSummaryDto>> GetSummaryByMonthAsync(int year, int month)
    {
        var records = await _db.TimeAttendanceRecords
            .Where(t => t.Year == year && t.Month == month)
            .ToListAsync();

        return records
            .GroupBy(r => r.EmployeeCode)
            .Select(g => new TimeAttendanceSummaryDto
            {
                EmployeeCode = g.Key,
                EmployeeName = g.First().EmployeeName,
                TotalDays = g.Count(),
                WorkDays = g.Count(r => !r.IsMissing),
                PenaltyDays = g.Count(r => r.IsPenalty),
                MissingDays = g.Count(r => r.IsMissing),
                TotalHours = g.Sum(r => r.TotalHours)
            })
            .OrderBy(s => s.EmployeeCode)
            .ToList();
    }

    public async Task<List<(int Year, int Month)>> GetAvailableMonthsAsync()
    {
        var rows = await _db.TimeAttendanceRecords
            .Select(t => new { t.Year, t.Month })
            .Distinct()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync();

        return rows.Select(x => (x.Year, x.Month)).ToList();
    }

    public async Task DeleteByMonthAsync(int year, int month)
    {
        var records = await _db.TimeAttendanceRecords
            .Where(t => t.Year == year && t.Month == month)
            .ToListAsync();
        _db.TimeAttendanceRecords.RemoveRange(records);
        await _db.SaveChangesAsync();
    }

    private static TimeAttendanceRecordDto MapToDto(TimeAttendanceRecord r) => new()
    {
        Id = r.Id,
        EmployeeCode = r.EmployeeCode,
        EmployeeName = r.EmployeeName,
        Department = r.Department,
        Date = r.Date,
        ShiftType = r.ShiftType,
        ClockIn = r.ClockIn.HasValue ? r.ClockIn.Value.ToString(@"hh\:mm") : null,
        ClockOut = r.ClockOut.HasValue ? r.ClockOut.Value.ToString(@"hh\:mm") : null,
        TotalHours = r.TotalHours,
        IsMissing = r.IsMissing,
        IsPenalty = r.IsPenalty,
        Month = r.Month,
        Year = r.Year
    };
}
