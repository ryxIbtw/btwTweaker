using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BtwTweaker
{
    // Язык интерфейса. В коде все тексты по-русски: L.T("…") возвращает перевод, если выбран английский.
    // Словарь — встроенный ресурс en.tsv: «русский<TAB>английский», \n \t \" \\ экранированы как в C#.
    public static class L
    {
        static readonly Dictionary<string, string> en = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> back = new Dictionary<string, string>(StringComparer.Ordinal);
        public static bool En;
        // Настройка: system / ru / en
        public static string Choice = "system";

        static L()
        {
            Choice = Sys.Pref("Lang") ?? "system";
            string forced = Environment.GetEnvironmentVariable("BTW_LANG");   // для проверок
            if (!string.IsNullOrEmpty(forced)) Choice = forced;
            En = Choice == "en" || (Choice != "ru" && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "ru");
            try { Load(); } catch { }
        }

        static void Load()
        {
            using (var s = typeof(L).Assembly.GetManifestResourceStream("en.tsv"))
            {
                if (s == null) return;
                using (var r = new StreamReader(s, Encoding.UTF8))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        int tab = line.IndexOf('\t');
                        if (tab <= 0 || line.StartsWith("#")) continue;
                        string ru = Unescape(line.Substring(0, tab)), tr = Unescape(line.Substring(tab + 1));
                        if (tr.Length == 0) continue;
                        en[ru] = tr;
                        if (!back.ContainsKey(tr)) back[tr] = ru;
                    }
                }
            }
        }

        static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char d = s[++i];
                    if (d == 'n') sb.Append('\n');
                    else if (d == 't') sb.Append('\t');
                    else if (d == 'r') sb.Append('\r');
                    else if (d == 'u' && i + 4 < s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(d);
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string T(string ru)
        {
            if (!En || ru == null) return ru;
            string r;
            if (en.TryGetValue(ru, out r)) return r;
#if TESTHOOKS
            lock (misses) misses.Add(ru);
#endif
            return ru;
        }

        // Русский оригинал по переводу (для постоянного ключа твика)
        public static string Ru(string text)
        {
            if (!En || text == null) return text;
            string r;
            return back.TryGetValue(text, out r) ? r : text;
        }

        // Число со словом: N(5, "твик", "твика", "твиков", "tweak", "tweaks") → «5 твиков» / «5 tweaks»
        public static string N(int n, string one, string few, string many, string enOne, string enMany)
        {
            if (En) return n + " " + (n == 1 ? enOne : enMany);
            int m10 = n % 10, m100 = n % 100;
            return n + " " + (m10 == 1 && m100 != 11 ? one : m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? few : many);
        }

        // Выбрать текст по языку прямо в коде (для склеенных фраз, где перевод по кусочкам неудобен)
        public static string Pick(string ru, string english) { return En ? english : ru; }

#if TESTHOOKS
        static readonly HashSet<string> misses = new HashSet<string>();
        public static List<string> Misses() { lock (misses) return new List<string>(misses); }
        public static bool Has(string ru) { return en.ContainsKey(ru); }
        public static int Count { get { return en.Count; } }
#endif
    }
}
