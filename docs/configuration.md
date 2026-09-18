# Configuring the license server

The server reads its settings from `appsettings.json`, in the application directory. Every setting
can also be given as an environment variable. In the name of the variable, a colon becomes a double
underscore: the connection string is `ConnectionStrings__SharpCrafters_LicenseServerConnectionString`.
Use an environment variable for a password, so that the password is not written in a file.

Every setting has a default value. A setting that is absent from the file keeps its default, so the
file needs to contain only the settings you change.

The server validates the settings when it starts. It refuses to start when a setting is invalid or
when two settings contradict each other, instead of failing later on a lease request.

## An example

The following `appsettings.json` configures a server that stores its data in SQL Server, restricts
the administrative pages to one Windows group, and sends notifications.

```json
{
  "ConnectionStrings": {
    "SharpCrafters_LicenseServerConnectionString": "Server=db.example.com,1433;Database=PostSharpLicenseServer;Integrated Security=True;Encrypt=False"
  },
  "Authentication": {
    "Scheme": "IISIntegrated"
  },
  "LicenseServer": {
    "DatabaseProvider": "SqlServer",
    "MachinesPerUser": 2,
    "NewLeaseDays": 3,
    "MinLeaseDays": 1,
    "BuildServers": "buildagent-1;buildagent-2",
    "AdminRoles": [ "DOMAIN\\PostSharp Administrators" ],
    "GracePeriodWarningEmailTo": "licenses@example.com",
    "DeniedRequestEmailTo": "licenses@example.com"
  },
  "Smtp": {
    "Enabled": true,
    "Host": "smtp.example.com",
    "Port": 587,
    "EnableSsl": true,
    "FromAddress": "licenses@example.com"
  }
}
```

The `appsettings.json` of the release package lists most of these settings with their default values.

## Database

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:DatabaseProvider` | `SqlServer` | The database engine: `SqlServer` or `Sqlite`. |
| `ConnectionStrings:SharpCrafters_LicenseServerConnectionString` | a local SQL Server | The database to connect to. |

The two settings are set together. The connection string is interpreted by the engine named in
`DatabaseProvider`, so the two must agree. For SQL Server:

```json
{
  "LicenseServer": { "DatabaseProvider": "SqlServer" },
  "ConnectionStrings": {
    "SharpCrafters_LicenseServerConnectionString": "Server=db.example.com,1433;Database=PostSharpLicenseServer;Integrated Security=True;Encrypt=False"
  }
}
```

For SQLite:

```json
{
  "LicenseServer": { "DatabaseProvider": "Sqlite" },
  "ConnectionStrings": {
    "SharpCrafters_LicenseServerConnectionString": "DataSource=licenseserver.db"
  }
}
```

SQL Server is the engine supported in production. Create the schema by running
`Database\CreateTables.sql`. The server never creates the schema and never modifies it, so an upgrade
of the server makes no change to the database.

SQLite is supported for tests and for evaluation. The test suite of this repository runs on SQLite,
and so does the development configuration of the web project. The database file is created at the
first start. A relative path is resolved against the application directory, not against the working
directory of the process. Do not use SQLite for a server that serves a team: SQLite accepts one
writer at a time, and every lease request writes.

`Microsoft.Data.SqlClient`, the SQL Server client, encrypts the connection by default. The connection
fails when the web server does not trust the certificate of the SQL Server. Add `Encrypt=False` to
the connection string to connect without encryption, or `TrustServerCertificate=True` to keep the
encryption and skip the verification of the certificate.

The default connection string contains `Integrated Security=True`. The application then authenticates
to SQL Server as the Windows account it runs under. This requires a host that is joined to the
domain. On another host, and on Linux and macOS, use a SQL Server login:

```
Server=db.example.com,1433;Database=PostSharpLicenseServer;User Id=licenseserver;Password=...;TrustServerCertificate=True
```

Give this connection string in the environment variable
`ConnectionStrings__SharpCrafters_LicenseServerConnectionString`, so that the password is not written
in `appsettings.json`.

## Licensing rules

A seat is one user working on up to `MachinesPerUser` machines. A user working on more machines takes
more than one seat. The number of seats is the number of machines divided by `MachinesPerUser`,
rounded up. With the default value of two, one or two machines are one seat, and three or four
machines are two seats.

The capacity of a license key is a number of seats. The seat is the only unit the server counts. The
license agreement expresses the same rule in the opposite direction: it grants a number of authorized
users, and each user may work on a number of devices.

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:MachinesPerUser` | 2 | How many machines one seat covers. Read your license agreement before you change it. |
| `LicenseServer:NewLeaseDays` | 3 | How long a new lease lasts. |
| `LicenseServer:MinLeaseDays` | 1 | How long before the end of a lease the client renews it. Raise this value above the number of days your developers work offline. Must be smaller than `NewLeaseDays`. |
| `LicenseServer:BuildServers` | empty | The machine names of the build agents, separated by semicolons, commas or spaces. A build agent receives a license but no lease, so that it does not consume the seat of a developer. A trailing hexadecimal identifier is ignored, so `buildagent-1f2e` matches `buildagent`. |

## Notifications

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:GracePeriodWarningEmailTo` | empty | Who is informed that the license is over capacity. |
| `LicenseServer:GracePeriodWarningEmailCC` | empty | Who else is informed. |
| `LicenseServer:DeniedRequestEmailTo` | empty | Who is informed that a request was denied. |
| `LicenseServer:GracePeriodWarningDays` | 1 | How many days the server waits before it repeats a warning. |
| `Smtp:Enabled` | `false` | Whether notifications are sent. |
| `Smtp:Host`, `Smtp:Port`, `Smtp:EnableSsl` | `localhost`, 25, `false` | The SMTP server. |
| `Smtp:FromAddress` | `sales@postsharp.net` | The sender. |
| `Smtp:UserName`, `Smtp:Password` | empty | The credentials of the SMTP server, when it requires them. Give the password in an environment variable. |

An address left empty disables that notification. A notification that cannot be sent is written to
the log. A failure to send a notification never denies a license.

## Access

| Setting | Default | Meaning |
|---|---|---|
| `Authentication:Scheme` | detected | `IISIntegrated`, `Negotiate` or `None`. See below. |
| `LicenseServer:AdminRoles` | empty | The Windows groups allowed to open the administrative pages, for example `["DOMAIN\\PostSharp Administrators"]`. |
| `LicenseServer:RequireAuthenticatedLeaseRequests` | `false` | Whether a lease request must be authenticated. |

A lease request carries the user name and the machine name in its query string. The server trusts
these two values and records them in the `UserName` and `Machine` columns, and it counts seats from
them. Authentication does not provide them.

Authentication has two other purposes. It records the caller in the `AuthenticatedUser` column of the
audit log, next to the user name that the request declared. It also restricts access:
`RequireAuthenticatedLeaseRequests` requires the caller of `Lease.ashx` to be authenticated, and
`AdminRoles` restricts the administrative pages to the members of the groups it names.

| Scheme | Use it when |
|---|---|
| `IISIntegrated` | The application is hosted by IIS. Windows authentication must also be enabled on the site in IIS Manager. |
| `Negotiate` | The application runs in its own process, on a host joined to your domain. On a system other than Windows, this requires Kerberos and a keytab. |
| `None` | There is no domain to authenticate against, for example in a container. Requests are served anonymously and the `AuthenticatedUser` column stays empty. The server writes a warning to the log at every start. |

When `Authentication:Scheme` is absent, the server selects `IISIntegrated` when IIS hosts it,
`Negotiate` on Windows, and `None` on another system, and writes the selected scheme to the log. Set
the scheme explicitly. A wrong selection is not reported as an error, because a server that
authenticates no caller still serves leases. The consequence is that the `AuthenticatedUser` column
stays empty.

### Securing the administrative pages

Restricting the administrative pages is the responsibility of the administrator. The server does not
restrict them on its own. `AdminRoles` and `RequireAuthenticatedLeaseRequests` are both empty in the
released configuration, as the corresponding sections were commented out in the `Web.config` of
earlier versions. A restrictive default would lock an administrator out of their own server during an
upgrade. The server writes a warning to the log at every start while `AdminRoles` is empty.

Restrict these pages. They are the only way to add and to revoke a license, and the export endpoint
at `/Admin/Export.ashx` returns the whole audit log. There are two mechanisms, and you can combine
them.

#### The AdminRoles setting

`LicenseServer:AdminRoles` covers every page under `/Admin` and the export endpoint. It requires an
authentication scheme that reports the Windows groups of the caller, so it works with
`IISIntegrated` and with `Negotiate`, and not with `None`.

#### A restriction on the path, in the web server

A restriction configured in the web server works with any authentication scheme. Under IIS, enable
Windows authentication on the site, then add a URL authorization rule for the `Admin` path to the
`web.config` of the application, which is in the published output:

```xml
<location path="Admin">
  <system.webServer>
    <security>
      <authorization>
        <remove users="*" roles="" verbs="" />
        <add accessType="Allow" roles="DOMAIN\PostSharp Administrators" />
      </authorization>
    </security>
  </system.webServer>
</location>
```

This rule requires the URL Authorization role service of IIS, which is not installed by default.
Behind another web server, and in a container, restrict the path in the component that receives the
requests.

## Concurrency

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:LeaseLockMode` | `InProcess` | How the server serializes concurrent lease requests. |
| `LicenseServer:MutexTimeout` | 30 | How many seconds a request waits for its turn before the server answers 503. |

The server serializes lease requests, so that two concurrent requests cannot both take the last free
seat. `InProcess` serializes the requests of one worker process. This is correct for the supported
deployment, which is one worker process per database.

Run one worker process. One process is enough: a developer sends about one request per day, and the
requests are short.

Two deployments run several worker processes against one database: an IIS web garden, and several
instances of the server. The serialization then covers each process separately, and the server can
grant more leases than the capacity of the license. If you need such a deployment, open an issue that
asks for `SqlApplicationLock`, which serializes through the database.

## Auditing

The `Leases` table is the audit log. The server never updates a lease and never deletes one:
prolonging a lease and cancelling a lease both insert a new row. Export the log from the page
[Audit log](protocol.md#exporting-the-audit-log-get-adminexportashx), which writes one line per lease.

The audit log has no setting. Protect it as you protect the database: with the permissions of the
database and with your backups.

## Storage

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:DataDirectory` | `App_Data` | Where the server stores the files it generates. |

The directory contains the test licensing authority, when the server has one. A relative path is
resolved against the application directory, so the default value works wherever you unpack the
release package. Set an absolute path to store these files on another volume.

A server that serves the license keys of a production authority writes nothing to this directory.

In a container, this directory must be a volume when the server uses a test licensing authority. The
image declares a volume at `/app/App_Data`, so the files survive the replacement of the container
even when no volume is named. See [docker.md](docker.md).

## Monitoring

| Path | Answers |
|---|---|
| `/health/live` | Whether the process answers. It runs no check. |
| `/health` | Whether the process answers, the database can be queried, and a license can serve a lease. |
| `/version` | Which build is deployed, and which version of the licensing library it uses to parse license keys. |

The server serves these three endpoints without authentication, as it serves `Lease.ashx`. A
monitoring agent has no Windows credentials, and a probe that receives 401 reports the server as
unavailable. The responses contain no license key, no user name and no connection string. The
version number is readable by anyone who can reach the server.

The operating requirements of this server are low. One developer sends about one request per day,
because the client stores its lease and renews it after `NewLeaseDays` minus `MinLeaseDays` days. A
client that cannot reach the server keeps the lease it holds, so an interruption of a few hours
affects nobody. Monitor the server to learn that it needs attention, and not to fail over.

`/health` reports the result of each check in its body. It has three states:

| Status | Code | Means |
|---|---|---|
| `Healthy` | 200 | The database answers and a license can serve a lease. |
| `Degraded` | 200 | The server works, but no license can serve a lease. |
| `Unhealthy` | 503 | The database cannot be queried. |

```json
{
  "status": "Degraded",
  "checks": [
    { "name": "database", "status": "Healthy", "description": "The database answers." },
    { "name": "licenses", "status": "Degraded", "description": "No license can serve a lease: 1 expired." }
  ]
}
```

The database check runs a query on the license table instead of opening a connection. The server
never creates its schema on SQL Server, so a database that accepts connections but has no schema is a
deployment error. The query detects it; opening a connection does not.

The license check reports whether a license can serve a lease at this moment. It warns when the
server has no license, and when every license is expired, disabled, unparsable, or full with its
grace period over. A server in that state answers every lease request with 403 while its process and
its database are healthy.

The license check warns and never fails. An expired license needs an administrator, and restarting
the server does not add a license, so the state is reported and the probe still succeeds. Only the
process and the database can fail the probe.

Configure your monitoring system to report the status in the body, and not only the status code. The
server also writes a warning to its log at each degraded check:

```
warn: Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService[103]
      Health check licenses with status Degraded completed after 33.8303ms with message 'No license is registered.'
```

The license check answers a weaker question than a lease request. A lease request names a product, a
version and a build date, and a license can be refused because of any of the three. A server that
reports `Healthy` can therefore still deny an individual request. See
[the protocol](protocol.md#what-decides-a-grant).

## Testing

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:TimeAcceleration` | 1 | How much faster than real time the clock of the server runs. |
| `LicenseServer:SeedTestLicenses` | `false` | Whether the server issues to itself the license keys it serves. |
| `LicenseServer:TestLicensingAuthorities` | empty | The licensing authorities whose license keys a development server accepts, in addition to the production authority. |

The server refuses to start when one of the last two settings is set outside the Development
environment.

Keep `TimeAcceleration` at 1. Another value exists so that a licensing scenario that lasts several
days can be replayed against a test server in a few minutes. The server writes a warning to the log
when the value is not 1.

`SeedTestLicenses` exists so that an evaluation or a load simulation has a license to lease before
anyone buys one. The server generates a licensing authority of its own in
[`DataDirectory`](#storage), accepts the license keys of that authority, and adds one license key per
product family when the database contains none. No other server accepts these license keys. The
authority must survive a restart, because the license keys it signed are stored in the database and
stop being valid when the key pair changes. In a container, this means that the data directory must
be a volume.

`TestLicensingAuthorities` exists so that a load simulation can run against license keys that are not
sold. Each entry contains the identifier of the key that signs a license key, and the public half of
that key, in the XML representation that SharpCrafters.Backstage reads:

```json
{
  "LicenseServer": {
    "TestLicensingAuthorities": [
      {
        "KeyId": 200,
        "PublicKey": "<ECDSAKeyValue><Curve>nistP256</Curve><X>…</X><Y>…</Y></ECDSAKeyValue>"
      }
    ]
  }
}
```

The identifier must differ from the identifiers of the production keys, which are 0, 1 and 2. The
server refuses to start on a duplicate identifier. It also refuses to start when this setting is used
outside the Development environment, because whoever holds the private half of the key pair can
create license keys that such a server accepts. Keep the private half out of source control, and
configure the public half in user secrets instead of `appsettings.json`.
