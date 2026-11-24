MOVING RENDERS 4.30 - PATCHED FOR THE 6 NOV 2026 GAME BUILD (your copy only - do not share)
=========================================================================================
Your game build (WWE2K26_x64.exe timestamp 0x6AED7585, size 0x3C760000) is newer than the two
builds Moving Renders 4.30 knows (24 Sep and 16 Oct), so it said "game version: UNKNOWN" and read
every match as "match of 0".

Changed: 12 bytes - the 24 Sep 2026 entry of the build table now holds the 6 Nov build:
  timestamp 0x6AB5B298 -> 0x6AED7585, size 0x3B559000 -> 0x3C760000,
  wrestler slot list 0x4AB3390 -> 0x4AC0530, wrestler objects 0x4A37D80 -> 0x4A44DF0
  (both addresses taken from MatchCast's own search of your game, MatchCast.log).
The 16 Oct build still works. Sign-in and everything else is unchanged.

INSTALL
 1. Close the game.
 2. In Plugins, rename WWE2K26_MovingRenders.dlp to WWE2K26_MovingRenders.dlp.430original
    (it must NOT end in .dlp).
 3. Copy this WWE2K26_MovingRenders.dlp into Plugins.
 4. Sign in with the original Gorilla Studio, start the game, play a 1v1.
 5. GorillaRenders\_status.txt should show: game version: OK, sign-in: OK,
    match of 2 and moving above 0 on the matchup screen.

UNDO: delete the patched file and rename the .430original back to WWE2K26_MovingRenders.dlp.
If sign-in shows OFF only with this file, put the original back and say so.
After the next game update this patch no longer applies.
