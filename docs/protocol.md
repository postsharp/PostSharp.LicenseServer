# The license server protocol

A client asks this server for a lease: permission to use a license key for a limited period. The
protocol is the one PostSharp has been speaking since version 5, and every deployed client speaks it,
so the shape of each request and each response is a contract that this server cannot change.

This document describes what the server accepts and what it answers. It is written for whoever
maintains a client, diagnoses a deployment, or changes this server.

The client side lives in `SharpCrafters.Backstage.Licensing.LicenseServer`, in the
SharpCrafters.Backstage repository. `LicenseServerClient` builds the requests, `LicenseLease` parses
the responses, and `LicenseServerLoadSimulator` drives the whole protocol against a running server.

## Conventions

| Property | Value |
|---|---|
| Transport | HTTP or HTTPS. The server does not require HTTPS; a client warns the user when a license server URL is plain HTTP. |
| Method | `GET` for every endpoint. |
| Content type | `text/plain` for every response body, without a charset parameter. Bodies are UTF-8. |
| Paths | `Lease.ashx`, `GetTime.ashx` and `Admin/Export.ashx`, relative to the base URL of the server. |
| Query arguments | Percent-encoded. Argument names are case-sensitive. |
| Instants | UTC, in the XML round-trip representation, for instance `2026-09-22T07:07:08.3615328Z`. |

The `.ashx` extension is historical. The server is no longer an ASP.NET application with handlers,
but the paths are kept because clients have them compiled in.

A client that is given a URL already carrying a query string sends that URL verbatim instead of
appending `Lease.ashx`. This exists for a URL supplied directly to a build, and registration refuses
such a URL.

### Authentication

A lease request is anonymous by design. The server answers one that carries no credentials, and that
is how the protocol is meant to be used. A client sends the credentials of the current user all the
same, unless it is told otherwise, because a server published by IIS with Windows authentication
answers 401 to an anonymous request. When credentials do arrive, the server records who borrowed each
lease.

Authentication is there to gate the administrative interface. The pages under `/Admin` and the audit
log export are the part that needs closing, and securing them is the administrator's responsibility;
see [configuration.md](configuration.md).

Setting `LicenseServer:RequireAuthenticatedLeaseRequests` makes the server refuse an anonymous lease
request as well. It is off by default.

The protocol carries the name of the user and the name of the machine in the query string, so a
server reached over plain HTTP transmits both in cleartext. A client warns about that and serves the
build anyway.

## Leasing a license: `GET /Lease.ashx`

### Request

| Argument | Required | Meaning |
|---|---|---|
| `user` | yes | The account borrowing the license, as the operating system names it, for instance `CONTOSO\alice`. |
| `machine` | yes | The machine name, a hyphen, and a hexadecimal machine identifier. See below. |
| `version` | no | The version of the client, for instance `2027.0.0`. |
| `buildDate` | no | The date the client was built, in the round-trip format. |
| `product` | no | The product whose pool of licenses the server should allocate from, for instance `MetalamaProfessional`. |

An example, before percent-encoding:

```
GET /Lease.ashx?user=CONTOSO\alice&machine=DESKTOP-ABC-1f2e3d4c&version=2027.0.0&buildDate=2026-01-15T00:00:00.0000000Z&product=MetalamaProfessional
```

The server lowercases `user` and `machine` before it stores or compares them, so two clients that
differ only in case are one user on one machine.

#### The machine argument

The value is the machine name, a hyphen, and the lower-case hexadecimal hash of the machine
identifier. The hash distinguishes two machines that carry the same name, which happens with cloned
virtual machines and with build agents created from an image.

The suffix is load-bearing on the server side as well. Before the server compares a machine name to
its list of build servers it strips one trailing `-` followed by hexadecimal digits, so
`buildagent-1f2e` matches the configured name `buildagent`. Exactly one group is removed and every
client appends the hash, so a machine whose own name ends in hexadecimal keeps it: `build-01` arrives
as `build-01-1f2e` and is compared as `build-01`.

The server has to find the hash by its shape, because the name and the hash travel in one argument.
Sending them separately would remove the guess, and could only be added beside this argument rather
than in place of it, since deployed clients send what they send.

#### The version argument

A client older than PostSharp 5 does not send `version`. The server reads an absent one as `4.9.9`,
which is what places the client before the version that introduced the argument. A client that
declares no version of its own sends `0.0` rather than omitting the argument.

The version decides two things: whether a license that requires a newer client may be served, and
which wording the server uses when it refuses. See [Denials](#denials).

#### The buildDate argument

The build date is compared to the end of the maintenance subscription of a license. A build produced
after the subscription ended is not covered by it, whatever the date on which it runs.

A client with no build date of its own sends the earliest representable instant,
`0001-01-01T00:00:00.0000000`, rather than an empty value, which the server cannot parse. A server
that receives no `buildDate` at all skips the subscription check.

#### The product argument

PostSharp never sent this argument. A server that receives no product allocates from any pool it
holds, which is what those clients rely on.

A client of the Backstage generation names the product of its family, so that one server can hold the
licenses of several products side by side.

The value is the name of a member of the `LicenseProduct` enumeration of SharpCrafters.Backstage.
The server matches it against the `ProductCode` column, accepting both spellings of a product whose
name changed between the two licensing libraries: a request for `PostSharpUltimate` also finds the
licenses that an earlier version of this server stored as `Ultimate`. The pairs are listed in
`ProductCodes`.

### Response

A granted lease is answered with `200 OK` and a body of four named parts:

```
License: 900001-ZEE78XQQ…ZAUPA; StartTime: 2026-09-22T07:07:08.3615328Z; EndTime: 2026-09-25T07:07:08.3615328Z; RenewTime: 2026-09-24T07:07:08.3615328Z
```

| Part | Meaning |
|---|---|
| `License` | The license key the client uses until the lease ends. |
| `StartTime` | The instant the lease began. |
| `EndTime` | The instant after which the license key may no longer be used. |
| `RenewTime` | The instant from which the client should ask for a new lease. It precedes `EndTime`. |

The parsing on the client side is lenient, and a server may rely on that:

- The parts are separated by `;`. A license key is an identifier, a hyphen and Base32 characters, so
  it never contains one.
- Each part is split at its first `:`, so the colons inside an instant are kept.
- The name of a part is matched without regard to case.
- A part that the client does not understand is ignored. A later version of the server may therefore
  add a part, but may not rename or reorder one.
- Only `License` is mandatory. A client that receives no `StartTime` uses the current instant, no
  `EndTime` gives a lease of one day, and no `RenewTime` gives `EndTime`.
- A client reads the instants as UTC and keeps them as UTC.

A client reads at most 64 KiB of the body. A real lease is a few hundred bytes; the bound is there so
that a broken or hostile server cannot make a client read without end.

### Status codes

| Code | Body | Meaning |
|---|---|---|
| 200 | The lease, as above. | A license was allocated. |
| 400 | `Missing query string argument: machine.` | `machine` was absent or empty. |
| 400 | `Missing query string argument: user.` | `user` was absent or empty. |
| 400 | `Cannot parse the argument: version.` | `version` is not a version number. |
| 400 | `Cannot parse the argument: buildDate.` | `buildDate` is not a round-trip date. |
| 403 | `No license with free capacity. ` followed by one explanation per license. | No license could serve the request. |
| 503 | `Service overloaded.` | The server did not obtain its lease lock within `LicenseServer:MutexTimeout` seconds. |

A client shows the body of a 403 to the user, because it is the explanation the administrator of the
server needs. Any other status is reported as the status alone.

### Denials

The body of a 403 begins with `No license with free capacity. ` and then carries the reason each
license was passed over, separated by spaces. A server holding no license at all answers the prefix
alone.

The reasons a license is passed over:

| Reason | Message |
|---|---|
| The key does not parse or its signature does not verify. | `The license key #N is invalid.` |
| The key requires a newer licensing library than the server carries. | `The license #N requires a higher version of the licensing library on the License Server. Please upgrade the License Server to >= X.Y.Z` |
| The key requires a newer client than the one asking. | `The license #N of type T requires PostSharp version >= X.Y.Z but the requested version is A.B.C.` |
| The key may not be served by a license server at all. | `The license #N, of type T, cannot be used in the license server.` |
| The client was built after the maintenance subscription ended. | `The maintenance subscription of license #N ends on D but the requested version X.Y.Z has been built on E.` |

The last message omits the version for a client that did not send one, because such a client predates
the argument.

A request that passes every check may still be denied for capacity. The server then reports no
per-license reason, so the body is the prefix alone.

### What decides a grant

The server tries the licenses it holds in the order of their `Priority` column, lowest first, and
skips a license whose priority is negative. For each license, in order:

1. A lease this user already holds on this machine is returned unchanged, if it ends more than
   `MinLeaseDays` from now.
2. Such a lease is prolonged if it ends sooner than that. Prolonging replaces the lease with a new
   one that overwrites it, so the audit log keeps both.
3. A new lease is granted if the license has a free seat.
4. A new lease is granted beyond capacity if the license is within its grace period.

A seat is one user working on up to `LicenseServer:MachinesPerUser` machines. A user working on more
machines takes more than one seat: the number of machines divided by that setting, rounded up. At the
default of two, one or two machines are one seat and three or four are two. The capacity of a license
key is a number of seats, and the seat is the only unit the server counts in.

`EndTime` is `NewLeaseDays` from now, clamped to the expiry of the license key. `RenewTime` is
`MinLeaseDays` before `EndTime`. The server refuses to start unless `MinLeaseDays` is smaller than
`NewLeaseDays`, because a renew time that lands in the past makes every client renew on every build.

A machine named in `LicenseServer:BuildServers` is served a license key without a lease being stored,
so that build agents do not consume the seats of the developers they build for. A build agent is
exempt from consuming a seat and from nothing else: the same validation runs, and an expired or
ineligible license is refused as it would be for anybody.

### Renewal

A client stores the lease it was granted and contacts the server again only when it needs to. This is
client behaviour rather than something the server enforces, and it is what keeps the load on a server
proportional to the number of developers rather than to the number of builds.

- The stored lease is used while the current instant is before `RenewTime`.
- The client downloads a new lease once `RenewTime` has passed. A renewal on a machine the user
  already holds prolongs a seat rather than allocating one.
- A renewal that fails while the stored lease is still valid keeps the stored lease and reports
  nothing. The build has a license, and failing it because the server is briefly unreachable would be
  worse than the problem.
- A lease whose `EndTime` has passed is discarded and a new one is downloaded.
- A client discards a lease whose `EndTime` is already in the past when it arrives. Without that
  guard, a server whose clock is behind the client's would make every process download a lease, find
  it expired and download again, without end.

With the default of a three-day lease renewed after two, one machine contacts the server about once
every two days, whatever the number of builds in between.

## Reading the server clock: `GET /GetTime.ashx`

The server answers its own idea of the current instant and how much faster than real time its clock
runs, separated by `;`:

```
2026-09-17T11:00:58.5236731Z;1440
```

The acceleration is `1` on a production server, where the clock is the real one. Any other value
comes from `LicenseServer:TimeAcceleration`, and the server warns at startup when it is set.

A test harness anchors its own virtual clock on the reading and advances it at the same rate:

```
virtualNow = serverTimeAtSync + (realNow - realTimeAtSync) * acceleration
```

The round trip is not compensated. At an acceleration of 1440 a round trip of 50 ms is already
72 seconds of virtual time, so a harness synchronizes again whenever the drift shows, which for a
licensing harness means whenever a lease arrives whose renewal instant has already passed.

The virtual clock of the server is anchored when the process starts and never resets. Restarting a
server therefore moves its clock backwards, and leases granted before the restart are then dated in
the future. Drop the database as well as recycling the process between two simulations.

## Exporting the audit log: `GET /Admin/Export.ashx`

The export is not part of the client protocol. It is the file an administrator hands to an auditor,
and its format is a contract of its own, because customers archive the files and compare them across
years.

### Request

| Argument | Meaning |
|---|---|
| `fy`, `fm` | The year and month the range starts at. |
| `ty`, `tm` | The year and month the range ends at, inclusive. |

Years are between 2010 and 2100 and months between 1 and 12. Anything else is answered with `400` and
the body `The range of months is missing or invalid. Years must be between 2010 and 2100.`

The response carries `Content-Disposition: attachment` with a file name derived from the range, for
instance `PostSharp_LicenseLog_2026-1_2026-12.txt`, and is streamed as the rows are read.

### Format

One line per lease, with eight fields separated by `;`:

```
40;;900001;2026-09-17T11:02:14.1234567Z;2026-09-20T11:02:14.1234567Z;d97556dbab6becaa;93cf1e71b44530bd;J+QFuzRr4fT+mkwzkYOQ/NZtkVy2EHWj1b9t82h/xB8=
```

| Field | Meaning |
|---|---|
| 1 | The identifier of the lease. |
| 2 | The identifier of the lease this one overwrote, empty for a lease that overwrote none. |
| 3 | The identifier of the license. |
| 4 | The instant the lease began, in UTC. |
| 5 | The instant the lease ended, in UTC. |
| 6 | The hash of the machine name. |
| 7 | The hash of the user name. |
| 8 | The signature. |

Names appear only as hashes, so the file can be shared without disclosing who works where. The hash
is the lower-case hexadecimal of an unkeyed 64-bit hash of the name, trimmed and lower-cased first.
The algorithm is MD5 truncated to its first eight bytes read as a little-endian signed integer. MD5
is not chosen for its cryptographic properties, which are irrelevant to an anonymising hash, but
because the values have to equal the ones PostSharp has been producing since 2013 and the license
audit of Backstage hashes the same names the same way.

### The signature chain

Each signature covers the signature of the previous lease, a semicolon, and the seven fields of its
own line:

```
signature(n) = base64( HMAC-SHA256( key, signature(n-1) + ";" + fields 1 to 7 of line n ) )
```

The first lease of a database chains onto an empty string. The key is the one described under
Auditing in [configuration.md](configuration.md).

An auditor recomputes the chain from an exported file alone, because the signed payload is exactly
the line that is exported. This is new. The previous implementation called the parameterless
`HMAC.Create()`, which produced a hash under a randomly generated key on every call, so no chain it
wrote was ever verifiable.

A range of months is resolved to a range of lease identifiers, and every lease in that range is
exported. The result is a contiguous run of the log rather than a filtered selection, which is what
keeps the chain verifiable, and it is why a few leases outside the requested months appear in the
file.

## Compatibility

The server keeps these behaviours because clients depend on them. Changing any of them breaks a
client that is already deployed.

| Behaviour | Why |
|---|---|
| The `.ashx` paths. | Clients have them compiled in. |
| An absent `version` means 4.9.9. | It is how a client older than PostSharp 5 is recognized. |
| An absent `product` means any product. | No PostSharp client ever sent the argument. |
| The four part names of a lease, and their order. | A client splits the body positionally in spirit, even though it matches by name. |
| The status codes, and the body of a 403. | A client shows the body of a 403 to the user and treats every other non-200 as a failure. |
| The trailing hexadecimal suffix of a machine name is stripped before matching a build server. | The list of build servers in an existing configuration names machines without it. |
| The field order of an audit line, and the hash of the names. | Customers archive exported files and compare them across years. |
