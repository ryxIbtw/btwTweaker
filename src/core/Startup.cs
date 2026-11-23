using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BtwTweaker
{
    public enum StartKind { Registry, File, Task, Service }

    public class StartupItem
    {
        public StartKind Kind;
        public string Name;
        public string Command;      // как записано в системе
        public string Exe;          // путь к программе (для иконки и издателя)
        public string Location;     // откуда запускается — для показа
        public bool Enabled;
        public bool Recommended;    // апдейтеры и т.п. — входят в «Отключить ненужное»
        public string Publisher;    // заполняется в фоне
        public string Impact;       // Высокое / Среднее / Низкое / null — нет данных
        public string Delayed;      // путь своего задания отложенного запуска (если отложено)
        public bool Missing { get { return !string.IsNullOrEmpty(Exe) && Kind != StartKind.Service && AppTaskKey == null && !File.Exists(Exe) && Path.IsPathRooted(Exe); } }
        public string Args;         // аргументы командной строки (для отложенного запуска)

        // для реестра и файлов
        internal RegistryHive Hive;
        internal string RunKey, ApprovedKey, FilePath;
        // автозапуск приложения Магазина (StartupTask): ключ с параметром State
        internal string AppTaskKey;
        // для служб
        internal int StartType;
    }

    // Автозагрузка: Run-ключи, папки «Автозагрузка», задания планировщика (кроме системных) и сторонние службы
    public static class Startup
    {
        const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        static readonly object[][] RunKeys =
        {
            new object[] { Reg.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run", "HKCU\\…\\Run" },
            new object[] { Reg.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run", "HKLM\\…\\Run" },
            new object[] { Reg.HKLM, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32", "HKLM\\…\\WOW6432Node\\…\\Run" },
        };

        static readonly Regex Junk = new Regex(@"update|updater|crash|telemetry|feedback|elevation|selfupdate", RegexOptions.IgnoreCase);

        public static List<StartupItem> List()
        {
            var res = new List<StartupItem>();

            // Реестр
            foreach (var rk in RunKeys)
            {
                var hive = (RegistryHive)rk[0];
                string key = (string)rk[1];
                using (var b = Reg.Base(hive))
                using (var k = b.OpenSubKey(key))
                {
                    if (k == null) continue;
                    foreach (var n in k.GetValueNames())
                    {
                        if (n.Length == 0) continue;
                        string cmd = Convert.ToString(k.GetValue(n));
                        var it = new StartupItem
                        {
                            Kind = StartKind.Registry, Name = n, Command = cmd, Exe = ExeFrom(cmd), Location = (string)rk[3],
                            Hive = hive, RunKey = key, ApprovedKey = Approved + (string)rk[2]
                        };
                        it.Enabled = ApprovedOn(hive, it.ApprovedKey, n);
                        res.Add(it);
                    }
                }
            }

            // Приложения Магазина: StartupTask (State: 2/5 — включено, 0/1/4 — отключено)
            try { AddAppTasks(res); } catch { }

            // Папки «Автозагрузка»
            AddFolder(res, Reg.HKCU, Environment.GetFolderPath(Environment.SpecialFolder.Startup), L.T("Автозагрузка пользователя"));
            AddFolder(res, Reg.HKLM, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), L.T("Общая автозагрузка"));

            // Задания (системные \Microsoft\Windows\ не показываем)
            try
            {
                foreach (var t in Tasks.EnumAll(@"\Microsoft\Windows\"))
                {
                    string exe = t.Exe == null ? null : Fs.E(t.Exe.Trim('"'));
                    res.Add(new StartupItem
                    {
                        Kind = StartKind.Task, Name = t.Name, Command = (t.Exe ?? "") + (string.IsNullOrEmpty(t.Args) ? "" : " " + t.Args),
                        Exe = exe, Location = L.T("Планировщик: ") + t.Path, Enabled = t.Enabled, FilePath = t.Path
                    });
                }
            }
            catch { }

            // Сторонние службы (не из папки Windows). Драйверы не трогаем.
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
            using (var b = Reg.Base(Reg.HKLM))
            using (var svcs = b.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
                if (svcs != null)
                    foreach (var name in svcs.GetSubKeyNames())
                    {
                        try
                        {
                            using (var k = svcs.OpenSubKey(name))
                            {
                                if (k == null) continue;
                                int type = Convert.ToInt32(k.GetValue("Type", 0));
                                if ((type & 0x30) == 0) continue;                       // не Win32-служба (драйвер)
                                if ((type & 0x40) != 0 || (type & 0x80) != 0) continue;  // шаблоны и экземпляры служб пользователя
                                string img = Convert.ToString(k.GetValue("ImagePath", "", RegistryValueOptions.DoNotExpandEnvironmentNames));
                                if (string.IsNullOrEmpty(img)) continue;
                                string exe = ExeFrom(img);
                                if (exe == null || exe.StartsWith(win, StringComparison.OrdinalIgnoreCase)) continue;
                                // Защитник Windows живёт в ProgramData/Program Files — это защита системы, не автозагрузка программ
                                if (exe.IndexOf(@"\Windows Defender", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                                int start = Convert.ToInt32(k.GetValue("Start", 3));
                                res.Add(new StartupItem
                                {
                                    Kind = StartKind.Service, Name = name, Command = img, Exe = exe, Location = L.T("Служба: ") + name,
                                    Enabled = start != 4, StartType = start   // «Вручную» тоже считается включённой
                                });
                            }
                        }
                        catch { }
                    }

            // Свои задания отложенного запуска (\btwTweaker\Delayed\…) — не отдельными пунктами, а меткой у исходной записи
            var delayed = res.FindAll(i => i.Kind == StartKind.Task && i.FilePath != null && i.FilePath.StartsWith(DelayFolder + "\\", StringComparison.OrdinalIgnoreCase));
            foreach (var d in delayed)
            {
                res.Remove(d);
                var src = res.Find(i => (i.Kind == StartKind.Registry || i.Kind == StartKind.File) && i.AppTaskKey == null && DelayName(i) == d.Name);
                if (src != null) src.Delayed = d.FilePath;
            }

            var impact = ImpactMap();
            foreach (var it in res)
            {
                it.Recommended = it.Kind != StartKind.Registry && it.Kind != StartKind.File && Junk.IsMatch(it.Name);
                string lvl;
                if (it.Exe != null && impact.TryGetValue(it.Exe.ToLowerInvariant(), out lvl)) it.Impact = lvl;
                if (it.Command != null && it.Exe != null)
                {
                    string c = Fs.E(it.Command.Trim());
                    int at = c.IndexOf(it.Exe, StringComparison.OrdinalIgnoreCase);
                    it.Args = at >= 0 ? c.Substring(at + it.Exe.Length).TrimStart('"').Trim() : "";
                }
            }
            return res;
        }

        // ───────── Влияние на запуск (данные Windows, как в Диспетчере задач) ─────────

        // %WINDIR%\System32\wdi\LogFiles\StartupInfo\<SID>_StartupInfo.xml — доступен только администратору.
        // Пороги Microsoft: высокое — >1 с ЦП или >3 МБ диска, среднее — >300 мс или >300 КБ, иначе низкое.
        static Dictionary<string, string> ImpactMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
                string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"wdi\LogFiles\StartupInfo\" + sid + "_StartupInfo.xml");
                if (!File.Exists(f)) return map;
                var doc = new System.Xml.XmlDocument();
                doc.Load(f);
                var cpu = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                var disk = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (System.Xml.XmlNode p in doc.GetElementsByTagName("Process"))
                {
                    var nameAttr = p.Attributes == null ? null : p.Attributes["Name"];
                    if (nameAttr == null) continue;
                    string exe = Fs.E(nameAttr.Value).ToLowerInvariant();
                    long c = Num(p, "CpuUsage"), d = Num(p, "DiskUsage");
                    long oc, od;
                    cpu[exe] = cpu.TryGetValue(exe, out oc) ? Math.Max(oc, c) : c;
                    disk[exe] = disk.TryGetValue(exe, out od) ? Math.Max(od, d) : d;
                }
                foreach (var exe in cpu.Keys)
                {
                    long c = cpu[exe], d = disk[exe];
                    map[exe] = c > 1000 || d > 3 * 1024 * 1024 ? L.T("Высокое") : c > 300 || d > 300 * 1024 ? L.T("Среднее") : L.T("Низкое");
                }
            }
            catch { }
            return map;
        }

        static long Num(System.Xml.XmlNode p, string tag)
        {
            var n = p[tag];
            long v;
            return n != null && long.TryParse(n.InnerText.Trim(), out v) ? v : 0;
        }

        // ───────── Отложенный запуск ─────────

        public const string DelayFolder = @"\btwTweaker\Delayed";

        static string DelayName(StartupItem it)
        {
            return Regex.Replace(it.Kind == StartKind.File ? Path.GetFileName(it.FilePath) : it.Name, @"[\\/:*?""<>|]", "_");
        }

        public static bool CanDelay(StartupItem it)
        {
            return (it.Kind == StartKind.Registry || it.Kind == StartKind.File) && it.AppTaskKey == null && !string.IsNullOrEmpty(it.Exe) && !it.Missing;
        }

        // Запуск через задание «при входе» с задержкой; исходная запись отключается (всё возвращает «Откатить всё»)
        public static void Delay(StartupItem it, int seconds)
        {
            string name = DelayName(it);
            string exe = it.Kind == StartKind.File ? it.FilePath : it.Exe;
            string args = it.Kind == StartKind.File ? "" : (it.Args ?? "");
            string path = DelayFolder + "\\" + name;
            Backup.SaveTaskAbsent(path);
            Tasks.CreateLogonTask(DelayFolder, name, exe, args, seconds, L.T("btwTweaker: отложенный запуск «") + it.Name + L.T("»"));
            if (it.Enabled) SetEnabled(it, false);
            it.Delayed = path;
        }

        public static void Undelay(StartupItem it)
        {
            if (it.Delayed == null) return;
            if (Tasks.Exists(it.Delayed)) Tasks.Delete(it.Delayed);
            it.Delayed = null;
            SetEnabled(it, true);
        }

        // ───────── Экспорт ─────────

        public static string ToCsv(List<StartupItem> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(L.T("Тип;Название;Состояние;Влияние;Издатель;Команда;Откуда"));
            string[] kinds = { L.T("Реестр"), L.T("Файл"), L.T("Задача"), L.T("Служба") };
            foreach (var i in items)
                sb.AppendLine(string.Join(";", new[] { kinds[(int)i.Kind], i.Name, i.Delayed != null ? L.T("Отложено") : i.Enabled ? L.T("Включено") : L.T("Отключено"), i.Impact ?? "", i.Publisher ?? "", i.Command ?? "", i.Location ?? "" }
                    .Select(s => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"")));
            return sb.ToString();
        }

        const string AppData = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";
        const string Repo = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

        static void AddAppTasks(List<StartupItem> res)
        {
            using (var b = Reg.Base(Reg.HKCU))
            using (var root = b.OpenSubKey(AppData))
            using (var repo = b.OpenSubKey(Repo))
            {
                if (root == null) return;
                string[] packages = repo == null ? new string[0] : repo.GetSubKeyNames();
                foreach (var fam in root.GetSubKeyNames())
                    using (var fk = root.OpenSubKey(fam))
                    {
                        if (fk == null) continue;
                        foreach (var task in fk.GetSubKeyNames())
                            using (var tk = fk.OpenSubKey(task))
                            {
                                object st = tk == null ? null : tk.GetValue("State");
                                if (st == null) continue;
                                int state = Convert.ToInt32(st);

                                // Название и папка пакета — из репозитория пакетов
                                string name = fam.Split('_')[0], folder = null;
                                string pub = fam.Substring(fam.LastIndexOf('_') + 1), pre = name + "_";
                                foreach (var full in packages)
                                    if (full.StartsWith(pre, StringComparison.OrdinalIgnoreCase) && full.EndsWith("_" + pub, StringComparison.OrdinalIgnoreCase))
                                        using (var pk = repo.OpenSubKey(full))
                                        {
                                            if (pk == null) continue;
                                            name = Debloat.ResolveName(name, full, Convert.ToString(pk.GetValue("DisplayName"))) ?? name;
                                            folder = Convert.ToString(pk.GetValue("PackageRootFolder"));
                                            break;
                                        }
                                res.Add(new StartupItem
                                {
                                    Kind = StartKind.Registry, Name = name, Command = fam + " → " + task,
                                    Exe = FirstExe(folder), Location = L.T("Приложение Магазина (StartupTask)"),
                                    Enabled = state == 2 || state == 5, Hive = Reg.HKCU, AppTaskKey = AppData + "\\" + fam + "\\" + task
                                });
                            }
                    }
            }
        }

        // Главный exe пакета — только для иконки
        static string FirstExe(string folder)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
                var top = Directory.GetFiles(folder, "*.exe");
                if (top.Length > 0) return top[0];
                foreach (var d in Directory.GetDirectories(folder))
                {
                    var f = Directory.GetFiles(d, "*.exe");
                    if (f.Length > 0) return f[0];
                }
            }
            catch { }
            return null;
        }

        static void AddFolder(List<StartupItem> res, RegistryHive hive, string dir, string where)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir))
            {
                string fn = Path.GetFileName(f);
                if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                var it = new StartupItem
                {
                    Kind = StartKind.File, Name = Path.GetFileNameWithoutExtension(f), Command = f, Exe = f, Location = where + ": " + fn,
                    Hive = hive, ApprovedKey = Approved + "StartupFolder", FilePath = f
                };
                it.Enabled = ApprovedOn(hive, it.ApprovedKey, fn);
                res.Add(it);
            }
        }

        // StartupApproved: первый байт чётный (02, 06) — включено, нечётный (03, 07) — отключено; нет значения — включено
        static bool ApprovedOn(RegistryHive h, string key, string name)
        {
            var v = Reg.Get(h, key, name) as byte[];
            return v == null || v.Length == 0 || (v[0] & 1) == 0;
        }

        // Путь к exe из командной строки: "C:\a b\x.exe" -arg, C:\x.exe /arg, %VAR%\x.exe, \SystemRoot\…
        public static string ExeFrom(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return null;
            cmd = Fs.E(cmd.Trim());
            if (cmd.StartsWith("\\SystemRoot\\", StringComparison.OrdinalIgnoreCase)) cmd = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + cmd.Substring(11);
            if (cmd.StartsWith("\\??\\")) cmd = cmd.Substring(4);
            if (cmd.StartsWith("\""))
            {
                int e = cmd.IndexOf('"', 1);
                return e > 1 ? cmd.Substring(1, e - 1) : cmd.Trim('"');
            }
            int x = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (x > 0) return cmd.Substring(0, x + 4);
            int sp = cmd.IndexOf(' ');
            return sp > 0 ? cmd.Substring(0, sp) : cmd;
        }

        public static string PublisherOf(string exe)
        {
            try
            {
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return null;
                var v = FileVersionInfo.GetVersionInfo(exe);
                return string.IsNullOrWhiteSpace(v.CompanyName) ? null : v.CompanyName.Trim();
            }
            catch { return null; }
        }

        // ───────── Действия ─────────

        public static void SetEnabled(StartupItem it, bool on)
        {
            if (it.AppTaskKey != null)
            {
                // Как переключатель в «Параметрах → Автозагрузка»: 2 — включено, 1 — отключено пользователем
                Reg.Apply(Reg.Dw(Reg.HKCU, it.AppTaskKey, "State", on ? 2 : 1));
                if (on) Reg.Apply(Reg.Dw(Reg.HKCU, it.AppTaskKey, "UserEnabledStartupOnce", 1));
                it.Enabled = on;
                return;
            }
            switch (it.Kind)
            {
                case StartKind.Registry:
                case StartKind.File:
                    string valueName = it.Kind == StartKind.File ? Path.GetFileName(it.FilePath) : it.Name;
                    var data = new byte[12];
                    if (on) data[0] = 2;
                    else
                    {
                        data[0] = 3;
                        Array.Copy(BitConverter.GetBytes(DateTime.Now.ToFileTime()), 0, data, 4, 8);
                    }
                    Reg.Apply(Reg.Bin(it.Hive, it.ApprovedKey, valueName, data));
                    break;
                case StartKind.Task:
                    Backup.SaveTask(it.FilePath);
                    Tasks.SetEnabled(it.FilePath, on);
                    break;
                case StartKind.Service:
                    // «Отключить» = служба отключена. «Включить» = прежний тип запуска (из резервной копии) или «Вручную».
                    int start = 4;
                    if (on)
                    {
                        int? orig = Backup.OriginalDword(Reg.HKLM, Reg.Services + it.Name, "Start");
                        start = orig.HasValue && orig.Value != 4 ? orig.Value : 3;
                    }
                    Reg.Apply(Reg.Start(it.Name, start));
                    it.StartType = start;
                    break;
            }
            it.Enabled = on;
        }

        public static bool CanDelete(StartupItem it) { return it.Kind != StartKind.Service && it.AppTaskKey == null; }

        public static void Delete(StartupItem it)
        {
            switch (it.Kind)
            {
                case StartKind.Registry:
                    Reg.Apply(Reg.Del(it.Hive, it.RunKey, it.Name));                 // вернёт «Откатить всё»
                    Reg.Apply(Reg.Del(it.Hive, it.ApprovedKey, it.Name));
                    break;
                case StartKind.File:
                    // в Корзину — оттуда можно восстановить
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(it.FilePath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    break;
                case StartKind.Task:
                    Backup.SaveTaskXml(it.FilePath, Tasks.Xml(it.FilePath));        // копия задания для «Откатить всё»
                    Tasks.Delete(it.FilePath);
                    break;
                default:
                    throw new Exception(L.T("Службы не удаляются — их можно только отключить."));
            }
        }

        // Своя программа в автозагрузку (HKCU\…\Run) — «Откатить всё» её уберёт
        public static void Add(string exe)
        {
            string name = Path.GetFileNameWithoutExtension(exe);
            Reg.Apply(Reg.Sz(Reg.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Run", name, "\"" + exe + "\""));
        }
    }
}
