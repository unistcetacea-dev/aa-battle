$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot 'outputs/desktop'
New-Item -ItemType Directory -Force $outputDirectory | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
Push-Location $projectRoot
try {
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /main:AABattle.Program /out:outputs\desktop\AABattle.exe /win32manifest:desktop\app.manifest /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /resource:lib\reference-data.json,data.json /resource:lib\reference-aa.json,aa.json /resource:public\fonts\HeadKasen.ttf,HeadKasen.ttf /resource:public\fonts\pokemon-bw.otf,pokemon-bw.otf desktop\BattleEngine.cs desktop\Mechanics.cs desktop\BattleApp.cs desktop\ConditionUI.cs desktop\MechanicsTests.cs desktop\DataEditor.cs
if ($LASTEXITCODE -ne 0) { throw 'Battle build failed' }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /main:AABattle.DataEditorProgram /out:outputs\desktop\AABattleDataEditor.exe /win32manifest:desktop\app.manifest /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /resource:lib\reference-data.json,data.json /resource:lib\reference-aa.json,aa.json /resource:public\fonts\HeadKasen.ttf,HeadKasen.ttf /resource:public\fonts\pokemon-bw.otf,pokemon-bw.otf desktop\BattleEngine.cs desktop\Mechanics.cs desktop\BattleApp.cs desktop\ConditionUI.cs desktop\MechanicsTests.cs desktop\DataEditor.cs
} finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw 'Data editor build failed' }
Write-Output "Built AABattle.exe and AABattleDataEditor.exe in $outputDirectory"
