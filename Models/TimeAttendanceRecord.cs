namespace Hummingbird.API.Models;

public class TimeAttendanceRecord
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty; // Employee_ID from CSV
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string ShiftType { get; set; } = string.Empty;
    public TimeSpan? ClockIn { get; set; }
    public TimeSpan? ClockOut { get; set; }
    public decimal TotalHours { get; set; }

    // Derived fields
    public bool IsMissing { get; set; }   // no show
    public bool IsPenalty { get; set; }   // late > 30 min

    public int Month { get; set; }
    public int Year { get; set; }
}
