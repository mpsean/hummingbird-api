using Hummingbird.API.Data;
using Hummingbird.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hummingbird.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly AppDbContext _db;

    public ConfigController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var configs = await _db.AppConfigs.OrderBy(c => c.Key).ToListAsync();
        return Ok(configs.ToDictionary(c => c.Key, c => c.Value));
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var config = await _db.AppConfigs.FirstOrDefaultAsync(c => c.Key == key);
        if (config == null) return NotFound();
        return Ok(new { key = config.Key, value = config.Value, description = config.Description });
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(string key, [FromBody] SetConfigDto dto)
    {
        if (key == "ServiceChargeVersion" && dto.Value != "A" && dto.Value != "B")
            return BadRequest(new { message = "ServiceChargeVersion must be 'A' or 'B'." });

        var config = await _db.AppConfigs.FirstOrDefaultAsync(c => c.Key == key);
        if (config == null)
        {
            config = new AppConfig { Key = key, Description = dto.Description ?? "" };
            _db.AppConfigs.Add(config);
        }

        config.Value = dto.Value;
        config.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Description))
            config.Description = dto.Description;

        await _db.SaveChangesAsync();
        return Ok(new { key = config.Key, value = config.Value });
    }

    [HttpPost("onboard")]
    public async Task<IActionResult> CompleteOnboarding([FromBody] OnboardingDto dto)
    {
        if (dto.ServiceChargeVersion != "A" && dto.ServiceChargeVersion != "B")
            return BadRequest(new { message = "ServiceChargeVersion must be 'A' or 'B'." });

        await UpsertConfig("ServiceChargeVersion", dto.ServiceChargeVersion, "A = Fix-rate, B = Workday-rate");
        await UpsertConfig("CompanyName", dto.CompanyName, "Company / property name");
        await UpsertConfig("Onboarded", "true", "Whether initial setup has been completed");

        await _db.SaveChangesAsync();
        return Ok(new { message = "Onboarding complete.", version = dto.ServiceChargeVersion });
    }

    private async Task UpsertConfig(string key, string value, string description)
    {
        var config = await _db.AppConfigs.FirstOrDefaultAsync(c => c.Key == key);
        if (config == null)
        {
            _db.AppConfigs.Add(new AppConfig
            {
                Key = key, Value = value, Description = description, UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            config.Value = value;
            config.UpdatedAt = DateTime.UtcNow;
        }
    }
}

public class SetConfigDto
{
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class OnboardingDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string ServiceChargeVersion { get; set; } = "A";
}
