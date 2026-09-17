# Running the license server in a container

`docker-compose.yml` brings up a license server: the application, a SQL Server database, and a
one-shot job that creates the schema from `Database/CreateTables.sql`.

```
export MSSQL_SA_PASSWORD='...'
./Build.ps1 build
docker compose up
```

The build comes first because the image carries the contents of the release archive rather than
building the sources again, so the container runs exactly what is released.

The deployment serves the license keys you add to it and holds nothing else. The database password
comes from the environment rather than from a file in the repository, and the server generates its
own audit signing key into a volume. To bring it up with license keys it issues to itself, which is
what a trial or a load simulation wants, see
[Trying it out without a license key](#trying-it-out-without-a-license-key).

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

The application keeps the files it generates and must not lose in the `licenseserver-data` volume,
mounted at `/app/App_Data`: the audit signing key, and the test licensing authority when there is
one. They have to outlive the container. Losing the audit signing key does not invalidate the rows
already written, but it does start a new signature chain, and losing the test authority stops the
license keys it signed from being accepted. The database keeps its files in `database-data`.

The image declares `/app/App_Data` as a volume, so a container started with `docker run` and no
explicit mount still keeps these files outside its own writable layer. Name the volume in a real
deployment: an anonymous one is easy to prune by accident. `LicenseServer__DataDirectory` moves the
directory somewhere else.

## Probing it

The server answers `/health/live` with the state of the process, `/health` with the state of the
process, the database and the licenses, and `/version` with the build it runs. All three are
anonymous. In Kubernetes, the liveness probe is `/health/live` and the readiness probe is `/health`.
A missing or expired license does not fail either of them, so a deployment is never held back by one;
it is reported in the body of `/health` and in the log of the server. See
[Monitoring](configuration.md#monitoring).

The image declares no `HEALTHCHECK`, and the compose file none either. A health check runs inside the
container, and the ASP.NET runtime image carries no HTTP client to run it with: adding `curl` to the
image for that is more attack surface than a probe is worth, when everything that runs containers can
make an HTTP request of its own.

## Trying it out without a license key

A server with no license key cannot serve a lease, and every key the production licensing authority
signs is one that was sold. The test override lets the server issue itself the keys it serves:

```
docker compose -f docker-compose.yml -f docker-compose.test.yml up
```

It puts the server in the Development environment, sets `LicenseServer__SeedTestLicenses`, and runs
the clock 1440 times faster than real time so that a fortnight of leases fits into a coffee break.
The server generates a licensing authority of its own into the data volume, trusts it, and adds one
license key per product family. No other server accepts those keys, and the server refuses to start
with either setting outside the Development environment, so the override cannot quietly turn a real
deployment into a test one.

This is also what [LicenseServerLoadSimulator](protocol.md#reading-the-server-clock-gettimeashx) in
the SharpCrafters.Backstage repository expects. Drop the data volume between two simulations: the
virtual clock is anchored when the process starts, so a restart moves it backwards and leaves leases
dated in the future.

## Before you run it for real

- **The application connects as `sa`.** A real deployment uses a login with rights on the one
  database. The password comes from `MSSQL_SA_PASSWORD` in the environment, and compose refuses to
  start without it.
- **Nobody is authenticated.** There is no domain controller in a container, so
  `Authentication__Scheme` is `None` and every lease is recorded without a user name. To attribute
  leases, run the container on a host joined to your domain with a Kerberos keytab and set the
  scheme to `Negotiate`.
- **The administrative pages are open**, as they are in the default configuration everywhere. Nothing
  in the container closes them. See
  [Securing the administrative pages](configuration.md#securing-the-administrative-pages).
- **Back up the `licenseserver-data` volume with the database.** It holds the audit signing key.

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
