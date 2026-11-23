# Unpack_Adefs.ps1 - writes an unpacked copy of the Move Installer's wwe.adefs
# so its layout can be checked. Uses the game's own Oodle library
# (oo2core_9_win64.dll in the WWE 2K26 folder). It only READS game files.
#
# Right-click > Run with PowerShell. Output: wwe.adefs.unpacked next to this script.

$game  = 'C:\Program Files (x86)\Steam\steamapps\common\WWE 2K26'
$adefs = Join-Path $game 'discovery\_moves\AnimSystem\wwe\wwe.adefs'
$dll   = Join-Path $game 'oo2core_9_win64.dll'
$out   = Join-Path $PSScriptRoot 'wwe.adefs.unpacked'

if (-not (Test-Path $adefs)) { Write-Host "Not found: $adefs" -ForegroundColor Red; Read-Host 'Press Enter'; exit 1 }
if (-not (Test-Path $dll))   { Write-Host "Not found: $dll" -ForegroundColor Red;   Read-Host 'Press Enter'; exit 1 }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class GorillaOodle
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibraryW(string path);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr module, string name);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate long Decompress(byte[] comp, long compLen, byte[] raw, long rawLen, int fuzz, int crc, int verbose,
        IntPtr dstBase, long dstSize, IntPtr callback, IntPtr callbackContext, IntPtr decoderMemory, long decoderMemorySize, int threadPhase);
    public static long Run(string dll, byte[] comp, byte[] raw)
    {
        IntPtr module = LoadLibraryW(dll);
        if (module == IntPtr.Zero) throw new Exception("Could not load " + dll + " (error " + Marshal.GetLastWin32Error() + ")");
        IntPtr proc = GetProcAddress(module, "OodleLZ_Decompress");
        if (proc == IntPtr.Zero) throw new Exception("OodleLZ_Decompress not found in " + dll);
        var fn = (Decompress)Marshal.GetDelegateForFunctionPointer(proc, typeof(Decompress));
        return fn(comp, comp.Length, raw, raw.Length, 1, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3);
    }
}
'@

try {
    $bytes = [System.IO.File]::ReadAllBytes($adefs)
    $tag = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4)
    $compressed = [BitConverter]::ToUInt32($bytes, 12)
    $unpacked   = [BitConverter]::ToUInt32($bytes, 20)
    if ($tag -ne '_rev' -or [System.Text.Encoding]::ASCII.GetString($bytes, 8, 4) -ne 'czis' -or $compressed -ne ($bytes.Length - 24)) {
        throw "This wwe.adefs does not have the expected header (_rev / czis / uzis)."
    }
    Write-Host "Unpacking $([math]::Round($compressed / 1MB, 1)) MB -> $([math]::Round($unpacked / 1MB, 1)) MB ..."
    $comp = New-Object byte[] $compressed
    [Array]::Copy($bytes, 24, $comp, 0, $compressed)
    $raw = New-Object byte[] $unpacked
    $done = [GorillaOodle]::Run($dll, $comp, $raw)
    if ($done -ne $unpacked) { throw "Oodle returned $done bytes, expected $unpacked." }
    [System.IO.File]::WriteAllBytes($out, $raw)
    Write-Host "Done: $out" -ForegroundColor Green
    Write-Host "Zip it with 7-Zip (it compresses well) and send the zip parts."
}
catch { Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red }
Read-Host 'Press Enter to close'
