namespace LiveSplit.ReCoFSplit
{
    /// <summary>
    /// What the game shows the autosplitter on one update: the values cryoffear.asl reads, and ReCoF's run timer.
    /// </summary>
    public sealed class GameSample
    {
        // the cryoffear.asl values, in the script's sense: the game reports them (g_RcRunTimer), and the last draw
        // time, the music and the save state come through g_RcAutosplit
        public float Pause;         // nonzero while the game is paused
        public float Loading;       // 0 while a map loads
        public float CanMove;       // 0 while the player cannot move
        public float Alive;         // 0 on the death screen
        public float Igt;           // lastDrawTime: the client time of the last frame drawn, 0 from a map's start to its first frame
        public float SaveState;     // nonzero while a tape recorder save is pending
        public int Cutscene;        // nonzero while the cutscene bars show
        public string Map = "";     // the client's map, as "c_nightmare.bsp"
        public string MenuMap = ""; // the server's map
        public string Music = "";   // the last string at the music place of the message buffer

        // ReCoF's run timer (g_RcRunTimer in client.dll)
        public int TimerState;      // 0 reset, 1 running, 2 stopped by rc_timer_stop, 3 ended at an ending
        public double Time;         // seconds counted
        public int RunId;           // one more each time a run starts from zero
        public bool Counting;       // the time advanced on the game's last frame

        public GameSample Clone()
        {
            return (GameSample)MemberwiseClone();
        }
    }
}
