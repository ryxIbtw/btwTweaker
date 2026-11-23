using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// wrap.exe wrap|extract <out-or-dash> files...
//  wrap    — обернуть обычные строковые литералы с кириллицей в L.T("...") (файлы перезаписываются, UTF-8 BOM)
//  extract — выписать уникальные литералы (в исходном экранированном виде) в файл, по одному на строку
class P
{
    static bool Cyr(string s) { foreach (char c in s) if ((c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё') return true; return false; }
    static readonly HashSet<string> Skip = new HashSet<string> { "отключ" };

    static int Main(string[] a)
    {
        string mode = a[0], outp = a[1];
        var found = new List<string>(); var seen = new HashSet<string>();
        int wrapped = 0, skipped = 0;
        for (int fi = 2; fi < a.Length; fi++)
        {
            string text = File.ReadAllText(a[fi], Encoding.UTF8);
            var sb = new StringBuilder();
            int i = 0, n = text.Length;
            while (i < n)
            {
                char c = text[i];
                if (c == '/' && i + 1 < n && text[i + 1] == '/') { int e = text.IndexOf('\n', i); if (e < 0) e = n; sb.Append(text, i, e - i); i = e; continue; }
                if (c == '/' && i + 1 < n && text[i + 1] == '*') { int e = text.IndexOf("*/", i + 2); e = e < 0 ? n : e + 2; sb.Append(text, i, e - i); i = e; continue; }
                if (c == '\'') { int j = i + 1; while (j < n && text[j] != '\'') { if (text[j] == '\\') j++; j++; } sb.Append(text, i, j + 1 - i); i = j + 1; continue; }
                if (c == '@' && i + 1 < n && text[i + 1] == '"')
                {
                    int j = i + 2;
                    while (j < n) { if (text[j] == '"') { if (j + 1 < n && text[j + 1] == '"') { j += 2; continue; } break; } j++; }
                    string lit = text.Substring(i, j + 1 - i);
                    if (Cyr(lit)) Console.Error.WriteLine("VERBATIM " + a[fi] + ": " + lit);
                    sb.Append(lit); i = j + 1; continue;
                }
                if (c == '"')
                {
                    int j = i + 1;
                    while (j < n && text[j] != '"') { if (text[j] == '\\') j++; j++; }
                    string body = text.Substring(i + 1, j - i - 1);
                    string lit = text.Substring(i, j + 1 - i);
                    i = j + 1;
                    if (!Cyr(body) || Skip.Contains(body)) { sb.Append(lit); continue; }
                    // контекст строки
                    int ls = sb.ToString().LastIndexOf('\n') + 1;
                    string before = sb.ToString(ls, sb.Length - ls);
                    bool already = before.EndsWith("L.T(");
                    bool bad = Regex.IsMatch(before, @"\bconst\b") || Regex.IsMatch(before, @"\bcase\s*$") || Regex.IsMatch(before, @"\bstring\s+\w+\s*=\s*$") && Regex.IsMatch(before, @"\(\s*[^)]*$") && !before.TrimStart().StartsWith("string");
                    if (seen.Add(body)) found.Add(body);
                    if (mode == "wrap" && !already && !bad) { sb.Append("L.T(" + lit + ")"); wrapped++; }
                    else { sb.Append(lit); if (bad && !already) { skipped++; Console.Error.WriteLine("SKIP " + Path.GetFileName(a[fi]) + ": " + before.Trim() + lit); } }
                    continue;
                }
                sb.Append(c); i++;
            }
            if (mode == "wrap") File.WriteAllText(a[fi], sb.ToString(), new UTF8Encoding(true));
        }
        if (mode == "extract") File.WriteAllLines(outp, found, new UTF8Encoding(false));
        Console.WriteLine("literals=" + found.Count + " wrapped=" + wrapped + " skipped=" + skipped);
        return 0;
    }
}
