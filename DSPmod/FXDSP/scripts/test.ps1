[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'common.ps1')
Set-FxDspBuildEnvironment
$dotnet = Get-FxDspDotnet
Push-Location (Get-FxDspRoot)
try {
    foreach ($suite in @('FXDSP.Core.Tests', 'FXDSP.Margin.Tests', 'FXDSP.Achievements.Tests', 'FXDSP.Compatibility.Tests', 'FXDSP.Persistence.Tests', 'FXDSP.Inventory.Tests', 'FXDSP.Delivery.Tests', 'FXDSP.DeliveryRuntime.Tests', 'FXDSP.Research.Tests', 'FXDSP.Localization.Tests')) {
        & $dotnet run --project "tests\$suite\$suite.csproj" --configuration Release --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "$suite failed with exit code $LASTEXITCODE." }
    }
} finally { Pop-Location }
