# Multi-stage build for the backend. One build stage, two runtime targets:
#   docker build --target api      -> ASP.NET Core API on port 8080
#   docker build --target migrator -> one-shot DbMigrator (architecture section 4.5)
# Build context: the backend/ folder. Image tags are pinned exactly; never use floating tags.

# --- Build stage ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build
WORKDIR /src

# Restore first, from the project files and central package versions only, so this
# layer stays cached until a project file or Directory.Packages.props changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Mra.Domain/Mra.Domain.csproj                 src/Mra.Domain/
COPY src/Mra.Application/Mra.Application.csproj       src/Mra.Application/
COPY src/Mra.Infrastructure/Mra.Infrastructure.csproj src/Mra.Infrastructure/
COPY src/Mra.Api/Mra.Api.csproj                       src/Mra.Api/
COPY src/Mra.DbMigrator/Mra.DbMigrator.csproj         src/Mra.DbMigrator/
RUN dotnet restore src/Mra.Api/Mra.Api.csproj \
 && dotnet restore src/Mra.DbMigrator/Mra.DbMigrator.csproj

COPY src/ src/
RUN dotnet publish src/Mra.Api/Mra.Api.csproj -c Release --no-restore -o /out/api /p:UseAppHost=false \
 && dotnet publish src/Mra.DbMigrator/Mra.DbMigrator.csproj -c Release --no-restore -o /out/migrator /p:UseAppHost=false

# --- API -----------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS api
WORKDIR /app
COPY --from=build /out/api ./
# The .NET images default to ASPNETCORE_HTTP_PORTS=8080; set explicitly for clarity.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Non-root user shipped with the .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "Mra.Api.dll"]

# --- Migrator ------------------------------------------------------------------
# Uses the aspnet image, not runtime: Mra.Infrastructure has a FrameworkReference to
# Microsoft.AspNetCore.App (IHttpContextAccessor, PasswordHasher<T>), so the migrator's
# runtimeconfig requires the ASP.NET Core shared framework.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS migrator
WORKDIR /app
COPY --from=build /out/migrator ./
USER $APP_UID
ENTRYPOINT ["dotnet", "Mra.DbMigrator.dll"]
