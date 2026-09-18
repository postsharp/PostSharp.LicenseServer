// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Docker;
using System;
using System.IO;

namespace BuildBackstageLicenseServer.Docker;

/// <summary>
/// Installs PostgreSQL into a Linux image, so that the image can run the test suite against the second
/// engine supported in production. <c>eng/TestDatabase.ps1</c> creates the cluster and starts it.
/// </summary>
/// <remarks>
/// The packages come from the repository of the PostgreSQL project and not from Ubuntu, whose
/// repository carries the version that shipped with the distribution. The tests therefore run against
/// a version a customer can deploy today.
/// </remarks>
internal sealed class PostgreSqlComponent : ContainerComponent
{
    /// <summary>
    /// The major version. It is part of the path of the executables, which
    /// <c>eng/TestDatabase.ps1</c> reads from <c>PGBINDIR</c>.
    /// </summary>
    public const string Version = "17";

    public override string Name => $"Install PostgreSQL {Version}";

    public override string Key => $"{nameof(PostgreSqlComponent)}:{Version}";

    /// <summary>
    /// Gets the kind of this component. The enumeration belongs to PostSharp.Engineering and cannot be
    /// extended from here, so this component takes the value of the nearest standard component, which
    /// is another external tool installed into the image, and places itself with <see cref="SortOrder"/>.
    /// </summary>
    public override ContainerComponentKind Kind => ContainerComponentKind.AzureCli;

    /// <summary>
    /// Gets the position of this component, which is just before the epilogue. The standard components
    /// therefore keep their place, and a change to this one invalidates no layer of theirs.
    /// </summary>
    public override int SortOrder => ((int) ContainerComponentKind.Epilogue * 100) - 50;

    public override void WriteDockerfile( TextWriter writer, ContainerOperatingSystem operatingSystem )
    {
        if ( operatingSystem != ContainerOperatingSystem.Linux )
        {
            throw new InvalidOperationException( "PostgreSQL is installed into a Linux image only." );
        }

        // jammy is Ubuntu 22.04, the base image of a Linux chain. The server is installed but no
        // cluster is started here: a container image runs no service, and the test script creates a
        // cluster of its own under /tmp.
        writer.WriteLine(
            $$"""
              RUN curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc -o /etc/apt/trusted.gpg.d/postgresql.asc \
                  && echo "deb http://apt.postgresql.org/pub/repos/apt jammy-pgdg main" > /etc/apt/sources.list.d/pgdg.list \
                  && apt-get update \
                  && apt-get install -y postgresql-{{Version}} \
                  && rm -rf /var/lib/apt/lists/*

              ENV PGBINDIR=/usr/lib/postgresql/{{Version}}/bin
              ENV PATH="${PGBINDIR}:${PATH}"
              """ );
    }
}
