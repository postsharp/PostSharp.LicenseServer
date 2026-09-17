# Running the license server in a container

`docker-compose.yml` brings up a complete license server: the application, a SQL Server database,
and a one-shot job that creates the schema from `Database/CreateTables.sql`.

```
./Build.ps1 build
docker compose up
```

The build comes first because the image carries the contents of the release archive rather than
building the sources again, so the container runs exactly what is released.

The server is then at http://localhost:8080 and the database at `localhost:1433`. Add a license key
on the **Add a license** page and point a PostSharp client at
`http://localhost:8080/Lease.ashx`.

To stop it, keeping the data:

```
docker compose down
```

To stop it and throw the database away:

```
docker compose down --volumes
```

## What it starts

| Service | What it is |
|---|---|
| `database` | SQL Server 2022 Developer Edition. Its health check runs a real query, because the server accepts connections well before it can answer one. |
| `database-schema` | Runs once: creates the database if it does not exist, then runs `CreateTables.sql` if the tables are not already there. Re-running `docker compose up` does not fail on an existing schema. It reuses the SQL Server image, which already carries `sqlcmd`, rather than pulling a second one. |
| `licenseserver` | The application, from `Dockerfile`. Waits for the schema job to finish. |

The application keeps its audit signing key in the `licenseserver-data` volume, so the signature
chain survives the container being replaced. The database keeps its files in `database-data`.

## This is a test deployment

It is meant for trying the license server out and for development. Four things make it unsuitable as
it stands for anything else.

- **The `sa` password is in the compose file in plain text**, and `sa` is what the application
  connects as. A real deployment uses a login with rights on the one database, and supplies the
  password from a secret rather than from a file in the repository.
- **Nobody is authenticated.** There is no domain controller in a container, so
  `Authentication__Scheme` is `None` and every lease is recorded without a user name. To attribute
  leases, run the container on a host joined to your domain with a Kerberos keytab and set the
  scheme to `Negotiate`.
- **The administrative pages are open**, as they are in the default configuration everywhere. Set
  `LicenseServer__AdminRoles` once you have an identity to check against.
- **The audit signing key is a fixed value in the compose file**, so that restarting the stack does
  not start a new signature chain. Remove it for a real deployment and let the server generate one
  into the volume.

## The image on its own

The image does not need the compose file. Against an existing SQL Server:

```
./Build.ps1 build
docker build -t backstage-licenseserver .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__SharpCrafters_LicenseServerConnectionString="Server=db;Database=PostSharpLicenseServer;User Id=licenseserver;Password=...;TrustServerCertificate=True" \
  -e Authentication__Scheme=None \
  -v licenseserver-data:/app/App_Data \
  backstage-licenseserver
```

Any setting from [configuration.md](configuration.md) can be given as an environment variable, with
a double underscore where the setting name has a colon.

For a trial with no database at all, the server can keep its data in a SQLite file, which it creates
itself:

```
docker run --rm -p 8080:8080 \
  -e LicenseServer__DatabaseProvider=Sqlite \
  -e ConnectionStrings__SharpCrafters_LicenseServerConnectionString="DataSource=/app/App_Data/licenseserver.db" \
  -v licenseserver-data:/app/App_Data \
  backstage-licenseserver
```

SQL Server remains the supported engine for a real installation.

## The image

`Dockerfile` has a single stage on the ASP.NET runtime image. It copies `artifacts/app`, which is
what `./Build.ps1 build` unpacks the release archive into, so the image and the archive carry the
same files. It runs as a non-root user and listens on port 8080.

The product is deliberately not built inside the image. The licensing component comes from a private
feed, and the build needs the `global.json` and `nuget.config` that `./Build.ps1 prepare` generates,
neither of which belongs in a container a customer may build.

`./Build.ps1 build` does not run the tests. Run `./Build.ps1 test` if the image is going anywhere
beyond your own machine.
