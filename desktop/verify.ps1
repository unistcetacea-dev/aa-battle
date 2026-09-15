param([switch]$Visual,[switch]$EditorOnly,[string]$OutputPath='outputs/verify')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$checkDirectory = Join-Path $projectRoot $OutputPath
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath $OutputPath
function Invoke-BuildCheck([string]$File,[string[]]$Arguments) {
    $process = Start-Process -FilePath (Join-Path $checkDirectory $File) -ArgumentList $Arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { throw "Check still running: $File (PID $($process.Id)). Inspect it before continuing." }
    if ($process.ExitCode -ne 0) { throw "Check failed: $File. See error files in $checkDirectory" }
}
Invoke-BuildCheck 'AABattle.exe' @('--self-test',"$OutputPath/battle-test.txt")
Invoke-BuildCheck 'AABattleDataEditor.exe' @('--self-test',"$OutputPath/editor-test.txt")
if ($Visual) {
    Invoke-BuildCheck 'AABattleDataEditor.exe' @('--render-preview',"$OutputPath/catalog.png")
    if (-not $EditorOnly) {
        Invoke-BuildCheck 'AABattle.exe' @('--render-preview',"$OutputPath/battle.png")
        Invoke-BuildCheck 'AABattle.exe' @('--flow-preview',"$OutputPath/flow")
    }
}
Get-Content -LiteralPath (Join-Path $checkDirectory 'battle-test.txt')
Get-Content -LiteralPath (Join-Path $checkDirectory 'editor-test.txt')
if ($Visual) { Write-Output "Editor screenshots: $checkDirectory/catalog.png, catalog.png.moves.png, catalog.png.potentials.png and catalog.png.abilities.png" }
