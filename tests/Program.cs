using System;
using System.Collections.Generic;
using System.Linq;
using LiveSplit.ReCoFSplit;

// Checks SplitRules against cryoffear.asl's behavior and against the split files: every category named by maps
// must split exactly once per segment when its route is played. Run: dotnet run --project tests
static class Program
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok    " : "FAIL  ") + what);
        if (!ok)
            failures++;
    }

    // a game played one update at a time
    sealed class Sim
    {
        public readonly SplitRules Rules = new SplitRules();
        public readonly SplitSettings Settings = new SplitSettings();
        public GameSample Last = new GameSample { Map = "c_nightmare.bsp", Loading = 1, Alive = 1, Igt = 1 };
        public int Splits;
        public int Undone;

        public Sim(Preset preset)
        {
            if (preset != null)
                Settings.Apply(preset);
            Rules.OnGameAttached();
            Rules.OnRunStart();
        }

        public void Tick(Action<GameSample> change)
        {
            GameSample next = Last.Clone();
            change(next);
            SplitRules.Result r = Rules.Update(Last, next, Settings);
            Undone += r.Undo;
            Splits -= r.Undo;
            if (r.Split)
                Splits++;
            Last = next;
        }

        // a level change: the map name changes before the new map draws a frame, then it draws
        public void Go(string map)
        {
            Tick(s => { s.Map = map; s.Igt = 0; s.Loading = 0; });
            Tick(s => { s.Igt = 1; s.Loading = 1; });
            Tick(s => { });
        }

        // the run's first map, in place: the rules start applying from here
        public void Start(string map)
        {
            Last.Map = map;
            Tick(s => { });
        }

        public void Ending(string music)
        {
            Tick(s => s.Music = music);
            Tick(s => { });
        }
    }

    static string MapOf(string segment)
    {
        string name = segment.Split(new[] { "  (" }, StringSplitOptions.None)[0].Replace(".bsp", "");
        return (name.StartsWith("c_") ? name : "c_" + name) + ".bsp";
    }

    static void ReplayCategories()
    {
        foreach (Preset preset in Presets.All)
        {
            List<string> maps = preset.Segments.Select(MapOf).ToList();
            if (!preset.MapChange || maps.Any(m => !System.Text.RegularExpressions.Regex.IsMatch(m, @"^c_(c_|cof_|doc_|[a-z])")) ||
                preset.Segments.Any(s => s.Contains(" ") && !s.Contains("  (end of")))
            {
                Console.WriteLine("skip  " + preset.Name + ": segments named by title, not by map");
                continue;
            }

            var sim = new Sim(preset);
            sim.Start(maps[0]);
            foreach (string map in maps.Skip(1))
                sim.Go(map);
            sim.Ending("endmusic1.mp3");

            Check(sim.Splits == preset.Segments.Length, string.Format("{0}: {1} splits for {2} segments", preset.Name, sim.Splits, preset.Segments.Length));
        }
    }

    static void Rules()
    {
        Preset newGame = Presets.All[0];
        Preset aki = Presets.Find("All Key Items + NMG");
        Preset chapters = Presets.Find("New Game / New Game+ (chapter splits)");

        var sim = new Sim(newGame);
        sim.Start("c_apartment4.bsp");
        sim.Go("c_apartment4sick.bsp");
        sim.Go("c_apartment4.bsp");
        Check(sim.Splits == 0, "water glitch on: no split into or out of c_apartment4sick");

        sim = new Sim(aki);
        sim.Start("c_apartment4.bsp");
        sim.Go("c_apartment4sick.bsp");
        Check(sim.Splits == 1, "water glitch off (All Key Items): c_apartment4sick splits");

        sim = new Sim(newGame);
        sim.Start("c_lake.bsp");
        sim.Go("c_forestday.bsp");
        sim.Go("c_lake.bsp");
        sim.Go("c_forestday.bsp");
        Check(sim.Splits == 0, "lake skip: no split between c_lake and c_forestday once it is taken");

        sim = new Sim(newGame);
        sim.Start("c_trainride.bsp");
        sim.Go("c_broscene.bsp");
        Check(sim.Splits == 0, "no split into c_broscene");
        sim.Go("c_bridge.bsp");
        Check(sim.Splits == 1, "c_broscene to c_bridge splits");

        sim = new Sim(newGame);
        sim.Start("c_start.bsp");
        sim.Go("c_nightmare.bsp");
        Check(sim.Splits == 0, "no split into a campaign's first map");

        sim = new Sim(chapters);
        sim.Start("c_park3.bsp");
        sim.Go("c_park2.bsp");
        Check(sim.Splits == 1, "chapters: c_park3 to c_park2 ends chapter 3");
        sim.Go("c_buscity.bsp");
        sim.Go("c_buscity2.bsp");
        sim.Go("c_park3.bsp");
        sim.Go("c_park2.bsp");
        Check(sim.Splits == 1, "chapters: after c_buscity2, c_park3 to c_park2 no longer splits");
        sim.Go("c_apartmentsick.bsp");
        sim.Go("c_basement.bsp");
        Check(sim.Splits == 2, "chapters: c_apartmentsick to c_basement splits");

        // a save, two splits, a death: both splits are undone, and the load into the saved map does not split
        sim = new Sim(newGame);
        sim.Start("c_city.bsp");
        sim.Tick(s => s.SaveState = 1);
        sim.Tick(s => { s.SaveState = 0; s.Music = "Saved"; });
        sim.Go("c_city2.bsp");
        sim.Go("c_park.bsp");
        Check(sim.Splits == 2, "two splits after a save");
        sim.Tick(s => s.Alive = 0);
        Check(sim.Undone == 2 && sim.Splits == 0, "death undoes the splits made since the save");
        sim.Tick(s => { s.Alive = 1; s.Map = "c_city.bsp"; s.Igt = 0; s.Loading = 0; });
        Check(sim.Splits == 0, "the load back into the saved map does not split");
        sim.Tick(s => { s.Igt = 1; s.Loading = 1; });
        sim.Go("c_city2.bsp");
        Check(sim.Splits == 1, "the next map change splits again");

        // rc_timer_start in a bind starts the run in the menu, before c_nightmare is in place: the menu must not
        // count as a save being loaded, and the first map change must split (first in-game test, 2026-11-05)
        sim = new Sim(newGame);
        sim.Last = new GameSample { Map = "c_game_menu1.bsp", Loading = 1, Alive = 1, Igt = 5 };
        sim.Tick(s => { s.Map = ""; s.Loading = 0; });
        sim.Tick(s => { s.Map = "c_nightmare.bsp"; s.Igt = 0; s.Alive = 0; });
        sim.Tick(s => { s.Loading = 1; s.Igt = 1; s.Alive = 1; });
        sim.Go("c_start.bsp");
        Check(sim.Splits == 1 && sim.Undone == 0, "a run started from the menu splits on its first map change");

        sim = new Sim(newGame);
        sim.Start("c_ending4b.bsp");
        sim.Ending("endmusic4.mp3");
        sim.Tick(s => { });
        Check(sim.Splits == 1, "an ending splits once");

        sim = new Sim(newGame);
        sim.Settings.SplitOnGameEnd = false;
        sim.Start("c_ending4b.bsp");
        sim.Ending("endmusic4.mp3");
        Check(sim.Splits == 0, "no ending split with Split on game end off");
    }

    static int Main()
    {
        ReplayCategories();
        Rules();
        Console.WriteLine(failures == 0 ? "all passed" : failures + " failed");
        return failures == 0 ? 0 : 1;
    }
}
