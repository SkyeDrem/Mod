Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-FxDspRoot {
    return (Split-Path -Parent $PSScriptRoot)
}

function Get-FxDspLocalConfig {
    param([string]$ConfigPath)

    if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
        throw "Local build configuration missing: $ConfigPath. Run scripts\setup-local.ps1 first."
    }

    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    foreach ($property in @('GameManagedDir', 'BepInExCoreDir')) {
        if (-not $config.PSObject.Properties[$property] -or [string]::IsNullOrWhiteSpace($config.$property)) {
            throw "Missing $property in $ConfigPath."
        }
        $value = [string]$config.$property
        if (-not [IO.Path]::IsPathRooted($value)) {
            $value = Join-Path (Get-FxDspRoot) $value
        }
        $config.$property = [IO.Path]::GetFullPath($value).TrimEnd('\', '/')
    }

    $requiredFiles = @(
        (Join-Path $config.GameManagedDir 'Assembly-CSharp.dll'),
        (Join-Path $config.GameManagedDir 'netstandard.dll'),
        (Join-Path $config.GameManagedDir 'UnityEngine.dll'),
        (Join-Path $config.GameManagedDir 'UnityEngine.CoreModule.dll'),
        (Join-Path $config.GameManagedDir 'UnityEngine.UI.dll'),
        (Join-Path $config.GameManagedDir 'UnityEngine.UIModule.dll'),
        (Join-Path $config.GameManagedDir 'UnityEngine.TextRenderingModule.dll'),
        (Join-Path $config.GameManagedDir 'UnityEngine.InputLegacyModule.dll'),
        (Join-Path $config.BepInExCoreDir 'BepInEx.dll'),
        (Join-Path $config.BepInExCoreDir '0Harmony.dll')
    )
    foreach ($requiredFile in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Required local assembly missing: $requiredFile"
        }
    }
    return $config
}

function Get-FxDspOutput {
    param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
    return (Join-Path (Get-FxDspRoot) "src\FXDSP\bin\$Configuration\FXDSP.dll")
}

function Get-FxDspDotnet {
    $localDotnet = Join-Path (Get-FxDspRoot) 'artifacts\tools\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
        return $localDotnet
    }
    $installedDotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($installedDotnet) { return $installedDotnet.Source }
    throw '.NET SDK not found. Run scripts\setup-sdk.ps1 to install the pinned SDK inside this project.'
}

function Set-FxDspBuildEnvironment {
    $env:DOTNET_CLI_HOME = Join-Path (Get-FxDspRoot) 'artifacts\dotnet-home'
    $env:NUGET_PACKAGES = Join-Path (Get-FxDspRoot) 'artifacts\nuget-packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path (Get-FxDspRoot) 'artifacts\nuget-http-cache'
    $env:NUGET_SCRATCH = Join-Path (Get-FxDspRoot) 'artifacts\nuget-scratch'
    $env:NUGET_PLUGINS_CACHE_PATH = Join-Path (Get-FxDspRoot) 'artifacts\nuget-plugins-cache'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
}
