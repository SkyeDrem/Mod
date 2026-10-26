[CmdletBinding(DefaultParameterSetName = 'Explicit')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Explicit')]
    [string]$GameManagedDir,
    [Parameter(Mandatory = $true, ParameterSetName = 'Explicit')]
    [string]$BepInExCoreDir,
    [Parameter(Mandatory = $true, ParameterSetName = 'Sibling')]
    [switch]$ReuseVeinProtectorReferences
)

. (Join-Path $PSScriptRoot 'common.ps1')
$projectRoot = Get-FxDspRoot

if ($ReuseVeinProtectorReferences) {
    $siblingRoot = Join-Path (Split-Path -Parent $projectRoot) 'VeinProtector\src\VeinProtector'
    $GameManagedDir = Join-Path $siblingRoot 'AssemblyFromGame'
    $BepInExCoreDir = Join-Path $siblingRoot 'AssemblyFromProfile'
}

$config = [PSCustomObject]@{
    GameManagedDir = (Resolve-Path -LiteralPath $GameManagedDir).Path
    BepInExCoreDir = (Resolve-Path -LiteralPath $BepInExCoreDir).Path
}
# Validate before replacing any existing local configuration.
foreach ($fileName in @('Assembly-CSharp.dll', 'netstandard.dll', 'UnityEngine.dll', 'UnityEngine.CoreModule.dll',
        'UnityEngine.UI.dll', 'UnityEngine.UIModule.dll', 'UnityEngine.TextRenderingModule.dll', 'UnityEngine.InputLegacyModule.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $config.GameManagedDir $fileName) -PathType Leaf)) {
        throw "Game assembly missing: $fileName"
    }
}
foreach ($fileName in @('BepInEx.dll', '0Harmony.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $config.BepInExCoreDir $fileName) -PathType Leaf)) {
        throw "Mod-loader assembly missing: $fileName"
    }
}

$configPath = Join-Path $projectRoot 'build.local.json'
$json = $config | ConvertTo-Json
[IO.File]::WriteAllText($configPath, $json + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
Write-Output "Local references configured: $configPath"
Write-Output 'References are read in place; no DLLs were copied or added to Git.'
