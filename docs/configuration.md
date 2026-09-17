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

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:MachinesPerUser` | 2 | How many devices one user may use on a single seat. Check your license agreement before changing it. |
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

**Both default to open**, which is how the license server has always shipped, so that an upgrade
cannot lock an administrator out of their own server. The administrative pages are the only way to
add or revoke a license, so setting `AdminRoles` is worth doing; until it is set, the server says so
in its log every time it starts.

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

## Testing

| Setting | Default | Meaning |
|---|---|---|
| `LicenseServer:TimeAcceleration` | 1 | How much faster than real time the server's clock runs. |
| `LicenseServer:TestLicensingAuthorities` | empty | Licensing authorities whose license keys a development server accepts besides the production one. |

Leave `TimeAcceleration` at 1. Any other value exists so that a multi-day licensing scenario can be
replayed in minutes against a test server, and the server warns at startup when it is set.

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
