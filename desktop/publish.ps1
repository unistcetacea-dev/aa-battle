param(
    [string]$Notes = '',
    [string]$Version = '',
    [switch]$NoUpload
)
# 배포: 빌드 → AABattle-<버전>.zip 생성 → GitHub Release에 zip 업로드(gh 필요).
#   - 저장소(release/windows)에는 EXE를 복사하지 않는다. zip은 outputs/publish에만 만들어지며 Git에 올리지 않는다.
#   - gh가 없거나 -NoUpload를 주면 zip만 만들고 수동 업로드 방법을 출력한다.
#   - 버전을 주지 않으면 오늘 날짜(yyyy.M.d)에 같은 날 번호(.1, .2 ...)를 붙인다.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot 'outputs\publish'
$gh = Get-Command gh -ErrorAction SilentlyContinue
# gh가 있어도 로그인하지 않았으면 업로드하지 않는다(로그인 안내 후 zip만 만든다)
if ($gh) {
    & cmd /c "gh auth status >nul 2>&1"
    if ($LASTEXITCODE -ne 0) {
        Write-Output 'gh is installed but not logged in. Run: gh auth login  (building the zip only)'
        $gh = $null
    }
}

# 이미 쓴 버전 번호(로컬 태그 + gh가 있으면 Release 태그)를 모은다
function Get-UsedTags {
    $tags = @()
    $local = & git -C $projectRoot tag --list 'v*' 2>$null
    if ($LASTEXITCODE -eq 0 -and $local) { $tags += $local }
    if ($gh) {
        $remote = & gh release list --limit 100 --json tagName --jq '.[].tagName' 2>$null
        if ($LASTEXITCODE -eq 0 -and $remote) { $tags += $remote }
    }
    return $tags
}

if ($Version -eq '') {
    $today = Get-Date -Format 'yyyy.M.d'
    $number = 1
    foreach ($tag in (Get-UsedTags)) {
        if ($tag -match ('^v' + [regex]::Escape($today) + '\.(\d+)$')) {
            $number = [Math]::Max($number, [int]$Matches[1] + 1)
        }
    }
    $Version = "$today.$number"
}
if ($Version -notmatch '^\d+(\.\d+){0,3}$') { throw "Invalid version: $Version" }

# 새 버전을 어셈블리 버전으로 넣어 빌드한다(프로그램이 자기 버전을 알아야 업데이트를 비교한다)
if (Test-Path $outputDirectory) { Remove-Item $outputDirectory -Recurse -Force }
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath 'outputs/publish' -Version $Version

# zip: EXE 2개 + 함께 쓰는 DLL 2개(.config 포함) + README (글꼴·참조 데이터는 DLL에 내장되어 있다)
$stage = Join-Path $outputDirectory 'zip-stage'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($file in 'AABattle.exe', 'AABattle.exe.config', 'AABattleDataEditor.exe', 'AABattleDataEditor.exe.config', 'AABattle.UI.dll', 'AABattle.Engine.dll') {
    Copy-Item (Join-Path $outputDirectory $file) (Join-Path $stage $file) -Force
}
# 샘플 파티(samples/*.aabdata.json)를 함께 넣는다. 업데이트는 실행 파일만 바꾸므로 받은 사람의 샘플은 덮어쓰지 않는다.
$samples = Join-Path (Split-Path $PSScriptRoot -Parent) 'samples'
if (Test-Path $samples) {
    $sampleStage = Join-Path $stage 'samples'
    New-Item -ItemType Directory -Path $sampleStage -Force | Out-Null
    Copy-Item (Join-Path $samples '*.aabdata.json') $sampleStage -Force
}
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
$readme = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'dist-readme.txt'), (New-Object System.Text.UTF8Encoding($false)))
$readme = $readme.Replace('{VERSION}', $Version).Replace('{DATE}', (Get-Date -Format 'yyyy-MM-dd'))
[System.IO.File]::WriteAllText((Join-Path $stage 'README.txt'), $readme, $utf8Bom)
$zip = Join-Path $outputDirectory "AABattle-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$size = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Output "Built $zip ($size MB)"

$tagName = "v$Version"
if ($Notes -eq '') { $Notes = "AA BATTLE $Version" }
if ($gh -and -not $NoUpload) {
    & gh release create $tagName $zip --title "AA BATTLE $Version" --notes $Notes
    if ($LASTEXITCODE -ne 0) { throw 'gh release create failed' }
    Write-Output "Released $tagName with $([System.IO.Path]::GetFileName($zip))."
} else {
    if (-not $gh) { Write-Output 'gh(GitHub CLI) is not installed, so nothing was uploaded.' }
    Write-Output ''
    Write-Output 'Upload manually:'
    Write-Output '  1. https://github.com/unistcetacea-dev/aa-battle/releases/new'
    Write-Output "  2. Choose a tag: $tagName (Create new tag on publish, target main)"
    Write-Output "  3. Release title: AA BATTLE $Version, description: $Notes"
    Write-Output "  4. Attach the zip: $zip"
    Write-Output '  5. Publish release (do not mark as pre-release)'
    Write-Output 'Or install gh (winget install GitHub.cli), run gh auth login, then re-run publish.ps1.'
}
