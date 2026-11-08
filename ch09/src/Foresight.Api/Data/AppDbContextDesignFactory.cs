using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Foresight.Api.Data;

/// <summary>
/// Lets the dotnet ef tools build the context without starting the app.
/// Program.cs refuses to start without its secrets, and a migration needs
/// none of them.
/// </summary>
public sealed class AppDbContextDesignFactory
    : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options);
}
