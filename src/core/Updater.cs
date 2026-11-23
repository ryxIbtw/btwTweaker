using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace BtwTweaker
{
    // Найденный релиз на GitHub
    public class ReleaseInfo
    {
        public Version Version;
        public string Tag;
        public string Notes;
        public string ExeUrl;
        public string ShaUrl;
    }

    // Самообновление из релизов GitHub (как в ZapretGUI): скачать, сверить SHA256, заменить exe, перезапуститься.
    public static class Updater
    {
        public const string Repo = "ryxIbtw/btwTweaker";
        public const string RepoUrl = "https://github.com/" + Repo;
        public const string ReleasesUrl = RepoUrl + "/releases";
        const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
        const string ExeName = "btwTweaker.exe";

        public static Version Current
        {
            get
            {
#if TESTHOOKS
                var fake = Environment.GetEnvironmentVariable("BTW_FAKE_VERSION");
                if (!string.IsNullOrEmpty(fake)) return Parse(fake);
#endif
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
        }

        public static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }
        public static string PrevPath { get { return ExePath + ".prev"; } }

        public static Version Parse(string tag)
        {
            var m = Regex.Match(tag ?? "", @"(\d+)\.(\d+)(?:\.(\d+))?");
            if (!m.Success) return new Version(0, 0, 0);
            return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
        }

        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2
            var wc = new WebClient { Encoding = System.Text.Encoding.UTF8 };
            wc.Headers["User-Agent"] = "btwTweaker";
            wc.Proxy = WebRequest.GetSystemWebProxy();
            return wc;
        }

        // Последний релиз. Бросает исключение, если GitHub недоступен или в релизе нет exe.
        public static ReleaseInfo Latest()
        {
            string json;
            using (var wc = Client())
            {
                wc.Headers["Accept"] = "application/vnd.github+json";
                json = wc.DownloadString(Api);
            }
            var rel = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (rel == null) throw new Exception(L.T("GitHub вернул непонятный ответ."));
            var r = new ReleaseInfo { Tag = Str(rel, "tag_name"), Notes = Str(rel, "body") };
            var assets = rel.ContainsKey("assets") ? rel["assets"] as object[] : null;
            if (assets != null)
                foreach (var o in assets)
                {
                    var a = o as Dictionary<string, object>;
                    if (a == null) continue;
                    string name = Str(a, "name"), url = Str(a, "browser_download_url");
                    if (string.Equals(name, ExeName, StringComparison.OrdinalIgnoreCase)) r.ExeUrl = url;
                    else if (string.Equals(name, ExeName + ".sha256", StringComparison.OrdinalIgnoreCase)) r.ShaUrl = url;
                }
            if (r.Tag == null || r.ExeUrl == null) throw new Exception(L.T("В последнем релизе нет файла ") + ExeName + ".");
            r.Version = Parse(r.Tag);
            return r;
        }

        static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : null;
        }

        public static string Sha256(byte[] data)
        {
            using (var h = SHA256.Create())
                return BitConverter.ToString(h.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        // Скачать, проверить контрольную сумму и подменить exe. Возвращает true, если сумма была сверена.
        public static bool DownloadAndInstall(ReleaseInfo r, string exePath)
        {
            string expected = null;
            if (r.ShaUrl != null)
            {
                string text;
                using (var wc = Client()) text = wc.DownloadString(r.ShaUrl);
                var m = Regex.Match(text, "[0-9a-fA-F]{64}");
                if (!m.Success) throw new Exception(L.T("Файл контрольной суммы в релизе повреждён — обновление отменено."));
                expected = m.Value.ToLowerInvariant();
            }
            byte[] data;
            using (var wc = Client()) data = wc.DownloadData(r.ExeUrl);
            if (expected != null && Sha256(data) != expected)
                throw new Exception(L.T("Контрольная сумма скачанного файла не совпала — обновление отменено."));
            Install(exePath, data);
            return expected != null;
        }

        // Новый exe на место текущего; текущий становится .old (запущенный файл переименовать можно)
        public static void Install(string exePath, byte[] data)
        {
            if (data == null || data.Length < 32 * 1024 || data[0] != (byte)'M' || data[1] != (byte)'Z')
                throw new Exception(L.T("Скачанный файл не похож на программу — обновление отменено."));
            string fresh = exePath + ".new", old = exePath + ".old";
            File.WriteAllBytes(fresh, data);
            if (File.Exists(old)) File.Delete(old);   // остаток прошлого обновления
            File.Move(exePath, old);
            try { File.Move(fresh, exePath); }
            catch { File.Move(old, exePath); throw; }
        }

        // После перезапуска: .old становится .prev — к нему можно вернуться
        public static void CleanupOld()
        {
            string exe = ExePath;
            try { if (File.Exists(exe + ".new")) File.Delete(exe + ".new"); } catch { }
            try
            {
                if (File.Exists(exe + ".old"))
                {
                    if (File.Exists(PrevPath)) File.Delete(PrevPath);
                    File.Move(exe + ".old", PrevPath);
                }
            }
            catch { }
        }

        public static string PrevVersion()
        {
            try
            {
                if (!File.Exists(PrevPath)) return null;
                var fv = FileVersionInfo.GetVersionInfo(PrevPath);
                return fv.FileMajorPart + "." + fv.FileMinorPart + "." + fv.FileBuildPart;
            }
            catch { return null; }
        }

        // Вернуть прошлую версию: текущий файл → .old (после перезапуска он станет .prev — можно вернуться обратно)
        public static void Rollback()
        {
            string exe = ExePath, old = exe + ".old";
            if (!File.Exists(PrevPath)) throw new Exception(L.T("Прошлая версия не сохранена."));
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try { File.Move(PrevPath, exe); }
            catch { File.Move(old, exe); throw; }
        }

        // Запустить новую копию (она дождётся закрытия этой) — вызывающий после этого закрывает программу
        public static void StartNew()
        {
            Process.Start(new ProcessStartInfo(ExePath, "--wait-pid " + Process.GetCurrentProcess().Id) { UseShellExecute = false });
        }

        // В начале запуска: если передан --wait-pid, дождаться закрытия старой копии
        public static void WaitForOld(string[] args)
        {
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--wait-pid")
                {
                    int pid;
                    if (!int.TryParse(args[i + 1], out pid)) return;
                    try { using (var p = Process.GetProcessById(pid)) p.WaitForExit(15000); } catch { }
                }
        }
    }
}
