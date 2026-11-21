MatchCast (Nov 17 build) - Moving Renders compatible copy
=========================================================
Same one fix as before: MatchCast no longer patches the game's video-open
function (BinkOpen), so Moving Renders can hook it and your renders move on
the match card / matchup screen.

Changed: 5 bytes in MatchCast.asi (file offset 0xF2DE, RVA 0xFEDE:
48 85 C0 75 09 -> EB 7F 90 90 90). Everything else is the author's build.

You lose: SkipIntro and FastBoot (the intro / legal screen skip uses that
patch). SkipNews still works.

INSTALL
1. Close the game.
2. Rename your current MatchCast.asi to MatchCast.asi.old (it must NOT end in .asi).
3. Copy this MatchCast.asi into plugins. Keep your MatchCast.ini.
4. Optional: run Check_MatchCast.ps1 from plugins - it should say OK.
5. MatchCast.log should NOT say "watching videos". After a matchup screen,
   GorillaRenders\_status.txt: sign-in OK, slots and moving above 0.

Original SHA-256 24a6f734d704071a03c263190bfe086e3482b262cd4963297006057be6e5e0ed
Patched  SHA-256 fe5b32ea12b5e66b67610192e388b3051a786aceaaa6d4c2b26657bfee0c6fed
