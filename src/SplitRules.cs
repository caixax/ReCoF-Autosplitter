using System;
using System.Collections.Generic;

namespace LiveSplit.ReCoFSplit
{
    /// <summary>
    /// The settings that decide when to split. Their names and defaults are the ones of cryoffear.asl, so a preset
    /// splits where the script splits with the same settings.
    /// </summary>
    public sealed class SplitSettings
    {
        public bool AutoSplit = true;
        public bool SplitOnGameEnd = true;
        public bool MapChange = true;       // "Split on map change"
        public bool ChapterChange = true;   // "Split on chapter change", used when MapChange is off, as in the script
        public bool WaterGlitch = true;     // "Water glitch": no split through c_apartment4sick
        public bool UndoOnLoad = true;      // "Undo when loading the savefile"
        public bool AutoStart = true;
        public bool AutoReset = true;
        public bool WriteLog = false;       // ReCoFSplit.log next to LiveSplit.exe
        public string Preset = Presets.All[0].Name;

        public void Apply(Preset preset)
        {
            Preset = preset.Name;
            MapChange = preset.MapChange;
            ChapterChange = preset.ChapterChange;
            WaterGlitch = preset.WaterGlitch;
            UndoOnLoad = preset.UndoOnLoad;
        }
    }

    /// <summary>
    /// The split block and the save and load tracking of cryoffear.asl
    /// (https://github.com/IlyaTsyganov/Livesplit.CryofFear), rule for rule, on two samples in a row. The script
    /// splits on its ending music on every update while the music plays; here an ending splits once, when it
    /// starts, under "Split on game end".
    /// </summary>
    public sealed class SplitRules
    {
        // maps a change into does not split
        static readonly HashSet<string> CurrentExcluded = new HashSet<string>
        {
            "c_trainscene.bsp", "c_broscene.bsp", "c_intro.bsp", "c_game_menu1.bsp", "cof_campaign_01.bsp",
            "c_difficulty_settings.bsp", "c_loadgame.bsp", "",
        };

        // maps a change out of does not split
        static readonly HashSet<string> OldExcluded = new HashSet<string>
        {
            "c_intro.bsp", "c_game_menu1.bsp", "c_difficulty_settings.bsp", "c_loadgame.bsp", "",
        };

        // the first maps of the campaigns: a run starts there, so a change into one does not split
        static readonly HashSet<string> StartMaps = new HashSet<string>
        {
            "c_nightmare.bsp", "c_doc_city.bsp", "c_rumpel1.bsp", "c_hallo_iambulletproof.bsp", "c_arvuti.bsp",
            "c_the_stairway1.bsp",
        };

        static readonly HashSet<string> EndingMusic = new HashSet<string>
        {
            "endmusic1.mp3", "endmusic2.mp3", "endmusic3.mp3", "endmusic4.mp3", "coopend.mp3", "manhunt.mp3",
            "lifelover.mp3", "survive_hotel_terror.mp3", "collab_csong.mp3",
        };

        // the script's vars
        bool flag = true;       // map changes may split; off after an undo until the loaded map draws a frame
        int maps;               // map splits since the last save
        int chapters;           // chapter splits since the last save
        bool saveflag;          // a save was made in this game
        bool lakeskip;          // the Lake Skip was taken in this run
        bool chapter3 = true;   // c_park3 to c_park2 still ends chapter 3
        bool waitingForMap;     // the run started before its first map was in place

        // what the rules decide, for the log
        public Action<string> Log;

        static readonly HashSet<string> Menus = new HashSet<string>
        {
            "c_game_menu1.bsp", "c_loadgame.bsp", "c_difficulty_settings.bsp", "c_intro.bsp", "",
        };

        public struct Result
        {
            public bool Split;
            public int Undo;    // splits to undo first
        }

        /// <summary>The script's init: the game was found.</summary>
        public void OnGameAttached()
        {
            flag = true;
            maps = 0;
            chapters = 0;
        }

        /// <summary>
        /// The script's start block: a run starts. The script starts while the first map loads, so its rules never see
        /// the menu or the empty map name that come before it; ReCoF's timer can start earlier (rc_timer_start after
        /// "map c_nightmare" in a bind), and the script's load and death test would take that menu for a save being
        /// loaded and turn the map splits off. So the rules wait until the run's first map is in place, and a run
        /// starts with no splits to undo and no save made.
        /// </summary>
        public void OnRunStart()
        {
            lakeskip = false;
            chapter3 = true;
            flag = true;
            maps = 0;
            chapters = 0;
            saveflag = false;
            waitingForMap = true;
        }

        void Say(string text)
        {
            if (Log != null)
                Log(text);
        }

        public static bool IsEnding(GameSample s)
        {
            return EndingMusic.Contains(s.Music) || s.Map == "c_doc_ending.bsp";
        }

        bool MapChangeSplits(GameSample old, GameSample cur, SplitSettings settings)
        {
            if (old.Map == cur.Map || CurrentExcluded.Contains(cur.Map) || OldExcluded.Contains(old.Map) || StartMaps.Contains(cur.Map))
                return false;

            if (settings.WaterGlitch && (cur.Map == "c_apartment4sick.bsp" || old.Map == "c_apartment4sick.bsp"))
                return false;

            if (lakeskip && ((cur.Map == "c_forestday.bsp" && old.Map == "c_lake.bsp") || (cur.Map == "c_lake.bsp" && old.Map == "c_forestday.bsp")))
                return false;

            return flag;
        }

        bool ChapterSplits(GameSample old, GameSample cur)
        {
            return (cur.Map == "c_basement.bsp" && old.Map == "c_apartmentsick.bsp")       // chapter 2
                || (cur.Map == "c_city.bsp" && old.Map == "c_sewer3.bsp")                  // chapter 3
                || (chapter3 && cur.Map == "c_park2.bsp" && old.Map == "c_park3.bsp")      // chapter 4
                || (cur.Map == "c_subway2st1.bsp" && old.Map == "c_subwaysick2.bsp")       // chapter 5
                || (cur.Map == "c_bridge.bsp" && old.Map == "c_broscene.bsp")              // chapter 6
                || (cur.Map == "c_asylum1day.bsp" && old.Map == "c_attic.bsp")            // chapter 7
                || (old.Map == "c_cof_asylumday.bsp" && cur.Map == "c_cof_campaign_01_p2.bsp")  // co-op chapter 10
                || (old.Map == "c_cof_bridge.bsp" && cur.Map == "c_cof_campaign_01_p3.bsp")     // co-op chapter 11
                || (old.Map == "c_cof_city.bsp" && cur.Map == "c_cof_campaign_01_p4.bsp");      // co-op chapter 12
        }

        /// <summary>One update while a run is on. Old is the sample of the update before.</summary>
        public Result Update(GameSample old, GameSample cur, SplitSettings settings)
        {
            var result = new Result();

            if (waitingForMap)
            {
                if (Menus.Contains(cur.Map) || cur.Loading == 0 || cur.Alive == 0)
                    return result;

                waitingForMap = false;
                Say("first map in place: " + cur.Map);
            }

            // the script's gameTime block: a save made, and a load, a death or the menu that brings an older state
            if (old.SaveState != 0 && cur.SaveState == 0 && cur.Music == "Saved")
            {
                maps = 0;
                saveflag = true;
                Say("save made on " + cur.Map);
            }

            bool loadOrDeath = (cur.MenuMap == "c_loadgame.bsp" && cur.Loading != 0) || cur.Alive == 0
                || (cur.Map == "" && cur.Loading == 0) || cur.Map == "c_game_menu1.bsp";

            if (loadOrDeath)
            {
                if (maps > 0 || chapters > 0 || flag)
                    Say(string.Format("load or death (map \"{0}\" loading {1} alive {2}): undo {3}", cur.Map, cur.Loading == 0, cur.Alive != 0, settings.MapChange ? maps : chapters));

                if (settings.MapChange)
                {
                    flag = true;

                    if (maps >= 0)
                    {
                        if (settings.UndoOnLoad)
                        {
                            flag = false;
                            result.Undo += maps;
                        }

                        maps = 0;
                    }
                }
                else if (settings.ChapterChange)
                {
                    if (chapters >= 0)
                    {
                        if (settings.UndoOnLoad)
                            result.Undo += chapters;

                        chapters = 0;
                    }
                }
            }

            // the script's split block
            if (old.Map == "c_lake.bsp" && cur.Map == "c_forestday.bsp")
                lakeskip = true;

            bool ending = settings.SplitOnGameEnd && IsEnding(cur) && !IsEnding(old);

            if (old.Map != cur.Map)
                Say(string.Format("map {0} -> {1}, splits on: {2}", old.Map == "" ? "(none)" : old.Map, cur.Map == "" ? "(none)" : cur.Map, flag));

            if (ending)
                Say("ending: " + (cur.Map == "c_doc_ending.bsp" ? cur.Map : cur.Music));

            if (settings.MapChange)
            {
                if (MapChangeSplits(old, cur, settings) || ending)
                {
                    if (saveflag && flag)
                        maps++;

                    result.Split = true;
                }
                else if (old.Igt == 0 && cur.Igt > 0)
                {
                    if (!flag)
                        Say("splits on again: " + cur.Map + " drew its first frame");

                    flag = true;
                }
            }
            else if (settings.ChapterChange)
            {
                if (cur.Map == "c_buscity2.bsp" && old.Map == "c_buscity.bsp")
                    chapter3 = false;

                if (ChapterSplits(old, cur) || ending)
                {
                    if (saveflag)
                        chapters++;

                    result.Split = true;
                }
            }
            else if (ending)
            {
                result.Split = true;
            }

            return result;
        }
    }
}
