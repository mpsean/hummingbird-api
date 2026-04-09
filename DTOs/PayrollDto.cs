namespace Hummingbird.API.DTOs;

public class PayrollRecordDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Deductions { get; set; }
    public decimal ServiceChargeBonus { get; set; }
    public decimal NetSalary { get; set; }
    public int TotalScheduledDays { get; set; }
    public int WorkDays { get; set; }
    public int PenaltyDays { get; set; }
    public int MissingDays { get; set; }
    public decimal ServiceChargeInput { get; set; }
    public string ServiceChargeVersion { get; set; } = "A";
    public DateTime CalculatedAt { get; set; }
}

public class CalculatePayrollDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal ServiceChargeTotal { get; set; }
}

public class PayrollSummaryDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int EmployeeCount { get; set; }
    public decimal TotalBaseSalary { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalServiceCharge { get; set; }
    public decimal TotalNetSalary { get; set; }
    public string ServiceChargeVersion { get; set; } = "A";
    public List<PayrollRecordDto> Records { get; set; } = new();
}
