using System;
using System.Collections.Generic;

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

        public class Info
        {
            public string Path, Name, Exe, Args;
            public bool Enabled;
        }

        // Все задания (включая скрытые), кроме тех, что начинаются с skipPrefix
        public static List<Info> EnumAll(string skipPrefix)
        {
            var res = new List<Info>();
            lock (sync) Walk(Service().GetFolder("\\"), skipPrefix, res);
            return res;
        }

        static void Walk(dynamic folder, string skip, List<Info> res)
        {
            string fp = (string)folder.Path;
            if (skip != null && (fp + "\\").StartsWith(skip, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                foreach (dynamic t in folder.GetTasks(1))   // 1 = TASK_ENUM_HIDDEN
                {
                    try
                    {
                        var i = new Info { Path = (string)t.Path, Name = (string)t.Name, Enabled = (bool)t.Enabled };
                        foreach (dynamic a in t.Definition.Actions)
                            if ((int)a.Type == 0) { i.Exe = (string)a.Path; i.Args = (string)a.Arguments; break; }
                        res.Add(i);
                    }
                    catch { }
                }
                foreach (dynamic f in folder.GetFolders(0)) Walk(f, skip, res);
            }
            catch { }
        }

        public static string Xml(string path)
        {
            dynamic t = Get(path);
            return t == null ? null : (string)t.Xml;
        }

        // Задание «при входе текущего пользователя» с задержкой (для отложенного запуска программ)
        public static void CreateLogonTask(string folderPath, string name, string exe, string args, int delaySeconds, string description)
        {
            lock (sync)
            {
                dynamic s = Service();
                dynamic folder = EnsureFolder(s, folderPath);
                dynamic def = s.NewTask(0);
                def.RegistrationInfo.Description = description;
                def.RegistrationInfo.Author = "btwTweaker";
                def.Settings.DisallowStartIfOnBatteries = false;
                def.Settings.StopIfGoingOnBatteries = false;
                def.Settings.ExecutionTimeLimit = "PT0S";
                def.Settings.MultipleInstances = 2;   // не запускать вторую копию
                string user = Environment.UserDomainName + "\\" + Environment.UserName;
                dynamic trig = def.Triggers.Create(9);   // TASK_TRIGGER_LOGON
                trig.UserId = user;
                trig.Delay = "PT" + delaySeconds + "S";
                dynamic act = def.Actions.Create(0);     // TASK_ACTION_EXEC
                act.Path = exe;
                if (!string.IsNullOrEmpty(args)) act.Arguments = args;
                act.WorkingDirectory = System.IO.Path.GetDirectoryName(exe);
                def.Principal.UserId = user;
                def.Principal.LogonType = 3;             // TASK_LOGON_INTERACTIVE_TOKEN
                def.Principal.RunLevel = 0;              // обычные права, как у автозагрузки
                folder.RegisterTaskDefinition(name, def, 6, user, null, 3);
            }
        }

        static dynamic EnsureFolder(dynamic s, string path)
        {
            dynamic cur = s.GetFolder("\\");
            foreach (var part in path.Trim('\\').Split('\\'))
            {
                dynamic next = null;
                try { next = cur.GetFolder(part); } catch { }
                cur = next ?? cur.CreateFolder(part);
            }
            return cur;
        }

        public static void Delete(string path)
        {
            int i = path.LastIndexOf('\\');
            string folder = i <= 0 ? "\\" : path.Substring(0, i);
            Service().GetFolder(folder).DeleteTask(path.Substring(i + 1), 0);
        }
    }
}
