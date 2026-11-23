using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace BtwTweaker
{
    // Резервная копия исходных значений. Каждое значение сохраняется один раз — перед первым изменением.
    // Файл: %LOCALAPPDATA%\BtwTweaker\backup.tsv
    // Строка: reg <TAB> hive <TAB> key <TAB> name <TAB> kind <TAB> data
    public static class Backup
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BtwTweaker");
        public static readonly string FilePath = Path.Combine(Dir, "backup.tsv");
        static HashSet<string> known;

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

        static readonly object sync = new object();

        // Твики применяются в фоне — запись в файл копии только по одному
        public static void SaveReg(RegistryHive h, string key, string name)
        {
            lock (sync) SaveRegLocked(h, key, name);
        }

        static void SaveRegLocked(RegistryHive h, string key, string name)
        {
            Load();
            string id = "reg|" + h + "|" + key + "|" + name;
            if (known.Contains(id)) return;

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
            Directory.CreateDirectory(Dir);
            File.AppendAllText(FilePath, "reg\t" + h + "\t" + key + "\t" + name + "\t" + kind + "\t" + data + "\r\n", Encoding.UTF8);
            known.Add(id);
        }

        // Запомнить, что ключ существовал (перед удалением) — чтобы откат мог его воссоздать
        public static void SaveKey(RegistryHive h, string key)
        {
            lock (sync)
            {
                Load();
                string id = "key|" + h + "|" + key + "|";
                if (known.Contains(id)) return;
                if (!Reg.KeyExists(h, key)) return;
                string def = Reg.DefaultValue(h, key);
                Directory.CreateDirectory(Dir);
                // "s" + base64 — значение по умолчанию есть (даже пустая строка); пусто — значения нет
                File.AppendAllText(FilePath, "key\t" + h + "\t" + key + "\t\tpresent\t" + (def == null ? "" : "s" + B64(def)) + "\r\n", Encoding.UTF8);
                known.Add(id);
            }
        }

        // Запомнить, что ключа не было (перед созданием) — откат его удалит
        public static void SaveKeyAbsent(RegistryHive h, string key)
        {
            lock (sync)
            {
                Load();
                string id = "nokey|" + h + "|" + key + "|";
                if (known.Contains(id)) return;
                if (Reg.KeyExists(h, key)) return;
                Directory.CreateDirectory(Dir);
                File.AppendAllText(FilePath, "nokey\t" + h + "\t" + key + "\t\tabsent\t\r\n", Encoding.UTF8);
                known.Add(id);
            }
        }

        // Запомнить, было ли задание планировщика включено (перед первым изменением)
        public static void SaveTask(string path)
        {
            lock (sync)
            {
                Load();
                string id = "task|-|" + path + "|";
                if (known.Contains(id)) return;
                bool? on = Tasks.IsEnabled(path);
                if (!on.HasValue) return;
                Directory.CreateDirectory(Dir);
                File.AppendAllText(FilePath, "task\t-\t" + path + "\t\tenabled\t" + (on.Value ? "1" : "0") + "\r\n", Encoding.UTF8);
                known.Add(id);
            }
        }

        // Сохранить копию файла (например, settings.json VS Code) перед первым изменением
        public static void SaveFile(string path)
        {
            lock (sync)
            {
                Load();
                string id = "file|-|" + path + "|";
                if (known.Contains(id) || !File.Exists(path)) return;
                string copy = Path.Combine(Dir, "files", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Path.GetFileName(path));
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(path, copy, true);
                File.AppendAllText(FilePath, "file\t-\t" + path + "\t\tcopy\t" + copy + "\r\n", Encoding.UTF8);
                known.Add(id);
            }
        }

        static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)); }
        static string UnB64(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }

        // Вернуть всё как было. Возвращает число восстановленных значений и список ошибок.
        public static int RestoreAll(List<string> errors)
        {
            lock (sync) return RestoreAllLocked(errors);
        }

        static int RestoreAllLocked(List<string> errors)
        {
            Load();
            if (!File.Exists(FilePath)) return 0;
            var lines = File.ReadAllLines(FilePath, Encoding.UTF8);
            int n = 0;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var p = lines[i].Split('\t');
                if (p.Length < 6) continue;
                try
                {
                    if (p[0] == "task")
                    {
                        Tasks.SetEnabled(p[2], p[5] == "1");
                        n++;
                        continue;
                    }
                    if (p[0] == "file")
                    {
                        if (File.Exists(p[5])) { File.Copy(p[5], p[2], true); n++; }
                        continue;
                    }
                    var h = (RegistryHive)Enum.Parse(typeof(RegistryHive), p[1]);
                    if (p[0] == "key")
                    {
                        Reg.CreateKey(h, p[2], p[5].Length == 0 ? null : UnB64(p[5].Substring(1)));
                        n++;
                        continue;
                    }
                    if (p[0] == "nokey")
                    {
                        using (var b = Reg.Base(h))
                            try { b.DeleteSubKeyTree(p[2], false); } catch { }
                        n++;
                        continue;
                    }
                    if (p[0] != "reg") continue;
                    string kind = p[4], d = p[5];
                    if (kind == "missing") Reg.Raw(h, p[2], p[3], null, RegistryValueKind.Unknown);
                    else
                    {
                        var vk = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), kind);
                        object v;
                        if (vk == RegistryValueKind.DWord) v = int.Parse(d);
                        else if (vk == RegistryValueKind.QWord) v = long.Parse(d);
                        else if (vk == RegistryValueKind.Binary) v = Convert.FromBase64String(d);
                        else if (vk == RegistryValueKind.MultiString) v = UnB64(d).Split('\0');
                        else v = UnB64(d);
                        Reg.Raw(h, p[2], p[3], v, vk);
                    }
                    n++;
                }
                catch (Exception ex) { errors.Add(p[2] + "\\" + p[3] + ": " + ex.Message); }
            }
            // Файл не удаляем — переименовываем, чтобы осталась история
            File.Move(FilePath, Path.Combine(Dir, "backup-restored-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".tsv"));
            known = null;
            return n;
        }
    }
}
