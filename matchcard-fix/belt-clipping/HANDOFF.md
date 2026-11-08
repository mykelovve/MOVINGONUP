# Hand-off: WWE 2K26 title belt clipping (Brock Lesnar 265) + project status

Paste this file into ChatGPT first, then upload the other files it asks for.

## Setup
- Game: WWE 2K26 (PC), modded with CakeHook, Discovery, Gorilla Studio, Moving Renders 4.23.
- Character: `265_Brock_Lesnar_2003` (a custom/ported model). The folder has `BaseModel/baseModel.mcd`
  and `265_Default_Attire/265_Attire.mcd`.
- Problem: **in the game** (entrance stage, see `Brock_ingame_belt_clipping.webp`) the championship title
  worn on the waist sinks into his stomach. The strap sides vanish into his waist and the plate sits too
  deep. This is the game's title belt, not a belt in his attire, and not a Gorilla Studio render.

## What his folder contains
- Attire materials: hair_ALPHA, l_elbowpad, l_kneepad, l_wrist, shoes, underwear. **No belt mesh.**
- Base model materials: body, head, eye_l/eye_r, eyelash, teeth, mouthbag, eye_overlay, eye_caruncle.

## Findings (from reading the .mcd files with Gorilla Studio's own model reader)
`.mcd` = sections, each a 4-char tag + u32 size + data. Section lists:

| Model | Bones | Sections |
|---|---|---|
| KO 578 (stock-style 2K26) | 175 | TEXT, BONE, **GtPt**, MTL!, MBfD, LODs |
| Jeff Hardy 112 | 175 | TEXT, BONE, **AtPt**, **GtPt**, MTL!, MBfD, LODs |
| **Brock 265** | **211** | TEXT, BONE, MTL!, MBfD, LODs (**no AtPt, no GtPt**) |

1. **Skeleton differs.** Brock has 211 bones in an older or other-game layout. He has 60 bones the others don't
   (`J_Gut_Jig_C/L/R_goal/plane`, `H_Pec_L`, `H_Lat_L`, `J_Collar_RBF_*`, `J_Quad_Jig_*`, ...). He is
   missing 24 stock 2K26 helper bones: `H_Pec_L_1..3`, `H_Lat_L_1..3`, `H_Delt_L_1/2`,
   `H_Bicep_L_1`, `H_Shld_Pad_L`, `H_Neck_L_1/2` and the matching R side. Full lists are in `data/*.json` (`bones`).
2. **No AtPt section** (attach points). Jeff's AtPt has 197 records of 24 bytes after a u32 count:
   `u16 a, u16 b, u32 bone_index, float x, float y, float z, u32 0x00ffff00`. The xyz look like a
   bone-local offset. They are spread over 22 bones (J_Head 20, J_Hips 17, the elbows 15 each, J_Leg 12,
   J_Knee 11, J_Spine1 11, J_Chest 8, ...). `u16 b` runs 0..21 (a group number?) and `u16 a` runs 10..2031.
   Jeff's TEXT names include `belt_dyn_APs` and `body_limit_APs`. **Theory:** these are body collision
   points the game uses to keep belts and cloth outside the body. **All 22 bones they use exist in Brock's
   skeleton.**
3. GtPt is **not** belt related: it holds 13 facial anchor points (`_ag_0000.._ag_0012` on M_Head/M_Teeth vertices).
   Record: `u16 name_idx, u16 group_name_idx, u16 slot, u16 vertex` after a u32 count.
4. KO's TEXT has the name `belt_APs` but KO has no AtPt section either, so that name alone is not the fit data.
   **Unknown:** whether KO's title fits well in game. That would show whether AtPt matters.

## Ideas to test (cheapest first)
A. In game, put the same title on a stock 2K26 wrestler of similar size. If it fits, the problem is Brock's model.
B. Put the title on another custom model that has the 175-bone stock skeleton. If that fits, the 211-bone
   skeleton is the cause, and the fix is re-rigging Brock to the stock skeleton (a Blender job with the
   WWE2K model add-on or Gorilla Studio export).
C. Copy Jeff's AtPt section into Brock's baseModel.mcd (bone indices remapped by bone NAME to Brock's
   order, offsets scaled for his bigger body). Insert it after BONE, update any section table and offsets,
   and check that the game still loads the model. This needs the exact MCD header layout; the tag+size
   bytes sit right before each section (e.g. `47 74 50 74 6c 00 00 00` = "GtPt", size 108).
D. Last resort: edit the title belt asset itself (wider waist) or the waist scaling for that title.

## Files in this package
- `Brock_ingame_belt_clipping.webp`: the problem.
- `models/*_baseModel.mcd`: Brock (problem), Jeff (has AtPt), KO (stock-style).
- `data/*_baseModel.json`: decoded sections, bones (with world matrices), render meshes with bounding
  boxes, the names table, GtPt and AtPt records.
- `scripts/`: the scripts used. They need Gorilla Studio's `engine\blender\gorilla_mcd.pyc` on Python 3.13:
  `python3.13 export.py <engine\blender folder> <baseModel.mcd> <out.json>`.
- `studio_renderer/`: the current Studio renderer wrapper (`gr_render.py`, Test 5 with the body-hair fix) and its diff/test.

## Other project status (for context)
- Moving match cards: the **8 types** zip works (32 slots per card). 59 slots made the game fall back to default cards.
- Moving Renders breaks if `revap_plug.dlp` / `revap_studio.dlp` are active, if there are duplicate MR .dlp
  files, or if there are two TITAVUSREVATCHCARD caks. Never bypass or alter the MR license.
- Character select moving: fixed via Studio "Game setup" files + discovery_data rebake.
- Studio 1.7.33: faster taunt scanning (parallel index reads, cache sidecar, log/status files ignored by the watcher).
- Renderer Test 5: white patches on Kevin Owens = body-hair cards with no cut-out; fixed in `gr_render.py`.
  Waiting on the user to confirm. Moving hair dropped (delete `engine\hair_motion.cfg`).
- Open: face paint (Jeff Hardy, needs the paint source), Too Sweet finger pose (deferred), Ilja Dragunov
  "missing file" message (needs a screenshot).
- Code lives on GitHub `mykelovve/movingonup`, branch `claude/awesome-keller-c4gz8y`.
