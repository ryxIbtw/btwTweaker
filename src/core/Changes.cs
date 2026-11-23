using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace BtwTweaker
{
    // Одна вещь, которую меняет твик — для блока «Что меняется»
    public class ChangeInfo
    {
        public string Kind;        // Реестр | Служба | Задание | Файл | Журнал | Команда
        public string Title;       // путь к ключу, имя службы, путь задания…
        public string Detail;      // имя значения реестра и т.п.
        public List<string[]> Variants = new List<string[]>();   // { подпись, значение }
        public int Current = -1;   // какой вариант стоит сейчас (-1 — ни один)
        public string RegPath;     // полный путь для «Открыть в редакторе реестра»

        public ChangeInfo Add(string label, string value) { Variants.Add(new[] { label, value }); return this; }
    }

    public static class ChangeText
    {
        public static string HiveName(RegistryHive h) { return h == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE" : h == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : h.ToString(); }
        public static string HiveShort(RegistryHive h) { return h == RegistryHive.LocalMachine ? "HKLM" : h == RegistryHive.CurrentUser ? "HKCU" : h.ToString(); }

        static readonly Dictionary<int, string> StartNames = new Dictionary<int, string>
        {
            { 0, L.T("Загрузка") }, { 1, L.T("Система") }, { 2, L.T("Автоматически") }, { 3, L.T("Вручную") }, { 4, L.T("Отключена") }
        };

        // Текст значения для показа
        public static string Value(RegVal v)
        {
            if (v == null) return "—";
            if (v.Kind == RK.Delete) return L.T("нет значения");
            if (v.Kind == RK.DWord)
            {
                int i = (int)v.Data;
                return i < 0 || i > 0xFFFF ? "0x" + ((uint)i).ToString("X8") : i.ToString();
            }
            if (v.Kind == RK.Binary) return BitConverter.ToString((byte[])v.Data).Replace("-", " ");
            return "\"" + (string)v.Data + "\"";
        }

        static bool IsService(RegVal v) { return v.Hive == RegistryHive.LocalMachine && v.Key.StartsWith(Reg.Services, StringComparison.OrdinalIgnoreCase) && v.Name == "Start"; }

        // Пара значений «включено / выключено» для одного параметра реестра
        public static ChangeInfo FromReg(RegVal on, RegVal off)
        {
            var any = on ?? off;
            if (IsService(any))
            {
                var s = new ChangeInfo { Kind = L.T("Служба"), Title = any.Key.Substring(Reg.Services.Length), Detail = L.T("Тип запуска"), RegPath = HiveName(any.Hive) + "\\" + any.Key };
                s.Add(L.T("Вкл"), on != null ? StartName((int)on.Data) : "—").Add(L.T("Откл"), off != null ? StartName((int)off.Data) : "—");
                s.Current = CurrentOf(on, off);
                return s;
            }
            var c = new ChangeInfo { Kind = L.T("Реестр"), Title = HiveShort(any.Hive) + "\\" + any.Key, Detail = string.IsNullOrEmpty(any.Name) ? L.T("(по умолчанию)") : any.Name, RegPath = HiveName(any.Hive) + "\\" + any.Key };
            c.Add(L.T("Вкл"), Value(on)).Add(L.T("Откл"), Value(off));
            c.Current = CurrentOf(on, off);
            return c;
        }

        static string StartName(int v) { string s; return StartNames.TryGetValue(v, out s) ? s : v.ToString(); }

        static int CurrentOf(RegVal on, RegVal off)
        {
            try
            {
                if (on != null && Reg.Matches(on)) return 0;
                if (off != null && Reg.Matches(off)) return 1;
            }
            catch { }
            return -1;
        }

        public static ChangeInfo FromTask(string path)
        {
            var c = new ChangeInfo { Kind = L.T("Задание"), Title = path };
            c.Add(L.T("Вкл"), L.T("Включено")).Add(L.T("Откл"), L.T("Отключено"));
            try
            {
                bool? on = Tasks.IsEnabled(path);
                if (!on.HasValue) { c.Detail = L.T("нет в этой Windows"); return c; }
                c.Current = on.Value ? 0 : 1;
            }
            catch { }
            return c;
        }

        // Все параметры RegToggle: значения «вкл» и «выкл» объединены по ключу и имени
        public static List<ChangeInfo> FromLists(List<RegVal> onVals, List<RegVal> offVals)
        {
            var res = new List<ChangeInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var all = new List<RegVal>(offVals);
            all.AddRange(onVals);
            foreach (var v in all)
            {
                string id = v.Hive + "|" + v.Key + "|" + v.Name;
                if (!seen.Add(id)) continue;
                RegVal on = onVals.Find(x => x.Hive == v.Hive && string.Equals(x.Key, v.Key, StringComparison.OrdinalIgnoreCase) && x.Name == v.Name);
                RegVal off = offVals.Find(x => x.Hive == v.Hive && string.Equals(x.Key, v.Key, StringComparison.OrdinalIgnoreCase) && x.Name == v.Name);
                if (v.OnlyIfKeyExists && !SafeExists(v)) continue;   // службы, которой нет в этой Windows
                res.Add(FromReg(on, off));
            }
            return res;
        }

        static bool SafeExists(RegVal v) { try { return Reg.KeyExists(v.Hive, v.Key); } catch { return true; } }
    }
}
