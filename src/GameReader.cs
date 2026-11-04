using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LiveSplit.ReCoFSplit
{
    /// <summary>
    /// Reads cof.exe's memory, as cryoffear.asl does. hw.dll values come from the offsets the script reads, which
    /// ReCoF keeps (tools/check_runner.py in the ReCoF repository fails a build that moves them). client.dll values
    /// come through two blocks client.dll fills at start-up and that are found by their tags: g_RcAutosplit
    /// ("ReCoF_AUTOSPLIT", pointers to the four values the script reads in client.dll) and g_RcRunTimer
    /// ("ReCoF_RUNTIMER", the run timer that rc_timer_start, rc_timer_stop and rc_timer_reset work).
    /// </summary>
    public sealed class GameReader : IDisposable
    {
        const int HwPause = 0x11119E4;
        const int HwLoading = 0x10D279C;
        const int HwMap = 0xF1AA15;
        const int HwMenuMap = 0x820495;
        const int HwCanMove = 0x9FDC30;
        const int HwAlive = 0x1F39F0;

        // each tag is followed by its block's version, 1
        static readonly byte[] AutosplitTag = Tag("ReCoF_AUTOSPLIT");
        static readonly byte[] TimerTag = Tag("ReCoF_RUNTIMER");

        Process process;
        IntPtr handle = IntPtr.Zero;
        IntPtr hw = IntPtr.Zero;
        IntPtr client = IntPtr.Zero;
        int clientSize;
        IntPtr cutscenePtr, igtPtr, musicPtr, savestatePtr;
        IntPtr timerBlock = IntPtr.Zero;
        int nextLook;

        public string Status { get; private set; } = "Waiting for Cry of Fear (cof.exe)";
        public string ReCoFVersion { get; private set; } = "";
        public bool Attached { get { return timerBlock != IntPtr.Zero; } }

        static byte[] Tag(string text)
        {
            var tag = new byte[20];
            Encoding.ASCII.GetBytes(text, 0, text.Length, tag, 0);
            tag[16] = 1;
            return tag;
        }

        /// <summary>A sample, or null when the game is not running or not ready.</summary>
        public GameSample Read()
        {
            if (!Attach())
                return null;

            try
            {
                var s = new GameSample();
                s.Pause = ReadFloat(hw + HwPause);
                s.Loading = ReadFloat(hw + HwLoading);
                s.CanMove = ReadFloat(hw + HwCanMove);
                s.Alive = ReadFloat(hw + HwAlive);
                s.Map = ReadString(hw + HwMap, 40);
                s.MenuMap = ReadString(hw + HwMenuMap, 40);

                s.Cutscene = ReadInt(cutscenePtr);
                s.Igt = ReadFloat(igtPtr);
                s.Music = ReadString(musicPtr, 40);
                s.SaveState = ReadFloat(savestatePtr);

                byte[] t = ReadBytes(timerBlock, 56);
                s.TimerState = BitConverter.ToInt32(t, 20);
                s.Time = BitConverter.ToDouble(t, 24);
                s.RunId = BitConverter.ToInt32(t, 32);
                s.Counting = BitConverter.ToInt32(t, 36) != 0;
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

            if (hw == IntPtr.Zero || client == IntPtr.Zero)
            {
                FindModules();
                if (hw == IntPtr.Zero || client == IntPtr.Zero)
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
                    Status = "No ReCoF run timer in client.dll (an older ReCoF or retail Cry of Fear, which keeps using cryoffear.asl)";
                    return false;
                }

                cutscenePtr = ReadPointer(autosplit + 20);
                igtPtr = ReadPointer(autosplit + 24);
                musicPtr = ReadPointer(autosplit + 28);
                savestatePtr = ReadPointer(autosplit + 32);

                byte[] t = ReadBytes(timer, 56);
                ReCoFVersion = Encoding.ASCII.GetString(t, 40, 16).TrimEnd('\0');
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
                if (n == "hw.dll")
                {
                    hw = info.lpBaseOfDll;
                }
                else if (n == "client.dll")
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
        int ReadInt(IntPtr address) { return BitConverter.ToInt32(ReadBytes(address, 4), 0); }

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
            hw = IntPtr.Zero;
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
