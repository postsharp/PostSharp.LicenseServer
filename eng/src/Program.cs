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
