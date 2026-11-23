using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BtwTweaker
{
    // Одна запись профиля: твик и его состояние
    public class ProfileItem
    {
        public string Key;
        public string Section;
        public bool? On;          // переключатель
        public int Index = -1;    // выбор из списка
        public string Option;     // текст варианта — только для чтения человеком
    }

    // Файл профиля (.btwprofile, JSON)
    public class ProfileFile
    {
        public string App = "btwTweaker";
        public int Format = 1;
        public string Version;
        public string Name;
        public string Created;
        public string Os;
        public string Pc;
        public List<ProfileItem> Tweaks = new List<ProfileItem>();
    }

    // Что изменится при применении профиля
    public class ProfileStep
    {
        public Tweak Tweak;
        public string Section;
        public bool On;
        public int Index = -1;
        public string From, To;
        public string Skip;       // почему нельзя применить (null — можно)
        public bool Risky { get { return Skip == null && Index < 0 && !On && (Tweak.Security != null || Tweak.Caution != null); } }
        public string Title { get { return Tweak.Title + ": " + From + " → " + To; } }
    }

    public static class Profiles
    {
        public const string Ext = ".btwprofile";

        // Снимок текущего состояния всех доступных твиков
        public static ProfileFile Capture(List<Section> sections, string name)
        {
            var p = new ProfileFile
            {
                Version = AppMain.Version,
                Name = name,
                Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Os = Sys.OsName,
                Pc = Environment.MachineName
            };
            foreach (var s in sections)
                foreach (var g in s.Groups)
                    foreach (var t in g.Tweaks)
                    {
                        if (!t.Usable()) continue;
                        try
                        {
                            var tt = t as ToggleTweak;
                            if (tt != null) { p.Tweaks.Add(new ProfileItem { Key = t.Id, Section = s.Title, On = tt.ReadEnabled() }); continue; }
                            var ct = t as ChoiceTweak;
                            if (ct != null)
                            {
                                int i = ct.ReadIndex();
                                if (i >= 0) p.Tweaks.Add(new ProfileItem { Key = t.Id, Section = s.Title, Index = i, Option = ct.Options[i] });
                            }
                        }
                        catch { }   // не смогли прочитать — просто не сохраняем
                    }
            return p;
        }

        public static void Save(string path, ProfileFile p)
        {
            var js = new JavaScriptSerializer();
            // По строке на твик — файл удобно читать и сравнивать
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"App\": " + js.Serialize(p.App) + ",\n");
            sb.Append("  \"Format\": " + p.Format + ",\n");
            sb.Append("  \"Version\": " + js.Serialize(p.Version) + ",\n");
            sb.Append("  \"Name\": " + js.Serialize(p.Name) + ",\n");
            sb.Append("  \"Created\": " + js.Serialize(p.Created) + ",\n");
            sb.Append("  \"Os\": " + js.Serialize(p.Os) + ",\n");
            sb.Append("  \"Pc\": " + js.Serialize(p.Pc) + ",\n");
            sb.Append("  \"Tweaks\": [\n");
            for (int i = 0; i < p.Tweaks.Count; i++)
            {
                var it = p.Tweaks[i];
                var d = new Dictionary<string, object> { { "Key", it.Key }, { "Section", it.Section } };
                if (it.On.HasValue) d["On"] = it.On.Value;
                else { d["Index"] = it.Index; d["Option"] = it.Option; }
                sb.Append("    " + js.Serialize(d) + (i < p.Tweaks.Count - 1 ? "," : "") + "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        public static ProfileFile Load(string path)
        {
            ProfileFile p;
            try { p = new JavaScriptSerializer().Deserialize<ProfileFile>(File.ReadAllText(path, Encoding.UTF8)); }
            catch (Exception ex) { throw new Exception(L.T("Файл повреждён или это не профиль btwTweaker. ") + ex.Message); }
            if (p == null || p.App != "btwTweaker" || p.Tweaks == null) throw new Exception(L.T("Это не профиль btwTweaker."));
            if (p.Format > 1) throw new Exception(L.T("Профиль сохранён более новой версией btwTweaker (") + p.Version + L.T("). Обновите программу."));
            return p;
        }

        // Сравнить профиль с этим ПК. notFound — твики, которых нет в этой версии программы.
        public static List<ProfileStep> Diff(List<Section> sections, ProfileFile p, List<string> notFound)
        {
            var map = new Dictionary<string, KeyValuePair<Tweak, string>>();
            foreach (var s in sections)
                foreach (var g in s.Groups)
                    foreach (var t in g.Tweaks)
                        if (!map.ContainsKey(t.Id)) map[t.Id] = new KeyValuePair<Tweak, string>(t, s.Title);

            var steps = new List<ProfileStep>();
            foreach (var it in p.Tweaks)
            {
                KeyValuePair<Tweak, string> kv;
                if (it.Key == null || !map.TryGetValue(it.Key, out kv)) { if (notFound != null) notFound.Add(it.Key); continue; }
                var t = kv.Key;
                var st = new ProfileStep { Tweak = t, Section = kv.Value };
                try
                {
                    var tt = t as ToggleTweak;
                    var ct = t as ChoiceTweak;
                    if (tt != null && it.On.HasValue)
                    {
                        st.On = it.On.Value;
                        st.To = st.On ? L.T("Включено") : L.T("Отключено");
                        if (!t.Usable()) { st.From = "—"; st.Skip = t.Win11Only && !Sys.IsWin11 ? L.T("только Windows 11") : L.T("не поддерживается на этом ПК"); steps.Add(st); continue; }
                        bool cur = tt.ReadEnabled();
                        if (cur == st.On) continue;
                        st.From = cur ? L.T("Включено") : L.T("Отключено");
                    }
                    else if (ct != null && !it.On.HasValue && it.Index >= 0)
                    {
                        if (it.Index >= ct.Options.Length) { if (notFound != null) notFound.Add(it.Key); continue; }
                        st.Index = it.Index;
                        st.To = ct.Options[it.Index];
                        if (!t.Usable()) { st.From = "—"; st.Skip = L.T("не поддерживается на этом ПК"); steps.Add(st); continue; }
                        int cur = ct.ReadIndex();
                        if (cur == it.Index) continue;
                        st.From = cur >= 0 ? ct.Options[cur] : ct.UnknownText();
                    }
                    else { if (notFound != null) notFound.Add(it.Key); continue; }
                    st.Skip = t.Blocker(st.Index >= 0 || st.On);
                }
                catch (Exception ex) { st.From = st.From ?? "?"; st.Skip = L.T("не удалось прочитать: ") + ex.Message; }
                steps.Add(st);
            }
            return steps;
        }

        // Применить один шаг (вызывается в фоне, внутри Journal.Do)
        public static void Apply(ProfileStep st)
        {
            if (st.Index >= 0) ((ChoiceTweak)st.Tweak).SetIndex(st.Index);
            else ((ToggleTweak)st.Tweak).SetEnabled(st.On);
        }
    }
}
