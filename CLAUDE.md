# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Context

.NET 10 / ASP.NET Core e-commerce sample (microservices + Blazor Server UI) orchestrated by .NET Aspire (`Aspire.AppHost.Sdk` 13.x). It is the running example of a training course and is shipped **deliberately bare**: no tests, no real database, no Dockerfile/manifests, no CI. Adding those is the expected work — don't assume they exist. The README and user-facing docs are in French.

## Commands

The SDK (10.0.x) lives in `/usr/local/share/dotnet`. If `dotnet` is not found, prepend it: `export PATH="/usr/local/share/dotnet:$PATH"`.

- Build everything: `dotnet build ECommerce.slnx`
- Run the whole system (all services + Aspire dashboard): `dotnet run --project src/ECommerce.AppHost`
  - Open the `Login URL` printed in the terminal; its `?t=<token>` changes on every run. The dashboard ports are fixed by `src/ECommerce.AppHost/Properties/launchSettings.json` (17089 https / 15202 http) and each service has its own `launchSettings.json`, but code must never hard-code service addresses — use service discovery names (see below).
  - HTTPS errors (`UntrustedRoot`): `dotnet dev-certs https --trust`.
- There are no test projects yet. Once added to the solution: `dotnet test ECommerce.slnx`, single test with `dotnet test --filter "FullyQualifiedName~<TestName>"`.
- No linter/formatter is configured beyond the compiler (`Nullable` and `ImplicitUsings` enabled everywhere); `dotnet format` is available if needed.

Running a service alone with `dotnet run --project src/<Service>` starts it, but only Catalog is fully usable that way (no outgoing calls). Ordering, Gateway and Web start too, yet their outgoing calls to `catalog` / `ordering` / `gateway` fail because those addresses are normally injected by the AppHost (see below).

## Architecture

Request flow: **Web (Blazor Server) → Gateway (YARP) → Catalog / Ordering**, plus **Ordering → Catalog** directly.

- `src/ECommerce.AppHost/AppHost.cs` is the source of truth for topology: resource names (`catalog`, `ordering`, `gateway`, `web`), dependencies (`WithReference`) and startup order (`WaitFor`). Adding a service or a dependency starts here.
- **Service discovery, no URLs in code.** HttpClients use logical addresses like `https+http://catalog` (HTTPS preferred, HTTP fallback). `WithReference` makes Aspire inject `services__<name>__<scheme>__0` env vars; `AddServiceDiscovery()` in ServiceDefaults resolves them. A new inter-service call needs both a `WithReference` in the AppHost and a client using the resource name.
- `src/ECommerce.ServiceDefaults/Extensions.cs` — every service calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`. This adds OpenTelemetry (OTLP export only if `OTEL_EXPORTER_OTLP_ENDPOINT` is set), service discovery and the standard resilience handler on **all** HttpClients, and `/health` + `/alive` (mapped **only in Development**).
- **Gateway** (`src/ECommerce.Gateway`): YARP routes live in `appsettings.json` under `ReverseProxy`. `/catalog/**` and `/ordering/**` have the prefix stripped (`PathRemovePrefix`) before forwarding, so the web calls `/ordering/api/orders` and Ordering receives `/api/orders`. Destinations are resolved via `AddServiceDiscoveryDestinationResolver()`.
- **Web** (`src/ECommerce.Web`): Blazor Server (`InteractiveServer`), so API calls run server-side through typed clients in `Services/` pointed at `https+http://gateway`. The web never calls Catalog/Ordering directly. DTOs are duplicated here (no shared contracts project).
- **Catalog / Ordering APIs**: Minimal APIs grouped in `Endpoints/*Endpoints.cs` (`MapGroup("/api/...")`), EF Core **InMemory** DbContexts in `Data/`, schema created via `EnsureCreated()` at startup (no migrations). Catalog seed products come from `HasData` in `CatalogDbContext`. Data is lost on restart.
- **Order creation** (`Ordering.Api/Endpoints/OrderingEndpoints.cs`): for each line it calls Catalog synchronously via `CatalogServiceClient` (404 → 400 "product does not exist"), snapshots name/price into `OrderItem`, sets status `Confirmed`, saves. Stock is **not** decremented and quantities are not validated. `Order.Total` is computed and ignored by EF. Ordering serializes enums as strings (`JsonStringEnumConverter`), and the Web's `OrderDto.Status` is a `string` to match.
- OpenAPI documents are mapped only in Development (`app.MapOpenApi()`).
