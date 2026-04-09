using Hummingbird.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Hummingbird.API.Tests.Helpers;

public static class DbContextFactory
{
    /// <summary>
    /// Creates an isolated InMemory AppDbContext with seed data applied.
    /// Each call gets its own database so tests do not interfere with each other.
    /// </summary>
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated(); // applies HasData seeds (positions, app config)
        return context;
    }
}
