using Hummingbird.API.DTOs;
using Hummingbird.API.Models;
using Hummingbird.API.Services;
using Hummingbird.API.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hummingbird.API.Tests.Services;

/// <summary>
/// Tests for PayrollService business logic.
///
/// Seeded positions (from AppDbContext.OnModelCreating):
///   Id=1  Food and Beverage  salary=15000  SC%=60  clockIn=10:00
///   Id=2  Cleaner            salary=12000  SC%=20  clockIn=07:00
///   Id=3  Receptionist       salary=18000  SC%=20  clockIn=08:00
///   Id=4  Manager            salary=35000  SC%=0   clockIn=09:00
///
/// ServiceChargeVersion defaults to "A" (equal split per position).
/// </summary>
public class PayrollServiceTests
{
    private static Employee MakeEmployee(int id, string code, int positionId, decimal salary) => new()
    {
        Id = id,
        EmployeeCode = code,
        Name = "Test",
        Surname = "User",
        PositionId = positionId,
        Salary = salary,
        DateJoined = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Status = EmployeeStatus.Active
    };

    private static TimeAttendanceRecord MakeAttendance(
        string code, DateTime date, bool isMissing = false, bool isPenalty = false) => new()
    {
        EmployeeCode = code,
        EmployeeName = "Test User",
        Department = "Test",
        Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
        ShiftType = "Morning",
        ClockIn = isMissing ? null : new TimeSpan(8, 0, 0),
        ClockOut = isMissing ? null : new TimeSpan(17, 0, 0),
        TotalHours = isMissing ? 0 : 9,
        IsMissing = isMissing,
        IsPenalty = isPenalty && !isMissing,
        Month = date.Month,
        Year = date.Year
    };

    // -------------------------------------------------------------------------
    // Service charge — Version A (equal split)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CalculatePayroll_VersionA_SplitsServiceChargeEquallyPerPosition()
    {
        using var db = DbContextFactory.Create();
        // Both employees in position 1 (60% SC pool)
        db.Employees.AddRange(
            MakeEmployee(100, "EMP001", 1, 30000m),
            MakeEmployee(101, "EMP002", 1, 30000m));
        await db.SaveChangesAsync();

        var result = await new PayrollService(db).CalculatePayrollAsync(new CalculatePayrollDto
        {
            Year = 2026, Month = 4, ServiceChargeTotal = 1000m
        });

        // pool = 1000 * 60% = 600 / 2 employees = 300 each
        Assert.Equal(2, result.Records.Count);
        Assert.All(result.Records, r => Assert.Equal(300m, r.ServiceChargeBonus));
    }

    [Fact]
    public async Task CalculatePayroll_VersionA_TwoPositions_EachPoolSplitIndependently()
    {
        using var db = DbContextFactory.Create();
        // Position 1 (60%) — 1 employee, Position 2 (20%) — 2 employees
        db.Employees.AddRange(
            MakeEmployee(100, "EMP001", 1, 30000m),
            MakeEmployee(101, "EMP002", 2, 12000m),
            MakeEmployee(102, "EMP003", 2, 12000m));
        await db.SaveChangesAsync();

        var result = await new PayrollService(db).CalculatePayrollAsync(new CalculatePayrollDto
        {
            Year = 2026, Month = 4, ServiceChargeTotal = 1000m
        });

        var emp1 = result.Records.Single(r => r.EmployeeCode == "EMP001");
        var emp2 = result.Records.Single(r => r.EmployeeCode == "EMP002");
        var emp3 = result.Records.Single(r => r.EmployeeCode == "EMP003");

        Assert.Equal(600m, emp1.ServiceChargeBonus);   // 1000*60%/1
        Assert.Equal(100m, emp2.ServiceChargeBonus);   // 1000*20%/2
        Assert.Equal(100m, emp3.ServiceChargeBonus);
    }

    // -------------------------------------------------------------------------
    // Service charge — Version B (proportional by work days)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CalculatePayroll_VersionB_SplitsProportionallyByWorkDays()
    {
        using var db = DbContextFactory.Create();
        var config = await db.AppConfigs.FirstAsync(c => c.Key == "ServiceChargeVersion");
        config.Value = "B";
        await db.SaveChangesAsync();

        db.Employees.AddRange(
            MakeEmployee(100, "EMP001", 1, 30000m),
            MakeEmployee(101, "EMP002", 1, 30000m));
        await db.SaveChangesAsync();

        // EMP001: 20 work days, EMP002: 10 work days
        var baseDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 20; i++)
            db.TimeAttendanceRecords.Add(MakeAttendance("EMP001", baseDate.AddDays(i)));
        for (int i = 0; i < 10; i++)
            db.TimeAttendanceRecords.Add(MakeAttendance("EMP002", baseDate.AddDays(i)));
        await db.SaveChangesAsync();

        var result = await new PayrollService(db).CalculatePayrollAsync(new CalculatePayrollDto
        {
            Year = 2026, Month = 4, ServiceChargeTotal = 1000m
        });

        // pool = 600, total work days = 30, per day = 20
        // EMP001: 20 * 20 = 400, EMP002: 10 * 20 = 200
        var emp1 = result.Records.Single(r => r.EmployeeCode == "EMP001");
        var emp2 = result.Records.Single(r => r.EmployeeCode == "EMP002");
        Assert.Equal(400m, emp1.ServiceChargeBonus);
        Assert.Equal(200m, emp2.ServiceChargeBonus);
    }

    [Fact]
    public async Task CalculatePayroll_VersionB_AllMissing_ZeroServiceCharge()
    {
        using var db = DbContextFactory.Create();
        var config = await db.AppConfigs.FirstAsync(c => c.Key == "ServiceChargeVersion");
        config.Value = "B";
        await db.SaveChangesAsync();

        db.Employees.Add(MakeEmployee(100, "EMP001", 1, 30000m));
        await db.SaveChangesAsync();

        var date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        db.TimeAttendanceRecords.Add(MakeAttendance("EMP001", date, isMissing: true));
        await db.SaveChangesAsync();

        var result = await new PayrollService(db).CalculatePayrollAsync(new CalculatePayrollDto
        {
            Year = 2026, Month = 4, ServiceChargeTotal = 5000m
        });

        Assert.Equal(0m, result.Records.Single().ServiceChargeBonus);
    }

    // -------------------------------------------------------------------------
    // Deductions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CalculatePayroll_MissingDay_DeductsOneDailyWage()
    {
        using var db = DbContextFactory.Create();
        // Position 4 (Manager, 0% SC) keeps SC out of the equation
        db.Employees.Add(MakeEmployee(100, "EMP001", 4, 30000m));
        await db.SaveChangesAsync();

        db.TimeAttendanceRecords.Add(
            MakeAttendance("EMP001", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), isMissing: true));
        await db.SaveChangesAsync();

        var result = await new PayrollService(db).CalculatePayrollAsync(new CalculatePayrollDto
        {
            Year = 2026, Month = 4, ServiceChargeTotal = 0m
        });

        var record = result.Records.Single();
        // daily wage = 30000 / 30 = 1000
        Assert.Equal(1000m, record.Deductions);
        Assert.Equal(29000m, record.NetSalary);
        Assert.Equal(1, record.MissingDays);
    }

    [Fact]
    public async Task CalculatePayroll_PenaltyDay_DeductsTenPercentDailyWage()
    {
        using var db = DbContextFactory.Create();
        db.Employees.Add(MakeEmployee(100, "EMP001", 4, 30000m));
        await db.SaveChangesAsync();

        db.TimeAttendanceRecords.Add(
            MakeAttendance("EMP001", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), isPenalty: true));
        await db.SaveChangesAsync();

        var result = await new PayrollService(db).CalculatePayrollAsync(new CalculatePayrollDto
        {
            Year = 2026, Month = 4, ServiceChargeTotal = 0m
        });

        var record = result.Records.Single();
        // penalty = 1 * (30000 / 30) * 0.10 = 100
        Assert.Equal(100m, record.Deductions);
        Assert.Equal(29900m, record.NetSalary);
        Assert.Equal(1, record.PenaltyDays);
    }

    [Fact]
    public async Task CalculatePayroll_MissingAndPenaltyDays_DeductionsSummed()
    {
        using var db = DbContextFactory.Create();
        db.Employees.Add(MakeEmployee(100, "EMP001", 4, 30000m));
        await db.SaveChangesAsync();

        var base1 = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        db.TimeAttendanceRecords.AddRange(
            MakeAttendance("EMP001", base1, isMissing: true),
            MakeAttendance("EMP001", base1.AddDays(1), isPenalty: true),
            MakeAttendance("EMP001", base1.AddDays(2)));
        await db.SaveChangesAsync();

        var result = await new PayrollService(db).CalculatePayrollAsync(new CalculatePayrollDto
        {
            Year = 2026, Month = 4, ServiceChargeTotal = 0m
        });

        var record = result.Records.Single();
        // missing: 1000, penalty: 100, total: 1100
        Assert.Equal(1100m, record.Deductions);
        Assert.Equal(28900m, record.NetSalary);
    }

    [Fact]
    public async Task CalculatePayroll_NoAttendance_NoDeductions()
    {
        using var db = DbContextFactory.Create();
        db.Employees.Add(MakeEmployee(100, "EMP001", 4, 30000m));
        await db.SaveChangesAsync();

        var result = await new PayrollService(db).CalculatePayrollAsync(new CalculatePayrollDto
        {
            Year = 2026, Month = 4, ServiceChargeTotal = 0m
        });

        var record = result.Records.Single();
        Assert.Equal(0m, record.Deductions);
        Assert.Equal(30000m, record.NetSalary);
    }

    // -------------------------------------------------------------------------
    // Recalculation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CalculatePayroll_CalledTwice_ReplacesExistingRecords()
    {
        using var db = DbContextFactory.Create();
        db.Employees.Add(MakeEmployee(100, "EMP001", 4, 30000m));
        await db.SaveChangesAsync();

        var input = new CalculatePayrollDto { Year = 2026, Month = 4, ServiceChargeTotal = 0m };
        var svc = new PayrollService(db);

        await svc.CalculatePayrollAsync(input);
        await svc.CalculatePayrollAsync(input); // recalculate same month

        var count = await db.PayrollRecords.CountAsync(p => p.Year == 2026 && p.Month == 4);
        Assert.Equal(1, count); // must not duplicate records
    }

    // -------------------------------------------------------------------------
    // Query helpers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPayroll_NoRecords_ReturnsNull()
    {
        using var db = DbContextFactory.Create();
        var result = await new PayrollService(db).GetPayrollAsync(2026, 4);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCalculatedMonths_ReturnsDescendingOrder()
    {
        using var db = DbContextFactory.Create();
        db.Employees.Add(MakeEmployee(100, "EMP001", 4, 30000m));
        await db.SaveChangesAsync();

        var svc = new PayrollService(db);
        await svc.CalculatePayrollAsync(new CalculatePayrollDto { Year = 2026, Month = 2, ServiceChargeTotal = 0m });
        await svc.CalculatePayrollAsync(new CalculatePayrollDto { Year = 2026, Month = 4, ServiceChargeTotal = 0m });
        await svc.CalculatePayrollAsync(new CalculatePayrollDto { Year = 2026, Month = 1, ServiceChargeTotal = 0m });

        var months = await svc.GetCalculatedMonthsAsync();
        Assert.Equal(3, months.Count);
        Assert.Equal((2026, 4), months[0]);
        Assert.Equal((2026, 2), months[1]);
        Assert.Equal((2026, 1), months[2]);
    }
}
