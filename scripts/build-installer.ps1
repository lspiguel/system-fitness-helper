<#
.SYNOPSIS
    Builds the System Fitness Helper MSIX package.

.DESCRIPTION
    1. Locates MSBuild via vswhere.
    2. Patches the version in Package.appxmanifest.
    3. Builds the Windows Application Packaging Project (wapproj).
    4. Signs the resulting .msix with a self-signed certificate
       (development) or a provided PFX (production).

    The final artifact is:
        publish\msix\SystemFitnessHelper.Packaging_<version>_x64_Test\
            SystemFitnessHelper.Packaging_<version>_x64.msix

    To install on a dev machine (elevated PowerShell):
        Add-AppxPackage .\SystemFitnessHelper.Packaging_<version>_x64.msix

.PARAMETER Configuration
    Build configuration. Default: Release.
    Use 'Debug' to package a debuggable build.

.PARAMETER Version
    Product version (three-part semver). Converted to four-part (x.y.z.0)
    for the MSIX identity. Default: 1.0.0.

.PARAMETER CertPfxPath
    Path to a PFX file for signing. If omitted a self-signed development
    certificate is created automatically (suitable for sideloading only).

.PARAMETER CertPfxPassword
    Plain-text password for the PFX. Required when CertPfxPath is provided.

.EXAMPLE
    .\scripts\build-installer.ps1

.EXAMPLE
    .\scripts\build-installer.ps1 -Version 1.2.0 -CertPfxPath prod.pfx -CertPfxPassword "s3cr3t"
#>

[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [string] $Version = '1.0.0',

    [string] $CertPfxPath = '',
    [SecureString] $CertPfxPassword = $null
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root       = Split-Path $PSScriptRoot -Parent
$OutputDir  = Join-Path $Root 'publish\msix'
$WapProj    = Join-Path $Root 'src\Packaging\SystemFitnessHelper.Packaging.wapproj'
$Manifest   = Join-Path $Root 'src\Packaging\Package.appxmanifest'
$PfxDev     = Join-Path $Root 'publish\dev-signing.pfx'
$DevCertPwd = 'dev'

# MSIX Identity Version must be four-part
$MsixVersion = "$Version.0"

# ------------------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------------------
function Step([string] $message) {
    Write-Host ''
    Write-Host "--- $message" -ForegroundColor Cyan
}

function Run([string] $exe, [string[]] $arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$exe' exited with code $LASTEXITCODE"
    }
}

# ------------------------------------------------------------------------------
# Locate MSBuild
# WAP projects are old-style MSBuild; dotnet build does not support them.
# Resolution order:
#   1. Already on PATH (Developer PowerShell sets this up automatically)
#   2. vswhere discovery (works in a plain PowerShell window)
# ------------------------------------------------------------------------------
Step 'Locating MSBuild'

$msbuild    = $null
$vswhere    = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsInstall  = $null

# 1. PATH — works when launched from a Developer PowerShell / Developer Command Prompt
$inPath = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if ($inPath) {
    $msbuild = $inPath.Source
}

# 2. vswhere fallback
if (-not $msbuild -and (Test-Path $vswhere)) {
    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
        -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}

if (-not $msbuild -or -not (Test-Path $msbuild)) {
    throw 'MSBuild.exe not found. Open a Developer PowerShell for VS 2026, or ensure the Visual Studio MSIX / Desktop workload is installed.'
}
Write-Host "  MSBuild: $msbuild" -ForegroundColor Green

# Discover VS installation root for WapProjPath (needed for Appx targets at command line)
if (Test-Path $vswhere) {
    # -prerelease is required for VS Insiders / Preview builds
    $vsInstall = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath 2>$null |
        Select-Object -First 1
    if (-not $vsInstall) {
        $vsInstall = & $vswhere -latest -prerelease -property installationPath 2>$null |
            Select-Object -First 1
    }
}
if (-not $vsInstall) {
    # Derive from msbuild.exe path — 5 levels up reaches the VS install root
    # e.g. ..\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe -> ..\Insiders
    $vsInstall = $msbuild
    for ($i = 0; $i -lt 5; $i++) { $vsInstall = Split-Path $vsInstall }
}

$wapProjPath = ''
$appxDir = Get-ChildItem (Join-Path $vsInstall 'MSBuild\Microsoft\VisualStudio') `
    -Filter 'AppxPackage' -Directory -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName
if ($appxDir) {
    $wapProjPath = "$appxDir\"
    Write-Host "  WapProjPath: $wapProjPath" -ForegroundColor Green
} else {
    Write-Host "  WapProjPath: not found - build may fail if MSIX workload is not installed" -ForegroundColor Yellow
}

# ------------------------------------------------------------------------------
# Locate signtool from the Windows SDK
# ------------------------------------------------------------------------------
Step 'Locating signtool'

$kitBin   = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$signtool = Get-ChildItem $kitBin -Filter signtool.exe -Recurse |
    Where-Object { $_.FullName -match 'x64' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $signtool) {
    throw 'signtool.exe not found. Install the Windows 10/11 SDK.'
}
Write-Host "  signtool: $signtool" -ForegroundColor Green

# ------------------------------------------------------------------------------
# Clean previous output
# ------------------------------------------------------------------------------
Step 'Cleaning previous output'
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item $OutputDir -ItemType Directory | Out-Null

# ------------------------------------------------------------------------------
# Patch version in Package.appxmanifest before building, restore in finally.
# The WAP build system does not propagate a Version MSBuild property into the
# manifest automatically, so we update the XML directly.
# ------------------------------------------------------------------------------
Step "Patching manifest version to $MsixVersion"

$manifestXml = [xml](Get-Content $Manifest -Encoding UTF8)
$ns = New-Object System.Xml.XmlNamespaceManager($manifestXml.NameTable)
$ns.AddNamespace('pkg', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$identity        = $manifestXml.SelectSingleNode('//pkg:Identity', $ns)
$originalVersion = $identity.Version
$originalPublisher = $identity.Publisher
$identity.Version = $MsixVersion
$manifestXml.Save($Manifest)
Write-Host "  $originalVersion -> $MsixVersion"

# ------------------------------------------------------------------------------
# Resolve signing certificate
# ------------------------------------------------------------------------------
Step 'Preparing signing certificate'

if ($CertPfxPath) {
    $pfxPath = $CertPfxPath
    $pfxPwd  = $CertPfxPassword   # already a SecureString from the caller
    Write-Host "  Using production certificate: $pfxPath" -ForegroundColor Yellow
}
else {
    Write-Host "  Creating self-signed dev certificate (Publisher: $originalPublisher)"

    $cert = New-SelfSignedCertificate `
        -Subject $originalPublisher `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyUsage DigitalSignature `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')

    $pfxPwd = ConvertTo-SecureString $DevCertPwd -AsPlainText -Force
    New-Item (Split-Path $PfxDev) -ItemType Directory -Force | Out-Null
    Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" `
        -FilePath $PfxDev -Password $pfxPwd | Out-Null

    $rootStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $rootStore.Add($cert)
    $rootStore.Close()

    $pfxPath = $PfxDev
    Write-Host "  Dev PFX: $pfxPath (trusted on this machine)" -ForegroundColor Green
}

# ------------------------------------------------------------------------------
# Build the MSIX package
# ------------------------------------------------------------------------------
Step "Building MSIX ($Configuration | x64 | v$MsixVersion)"

try {
    $msbuildArgs = @(
        $WapProj,
        "/p:Configuration=$Configuration",
        '/p:Platform=x64',
        '/p:AppxBundle=Never',
        '/p:UapAppxPackageBuildMode=SideLoadOnly',
        '/p:AppxPackageSigningEnabled=False',
        "/p:AppxPackageDir=$OutputDir\",
        '/m'
    )
    if ($wapProjPath) {
        $msbuildArgs += "/p:WapProjPath=$wapProjPath"
    }
    Run $msbuild $msbuildArgs
}
finally {
    $identity.Version = $originalVersion
    $manifestXml.Save($Manifest)
    Write-Host "  Manifest restored to $originalVersion"
}

# ------------------------------------------------------------------------------
# Sign the .msix
# ------------------------------------------------------------------------------
Step 'Signing package'

$msixFile = Get-ChildItem $OutputDir -Filter '*.msix' -Recurse |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $msixFile) {
    throw "No .msix file found in $OutputDir after build."
}

# signtool requires a plain-text /p argument — unwrap SecureString only here,
# in the narrowest possible scope, and let it go out of scope immediately.
$plainPwd = [System.Net.NetworkCredential]::new('', $pfxPwd).Password

Run $signtool @(
    'sign',
    '/fd', 'SHA256',
    '/f', $pfxPath,
    '/p', $plainPwd,
    '/tr', 'http://timestamp.digicert.com',
    '/td', 'SHA256',
    $msixFile
)

$plainPwd = $null   # clear from memory as soon as signtool is done

# ------------------------------------------------------------------------------
# Done
# ------------------------------------------------------------------------------
Write-Host ''
Write-Host '=======================================================' -ForegroundColor Green
Write-Host "  Build complete ($Configuration  v$Version)"           -ForegroundColor Green
Write-Host "  Package : $msixFile"                                   -ForegroundColor Green
Write-Host ''
Write-Host '  To install (elevated PowerShell):'                     -ForegroundColor Yellow
Write-Host "    Add-AppxPackage $msixFile"                           -ForegroundColor Yellow
Write-Host '=======================================================' -ForegroundColor Green
