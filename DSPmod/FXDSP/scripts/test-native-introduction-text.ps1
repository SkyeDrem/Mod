#Requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Debug', [switch]$English)
. (Join-Path $PSScriptRoot 'common.ps1')
$taskConfig=Get-FxDspLocalConfig -ConfigPath (Join-Path (Get-FxDspRoot) 'build.local.json')
Add-Type -LiteralPath (Join-Path $taskConfig.BepInExCoreDir 'Mono.Cecil.dll')
$taskAssembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Get-FxDspOutput -Configuration $Configuration))
try {
    $taskCopy=$taskAssembly.MainModule.Types | Where-Object FullName -eq 'FXDSP.UI.IntroductionCopy'
    $taskBody=[string]($taskCopy.Fields | Where-Object Name -eq 'Body').Constant
} finally { $taskAssembly.Dispose() }
$taskEngine=[Reflection.Assembly]::LoadFrom((Join-Path $taskConfig.GameManagedDir 'UnityEngine.CoreModule.dll'))
$taskUi=[Reflection.Assembly]::LoadFrom((Join-Path $taskConfig.GameManagedDir 'UnityEngine.UI.dll'))
$taskParser=$taskUi.GetType('UnityEngine.UI.Youthcat.TextGenerator',$true).GetMethod('ReadRichTextColor',[Reflection.BindingFlags]'Static,Public,NonPublic')
$taskMatches=[regex]::Matches($taskBody,'<color=#([0-9A-F]{6})>(.*?)</color>')
$taskExpected=@('开多','上涨','开空','下跌','独立','仓位','杠杆','建筑币','F8','物流塔交付平台','科技升级','FX战士伊卡洛斯')
if ($English) {
    $taskCore=[Reflection.Assembly]::LoadFrom((Get-FxDspOutput -Configuration $Configuration))
    $taskLocalization=$taskCore.GetType('FXDSP.Core.TextLocalization',$true)
    $taskLocalization.GetProperty('English').SetValue($null,$true)
    $taskBody=$taskLocalization.GetMethod('Translate').Invoke($null,[object[]]@($taskBody))
    $taskMatches=[regex]::Matches($taskBody,'<color=#([0-9A-F]{6})>(.*?)</color>')
    $taskExpected=@('long','rise','short','fall','independent','position allocation','leverage','Building Coins','F8','logistics delivery platforms','technology upgrades','FX Warrior Icarus')
}
if ($taskMatches.Count -ne $taskExpected.Count) { throw 'Missing introduction emphasis.' }
for ($taskIndex=0; $taskIndex -lt $taskMatches.Count; $taskIndex++) {
    $taskMatch=$taskMatches[$taskIndex]
    if ($taskMatch.Groups[2].Value -ne $taskExpected[$taskIndex]) { throw 'Introduction emphasis order mismatch.' }
    $taskHex=$taskMatch.Groups[1].Value
    $taskTag='<color=#'+$taskHex+'>'
    $taskColor=[Activator]::CreateInstance($taskEngine.GetType('UnityEngine.Color32'))
    $taskArguments=[object[]]@($taskTag,[int]0,[int]7,$taskColor,[int]0,$false)
    if (-not $taskParser.Invoke($null,$taskArguments) -or $taskArguments[1] -ne $taskTag.Length -or $taskArguments[4] -ne 6) {
        throw 'Native color parser rejected an introduction tag.'
    }
    if ($taskArguments[3].r -ne [Convert]::ToByte($taskHex.Substring(0,2),16) -or
        $taskArguments[3].g -ne [Convert]::ToByte($taskHex.Substring(2,2),16) -or
        $taskArguments[3].b -ne [Convert]::ToByte($taskHex.Substring(4,2),16)) { throw 'Native parsed color differs.' }
    if ($taskIndex -lt 2 -and $taskHex -ne 'D45954') { throw 'Long/rise must remain red.' }
    if ($taskIndex -ge 2 -and $taskIndex -lt 4 -and $taskHex -ne '52BA80') { throw 'Short/fall must remain green.' }
}
$taskPlain=[regex]::Replace($taskBody,'</?color(?:=#[0-9A-F]{6})?>','')
if ($taskPlain -match '<[^>]+>|\*\*' -or ($taskPlain -split "`n`n").Count -ne 9) { throw 'Unsupported literal tags or missing paragraphs.' }
Write-Output 'All 12 emphasis tags accepted by the actual native color parser; no unsupported bold/Markdown tags; all nine paragraphs retained.'
