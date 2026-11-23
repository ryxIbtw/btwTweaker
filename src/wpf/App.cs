using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

[assembly: AssemblyTitle("btwTweaker")]
[assembly: AssemblyProduct("btwTweaker")]
[assembly: AssemblyVersion("1.1.0.0")]

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
            MessageBox.Show(L.T("Что-то пошло не так: ") + ex.Message + L.T("\n\nПодробности сохранены в файл:\n") + LogPath,
                L.T("btwTweaker — ошибка"), MessageBoxButton.OK, MessageBoxImage.Error);
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
            // Только чтение настроек драйвера NVIDIA: --test-nv <файл отчёта> id1 id2 …
            if (args.Length >= 2 && args[0] == "--test-nv")
            {
                var sb = new StringBuilder();
                try
                {
                    string err;
                    sb.AppendLine("available=" + NvDrs.Available(out err) + " " + err);
                    var ids = new System.Collections.Generic.List<uint>();
                    for (int i = 2; i < args.Length; i++) ids.Add(Convert.ToUInt32(args[i], 16));
                    foreach (var kv in NvDrs.Read(ids))
                        sb.AppendLine(kv.Key.ToString("X8") + " found=" + kv.Value.Found + " value=0x" + kv.Value.Value.ToString("X8") + " predefined=" + kv.Value.Predefined);
                }
                catch (Exception ex) { sb.AppendLine("FAIL " + ex); }
                File.WriteAllText(args[1], sb.ToString(), Encoding.UTF8);
                return;
            }
            // Проверка журнала на тестовом ключе HKCU\Software\BtwTweakerTest (с BTW_DATA_DIR во временной папке)
            if (args.Length >= 2 && args[0] == "--test-journal")
            {
                const string K = @"Software\BtwTweakerTest";
                var sb = new StringBuilder();
                Func<string> state = () => "A=" + (Reg.Get(Reg.HKCU, K, "A") ?? L.T("нет")) + " B=" + (Reg.Get(Reg.HKCU, K, "B") ?? L.T("нет"));
                try
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BTW_DATA_DIR"))) throw new Exception(L.T("нужен BTW_DATA_DIR"));
                    Journal.Do(L.T("Тест"), "A=1", () => Reg.Apply(Reg.Dw(Reg.HKCU, K, "A", 1)));
                    sb.AppendLine(L.T("после 1: ") + state());
                    Journal.Do(L.T("Тест"), "A=2, B=x", () => { Reg.Apply(Reg.Dw(Reg.HKCU, K, "A", 2)); Reg.Apply(Reg.Sz(Reg.HKCU, K, "B", "x")); });
                    sb.AppendLine(L.T("после 2: ") + state());
                    var all = Journal.All();
                    sb.AppendLine(L.T("записей: ") + all.Count + " — " + all[0].Title + " / " + all[1].Title);
                    var errs = new System.Collections.Generic.List<string>();
                    Journal.Rollback(all[0].Id, errs);
                    sb.AppendLine(L.T("откат 2: ") + state() + L.T("  (ждём A=1 B=нет)"));
                    Journal.Rollback(all[1].Id, errs);
                    sb.AppendLine(L.T("откат 1: ") + state() + L.T("  (ждём A=нет B=нет)"));
                    Journal.Do(L.T("Тест"), "A=5", () => Reg.Apply(Reg.Dw(Reg.HKCU, K, "A", 5)));
                    Backup.RestoreAll(errs);
                    sb.AppendLine(L.T("откатить всё: ") + state() + L.T("  (ждём A=нет B=нет)"));
                    sb.AppendLine(L.T("ошибок: ") + errs.Count + " " + string.Join("; ", errs));
                    foreach (var e in Journal.All()) sb.AppendLine(L.T("  журнал: ") + e.Title + L.T(" откачено=") + e.RolledBack + L.T(" необратимо=") + e.Irreversible);
                }
                catch (Exception ex) { sb.AppendLine("FAIL " + ex); }
                finally { using (var b = Reg.Base(Reg.HKCU)) try { b.DeleteSubKeyTree(K, false); } catch { } }
                File.WriteAllText(args[1], sb.ToString(), Encoding.UTF8);
                return;
            }
            // Профили, только чтение: снимок → файл → загрузка → сравнение (ждём 0 отличий) + изменённый профиль для снимка диалога
            if (args.Length >= 2 && args[0] == "--test-profile")
            {
                var sb = new StringBuilder();
                try
                {
                    var secs = Catalog.Build();
                    var keys = new System.Collections.Generic.HashSet<string>();
                    foreach (var s in secs) foreach (var g in s.Groups) foreach (var t in g.Tweaks)
                        if (!keys.Add(t.Id)) sb.AppendLine(L.T("ПОВТОР КЛЮЧА: ") + t.Id);
                    string f1 = Path.Combine(args[1], "same" + Profiles.Ext);
                    var p = Profiles.Capture(secs, "same");
                    Profiles.Save(f1, p);
                    var back = Profiles.Load(f1);
                    var nf = new System.Collections.Generic.List<string>();
                    var diff = Profiles.Diff(secs, back, nf);
                    sb.AppendLine(L.T("твиков в профиле: ") + p.Tweaks.Count + L.T(", загружено: ") + back.Tweaks.Count + L.T(", отличий: ") + diff.Count + L.T(", не найдено: ") + nf.Count);
                    foreach (var d in diff) sb.AppendLine(L.T("  отличие: ") + d.Title + " skip=" + d.Skip);
                    // Изменённый профиль: перевернуть каждый 4-й переключатель, сменить выборы, добавить несуществующий
                    int n = 0;
                    foreach (var it in back.Tweaks)
                    {
                        if (it.On.HasValue && n++ % 4 == 0) it.On = !it.On.Value;
                        else if (!it.On.HasValue) it.Index = it.Index == 0 ? 1 : 0;
                    }
                    back.Tweaks.Add(new ProfileItem { Key = L.T("Несуществующий твик"), On = true });
                    back.Name = L.T("Игровой ПК"); back.Pc = "GAMING-PC";
                    string f2 = Path.Combine(args[1], "changed" + Profiles.Ext);
                    Profiles.Save(f2, back);
                    nf.Clear();
                    diff = Profiles.Diff(secs, Profiles.Load(f2), nf);
                    sb.AppendLine(L.T("изменённый: отличий ") + diff.Count + L.T(", не найдено ") + nf.Count + L.T(", рискованных ") + diff.FindAll(x => x.Risky).Count + L.T(", пропусков ") + diff.FindAll(x => x.Skip != null).Count);
                    foreach (var d in diff) sb.AppendLine("  " + d.Section + " | " + d.Title + (d.Skip != null ? " | skip=" + d.Skip : "") + (d.Risky ? " | RISKY" : ""));
                }
                catch (Exception ex) { sb.AppendLine("FAIL " + ex); }
                File.WriteAllText(Path.Combine(args[1], "profile-test.txt"), sb.ToString(), Encoding.UTF8);
                return;
            }
            // Перевод: ключи твиков одинаковы на обоих языках, нет ли русского в каталоге в режиме English
            if (args.Length >= 2 && args[0] == "--test-lang")
            {
                var sb = new StringBuilder();
                try
                {
                    L.En = false;
                    var ru = Catalog.Build();
                    var ruIds = new System.Collections.Generic.List<string>();
                    foreach (var s in ru) foreach (var g in s.Groups) foreach (var t in g.Tweaks) ruIds.Add(t.Title);
                    L.En = true;
                    var en = Catalog.Build();
                    int i = 0, bad = 0, cyr = 0;
                    Func<string, bool> hasCyr = x => x != null && System.Text.RegularExpressions.Regex.IsMatch(x, "[А-Яа-яЁё]");
                    foreach (var s in en)
                    {
                        if (hasCyr(s.Title)) { cyr++; sb.AppendLine("RU section: " + s.Title); }
                        foreach (var g in s.Groups)
                        {
                            if (hasCyr(g.Title)) { cyr++; sb.AppendLine("RU group: " + g.Title); }
                            foreach (var t in g.Tweaks)
                            {
                                if (t.Id != ruIds[i]) { bad++; sb.AppendLine("ID: «" + t.Title + "» → " + t.Id + " (ждали " + ruIds[i] + ")"); }
                                i++;
                                foreach (var x in new[] { t.Title, t.Description, t.Caution, t.Security }) if (hasCyr(x)) { cyr++; sb.AppendLine("RU: " + x); }
                                var ct = t as ChoiceTweak;
                                if (ct != null) foreach (var o in ct.Options) if (hasCyr(o)) { cyr++; sb.AppendLine("RU option: " + o); }
                            }
                        }
                    }
                    sb.Insert(0, "dict=" + L.Count + " tweaks=" + i + " badIds=" + bad + " cyrillic=" + cyr + "\n");
                }
                catch (Exception ex) { sb.AppendLine("FAIL " + ex); }
                File.WriteAllText(args[1], sb.ToString(), Encoding.UTF8);
                return;
            }
            // Слежение: что программа возьмёт из журнала при первом запуске (только чтение)
            if (args.Length >= 2 && args[0] == "--test-watch-seed")
            {
                string r;
                try { r = Watch.TestSeed(Catalog.Build()); } catch (Exception ex) { r = "FAIL " + ex; }
                File.WriteAllText(args[1], r, Encoding.UTF8);
                return;
            }
            if (args.Length >= 2 && args[0] == "--test-nv-all")
            {
                var sb3 = new StringBuilder();
                try { foreach (var id in NvDrs.AvailableIds()) sb3.AppendLine(id.ToString("X8") + "  " + NvDrs.NameOf(id)); }
                catch (Exception ex) { sb3.AppendLine("FAIL " + ex); }
                File.WriteAllText(args[1], sb3.ToString(), Encoding.UTF8);
                return;
            }
            if (args.Length >= 2 && args[0] == "--test-nv-names")
            {
                var sb2 = new StringBuilder();
                for (int i = 2; i < args.Length; i++)
                {
                    uint id = Convert.ToUInt32(args[i], 16);
                    string nm;
                    try { nm = NvDrs.NameOf(id) ?? L.T("<неизвестно драйверу>"); } catch (Exception ex) { nm = "FAIL " + ex.Message; }
                    sb2.AppendLine(id.ToString("X8") + "  " + nm);
                }
                File.WriteAllText(args[1], sb2.ToString(), Encoding.UTF8);
                return;
            }
            if (args.Length >= 2 && args[0] == "--test-nv-enum")
            {
                string r;
                try { r = string.Join("\r\n", NvDrs.EnumAll()); } catch (Exception ex) { r = "FAIL " + ex; }
                File.WriteAllText(args[1], r, Encoding.UTF8);
                return;
            }
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
