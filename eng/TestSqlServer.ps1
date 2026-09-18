#Requires -Version 7

<#
.SYNOPSIS
    Runs the test suite against SQL Server.

.DESCRIPTION
    The suite runs on SQLite by default, which needs no server. This script runs the same tests a
    second time against SQL Server, the engine that customers use. The two engines differ in the
    collation, in the column types and in the lock that serializes the lease requests.

    The script takes the first SQL Server it finds:

    1. The connection string in LICENSESERVER_TEST_SQLSERVER, when it is already set.
    2. A SQL Server installed in the image the script runs in, which the continuous integration
       build uses. The script starts it and stops it.
    3. The database service of docker-compose.yml, which is the shortest path on a developer
       machine. The script starts it and leaves it running.

    The connection string names no database. Each test receives a database of its own, created from
    Database\CreateTables.sql.

.PARAMETER Password
    The password of the sa login. It is read from MSSQL_SA_PASSWORD when the parameter is omitted,
    and a password is generated when neither is given.

.PARAMETER TimeoutInSeconds
    How long to wait for the server to accept a query.

.PARAMETER BuildArguments
    The arguments that are passed on to Build.ps1 test.
#>

[CmdletBinding()]
param(
    [string] $Password = $env:MSSQL_SA_PASSWORD,
    [int] $TimeoutInSeconds = 180,

    # Passed on to Build.ps1 test, for instance --configuration Public.
    [Parameter( ValueFromRemainingArguments = $true )]
    [string[]] $BuildArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryDirectory = Split-Path -Parent $PSScriptRoot
$sqlServerExecutable = '/opt/mssql/bin/sqlservr'
$sqlCmd = '/opt/mssql-tools18/bin/sqlcmd'

function Wait-ForServer( [scriptblock] $Query, [int] $Timeout ) {
    $deadline = (Get-Date).AddSeconds( $Timeout )

    while ( (Get-Date) -lt $deadline ) {
        & $Query 2>&1 | Out-Null

        if ( $LASTEXITCODE -eq 0 ) {
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "SQL Server did not accept a query within $Timeout seconds."
}

$serverProcess = $null

try {
    if ( $env:LICENSESERVER_TEST_SQLSERVER ) {
        Write-Host 'Using the SQL Server named by LICENSESERVER_TEST_SQLSERVER.'
    }
    else {
        if ( -not $Password ) {
            # The server is reachable from this machine alone, and it is deleted with the container,
            # but it still refuses a password that does not meet its complexity rules.
            $Password = 'Lease-' + [System.Guid]::NewGuid().ToString( 'N' ).Substring( 0, 12 ) + '-1'
        }

        if ( Test-Path $sqlServerExecutable ) {
            Write-Host 'Starting the SQL Server of this image.'

            $env:ACCEPT_EULA = 'Y'
            $env:MSSQL_SA_PASSWORD = $Password
            $env:MSSQL_PID = 'Developer'

            # The server refuses to run as root, and the image runs the build as root, so the server
            # runs under the account its own package creates.
            $serverProcess = Start-Process -FilePath 'runuser' `
                -ArgumentList '-u', 'mssql', '--', $sqlServerExecutable `
                -PassThru -NoNewWindow

            Wait-ForServer { & $sqlCmd -S 127.0.0.1 -U sa -P $Password -C -b -Q 'SELECT 1' } $TimeoutInSeconds
        }
        else {
            Write-Host 'Starting the database service of docker-compose.yml.'

            $env:MSSQL_SA_PASSWORD = $Password

            & docker compose --file "$repositoryDirectory/docker-compose.yml" up --detach database

            if ( $LASTEXITCODE -ne 0 ) {
                throw 'The database service did not start.'
            }

            Wait-ForServer {
                & docker compose --file "$repositoryDirectory/docker-compose.yml" exec -T database `
                    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $Password -C -b -Q 'SELECT 1'
            } $TimeoutInSeconds
        }

        # The address is written as 127.0.0.1 and not as localhost. On a host that resolves localhost
        # to an address of version 6 first, the client reaches nothing, because the port is published
        # on version 4.
        $env:LICENSESERVER_TEST_SQLSERVER =
            "Server=127.0.0.1,1433;User Id=sa;Password=$Password;TrustServerCertificate=True;Encrypt=False"
    }

    & "$repositoryDirectory/Build.ps1" test @BuildArguments

    if ( $LASTEXITCODE -ne 0 ) {
        throw "The tests failed with the exit code $LASTEXITCODE."
    }
}
finally {
    if ( $serverProcess ) {
        Write-Host 'Stopping SQL Server.'
        Stop-Process -InputObject $serverProcess -ErrorAction SilentlyContinue
    }
}
