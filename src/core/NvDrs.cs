using System;
using System.Runtime.InteropServices;

namespace BtwTweaker
{
    // Значение настройки драйвера в глобальном профиле
    public struct NvValue
    {
        public bool Found;        // задано в глобальном профиле (иначе — внутреннее значение драйвера)
        public uint Value;
        public bool Predefined;   // совпадает со значением, которое поставляет NVIDIA
    }

    // Настройки драйвера NVIDIA (DRS) через nvapi64.dll — так же работает NVIDIA Profile Inspector.
    // Работаем только с глобальным профилем. Каждый вызов открывает и закрывает свою сессию.
    public static class NvDrs
    {
        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr QueryInterface(uint id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D0();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DOut(out IntPtr h);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D1(IntPtr h);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int D1Out(IntPtr s, out IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DGet(IntPtr s, IntPtr p, uint id, IntPtr setting);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DSet(IntPtr s, IntPtr p, IntPtr setting);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DId(IntPtr s, IntPtr p, uint id);

        const int NVAPI_OK = 0;
        const int NVAPI_SETTING_NOT_FOUND = -160;

        // Размер NVDRS_SETTING_V1 и смещения полей
        const int Size = 12320;
        const int OffId = 4100, OffType = 4104, OffCurPredef = 4112, OffCurrent = 8220;
        static readonly uint Version = (uint)Size | (1u << 16);

        static readonly object sync = new object();
        static bool inited;
        static D0 initialize;
        static DOut createSession;
        static D1 destroySession, loadSettings, saveSettings;
        static D1Out getGlobal;
        static DGet getSetting;
        static DSet setSetting;
        static DId deleteSetting, restoreDefault;

        static T Fn<T>(uint id) where T : class
        {
            var p = QueryInterface(id);
            if (p == IntPtr.Zero) throw new Exception(L.T("Драйвер NVIDIA не поддерживает функцию 0x") + id.ToString("X8") + ".");
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        static void Init()
        {
            if (inited) return;
            initialize = Fn<D0>(0x0150E828);
            createSession = Fn<DOut>(0x0694D52E);
            destroySession = Fn<D1>(0xDAD9CFF8);
            loadSettings = Fn<D1>(0x375DBD6B);
            saveSettings = Fn<D1>(0xFCBC7E14);
            getGlobal = Fn<D1Out>(0x617BFF9F);
            getSetting = Fn<DGet>(0x73BF8338);
            setSetting = Fn<DSet>(0x577DD202);
            deleteSetting = Fn<DId>(0xE4A26362);
            restoreDefault = Fn<DId>(0x53F0381E);
            Check(initialize(), L.T("инициализация"));
            inited = true;
        }

        static void Check(int r, string what)
        {
            if (r != NVAPI_OK) throw new Exception(L.T("Драйвер NVIDIA: ошибка ") + r + " (" + what + ").");
        }

        // Есть ли драйвер NVIDIA с поддержкой настроек
        public static bool Available(out string error)
        {
            error = null;
            try { lock (sync) Init(); return true; }
            catch (DllNotFoundException) { error = L.T("Драйвер NVIDIA не найден."); return false; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        // Выполнить действие в сессии с глобальным профилем; save — сохранить изменения
        static void Session(Action<IntPtr, IntPtr> body, bool save)
        {
            lock (sync)
            {
                Init();
                IntPtr s;
                Check(createSession(out s), L.T("сессия"));
                try
                {
                    Check(loadSettings(s), L.T("загрузка настроек"));
                    IntPtr prof;
                    Check(getGlobal(s, out prof), L.T("глобальный профиль"));
                    body(s, prof);
                    if (save) Check(saveSettings(s), L.T("сохранение"));
                }
                finally { destroySession(s); }
            }
        }

        static NvValue ReadOne(IntPtr s, IntPtr prof, uint id, IntPtr buf)
        {
            for (int i = 0; i < Size; i += 4) Marshal.WriteInt32(buf, i, 0);
            Marshal.WriteInt32(buf, 0, (int)Version);
            int r = getSetting(s, prof, id, buf);
            if (r == NVAPI_SETTING_NOT_FOUND) return new NvValue { Found = false };
            Check(r, L.T("чтение 0x") + id.ToString("X8"));
            return new NvValue
            {
                Found = true,
                Value = (uint)Marshal.ReadInt32(buf, OffCurrent),
                Predefined = Marshal.ReadInt32(buf, OffCurPredef) != 0,
            };
        }

        public static System.Collections.Generic.Dictionary<uint, NvValue> Read(System.Collections.Generic.IEnumerable<uint> ids)
        {
            var res = new System.Collections.Generic.Dictionary<uint, NvValue>();
            IntPtr buf = Marshal.AllocHGlobal(Size);
            try { Session((s, p) => { foreach (var id in ids) if (!res.ContainsKey(id)) res[id] = ReadOne(s, p, id, buf); }, false); }
            finally { Marshal.FreeHGlobal(buf); }
            return res;
        }

        // Записать значения (DWORD); перед этим прежние значения сохраняются в резервную копию
        public static void Write(System.Collections.Generic.IDictionary<uint, uint> values)
        {
            // Шаг 1: прежние значения — в резервную копию (вне блокировки драйвера, чтобы не пересечься с откатом)
            var olds = Read(values.Keys);
            foreach (var kv in olds) Backup.SaveNv(kv.Key, kv.Value);

            // Шаг 2: запись
            IntPtr buf = Marshal.AllocHGlobal(Size);
            try
            {
                Session((s, p) =>
                {
                    foreach (var kv in values)
                    {
                        for (int i = 0; i < Size; i += 4) Marshal.WriteInt32(buf, i, 0);
                        Marshal.WriteInt32(buf, 0, (int)Version);
                        Marshal.WriteInt32(buf, OffId, (int)kv.Key);
                        Marshal.WriteInt32(buf, OffType, 0);              // NVDRS_DWORD_TYPE
                        Marshal.WriteInt32(buf, OffCurrent, (int)kv.Value);
                        Check(setSetting(s, p, buf), L.T("запись 0x") + kv.Key.ToString("X8"));
                    }
                }, true);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DEnum(IntPtr s, IntPtr p, uint start, ref uint count, IntPtr settings);

        // Все настройки, заданные в глобальном профиле: (id, значение, совпадает с NVIDIA, место). Только чтение.
        public static System.Collections.Generic.List<string> EnumAll()
        {
            var res = new System.Collections.Generic.List<string>();
            const int Max = 512;
            IntPtr arr = Marshal.AllocHGlobal(Size * Max);
            try
            {
                Session((s, p) =>
                {
                    var en = Fn<DEnum>(0xAE3039DA);
                    for (int i = 0; i < Size * Max; i += 4) Marshal.WriteInt32(arr, i, 0);
                    for (int k = 0; k < Max; k++) Marshal.WriteInt32(arr, k * Size, (int)Version);
                    uint count = Max;
                    Check(en(s, p, 0, ref count, arr), L.T("перечисление"));
                    for (int k = 0; k < count; k++)
                    {
                        IntPtr b = new IntPtr(arr.ToInt64() + (long)k * Size);
                        string name = Marshal.PtrToStringUni(new IntPtr(b.ToInt64() + 4));
                        res.Add(((uint)Marshal.ReadInt32(b, OffId)).ToString("X8") + "  value=0x" + ((uint)Marshal.ReadInt32(b, OffCurrent)).ToString("X8") +
                                "  predefined=" + (Marshal.ReadInt32(b, OffCurPredef) != 0) + "  location=" + Marshal.ReadInt32(b, 4108) + "  type=" + Marshal.ReadInt32(b, OffType) + "  " + name);
                    }
                }, false);
            }
            finally { Marshal.FreeHGlobal(arr); }
            return res;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DName(uint id, IntPtr name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DAvail(IntPtr ids, ref uint count);

        // Все номера настроек, которые знает драйвер (только для проверки)
        public static uint[] AvailableIds()
        {
            lock (sync)
            {
                Init();
                var fn = Fn<DAvail>(0xF020614A);
                const int Max = 4096;
                IntPtr buf = Marshal.AllocHGlobal(Max * 4);
                try
                {
                    uint count = Max;
                    Check(fn(buf, ref count), L.T("список настроек"));
                    var res = new uint[count];
                    for (int i = 0; i < count; i++) res[i] = (uint)Marshal.ReadInt32(buf, i * 4);
                    return res;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }

        // Официальное название настройки по номеру (проверка, что номер настоящий). null — драйвер такой не знает.
        public static string NameOf(uint id)
        {
            lock (sync)
            {
                Init();
                var fn = Fn<DName>(0xD61CBE6E);
                IntPtr buf = Marshal.AllocHGlobal(4096);
                try
                {
                    for (int i = 0; i < 4096; i += 4) Marshal.WriteInt32(buf, i, 0);
                    return fn(id, buf) == NVAPI_OK ? Marshal.PtrToStringUni(buf) : null;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }

        // Вернуть одну настройку как было (для «Откатить всё»)
        public static void Restore(uint id, NvValue old)
        {
            IntPtr buf = Marshal.AllocHGlobal(Size);
            try
            {
                Session((s, p) =>
                {
                    if (!old.Found) { deleteSetting(s, p, id); return; }
                    if (old.Predefined && restoreDefault(s, p, id) == NVAPI_OK) return;
                    for (int i = 0; i < Size; i += 4) Marshal.WriteInt32(buf, i, 0);
                    Marshal.WriteInt32(buf, 0, (int)Version);
                    Marshal.WriteInt32(buf, OffId, (int)id);
                    Marshal.WriteInt32(buf, OffType, 0);
                    Marshal.WriteInt32(buf, OffCurrent, (int)old.Value);
                    Check(setSetting(s, p, buf), L.T("восстановление 0x") + id.ToString("X8"));
                }, true);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
    }
}
