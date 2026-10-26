#Requires -Version 7.2
[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'common.ps1')
$taskRoot = Get-FxDspRoot
& (Join-Path $PSScriptRoot 'validate-release.ps1') -Configuration Release
$taskSource = Join-Path $taskRoot 'release'
$taskManifest = Get-Content -LiteralPath (Join-Path $taskSource 'manifest.json') -Raw | ConvertFrom-Json
if ($taskManifest.name -notmatch '^[A-Za-z0-9_]{1,128}$' -or $taskManifest.version_number -notmatch '^\d+\.\d+\.\d+$' -or
    $taskManifest.description.Length -gt 250) { throw 'Invalid Thunderstore manifest.' }
$taskAudit = Get-Content -LiteralPath (Join-Path $taskRoot "artifacts\release-$($taskManifest.version_number)-assemblies.json") -Raw | ConvertFrom-Json
if ($taskAudit.Version -ne $taskManifest.version_number) { throw 'Manifest version does not match validated build.' }
foreach ($taskDependency in $taskManifest.dependencies) {
    if ($taskDependency -notmatch '^[A-Za-z0-9_]+-[A-Za-z0-9_]+-\d+\.\d+\.\d+$') { throw "Invalid dependency: $taskDependency" }
}
$taskIcon = [IO.File]::ReadAllBytes((Join-Path $taskSource 'icon.png'))
if ($taskIcon.Length -lt 24 -or [Convert]::ToHexString($taskIcon[0..7]) -ne '89504E470D0A1A0A' -or
    [Convert]::ToHexString($taskIcon[16..23]) -ne '0000010000000100') { throw 'icon.png must be a 256x256 PNG.' }
$taskName = "$($taskManifest.name)-$($taskManifest.version_number)"
$taskDist = Join-Path $taskRoot 'dist'
$taskStage = Join-Path $taskDist $taskName
New-Item -ItemType Directory -Path (Join-Path $taskStage 'plugins\FXDSP') -Force | Out-Null
$taskFiles = @('manifest.json','README.md','CHANGELOG.md','icon.png')
foreach ($taskFile in $taskFiles) { Copy-Item -LiteralPath (Join-Path $taskSource $taskFile) -Destination (Join-Path $taskStage $taskFile) -Force }
foreach ($taskModule in @('FXDSP','FXDSP.DeliveryBootstrap')) {
    $taskBuilt = Join-Path $taskRoot "src\$taskModule\bin\Release\$taskModule.dll"
    Copy-Item -LiteralPath $taskBuilt -Destination (Join-Path $taskStage "plugins\FXDSP\$taskModule.dll") -Force
    $taskFiles += "plugins/FXDSP/$taskModule.dll"
}
$taskArchive = Join-Path $taskDist "$taskName.zip"
$taskFileStream = [IO.File]::Open($taskArchive, [IO.FileMode]::Create)
$taskZip = [IO.Compression.ZipArchive]::new($taskFileStream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($taskFile in $taskFiles) {
        $taskEntry = $taskZip.CreateEntry($taskFile, [IO.Compression.CompressionLevel]::Optimal)
        $taskEntry.LastWriteTime = [DateTimeOffset]::new(2000,1,1,0,0,0,[TimeSpan]::Zero)
        $taskOutput = $taskEntry.Open()
        try { $taskBytes = [IO.File]::ReadAllBytes((Join-Path $taskStage $taskFile)); $taskOutput.Write($taskBytes, 0, $taskBytes.Length) }
        finally { $taskOutput.Dispose() }
    }
} finally { $taskZip.Dispose(); $taskFileStream.Dispose() }
$taskEntries = [Collections.Generic.List[object]]::new()
$taskReadZip = [IO.Compression.ZipFile]::OpenRead($taskArchive)
try {
    if ($taskReadZip.Entries.Count -ne $taskFiles.Count) { throw 'Package contains unexpected files.' }
    foreach ($taskFile in $taskFiles) {
        $taskEntry = $taskReadZip.GetEntry($taskFile)
        if ($null -eq $taskEntry) { throw "Missing ZIP entry: $taskFile" }
        $taskStream = $taskEntry.Open()
        try { $taskDigest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskStream)) }
        finally { $taskStream.Dispose() }
        if ($taskDigest -ne (Get-FileHash -LiteralPath (Join-Path $taskStage $taskFile)).Hash) { throw "ZIP verification failed: $taskFile" }
        $taskEntries.Add([ordered]@{Path=$taskFile; Bytes=$taskEntry.Length; SHA256=$taskDigest})
    }
} finally { $taskReadZip.Dispose() }
$taskHash = (Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash.ToLowerInvariant()
"$taskHash  $taskName.zip" | Set-Content -LiteralPath "$taskArchive.sha256" -Encoding ascii
$taskEntries | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskDist "$taskName-files.json") -Encoding utf8
Write-Output "Package: $taskArchive"
Write-Output "Verified $($taskFiles.Count) archive entries; SHA256 $taskHash"
