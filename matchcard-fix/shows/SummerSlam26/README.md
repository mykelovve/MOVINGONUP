# SummerSlam 2026 (Rev's arena) match cards

Every zip contains one file: `Arena/Rev's_Arenas/SUMMERSLAM_2026/Data/matchup.jsfb`.
The original card has no movie layers and one background movie, so up to 15 clips fit. These sets use 14 or fewer.

## About the earlier launch crash

- The earlier SummerSlam test card had 14 clips (1on1, 2on2, triple threat, five-person) plus `bg`,
  the same recipe as the working Dynamite card. It was **within** the plugin's 16-movie limit.
- The CAK received after that crash was a full 1,113-file `discovery_data.cak` with **no** SummerSlam
  card in it, so the actual SummerSlam bake has never been inspected. The cause is still unknown.

## Test order

1. **Set 00 – Original control.** This is your unchanged card, baked the same way.
   - If WWE crashes at launch with it, the crash comes from overriding this Rev arena file (bake or
     archive), **not** from movie layers. Stop and send the baked CAK and its exact filename.
   - If WWE launches, the override itself is fine. Go on to step 2.
2. **Set 01** – 1on1, 2on2, triple threat, fatal four-way. If this crashes but Set 00 didn't, the layers are involved.
3. Sets 02–14 – the same match-type rotation as Dynamite/Collision (see `../../sets/README.md`).

Each test: back up your working CAK, put the file in place, don't run Game setup, bake **only your usual
input** once with your usual entry/output filename, restart. If the game crashes at launch, put the backup CAK back.
Don't bake the whole Discovery data folder, and keep `Gorilla Studio Layout Backups` out of the bake input.
