namespace BtwTweaker
{
    public static class Guard
    {
        // Защита от подделки Windows Defender: младший бит = включена (обычно 5 — вкл, 4 — выкл)
        public static bool TamperOn()
        {
            int? v = Reg.GetInt(Reg.HKLM, @"SOFTWARE\Microsoft\Windows Defender\Features", "TamperProtection");
            return v.HasValue && (v.Value & 1) == 1;
        }

        public const string TamperMsg =
            "Включена «Защита от подделки» — Microsoft специально не даёт программам менять настройки антивируса.\n\n" +
            "Выключите её вручную: «Безопасность Windows» → «Защита от вирусов и угроз» → «Управление настройками» → «Защита от подделки» → Выкл. Потом вернитесь сюда.\n\n" +
            "btwTweaker не меняет эту настройку сам — это сделано в целях безопасности.";
    }

    // Windows Defender: отключение работает только при выключенной защите от подделки
    public class DefenderTweak : RegToggle
    {
        const string Pol = @"SOFTWARE\Policies\Microsoft\Windows Defender";
        const string RT = @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection";

        public DefenderTweak()
        {
            Title = "Windows Defender (Антивирус)";
            Description = "Встроенный антивирус Windows. Отключать стоит только если установлен другой антивирус — иначе компьютер остаётся без защиты от вирусов.";
            Icon = "";
            IconColor = "#E5484D";
            Security = "компьютер останется без антивирусной защиты.";
            Restart = Restart.Reboot;

            WhenOff(Reg.Dw(Reg.HKLM, Pol, "DisableAntiSpyware", 1),
                    Reg.Dw(Reg.HKLM, RT, "DisableRealtimeMonitoring", 1),
                    Reg.Dw(Reg.HKLM, RT, "DisableBehaviorMonitoring", 1),
                    Reg.Dw(Reg.HKLM, RT, "DisableOnAccessProtection", 1),
                    Reg.Dw(Reg.HKLM, RT, "DisableScanOnRealtimeEnable", 1));
            WhenOn(Reg.Del(Reg.HKLM, Pol, "DisableAntiSpyware"),
                   Reg.Del(Reg.HKLM, RT, "DisableRealtimeMonitoring"),
                   Reg.Del(Reg.HKLM, RT, "DisableBehaviorMonitoring"),
                   Reg.Del(Reg.HKLM, RT, "DisableOnAccessProtection"),
                   Reg.Del(Reg.HKLM, RT, "DisableScanOnRealtimeEnable"));
        }

        public override string Blocker(bool wantEnable)
        {
            return Guard.TamperOn() ? Guard.TamperMsg : null;
        }
    }
}
