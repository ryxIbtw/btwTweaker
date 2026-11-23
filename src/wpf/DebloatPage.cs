using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace BtwTweaker
{
    // «Выпиливание»: автоустановка Teams, OneDrive, Edge и приложения UWP
    public class DebloatPage : StackPanel
    {
        readonly MainWindow owner;
        readonly StackPanel scripts = new StackPanel();
        readonly StackPanel apps = new StackPanel();
        readonly TextBlock appsHeader = Ui.Txt(L.T("Программы UWP"), 15, Theme.Text, FontWeights.SemiBold);
        readonly TextBlock progress = new TextBlock { FontSize = 12.5, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        Border scriptsBlock, appsBlock;
        List<AppxInfo> list = new List<AppxInfo>();
        Button optimal, reload;
        bool busy;
        static int filter;   // 0 — все, 1 — скрипты, 2 — UWP

        static bool KeepData { get { return Sys.Pref("DebloatData") != "0"; } }

        static readonly RegToggle Teams = new RegToggle
        {
            Title = L.T("Автоустановка MS Teams"),
            Description = L.T("В новых сборках Windows 11 личный Teams («Чат») не встроен в систему, а докачивается из Магазина после первой настройки. ConfigureChatAutoInstall = 0 запрещает это скачивание — способ описан в документации Microsoft Intune и Autopilot. Корпоративный Teams (работа или учёба) продолжит работать, если установлен вручную."),
            Icon = "", IconColor = "#7B83EB",
        }
        .WhenOff(Reg.Dw(Reg.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Communications", "ConfigureChatAutoInstall", 0))
        .WhenOn(Reg.Del(Reg.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Communications", "ConfigureChatAutoInstall"));

        public DebloatPage(MainWindow w)
        {
            owner = w;
            Children.Add(Toolbar());

            var seg = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            seg.Children.Add(Ui.Segmented(new[] { L.T("Все"), L.T("Скрипты и настройки"), L.T("Программы UWP") }, filter, i => { filter = i; ApplyFilter(); }));
            Children.Add(seg);

            // Скрипты и настройки
            var sh = Ui.Txt(L.T("Скрипты и настройки"), 15, Theme.Text, FontWeights.SemiBold);
            sh.Margin = new Thickness(2, 16, 0, 10);
            var sp = new StackPanel();
            sp.Children.Add(sh);
            scripts.Children.Add(new TweakCard(Teams, owner));
            scripts.Children.Add(RemoveRow("OneDrive", "", "#2E8BE6",
                L.T("Удаляет клиент OneDrive штатным OneDriveSetup /uninstall, затем его кэш, задачи обновления, автозапуск и значок в Проводнике. Папка OneDrive с вашими файлами остаётся на месте."),
                L.T("файлы перестанут синхронизироваться с облаком. Вернуть OneDrive можно, скачав его с сайта Microsoft."),
                Debloat.OneDriveInstalled, Debloat.RemoveOneDrive));
            scripts.Children.Add(RemoveRow("Microsoft Edge", "", "#3FA9F5",
                L.T("Удаляет браузер Edge собственным установщиком Edge. WebView2 (на нём работают виджеты, поиск и многие программы) не трогается. В некоторых регионах Windows может не разрешить удаление — тогда Edge останется, а система не пострадает."),
                L.T("ссылки из программ будут открываться в браузере по умолчанию. После крупных обновлений Windows Edge может вернуться."),
                Debloat.EdgeInstalled, Debloat.RemoveEdge));
            sp.Children.Add(scripts);
            scriptsBlock = new Border { Child = sp };
            Children.Add(scriptsBlock);

            // Программы UWP
            var ap = new StackPanel();
            appsHeader.Margin = new Thickness(2, 18, 0, 10);
            ap.Children.Add(appsHeader);
            ap.Children.Add(apps);
            appsBlock = new Border { Child = ap };
            Children.Add(appsBlock);

            ApplyFilter();
            LoadApps();
        }

        void ApplyFilter()
        {
            scriptsBlock.Visibility = filter == 2 ? Visibility.Collapsed : Visibility.Visible;
            appsBlock.Visibility = filter == 1 ? Visibility.Collapsed : Visibility.Visible;
        }

        FrameworkElement Toolbar()
        {
            var g = Ui.Cols("auto", "*", "auto", "auto");
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.Children.Add(Ui.At(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Theme.AccentSoft, VerticalAlignment = VerticalAlignment.Top, Child = Ui.Icon("", 17, Theme.AccentText) }, 0, 0));
            var sp = new StackPanel { Margin = new Thickness(14, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(Ui.Txt(L.T("Удаление встроенных программ"), 14, Theme.Text, FontWeights.SemiBold));
            sp.Children.Add(new TextBlock { Text = L.T("Удалённое не возвращается кнопкой «Откатить всё» — только переустановкой из Microsoft Store или с сайта. Перед каждым удалением программа спросит подтверждение."), FontSize = 12.5, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            g.Children.Add(Ui.At(sp, 0, 1));
            optimal = Ui.IconBtn("", L.T("Оптимальное"), Theme.Accent, Theme.OnAccent, ApplyOptimal);
            optimal.Margin = new Thickness(0, 0, 8, 0);
            optimal.VerticalAlignment = VerticalAlignment.Top;
            optimal.ToolTip = L.T("Удалить известные ненужные приложения (Clipchamp, Новости, Погода, Советы…). Сначала покажу список.");
            g.Children.Add(Ui.At(optimal, 0, 2));
            reload = Ui.IconBtn("", L.T("Обновить"), Theme.Second, Theme.Text, () => { LoadApps(); RefreshScripts(); });
            reload.VerticalAlignment = VerticalAlignment.Top;
            g.Children.Add(Ui.At(reload, 0, 3));

            var opt = Ui.Cols("*", "auto");
            opt.Margin = new Thickness(54, 12, 0, 0);
            var ot = new StackPanel();
            ot.Children.Add(Ui.Txt(L.T("Удалять данные удалённых приложений"), 13, Theme.Text));
            ot.Children.Add(new TextBlock { Text = L.T("После удаления очищать папку приложения в %LOCALAPPDATA%\\Packages (настройки, кэш, вход в аккаунт)."), FontSize = 12, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap });
            opt.Children.Add(Ui.At(ot, 0, 0));
            var sw = new ToggleButton { Style = Theme.S("Switch"), IsChecked = KeepData, VerticalAlignment = VerticalAlignment.Center };
            sw.Click += (s, e) => Sys.SetPref("DebloatData", sw.IsChecked == true ? "1" : "0");
            opt.Children.Add(Ui.At(sw, 0, 1));
            Grid.SetColumnSpan(opt, 4);
            g.Children.Add(Ui.At(opt, 1, 0));

            var all = new StackPanel();
            all.Children.Add(g);
            all.Children.Add(progress);
            var card = Ui.Card(all, new Thickness(18, 14, 14, 14), false);
            card.Margin = new Thickness(0, 0, 0, 6);
            return card;
        }

        void SetBusy(bool on, string text)
        {
            busy = on;
            optimal.IsEnabled = reload.IsEnabled = !on;
            progress.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
            progress.Text = text ?? "";
            progress.Foreground = Theme.AccentText;
            Mouse.OverrideCursor = on ? Cursors.Wait : null;
        }

        // ───────── OneDrive / Edge ─────────

        readonly List<Action> scriptRefreshers = new List<Action>();
        void RefreshScripts() { foreach (var r in scriptRefreshers) r(); }

        FrameworkElement RemoveRow(string title, string icon, string iconColor, string desc, string caution, Func<bool> installed, Action remove)
        {
            var g = Ui.Cols("34", "*", "auto");
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var ic = Ui.Icon(icon, 17, Theme.B(iconColor));
            ic.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(Ui.At(ic, 0, 0));

            var head = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            head.Children.Add(Ui.Txt(title, 14, Theme.Text));
            var chev = Ui.Icon("", 9, Theme.Sub);
            chev.Margin = new Thickness(7, 2, 0, 0);
            chev.RenderTransformOrigin = new Point(0.5, 0.5);
            chev.RenderTransform = new RotateTransform(0);
            head.Children.Add(chev);
            g.Children.Add(Ui.At(head, 0, 1));

            var det = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 4, 2) };
            det.Children.Add(new TextBlock { Text = desc, TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Theme.Sub, LineHeight = 20, Margin = new Thickness(0, 0, 40, 0) });
            det.Children.Add(Callout("", Theme.Yellow, L.T("Учтите"), char.ToUpper(caution[0]) + caution.Substring(1)));
            Grid.SetColumnSpan(det, 2);
            g.Children.Add(Ui.At(det, 1, 1));
            head.MouseLeftButtonUp += (s, e) =>
            {
                bool show = det.Visibility != Visibility.Visible;
                det.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                ((RotateTransform)chev.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(show ? 180 : 0, Theme.Anim));
                if (show) Ui.FadeIn(det);
            };

            var right = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var removed = DonePill(L.T("Удалено"));
            var btn = DangerBtn(L.T("Удалить"));
            right.Children.Add(removed);
            right.Children.Add(btn);
            g.Children.Add(Ui.At(right, 0, 2));

            Action refresh = () =>
            {
                bool inst = false;
                try { inst = installed(); } catch { }
                btn.Visibility = inst ? Visibility.Visible : Visibility.Collapsed;
                removed.Visibility = inst ? Visibility.Collapsed : Visibility.Visible;
            };
            refresh();
            scriptRefreshers.Add(refresh);

            btn.Click += async (s, e) =>
            {
                if (busy) return;
                if (!Dialog.Ask(owner, L.T("Удалить ") + title + "?", desc + L.T("\n\nУчтите: ") + caution + L.T("\n\nЭто нельзя отменить кнопкой «Откатить всё»."), L.T("Удалить"), L.T("Отмена"), true)) return;
                SetBusy(true, L.T("Удаляю ") + title + "…");
                Exception err = null;
                try
                {
                    await Task.Run(remove);
                    Journal.Info(L.T("Выпиливание"), L.T("Удалено: ") + title, L.T("Не откатывается — вернуть можно только установкой заново."));
                }
                catch (Exception ex) { err = ex; }
                SetBusy(false, null);
                refresh();
                if (err != null) Dialog.Info(owner, L.T("Не удалось удалить ") + title, err.Message);
                else Ui.FadeIn(removed);
            };

            var card = Ui.Card(g, new Thickness(16, 12, 16, 12));
            card.Margin = new Thickness(0, 0, 0, 8);
            return card;
        }

        static Border DonePill(string text)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var ch = Ui.Icon("", 11, Theme.AccentText);
            ch.Margin = new Thickness(0, 1, 6, 0);
            sp.Children.Add(ch);
            sp.Children.Add(Ui.Txt(text, 12.5, Theme.AccentText, FontWeights.SemiBold));
            return new Border { Background = Theme.Done, BorderBrush = Theme.DoneBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 3, 12, 3), Child = sp, HorizontalAlignment = HorizontalAlignment.Right };
        }

        static Button DangerBtn(string text)
        {
            var b = Ui.Btn(text, Brushes.Transparent, () => { });
            b.BorderBrush = Theme.Red;
            b.Foreground = Theme.Danger;
            b.BorderThickness = new Thickness(1.5);
            b.Padding = new Thickness(12, 4, 12, 4);
            b.FontSize = 12.5;
            return b;
        }

        static Border Callout(string glyph, Brush color, string title, string text)
        {
            var c = ((SolidColorBrush)color).Color;
            var g = Ui.Cols("auto", "*");
            var icon = Ui.Icon(glyph, 15, color);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 1, 10, 0);
            g.Children.Add(Ui.At(icon, 0, 0));
            var sp = new StackPanel();
            sp.Children.Add(Ui.Txt(title, 13, Theme.Text, FontWeights.SemiBold));
            sp.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Foreground = Theme.Sub, LineHeight = 18, Margin = new Thickness(0, 2, 0, 0) });
            g.Children.Add(Ui.At(sp, 0, 1));
            return new Border
            {
                Background = new SolidColorBrush(Theme.A(c, 0x1A)), BorderBrush = new SolidColorBrush(Theme.A(c, 0x50)), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 10, 40, 4), Child = g
            };
        }

        // ───────── Приложения UWP ─────────

        async void LoadApps()
        {
            if (busy) return;
            SetBusy(true, null);
            apps.Children.Clear();
            apps.Children.Add(Ui.Txt(L.T("Читаю список приложений…"), 13, Theme.Sub));
            Exception err = null;
            try { list = await Task.Run(() => Debloat.ListApps()); }
            catch (Exception ex) { err = ex; list = new List<AppxInfo>(); }
            SetBusy(false, null);
            apps.Children.Clear();
            if (err != null) { apps.Children.Add(Ui.Txt(err.Message, 13, Theme.Red)); return; }
            appsHeader.Text = L.T("Программы UWP · ") + list.Count;
            var sizes = new List<KeyValuePair<AppxInfo, TextBlock>>();
            foreach (var a in list)
            {
                TextBlock size;
                apps.Children.Add(AppRow(a, out size));
                sizes.Add(new KeyValuePair<AppxInfo, TextBlock>(a, size));
            }
            Ui.FadeIn(apps);
            // Размеры — в фоне, по одному
            var ui = Dispatcher;
            await Task.Run(() =>
            {
                foreach (var kv in sizes)
                {
                    long s = 0;
                    try { s = Debloat.Size(kv.Key); } catch { }
                    var tb = kv.Value;
                    string text = s > 0 ? Fs.Fmt(s) : "";
                    if (!ui.HasShutdownStarted) ui.BeginInvoke(new Action(() => tb.Text = text));
                }
            });
        }

        FrameworkElement AppRow(AppxInfo a, out TextBlock size)
        {
            var g = Ui.Cols("auto", "*", "auto", "auto");
            var logo = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = Theme.Second, ClipToBounds = true };
            var img = LoadLogo(a.Logo);
            if (img != null) logo.Child = new Image { Source = img, Width = 30, Height = 30, Stretch = Stretch.Uniform };
            else logo.Child = Ui.Icon("", 15, Theme.Sub);
            g.Children.Add(Ui.At(logo, 0, 0));

            var names = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            var t = Ui.Txt(a.DisplayName, 14, Theme.Text);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            top.Children.Add(t);
            if (a.Recommended)
            {
                var chip = new Border { Background = Theme.Done, CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 1, 8, 2), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ui.Txt(L.T("можно удалить"), 11, Theme.AccentText, FontWeights.SemiBold), ToolTip = L.T("Входит в набор «Оптимальное»") };
                top.Children.Add(chip);
            }
            names.Children.Add(top);
            names.Children.Add(new TextBlock { Text = a.Name, FontSize = 11.5, Foreground = Theme.Sub, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) });
            g.Children.Add(Ui.At(names, 0, 1));

            size = Ui.Txt("…", 12.5, Theme.Sub);
            size.Margin = new Thickness(0, 0, 12, 0);
            g.Children.Add(Ui.At(size, 0, 2));

            var right = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var removed = DonePill(L.T("Удалено"));
            removed.Visibility = Visibility.Collapsed;
            var btn = DangerBtn(L.T("Удалить"));
            right.Children.Add(removed);
            right.Children.Add(btn);
            g.Children.Add(Ui.At(right, 0, 3));

            btn.Click += async (s, e) =>
            {
                if (busy) return;
                if (!Dialog.Ask(owner, L.T("Удалить «") + a.DisplayName + L.T("»?"),
                    L.T("Приложение будет удалено для вашей учётной записи") + (KeepData ? L.T(" вместе с его данными (настройки, кэш, вход в аккаунт)") : "") + L.T(".\n\nЭто нельзя отменить кнопкой «Откатить всё» — вернуть можно только из Microsoft Store."),
                    L.T("Удалить"), L.T("Отмена"), true)) return;
                SetBusy(true, L.T("Удаляю «") + a.DisplayName + L.T("»…"));
                bool data = KeepData;
                Exception err = null;
                try { await Task.Run(() => Debloat.RemoveApp(a, data)); Journal.Info(L.T("Выпиливание"), L.T("Удалено приложение: ") + a.DisplayName, L.T("Не откатывается — вернуть можно из Microsoft Store.")); }
                catch (Exception ex) { err = ex; }
                SetBusy(false, null);
                if (err != null) { Dialog.Info(owner, L.T("Не удалось удалить"), err.Message); return; }
                btn.Visibility = Visibility.Collapsed;
                removed.Visibility = Visibility.Visible;
                Ui.FadeIn(removed);
            };

            var card = Ui.Card(g, new Thickness(12, 9, 14, 9));
            card.Margin = new Thickness(0, 0, 0, 6);
            return card;
        }

        static ImageSource LoadLogo(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                var b = new BitmapImage();
                b.BeginInit();
                b.UriSource = new Uri(path);
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.DecodePixelWidth = 64;
                b.EndInit();
                b.Freeze();
                return b;
            }
            catch { return null; }
        }

        // ───────── Оптимальное ─────────

        async void ApplyOptimal()
        {
            if (busy) return;
            var todo = list.FindAll(a => a.Recommended);
            if (todo.Count == 0)
            {
                Dialog.Info(owner, L.T("Уже чисто"), L.T("Ни одного приложения из набора «Оптимальное» не установлено."));
                return;
            }
            var names = todo.ConvertAll(a => a.DisplayName);
            if (!Dialog.AskList(owner, L.T("Удалить ") + todo.Count + L.T(" прил.?"),
                L.T("Будут удалены для вашей учётной записи") + (KeepData ? L.T(" вместе с данными") : "") + L.T(". Вернуть можно только из Microsoft Store."), names, L.T("Удалить"), true))
                return;
            bool data = KeepData;
            var failed = new List<string>();
            for (int i = 0; i < todo.Count; i++)
            {
                var a = todo[i];
                SetBusy(true, L.T("Удаляю ") + (i + 1) + L.T(" из ") + todo.Count + ": " + a.DisplayName + "…");
                try { await Task.Run(() => Debloat.RemoveApp(a, data)); Journal.Info(L.T("Выпиливание"), L.T("Удалено приложение: ") + a.DisplayName, L.T("Не откатывается — вернуть можно из Microsoft Store.")); }
                catch { failed.Add(a.DisplayName); }
            }
            SetBusy(false, null);
            LoadApps();
            Dialog.Info(owner, L.T("Готово"), L.T("Удалено: ") + (todo.Count - failed.Count) + L.T(" из ") + todo.Count + "." + (failed.Count > 0 ? L.T("\n\nНе удалось: ") + string.Join(", ", failed) : ""));
        }
    }
}
