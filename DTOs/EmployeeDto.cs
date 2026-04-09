using Hummingbird.API.Models;

namespace Hummingbird.API.DTOs;

public class EmployeeDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public int PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime DateJoined { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateEmployeeDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public int PositionId { get; set; }
    public decimal Salary { get; set; }
    public DateTime DateJoined { get; set; }
    public string Status { get; set; } = "Active";
}

public class UpdateEmployeeDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public int PositionId { get; set; }
    public decimal Salary { get; set; }
    public DateTime DateJoined { get; set; }
    public string Status { get; set; } = string.Empty;
}
