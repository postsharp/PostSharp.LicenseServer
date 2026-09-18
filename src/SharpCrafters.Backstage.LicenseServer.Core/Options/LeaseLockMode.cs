namespace SharpCrafters.Backstage.LicenseServer.Options;

/// <summary>
/// Determines how the license server serializes concurrent lease requests.
/// </summary>
public enum LeaseLockMode
{
    /// <summary>
    /// A semaphore shared by every request that the current process serves. This is the default
    /// value. It is correct when one process serves the database, which is the supported deployment.
    /// </summary>
    InProcess,

    /// <summary>
    /// A SQL Server application lock, shared by every process connected to the same database.
    /// Required when the license server runs in a web garden, behind a load balancer, or in several
    /// containers.
    /// </summary>
    SqlApplicationLock,

    /// <summary>
    /// No lock. This value exists for the tests.
    /// </summary>
    None
}
