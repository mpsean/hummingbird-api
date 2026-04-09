using Hummingbird.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Hummingbird.API.Data;

public class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Tenant>(t =>
        {
            t.HasIndex(x => x.Subdomain).IsUnique();
            t.HasIndex(x => x.DatabaseName).IsUnique();
        });
    }
}
