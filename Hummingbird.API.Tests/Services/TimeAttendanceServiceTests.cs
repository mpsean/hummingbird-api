using Hummingbird.API.Models;
using Hummingbird.API.Services;
using Hummingbird.API.Tests.Helpers;
using Xunit;

namespace Hummingbird.API.Tests.Services;

public class TimeAttendanceServiceTests
{
    private static TimeAttendanceRecord MakeRecord(
        string code, DateTime date, bool isMissing = false, bool isPenalty = false) => new()
    {
        EmployeeCode = code,
        EmployeeName = "Test User",
        Department = "Test Dept",
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
    // GetSummaryByMonthAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSummaryByMonth_ReturnsCorrectCounts()
    {
        using var db = DbContextFactory.Create();
        var d = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        db.TimeAttendanceRecords.AddRange(
            MakeRecord("EMP001", d),
            MakeRecord("EMP001", d.AddDays(1), isMissing: true),
            MakeRecord("EMP001", d.AddDays(2), isPenalty: true));
        await db.SaveChangesAsync();

        var summary = await new TimeAttendanceService(db).GetSummaryByMonthAsync(2026, 4);

        Assert.Single(summary);
        var s = summary[0];
        Assert.Equal("EMP001", s.EmployeeCode);
        Assert.Equal(3, s.TotalDays);
        Assert.Equal(2, s.WorkDays);     // not-missing days
        Assert.Equal(1, s.MissingDays);
        Assert.Equal(1, s.PenaltyDays);
    }

    [Fact]
    public async Task GetSummaryByMonth_MultipleEmployees_GroupedCorrectly()
    {
        using var db = DbContextFactory.Create();
        var d = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        db.TimeAttendanceRecords.AddRange(
            MakeRecord("EMP001", d),
            MakeRecord("EMP001", d.AddDays(1)),
            MakeRecord("EMP002", d));
        await db.SaveChangesAsync();

        var summary = await new TimeAttendanceService(db).GetSummaryByMonthAsync(2026, 4);

        Assert.Equal(2, summary.Count);
        Assert.Equal(2, summary.Single(s => s.EmployeeCode == "EMP001").TotalDays);
        Assert.Equal(1, summary.Single(s => s.EmployeeCode == "EMP002").TotalDays);
    }

    [Fact]
    public async Task GetSummaryByMonth_NoRecords_ReturnsEmpty()
    {
        using var db = DbContextFactory.Create();
        var summary = await new TimeAttendanceService(db).GetSummaryByMonthAsync(2026, 4);
        Assert.Empty(summary);
    }

    [Fact]
    public async Task GetSummaryByMonth_TotalHours_SumsCorrectly()
    {
        using var db = DbContextFactory.Create();
        var d = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        db.TimeAttendanceRecords.AddRange(
            MakeRecord("EMP001", d),              // 9 hours
            MakeRecord("EMP001", d.AddDays(1)));   // 9 hours
        await db.SaveChangesAsync();

        var summary = await new TimeAttendanceService(db).GetSummaryByMonthAsync(2026, 4);
        Assert.Equal(18m, summary[0].TotalHours);
    }

    // -------------------------------------------------------------------------
    // GetByMonthAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByMonth_ReturnsOnlyRequestedMonth()
    {
        using var db = DbContextFactory.Create();
        db.TimeAttendanceRecords.AddRange(
            MakeRecord("EMP001", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakeRecord("EMP001", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var records = await new TimeAttendanceService(db).GetByMonthAsync(2026, 4);

        Assert.Single(records);
        Assert.Equal(4, records[0].Month);
        Assert.Equal(2026, records[0].Year);
    }

    [Fact]
    public async Task GetByMonth_NoRecords_ReturnsEmpty()
    {
        using var db = DbContextFactory.Create();
        var records = await new TimeAttendanceService(db).GetByMonthAsync(2026, 4);
        Assert.Empty(records);
    }

    [Fact]
    public async Task GetByMonth_MapsIsMissingAndIsPenaltyCorrectly()
    {
        using var db = DbContextFactory.Create();
        var d = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        db.TimeAttendanceRecords.AddRange(
            MakeRecord("EMP001", d, isMissing: true),
            MakeRecord("EMP001", d.AddDays(1), isPenalty: true));
        await db.SaveChangesAsync();

        var records = await new TimeAttendanceService(db).GetByMonthAsync(2026, 4);

        var missing = records.Single(r => r.IsMissing);
        var penalty = records.Single(r => r.IsPenalty);
        Assert.Null(missing.ClockIn);
        Assert.NotNull(penalty.ClockIn);
    }

    // -------------------------------------------------------------------------
    // DeleteByMonthAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteByMonth_RemovesOnlySpecifiedMonth()
    {
        using var db = DbContextFactory.Create();
        db.TimeAttendanceRecords.AddRange(
            MakeRecord("EMP001", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakeRecord("EMP001", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var svc = new TimeAttendanceService(db);
        await svc.DeleteByMonthAsync(2026, 4);

        Assert.Single(await svc.GetByMonthAsync(2026, 3));
        Assert.Empty(await svc.GetByMonthAsync(2026, 4));
    }

    [Fact]
    public async Task DeleteByMonth_NoRecords_DoesNotThrow()
    {
        using var db = DbContextFactory.Create();
        var exception = await Record.ExceptionAsync(() =>
            new TimeAttendanceService(db).DeleteByMonthAsync(2026, 4));
        Assert.Null(exception);
    }

    // -------------------------------------------------------------------------
    // GetAvailableMonthsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAvailableMonths_ReturnsSortedDescending()
    {
        using var db = DbContextFactory.Create();
        db.TimeAttendanceRecords.AddRange(
            MakeRecord("EMP001", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakeRecord("EMP001", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            MakeRecord("EMP001", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var months = await new TimeAttendanceService(db).GetAvailableMonthsAsync();

        Assert.Equal(3, months.Count);
        Assert.Equal((2026, 4), months[0]);
        Assert.Equal((2026, 2), months[1]);
        Assert.Equal((2026, 1), months[2]);
    }

    [Fact]
    public async Task GetAvailableMonths_NoDuplicateMonths()
    {
        using var db = DbContextFactory.Create();
        var d = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        // Two records in the same month for different employees
        db.TimeAttendanceRecords.AddRange(
            MakeRecord("EMP001", d),
            MakeRecord("EMP002", d));
        await db.SaveChangesAsync();

        var months = await new TimeAttendanceService(db).GetAvailableMonthsAsync();
        Assert.Single(months);
    }
}
