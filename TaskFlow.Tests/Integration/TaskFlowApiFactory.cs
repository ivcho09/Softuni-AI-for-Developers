using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskFlow.Tests.Integration;

/// <summary>
/// Boots the real application in memory for the integration tests.
/// <para>
/// Only the connection string is overridden, so the pipeline under test is the genuine
/// one: the same middleware, the same validation filter, the same controllers and the
/// same EF Core configuration. Each factory instance points at its own SQLite file in
/// the temp directory, which is deleted on disposal, so a test run never touches the
/// application's own taskflow.db and test classes cannot interfere with one another.
/// </para>
/// </summary>
public class TaskFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"taskflow-tests-{Guid.NewGuid():N}.db");

    /// <summary>JSON settings matching the API, so responses deserialise the same way a client would.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TaskFlowDatabase", $"Data Source={_databasePath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        // SQLite pools connections; without this the file stays locked and cannot be removed.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // A leftover file in the temp directory is harmless; never fail a test over it.
        }
    }
}
