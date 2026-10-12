param([switch]$Visual,[switch]$EditorOnly,[switch]$Fast,[string]$OutputPath='outputs/verify')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$checkDirectory = Join-Path $projectRoot $OutputPath
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath $OutputPath
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
# 회귀 검사(NUnit). -Fast는 UI 검사를 건너뛴다
$filter = @(); if ($Fast) { $filter = @('--filter','TestCategory!=UI') }
& $dotnet test (Join-Path $PSScriptRoot 'AABattle.Tests\AABattle.Tests.csproj') -c Release -v q --nologo @filter
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed' }
function Invoke-BuildCheck([string]$File,[string[]]$Arguments) {
    $process = Start-Process -FilePath (Join-Path $checkDirectory $File) -ArgumentList $Arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { throw "Check still running: $File (PID $($process.Id)). Inspect it before continuing." }
    if ($process.ExitCode -ne 0) { throw "Check failed: $File. See error files in $checkDirectory" }
}
if ($Visual) {
    Invoke-BuildCheck 'AABattleDataEditor.exe' @('--render-preview',"$OutputPath/catalog.png")
    if (-not $EditorOnly) {
        Invoke-BuildCheck 'AABattle.exe' @('--render-preview',"$OutputPath/battle.png")
        Invoke-BuildCheck 'AABattle.exe' @('--flow-preview',"$OutputPath/flow")
    }
    Write-Output "Editor screenshots: $checkDirectory/catalog.png, catalog.png.moves.png, catalog.png.potentials.png and catalog.png.abilities.png"
}
