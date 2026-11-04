using System;
using LiveSplit.Model;
using LiveSplit.ReCoFSplit;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(ReCoFSplitFactory))]

namespace LiveSplit.ReCoFSplit
{
    public sealed class ReCoFSplitFactory : IComponentFactory
    {
        public const string Name = "ReCoF Autosplitter";

        public string ComponentName
        {
            get { return Name; }
        }

        public string Description
        {
            get { return "Autosplitter and game time for Cry of Fear on ReCoF, from the game's run timer."; }
        }

        public ComponentCategory Category
        {
            get { return ComponentCategory.Control; }
        }

        public IComponent Create(LiveSplitState state)
        {
            return new ReCoFSplitComponent(state);
        }

        // no update server while the component is private
        public string UpdateName
        {
            get { return Name; }
        }

        public string XMLURL
        {
            get { return ""; }
        }

        public string UpdateURL
        {
            get { return ""; }
        }

        public Version Version
        {
            get { return typeof(ReCoFSplitFactory).Assembly.GetName().Version; }
        }
    }
}
