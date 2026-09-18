// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using BuildBackstageLicenseServer.Docker;
using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Build.Solutions;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.TeamCity;
using PostSharp.Engineering.BuildTools.Docker;
using BackstageDependencies = PostSharp.Engineering.BuildTools.Dependencies.Definitions.BackstageDependencies.V2027_0;

var dotNetSdkVersion = BackstageDependencies.Family.PreferredVersions.DotNetSdk.V_10_0;

var product = new Product( BackstageDependencies.BackstageLicenseServer )
{
    // The product consumes SharpCrafters.Backstage, whose packages carry a prefix that does not match the
    // default source, so the generated nuget.config has to carry the source mapping of the dependency.
    GenerateNuGetConfig = true,

    DotNetSdkVersion = new DotNetSdkVersion( dotNetSdkVersion ),

    // The build runs in a container, as the builds of the other products of the family do. The image carries the
    // .NET SDK and nothing else: the product is a set of SDK-style projects, and its dependencies come from
    // nuget.org and from the artifacts of the build it depends on.
    OverriddenBuildAgentRequirements = new ContainerRequirements( ContainerHostKind.Windows )
    {
        Components = [new DotNetComponent( dotNetSdkVersion, DotNetComponentKind.Sdk )]
    },

    // The image of each database test run. Both are Linux images: Microsoft publishes no SQL Server for a Windows
    // container after the 2019 version, and PostgreSQL is deployed on Linux. Each image carries its server rather
    // than starting a second container, which a build step running inside a container cannot do without the
    // Docker socket of the agent.
    AdditionalDockerfiles =
    [
        DatabaseTests.SqlServer.Dockerfile( dotNetSdkVersion ),
        DatabaseTests.PostgreSql.Dockerfile( dotNetSdkVersion )
    ],

    // The test runs that need a database server. The build itself runs the suite on SQLite, which needs no
    // server; these configurations run the same tests against the two engines that customers deploy, and they
    // are what proves the lock, the collation and the column types of each one. See eng/TestDatabase.ps1 and the
    // section "Running the tests" of README.md.
    AdditionalCiBuildConfigurations = [DatabaseTests.SqlServer.Configuration, DatabaseTests.PostgreSql.Configuration],

    // Built rather than packed: the product ships a deployable archive and no NuGet package, so the Pack
    // target of every project would be a no-op.
    Solutions =
    [
        new DotNetSolution( "SharpCrafters.Backstage.LicenseServer.slnx" )
        {
            BuildMethod = BuildMethod.Build, CanFormatCode = true
        }
    ],

    // The deliverable is the archive that an administrator unpacks into an IIS application or runs with
    // `dotnet SharpCrafters.Backstage.LicenseServer.dll`. The web project builds it; see the PackAndZip
    // target of SharpCrafters.Backstage.LicenseServer.Web.csproj.
    //
    // Note that none of the default publishers matches it: they publish NuGet packages and Visual Studio
    // extensions. A public build therefore produces the archive and uploads nothing, until the upload to
    // S3 is added here.
    PublicArtifacts = Pattern.Create( "SharpCrafters.Backstage.LicenseServer.$(PackageVersion).zip" ),

    // The archive carries the whole dependency tree of an ASP.NET Core application, and the sign service
    // descends into a container and signs every executable it finds. Without this filter it would sign
    // MailKit, the Azure libraries and the rest with our certificate, asserting authorship of code that
    // is not ours.
    SigningFilter = ["**/SharpCrafters.Backstage.LicenseServer*.dll"]
};

return new EngineeringApp( product ).Run( args );

/// <summary>
/// The continuous integration configurations that run the test suite against a database server, each one in the
/// image that carries its server.
/// </summary>
internal static class DatabaseTests
{
    public static DatabaseTestRun SqlServer { get; } = new( "sqlserver", "SqlServer", "SQL Server", new SqlServerComponent() );

    public static DatabaseTestRun PostgreSql { get; } = new( "postgresql", "PostgreSql", "PostgreSQL", new PostgreSqlComponent() );
}

/// <summary>
/// One such configuration, with the image it runs in.
/// </summary>
/// <param name="name">
/// The name of the image. PostSharp.Engineering writes the Dockerfile of its build layer to
/// <c>eng/docker/{name}-build.Dockerfile</c>.
/// </param>
/// <param name="engine">The name of the engine, as the parameter of eng/TestDatabase.ps1 spells it.</param>
/// <param name="displayName">The name of the engine, as a person writes it.</param>
/// <param name="server">The component that installs the server into the image.</param>
internal sealed class DatabaseTestRun( string name, string engine, string displayName, ContainerComponent server )
{
    public AdditionalDockerfile Dockerfile( string dotNetSdkVersion )
        => new( name, [] )
        {
            Requirements = new ContainerRequirements( ContainerHostKind.Linux )
            {
                OperatingSystem = ContainerOperatingSystem.Linux,
                Components = [new DotNetComponent( dotNetSdkVersion, DotNetComponentKind.Sdk ), server]
            }
        };

    public PowershellAdditionalCiBuildConfiguration Configuration
        => new( $"{engine}Tests", $"Tests on {displayName}", "./eng/TestDatabase.ps1", $"-Engine {engine}" )
        {
            BuildAgentRequirements = LinuxContainerHost,
            Dockerfile = $"eng/docker/{name}-build.Dockerfile",
            BuildSnapshotDependency = BuildConfiguration.Debug
        };

    /// <summary>
    /// Gets the agent this configuration runs on, which is a Linux host of an amd64 container.
    /// </summary>
    /// <remarks>
    /// The requirements that PostSharp.Engineering derives for a Linux container host ask for an
    /// <c>env.BuildAgentType</c> that no agent of this farm publishes, so nothing would be compatible and the
    /// build would wait instead of failing. The operating system and the architecture are what the agents offer.
    /// The architecture is named because SQL Server runs on amd64 only.
    /// </remarks>
    private static ContainerHostRequirements LinuxContainerHost
        => new ContainerHostRequirements( ContainerHostKind.Linux ) with
        {
            Items =
            [
                new BuildAgentRequirement( "teamcity.agent.jvm.os.name", "Linux" ),
                new BuildAgentRequirement( "teamcity.agent.jvm.os.arch", "amd64" )
            ]
        };
}
