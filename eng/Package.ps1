<#
.SYNOPSIS
    Builds the release package of the PostSharp License Server.

.DESCRIPTION
    Publishes the web application and zips it into artifacts/PostSharp.LicenseServer.zip, which is
    the artifact attached to a GitHub release and which an administrator unpacks into an IIS site.

    The package is framework-dependent: the target machine needs the ASP.NET Core Hosting Bundle.

.PARAMETER Configuration
    The build configuration. Release by default.

.PARAMETER OutputPath
    Where to write the zip. artifacts/ by default.
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $OutputPath = (Join-Path $PSScriptRoot '..' 'artifacts')
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $repositoryRoot 'src' 'PostSharp.LicenseServer.Web' 'PostSharp.LicenseServer.Web.csproj'
$publishPath = Join-Path $repositoryRoot 'artifacts' 'publish'
$zipPath = Join-Path $OutputPath 'PostSharp.LicenseServer.zip'

if ( Test-Path $publishPath ) { Remove-Item $publishPath -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

Write-Host "Testing..."
Push-Location $repositoryRoot
try { dotnet test --configuration $Configuration --nologo }
finally { Pop-Location }
if ( $LASTEXITCODE -ne 0 ) { throw "The tests failed." }

Write-Host "Publishing..."
# Targeting the Windows runtime keeps the Linux and macOS native libraries -- which an IIS
# deployment never loads -- out of the package. It stays framework-dependent: the target machine
# needs the ASP.NET Core Hosting Bundle.
dotnet publish $project --configuration $Configuration --output $publishPath --runtime win-x64 --self-contained false --nologo
if ( $LASTEXITCODE -ne 0 ) { throw "The publish failed." }

# Development-only settings must not reach a customer's server.
Remove-Item (Join-Path $publishPath 'appsettings.Development.json') -Force -ErrorAction SilentlyContinue

Write-Host "Packing $zipPath..."
if ( Test-Path $zipPath ) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $zipPath

Write-Host "Created $zipPath ($([math]::Round((Get-Item $zipPath).Length / 1MB, 1)) MB)."
