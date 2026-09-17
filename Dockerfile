# Builds the PostSharp License Server as a Linux container.
#
# The image runs the same application as the Windows release package; only the host differs. See
# docker-compose.yml for a ready-to-run deployment with a SQL Server database.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the manifests alone, so that a change to the sources does not invalidate the
# restore layer.
COPY Directory.Build.props Directory.Packages.props nuget.config global.json ./
COPY src/SharpCrafters.Backstage.LicenseServer.Core/SharpCrafters.Backstage.LicenseServer.Core.csproj src/SharpCrafters.Backstage.LicenseServer.Core/
COPY src/SharpCrafters.Backstage.LicenseServer.Web/SharpCrafters.Backstage.LicenseServer.Web.csproj src/SharpCrafters.Backstage.LicenseServer.Web/
RUN dotnet restore src/SharpCrafters.Backstage.LicenseServer.Web/SharpCrafters.Backstage.LicenseServer.Web.csproj

COPY src/ src/
RUN dotnet publish src/SharpCrafters.Backstage.LicenseServer.Web/SharpCrafters.Backstage.LicenseServer.Web.csproj \
        --configuration Release \
        --no-restore \
        --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The audit signing key is written here on first start. Mount a volume over it, or the signature
# chain restarts whenever the container is replaced.
RUN mkdir -p /app/App_Data && useradd --uid 64198 --create-home licenseserver \
    && chown -R licenseserver /app
USER licenseserver

COPY --from=build --chown=licenseserver /app ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SharpCrafters.Backstage.LicenseServer.dll"]
