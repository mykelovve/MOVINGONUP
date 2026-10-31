# Puts each moving card in this zip in place of its original in discovery_data
# (found by file name), keeps the original as .bak, and copies it to the same path
# in the TITAVUSREVATCHCARD bake folder. Run in PowerShell as Administrator with WWE closed.

$game  = "C:\Program Files (x86)\Steam\steamapps\common\WWE 2K26\discovery"
$src   = Join-Path $game "discovery_data"
$bake  = Join-Path $game "TITAVUSREVATCHCARD"
$cards = Join-Path $PSScriptRoot "cards"

foreach ($card in Get-ChildItem $cards -Filter *.jsfb -File) {
    $found = @(Get-ChildItem $src -Recurse -Filter $card.Name -File |
        Where-Object { $_.FullName -notlike "*Gorilla Studio Layout Backups*" })
    if ($found.Count -eq 0) { Write-Host "NOT FOUND in discovery_data: $($card.Name) - send me its folder path"; continue }
    if ($found.Count -gt 1) {
        Write-Host "MORE THAN ONE $($card.Name) - skipped, send me these paths:"
        $found | ForEach-Object { Write-Host "   $($_.FullName)" }
        continue
    }
    $target = $found[0].FullName
    $backup = $target + ".bak"
    if (-not (Test-Path $backup)) { Copy-Item $target $backup }
    Copy-Item $card.FullName $target -Force
    $rel = $target.Substring($src.Length + 1)
    $out = Join-Path $bake $rel
    New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null
    Copy-Item $card.FullName $out -Force
    Write-Host "OK  $rel"
}
Write-Host ""
Write-Host "Now bake TITAVUSREVATCHCARD, keep only the newest TITAVUSREVATCHCARD .cak in mods, restart WWE."
