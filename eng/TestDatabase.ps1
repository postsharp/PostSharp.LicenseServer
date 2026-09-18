#Requires -Version 7

<#
.SYNOPSIS
    Runs the test suite against SQL Server or PostgreSQL.

.DESCRIPTION
    The suite runs on SQLite by default, which needs no server. This script runs the same tests again
    against an engine that customers deploy. The engines differ in the collation, in the column types
    and in the lock that serializes the lease requests, so each one is run in full.

    The script takes the first server it finds:

    1. The connection string in LICENSESERVER_TEST_SQLSERVER or LICENSESERVER_TEST_POSTGRESQL, when it
       is already set.
    2. A server installed in the image the script runs in, which the continuous integration build
       uses. The script starts it and stops it.
    3. A container it starts itself, which is the shortest path on a developer machine. The container
       is left running.

    The connection string names no database. Each test receives a database of its own, created from
    Database\CreateTables.sql or from Database\CreateTables.PostgreSql.sql.

.PARAMETER Engine
    SqlServer or PostgreSql.

.PARAMETER Password
    The password of the administrative login, which applies to SQL Server and to the PostgreSQL
    container. It is read from MSSQL_SA_PASSWORD or POSTGRES_PASSWORD when the parameter is omitted,
    and a password is generated when neither is given.

.PARAMETER StartOnly
    Starts the server, prints the connection string, and runs no test. Use it to keep a server for
    the runs an editor starts.

.PARAMETER TimeoutInSeconds
    How long to wait for the server to accept a query.

.PARAMETER BuildArguments
    The arguments that are passed on to Build.ps1 test.
#>

[CmdletBinding()]
param(
    [Parameter( Mandatory = $true )]
    [ValidateSet( 'SqlServer', 'PostgreSql' )]
    [string] $Engine,

    [string] $Password,

    [switch] $StartOnly,

    [int] $TimeoutInSeconds = 180,

    [Parameter( ValueFromRemainingArguments = $true )]
    [string[]] $BuildArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryDirectory = Split-Path -Parent $PSScriptRoot

# The address is written as 127.0.0.1 and not as localhost. On a host that resolves localhost to an
# address of version 6 first, the client reaches nothing, because the port of a container is published
# on version 4.
$address = '127.0.0.1'

$sqlServerExecutable = '/opt/mssql/bin/sqlservr'
$sqlCmd = '/opt/mssql-tools18/bin/sqlcmd'
$sqlServerContainer = 'licenseserver-test-sqlserver'
$postgreSqlContainer = 'licenseserver-test-postgres'
$postgreSqlData = '/tmp/licenseserver-postgres'

# A container of this script publishes its port beside the default one, so that a license server
# deployed on this machine with docker-compose.yml keeps the default port for itself. A server
# installed in an image listens on the default port, because it is alone in its container.
$sqlServerContainerPort = 14330
$postgreSqlContainerPort = 55432

# How Docker is reached, which Initialize-Docker resolves at the first call.
$script:dockerCommand = $null

<#
.SYNOPSIS
    Resolves how Docker is reached, and stores the command and the arguments that precede every Docker
    argument.
.DESCRIPTION
    The servers run in Linux containers, so the daemon has to be one that runs Linux containers. A
    Windows machine can answer on the client and still refuse them: Docker Desktop in the Windows
    container mode reports a server, and pulling a Linux image then fails with "no matching manifest
    for windows". The engine can also live inside a distribution of the Windows Subsystem for Linux
    rather than under Docker Desktop, and the client of the distribution is then the only one that
    reaches it. The daemon is therefore chosen by the operating system it reports, and not by whether
    a client answers.

    Docker is called with a relative path when it is called through wsl, because wsl starts in the
    translated form of the current directory and would not understand a path that names a Windows
    drive.
#>
function Initialize-Docker {
    if ( $script:dockerCommand ) {
        return
    }

    function Test-LinuxDaemon( [string[]] $Command ) {
        $name = $Command[0]
        $arguments = @( $Command | Select-Object -Skip 1 ) + @( 'version', '--format', '{{.Server.Os}}' )

        $operatingSystem = & $name @arguments 2>&1

        return $LASTEXITCODE -eq 0 -and "$operatingSystem".Trim() -eq 'linux'
    }

    if ( (Get-Command docker -ErrorAction SilentlyContinue) -and (Test-LinuxDaemon @( 'docker' )) ) {
        $script:dockerCommand = @( 'docker' )

        return
    }

    if ( (Get-Command wsl -ErrorAction SilentlyContinue) -and (Test-LinuxDaemon @( 'wsl', '-e', 'docker' )) ) {
        Write-Host 'Reaching Docker through the Windows Subsystem for Linux.'
        $script:dockerCommand = @( 'wsl', '-e', 'docker' )

        return
    }

    throw 'No Docker daemon that runs Linux containers was reached. Start one, or set the connection string of a server of your own.'
}

function Invoke-Docker {
    Initialize-Docker

    $command = $script:dockerCommand[0]
    $arguments = @( $script:dockerCommand | Select-Object -Skip 1 ) + $args

    & $command @arguments
}

<#
.SYNOPSIS
    Holds the distribution of the Windows Subsystem for Linux open, and returns the process that holds
    it.
.DESCRIPTION
    The subsystem stops a distribution a few seconds after its last process ends. The engine of Docker
    and the containers it runs stop with it, so a suite that runs for minutes loses its server in the
    middle: the connections are refused and every test that touches the database fails. A process that
    sleeps for the length of this script keeps the distribution running.
#>
function Start-DistributionKeepAlive {
    if ( $script:dockerCommand[0] -ne 'wsl' ) {
        return $null
    }

    return Start-Process -FilePath 'wsl' -ArgumentList '-e', 'sleep', '86400' -PassThru -WindowStyle Hidden
}

function Wait-ForServer( [scriptblock] $Query, [int] $Timeout ) {
    $deadline = (Get-Date).AddSeconds( $Timeout )

    while ( (Get-Date) -lt $deadline ) {
        & $Query 2>&1 | Out-Null

        if ( $LASTEXITCODE -eq 0 ) {
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "The server did not accept a query within $Timeout seconds."
}

function New-Password {
    # The server is reachable from this machine alone, and it is deleted with the container, but it
    # still refuses a password that does not meet its complexity rules.
    return 'Lease-' + [System.Guid]::NewGuid().ToString( 'N' ).Substring( 0, 12 ) + '-1'
}

$serverProcess = $null
$startedLocalCluster = $false
$keepAlive = $null

# Docker is called with a relative path, so the script works from the directory of the repository.
Push-Location $repositoryDirectory

try {
    if ( $Engine -eq 'SqlServer' ) {
        if ( -not $env:LICENSESERVER_TEST_SQLSERVER ) {
            if ( -not $Password ) {
                $Password = if ( $env:MSSQL_SA_PASSWORD ) { $env:MSSQL_SA_PASSWORD } else { New-Password }
            }

            # A server installed in the image listens on the default port; a container of this script
            # publishes another one.
            $port = 1433

            if ( Test-Path $sqlServerExecutable ) {
                Write-Host 'Starting the SQL Server of this image.'

                $env:ACCEPT_EULA = 'Y'
                $env:MSSQL_SA_PASSWORD = $Password
                $env:MSSQL_PID = 'Developer'

                # The server refuses to run as root, and the image runs the build as root, so the
                # server runs under the account its own package creates.
                $serverProcess = Start-Process -FilePath 'runuser' `
                    -ArgumentList '-u', 'mssql', '--', $sqlServerExecutable `
                    -PassThru -NoNewWindow

                Wait-ForServer { & $sqlCmd -S $address -U sa -P $Password -C -b -Q 'SELECT 1' } $TimeoutInSeconds
            }
            else {
                Write-Host 'Starting a SQL Server container.'

                # The container carries no volume and is replaced at every run. The database service
                # of docker-compose.yml is left alone: it does carry a volume, and SQL Server sets the
                # password of sa when it creates its files, so a second run with another password
                # would be refused by the server it started the first time.
                Invoke-Docker rm --force $sqlServerContainer 2>&1 | Out-Null

                Invoke-Docker run --detach --name $sqlServerContainer `
                    --env ACCEPT_EULA=Y --env MSSQL_SA_PASSWORD=$Password --env MSSQL_PID=Developer `
                    --publish "${sqlServerContainerPort}:1433" mcr.microsoft.com/mssql/server:2022-latest

                if ( $LASTEXITCODE -ne 0 ) {
                    throw 'The SQL Server container did not start.'
                }

                Wait-ForServer {
                    Invoke-Docker exec $sqlServerContainer `
                        /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $Password -C -b -Q 'SELECT 1'
                } $TimeoutInSeconds

                $port = $sqlServerContainerPort
            }

            $env:LICENSESERVER_TEST_SQLSERVER =
                "Server=$address,$port;User Id=sa;Password=$Password;TrustServerCertificate=True;Encrypt=False"
        }

        Write-Host "The connection string is in LICENSESERVER_TEST_SQLSERVER."
    }
    else {
        if ( -not $env:LICENSESERVER_TEST_POSTGRESQL ) {
            if ( $env:PGBINDIR -and (Test-Path "$env:PGBINDIR/initdb") ) {
                Write-Host 'Starting the PostgreSQL server of this image.'

                # The package installs the server but starts no cluster, so the script creates one of
                # its own. Both connection methods trust the client: the cluster lives in a container,
                # it listens on the loopback address alone, and it is deleted with the container.
                New-Item -ItemType Directory -Force -Path $postgreSqlData | Out-Null
                & chown postgres $postgreSqlData

                & runuser -u postgres -- "$env:PGBINDIR/initdb" --pgdata=$postgreSqlData `
                    --username=postgres --auth-local=trust --auth-host=trust

                if ( $LASTEXITCODE -ne 0 ) {
                    throw 'The PostgreSQL cluster could not be created.'
                }

                $startedLocalCluster = $true

                & runuser -u postgres -- "$env:PGBINDIR/pg_ctl" --pgdata=$postgreSqlData `
                    --log=/tmp/postgres.log --options="-c listen_addresses=$address" --wait start

                if ( $LASTEXITCODE -ne 0 ) {
                    throw 'The PostgreSQL cluster did not start.'
                }

                Wait-ForServer { & runuser -u postgres -- "$env:PGBINDIR/pg_isready" -h $address } $TimeoutInSeconds

                $env:LICENSESERVER_TEST_POSTGRESQL = "Host=$address;Port=5432;Username=postgres"
            }
            else {
                Write-Host 'Starting a PostgreSQL container.'

                if ( -not $Password ) {
                    $Password = if ( $env:POSTGRES_PASSWORD ) { $env:POSTGRES_PASSWORD } else { New-Password }
                }

                Invoke-Docker rm --force $postgreSqlContainer 2>&1 | Out-Null

                Invoke-Docker run --detach --name $postgreSqlContainer `
                    --env POSTGRES_PASSWORD=$Password `
                    --publish "${postgreSqlContainerPort}:5432" postgres:17

                if ( $LASTEXITCODE -ne 0 ) {
                    throw 'The PostgreSQL container did not start.'
                }

                Wait-ForServer { Invoke-Docker exec $postgreSqlContainer pg_isready -U postgres } $TimeoutInSeconds

                $env:LICENSESERVER_TEST_POSTGRESQL =
                    "Host=$address;Port=$postgreSqlContainerPort;Username=postgres;Password=$Password"
            }
        }

        Write-Host "The connection string is in LICENSESERVER_TEST_POSTGRESQL."
    }

    if ( $StartOnly ) {
        Write-Host 'The server is running, and no test was run.'

        if ( $script:dockerCommand -and $script:dockerCommand[0] -eq 'wsl' ) {
            Write-Warning (
                'Docker runs inside the Windows Subsystem for Linux, which stops a distribution a few seconds ' +
                'after its last process ends, and the container stops with it. Keep a process of the ' +
                'distribution running, for instance "wsl -e sleep 86400", for as long as you need the server.' )
        }

        return
    }

    $keepAlive = Start-DistributionKeepAlive

    Write-Host "Running the tests against $Engine."

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

    if ( $keepAlive ) {
        Stop-Process -InputObject $keepAlive -ErrorAction SilentlyContinue
    }

    if ( $startedLocalCluster -and -not $StartOnly ) {
        Write-Host 'Stopping PostgreSQL.'

        & runuser -u postgres -- "$env:PGBINDIR/pg_ctl" --pgdata=$postgreSqlData `
            --mode=immediate stop 2>&1 | Out-Null
    }

    Pop-Location
}
