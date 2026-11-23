namespace BtwTweaker
{
    public static partial class Catalog
    {
        const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string Exp = @"Software\Microsoft\Windows\CurrentVersion\Explorer";

        static Group[] CustomGroups()
        {
            return new[]
            {
                new Group(L.T("Визуальные эффекты"),
                    new VisualFx(), Transparency(), WallpaperCompression(), DynamicLighting()),
                new Group(L.T("Поиск и ассистенты"),
                    WebSearch(), SearchHighlights(), Ceip(), StartTips(), Copilot(), Widgets(), Spotlight(), HideMostUsed()),
                new Group(L.T("Оболочка Windows"),
                    new ClassicMenu(), new TrayIcons(), EndTask(), PhotoViewer(), RecentItems(), TrackDocs(),
                    MenuDelay(), TaskViewButton(), LockScreen(), ClockSeconds()),
                new Group(L.T("Ввод и клавиатура"),
                    StickyKeys(), InkTextCollection(), TypingFeedback()),
                new Group(L.T("Функции Windows"),
                    SettingSync(), MobilityCenter(), StepsRecorder(), ActivityFeed(), LanguageList()),
            };
        }

        // ───────── Визуальные эффекты ─────────

        static Tweak Transparency()
        {
            const string k = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            return new RegToggle
            {
                Title = L.T("Прозрачность Windows"),
                Description = L.T("Полупрозрачные «Пуск», панель задач и окна. Выключение немного разгружает видеокарту."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, k, "EnableTransparency", 0))
            .WhenOn(Reg.Dw(HKCU, k, "EnableTransparency", 1));
        }

        static Tweak WallpaperCompression()
        {
            return new RegToggle
            {
                Title = L.T("Сжатие фона рабочего стола"),
                Description = L.T("Windows пережимает JPEG-обои до качества 85%, из-за чего они теряют чёткость. После отключения обои ставятся в исходном качестве. Применится, когда обои установят заново."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, @"Control Panel\Desktop", "JPEGImportQuality", 100))
            .WhenOn(Reg.Del(HKCU, @"Control Panel\Desktop", "JPEGImportQuality"));
        }

        static Tweak DynamicLighting()
        {
            const string k = @"Software\Microsoft\Lighting";
            return new RegToggle
            {
                Title = L.T("Динамическое освещение"),
                Description = L.T("Управление RGB-подсветкой клавиатур, мышей и т.д. из Windows. Может конфликтовать с фирменными программами подсветки (iCUE, Armoury Crate, G Hub)."),
                Icon = "", IconColor = "#F5C542",
                Win11Only = true,
                MinBuild = 22621,
            }
            .WhenOff(Reg.Dw(HKCU, k, "AmbientLightingEnabled", 0))
            .WhenOn(Reg.Dw(HKCU, k, "AmbientLightingEnabled", 1));
        }

        // ───────── Поиск и ассистенты ─────────

        static Tweak WebSearch()
        {
            const string pol = @"Software\Policies\Microsoft\Windows\Explorer";
            return new RegToggle
            {
                Title = L.T("Результаты из интернета в Поиске"),
                Description = L.T("Поиск в «Пуске» показывает подсказки и результаты Bing. После отключения ищутся только приложения, файлы и настройки на компьютере — быстрее и без отправки запросов в интернет."),
                Icon = "",
                Restart = Restart.Explorer,
            }
            .WhenOff(Reg.Dw(HKCU, pol, "DisableSearchBoxSuggestions", 1),
                     Reg.Dw(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0),
                     Reg.Dw(HKLM, WinSearchPol, "DisableWebSearch", 1),
                     Reg.Dw(HKLM, WinSearchPol, "ConnectedSearchUseWeb", 0))
            .WhenOn(Reg.Del(HKCU, pol, "DisableSearchBoxSuggestions"),
                    Reg.Del(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled"),
                    Reg.Del(HKLM, WinSearchPol, "DisableWebSearch"),
                    Reg.Del(HKLM, WinSearchPol, "ConnectedSearchUseWeb"));
        }

        const string WinSearchPol = @"SOFTWARE\Policies\Microsoft\Windows\Windows Search";

        static Tweak SearchHighlights()
        {
            const string k = @"Software\Microsoft\Windows\CurrentVersion\SearchSettings";
            return new RegToggle
            {
                Title = L.T("WebView2 для Поиска"),
                Description = L.T("«Главное в поиске»: картинки дня, новости и праздники в окне поиска. Они загружаются через встроенный браузер WebView2. После отключения окно поиска открывается быстрее и не грузит интернет-контент."),
                Icon = "",
                Restart = Restart.Explorer,
            }
            .WhenOff(Reg.Dw(HKLM, WinSearchPol, "EnableDynamicContentInWSB", 0), Reg.Dw(HKCU, k, "IsDynamicSearchBoxEnabled", 0))
            .WhenOn(Reg.Del(HKLM, WinSearchPol, "EnableDynamicContentInWSB"), Reg.Dw(HKCU, k, "IsDynamicSearchBoxEnabled", 1));
        }

        static Tweak Ceip()
        {
            return new RegToggle
            {
                Title = "Customer Experience",
                Description = L.T("Программа улучшения качества (CEIP): Windows собирает статистику использования и отправляет её в Microsoft."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable", 0),
                     Reg.Dw(HKLM, @"SOFTWARE\Microsoft\SQMClient\Windows", "CEIPEnable", 0))
            .WhenOn(Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable"),
                    Reg.Dw(HKLM, @"SOFTWARE\Microsoft\SQMClient\Windows", "CEIPEnable", 1));
        }

        static Tweak StartTips()
        {
            const string cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            return new RegToggle
            {
                Title = L.T("Советы и новые приложения в меню «Пуск»"),
                Description = L.T("Рекомендации, советы и рекламные приложения в «Пуске», а также их тихая автоустановка (Candy Crush и т.п.)."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, cdm, "SubscribedContent-338388Enabled", 0),
                     Reg.Dw(HKCU, cdm, "SubscribedContent-338389Enabled", 0),
                     Reg.Dw(HKCU, cdm, "SystemPaneSuggestionsEnabled", 0),
                     Reg.Dw(HKCU, cdm, "SilentInstalledAppsEnabled", 0),
                     Reg.Dw(HKCU, Adv, "Start_IrisRecommendations", 0))
            .WhenOn(Reg.Dw(HKCU, cdm, "SubscribedContent-338388Enabled", 1),
                    Reg.Dw(HKCU, cdm, "SubscribedContent-338389Enabled", 1),
                    Reg.Dw(HKCU, cdm, "SystemPaneSuggestionsEnabled", 1),
                    Reg.Dw(HKCU, cdm, "SilentInstalledAppsEnabled", 1),
                    Reg.Dw(HKCU, Adv, "Start_IrisRecommendations", 1));
        }

        static Tweak Copilot()
        {
            const string pol = @"Software\Policies\Microsoft\Windows\WindowsCopilot";
            return new RegToggle
            {
                Title = "Windows Copilot",
                Description = L.T("ИИ-помощник Microsoft: кнопка на панели задач и вызов по Win+C. Если Copilot установлен как отдельное приложение, его можно удалить через «Установленные приложения»."),
                Icon = "",
                Win11Only = true,
                Restart = Restart.Explorer,
            }
            .WhenOff(Reg.Dw(HKCU, pol, "TurnOffWindowsCopilot", 1), Reg.Dw(HKCU, Adv, "ShowCopilotButton", 0))
            .WhenOn(Reg.Del(HKCU, pol, "TurnOffWindowsCopilot"), Reg.Dw(HKCU, Adv, "ShowCopilotButton", 1));
        }

        static Tweak Widgets()
        {
            return new RegToggle
            {
                Title = L.T("Виджеты"),
                Description = L.T("Панель виджетов с новостями и погодой (в Windows 10 — «Новости и интересы» на панели задач). Работает в фоне и грузит контент из интернета."),
                Icon = "", IconColor = "#F5C542",
                Restart = Restart.Explorer,
            }
            .WhenOff(Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0),
                     Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0))
            .WhenOn(Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests"),
                    Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds"));
        }

        static Tweak Spotlight()
        {
            const string cc = @"Software\Policies\Microsoft\Windows\CloudContent";
            return new RegToggle
            {
                Title = "Spotlight",
                Description = L.T("«Windows: интересное» — меняющиеся фото на экране блокировки и рабочем столе с подсказками и рекламой."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, cc, "DisableWindowsSpotlightFeatures", 1), Reg.Dw(HKCU, cc, "DisableSpotlightCollectionOnDesktop", 1))
            .WhenOn(Reg.Del(HKCU, cc, "DisableWindowsSpotlightFeatures"), Reg.Del(HKCU, cc, "DisableSpotlightCollectionOnDesktop"));
        }

        static Tweak HideMostUsed()
        {
            return new RegToggle
            {
                Title = L.T("Скрыть наиболее используемые приложения"),
                Description = L.T("Убирает список «Часто используемые» из меню «Пуск» — Windows перестаёт отслеживать запуск программ."),
                Icon = "",
                CheckOn = true,
                Restart = Restart.Explorer,
            }
            .WhenOn(Reg.Dw(HKCU, Adv, "Start_TrackProgs", 0))
            .WhenOff(Reg.Dw(HKCU, Adv, "Start_TrackProgs", 1));
        }

        // ───────── Оболочка Windows ─────────

        static Tweak EndTask()
        {
            return new RegToggle
            {
                Title = L.T("\"Завершить задачу\" в меню панели задач"),
                Description = L.T("Пункт «Завершить задачу» в меню по правому клику на значке программы — закрывает зависшую программу без Диспетчера задач."),
                Icon = "", IconColor = "#E5484D",
                Win11Only = true,
                MinBuild = 22631,
                CheckOn = true,
            }
            .WhenOn(Reg.Dw(HKCU, Adv + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 1))
            .WhenOff(Reg.Dw(HKCU, Adv + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 0));
        }

        static Tweak PhotoViewer()
        {
            const string fa = @"SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations";
            const string prog = "PhotoViewer.FileAssoc.Tiff";
            string[] ext = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".ico", ".jfif", ".jpe", ".dib", ".wdp" };
            var t = new RegToggle
            {
                Title = L.T("Старый смотрельщик фото"),
                Description = L.T("Возвращает классический «Просмотр фотографий Windows» для JPG, PNG, BMP, GIF и др. После включения откройте картинку через «Открыть с помощью» → «Просмотр фотографий Windows» → «Всегда»."),
                Icon = "", IconColor = "#F5C542",
                CheckOn = true,
            };
            foreach (var e in ext)
            {
                t.WhenOn(Reg.Sz(HKLM, fa, e, prog));
                t.WhenOff(Reg.Del(HKLM, fa, e));
            }
            return t;
        }

        static Tweak RecentItems()
        {
            return new RegToggle
            {
                Title = L.T("Отображение недавних и часто используемых элементов"),
                Description = L.T("Списки «Последние файлы» и «Часто используемые папки» на панели быстрого доступа Проводника."),
                Icon = "", IconColor = "#F5C542",
                Restart = Restart.Explorer,
            }
            .WhenOff(Reg.Dw(HKCU, Exp, "ShowRecent", 0), Reg.Dw(HKCU, Exp, "ShowFrequent", 0))
            .WhenOn(Reg.Dw(HKCU, Exp, "ShowRecent", 1), Reg.Dw(HKCU, Exp, "ShowFrequent", 1));
        }

        static Tweak TrackDocs()
        {
            return new RegToggle
            {
                Title = L.T("Отслеживание недавно открытых документов"),
                Description = L.T("Windows запоминает недавно открытые файлы для «Пуска», списков переходов и Проводника."),
                Icon = "",
                Restart = Restart.Explorer,
            }
            .WhenOff(Reg.Dw(HKCU, Adv, "Start_TrackDocs", 0))
            .WhenOn(Reg.Dw(HKCU, Adv, "Start_TrackDocs", 1));
        }

        static Tweak TaskViewButton()
        {
            return new RegToggle
            {
                Title = L.T("Показ Просмотра задач"),
                Description = L.T("Кнопка «Представление задач» (виртуальные рабочие столы) на панели задач. Сама функция по Win+Tab остаётся."),
                Icon = "", IconColor = "#F5C542",
            }
            .WhenOff(Reg.Dw(HKCU, Adv, "ShowTaskViewButton", 0))
            .WhenOn(Reg.Dw(HKCU, Adv, "ShowTaskViewButton", 1));
        }

        static Tweak LockScreen()
        {
            const string k = @"SOFTWARE\Policies\Microsoft\Windows\Personalization";
            return new RegToggle
            {
                Title = L.T("Экран блокировки"),
                Description = L.T("Экран с часами и картинкой перед вводом пароля. После отключения сразу показывается окно входа."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, k, "NoLockScreen", 1))
            .WhenOn(Reg.Del(HKLM, k, "NoLockScreen"));
        }

        static Tweak ClockSeconds()
        {
            return new RegToggle
            {
                Title = L.T("Показывать секунды в часах панели задач"),
                Description = L.T("Часы на панели задач показывают секунды."),
                Icon = "", IconColor = "#E5484D",
                CheckOn = true,
                Restart = Restart.Explorer,
            }
            .WhenOn(Reg.Dw(HKCU, Adv, "ShowSecondsInSystemClock", 1))
            .WhenOff(Reg.Dw(HKCU, Adv, "ShowSecondsInSystemClock", 0));
        }

        // ───────── Ввод и клавиатура ─────────

        static Tweak StickyKeys()
        {
            const string k = @"Control Panel\Accessibility\StickyKeys";
            return new RegToggle
            {
                Title = L.T("Залипание клавиш"),
                Description = L.T("Окно «Залипание клавиш» после пятикратного нажатия Shift — часто выскакивает в играх. После отключения сочетание перестаёт срабатывать."),
                Icon = "",
                Restart = Restart.SignOut,
            }
            .WhenOff(Reg.Sz(HKCU, k, "Flags", "506"))
            .WhenOn(Reg.Sz(HKCU, k, "Flags", "510"));
        }

        static Tweak InkTextCollection()
        {
            const string ip = @"Software\Microsoft\InputPersonalization";
            return new RegToggle
            {
                Title = L.T("Сбор текстовых сообщений и рукописного ввода"),
                Description = L.T("Windows собирает набранный текст и рукописный ввод, чтобы «улучшать» распознавание и подсказки."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\InputPersonalization", "AllowInputPersonalization", 0),
                     Reg.Dw(HKCU, ip, "RestrictImplicitTextCollection", 1),
                     Reg.Dw(HKCU, ip, "RestrictImplicitInkCollection", 1),
                     Reg.Dw(HKCU, ip + @"\TrainedDataStore", "HarvestContacts", 0))
            .WhenOn(Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\InputPersonalization", "AllowInputPersonalization"),
                    Reg.Dw(HKCU, ip, "RestrictImplicitTextCollection", 0),
                    Reg.Dw(HKCU, ip, "RestrictImplicitInkCollection", 0),
                    Reg.Dw(HKCU, ip + @"\TrainedDataStore", "HarvestContacts", 1));
        }

        static Tweak TypingFeedback()
        {
            return new RegToggle
            {
                Title = L.T("Обратная связь при вводе"),
                Description = L.T("«Улучшение распознавания рукописного ввода и набора»: отправка данных о вводе в Microsoft."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, @"Software\Microsoft\Input\TIPC", "Enabled", 0))
            .WhenOn(Reg.Dw(HKCU, @"Software\Microsoft\Input\TIPC", "Enabled", 1));
        }

        // ───────── Функции Windows ─────────

        static Tweak SettingSync()
        {
            const string k = @"SOFTWARE\Policies\Microsoft\Windows\SettingSync";
            return new RegToggle
            {
                Title = L.T("Синхронизация настроек аккаунта Microsoft"),
                Description = L.T("Синхронизация темы, паролей, языка и других настроек между устройствами через аккаунт Microsoft."),
                Icon = "", IconColor = "#F5A524",
            }
            .WhenOff(Reg.Dw(HKLM, k, "DisableSettingSync", 2), Reg.Dw(HKLM, k, "DisableSettingSyncUserOverride", 1))
            .WhenOn(Reg.Del(HKLM, k, "DisableSettingSync"), Reg.Del(HKLM, k, "DisableSettingSyncUserOverride"));
        }

        static Tweak MobilityCenter()
        {
            return new RegToggle
            {
                Title = L.T("Центр мобильности Windows"),
                Description = L.T("Старая панель для ноутбуков (Win+X → Центр мобильности): яркость, громкость, батарея."),
                Icon = "", IconColor = "#F5A524",
            }
            .WhenOff(Reg.Dw(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Policies\MobilityCenter", "NoMobilityCenter", 1),
                     Reg.Dw(HKCU, @"Software\Policies\Microsoft\MobilityCenter", "NoMobilityCenter", 1))
            .WhenOn(Reg.Del(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Policies\MobilityCenter", "NoMobilityCenter"),
                    Reg.Del(HKCU, @"Software\Policies\Microsoft\MobilityCenter", "NoMobilityCenter"));
        }

        static Tweak StepsRecorder()
        {
            return new RegToggle
            {
                Title = L.T("Запись действий"),
                Description = L.T("«Средство записи действий» (psr.exe) — записывает клики и скриншоты для отчётов об ошибках."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableUAR", 1))
            .WhenOn(Reg.Del(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableUAR"));
        }

        static Tweak ActivityFeed()
        {
            const string k = @"SOFTWARE\Policies\Microsoft\Windows\System";
            return new RegToggle
            {
                Title = L.T("Лента активности"),
                Description = L.T("Журнал действий: какие программы и файлы вы открывали. Сохраняется на компьютере и может отправляться в Microsoft."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, k, "EnableActivityFeed", 0), Reg.Dw(HKLM, k, "PublishUserActivities", 0), Reg.Dw(HKLM, k, "UploadUserActivities", 0))
            .WhenOn(Reg.Del(HKLM, k, "EnableActivityFeed"), Reg.Del(HKLM, k, "PublishUserActivities"), Reg.Del(HKLM, k, "UploadUserActivities"));
        }

        static Tweak LanguageList()
        {
            const string k = @"Control Panel\International\User Profile";
            return new RegToggle
            {
                Title = L.T("Доступ сайтов к списку языков"),
                Description = L.T("Сайты видят список установленных у вас языков и показывают контент на нём. Это также помогает отслеживать вас."),
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, k, "HttpAcceptLanguageOptOut", 1))
            .WhenOn(Reg.Del(HKCU, k, "HttpAcceptLanguageOptOut"));
        }
    }
}
