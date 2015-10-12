# PostSharp.LicenseServer

This repository contains the source code and releases of PostSharp License Server.

The use of the license server is optional. Since all commercial licenses are floating ones,
the license server can help teams knowing how many licenses they actually use.

The license server is a classic ASP.NET application with an MS SQL back-end.

We at PostSharp consider that it is the customer's sole responsibility to respect the license agreement, and this is why we are providing the source code of the license server. Note that the use of licenses keys [is audited anyway](http://doc.postsharp.net/license-audit); if this is not an option for your organization, you can ask the PostSharp sales team for a license key with audit waiver.

## License

The license server itself is licensed under the *MIT License*. Note that PostSharp is a commercial product with a proprietary license.

## Download

You can download the latest release from https://github.com/postsharp/PostSharp.LicenseServer/releases/latest.

## Documentation

* [Installing the license server](http://doc.postsharp.net/license-server-admin).
* [Using the license server](http://doc.postsharp.net/license-server).

## Building from source

### Requirements

* Visual Studio 2013 with Web Tools.
* To build from the command line, download `nuget.exe` from http://dist.nuget.org/win-x86-commandline/latest/nuget.exe.

### Instructions

1. Open a Developer Command Prompt and go to repository directory.
2. Restore NuGet packages with the command: `nuget restore`.
3. Go to directory `src\PostSharp.LicenseServer`.
4. Execute `msbuild PostSharp.LicenseServer.csproj /t:Zip`.

## Support

Please use PostSharp support facility at https://www.postsharp.net/support.
