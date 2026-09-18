#!/bin/bash
# Starts the SQL Server of this container and runs the test suite against it.
#
# The script runs inside the container, with the repository mounted and as the working directory. RunTest.ps1
# is what starts the container; this file holds the part that needs no PowerShell, because a test image is
# chosen for the tool chain under test and carries no PowerShell.

set -e

password="Lease-$(head -c 8 /dev/urandom | od -An -tx1 | tr -d ' \n')-1"

export ACCEPT_EULA=Y
export MSSQL_SA_PASSWORD="$password"
export MSSQL_PID=Developer

# The server refuses to run as root, and the container runs the command as root, so it runs under the account
# its own package creates.
runuser -u mssql -- /opt/mssql/bin/sqlservr > /tmp/sqlservr.log 2>&1 &

for _ in $(seq 1 90); do
    if /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U sa -P "$password" -C -b -Q "SELECT 1" > /dev/null 2>&1; then
        break
    fi

    sleep 2
done

if ! /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U sa -P "$password" -C -b -Q "SELECT 1" > /dev/null 2>&1; then
    echo "SQL Server did not accept a query. The log of the server follows."
    tail -50 /tmp/sqlservr.log
    exit 1
fi

# The suite reads this variable and gives every test a database of its own, created from
# Database/CreateTables.sql. The connection string names no database.
export LICENSESERVER_TEST_SQLSERVER="Server=127.0.0.1,1433;User Id=sa;Password=$password;TrustServerCertificate=True;Encrypt=False"

# The restore and the build write outside the repository, and they read the packages of the licensing
# component from the source that nuget.config names.
# shellcheck source=../prepare-restore.sh
source tests/docker/prepare-restore.sh

dotnet restore tests/SharpCrafters.Backstage.LicenseServer.Tests $RESTORE_ONLY_ARGUMENTS $COMMON_ARGUMENTS

dotnet test tests/SharpCrafters.Backstage.LicenseServer.Tests --no-restore --nologo $COMMON_ARGUMENTS
