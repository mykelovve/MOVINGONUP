# MovingRenders 4.21 plugin: how match-card movies are handled

These notes come from disassembling the user's `WWE2K26_MovingRenders.dlp`
(SHA-256 `b752da3c9a9dc48dc377972a7183db0194c2b3b551074f7d0f0c9766432bf75c`, the same file the
capacity companion is pinned to). The plugin binary itself is not committed here.
Addresses are RVAs.

## What it hooks

| RVA | Hooked | Role |
| --- | --- | --- |
| `0xaae0` | `BinkOpen` (dbink2w64) | Recognises the 32×32, 10-frame, 29.97 fps `ph_L##_#P.bk2` placeholders. Closes the placeholder and returns a Bink handle for the wrestler's GMR, opened from memory (`0xb830`). Registers a match-card context. |
| `0xa9c0` | a second Bink entry point (the 14-byte jump patch at `0x4187`) | Per-frame handling. |
| `0xa390`, `0xa540` | two game functions found by byte signature (`0xa240` pattern scanner) | Take a Bink handle in RCX and look it up in the context table. |
| `0x1040` (`my_bink_next`) | `BinkNextFrame`/`BinkGoto` routing | Layout/player (`mu_%dP`) selection. |

## The context table

- At `0x137e0`: **16 entries of 0xC0 bytes**. Offset `+0x00` is the Bink handle, `+0x08` the card id,
  `+0x0C` the layout, `+0x28`/`+0x30` the UI node and its property offset,
  and `+0x38`/`+0x3C` the skipped/hidden flags.
- Lookups are unrolled `cmp` chains over exactly 16 entries. Registration at `0xb6a6` stops at 16,
  so a 17th movie overwrites the first entry. The capacity companion widens all of these to 120.

## Existing "hidden layout" skip (`0xa540`)

For a tracked handle, the plugin reads the node's scale pair at `node+offset-0x54`.
If it is (0, 0), meaning that layout is hidden, and **another tracked entry on the same card with a
different layout is visible**, the plugin returns 1 without calling the game function, so the hidden
movie is skipped. This only works among movies that are in the table. With the original 16 entries,
a 101-clip card loses most of its entries, so the skip cannot work.

## Why a full card is still heavy with the companion

Every placeholder that opens is replaced with a full GMR Bink when it opens, even for layouts that are
hidden. A 1-on-1 match on a 101-clip card therefore opens about 20 GMR copies per wrestler (one in each
layout's 1P/2P slot). The game sizes its movie surfaces at open time. A likely explanation for the
reported 3% stall and green/pink portraits is the cost of opening and creating surfaces for about 100
GMR movies. This has **not been confirmed**.

## Improvement direction (not built yet)

Open a real GMR only for slots whose layout is the one being shown. Leave the other slots as their tiny
placeholder, or share one decoder per wrestler. This changes `BinkOpen` behaviour, and the game holds
on to the handle that call returns, so it has to be tested in-game step by step.

Evidence still needed:
- `Plugins\WWE2K26_MovingRenders.log` from a failing run (`verbose 1` is already set), and from the
  working 14-clip card for comparison.
- Whether the WWE cards (which already have all 20 layouts) play moving renders in fatal four-way and
  larger matches.
