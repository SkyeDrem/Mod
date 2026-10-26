[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$LocalConfigPath = ''
)

. (Join-Path $PSScriptRoot 'common.ps1')
if ([string]::IsNullOrWhiteSpace($LocalConfigPath)) { $LocalConfigPath = Join-Path (Get-FxDspRoot) 'build.local.json' }
$config = Get-FxDspLocalConfig -ConfigPath $LocalConfigPath
$dotnet = Get-FxDspDotnet
Set-FxDspBuildEnvironment

$project = Join-Path (Get-FxDspRoot) 'src\FXDSP\FXDSP.csproj'
Push-Location (Get-FxDspRoot)
try {
    & $dotnet build $project --configuration $Configuration --nologo --verbosity minimal "/p:DspManagedDir=$($config.GameManagedDir)" "/p:BepInExCoreDir=$($config.BepInExCoreDir)"
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
    $bootstrap = Join-Path (Get-FxDspRoot) 'src\FXDSP.DeliveryBootstrap\FXDSP.DeliveryBootstrap.csproj'
    if (-not (Test-Path -LiteralPath (Join-Path (Get-FxDspRoot) 'artifacts\dependencies\LDBTool-3.0.3\LDBTool.dll'))) {
        throw 'LDBTool 3.0.3 build reference missing. Run scripts\setup-delivery.ps1 first.'
    }
    & $dotnet build $bootstrap --configuration $Configuration --nologo --verbosity minimal "/p:DspManagedDir=$($config.GameManagedDir)" "/p:BepInExCoreDir=$($config.BepInExCoreDir)"
    if ($LASTEXITCODE -ne 0) { throw "Delivery bootstrap build failed: $LASTEXITCODE" }
} finally {
    Pop-Location
}

$output = Get-FxDspOutput -Configuration $Configuration
if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
    throw "MSBuild did not produce the expected DLL: $output"
}
Write-Output "Built: $output"
