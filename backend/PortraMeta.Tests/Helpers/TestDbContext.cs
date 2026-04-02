using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PortraMeta.Data;

namespace PortraMeta.Tests.Helpers;

public static class TestDbContext
{
    /// <summary>
    /// Creates an in-memory SQLite AppDbContext for testing.
    /// The caller should dispose both the returned context and the connection.
    /// </summary>
    public static (AppDbContext Db, SqliteConnection Connection) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        return (db, connection);
    }
}
