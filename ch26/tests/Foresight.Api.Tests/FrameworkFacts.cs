using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Foresight.Api.Tests;

/// <summary>Framework facts the database code has to respect.</summary>
[Trait("Chapter", "3")]
public sealed class FrameworkFacts
{
    [Fact]
    public void EnsureCreated_NeverAddsATable_ToAnExistingDatabase()
    {
        var file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var options = $"Data Source={file};Pooling=False";

        using (var first = new FirstRelease(options))
            Assert.True(first.Database.EnsureCreated());

        // The next release has a second table. EnsureCreated sees that a
        // database exists, answers false and changes nothing.
        using var next = new NextRelease(options);
        Assert.False(next.Database.EnsureCreated());
        Assert.Throws<SqliteException>(() => next.Notes.Count());
    }

    private sealed class Account
    {
        public int Id { get; set; }
    }

    private sealed class Note
    {
        public int Id { get; set; }
    }

    private class FirstRelease : DbContext
    {
        private readonly string _connection;

        public FirstRelease(string connection)
        {
            _connection = connection;
        }

        public DbSet<Account> Accounts => Set<Account>();

        protected override void OnConfiguring(DbContextOptionsBuilder o) =>
            o.UseSqlite(_connection);
    }

    private sealed class NextRelease : FirstRelease
    {
        public NextRelease(string connection) : base(connection) { }

        public DbSet<Note> Notes => Set<Note>();
    }
}
