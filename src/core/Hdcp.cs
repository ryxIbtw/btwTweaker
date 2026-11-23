using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace BtwTweaker
{
    // HDCP — настройка драйвера NVIDIA RMHdcpKeyglobZero в разделе видеоадаптера.
    // Для других видеокарт такой настройки нет — пункт показывается как «Не поддерживается».
    public class HdcpTweak : ToggleTweak
    {
        const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        const string Name = "RMHdcpKeyglobZero";

        public HdcpTweak()
        {
            Title = "HDCP";
            Description = L.T("Защита видеосигнала от копирования (для видеокарт NVIDIA). Иногда мешает захвату и записи экрана. После отключения часть защищённого видео (например, стриминг в 4K) может не воспроизводиться.");
            Icon = "";
            Restart = Restart.Reboot;
        }

        // Разделы адаптеров NVIDIA: 0000, 0001, …
        static List<string> NvidiaKeys()
        {
            var list = new List<string>();
            using (var b = Reg.Base(Reg.HKLM))
            using (var cls = b.OpenSubKey(DisplayClass))
            {
                if (cls == null) return list;
                foreach (var sub in cls.GetSubKeyNames())
                {
                    if (sub.Length != 4) continue;   // пропускаем «Properties» и т.п.
                    try
                    {
                        using (var k = cls.OpenSubKey(sub))
                        {
                            if (k == null) continue;
                            string prov = Convert.ToString(k.GetValue("ProviderName")) + " " + Convert.ToString(k.GetValue("DriverDesc"));
                            if (prov.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                                list.Add(DisplayClass + "\\" + sub);
                        }
                    }
                    catch { }
                }
            }
            return list;
        }

        public override bool IsSupported() { return NvidiaKeys().Count > 0; }

        public override bool ReadEnabled()
        {
            foreach (var k in NvidiaKeys())
            {
                int? v = Reg.GetInt(Reg.HKLM, k, Name);
                if (!v.HasValue || v.Value != 1) return true;
            }
            return false;
        }

        public override void SetEnabled(bool on)
        {
            foreach (var k in NvidiaKeys())
                Reg.Apply(on ? Reg.Del(Reg.HKLM, k, Name) : Reg.Dw(Reg.HKLM, k, Name, 1));
        }
    }
}
