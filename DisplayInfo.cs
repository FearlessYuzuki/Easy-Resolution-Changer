using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace ResolutionChanger
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2;
        public int dmPanningWidth, dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    public sealed class DisplayInfo
    {
        private const int DM_BITSPERPEL = 0x00040000;
        private const int DM_PELSWIDTH = 0x00080000;
        private const int DM_PELSHEIGHT = 0x00100000;
        private const int DM_DISPLAYFREQUENCY = 0x00400000;
        private const uint CDS_UPDATEREGISTRY = 0x1;
        private const int ENUM_CURRENT_SETTINGS = -1;

        public string DeviceName;
        public string AdapterName;
        public string MonitorName;
        public bool Primary;
        public int PositionX, PositionY;
        public DEVMODE Current;
        public List<DEVMODE> Modes = new List<DEVMODE>();

        public string ModeLabel(DEVMODE m)
        {
            return string.Format("{0}×{1} @ {2}Hz", m.dmPelsWidth, m.dmPelsHeight, m.dmDisplayFrequency);
        }

        public string ModeKey(DEVMODE m)
        {
            return string.Format("{0}x{1}@{2}", m.dmPelsWidth, m.dmPelsHeight, m.dmDisplayFrequency);
        }

        public string Apply(DEVMODE mode, bool persist)
        {
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            mode.dmFields = DM_BITSPERPEL | DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
            mode.dmBitsPerPel = 32;
            int r = ChangeDisplaySettingsEx(DeviceName, ref mode, IntPtr.Zero, persist ? CDS_UPDATEREGISTRY : 0u, IntPtr.Zero);
            if (r == 0)
            {
                Current = mode;
                return null;
            }
            switch (r)
            {
                case 1: return "需要重启系统生效";
                case -1: return "设置失败";
                case -2: return "不支持的显示模式";
                case -3: return "无法更新注册表";
                case -4: return "参数错误";
                case -5: return "参数无效";
                case -6: return "双屏配置不支持";
            }
            return "错误代码 " + r;
        }

        public static List<DisplayInfo> Enumerate()
        {
            var list = new List<DisplayInfo>();
            int i = 0;
            while (true)
            {
                var dd = new DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
                if (!EnumDisplayDevices(null, (uint)i, ref dd, 0)) break;
                i++;
                if ((dd.StateFlags & 0x1) == 0) continue;
                if ((dd.StateFlags & 0x8) != 0) continue;
                var dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                EnumDisplaySettings(dd.DeviceName, ENUM_CURRENT_SETTINGS, ref dm);
                var info = new DisplayInfo
                {
                    DeviceName = dd.DeviceName,
                    AdapterName = dd.DeviceString,
                    Primary = (dd.StateFlags & 0x4) != 0,
                    PositionX = dm.dmPositionX,
                    PositionY = dm.dmPositionY,
                    Current = dm
                };
                info.MonitorName = ResolveMonitorName(dd.DeviceName);
                int m = 0;
                var seen = new HashSet<string>();
                while (true)
                {
                    var mode = new DEVMODE();
                    mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                    if (!EnumDisplaySettings(dd.DeviceName, m, ref mode)) break;
                    m++;
                    if (mode.dmBitsPerPel != 32) continue;
                    string key = info.ModeKey(mode);
                    if (!seen.Add(key)) continue;
                    info.Modes.Add(mode);
                }
                info.Modes.Sort((a, b) =>
                {
                    long aa = (long)a.dmPelsWidth * a.dmPelsHeight;
                    long bb = (long)b.dmPelsWidth * b.dmPelsHeight;
                    if (aa != bb) return bb.CompareTo(aa);
                    return b.dmDisplayFrequency.CompareTo(a.dmDisplayFrequency);
                });
                list.Add(info);
            }
            list.Sort((a, b) =>
            {
                if (a.PositionX != b.PositionX) return a.PositionX.CompareTo(b.PositionX);
                return a.PositionY.CompareTo(b.PositionY);
            });
            return list;
        }

        private static string ResolveMonitorName(string deviceName)
        {
            var dd = new DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
            if (EnumDisplayDevices(deviceName, 0, ref dd, 0))
            {
                string id = dd.DeviceID ?? "";
                int s = id.IndexOf("MONITOR\\", StringComparison.OrdinalIgnoreCase);
                if (s >= 0 && s + 11 < id.Length)
                {
                    string code = id.Substring(s + 8, 3).ToUpperInvariant();
                    string n = EdidName(code);
                    if (!string.IsNullOrEmpty(n)) return n;
                }
                if (!string.Equals(dd.DeviceString, "Generic PnP Monitor", StringComparison.OrdinalIgnoreCase))
                    return dd.DeviceString;
            }
            return null;
        }

        private static string EdidName(string mfrCode)
        {
            try
            {
                using (var root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY"))
                {
                    if (root == null) return null;
                    foreach (string sub in root.GetSubKeyNames())
                    {
                        using (var adapter = root.OpenSubKey(sub))
                        {
                            if (adapter == null) continue;
                            foreach (string inst in adapter.GetSubKeyNames())
                            {
                                using (var node = adapter.OpenSubKey(inst))
                                {
                                    if (node == null) continue;
                                    using (var dp = node.OpenSubKey("Device Parameters"))
                                    {
                                        if (dp == null) continue;
                                        var edid = dp.GetValue("EDID") as byte[];
                                        if (edid == null || edid.Length < 128) continue;
                                        int v = (edid[8] << 8) | edid[9];
                                        string code = "" + (char)('A' + ((v >> 10) & 31) - 1) + (char)('A' + ((v >> 5) & 31) - 1) + (char)('A' + (v & 31) - 1);
                                        if (code != mfrCode) continue;
                                        for (int p = 0; p + 5 < edid.Length; p++)
                                        {
                                            if (edid[p] == 0 && edid[p + 1] == 0 && edid[p + 2] == 0
                                                && edid[p + 3] == 0xFC && edid[p + 4] == 0)
                                            {
                                                var sb = new StringBuilder();
                                                for (int j = p + 5; j < p + 18 && j < edid.Length; j++)
                                                {
                                                    char ch = (char)edid[j];
                                                    if (ch == '\n' || ch == 0) break;
                                                    sb.Append(ch);
                                                }
                                                string name = sb.ToString().Trim();
                                                if (name.Length > 0) return name;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string d, uint i, ref DISPLAY_DEVICE dd, uint f);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string d, int m, ref DEVMODE dm);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);
    }
}
