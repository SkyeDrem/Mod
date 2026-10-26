[CmdletBinding()]
param([switch]$English)
. (Join-Path $PSScriptRoot 'common.ps1')
$taskConfig = Get-FxDspLocalConfig -ConfigPath (Join-Path (Get-FxDspRoot) 'build.local.json')
[Reflection.Assembly]::LoadFrom((Join-Path $taskConfig.GameManagedDir 'UnityEngine.CoreModule.dll')) | Out-Null
$taskUi = [Reflection.Assembly]::LoadFrom((Join-Path $taskConfig.GameManagedDir 'UnityEngine.UI.dll'))
$taskFlags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$taskType = $taskUi.GetType('UnityEngine.UI.Youthcat.TextIconMapping', $true)
$taskConfigType = $taskType.GetNestedType('IconConfig', $taskFlags)
$taskKey = $taskType.GetMethod('StringToKey', $taskFlags, $null, [Type[]]@([string]), $null)
$taskParse = $taskType.GetMethod('GetIconInString', $taskFlags)
$taskIds = @(1951) + @(7701..7705) + @(7801..7806)
$taskNames = @('交易系统') + @(1..5 | ForEach-Object { '建筑兑换' }) + @(1..6 | ForEach-Object { '交易效率' })
if ($English) { $taskNames = @('Trading System') + @(1..5 | ForEach-Object { 'Building Exchange' }) + @(1..6 | ForEach-Object { 'Trading Efficiency' }) }
$taskEntries = [Array]::CreateInstance($taskConfigType, $taskIds.Count)
$taskKeys = [Collections.Generic.HashSet[uint32]]::new()
for ($taskIndex = 0; $taskIndex -lt $taskIds.Count; $taskIndex++) {
    $taskTag = 'fx' + $taskIds[$taskIndex]
    $taskValue = [uint32]$taskKey.Invoke($null, [object[]]@($taskTag))
    if ($taskValue -eq 0 -or -not $taskKeys.Add($taskValue)) { throw "Invalid/colliding native icon key: $taskTag" }
    $taskEntry = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskConfigType)
    $taskConfigType.GetField('name', $taskFlags).SetValue($taskEntry, $taskNames[$taskIndex])
    $taskConfigType.GetField('tag', $taskFlags).SetValue($taskEntry, $taskTag)
    $taskEntries.SetValue($taskEntry, $taskIndex)
}
# Only the managed dictionary/parser is exercised. Bypass ScriptableObject's native constructor;
# Refresh and GetIconInString contain no Unity engine calls in the inspected game assembly.
$taskMapping = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskType)
$taskType.GetField('iconMappings', $taskFlags).SetValue($taskMapping, $taskEntries)
$taskType.GetMethod('Refresh', $taskFlags).Invoke($taskMapping, [object[]]@()) | Out-Null
for ($taskIndex = 0; $taskIndex -lt $taskIds.Count; $taskIndex++) {
    $taskTag = 'fx' + $taskIds[$taskIndex]
    foreach ($taskWithName in @($false, $true)) {
        $taskToken = '\' + $taskTag + $(if ($taskWithName) { '-;' } else { ';' })
        $taskArguments = [object[]]@($taskToken, [int]0, [bool]$false)
        $taskResult = $taskParse.Invoke($taskMapping, $taskArguments)
        if ($null -eq $taskResult -or $taskArguments[2] -ne $taskWithName -or $taskArguments[1] -ne $taskToken.Length - 1) {
            throw "Native parser failed token: $taskToken"
        }
        if ($taskConfigType.GetField('name', $taskFlags).GetValue($taskResult) -ne $taskNames[$taskIndex] -or
            $taskConfigType.GetField('tag', $taskFlags).GetValue($taskResult) -ne $taskTag) { throw 'Native parser returned wrong technology.' }
    }
    Write-Output "PASS native icon/name tokens: $taskTag"
}
if ([uint32]$taskKey.Invoke($null, [object[]]@('fxdsptech1951')) -ne 0) { throw 'Unexpected native tag length contract.' }
Write-Output 'All 24 native inline icon/name token checks passed; overlength tags correctly rejected.'
