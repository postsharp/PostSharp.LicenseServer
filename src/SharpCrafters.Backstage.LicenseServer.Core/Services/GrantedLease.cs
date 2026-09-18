// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCrafters.Backstage.LicenseServer.Data;
using SharpCrafters.Backstage.LicenseServer.Email;
using SharpCrafters.Backstage.LicenseServer.Licensing;
using SharpCrafters.Backstage.LicenseServer.Options;

namespace SharpCrafters.Backstage.LicenseServer.Services;

/// <summary>
/// The lease granted to a client: which license key to use, and for how long.
/// </summary>
public sealed record GrantedLease( string LicenseKey, DateTime StartTime, DateTime EndTime, DateTime RenewTime );