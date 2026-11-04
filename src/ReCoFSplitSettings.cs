using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.UI;

namespace LiveSplit.ReCoFSplit
{
    /// <summary>
    /// The component's settings page, laid out as BunnySplit's: an AutoSplit group and an AutoStart group. Instead of
    /// a list of maps to type, a category list sets the split rules and shows the splits of that category.
    /// </summary>
    public sealed class ReCoFSplitSettings : UserControl
    {
        public readonly SplitSettings Values = new SplitSettings();

        readonly CheckBox autoSplit = Box("Enable AutoSplit");
        readonly ComboBox category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        readonly CheckBox gameEnd = Box("Split on game end (the endings)");
        readonly CheckBox mapChange = Box("Split on map change");
        readonly CheckBox chapterChange = Box("Split on Cry of Fear chapters (when map change is off)");
        readonly CheckBox waterGlitch = Box("Water glitch: no split through c_apartment4sick");
        readonly CheckBox undoOnLoad = Box("Undo splits made since the last save when it is loaded");
        readonly ListBox splits = new ListBox { Height = 150, Width = 410, SelectionMode = SelectionMode.None };
        readonly CheckBox autoStart = Box("Enable AutoStart");
        readonly CheckBox autoReset = Box("Enable AutoReset");
        readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(430, 0) };
        bool loading;

        static CheckBox Box(string text)
        {
            return new CheckBox { Text = text, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
        }

        static FlowLayoutPanel Column()
        {
            return new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(3),
            };
        }

        public ReCoFSplitSettings()
        {
            AutoScaleMode = AutoScaleMode.Font;
            Size = new Size(460, 560);

            foreach (Preset p in Presets.All)
                category.Items.Add(p.Name);
            category.Items.Add(Presets.Custom);

            var categoryRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
            categoryRow.Controls.Add(new Label { Text = "Category:", AutoSize = true, Margin = new Padding(3, 6, 3, 0) });
            categoryRow.Controls.Add(category);

            var split = Column();
            split.Controls.AddRange(new Control[]
            {
                autoSplit, categoryRow, gameEnd, mapChange, chapterChange, waterGlitch, undoOnLoad,
                new Label { Text = "Splits of this category:", AutoSize = true, Margin = new Padding(3, 8, 3, 2) },
                splits,
            });

            var start = Column();
            start.Controls.AddRange(new Control[]
            {
                autoStart, autoReset,
                new Label
                {
                    Text = "The game's run timer drives LiveSplit's Game Time; compare against Game Time. In the game, " +
                        "rc_timer_start, rc_timer_stop and rc_timer_reset work it by hand, for example " +
                        "bind DEL \"map c_nightmare;rc_timer_reset;rc_timer_start\".",
                    AutoSize = true,
                    MaximumSize = new Size(410, 0),
                    Margin = new Padding(3, 6, 3, 2),
                },
            });

            var splitGroup = new GroupBox { Text = "AutoSplit", AutoSize = true, Dock = DockStyle.Top };
            splitGroup.Controls.Add(split);
            var startGroup = new GroupBox { Text = "AutoStart", AutoSize = true, Dock = DockStyle.Top };
            startGroup.Controls.Add(start);

            var page = Column();
            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { splitGroup, startGroup, status });
            Controls.Add(page);

            category.SelectedIndexChanged += (s, e) => OnCategory();
            foreach (CheckBox box in new[] { autoSplit, gameEnd, mapChange, chapterChange, waterGlitch, undoOnLoad, autoStart, autoReset })
                box.CheckedChanged += (s, e) => OnBox();

            Show(Values);
        }

        public void SetStatus(string text)
        {
            if (status.Text != text)
                status.Text = text;
        }

        // a category sets the split rules and lists its splits
        void OnCategory()
        {
            if (loading)
                return;

            Preset preset = Presets.Find((string)category.SelectedItem);
            if (preset != null)
                Values.Apply(preset);
            else
                Values.Preset = Presets.Custom;

            Show(Values);
        }

        // a box changed by hand: a rule that no longer matches the category makes it Custom
        void OnBox()
        {
            if (loading)
                return;

            Values.AutoSplit = autoSplit.Checked;
            Values.SplitOnGameEnd = gameEnd.Checked;
            Values.MapChange = mapChange.Checked;
            Values.ChapterChange = chapterChange.Checked;
            Values.WaterGlitch = waterGlitch.Checked;
            Values.UndoOnLoad = undoOnLoad.Checked;
            Values.AutoStart = autoStart.Checked;
            Values.AutoReset = autoReset.Checked;

            Preset preset = Presets.Find(Values.Preset);
            if (preset != null && (preset.MapChange != Values.MapChange || preset.ChapterChange != Values.ChapterChange ||
                preset.WaterGlitch != Values.WaterGlitch || preset.UndoOnLoad != Values.UndoOnLoad))
            {
                Values.Preset = Presets.Custom;
                Show(Values);
            }
        }

        void Show(SplitSettings v)
        {
            loading = true;

            autoSplit.Checked = v.AutoSplit;
            gameEnd.Checked = v.SplitOnGameEnd;
            mapChange.Checked = v.MapChange;
            chapterChange.Checked = v.ChapterChange;
            waterGlitch.Checked = v.WaterGlitch;
            undoOnLoad.Checked = v.UndoOnLoad;
            autoStart.Checked = v.AutoStart;
            autoReset.Checked = v.AutoReset;
            category.SelectedItem = Presets.Find(v.Preset) != null ? v.Preset : Presets.Custom;

            splits.BeginUpdate();
            splits.Items.Clear();
            Preset preset = Presets.Find(v.Preset);
            if (preset != null)
                splits.Items.AddRange(preset.Segments);
            else
                splits.Items.Add(v.MapChange ? "Every map change" : (v.ChapterChange ? "Every chapter" : "Only the ending"));
            splits.EndUpdate();

            loading = false;
        }

        public XmlNode GetSettings(XmlDocument document)
        {
            XmlElement parent = document.CreateElement("Settings");
            SettingsHelper.CreateSetting(document, parent, "Version", "1");
            SettingsHelper.CreateSetting(document, parent, "Category", Values.Preset);
            SettingsHelper.CreateSetting(document, parent, "AutoSplit", Values.AutoSplit);
            SettingsHelper.CreateSetting(document, parent, "SplitOnGameEnd", Values.SplitOnGameEnd);
            SettingsHelper.CreateSetting(document, parent, "SplitOnMapChange", Values.MapChange);
            SettingsHelper.CreateSetting(document, parent, "SplitOnChapterChange", Values.ChapterChange);
            SettingsHelper.CreateSetting(document, parent, "WaterGlitch", Values.WaterGlitch);
            SettingsHelper.CreateSetting(document, parent, "UndoOnLoad", Values.UndoOnLoad);
            SettingsHelper.CreateSetting(document, parent, "AutoStart", Values.AutoStart);
            SettingsHelper.CreateSetting(document, parent, "AutoReset", Values.AutoReset);
            return parent;
        }

        public void SetSettings(XmlNode node)
        {
            var e = node as XmlElement;
            if (e == null)
                return;

            Values.Preset = SettingsHelper.ParseString(e["Category"], Presets.All[0].Name);
            Values.AutoSplit = SettingsHelper.ParseBool(e["AutoSplit"], true);
            Values.SplitOnGameEnd = SettingsHelper.ParseBool(e["SplitOnGameEnd"], true);
            Values.MapChange = SettingsHelper.ParseBool(e["SplitOnMapChange"], true);
            Values.ChapterChange = SettingsHelper.ParseBool(e["SplitOnChapterChange"], true);
            Values.WaterGlitch = SettingsHelper.ParseBool(e["WaterGlitch"], true);
            Values.UndoOnLoad = SettingsHelper.ParseBool(e["UndoOnLoad"], true);
            Values.AutoStart = SettingsHelper.ParseBool(e["AutoStart"], true);
            Values.AutoReset = SettingsHelper.ParseBool(e["AutoReset"], true);
            Show(Values);
        }
    }
}
