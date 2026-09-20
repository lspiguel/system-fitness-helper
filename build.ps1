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
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDir = (Join-Path $PSScriptRoot 'publish'),

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

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
        -c $Configuration --self-contained false -o $target
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $($component.Name)." }
}

Write-Host "`nPublishing installer -> $OutputDir" -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot 'src\Installer\SystemFitnessHelper.Installer.csproj') `
    -c $Configuration --self-contained false -o $OutputDir
if ($LASTEXITCODE -ne 0) { throw "Publish failed for the installer." }

# Seed configuration. The installer disables every rule before writing it to ProgramData, so a
# fresh install never stops anything until the user reviews and enables rules deliberately.
$sampleSource = Join-Path $PSScriptRoot 'docs\sample-config1.json'
$sampleTarget = Join-Path $OutputDir 'rules.sample.json'
if (Test-Path $sampleSource) {
    Copy-Item $sampleSource $sampleTarget -Force
    Write-Host "Copied sample configuration -> $sampleTarget"
} else {
    Write-Warning "Sample configuration not found at $sampleSource"
}

Write-Host "`nBuild complete." -ForegroundColor Green
Write-Host "Install with:" -ForegroundColor Green
Write-Host "    cd `"$OutputDir`"" -ForegroundColor Green
Write-Host "    .\sfhi.exe install" -ForegroundColor Green
