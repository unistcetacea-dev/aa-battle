param([string]$OutputPath='outputs/desktop')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot $OutputPath
# .NET SDK(8 이상)로 빌드한다. 대상은 .NET Framework 4.8이라 실행에는 추가 설치가 필요 없다
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
# 프로그램이 자기 버전을 알도록 release/windows/version.txt 첫 줄을 어셈블리 버전으로 넣는다(업데이트 확인용)
$versionFile = Join-Path $projectRoot 'release\windows\version.txt'
$versionArgs = @()
if (Test-Path $versionFile) {
    $version = ((Get-Content $versionFile -Encoding UTF8 | Where-Object { $_.Trim() -ne '' } | Select-Object -First 1) -replace '^﻿','').Trim()
    if ($version -match '^\d+(\.\d+){0,3}$') { $versionArgs = @("-p:Version=$version") }
}
foreach ($project in 'AABattle\AABattle.csproj','AABattleDataEditor\AABattleDataEditor.csproj') {
    & $dotnet build (Join-Path $PSScriptRoot $project) -c Release -o $outputDirectory -v q -nologo @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
Write-Output "Built AABattle.exe and AABattleDataEditor.exe in $outputDirectory"
