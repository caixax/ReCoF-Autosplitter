# ReCoF Autosplitter

A LiveSplit component for Cry of Fear on ReCoF, as BunnySplit is for BunnymodXT. The game's run timer drives LiveSplit: it starts and resets the run, and LiveSplit's Game Time is the game's time, so the timer on the game's HUD and LiveSplit always show the same time. Splits follow the rules of the community autosplitter, `cryoffear.asl`, so existing split files keep their segments, personal bests, golds and attempts.

Download `LiveSplit.ReCoFSplit.dll` from the [latest release](https://github.com/caixax/ReCoF-Autosplitter/releases/latest). Retail Cry of Fear keeps using `cryoffear.asl`; this component is for [ReCoF](https://github.com/caixax/ReCoF).

## What it needs

- ReCoF with the run timer (`rc_timer_start`, `rc_timer_stop`, `rc_timer_reset`, `rc_hud_timer`; the ReCoF build after 0.1.5). Retail Cry of Fear keeps using `cryoffear.asl`: the component says so in its settings page when it finds no ReCoF timer.
- LiveSplit 1.8 or later on .NET Framework 4.8.1.

## Install

1. Download `LiveSplit.ReCoFSplit.dll` from the [latest release](https://github.com/caixax/ReCoF-Autosplitter/releases/latest), or build it (below). If Windows blocks the file, open its Properties and tick Unblock.
2. Copy `LiveSplit.ReCoFSplit.dll` into LiveSplit's `Components` folder.
3. In LiveSplit, Edit Layout, add Control, ReCoF Autosplitter.
4. In Edit Splits, turn off the scriptable autosplitter if `cryoffear.asl` is active, so the two do not both split.
5. Right click, Compare Against, Game Time.

The settings live in the layout file, not in the split file, so nothing in a split file changes.

## Settings

The page has the layout of BunnySplit's.

- **AutoSplit.** Enable AutoSplit, then pick a category: New Game / New Game+ (map splits), New Game / New Game+ (chapter splits), All Key Items + NMG, The Doctor's Story, Co-op, Manhunt, Memories, Halloween Collaboration, Community Collaboration, The Stairway. The category sets the split rules (the settings of that category's split file) and lists its splits; no map has to be typed. The rules can still be changed by hand, which makes the category Custom:
  - Split on game end: the endings (end music of the four endings, co-op, Manhunt, Memories, Halloween and Community Collaboration, and The Doctor's Story's last map).
  - Split on map change: every map change but the menus, the train and bridge scenes, the first maps of the campaigns, and, once the Lake Skip is taken, c_lake and c_forestday.
  - Split on Cry of Fear chapters, used when map change is off.
  - Water glitch: no split into or out of c_apartment4sick.
  - Undo splits made since the last save when it is loaded, or after a death.
- **AutoStart.** Enable AutoStart and Enable AutoReset follow the game's run timer.

## The game's timer

ReCoF counts the time `cryoffear.asl` counts: real time without pauses, loads, the menus and the cutscenes that take control away. It starts and resets only by hand, as BunnymodXT's `bxt_timer_*`, and stops by itself at the endings:

- `rc_timer_start` starts a run from zero after a reset, or goes on with a stopped one.
- `rc_timer_stop` stops the time until `rc_timer_start`.
- `rc_timer_reset` sets it to zero and stops it.

For example: `bind DEL "map c_nightmare;rc_timer_reset;rc_timer_start"`. `rc_hud_timer 1` shows the time on the left of the screen.

## How it reads the game

The component reads cof.exe's memory as `cryoffear.asl` does, about 60 times a second, through two blocks client.dll fills, found by their tags: `ReCoF_AUTOSPLIT`, version 1 (pointers to the last draw time, the music name and the save state, read live) and `ReCoF_RUNTIMER`, version 2 (the run timer, its state, time and run number, and the game state: the last map loaded, loading, paused, dead, cutscene bars, frozen, written by the game every frame). The map stays through a level change, as the script's does: the level name is empty for a frame then, which the script's load test would take for a save being loaded.

The script's hw.dll addresses are not read. They only happen to follow the game's state in retail: its "loading" is the text of light style 21, its "can move" the name of the 27th physics entity of the server's player move, its "paused" a field of player 16 in a client frame. In ReCoF they say loading or paused all the time, so the game reports its real state instead: the engine's pause flag, a map not drawn yet since it loaded, the player frozen or without speed, no health, the level name.

Differences from `cryoffear.asl`:

- The script splits on an ending on every update while its music plays; the component splits once, when the ending starts.
- The script's crash handler test (crashhandler.dll) and its death screen test on combase.dll are left out; the second reads data of a Windows file that changes between Windows builds.
- The script starts and resets while the first map of a campaign loads; the game does it on the first frame that map is drawn, since no game frame runs during a load.
- A run can start before its first map is in place (`rc_timer_start` in a bind, after `map c_nightmare`). The script's load and death test would take the menu or the empty map name before it for a save being loaded and turn the map splits off, so the rules wait for the run's first map. A run also starts with no splits to undo and no save made; the script keeps its save flag from earlier runs, so a death before any save could undo every split of the run.
- "Write what the autosplitter sees to ReCoFSplit.log" (off by default) logs the starts, resets, map changes, saves, loads, deaths, undos and splits next to LiveSplit.exe.

## Build

```
dotnet build src/ReCoFSplit.csproj -c Release -p:LiveSplitDir="C:\path\to\LiveSplit"
```

`LiveSplitDir` (or the `LIVESPLIT_DIR` environment variable) names the LiveSplit folder whose `LiveSplit.Core.dll` and `UpdateManager.dll` the component builds against. The .NET SDK and the .NET Framework 4.8.1 targeting pack are needed.

## Tests

```
dotnet run --project tests
```

The tests check the split rules against the script's cases (water glitch, Lake Skip, the train and bridge scenes, chapters, the undo after a death) and replay every category whose splits are named by maps: each must split once per segment.

## Credits

- **IlyaTsyganov** wrote `cryoffear.asl`, the Cry of Fear autosplitter whose split rules this component follows; its code is not used.
- **YaLTeR** made BunnySplit, whose settings page this one mimics, and BunnymodXT, whose timer ReCoF's follows.
- **LiveSplit** is by its contributors; the component builds against `LiveSplit.Core.dll`.

## License

MIT, see [LICENSE](LICENSE).

## Categories

`src/Presets.cs` is written by `tools/make_presets.py` from the split files of the categories:

```
python tools/make_presets.py <folder with the .lss files>
```
