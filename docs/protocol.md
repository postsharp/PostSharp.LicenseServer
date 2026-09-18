# The license server protocol

A client asks this server for a lease: the permission to use a license key during a limited period.
The protocol has not changed since PostSharp 5, and every deployed client uses it. The shape of each
request and of each response is therefore a contract that this server cannot change.

This document describes what the server accepts and what it answers. It is written for the developer
who maintains a client, diagnoses a deployment, or modifies this server.

The client is in `SharpCrafters.Backstage.Licensing.LicenseServer`, in the SharpCrafters.Backstage
repository. `LicenseServerClient` builds the requests, `LicenseLease` parses the responses, and
`LicenseServerLoadSimulator` exercises the whole protocol against a running server.

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
but the paths are kept because deployed clients contain them.

A client that receives a URL that already contains a query string sends that URL without appending
`Lease.ashx`. This case exists for a URL passed directly to a build. Registration refuses such a URL.

### Authentication

A lease request is anonymous by design. The server answers a request that carries no credentials.

The request declares the user and the machine in its query string, and the server trusts both values.
It stores them and counts seats from them. Authentication does not provide them.

A client sends the credentials of the current user nevertheless, unless it is configured otherwise,
because a server published by IIS with Windows authentication answers 401 to an anonymous request.
When credentials arrive, the server records the authenticated caller in the `AuthenticatedUser`
column, in addition to the user that the request declares.

Authentication protects the administrative interface. The pages under `/Admin` and the export of the
audit log are the resources that need protection, and protecting them is the responsibility of the
administrator. See [configuration.md](configuration.md).

`LicenseServer:RequireAuthenticatedLeaseRequests` makes the server refuse an anonymous lease request
as well. It is disabled by default.

The query string contains the name of the user and the name of the machine, so a server reached over
plain HTTP transmits both without encryption. A client warns about this and serves the build.

## Leasing a license: `GET /Lease.ashx`

### Request

| Argument | Required | Meaning |
|---|---|---|
| `user` | yes | The account that borrows the license, as the operating system names it, for instance `CONTOSO\alice`. |
| `machine` | yes | The machine name, a hyphen, and a hexadecimal machine identifier. See below. |
| `version` | no | The version of the client, for instance `2027.0.0`. |
| `buildDate` | no | The date the client was built, in the round-trip format. |
| `product` | no | The product whose pool of licenses the server allocates from, for instance `MetalamaProfessional`. |

An example, before percent-encoding:

```
GET /Lease.ashx?user=CONTOSO\alice&machine=DESKTOP-ABC-1f2e3d4c&version=2027.0.0&buildDate=2026-01-15T00:00:00.0000000Z&product=MetalamaProfessional
```

The server converts `user` and `machine` to lower case before it stores or compares them, so two
clients that differ only in case are one user on one machine.

#### The machine argument

The value is the machine name, a hyphen, and the hexadecimal hash of the machine identifier, in lower
case. The hash distinguishes two machines that have the same name. This happens with cloned virtual
machines and with build agents created from an image.

The server uses the suffix as well. Before it compares a machine name to the list of build servers,
it removes one trailing `-` followed by hexadecimal digits, so `buildagent-1f2e` matches the
configured name `buildagent`. It removes exactly one group, and every client appends the hash, so a
machine whose own name ends with hexadecimal digits keeps them: `build-01` arrives as `build-01-1f2e`
and is compared as `build-01`.

The name and the hash are in a single argument, so the server has to recognize the hash by its form.
Two separate arguments would remove this heuristic. They could only be added next to this argument
and not replace it, because deployed clients cannot be changed.

#### The version argument

A client older than PostSharp 5 does not send `version`. The server reads an absent value as `4.9.9`,
so that the client is treated as older than the version that introduced the argument. A client that
has no version of its own sends `0.0` instead of omitting the argument.

The version has two effects. It decides whether a license that requires a newer client may be served,
and it selects the wording of a refusal. See [Denials](#denials).

#### The buildDate argument

The server compares the build date to the end of the maintenance subscription of a license. A build
produced after the end of the subscription is not covered by it, whatever the date on which it runs.

A client that has no build date of its own sends the earliest representable instant,
`0001-01-01T00:00:00.0000000`, instead of an empty value, which the server cannot parse. When the
server receives no `buildDate`, it skips the subscription check.

#### The product argument

PostSharp never sent this argument. A server that receives no product allocates from any pool it
holds, and PostSharp clients depend on this behaviour.

A client of the Backstage generation names the product of its family, so that one server can hold the
licenses of several products at the same time.

The value is the name of a member of the `LicenseProduct` enumeration of SharpCrafters.Backstage. The
server matches it against the `ProductCode` column. It accepts both spellings of a product whose name
changed between the two licensing libraries: a request for `PostSharpUltimate` also finds the
licenses that an earlier version of this server stored as `Ultimate`. The pairs are listed in
`ProductCodes`.

### Response

The server answers a granted lease with `200 OK` and a body of four named parts:

```
License: 900001-ZEE78XQQ…ZAUPA; StartTime: 2026-09-22T07:07:08.3615328Z; EndTime: 2026-09-25T07:07:08.3615328Z; RenewTime: 2026-09-24T07:07:08.3615328Z
```

| Part | Meaning |
|---|---|
| `License` | The license key the client uses until the lease ends. |
| `StartTime` | The instant the lease began. |
| `EndTime` | The instant after which the license key may no longer be used. |
| `RenewTime` | The instant from which the client asks for a new lease. It precedes `EndTime`. |

The client parses the body leniently, and the server relies on this:

- The parts are separated by `;`. A license key is an identifier, a hyphen and Base32 characters, so
  it never contains a semicolon.
- Each part is split at its first `:`, so the colons inside an instant are preserved.
- The name of a part is matched without regard to case.
- A part that the client does not know is ignored. A later version of the server can therefore add a
  part, but it cannot rename one.
- Only `License` is mandatory. A client that receives no `StartTime` uses the current instant. An
  absent `EndTime` gives a lease of one day, and an absent `RenewTime` gives `EndTime`.
- A client reads the instants as UTC and keeps them as UTC.

A client reads at most 64 KiB of the body. A lease is a few hundred bytes. The limit prevents a broken
or hostile server from sending an unbounded response.

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

A client displays the body of a 403 to the user, because that text is the explanation the
administrator of the server needs. It reports any other status without its body.

### Denials

The body of a 403 begins with `No license with free capacity. `, followed by the reason each license
was not used, separated by spaces. A server that holds no license answers with the prefix alone.

The server does not use a license for the following reasons:

| Reason | Message |
|---|---|
| The key does not parse or its signature does not verify. | `The license key #N is invalid.` |
| The key requires a newer licensing library than the server carries. | `The license #N requires a higher version of the licensing library on the License Server. Please upgrade the License Server to >= X.Y.Z` |
| The key requires a newer client than the one that asks. | `The license #N of type T requires PostSharp version >= X.Y.Z but the requested version is A.B.C.` |
| The key may not be served by a license server. | `The license #N, of type T, cannot be used in the license server.` |
| The client was built after the end of the maintenance subscription. | `The maintenance subscription of license #N ends on D but the requested version X.Y.Z has been built on E.` |

The last message omits the version when the client did not send one, because such a client is older
than the argument.

A request that passes every check can still be denied for capacity. The server then reports no reason
for any license, so the body is the prefix alone.

### What decides a grant

The server tries the licenses it holds in the order of their `Priority` column, lowest first, and
skips a license whose priority is negative. For each license, in order:

1. A lease that this user already holds on this machine is returned unchanged, when it ends more than
   `MinLeaseDays` from now.
2. Such a lease is prolonged when it ends sooner. Prolonging replaces the lease with a new lease that
   overwrites it, so the audit log keeps both.
3. A new lease is granted when the license has a free seat.
4. A new lease is granted beyond the capacity of the license when the license is within its grace
   period.

A seat is one user working on up to `LicenseServer:MachinesPerUser` machines. A user working on more
machines takes more than one seat: the number of machines divided by that setting, rounded up. With
the default value of two, one or two machines are one seat, and three or four machines are two seats.
The capacity of a license key is a number of seats, and the seat is the only unit the server counts.

`EndTime` is `NewLeaseDays` from now, limited to the expiry of the license key. `RenewTime` is
`MinLeaseDays` before `EndTime`. The server refuses to start unless `MinLeaseDays` is smaller than
`NewLeaseDays`, because a renew time in the past makes every client renew at every build.

A machine named in `LicenseServer:BuildServers` receives a license key, and the server stores no lease
for it, so that build agents do not consume the seats of the developers they build for. A build agent
is exempt from consuming a seat and from nothing else. The same validation runs, and the server
refuses an expired or ineligible license as it does for any other request.

### Renewal

A client stores the lease it received and contacts the server again only when it needs to. This is
the behaviour of the client, and the server does not enforce it. It keeps the load of the server
proportional to the number of developers instead of the number of builds.

- The client uses the stored lease while the current instant precedes `RenewTime`.
- The client downloads a new lease after `RenewTime`. A renewal on a machine that the user already
  holds prolongs a seat instead of allocating one.
- A renewal that fails while the stored lease is still valid keeps the stored lease and reports
  nothing. The build has a license, and a short interruption of the server must not fail it.
- The client discards a lease whose `EndTime` has passed and downloads a new one.
- The client also discards a lease whose `EndTime` is already in the past when the lease arrives.
  Without this rule, a server whose clock is behind the clock of the client would make every process
  download a lease, find it expired, and download another one, without end.

With the default values, a lease of three days renewed after two, one machine contacts the server
about once every two days, whatever the number of builds in between.

## Reading the server clock: `GET /GetTime.ashx`

The server answers with its own current instant and with the factor by which its clock runs faster
than real time, separated by `;`:

```
2026-09-17T11:00:58.5236731Z;1440
```

The factor is `1` on a production server, where the clock is the real one. Another value comes from
`LicenseServer:TimeAcceleration`, and the server writes a warning to the log at startup when the
setting is used.

A test harness anchors its own virtual clock on this reading and advances it at the same rate:

```
virtualNow = serverTimeAtSync + (realNow - realTimeAtSync) * acceleration
```

The reading does not compensate the round trip. At a factor of 1440, a round trip of 50 ms is
72 seconds of virtual time. A harness therefore reads the clock again when it observes drift. For a
licensing harness, drift becomes visible when a lease arrives whose renewal instant has already
passed.

The virtual clock of the server is anchored when the process starts, and it never resets. Restarting
a server therefore moves its clock backwards, and the leases granted before the restart are then
dated in the future. Delete the database as well as restarting the process between two simulations.

## Exporting the audit log: `GET /Admin/Export.ashx`

The export is not part of the client protocol. An administrator gives the exported file to an
auditor. Its format is a contract of its own, because customers archive these files and compare them
across years.

### Request

| Argument | Meaning |
|---|---|
| `fy`, `fm` | The year and the month the range starts at. |
| `ty`, `tm` | The year and the month the range ends at, inclusive. |

Years are between 2010 and 2100, and months between 1 and 12. The server answers any other value with
`400` and the body `The range of months is missing or invalid. Years must be between 2010 and 2100.`

The response carries `Content-Disposition: attachment` with a file name derived from the range, for
instance `PostSharp_LicenseLog_2026-1_2026-12.txt`. The server writes the response while it reads the
rows.

### Format

One line per lease, with eight fields separated by `;`:

```
40;;900001;2026-09-17T11:02:14.1234567Z;2026-09-20T11:02:14.1234567Z;d97556dbab6becaa;93cf1e71b44530bd;J+QFuzRr4fT+mkwzkYOQ/NZtkVy2EHWj1b9t82h/xB8=
```

| Field | Meaning |
|---|---|
| 1 | The identifier of the lease. |
| 2 | The identifier of the lease that this one overwrote, empty when it overwrote none. |
| 3 | The identifier of the license. |
| 4 | The instant the lease began, in UTC. |
| 5 | The instant the lease ended, in UTC. |
| 6 | The hash of the machine name. |
| 7 | The hash of the user name. |
| 8 | The signature. |

Names appear only as hashes, so the file can be shared without disclosing who works where. The hash
is the hexadecimal representation, in lower case, of an unkeyed 64-bit hash of the name. The name is
trimmed and converted to lower case first. The algorithm is MD5, truncated to its first eight bytes
and read as a little-endian signed integer. MD5 is not used for its cryptographic properties, which
are irrelevant to an anonymizing hash. It is used because the values must be equal to the values
PostSharp has produced since 2013, and because the license audit of Backstage hashes the same names
in the same way.

### The signature chain

Each signature covers the signature of the previous lease, a semicolon, and the seven fields of its
own line:

```
signature(n) = base64( HMAC-SHA256( key, signature(n-1) + ";" + fields 1 to 7 of line n ) )
```

The first lease of a database is chained to an empty string. The key is described under Auditing in
[configuration.md](configuration.md).

An auditor can recompute the chain from an exported file alone, because the signed payload is the
exported line. Earlier versions did not allow this. They called the parameterless `HMAC.Create()`,
which generates a random key at every call, so the signatures they wrote could never be verified.

The server resolves a range of months to a range of lease identifiers, and exports every lease in
that range. The result is a contiguous section of the log and not a filtered selection, so the chain
remains verifiable. This is also the reason why a few leases outside the requested months appear in
the file.

## Compatibility

The server keeps the following behaviours because clients depend on them. A change to any of them
breaks a client that is already deployed.

| Behaviour | Why |
|---|---|
| The `.ashx` paths. | Deployed clients contain them. |
| An absent `version` means 4.9.9. | This is how a client older than PostSharp 5 is recognized. |
| An absent `product` means any product. | No PostSharp client ever sent the argument. |
| The four part names of a lease. | A client matches the parts by name and ignores a part it does not know. |
| The status codes, and the body of a 403. | A client displays the body of a 403 to the user and treats every other status than 200 as a failure. |
| The trailing hexadecimal suffix of a machine name is removed before a build server is matched. | The list of build servers in an existing configuration names the machines without it. |
| The order of the fields of an audit line, and the hash of the names. | Customers archive exported files and compare them across years. |
