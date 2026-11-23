using System;

namespace BtwTweaker
{
    public static partial class Catalog
    {
        const string FsKey = @"SYSTEM\CurrentControlSet\Control\FileSystem";

        static Group[] TweaksGroups()
        {
            return new[]
            {
                new Group(L.T("Системные политики и интеграции"), CrossDeviceResume(), Ucpd()),
                new Group(L.T("Планировщик и память"), PrioritySeparation(), SvcHostSplit()),
                new Group(L.T("Ввод"), RawInputThrottle()),
                new Group(L.T("Диск и файловая система"), new ReservedStorage(), NtfsLastAccess(), Ntfs83()),
                new Group(L.T("Обслуживание и диагностика"), new DiagEventsTweak(), AppCompatibility(), AutoMaintenance(), ScheduledDiagnostics()),
            };
        }

        // ───────── Системные политики ─────────

        static Tweak CrossDeviceResume() { return new CrossDeviceResumeTweak(); }

        static Tweak Ucpd()
        {
            return new RegToggle
            {
                Title = "UCPD",
                Description = L.T("User Choice Protection Driver — драйвер, который не даёт программам менять браузер и приложения по умолчанию и некоторые настройки панели задач (например, скрытие виджетов через реестр). После отключения такие изменения снова можно делать программами и скриптами."),
                Icon = "", IconColor = "#F5C542",
                Restart = Restart.Reboot,
                TaskPaths = new[] { @"\Microsoft\Windows\AppxDeploymentClient\UCPD velocity" },
            }
            .WhenOff(Reg.Start("UCPD", 4))
            .WhenOn(Reg.Start("UCPD", 1));
        }

        // ───────── Планировщик и память ─────────

        static Tweak PrioritySeparation()
        {
            const string k = @"SYSTEM\CurrentControlSet\Control\PriorityControl";
            int[] v = { 2, 0x26, 0x28, 0x18 };
            var vals = new RegVal[v.Length];
            for (int i = 0; i < v.Length; i++) vals[i] = Reg.Dw(HKLM, k, "Win32PrioritySeparation", v[i]);
            return new RegChoice
            {
                Title = "Win32PrioritySeparation",
                Description = L.T("Как Windows делит процессорное время между активным окном и фоном.\n") +
                              L.T("По умолчанию (2) — стандарт Windows.\n") +
                              L.T("Отзывчивость (0x26) — короткие кванты и максимальный приоритет активной программы: подходит для игр и работы.\n") +
                              L.T("Короткие фиксированные (0x28) — равные короткие кванты без бонуса активному окну.\n") +
                              L.T("Фоновые службы (0x18) — длинные кванты, как на серверах: фон работает ровнее, интерфейс медленнее."),
                Icon = "",
                Options = new[] { L.T("По умолчанию"), L.T("Отзывчивость"), L.T("Короткие фиксированные"), L.T("Фоновые службы") },
                Values = vals,
            };
        }

        static Tweak SvcHostSplit()
        {
            const string k = @"SYSTEM\CurrentControlSet\Control";
            return new SvcSplit
            {
                Title = L.T("Разделение служб (svchost.exe)"),
                Description = L.T("Если памяти больше 3,5 ГБ, Windows запускает почти каждую службу в отдельном процессе svchost.exe — так надёжнее: сбой одной службы не роняет другие. После отключения службы объединяются в общие процессы: процессов становится заметно меньше, памяти тратится чуть меньше."),
                Icon = "",
                Caution = L.T("если одна служба в общем процессе упадёт, вместе с ней перезапустятся соседние."),
                Restart = Restart.Reboot,
                Key = k,
            };
        }

        // ───────── Ввод ─────────

        static Tweak RawInputThrottle()
        {
            const string k = @"Control Panel\Mouse";
            return new RegToggle
            {
                Title = L.T("Уменьшить частоту фоновых событий Raw Input"),
                Description = L.T("Windows 11 24H2+: фоновые окна получают события мыши с высокой частотой опроса (1000+ Гц) реже. Снижает нагрузку на процессор, когда в фоне открыто много программ, и не влияет на активную игру."),
                Icon = "", IconColor = "#F5C542",
                MinBuild = 26100,
                CheckOn = true, DefaultOn = true,
            }
            .WhenOn(Reg.Dw(HKCU, k, "RawMouseThrottleEnabled", 1))
            .WhenOff(Reg.Dw(HKCU, k, "RawMouseThrottleEnabled", 0));
        }

        // ───────── Диск ─────────

        static Tweak NtfsLastAccess()
        {
            return new NtfsLastAccessTweak();
        }

        static Tweak Ntfs83()
        {
            return new RegToggle
            {
                Title = L.T("Конвенция имён файлов 8.3"),
                Description = L.T("Для каждого файла NTFS создаёт дополнительное короткое имя в стиле DOS (PROGRA~1). Нужно только очень старым программам; без этого создание файлов в больших папках идёт быстрее. Уже существующие короткие имена не удаляются."),
                Icon = "",
                Caution = L.T("очень старые 16-битные программы и некоторые старые установщики могут не найти файлы."),
                Restart = Restart.Reboot,
            }
            .WhenOff(Reg.Dw(HKLM, FsKey, "NtfsDisable8dot3NameCreation", 1))
            .WhenOn(Reg.Dw(HKLM, FsKey, "NtfsDisable8dot3NameCreation", 2));
        }

        // ───────── Обслуживание ─────────

        static Tweak AppCompatibility()
        {
            return new RegToggle
            {
                Title = L.T("Совместимость приложений"),
                Description = L.T("Помощник по совместимости программ (PCA) и механизм исправлений совместимости: следят за запуском старых программ и предлагают «запустить с другими параметрами»."),
                Icon = "", IconColor = "#F5C542",
                Caution = L.T("старые программы, которым нужны исправления совместимости (шимы), могут перестать запускаться."),
                Restart = Restart.Reboot,
                StopServices = new[] { "PcaSvc" },
                StartServices = new[] { "PcaSvc" },
            }
            .WhenOff(Reg.Dw(HKLM, AppCompatPol, "DisablePCA", 1), Reg.Dw(HKLM, AppCompatPol, "DisableEngine", 1), Reg.Start("PcaSvc", 4))
            .WhenOn(Reg.Del(HKLM, AppCompatPol, "DisablePCA"), Reg.Del(HKLM, AppCompatPol, "DisableEngine"), Reg.Start("PcaSvc", 2));
        }

        static Tweak AutoMaintenance()
        {
            const string k = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance";
            return new RegToggle
            {
                Title = L.T("Автоматическое обслуживание"),
                Description = L.T("Ежедневное обслуживание в простое: оптимизация дисков, проверка системы, установка обновлений Магазина и т.п. Может внезапно нагружать ПК. Если отключить, иногда запускайте «Оптимизацию дисков» вручную (для SSD это TRIM)."),
                Icon = "", IconColor = "#F5C542",
            }
            .WhenOff(Reg.Dw(HKLM, k, "MaintenanceDisabled", 1))
            .WhenOn(Reg.Del(HKLM, k, "MaintenanceDisabled"));
        }

        static Tweak ScheduledDiagnostics()
        {
            return new RegToggle
            {
                Title = L.T("Запланированная диагностика"),
                Description = L.T("Регулярная автоматическая проверка системы на неполадки (средства устранения неполадок по расписанию)."),
                Icon = "", IconColor = "#F5C542",
                TaskPaths = new[] { @"\Microsoft\Windows\Diagnosis\Scheduled" },
            }
            .WhenOff(Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\ScheduledDiagnostics", "EnabledExecution", 0))
            .WhenOn(Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\ScheduledDiagnostics", "EnabledExecution"));
        }
    }

    // Cross-Device Resume: пользовательская настройка + MDM-политика Connectivity/DisableCrossDeviceResume
    public class CrossDeviceResumeTweak : ToggleTweak
    {
        const string Cfg = @"Software\Microsoft\Windows\CurrentVersion\CrossDeviceResume\Configuration";
        const string PM = @"SOFTWARE\Microsoft\PolicyManager\";
        const string Name = "DisableCrossDeviceResume";

        public CrossDeviceResumeTweak()
        {
            Title = "Cross-Device Resume";
            Description = L.T("«Продолжить на этом ПК»: уведомления с предложением продолжить на компьютере работу, начатую на телефоне (через OneDrive и «Связь с телефоном»). Применяется к текущему пользователю.");
            Icon = "";
            IconColor = "#F5C542";
            Caution = L.T("если функцию раньше отключали через скрытые A/B-функции Windows (например, ViVeTool), такое переопределение может продолжать её запрещать.");
            Restart = Restart.Reboot;
            Win11Only = true;
        }

        static string Sid { get { return System.Security.Principal.WindowsIdentity.GetCurrent().User.Value; } }

        // Все места, где лежит политика для текущего пользователя: итоговая (current) и у поставщиков
        static System.Collections.Generic.List<string> PolicyKeys()
        {
            var l = new System.Collections.Generic.List<string>();
            string sid = Sid;
            l.Add(PM + @"current\" + sid + @"\Connectivity");
            using (var b = Reg.Base(Reg.HKLM))
            using (var prov = b.OpenSubKey(PM + "providers"))
                if (prov != null)
                    foreach (var p in prov.GetSubKeyNames())
                        l.Add(PM + @"providers\" + p + @"\default\" + sid + @"\Connectivity");
            return l;
        }

        public override bool ReadEnabled()
        {
            int? pol = Reg.GetInt(Reg.HKLM, PM + @"current\" + Sid + @"\Connectivity", Name);
            if (pol.HasValue && pol.Value == 1) return false;
            int? allowed = Reg.GetInt(Reg.HKCU, Cfg, "IsResumeAllowed");
            return !allowed.HasValue || allowed.Value != 0;
        }

        public override void SetEnabled(bool on)
        {
            Reg.Apply(Reg.Dw(Reg.HKCU, Cfg, "IsResumeAllowed", on ? 1 : 0));
            Reg.Apply(Reg.Dw(Reg.HKCU, Cfg, "IsOneDriveResumeAllowed", on ? 1 : 0));
            if (!on) return;
            ClearPolicy();
        }

        public override System.Collections.Generic.List<ChangeInfo> Changes()
        {
            var l = new System.Collections.Generic.List<ChangeInfo>
            {
                ChangeText.FromReg(Reg.Dw(Reg.HKCU, Cfg, "IsResumeAllowed", 1), Reg.Dw(Reg.HKCU, Cfg, "IsResumeAllowed", 0)),
                ChangeText.FromReg(Reg.Dw(Reg.HKCU, Cfg, "IsOneDriveResumeAllowed", 1), Reg.Dw(Reg.HKCU, Cfg, "IsOneDriveResumeAllowed", 0)),
            };
            var p = new ChangeInfo { Kind = L.T("Политика MDM"), Title = "HKLM\\" + PM + L.T("current\\<ваш SID>\\Connectivity"), Detail = Name, RegPath = "HKEY_LOCAL_MACHINE\\" + PM + "current" };
            p.Add(L.T("Вкл"), L.T("0 — запрет снимается")).Add(L.T("Откл"), L.T("не меняется"));
            int? pol = Reg.GetInt(Reg.HKLM, PM + @"current\" + Sid + @"\Connectivity", Name);
            if (pol.HasValue) p.Current = pol.Value == 1 ? 1 : 0;
            l.Add(p);
            return l;
        }

        void ClearPolicy()
        {
            // Снять запрет политики там, где он стоит (иначе она продолжит блокировать функцию)
            foreach (var k in PolicyKeys())
            {
                int? v = Reg.GetInt(Reg.HKLM, k, Name);
                if (v.HasValue && v.Value != 0) Reg.Apply(Reg.Dw(Reg.HKLM, k, Name, 0));
            }
        }
    }

    // Диагностические события: подробные журналы SleepStudy и энергопотребления (каналы журнала событий)
    public class DiagEventsTweak : ToggleTweak
    {
        const string Ch = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WINEVT\Channels\";
        static readonly string[] Channels =
        {
            "Microsoft-Windows-SleepStudy/Diagnostic",
            "Microsoft-Windows-Kernel-Processor-Power/Diagnostic",
            "Microsoft-Windows-UserModePowerService/Diagnostic",
        };

        public DiagEventsTweak()
        {
            Title = L.T("Диагностические события");
            Description = L.T("Подробные журналы SleepStudy и энергопотребления (Modern Standby, питание процессора). Если вы не разбираетесь с режимом сна, их можно выключить: журнал событий станет меньше и чище.");
            Icon = "";
        }

        public override bool IsSupported()
        {
            foreach (var c in Channels) if (Reg.KeyExists(Reg.HKLM, Ch + c)) return true;
            return false;
        }

        public override bool ReadEnabled()
        {
            foreach (var c in Channels)
            {
                if (!Reg.KeyExists(Reg.HKLM, Ch + c)) continue;
                int? v = Reg.GetInt(Reg.HKLM, Ch + c, "Enabled");
                return !v.HasValue || v.Value != 0;
            }
            return true;
        }

        public override void SetEnabled(bool on)
        {
            foreach (var c in Channels)
            {
                if (!Reg.KeyExists(Reg.HKLM, Ch + c)) continue;
                Reg.Apply(Reg.Dw(Reg.HKLM, Ch + c, "Enabled", on ? 1 : 0));   // в резервную копию
                string o;
                Sys.Run("wevtutil.exe", "sl \"" + c + "\" /e:" + (on ? "true" : "false") + " /q:true", out o);   // применить сразу
            }
        }

        public override System.Collections.Generic.List<ChangeInfo> Changes()
        {
            var l = new System.Collections.Generic.List<ChangeInfo>();
            foreach (var c in Channels)
            {
                var ci = new ChangeInfo { Kind = L.T("Журнал"), Title = c, Detail = "wevtutil sl … /e", RegPath = "HKEY_LOCAL_MACHINE\\" + Ch + c };
                ci.Add(L.T("Вкл"), L.T("Включён")).Add(L.T("Откл"), L.T("Выключен"));
                try
                {
                    if (!Reg.KeyExists(Reg.HKLM, Ch + c)) ci.Detail = L.T("нет в этой Windows");
                    else { int? v = Reg.GetInt(Reg.HKLM, Ch + c, "Enabled"); ci.Current = (!v.HasValue || v.Value != 0) ? 0 : 1; }
                }
                catch { }
                l.Add(ci);
            }
            return l;
        }
    }

    // Разделение svchost: SvcHostSplitThresholdInKB. Больше объёма памяти — службы объединяются.
    public class SvcSplit : ToggleTweak
    {
        public string Key;
        const string Name = "SvcHostSplitThresholdInKB";
        const int Default = 0x380000;   // 3,5 ГБ — значение Windows по умолчанию

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool GetPhysicallyInstalledSystemMemory(out long kb);

        static long RamKb()
        {
            long kb;
            return GetPhysicallyInstalledSystemMemory(out kb) ? kb : 0;
        }

        public override bool ReadEnabled()
        {
            var v = Reg.Get(Reg.HKLM, Key, Name);
            if (v == null) return true;
            long threshold = (uint)(int)v;   // DWORD без знака
            return threshold < RamKb();
        }

        public override void SetEnabled(bool on)
        {
            Reg.Apply(Reg.Dw(Reg.HKLM, Key, Name, on ? Default : unchecked((int)0xFFFFFFFF)));
        }

        public override System.Collections.Generic.List<ChangeInfo> Changes()
        {
            var c = ChangeText.FromReg(Reg.Dw(Reg.HKLM, Key, Name, Default), Reg.Dw(Reg.HKLM, Key, Name, unchecked((int)0xFFFFFFFF)));
            c.Variants[0][1] = L.T("3 670 016 КБ (3,5 ГБ)");
            c.Variants[1][1] = L.T("0xFFFFFFFF (максимум)");
            try { c.Current = ReadEnabled() ? 0 : 1; } catch { }
            return new System.Collections.Generic.List<ChangeInfo> { c };
        }
    }

    // Обновление времени последнего доступа NTFS: NtfsDisableLastAccessUpdate, младший бит = отключено
    public class NtfsLastAccessTweak : ToggleTweak
    {
        const string K = @"SYSTEM\CurrentControlSet\Control\FileSystem";
        const string N = "NtfsDisableLastAccessUpdate";

        public NtfsLastAccessTweak()
        {
            Title = L.T("Обновление последнего доступа NTFS");
            Description = L.T("При каждом чтении файла NTFS записывает время последнего доступа — лишние операции записи на диск. После отключения время доступа не обновляется (на SSD и больших дисках Windows обычно и так его выключает).");
            Icon = "";
            Restart = Restart.Reboot;
        }

        public override bool ReadEnabled()
        {
            int? v = Reg.GetInt(Reg.HKLM, K, N);
            return v.HasValue && (v.Value & 1) == 0;
        }

        // 0x80000001 — «вручную, отключено», 0x80000000 — «вручную, включено» (как fsutil behavior set disablelastaccess)
        public override void SetEnabled(bool on)
        {
            Reg.Apply(Reg.Dw(Reg.HKLM, K, N, unchecked((int)(on ? 0x80000000 : 0x80000001))));
        }

        public override System.Collections.Generic.List<ChangeInfo> Changes()
        {
            var c = ChangeText.FromReg(Reg.Dw(Reg.HKLM, K, N, unchecked((int)0x80000000)), Reg.Dw(Reg.HKLM, K, N, unchecked((int)0x80000001)));
            try { c.Current = ReadEnabled() ? 0 : 1; } catch { }
            return new System.Collections.Generic.List<ChangeInfo> { c };
        }
    }

    // Зарезервированное хранилище для обновлений: официально через DISM
    public class ReservedStorage : ToggleTweak
    {
        const string K = @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager";

        public ReservedStorage()
        {
            Title = L.T("Хранилище обновлений");
            Description = L.T("Windows держит несколько гигабайт диска (обычно ~7 ГБ) свободными для установки обновлений. После отключения это место освобождается. Отключить нельзя, пока устанавливается обновление.");
            Icon = "";
            IconColor = "#F5C542";
        }

        public override bool ReadEnabled()
        {
            int? v = Reg.GetInt(Reg.HKLM, K, "ShippedWithReserves");
            return v.HasValue && v.Value == 1;
        }

        public override void SetEnabled(bool on)
        {
            string o;
            int code = Sys.Run("dism.exe", "/Online /Set-ReservedStorageState /State:" + (on ? "Enabled" : "Disabled"), out o);
            if (code != 0)
                throw new Exception(L.T("DISM не смог изменить хранилище (код ") + code + L.T("). Часто это значит, что сейчас устанавливается обновление — попробуйте после перезагрузки.\n\n") + o.Trim());
            Reg.Apply(Reg.Dw(Reg.HKLM, K, "ShippedWithReserves", on ? 1 : 0));
        }

        public override System.Collections.Generic.List<ChangeInfo> Changes()
        {
            var cmd = new ChangeInfo { Kind = L.T("Команда"), Title = "DISM /Online /Set-ReservedStorageState", Detail = L.T("официальный способ Windows") };
            cmd.Add(L.T("Вкл"), "/State:Enabled").Add(L.T("Откл"), "/State:Disabled");
            try { cmd.Current = ReadEnabled() ? 0 : 1; } catch { }
            return new System.Collections.Generic.List<ChangeInfo> { cmd, ChangeText.FromReg(Reg.Dw(Reg.HKLM, K, "ShippedWithReserves", 1), Reg.Dw(Reg.HKLM, K, "ShippedWithReserves", 0)) };
        }
    }
}
