<#
.SYNOPSIS
    Builds the release package of the PostSharp License Server.

.DESCRIPTION
    Publishes the web application and zips it into artifacts/SharpCrafters.Backstage.LicenseServer.zip, which is
    the artifact attached to a GitHub release.

    The package is portable: it carries no platform-specific build and runs wherever the .NET 10
    runtime does. On Windows that means unpacking it into an IIS application, with the ASP.NET Core
    Hosting Bundle installed. Elsewhere it is run with `dotnet SharpCrafters.Backstage.LicenseServer.dll`.

    Runs on Windows PowerShell and on PowerShell 7 for Linux and macOS.

.PARAMETER Configuration
    The build configuration. Release by default.

.PARAMETER OutputPath
    Where to write the zip. artifacts/ by default.

.PARAMETER SkipTests
    Skips the test run. Intended for iterating on the packaging itself.
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $OutputPath,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if ( -not $OutputPath ) { $OutputPath = Join-Path $repositoryRoot 'artifacts' }

$project = Join-Path $repositoryRoot 'src' 'SharpCrafters.Backstage.LicenseServer.Web' 'SharpCrafters.Backstage.LicenseServer.Web.csproj'
$publishPath = Join-Path $repositoryRoot 'artifacts' 'publish'
$zipPath = Join-Path $OutputPath 'SharpCrafters.Backstage.LicenseServer.zip'

if ( Test-Path $publishPath ) { Remove-Item $publishPath -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

if ( -not $SkipTests ) {
    Write-Host 'Testing...'
    Push-Location $repositoryRoot
    try { dotnet test --configuration $Configuration --nologo }
    finally { Pop-Location }
    if ( $LASTEXITCODE -ne 0 ) { throw 'The tests failed.' }
}

Write-Host 'Publishing...'
dotnet publish $project --configuration $Configuration --output $publishPath --nologo
if ( $LASTEXITCODE -ne 0 ) { throw 'The publish failed.' }

# Development-only settings must not reach a customer's server.
Remove-Item (Join-Path $publishPath 'appsettings.Development.json') -Force -ErrorAction SilentlyContinue

Write-Host "Packing $zipPath..."
if ( Test-Path $zipPath ) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $zipPath

Write-Host "Created $zipPath ($([math]::Round((Get-Item $zipPath).Length / 1MB, 1)) MB)."
