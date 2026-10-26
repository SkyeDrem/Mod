#Requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Debug')
. (Join-Path $PSScriptRoot 'common.ps1')
$taskConfig=Get-FxDspLocalConfig -ConfigPath (Join-Path (Get-FxDspRoot) 'build.local.json')
$taskEngine=[Reflection.Assembly]::LoadFrom((Join-Path $taskConfig.GameManagedDir 'UnityEngine.CoreModule.dll'))
[Reflection.Assembly]::LoadFrom((Join-Path $taskConfig.GameManagedDir 'Assembly-CSharp.dll')) | Out-Null
$taskFile=Join-Path (Get-FxDspRoot) ("src\FXDSP.DeliveryBootstrap\bin\$Configuration\FXDSP.DeliveryBootstrap.dll")
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskFile)
$taskType=$taskAssembly.GetType('FXDSP.DeliveryBootstrap.TechnologyIcons',$true)
$taskMethod=$taskType.GetMethod('ResizeMask',[Reflection.BindingFlags]'Static,NonPublic')
$taskSize=[int]$taskType.GetField('NativeIconSize',[Reflection.BindingFlags]'Static,NonPublic').GetRawConstantValue()
$taskGame=[Reflection.Assembly]::LoadFrom((Join-Path $taskConfig.GameManagedDir 'Assembly-CSharp.dll'))
$taskNativeSize=[int]$taskGame.GetType('IconSet').GetField('kIconSize',[Reflection.BindingFlags]'Static,Public,NonPublic').GetRawConstantValue()
if($taskSize -ne $taskNativeSize -or $taskSize -ne 80){throw 'Importer does not match native IconSet texture-copy size.'}
$taskPixelType=$taskEngine.GetType('UnityEngine.Color32')
$taskWhite=[Activator]::CreateInstance($taskPixelType,[object[]]@([byte]245,[byte]245,[byte]245,[byte]255))
function Invoke-Resize($taskPixels,[int]$taskSide) {
    return ,$taskMethod.Invoke($null,[object[]]@($taskPixels,$taskSide))
}
$taskPixels=[Array]::CreateInstance($taskPixelType,360*360)
for($taskY=90;$taskY -lt 270;$taskY++) {
    for($taskX=90;$taskX -lt 270;$taskX++) { $taskPixels.SetValue($taskWhite,$taskY*360+$taskX) }
}
$taskOldCopyAlpha=0
for($taskY=0;$taskY -lt 80;$taskY++) {
    for($taskX=0;$taskX -lt 80;$taskX++){ $taskOldCopyAlpha += $taskPixels.GetValue($taskY*360+$taskX).a }
}
if($taskOldCopyAlpha -ne 0){throw 'Regression fixture does not reproduce the empty native copy.'}
$taskResult=Invoke-Resize $taskPixels 360
if($taskResult.Length -ne 6400 -or $taskResult.GetValue(40*80+40).a -ne 255 -or $taskResult.GetValue(0).a -ne 0){throw 'Native-sized output loses center or transparency.'}
$taskNonempty=@($taskResult | Where-Object {$_.a -gt 0}).Count
if($taskNonempty -lt 1500){throw 'Native copy still contains an empty icon.'}
Write-Output 'PASS actual compiled importer: formerly blank 80px copy now contains centered icon.'
$taskClear=[Array]::CreateInstance($taskPixelType,320*320)
if(@((Invoke-Resize $taskClear 320) | Where-Object {$_.a -ne 0}).Count -ne 0){throw 'Transparent input acquired artifacts.'}
Write-Output 'PASS transparency is preserved.'
$taskThin=[Array]::CreateInstance($taskPixelType,360*360)
for($taskY=100;$taskY -lt 260;$taskY++){ $taskThin.SetValue($taskWhite,$taskY*360+180) }
if(@((Invoke-Resize $taskThin 360) | Where-Object {$_.a -gt 0}).Count -lt 30){throw 'Area filter dropped thin rank strokes.'}
Write-Output 'PASS thin rank strokes survive reduction.'
Write-Output "$Configuration icon size matches actual game IconSet: ${taskSize}x${taskSize}."
