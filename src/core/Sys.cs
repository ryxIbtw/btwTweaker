using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;

namespace BtwTweaker
{
    public static class Sys
    {
        static int build = -1;

        // Номер сборки Windows (Environment.OSVersion на .NET Framework может врать)
        public static int Build
        {
            get
            {
                if (build < 0)
                {
                    int b;
                    var s = Reg.Get(Reg.HKLM, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber") as string;
                    build = int.TryParse(s, out b) ? b : 0;
                }
                return build;
            }
        }

        public static bool IsWin11 { get { return Build >= 22000; } }

        // Номер накопительного обновления (UBR): 26100.**2605** — меняется после каждого обновления Windows
        public static int Ubr
        {
            get
            {
                var v = Reg.Get(Reg.HKLM, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR");
                try { return v == null ? 0 : Convert.ToInt32(v); } catch { return 0; }
            }
        }

        public static string OsName
        {
            get { return (IsWin11 ? "Windows 11" : "Windows 10") + L.T(" (сборка ") + Build + ")"; }
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint action, uint uParam, int[] pvParam, uint winIni);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint action, uint uParam, IntPtr pvParam, uint winIni);
        public const uint SPIF_UPDATE = 0x01 | 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

        // Сообщить Windows, что переменные среды изменились (новые программы увидят их без перезахода)
        public static void BroadcastEnvironment()
        {
            IntPtr r;
            SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, IntPtr.Zero, "Environment", 0x0002, 3000, out r);
        }

        static System.Text.Encoding OemEncoding
        {
            get
            {
                try { return System.Text.Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
                catch { return System.Text.Encoding.Default; }
            }
        }

        // Запустить программу и дождаться; возвращает код выхода, вывод — в output
        public static int Run(string exe, string args, out string output)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Консольные программы пишут в кодировке OEM (для русской Windows — 866), иначе будут кракозябры
                StandardOutputEncoding = OemEncoding,
                StandardErrorEncoding = OemEncoding
            };
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                output = o + err.Result;
                return p.ExitCode;
            }
        }

        // Остановить / запустить службу; ошибки не критичны (служба может отсутствовать или быть занята)
        public static void StopService(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.CanStop)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                    }
            }
            catch { }
        }

        public static bool IsRunning(string name)
        {
            try { using (var sc = new ServiceController(name)) return sc.Status == ServiceControllerStatus.Running; }
            catch { return false; }
        }

        public static void StartService(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    }
            }
            catch { }
        }

        // Открыть ключ в редакторе реестра: regedit при запуске открывает последний ключ (LastKey)
        public static void OpenRegedit(string fullPath)
        {
            const string k = @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit";
            var last = Reg.Get(Reg.HKCU, k, "LastKey") as string;
            string root = last != null && last.IndexOf('\\') > 0 ? last.Substring(0, last.IndexOf('\\')) :
                (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? L.T("Компьютер") : "Computer");
            using (var b = Reg.Base(Reg.HKCU))
            using (var key = b.CreateSubKey(k))
                key.SetValue("LastKey", root + "\\" + fullPath, RegistryValueKind.String);
            // Если regedit уже открыт, он не перейдёт к ключу — закрываем его
            foreach (var p in Process.GetProcessesByName("regedit"))
                try { p.CloseMainWindow(); p.WaitForExit(2000); } catch { }
            Process.Start(new ProcessStartInfo("regedit.exe", "-m") { UseShellExecute = true });
        }

        // Скрипт PowerShell без проблем с кавычками (-EncodedCommand) и с выводом в UTF-8
        public static int RunPowerShell(string script, out string output)
        {
            string so, se;
            int code = RunPowerShell(script, out so, out se);
            output = so + (string.IsNullOrWhiteSpace(se) ? "" : "\n" + se);
            return code;
        }

        public static int RunPowerShell(string script, out string stdout, out string stderr)
        {
            string full = "[Console]::OutputEncoding = [Text.Encoding]::UTF8; $ProgressPreference = 'SilentlyContinue'; " + script;
            string enc = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(full));
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + enc)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = OemEncoding
            };
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                stderr = err.Result;
                return p.ExitCode;
            }
        }

        public static void RestartExplorer()
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            // Обычно Windows сама перезапускает Проводник; если нет — запускаем
            Thread.Sleep(2500);
            if (Process.GetProcessesByName("explorer").Length == 0)
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        }

        public static void Reboot()
        {
            string o;
            Run("shutdown.exe", "/r /t 0", out o);
        }

        // Точка восстановления. Возвращает текст для пользователя; ok = успех
        public static string CreateRestorePoint(out bool ok)
        {
            string o;
            int code = Run("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'btwTweaker' -RestorePointType MODIFY_SETTINGS -WarningVariable w -ErrorAction Stop; if ($w) { 'WARN: ' + $w }\"",
                out o);
            ok = false;
            if (o.Contains("1440"))
                return L.T("Windows разрешает создавать точку восстановления не чаще раза в сутки — сегодня она уже есть.");
            if (code != 0 || o.Contains("WARN"))
            {
                if (o.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0 || o.IndexOf("отключ", StringComparison.OrdinalIgnoreCase) >= 0)
                    return L.T("Защита системы выключена. Включите её: Пуск → «Создание точки восстановления» → Настроить → Включить защиту системы.");
                return L.T("Не удалось создать точку восстановления.\n\n") + o.Trim();
            }
            ok = true;
            return L.T("Точка восстановления «btwTweaker» создана.");
        }

        // Настройки самой программы: HKCU\Software\BtwTweaker
        const string PrefKey = @"Software\BtwTweaker";
        public static string Pref(string name)
        {
            return Reg.Get(Reg.HKCU, PrefKey, name) as string;
        }
        public static void SetPref(string name, string value)
        {
            using (var b = Reg.Base(Reg.HKCU))
            using (var k = b.CreateSubKey(PrefKey))
                k.SetValue(name, value, RegistryValueKind.String);
        }
    }
}
