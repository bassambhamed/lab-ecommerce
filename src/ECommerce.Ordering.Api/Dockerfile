# Multi-stage build of ECommerce.Ordering.Api.
# Build context must be the repository root (ServiceDefaults is a project reference):
#   docker build -f src/ECommerce.Ordering.Api/Dockerfile -t ecommerce/ordering:1.0 .

# Base images are pinned to an exact version AND a digest (multi-arch manifest list),
# both checked with: docker buildx imagetools inspect <image>
FROM mcr.microsoft.com/dotnet/sdk:10.0.404@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /src

# Restore first, from the project files only: this layer stays cached
# until a package or project reference changes, not on every code edit.
COPY src/ECommerce.Ordering.Api/ECommerce.Ordering.Api.csproj src/ECommerce.Ordering.Api/
COPY src/ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj src/ECommerce.ServiceDefaults/
RUN dotnet restore src/ECommerce.Ordering.Api/ECommerce.Ordering.Api.csproj

# Then copy the sources and publish without restoring again.
COPY src/ECommerce.Ordering.Api/ src/ECommerce.Ordering.Api/
COPY src/ECommerce.ServiceDefaults/ src/ECommerce.ServiceDefaults/
RUN dotnet publish src/ECommerce.Ordering.Api/ECommerce.Ordering.Api.csproj -c Release --no-restore -p:UseAppHost=false -o /app

# Chiseled runtime: no shell, no package manager, only what ASP.NET Core needs.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled@sha256:7c625e44e5d588b4d7b2a913e94ca4bd5098ce47442da404829e4c7936a764e1 AS final
WORKDIR /app
COPY --from=build /app .

# Run as the non-root user provided by the base image (numeric UID, 1654).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ECommerce.Ordering.Api.dll"]
