using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace LiveSplit.ReCoFSplit
{
    /// <summary>
    /// Drives LiveSplit from ReCoF: the game's run timer starts, resets and times the run (LiveSplit's Game Time is
    /// the game's), and the split rules of cryoffear.asl split and undo, under the settings page's category.
    /// </summary>
    public sealed class ReCoFSplitComponent : LogicComponent
    {
        readonly LiveSplitState state;
        readonly TimerModel model;
        readonly ReCoFSplitSettings settings = new ReCoFSplitSettings();
        readonly GameReader reader = new GameReader();
        readonly SplitRules rules = new SplitRules();
        GameSample previous;

        public ReCoFSplitComponent(LiveSplitState state)
        {
            this.state = state;
            model = new TimerModel { CurrentState = state };
            rules.Log = Log;
        }

        // ReCoFSplit.log next to LiveSplit.exe, when the settings ask for it
        void Log(string text)
        {
            if (!settings.Values.WriteLog)
                return;

            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReCoFSplit.log");
                var info = new FileInfo(path);
                if (info.Exists && info.Length > 1024 * 1024)
                    info.Delete();

                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + text + Environment.NewLine);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public override string ComponentName
        {
            get { return ReCoFSplitFactory.Name; }
        }

        public override Control GetSettingsControl(LayoutMode mode)
        {
            return settings;
        }

        public override XmlNode GetSettings(XmlDocument document)
        {
            return settings.GetSettings(document);
        }

        public override void SetSettings(XmlNode node)
        {
            settings.SetSettings(node);
        }

        bool InRun
        {
            get { return state.CurrentPhase == TimerPhase.Running || state.CurrentPhase == TimerPhase.Paused; }
        }

        public override void Update(IInvalidator invalidator, LiveSplitState lsState, float width, float height, LayoutMode mode)
        {
            GameSample current = reader.Read();
            if (settings.SetStatus(reader.Status))
                Log(reader.Status);

            if (current == null)
            {
                previous = null;
                return;
            }

            if (previous == null)
            {
                // the game was just found: take its state as it is, without starting or splitting
                rules.OnGameAttached();
                previous = current;
                return;
            }

            SplitSettings s = settings.Values;

            // the game's timer starts and resets the run
            bool started = current.TimerState != 0 && (previous.TimerState == 0 || current.RunId != previous.RunId);
            bool reset = current.TimerState == 0 && previous.TimerState != 0;

            if ((reset || started) && s.AutoReset && state.CurrentPhase != TimerPhase.NotRunning)
            {
                Log(started ? "new run in the game: LiveSplit reset" : "the game's timer was reset: LiveSplit reset");
                model.Reset();
            }

            if (started && s.AutoStart && state.CurrentPhase == TimerPhase.NotRunning)
            {
                Log(string.Format("run {0} started on {1}", current.RunId, current.Map == "" ? "(no map yet)" : current.Map));
                model.Start();
                rules.OnRunStart();
            }

            // the game time is the game's, set before any split so a split takes it
            if (InRun)
            {
                if (!state.IsGameTimeInitialized)
                    model.InitializeGameTime();

                state.IsGameTimePaused = true;
                state.SetGameTime(TimeSpan.FromSeconds(current.Time));
            }

            if (s.AutoSplit && InRun)
            {
                SplitRules.Result result = rules.Update(previous, current, s);

                for (int i = 0; i < result.Undo && state.CurrentSplitIndex > 0; i++)
                {
                    Log("undo split");
                    model.UndoSplit();
                }

                if (result.Split && state.CurrentPhase == TimerPhase.Running)
                {
                    Log(string.Format("split {0} at {1:0.000} s", state.CurrentSplitIndex + 1, current.Time));
                    model.Split();
                }
            }

            previous = current;
        }

        public override void Dispose()
        {
            reader.Dispose();
            settings.Dispose();
        }
    }
}
