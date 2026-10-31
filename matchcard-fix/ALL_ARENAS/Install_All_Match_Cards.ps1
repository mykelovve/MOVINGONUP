# One-click: fills the TITAVUSREVATCHCARD bake folder with every working moving match card.
#  1. cards Gorilla Studio prepared in discovery_data (they contain GM_clip)
#  2. the known-working cards in this zip (TITAVUSREVATCHCARD folder), same paths
#  3. the cards in by_name, placed where their original of the same name lives
#  4. the placeholder movies
# Run in PowerShell as Administrator with WWE and Discovery closed. Then bake once.

$game = "C:\Program Files (x86)\Steam\steamapps\common\WWE 2K26\discovery"
$data = Join-Path $game "discovery_data"
$bake = Join-Path $game "TITAVUSREVATCHCARD"
$here = $PSScriptRoot

if (-not (Test-Path $data)) { Write-Host "Not found: $data"; exit 1 }
New-Item -ItemType Directory -Force -Path $bake | Out-Null

# Arena nested inside UI bakes as UI/Arena/... which the game never reads.
$nested = Join-Path $bake "UI\Arena"
if (Test-Path $nested) { Write-Host "Removing wrong folder: $nested"; Remove-Item $nested -Recurse -Force }

function Copy-Card($from, $rel) {
    $out = Join-Path $bake $rel
    New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null
    Copy-Item $from $out -Force
}

# 1. Studio-prepared cards
$studio = 0
Get-ChildItem $data -Recurse -Filter *.jsfb -File |
    Where-Object { $_.FullName -notlike "*Gorilla Studio Layout Backups*" } |
    ForEach-Object {
        $text = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($_.FullName))
        if ($text.Contains("GM_clip")) { Copy-Card $_.FullName $_.FullName.Substring($data.Length + 1); $studio++ }
    }
Write-Host "Studio-prepared cards copied: $studio"

# 2. Known-working cards and placeholder movies from this zip
$zipRoot = Join-Path $here "TITAVUSREVATCHCARD"
$known = 0
Get-ChildItem $zipRoot -Recurse -File | ForEach-Object {
    Copy-Card $_.FullName $_.FullName.Substring($zipRoot.Length + 1)
    if ($_.Extension -eq ".jsfb") { $known++ }
}
Write-Host "Known-working cards copied: $known (plus placeholder movies)"

# 3. Cards placed by file name (a sub-folder name, like allin18, narrows the search)
$byName = Join-Path $here "by_name"
$placed = 0
foreach ($card in Get-ChildItem $byName -Recurse -Filter *.jsfb -File) {
    $hint = if ($card.DirectoryName -ne $byName) { Split-Path $card.DirectoryName -Leaf } else { $null }
    # RAW_2026_V2 also matches "RAW 2026 V2", "Raw2026v2", ...
    $pattern = if ($hint) { "*" + ($hint -replace "_", "*") + "*" } else { $null }
    $found = @(Get-ChildItem $data -Recurse -Filter $card.Name -File |
        Where-Object { $_.FullName -notlike "*Gorilla Studio Layout Backups*" -and
                       (-not $pattern -or $_.FullName -like $pattern) })
    if ($found.Count -ne 1) {
        Write-Host "SKIPPED $($card.Name)$(if ($hint) { " ($hint)" }): found $($found.Count) original(s) in discovery_data - send me this line"
        $found | ForEach-Object { Write-Host "    $($_.FullName)" }
        continue
    }
    $target = $found[0].FullName
    if (-not (Test-Path ($target + ".bak"))) { Copy-Item $target ($target + ".bak") }
    Copy-Item $card.FullName $target -Force
    Copy-Card $card.FullName $target.Substring($data.Length + 1)
    Write-Host "OK  $($target.Substring($data.Length + 1))"
    $placed++
}
Write-Host "Cards placed by name: $placed"
Write-Host ""
Write-Host "Done. Now bake TITAVUSREVATCHCARD once, keep only the newest TITAVUSREVATCHCARD .cak in mods, restart WWE."
