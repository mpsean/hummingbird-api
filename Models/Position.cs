namespace Hummingbird.API.Models;

public enum ShiftType { Morning, Afternoon, Evening }

public class Position
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DefaultSalary { get; set; }
    public ShiftType ShiftType { get; set; }
    public TimeSpan ClockInTime { get; set; }
    public TimeSpan ClockOutTime { get; set; }
    public decimal TotalHours { get; set; }

    // Used for Version A only (percentage of total service charge pool)
    public decimal ServiceChargePercentage { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
