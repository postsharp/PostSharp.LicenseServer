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
internal sealed class DatabaseTestRun
{
    private readonly string name;
    private readonly string engine;
    private readonly string displayName;
    private readonly ContainerComponent server;

    public DatabaseTestRun( string name, string engine, string displayName, ContainerComponent server )
    {
        this.name = name;
        this.engine = engine;
        this.displayName = displayName;
        this.server = server;
    }

    public AdditionalDockerfile Dockerfile( string dotNetSdkVersion )
        => new( this.name, [] )
        {
            Requirements = new ContainerRequirements( ContainerHostKind.Linux )
            {
                OperatingSystem = ContainerOperatingSystem.Linux,
                Components = [new DotNetComponent( dotNetSdkVersion, DotNetComponentKind.Sdk ), this.server]
            }
        };

    public PowershellAdditionalCiBuildConfiguration Configuration
        => new( $"{this.engine}Tests", $"Tests on {this.displayName}", "./eng/TestDatabase.ps1", $"-Engine {this.engine}" )
        {
            BuildAgentRequirements = LinuxContainerHost,
            Dockerfile = $"eng/docker/{this.name}-build.Dockerfile",
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
