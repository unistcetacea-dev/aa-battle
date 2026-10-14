param([string]$OutputPath='outputs/desktop', [string]$Version='')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot $OutputPath
# .NET SDK(8 이상)로 빌드한다. 대상은 .NET Framework 4.8이라 실행에는 추가 설치가 필요 없다
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
# -Version을 주면 어셈블리 버전으로 넣는다(업데이트 확인용, publish.ps1이 사용). 주지 않으면 기본 버전
$versionArgs = @()
if ($Version -match '^\d+(\.\d+){0,3}$') { $versionArgs = @("-p:Version=$Version") }
foreach ($project in 'AABattle\AABattle.csproj','AABattleDataEditor\AABattleDataEditor.csproj') {
    & $dotnet build (Join-Path $PSScriptRoot $project) -c Release -o $outputDirectory -v q -nologo @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
Write-Output "Built AABattle.exe and AABattleDataEditor.exe in $outputDirectory"
