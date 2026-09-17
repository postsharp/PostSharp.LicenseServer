# PostSharp.LicenseServer

This repository contains the source code and releases of PostSharp License Server.

The use of the license server is optional. Since all commercial licenses are floating ones,
the license server can help teams knowing how many licenses they actually use.

The license server is an ASP.NET Core application with an MS SQL back-end.

We at PostSharp consider that it is the customer's sole responsibility to respect the license agreement, and this is why we are providing the source code of the license server. Note that the use of licenses keys [is audited anyway](http://doc.postsharp.net/license-audit); if this is not an option for your organization, you can ask the PostSharp sales team for a license key with audit waiver.

## License

The license server itself is licensed under the *MIT License*. Note that PostSharp is a commercial product with a proprietary license.

## Download

You can download the latest release from https://github.com/postsharp/PostSharp.LicenseServer/releases/latest.

## Documentation

* [Installing the license server](http://doc.postsharp.net/license-server-admin).
* [Using the license server](http://doc.postsharp.net/license-server).

## Installing

### Requirements

* Windows Server with IIS.
* The [ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) for .NET 10.
* SQL Server 2016 or later.

### Instructions

1. Install the ASP.NET Core Hosting Bundle on the web server, then restart IIS with `iisreset`.
2. Create the database and run `Database\CreateTables.sql` against it.
3. Unpack `PostSharp.LicenseServer.zip` into the directory of an IIS application.
4. Edit `appsettings.json`: set the connection string, the notification e-mail addresses and the
   SMTP server. The settings are described in [docs/configuration.md](docs/configuration.md).
5. In IIS Manager, enable **Windows Authentication** on the application and disable
   **Anonymous Authentication** if you want every lease request to be attributed to a user.
6. Browse to the application and add your license key.

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

The audit log is signed with a key kept in `App_Data\audit-signing.key`, generated on first start.
Include it in your backups and preserve it across upgrades: losing it does not invalidate existing
rows, but it does start a new signature chain.

## Building from source

### Requirements

* The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

### Instructions

```
dotnet test
.\eng\Package.ps1
```

The package is written to `artifacts\PostSharp.LicenseServer.zip`.

### Running locally

```
dotnet run --project src\PostSharp.LicenseServer.Web
```

The development configuration uses a SQLite database created on first start, so no SQL Server is
needed. Once the server is running, `/Admin/GenerateDemoData` fills it with simulated activity; that
page exists only in a development environment.

### Repository layout

| Project | Contents |
|---|---|
| `src\PostSharp.LicenseServer.Core` | The licensing rules, the database model and the services they depend on. |
| `src\PostSharp.LicenseServer.Web` | The web application: the pages, the endpoints and the composition root. |
| `tests\PostSharp.LicenseServer.Tests` | The test suite. Runs against an in-memory database, so it needs no SQL Server. |
| `tests\PostSharp.LicenseServer.Simulator` | A manual load-testing tool. See the note below. |

The simulator does not currently run: it needs a client that can download a lease, which is being
written in SharpCrafters.Backstage. It is kept building so that it is ready when that client is.

## Support

Please use PostSharp support facility at https://www.postsharp.net/support.
