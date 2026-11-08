using Microsoft.Data.Sqlite;

namespace Foresight.Api.Data;

/// <summary>
/// Makes sure at startup that the app can write its database, not only
/// read it. A database the app can only read lets every page load and
/// fails every write, which is the worst way to fail: it looks fine.
/// </summary>
public static class DatabaseCheck
{
    // SQLite's code for "attempt to write a readonly database".
    private const int ReadOnly = 8;

    public static void EnsureWritable(string databaseFile)
    {
        // A database that does not exist yet is created by this process,
        // so it is ours. There is nothing to check.
        if (!File.Exists(databaseFile)) return;

        // No pooling: the check must not keep a connection open.
        using var connection = new SqliteConnection(
            $"Data Source={databaseFile};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        // Takes the write lock and gives it back. Nothing changes.
        command.CommandText = "BEGIN IMMEDIATE; ROLLBACK;";
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == ReadOnly)
        {
            throw new InvalidOperationException(
                $"The database {databaseFile} can be read but not "
                + "written. Check who owns its -wal and -shm files: this "
                + "app must be allowed to write them.", ex);
        }
    }
}
