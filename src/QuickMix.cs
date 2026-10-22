// QuickMix — 快捷音量合成器
// 热键唤出，自动锁定"当前前台应用"的音频会话，一条滑杆直接调它。
// .NET Framework 4.x / WinForms / 单文件，零外部依赖。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickMix
{
    #region Core Audio COM interop

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                                   [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        // --- IAudioSessionManager ---
        [PreserveSig] int GetAudioSessionControl(IntPtr audioSessionGuid, int streamFlags,
                                                 out IAudioSessionControl sessionControl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr audioSessionGuid, int streamFlags,
                                               out ISimpleAudioVolume audioVolume);
        // --- IAudioSessionManager2 ---
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
        [PreserveSig] int RegisterSessionNotification(IntPtr sessionNotification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr sessionNotification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId,
                                                   IntPtr duckNotification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int sessionCount);
        [PreserveSig] int GetSession(int sessionCount, out IAudioSessionControl session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr eventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr eventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingParam);
        [PreserveSig] int SetGroupingParam(ref Guid groupingParam, IntPtr eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr newNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr newNotifications);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        // --- IAudioSessionControl ---
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr eventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr eventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingParam);
        [PreserveSig] int SetGroupingParam(ref Guid groupingParam, IntPtr eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr newNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr newNotifications);
        // --- IAudioSessionControl2 ---
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);
        [PreserveSig] int GetProcessId(out int retVal);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, IntPtr eventContext);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
        [PreserveSig] int GetMeteringChannelCount(out int channelCount);
        [PreserveSig] int GetChannelsPeakValues(int channelCount, [Out] float[] peakValues);
        [PreserveSig] int QueryHardwareSupport(out int hardwareSupportMask);
    }

    #endregion

    #region Win32

    internal static class Win32
    {
        public const int WM_HOTKEY = 0x0312;
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int count);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        /// <summary>按 Z 序找"你刚才在用的那个有声音的应用"：跳过自己和外壳窗口。</summary>
        public static int GuessActiveAppPid(List<AudioSession> sessions)
        {
            int self = Process.GetCurrentProcess().Id;
            int best = -1;
            try
            {
                EnumWindows(delegate(IntPtr h, IntPtr lp)
                {
                    if (!IsWindowVisible(h)) return true;
                    if (GetWindowTextLength(h) <= 0) return true;

                    StringBuilder cls = new StringBuilder(128);
                    GetClassName(h, cls, cls.Capacity);
                    string c = cls.ToString();
                    if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd"
                        || c == "Windows.UI.Core.CoreWindow" || c == "ApplicationFrameWindow")
                        return true;

                    uint pid;
                    GetWindowThreadProcessId(h, out pid);
                    if ((int)pid == self) return true;

                    for (int i = 0; i < sessions.Count; i++)
                    {
                        if (sessions[i].Pid == (int)pid)
                        {
                            best = (int)pid;
                            return false;
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            return best;
        }

        public static int GetForegroundPid()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return -1;
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            return (int)pid;
        }

        /// <summary>UWP 前台窗口的宿主进程是 ApplicationFrameHost，真正的应用在子窗口里。</summary>
        public static int ResolveRealForegroundPid(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    if (!string.Equals(p.ProcessName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                        return pid;
                }
            }
            catch { return pid; }

            IntPtr host = GetForegroundWindow();
            IntPtr child = FindWindowEx(host, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
            if (child != IntPtr.Zero)
            {
                uint cpid;
                GetWindowThreadProcessId(child, out cpid);
                if (cpid != 0) return (int)cpid;
            }
            return pid;
        }

        public static string WindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }

    #endregion

    #region 会话模型

    internal sealed class AudioSession
    {
        public int Pid;
        public string Process = "";
        public string Title = "";
        public readonly List<ISimpleAudioVolume> Volumes = new List<ISimpleAudioVolume>();
        public readonly List<IAudioMeterInformation> Meters = new List<IAudioMeterInformation>();
        public float Volume = 1f;
        public bool Muted;
        public float Peak;
        public bool Active;

        public void SetVolume(float v)
        {
            if (v < 0f) v = 0f;
            if (v > 1f) v = 1f;
            Volume = v;
            for (int i = 0; i < Volumes.Count; i++)
            {
                try { Volumes[i].SetMasterVolume(v, IntPtr.Zero); } catch { }
            }
        }

        public void SetMute(bool m)
        {
            Muted = m;
            for (int i = 0; i < Volumes.Count; i++)
            {
                try { Volumes[i].SetMute(m, IntPtr.Zero); } catch { }
            }
        }

        public void RefreshReadback()
        {
            if (Volumes.Count > 0)
            {
                float v; bool m;
                if (Volumes[0].GetMasterVolume(out v) == 0) Volume = v;
                if (Volumes[0].GetMute(out m) == 0) Muted = m;
            }
            float peak = 0f;
            for (int i = 0; i < Meters.Count; i++)
            {
                float p;
                if (Meters[i].GetPeakValue(out p) == 0 && p > peak) peak = p;
            }
            Peak = peak;
        }
    }

    internal static class AudioEngine
    {
        static IMMDeviceEnumerator _enumerator;

        static IMMDeviceEnumerator Enumerator
        {
            get
            {
                if (_enumerator == null)
                    _enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                return _enumerator;
            }
        }

        public static List<AudioSession> Enumerate()
        {
            List<AudioSession> found = new List<AudioSession>();
            IMMDevice device = null;
            try
            {
                int hr = Enumerator.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out device);
                if (hr != 0 || device == null) return found;

                Guid iid = typeof(IAudioSessionManager2).GUID;
                object raw;
                hr = device.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out raw);
                if (hr != 0 || raw == null) return found;

                IAudioSessionManager2 mgr = (IAudioSessionManager2)raw;
                IAudioSessionEnumerator walker;
                if (mgr.GetSessionEnumerator(out walker) != 0 || walker == null) return found;

                int count;
                if (walker.GetCount(out count) != 0) return found;

                Dictionary<int, AudioSession> byPid = new Dictionary<int, AudioSession>();

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl ctrl;
                    if (walker.GetSession(i, out ctrl) != 0 || ctrl == null) continue;

                    IAudioSessionControl2 c2 = ctrl as IAudioSessionControl2;
                    if (c2 == null) continue;

                    // S_OK 表示这是"系统声音"会话，跳过。
                    if (c2.IsSystemSoundsSession() == 0) continue;

                    int pid;
                    if (c2.GetProcessId(out pid) != 0 || pid == 0 || pid == 4) continue;

                    int state;
                    if (c2.GetState(out state) != 0) continue;
                    if (state == 2 /*Expired*/) continue;

                    AudioSession s;
                    if (!byPid.TryGetValue(pid, out s))
                    {
                        s = new AudioSession();
                        s.Pid = pid;
                        byPid[pid] = s;
                        found.Add(s);
                    }

                    ISimpleAudioVolume vol = c2 as ISimpleAudioVolume;
                    if (vol != null) s.Volumes.Add(vol);

                    IAudioMeterInformation meter = c2 as IAudioMeterInformation;
                    if (meter != null) s.Meters.Add(meter);

                    if (state == 1 /*Active*/) s.Active = true;
                }

                for (int i = 0; i < found.Count; i++) Decorate(found[i]);
            }
            catch { }
            finally
            {
                if (device != null) { try { Marshal.ReleaseComObject(device); } catch { } }
            }
            return found;
        }

        static readonly Dictionary<int, string> _procNameCache = new Dictionary<int, string>();
        static readonly Dictionary<int, string> _procPathCache = new Dictionary<int, string>();

        public static void Decorate(AudioSession s)
        {
            string name, path = null;
            if (_procNameCache.TryGetValue(s.Pid, out name)) s.Process = name;
            else
            {
                try
                {
                    using (Process p = Process.GetProcessById(s.Pid))
                    {
                        s.Process = p.ProcessName;
                        try { path = p.MainModule.FileName; } catch { path = null; }
                        s.Title = string.IsNullOrEmpty(p.MainWindowTitle) ? p.ProcessName : p.MainWindowTitle;
                    }
                }
                catch
                {
                    s.Process = "pid " + s.Pid;
                    s.Title = s.Process;
                }
                _procNameCache[s.Pid] = s.Process;
                _procPathCache[s.Pid] = path;
            }

            if (string.IsNullOrEmpty(s.Title))
            {
                s.Title = s.Process;
                try
                {
                    using (Process p = Process.GetProcessById(s.Pid))
                        if (!string.IsNullOrEmpty(p.MainWindowTitle)) s.Title = p.MainWindowTitle;
                }
                catch { }
            }
            s.RefreshReadback();
        }

        public static Icon IconFor(int pid)
        {
            string path;
            if (_procPathCache.TryGetValue(pid, out path) && !string.IsNullOrEmpty(path))
            {
                try { return Icon.ExtractAssociatedIcon(path); } catch { }
            }
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    string exe = p.MainModule.FileName;
                    _procPathCache[pid] = exe;
                    return Icon.ExtractAssociatedIcon(exe);
                }
            }
            catch { }
            return null;
        }
    }

    #endregion

    #region 配置

    internal static class Config
    {
        public struct Hotkey
        {
            public int Mod;
            public int Vk;
            public string Text;
        }

        public static readonly Hotkey[] Presets = new Hotkey[]
        {
            new Hotkey { Mod = Win32.MOD_CONTROL | Win32.MOD_ALT, Vk = 0x56, Text = "Ctrl + Alt + V" },
            new Hotkey { Mod = Win32.MOD_CONTROL | Win32.MOD_ALT, Vk = 0x4D, Text = "Ctrl + Alt + M" },
            new Hotkey { Mod = Win32.MOD_CONTROL | Win32.MOD_ALT, Vk = 0xC0, Text = "Ctrl + Alt + `" },
            new Hotkey { Mod = Win32.MOD_WIN | Win32.MOD_ALT,     Vk = 0x56, Text = "Win + Alt + V" },
        };

        public static int HotkeyIndex = 0;

        public static int HotkeyMod { get { return Presets[HotkeyIndex].Mod; } }
        public static int HotkeyVk { get { return Presets[HotkeyIndex].Vk; } }
        public static string HotkeyText { get { return Presets[HotkeyIndex].Text; } }

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickMix"); }
        }

        static string FilePath
        {
            get { return Path.Combine(Dir, "settings.ini"); }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    if (k == "hotkey")
                    {
                        int idx;
                        if (int.TryParse(v, out idx) && idx >= 0 && idx < Presets.Length) HotkeyIndex = idx;
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, "hotkey=" + HotkeyIndex + Environment.NewLine);
            }
            catch { }
        }

        public static bool AutoStart
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (k == null) return false;
                    object v = k.GetValue("QuickMix");
                    return v != null;
                }
            }
            set
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (value) k.SetValue("QuickMix", "\"" + Application.ExecutablePath + "\" --tray");
                    else k.DeleteValue("QuickMix", false);
                }
            }
        }
    }

    #endregion

    #region 界面

    internal static class Theme
    {
        public static float S = 1f;

        public static readonly Color Bg = Color.FromArgb(0x16, 0x18, 0x1D);
        public static readonly Color Card = Color.FromArgb(0x1C, 0x20, 0x27);
        public static readonly Color CardHot = Color.FromArgb(0x23, 0x28, 0x32);
        public static readonly Color Line = Color.FromArgb(0x2C, 0x31, 0x3B);
        public static readonly Color Track = Color.FromArgb(0x2E, 0x33, 0x3E);
        public static readonly Color Text = Color.FromArgb(0xEC, 0xEE, 0xF3);
        public static readonly Color TextDim = Color.FromArgb(0x8A, 0x92, 0xA1);
        public static readonly Color TextFaint = Color.FromArgb(0x5E, 0x66, 0x73);
        public static readonly Color AccentA = Color.FromArgb(0x4D, 0x7C, 0xFF);
        public static readonly Color AccentB = Color.FromArgb(0xA8, 0x6B, 0xFF);

        public static Font FHead, FName, FSub, FPct, FHint, FBtn;

        public static void Init(float scale)
        {
            if (scale < 1f) scale = 1f;
            S = scale;
            if (FHead != null) return;
            FHead = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            FName = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
            FPct = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold);
            FSub = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
            FHint = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
            FBtn = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
        }

        public static int Px(float v) { return (int)Math.Round(v * S); }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad <= 0.5f || r.Width <= 0f || r.Height <= 0f)
            {
                p.AddRectangle(r);
                return p;
            }
            float d = rad * 2f;
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        public static string Fit(Graphics g, string s, Font f, float maxW)
        {
            if (string.IsNullOrEmpty(s) || maxW <= 12f) return "";
            if (g.MeasureString(s, f).Width <= maxW) return s;
            int n = s.Length;
            while (n > 1 && g.MeasureString(s.Substring(0, n) + "\u2026", f).Width > maxW) n--;
            return s.Substring(0, n) + "\u2026";
        }

        public static Icon Mark()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Pen p = new Pen(Color.FromArgb(255, 0x4D, 0x7C, 0xFF), 3f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLine(p, 7, 18, 7, 14);
                    g.DrawLine(p, 14, 24, 14, 8);
                    g.DrawLine(p, 21, 20, 21, 12);
                }
                using (Pen p2 = new Pen(Color.FromArgb(255, 0xA8, 0x6B, 0xFF), 3f))
                {
                    p2.StartCap = LineCap.Round;
                    p2.EndCap = LineCap.Round;
                    g.DrawLine(p2, 28, 18, 28, 14);
                }
            }
            IntPtr h = bmp.GetHicon();
            using (Icon tmp = Icon.FromHandle(h))
            {
                Icon clone = (Icon)tmp.Clone();
                bmp.Dispose();
                return clone;
            }
        }
    }

    /// <summary>一行 = 一个应用：图标 / 名字 / 音量 / 滑条。</summary>
    internal sealed class AudioRow : Control
    {
        public readonly AudioSession Session;
        public bool IsForeground;

        readonly Icon _icon;
        bool _hot, _overSlider, _overMute, _dragging;
        float _shot;
        float _lastVol = -1f;
        bool _lastMute;

        public AudioRow(AudioSession s)
        {
            Session = s;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Theme.Bg;
            Height = Theme.Px(72);
            try { _icon = AudioEngine.IconFor(s.Pid); } catch { _icon = null; }
        }

        public void SyncPeak(float peak)
        {
            if (peak > _shot) _shot += (peak - _shot) * 0.75f;
            else _shot *= 0.80f;
            if (_shot < 0.002f) _shot = 0f;
        }

        /// <summary>按需重绘：只有音量/静音变了才整行重画，电平动只刷那一条。</summary>
        public void RefreshVisual()
        {
            float v = Session.Volume;
            bool m = Session.Muted;
            bool volChanged = Math.Abs(v - _lastVol) > 0.0005f || m != _lastMute;
            _lastVol = v;
            _lastMute = m;

            float before = _shot;
            SyncPeak(Session.Peak);

            if (volChanged)
            {
                Invalidate();
                return;
            }
            if (Math.Abs(_shot - before) > 0.0025f) Invalidate(MeterBox);
        }

        Rectangle MeterBox
        {
            get
            {
                Rectangle c = CardRect;
                return new Rectangle(Theme.Px(12) - 2, c.Y + Theme.Px(38) - 2,
                                     Math.Max(10, c.Width - Theme.Px(24)) + 4, Theme.Px(3) + 4);
            }
        }

        Rectangle CardRect
        {
            get { return new Rectangle(1, Theme.Px(3), Math.Max(40, Width - 3), Height - Theme.Px(9)); }
        }

        Rectangle MuteRect
        {
            get { Rectangle c = CardRect; return new Rectangle(c.Right - Theme.Px(8) - Theme.Px(22), c.Y + Theme.Px(8), Theme.Px(22), Theme.Px(22)); }
        }

        RectangleF TrackRect
        {
            get { Rectangle c = CardRect; return new RectangleF(Theme.Px(12), c.Y + Theme.Px(46), Math.Max(20, c.Width - Theme.Px(24)), Theme.Px(6)); }
        }

        static bool Hit(RectangleF r, int padX, int padY, Point p)
        {
            return p.X >= r.X - padX && p.X <= r.Right + padX && p.Y >= r.Y - padY && p.Y <= r.Bottom + padY;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool muted = Session.Muted;
            Rectangle card = CardRect;

            using (GraphicsPath p = Theme.Round(card, Theme.Px(12)))
            {
                using (SolidBrush b = new SolidBrush(_hot ? Theme.CardHot : Theme.Card))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.Line, 1f))
                    g.DrawPath(pen, p);
            }

            int ix = Theme.Px(12);
            Rectangle iconR = new Rectangle(ix, card.Y + Theme.Px(9), Theme.Px(24), Theme.Px(24));
            if (_icon != null)
            {
                try
                {
                    using (Icon sized = new Icon(_icon, iconR.Size))
                        g.DrawIcon(sized, iconR);
                }
                catch { }
            }
            else
            {
                using (GraphicsPath gp = Theme.Round(iconR, Theme.Px(7)))
                using (SolidBrush gb = new SolidBrush(Theme.Line))
                    g.FillPath(gb, gp);
            }

            Rectangle muteR = MuteRect;
            string pct = muted ? "\u9759\u97f3" : ((int)Math.Round(Session.Volume * 100)) + "%";
            float pctW = g.MeasureString(pct, Theme.FPct).Width;
            float pctX = muteR.X - Theme.Px(10) - pctW;
            using (SolidBrush pb = new SolidBrush(muted ? Theme.TextFaint : Theme.Text))
                g.DrawString(pct, Theme.FPct, pb, pctX, card.Y + Theme.Px(7));

            float nameX = iconR.Right + Theme.Px(9);
            float nameMax = pctX - nameX - Theme.Px(8);
            string title = Session.Title;
            if (string.IsNullOrEmpty(title)) title = Session.Process;
            title = Theme.Fit(g, title, Theme.FName, nameMax);

            using (SolidBrush tb = new SolidBrush(muted ? Theme.TextDim : Theme.Text))
                g.DrawString(title, Theme.FName, tb, nameX, card.Y + Theme.Px(8));

            string proc = Session.Process;
            if (!string.IsNullOrEmpty(proc) && string.Compare(proc, title, true) != 0)
            {
                float tw = g.MeasureString(title, Theme.FName).Width;
                float room = nameMax - tw - Theme.Px(6);
                if (room > Theme.Px(20))
                {
                    using (SolidBrush sb = new SolidBrush(muted ? Theme.TextFaint : Theme.TextDim))
                        g.DrawString(Theme.Fit(g, proc, Theme.FSub, room), Theme.FSub, sb,
                                     nameX + tw + Theme.Px(6), card.Y + Theme.Px(13));
                }
            }

            // 实时电平
            Rectangle mbx = MeterBox;
            RectangleF meter = new RectangleF(mbx.X + 2, mbx.Y + 2, mbx.Width - 4, mbx.Height - 4);
            using (GraphicsPath mp = Theme.Round(meter, meter.Height / 2f))
            using (SolidBrush mb = new SolidBrush(Color.FromArgb(16, 255, 255, 255)))
                g.FillPath(mb, mp);
            if (_shot > 0.002f && !muted)
            {
                RectangleF mf = new RectangleF(meter.X, meter.Y, meter.Width * Math.Min(1f, _shot), meter.Height);
                using (GraphicsPath fp = Theme.Round(mf, meter.Height / 2f))
                using (LinearGradientBrush fb = new LinearGradientBrush(
                    new PointF(meter.X, 0f), new PointF(meter.Right, 0f),
                    Color.FromArgb(190, Theme.AccentA), Color.FromArgb(220, Theme.AccentB)))
                {
                    g.FillPath(fb, fp);
                }
            }

            // 滑条
            RectangleF track = TrackRect;
            using (GraphicsPath tp = Theme.Round(track, track.Height / 2f))
            using (SolidBrush tb2 = new SolidBrush(Theme.Track))
                g.FillPath(tb2, tp);

            float vol = muted ? 0f : Session.Volume;
            float fx = track.X + track.Width * vol;
            if (fx - track.X > 1f)
            {
                RectangleF filled = new RectangleF(track.X, track.Y, fx - track.X, track.Height);
                using (GraphicsPath fp = Theme.Round(filled, track.Height / 2f))
                {
                    if (muted)
                    {
                        using (SolidBrush gb = new SolidBrush(Color.FromArgb(0x50, 0x56, 0x62)))
                            g.FillPath(gb, fp);
                    }
                    else
                    {
                        using (LinearGradientBrush fb = new LinearGradientBrush(
                            new PointF(track.X, 0f), new PointF(track.Right, 0f), Theme.AccentA, Theme.AccentB))
                        {
                            g.FillPath(fb, fp);
                        }
                    }
                }
            }

            float knobR = (_dragging || _overSlider) ? Theme.Px(7f) : Theme.Px(6f);
            float cy = track.Y + track.Height / 2f;
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(muted ? 40 : 65, Theme.AccentA)))
                g.FillEllipse(halo, fx - knobR - Theme.Px(3), cy - knobR - Theme.Px(3), (knobR + Theme.Px(3)) * 2f, (knobR + Theme.Px(3)) * 2f);
            using (SolidBrush kb = new SolidBrush(muted ? Color.FromArgb(0x9A, 0xA0, 0xAC) : Color.White))
                g.FillEllipse(kb, fx - knobR, cy - knobR, knobR * 2f, knobR * 2f);

            DrawMuteButton(g, muteR, muted);
        }

        void DrawMuteButton(Graphics g, Rectangle r, bool muted)
        {
            float s = Theme.S;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;

            if (_overMute)
            {
                RectangleF pill = new RectangleF(cx - 13f * s, cy - 11f * s, 26f * s, 22f * s);
                using (GraphicsPath bg = Theme.Round(pill, 7f * s))
                using (SolidBrush b = new SolidBrush(Theme.CardHot))
                    g.FillPath(b, bg);
            }

            Color c = muted ? Theme.TextFaint : (_overMute ? Color.White : Theme.TextDim);

            using (GraphicsPath cone = new GraphicsPath())
            {
                cone.AddPolygon(new PointF[] {
                    new PointF(cx - 7.4f * s, cy - 2.1f * s),
                    new PointF(cx - 4.1f * s, cy - 2.1f * s),
                    new PointF(cx - 0.9f * s, cy - 6.1f * s),
                    new PointF(cx - 0.9f * s, cy + 6.1f * s),
                    new PointF(cx - 4.1f * s, cy + 2.1f * s),
                    new PointF(cx - 7.4f * s, cy + 2.1f * s)
                });
                cone.CloseFigure();
                using (SolidBrush sb = new SolidBrush(c))
                    g.FillPath(sb, cone);
            }

            using (Pen pen = new Pen(c, Math.Max(1.2f, 1.3f * s)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (muted)
                {
                    g.DrawLine(pen, cx + 1.3f * s, cy - 3.5f * s, cx + 6.1f * s, cy + 3.5f * s);
                    g.DrawLine(pen, cx + 6.1f * s, cy - 3.5f * s, cx + 1.3f * s, cy + 3.5f * s);
                }
                else
                {
                    g.DrawArc(pen, cx - 3.2f * s, cy - 4.4f * s, 8.4f * s, 8.8f * s, -56f, 112f);
                    g.DrawArc(pen, cx - 3.2f * s, cy - 7.9f * s, 15.4f * s, 15.8f * s, -56f, 112f);
                }
            }
        }

        void ApplyFromX(int x)
        {
            RectangleF t = TrackRect;
            float v = (x - t.X) / t.Width;
            if (v < 0f) v = 0f;
            if (v > 1f) v = 1f;
            Session.SetVolume(v);
            if (Session.Muted) Session.SetMute(false);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                Session.SetMute(!Session.Muted);
                Invalidate();
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            if (MuteRect.Contains(e.Location))
            {
                Session.SetMute(!Session.Muted);
                Invalidate();
                return;
            }
            if (Hit(TrackRect, Theme.Px(4), Theme.Px(10), e.Location) || e.Y > TrackRect.Y)
            {
                _dragging = true;
                Capture = true;
                ApplyFromX(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool overMute = MuteRect.Contains(e.Location);
            bool overSlider = Hit(TrackRect, Theme.Px(4), Theme.Px(10), e.Location);
            if (overMute != _overMute || overSlider != _overSlider)
            {
                _overMute = overMute;
                _overSlider = overSlider;
                Invalidate();
            }
            Cursor = (overMute || overSlider || _dragging) ? Cursors.Hand : Cursors.Default;
            if (_dragging) ApplyFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                Invalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (MuteRect.Contains(e.Location)) return;
            Session.SetMute(false);
            Session.SetVolume(1f);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hot = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = false;
            _overMute = false;
            _overSlider = false;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _icon != null) _icon.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>带双缓冲的面板，滚动时不闪。</summary>
    internal sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }
    }

    internal sealed class MixerForm : Form
    {
        readonly BufferedPanel _list = new BufferedPanel();
        readonly Panel _head = new Panel();
        readonly Label _title = new Label();
        readonly Label _empty = new Label();
        readonly CheckBox _pin = new CheckBox();
        readonly Button _reload = new Button();
        readonly Label _hint = new Label();
        readonly System.Windows.Forms.Timer _tick = new System.Windows.Forms.Timer();
        readonly List<AudioRow> _rows = new List<AudioRow>();

        List<AudioSession> _sessions = new List<AudioSession>();
        int _fgPid = -1;
        int _ticks;

        public bool HideOnClose;

        public MixerForm(int foregroundPid)
        {
            _fgPid = foregroundPid;

            float scale = 1f;
            try { using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f; }
            catch { }
            Theme.Init(scale);

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Text = "QuickMix \u97f3\u91cf\u5408\u6210\u5668";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.FSub;
            ClientSize = new Size(Theme.Px(400), Theme.Px(560));
            MinimumSize = new Size(Theme.Px(330), Theme.Px(240));
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            DoubleBuffered = true;
            try { Icon = Theme.Mark(); } catch { }

            _head.Dock = DockStyle.Top;
            _head.Height = Theme.Px(50);
            _head.BackColor = Theme.Bg;
            _head.Resize += delegate { LayoutHeader(); };

            _title.Text = "\u97f3\u91cf\u5408\u6210\u5668";
            _title.Font = Theme.FHead;
            _title.ForeColor = Theme.Text;
            _title.AutoSize = true;
            _head.Controls.Add(_title);

            _pin.Text = "\u7f6e\u9876";
            _pin.Font = Theme.FBtn;
            _pin.ForeColor = Theme.TextDim;
            _pin.BackColor = Color.Transparent;
            _pin.AutoSize = true;
            _pin.Checked = true;
            TopMost = true;
            _pin.CheckedChanged += delegate { TopMost = _pin.Checked; };
            _head.Controls.Add(_pin);

            _reload.Text = "\u5237\u65b0";
            _reload.Font = Theme.FBtn;
            _reload.FlatStyle = FlatStyle.Flat;
            _reload.FlatAppearance.BorderSize = 1;
            _reload.FlatAppearance.BorderColor = Theme.Line;
            _reload.BackColor = Theme.Card;
            _reload.ForeColor = Theme.Text;
            _reload.Size = new Size(Theme.Px(54), Theme.Px(24));
            _reload.Click += delegate { Rebuild(AudioEngine.Enumerate()); };
            _head.Controls.Add(_reload);

            _list.Dock = DockStyle.Fill;
            _list.AutoScroll = true;
            _list.BackColor = Theme.Bg;
            _list.Padding = new Padding(Theme.Px(10), Theme.Px(1), Theme.Px(8), Theme.Px(8));

            _empty.Text = "\u8fd9\u4f1a\u513f\u6ca1\u6709\u5e94\u7528\u5728\u51fa\u58f0\r\n\u653e\u70b9\u58f0\u97f3\uff0c\u518d\u70b9\u5237\u65b0";
            _empty.Dock = DockStyle.Fill;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = Theme.TextFaint;
            _empty.Font = Theme.FSub;
            _list.Controls.Add(_empty);

            _hint.Dock = DockStyle.Bottom;
            _hint.Height = Theme.Px(28);
            _hint.TextAlign = ContentAlignment.MiddleCenter;
            _hint.ForeColor = Theme.TextFaint;
            _hint.Font = Theme.FHint;
            _hint.Text = "\u62d6\u6ed1\u6761\u8c03\u97f3\u91cf \u00b7 \u53f3\u952e\u9759\u97f3 \u00b7 \u53cc\u51fb\u62c9\u6ee1 \u00b7 \u6eda\u8f6e\u7ffb\u5217\u8868";

            Controls.Add(_list);
            Controls.Add(_hint);
            Controls.Add(_head);

            LayoutHeader();

            _tick.Interval = 120;
            _tick.Tick += delegate { OnTick(); };

            Rebuild(AudioEngine.Enumerate());
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            try
            {
                if (Win32.DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0)
                    Win32.DwmSetWindowAttribute(Handle, 19, ref on, 4);
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _tick.Enabled = true;
            _list.Focus();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            _tick.Enabled = Visible;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (HideOnClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HiddenByUser = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        public bool HiddenByUser;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            if (e.KeyCode == Keys.F5) Rebuild(AudioEngine.Enumerate());
        }

        void LayoutHeader()
        {
            int pad = Theme.Px(16);
            _title.Location = new Point(pad, (_head.Height - _title.Height) / 2);
            int y = (_head.Height - _reload.Height) / 2;
            _reload.Location = new Point(Math.Max(pad, _head.ClientSize.Width - pad - _reload.Width), y);
            _pin.Location = new Point(Math.Max(pad, _reload.Left - Theme.Px(12) - _pin.Width),
                                      (_head.Height - _pin.Height) / 2);
        }

        void OnTick()
        {
            _ticks++;
            bool readBack = (_ticks % 3 == 0);
            for (int i = 0; i < _rows.Count; i++)
            {
                AudioRow r = _rows[i];
                if (readBack) r.Session.RefreshReadback();
                r.RefreshVisual();
            }
            if (_ticks % 12 == 0)
            {
                List<AudioSession> fresh = AudioEngine.Enumerate();
                if (Signature(fresh) != Signature(_sessions)) Rebuild(fresh);
            }
        }

        static string Signature(List<AudioSession> list)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++) sb.Append(list[i].Pid).Append(',');
            return sb.ToString();
        }

        void Rebuild(List<AudioSession> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                AudioSession s = list[i];
                if (s.Pid == Process.GetCurrentProcess().Id) { list.RemoveAt(i); i--; continue; }
            }

            bool fgFound = false;
            for (int i = 0; i < list.Count; i++) if (list[i].Pid == _fgPid) fgFound = true;
            if (!fgFound)
            {
                _fgPid = Win32.GuessActiveAppPid(list);
                fgFound = _fgPid > 0;
            }

            list.Sort(delegate(AudioSession a, AudioSession b)
            {
                bool fa = a.Pid == _fgPid, fb = b.Pid == _fgPid;
                if (fa != fb) return fa ? -1 : 1;
                if (a.Active != b.Active) return a.Active ? -1 : 1;
                return string.Compare(a.Process, b.Process, StringComparison.OrdinalIgnoreCase);
            });

            int keepY = 0;
            try { keepY = -_list.AutoScrollPosition.Y; } catch { }

            _list.SuspendLayout();
            for (int i = 0; i < _rows.Count; i++)
            {
                _list.Controls.Remove(_rows[i]);
                _rows[i].Dispose();
            }
            _rows.Clear();

            _sessions = list;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                AudioRow r = new AudioRow(list[i]);
                r.IsForeground = list[i].Pid == _fgPid;
                r.Dock = DockStyle.Top;
                _list.Controls.Add(r);
                _rows.Insert(0, r);
            }
            _empty.Visible = list.Count == 0;
            if (_empty.Visible) _empty.BringToFront();
            _list.ResumeLayout();

            FitHeight();

            if (keepY > 0)
            {
                try { _list.AutoScrollPosition = new Point(0, keepY); }
                catch { }
            }
        }

        void FitHeight()
        {
            int content = _head.Height + _hint.Height + Theme.Px(20);
            for (int i = 0; i < _rows.Count; i++) content += _rows[i].Height;
            if (_rows.Count == 0) content = Theme.Px(300);

            int chrome = Height - ClientSize.Height;
            int want = content + chrome;

            int max = Theme.Px(760);
            try { max = Screen.FromControl(this).WorkingArea.Height - Theme.Px(60); }
            catch { }
            if (want > max) want = max;
            int min = Theme.Px(240) + chrome;
            if (want < min) want = min;

            if (Height != want) Height = want;
        }

        public void Reload() { Rebuild(AudioEngine.Enumerate()); }

        public void BringUp()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            TopMost = _pin.Checked;
        }
    }

    #endregion

    #region 托盘

    internal sealed class TrayContext : ApplicationContext
    {
        readonly NotifyIcon _tray;
        readonly HotkeyWindow _hotkey;
        MixerForm _win;

        public TrayContext(bool registerHotkey)
        {
            _tray = new NotifyIcon();
            _tray.Icon = Theme.Mark();
            _tray.Text = "QuickMix \u2014 \u5feb\u6377\u97f3\u91cf\u5408\u6210\u5668";
            _tray.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("\u6253\u5f00\u97f3\u91cf\u9762\u677f\t" + Config.HotkeyText, null, delegate { OpenPanel(); });
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem hot = new ToolStripMenuItem("\u5feb\u6377\u952e");
            for (int i = 0; i < Config.Presets.Length; i++)
            {
                int idx = i;
                ToolStripMenuItem it = new ToolStripMenuItem(Config.Presets[i].Text);
                it.Checked = (Config.HotkeyIndex == i);
                it.Tag = idx;
                it.Click += delegate(object s, EventArgs e) { PickHotkey((ToolStripMenuItem)s, idx); };
                hot.DropDownItems.Add(it);
            }
            menu.Items.Add(hot);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem auto = new ToolStripMenuItem("\u5f00\u673a\u81ea\u52a8\u542f\u52a8");
            auto.Checked = Config.AutoStart;
            auto.Click += delegate(object s, EventArgs e)
            {
                ToolStripMenuItem it = (ToolStripMenuItem)s;
                bool next = !it.Checked;
                try { Config.AutoStart = next; it.Checked = next; }
                catch { }
            };
            menu.Items.Add(auto);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("\u9000\u51fa", null, delegate { Quit(); });

            _tray.ContextMenuStrip = menu;
            _tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) OpenPanel();
            };

            if (registerHotkey)
            {
                _hotkey = new HotkeyWindow();
                _hotkey.HotkeyPressed += delegate { OpenPanel(); };
                if (!_hotkey.Register(Config.HotkeyMod, Config.HotkeyVk))
                    _tray.ShowBalloonTip(4000, "QuickMix",
                        "\u5feb\u6377\u952e " + Config.HotkeyText + " \u88ab\u5360\u7528\u4e86\uff0c\u53f3\u952e\u6258\u76d8\u56fe\u6807\u91cc\u53ef\u4ee5\u6362\u4e00\u4e2a\u3002",
                        ToolTipIcon.Warning);
            }
        }

        void PickHotkey(ToolStripMenuItem sender, int idx)
        {
            if (idx == Config.HotkeyIndex) { sender.Checked = true; return; }

            if (_hotkey != null && !_hotkey.ReRegister(Config.Presets[idx].Mod, Config.Presets[idx].Vk))
            {
                sender.Checked = false;
                _tray.ShowBalloonTip(4000, "QuickMix",
                    Config.Presets[idx].Text + " \u88ab\u5360\u7528\u4e86\uff0c\u6362\u4e00\u4e2a\u8bd5\u8bd5\u3002", ToolTipIcon.Warning);
                return;
            }

            int old = Config.HotkeyIndex;
            Config.HotkeyIndex = idx;
            Config.Save();

            ToolStripMenuItem owner = sender.OwnerItem as ToolStripMenuItem;
            if (owner != null)
            {
                for (int i = 0; i < owner.DropDownItems.Count; i++)
                {
                    ToolStripMenuItem it = owner.DropDownItems[i] as ToolStripMenuItem;
                    if (it != null) it.Checked = (it.Tag is int && (int)it.Tag == idx);
                }
            }

            if (_tray.ContextMenuStrip != null && _tray.ContextMenuStrip.Items.Count > 0)
                _tray.ContextMenuStrip.Items[0].Text = "\u6253\u5f00\u97f3\u91cf\u9762\u677f\t" + Config.Presets[idx].Text;
        }

        public void OpenPanel()
        {
            if (_win == null)
            {
                int pid = Win32.GetForegroundPid();
                pid = Win32.ResolveRealForegroundPid(pid);
                if (pid == Process.GetCurrentProcess().Id) pid = -1;
                _win = new MixerForm(pid);
                _win.HideOnClose = true;
            }
            _win.BringUp();
        }

        public void Quit()
        {
            if (_hotkey != null) _hotkey.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            if (_win != null) { _win.HideOnClose = false; _win.Dispose(); }
            ExitThread();
        }
    }

    #endregion

    #region 热键窗口

    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        const int HOTKEY_ID = 0x5BB1;
        public event EventHandler HotkeyPressed;

        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Caption = "QuickMixHotkey";
            CreateHandle(cp);
        }

        public bool Register(int mod, int vk)
        {
            return Win32.RegisterHotKey(Handle, HOTKEY_ID, mod, vk);
        }

        public bool ReRegister(int mod, int vk)
        {
            try { Win32.UnregisterHotKey(Handle, HOTKEY_ID); } catch { }
            return Win32.RegisterHotKey(Handle, HOTKEY_ID, mod, vk);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32.WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                if (HotkeyPressed != null) HotkeyPressed(this, EventArgs.Empty);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            try { Win32.UnregisterHotKey(Handle, HOTKEY_ID); } catch { }
            DestroyHandle();
        }
    }

    #endregion

    internal static class Dpi
    {
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int level);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        public static void Enable()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }
            try { if (SetProcessDpiAwareness(2) == 0) return; } catch { }
            try { SetProcessDPIAware(); } catch { }
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Dpi.Enable();

            if (args.Length > 0 && (args[0] == "--dump" || args[0] == "--set" || args[0] == "--mute"))
            {
                try { RunCli(args); } catch (Exception ex) { File.WriteAllText(DumpPath, ex.ToString()); }
                return;
            }

            Config.Load();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool tray = false;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--tray" || args[i] == "/tray") tray = true;

            if (tray)
            {
                bool created;
                using (Mutex mtx = new Mutex(true, "QuickMix.SingleInstance.v1", out created))
                {
                    if (!created) return;
                    Application.Run(new TrayContext(true));
                }
                return;
            }

            int pid = Win32.GetForegroundPid();
            pid = Win32.ResolveRealForegroundPid(pid);
            if (pid == Process.GetCurrentProcess().Id) pid = -1;
            Application.Run(new MixerForm(pid));
        }

        static void RunCli(string[] args)
        {
            try { Win32.SetProcessDPIAware(); } catch { }

            StringBuilder log = new StringBuilder();
            log.AppendLine(string.Join(" ", args));

            List<AudioSession> list = AudioEngine.Enumerate();
            log.AppendLine("foreground pid = " + Win32.GetForegroundPid());
            log.AppendLine("sessions = " + list.Count);

            if (args[0] == "--set" || args[0] == "--mute")
            {
                string who = args.Length > 1 ? args[1] : "";
                string what = args.Length > 2 ? args[2] : "";

                List<AudioSession> hit = new List<AudioSession>();
                int pid;
                if (int.TryParse(who, out pid))
                {
                    for (int i = 0; i < list.Count; i++) if (list[i].Pid == pid) hit.Add(list[i]);
                }
                if (hit.Count == 0)
                {
                    for (int i = 0; i < list.Count; i++)
                        if (string.Equals(list[i].Process, who, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(list[i].Process + ".exe", who, StringComparison.OrdinalIgnoreCase)
                            || (list[i].Title != null && list[i].Title.IndexOf(who, StringComparison.OrdinalIgnoreCase) >= 0))
                            hit.Add(list[i]);
                }

                log.AppendLine("matched = " + hit.Count);
                for (int i = 0; i < hit.Count; i++)
                {
                    if (args[0] == "--set")
                    {
                        float pct;
                        if (float.TryParse(what, System.Globalization.NumberStyles.Float,
                                           System.Globalization.CultureInfo.InvariantCulture, out pct))
                        {
                            hit[i].SetVolume(pct / 100f);
                            hit[i].SetMute(false);
                        }
                    }
                    else
                    {
                        hit[i].SetMute(what == "1" || what == "true" || what == "on");
                    }
                    hit[i].RefreshReadback();
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                AudioSession s = list[i];
                log.AppendLine(string.Format("  pid={0} proc={1} title=\"{2}\" vol={3:0.00} mute={4} active={5} peak={6:0.000} n={7}",
                    s.Pid, s.Process, s.Title, s.Volume, s.Muted, s.Active, s.Peak, s.Volumes.Count));
            }

            File.WriteAllText(DumpPath, log.ToString());
        }

        static string DumpPath
        {
            get { return Path.Combine(Path.GetTempPath(), "quickmix-dump.txt"); }
        }

    }
}
