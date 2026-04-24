using Hummingbird.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Hummingbird.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<TimeAttendanceRecord> TimeAttendanceRecords => Set<TimeAttendanceRecord>();
    public DbSet<PayrollRecord> PayrollRecords => Set<PayrollRecord>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.Salary).HasColumnType("numeric(18,2)");
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Position)
             .WithMany(p => p.Employees)
             .HasForeignKey(x => x.PositionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Position>(p =>
        {
            p.Property(x => x.DefaultSalary).HasColumnType("numeric(18,2)");
            p.Property(x => x.ServiceChargePercentage).HasColumnType("numeric(5,2)");
            p.Property(x => x.TotalHours).HasColumnType("numeric(5,2)");
            p.Property(x => x.ShiftType).HasConversion<string>();
        });

        modelBuilder.Entity<TimeAttendanceRecord>(t =>
        {
            t.HasIndex(x => new { x.EmployeeCode, x.Date }).IsUnique();
            t.HasIndex(x => new { x.Year, x.Month });
            t.Property(x => x.TotalHours).HasColumnType("numeric(5,2)");
        });

        modelBuilder.Entity<PayrollRecord>(p =>
        {
            p.HasIndex(x => new { x.EmployeeId, x.Month, x.Year }).IsUnique();
            p.HasIndex(x => new { x.Year, x.Month });
            p.Property(x => x.BaseSalary).HasColumnType("numeric(18,2)");
            p.Property(x => x.Deductions).HasColumnType("numeric(18,2)");
            p.Property(x => x.ServiceChargeBonus).HasColumnType("numeric(18,2)");
            p.Property(x => x.NetSalary).HasColumnType("numeric(18,2)");
            p.Property(x => x.ServiceChargeInput).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<AppConfig>(c =>
        {
            c.HasIndex(x => x.Key).IsUnique();
        });

        // Seed default positions
        modelBuilder.Entity<Position>().HasData(
            new Position
            {
                Id = 1,
                Name = "Food and Beverage",
                DefaultSalary = 15000,
                ShiftType = ShiftType.Morning,
                ClockInTime = new TimeSpan(10, 0, 0),
                ClockOutTime = new TimeSpan(14, 0, 0),
                TotalHours = 4,
                ServiceChargePercentage = 60
            },
            new Position
            {
                Id = 2,
                Name = "Cleaner",
                DefaultSalary = 12000,
                ShiftType = ShiftType.Morning,
                ClockInTime = new TimeSpan(7, 0, 0),
                ClockOutTime = new TimeSpan(16, 0, 0),
                TotalHours = 9,
                ServiceChargePercentage = 20
            },
            new Position
            {
                Id = 3,
                Name = "Receptionist",
                DefaultSalary = 18000,
                ShiftType = ShiftType.Morning,
                ClockInTime = new TimeSpan(8, 0, 0),
                ClockOutTime = new TimeSpan(17, 0, 0),
                TotalHours = 9,
                ServiceChargePercentage = 20
            },
            new Position
            {
                Id = 4,
                Name = "Manager",
                DefaultSalary = 35000,
                ShiftType = ShiftType.Morning,
                ClockInTime = new TimeSpan(9, 0, 0),
                ClockOutTime = new TimeSpan(17, 0, 0),
                TotalHours = 8,
                ServiceChargePercentage = 0
            }
        );

        // Seed default config
        modelBuilder.Entity<AppConfig>().HasData(
            new AppConfig
            {
                Id = 1,
                Key = "ServiceChargeVersion",
                Value = "A",
                Description = "A = Fix-rate per employee, B = Workday-rate",
                UpdatedAt = DateTime.UtcNow
            },
            new AppConfig
            {
                Id = 2,
                Key = "CompanyName",
                Value = "My Hotel",
                Description = "Company / property name",
                UpdatedAt = DateTime.UtcNow
            },
            new AppConfig
            {
                Id = 3,
                Key = "Onboarded",
                Value = "false",
                Description = "Whether initial setup has been completed",
                UpdatedAt = DateTime.UtcNow
            }
        );
    }
}
