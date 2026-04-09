namespace Hummingbird.API.DTOs;

public class PositionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DefaultSalary { get; set; }
    public string ShiftType { get; set; } = string.Empty;
    public string ClockInTime { get; set; } = string.Empty;   // "HH:mm"
    public string ClockOutTime { get; set; } = string.Empty;  // "HH:mm"
    public decimal TotalHours { get; set; }
    public decimal ServiceChargePercentage { get; set; }
    public int EmployeeCount { get; set; }
}

public class CreatePositionDto
{
    public string Name { get; set; } = string.Empty;
    public decimal DefaultSalary { get; set; }
    public string ShiftType { get; set; } = "Morning";
    public string ClockInTime { get; set; } = "08:00";
    public string ClockOutTime { get; set; } = "17:00";
    public decimal TotalHours { get; set; }
    public decimal ServiceChargePercentage { get; set; }
}

public class UpdatePositionDto
{
    public string Name { get; set; } = string.Empty;
    public decimal DefaultSalary { get; set; }
    public string ShiftType { get; set; } = string.Empty;
    public string ClockInTime { get; set; } = string.Empty;
    public string ClockOutTime { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public decimal ServiceChargePercentage { get; set; }
}
