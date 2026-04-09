using Hummingbird.API.Data;
using Hummingbird.API.DTOs;
using Hummingbird.API.Filters;
using Hummingbird.API.Models;
using Hummingbird.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hummingbird.API.Controllers;

[ApiController]
[Route("api/admin")]
[AdminAuth]
public class AdminController : ControllerBase
{
    private readonly MasterDbContext _master;
    private readonly IConfiguration _config;
    private readonly IKubernetesProvisioningService _k8s;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        MasterDbContext master,
        IConfiguration config,
        IKubernetesProvisioningService k8s,
        ILogger<AdminController> logger)
    {
        _master = master;
        _config = config;
        _k8s    = k8s;
        _logger = logger;
    }

    // ── Tenants ───────────────────────────────────────────────────────────────

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants()
    {
        var tenants = await _master.Tenants.OrderBy(t => t.CreatedAt).ToListAsync();
        return Ok(tenants.Select(MapToDto));
    }

    [HttpGet("tenants/{id}")]
    public async Task<IActionResult> GetTenant(int id)
    {
        var tenant = await _master.Tenants.FindAsync(id);
        if (tenant == null) return NotFound();

        var detail = new TenantDetailDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Subdomain = tenant.Subdomain,
            DatabaseName = tenant.DatabaseName,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
        };

        try
        {
            using var tenantDb = OpenTenantDb(tenant.ConnectionString);
            detail.Config = await tenantDb.AppConfigs
                .ToDictionaryAsync(c => c.Key, c => c.Value);
            detail.Stats = await GetStatsAsync(tenantDb);
        }
        catch
        {
            // Tenant DB unreachable — return partial data
        }

        return Ok(detail);
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantDto dto)
    {
        var subdomain = dto.Subdomain.ToLowerInvariant().Trim();

        if (string.IsNullOrWhiteSpace(subdomain) || !System.Text.RegularExpressions.Regex.IsMatch(subdomain, @"^[a-z0-9\-]+$"))
            return BadRequest(new { message = "Subdomain must be lowercase alphanumeric/hyphens only." });

        if (dto.ServiceChargeVersion != "A" && dto.ServiceChargeVersion != "B")
            return BadRequest(new { message = "ServiceChargeVersion must be 'A' or 'B'." });

        if (await _master.Tenants.AnyAsync(t => t.Subdomain == subdomain))
            return Conflict(new { message = $"Subdomain '{subdomain}' already in use." });

        var dbName = $"hummingbird_{subdomain.Replace("-", "_")}";
        var connStr = BuildConnectionString(dbName);

        // Provision database
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connStr);
            using var tenantDb = new AppDbContext(optionsBuilder.Options);

            await tenantDb.Database.MigrateAsync();

            // Override seed defaults with admin-provided values
            await UpsertConfig(tenantDb, "ServiceChargeVersion", dto.ServiceChargeVersion);
            await UpsertConfig(tenantDb, "CompanyName", dto.Name);
            await UpsertConfig(tenantDb, "Onboarded", "true");
            await tenantDb.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Failed to provision database: {ex.Message}" });
        }

        var tenant = new Tenant
        {
            Name = dto.Name,
            Subdomain = subdomain,
            DatabaseName = dbName,
            ConnectionString = connStr,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _master.Tenants.Add(tenant);
        await _master.SaveChangesAsync();

        // K8s provisioning is non-fatal — DB tenant always takes precedence
        var k8sResult = await _k8s.ProvisionTenantAsync(subdomain);
        if (!k8sResult.Success)
        {
            _logger.LogWarning(
                "Tenant {Subdomain} (id={Id}) created in DB but K8s provisioning failed: {Error}",
                subdomain, tenant.Id, k8sResult.ErrorMessage);
            return StatusCode(201, new
            {
                tenant  = MapToDto(tenant),
                warning = $"Tenant created but Kubernetes provisioning failed: {k8sResult.ErrorMessage}"
            });
        }

        return CreatedAtAction(nameof(GetTenant), new { id = tenant.Id }, MapToDto(tenant));
    }

    [HttpPatch("tenants/{id}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetActiveDto dto)
    {
        var tenant = await _master.Tenants.FindAsync(id);
        if (tenant == null) return NotFound();

        tenant.IsActive = dto.IsActive;
        await _master.SaveChangesAsync();

        return Ok(new { message = $"Tenant '{tenant.Subdomain}' is now {(dto.IsActive ? "active" : "inactive")}." });
    }

    // ── Tenant Config ─────────────────────────────────────────────────────────

    [HttpGet("tenants/{id}/config")]
    public async Task<IActionResult> GetConfig(int id)
    {
        var tenant = await _master.Tenants.FindAsync(id);
        if (tenant == null) return NotFound();

        using var tenantDb = OpenTenantDb(tenant.ConnectionString);
        var config = await tenantDb.AppConfigs.ToDictionaryAsync(c => c.Key, c => c.Value);
        return Ok(config);
    }

    [HttpPut("tenants/{id}/config/{key}")]
    public async Task<IActionResult> SetConfig(int id, string key, [FromBody] UpdateTenantConfigDto dto)
    {
        if (key == "ServiceChargeVersion" && dto.Value != "A" && dto.Value != "B")
            return BadRequest(new { message = "ServiceChargeVersion must be 'A' or 'B'." });

        var tenant = await _master.Tenants.FindAsync(id);
        if (tenant == null) return NotFound();

        using var tenantDb = OpenTenantDb(tenant.ConnectionString);
        await UpsertConfig(tenantDb, key, dto.Value);
        await tenantDb.SaveChangesAsync();

        return Ok(new { key, value = dto.Value });
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    [HttpGet("tenants/{id}/stats")]
    public async Task<IActionResult> GetStats(int id)
    {
        var tenant = await _master.Tenants.FindAsync(id);
        if (tenant == null) return NotFound();

        using var tenantDb = OpenTenantDb(tenant.ConnectionString);
        var stats = await GetStatsAsync(tenantDb);
        return Ok(stats);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private AppDbContext OpenTenantDb(string connectionString)
    {
        var opt = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(opt);
    }

    private string BuildConnectionString(string dbName)
    {
        var template = _config["TenantDbTemplate"]
            ?? "Host=localhost;Port=5432;Database={db};Username=postgres;Password=postgres";
        return template.Replace("{db}", dbName);
    }

    private static async Task UpsertConfig(AppDbContext db, string key, string value)
    {
        var cfg = await db.AppConfigs.FirstOrDefaultAsync(c => c.Key == key);
        if (cfg != null)
            cfg.Value = value;
        // If not yet seeded (edge case), the migration seed will handle it on next access
    }

    private static async Task<TenantStatsDto> GetStatsAsync(AppDbContext db)
    {
        var lastPayroll = await db.PayrollRecords
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .Select(p => new { p.Year, p.Month })
            .FirstOrDefaultAsync();

        return new TenantStatsDto
        {
            ActiveEmployees = await db.Employees.CountAsync(e => e.Status == EmployeeStatus.Active),
            TotalPositions = await db.Positions.CountAsync(),
            AttendanceMonths = await db.TimeAttendanceRecords
                .Select(t => new { t.Year, t.Month }).Distinct().CountAsync(),
            PayrollMonths = await db.PayrollRecords
                .Select(p => new { p.Year, p.Month }).Distinct().CountAsync(),
            LastPayrollMonth = lastPayroll != null
                ? $"{lastPayroll.Month:D2}/{lastPayroll.Year}"
                : null
        };
    }

    private static TenantDto MapToDto(Tenant t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Subdomain = t.Subdomain,
        DatabaseName = t.DatabaseName,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt
    };
}

public class SetActiveDto
{
    public bool IsActive { get; set; }
}
