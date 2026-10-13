param([string]$Notes = '')
# 배포: 빌드 → release/windows에 EXE 복사 → version.txt 버전 올림 → README 빌드 기준 날짜 갱신.
# 커밋·push는 하지 않는다. 변경을 확인한 뒤 release/windows를 직접 커밋·push한다.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$release = Join-Path $projectRoot 'release\windows'
$versionFile = Join-Path $release 'version.txt'
$readmeFile = Join-Path $release 'README.txt'
$utf8 = New-Object System.Text.UTF8Encoding($false)

# 새 버전: 오늘 날짜 + 같은 날 번호(이미 오늘 버전이면 번호 +1)
$today = Get-Date -Format 'yyyy.M.d'
$version = "$today.1"
if (Test-Path $versionFile) {
    $current = (Get-Content $versionFile -Encoding UTF8 | Where-Object { $_.Trim() -ne '' } | Select-Object -First 1)
    if ($current) {
        $current = ($current -replace '^﻿','').Trim()
        # 현재 버전이 오늘 날짜 이상이면(같은 날 재배포 등) 번호만 올려서 버전이 거꾸로 가지 않게 한다
        if ($current -match '^(\d+\.\d+\.\d+)\.(\d+)$' -and ([version]"$($Matches[1]).0") -ge ([version]"$today.0")) {
            $version = "$($Matches[1]).$([int]$Matches[2] + 1)"
        }
    }
}

# 버전 파일을 먼저 쓰고 빌드해야 EXE에 새 버전이 들어간다
$text = $version + "`r`n"
if ($Notes -ne '') { $text += $Notes + "`r`n" }
[System.IO.File]::WriteAllText($versionFile, $text, $utf8)

$buildOutput = 'outputs/publish'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath $buildOutput
$built = Join-Path $projectRoot $buildOutput
foreach ($file in 'AABattle.exe','AABattleDataEditor.exe') {
    Copy-Item (Join-Path $built $file) (Join-Path $release $file) -Force
}

# README의 「빌드 기준」 날짜
if (Test-Path $readmeFile) {
    $readme = [System.IO.File]::ReadAllText($readmeFile, $utf8)
    $date = Get-Date -Format 'yyyy-MM-dd'
    $readme = [regex]::Replace($readme, '빌드 기준: \d{4}-\d{2}-\d{2}', "빌드 기준: $date")
    [System.IO.File]::WriteAllText($readmeFile, $readme, $utf8)
}
Write-Output "Published $version to $release. Review and commit/push release/windows yourself."
