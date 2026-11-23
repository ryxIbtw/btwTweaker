using System;
using Microsoft.Win32;

namespace BtwTweaker
{
    public enum RK { DWord, String, Delete, Binary }

    // Одно значение реестра, которое твик хочет записать (или удалить — RK.Delete)
    public class RegVal
    {
        public RegistryHive Hive;
        public string Key;
        public string Name;
        public RK Kind;
        public object Data;
        // Трогать только если раздел уже есть (например, служба, которой может не быть в системе)
        public bool OnlyIfKeyExists;
    }

    public static class Reg
    {
        public const RegistryHive HKLM = RegistryHive.LocalMachine;
        public const RegistryHive HKCU = RegistryHive.CurrentUser;

        public static RegVal Dw(RegistryHive h, string key, string name, int v) { return new RegVal { Hive = h, Key = key, Name = name, Kind = RK.DWord, Data = v }; }
        public static RegVal Sz(RegistryHive h, string key, string name, string v) { return new RegVal { Hive = h, Key = key, Name = name, Kind = RK.String, Data = v }; }
        public static RegVal Del(RegistryHive h, string key, string name) { return new RegVal { Hive = h, Key = key, Name = name, Kind = RK.Delete }; }
        public static RegVal Bin(RegistryHive h, string key, string name, byte[] v) { return new RegVal { Hive = h, Key = key, Name = name, Kind = RK.Binary, Data = v }; }

        // Тип запуска службы: 2 — автоматически, 3 — вручную, 4 — отключена
        public const string Services = @"SYSTEM\CurrentControlSet\Services\";
        public static RegVal Start(string svc, int v) { return new RegVal { Hive = HKLM, Key = Services + svc, Name = "Start", Kind = RK.DWord, Data = v, OnlyIfKeyExists = true }; }
        public static RegVal Delayed(string svc, int v) { return new RegVal { Hive = HKLM, Key = Services + svc, Name = "DelayedAutostart", Kind = RK.DWord, Data = v, OnlyIfKeyExists = true }; }

        public static RegistryKey Base(RegistryHive h) { return RegistryKey.OpenBaseKey(h, RegistryView.Registry64); }

        public static object Get(RegistryHive h, string key, string name)
        {
            using (var b = Base(h))
            using (var k = b.OpenSubKey(key))
                return k == null ? null : k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        }

        public static int? GetInt(RegistryHive h, string key, string name)
        {
            var v = Get(h, key, name);
            if (v is int) return (int)v;
            if (v is long) return (int)(long)v;
            int r;
            if (v is string && int.TryParse((string)v, out r)) return r;
            return null;
        }

        // Совпадает ли текущее значение в реестре с желаемым
        public static bool KeyExists(RegistryHive h, string key)
        {
            using (var b = Base(h))
            using (var k = b.OpenSubKey(key))
                return k != null;
        }

        public static bool Matches(RegVal v)
        {
            if (v.OnlyIfKeyExists && !KeyExists(v.Hive, v.Key)) return true;
            var cur = Get(v.Hive, v.Key, v.Name);
            if (v.Kind == RK.Delete) return cur == null;
            if (cur == null) return false;
            if (v.Kind == RK.DWord)
            {
                int? i = GetInt(v.Hive, v.Key, v.Name);
                return i.HasValue && i.Value == (int)v.Data;
            }
            if (v.Kind == RK.Binary)
            {
                var a = cur as byte[];
                var w = (byte[])v.Data;
                if (a == null || a.Length != w.Length) return false;
                for (int j = 0; j < a.Length; j++) if (a[j] != w[j]) return false;
                return true;
            }
            return string.Equals(Convert.ToString(cur), (string)v.Data, StringComparison.OrdinalIgnoreCase);
        }

        // Записать значение; перед первым изменением исходное значение сохраняется в резервную копию
        public static void Apply(RegVal v)
        {
            if (v.OnlyIfKeyExists && !KeyExists(v.Hive, v.Key)) return;
            Backup.SaveReg(v.Hive, v.Key, v.Name);
            using (var b = Base(v.Hive))
            {
                if (v.Kind == RK.Delete)
                {
                    using (var k = b.OpenSubKey(v.Key, true))
                        if (k != null) k.DeleteValue(v.Name, false);
                    return;
                }
                using (var k = b.CreateSubKey(v.Key))
                {
                    if (v.Kind == RK.DWord) k.SetValue(v.Name, (int)v.Data, RegistryValueKind.DWord);
                    else if (v.Kind == RK.Binary) k.SetValue(v.Name, (byte[])v.Data, RegistryValueKind.Binary);
                    else k.SetValue(v.Name, (string)v.Data, RegistryValueKind.String);
                }
            }
        }

        // Значение по умолчанию (безымянное) у ключа — например, регистрация COM-провайдера
        public static string DefaultValue(RegistryHive h, string key)
        {
            using (var b = Base(h))
            using (var k = b.OpenSubKey(key))
                return k == null ? null : k.GetValue(null) as string;
        }

        public static void DeleteKey(RegistryHive h, string key)
        {
            Backup.SaveKey(h, key);
            using (var b = Base(h))
                try { b.DeleteSubKeyTree(key, false); } catch { }
        }

        // Создать ключ (при необходимости со значением по умолчанию)
        public static void CreateKey(RegistryHive h, string key, string defaultValue)
        {
            using (var b = Base(h))
            using (var k = b.CreateSubKey(key))
                if (defaultValue != null) k.SetValue(null, defaultValue, RegistryValueKind.String);
        }

        // Запись «как есть» — используется при откате из резервной копии
        public static void Raw(RegistryHive h, string key, string name, object data, RegistryValueKind kind)
        {
            using (var b = Base(h))
            {
                if (data == null)
                {
                    using (var k = b.OpenSubKey(key, true))
                        if (k != null) k.DeleteValue(name, false);
                    return;
                }
                using (var k = b.CreateSubKey(key))
                    k.SetValue(name, data, kind);
            }
        }
    }
}
