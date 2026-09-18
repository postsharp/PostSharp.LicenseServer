# Running the license server in a container

`docker-compose.yml` starts a license server: the application, a SQL Server database, and a job that
creates the schema from `Database/CreateTables.sql` and then stops.

```
export MSSQL_SA_PASSWORD='...'
./Build.ps1 build
docker compose up
```

The build runs first because the image contains the files of the release archive. The image does not
build the sources again, so the container runs what is released.

This deployment serves the license keys that you add to it. The database password comes from the
environment and not from a file of the repository, and the server generates its own audit signing key
in a volume. To start a server that issues license keys to itself, see
[Trying it out without a license key](#trying-it-out-without-a-license-key).

The server then listens on http://localhost:8080, and the database on `localhost:1433`. Add a license
key on the *Add a license* page, then configure a PostSharp client with
`http://localhost:8080/Lease.ashx`.

To stop the server and keep the data:

```
docker compose down
```

To stop the server and delete the database:

```
docker compose down --volumes
```

## What it starts

| Service | What it is |
|---|---|
| `database` | SQL Server 2022 Developer Edition. Its health check runs a query, because the server accepts connections before it can answer one. |
| `database-schema` | Runs once: it creates the database when the database does not exist, then runs `CreateTables.sql` when the tables do not exist. A second `docker compose up` does not fail on an existing schema. The service reuses the SQL Server image, which contains `sqlcmd`, instead of pulling a second image. |
| `licenseserver` | The application, built from `Dockerfile`. It waits for the schema job to finish. |

The application stores the files it generates in the `licenseserver-data` volume, mounted at
`/app/App_Data`. The only such file is the test licensing authority, which a server that serves
license keys of the production authority does not have. That file must survive the container,
because its loss invalidates the license keys that the authority signed. The database stores its own
files in `database-data`.

The image declares `/app/App_Data` as a volume, so a container started with `docker run` and no
explicit mount still stores these files outside its writable layer.
`LicenseServer__DataDirectory` moves the directory to another location.

## Probing it

The server answers `/health/live` with the state of the process, `/health` with the state of the
process, of the database and of the licenses, and `/version` with the build it runs. The three
endpoints require no authentication. In Kubernetes, use `/health/live` as the liveness probe and
`/health` as the readiness probe. A missing or expired license fails neither of them, so it never
blocks a deployment; the server reports it in the body of `/health` and in its log. See
[Monitoring](configuration.md#monitoring).

The image declares no `HEALTHCHECK`, and the compose file declares none either. A health check of
Docker runs inside the container, and the ASP.NET runtime image contains no HTTP client to run it
with. Adding `curl` to the image would increase the attack surface, and every system that runs
containers can send an HTTP request of its own.

## Trying it out without a license key

A server with no license key cannot serve a lease, and the production licensing authority signs only
license keys that were sold. The test override makes the server issue to itself the license keys it
serves:

```
docker compose -f docker-compose.yml -f docker-compose.test.yml up
```

The override puts the server in the Development environment, sets
`LicenseServer__SeedTestLicenses`, and runs the clock 1440 times faster than real time, so that two
weeks of leases take fifteen minutes. The server generates a licensing authority of its own in the
data volume, accepts the license keys of that authority, and adds one license key per product family.
No other server accepts these license keys. The server also refuses to start with either setting
outside the Development environment, so this file cannot turn a real deployment into a test
deployment.

[LicenseServerLoadSimulator](protocol.md#reading-the-server-clock-gettimeashx), in the
SharpCrafters.Backstage repository, expects a server configured in this way. Delete the data volume
between two simulations: the virtual clock is anchored when the process starts, so a restart moves it
backwards and leaves the existing leases dated in the future.

## Before you run it for real

- The application connects as `sa`. A real deployment uses a login that has rights on one database.
  The password comes from `MSSQL_SA_PASSWORD` in the environment, and compose refuses to start
  without it.
- No caller is authenticated. A container has no domain controller, so `Authentication__Scheme` is
  `None` and the server records every lease without an authenticated user. To record the caller, run
  the container on a host joined to your domain with a Kerberos keytab, and set the scheme to
  `Negotiate`.
- The administrative pages are open, as they are in the default configuration of every deployment.
  The container does not restrict them. See
  [Securing the administrative pages](configuration.md#securing-the-administrative-pages).
- Back up the database. A deployment that serves license keys of the production authority keeps
  everything it must not lose in the database, and the `licenseserver-data` volume stays empty.

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

Every setting of [configuration.md](configuration.md) can be given as an environment variable, with a
double underscore where the name of the setting contains a colon.

For an evaluation without a SQL Server, the server can store its data in a SQLite file, which it
creates itself:

```
docker run --rm -p 8080:8080 \
  -e LicenseServer__DatabaseProvider=Sqlite \
  -e ConnectionStrings__SharpCrafters_LicenseServerConnectionString="DataSource=/app/App_Data/licenseserver.db" \
  -v licenseserver-data:/app/App_Data \
  backstage-licenseserver
```

SQL Server is the engine supported for a real installation.

## The image

`Dockerfile` has a single stage, based on the ASP.NET runtime image. It copies `artifacts/app`, the
directory into which `./Build.ps1 build` unpacks the release archive, so the image and the archive
contain the same files. The application runs as a user that is not root, and listens on port 8080.

The image does not build the product. The licensing component comes from a private feed, and the
build requires the `global.json` and the `nuget.config` that `./Build.ps1 prepare` generates. Neither
file belongs in a container that a customer can build.

`./Build.ps1 build` does not run the tests. Run `./Build.ps1 test` before you publish the image.
