# SharpCrafters.Backstage.LicenseServer

This repository contains the source code and the releases of the license server of PostSharp and
Metalama. The server serves the license keys of both product families. The license keys that its
administrator adds to it decide which products it serves.

The license server is optional. All commercial licenses are floating licenses, and the license server
reports how many of them a team uses.

The license server is an ASP.NET Core application with a SQL Server or a PostgreSQL database. It runs
on Windows behind IIS, and on Linux and macOS in its own process or in a container.

Respecting the license agreement is the responsibility of the customer. This is the reason why we
publish the source code of the license server. The use of license keys
[is audited in any case](http://doc.postsharp.net/license-audit). If the audit is not acceptable for
your organization, ask the PostSharp sales team for a license key that waives it.

## License

The license server is published under the MIT License. PostSharp itself is a commercial product with
a proprietary license.

## Download

Download the latest release from
https://github.com/postsharp-ops/SharpCrafters.Backstage.LicenseServer/releases/latest.

## Documentation

* [Installing the license server](http://doc.postsharp.net/license-server-admin).
* [Using the license server](http://doc.postsharp.net/license-server).
* [Configuring the license server](docs/configuration.md).
* [Running the license server in a container](docs/docker.md).
* [The license server protocol](docs/protocol.md), for the developer who maintains a client or
  diagnoses a deployment.

## Trying it out

On a machine that runs Docker, the container deployment is the shortest way to see the license server
work. It starts the server, a SQL Server database, and a job that creates the schema:

```
export MSSQL_SA_PASSWORD='...'
./Build.ps1 build
docker compose up
```

The build runs first because the image contains the files of the release archive, so the container
runs what is released.

Open http://localhost:8080 and add your license key. If you have no license key, the test override
makes the server issue to itself the license keys it serves:

```
docker compose -f docker-compose.yml -f docker-compose.test.yml up
```

[docs/docker.md](docs/docker.md) describes what this override changes, and what to configure before
you use the container for anything else than an evaluation.

## Installing on IIS

### Requirements

* Windows Server with IIS.
* The [ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) for .NET 10.
* SQL Server 2016 or later, or PostgreSQL 14 or later.

### Instructions

1. Install the ASP.NET Core Hosting Bundle on the web server, then restart IIS with `iisreset`.
2. Create the database, then run `Database\CreateTables.sql` against it. On PostgreSQL, run
   `Database\CreateTables.PostgreSql.sql` instead and set `LicenseServer:DatabaseProvider` to
   `PostgreSql`.
3. Unpack `SharpCrafters.Backstage.LicenseServer.<version>.zip` into the directory of an IIS
   application.
4. Edit `appsettings.json`: set the connection string, the addresses for notifications and the SMTP
   server. [docs/configuration.md](docs/configuration.md) describes every setting.
5. In IIS Manager, enable Windows authentication on the application to record which account sends
   each lease request. Lease requests are served anonymously in both cases.
6. Restrict the administrative pages. They are open in the released configuration, and restricting
   them is your responsibility. See
   [Securing the administrative pages](docs/configuration.md#securing-the-administrative-pages).
7. Open the application in a browser and add your license key.

## Installing elsewhere

The release package is portable. The same archive runs on every system that runs the .NET 10 runtime.

1. Install the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
   (`aspnetcore-runtime-10.0`).
2. Create the database, then run `Database/CreateTables.sql` against it.
3. Unpack the archive, edit `appsettings.json`, and run the application:

   ```
   dotnet SharpCrafters.Backstage.LicenseServer.dll
   ```

Two settings usually need another value outside Windows. The connection string cannot use
`Integrated Security=True` unless the host is joined to the domain, so use a SQL Server login or a
managed identity. Windows authentication requires Kerberos, so either join the host to the domain and
set `Authentication:Scheme` to `Negotiate`, or set it to `None` and accept that the leases record no
authenticated caller. [docs/configuration.md](docs/configuration.md) describes both settings.

## Upgrading from version 2025.1 or earlier

Earlier versions ran on .NET Framework and ASP.NET WebForms. The database schema has not changed, so
an existing database is used as it is, and there is no migration step. Four points require attention.

* The ASP.NET Core Hosting Bundle is now required. This is the only new prerequisite.
* The settings have moved from `Web.config` to `appsettings.json`. Their names have not changed, so
  you can copy the values one by one. See [docs/configuration.md](docs/configuration.md).
* Connection strings now need `Encrypt=False`, unless your SQL Server presents a certificate that the
  web server trusts, because the current SQL client encrypts the connection by default. A connection
  string that worked before the upgrade can otherwise fail with an error about the certificate.
* The timestamps of the exported audit log are now correct on a server that does not run in UTC.
  Earlier versions wrote the local time as if it were UTC, which shifted the values by the offset of
  the server. The exports produced after the upgrade therefore differ from the earlier ones by that
  offset.

License keys are now parsed by SharpCrafters.Backstage instead of the PostSharp SDK. This changes two
points in the interpretation of a license key.

* A license key that carries no grace period now receives thirty days instead of none. Such a license
  used to deny a request as soon as the capacity was exceeded. It now serves requests during thirty
  days beyond the capacity, and the server sends the warning e-mail as usual. A license key issued
  with an explicit grace period is not affected, and the capacity during the grace period does not
  change.
* Products are recorded under a new name. A license added after the upgrade is stored as
  `PostSharpUltimate` where it used to be stored as `Ultimate`. The existing rows are not modified,
  and a request that names either spelling finds both, so there is nothing to migrate.

Two changes concern the exported audit log.

* A line now has seven fields instead of eight. The eighth field contained a signature of the line,
  and that signature could not be verified: the server generated a random key at every call, so no
  two lines were signed with the same key. The server no longer writes that field, and it no longer
  writes the `HMAC` column of the `Leases` table. The column is left in place, and the values already
  written are left as they are.
* The sixth field and the seventh field contain the machine name and the user name, where they used
  to contain a hash of each. The log is read by the administrator of the server, who needs to know
  which user and which machine hold a seat. The exported file therefore contains personal data.
  Handle it as you handle the database, which has always contained the same names.

## Building from source

### Requirements

* The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
* PowerShell 7.4 or later.
* Access to the package feed that contains `SharpCrafters.Backstage`, the licensing component.

### Instructions

The repository is built with
[PostSharp.Engineering](https://github.com/postsharp/PostSharp.Engineering), as the
`Backstage.LicenseServer` product of the Backstage 2027.0 family.

```
./Build.ps1 prepare
./Build.ps1 test
```

`prepare` resolves the dependencies and writes `global.json`, `nuget.config` and the version files.
None of these files is in source control. `build` and `test` run `prepare` themselves, so you need it
only before you open the solution in an IDE, or before you run `dotnet` directly.

`./Build.ps1 build` writes the release archive to `artifacts\publish\private`, and unpacks its
contents into `artifacts\app`, which is the directory the container image is built from. A public
build also copies the archive to `artifacts\publish\public`, the directory the deployment uploads.

To build against a local clone of the licensing component instead of the published packages, point
the dependency at it, and build that repository first:

```
./Build.ps1 dependencies set local Backstage --path <path to the SharpCrafters.Backstage repository>
```

### Running the tests

`./Build.ps1 test` runs the suite on SQLite, which needs no server and keeps the loop short. Run the
same tests again against each engine that customers deploy before you open a pull request. The
engines differ in the collation, in the column types and in the lock that serializes the lease
requests, so a defect can appear on one of them alone:

```
./eng/TestDatabase.ps1 -Engine SqlServer
./eng/TestDatabase.ps1 -Engine PostgreSql
```

Each run starts its server in a container, waits for it, and runs `Build.ps1 test` against it. Docker
is the only prerequisite. To use a server of your own instead, set the connection string and the
script leaves the server alone:

```
$env:LICENSESERVER_TEST_SQLSERVER = "Server=127.0.0.1,1433;User Id=sa;Password=<a password>;TrustServerCertificate=True;Encrypt=False"
$env:LICENSESERVER_TEST_POSTGRESQL = "Host=127.0.0.1;Port=5432;Username=postgres;Password=<a password>"
dotnet test tests\SharpCrafters.Backstage.LicenseServer.Tests
```

The tests read those two variables, and they run on SQLite when neither is set. A connection string
names no database: each test receives a database of its own, created from `Database\CreateTables.sql`
or from `Database\CreateTables.PostgreSql.sql`, so each run also proves that the script and the
Entity Framework model agree. The databases are named `licenseserver_test_` followed by a hexadecimal
number. They are reused during the run, and the next run drops the ones an interrupted run left
behind, so the login needs the permission to create a database.

The continuous integration build runs both of them, in the configurations `Tests on SQL Server` and
`Tests on PostgreSQL`. Each one runs `eng\TestDatabase.ps1` in an image that carries its server,
described by `eng\src\Docker\SqlServerComponent.cs` and `eng\src\Docker\PostgreSqlComponent.cs`.

### Running locally

```
./Build.ps1 prepare
dotnet run --project src\SharpCrafters.Backstage.LicenseServer.Web
```

The development configuration uses a SQLite database, created at the first start, so no database
server is required. Once the server runs, `/Admin/GenerateDemoData` fills the database with simulated activity.
That page exists only in the Development environment.

### Repository layout

| Project | Contents |
|---|---|
| `src\SharpCrafters.Backstage.LicenseServer.Core` | The licensing rules, the database model, and the services they depend on. |
| `src\SharpCrafters.Backstage.LicenseServer.Web` | The web application: the pages, the endpoints and the composition root. |
| `tests\SharpCrafters.Backstage.LicenseServer.Tests` | The test suite. It runs on SQLite by default, and on SQL Server or PostgreSQL when the run is given one. |
| `eng` | The product definition and the version files that PostSharp.Engineering builds from. |

To put a server under load, use `LicenseServerLoadSimulator`, in the SharpCrafters.Backstage
repository. It simulates an organization of many users on many machines, and it sends the requests
that the product sends, so it measures the load that real clients produce.

## Support

Please use the PostSharp support facility at https://www.postsharp.net/support.
