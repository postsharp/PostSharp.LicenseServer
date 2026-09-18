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

    // The image of the SQL Server test run. It is a Linux image, because Microsoft publishes no SQL Server for a
    // Windows container after the 2019 version, and it carries the server itself rather than starting a second
    // container, which a build step running inside a container cannot do without the Docker socket of the agent.
    AdditionalDockerfiles =
    [
        new AdditionalDockerfile( SqlServerTests.DockerfileName, [] )
        {
            Requirements = new ContainerRequirements( ContainerHostKind.Linux )
            {
                OperatingSystem = ContainerOperatingSystem.Linux,
                Components = [new DotNetComponent( dotNetSdkVersion, DotNetComponentKind.Sdk ), new SqlServerComponent()]
            }
        }
    ],

    // The second test run. The build itself runs the suite on SQLite, which needs no server; this configuration
    // runs the same tests against SQL Server, and it is what proves the lock, the collation and the column types
    // of the engine that customers use. See eng/TestSqlServer.ps1 and docs in README.md.
    AdditionalCiBuildConfigurations = [SqlServerTests.Configuration],

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
/// The continuous integration configuration that runs the test suite against SQL Server, in the image that
/// carries the server.
/// </summary>
internal static class SqlServerTests
{
    /// <summary>
    /// The name of the additional image. PostSharp.Engineering writes the Dockerfile of its build layer to
    /// <c>eng/docker/{name}-build.Dockerfile</c>.
    /// </summary>
    public const string DockerfileName = "sqlserver";

    public static PowershellAdditionalCiBuildConfiguration Configuration { get; } =
        new( "SqlServerTests", "Tests on SQL Server", "./eng/TestSqlServer.ps1", "" )
        {
            BuildAgentRequirements = LinuxContainerHost,
            Dockerfile = $"eng/docker/{DockerfileName}-build.Dockerfile",
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
