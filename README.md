# SharpCrafters.Backstage.LicenseServer

This repository contains the source code and the releases of the license server of PostSharp and
Metalama. It serves the license keys of both product families: which products it serves is decided
by the license keys its administrator adds to it.

The use of the license server is optional. Since all commercial licenses are floating ones,
the license server can help teams knowing how many licenses they actually use.

The license server is an ASP.NET Core application with an MS SQL back-end. It runs on Windows behind
IIS, and on Linux or macOS under its own process or in a container.

We at PostSharp consider that it is the customer's sole responsibility to respect the license agreement, and this is why we are providing the source code of the license server. Note that the use of licenses keys [is audited anyway](http://doc.postsharp.net/license-audit); if this is not an option for your organization, you can ask the PostSharp sales team for a license key with audit waiver.

## License

The license server itself is licensed under the *MIT License*. Note that PostSharp is a commercial product with a proprietary license.

## Download

You can download the latest release from https://github.com/postsharp-ops/SharpCrafters.Backstage.LicenseServer/releases/latest.

## Documentation

* [Installing the license server](http://doc.postsharp.net/license-server-admin).
* [Using the license server](http://doc.postsharp.net/license-server).
* [Configuring the license server](docs/configuration.md).
* [Running the license server in a container](docs/docker.md).
* [The license server protocol](docs/protocol.md), for whoever maintains a client or diagnoses a
  deployment.

## Trying it out

The quickest way to see the license server working, on any machine with Docker, is the container
deployment. It starts the server, a SQL Server database and a job that creates the schema:

```
./Build.ps1 build
docker compose up
```

The image carries the contents of the release archive, so the build comes first and the container
runs exactly what is released.

Then open http://localhost:8080 and add your license key. See
[docs/docker.md](docs/docker.md) for what it contains and what to change before using it for
anything other than a trial.

## Installing on IIS

### Requirements

* Windows Server with IIS.
* The [ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) for .NET 10.
* SQL Server 2016 or later.

### Instructions

1. Install the ASP.NET Core Hosting Bundle on the web server, then restart IIS with `iisreset`.
2. Create the database and run `Database\CreateTables.sql` against it.
3. Unpack `SharpCrafters.Backstage.LicenseServer.<version>.zip` into the directory of an IIS application.
4. Edit `appsettings.json`: set the connection string, the notification e-mail addresses and the
   SMTP server. The settings are described in [docs/configuration.md](docs/configuration.md).
5. In IIS Manager, enable Windows Authentication on the application if you want every lease request
   to be attributed to a user. Lease requests are served anonymously either way.
6. Restrict the administrative pages. They are open by default, and closing them is your
   responsibility; see
   [Securing the administrative pages](docs/configuration.md#securing-the-administrative-pages).
7. Browse to the application and add your license key.

## Installing elsewhere

The release package is portable: the same zip runs wherever the .NET 10 runtime does.

1. Install the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
   (`aspnetcore-runtime-10.0`).
2. Create the database and run `Database/CreateTables.sql` against it.
3. Unpack the zip, edit `appsettings.json`, and run it:

   ```
   dotnet SharpCrafters.Backstage.LicenseServer.dll
   ```

Two settings usually need changing away from Windows. The connection string cannot use
`Integrated Security=True` unless the host is joined to the domain, so use a SQL Server login or a
managed identity instead. And Windows authentication needs Kerberos, so either join the host to the
domain and set `Authentication:Scheme` to `Negotiate`, or set it to `None` and accept that leases
will not record who requested them. Both are covered in
[docs/configuration.md](docs/configuration.md).

## Upgrading from version 2025.1 or earlier

Earlier versions ran on .NET Framework and ASP.NET WebForms. The database schema has not changed,
so an existing database is used as it is, with no migration step. Four things do change.

* **The ASP.NET Core Hosting Bundle is now required.** This is the one prerequisite the previous
  version did not have.
* **Settings have moved from `Web.config` to `appsettings.json`.** The names are unchanged, so the
  values can be copied across one by one. See [docs/configuration.md](docs/configuration.md).
* **Connection strings now need `Encrypt=False`** unless your SQL Server presents a certificate the
  web server trusts, because the modern SQL client encrypts by default. A connection string that
  worked before may otherwise fail with a certificate error.
* **Timestamps in the exported audit log are now correct on servers that do not run in UTC.**
  Previously they were written as if local time were UTC, which shifted them by the server's offset.
  Exports taken after the upgrade therefore differ from earlier ones by that offset.

License keys are now parsed by SharpCrafters.Backstage instead of the PostSharp SDK, which changes
two things in what a key is taken to mean.

* **A license key that carries no grace period now gets thirty days rather than none.** Such a
  license used to deny a request as soon as its capacity was exceeded; it now keeps serving for
  thirty days beyond capacity, with the warning e-mail sent as usual. License keys issued with an
  explicit grace period are unaffected, and the grace capacity is unchanged.
* **Products are recorded under a new name.** A license added from now on is stored as
  `PostSharpUltimate` where it used to be stored as `Ultimate`. Existing rows are left alone and a
  request naming either spelling finds both, so nothing has to be migrated.

The audit log is signed with a key kept in `App_Data\audit-signing.key`, generated on first start.
Include it in your backups and preserve it across upgrades: losing it does not invalidate existing
rows, but it does start a new signature chain.

## Building from source

### Requirements

* The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
* PowerShell 7.4 or later.
* Access to the package feed that carries `SharpCrafters.Backstage`, which is where the licensing
  component comes from.

### Instructions

The repository is built with [PostSharp.Engineering](https://github.com/postsharp/PostSharp.Engineering)
as the `Backstage.LicenseServer` product of the Backstage 2027.0 family.

```
./Build.ps1 prepare
./Build.ps1 test
```

`prepare` resolves the dependencies and writes `global.json`, `nuget.config` and the version files,
none of which is in source control. `build` and `test` do it themselves, so `prepare` is only needed
before opening the solution in an IDE or running `dotnet` directly.

`./Build.ps1 build` writes the release archive to `artifacts\publish\private`, and leaves its
contents unpacked in `artifacts\app`, which is what the container image is made from. A public build
also copies the archive to `artifacts\publish\public`, which is what the deployment uploads.

To build against a local checkout of the licensing component instead of the published packages,
point the dependency at it and build that repository first:

```
./Build.ps1 dependencies set local Backstage --path <path to the SharpCrafters.Backstage repository>
```

### Running locally

```
./Build.ps1 prepare
dotnet run --project src\SharpCrafters.Backstage.LicenseServer.Web
```

The development configuration uses a SQLite database created on first start, so no SQL Server is
needed. Once the server is running, `/Admin/GenerateDemoData` fills it with simulated activity; that
page exists only in a development environment.

### Repository layout

| Project | Contents |
|---|---|
| `src\SharpCrafters.Backstage.LicenseServer.Core` | The licensing rules, the database model and the services they depend on. |
| `src\SharpCrafters.Backstage.LicenseServer.Web` | The web application: the pages, the endpoints and the composition root. |
| `tests\SharpCrafters.Backstage.LicenseServer.Tests` | The test suite. Runs against an in-memory database, so it needs no SQL Server. |
| `eng` | The product definition and the version files that PostSharp.Engineering builds from. |

To put a server under load, use `LicenseServerLoadSimulator` in the SharpCrafters.Backstage
repository. It simulates an organization of many users on many machines, and it builds the same
request the product builds, so what it measures is what a real client costs.

## Support

Please use PostSharp support facility at https://www.postsharp.net/support.
