using System;
using System.Collections.Generic;

namespace BtwTweaker
{
    // «Мои твики»: избранное по названию твика (HKCU\Software\BtwTweaker\Favorites, по строке на твик)
    public static class Favorites
    {
        static HashSet<string> set;

        static HashSet<string> Set
        {
            get
            {
                if (set == null)
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    var s = Sys.Pref("Favorites");
                    if (!string.IsNullOrEmpty(s)) foreach (var x in s.Split('\n')) if (x.Length > 0) set.Add(x);
                }
                return set;
            }
        }

        public static bool Is(Tweak t) { return t != null && Set.Contains(t.Id); }

        public static int Count { get { return Set.Count; } }

        public static bool Toggle(Tweak t)
        {
            bool on = !Set.Remove(t.Id);
            if (on) Set.Add(t.Id);
#if TESTHOOKS
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BTW_SNAP"))) return on;
#endif
            Sys.SetPref("Favorites", string.Join("\n", Set));
            return on;
        }

#if TESTHOOKS
        // Для снимков: BTW_SNAP_FAVS=название1|название2
        public static void TestSet(string list)
        {
            set = new HashSet<string>(list.Split('|'), StringComparer.Ordinal);
        }
#endif
    }
}
