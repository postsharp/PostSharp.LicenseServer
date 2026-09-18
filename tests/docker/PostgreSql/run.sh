#!/bin/bash
# Creates a PostgreSQL cluster in this container, starts it, and runs the test suite against it.
#
# The script runs inside the container, with the repository mounted and as the working directory. RunTest.ps1
# is what starts the container; this file holds the part that needs no PowerShell, because a test image is
# chosen for the tool chain under test and carries no PowerShell.

set -e

data=/tmp/licenseserver-postgres

mkdir -p "$data"
chown postgres "$data"

# Both connection methods trust the client: the cluster lives in this container, it listens on the loopback
# address alone, and it is deleted with the container.
runuser -u postgres -- "$PGBINDIR/initdb" --pgdata="$data" --username=postgres     --auth-local=trust --auth-host=trust > /tmp/initdb.log 2>&1

runuser -u postgres -- "$PGBINDIR/pg_ctl" --pgdata="$data" --log=/tmp/postgres.log     --options="-c listen_addresses=127.0.0.1" --wait start

if ! runuser -u postgres -- "$PGBINDIR/pg_isready" -h 127.0.0.1 > /dev/null 2>&1; then
    echo "PostgreSQL did not accept a connection. The log of the server follows."
    tail -50 /tmp/postgres.log
    exit 1
fi

# The suite reads this variable and gives every test a database of its own, created from
# Database/CreateTables.PostgreSql.sql. The connection string names no database.
export LICENSESERVER_TEST_POSTGRESQL="Host=127.0.0.1;Port=5432;Username=postgres"

# The restore and the build write outside the repository, and they read the packages of the licensing
# component from the source that nuget.config names.
# shellcheck source=../prepare-restore.sh
source tests/docker/prepare-restore.sh

dotnet restore tests/SharpCrafters.Backstage.LicenseServer.Tests $RESTORE_ONLY_ARGUMENTS $COMMON_ARGUMENTS

dotnet test tests/SharpCrafters.Backstage.LicenseServer.Tests --no-restore --nologo $COMMON_ARGUMENTS
