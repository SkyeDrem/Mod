[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'common.ps1')
$dependencyRoot = Join-Path (Get-FxDspRoot) 'artifacts\dependencies'
$archive = Join-Path $dependencyRoot 'LDBTool-3.0.3.zip'
$target = Join-Path $dependencyRoot 'LDBTool-3.0.3'
New-Item -ItemType Directory -Path $dependencyRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
    Invoke-WebRequest 'https://thunderstore.io/package/download/xiaoye97/LDBTool/3.0.3/' -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '71EC2D01AD484422C5C64385AEA783CED81800E0F4F97BCF6628599C495C17E6') {
    throw 'LDBTool 3.0.3 archive checksum mismatch.'
}
Expand-Archive -LiteralPath $archive -DestinationPath $target -Force
Write-Output "Prepared LDBTool reference: $target. Profile installation is a separate deployment step."
