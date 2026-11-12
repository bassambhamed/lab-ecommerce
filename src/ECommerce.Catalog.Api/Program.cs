using ECommerce.Catalog.Api.Data;
using ECommerce.Catalog.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Aspire: telemetry, health checks, resilience, service discovery.
builder.AddServiceDefaults();

builder.Services.AddOpenApi();
// "catalogdb" is the Aspire database resource; the AppHost injects ConnectionStrings__catalogdb.
// Plain AddDbContext + Enrich (instead of AddNpgsqlDbContext, which pools contexts) keeps
// the registration overridable and still adds Aspire's tracing, health check and retries.
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("catalogdb")));
builder.EnrichNpgsqlDbContext<CatalogDbContext>();

var app = builder.Build();

// Aspire default endpoints (/health, /alive).
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapCatalogEndpoints();

// Apply pending migrations on startup (creates the database and the demo products 1 to 4).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    db.Database.Migrate();
}

app.Run();
