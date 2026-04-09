namespace Hummingbird.API.Models;

public class PayrollRecord
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int Month { get; set; }
    public int Year { get; set; }

    public decimal BaseSalary { get; set; }          // from personnel
    public decimal Deductions { get; set; }           // penalty + missing day deductions
    public decimal ServiceChargeBonus { get; set; }   // from service charge input
    public decimal NetSalary { get; set; }            // BaseSalary - Deductions + ServiceChargeBonus

    public int TotalScheduledDays { get; set; }
    public int WorkDays { get; set; }                 // days actually worked (not missing)
    public int PenaltyDays { get; set; }
    public int MissingDays { get; set; }

    public decimal ServiceChargeInput { get; set; }  // total SC revenue entered for this month
    public string ServiceChargeVersion { get; set; } = "A";

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
