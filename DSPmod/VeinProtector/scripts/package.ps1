# Shared entry-point name with FXDSP; keep the existing implementation available.
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'package-local.ps1') @args
