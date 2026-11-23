using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BtwTweaker
{
    // Главная: приветственная карточка, изменения, сведения о ПК и плитки разделов
    public class HomePage : StackPanel
    {
        readonly MainWindow owner;

        public HomePage(MainWindow w)
        {
            owner = w;

            // ── Верхний ряд: приветствие + изменения ──
            var top = Ui.Cols("2*", "16", "*");
            top.Children.Add(Ui.At(Hero(), 0, 0));
            top.Children.Add(Ui.At(Changes(), 0, 2));
            Children.Add(top);

            // ── Ваш компьютер ──
            Children.Add(Header("Ваш компьютер"));
            var info = Ui.Cols("*", "12", "*", "12", "*");
            info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            info.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            info.Children.Add(Ui.At(Tile("", "Windows", WindowsName(), null), 0, 0));
            info.Children.Add(Ui.At(Tile("", "Процессор", Cpu(), null), 0, 2));
            TextBlock gpuText;
            var gpu = Tile("", "Видеокарта", "…", null, out gpuText);
            info.Children.Add(Ui.At(gpu, 0, 4));
            info.Children.Add(Ui.At(MemoryTile(), 2, 0));
            info.Children.Add(Ui.At(DiskTile(), 2, 2));
            info.Children.Add(Ui.At(Tile("", "Работает без перезагрузки", Uptime(), null), 2, 4));
            Children.Add(info);
            LoadGpu(gpuText);

            // ── Разделы ──
            Children.Add(Header("Разделы"));
            var grid = new UniformGrid4();
            foreach (var s in owner.Sections) grid.Add(SectionTile(s));
            Children.Add(grid.Panel);
        }

        static TextBlock Header(string text)
        {
            var t = Ui.Txt(text, 15, Theme.Text, FontWeights.SemiBold);
            t.Margin = new Thickness(2, 24, 0, 12);
            return t;
        }

        // ───────── Карточки ─────────

        FrameworkElement Hero()
        {
            var g = new Grid();
            var deco = Ui.Icon("", 150, Brushes.White);
            deco.Opacity = 0.12;
            deco.HorizontalAlignment = HorizontalAlignment.Right;
            deco.VerticalAlignment = VerticalAlignment.Center;
            deco.Margin = new Thickness(0, 0, -10, 0);
            g.Children.Add(deco);

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = "Настройте Windows под себя", FontFamily = Theme.Display, FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(new TextBlock
            {
                Text = "Быстрее, тише и без лишней телеметрии. Каждое изменение запоминается — его можно откатить одной кнопкой.",
                FontSize = 13.5, Foreground = new SolidColorBrush(Color.FromArgb(0xD9, 255, 255, 255)), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 90, 18), LineHeight = 20
            });
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            var go = Ui.Btn("Начать с базовых твиков", Brushes.White, () => owner.NavigateSection(owner.Sections[0]));
            go.Foreground = Theme.Accent;
            go.FontWeight = FontWeights.SemiBold;
            go.Padding = new Thickness(16, 9, 16, 9);
            btns.Children.Add(go);
            var rp = Ui.IconBtn("", "Точка восстановления", new SolidColorBrush(Color.FromArgb(0x33, 255, 255, 255)), Brushes.White, () => owner.CreateRestorePoint(false));
            rp.Padding = new Thickness(14, 9, 16, 9);
            rp.Margin = new Thickness(10, 0, 0, 0);
            btns.Children.Add(rp);
            sp.Children.Add(btns);
            g.Children.Add(sp);

            return new Border { Background = Theme.HeroBrush(), CornerRadius = new CornerRadius(18), Padding = new Thickness(28, 26, 28, 26), Child = g, ClipToBounds = true };
        }

        FrameworkElement Changes()
        {
            int n = Backup.Count;
            var sp = new StackPanel();
            sp.Children.Add(Ui.Txt("Изменено настроек", 13, Theme.Sub));
            sp.Children.Add(new TextBlock { Text = n.ToString(), FontFamily = Theme.Display, FontSize = 44, FontWeight = FontWeights.Bold, Foreground = Theme.Accent, Margin = new Thickness(0, 2, 0, 0) });
            sp.Children.Add(new TextBlock
            {
                Text = n == 0 ? "btwTweaker ещё ничего не менял. Исходные значения запоминаются перед первым изменением." : "Исходные значения сохранены — всё можно вернуть как было.",
                FontSize = 12.5, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 14), LineHeight = 18
            });
            var rb = Ui.IconBtn("", "Откатить всё", Theme.Second, Theme.Danger, owner.RollbackAll);
            rb.IsEnabled = n > 0;
            rb.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(rb);
            return Ui.Card(sp, new Thickness(22, 20, 22, 20), false);
        }

        static Border Tile(string icon, string label, string value, FrameworkElement extra)
        {
            TextBlock v;
            return Tile(icon, label, value, extra, out v);
        }

        static Border Tile(string icon, string label, string value, FrameworkElement extra, out TextBlock v)
        {
            var g = Ui.Cols("auto", "*");
            var circle = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(11), Background = Theme.AccentSoft, VerticalAlignment = VerticalAlignment.Top, Child = Ui.Icon(icon, 16, Theme.Accent) };
            g.Children.Add(Ui.At(circle, 0, 0));
            var sp = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            sp.Children.Add(Ui.Txt(label, 12, Theme.Sub));
            v = new TextBlock { Text = value, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Theme.Text, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) };
            v.ToolTip = value;
            sp.Children.Add(v);
            if (extra != null) sp.Children.Add(extra);
            g.Children.Add(Ui.At(sp, 0, 1));
            return Ui.Card(g, new Thickness(16, 14, 16, 14));
        }

        static FrameworkElement Bar(double part)
        {
            var g = new Grid { Height = 5, Margin = new Thickness(0, 8, 0, 0) };
            g.Children.Add(new Border { Background = Theme.Second, CornerRadius = new CornerRadius(3) });
            var fill = new Border { Background = Theme.Accent, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left };
            g.Children.Add(fill);
            g.SizeChanged += (s, e) => fill.Width = Math.Max(0, Math.Min(1, part)) * g.ActualWidth;
            return g;
        }

        Border MemoryTile()
        {
            var m = new MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (!GlobalMemoryStatusEx(ref m)) return Tile("", "Оперативная память", "—", null);
            double total = m.ullTotalPhys / 1073741824.0;
            string v = Math.Round(total) + " ГБ · занято " + m.dwMemoryLoad + "%";
            return Tile("", "Оперативная память", v, Bar(m.dwMemoryLoad / 100.0));
        }

        Border DiskTile()
        {
            try
            {
                var d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                double used = 1 - (double)d.TotalFreeSpace / d.TotalSize;
                string v = "свободно " + Fs.Fmt(d.TotalFreeSpace) + " из " + Fs.Fmt(d.TotalSize);
                return Tile("", "Системный диск " + d.Name.TrimEnd('\\'), v, Bar(used));
            }
            catch { return Tile("", "Системный диск", "—", null); }
        }

        Border SectionTile(Section s)
        {
            int n = 0;
            foreach (var g in s.Groups) n += g.Tweaks.Count;
            string sub = s.Page == "power" ? "Схемы питания" : s.Page == "clean" ? "Освободить место" : MainWindow.Tweaks(n);
            var sp = new StackPanel();
            var circle = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Theme.AccentSoft, HorizontalAlignment = HorizontalAlignment.Left, Child = Ui.Icon(s.Icon, 17, Theme.Accent) };
            sp.Children.Add(circle);
            sp.Children.Add(new TextBlock { Text = s.Title, FontSize = 14.5, FontWeight = FontWeights.SemiBold, Foreground = Theme.Text, Margin = new Thickness(0, 12, 0, 2), TextTrimming = TextTrimming.CharacterEllipsis });
            sp.Children.Add(Ui.Txt(sub, 12.5, Theme.Sub));
            var card = Ui.Card(sp, new Thickness(18, 16, 18, 16));
            card.Cursor = Cursors.Hand;
            card.MouseLeftButtonUp += (o, e) => owner.NavigateSection(s);
            return card;
        }

        // ───────── Сведения о системе ─────────

        static string WindowsName()
        {
            const string k = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string name = Reg.Get(Reg.HKLM, k, "ProductName") as string ?? "Windows";
            if (Sys.IsWin11) name = name.Replace("Windows 10", "Windows 11");   // ProductName в Windows 11 всё ещё «Windows 10»
            string ver = Reg.Get(Reg.HKLM, k, "DisplayVersion") as string;
            return name + (ver != null ? " " + ver : "") + " · " + Sys.Build;
        }

        static string Cpu()
        {
            var s = Reg.Get(Reg.HKLM, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") as string;
            return s == null ? "—" : System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " ");
        }

        // Название видеокарты через WMI — в фоне; результат возвращаем в поток окна
        static void LoadGpu(TextBlock v)
        {
            var ui = v.Dispatcher;
            Task.Run(() =>
            {
                try
                {
                    var names = new System.Collections.Generic.List<string>();
                    using (var q = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                        foreach (System.Management.ManagementObject o in q.Get())
                        {
                            var n = Convert.ToString(o["Name"]);
                            if (!string.IsNullOrEmpty(n) && n.IndexOf("Basic Display", StringComparison.OrdinalIgnoreCase) < 0 && !names.Contains(n)) names.Add(n);
                        }
                    return names.Count == 0 ? "—" : string.Join(" + ", names);
                }
                catch { return "—"; }
            }).ContinueWith(t =>
            {
                string name = t.Status == TaskStatus.RanToCompletion ? t.Result : "—";
                try
                {
                    if (!ui.HasShutdownStarted)
                        ui.BeginInvoke(new Action(() => { v.Text = name; v.ToolTip = name; }));
                }
                catch { }
            });
        }

        static string Uptime()
        {
            var t = TimeSpan.FromMilliseconds(GetTickCount64());
            if (t.TotalDays >= 1) return (int)t.TotalDays + " д " + t.Hours + " ч";
            if (t.TotalHours >= 1) return (int)t.TotalHours + " ч " + t.Minutes + " мин";
            return t.Minutes + " мин";
        }

        [DllImport("kernel32.dll")] static extern ulong GetTickCount64();

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
    }

    // Сетка плиток по 4 в ряд с отступами
    public class UniformGrid4
    {
        public readonly Grid Panel = Ui.Cols("*", "12", "*", "12", "*", "12", "*");
        int count;

        public void Add(UIElement el)
        {
            int row = count / 4, col = (count % 4) * 2;
            while (Panel.RowDefinitions.Count <= row * 2)
            {
                if (Panel.RowDefinitions.Count > 0) Panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
                Panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            Panel.Children.Add(Ui.At(el, row * 2, col));
            count++;
        }
    }
}
