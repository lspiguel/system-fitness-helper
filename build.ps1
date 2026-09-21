<#
.SYNOPSIS
    Publishes System Fitness Helper into the layout that 'sfhi install' expects.

.DESCRIPTION
    Produces:

        publish/
          sfhi.exe            the installer
          rules.sample.json   seed configuration
          Service/            SystemFitnessHelper.Service.exe + dependencies
          TrayApp/            SystemFitnessHelper.TrayApp.exe + dependencies
          Ui/                 SystemFitnessHelper.Ui.exe + dependencies

    Run 'sfhi install' from the publish folder. Each component is published into its own
    directory so the installer copies whole directories rather than guessing which loose files
    belong to which executable.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Debug
    .\build.ps1 -SkipTests
    .\build.ps1 -Msi                        # also build publish\SystemFitnessHelper.msi
    .\build.ps1 -Msi -ProductVersion 1.1.0  # stamp a new version for a major upgrade
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # Defaulted below rather than here: with [CmdletBinding()], Windows PowerShell 5.1 does not
    # populate $PSScriptRoot while binding parameter defaults, so a default referencing it breaks
    # when the script is invoked as `powershell -File build.ps1`.
    [string]$OutputDir,

    [switch]$SkipTests,

    # Also build the MSI package. Requires the payload, so it runs last.
    [switch]$Msi,

    # Version stamped into both the assemblies and the MSI, so that the version reported by
    # 'sfhi status' matches the one shown in Apps & Features. Increment it for a major upgrade to
    # replace an installed package rather than sit alongside it.
    [string]$ProductVersion = '1.0.0'
)

$ErrorActionPreference = 'Stop'

if (-not $OutputDir) { $OutputDir = Join-Path $PSScriptRoot 'publish' }

$solution = Join-Path $PSScriptRoot 'src\SystemFitnessHelper.slnx'

$components = @(
    @{ Name = 'Service'; Project = 'src\Service\SystemFitnessHelper.Service.csproj' }
    @{ Name = 'TrayApp'; Project = 'src\TrayApp\SystemFitnessHelper.TrayApp.csproj' }
    @{ Name = 'Ui';      Project = 'src\Ui\SystemFitnessHelper.Ui.csproj' }
)

Write-Host "Building $Configuration..." -ForegroundColor Cyan
dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

if (-not $SkipTests) {
    Write-Host "`nRunning tests..." -ForegroundColor Cyan
    dotnet test $solution -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
}

# Start from a clean layout so removed files never linger and get copied to Program Files.
if (Test-Path $OutputDir) {
    Write-Host "`nCleaning $OutputDir" -ForegroundColor Cyan
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

foreach ($component in $components) {
    $target = Join-Path $OutputDir $component.Name
    Write-Host "`nPublishing $($component.Name) -> $target" -ForegroundColor Cyan

    dotnet publish (Join-Path $PSScriptRoot $component.Project) `
        -c $Configuration --self-contained false -o $target "-p:Version=$ProductVersion"
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $($component.Name)." }
}

Write-Host "`nPublishing installer -> $OutputDir" -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot 'src\Installer\SystemFitnessHelper.Installer.csproj') `
    -c $Configuration --self-contained false -o $OutputDir "-p:Version=$ProductVersion"
if ($LASTEXITCODE -ne 0) { throw "Publish failed for the installer." }

# Seed configuration, with every rule forced off so a fresh install never stops anything before
# the user has reviewed it. Disabling happens here rather than in an installer because both
# installers seed this file and the MSI copies it verbatim - it has nowhere to run logic.
$sampleSource = Join-Path $PSScriptRoot 'docs\sample-config1.json'
$sampleTarget = Join-Path $OutputDir 'rules.sample.json'
if (Test-Path $sampleSource) {
    $cfg = Get-Content $sampleSource -Raw | ConvertFrom-Json

    if (-not $cfg.ruleSets) {
        throw "$sampleSource has no 'ruleSets' object. It must use the multi-ruleset schema, or everything seeded from it deserialises to a config with no rulesets."
    }

    $ruleCount = 0
    foreach ($name in $cfg.ruleSets.PSObject.Properties.Name) {
        foreach ($rule in $cfg.ruleSets.$name.rules) {
            $rule.enabled = $false
            $ruleCount++
        }
    }

    $cfg | ConvertTo-Json -Depth 20 | Set-Content $sampleTarget -Encoding UTF8
    Write-Host "Seed configuration -> $sampleTarget ($ruleCount rules, all disabled)"
} else {
    Write-Warning "Sample configuration not found at $sampleSource"
}

if ($Msi) {
    # Built after the payload because the package harvests its file list from it.
    Write-Host "`nBuilding MSI (version $ProductVersion)" -ForegroundColor Cyan
    $wixproj = Join-Path $PSScriptRoot 'src\Package\SystemFitnessHelper.Package.wixproj'
    # -t:Rebuild, not build: WiX decides it is up to date from source timestamps alone and ignores
    # changes to the preprocessor constants below, so an incremental build would silently stamp
    # the previous ProductVersion into a package built from a newer payload.
    dotnet build $wixproj -c $Configuration -t:Rebuild `
        "-p:PayloadDir=$OutputDir" "-p:ProductVersion=$ProductVersion"
    if ($LASTEXITCODE -ne 0) { throw "MSI build failed." }

    $msiSource = Join-Path $PSScriptRoot "src\Package\bin\$Configuration\SystemFitnessHelper.msi"
    $msiTarget = Join-Path $OutputDir 'SystemFitnessHelper.msi'
    Copy-Item $msiSource $msiTarget -Force
    Write-Host "MSI -> $msiTarget"
}

Write-Host "`nBuild complete." -ForegroundColor Green
if ($Msi) {
    Write-Host "Install with either:" -ForegroundColor Green
    Write-Host "    msiexec /i `"$(Join-Path $OutputDir 'SystemFitnessHelper.msi')`"" -ForegroundColor Green
    Write-Host "  or, for development:" -ForegroundColor Green
    Write-Host "    cd `"$OutputDir`"; .\sfhi.exe install" -ForegroundColor Green
} else {
    Write-Host "Install with:" -ForegroundColor Green
    Write-Host "    cd `"$OutputDir`"" -ForegroundColor Green
    Write-Host "    .\sfhi.exe install" -ForegroundColor Green
}
