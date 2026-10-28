# Option 2: "only play what's on screen" for match cards

## Short version (for you)

- **The plugin already skips decoding hidden match types.** When a match card loads, every
  match type's movie opens. But the plugin stops decoding the ones whose layout is hidden, as
  long as it is still *tracking* them. So "only play the match type on screen" mostly exists
  already. It breaks when the plugin loses track of movies.
- **Opening only the visible match type isn't safely possible.** When a movie opens, the game
  sets aside a picture area sized for that movie. At that moment nobody knows yet which match
  type will be shown. Swapping in a different-sized movie later is the kind of thing that
  causes green/pink pictures or crashes. I'm not building that.
- **I found a real bug that explains "it goes back to the default render".** The plugin never
  frees its tracking slot when a match-card movie closes. After enough match cards have been
  opened, the table is full of dead entries. Every new movie then lands on slot 0, overwriting
  the one before it. Untracked portraits keep the normal still picture on top, so you see the
  default render. Hidden ones are also no longer skipped. This happens with or without the
  capacity add-on. The add-on only makes the table bigger (120 instead of 16) before it fills.
  With 59-clip cards, two card views are enough to fill it.
- **What's built:** `Plugins/WWE2K26_MatchCardRelease.dlp` + `.cfg`, a small add-on that frees a
  movie's slot when the game closes that movie. It also writes `WWE2K26_MatchCardRelease.log`,
  which shows, per portrait slot, whether the plugin bound it to the card, hid the still
  picture, and sees its layout as shown or hidden. It works with the original plugin alone or
  together with the capacity add-on. It changes nothing on disk. **It has not been run on
  Windows or in WWE.** It needs your test and log.

## How the plugin handles a match-card movie (MovingRenders 4.21, SHA-256 b752da3c…)

All addresses are RVAs in the original image.

### Hooks it installs

| Detour target | Plugin function | Role |
| --- | --- | --- |
| `BinkOpen` (bink2w64) | `0xaae0` | Recognises placeholders, opens the wrestler GMR instead, registers a context |
| `BinkClose` | `0xa9c0` | Clears the **selection** table `0x13090` only; counts live handles (`0xf414a4`, peak `0xf414a8`) |
| `BinkNextFrame` | `next_entry 0x1000` → `my_bink_next 0x1040` | Binds the context to the UI node, applies the still-picture marker, selection-screen logic |
| `BinkDoFrameAsyncMulti` | `0xa540` | **Hidden-layout decode skip** |
| `BinkDoFrameAsyncWait` | `0xa390` | Completes a skipped frame (returns 1 without waiting) |

The last two targets come from `GetProcAddress(bink,"BinkDoFrameAsyncMulti"/"BinkDoFrameAsyncWait")`
(`0x3f81`/`0x3f9b`) and are verified against 17/15-byte prologues (`0xe760`/`0xe771`) before
`0xa240` writes a 14-byte `FF 25` jump. The BinkClose detour is written at `0x4187`. The flag
`0xf414a0` becomes 1 after it.

### Placeholder recognition and registration (`0xaae0`)

- Reads the opened placeholder's `FrameRate` (`+0x14`) and `FrameRateDiv` (`+0x18`). It requires
  `div % 1001 == 0` and `rate == 30000 * div/1001` (`0xb000–0xb055`).
- `m = div/1001`. For `m >= 32` (`0xb517`): `layout = (m-32) >> 3` and `player = ((m-32) & 7) + 1`.
  The player is accepted only if `player-1 < slots[layout]` (table `0xe7d0` =
  2,3,4,5,6,7,8,4,5,6,8,5,6,8,4,1,2,3,6,8). This matches Studio's `31 + 8*layout + player` encoding.
- The wrestler for that player comes from game memory: the match-setup structures at game base
  `+0x4ab3390` / `+0x4a37d80`, logical ID at `+0x334` (`0xb2a7`, `0xb4ed`). The GMR library
  entry comes from the `0x1b73e0` table (stride `0x580`, screen flags `+0x568`/`+0x56c`).
  `0xb830` then opens that GMR from memory.
- The placeholder is closed through the original-BinkClose trampoline (`0xb667`), so the game
  receives the **GMR** handle, with GMR dimensions.
- Context registration (`0xb67e–0xb764`) works under the lock `0x13068` (CRITICAL_SECTION):
  - Any entry already holding this new handle is zeroed.
  - It picks the **first entry whose handle is 0**. If none is free, it picks **entry 0**.
  - It zero-fills the 0xC0-byte entry, then writes `+0x00` handle, `+0x08` player, `+0x0C` layout.

### Context fields (stride 0xC0)

| Offset | Meaning |
| --- | --- |
| `+0x00` | Bink handle |
| `+0x08` | player (1-based) |
| `+0x0C` | layout index |
| `+0x10` | first-frame state (0→1 in `my_bink_next`, 2 under `mu_scale_test`) |
| `+0x14` | still-marker applied |
| `+0x18` | set by `0xb7f4` (selection pairing) |
| `+0x20` | game movie-player object (`note_caller 0x1dd0`, when `[obj+0x40]==handle`) |
| `+0x28` | UI node, `[0x4de0(obj)+0xd8]` |
| `+0x30` / `+0x34` | offsets of the movie scale (≈1.0762, size 1024) and translation (0, −551) found by scanning ±64 KB of node memory (`0x139d–0x1546`) |
| `+0x38` / `+0x3C` | decode-skipped flags |
| `+0x40…` | still-picture state used by `0x4ab0`: finds the `SS_dummy` instances by marker scale `1 + (8*layout+player)/2^20` and rewrites their scale so the still disappears |

### Hidden-layout skip (`0xa540`, `0xa390`)

`BinkDoFrameAsyncMulti(h, …)`: look up `h`. If its node's float pair at `node + [+0x30] − 0x54`
is (0, 0), meaning the layout is hidden, **and** another context with the **same player** and a
**different layout** is not (0, 0), it sets `+0x38`, increments `0xf41490`, and returns 1 without
decoding. `BinkDoFrameAsyncWait(h)` then sees `+0x38`, clears it and returns 1.

### The defect: contexts are never released

Only `0xaae0` writes the handle field. `0xa9c0` (BinkClose) does not touch `0x137e0`, and no
other function clears it. A closed movie's entry only gets reused if a later BinkOpen happens to
return the same heap address. Once every entry is stale, each new match-card movie is registered
at entry 0, overwriting the previous one. Every other new movie is untracked:
- no UI-node binding,
- no still-picture hiding, so the default render stays on top,
- no hidden-layout decode skip.

The capacity companion widens the table to 120 but does not release entries either.

## Why "open the GMR only when visible" was rejected

1. Visibility is unknown at `BinkOpen`. The UI node and its scale field are bound later, on the
   movie's first `BinkNextFrame`.
2. The game sizes its surface from the handle `BinkOpen` returns. Keeping the 32×32 placeholder
   and later swapping in a GMR handle would hand the game a different-sized movie. That's the
   exact failure class behind green/pink output and crashes.
3. Decode of hidden layouts is already skipped by the original. What it lacks is reliable
   tracking.

## What `WWE2K26_MatchCardRelease` does

- Waits for the exact MovingRenders 4.21 image. It checks the SHA-256 of the file on disk, the
  `MOVINGRENDERS-4.21` marker and the image size, then waits for `0xf414a0 == 1`. It reads the
  recorded Bink module (`0x13058`) and resolves `BinkClose`.
- **Byte guard:** `BinkClose` must start with `FF 25 00 00 00 00` followed by the absolute target
  `base+0xa9c0`. Otherwise it does nothing.
- Freezes every other thread and refuses while any thread's RIP is inside those 14 bytes. It then
  replaces only the 8-byte absolute target with `ReleaseThenClose`, flushes the instruction cache,
  restores protection and resumes. If anything fails, it logs and leaves the original untouched.
- `ReleaseThenClose(h)`:
  1. Enters the original lock `0x13068`.
  2. Reads the **live** table base from the original's own `lea rcx,[rip+disp]` at `0x12eb`, and
     the capacity from `cmp rcx,imm8` at `0xb6a6` (0x10 or 0x78). This works whether or not the
     capacity companion has relocated the table.
  3. Zero-fills every entry whose handle is `h`, the same thing registration does for reuse.
  4. Leaves the lock and calls the original hook `base+0xa9c0`, which closes the movie as before.
- Telemetry: once a second, using `TryEnterCriticalSection` so it never blocks the game, it
  copies the table. After releasing the lock it logs any change in:
  - occupancy and capacity,
  - the release count,
  - the plugin's skip counter `0xf41490`,
  - live Bink handles,
  - `TABLE FULL` when occupancy equals capacity,
  - one line per changed entry: layout, player, node bound, scale field found, still marker
    applied, first-frame state, and whether the layer reads shown or hidden.
- Built with Zig 0.13.0 (`source/BUILD.txt`), `-Wall -Wextra -Werror`, x86_64-windows-gnu.

### Risks and limits

- Nothing has been executed on Windows or in WWE. Wine isn't available here.
- The plugin's own internal placeholder close goes straight to the original-BinkClose trampoline,
  not through the detour, so it isn't affected. Any other mod that re-hooks `BinkClose` after the
  plugin would make the guard fail, and the companion would then stay inactive.
- An entry is cleared only when the game closes that exact handle. If the game kept a card's
  movies open across screens, entries would stay in use. The log shows that as occupancy that
  never drops.
- If the capacity companion relocates the table while this companion is mid-release, the clear
  hits the old, unused original table, which is harmless. Both companions publish with all other
  threads frozen.
- The shown/hidden test in the log copies the original's own heuristic (`node+off−0x54 == 0,0`).
  If that heuristic is wrong for a card, the log line says so, but nothing is changed.

## Open questions only your in-game log can answer

Install both `.dlp`/`.cfg` files in `Plugins` (with or without the capacity add-on), open one
match card, go back, open it again, then send `WWE2K26_MatchCardRelease.log`:

1. Does it reach `ACTIVE: closed match-card movies now release their contexts`?
2. After leaving a match card, does `contexts in use` drop back toward 0, with `released on
   close` rising? That confirms the game closes card movies, and that the stale-entry bug was
   real for your setup.
3. For a card that showed the default render: are its entries `node NOT bound`, `scale-field not
   found`, or `still-marker not applied`? Each points to a different next fix: binding through
   `note_caller`, the ±64 KB scale scan, or the marker search in `0x4ab0`.
4. Does the `plugin hidden-layout decode skips` count rise while a card is on screen? That
   confirms hidden layouts get `BinkDoFrameAsyncMulti` calls and are skipped.
5. Does `TABLE FULL` ever appear? It shouldn't with releases working.
