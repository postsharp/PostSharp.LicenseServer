// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using SharpCrafters.Backstage.LicenseServer.Data;

namespace SharpCrafters.Backstage.LicenseServer.Tests.Infrastructure;

/// <summary>
/// A database that belongs to one test, on the engine the test run was started with.
/// </summary>
public interface ITestDatabase : IAsyncDisposable
{
    /// <summary>
    /// Gets the name of the provider, as <c>LicenseServer:DatabaseProvider</c> spells it.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the connection string of this database, which the tests that host the whole application
    /// pass to it as configuration.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Creates a context over this database. Each context is a separate unit of work with its own
    /// change tracker, so a test can distinguish a lease that is pending from a lease that is saved.
    /// </summary>
    LicenseServerDbContext CreateContext();

    /// <summary>
    /// States that this database must not serve another test, which a test that modifies the schema
    /// calls. A SQL Server run lends its databases to one test after another, and a test that drops
    /// a table would leave the next test without one.
    /// </summary>
    void DoNotReuse();
}