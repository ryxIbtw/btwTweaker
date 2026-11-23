using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BtwTweaker
{
    // Слежение за твиками: какое состояние выбрал пользователь — чтобы при запуске заметить,
    // что Windows (обычно крупное обновление) или другая программа вернула настройку назад.
    // Файл: %LOCALAPPDATA%\BtwTweaker\watch.json — список ProfileItem (тот же формат, что в профилях).
    public static class Watch
    {
        static readonly string FilePath = Path.Combine(Backup.Dir, "watch.json");
        static readonly object sync = new object();
        public static List<Section> Sections;   // каталог программы — для первого заполнения из журнала

        static List<ProfileItem> LoadLocked()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return new JavaScriptSerializer().Deserialize<List<ProfileItem>>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? new List<ProfileItem>();
            }
            catch { return new List<ProfileItem>(); }
        }

        static void SaveLocked(List<ProfileItem> all)
        {
            Directory.CreateDirectory(Backup.Dir);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(all), Encoding.UTF8);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }

        // Запомнить текущее состояние твика (вызывать после успешного изменения)
        public static void Remember(Tweak t, string section)
        {
            ProfileItem it = null;
            try
            {
                var tt = t as ToggleTweak;
                var ct = t as ChoiceTweak;
                if (tt != null) it = new ProfileItem { Key = t.Id, Section = section, On = tt.ReadEnabled() };
                else if (ct != null)
                {
                    int i = ct.ReadIndex();
                    if (i >= 0) it = new ProfileItem { Key = t.Id, Section = section, Index = i, Option = ct.Options[i] };
                }
            }
            catch { }
            lock (sync)
            {
                var all = LoadLocked() ?? (Sections != null ? Seed(Sections) : new List<ProfileItem>());
                all.RemoveAll(x => x.Key == t.Id);
                if (it != null) all.Add(it);
                SaveLocked(all);
            }
        }

        public static void Forget(IEnumerable<string> ids)
        {
            lock (sync)
            {
                var all = LoadLocked();
                if (all == null) return;
                var set = new HashSet<string>(ids);
                all.RemoveAll(x => set.Contains(x.Key));
                SaveLocked(all);
            }
        }

        public static void Forget(string id) { Forget(new[] { id }); }

        public static void Clear() { lock (sync) SaveLocked(new List<ProfileItem>()); }

        public static int Count { get { lock (sync) { var a = LoadLocked(); return a == null ? 0 : a.Count; } } }

        // Что сброшено: шаги «сейчас → как вы выбрали». Без неподдерживаемых на этом ПК.
        public static List<ProfileStep> Check(List<Section> sections)
        {
            List<ProfileItem> all;
            lock (sync)
            {
                all = LoadLocked();
                if (all == null) { all = Seed(sections); SaveLocked(all); }
            }
            var p = new ProfileFile { Tweaks = all };
            var steps = Profiles.Diff(sections, p, null);
            steps.RemoveAll(s => s.From == "—");   // твик недоступен на этом ПК
            return steps;
        }

        // Первый запуск слежения: восстановить выбор из журнала (записи «Название → Состояние», новые сверху)
        static List<ProfileItem> Seed(List<Section> sections)
        {
            var byTitle = new Dictionary<string, KeyValuePair<Tweak, string>>();
            foreach (var s in sections)
                foreach (var g in s.Groups)
                    foreach (var t in g.Tweaks)
                        if (!byTitle.ContainsKey(t.Title)) byTitle[t.Title] = new KeyValuePair<Tweak, string>(t, s.Title);

            var list = new List<ProfileItem>();
            var seen = new HashSet<string>();
            foreach (var e in Journal.All())
            {
                if (e.Irreversible || e.Title == null) continue;
                string title = e.Title;
                if (title.StartsWith("Профиль «")) { int c = title.IndexOf("»: "); if (c > 0) title = title.Substring(c + 3); }
                if (title.StartsWith("Возврат после сброса: ")) title = title.Substring("Возврат после сброса: ".Length);
                int arrow = title.LastIndexOf(" → ");
                if (arrow <= 0) continue;
                string name = title.Substring(0, arrow), value = title.Substring(arrow + 3);
                KeyValuePair<Tweak, string> kv;
                if (!byTitle.TryGetValue(name, out kv)) continue;
                var tw = kv.Key;
                if (!seen.Add(tw.Id)) continue;     // уже взяли более новую запись
                if (e.RolledBack) continue;         // последнее действие откачено — не следим
                if (tw is ToggleTweak && (value == "Включено" || value == "Отключено"))
                    list.Add(new ProfileItem { Key = tw.Id, Section = kv.Value, On = value == "Включено" });
                else if (tw is ChoiceTweak)
                {
                    int i = Array.IndexOf(((ChoiceTweak)tw).Options, value);
                    if (i >= 0) list.Add(new ProfileItem { Key = tw.Id, Section = kv.Value, Index = i, Option = value });
                }
            }
            return list;
        }

#if TESTHOOKS
        public static string TestSeed(List<Section> sections)
        {
            var sb = new StringBuilder();
            foreach (var it in Seed(sections)) sb.AppendLine(it.Section + " | " + it.Key + " = " + (it.On.HasValue ? (it.On.Value ? "Вкл" : "Откл") : it.Option));
            return sb.ToString();
        }
#endif
    }
}
