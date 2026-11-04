using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LiveSplit.ReCoFSplit
{
    /// <summary>
    /// Reads cof.exe's memory, as cryoffear.asl does, through two blocks client.dll fills and that are found by
    /// their tags: g_RcAutosplit ("ReCoF_AUTOSPLIT", version 1: pointers to the client values the script reads, the
    /// last draw time, the music name and the save state, read live) and g_RcRunTimer ("ReCoF_RUNTIMER", version 2:
    /// the run timer and the game state, written by the game every frame). The script's hw.dll addresses are not
    /// read: they only happen to follow the game's state in retail (its "loading" is the text of a light style, its
    /// "can move" the name of a physics entity), and in ReCoF they do not, so the game reports its real state.
    /// </summary>
    public sealed class GameReader : IDisposable
    {
        const int TimerSize = 128;

        // each tag is followed by its block's version
        static readonly byte[] AutosplitTag = Tag("ReCoF_AUTOSPLIT", 1);
        static readonly byte[] TimerTag = Tag("ReCoF_RUNTIMER", 2);

        Process process;
        IntPtr handle = IntPtr.Zero;
        IntPtr client = IntPtr.Zero;
        int clientSize;
        IntPtr igtPtr, musicPtr, savestatePtr;
        IntPtr timerBlock = IntPtr.Zero;
        int nextLook;

        public string Status { get; private set; } = "Waiting for Cry of Fear (cof.exe)";
        public string ReCoFVersion { get; private set; } = "";
        public bool Attached { get { return timerBlock != IntPtr.Zero; } }

        static byte[] Tag(string text, byte version)
        {
            var tag = new byte[20];
            Encoding.ASCII.GetBytes(text, 0, text.Length, tag, 0);
            tag[16] = version;
            return tag;
        }

        static string Text(byte[] b, int offset, int max)
        {
            int n = Array.IndexOf(b, (byte)0, offset, max);
            return Encoding.ASCII.GetString(b, offset, (n < 0 ? offset + max : n) - offset);
        }

        /// <summary>A sample, or null when the game is not running or not ready.</summary>
        public GameSample Read()
        {
            if (!Attach())
                return null;

            try
            {
                var s = new GameSample();
                byte[] t = ReadBytes(timerBlock, TimerSize);
                s.TimerState = BitConverter.ToInt32(t, 20);
                s.Time = BitConverter.ToDouble(t, 24);
                s.RunId = BitConverter.ToInt32(t, 32);
                s.Counting = BitConverter.ToInt32(t, 36) != 0;

                // the script's values, in the script's sense (its "loading" is 0 while a map loads)
                s.Map = Text(t, 56, 48);
                s.MenuMap = s.Map;
                s.Loading = BitConverter.ToInt32(t, 104) != 0 ? 0 : 1;
                s.Pause = BitConverter.ToInt32(t, 108);
                s.Alive = BitConverter.ToInt32(t, 112) != 0 ? 0 : 1;
                s.Cutscene = BitConverter.ToInt32(t, 116);
                s.CanMove = BitConverter.ToInt32(t, 120) != 0 ? 0 : 1;

                // live, so a map load that holds the game's frames is still seen
                s.Igt = ReadFloat(igtPtr);
                s.Music = ReadString(musicPtr, 40);
                s.SaveState = ReadFloat(savestatePtr);
                return s;
            }
            catch (Win32Exception)
            {
                Detach();
                return null;
            }
        }

        bool Attach()
        {
            if (process != null && process.HasExited)
                Detach();

            if (timerBlock != IntPtr.Zero)
                return true;

            // look for the game, its modules and the blocks once a second
            if (unchecked(Environment.TickCount - nextLook) < 0)
                return false;

            nextLook = Environment.TickCount + 1000;

            if (process == null)
            {
                Process[] found = Process.GetProcessesByName("cof");
                if (found.Length == 0)
                {
                    Status = "Waiting for Cry of Fear (cof.exe)";
                    return false;
                }

                process = found[0];
                handle = Native.OpenProcess(Native.PROCESS_VM_READ | Native.PROCESS_QUERY_INFORMATION, false, process.Id);
                if (handle == IntPtr.Zero)
                {
                    Status = "Cannot read cof.exe (run LiveSplit as the same user as the game)";
                    Detach();
                    return false;
                }
            }

            if (client == IntPtr.Zero)
            {
                FindModules();
                if (client == IntPtr.Zero)
                {
                    Status = "Cry of Fear is starting";
                    return false;
                }
            }

            try
            {
                IntPtr autosplit = Scan(client, clientSize, AutosplitTag);
                IntPtr timer = Scan(client, clientSize, TimerTag);

                if (autosplit == IntPtr.Zero || timer == IntPtr.Zero)
                {
                    Status = "No ReCoF run timer (version 2) in client.dll: an older ReCoF, or retail Cry of Fear, which keeps using cryoffear.asl";
                    return false;
                }

                igtPtr = ReadPointer(autosplit + 24);
                musicPtr = ReadPointer(autosplit + 28);
                savestatePtr = ReadPointer(autosplit + 32);

                byte[] t = ReadBytes(timer, TimerSize);
                ReCoFVersion = Text(t, 40, 16);
                timerBlock = timer;
                Status = "Connected to ReCoF " + ReCoFVersion;
                return true;
            }
            catch (Win32Exception)
            {
                Detach();
                return false;
            }
        }

        void FindModules()
        {
            var modules = new IntPtr[1024];
            int needed;

            if (!Native.EnumProcessModulesEx(handle, modules, modules.Length * IntPtr.Size, out needed, Native.LIST_MODULES_ALL))
                return;

            int count = Math.Min(needed / IntPtr.Size, modules.Length);
            var name = new StringBuilder(260);

            for (int i = 0; i < count; i++)
            {
                name.Length = 0;
                if (Native.GetModuleBaseName(handle, modules[i], name, name.Capacity) == 0)
                    continue;

                Native.MODULEINFO info;
                if (!Native.GetModuleInformation(handle, modules[i], out info, Marshal.SizeOf(typeof(Native.MODULEINFO))))
                    continue;

                string n = name.ToString().ToLowerInvariant();
                if (n == "client.dll")
                {
                    client = info.lpBaseOfDll;
                    clientSize = (int)info.SizeOfImage;
                }
            }
        }

        // the address of pattern in the module image, or zero; read in chunks that overlap by the pattern's length
        IntPtr Scan(IntPtr start, int size, byte[] pattern)
        {
            const int Chunk = 0x10000;
            var buffer = new byte[Chunk + pattern.Length];

            for (int offset = 0; offset < size; offset += Chunk)
            {
                int length = Math.Min(Chunk + pattern.Length, size - offset);
                if (length < pattern.Length)
                    break;

                IntPtr read;
                if (!Native.ReadProcessMemory(handle, start + offset, buffer, (IntPtr)length, out read))
                    continue;

                int got = (int)read;
                for (int i = 0; i + pattern.Length <= got; i++)
                {
                    int j = 0;
                    while (j < pattern.Length && buffer[i + j] == pattern[j])
                        j++;

                    if (j == pattern.Length)
                        return start + offset + i;
                }
            }

            return IntPtr.Zero;
        }

        byte[] ReadBytes(IntPtr address, int count)
        {
            var buffer = new byte[count];
            IntPtr read;

            if (address == IntPtr.Zero || !Native.ReadProcessMemory(handle, address, buffer, (IntPtr)count, out read) || (int)read != count)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return buffer;
        }

        float ReadFloat(IntPtr address) { return BitConverter.ToSingle(ReadBytes(address, 4), 0); }

        // the game is 32-bit: its pointers are 4 bytes
        IntPtr ReadPointer(IntPtr address) { return (IntPtr)(long)BitConverter.ToUInt32(ReadBytes(address, 4), 0); }

        string ReadString(IntPtr address, int max)
        {
            byte[] b = ReadBytes(address, max);
            int n = Array.IndexOf(b, (byte)0);
            return Encoding.ASCII.GetString(b, 0, n < 0 ? max : n);
        }

        void Detach()
        {
            if (handle != IntPtr.Zero)
                Native.CloseHandle(handle);

            handle = IntPtr.Zero;
            process = null;
            client = IntPtr.Zero;
            timerBlock = IntPtr.Zero;
            ReCoFVersion = "";
            Status = "Waiting for Cry of Fear (cof.exe)";
        }

        public void Dispose()
        {
            Detach();
        }

        static class Native
        {
            public const int PROCESS_VM_READ = 0x0010;
            public const int PROCESS_QUERY_INFORMATION = 0x0400;
            public const int LIST_MODULES_ALL = 0x03;

            [StructLayout(LayoutKind.Sequential)]
            public struct MODULEINFO
            {
                public IntPtr lpBaseOfDll;
                public uint SizeOfImage;
                public IntPtr EntryPoint;
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(int access, bool inherit, int processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr handle);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, IntPtr size, out IntPtr read);

            [DllImport("psapi.dll", SetLastError = true)]
            public static extern bool EnumProcessModulesEx(IntPtr process, [Out] IntPtr[] modules, int size, out int needed, int filter);

            [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int GetModuleBaseName(IntPtr process, IntPtr module, StringBuilder name, int size);

            [DllImport("psapi.dll", SetLastError = true)]
            public static extern bool GetModuleInformation(IntPtr process, IntPtr module, out MODULEINFO info, int size);
        }
    }
}
