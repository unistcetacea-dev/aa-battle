param([string]$OutputPath='outputs/desktop')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot $OutputPath
# .NET SDK(8 이상)로 빌드한다. 대상은 .NET Framework 4.8이라 실행에는 추가 설치가 필요 없다
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
foreach ($project in 'AABattle\AABattle.csproj','AABattleDataEditor\AABattleDataEditor.csproj') {
    & $dotnet build (Join-Path $PSScriptRoot $project) -c Release -o $outputDirectory -v q -nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
Write-Output "Built AABattle.exe and AABattleDataEditor.exe in $outputDirectory"
