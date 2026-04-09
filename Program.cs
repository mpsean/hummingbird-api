using Hummingbird.API.Data;
using Hummingbird.API.Middleware;
using Hummingbird.API.Services;
using k8s;
using Microsoft.EntityFrameworkCore;

// Npgsql 6+: allow DateTime with Kind=Unspecified (from JSON / EF seed data)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Hummingbird HR API", Version = "v1" });
});

// Master database (shared — stores tenant registry)
builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MasterConnection")));

// Tenant context (scoped — populated per-request by middleware)
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// Tenant database (scoped — connection string resolved from ITenantContext at request time)
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var tenant = sp.GetRequiredService<ITenantContext>();
    if (!tenant.IsResolved)
        throw new InvalidOperationException("Tenant not resolved. AppDbContext is unavailable for this route.");
    options.UseNpgsql(tenant.ConnectionString);
});

// Application services
builder.Services.AddScoped<TimeAttendanceService>();
builder.Services.AddScoped<PayrollService>();

// Kubernetes provisioning
var k8sEnabled = builder.Configuration.GetValue<bool>("Kubernetes:Enabled");
if (k8sEnabled)
{
    var k8sConfig = KubernetesClientConfiguration.IsInCluster()
        ? KubernetesClientConfiguration.InClusterConfig()
        : KubernetesClientConfiguration.BuildConfigFromConfigFile();
    builder.Services.AddSingleton<IKubernetes>(new Kubernetes(k8sConfig));
    builder.Services.AddScoped<IKubernetesProvisioningService, KubernetesProvisioningService>();
}
else
{
    builder.Services.AddScoped<IKubernetesProvisioningService, NoOpKubernetesProvisioningService>();
}

// CORS — allow any *.hmmbird.xyz subdomain + localhost for dev
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin =>
        {
            var host = new Uri(origin).Host;
            return host == "localhost" ||
                   host.EndsWith(".hmmbird.xyz") ||
                   host == "hmmbird.xyz";
        })
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ── Pipeline ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// Bootstrap master DB (creates schema if not exists)
using (var scope = app.Services.CreateScope())
{
    var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    masterDb.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Resolve tenant from subdomain on every non-admin request
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<JwtTenantAuthMiddleware>();

app.MapControllers();

app.Run();
