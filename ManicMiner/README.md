# Manic Miner (Amstrad CPC) – C# / MonoGame remake

Reverse-engineered from the CPC `.dsk` and the two `.sna` snapshots in `Amstrad Source Media/`.

## Projects
- `src/ManicMiner.Remake` – **the game engine used by the player and editor**: a clean C# implementation
  (typed fields, a `Willy` state object, guardian objects, coroutine-based program flow, a 16K CPC mode-1
  screen buffer). It does not use a Z80 memory image or the disk image at run time; graphics, tables and levels are
  loaded from `Assets/`. Physics, timing (300 Hz tick, 25 ticks per frame), collision and screen layout are
  translations of the original routines.
- `src/ManicMiner.Core` – the earlier literal port of the Z80 logic (runs on a 64K `byte[]`). Kept as a reference
  oracle for the parity test; it also supplies the shared `LevelData`, `CpcSound` and `Cpc` types.
- `src/ManicMiner.Game` – MonoGame (DesktopGL) player. `dotnet run --project src/ManicMiner.Game`.
  Optional: `--level file.json` (test one cavern), `--levels folder` (play a custom campaign).
  Keys follow the original (Q/E/T/U/O/@ or ← left; W/R/Y/I/P/[ or → right; Space/bottom row/↑ jump;
  Enter pause; A/S/D/F/G music; Esc to title).
- `src/ManicMiner.Editor` – cavern editor. Tiles, guardians (drag to move), Willy start, portal,
  palette/name/air/special-handler properties, F5 test-play inside the editor. Levels are saved as JSON in
  `~/Documents/ManicMinerLevels`.  The 20 originals can be edited and are saved as custom copies.
- `verify/ManicMiner.Verify` – headless checks (`dotnet run --project verify/ManicMiner.Verify -- . all|smoke|preview|parity`).
  `parity` runs Core and Remake side by side with identical random input and compares the rendered screen, palette and
  border every tick (title, scroller, demo mode, all 20 caverns; options `ONLY=menu|game|L|L<n>`, `TICKS=`, `KEYS=none`).
- `Assets/` – extracted data (`sprites.bin`, `tiles.bin`, `levels.json`, …) plus PNG sprite sheets/previews in
  `Assets/Sheets`, `Assets/Previews`. `tools/` holds the Python extractor/disassembler that produced them.

## Expansion packs
A pack is one `.mmpack` file (JSON: Name, Author, Description, Levels) holding any number of caverns.
- **Editor**: `Ctrl+G` marks/unmarks the current cavern for the pack (`[P]` in the title), `Ctrl+E` asks for a pack
  name and exports the marked caverns (or all custom caverns if none are marked) to `~/Documents/ManicMinerPacks/`.
  `Ctrl+I` imports the next pack found on disk as editable caverns, so a pack can be revised and re-exported.
- **Game**: packs in `~/Documents/ManicMinerPacks/` or a `Packs/` folder beside the game are listed in a
  "Select game" menu at start-up (Up/Down, Enter); `F9` returns to it. `--pack file.mmpack` plays one directly.
  Share a pack by copying the file. Caverns are played in order and loop back to the first after the last.

## Fidelity notes
- Screen RAM after the first frames of cavern 1 matches the snapshot byte-for-byte except for item flash phase
  (a frame-counter parity difference). The menu differs only in animation phase (Willy/piano keys).
- The CPC font is **not** in the supplied media; `font.bin` was taken from a CPC464 ROM image
  (`/usr/share/caprice32/rom/cpc464.rom`). Replace it with your own ROM dump if needed.
- Sound is a synthesised approximation of the AY chip/firmware envelopes, not a register-exact emulation.
- Busy-wait delays (game over, level-complete, pause) are converted to 300 Hz ticks by estimate.
- Special cavern logic (Eugene, Kong, Skylab, Solar Generator, Final Barrier) is ported but only smoke-tested.
- One deliberate deviation from the original: jumping no longer refuses to start when the cell under Willy's right
  foot is empty (the original made jumps near platform edges unreliable). Keys are also latched between the game's
  keyboard scans so short taps are not lost.
- Remake dependencies on Core are limited to `LevelData`, `CpcSound` and `Cpc` (data types, sound synthesis, palette).

## Tests

`dotnet test tests/ManicMiner.Tests` runs data, screen, pack, sound and gameplay tests, plus parity checks against the Core Z80 port. The GUI (editor and menus) is not covered.
