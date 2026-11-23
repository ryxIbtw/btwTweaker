using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BtwTweaker
{
    public static class CleanCatalog
    {
        static List<string> Lst(params string[] p) { return p.Select(Fs.E).ToList(); }

        public static List<CleanGroup> Build()
        {
            return new List<CleanGroup>
            {
                new CleanGroup(L.T("Конфиденциальность"), new TracesCleaner()) { CleanAll = false },
                new CleanGroup("Windows", Temp(), Thumbs(), UpdateCache(), SetupLogs(), DefenderLogs(), ErrorReports(), DiagData(), ChkDsk()),
                new CleanGroup(L.T("Графика"), ShaderCache(), Nvidia()),
                new CleanGroup(L.T("Браузеры"),
                    Chromium(L.T("Кэш Google Chrome"), @"%LOCALAPPDATA%\Google\Chrome\User Data", "\uE774", new[] { "chrome" }, "Google Chrome"),
                    Chromium(L.T("Кэш Microsoft Edge"), @"%LOCALAPPDATA%\Microsoft\Edge\User Data", "\uE774", new[] { "msedge" }, "Microsoft Edge"),
                    Chromium(L.T("Кэш Brave"), @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data", "\uE774", new[] { "brave" }, "Brave"),
                    VivaldiOpera(), Firefox()),
                new CleanGroup(L.T("Разработка"), VisualStudio(), NuGet(), NodeCaches(), DotNet()),
                new CleanGroup(L.T("Приложения"), new UwpLeftovers()) { CleanAll = false },
                new CleanGroup(L.T("Устройства"), new GhostDevices()) { CleanAll = false },
            };
        }

        // ───────── Windows ─────────

        static Cleaner Temp()
        {
            return new PathCleaner
            {
                Title = L.T("Временные файлы"),
                Description = L.T("Временные файлы программ и Windows (%TEMP% и C:\\Windows\\Temp). Файлы, которые сейчас используются, пропускаются."),
                Icon = "\uE81C",
                Dirs = () => Lst(@"%TEMP%", @"%WINDIR%\Temp"),
            };
        }

        static Cleaner Thumbs()
        {
            const string exp = @"%LOCALAPPDATA%\Microsoft\Windows\Explorer";
            return new PathCleaner
            {
                Title = L.T("Кэш эскизов"),
                Description = L.T("Миниатюры картинок и видео в Проводнике. Пересоздаются при следующем открытии папок. Часть файлов занята Проводником — они пропустятся."),
                Icon = "\uE8B7",
                Files = () => { var l = Fs.Glob(Fs.E(exp), "thumbcache_*.db"); l.AddRange(Fs.Glob(Fs.E(exp), "iconcache_*.db")); return l; },
            };
        }

        static Cleaner UpdateCache()
        {
            var restart = new List<string>();
            return new PathCleaner
            {
                Title = L.T("Кэш Windows Update"),
                Description = L.T("Уже установленные файлы обновлений (SoftwareDistribution\\Download). Перед очисткой службы обновления ненадолго останавливаются."),
                Icon = "\uE895",
                Dirs = () => Lst(@"%WINDIR%\SoftwareDistribution\Download"),
                Before = () =>
                {
                    restart.Clear();
                    foreach (var s in new[] { "wuauserv", "bits" })
                        if (Sys.IsRunning(s)) { restart.Add(s); Sys.StopService(s); }
                },
                After = () => { foreach (var s in restart) Sys.StartService(s); },
            };
        }

        static Cleaner SetupLogs()
        {
            return new PathCleaner
            {
                Title = L.T("Файлы журнала установок"),
                Description = L.T("Журналы установки Windows и обновлений (Logs\\CBS, DISM, Panther, setupapi). Нужны только для разбора ошибок установки."),
                Icon = "\uE7C3",
                Dirs = () => Lst(@"%WINDIR%\Logs\CBS", @"%WINDIR%\Logs\DISM", @"%WINDIR%\Logs\MoSetup", @"%WINDIR%\Panther"),
                Files = () => Fs.Glob(Fs.E(@"%WINDIR%\inf"), "setupapi.*.log"),
            };
        }

        static Cleaner DefenderLogs()
        {
            return new PathCleaner
            {
                Title = L.T("Журналы Microsoft Defender"),
                Description = L.T("Журналы поддержки и диагностики антивируса. История угроз и карантин не затрагиваются."),
                Icon = "\uE83D",
                Dirs = () => Lst(@"%PROGRAMDATA%\Microsoft\Windows Defender\Support", @"%PROGRAMDATA%\Microsoft\Windows Defender\Network Inspection System\Support"),
            };
        }

        static Cleaner ErrorReports()
        {
            return new PathCleaner
            {
                Title = L.T("Отчёты об ошибках и дампы"),
                Description = L.T("Отчёты о сбоях программ (WER), дампы памяти после «синих экранов» и падений программ."),
                Icon = "\uE7BA",
                Dirs = () => Lst(@"%PROGRAMDATA%\Microsoft\Windows\WER\ReportArchive", @"%PROGRAMDATA%\Microsoft\Windows\WER\ReportQueue",
                               @"%LOCALAPPDATA%\Microsoft\Windows\WER", @"%LOCALAPPDATA%\CrashDumps", @"%WINDIR%\Minidump"),
                Files = () => { var l = Lst(@"%WINDIR%\MEMORY.DMP"); l.AddRange(Fs.Glob(Fs.E(@"%WINDIR%\LiveKernelReports"), "*.dmp")); return l; },
            };
        }

        static Cleaner DiagData()
        {
            return new PathCleaner
            {
                Title = L.T("Файлы базы данных программы просмотра диагностических данных"),
                Description = L.T("Локальная копия телеметрии, которую можно смотреть в «Просмотре диагностических данных»."),
                Icon = "\uE9D9",
                Dirs = () => Lst(@"%PROGRAMDATA%\Microsoft\Diagnosis\EventTranscript"),
            };
        }

        static Cleaner ChkDsk()
        {
            return new PathCleaner
            {
                Title = L.T("Старые файлы ChkDsk"),
                Description = L.T("Папки FOUND.000 и файлы *.chk в корне дисков — обрывки файлов, которые ChkDsk нашёл при проверке диска."),
                Icon = "\uE8B7",
                RemoveRoots = true,
                Dirs = () =>
                {
                    var l = new List<string>();
                    foreach (var d in DriveInfo.GetDrives())
                        if (d.DriveType == DriveType.Fixed && d.IsReady) l.AddRange(Fs.SubDirs(d.RootDirectory.FullName, "FOUND.???"));
                    return l;
                },
                Files = () =>
                {
                    var l = new List<string>();
                    foreach (var d in DriveInfo.GetDrives())
                        if (d.DriveType == DriveType.Fixed && d.IsReady) l.AddRange(Fs.Glob(d.RootDirectory.FullName, "*.chk"));
                    return l;
                },
            };
        }

        // ───────── Графика ─────────

        static Cleaner ShaderCache()
        {
            return new PathCleaner
            {
                Title = "DirectX Shader Cache",
                Description = L.T("Скомпилированные шейдеры игр (DirectX, AMD). Windows пересоздаст их сама."),
                Icon = "\uE950",
                Caution = L.T("первый запуск игр после очистки может быть дольше и с подтормаживаниями, пока шейдеры не соберутся заново."),
                Dirs = () => Lst(@"%LOCALAPPDATA%\D3DSCache", @"%LOCALAPPDATA%\AMD\DxCache", @"%LOCALAPPDATA%\AMD\DxcCache", @"%LOCALAPPDATA%\AMD\VkCache"),
            };
        }

        static Cleaner Nvidia()
        {
            return new PathCleaner
            {
                Title = L.T("Кэш, загрузки и журналы NVIDIA"),
                Description = L.T("Кэш шейдеров драйвера NVIDIA, скачанные установщики драйверов и журналы."),
                Icon = "\uE7F4",
                Dirs = () => Lst(@"%LOCALAPPDATA%\NVIDIA\DXCache", @"%LOCALAPPDATA%\NVIDIA\GLCache", @"%LOCALAPPDATA%\NVIDIA Corporation\NV_Cache",
                               @"%PROGRAMDATA%\NVIDIA Corporation\Downloader"),
                Files = () => Fs.Glob(Fs.E(@"%PROGRAMDATA%\NVIDIA"), "*.log"),
            };
        }

        // ───────── Браузеры ─────────

        static readonly string[] ProfileCaches = { "Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache" };
        static readonly string[] RootCaches = { "ShaderCache", "GrShaderCache", "GraphiteDawnCache" };

        // Папки кэша Chromium-браузера: для каждого профиля + общие
        static List<string> ChromiumDirs(string userData)
        {
            var l = new List<string>();
            string ud = Fs.E(userData);
            if (!Directory.Exists(ud)) return l;
            var profiles = new List<string>();
            profiles.AddRange(Fs.SubDirs(ud, "Default"));
            profiles.AddRange(Fs.SubDirs(ud, "Profile *"));
            profiles.AddRange(Fs.SubDirs(ud, "Guest Profile"));
            foreach (var p in profiles) foreach (var c in ProfileCaches) l.Add(Path.Combine(p, c));
            foreach (var c in RootCaches) l.Add(Path.Combine(ud, c));
            return l;
        }

        static readonly string BrowserNote = L.T(" Удаляется только кэш — пароли, куки, история и вкладки остаются.");

        static Cleaner Chromium(string title, string userData, string icon, string[] procs, string name)
        {
            return new PathCleaner
            {
                Title = title,
                Description = L.T("Сохранённые копии страниц, картинок и скриптов.") + BrowserNote,
                Icon = icon,
                Processes = procs, ProcessTitle = name,
                Dirs = () => ChromiumDirs(userData),
            };
        }

        static Cleaner VivaldiOpera()
        {
            return new PathCleaner
            {
                Title = L.T("Кэш Vivaldi и Opera"),
                Description = L.T("Кэш Vivaldi, Opera и Opera GX.") + BrowserNote,
                Icon = "\uE774",
                Processes = new[] { "vivaldi", "opera" }, ProcessTitle = "Vivaldi / Opera",
                Dirs = () =>
                {
                    var l = ChromiumDirs(@"%LOCALAPPDATA%\Vivaldi\User Data");
                    foreach (var o in new[] { "Opera Stable", "Opera GX Stable" })
                    {
                        l.Add(Fs.E(@"%LOCALAPPDATA%\Opera Software\" + o + @"\Cache"));
                        foreach (var c in ProfileCaches) l.Add(Fs.E(@"%APPDATA%\Opera Software\" + o + "\\" + c));
                        foreach (var c in RootCaches) l.Add(Fs.E(@"%APPDATA%\Opera Software\" + o + "\\" + c));
                    }
                    return l;
                },
            };
        }

        static Cleaner Firefox()
        {
            return new PathCleaner
            {
                Title = L.T("Кэш Mozilla Firefox"),
                Description = L.T("Кэш страниц, запуска и миниатюр Firefox.") + BrowserNote,
                Icon = "\uE774",
                Processes = new[] { "firefox" }, ProcessTitle = "Firefox",
                Dirs = () =>
                {
                    var l = new List<string>();
                    foreach (var p in Fs.SubDirs(Fs.E(@"%LOCALAPPDATA%\Mozilla\Firefox\Profiles"), "*"))
                        foreach (var c in new[] { "cache2", "startupCache", "thumbnails", "jumpListCache" })
                            l.Add(Path.Combine(p, c));
                    return l;
                },
            };
        }

        // ───────── Разработка ─────────

        static Cleaner VisualStudio()
        {
            return new PathCleaner
            {
                Title = L.T("Кэш Visual Studio"),
                Description = L.T("Кэш компонентов (ComponentModelCache) и телеметрии Visual Studio. Закройте Visual Studio перед очисткой."),
                Icon = "\uE943",
                Processes = new[] { "devenv" }, ProcessTitle = "Visual Studio",
                Dirs = () =>
                {
                    var l = new List<string>();
                    foreach (var v in Fs.SubDirs(Fs.E(@"%LOCALAPPDATA%\Microsoft\VisualStudio"), "*"))
                        l.Add(Path.Combine(v, "ComponentModelCache"));
                    l.Add(Fs.E(@"%LOCALAPPDATA%\Microsoft\VSApplicationInsights"));
                    return l;
                },
            };
        }

        static Cleaner NuGet()
        {
            return new PathCleaner
            {
                Title = L.T("Временный кэш NuGet"),
                Description = L.T("HTTP-кэш и временные файлы NuGet. Скачанные пакеты (global-packages) не трогаются."),
                Icon = "\uE7B8",
                Dirs = () => Lst(@"%LOCALAPPDATA%\NuGet\v3-cache", @"%LOCALAPPDATA%\NuGet\plugins-cache", @"%TEMP%\NuGetScratch"),
            };
        }

        static Cleaner NodeCaches()
        {
            return new PathCleaner
            {
                Title = L.T("Кэш npm, Yarn и pnpm"),
                Description = L.T("Скачанные пакеты Node.js в кэше менеджеров пакетов. Проекты не затрагиваются — пакеты скачаются заново при необходимости."),
                Icon = "\uE943",
                Dirs = () => Lst(@"%LOCALAPPDATA%\npm-cache", @"%APPDATA%\npm-cache", @"%LOCALAPPDATA%\Yarn\Cache", @"%LOCALAPPDATA%\pnpm-cache"),
            };
        }

        static Cleaner DotNet()
        {
            return new PathCleaner
            {
                Title = L.T("Диагностические файлы .NET"),
                Description = L.T("Телеметрия и временные файлы .NET SDK."),
                Icon = "\uE943",
                Dirs = () => Lst(@"%USERPROFILE%\.dotnet\TelemetryStorageService"),
            };
        }
    }
}
