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
            Description = L.T("Antimalware Scan Interface — интерфейс, через который антивирус проверяет скрипты (PowerShell, макросы) в момент запуска. Отключение снимает регистрацию провайдера Defender, и скрипты перестают проверяться.");
            Icon = "";
            IconColor = "#E5484D";
            Security = L.T("скрипты (PowerShell, макросы) перестанут проверяться антивирусом при запуске.");
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

        public override System.Collections.Generic.List<ChangeInfo> Changes()
        {
            var c = new ChangeInfo { Kind = L.T("Реестр"), Title = "HKLM\\" + Provider, Detail = L.T("регистрация провайдера Defender"), RegPath = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\AMSI\\Providers" };
            c.Add(L.T("Вкл"), L.T("ключ есть")).Add(L.T("Откл"), L.T("ключ удалён"));
            try { c.Current = ReadEnabled() ? 0 : 1; } catch { }
            return new System.Collections.Generic.List<ChangeInfo> { c };
        }
    }

    // Брандмауэр Windows через реестр: EnableFirewall для трёх профилей
    public class FirewallTweak : ToggleTweak
    {
        const string Base = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\";
        static readonly string[] Profiles = { "StandardProfile", "PublicProfile", "DomainProfile" };

        public FirewallTweak()
        {
            Title = L.T("Брандмауэр Windows");
            Description = L.T("Сетевой экран Windows: блокирует нежелательные входящие и исходящие подключения. Отключать стоит, только если его заменяет другой файрвол.");
            Icon = "";
            IconColor = "#E5484D";
            Security = L.T("компьютер перестанет блокировать нежелательные сетевые подключения.");
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

        public override System.Collections.Generic.List<ChangeInfo> Changes()
        {
            var l = new System.Collections.Generic.List<ChangeInfo>();
            foreach (var p in Profiles)
                l.Add(ChangeText.FromReg(Reg.Dw(Reg.HKLM, Base + p, "EnableFirewall", 1), Reg.Dw(Reg.HKLM, Base + p, "EnableFirewall", 0)));
            var cmd = new ChangeInfo { Kind = L.T("Команда"), Title = "netsh advfirewall set allprofiles state", Detail = L.T("применяет сразу, без перезагрузки") };
            cmd.Add(L.T("Вкл"), "on").Add(L.T("Откл"), "off");
            try { cmd.Current = ReadEnabled() ? 0 : 1; } catch { }
            l.Add(cmd);
            return l;
        }
    }
}
