using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace BtwTweaker
{
    public class PowerScheme
    {
        public Guid Id;
        public string Name;
        public bool Active;
    }

    // Схемы электропитания через powrprof.dll (без разбора вывода powercfg — нет проблем с кодировкой)
    public static class Power
    {
        public static readonly Guid Balanced = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        public static readonly Guid High = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        public static readonly Guid Saver = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
        public static readonly Guid Ultimate = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");

        // Шаблоны для «Добавить схему»
        public static readonly string[] TemplateNames = { "Максимальная производительность", "Высокая производительность", "Сбалансированная", "Экономия энергии" };
        public static readonly Guid[] Templates = { Ultimate, High, Balanced, Saver };

        const uint ACCESS_SCHEME = 16;
        const uint ERROR_NO_MORE_ITEMS = 259;
        const uint ERROR_MORE_DATA = 234;

        [DllImport("powrprof.dll")]
        static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr subgroup, uint flags, uint index, byte[] buffer, ref uint size);
        [DllImport("powrprof.dll")]
        static extern uint PowerReadFriendlyName(IntPtr root, ref Guid scheme, IntPtr subgroup, IntPtr setting, byte[] buffer, ref uint size);
        [DllImport("powrprof.dll")]
        static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr active);
        [DllImport("powrprof.dll")]
        static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
        [DllImport("powrprof.dll")]
        static extern uint PowerDeleteScheme(IntPtr root, ref Guid scheme);
        [DllImport("powrprof.dll")]
        static extern uint PowerDuplicateScheme(IntPtr root, ref Guid source, ref IntPtr dest);
        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr mem);

        static void Check(uint code)
        {
            if (code != 0) throw new Win32Exception((int)code);
        }

        public static Guid ActiveId()
        {
            IntPtr p;
            Check(PowerGetActiveScheme(IntPtr.Zero, out p));
            try { return (Guid)Marshal.PtrToStructure(p, typeof(Guid)); }
            finally { LocalFree(p); }
        }

        static string Name(Guid id)
        {
            uint size = 512;
            var buf = new byte[size];
            uint r = PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, buf, ref size);
            if (r == ERROR_MORE_DATA)
            {
                buf = new byte[size];
                r = PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, buf, ref size);
            }
            if (r != 0) return id.ToString();
            return Encoding.Unicode.GetString(buf, 0, (int)size).TrimEnd('\0');
        }

        public static List<PowerScheme> List()
        {
            var list = new List<PowerScheme>();
            Guid active = ActiveId();
            for (uint i = 0; ; i++)
            {
                var buf = new byte[16];
                uint size = 16;
                uint r = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ACCESS_SCHEME, i, buf, ref size);
                if (r == ERROR_NO_MORE_ITEMS) break;
                Check(r);
                var id = new Guid(buf);
                list.Add(new PowerScheme { Id = id, Name = Name(id), Active = id == active });
            }
            return list;
        }

        public static void SetActive(Guid id) { Check(PowerSetActiveScheme(IntPtr.Zero, ref id)); }

        public static void Delete(Guid id) { Check(PowerDeleteScheme(IntPtr.Zero, ref id)); }

        // Создать копию стандартной схемы (так добавляется и скрытая «Максимальная производительность»)
        public static Guid Duplicate(Guid template)
        {
            IntPtr dest = IntPtr.Zero;
            Check(PowerDuplicateScheme(IntPtr.Zero, ref template, ref dest));
            try { return (Guid)Marshal.PtrToStructure(dest, typeof(Guid)); }
            finally { LocalFree(dest); }
        }
    }
}
