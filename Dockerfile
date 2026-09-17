# Runs the SharpCrafters Backstage License Server as a Linux container.
#
# The image carries the contents of the release archive, so it runs exactly what is released rather
# than a second build of the same sources. Build the product first:
#
#   ./Build.ps1 build
#   docker compose up
#
# See docker-compose.yml for a ready-to-run deployment with a SQL Server database.
#
# The build is not done inside the image on purpose. The licensing component comes from a private
# feed and the build needs the generated global.json and nuget.config that `./Build.ps1 prepare`
# writes, none of which belongs in a container that a customer may build.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The audit signing key is written here on first start. Mount a volume over it, or the signature
# chain restarts whenever the container is replaced.
RUN mkdir -p /app/App_Data && useradd --uid 64198 --create-home licenseserver \
    && chown -R licenseserver /app
USER licenseserver

# Written by the PackAndZip target of the web project; it is what the release archive contains.
COPY --chown=licenseserver artifacts/app/ ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SharpCrafters.Backstage.LicenseServer.dll"]
