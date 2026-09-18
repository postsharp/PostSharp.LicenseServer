@{
    # SQL Server runs on amd64 alone: Microsoft publishes no arm64 image of it.
    Platforms = @( 'linux-x64' )

    # The image is about one and a half gigabytes, and an agent that meets it for the first time pulls it
    # before the test does anything. The test itself then restores, builds and runs the suite. The neighbouring
    # PostgreSQL test needs less, because its server is a fraction of that size.
    TimeoutSeconds = 2400
}
