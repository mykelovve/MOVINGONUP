# AEW match-card fix: missing match types

## What was wrong

I checked every match type in the uploaded `UI` folder (131 projects).

- 127 WWE/show match cards have movie layers for all 20 match types.
- All 101 placeholder movies (`UI/Assets/movies/gorilla_renders/ph_L##_#P.bk2`) are present.
- `AEW_Dynamite_2026.jsfb` and `AEW_Collision_2026.jsfb` have movie layers for **only 4 of the 20** match types:
  one-on-one, 2 vs 2, triple threat and five-person. Fatal four-way had none, so it showed the original photos.

## Why adding all 20 broke everything

The original MovingRenders 4.21 plugin keeps a table of only **16 match-card movies**.
When a 17th movie opens, it overwrites the first one (see `src/MatchCardCapacityPlugin/README.md`
in the source ZIP).

The working Dynamite card already had exactly 16 movies: 14 portrait clips plus the `bg` and `fg`
movies. Collision had 15 (14 clips plus `bg`). The earlier all-20 pack had 101 clips per card, so even
one-on-one lost its moving render. That matches the user's Step 2 result.
The 18-clip "add fatal four-way" pack was also over the limit, so both earlier packs are withdrawn.

**One card can only have about 14 portrait clips moving at once.** To add a match type, another one has to be taken out.

## Current test pack: `AEW_Fatal4Way_Swap_Under16.zip`

Five-person is swapped out for fatal four-way:

| Match type | Clips | Moving? |
| --- | ---: | --- |
| 1on1 | 2 | yes |
| 2on2 | 4 | yes |
| triple_threat | 3 | yes |
| fatal_4way | 4 | **yes (new)** |
| 5man | 5 | no (back to the original photo) |
| all others | – | no |

That makes 13 clips: 15 movies in total on Dynamite (with `bg`/`fg`) and 14 on Collision (with `bg`).

Each new layer is a copy of the card's own working one-on-one layer, pointed at that slot's placeholder
movie, with Studio's position, size and plugin marker. The five-person slots go back to their plain
`CAS_dummy`/`SS_dummy` state. Only the child pointers of the changed slots differ from your uploaded
files; everything else is identical.

## How to test

1. Back up your current working CAK and `UI/Projects/ShowAssets/matchup` folder.
2. Unzip over the folder that **contains** `UI`, replacing the two AEW `.jsfb` files.
3. Do **not** run Game setup.
4. Bake once in Discovery with the same entry/output filename as your working bake. Restart the game.
5. Test one-on-one first, then fatal four-way.

If something breaks, put your backup CAK back.

## Choosing other match types

Any mix of match types that adds up to 14 clips or fewer can be built. Clip counts per match type:
1on1 2, 1on2 3, 1on3 4, 1on4 5, 1on5 6, 1on6 7, 1on7 8, 2on2 4, 2on3 5, 3on3 6, 4on4 8,
5man 5, 6man 6, 8man 8, fatal_4way 4, promo_1 1, promo_2 2, triple_threat 3,
triple_threat_tag 6, fatal_4way_tag 8.

## Tools

- `tools/audit_layouts.py <folder containing UI> <project.jsfb>...` lists every match type and slot as OK or BAD.
- `tools/patch_layouts.py`: `patch(bytes, layouts)` adds movie layers and `strip(bytes, layouts)` removes them.

These were checked only on the files. The game itself hasn't been run here.
