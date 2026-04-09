namespace Hummingbird.API.DTOs;

public class TenantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenantDetailDto : TenantDto
{
    public Dictionary<string, string> Config { get; set; } = new();
    public TenantStatsDto Stats { get; set; } = new();
}

public class TenantStatsDto
{
    public int ActiveEmployees { get; set; }
    public int TotalPositions { get; set; }
    public int AttendanceMonths { get; set; }
    public int PayrollMonths { get; set; }
    public string? LastPayrollMonth { get; set; }
}

public class CreateTenantDto
{
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string ServiceChargeVersion { get; set; } = "A";
}

public class UpdateTenantConfigDto
{
    public string Value { get; set; } = string.Empty;
}
