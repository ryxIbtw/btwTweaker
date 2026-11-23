using System;
using System.Collections.Generic;

namespace BtwTweaker
{
    // Что нужно, чтобы изменение применилось
    public enum Restart { None = 0, Explorer = 1, SignOut = 2, Reboot = 3 }

    public abstract class Tweak
    {
        public string Title;
        public string Description;
        public string Icon = "";          // символ из Segoe Fluent Icons / MDL2 Assets
        public string IconColor;                // null = обычный цвет текста
        public string Caution;                  // жёлтый «!»: что может сломаться
        public string Security;                 // красный щит: чем опасно для защиты
        public Restart Restart = Restart.None;
        public bool Win11Only;
        public int MinBuild;                    // минимальная сборка Windows (0 = любая)
        // Постоянный ключ твика для избранного, профилей и слежения — русское название,
        // даже когда интерфейс на английском (файлы профилей не зависят от языка).
        string key;
        public string Id { get { if (key == null) key = L.Ru(Title); return key; } }
        // Поддерживает ли этот ПК функцию (например, видеокарта для HAGS)
        public virtual bool IsSupported() { return true; }
        // Можно ли пользоваться твиком на этом ПК (версия Windows и поддержка)
        public bool Usable() { return !(Win11Only && !Sys.IsWin11) && Sys.Build >= MinBuild && IsSupported(); }
        // Если изменение сейчас невозможно — текст для пользователя (например, включена защита от подделки)
        public virtual string Blocker(bool wantEnable) { return null; }
        // Что именно меняет твик (для блока «Что меняется»); null — не показываем
        public virtual List<ChangeInfo> Changes() { return null; }
    }

    // Переключатель: «Включено» / «Отключено» — состояние самой функции Windows
    public abstract class ToggleTweak : Tweak
    {
        public abstract bool ReadEnabled();
        public abstract void SetEnabled(bool on);
    }

    // Выбор из списка (например, «Пауза» для обновлений)
    public abstract class ChoiceTweak : Tweak
    {
        public string[] Options;
        // -1 = текущее значение не совпадает ни с одним вариантом
        public abstract int ReadIndex();
        public abstract void SetIndex(int i);
        public virtual string UnknownText() { return L.T("Своё"); }
        // Текст подтверждения перед выбором варианта (null — без вопроса)
        public virtual string Confirm(int i) { return null; }
    }

    // Переключатель на основе реестра: набор значений «когда выключено» и «когда включено»
    public class RegToggle : ToggleTweak
    {
        public List<RegVal> OffVals = new List<RegVal>();
        public List<RegVal> OnVals = new List<RegVal>();
        public Action<bool> After;
        // true: состояние читается по главному значению «включено»
        public bool CheckOn;
        // Если главного значения нет вовсе — функция включена (работает по умолчанию Windows)
        public bool DefaultOn;
        // Службы: при отключении — остановить, при включении — запустить (только если тип запуска не «вручную»)
        public string[] StopServices;
        public string[] StartServices;
        // Задания планировщика: выключаются вместе с функцией, включаются обратно вместе с ней
        public string[] TaskPaths;

        public RegToggle WhenOff(params RegVal[] v) { OffVals.AddRange(v); return this; }
        public RegToggle WhenOn(params RegVal[] v) { OnVals.AddRange(v); return this; }

        // Состояние определяется по первому (главному) значению; остальные — дополнительные.
        // Так программа правильно видит настройки, сделанные вручную или другими твикерами.
        public override bool ReadEnabled()
        {
            // Твик только из заданий: состояние — по первому существующему заданию
            if (OffVals.Count == 0 && OnVals.Count == 0 && TaskPaths != null)
            {
                foreach (var p in TaskPaths)
                {
                    bool? on = Tasks.IsEnabled(p);
                    if (on.HasValue) return on.Value;
                }
                return true;
            }
            if (CheckOn)
            {
                if (OnVals.Count == 0) return false;
                var v = OnVals[0];
                if (Reg.Get(v.Hive, v.Key, v.Name) == null) return DefaultOn;
                return Reg.Matches(v);
            }
            return OffVals.Count == 0 || !Reg.Matches(OffVals[0]);
        }

        public override void SetEnabled(bool on)
        {
            foreach (var v in on ? OnVals : OffVals) Reg.Apply(v);
            if (!on && StopServices != null) foreach (var s in StopServices) Sys.StopService(s);
            if (on && StartServices != null) foreach (var s in StartServices) Sys.StartService(s);
            if (TaskPaths != null)
                foreach (var p in TaskPaths)
                    if (Tasks.Exists(p)) { Backup.SaveTask(p); Tasks.SetEnabled(p, on); }
            if (After != null) After(on);
        }

        public override List<ChangeInfo> Changes()
        {
            var l = ChangeText.FromLists(OnVals, OffVals);
            if (TaskPaths != null) foreach (var p in TaskPaths) l.Add(ChangeText.FromTask(p));
            return l;
        }

        // Твик только из заданий, которых нет в этой Windows, — «Не поддерживается»
        public override bool IsSupported()
        {
            if (OffVals.Count == 0 && OnVals.Count == 0 && TaskPaths != null)
            {
                foreach (var p in TaskPaths) if (Tasks.Exists(p)) return true;
                return false;
            }
            return true;
        }
    }

    // Выбор на основе одного значения реестра
    public class RegChoice : ChoiceTweak
    {
        public RegVal[] Values;   // по одному на вариант
        public Action<int> After;

        public override int ReadIndex()
        {
            for (int i = 0; i < Values.Length; i++) if (Reg.Matches(Values[i])) return i;
            return -1;
        }

        public override void SetIndex(int i)
        {
            Reg.Apply(Values[i]);
            if (After != null) After(i);
        }

        public override List<ChangeInfo> Changes()
        {
            var v0 = Values[0];
            var c = new ChangeInfo { Kind = L.T("Реестр"), Title = ChangeText.HiveShort(v0.Hive) + "\\" + v0.Key, Detail = v0.Name, RegPath = ChangeText.HiveName(v0.Hive) + "\\" + v0.Key };
            for (int i = 0; i < Values.Length; i++) c.Add(Options[i], ChangeText.Value(Values[i]));
            try { c.Current = ReadIndex(); } catch { }
            return new List<ChangeInfo> { c };
        }
    }

    public class Group
    {
        public string Title;
        public List<Tweak> Tweaks = new List<Tweak>();
        public Group(string title, params Tweak[] t) { Title = title; Tweaks.AddRange(t); }
    }

    public class Section
    {
        public string Title;
        public string Icon;
        public string Page;   // особая страница вместо списка твиков (например, "power")
        public List<Group> Groups = new List<Group>();
        public Section(string title, string icon, params Group[] g) { Title = title; Icon = icon; Groups.AddRange(g); }
    }
}
