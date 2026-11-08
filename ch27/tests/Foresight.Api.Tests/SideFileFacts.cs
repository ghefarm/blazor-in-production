using Microsoft.Data.Sqlite;

namespace Foresight.Api.Tests;

/// <summary>
/// What SQLite does with its two side files, and what that means for an
/// app that has to write. Each test works in a folder of its own.
/// </summary>
[Trait("Chapter", "25")]
public sealed class SideFileFacts : IDisposable
{
    private readonly string _folder =
        Directory.CreateTempSubdirectory("foresight-sqlite-").FullName;

    private string PathOf(string name) => Path.Combine(_folder, name);

    [Fact]
    public void AReadOnlyVisit_LeavesSideFiles_ThatCanStopEveryWrite()
    {
        var db = PathOf("app.db");
        var app = $"Data Source={db};Pooling=False";
        Run(app, "PRAGMA journal_mode=WAL; CREATE TABLE t(x);");

        // A script that only reads. It leaves the side files behind.
        Run(app + ";Mode=ReadOnly", "SELECT count(*) FROM t;");
        string[] side = [db + "-wal", db + "-shm"];
        Assert.All(side, s => Assert.True(File.Exists(s)));

        // Made by another account: this process may read them, not write.
        foreach (var s in side)
            File.SetAttributes(s, FileAttributes.ReadOnly);

        Assert.Equal("0", Scalar(app, "SELECT count(*) FROM t;"));
        var refused = Assert.Throws<SqliteException>(
            () => Run(app, "INSERT INTO t VALUES (1);"));
        Assert.Equal(
            "SQLite Error 8: 'attempt to write a readonly database'.",
            refused.Message);
    }

    private static void Run(string connectionString, string sql)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string? Scalar(string connectionString, string sql)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar());
    }

    public void Dispose()
    {
        foreach (var f in Directory.GetFiles(_folder))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_folder, recursive: true);
    }
}
