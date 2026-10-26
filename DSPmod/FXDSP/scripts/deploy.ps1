[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$BepInExDirectory,
    [ValidateSet('Plugins', 'Scripts')][string]$Mode = 'Plugins',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)

. (Join-Path $PSScriptRoot 'common.ps1')
$pluginDll = Get-FxDspOutput -Configuration $Configuration
if (-not (Test-Path -LiteralPath $pluginDll -PathType Leaf)) {
    throw "Build the $Configuration configuration before deployment."
}

$loaderRoot = (Resolve-Path -LiteralPath $BepInExDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $loaderRoot 'core\BepInEx.dll') -PathType Leaf)) {
    throw "Not a configured BepInEx directory: $loaderRoot"
}
$pluginsDir = Join-Path $loaderRoot 'plugins'
$scriptsDir = Join-Path $loaderRoot 'scripts'
$modSaveCopies = @(Get-ChildItem -LiteralPath $pluginsDir -Filter 'DSPModSave.dll' -File -Recurse)
if ($modSaveCopies.Count -ne 1) {
    throw 'The test profile must contain exactly one DSPModSave 1.2.2 installation before deploying FXDSP.'
}
$targetDir = Join-Path $pluginsDir 'FXDSP'
if ($Mode -eq 'Scripts') {
    # ScriptEngine does not search subdirectories by default.
    $targetDir = $scriptsDir
}
$targetDll = Join-Path $targetDir 'FXDSP.dll'

foreach ($searchDir in @($pluginsDir, $scriptsDir)) {
    if (-not (Test-Path -LiteralPath $searchDir -PathType Container)) { continue }
    $duplicates = @(Get-ChildItem -LiteralPath $searchDir -Filter '*.dll' -File -Recurse |
        Where-Object { $_.Name -in @('FXDSP.dll', 'DSPFX.dll') } |
        Where-Object { -not [string]::Equals($_.FullName, $targetDll, [StringComparison]::OrdinalIgnoreCase) })
    if ($duplicates.Count -gt 0) {
        throw "Another FXDSP or legacy DSPFX copy exists. Migrate the previous deployment before installing: $($duplicates.FullName -join ', ')"
    }
}

if ($PSCmdlet.ShouldProcess($targetDll, "Deploy FXDSP ($Configuration, $Mode)")) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Copy-Item -LiteralPath $pluginDll -Destination $targetDll -Force
    $symbols = [IO.Path]::ChangeExtension($pluginDll, '.pdb')
    if (Test-Path -LiteralPath $symbols -PathType Leaf) {
        Copy-Item -LiteralPath $symbols -Destination (Join-Path $targetDir 'FXDSP.pdb') -Force
    }
    if ((Get-FileHash -LiteralPath $pluginDll -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash) {
        throw "Deployment checksum mismatch: $targetDll"
    }
    Write-Output "Deployed: $targetDll"
    if ($Mode -eq 'Scripts') {
        Write-Output 'Requires ScriptEngine installed separately; reload with its configured shortcut (default F6).'
    } else {
        Write-Output 'Restart the game to load this build through the normal BepInEx plugin loader.'
    }
}
