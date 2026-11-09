MatchCast 1.2.2 - Moving Renders compatible copy
================================================

WHY MOVING RENDERS STOPS WITH MATCHCAST
MatchCast patches the game's video-open function (BinkOpen) the moment the game
starts, so it can skip the intro, legal and news videos. Moving Renders needs to
patch that same function to put your renders on the match cards. Before it does,
it checks the function still has its original bytes. MatchCast always loads
first, so the check fails and Moving Renders quietly skips its hook: it loads and
signs in, but nothing moves.

WHAT THIS COPY CHANGES
One jump (5 bytes) so MatchCast no longer patches BinkOpen. Everything else in
MatchCast is unchanged: cameras, replays, score bug, posters, arena video, menus.

WHAT YOU LOSE
MatchCast's video skipping at startup (intro video, legal/warning screens),
because that feature is the BinkOpen patch. The LATEST NEWS popup skip is a
separate change and should still work.

INSTALL
1. Close the game.
2. Rename your current MatchCast.asi to MatchCast.asi.original (keep it).
3. Copy this MatchCast.asi into the same folder.
4. Start the game. After the startup screens:
   - MatchCast.log: should NOT say "watching videos" any more.
   - GorillaRenders\_status.txt: sign-in OK, and after a match card screen,
     match card slots 32 with a moving count above 0.

UNDO: delete this MatchCast.asi and rename MatchCast.asi.original back.

If MatchCast updates, this copy stops matching. Ask MatchCast's author for an
option to turn off the video-skip patch, or to leave BinkOpen alone when no skip
rules are on, so both mods work without a patched copy.

Technical: file offset 0x44AE (RVA 0x50AE), 48 85 C0 75 09 -> EB 7F 90 90 90.
Original SHA-256 960d987bbb0db8ca6a97da53d3be8978d6b97cdf28ff36faad633515ed33b649
Patched  SHA-256 cf60800d064e10142e6423c85ebf57f8b76d6ff2379bd68b226759e821d38b88
