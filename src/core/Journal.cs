using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BtwTweaker
{
    public class JournalEntry
    {
        public string Id;
        public DateTime Time;
        public string Title;           // «Game Bar → Отключено»
        public string Where;           // раздел: «Базовые твики», «Автозагрузки»…
        public List<string> Ids = new List<string>();
        public List<string> Lines = new List<string>();   // состояние ПЕРЕД действием (строки как в резервной копии)
        public bool RolledBack;
        public bool Irreversible;      // удаление приложений и т.п. — откатить нельзя
        public string Note;
        public string Tweak;           // Id твика, если действие — изменение твика (для слежения за сбросом)
    }

    // Журнал изменений: каждое действие и состояние перед ним — чтобы откатывать по одному.
    // Файл: %LOCALAPPDATA%\BtwTweaker\journal.json
    public static class Journal
    {
        static readonly string FilePath = Path.Combine(Backup.Dir, "journal.json");
        const int Max = 300;
        static readonly object sync = new object();
        static readonly object runLock = new object();   // журналируемые действия идут по одному
        static JournalEntry current;

        public static bool Active { get { lock (sync) return current != null; } }

        // Выполнить действие с записью в журнал (вызывать из фонового потока)
        public static void Do(string where, string title, Action a) { Do(where, title, a, null); }

        // tweak — изменение твика: после успеха его состояние запоминается для проверки сброса
        public static void Do(string where, string title, Action a, Tweak tweak)
        {
            lock (runLock)
            {
                lock (sync) current = new JournalEntry { Id = Guid.NewGuid().ToString("N"), Time = DateTime.Now, Title = title, Where = where, Tweak = tweak != null ? tweak.Id : null };
                Exception err = null;
                try { a(); }
                catch (Exception ex) { err = ex; }
                JournalEntry e;
                lock (sync) { e = current; current = null; }
                if (e.Lines.Count > 0)
                {
                    if (err != null) e.Note = L.T("Выполнено с ошибкой: ") + err.Message;
                    Add(e);
                }
                if (tweak != null) Watch.Remember(tweak, where);
                if (err != null) throw err;
            }
        }

        // Строка-снимок от резервной копии (только первая для каждого значения в пределах действия)
        public static void Capture(string id, string line)
        {
            lock (sync)
            {
                if (current == null || current.Ids.Contains(id)) return;
                current.Ids.Add(id);
                current.Lines.Add(line);
            }
        }

        // Запись без отката (удаление приложений, очистка и т.п.)
        public static void Info(string where, string title, string note)
        {
            Add(new JournalEntry { Id = Guid.NewGuid().ToString("N"), Time = DateTime.Now, Title = title, Where = where, Irreversible = true, Note = note });
        }

        static void Add(JournalEntry e)
        {
            lock (sync)
            {
                var all = LoadLocked();
                all.Insert(0, e);
                if (all.Count > Max) all.RemoveRange(Max, all.Count - Max);
                SaveLocked(all);
            }
        }

        public static List<JournalEntry> All() { lock (sync) return LoadLocked(); }

        public static int Count { get { lock (sync) return LoadLocked().Count; } }

        static List<JournalEntry> LoadLocked()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<JournalEntry>();
                var list = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<List<JournalEntry>>(File.ReadAllText(FilePath, Encoding.UTF8));
                return list ?? new List<JournalEntry>();
            }
            catch { return new List<JournalEntry>(); }
        }

        static void SaveLocked(List<JournalEntry> all)
        {
            Directory.CreateDirectory(Backup.Dir);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(all), Encoding.UTF8);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }

        // Откатить одно действие: вернуть состояние, которое было перед ним
        public static int Rollback(string id, List<string> errors)
        {
            lock (runLock)
            {
                JournalEntry e;
                lock (sync) e = LoadLocked().Find(x => x.Id == id);
                if (e == null || e.Irreversible || e.RolledBack) return 0;
                int n = 0;
                for (int i = e.Lines.Count - 1; i >= 0; i--)
                {
                    try { Backup.RestoreLine(e.Lines[i]); n++; }
                    catch (Exception ex) { errors.Add(Backup.Describe(e.Lines[i]) + ": " + ex.Message); }
                }
                lock (sync)
                {
                    var all = LoadLocked();
                    var x = all.Find(y => y.Id == id);
                    if (x != null) { x.RolledBack = true; if (errors.Count > 0) x.Note = L.T("Откачено с ошибками: ") + errors.Count; }
                    SaveLocked(all);
                }
                if (e.Tweak != null) Watch.Forget(e.Tweak);   // откатили — значит, такое состояние больше не нужно
                return n;
            }
        }

        // После «Откатить всё» — все записи считаются откаченными
        public static void AllRolledBack(int values)
        {
            lock (sync)
            {
                var all = LoadLocked();
                foreach (var e in all) if (!e.Irreversible) e.RolledBack = true;
                all.Insert(0, new JournalEntry { Id = Guid.NewGuid().ToString("N"), Time = DateTime.Now, Title = L.T("Откатить всё"), Where = L.T("Резервная копия"), Irreversible = true, Note = L.T("Восстановлено значений: ") + values });
                SaveLocked(all);
            }
        }

        public static void Clear()
        {
            lock (sync) SaveLocked(new List<JournalEntry>());
        }
    }
}
