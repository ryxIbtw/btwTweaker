using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace BtwTweaker
{
    // Резервная копия исходных значений. Каждое значение сохраняется один раз — перед первым изменением.
    // Файл: %LOCALAPPDATA%\BtwTweaker\backup.tsv
    // Строка: вид <TAB> hive <TAB> ключ/путь <TAB> имя <TAB> тип <TAB> данные
    // Те же строки-снимки пишет журнал (Journal) — перед каждым действием, а не только перед первым.
    public static class Backup
    {
        public static readonly string Dir = DataDir();

        static string DataDir()
        {
#if TESTHOOKS
            // Тесты: свои резервная копия и журнал во временной папке, чтобы не трогать настоящие
            var d = Environment.GetEnvironmentVariable("BTW_DATA_DIR");
            if (!string.IsNullOrEmpty(d)) return d;
#endif
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BtwTweaker");
        }
        public static readonly string FilePath = Path.Combine(Dir, "backup.tsv");
        static HashSet<string> known;
        static readonly object sync = new object();

        static void Load()
        {
            if (known != null) return;
            known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(FilePath)) return;
            foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                var p = line.Split('\t');
                if (p.Length >= 4) known.Add(p[0] + "|" + p[1] + "|" + p[2] + "|" + p[3]);
            }
        }

        public static int Count
        {
            get { lock (sync) { Load(); return known.Count; } }
        }

        // Общая схема: снимок текущего состояния → в журнал (всегда) и в резервную копию (только первый раз)
        static void Save(string id, Func<string> snapshot)
        {
            lock (sync)
            {
                Load();
                bool first = !known.Contains(id);
                if (!first && !Journal.Active) return;
                string line = snapshot();
                if (line == null) return;
                Journal.Capture(id, line);
                if (!first) return;
                Directory.CreateDirectory(Dir);
                File.AppendAllText(FilePath, line + "\r\n", Encoding.UTF8);
                known.Add(id);
            }
        }

        // ───────── Снимки ─────────

        public static void SaveReg(RegistryHive h, string key, string name)
        {
            Save("reg|" + h + "|" + key + "|" + name, () =>
            {
                string kind = "missing", data = "";
                using (var b = Reg.Base(h))
                using (var k = b.OpenSubKey(key))
                {
                    object v = k == null ? null : k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (v != null)
                    {
                        var vk = k.GetValueKind(name);
                        kind = vk.ToString();
                        if (vk == RegistryValueKind.DWord) data = ((int)v).ToString();
                        else if (vk == RegistryValueKind.QWord) data = ((long)v).ToString();
                        else if (vk == RegistryValueKind.Binary) data = Convert.ToBase64String((byte[])v);
                        else if (vk == RegistryValueKind.MultiString) data = B64(string.Join("\0", (string[])v));
                        else data = B64(Convert.ToString(v));
                    }
                }
                return "reg\t" + h + "\t" + key + "\t" + name + "\t" + kind + "\t" + data;
            });
        }

        // Ключ существовал (перед удалением) — откат его воссоздаст
        public static void SaveKey(RegistryHive h, string key)
        {
            Save("key|" + h + "|" + key + "|", () =>
            {
                if (!Reg.KeyExists(h, key)) return null;
                string def = Reg.DefaultValue(h, key);
                // "s" + base64 — значение по умолчанию есть (даже пустая строка); пусто — значения нет
                return "key\t" + h + "\t" + key + "\t\tpresent\t" + (def == null ? "" : "s" + B64(def));
            });
        }

        // Ключа не было (перед созданием) — откат его удалит
        public static void SaveKeyAbsent(RegistryHive h, string key)
        {
            Save("nokey|" + h + "|" + key + "|", () => Reg.KeyExists(h, key) ? null : "nokey\t" + h + "\t" + key + "\t\tabsent\t");
        }

        // Было ли задание планировщика включено
        public static void SaveTask(string path)
        {
            Save("task|-|" + path + "|", () =>
            {
                bool? on = Tasks.IsEnabled(path);
                return on.HasValue ? "task\t-\t" + path + "\t\tenabled\t" + (on.Value ? "1" : "0") : null;
            });
        }

        // Задания не было — его создаёт программа; откат его удалит
        public static void SaveTaskAbsent(string path)
        {
            Save("notask|-|" + path + "|", () => Tasks.Exists(path) ? null : "notask\t-\t" + path + "\t\tabsent\t");
        }

        // Копия задания перед удалением (XML) — откат зарегистрирует его заново через schtasks
        public static void SaveTaskXml(string path, string xml)
        {
            if (string.IsNullOrEmpty(xml)) return;
            Save("taskxml|-|" + path + "|", () =>
            {
                string copy = Copy("task-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".xml");
                File.WriteAllText(copy, xml, Encoding.Unicode);   // schtasks ждёт XML в UTF-16
                return "taskxml\t-\t" + path + "\t\txml\t" + copy;
            });
        }

        // Копия файла (например, settings.json VS Code) перед изменением
        public static void SaveFile(string path)
        {
            Save("file|-|" + path + "|", () =>
            {
                if (!File.Exists(path)) return null;
                string copy = Copy(Path.GetFileName(path));
                File.Copy(path, copy, true);
                return "file\t-\t" + path + "\t\tcopy\t" + copy;
            });
        }

        // Прежнее значение настройки драйвера NVIDIA (глобальный профиль)
        public static void SaveNv(uint id, NvValue old)
        {
            Save("nv|-|" + id.ToString("X8") + "|", () =>
                "nv\t-\t" + id.ToString("X8") + "\t\tdrs\t" + (old.Found ? "1" : "0") + ";" + old.Value + ";" + (old.Predefined ? "1" : "0"));
        }

        static string Copy(string name)
        {
            string copy = Path.Combine(Dir, "files", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + "-" + name);
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            return copy;
        }

        // Исходное DWORD-значение из резервной копии (до первого изменения программой); null — не меняли или не DWORD
        public static int? OriginalDword(RegistryHive h, string key, string name)
        {
            lock (sync)
            {
                if (!File.Exists(FilePath)) return null;
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var p = line.Split('\t');
                    if (p.Length >= 6 && p[0] == "reg" && p[1] == h.ToString() && string.Equals(p[2], key, StringComparison.OrdinalIgnoreCase) && p[3] == name && p[4] == "DWord")
                    {
                        int v;
                        if (int.TryParse(p[5], out v)) return v;
                    }
                }
                return null;
            }
        }

        static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)); }
        static string UnB64(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }

        // ───────── Восстановление ─────────

        // Вернуть одно состояние из строки-снимка. Бросает исключение при ошибке.
        public static void RestoreLine(string line)
        {
            var p = line.Split('\t');
            if (p.Length < 6) return;
            switch (p[0])
            {
                case "task":
                    Tasks.SetEnabled(p[2], p[5] == "1");
                    return;
                case "notask":
                    if (Tasks.Exists(p[2])) Tasks.Delete(p[2]);
                    return;
                case "taskxml":
                    if (File.Exists(p[5]) && !Tasks.Exists(p[2]))
                    {
                        string o;
                        if (Sys.Run("schtasks.exe", "/Create /TN \"" + p[2] + "\" /XML \"" + p[5] + "\" /F", out o) != 0)
                            throw new Exception("schtasks: " + o.Trim());
                    }
                    return;
                case "nv":
                    var nvd = p[5].Split(';');
                    NvDrs.Restore(Convert.ToUInt32(p[2], 16), new NvValue { Found = nvd[0] == "1", Value = uint.Parse(nvd[1]), Predefined = nvd[2] == "1" });
                    return;
                case "file":
                    if (File.Exists(p[5])) File.Copy(p[5], p[2], true);
                    return;
            }
            var h = (RegistryHive)Enum.Parse(typeof(RegistryHive), p[1]);
            if (p[0] == "key") { Reg.CreateKey(h, p[2], p[5].Length == 0 ? null : UnB64(p[5].Substring(1))); return; }
            if (p[0] == "nokey")
            {
                using (var b = Reg.Base(h))
                    try { b.DeleteSubKeyTree(p[2], false); } catch { }
                return;
            }
            if (p[0] != "reg") return;
            string kind = p[4], d = p[5];
            if (kind == "missing") { Reg.Raw(h, p[2], p[3], null, RegistryValueKind.Unknown); return; }
            var vk = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), kind);
            object val;
            if (vk == RegistryValueKind.DWord) val = int.Parse(d);
            else if (vk == RegistryValueKind.QWord) val = long.Parse(d);
            else if (vk == RegistryValueKind.Binary) val = Convert.FromBase64String(d);
            else if (vk == RegistryValueKind.MultiString) val = UnB64(d).Split('\0');
            else val = UnB64(d);
            Reg.Raw(h, p[2], p[3], val, vk);
        }

        // Понятное название строки для журнала и сообщений об ошибках
        public static string Describe(string line)
        {
            var p = line.Split('\t');
            if (p.Length < 6) return line;
            switch (p[0])
            {
                case "reg": return (p[1] == "LocalMachine" ? "HKLM" : p[1] == "CurrentUser" ? "HKCU" : p[1]) + "\\" + p[2] + " → " + (p[3].Length == 0 ? L.T("(по умолчанию)") : p[3]);
                case "key": case "nokey": return L.T("Ключ ") + (p[1] == "LocalMachine" ? "HKLM" : "HKCU") + "\\" + p[2];
                case "task": case "notask": case "taskxml": return L.T("Задание ") + p[2];
                case "nv": return L.T("Драйвер NVIDIA 0x") + p[2];
                case "file": return L.T("Файл ") + p[2];
            }
            return line;
        }

        // Значение из строки-снимка — для показа «было: …»
        public static string ValueText(string line)
        {
            var p = line.Split('\t');
            if (p.Length < 6) return "";
            try
            {
                switch (p[0])
                {
                    case "task": return p[5] == "1" ? L.T("включено") : L.T("отключено");
                    case "notask": return L.T("задания не было");
                    case "taskxml": return L.T("задание было (копия сохранена)");
                    case "key": return L.T("ключ был");
                    case "nokey": return L.T("ключа не было");
                    case "file": return L.T("копия файла сохранена");
                    case "nv":
                        var d = p[5].Split(';');
                        return d[0] == "1" ? "0x" + uint.Parse(d[1]).ToString("X8") + (d[2] == "1" ? L.T(" (значение NVIDIA)") : "") : L.T("не задано (по умолчанию)");
                    case "reg":
                        if (p[4] == "missing") return L.T("нет значения");
                        if (p[4] == "DWord" || p[4] == "QWord") return p[5];
                        if (p[4] == "Binary") { var b = Convert.FromBase64String(p[5]); return BitConverter.ToString(b, 0, Math.Min(b.Length, 12)).Replace("-", " ") + (b.Length > 12 ? " …" : ""); }
                        if (p[4] == "MultiString") return "\"" + UnB64(p[5]).Replace("\0", "; ") + "\"";
                        return "\"" + UnB64(p[5]) + "\"";
                }
            }
            catch { }
            return p[5];
        }

        // Вернуть всё как было. Возвращает число восстановленных значений и список ошибок.
        public static int RestoreAll(List<string> errors)
        {
            int n = 0;
            lock (sync)
            {
                Load();
                if (!File.Exists(FilePath)) return 0;
                var lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    if (lines[i].Split('\t').Length < 6) continue;
                    try { RestoreLine(lines[i]); n++; }
                    catch (Exception ex) { errors.Add(Describe(lines[i]) + ": " + ex.Message); }
                }
                // Файл не удаляем — переименовываем, чтобы осталась история
                File.Move(FilePath, Path.Combine(Dir, "backup-restored-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".tsv"));
                known = null;
            }
            Journal.AllRolledBack(n);
            return n;
        }
    }
}
