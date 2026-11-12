using Npgsql;
using Testcontainers.PostgreSql;

namespace ECommerce.Ordering.Api.Tests;

/// <summary>
/// One PostgreSQL container for the whole test run, started on first use.
/// Each factory gets its own database on it, so tests stay isolated and can run in parallel.
/// Testcontainers removes the container when the test process exits.
/// </summary>
internal static class PostgresServer
{
    // Same image as the AppHost's AddPostgres (Aspire 13.4).
    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(async () =>
    {
        var container = new PostgreSqlBuilder().WithImage("postgres:18.3").Build();
        await container.StartAsync();
        return container;
    });

    /// <summary>Connection string to a database that does not exist yet: Migrate() creates it.</summary>
    public static string NewDatabaseConnectionString(string prefix)
    {
        var container = Container.Value.GetAwaiter().GetResult();
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = $"{prefix}_{Guid.NewGuid():N}",
        }.ConnectionString;
    }
}
