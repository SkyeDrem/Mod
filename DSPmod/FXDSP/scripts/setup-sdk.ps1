[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'common.ps1')
$sdkVersion = (Get-Content -LiteralPath (Join-Path (Get-FxDspRoot) 'global.json') -Raw | ConvertFrom-Json).sdk.version
$expectedHash = 'ae4fd528cb7e9f6af6aaf7de36715a69f761f183744f1bb3025d19f5a92447090ec561410e46266c1d10a63781ed6f3de771760bc76c876d87daabc367b898cd'
$sdkRoot = Join-Path (Get-FxDspRoot) 'artifacts\tools\dotnet'
$sdkMarker = Join-Path $sdkRoot "sdk\$sdkVersion\dotnet.dll"
if (Test-Path -LiteralPath $sdkMarker -PathType Leaf) {
    Write-Output "SDK $sdkVersion already installed: $sdkRoot"
    return
}

$downloadDir = Join-Path (Get-FxDspRoot) 'artifacts\downloads'
New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
$archive = Join-Path $downloadDir "dotnet-sdk-$sdkVersion-win-x64.zip"
$sdkUrl = "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdkVersion/dotnet-sdk-$sdkVersion-win-x64.zip"
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
Write-Output "Downloading .NET SDK $sdkVersion from Microsoft..."
Invoke-WebRequest -Uri $sdkUrl -OutFile $archive -UseBasicParsing
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $expectedHash) {
    throw 'SDK SHA512 mismatch. Archive was not extracted. Verify the pinned version and official release hash.'
}
Expand-Archive -LiteralPath $archive -DestinationPath $sdkRoot -Force
if (-not (Test-Path -LiteralPath $sdkMarker -PathType Leaf)) {
    throw 'SDK extraction did not produce the expected files.'
}
Remove-Item -LiteralPath $archive
Write-Output "Installed local SDK: $sdkRoot"
