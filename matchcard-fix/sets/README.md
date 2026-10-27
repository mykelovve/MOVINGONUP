# Dynamite / Collision match-type sets

The plugin can only play about 14 portrait clips per card (see `../README.md`), so the 20 match types
are spread over 14 sets. **Every set keeps one-on-one and 2 vs 2 moving.** Install one set at a time.

| Set | Moving match types (besides 1on1 and 2on2) | Clips |
| --- | --- | ---: |
| 01 | triple threat, fatal four-way (**the version that worked**) | 13 |
| 02 | five-person, 1 vs 2 handicap | 14 |
| 03 | 3 vs 3 tag, two-portrait promo | 14 |
| 04 | six-person, one-portrait promo | 13 |
| 05 | triple-threat tag | 12 |
| 06 | 4 vs 4 tag | 14 |
| 07 | eight-person | 14 |
| 08 | fatal-four-way tag | 14 |
| 09 | 2 vs 3 handicap, triple threat | 14 |
| 10 | 1 vs 3 handicap, fatal four-way | 14 |
| 11 | 1 vs 4 handicap | 11 |
| 12 | 1 vs 5 handicap | 12 |
| 13 | 1 vs 6 handicap | 13 |
| 14 | 1 vs 7 handicap | 14 |

Every match type is covered by at least one set. Each zip holds only the two files
`UI/Projects/ShowAssets/matchup/AEW_Dynamite_2026.jsfb` and `AEW_Collision_2026.jsfb`.

## Each test

1. Extract the set into the folder that **contains** `UI` and choose Replace.
2. Don't run Game setup.
3. Bake once with your usual entry/output filename, then restart the game.
4. Try the set's match types on Dynamite and Collision. Note which ones move.

Keep a copy of the working Set 01 CAK so you can always go back to it.
Once you know which match types you play most, a single "final" card can be built from those, up to 14 clips.
