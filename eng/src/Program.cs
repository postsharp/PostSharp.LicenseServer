// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Build.Solutions;
using BackstageDependencies = PostSharp.Engineering.BuildTools.Dependencies.Definitions.BackstageDependencies.V2027_0;

var product = new Product( BackstageDependencies.BackstageLicenseServer )
{
    // The product consumes SharpCrafters.Backstage, whose packages carry a prefix that does not match the
    // default source, so the generated nuget.config has to carry the source mapping of the dependency.
    GenerateNuGetConfig = true,

    DotNetSdkVersion = new DotNetSdkVersion( BackstageDependencies.Family.PreferredVersions.DotNetSdk.V_10_0 ),

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
    PublicArtifacts = Pattern.Create( "SharpCrafters.Backstage.LicenseServer.$(PackageVersion).zip" )
};

return new EngineeringApp( product ).Run( args );
