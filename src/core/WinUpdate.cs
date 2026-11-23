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
            Title = "Обновление Windows";
            Icon = "";
            IconColor = "#F5A524";
            Options = new[] { "Включено", "Пауза", "Отключено" };
            Description = "Включено — как в Windows по умолчанию.\n" +
                          "Пауза — обновления приостановлены до отмены (в «Параметрах» будет видна дата). Windows продолжает работать как обычно, снять паузу можно здесь или кнопкой «Возобновить» в Параметрах.\n" +
                          "Отключено — автообновление выключено политикой, служба Windows Update отключена. Магазин и обновление баз Defender тоже перестанут работать.";
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

        public override string Confirm(int i)
        {
            if (i == 2)
                return "Без обновлений не приходят исправления уязвимостей. Также перестанут обновляться приложения Магазина и базы Windows Defender.\n\nWindows может сама включить службу обратно через какое-то время.";
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
