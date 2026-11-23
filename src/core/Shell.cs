using System;
using System.Collections.Generic;

namespace BtwTweaker
{
    // «Внешний вид»: визуальные эффекты Windows (как в «Параметрах быстродействия»)
    public class VisualFx : ChoiceTweak
    {
        const string VE = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
        const string Desk = @"Control Panel\Desktop";
        const string WM = @"Control Panel\Desktop\WindowMetrics";
        const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string Dwm = @"Software\Microsoft\Windows\DWM";

        // Маски UserPreferencesMask: стандартная Windows и «наилучшее быстродействие»
        static readonly byte[] MaskDefault = { 0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 };
        static readonly byte[] MaskPerf = { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 };

        public VisualFx()
        {
            Title = "Внешний вид";
            Icon = "";
            Options = new[] { "По умолчанию", "Лучший вид", "Оптимальное", "Производительность" };
            Description = "Визуальные эффекты Windows: анимации окон, тени, сглаживание и т.д.\n" +
                          "Оптимальное — без анимаций и теней, но со сглаживанием шрифтов, эскизами и содержимым окон при перетаскивании. Хороший баланс для слабых ПК и игр.\n" +
                          "Производительность — все эффекты выключены (шрифты остаются сглаженными).";
            Caution = "изменения полностью применятся после выхода из системы.";
            Restart = Restart.SignOut;
        }

        public override int ReadIndex()
        {
            int? fx = Reg.GetInt(Reg.HKCU, VE, "VisualFXSetting");
            if (!fx.HasValue || fx.Value == 0) return 0;
            if (fx.Value == 1) return 1;
            if (fx.Value == 2) return 3;
            // 3 = «особые эффекты» — проверяем, совпадает ли с нашим «Оптимальным»
            if (Reg.Matches(Reg.Sz(Reg.HKCU, WM, "MinAnimate", "0")) &&
                Reg.Matches(Reg.Dw(Reg.HKCU, Adv, "TaskbarAnimations", 0)) &&
                Reg.Matches(Reg.Sz(Reg.HKCU, Desk, "DragFullWindows", "1")))
                return 2;
            return -1;
        }

        public override void SetIndex(int i)
        {
            bool fancy = i <= 1;
            bool perf = i == 3;
            var list = new List<RegVal>
            {
                Reg.Dw(Reg.HKCU, VE, "VisualFXSetting", i == 0 ? 0 : i == 1 ? 1 : i == 2 ? 3 : 2),
                Reg.Bin(Reg.HKCU, Desk, "UserPreferencesMask", fancy ? MaskDefault : MaskPerf),
                Reg.Sz(Reg.HKCU, WM, "MinAnimate", fancy ? "1" : "0"),
                Reg.Dw(Reg.HKCU, Adv, "TaskbarAnimations", fancy ? 1 : 0),
                Reg.Dw(Reg.HKCU, Dwm, "EnableAeroPeek", fancy ? 1 : 0),
                Reg.Sz(Reg.HKCU, Desk, "DragFullWindows", perf ? "0" : "1"),
                Reg.Sz(Reg.HKCU, Desk, "FontSmoothing", "2"),
                Reg.Dw(Reg.HKCU, Adv, "IconsOnly", perf ? 1 : 0),
                Reg.Dw(Reg.HKCU, Adv, "ListviewAlphaSelect", perf ? 0 : 1),
                Reg.Dw(Reg.HKCU, Adv, "ListviewShadow", perf ? 0 : 1),
            };
            foreach (var v in list) Reg.Apply(v);
        }
    }

    // Классическое контекстное меню (Windows 11): пустой InprocServer32 у CLSID нового меню
    public class ClassicMenu : ToggleTweak
    {
        const string Clsid = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
        const string Inproc = Clsid + @"\InprocServer32";

        public ClassicMenu()
        {
            Title = "Контекстное меню как в Windows 10";
            Description = "Сразу открывать полное контекстное меню, без пункта «Показать дополнительные параметры».";
            Icon = "";
            IconColor = "#F5A524";
            Win11Only = true;
            Restart = Restart.Explorer;
        }

        public override bool ReadEnabled() { return Reg.KeyExists(Reg.HKCU, Inproc); }

        public override void SetEnabled(bool on)
        {
            if (on)
            {
                Backup.SaveKeyAbsent(Reg.HKCU, Clsid);
                Reg.CreateKey(Reg.HKCU, Inproc, "");
            }
            else
            {
                Backup.SaveKey(Reg.HKCU, Inproc);
                Reg.DeleteKey(Reg.HKCU, Clsid);
            }
        }
    }

    // Всегда показывать все значки в трее.
    // Windows 10: EnableAutoTray. Windows 11: IsPromoted у каждого значка в NotifyIconSettings.
    public class TrayIcons : ToggleTweak
    {
        const string Exp = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
        const string Nis = @"Control Panel\NotifyIconSettings";

        public TrayIcons()
        {
            Title = "Всегда отображать все иконки на панели задач";
            Description = "Значки программ в трее (возле часов) не прячутся под стрелку «^». В Windows 11 настройка ставится для уже известных значков — если появится новая программа, переключите ещё раз.";
            Icon = "";
            IconColor = "#F5A524";
            Restart = Restart.Explorer;
        }

        static string[] Icons()
        {
            using (var b = Reg.Base(Reg.HKCU))
            using (var k = b.OpenSubKey(Nis))
                return k == null ? new string[0] : k.GetSubKeyNames();
        }

        // Состояние — по EnableAutoTray (так же читают другие твикеры); IsPromoted ставится дополнительно
        public override bool ReadEnabled()
        {
            int? auto = Reg.GetInt(Reg.HKCU, Exp, "EnableAutoTray");
            return auto.HasValue && auto.Value == 0;
        }

        public override void SetEnabled(bool on)
        {
            Reg.Apply(on ? Reg.Dw(Reg.HKCU, Exp, "EnableAutoTray", 0) : Reg.Del(Reg.HKCU, Exp, "EnableAutoTray"));
            if (Sys.IsWin11)
                foreach (var id in Icons())
                    Reg.Apply(Reg.Dw(Reg.HKCU, Nis + "\\" + id, "IsPromoted", on ? 1 : 0));
        }
    }
}
