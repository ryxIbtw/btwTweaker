using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace BtwTweaker
{
    public class ScanResult
    {
        public long Bytes;
        public int Count = -1;                         // -1 = показываем размер, иначе «Найдено: N»
        public List<string> Items = new List<string>();  // что будет удалено (для подтверждения)
    }

    public class CleanResult
    {
        public long Freed;
        public int Removed;
        public int Failed;    // занятые / недоступные файлы
    }

    // Один пункт очистки
    public abstract class Cleaner
    {
        public string Title;
        public string Description;
        public string Icon = "";
        public string IconColor;
        public string Caution;
        public string[] Processes;     // программы, которые надо закрыть перед очисткой
        public string ProcessTitle;    // как их назвать пользователю
        public bool Confirm;           // перед очисткой показать список и спросить

        public abstract ScanResult Scan();
        public abstract CleanResult Clean();

        public string RunningProcess()
        {
            if (Processes == null) return null;
            foreach (var p in Processes)
                if (Process.GetProcessesByName(p).Length > 0) return ProcessTitle ?? p;
            return null;
        }
    }

    public class CleanGroup
    {
        public string Title;
        public bool CleanAll = true;   // кнопка «Очистить» у группы
        public List<Cleaner> Items = new List<Cleaner>();
        public CleanGroup(string title, params Cleaner[] items) { Title = title; Items.AddRange(items); }
    }

    // Очистка содержимого папок (сами папки остаются) и отдельных файлов
    public class PathCleaner : Cleaner
    {
        public Func<List<string>> Dirs;
        public Func<List<string>> Files;
        public Action Before;
        public Action After;
        public bool RemoveRoots;   // удалять и сами папки из Dirs (например, FOUND.000)

        List<string> D() { return Dirs == null ? new List<string>() : Dirs(); }
        List<string> F() { return Files == null ? new List<string>() : Files(); }

        public override ScanResult Scan()
        {
            var r = new ScanResult();
            foreach (var d in D()) if (Directory.Exists(d)) r.Bytes += Fs.Size(d);
            foreach (var f in F()) try { r.Bytes += new FileInfo(f).Length; } catch { }
            return r;
        }

        public override CleanResult Clean()
        {
            var r = new CleanResult();
            if (Before != null) Before();
            try
            {
                foreach (var d in D())
                {
                    if (!Directory.Exists(d)) continue;
                    if (RemoveRoots) Fs.RemoveDir(d, r); else Fs.ClearDir(d, r);
                }
                foreach (var f in F()) Fs.DeleteFile(f, r);
            }
            finally { if (After != null) After(); }
            return r;
        }
    }

    public static class Fs
    {
        public static string E(string s) { return Environment.ExpandEnvironmentVariables(s); }

        static bool IsLink(FileSystemInfo f) { return (f.Attributes & FileAttributes.ReparsePoint) != 0; }

        // Размер папки; по ссылкам (junction / symlink) не ходим
        public static long Size(string dir)
        {
            long s = 0;
            try
            {
                foreach (var f in new DirectoryInfo(dir).EnumerateFileSystemInfos())
                {
                    if (IsLink(f)) continue;
                    var di = f as DirectoryInfo;
                    if (di != null) s += Size(di.FullName);
                    else s += ((FileInfo)f).Length;
                }
            }
            catch { }
            return s;
        }

        // Удалить всё внутри папки. Ссылку на папку удаляем как ссылку, внутрь не заходим.
        public static void ClearDir(string dir, CleanResult r)
        {
            FileSystemInfo[] items;
            try { items = new DirectoryInfo(dir).GetFileSystemInfos(); }
            catch { r.Failed++; return; }
            foreach (var f in items)
            {
                var di = f as DirectoryInfo;
                if (di == null) { DeleteFile(f.FullName, r); continue; }
                if (IsLink(di))
                {
                    try { Directory.Delete(di.FullName, false); } catch { r.Failed++; }
                    continue;
                }
                ClearDir(di.FullName, r);
                try { di.Attributes = FileAttributes.Directory; Directory.Delete(di.FullName, false); } catch { }
            }
        }

        public static void RemoveDir(string dir, CleanResult r)
        {
            if (!Directory.Exists(dir)) return;
            ClearDir(dir, r);
            try { Directory.Delete(dir, false); } catch { r.Failed++; }
        }

        public static void DeleteFile(string path, CleanResult r)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return;
                long len = fi.Length;
                if ((fi.Attributes & FileAttributes.ReadOnly) != 0) fi.Attributes = FileAttributes.Normal;
                fi.Delete();
                r.Freed += len;
                r.Removed++;
            }
            catch { r.Failed++; }
        }

        // Файлы по маске в одной папке (без подпапок)
        public static List<string> Glob(string dir, string pattern)
        {
            var l = new List<string>();
            try { if (Directory.Exists(dir)) l.AddRange(Directory.GetFiles(dir, pattern)); } catch { }
            return l;
        }

        // Подпапки по маске
        public static List<string> SubDirs(string dir, string pattern)
        {
            var l = new List<string>();
            try { if (Directory.Exists(dir)) l.AddRange(Directory.GetDirectories(dir, pattern)); } catch { }
            return l;
        }

        static readonly CultureInfo Ru = new CultureInfo("ru-RU");

        public static string Fmt(long b)
        {
            if (b < 1024) return b + L.T(" Б");
            if (b < 1024L * 1024) return (b / 1024.0).ToString("0.00", Ru) + L.T(" КБ");
            if (b < 1024L * 1024 * 1024) return (b / 1048576.0).ToString("0.00", Ru) + L.T(" МБ");
            return (b / 1073741824.0).ToString("0.00", Ru) + L.T(" ГБ");
        }
    }
}
