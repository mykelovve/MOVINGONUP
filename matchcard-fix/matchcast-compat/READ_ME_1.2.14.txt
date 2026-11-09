MatchCast 1.2.14 - Moving Renders compatible copy
=================================================
Same one fix as the 1.2.2 copy: MatchCast no longer patches the game's
video-open function (BinkOpen), so Moving Renders can hook it and your
renders move on the match card / matchup screen again.

Changed: 5 bytes in MatchCast.asi (file offset 0x461E, RVA 0x521E:
48 85 C0 75 09 -> EB 7F 90 90 90). Everything else is the author's 1.2.14.
The optional add-on and README.txt are the author's files, unchanged.

You lose: SkipIntro and FastBoot (the intro / legal screen skip uses that
patch). SkipNews still works.

INSTALL
1. Close the game.
2. Rename your current MatchCast.asi to MatchCast.asi.old.
3. Copy this MatchCast.asi to the same folder (plugins). Keep your MatchCast.ini.
4. Start the game; MatchCast.log should NOT say "watching videos".
   GorillaRenders\_status.txt: sign-in OK; after a matchup screen, slots 32
   and moving above 0.

Original SHA-256 cd43b78228e8b44c412c9cc4de8061bf021a60892bfeba1d2e36359b26ee35ed
Patched  SHA-256 4e2d2f874c97fd3730c554a24035d8ce7887177062ae5281d9785ee14fe90e23
