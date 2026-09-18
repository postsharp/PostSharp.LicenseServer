// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Docker;
using System;
using System.IO;

namespace BuildBackstageLicenseServer.Docker;

/// <summary>
/// Installs SQL Server 2022 and its command-line tools, so that the image can run the test suite against the
/// engine that customers use. <c>eng/TestSqlServer.ps1</c> starts the server and stops it.
/// </summary>
/// <remarks>
/// The server is installed in the image rather than started as a second container. A build step runs inside a
/// container already, and reaching a sibling container from there would require the Docker socket of the agent.
/// </remarks>
internal sealed class SqlServerComponent : ContainerComponent
{
    public override string Name => "Install SQL Server 2022";

    /// <summary>
    /// Gets the kind of this component. The enumeration belongs to PostSharp.Engineering and cannot be extended
    /// from here, so this component takes the value of the nearest standard component, which is another external
    /// tool installed into the image, and places itself with <see cref="SortOrder"/>.
    /// </summary>
    public override ContainerComponentKind Kind => ContainerComponentKind.AzureCli;

    /// <summary>
    /// Gets the position of this component, which is just before the epilogue. The standard components therefore
    /// keep their place, and a change to this one invalidates no layer of theirs.
    /// </summary>
    public override int SortOrder => ((int) ContainerComponentKind.Epilogue * 100) - 50;

    public override void WriteDockerfile( TextWriter writer, ContainerOperatingSystem operatingSystem )
    {
        if ( operatingSystem != ContainerOperatingSystem.Linux )
        {
            throw new InvalidOperationException(
                "SQL Server is installed into a Linux image only. Microsoft publishes no SQL Server for a Windows container after the 2019 version." );
        }

        // The package repository of Microsoft for Ubuntu 22.04, which is the base image of a Linux chain. The
        // tools accept the license through ACCEPT_EULA, and the server accepts it at the first start, which is
        // where TestSqlServer.ps1 passes it.
        writer.WriteLine(
            """
            RUN curl -fsSL https://packages.microsoft.com/keys/microsoft.asc -o /etc/apt/trusted.gpg.d/microsoft.asc \
                && curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/mssql-server-2022.list -o /etc/apt/sources.list.d/mssql-server-2022.list \
                && curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/prod.list -o /etc/apt/sources.list.d/microsoft-prod.list \
                && apt-get update \
                && apt-get install -y mssql-server \
                && ACCEPT_EULA=Y apt-get install -y mssql-tools18 \
                && rm -rf /var/lib/apt/lists/*

            ENV PATH="/opt/mssql-tools18/bin:${PATH}"
            """ );
    }
}
