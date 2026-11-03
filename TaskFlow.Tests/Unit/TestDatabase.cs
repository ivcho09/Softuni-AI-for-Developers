using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;

namespace TaskFlow.Tests.Unit;

/// <summary>
/// A private SQLite database for a single unit test.
/// <para>
/// This is real SQLite running in memory, not the EF in-memory provider, so the tests
/// exercise the same SQL translation, constraints and delete behaviour as production.
/// The connection is held open for the lifetime of the test because an in-memory SQLite
/// database is destroyed as soon as its last connection closes. Each test gets a fresh
/// instance, which is what keeps the suite repeatable and order-independent.
/// </para>
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TaskFlowDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TaskFlowDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Built from the real migrations, so the tests run against the shipped schema.
        using var migrationContext = new TaskFlowDbContext(_options);
        migrationContext.Database.Migrate();
    }

    /// <summary>
    /// Creates a context with an empty change tracker. Assertions use a fresh context so
    /// that they read what was actually persisted rather than a cached in-memory entity.
    /// </summary>
    public TaskFlowDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
