# Finger and hair findings

## Fingers: `Animation/animbanks/orion/taunts.clips`

- A bank of **193 clips**. Its header gives the count at 0x10, then 16-byte entries:
  key (u64), size (u32), offset (u32). The key is the FNV-1a 64 hash of the lowercase clip id,
  for example `37255a000`.
- Every clip decodes with Studio's `DiscoveryMotionDecoder`: 193 of 193.
- Track counts:
  - 26: body only (56 clips)
  - 64 or 73: **body plus all 38 finger bones** (137 clips)
- **114 clips were matched to taunt ids** (`NNNNNa000`). **58 of those carry full finger animation**,
  for example 37022a000, 37098a000, 37120a000, 62148a000, 62604a000 and 62637a000.
  See `taunts_bank_clip_list.txt` (id, tracks, frames, finger tracks).
  Stone Cold 1 (37255a000) is body-only here as well.
- **64 unnamed clips are 61-frame still poses** with all 38 finger bones. They use only 7 distinct
  body poses but 63 or 64 distinct hand shapes, so this looks like a **hand-pose library**.
  Measured finger bend (`hand_pose_finger_bend.txt`; low = straight, about 200+ = curled) shows
  these shapes:
  open hand, fist, point (index), middle finger, peace/V, too sweet/horns, thumbs up,
  hang loose, pinky out, four fingers with the thumb tucked, and relaxed variants.
- `Cutscene/Sequences/SimpleTaunt.jsfb` is a test sequence (crowd SFX, React_Cheer and
  TEST_Entrance_Taunt) with no hand layer.

Implication: Studio can use **real game hand shapes** from this bank for a Hand pose option.
For taunts whose bank version has finger tracks, Studio can use that version so the
fingers animate exactly as authored.

## Hair: Roman Reigns `Attire.mcd` / `Attire.ycl`

- `Attire.mcd` has 428 hair bones. That includes 21 back chains `Hair_b00`…`Hair_b20` of about 6
  segments each, edge chains `Hair_e…`, and the roots `J_Hair`, `J_Hair_b` and `J_Hair_e`.
- `Attire.ycl` is the game's physics/cloth setup, with `Ct_sim_b…` chains and numeric parameters.
- Bone chains mean a spring/secondary-motion pass in the Blender render is feasible.
  The `.ycl` values may supply the game's own stiffness and damping.
- `baseModel.mcd` holds the body (201 bones, including `J_{Thumb,Index,Middle,Ring,Pinky}F*_{L,R}`)
  and has no hair bones.

Nothing here has been rendered yet. Blender and the game were not run in this environment.
