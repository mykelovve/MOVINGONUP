ALL ARENAS - EVERY WORKING MOVING MATCH CARD IN ONE ZIP
Moving types: 1on1, 2on2, triple threat, fatal 4-way. Every card <= 16 movies; all 167 cards checked.

WHAT'S INSIDE
  TITAVUSREVATCHCARD\   152 known-working cards at their exact game paths
                        (Dynamite, Collision, AAA, WWE shows/matchups, Titavius AEW 2026,
                        SummerSlam 2026, Night of Champions 2026, SmackDown 2026 V2,
                        Jericho Cruise, All In 18 (ROH), SNME 41,
                        Lucha Underground) + placeholder movies
  by_name\              14 cards whose folder is found by file name on your PC:
                        AEW 2025 (All Out, DoN, Forbidden Door, Full Gear, Worlds End, WrestleDream),
                        ROH 26, All Out 19, WCW Nitro,
                        Grand Slam MX 26, Spring Break X, SNME 46,
                        matchup_saturday, matchup_sunday
  manual\               1 card (unnamed card from UIFIXES) - its file is just "matchup.jsfb",
                        so it can't be found by name; put it in its original folder by hand
  Install_All_Match_Cards.ps1

INSTALL
1. Close WWE and Discovery. Back up your current TITAVUSREVATCHCARD .cak (just in case).
2. Extract this zip anywhere (e.g. Desktop).
3. Right-click Install_All_Match_Cards.ps1 > Run with PowerShell (as Administrator).
   It also includes every card Studio's Game setup prepared, so nothing you set up is lost.
   If PowerShell refuses: open PowerShell as Administrator, run
      Set-ExecutionPolicy -Scope Process Bypass
   then drag the .ps1 into that window and press Enter.
   NEW: it also copies each card's graphics (bg/fg movies, logos, textures that sit in the
   same folder as the card in discovery_data) into the bake, so arenas keep their graphics.
4. Any "SKIPPED" line, or the list of card folders "not in discovery_data": send it to me.
5. Bake TITAVUSREVATCHCARD once, keep only the newest TITAVUSREVATCHCARD .cak in mods, restart WWE.

Still missing: SATURDAY NIGHT'S MAIN EVENT XLII (send its matchup .jsfb + path).
