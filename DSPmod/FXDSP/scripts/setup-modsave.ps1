[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'common.ps1')
$dependencyRoot = Join-Path (Get-FxDspRoot) 'artifacts\dependencies\DSPModSave'
$library = Join-Path $dependencyRoot 'DSPModSave.dll'
$expectedLibraryHash = '7B840504D750EFCA661703F8F9E9530BFC58BF18CBE6DD91840F3F77B73B9727'
$expectedArchiveHash = '89F7E2ABA66B633DA3BB5BC28A64C71FD4538F6EDD427919264281661CA8B8B0'
if (Test-Path -LiteralPath $library -PathType Leaf) {
    if ((Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash -eq $expectedLibraryHash) {
        Write-Output "DSPModSave 1.2.2 already prepared: $library"
        return
    }
    throw 'The existing DSPModSave.dll differs from the pinned package. Preserve and inspect it before replacing it.'
}
New-Item -ItemType Directory -Path $dependencyRoot -Force | Out-Null
$archive = Join-Path $dependencyRoot 'CommonAPI-DSPModSave-1.2.2.zip'
Invoke-WebRequest -Uri 'https://thunderstore.io/package/download/CommonAPI/DSPModSave/1.2.2/' -OutFile $archive -UseBasicParsing
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedArchiveHash) {
    throw 'DSPModSave download checksum differs from the reviewed official package.'
}
$package = Join-Path $dependencyRoot 'package'
Expand-Archive -LiteralPath $archive -DestinationPath $package -Force
$candidates = @(Get-ChildItem -LiteralPath $package -Filter 'DSPModSave.dll' -File -Recurse)
if ($candidates.Count -ne 1) { throw 'Expected exactly one DSPModSave.dll.' }
if ((Get-FileHash -LiteralPath $candidates[0].FullName -Algorithm SHA256).Hash -ne $expectedLibraryHash) {
    throw 'DSPModSave library checksum mismatch.'
}
Copy-Item -LiteralPath $candidates[0].FullName -Destination $library
Write-Output "Prepared build dependency: $library"
