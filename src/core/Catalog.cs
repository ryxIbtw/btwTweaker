using System;
using System.Collections.Generic;

namespace BtwTweaker
{
    // Список разделов и твиков
    public static partial class Catalog
    {
        public static List<Section> Build()
        {
            return new List<Section>
            {
                new Section("Базовые твики", "",
                    new Group("Ввод и интерфейс", MouseAccel(), Notifications()),
                    new Group("Обновления и доставка", DriverUpdates(), MapsUpdates(), StoreUpdates(), new WinUpdate(), DeliveryOpt()),
                    new Group("Службы и фоновые процессы", UwpBackground(), Indexing(), SysMain(), Printing(), Diagnostics(), EdgeBoost()),
                    new Group("Игры и графика", GameBar(), GameMode(), new HagsTweak())),
                new Section("Безопасность", "", SecurityGroups()),
                new Section("Приватность", "", PrivacyGroups()),
                new Section("Твики", "", TweaksGroups()),
                new Section("Кастомизация", "", CustomGroups()),
                new Section("Электропитание", "") { Page = "power" },
                new Section("Очистка", "") { Page = "clean" },
            };
        }

        static readonly Microsoft.Win32.RegistryHive HKLM = Reg.HKLM, HKCU = Reg.HKCU;

        // ───────── Ввод и интерфейс ─────────

        const string Mouse = @"Control Panel\Mouse";

        static Tweak MouseAccel()
        {
            var t = new RegToggle
            {
                Title = "Акселерация мыши",
                Description = "«Повышенная точность установки указателя»: курсор проходит разное расстояние в зависимости от скорости движения мыши. Без неё движение предсказуемее — особенно в играх.",
                Icon = "",
            }
            .WhenOff(Reg.Sz(HKCU, Mouse, "MouseSpeed", "0"), Reg.Sz(HKCU, Mouse, "MouseThreshold1", "0"), Reg.Sz(HKCU, Mouse, "MouseThreshold2", "0"))
            .WhenOn(Reg.Sz(HKCU, Mouse, "MouseSpeed", "1"), Reg.Sz(HKCU, Mouse, "MouseThreshold1", "6"), Reg.Sz(HKCU, Mouse, "MouseThreshold2", "10"));
            // Применяем сразу, без перезахода: SPI_SETMOUSE
            t.After = on => Sys.SystemParametersInfo(0x0004, 0, on ? new[] { 6, 10, 1 } : new[] { 0, 0, 0 }, Sys.SPIF_UPDATE);
            return t;
        }

        static Tweak Notifications()
        {
            const string push = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";
            return new RegToggle
            {
                Title = "Уведомления Windows",
                Description = "Всплывающие баннеры от приложений и системы в углу экрана. После отключения баннеры перестанут появляться, а важные системные сообщения останутся в Центре уведомлений.",
                Icon = "", IconColor = "#F5C542",
                Restart = Restart.Explorer,
            }
            .WhenOff(Reg.Dw(HKCU, push, "ToastEnabled", 0))
            .WhenOn(Reg.Dw(HKCU, push, "ToastEnabled", 1));
        }

        // ───────── Обновления и доставка ─────────

        static Tweak DriverUpdates()
        {
            return new RegToggle
            {
                Title = "Автообновление драйверов при запуске системы",
                Description = "Windows Update сам скачивает и ставит драйверы — иногда старее или хуже тех, что вы поставили вручную (частая проблема с видеокартами). После отключения драйверы обновляются только вами.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1),
                     Reg.Dw(HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", 0),
                     Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Device Metadata", "PreventDeviceMetadataFromNetwork", 1))
            .WhenOn(Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate"),
                    Reg.Dw(HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", 1),
                    Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Device Metadata", "PreventDeviceMetadataFromNetwork"));
        }

        static Tweak MapsUpdates()
        {
            return new RegToggle
            {
                Title = "Автообновление карт",
                Description = "Фоновое скачивание офлайн-карт для приложения «Карты». Почти никому не нужно, но тратит трафик и диск.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, @"SYSTEM\Maps", "AutoUpdateEnabled", 0),
                     Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Maps", "AutoDownloadAndUpdateMapData", 0))
            .WhenOn(Reg.Dw(HKLM, @"SYSTEM\Maps", "AutoUpdateEnabled", 1),
                    Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Maps", "AutoDownloadAndUpdateMapData"));
        }

        static Tweak StoreUpdates()
        {
            const string store = @"SOFTWARE\Policies\Microsoft\WindowsStore";
            return new RegToggle
            {
                Title = "Автообновление приложений магазина",
                Description = "Microsoft Store сам обновляет приложения в фоне. После отключения обновлять их можно вручную: Магазин → Библиотека → «Получить обновления».",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, store, "AutoDownload", 2))
            .WhenOn(Reg.Del(HKLM, store, "AutoDownload"));
        }

        static Tweak DeliveryOpt()
        {
            const string doKey = @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            return new RegToggle
            {
                Title = "Оптимизация доставки",
                Description = "Windows скачивает обновления частями с других компьютеров и раздаёт ваши обновления другим — это тратит трафик и канал. После отключения обновления качаются только с серверов Microsoft.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, doKey, "DODownloadMode", 0))
            .WhenOn(Reg.Del(HKLM, doKey, "DODownloadMode"));
        }

        // ───────── Службы и фоновые процессы ─────────

        static Tweak UwpBackground()
        {
            const string bg = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
            const string pol = @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy";
            return new RegToggle
            {
                Title = "Работа UWP приложений в фоне",
                Description = "Приложения из Магазина (Почта, Погода, Xbox и др.) работают в фоне, даже когда закрыты. После отключения они не тратят память и процессор, но и не присылают уведомления, пока не открыты.",
                Icon = "",
                Restart = Restart.SignOut,
            }
            .WhenOff(Reg.Dw(HKCU, bg, "GlobalUserDisabled", 1), Reg.Dw(HKLM, pol, "LetAppsRunInBackground", 2))
            .WhenOn(Reg.Dw(HKCU, bg, "GlobalUserDisabled", 0), Reg.Del(HKLM, pol, "LetAppsRunInBackground"));
        }

        static Tweak Indexing()
        {
            return new RegToggle
            {
                Title = "Индексация",
                Description = "Служба Windows Search постоянно сканирует файлы, чтобы поиск был мгновенным. После отключения диск меньше нагружается, но поиск в «Пуске» и Проводнике по содержимому файлов станет медленнее.",
                Icon = "",
                StopServices = new[] { "WSearch" },
                StartServices = new[] { "WSearch" },
            }
            .WhenOff(Reg.Start("WSearch", 4))
            .WhenOn(Reg.Start("WSearch", 2), Reg.Delayed("WSearch", 1));
        }

        static Tweak SysMain()
        {
            return new RegToggle
            {
                Title = "SysMain (Prefetch, Superfetch…)",
                Description = "Служба заранее загружает в память часто используемые программы. На SSD пользы почти нет, а на слабых ПК она может грузить диск.",
                Icon = "",
                Caution = "на HDD Windows и программы могут запускаться медленнее.",
                StopServices = new[] { "SysMain" },
                StartServices = new[] { "SysMain" },
            }
            .WhenOff(Reg.Start("SysMain", 4))
            .WhenOn(Reg.Start("SysMain", 2));
        }

        static Tweak Printing()
        {
            return new RegToggle
            {
                Title = "Службы печати",
                Description = "Диспетчер печати и уведомления принтеров. Если принтера нет, службы можно отключить.",
                Icon = "", IconColor = "#F5C542",
                Caution = "перестанут работать принтеры, МФУ и «Печать в PDF».",
                StopServices = new[] { "PrintNotify", "Spooler" },
                StartServices = new[] { "Spooler" },
            }
            .WhenOff(Reg.Start("Spooler", 4), Reg.Start("PrintNotify", 4))
            .WhenOn(Reg.Start("Spooler", 2), Reg.Start("PrintNotify", 3));
        }

        static Tweak Diagnostics()
        {
            return new RegToggle
            {
                Title = "Службы диагностики",
                Description = "Телеметрия (DiagTrack), служба политики диагностики и сборщики диагностических данных. Работают в фоне и отправляют данные в Microsoft.",
                Icon = "",
                Caution = "перестанет работать «Устранение неполадок» и диагностика сети.",
                StopServices = new[] { "DiagTrack", "dmwappushservice", "diagnosticshub.standardcollector.service", "WdiServiceHost", "WdiSystemHost", "DPS" },
                StartServices = new[] { "DiagTrack", "DPS" },
            }
            .WhenOff(Reg.Start("DiagTrack", 4), Reg.Start("dmwappushservice", 4), Reg.Start("diagnosticshub.standardcollector.service", 4),
                     Reg.Start("DPS", 4), Reg.Start("WdiServiceHost", 4), Reg.Start("WdiSystemHost", 4))
            .WhenOn(Reg.Start("DiagTrack", 2), Reg.Start("dmwappushservice", 3), Reg.Start("diagnosticshub.standardcollector.service", 3),
                    Reg.Start("DPS", 2), Reg.Start("WdiServiceHost", 3), Reg.Start("WdiSystemHost", 3));
        }

        static Tweak EdgeBoost()
        {
            const string edge = @"SOFTWARE\Policies\Microsoft\Edge";
            return new RegToggle
            {
                Title = "Ускорение запуска и фоновая работа Edge",
                Description = "Edge держит свои процессы в памяти после закрытия и при старте Windows, чтобы открываться быстрее. После отключения Edge не висит в фоне, но первый запуск чуть дольше.",
                Icon = "", IconColor = "#3FA9F5",
            }
            .WhenOff(Reg.Dw(HKLM, edge, "StartupBoostEnabled", 0), Reg.Dw(HKLM, edge, "BackgroundModeEnabled", 0))
            .WhenOn(Reg.Del(HKLM, edge, "StartupBoostEnabled"), Reg.Del(HKLM, edge, "BackgroundModeEnabled"));
        }

        // ───────── Игры и графика ─────────

        static Tweak GameBar()
        {
            return new RegToggle
            {
                Title = "Game Bar",
                Description = "Игровая панель Xbox (Win+G) и фоновая запись игр. Отключение убирает лишнюю нагрузку и всплывающие окна при запуске игр.",
                Icon = "", IconColor = "#E5484D",
            }
            .WhenOff(Reg.Dw(HKCU, @"System\GameConfigStore", "GameDVR_Enabled", 0),
                     Reg.Dw(HKCU, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
                     Reg.Dw(HKCU, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0),
                     Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0))
            .WhenOn(Reg.Dw(HKCU, @"System\GameConfigStore", "GameDVR_Enabled", 1),
                    Reg.Dw(HKCU, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1),
                    Reg.Dw(HKCU, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 1),
                    Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR"));
        }

        static Tweak GameMode()
        {
            const string gb = @"Software\Microsoft\GameBar";
            return new RegToggle
            {
                Title = "Игровой режим",
                Description = "Когда запущена игра, Windows отдаёт ей приоритет и откладывает фоновые задачи (например, установку обновлений). Обычно лучше оставить включённым.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, gb, "AutoGameModeEnabled", 0), Reg.Dw(HKCU, gb, "AllowAutoGameMode", 0))
            .WhenOn(Reg.Dw(HKCU, gb, "AutoGameModeEnabled", 1), Reg.Dw(HKCU, gb, "AllowAutoGameMode", 1));
        }

        // ───────── Кастомизация: задержка меню (остальное — в CatalogCustom.cs) ─────────

        static Tweak MenuDelay()
        {
            const string desk = @"Control Panel\Desktop";
            int[] ms = { 0, 100, 200, 400 };
            var vals = new RegVal[ms.Length];
            for (int i = 0; i < ms.Length; i++) vals[i] = Reg.Sz(HKCU, desk, "MenuShowDelay", ms[i].ToString());
            return new RegChoice
            {
                Title = "Задержка появления контекстного меню",
                Description = "Сколько Windows ждёт, прежде чем раскрыть вложенное меню при наведении. По умолчанию — 400 мс.",
                Icon = "",
                Options = new[] { "0 мс", "100 мс", "200 мс", "400 мс (стандарт)" },
                Values = vals,
                // Применяем сразу: SPI_SETMENUSHOWDELAY
                After = i => Sys.SystemParametersInfo(0x006B, (uint)ms[i], IntPtr.Zero, Sys.SPIF_UPDATE),
            };
        }
    }
}
