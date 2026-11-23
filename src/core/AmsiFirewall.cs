using System;

namespace BtwTweaker
{
    // AMSI отключается снятием регистрации провайдера-антивируса Windows Defender.
    // Ключ воссоздаётся при включении и при «Откатить всё».
    public class AmsiTweak : ToggleTweak
    {
        const string Provider = @"SOFTWARE\Microsoft\AMSI\Providers\{2781761E-28E0-4109-99FE-B9D127C57AFE}";

        public AmsiTweak()
        {
            Title = "AMSI";
            Description = "Antimalware Scan Interface — интерфейс, через который антивирус проверяет скрипты (PowerShell, макросы) в момент запуска. Отключение снимает регистрацию провайдера Defender, и скрипты перестают проверяться.";
            Icon = "";
            IconColor = "#E5484D";
            Security = "скрипты (PowerShell, макросы) перестанут проверяться антивирусом при запуске.";
            Restart = Restart.Reboot;
        }

        public override bool ReadEnabled() { return Reg.KeyExists(Reg.HKLM, Provider); }

        public override void SetEnabled(bool on)
        {
            if (on)
            {
                // Ключ провайдера — пустой (регистрация COM-класса лежит отдельно и не удаляется).
                // Если ключа изначально не было — откат его снова удалит.
                Backup.SaveKeyAbsent(Reg.HKLM, Provider);
                Reg.CreateKey(Reg.HKLM, Provider, null);
            }
            else Reg.DeleteKey(Reg.HKLM, Provider);
        }

        public override string Blocker(bool wantEnable)
        {
            return Guard.TamperOn() ? Guard.TamperMsg : null;
        }
    }

    // Брандмауэр Windows через реестр: EnableFirewall для трёх профилей
    public class FirewallTweak : ToggleTweak
    {
        const string Base = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\";
        static readonly string[] Profiles = { "StandardProfile", "PublicProfile", "DomainProfile" };

        public FirewallTweak()
        {
            Title = "Брандмауэр Windows";
            Description = "Сетевой экран Windows: блокирует нежелательные входящие и исходящие подключения. Отключать стоит, только если его заменяет другой файрвол.";
            Icon = "";
            IconColor = "#E5484D";
            Security = "компьютер перестанет блокировать нежелательные сетевые подключения.";
        }

        // Включён, если хотя бы в одном профиле EnableFirewall ещё не выключен
        public override bool ReadEnabled()
        {
            foreach (var p in Profiles)
            {
                int? v = Reg.GetInt(Reg.HKLM, Base + p, "EnableFirewall");
                if (!v.HasValue || v.Value != 0) return true;
            }
            return false;
        }

        public override void SetEnabled(bool on)
        {
            foreach (var p in Profiles)
                Reg.Apply(Reg.Dw(Reg.HKLM, Base + p, "EnableFirewall", on ? 1 : 0));
            // Реестр служба читает при запуске; netsh применяет сразу (и пишет те же значения)
            string o;
            Sys.Run("netsh.exe", "advfirewall set allprofiles state " + (on ? "on" : "off"), out o);
        }
    }
}
