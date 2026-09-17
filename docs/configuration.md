# Configuring the license server

All settings live in `appsettings.json`, next to the application. Any of them can also be supplied
as an environment variable, where a colon becomes a double underscore: the connection string, for
instance, is `ConnectionStrings__SharpCrafters_LicenseServerConnectionString`. That is the usual way
to keep a password out of a file.

The server validates its settings when it starts and refuses to start on an invalid combination,
rather than failing later on a lease request.

## Database

| Setting | Default | Meaning |
|---|---|---|
| `ConnectionStrings:SharpCrafters_LicenseServerConnectionString` | a local SQL Server | The database. |
| `LicenseServer:DatabaseProvider` | `SqlServer` | `SqlServer` or `Sqlite`. |

SQL Server is the supported engine for a production installation. Create its schema by running
`Database\CreateTables.sql`; the server never creates or alters it, so an upgrade cannot surprise you.

SQLite is offered for evaluation, and is what the test suite uses. Its database is created on first
start. A relative path is resolved against the application directory, not against whatever the
working directory happens to be.

Note that the modern SQL client encrypts connections by default. Against a server whose certificate
the web server does not trust, add `Encrypt=False` to the connection string, or
`TrustServerCertificate=True` to keep the encryption and skip only the certificate check.

The default connection string uses `Integrated Security=True`, which authenticates as the Windows
account the application runs under. That does not work on a host which is not joined to the domain,
so off Windows use a SQL Server login:

```
Server=db.example.com,1433;Database=PostSharpLicenseServer;User Id=licenseserver;Password=...;TrustServerCertificate=True
```

Supply that as the environment variable
`ConnectionStrings__SharpCrafters_LicenseServerConnectionString` rather than writing the password
into `appsettings.json`.

## Licensing rules

A seat is one user working on up to `MachinesPerUser` machines. A user working on more machines takes
more than one seat: the number of machines divided by `MachinesPerUser`, rounded up. At the default of
two, one or two machines are one seat and three or four are two.

The capacity of a license key is a number of seats, and the seat is the only unit the server counts
in. The license agreement states the same rule the other way round, as a number of authorized users
each entitled to a number of devices.

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:MachinesPerUser` | 2 | How many machines one seat covers. Check your license agreement before changing it. |
| `LicenseServer:NewLeaseDays` | 3 | How long a new lease lasts. |
| `LicenseServer:MinLeaseDays` | 1 | How long before the end of a lease a client starts renewing it. If your developers work offline for weeks at a time, raise this above the number of days they are away. Must be smaller than `NewLeaseDays`. |
| `LicenseServer:BuildServers` | empty | The machine names of build agents, separated by semicolons, commas or spaces. A build agent is served a license but is not given a lease, so that it does not consume a developer's seat. A trailing hexadecimal identifier is ignored, so `buildagent-1f2e` matches `buildagent`. |

## Notifications

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:GracePeriodWarningEmailTo` | empty | Who is told that the license is over capacity. |
| `LicenseServer:GracePeriodWarningEmailCC` | empty | Who else is told. |
| `LicenseServer:DeniedRequestEmailTo` | empty | Who is told that a request was denied. |
| `LicenseServer:GracePeriodWarningDays` | 1 | How many days to wait before repeating a warning. |
| `Smtp:Enabled` | `false` | Whether notifications are sent at all. |
| `Smtp:Host`, `Smtp:Port`, `Smtp:EnableSsl` | `localhost`, 25, `false` | The SMTP server. |
| `Smtp:FromAddress` | `sales@postsharp.net` | The sender. |
| `Smtp:UserName`, `Smtp:Password` | empty | Credentials, if the SMTP server needs them. Supply the password as an environment variable. |

An address left empty suppresses that notification. A notification that cannot be delivered is
logged and never denies a developer their license.

## Access

| Setting | Default | Meaning |
|---|---|---|
| `Authentication:Scheme` | detected | `IISIntegrated`, `Negotiate` or `None`. See below. |
| `LicenseServer:AdminRoles` | empty | The Windows groups allowed to reach the administrative pages, for example `["DOMAIN\\PostSharp Administrators"]`. |
| `LicenseServer:RequireAuthenticatedLeaseRequests` | `false` | Whether a lease request must be authenticated. |

The authentication scheme decides how the server learns who is borrowing a license, which is what it
records in the `AuthenticatedUser` column of the audit log.

| Scheme | Use it when |
|---|---|
| `IISIntegrated` | The application is hosted by IIS. Windows authentication must also be enabled on the site in IIS Manager. |
| `Negotiate` | The application runs under its own process on a host joined to your domain. On anything other than Windows this needs Kerberos and a keytab. |
| `None` | There is no domain to authenticate against, as in a container. Requests are served anonymously and leases record an empty user. The server warns at startup that it is doing this. |

Left unset, the server picks `IISIntegrated` when it finds itself hosted by IIS, `Negotiate` on
Windows, and `None` elsewhere, and logs which one it chose. Set the value explicitly on anything you
care about: guessing wrong is quiet rather than loud, because a server that authenticates nobody
still serves leases perfectly well — it just cannot say who took them.

### Securing the administrative pages

Securing the administrative pages is the administrator's responsibility. The server does not do it on
its own: `AdminRoles` and `RequireAuthenticatedLeaseRequests` both default to open, which is how the
license server has always shipped, so that an upgrade cannot lock an administrator out of their own
server. The server writes a warning to its log at every start until `AdminRoles` is set.

Closing them is worth doing. The administrative pages are the only way to add or revoke a license,
and the export at `/Admin/Export.ashx` hands over the whole audit log. There are two ways to close
them, and they can be combined.

The first is `LicenseServer:AdminRoles`. The check covers every page under `/Admin` and the export
endpoint. It needs an authentication scheme that supplies the Windows groups, so it works with
`IISIntegrated` and with `Negotiate`, and not with `None`.

The second is to restrict the path at the web server, which works whatever the scheme. Under IIS,
enable Windows authentication on the site and add a URL authorization rule for the `Admin` path to
the `web.config` of the application, which is in the published output:

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

This needs the URL Authorization role service of IIS, which is not installed by default. Behind any
other web server, and in a container, restrict the path in whatever sits in front of the application.

## Concurrency

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:LeaseLockMode` | `InProcess` | How concurrent lease requests are serialized. |
| `LicenseServer:MutexTimeout` | 30 | How many seconds a request waits for its turn before the server answers 503. |

Lease requests are serialized so that two of them cannot both conclude that the last free seat is
theirs. `InProcess` serializes them within one worker process, which is correct for the supported
deployment of a single process per database.

If you run the license server as an IIS web garden, behind a load balancer, or as several
containers, that guarantee no longer holds and the server can over-allocate. Either run a single
worker process, or open an issue asking for `SqlApplicationLock`, which serializes through the
database and is reserved for exactly this case.

## Auditing

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:AuditHmacKey` | empty | The base64 key that signs the audit log. |

Each lease in the audit log is signed together with the signature of the previous one, so that a
removed or altered row breaks every signature after it. When no key is configured, one is generated
on first start and written to `App_Data\audit-signing.key`.

Include that file in your backups and preserve it across upgrades. Losing it does not invalidate the
rows already written, but it does start a new chain.

## Storage

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:DataDirectory` | `App_Data` | Where the server keeps the files it generates and must not lose. |

The directory holds the audit signing key, and the test licensing authority when there is one. A
relative path is resolved against the application, so the default works wherever the archive is
unpacked. Set an absolute path to put the directory on storage of its own.

In a container this directory has to be a volume. The image declares one at `/app/App_Data`, so the
files survive the container being replaced even when no mount is given; name the volume in a real
deployment and back it up with the database. See [docker.md](docker.md).

## Testing

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:TimeAcceleration` | 1 | How much faster than real time the server's clock runs. |
| `LicenseServer:SeedTestLicenses` | `false` | Whether the server issues itself the license keys it serves. |
| `LicenseServer:TestLicensingAuthorities` | empty | Licensing authorities whose license keys a development server accepts besides the production one. |

The server refuses to start with either of the last two set outside the Development environment.

Leave `TimeAcceleration` at 1. Any other value exists so that a multi-day licensing scenario can be
replayed in minutes against a test server, and the server warns at startup when it is set.

`SeedTestLicenses` exists so that a trial or a load simulation has something to lease without anybody
buying a license first. The server generates a licensing authority of its own into
[`DataDirectory`](#storage), trusts it, and adds one license key per product family if the database
has none. No other server accepts those keys. The authority has to survive a restart, because the
license keys it signed are in the database and stop verifying when the key pair changes; in a
container that means the data directory has to be a volume.

`TestLicensingAuthorities` exists so that a load simulation can be run against license keys that
nobody sells. Each entry carries the identifier that the signature of a license key names and the
public half of the key pair, in the XML representation that SharpCrafters.Backstage reads:

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

The identifier must differ from the identifiers of the production keys, which are 0, 1 and 2, and
the server refuses to start on a duplicate. It also refuses to start with this setting outside the
Development environment: whoever holds the private half of the pair can mint license keys that a
server configured this way honours. Keep the private half out of source control, and configure the
public half through user secrets rather than through `appsettings.json`.
