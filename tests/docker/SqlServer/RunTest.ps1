# Runs the test suite against SQL Server, in a container that carries the server.
#
# The suite runs on SQLite in the ordinary build, which needs no server. SQL Server is one of the two engines
# that customers deploy, and it differs from SQLite in the collation, in the column types and in the lock that
# serializes the lease requests, so the same tests are run against it in full.

param(
    # The platform identifier the launcher selected, for example 'linux-x64'.
    [Parameter( Mandatory = $true )] [string] $Platform
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The operating system of the container engine to use. On a Windows development machine, Linux containers run
# on the engine inside the Windows Subsystem for Linux, and DockerBuild.ps1 re-executes itself there.
$os = if ( $Platform -like 'linux-*' ) { 'linux' } else { 'windows' }

$arguments = @{
    Test = $true
    OS = $os
    Dockerfile = "$PSScriptRoot/Dockerfile"
    Command = 'bash tests/docker/SqlServer/run.sh'
}

& "$PSScriptRoot/../../../DockerBuild.ps1" @arguments

exit $LASTEXITCODE
