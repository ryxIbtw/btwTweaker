using Microsoft.Win32;

namespace BtwTweaker
{
    public static partial class Catalog
    {
        static Group[] SecurityGroups()
        {
            return new[]
            {
                new Group(L.T("Базовая защита Windows"),
                    DefenderNag(), new DefenderTweak(), SecurityCenter(), SmartScreen(), Firewall(), Amsi()),
                new Group(L.T("Усиление защиты (VBS/UAC)"),
                    Vbs(), AutoEncrypt(), Uac()),
                new Group(L.T("Микрокод и CPU-митигации"),
                    SpectreV2()),
                new Group(L.T("Сеть, фильтры и DRM"),
                    Hdcp(), Llmnr()),
            };
        }

        // ───────── Базовая защита ─────────

        static Tweak DefenderNag()
        {
            const string ns = @"SOFTWARE\Microsoft\Windows Security Health\State";
            return new RegToggle
            {
                Title = L.T("Назойливые уведомления защиты"),
                Description = L.T("Напоминания «Безопасности Windows»: включить защиту, настроить учётную запись, аккаунт Microsoft и т.п. Отключает только эти рекламные уведомления, саму защиту не трогает."),
                Icon = "", IconColor = "#F5C542",
            }
            .WhenOff(Reg.Dw(Reg.HKCU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance", "Enabled", 0),
                     Reg.Dw(Reg.HKLM, ns, "AccountProtection_MicrosoftAccount_Disconnected", 1))
            .WhenOn(Reg.Dw(Reg.HKCU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance", "Enabled", 1),
                    Reg.Del(Reg.HKLM, ns, "AccountProtection_MicrosoftAccount_Disconnected"));
        }

        static Tweak SecurityCenter()
        {
            return new RegToggle
            {
                Title = L.T("Центр безопасности"),
                Description = L.T("Служба «Безопасность Windows» (SecurityHealthService / wscsvc), которая собирает состояние защиты и показывает значок в трее. Без неё пропадает окно «Безопасность Windows»."),
                Icon = "", IconColor = "#E5484D",
                Security = L.T("перестанет работать окно «Безопасность Windows» и значок защиты."),
                Restart = Restart.Reboot,
            }
            .WhenOff(Reg.Start("wscsvc", 4), Reg.Start("SecurityHealthService", 4))
            .WhenOn(Reg.Start("wscsvc", 2), Reg.Start("SecurityHealthService", 3));
        }

        static Tweak SmartScreen()
        {
            const string sys = @"SOFTWARE\Policies\Microsoft\Windows\System";
            const string edge = @"SOFTWARE\Policies\Microsoft\Edge";
            return new RegToggle
            {
                Title = "SmartScreen",
                Description = L.T("Проверка скачанных файлов и сайтов по базе Microsoft. Предупреждает о подозрительных программах. Отключение убирает проверку и фильтр «неизвестный издатель»."),
                Icon = "", IconColor = "#E5484D",
                Security = L.T("пропадёт предупреждение о запуске подозрительных и неизвестных программ."),
            }
            .WhenOff(Reg.Dw(Reg.HKLM, sys, "EnableSmartScreen", 0),
                     Reg.Sz(Reg.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "Off"),
                     Reg.Dw(Reg.HKLM, edge, "SmartScreenEnabled", 0))
            .WhenOn(Reg.Del(Reg.HKLM, sys, "EnableSmartScreen"),
                    Reg.Del(Reg.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled"),
                    Reg.Del(Reg.HKLM, edge, "SmartScreenEnabled"));
        }

        static Tweak Firewall() { return new FirewallTweak(); }

        static Tweak Amsi() { return new AmsiTweak(); }

        // ───────── Усиление защиты ─────────

        static Tweak Vbs()
        {
            const string dg = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
            const string hvci = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
            return new RegToggle
            {
                Title = "VBS (Memory Integrity, DeviceGuard, HVCI, CG)",
                Description = L.T("Безопасность на основе виртуализации: изолирует часть памяти ядра аппаратно. Повышает защиту, но может снижать производительность в играх на 5–15%. Многие отключают ради FPS."),
                Icon = "", IconColor = "#E5484D",
                Security = L.T("ослабнет аппаратная защита памяти ядра (Memory Integrity)."),
                Caution = L.T("ради этого прироста FPS вы снижаете защиту от продвинутых атак."),
                Restart = Restart.Reboot,
            }
            .WhenOff(Reg.Dw(Reg.HKLM, dg, "EnableVirtualizationBasedSecurity", 0),
                     Reg.Dw(Reg.HKLM, hvci, "Enabled", 0))
            .WhenOn(Reg.Dw(Reg.HKLM, dg, "EnableVirtualizationBasedSecurity", 1),
                    Reg.Dw(Reg.HKLM, hvci, "Enabled", 1));
        }

        static Tweak AutoEncrypt()
        {
            return new RegToggle
            {
                Title = L.T("Автошифрование"),
                Description = L.T("Автоматическое шифрование диска устройства (Device Encryption / BitLocker) на новых Windows 11. Отключение не даёт Windows зашифровать диск без вашего ведома — так проще восстановить данные при сбое, но диск остаётся незашифрованным."),
                Icon = "", IconColor = "#E5484D",
                Security = L.T("данные на диске не будут зашифрованы при краже или потере устройства."),
            }
            .WhenOff(Reg.Dw(Reg.HKLM, @"SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", 1))
            .WhenOn(Reg.Del(Reg.HKLM, @"SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption"));
        }

        static Tweak Uac()
        {
            const string sys = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
            return new RegToggle
            {
                Title = "UAC",
                Description = L.T("Контроль учётных записей — окно «Разрешить этому приложению вносить изменения?». Отключение убирает эти запросы для администратора. Сам механизм UAC остаётся (иначе перестают работать приложения Магазина)."),
                Icon = "", IconColor = "#E5484D",
                Security = L.T("программы смогут получать права администратора без вашего подтверждения."),
            }
            .WhenOff(Reg.Dw(Reg.HKLM, sys, "ConsentPromptBehaviorAdmin", 0), Reg.Dw(Reg.HKLM, sys, "PromptOnSecureDesktop", 0))
            .WhenOn(Reg.Dw(Reg.HKLM, sys, "ConsentPromptBehaviorAdmin", 5), Reg.Dw(Reg.HKLM, sys, "PromptOnSecureDesktop", 1));
        }

        // ───────── CPU-митигации ─────────

        static Tweak SpectreV2()
        {
            const string mm = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
            return new RegToggle
            {
                Title = "Spectre v2",
                Description = L.T("Защита процессора от уязвимостей Spectre/Meltdown. Небольшой ценой производительности закрывает атаки через спекулятивное выполнение. Отключать стоит только на свой риск."),
                Icon = "", IconColor = "#E5484D",
                Security = L.T("процессор станет уязвим к атакам Spectre/Meltdown (чтение чужой памяти)."),
                Restart = Restart.Reboot,
                CheckOn = true,
                DefaultOn = true,
            }
            .WhenOn(Reg.Dw(Reg.HKLM, mm, "FeatureSettingsOverride", 0), Reg.Dw(Reg.HKLM, mm, "FeatureSettingsOverrideMask", 3))
            .WhenOff(Reg.Dw(Reg.HKLM, mm, "FeatureSettingsOverride", 3), Reg.Dw(Reg.HKLM, mm, "FeatureSettingsOverrideMask", 3));
        }

        // ───────── Сеть и DRM ─────────

        static Tweak Hdcp() { return new HdcpTweak(); }

        static Tweak Llmnr()
        {
            const string pol = @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient";
            const string svc = @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters";
            return new RegToggle
            {
                Title = L.T("Разрешение локальных имён LLMNR"),
                Description = L.T("Протокол разрешения имён в локальной сети без DNS-сервера. Дома почти не нужен и считается небезопасным: через него возможен перехват учётных данных в сети. Отключение повышает безопасность."),
                Icon = "", IconColor = "#F5C542",
            }
            .WhenOff(Reg.Dw(Reg.HKLM, svc, "EnableMulticast", 0), Reg.Dw(Reg.HKLM, pol, "EnableMulticast", 0))
            .WhenOn(Reg.Del(Reg.HKLM, svc, "EnableMulticast"), Reg.Del(Reg.HKLM, pol, "EnableMulticast"));
        }
    }
}
