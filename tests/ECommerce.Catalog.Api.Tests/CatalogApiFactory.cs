using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ECommerce.Catalog.Api.Tests;

/// <summary>
/// Hosts Catalog.Api in memory against its own, brand-new PostgreSQL database, so tests don't share data.
/// Migrate() in Program.cs creates that database and seeds products 1 to 4.
/// </summary>
public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = PostgresServer.NewDatabaseConnectionString("catalog_tests");

    // What the AppHost normally injects as ConnectionStrings__catalogdb.
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:catalogdb", _connectionString);
}
