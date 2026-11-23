using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace BtwTweaker
{
    public class AppxInfo
    {
        public string Name;          // Microsoft.BingNews
        public string FullName;      // Microsoft.BingNews_4.1…_x64__8wekyb3d8bbwe
        public string Family;        // Microsoft.BingNews_8wekyb3d8bbwe
        public string DisplayName;
        public string Location;
        public string Logo;          // путь к найденной картинке или null
        public bool Recommended;     // входит в «Оптимальное»
    }

    // Выпиливание: приложения UWP, OneDrive, Edge
    public static class Debloat
    {
        // «Оптимальное»: известные предустановленные приложения, которые обычно не нужны
        public static readonly string[] Bloat =
        {
            "Clipchamp.Clipchamp", "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.BingSearch", "Microsoft.GetHelp",
            "Microsoft.Getstarted", "Microsoft.MicrosoftOfficeHub", "Microsoft.MicrosoftSolitaireCollection", "Microsoft.People",
            "Microsoft.PowerAutomateDesktop", "Microsoft.Todos", "Microsoft.WindowsFeedbackHub", "Microsoft.WindowsMaps",
            "Microsoft.ZuneVideo", "MicrosoftCorporationII.MicrosoftFamily", "Microsoft.549981C3F5F10", "Microsoft.MixedReality.Portal",
            "Microsoft.Microsoft3DViewer", "Microsoft.SkypeApp", "MicrosoftTeams", "MSTeams", "Microsoft.OutlookForWindows",
            "Microsoft.Windows.DevHome", "Microsoft.MicrosoftStickyNotes", "Microsoft.Copilot", "Microsoft.Wallet",
        };

        // Никогда не показываем и не удаляем: без них ломается Магазин, установка приложений или защита
        static readonly string[] Protected =
        {
            "Microsoft.WindowsStore", "Microsoft.DesktopAppInstaller", "Microsoft.StorePurchaseApp", "Microsoft.SecHealthUI",
            "Microsoft.Windows.ShellExperienceHost", "Microsoft.Windows.StartMenuExperienceHost", "MicrosoftWindows.Client.",
            "Microsoft.WindowsAppRuntime", "Microsoft.VCLibs", "Microsoft.UI.Xaml", "Microsoft.NET.", "Microsoft.Services.Store",
            "Microsoft.WebpImageExtension_DUMMY", "Microsoft.Windows.CloudExperienceHost", "Microsoft.AAD.BrokerPlugin",
            "Microsoft.AccountsControl", "Microsoft.LockApp", "Microsoft.Win32WebViewHost", "Microsoft.Winget.Source",
        };

        public static bool IsProtected(string name)
        {
            foreach (var p in Protected) if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ───────── Приложения UWP ─────────

        const string ListScript = @"
Get-AppxPackage | Where-Object { -not $_.IsFramework -and -not $_.NonRemovable -and $_.SignatureKind -ne 'System' } | ForEach-Object {
  $m = $null; try { $m = Get-AppxPackageManifest -Package $_.PackageFullName } catch {}
  $d = $null; $l = $null
  if ($m) { $d = [string]$m.Package.Properties.DisplayName; $l = [string]$m.Package.Properties.Logo }
  [pscustomobject]@{ N = $_.Name; F = $_.PackageFullName; Fam = $_.PackageFamilyName; L = $_.InstallLocation; D = $d; Logo = $l }
} | ConvertTo-Json -Compress";

        public static List<AppxInfo> ListApps()
        {
            string json, err;
            int code = Sys.RunPowerShell(ListScript, out json, out err);
            if (string.IsNullOrWhiteSpace(json))
            {
                if (code != 0) throw new Exception(L.T("Не удалось получить список приложений.\n\n") + err.Trim());
                return new List<AppxInfo>();
            }
            json = json.Trim();
            int start = json.IndexOfAny(new[] { '[', '{' });
            if (start < 0) return new List<AppxInfo>();
            object parsed = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json.Substring(start));
            var items = parsed as object[] ?? new[] { parsed };
            var res = new List<AppxInfo>();
            foreach (var o in items)
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                string name = S(d, "N");
                if (string.IsNullOrEmpty(name) || IsProtected(name)) continue;
                var a = new AppxInfo { Name = name, FullName = S(d, "F"), Family = S(d, "Fam"), Location = S(d, "L") };
                a.DisplayName = Resolve(a, S(d, "D")) ?? name;
                a.Logo = FindLogo(a.Location, S(d, "Logo"));
                a.Recommended = Bloat.Any(b => string.Equals(b, name, StringComparison.OrdinalIgnoreCase));
                res.Add(a);
            }
            res.Sort((x, y) => string.Compare(x.DisplayName, y.DisplayName, StringComparison.CurrentCultureIgnoreCase));
            return res;
        }

        static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : null; }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int SHLoadIndirectString(string source, StringBuilder outBuf, int size, IntPtr reserved);

        // Строка вида «@{пакет?ms-resource://…}» или «@файл,-id» → текст; null — не получилось
        public static string Indirect(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            if (!s.StartsWith("@")) return s;
            var sb = new StringBuilder(512);
            return SHLoadIndirectString(s, sb, sb.Capacity, IntPtr.Zero) == 0 && sb.Length > 0 ? sb.ToString() : null;
        }

        // Название пакета по его полному имени (для «Автозагрузки»)
        public static string ResolveName(string name, string fullName, string raw)
        {
            if (raw != null && raw.StartsWith("@")) return Indirect(raw);
            return Resolve(new AppxInfo { Name = name, FullName = fullName }, raw);
        }

        // Название из ресурсов пакета (ms-resource:…)
        static string Resolve(AppxInfo a, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            if (!raw.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)) return raw;
            string key = raw.Substring("ms-resource:".Length);
            var tries = new List<string>();
            if (key.StartsWith("//")) tries.Add("ms-resource:" + key);
            else if (key.StartsWith("/")) tries.Add("ms-resource://" + a.Name + key);
            else
            {
                tries.Add("ms-resource://" + a.Name + "/resources/" + key);
                tries.Add("ms-resource://" + a.Name + "/" + key);
                tries.Add("ms-resource:" + key);
            }
            foreach (var t in tries)
            {
                var sb = new StringBuilder(512);
                if (SHLoadIndirectString("@{" + a.FullName + "? " + t + "}", sb, sb.Capacity, IntPtr.Zero) == 0 && sb.Length > 0 && !sb.ToString().StartsWith("ms-resource"))
                    return sb.ToString();
            }
            return null;
        }

        // Логотип: в манифесте «Assets\StoreLogo.png», на диске — StoreLogo.scale-100.png и т.п.
        static string FindLogo(string dir, string rel)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(rel)) return null;
                string full = Path.Combine(dir, rel);
                if (File.Exists(full)) return full;
                string folder = Path.GetDirectoryName(full), bas = Path.GetFileNameWithoutExtension(full), ext = Path.GetExtension(full);
                if (!Directory.Exists(folder)) return null;
                var files = Directory.GetFiles(folder, bas + ".*" + ext);
                if (files.Length == 0) return null;
                // предпочитаем scale-200 / scale-100 и без контрастных вариантов
                var good = files.Where(f => f.IndexOf("contrast", StringComparison.OrdinalIgnoreCase) < 0).ToArray();
                if (good.Length == 0) good = files;
                return good.FirstOrDefault(f => f.Contains("scale-200")) ?? good.FirstOrDefault(f => f.Contains("scale-100")) ?? good[0];
            }
            catch { return null; }
        }

        public static long Size(AppxInfo a)
        {
            return string.IsNullOrEmpty(a.Location) || !Directory.Exists(a.Location) ? 0 : Fs.Size(a.Location);
        }

        // Удалить приложение у текущего пользователя; data — заодно папку его данных
        public static void RemoveApp(AppxInfo a, bool data)
        {
            string o;
            int code = Sys.RunPowerShell("$ErrorActionPreference='Stop'; Remove-AppxPackage -Package '" + a.FullName.Replace("'", "''") + "'", out o);
            if (code != 0) throw new Exception(L.T("Windows не дал удалить «") + a.DisplayName + L.T("».\n\n") + o.Trim());
            if (data && !string.IsNullOrEmpty(a.Family))
            {
                var r = new CleanResult();
                Fs.RemoveDir(Path.Combine(Fs.E(@"%LOCALAPPDATA%\Packages"), a.Family), r);
            }
        }

        // ───────── OneDrive ─────────

        static string[] OneDriveExes
        {
            get
            {
                return new[]
                {
                    Fs.E(@"%LOCALAPPDATA%\Microsoft\OneDrive\OneDrive.exe"),
                    Fs.E(@"%ProgramFiles%\Microsoft OneDrive\OneDrive.exe"),
                    Fs.E(@"%ProgramFiles(x86)%\Microsoft OneDrive\OneDrive.exe"),
                };
            }
        }

        public static bool OneDriveInstalled() { return OneDriveExes.Any(File.Exists); }

        public static void RemoveOneDrive()
        {
            foreach (var p in Process.GetProcessesByName("OneDrive")) try { p.Kill(); p.WaitForExit(5000); } catch { }

            // Штатный удалитель: из папки пользователя или системный
            var setups = new List<string>();
            foreach (var d in Fs.SubDirs(Fs.E(@"%LOCALAPPDATA%\Microsoft\OneDrive"), "*.*"))
                setups.AddRange(Fs.Glob(d, "OneDriveSetup.exe"));
            setups.Add(Fs.E(@"%WINDIR%\System32\OneDriveSetup.exe"));
            setups.Add(Fs.E(@"%WINDIR%\SysWOW64\OneDriveSetup.exe"));
            string o;
            foreach (var s in setups)
                if (File.Exists(s)) { Sys.Run(s, "/uninstall", out o); if (!OneDriveInstalled()) break; }
            foreach (var s in setups)
                if (File.Exists(s) && OneDriveInstalled()) Sys.Run(s, "/uninstall /allusers", out o);
            if (OneDriveInstalled()) throw new Exception(L.T("Удалитель OneDrive не справился — попробуйте удалить его через «Установленные приложения»."));

            // Остатки: программа, кэш, задачи обновления, значок в Проводнике. Папка с файлами пользователя не трогается.
            var r = new CleanResult();
            Fs.RemoveDir(Fs.E(@"%LOCALAPPDATA%\Microsoft\OneDrive"), r);
            Fs.RemoveDir(Fs.E(@"%PROGRAMDATA%\Microsoft OneDrive"), r);
            Sys.RunPowerShell("Get-ScheduledTask -TaskName 'OneDrive*' -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false", out o);
            Reg.Apply(Reg.Dw(Reg.HKCU, @"Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", "System.IsPinnedToNameSpaceTree", 0));
            Reg.Apply(Reg.Del(Reg.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Run", "OneDrive"));
        }

        // ───────── Microsoft Edge ─────────

        static string EdgeApp { get { return Fs.E(@"%ProgramFiles(x86)%\Microsoft\Edge\Application"); } }

        public static bool EdgeInstalled() { return File.Exists(Path.Combine(EdgeApp, "msedge.exe")); }

        public static void RemoveEdge()
        {
            string setup = null;
            foreach (var d in Fs.SubDirs(EdgeApp, "*.*"))
            {
                var s = Path.Combine(d, @"Installer\setup.exe");
                if (File.Exists(s)) setup = s;
            }
            if (setup == null) throw new Exception(L.T("Не найден установщик Edge (Application\\<версия>\\Installer\\setup.exe)."));
            foreach (var p in Process.GetProcessesByName("msedge")) try { p.Kill(); } catch { }

            // Разрешить удаление (видимость в «Программах» и флаг для служебного удалителя)
            Reg.Apply(Reg.Dw(Reg.HKLM, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge", "NoRemove", 0));
            Reg.Apply(Reg.Dw(Reg.HKLM, @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdateDev", "AllowUninstall", 1));
            string o;
            Sys.Run(setup, "--uninstall --system-level --verbose-logging --force-uninstall", out o);
            System.Threading.Thread.Sleep(2000);
            if (EdgeInstalled())
                throw new Exception(L.T("Windows не разрешил удалить Edge в вашем регионе. Edge остался на месте, система не пострадала."));
        }
    }
}
