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
                new Group("Визуальные эффекты",
                    new VisualFx(), Transparency(), WallpaperCompression(), DynamicLighting()),
                new Group("Поиск и ассистенты",
                    WebSearch(), SearchHighlights(), Ceip(), StartTips(), Copilot(), Widgets(), Spotlight(), HideMostUsed()),
                new Group("Оболочка Windows",
                    new ClassicMenu(), new TrayIcons(), EndTask(), PhotoViewer(), RecentItems(), TrackDocs(),
                    MenuDelay(), TaskViewButton(), LockScreen(), ClockSeconds()),
                new Group("Ввод и клавиатура",
                    StickyKeys(), InkTextCollection(), TypingFeedback()),
                new Group("Функции Windows",
                    SettingSync(), MobilityCenter(), StepsRecorder(), ActivityFeed(), LanguageList()),
            };
        }

        // ───────── Визуальные эффекты ─────────

        static Tweak Transparency()
        {
            const string k = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            return new RegToggle
            {
                Title = "Прозрачность Windows",
                Description = "Полупрозрачные «Пуск», панель задач и окна. Выключение немного разгружает видеокарту.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, k, "EnableTransparency", 0))
            .WhenOn(Reg.Dw(HKCU, k, "EnableTransparency", 1));
        }

        static Tweak WallpaperCompression()
        {
            return new RegToggle
            {
                Title = "Сжатие фона рабочего стола",
                Description = "Windows пережимает JPEG-обои до качества 85%, из-за чего они теряют чёткость. После отключения обои ставятся в исходном качестве. Применится, когда обои установят заново.",
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
                Title = "Динамическое освещение",
                Description = "Управление RGB-подсветкой клавиатур, мышей и т.д. из Windows. Может конфликтовать с фирменными программами подсветки (iCUE, Armoury Crate, G Hub).",
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
                Title = "Результаты из интернета в Поиске",
                Description = "Поиск в «Пуске» показывает подсказки и результаты Bing. После отключения ищутся только приложения, файлы и настройки на компьютере — быстрее и без отправки запросов в интернет.",
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
                Title = "WebView2 для Поиска",
                Description = "«Главное в поиске»: картинки дня, новости и праздники в окне поиска. Они загружаются через встроенный браузер WebView2. После отключения окно поиска открывается быстрее и не грузит интернет-контент.",
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
                Description = "Программа улучшения качества (CEIP): Windows собирает статистику использования и отправляет её в Microsoft.",
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
                Title = "Советы и новые приложения в меню «Пуск»",
                Description = "Рекомендации, советы и рекламные приложения в «Пуске», а также их тихая автоустановка (Candy Crush и т.п.).",
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
                Description = "ИИ-помощник Microsoft: кнопка на панели задач и вызов по Win+C. Если Copilot установлен как отдельное приложение, его можно удалить через «Установленные приложения».",
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
                Title = "Виджеты",
                Description = "Панель виджетов с новостями и погодой (в Windows 10 — «Новости и интересы» на панели задач). Работает в фоне и грузит контент из интернета.",
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
                Description = "«Windows: интересное» — меняющиеся фото на экране блокировки и рабочем столе с подсказками и рекламой.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, cc, "DisableWindowsSpotlightFeatures", 1), Reg.Dw(HKCU, cc, "DisableSpotlightCollectionOnDesktop", 1))
            .WhenOn(Reg.Del(HKCU, cc, "DisableWindowsSpotlightFeatures"), Reg.Del(HKCU, cc, "DisableSpotlightCollectionOnDesktop"));
        }

        static Tweak HideMostUsed()
        {
            return new RegToggle
            {
                Title = "Скрыть наиболее используемые приложения",
                Description = "Убирает список «Часто используемые» из меню «Пуск» — Windows перестаёт отслеживать запуск программ.",
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
                Title = "\"Завершить задачу\" в меню панели задач",
                Description = "Пункт «Завершить задачу» в меню по правому клику на значке программы — закрывает зависшую программу без Диспетчера задач.",
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
                Title = "Старый смотрельщик фото",
                Description = "Возвращает классический «Просмотр фотографий Windows» для JPG, PNG, BMP, GIF и др. После включения откройте картинку через «Открыть с помощью» → «Просмотр фотографий Windows» → «Всегда».",
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
                Title = "Отображение недавних и часто используемых элементов",
                Description = "Списки «Последние файлы» и «Часто используемые папки» на панели быстрого доступа Проводника.",
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
                Title = "Отслеживание недавно открытых документов",
                Description = "Windows запоминает недавно открытые файлы для «Пуска», списков переходов и Проводника.",
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
                Title = "Показ Просмотра задач",
                Description = "Кнопка «Представление задач» (виртуальные рабочие столы) на панели задач. Сама функция по Win+Tab остаётся.",
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
                Title = "Экран блокировки",
                Description = "Экран с часами и картинкой перед вводом пароля. После отключения сразу показывается окно входа.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKLM, k, "NoLockScreen", 1))
            .WhenOn(Reg.Del(HKLM, k, "NoLockScreen"));
        }

        static Tweak ClockSeconds()
        {
            return new RegToggle
            {
                Title = "Показывать секунды в часах панели задач",
                Description = "Часы на панели задач показывают секунды.",
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
                Title = "Залипание клавиш",
                Description = "Окно «Залипание клавиш» после пятикратного нажатия Shift — часто выскакивает в играх. После отключения сочетание перестаёт срабатывать.",
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
                Title = "Сбор текстовых сообщений и рукописного ввода",
                Description = "Windows собирает набранный текст и рукописный ввод, чтобы «улучшать» распознавание и подсказки.",
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
                Title = "Обратная связь при вводе",
                Description = "«Улучшение распознавания рукописного ввода и набора»: отправка данных о вводе в Microsoft.",
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
                Title = "Синхронизация настроек аккаунта Microsoft",
                Description = "Синхронизация темы, паролей, языка и других настроек между устройствами через аккаунт Microsoft.",
                Icon = "", IconColor = "#F5A524",
            }
            .WhenOff(Reg.Dw(HKLM, k, "DisableSettingSync", 2), Reg.Dw(HKLM, k, "DisableSettingSyncUserOverride", 1))
            .WhenOn(Reg.Del(HKLM, k, "DisableSettingSync"), Reg.Del(HKLM, k, "DisableSettingSyncUserOverride"));
        }

        static Tweak MobilityCenter()
        {
            return new RegToggle
            {
                Title = "Центр мобильности Windows",
                Description = "Старая панель для ноутбуков (Win+X → Центр мобильности): яркость, громкость, батарея.",
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
                Title = "Запись действий",
                Description = "«Средство записи действий» (psr.exe) — записывает клики и скриншоты для отчётов об ошибках.",
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
                Title = "Лента активности",
                Description = "Журнал действий: какие программы и файлы вы открывали. Сохраняется на компьютере и может отправляться в Microsoft.",
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
                Title = "Доступ сайтов к списку языков",
                Description = "Сайты видят список установленных у вас языков и показывают контент на нём. Это также помогает отслеживать вас.",
                Icon = "",
            }
            .WhenOff(Reg.Dw(HKCU, k, "HttpAcceptLanguageOptOut", 1))
            .WhenOn(Reg.Del(HKCU, k, "HttpAcceptLanguageOptOut"));
        }
    }
}
