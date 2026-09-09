param([switch]$Visual)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$checkDirectory = Join-Path $projectRoot 'outputs/verify'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath 'outputs/verify'
function Invoke-BuildCheck([string]$File,[string[]]$Arguments) {
    $process = Start-Process -FilePath (Join-Path $checkDirectory $File) -ArgumentList $Arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { throw "Check still running: $File (PID $($process.Id)). Inspect it before continuing." }
    if ($process.ExitCode -ne 0) { throw "Check failed: $File. See error files in $checkDirectory" }
}
Invoke-BuildCheck 'AABattle.exe' @('--self-test','outputs/verify/battle-test.txt')
Invoke-BuildCheck 'AABattleDataEditor.exe' @('--self-test','outputs/verify/editor-test.txt')
if ($Visual) { Invoke-BuildCheck 'AABattleDataEditor.exe' @('--render-preview','outputs/verify/catalog.png') }
Get-Content -LiteralPath (Join-Path $checkDirectory 'battle-test.txt')
Get-Content -LiteralPath (Join-Path $checkDirectory 'editor-test.txt')
if ($Visual) { Write-Output "Screenshots: $checkDirectory/catalog.png.moves.png and catalog.png.potentials.png" }
