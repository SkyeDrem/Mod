[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
. (Join-Path $PSScriptRoot 'common.ps1')
$taskRoot = Get-FxDspRoot
$taskConfig = Get-FxDspLocalConfig -ConfigPath (Join-Path $taskRoot 'build.local.json')
Add-Type -LiteralPath (Join-Path $taskConfig.BepInExCoreDir 'Mono.Cecil.dll')
$taskResolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
# Prefer the unmodified published BepInEx package if it was downloaded for the release audit.
$taskPublishedCore = Join-Path $taskRoot 'artifacts\release-dependency-check\BepInEx-5.4.17\BepInExPack\BepInEx\core'
if (Test-Path -LiteralPath $taskPublishedCore) { $taskResolver.AddSearchDirectory($taskPublishedCore) }
foreach ($taskDirectory in @($taskConfig.GameManagedDir, $taskConfig.BepInExCoreDir,
    (Join-Path $taskRoot 'artifacts\dependencies\DSPModSave'),
    (Join-Path $taskRoot 'artifacts\dependencies\LDBTool-3.0.3'))) { $taskResolver.AddSearchDirectory($taskDirectory) }
$taskReader = [Mono.Cecil.ReaderParameters]::new()
$taskReader.AssemblyResolver = $taskResolver
$taskReader.ReadSymbols = $true
$taskReader.ReadingMode = [Mono.Cecil.ReadingMode]::Immediate
$taskVersion = [regex]::Match((Get-Content (Join-Path $taskRoot 'src\FXDSP\PluginInfo.cs') -Raw), 'Version = "([^"]+)"').Groups[1].Value
$taskHashes = @{}
function Get-AllTypes($taskTypes) {
    foreach ($taskType in $taskTypes) { $taskType; Get-AllTypes $taskType.NestedTypes }
}
try {
    foreach ($taskName in @('FXDSP','FXDSP.DeliveryBootstrap')) {
        $taskFile = Join-Path $taskRoot "src\$taskName\bin\$Configuration\$taskName.dll"
        $taskAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($taskFile, $taskReader)
        try {
            $taskPlugins = @($taskAssembly.MainModule.Types | ForEach-Object CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'BepInPlugin' })
            if ($taskPlugins.Count -ne 1 -or $taskPlugins[0].ConstructorArguments[2].Value -ne $taskVersion) { throw "Plugin version/identity mismatch: $taskName" }
            if ($taskAssembly.MainModule.AssemblyReferences.Name -match 'ScriptEngine|ConfigurationManager|dnSpy') { throw 'Unexpected development dependency.' }
            if ($Configuration -eq 'Release') {
                foreach ($taskDebugEntry in $taskAssembly.MainModule.GetDebugHeader().Entries) {
                    if ($taskDebugEntry.Directory.Type.ToString() -eq 'CodeView') {
                        $taskPdbPath = [Text.Encoding]::UTF8.GetString($taskDebugEntry.Data, 24, $taskDebugEntry.Data.Length - 24).TrimEnd([char]0)
                        if ($taskPdbPath -notlike '/_/FXDSP/*') { throw "Unmapped local debug path in $taskName" }
                    }
                }
            }
            $taskMembers = [Collections.Generic.HashSet[string]]::new()
            $taskPatchCount = 0
            foreach ($taskType in (Get-AllTypes $taskAssembly.MainModule.Types)) {
                foreach ($taskPatch in @($taskType.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' })) {
                    $taskArguments = $taskPatch.ConstructorArguments
                    if ($taskArguments.Count -ge 2 -and $taskArguments[0].Value -is [Mono.Cecil.TypeReference] -and $taskArguments[1].Value -is [string]) {
                        $taskTarget = $taskArguments[0].Value.Resolve()
                        $taskMethodName = [string]$taskArguments[1].Value
                        if (@($taskTarget.Methods | Where-Object Name -eq $taskMethodName).Count -eq 0) { throw "Missing patch target: $taskTarget::$taskMethodName" }
                        $taskPatchCount++
                    }
                }
                foreach ($taskMethod in $taskType.Methods) {
                    if (-not $taskMethod.HasBody) { continue }
                    foreach ($taskInstruction in $taskMethod.Body.Instructions) {
                        $taskMember = $taskInstruction.Operand
                        if ($taskMember -isnot [Mono.Cecil.MethodReference] -and $taskMember -isnot [Mono.Cecil.FieldReference]) { continue }
                        $taskScope = $taskMember.DeclaringType.Scope.Name
                        if ($taskScope -notmatch '^(Assembly-CSharp|UnityEngine(?:\..*)?|BepInEx|0Harmony|DSPModSave|LDBTool)$') { continue }
                        if (-not $taskMembers.Add($taskMember.FullName)) { continue }
                        if ($null -eq $taskMember.Resolve()) { throw "Unresolved runtime API: $($taskMember.FullName)" }
                    }
                }
            }
            $taskLocale = $taskAssembly.MainModule.Resources | Where-Object Name -eq 'FXDSP.Localization.en-US.tsv'
            $taskSourceLocale = [IO.File]::ReadAllBytes((Join-Path $taskRoot 'src\FXDSP\Localization\en-US.tsv'))
            $taskHash = [Security.Cryptography.SHA256]::HashData($taskLocale.GetResourceData())
            if (-not [Linq.Enumerable]::SequenceEqual([byte[]]$taskHash, [byte[]][Security.Cryptography.SHA256]::HashData($taskSourceLocale))) { throw 'Embedded localization differs from source.' }
            if ($taskName -eq 'FXDSP.DeliveryBootstrap') {
                $taskIcon = $taskAssembly.MainModule.Resources | Where-Object Name -eq 'FXDSP.DeliveryBootstrap.Assets.technology-icons.png'
                $taskIconSource = [IO.File]::ReadAllBytes((Join-Path $taskRoot 'src\FXDSP.DeliveryBootstrap\Assets\technology-icons.png'))
                if (-not [Linq.Enumerable]::SequenceEqual([byte[]][Security.Cryptography.SHA256]::HashData($taskIcon.GetResourceData()), [byte[]][Security.Cryptography.SHA256]::HashData($taskIconSource))) { throw 'Embedded icons differ from source.' }
            }
            $taskAssembly.Name.Name += '_ReloadValidation'
            $taskStream = [IO.MemoryStream]::new()
            try { $taskAssembly.Write($taskStream); if ($taskStream.Length -lt 1024) { throw 'Assembly rewrite failed.' } }
            finally { $taskStream.Dispose() }
            $taskHashes[$taskName] = (Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash
            Write-Output "PASS $taskName $taskVersion`: $($taskMembers.Count) runtime API references, $taskPatchCount patch targets, embedded assets and assembly rewrite."
        } finally { $taskAssembly.Dispose() }
    }
} finally { $taskResolver.Dispose() }
[ordered]@{ Version=$taskVersion; Configuration=$Configuration; Assemblies=$taskHashes } | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $taskRoot "artifacts\release-$taskVersion-assemblies.json") -Encoding utf8
