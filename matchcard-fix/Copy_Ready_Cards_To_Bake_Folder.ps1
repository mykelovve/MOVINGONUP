# Copies every match card Gorilla Studio prepared in discovery_data into the
# TITAVUSREVATCHCARD bake folder, keeping each card's exact path, plus the
# placeholder movies. Run in PowerShell (as Administrator, because the game is
# under Program Files). Close WWE and Studio first.

$game = "C:\Program Files (x86)\Steam\steamapps\common\WWE 2K26\discovery"
$src  = Join-Path $game "discovery_data"
$dst  = Join-Path $game "TITAVUSREVATCHCARD"

if (-not (Test-Path $src)) { Write-Host "Not found: $src"; exit 1 }
if (-not (Test-Path $dst)) { Write-Host "Not found: $dst"; exit 1 }

# A wrongly nested UI\Arena would bake as UI/Arena/... which the game never reads.
$nested = Join-Path $dst "UI\Arena"
if (Test-Path $nested) { Write-Host "Removing wrong folder: $nested"; Remove-Item $nested -Recurse -Force }

$count = 0
Get-ChildItem $src -Recurse -Filter *.jsfb -File |
    Where-Object { $_.FullName -notlike "*Gorilla Studio Layout Backups*" } |
    ForEach-Object {
        $text = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($_.FullName))
        # Only cards Studio gave moving layers (they contain GM_clip).
        if ($text.Contains("GM_clip")) {
            $rel = $_.FullName.Substring($src.Length + 1)
            $out = Join-Path $dst $rel
            New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null
            Copy-Item $_.FullName $out -Force
            Write-Host $rel
            $count++
        }
    }

# Placeholder movies the moving layers point to.
$movies = Join-Path $src "UI\Assets\movies\gorilla_renders"
if (Test-Path $movies) {
    $outMovies = Join-Path $dst "UI\Assets\movies\gorilla_renders"
    New-Item -ItemType Directory -Force -Path $outMovies | Out-Null
    Copy-Item (Join-Path $movies "*") $outMovies -Force
}

Write-Host ""
Write-Host "Copied $count match card(s) into $dst"
Write-Host "Now bake TITAVUSREVATCHCARD in Discovery, keep only the newest TITAVUSREVATCHCARD .cak in mods, restart WWE."
