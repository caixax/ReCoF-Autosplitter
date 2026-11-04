# ReCoF Autosplitter

A LiveSplit component for Cry of Fear on ReCoF, as BunnySplit is for BunnymodXT. The game's run timer drives LiveSplit: it starts and resets the run, and LiveSplit's Game Time is the game's time, so the timer on the game's HUD and LiveSplit always show the same time. Splits follow the rules of the community autosplitter, `cryoffear.asl`, so existing split files keep their segments, personal bests, golds and attempts.

Private for now: not published, not tested in game yet.

## What it needs

- ReCoF with the run timer (`rc_timer_start`, `rc_timer_stop`, `rc_timer_reset`, `rc_hud_timer`; the ReCoF build after 0.1.5). Retail Cry of Fear keeps using `cryoffear.asl`: the component says so in its settings page when it finds no ReCoF timer.
- LiveSplit 1.8 or later on .NET Framework 4.8.1.

## Install

1. Build it (below) or take `LiveSplit.ReCoFSplit.dll` from a build.
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

ReCoF counts the time `cryoffear.asl` counts: real time without pauses, loads, the menus and the cutscenes that take control away. It starts, resets and stops where the script does (`rc_timer_auto 1`, the default). By hand, as BunnymodXT's `bxt_timer_*`:

- `rc_timer_start` starts a run from zero after a reset, or goes on with a stopped one.
- `rc_timer_stop` stops the time until `rc_timer_start`.
- `rc_timer_reset` sets it to zero and stops it.

For example: `bind DEL "map c_nightmare;rc_timer_reset;rc_timer_start"`. `rc_hud_timer 1` shows the time on the left of the screen.

## How it reads the game

The component reads cof.exe's memory as `cryoffear.asl` does, about 60 times a second. hw.dll values come from the offsets the script reads, which ReCoF keeps (its `tools/check_runner.py` fails a build that moves them). client.dll values come through two blocks client.dll fills at start-up, found by their tags: `ReCoF_AUTOSPLIT` (pointers to the cutscene state, the decal counter, the music name and the save state) and `ReCoF_RUNTIMER` (the run timer: state, time, run number). Both are followed by their version, 1.

Differences from `cryoffear.asl`:

- The script splits on an ending on every update while its music plays; the component splits once, when the ending starts.
- The script's crash handler test (crashhandler.dll) and its death screen test on combase.dll are left out; the second reads data of a Windows file that changes between Windows builds.

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

## Categories

`src/Presets.cs` is written by `tools/make_presets.py` from the split files of the categories:

```
python tools/make_presets.py <folder with the .lss files>
```
