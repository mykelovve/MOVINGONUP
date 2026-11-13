MatchCast 1.3.13 (Nov 12 build) - Moving Renders compatible copy
=================================================
Same one fix as the 1.2.2 copy: MatchCast no longer patches the game's
video-open function (BinkOpen), so Moving Renders can hook it and your
renders move on the match card / matchup screen again.

Changed: 5 bytes in MatchCast.asi (file offset 0x459E, RVA 0x519E:
48 85 C0 75 09 -> EB 7F 90 90 90). Everything else is the author's 1.3.13.
(Only MatchCast.asi was sent; the add-on and README are not in this zip.)

You lose: SkipIntro and FastBoot (the intro / legal screen skip uses that
patch). SkipNews still works.

INSTALL
1. Close the game.
2. Rename your current MatchCast.asi to MatchCast.asi.old (it must NOT end in .asi,
   or it still loads and breaks Moving Renders again).
3. Copy this MatchCast.asi to the same folder (plugins). Keep your MatchCast.ini.
4. Start the game; MatchCast.log should NOT say "watching videos".
   GorillaRenders\_status.txt: sign-in OK; after a matchup screen, slots 32
   and moving above 0.

Original SHA-256 519b936872e0515e563b75429feab08aeab3be07d07fc229d99539c808b38e65
Patched  SHA-256 fba24457dcab200459426f18fea6222f875f5ee4fe299ac4e1d79147bcc63b2f
