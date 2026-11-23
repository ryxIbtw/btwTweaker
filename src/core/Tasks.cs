using System;

namespace BtwTweaker
{
    // Задания планировщика Windows через COM «Schedule.Service»
    public static class Tasks
    {
        static readonly object sync = new object();
        static dynamic svc;

        static dynamic Service()
        {
            lock (sync)
            {
                if (svc == null)
                {
                    dynamic s = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                    s.Connect();
                    svc = s;
                }
                return svc;
            }
        }

        // path: \Microsoft\Windows\Папка\Имя
        static dynamic Get(string path)
        {
            int i = path.LastIndexOf('\\');
            string folder = i <= 0 ? "\\" : path.Substring(0, i);
            string name = path.Substring(i + 1);
            try { return Service().GetFolder(folder).GetTask(name); }
            catch { return null; }
        }

        public static bool Exists(string path) { return Get(path) != null; }

        // null — задания нет
        public static bool? IsEnabled(string path)
        {
            dynamic t = Get(path);
            if (t == null) return null;
            return (bool)t.Enabled;
        }

        public static void SetEnabled(string path, bool on)
        {
            dynamic t = Get(path);
            if (t == null) return;
            t.Enabled = on;
        }
    }
}
