@{
    # PostgreSQL publishes packages for both architectures, so the test applies to both. The continuous
    # integration build declares the amd64 platform alone, because the neighbouring SQL Server test needs it.
    Platforms = @( 'linux-x64', 'linux-arm64' )

    # The server is a fraction of the size of SQL Server, so the image costs less to acquire. The rest of the
    # budget is the restore, the build and the suite.
    TimeoutSeconds = 1800
}
