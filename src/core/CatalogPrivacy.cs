using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BtwTweaker
{
    public static partial class Catalog
    {
        const string AppPriv = @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy";
        const string Consent = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\";
        const string AppExp = @"\Microsoft\Windows\Application Experience\";
        const string CeipTask = @"\Microsoft\Windows\Customer Experience Improvement Program\";

        static Group[] PrivacyGroups()
        {
            return new[]
            {
                new Group("Диагностика и отчёты Windows",
                    EtwLoggers(), TelemetryServices(), CeipSqm(), AppImpact(), DataCollection(), LicenseTelemetry(), Wer(), Inventory()),
                new Group("Dev-инструменты и CLI",
                    EnvOptOut("Телеметрия Dot Net", "DOTNET_CLI_TELEMETRY_OPTOUT", ".NET SDK (команда dotnet) отправляет в Microsoft данные об использовании команд.", ""),
                    EnvOptOut("Телеметрия PowerShell", "POWERSHELL_TELEMETRY_OPTOUT", "PowerShell 7 при запуске отправляет в Microsoft данные о версии и окружении.", ""),
                    new VsCodeTelemetry(), VisualStudioTelemetry()),
                new Group("Планировщик и фоновые задания",
                    WebcamCensus(), CompatAppraiser(), StartupAppTask(), CeipTasks(), FeedbackDiag()),
                new Group("Системная приватность",
                    LocationProvider(), Advertising(), CloudSync(), PrivacyConsentOptOut(), Sensors()),
                new Group("Разрешения приложений",
                    AppAccess("местоположению", "LetAppsAccessLocation", "location", ""),
                    AppAccess("информации об учётной записи", "LetAppsAccessAccountInfo", "userAccountInformation", ""),
                    AppAccess("данным о движении", "LetAppsAccessMotion", "activity", ""),
                    AppAccess("телефону", "LetAppsAccessPhone", "phoneCall", ""),
                    AppAccess("доверенным устройствам", "LetAppsAccessTrustedDevices", null, ""),
                    AppAccess("синхронизации с устройствами", "LetAppsSyncWithDevices", null, ""),
                    AppAccess("диагностической информации о других приложениях", "LetAppsGetDiagnosticInfo", "appDiagnostics", ""),
                    AppAccess("контактам", "LetAppsAccessContacts", "contacts", ""),
                    AppAccess("календарю", "LetAppsAccessCalendar", "appointments", ""),
                    AppAccess("журналу вызовов", "LetAppsAccessCallHistory", "phoneCallHistory", ""),
                    AppAccess("электронной почте", "LetAppsAccessEmail", "email", ""),
                    AppAccess("задачам", "LetAppsAccessTasks", "userDataTasks", ""),
                    AppAccess("сообщениям", "LetAppsAccessMessaging", "chat", ""),
                    AppAccess("радио", "LetAppsAccessRadios", "radios", ""),
                    ConsentAccess("устройствам Bluetooth", new[] { "bluetoothSync", "bluetooth" }, ""),
                    ConsentAccess("папке \"Документы\"", new[] { "documentsLibrary" }, ""),
                    ConsentAccess("папке \"Изображения\"", new[] { "picturesLibrary" }, ""),
                    ConsentAccess("папке \"Видео\"", new[] { "videosLibrary" }, ""),
                    ConsentAccess("другой файловой системе", new[] { "broadFileSystemAccess" }, "")),
            };
        }

        // ───────── Диагностика и отчёты ─────────

        static RegVal Autologger(string name, int start)
        {
            return new RegVal { Hive = HKLM, Key = @"SYSTEM\CurrentControlSet\Control\WMI\Autologger\" + name, Name = "Start", Kind = RK.DWord, Data = start, OnlyIfKeyExists = true };
        }

        static readonly string[] Loggers = { "AutoLogger-Diagtrack-Listener", "DiagLog", "SQMLogger", "CloudExperienceHostOobe", "WiFiSession" };

        static Tweak EtwLoggers()
        {
            var t = new RegToggle
            {
                Title = "Фоновые ETW-автологгеры",
                Description = "Журналы событий, которые Windows пишет с самого запуска системы для телеметрии и диагностики (DiagTrack, SQM, OOBE, Wi-Fi). Пишут на диск постоянно.",
                Icon = "", IconColor = "#F5C542",
                Caution = "при разборе сбоев не будет части диагностических журналов.",
                Restart = Restart.Reboot,
            };
            foreach (var l in Loggers) { t.WhenOff(Autologger(l, 0)); t.WhenOn(Autologger(l, 1)); }
            return t;
        }

        static Tweak TelemetryServices()
        {
            return new RegToggle
            {
                Title = "Службы телеметрии",
                Description = "«Функциональные возможности для подключённых пользователей и телеметрия» (DiagTrack) и служба маршрутизации push-сообщений WAP. Собирают и отправляют данные в Microsoft.",
                Icon = "",
                StopServices = new[] { "DiagTrack", "dmwappushservice" },
                StartServices = new[] { "DiagTrack" },
            }
            .WhenOff(Reg.Start("DiagTrack", 4), Reg.Start("dmwappushservice", 4))
            .WhenOn(Reg.Start("DiagTrack", 2), Reg.Start("dmwappushservice", 3));
        }

        static Tweak CeipSqm()
        {
            return new RegToggle
            {
                Title = "CEIP/SQM",
                Description = "Программа улучшения качества (CEIP) и сбор статистики SQM в Windows, App-V и Internet Explorer.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable", 0),
                     Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\AppV\CEIP", "CEIPEnable", 0),
                     Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Internet Explorer\SQM", "DisableCustomerImprovementProgram", 0))
            .WhenOn(Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable"),
                    Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\AppV\CEIP", "CEIPEnable"),
                    Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Internet Explorer\SQM", "DisableCustomerImprovementProgram"));
        }

        const string AppCompatPol = @"SOFTWARE\Policies\Microsoft\Windows\AppCompat";

        static Tweak AppImpact()
        {
            return new RegToggle
            {
                Title = "Телеметрия влияния приложения",
                Description = "Application Impact Telemetry: Windows следит, как программы используют системные компоненты, и отправляет отчёты.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, AppCompatPol, "AITEnable", 0))
            .WhenOn(Reg.Del(HKLM, AppCompatPol, "AITEnable"));
        }

        static Tweak DataCollection()
        {
            const string pol = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";
            const string sys = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection";
            return new RegToggle
            {
                Title = "Телеметрия в политике сбора данных",
                Description = "Уровень диагностических данных Windows. Значение «0 — безопасность» полностью работает только в Enterprise/Education; в Home и Pro Windows понизит его до минимально допустимого («обязательные данные»).",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, pol, "AllowTelemetry", 0), Reg.Dw(HKLM, sys, "AllowTelemetry", 0), Reg.Dw(HKLM, sys, "MaxTelemetryAllowed", 1))
            .WhenOn(Reg.Del(HKLM, pol, "AllowTelemetry"), Reg.Del(HKLM, sys, "AllowTelemetry"), Reg.Del(HKLM, sys, "MaxTelemetryAllowed"));
        }

        static Tweak LicenseTelemetry()
        {
            const string spp = @"SOFTWARE\Policies\Microsoft\Windows NT\CurrentVersion\Software Protection Platform";
            return new RegToggle
            {
                Title = "Телеметрия лицензии",
                Description = "Отправка в Microsoft данных об активации при проверке лицензии (KMS Client Online AVS Validation). На саму активацию Windows не влияет.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, spp, "NoGenTicket", 1))
            .WhenOn(Reg.Del(HKLM, spp, "NoGenTicket"));
        }

        static Tweak Wer()
        {
            const string wer = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting";
            return new RegToggle
            {
                Title = "Отчёт об ошибках Windows (WER)",
                Description = "После сбоя программы Windows собирает отчёт и отправляет его в Microsoft. После отключения отчёты не создаются и не отправляются.",
                Icon = "",
                TaskPaths = new[] { @"\Microsoft\Windows\Windows Error Reporting\QueueReporting" },
            }
            .WhenOff(Reg.Dw(HKLM, wer, "Disabled", 1), Reg.Dw(HKLM, wer, "DontSendAdditionalData", 1), Reg.Dw(HKLM, wer, "LoggingDisabled", 1))
            .WhenOn(Reg.Del(HKLM, wer, "Disabled"), Reg.Del(HKLM, wer, "DontSendAdditionalData"), Reg.Del(HKLM, wer, "LoggingDisabled"));
        }

        static Tweak Inventory()
        {
            return new RegToggle
            {
                Title = "Inventory Collector",
                Description = "Сборщик инвентаризации: составляет список установленных программ, драйверов и устройств и отправляет его в Microsoft.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, AppCompatPol, "DisableInventory", 1))
            .WhenOn(Reg.Del(HKLM, AppCompatPol, "DisableInventory"));
        }

        // ───────── Dev-инструменты ─────────

        // Переменная среды пользователя XXX_TELEMETRY_OPTOUT=1
        static Tweak EnvOptOut(string title, string variable, string desc, string icon)
        {
            var t = new RegToggle
            {
                Title = title,
                Description = desc + " Отключается переменной среды " + variable + "=1 — программы, запущенные после изменения, увидят её сразу.",
                Icon = icon,
            }
            .WhenOff(Reg.Sz(HKCU, "Environment", variable, "1"))
            .WhenOn(Reg.Del(HKCU, "Environment", variable));
            t.After = on => Sys.BroadcastEnvironment();
            return t;
        }

        static Tweak VisualStudioTelemetry()
        {
            const string pol = @"SOFTWARE\Policies\Microsoft\VisualStudio";
            var t = new RegToggle
            {
                Title = "Телеметрия Visual Studio",
                Description = "Программа улучшения качества Visual Studio (SQM) и окно обратной связи со скриншотами и почтой.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, pol + @"\SQM", "OptIn", 0),
                     Reg.Dw(HKLM, pol + @"\Feedback", "DisableFeedbackDialog", 1),
                     Reg.Dw(HKLM, pol + @"\Feedback", "DisableEmailInput", 1),
                     Reg.Dw(HKLM, pol + @"\Feedback", "DisableScreenshotCapture", 1))
            .WhenOn(Reg.Del(HKLM, pol + @"\SQM", "OptIn"),
                    Reg.Del(HKLM, pol + @"\Feedback", "DisableFeedbackDialog"),
                    Reg.Del(HKLM, pol + @"\Feedback", "DisableEmailInput"),
                    Reg.Del(HKLM, pol + @"\Feedback", "DisableScreenshotCapture"));
            // Собственная настройка установленных версий (14.0–18.0), если они есть
            foreach (var v in new[] { "14.0", "15.0", "16.0", "17.0", "18.0" })
            {
                string k = @"SOFTWARE\WOW6432Node\Microsoft\VSCommon\" + v + @"\SQM";
                t.WhenOff(new RegVal { Hive = HKLM, Key = k, Name = "OptIn", Kind = RK.DWord, Data = 0, OnlyIfKeyExists = true });
                t.WhenOn(new RegVal { Hive = HKLM, Key = k, Name = "OptIn", Kind = RK.DWord, Data = 1, OnlyIfKeyExists = true });
            }
            return t;
        }

        // ───────── Планировщик ─────────

        static Tweak WebcamCensus()
        {
            return new RegToggle
            {
                Title = "Телеметрия веб-камеры",
                Description = "Задания DeviceCensus.exe — сканер подключённых устройств, включая веб-камеры, который отправляет сведения о них в Microsoft. Стримерам полезно выключить, чтобы камерой управлял только OBS; на ноутбуках камера реже «просыпается» во сне (S0ix).",
                Icon = "",
                TaskPaths = new[] { @"\Microsoft\Windows\Device Information\Device", @"\Microsoft\Windows\Device Information\Device User" },
            };
        }

        static Tweak CompatAppraiser()
        {
            return new RegToggle
            {
                Title = "Задача оценки совместимости Microsoft Compatibility Appraiser",
                Description = "Задания, которые регулярно сканируют программы и файлы на совместимость с новыми версиями Windows и отправляют результаты в Microsoft. Сильно нагружают диск и процессор во время работы.",
                Icon = "",
                TaskPaths = new[] { AppExp + "Microsoft Compatibility Appraiser", AppExp + "Microsoft Compatibility Appraiser Exp",
                                    AppExp + "ProgramDataUpdater", AppExp + "PcaPatchDbTask", AppExp + "MareBackup" },
            };
        }

        static Tweak StartupAppTask()
        {
            return new RegToggle
            {
                Title = "Напоминание о приложениях для улучшения производительности",
                Description = "Задание StartupAppTask проверяет программы в автозагрузке и показывает уведомление, что они замедляют запуск Windows.",
                Icon = "",
                TaskPaths = new[] { AppExp + "StartupAppTask" },
            };
        }

        static Tweak CeipTasks()
        {
            return new RegToggle
            {
                Title = "Задачи CEIP",
                Description = "Задания программы улучшения качества: сбор и отправка данных об использовании Windows и USB-устройств.",
                Icon = "",
                TaskPaths = new[] { CeipTask + "Consolidator", CeipTask + "UsbCeip", CeipTask + "KernelCeipTask" },
            };
        }

        static Tweak FeedbackDiag()
        {
            return new RegToggle
            {
                Title = "Обратная связь Windows и диагностика",
                Description = "Запросы «Оцените Windows» и задания, которые скачивают и запускают сценарии сбора отзывов.",
                Icon = "",
                TaskPaths = new[] { @"\Microsoft\Windows\Feedback\Siuf\DmClient", @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload" },
            }
            .WhenOff(Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1),
                     Reg.Dw(HKCU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0))
            .WhenOn(Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications"),
                    Reg.Del(HKCU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod"));
        }

        // ───────── Системная приватность ─────────

        const string LocSens = @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors";

        static Tweak LocationProvider()
        {
            return new RegToggle
            {
                Title = "Поставщик местоположения Windows",
                Description = "Определение местоположения компьютера по Wi-Fi и IP для Windows и приложений (карты, погода, «Найти устройство»).",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, LocSens, "DisableWindowsLocationProvider", 1), Reg.Dw(HKLM, LocSens, "DisableLocation", 1), Reg.Dw(HKLM, LocSens, "DisableLocationScripting", 1))
            .WhenOn(Reg.Del(HKLM, LocSens, "DisableWindowsLocationProvider"), Reg.Del(HKLM, LocSens, "DisableLocation"), Reg.Del(HKLM, LocSens, "DisableLocationScripting"));
        }

        static Tweak Advertising()
        {
            return new RegToggle
            {
                Title = "Целевая реклама и маркетинг",
                Description = "Рекламный идентификатор, по которому приложения показывают персональную рекламу, и «персонализированные возможности» на основе диагностических данных.",
                Icon = "", IconColor = "#F5C542",
                Caution = "переключатель «Рекламный идентификатор» в «Параметрах» станет недоступен — им будет управлять политика.",
            }
            .WhenOff(Reg.Dw(HKCU, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
                     Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1),
                     Reg.Dw(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0))
            .WhenOn(Reg.Dw(HKCU, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1),
                    Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy"),
                    Reg.Dw(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 1));
        }

        static Tweak CloudSync()
        {
            const string k = @"SOFTWARE\Policies\Microsoft\Windows\SettingSync";
            string[] parts = { "Credentials", "Application", "AppSync", "DesktopTheme", "Personalization", "StartLayout", "WebBrowser", "Windows" };
            var t = new RegToggle
            {
                Title = "Синхронизация в облаке",
                Description = "Отправка в облако Microsoft отдельных групп данных: паролей, настроек приложений, темы, макета «Пуска», браузера и Windows, а также синхронизация через лимитные сети.",
                Icon = "",
            };
            foreach (var p in parts)
            {
                t.WhenOff(Reg.Dw(HKLM, k, "Disable" + p + "SettingSync", 2), Reg.Dw(HKLM, k, "Disable" + p + "SettingSyncUserOverride", 1));
                t.WhenOn(Reg.Del(HKLM, k, "Disable" + p + "SettingSync"), Reg.Del(HKLM, k, "Disable" + p + "SettingSyncUserOverride"));
            }
            t.WhenOff(Reg.Dw(HKLM, k, "DisableSyncOnPaidNetwork", 1));
            t.WhenOn(Reg.Del(HKLM, k, "DisableSyncOnPaidNetwork"));
            return t;
        }

        static Tweak PrivacyConsentOptOut()
        {
            const string k = @"SOFTWARE\Microsoft\Personalization\Settings";
            return new RegToggle
            {
                Title = "Отказ от согласия на конфиденциальность Windows",
                Description = "Сбрасывает флаг «Приняты политики конфиденциальности» (AcceptedPrivacyPolicy = 0) — баннер с предложением согласиться больше не появится. Позже можно снова изменить решение.",
                Icon = "",
                CheckOn = true,
            }
            .WhenOn(Reg.Dw(HKCU, k, "AcceptedPrivacyPolicy", 0))
            .WhenOff(Reg.Dw(HKCU, k, "AcceptedPrivacyPolicy", 1));
        }

        static Tweak Sensors()
        {
            return new RegToggle
            {
                Title = "Датчики",
                Description = "Доступ Windows и приложений к датчикам: освещённости, ориентации, движения и т.д. На ноутбуках с автояркостью и поворотом экрана лучше не отключать.",
                Icon = "", IconColor = "#F5A524",
            }
            .WhenOff(Reg.Dw(HKLM, LocSens, "DisableSensors", 1))
            .WhenOn(Reg.Del(HKLM, LocSens, "DisableSensors"));
        }

        // ───────── Разрешения приложений ─────────

        // Политика LetAppsAccess* = 2 (запретить) + общий запрет в ConsentStore
        static Tweak AppAccess(string what, string policy, string consent, string icon)
        {
            var t = new RegToggle
            {
                Title = "Доступ приложений к " + what,
                Description = "Могут ли приложения из Магазина получать доступ к " + what + ". После отключения запрет действует для всех приложений и пользователей, а переключатель в «Параметрах» становится недоступен.",
                Icon = icon,
            }
            .WhenOff(Reg.Dw(HKLM, AppPriv, policy, 2))
            .WhenOn(Reg.Del(HKLM, AppPriv, policy));
            if (consent != null)
            {
                t.WhenOff(new RegVal { Hive = HKLM, Key = Consent + consent, Name = "Value", Kind = RK.String, Data = "Deny", OnlyIfKeyExists = true });
                t.WhenOn(new RegVal { Hive = HKLM, Key = Consent + consent, Name = "Value", Kind = RK.String, Data = "Allow", OnlyIfKeyExists = true });
            }
            return t;
        }

        // Только ConsentStore (для этих разрешений нет групповой политики)
        static Tweak ConsentAccess(string what, string[] consents, string icon)
        {
            var t = new RegToggle
            {
                Title = "Доступ приложений к " + what,
                Description = "Могут ли приложения из Магазина получать доступ к " + what + ". Запрет ставится для всего компьютера.",
                Icon = icon,
            };
            foreach (var c in consents)
            {
                t.WhenOff(new RegVal { Hive = HKLM, Key = Consent + c, Name = "Value", Kind = RK.String, Data = "Deny", OnlyIfKeyExists = true });
                t.WhenOn(new RegVal { Hive = HKLM, Key = Consent + c, Name = "Value", Kind = RK.String, Data = "Allow", OnlyIfKeyExists = true });
            }
            return t;
        }
    }

    // Телеметрия VS Code: политика TelemetryLevel и настройка telemetry.telemetryLevel в settings.json
    public class VsCodeTelemetry : ToggleTweak
    {
        const string Pol = @"SOFTWARE\Policies\Microsoft\VSCode";
        static string Settings { get { return Fs.E(@"%APPDATA%\Code\User\settings.json"); } }
        static readonly Regex Key = new Regex("\"telemetry\\.telemetryLevel\"\\s*:\\s*\"([^\"]*)\"");

        public VsCodeTelemetry()
        {
            Title = "Телеметрия Visual Studio Code";
            Description = "VS Code отправляет в Microsoft данные об использовании и ошибках. Отключается политикой и настройкой telemetry.telemetryLevel = off в settings.json (перед изменением файла сохраняется его копия).";
            Icon = "";
        }

        public override bool IsSupported()
        {
            return Directory.Exists(Fs.E(@"%APPDATA%\Code")) || Directory.Exists(Fs.E(@"%LOCALAPPDATA%\Programs\Microsoft VS Code"));
        }

        public override bool ReadEnabled()
        {
            var p = Reg.Get(Reg.HKLM, Pol, "TelemetryLevel") as string;
            if (p != null && p.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;
            if (File.Exists(Settings))
            {
                var m = Key.Match(File.ReadAllText(Settings));
                if (m.Success && m.Groups[1].Value.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        public override void SetEnabled(bool on)
        {
            Reg.Apply(on ? Reg.Del(Reg.HKLM, Pol, "TelemetryLevel") : Reg.Sz(Reg.HKLM, Pol, "TelemetryLevel", "off"));
            if (!File.Exists(Settings)) return;
            string text = File.ReadAllText(Settings);
            string updated = text;
            var m = Key.Match(text);
            if (on)
            {
                if (m.Success && m.Groups[1].Value.Equals("off", StringComparison.OrdinalIgnoreCase))
                    updated = Key.Replace(text, "\"telemetry.telemetryLevel\": \"all\"", 1);
            }
            else if (m.Success) updated = Key.Replace(text, "\"telemetry.telemetryLevel\": \"off\"", 1);
            else
            {
                int i = text.IndexOf('{');
                if (i < 0) return;   // файл не похож на JSON — не трогаем
                updated = text.Insert(i + 1, "\n    \"telemetry.telemetryLevel\": \"off\",");
            }
            if (updated == text) return;
            Backup.SaveFile(Settings);
            File.WriteAllText(Settings, updated, new UTF8Encoding(false));
        }
    }
}
