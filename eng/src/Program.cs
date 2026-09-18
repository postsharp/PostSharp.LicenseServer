// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

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

    // The test runs that need a database server. The build itself runs the suite on SQLite, which needs no
    // server; these runs exercise the same tests against the two engines that customers deploy, each in a
    // container of its own. See Tests/Docker and the section "Running the tests" of README.md.
    //
    // One configuration runs every test of a platform, so there is one per platform and not one per engine.
    // SQL Server runs on amd64 alone, which is why linux-x64 is the only platform declared here.
    AdditionalCiBuildConfigurations = DockerTestsAdditionalCiBuildConfiguration.WithCompositeConfiguration(
        new DockerTestsAdditionalCiBuildConfiguration(
            "DockerTestsLinuxX64",
            "Docker Tests (Linux x64)",
            DockerTestPlatform.LinuxX64,

            // The directories of this repository are named in lower case, so the tests are under tests/docker
            // and not under the default Tests/Docker. The name is compared as it is written on a Linux agent.
            "tests/docker" )
        {
            // The tests build the product from the sources of the repository, and they need the packages of the
            // licensing component, which the artifacts of the debug build carry.
            BuildSnapshotDependency = BuildConfiguration.Debug,

            // Each test acquires an image of several hundred megabytes on an agent that meets it for the first
            // time, installs a database server, and then builds and runs the suite.
            TimeoutInMinutes = 60
        } ),

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
