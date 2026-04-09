namespace Hummingbird.API.Models;

public enum EmployeeStatus { Active, Terminated }

public class Employee
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty; // matches Employee_ID in CSV
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public decimal Salary { get; set; }
    public DateTime DateJoined { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public ICollection<PayrollRecord> PayrollRecords { get; set; } = new List<PayrollRecord>();
}
