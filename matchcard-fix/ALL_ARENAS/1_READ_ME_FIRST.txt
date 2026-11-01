ALL ARENAS - EVERY WORKING MOVING MATCH CARD IN ONE ZIP
Moving types: 1on1, 2on2, triple threat, fatal 4-way. Every card <= 16 movies; all 183 cards checked.

WHAT'S INSIDE
  TITAVUSREVATCHCARD\   182 known-working cards at their exact game paths
                        (Dynamite, Collision, AAA, WWE shows/matchups, Titavius AEW 2026,
                        SummerSlam 2026, Night of Champions 2026, SmackDown + RAW 2026 V2,
                        Jericho Cruise, All In 18 + Ring of Honor 26 (ROH), SNME 41,
                        Lucha Underground,
                        AEW 2025 from titavius_aew_2025.cak at Arena\Titavius\8xx_-_AEW_..._2025\Interface\Matchup:
                        Grand Slam Australia, Double Or Nothing, Forbidden Door, All Out, WrestleDream,
                        Full Gear, Worlds End;
                        stand-ins (no card of their own; copies under the likely file names):
                        Dynasty 2025 = Dynasty 2026 card, Revolution 2025 = Revolution 2026 card,
                        Title Tuesday 2025 = Dynamite card;
                        All In 2026 NIGHT from titavius_aew_2026_extended.cak;
                        Grand Slam MX 26, Arena Mexico (CC64), Spring Break X (TxM Project Indy),
                        ECW on TNN (titavius_ecw), Clash in Italy (Rev),
                        SNME 46 (Rev), SNME XLII Last Cena (John Cena arenas 2025), MLW Fusion,
                        All Out 2019 (titavius_aew_classic), WrestleMania 24 (Rev),
                        WrestleMania 42 Saturday + Sunday (wrestlemania_42_mf),
                        WCW Nitro 2000 + Road Wild 1997 (titavius_wcw), Royal Rumble 2026 (Rev),
                        SmackDown + RAW Madison Square Garden (Rev), Backlash 2026 (Rev),
                        ROH Supercard of Honor 2026 + Double or Nothing 2026 (titavius_aew_2026))
                        + placeholder movies
  by_name\              (empty - every card is now at its exact path)
  manual\               1 card (unnamed card from UIFIXES) - its file is just "matchup.jsfb",
                        so it can't be found by name; put it in its original folder by hand
  Install_All_Match_Cards.ps1

INSTALL (clean rebuild - do this even if you installed an earlier zip)
1. Close WWE and Discovery.
2. Extract this zip anywhere (e.g. Desktop).
3. Right-click Install_All_Match_Cards.ps1 > Run with PowerShell (as Administrator).
   It RENAMES your current TITAVUSREVATCHCARD folder to TITAVUSREVATCHCARD_before_<date-time>
   (a backup - nothing is deleted) and builds a fresh one with only:
     - every card Studio's Game setup prepared in discovery_data
     - every moving card in this zip, at its exact game path
     - the placeholder movies
   No other files go into the bake (the earlier "card graphics" copy step is removed).
   At the end it prints: "Bake folder now holds: N cards, N placeholder movies, N other files".
   "other files" should be 0.
4. In mods: delete or move out EVERY old TITAVUSREVATCHCARD-*.cak.
5. Bake TITAVUSREVATCHCARD once. Exactly one new TITAVUSREVATCHCARD .cak should appear in mods.
6. Restart WWE. Test Dynamite first, then other arenas.

If PowerShell refuses: open PowerShell as Administrator, run
      Set-ExecutionPolicy -Scope Process Bypass
   then drag the .ps1 into that window and press Enter.
   NEW: it also copies each card's graphics (bg/fg movies, logos, textures that sit in the
   same folder as the card in discovery_data) into the bake, so arenas keep their graphics.
4. Any "SKIPPED" line, or the list of card folders "not in discovery_data": send it to me.
5. Bake TITAVUSREVATCHCARD once, keep only the newest TITAVUSREVATCHCARD .cak in mods, restart WWE.

All arenas Studio reported are now covered.
