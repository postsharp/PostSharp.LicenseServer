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
the web server does not trust, add `Encrypt=False` to the connection string.

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
| `Authentication:Scheme` | `IISIntegrated` | `IISIntegrated` behind IIS, `Negotiate` for self-hosting. |
| `LicenseServer:AdminRoles` | empty | The Windows groups allowed to reach the administrative pages, for example `["DOMAIN\\PostSharp Administrators"]`. |
| `LicenseServer:RequireAuthenticatedLeaseRequests` | `false` | Whether a lease request must be authenticated. |

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

Leave this at 1. Any other value exists so that a multi-day licensing scenario can be replayed in
minutes against a test server, and the server warns at startup when it is set.
