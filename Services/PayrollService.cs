using Hummingbird.API.Data;
using Hummingbird.API.DTOs;
using Hummingbird.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Hummingbird.API.Services;

public class PayrollService
{
    private readonly AppDbContext _db;

    public PayrollService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PayrollSummaryDto> CalculatePayrollAsync(CalculatePayrollDto input)
    {
        var version = await GetServiceChargeVersionAsync();

        var employees = await _db.Employees
            .Include(e => e.Position)
            .Where(e => e.Status == EmployeeStatus.Active)
            .ToListAsync();

        var attendanceRecords = await _db.TimeAttendanceRecords
            .Where(t => t.Year == input.Year && t.Month == input.Month)
            .ToListAsync();

        // Remove old payroll records for this month if recalculating
        var existing = await _db.PayrollRecords
            .Where(p => p.Year == input.Year && p.Month == input.Month)
            .ToListAsync();
        _db.PayrollRecords.RemoveRange(existing);
        await _db.SaveChangesAsync();

        // Group attendance by employee code
        var attendanceByEmployee = attendanceRecords
            .GroupBy(a => a.EmployeeCode)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Calculate service charge bonuses
        var servicechargeBonuses = CalculateServiceChargeBonuses(
            employees, attendanceByEmployee, input.ServiceChargeTotal, version);

        var payrollRecords = new List<PayrollRecord>();

        foreach (var emp in employees)
        {
            var empAttendance = attendanceByEmployee.TryGetValue(emp.EmployeeCode, out var att)
                ? att : new List<TimeAttendanceRecord>();

            int totalDays = empAttendance.Count;
            int missingDays = empAttendance.Count(a => a.IsMissing);
            int penaltyDays = empAttendance.Count(a => a.IsPenalty);
            int workDays = totalDays - missingDays;

            // Daily wage based on 30-day month standard
            decimal dailyWage = emp.Salary / 30m;

            // Deductions
            decimal missingDeduction = missingDays * dailyWage;
            decimal penaltyDeduction = penaltyDays * dailyWage * 0.10m;
            decimal totalDeductions = missingDeduction + penaltyDeduction;

            decimal serviceChargeBonus = servicechargeBonuses.TryGetValue(emp.Id, out var scb) ? scb : 0;

            var record = new PayrollRecord
            {
                EmployeeId = emp.Id,
                Month = input.Month,
                Year = input.Year,
                BaseSalary = emp.Salary,
                Deductions = Math.Round(totalDeductions, 2),
                ServiceChargeBonus = Math.Round(serviceChargeBonus, 2),
                NetSalary = Math.Round(emp.Salary - totalDeductions + serviceChargeBonus, 2),
                TotalScheduledDays = totalDays,
                WorkDays = workDays,
                PenaltyDays = penaltyDays,
                MissingDays = missingDays,
                ServiceChargeInput = input.ServiceChargeTotal,
                ServiceChargeVersion = version,
                CalculatedAt = DateTime.UtcNow
            };

            payrollRecords.Add(record);
        }

        _db.PayrollRecords.AddRange(payrollRecords);
        await _db.SaveChangesAsync();

        return await BuildSummaryAsync(input.Year, input.Month);
    }

    public async Task<PayrollSummaryDto?> GetPayrollAsync(int year, int month)
    {
        var hasRecords = await _db.PayrollRecords
            .AnyAsync(p => p.Year == year && p.Month == month);
        if (!hasRecords) return null;

        return await BuildSummaryAsync(year, month);
    }

    public async Task<List<(int Year, int Month)>> GetCalculatedMonthsAsync()
    {
        var rows = await _db.PayrollRecords
            .Select(p => new { p.Year, p.Month })
            .Distinct()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync();

        return rows.Select(x => (x.Year, x.Month)).ToList();
    }

    private async Task<PayrollSummaryDto> BuildSummaryAsync(int year, int month)
    {
        var records = await _db.PayrollRecords
            .Include(p => p.Employee)
                .ThenInclude(e => e.Position)
            .Where(p => p.Year == year && p.Month == month)
            .OrderBy(p => p.Employee.EmployeeCode)
            .ToListAsync();

        var version = records.FirstOrDefault()?.ServiceChargeVersion ?? "A";

        return new PayrollSummaryDto
        {
            Month = month,
            Year = year,
            EmployeeCount = records.Count,
            TotalBaseSalary = records.Sum(r => r.BaseSalary),
            TotalDeductions = records.Sum(r => r.Deductions),
            TotalServiceCharge = records.Sum(r => r.ServiceChargeBonus),
            TotalNetSalary = records.Sum(r => r.NetSalary),
            ServiceChargeVersion = version,
            Records = records.Select(r => new PayrollRecordDto
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                EmployeeCode = r.Employee.EmployeeCode,
                EmployeeName = $"{r.Employee.Name} {r.Employee.Surname}",
                PositionName = r.Employee.Position.Name,
                Month = r.Month,
                Year = r.Year,
                BaseSalary = r.BaseSalary,
                Deductions = r.Deductions,
                ServiceChargeBonus = r.ServiceChargeBonus,
                NetSalary = r.NetSalary,
                TotalScheduledDays = r.TotalScheduledDays,
                WorkDays = r.WorkDays,
                PenaltyDays = r.PenaltyDays,
                MissingDays = r.MissingDays,
                ServiceChargeInput = r.ServiceChargeInput,
                ServiceChargeVersion = r.ServiceChargeVersion,
                CalculatedAt = r.CalculatedAt
            }).ToList()
        };
    }

    private Dictionary<int, decimal> CalculateServiceChargeBonuses(
        List<Employee> employees,
        Dictionary<string, List<TimeAttendanceRecord>> attendanceByEmployee,
        decimal totalServiceCharge,
        string version)
    {
        var result = new Dictionary<int, decimal>();

        // Group employees by position
        var byPosition = employees.GroupBy(e => e.PositionId).ToList();

        foreach (var posGroup in byPosition)
        {
            var position = posGroup.First().Position;
            decimal positionPool = totalServiceCharge * (position.ServiceChargePercentage / 100m);
            var posEmployees = posGroup.ToList();

            if (version == "A")
            {
                // Fix-rate: split equally among all employees in the position
                if (posEmployees.Count == 0) continue;
                decimal perEmployee = positionPool / posEmployees.Count;
                foreach (var emp in posEmployees)
                    result[emp.Id] = perEmployee;
            }
            else // Version B
            {
                // Workday-rate: split proportionally by work days
                var workDaysByEmp = posEmployees.ToDictionary(
                    emp => emp.Id,
                    emp =>
                    {
                        if (!attendanceByEmployee.TryGetValue(emp.EmployeeCode, out var att))
                            return 0;
                        return att.Count(a => !a.IsMissing);
                    });

                int totalWorkDays = workDaysByEmp.Values.Sum();
                if (totalWorkDays == 0)
                {
                    foreach (var emp in posEmployees) result[emp.Id] = 0;
                    continue;
                }

                decimal perWorkday = positionPool / totalWorkDays;
                foreach (var emp in posEmployees)
                    result[emp.Id] = perWorkday * workDaysByEmp[emp.Id];
            }
        }

        return result;
    }

    private async Task<string> GetServiceChargeVersionAsync()
    {
        var config = await _db.AppConfigs
            .FirstOrDefaultAsync(c => c.Key == "ServiceChargeVersion");
        return config?.Value ?? "A";
    }
}
