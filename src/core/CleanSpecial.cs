using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace BtwTweaker
{
    // Следы активности: недавние файлы, списки переходов, история «Выполнить» и адресной строки Проводника
    public class TracesCleaner : Cleaner
    {
        const string RunMru = @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU";
        const string Typed = @"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths";
        static string Recent { get { return Fs.E(@"%APPDATA%\Microsoft\Windows\Recent"); } }

        public TracesCleaner()
        {
            Title = "Очистка следов активности";
            Description = "Список недавних файлов, списки переходов на панели задач, история окна «Выполнить» (Win+R) и адресной строки Проводника.";
            Icon = "\uE81C";
            Confirm = true;
        }

        List<string> Files()
        {
            var l = Fs.Glob(Recent, "*.lnk");
            l.AddRange(Fs.Glob(Path.Combine(Recent, "AutomaticDestinations"), "*"));
            l.AddRange(Fs.Glob(Path.Combine(Recent, "CustomDestinations"), "*"));
            return l;
        }

        static List<string> Values(string key)
        {
            var l = new List<string>();
            using (var b = Reg.Base(Reg.HKCU))
            using (var k = b.OpenSubKey(key))
                if (k != null)
                    foreach (var n in k.GetValueNames())
                        if (n.Length > 0 && n != "MRUList") l.Add(n);
            return l;
        }

        public override ScanResult Scan()
        {
            var r = new ScanResult();
            var files = Files();
            int runs = Values(RunMru).Count, typed = Values(Typed).Count;
            r.Count = files.Count + runs + typed;
            foreach (var f in files) try { r.Bytes += new FileInfo(f).Length; } catch { }
            r.Items.Add("Недавние файлы и списки переходов: " + files.Count);
            r.Items.Add("История «Выполнить»: " + runs);
            r.Items.Add("История адресной строки Проводника: " + typed);
            return r;
        }

        public override CleanResult Clean()
        {
            var r = new CleanResult();
            foreach (var f in Files()) Fs.DeleteFile(f, r);
            foreach (var key in new[] { RunMru, Typed })
                using (var b = Reg.Base(Reg.HKCU))
                using (var k = b.OpenSubKey(key, true))
                    if (k != null)
                        foreach (var n in k.GetValueNames())
                            if (n.Length > 0) { try { k.DeleteValue(n); r.Removed++; } catch { r.Failed++; } }
            return r;
        }
    }

    // Остатки UWP: папки в %LOCALAPPDATA%\Packages от приложений, которых уже нет
    public class UwpLeftovers : Cleaner
    {
        const string Repo = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository";

        public UwpLeftovers()
        {
            Title = "Остатки UWP";
            Description = "Папки с данными приложений Магазина, которые уже удалены. Перед очисткой будет показан список.";
            Icon = "\uE71D";
            Confirm = true;
        }

        // Семейства установленных пакетов: Имя_ИздательId
        static HashSet<string> Installed()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var b = Reg.Base(Reg.HKCU))
            {
                using (var k = b.OpenSubKey(Repo + @"\Families"))
                    if (k != null) foreach (var n in k.GetSubKeyNames()) set.Add(n);
                using (var k = b.OpenSubKey(Repo + @"\Packages"))
                    if (k != null)
                        foreach (var full in k.GetSubKeyNames())
                        {
                            var p = full.Split('_');   // Имя_Версия_Архитектура_Ресурс_ИздательId
                            if (p.Length >= 5) set.Add(p[0] + "_" + p[p.Length - 1]);
                        }
            }
            // Системные приложения из C:\Windows\SystemApps
            foreach (var d in Fs.SubDirs(Fs.E(@"%WINDIR%\SystemApps"), "*")) set.Add(Path.GetFileName(d));
            return set;
        }

        List<string> Find()
        {
            var l = new List<string>();
            var inst = Installed();
            if (inst.Count < 10) return l;   // реестр не прочитался — ничего не трогаем
            foreach (var d in Fs.SubDirs(Fs.E(@"%LOCALAPPDATA%\Packages"), "*_*"))
            {
                string name = Path.GetFileName(d);
                // Настоящее семейство пакета: Имя_ИздательId, где ИздательId — 13 символов (например, 8wekyb3d8bbwe).
                // Служебные папки вроде windows_ie_ac_001 не подходят — их не трогаем.
                string pub = name.Substring(name.LastIndexOf('_') + 1);
                if (pub.Length != 13) continue;
                if (!inst.Contains(name)) l.Add(d);
            }
            return l;
        }

        public override ScanResult Scan()
        {
            var r = new ScanResult();
            var l = Find();
            r.Count = l.Count;
            foreach (var d in l) { long s = Fs.Size(d); r.Bytes += s; r.Items.Add(Path.GetFileName(d) + "  —  " + Fs.Fmt(s)); }
            return r;
        }

        public override CleanResult Clean()
        {
            var r = new CleanResult();
            foreach (var d in Find()) Fs.RemoveDir(d, r);
            return r;
        }
    }

    // Отключённые («призрачные») устройства: были подключены раньше, сейчас их нет
    public class GhostDevices : Cleaner
    {
        public GhostDevices()
        {
            Title = "Отключённые устройства";
            Description = "Записи об устройствах, которые когда-то подключались (флешки, телефоны, старые видеокарты и т.п.), а сейчас отсутствуют. Если устройство подключить снова, Windows установит его заново. Программные устройства и Bluetooth (чтобы не сбить сопряжение) не трогаются.";
            Icon = "\uE772";
            Confirm = true;
        }

        public override ScanResult Scan()
        {
            var r = new ScanResult();
            var l = Ghosts();
            r.Count = l.Count;
            foreach (var g in l) r.Items.Add(g.Name + "  —  " + g.Id);
            return r;
        }

        public override CleanResult Clean()
        {
            var r = new CleanResult();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in Ghosts()) ids.Add(g.Id);
            IntPtr set = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, DIGCF_ALLCLASSES);
            if (set == INVALID) return r;
            try
            {
                var data = NewData();
                for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref data); i++)
                {
                    string id = InstanceId(set, ref data);
                    if (id == null || !ids.Contains(id)) continue;
                    if (SetupDiCallClassInstaller(DIF_REMOVE, set, ref data)) r.Removed++;
                    else r.Failed++;
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return r;
        }

        class Dev { public string Id; public string Name; }

        // Не трогаем: программные устройства и Bluetooth. Выключенные сопряжённые наушники/мыши тоже «отсутствуют»,
        // и их удаление разорвало бы сопряжение.
        static readonly string[] Skip = { @"ROOT\", @"SWD\", @"HTREE\", @"BTHENUM\", @"BTHLE\", @"BTHLEDEVICE\", @"BTH\", @"BTHHFENUM\" };

        static List<Dev> Ghosts()
        {
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in Enum(DIGCF_ALLCLASSES | DIGCF_PRESENT)) present.Add(d.Id);
            var l = new List<Dev>();
            foreach (var d in Enum(DIGCF_ALLCLASSES))
            {
                if (present.Contains(d.Id)) continue;
                bool skip = false;
                foreach (var s in Skip) if (d.Id.StartsWith(s, StringComparison.OrdinalIgnoreCase)) skip = true;
                if (!skip) l.Add(d);
            }
            l.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return l;
        }

        static List<Dev> Enum(uint flags)
        {
            var l = new List<Dev>();
            IntPtr set = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, flags);
            if (set == INVALID) return l;
            try
            {
                var data = NewData();
                for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref data); i++)
                {
                    string id = InstanceId(set, ref data);
                    if (id == null) continue;
                    string name = Prop(set, ref data, SPDRP_FRIENDLYNAME) ?? Prop(set, ref data, SPDRP_DEVICEDESC) ?? id;
                    l.Add(new Dev { Id = id, Name = name });
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return l;
        }

        // ── SetupAPI ──
        const uint DIGCF_PRESENT = 0x2, DIGCF_ALLCLASSES = 0x4;
        const uint SPDRP_DEVICEDESC = 0x0, SPDRP_FRIENDLYNAME = 0xC;
        const uint DIF_REMOVE = 0x5;
        static readonly IntPtr INVALID = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA { public uint cbSize; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }

        static SP_DEVINFO_DATA NewData() { var d = new SP_DEVINFO_DATA(); d.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA)); return d; }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll")]
        static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SP_DEVINFO_DATA data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SP_DEVINFO_DATA data, StringBuilder id, int size, out int required);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref SP_DEVINFO_DATA data, uint prop, out uint type, byte[] buf, uint size, out uint required);
        [DllImport("setupapi.dll")]
        static extern bool SetupDiCallClassInstaller(uint fn, IntPtr set, ref SP_DEVINFO_DATA data);
        [DllImport("setupapi.dll")]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        static string InstanceId(IntPtr set, ref SP_DEVINFO_DATA data)
        {
            var sb = new StringBuilder(512);
            int req;
            return SetupDiGetDeviceInstanceIdW(set, ref data, sb, sb.Capacity, out req) ? sb.ToString() : null;
        }

        static string Prop(IntPtr set, ref SP_DEVINFO_DATA data, uint prop)
        {
            var buf = new byte[1024];
            uint type, req;
            if (!SetupDiGetDeviceRegistryPropertyW(set, ref data, prop, out type, buf, (uint)buf.Length, out req)) return null;
            var s = Encoding.Unicode.GetString(buf, 0, (int)Math.Min(req, (uint)buf.Length)).TrimEnd('\0');
            return s.Length == 0 ? null : s;
        }
    }
}
