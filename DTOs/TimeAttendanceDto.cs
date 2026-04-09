namespace Hummingbird.API.DTOs;

public class TimeAttendanceRecordDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string ShiftType { get; set; } = string.Empty;
    public string? ClockIn { get; set; }
    public string? ClockOut { get; set; }
    public decimal TotalHours { get; set; }
    public bool IsMissing { get; set; }
    public bool IsPenalty { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
}

public class TimeAttendanceSummaryDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public int TotalDays { get; set; }
    public int WorkDays { get; set; }
    public int PenaltyDays { get; set; }
    public int MissingDays { get; set; }
    public decimal TotalHours { get; set; }
}

public class ImportResultDto
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public int Month { get; set; }
    public int Year { get; set; }
}
