using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

[assembly: AssemblyTitle("btwTweaker")]
[assembly: AssemblyProduct("btwTweaker")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace BtwTweaker
{
    public static class AppMain
    {
        public static string Version
        {
            get { var v = Assembly.GetExecutingAssembly().GetName().Version; return v.Major + "." + v.Minor + "." + v.Build; }
        }

        public static string LogPath
        {
            get
            {
#if TESTHOOKS
                // Снимки: журнал рядом со снимком, а не в папке пользователя
                var snap = Environment.GetEnvironmentVariable("BTW_SNAP");
                if (!string.IsNullOrEmpty(snap)) return Path.ChangeExtension(snap, ".log");
#endif
                return Path.Combine(Backup.Dir, "error.log");
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint SetErrorMode(uint mode);

        // Тестовый режим снимков: ошибки только в журнал, без окон на экране пользователя
        static bool Quiet
        {
            get
            {
#if TESTHOOKS
                return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BTW_SNAP"));
#else
                return false;
#endif
            }
        }

        public static void Log(string where, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] v" + Version + " " + where + "\r\n" + ex + "\r\n\r\n", Encoding.UTF8);
            }
            catch { }
        }

        static void Report(string where, Exception ex)
        {
            Log(where, ex);
            if (Quiet) return;
            MessageBox.Show("Что-то пошло не так: " + ex.Message + "\n\nПодробности сохранены в файл:\n" + LogPath,
                "btwTweaker — ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            // Ошибки вне окна (фоновые потоки) — хотя бы записать в журнал
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log("AppDomain", e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject)));
                if (Quiet) Environment.Exit(5);   // тестовый режим: без системного окна «Ошибка приложения»
            };
            if (Quiet) SetErrorMode(0x0002);      // SEM_NOGPFAULTERRORBOX
#if TESTHOOKS
            // Проверка обновления без окна: --test-update <путь к exe> — скачать последний релиз и поставить на место этого файла
            if (args.Length >= 2 && args[0] == "--test-update")
            {
                string report;
                try
                {
                    var r = Updater.Latest();
                    bool sha = Updater.DownloadAndInstall(r, args[1]);
                    report = "OK " + r.Tag + " sha=" + sha + " new=" + Updater.Sha256(File.ReadAllBytes(args[1]));
                }
                catch (Exception ex) { report = "FAIL " + ex; }
                File.WriteAllText(args[1] + ".report", report, Encoding.UTF8);
                return;
            }
#endif
            Updater.WaitForOld(args);   // перезапуск после обновления: дождаться закрытия старой копии
            Updater.CleanupOld();
            TaskScheduler.UnobservedTaskException += (s, e) => { Log("Task", e.Exception); e.SetObserved(); };
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                // Окно строится до app.Run — без этого «await» внутри него продолжается в чужом потоке
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                app.DispatcherUnhandledException += (s, e) =>
                {
                    e.Handled = true;
                    Report("UI", e.Exception);
#if TESTHOOKS
                    if (Quiet) Environment.Exit(3);
#endif
                };
                Theme.Install(app);
                app.Run(new MainWindow());
            }
            catch (Exception ex)
            {
                Report("Main", ex);
#if TESTHOOKS
                if (Quiet) Environment.Exit(4);
#endif
            }
        }
    }
}
