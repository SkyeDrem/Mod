[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$BepInExDirectory,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug'
)
. (Join-Path $PSScriptRoot 'common.ps1')
$repo = Get-FxDspRoot
$deploymentVersion = [regex]::Match((Get-Content -LiteralPath (Join-Path $repo 'src\FXDSP\PluginInfo.cs') -Raw), 'Version\s*=\s*"([0-9.]+)"').Groups[1].Value
if ([string]::IsNullOrEmpty($deploymentVersion)) { throw 'Cannot determine FXDSP build version.' }
$loader = (Resolve-Path -LiteralPath $BepInExDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $loader 'core\BepInEx.dll') -PathType Leaf)) { throw 'Not a BepInEx profile.' }
$dependency = Join-Path $repo 'artifacts\dependencies\LDBTool-3.0.3'
$bootstrap = Join-Path $repo "src\FXDSP.DeliveryBootstrap\bin\$Configuration"
$targets = @(
    [PSCustomObject]@{ Source=(Join-Path $dependency 'LDBTool.dll'); Target=(Join-Path $loader 'plugins\xiaoye97-LDBTool\LDBTool.dll') },
    [PSCustomObject]@{ Source=(Join-Path $dependency 'LDBTool.pdb'); Target=(Join-Path $loader 'plugins\xiaoye97-LDBTool\LDBTool.pdb') },
    [PSCustomObject]@{ Source=(Join-Path $dependency 'manifest.json'); Target=(Join-Path $loader 'plugins\xiaoye97-LDBTool\manifest.json') },
    [PSCustomObject]@{ Source=(Join-Path $dependency 'README.md'); Target=(Join-Path $loader 'plugins\xiaoye97-LDBTool\README.md') },
    [PSCustomObject]@{ Source=(Join-Path $bootstrap 'FXDSP.DeliveryBootstrap.dll'); Target=(Join-Path $loader 'plugins\FXDSP.DeliveryBootstrap\FXDSP.DeliveryBootstrap.dll') },
    [PSCustomObject]@{ Source=(Join-Path $bootstrap 'FXDSP.DeliveryBootstrap.pdb'); Target=(Join-Path $loader 'plugins\FXDSP.DeliveryBootstrap\FXDSP.DeliveryBootstrap.pdb') }
)
foreach ($target in $targets) { if (-not (Test-Path -LiteralPath $target.Source -PathType Leaf)) { throw "Missing build/dependency: $($target.Source)" } }
$existingLdb = @(Get-ChildItem -LiteralPath (Join-Path $loader 'plugins') -Filter 'LDBTool.dll' -File -Recurse)
if ($existingLdb.Count -gt 1 -or ($existingLdb.Count -eq 1 -and $existingLdb[0].FullName -ne $targets[0].Target)) {
    throw 'A different LDBTool installation already exists; refusing to create a duplicate.'
}
if ($PSCmdlet.ShouldProcess($loader, 'Back up and install FXDSP delivery prototypes, pinned LDBTool, and reloadable FXDSP')) {
    $backup = Join-Path $repo ('artifacts\deployment-backups\FXDSP-' + $deploymentVersion + '-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    $previous = @((Join-Path $loader 'scripts\FXDSP.dll'), (Join-Path $loader 'scripts\FXDSP.pdb')) + @($targets | ForEach-Object Target)
    foreach ($path in $previous) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $relative = $path.Substring($loader.Length).TrimStart('\')
        $copy = Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $copy) -Force | Out-Null
        Copy-Item -LiteralPath $path -Destination $copy
        if ((Get-FileHash -LiteralPath $path).Hash -ne (Get-FileHash -LiteralPath $copy).Hash) { throw 'Backup checksum mismatch.' }
    }
    foreach ($target in $targets) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $target.Target) -Force | Out-Null
        Copy-Item -LiteralPath $target.Source -Destination $target.Target -Force
        if ((Get-FileHash -LiteralPath $target.Source).Hash -ne (Get-FileHash -LiteralPath $target.Target).Hash) { throw 'Delivery deployment checksum mismatch.' }
    }
    & (Join-Path $repo 'scripts\deploy.ps1') -BepInExDirectory $loader -Mode Scripts -Configuration $Configuration
    [PSCustomObject]@{ Version=$deploymentVersion; Profile='FXDSP'; ProfilePath=(Split-Path -Parent $loader); BackupPath=$backup
        PluginPath=(Join-Path $loader 'scripts\FXDSP.dll'); PrototypePath=$targets[4].Target; LDBToolVersion='3.0.3'
        RequiresRestart=$true; Acceptance='Restart via r2modman; follow the current version acceptance document for save and prototype checks.'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repo 'artifacts\prototype-deployment.json') -Encoding utf8
    Write-Output "Delivery update installed. Restart the game once before using new buildings. Backup: $backup"
}
