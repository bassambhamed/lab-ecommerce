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
- There are no test projects yet. Once added to the solution: `dotnet test ECommerce.slnx`, single test with `dotnet test --filter "FullyQualifiedName~<TestName>"`. Until then `dotnet test` "passes" without testing anything — don't report it as proof.
- Format check: `dotnet format ECommerce.slnx --verify-no-changes --include <files>`. Check only the files you touched: `src/ECommerce.ServiceDefaults/Extensions.cs` already has 7 known format violations.
- No `.gitattributes`: many files (most `appsettings*.json` and `launchSettings.json`, three `.csproj`, `Extensions.cs`, every Web `.razor` except `Pages/Home`, `Orders` and `Products`) use **CRLF** line endings, the rest LF. Preserve each file's line endings when editing, or the diff covers the whole file.

Running a service alone with `dotnet run --project src/<Service>` starts it, but only Catalog is fully usable that way (no outgoing calls). Ordering, Gateway and Web start too, yet their outgoing calls to `catalog` / `ordering` / `gateway` fail because those addresses are normally injected by the AppHost (see below).

## Verifying a change

- **End to end**: the `/smoke-test` skill (`.claude/skills/smoke-test/smoke.sh`) hits the gateway on its fixed http port `http://localhost:5067` (catalog list, a valid order, an unknown product). If the app is already running it reuses it and leaves it running; otherwise it starts the AppHost and stops it afterwards. A running AppHost does **not** pick up a rebuild: restart it before smoke-testing new code. Every run creates a real order in the in-memory database.
- **Ordering alone, without touching a running AppHost** (works because Catalog's http endpoint stays on `5147`):
  ```bash
  ASPNETCORE_ENVIRONMENT=Development services__catalog__http__0=http://localhost:5147 \
    dotnet run --no-build --no-launch-profile --project src/ECommerce.Ordering.Api --urls http://localhost:5299
  ```
  Then `curl` `http://localhost:5299/api/orders` (no `/ordering` prefix: that is the gateway's), and stop the process afterwards.
- Service logs don't show in the AppHost console; they are in the Aspire dashboard (console logs, structured logs, traces).
- `preuves/lab<N>/README.md` holds raw before/after outputs (curl) for each lab.

## Git workflow and project tooling

- Work on a branch (`feat/...`, `fix/...`), never directly on `main`. Commit messages: conventional (`feat:`, `fix:`, `test:`, `docs:`, `chore:`), in English, saying why.
- Stage files by name: `git add -A` gets blocked by the safety classifier (`.env` exists at the root; it is gitignored and reading it is denied in `.claude/settings.json`).
- `.claude/settings.json` denies `git push --force`/`-f` and `git reset --hard`, and asks for confirmation on `git push`, `gh pr create` and `gh pr merge`. Never merge a PR: a human decides.
- `/revue-diff [focus]` (`.claude/commands/revue-diff.md`): read-only critical review of the branch + uncommitted diff, output as a severity table.
- `/ship <title>` (`.claude/skills/ship/`): refuses on `main`, runs `verifier.sh` (build → tests → format of changed `.cs` files, stops at first failure), reviews, commits, pushes and opens the PR with the `modele-pr.md` template (every section filled with facts).

## Architecture

Request flow: **Web (Blazor Server) → Gateway (YARP) → Catalog / Ordering**, plus **Ordering → Catalog** directly.

- `src/ECommerce.AppHost/AppHost.cs` is the source of truth for topology: resource names (`catalog`, `ordering`, `gateway`, `web`), dependencies (`WithReference`) and startup order (`WaitFor`). Adding a service or a dependency starts here.
- **Service discovery, no URLs in code.** HttpClients use logical addresses like `https+http://catalog` (HTTPS preferred, HTTP fallback). `WithReference` makes Aspire inject `services__<name>__<scheme>__0` env vars; `AddServiceDiscovery()` in ServiceDefaults resolves them. A new inter-service call needs both a `WithReference` in the AppHost and a client using the resource name.
- `src/ECommerce.ServiceDefaults/Extensions.cs` — every service calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`. This adds OpenTelemetry (OTLP export only if `OTEL_EXPORTER_OTLP_ENDPOINT` is set), service discovery and the standard resilience handler on **all** HttpClients, and `/health` + `/alive` (mapped **only in Development**).
- **Gateway** (`src/ECommerce.Gateway`): YARP routes live in `appsettings.json` under `ReverseProxy`. `/catalog/**` and `/ordering/**` have the prefix stripped (`PathRemovePrefix`) before forwarding, so the web calls `/ordering/api/orders` and Ordering receives `/api/orders`. Destinations are resolved via `AddServiceDiscoveryDestinationResolver()`.
- **Web** (`src/ECommerce.Web`): Blazor Server (`InteractiveServer`), so API calls run server-side through typed clients in `Services/` pointed at `https+http://gateway`. The web never calls Catalog/Ordering directly. DTOs are duplicated here (no shared contracts project).
- **Catalog / Ordering APIs**: Minimal APIs grouped in `Endpoints/*Endpoints.cs` (`MapGroup("/api/...")`), EF Core **InMemory** DbContexts in `Data/`, schema created via `EnsureCreated()` at startup (no migrations). Catalog seed products come from `HasData` in `CatalogDbContext`. Data is lost on restart.
- **Order creation** (`Ordering.Api/Endpoints/OrderingEndpoints.cs`): `Validate()` checks customer/items/quantities, then the optional promo code is checked, then each line calls Catalog synchronously via `CatalogServiceClient` (404 → 400 "product does not exist"), snapshots name/price into `OrderItem`, sets status `Confirmed`, saves. Stock is **not** decremented. `Order.Subtotal` and `Order.Total` (= Subtotal − `DiscountAmount`) are computed and ignored by EF.
- **Promo codes** (`Ordering.Api/Services/PromoCodeService.cs`): every confirmed order gets a new single-use `PROMO-XXXXXXXX` code (`Order.IssuedPromoCode`); passing it as `promoCode` on a later order applies `DiscountPercent` to the subtotal and sets `RedeemedAt`. Percent and validity come from the `PromoCodes` section of Ordering's `appsettings.json` (default 10 % / 30 days). `RedeemedAt` is marked as a concurrency token to reject a double redemption on save (not yet covered by a test). Time comes from the injected `TimeProvider`. Codes aren't tied to a customer, so they are bearer codes: only the POST response returns `issuedPromoCode`; the GET endpoints and the Orders page must not expose them.
- Ordering serializes enums as strings (`JsonStringEnumConverter`), and the Web's `OrderDto.Status` is a `string` to match.
- OpenAPI documents are mapped only in Development (`app.MapOpenApi()`).
