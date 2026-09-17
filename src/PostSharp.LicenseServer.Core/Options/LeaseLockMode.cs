namespace PostSharp.LicenseServer.Options;

/// <summary>
/// Determines how the license server serializes concurrent lease requests.
/// </summary>
public enum LeaseLockMode
{
    /// <summary>
    /// A semaphore shared by all requests served by the current process. This is the default and is
    /// correct when a single process serves the database, which is the supported deployment.
    /// </summary>
    InProcess,

    /// <summary>
    /// A SQL Server application lock, shared by every process connected to the same database.
    /// Required when the license server runs in a web garden, behind a load balancer, or in several
    /// containers.
    /// </summary>
    SqlApplicationLock,

    /// <summary>
    /// No locking at all. Intended for tests.
    /// </summary>
    None
}
