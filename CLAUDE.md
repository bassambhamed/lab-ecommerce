# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Context

.NET 10 / ASP.NET Core e-commerce sample (microservices + Blazor Server UI) orchestrated by .NET Aspire (`Aspire.AppHost.Sdk` 13.x). It is the running example of a training course and was shipped **deliberately bare**: adding tests, a real database, Dockerfile/manifests and CI is the expected work. Integration tests now exist (`tests/`); there is still no real database, no container files and no CI — don't assume they exist. The README and user-facing docs are in French.

## Commands

The SDK (10.0.x) lives in `/usr/local/share/dotnet`. If `dotnet` is not found, prepend it: `export PATH="/usr/local/share/dotnet:$PATH"`.

- Build everything: `dotnet build ECommerce.slnx`
- Run the whole system (all services + Aspire dashboard): `dotnet run --project src/ECommerce.AppHost`
  - Open the `Login URL` printed in the terminal; its `?t=<token>` changes on every run. The dashboard ports are fixed by `src/ECommerce.AppHost/Properties/launchSettings.json` (17089 https / 15202 http) and each service has its own `launchSettings.json`, but code must never hard-code service addresses — use service discovery names (see below).
  - HTTPS errors (`UntrustedRoot`): `dotnet dev-certs https --trust`.
- Tests: `dotnet test ECommerce.slnx` (both test projects). One project: `dotnet test tests/ECommerce.Ordering.Api.Tests`. One test: `dotnet test ECommerce.slnx --filter "FullyQualifiedName~CreateOrder_ZeroQuantity_Returns400"` (the other project then prints a harmless "No test matches").
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

## Tests

xUnit integration tests, one project per API (`tests/ECommerce.Catalog.Api.Tests`, `tests/ECommerce.Ordering.Api.Tests`): both APIs define a top-level `Program`, so a single project referencing both would not compile. Scenarios to cover are listed in `tests/scenarios.md`; the `/test-integration` skill (`.claude/skills/test-integration/`, templates in `modeles.md`) encodes the conventions below.

- `WebApplicationFactory<Program>` directly: `Program` is public since .NET 10, don't add `public partial class Program`.
- **One factory per test** (created in the test class constructor, disposed via `IDisposable`), each with its **own in-memory database**: `Program.cs` uses a named database (`"catalog"`, `"ordering"`) shared by every host in the process, so `ConfigureTestServices` removes `DbContextOptions<T>` **and** `IDbContextOptionsConfiguration<T>` and re-registers the context with a unique name.
- **Ordering never calls the real Catalog**: the factory sets `services:catalog:http:0` (what the AppHost normally injects) and replaces the typed client's primary handler with `FakeCatalogHandler` (products 1 and 2 only; anything else → 404).
- Request bodies are raw JSON strings (`"""..."""`), the only way to test absent or mistyped properties. Assert the status code first, then the body, deserialized into a test-local `record` (the consumer's view; `Status` is a string).
- Naming: `Method_Scenario_Result`, e.g. `CreateOrder_EmptyCart_Returns400`.
- A test reproducing a bug must be seen **red** before the production fix; then fix production code, never the test.

## Git workflow and project tooling

- Work on a branch (`feat/...`, `fix/...`), never directly on `main`. Commit messages: conventional (`feat:`, `fix:`, `test:`, `docs:`, `chore:`), in English, saying why.
- Stage files by name: `git add -A` gets blocked by the safety classifier (`.env` exists at the root; it is gitignored and reading it is denied in `.claude/settings.json`).
- `.claude/settings.json` denies `git push --force`/`-f` and `git reset --hard`, and asks for confirmation on `git push`, `gh pr create` and `gh pr merge`. Never merge a PR: a human decides.
- `/revue-diff [focus]` (`.claude/commands/revue-diff.md`): read-only critical review of the branch + uncommitted diff, output as a severity table.
- Hooks (`.claude/hooks/`, wired in `.claude/settings.json`):
  - `garde-git.sh` (PreToolUse, Bash) blocks any Bash command whose text contains a force push or a hard reset — even inside a heredoc or a string (e.g. a script writing docs that mention them). Use Edit/Write for such text; for real history fixes, propose a new commit, `git revert` or a branch.
  - `formater.sh` (PostToolUse, Edit|Write) runs `dotnet format whitespace` on each edited `.cs` file, so files may be reformatted right after you write them.
  - `stop-tests-verts.sh` (Stop) runs the tests and prevents ending the turn while any fails (at most 3 times per session; `CLAUDE_SKIP_STOP_TESTS=1` disables it, e.g. for a deliberate red phase).
- `/ship <title>` (`.claude/skills/ship/`): refuses on `main`, runs `verifier.sh` (build → tests → format of changed `.cs` files, stops at first failure), reviews, commits, pushes and opens the PR with the `modele-pr.md` template (every section filled with facts).

## Architecture

Request flow: **Web (Blazor Server) → Gateway (YARP) → Catalog / Ordering**, plus **Ordering → Catalog** directly.

- `src/ECommerce.AppHost/AppHost.cs` is the source of truth for topology: resource names (`catalog`, `ordering`, `gateway`, `web`), dependencies (`WithReference`) and startup order (`WaitFor`). Adding a service or a dependency starts here.
- **Service discovery, no URLs in code.** HttpClients use logical addresses like `https+http://catalog` (HTTPS preferred, HTTP fallback). `WithReference` makes Aspire inject `services__<name>__<scheme>__0` env vars; `AddServiceDiscovery()` in ServiceDefaults resolves them. A new inter-service call needs both a `WithReference` in the AppHost and a client using the resource name.
- `src/ECommerce.ServiceDefaults/Extensions.cs` — every service calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`. This adds OpenTelemetry (OTLP export only if `OTEL_EXPORTER_OTLP_ENDPOINT` is set), service discovery and the standard resilience handler on **all** HttpClients, and `/health` + `/alive` (mapped **only in Development**).
- **Gateway** (`src/ECommerce.Gateway`): YARP routes live in `appsettings.json` under `ReverseProxy`. `/catalog/**` and `/ordering/**` have the prefix stripped (`PathRemovePrefix`) before forwarding, so the web calls `/ordering/api/orders` and Ordering receives `/api/orders`. Destinations are resolved via `AddServiceDiscoveryDestinationResolver()`.
- **Web** (`src/ECommerce.Web`): Blazor Server (`InteractiveServer`), so API calls run server-side through typed clients in `Services/` pointed at `https+http://gateway`. The web never calls Catalog/Ordering directly. DTOs are duplicated here (no shared contracts project).
- **Catalog / Ordering APIs**: Minimal APIs grouped in `Endpoints/*Endpoints.cs` (`MapGroup("/api/...")`), EF Core **InMemory** DbContexts in `Data/`, schema created via `EnsureCreated()` at startup (no migrations). Catalog seed products come from `HasData` in `CatalogDbContext`. Data is lost on restart.
- **Order creation** (`Ordering.Api/Endpoints/OrderingEndpoints.cs`): for each line it calls Catalog synchronously via `CatalogServiceClient` (404 → 400 "product does not exist"), snapshots name/price into `OrderItem`, sets status `Confirmed`, saves. `Validate()` rejects (400) an empty/missing customer, a customer over 200 chars, missing/empty/null items and quantities ≤ 0 before any Catalog call. Stock is **not** decremented. `Order.Total` is computed and ignored by EF. Ordering serializes enums as strings (`JsonStringEnumConverter`), and the Web's `OrderDto.Status` is a `string` to match.
- OpenAPI documents are mapped only in Development (`app.MapOpenApi()`).
