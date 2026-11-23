using System;
using System.Runtime.InteropServices;

namespace BtwTweaker
{
    // HAGS — аппаратное планирование GPU.
    // Если значение HwSchMode в реестре есть — показываем его (оно применится после перезагрузки).
    // Если нет — спрашиваем видеодрайвер, включено ли планирование сейчас (Windows могла включить его по умолчанию).
    public class HagsTweak : ToggleTweak
    {
        const string GD = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        int caps = -1;   // кэш ответа драйвера: биты 0 — поддерживается, 1 — включено

        public HagsTweak()
        {
            Title = "HAGS";
            Description = "Аппаратное ускорение планирования GPU: видеокарта сама управляет своей памятью. Может снизить задержку ввода и нужно для генерации кадров DLSS 3. Требует поддержки видеокартой (NVIDIA GTX 10xx и новее, AMD RX 5000 и новее).";
            Icon = "";
            Restart = Restart.Reboot;
            MinBuild = 19041;
        }

        int Caps()
        {
            if (caps < 0)
            {
                try { caps = QueryCaps(); } catch { caps = 0; }
            }
            return caps;
        }

        public override bool IsSupported() { return (Caps() & 1) != 0; }

        public override bool ReadEnabled()
        {
            int? mode = Reg.GetInt(Reg.HKLM, GD, "HwSchMode");
            if (mode.HasValue) return mode.Value == 2;
            return (Caps() & 2) != 0;
        }

        public override void SetEnabled(bool on)
        {
            Reg.Apply(Reg.Dw(Reg.HKLM, GD, "HwSchMode", on ? 2 : 1));
        }

        // ── D3DKMT: опрос видеоадаптеров ──
        [StructLayout(LayoutKind.Sequential)]
        struct ENUMADAPTERS2 { public uint NumAdapters; public IntPtr pAdapters; }

        [StructLayout(LayoutKind.Sequential)]
        struct ADAPTERINFO { public uint hAdapter; public uint LuidLow; public int LuidHigh; public uint NumOfSources; public int bPrecise; }

        [StructLayout(LayoutKind.Sequential)]
        struct QUERYADAPTERINFO { public uint hAdapter; public int Type; public IntPtr pData; public uint DataSize; }

        [StructLayout(LayoutKind.Sequential)]
        struct CLOSEADAPTER { public uint hAdapter; }

        [DllImport("gdi32.dll")] static extern int D3DKMTEnumAdapters2(ref ENUMADAPTERS2 p);
        [DllImport("gdi32.dll")] static extern int D3DKMTQueryAdapterInfo(ref QUERYADAPTERINFO p);
        [DllImport("gdi32.dll")] static extern int D3DKMTCloseAdapter(ref CLOSEADAPTER p);

        const int KMTQAITYPE_WDDM_2_7_CAPS = 70;

        static int QueryCaps()
        {
            var e = new ENUMADAPTERS2();
            if (D3DKMTEnumAdapters2(ref e) != 0 || e.NumAdapters == 0) return 0;
            int size = Marshal.SizeOf(typeof(ADAPTERINFO));
            e.pAdapters = Marshal.AllocHGlobal(size * (int)e.NumAdapters);
            IntPtr buf = Marshal.AllocHGlobal(4);
            int result = 0;
            try
            {
                if (D3DKMTEnumAdapters2(ref e) != 0) return 0;
                for (int i = 0; i < e.NumAdapters; i++)
                {
                    var a = (ADAPTERINFO)Marshal.PtrToStructure(new IntPtr(e.pAdapters.ToInt64() + i * size), typeof(ADAPTERINFO));
                    Marshal.WriteInt32(buf, 0);
                    var q = new QUERYADAPTERINFO { hAdapter = a.hAdapter, Type = KMTQAITYPE_WDDM_2_7_CAPS, pData = buf, DataSize = 4 };
                    if (D3DKMTQueryAdapterInfo(ref q) == 0)
                    {
                        int v = Marshal.ReadInt32(buf);
                        if ((v & 1) != 0) result |= 1;   // HwSchSupported
                        if ((v & 2) != 0) result |= 2;   // HwSchEnabled
                    }
                    var c = new CLOSEADAPTER { hAdapter = a.hAdapter };
                    D3DKMTCloseAdapter(ref c);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(e.pAdapters);
                Marshal.FreeHGlobal(buf);
            }
            return result;
        }
    }
}
