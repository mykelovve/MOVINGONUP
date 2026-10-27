# AEW match-card fix: missing match types

## What was wrong

I checked every match type in the uploaded `UI` folder (131 projects).

- 127 WWE/show match cards have movie layers for all 20 match types.
- All 101 placeholder movies (`UI/Assets/movies/gorilla_renders/ph_L##_#P.bk2`) are present.
- `AEW_Dynamite_2026.jsfb` and `AEW_Collision_2026.jsfb` have movie layers for **only 4 of the 20** match types:
  one-on-one, 2 vs 2, triple threat and five-person.

Any other match type on those two cards has no movie layer, so the game shows the original photos.
That is why fatal four-way "defaulted back" while one-on-one and 2-on-2 worked.

| Match type | Dynamite / Collision before | Step 1 | Step 2 |
| --- | --- | --- | --- |
| 1on1, 2on2, triple_threat, 5man | ready | ready | ready |
| fatal_4way | **missing** | ready | ready |
| 1on2–1on7, 2on3, 3on3, 4on4, 6man, 8man, promo_1, promo_2, triple_threat_tag, fatal_4way_tag | **missing** | missing | ready |

## The two test packs

Each new layer is a copy of the card's own working one-on-one layer. It is pointed at
the correct placeholder movie for that match type and player, with the same position,
size and plugin marker that Studio uses. I compared the new fatal-four-way layer with
the WWE 2K19 card's fatal four-way: the movie file, type, autoplay flag and position/size
are identical. No other bytes in the projects changed.

- `Step1_AEW_Add_Fatal4Way.zip`: the current 4 match types plus fatal four-way.
- `Step2_AEW_All_20_Match_Types.zip`: all 20 match types.

Earlier all-layout AEW bakes stalled at loading, so test **Step 1 first**.

## How to test

1. Back up your current working CAK and your current `UI/Projects/ShowAssets/matchup` folder.
2. Unzip Step 1 over the folder that **contains** `UI`, replacing the two AEW `.jsfb` files.
3. Do **not** run Game setup. These files are already complete, and Game setup can re-import over them.
4. Bake once in Discovery with the same entry/output filename as your working bake. Restart the game.
5. Test one-on-one, then fatal four-way, on Dynamite and Collision.
6. If that works, repeat with Step 2 and go through the other match types.

If something breaks, put your backup CAK back.

## Tools

- `tools/audit_layouts.py <folder containing UI> <project.jsfb>...` lists every match type and slot as OK or BAD.
- `tools/patch_layouts.py <in.jsfb> <out.jsfb> <layout>...` adds movie layers to the named layouts.

These were checked only on the files. The game itself hasn't been run here.
