using System;
using System.Globalization;

namespace BtwTweaker
{
    // Обновление Windows: Включено / Пауза / Отключено
    public class WinUpdate : ChoiceTweak
    {
        const string UX = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        const string AU = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
        const int PauseDays = 3650;   // «пауза до отмены» — 10 лет

        static readonly string[] PauseNames =
        {
            "PauseUpdatesStartTime", "PauseUpdatesExpiryTime",
            "PauseFeatureUpdatesStartTime", "PauseFeatureUpdatesEndTime",
            "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime"
        };

        public WinUpdate()
        {
            Title = L.T("Обновление Windows");
            Icon = "";
            IconColor = "#F5A524";
            Options = new[] { L.T("Включено"), L.T("Пауза"), L.T("Отключено") };
            Description = L.T("Включено — как в Windows по умолчанию.\n") +
                          L.T("Пауза — обновления приостановлены до отмены (в «Параметрах» будет видна дата). Windows продолжает работать как обычно, снять паузу можно здесь или кнопкой «Возобновить» в Параметрах.\n") +
                          L.T("Отключено — автообновление выключено политикой, служба Windows Update отключена. Магазин и обновление баз Defender тоже перестанут работать.");
        }

        public override int ReadIndex()
        {
            int? no = Reg.GetInt(Reg.HKLM, AU, "NoAutoUpdate");
            int? start = Reg.GetInt(Reg.HKLM, Reg.Services + "wuauserv", "Start");
            if ((no.HasValue && no.Value == 1) || (start.HasValue && start.Value == 4)) return 2;

            var exp = Reg.Get(Reg.HKLM, UX, "PauseUpdatesExpiryTime") as string;
            DateTime d;
            if (exp != null && DateTime.TryParse(exp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out d) && d > DateTime.UtcNow)
                return 1;
            return 0;
        }

        public override System.Collections.Generic.List<ChangeInfo> Changes()
        {
            int cur = -1;
            try { cur = ReadIndex(); } catch { }
            var a = new ChangeInfo { Kind = L.T("Политика"), Title = "HKLM\\" + AU, Detail = "NoAutoUpdate", RegPath = "HKEY_LOCAL_MACHINE\\" + AU, Current = cur };
            a.Add(L.T("Включено"), L.T("нет значения")).Add(L.T("Пауза"), L.T("нет значения")).Add(L.T("Отключено"), "1");
            var s = new ChangeInfo { Kind = L.T("Служба"), Title = "wuauserv", Detail = L.T("Тип запуска"), RegPath = "HKEY_LOCAL_MACHINE\\" + Reg.Services + "wuauserv", Current = cur };
            s.Add(L.T("Включено"), L.T("Вручную")).Add(L.T("Пауза"), L.T("Вручную")).Add(L.T("Отключено"), L.T("Отключена"));
            var p = new ChangeInfo { Kind = L.T("Реестр"), Title = "HKLM\\" + UX, Detail = L.T("PauseUpdates… (6 дат) и FlightSettingsMaxPauseDays"), RegPath = "HKEY_LOCAL_MACHINE\\" + UX, Current = cur };
            p.Add(L.T("Включено"), L.T("нет значений")).Add(L.T("Пауза"), L.T("сегодня → +") + PauseDays + L.T(" дней")).Add(L.T("Отключено"), L.T("нет значений"));
            return new System.Collections.Generic.List<ChangeInfo> { a, s, p };
        }

        public override string Confirm(int i)
        {
            if (i == 2)
                return L.T("Без обновлений не приходят исправления уязвимостей. Также перестанут обновляться приложения Магазина и базы Windows Defender.\n\nWindows может сама включить службу обратно через какое-то время.");
            return null;
        }

        public override void SetIndex(int i)
        {
            // Снять паузу и запрет — общее для «Включено» и «Пауза»
            foreach (var n in PauseNames) Reg.Apply(Reg.Del(Reg.HKLM, UX, n));
            Reg.Apply(Reg.Del(Reg.HKLM, UX, "FlightSettingsMaxPauseDays"));

            if (i == 2)
            {
                Reg.Apply(Reg.Dw(Reg.HKLM, AU, "NoAutoUpdate", 1));
                Reg.Apply(Reg.Start("wuauserv", 4));
                Sys.StopService("wuauserv");
                return;
            }

            Reg.Apply(Reg.Del(Reg.HKLM, AU, "NoAutoUpdate"));
            Reg.Apply(Reg.Start("wuauserv", 3));

            if (i == 1)
            {
                var now = DateTime.UtcNow;
                string s = now.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                string e = now.AddDays(PauseDays).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                Reg.Apply(Reg.Dw(Reg.HKLM, UX, "FlightSettingsMaxPauseDays", PauseDays));
                for (int k = 0; k < PauseNames.Length; k++)
                    Reg.Apply(Reg.Sz(Reg.HKLM, UX, PauseNames[k], k % 2 == 0 ? s : e));
            }
        }
    }
}
