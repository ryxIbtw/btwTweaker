using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace BtwTweaker
{
    // Пункт бокового меню: раздел твиков или особая страница
    public class NavEntry
    {
        public string Title;
        public string Icon;
        public string Key;      // home | settings | section
        public Section Sec;
    }

    public class MainWindow : Window
    {
        public readonly List<Section> Sections = Catalog.Build();
        public readonly List<NavEntry> Entries = new List<NavEntry>();
        readonly List<Border> navItems = new List<Border>();
        readonly List<TweakCard> cards = new List<TweakCard>();

        const double RailNarrow = 68, RailWide = 236;

        Grid rootGrid;
        Border rail;
        Grid contentHost;
        StackPanel content;
        ScrollViewer scroll;
        TextBlock pageTitle, pageSub;
        TextBox search;
        TextBlock searchHint;
        Border banner;
        TextBlock bannerText;
        Button bannerBtn;
        TextBlock maxGlyph;
        Border updBar;
        TextBlock updText;
        Button updBtn;
        ReleaseInfo found;
        bool updating;
        int current;
        Restart pending = Restart.None;

        Border resetBar;
        TextBlock resetText;
        List<ProfileStep> resetSteps;
        string resetBuildNote;

        public MainWindow()
        {
            Watch.Sections = Sections;
            Title = "btwTweaker";
            Width = 1180; Height = 780; MinWidth = 960; MinHeight = 580;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = Theme.Font;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            Entries.Add(new NavEntry { Title = L.T("Главная"), Icon = "", Key = "home" });
            Entries.Add(new NavEntry { Title = L.T("Мои твики"), Icon = "", Key = "favorites" });
            foreach (var s in Sections) Entries.Add(new NavEntry { Title = s.Title, Icon = s.Icon, Key = "section", Sec = s });
            Entries.Add(new NavEntry { Title = L.T("Журнал"), Icon = "", Key = "journal" });
            Entries.Add(new NavEntry { Title = L.T("Параметры"), Icon = "", Key = "settings" });

            BuildUi();
            ShowPage(0);

#if TESTHOOKS
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BTW_SNAP")))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = -6000; Top = 0; ShowInTaskbar = false;
                int h;
                if (int.TryParse(Environment.GetEnvironmentVariable("BTW_SNAP_H"), out h)) Height = h;
            }
#endif
            SourceInitialized += (s, e) => ApplyBackdrop();
            ContentRendered += (s, e) => OnFirstShow();
            StateChanged += (s, e) => OnState();
        }

        // ───────── Построение интерфейса ─────────

        void BuildUi()
        {
            navItems.Clear();
            Background = Theme.WindowBackground();
            Foreground = Theme.Text;
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 40,
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = Theme.Mica ? new Thickness(-1) : new Thickness(0),
                UseAeroCaptionButtons = false,
            });

            rootGrid = new Grid();
            rootGrid.Children.Add(Theme.BackgroundLayer());

            // ── Содержимое ──
            contentHost = Ui.Rows("40", "auto", "auto", "auto", "*");
            contentHost.Margin = new Thickness(Theme.Cfg.Menu == "expanded" ? RailWide : RailNarrow, 0, 0, 0);

            // Строка заголовка окна: поиск и кнопки окна
            var cap = Ui.Cols("*", "auto", "auto");
            var sg = new Grid { Width = 320, Height = 30, Margin = new Thickness(0, 6, 16, 0) };
            WindowChrome.SetIsHitTestVisibleInChrome(sg, true);
            search = new TextBox { Style = Theme.S("Search"), Padding = new Thickness(32, 4, 10, 4) };
            searchHint = new TextBlock { Text = L.T("Поиск по всем твикам"), Foreground = Theme.Sub, FontSize = 13, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(34, 0, 0, 0) };
            var sIcon = Ui.Icon("", 12, Theme.Sub);
            sIcon.HorizontalAlignment = HorizontalAlignment.Left;
            sIcon.Margin = new Thickness(12, 0, 0, 0);
            sIcon.IsHitTestVisible = false;
            sg.Children.Add(search); sg.Children.Add(searchHint); sg.Children.Add(sIcon);
            search.TextChanged += (s, e) => { searchHint.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Render(false); };
            search.KeyDown += (s, e) => { if (e.Key == Key.Escape) search.Text = ""; };
            cap.Children.Add(Ui.At(sg, 0, 1));

            var wb = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            wb.Children.Add(CaptionBtn("", "Caption", () => WindowState = WindowState.Minimized));
            maxGlyph = Ui.Icon(WindowState == WindowState.Maximized ? "" : "", 10, Theme.Text);
            var maxBtn = new Button { Style = Theme.S("Caption"), Content = maxGlyph };
            maxBtn.Click += (s, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            WindowChrome.SetIsHitTestVisibleInChrome(maxBtn, true);
            wb.Children.Add(maxBtn);
            wb.Children.Add(CaptionBtn("", "CaptionClose", Close));
            cap.Children.Add(Ui.At(wb, 0, 2));
            contentHost.Children.Add(Ui.At(cap, 0, 0));

            // Заголовок страницы
            var head = new StackPanel { Margin = new Thickness(32, 6, 32, 14) };
            pageTitle = new TextBlock { FontFamily = Theme.Display, FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Theme.Text };
            pageSub = new TextBlock { FontSize = 13, Foreground = Theme.Sub, Margin = new Thickness(1, 2, 0, 0) };
            head.Children.Add(pageTitle);
            head.Children.Add(pageSub);
            contentHost.Children.Add(Ui.At(head, 1, 0));

            // Плашка «нужен перезапуск»
            var bg = Ui.Cols("auto", "*", "auto");
            bg.Children.Add(Ui.At(Ui.Icon("", 16, Theme.Yellow), 0, 0));
            bannerText = Ui.Txt("", 13, Theme.Text);
            bannerText.Margin = new Thickness(12, 0, 12, 0);
            bannerText.TextWrapping = TextWrapping.Wrap;
            bg.Children.Add(Ui.At(bannerText, 0, 1));
            bannerBtn = Ui.Btn("", Theme.Accent, DoRestart);
            bg.Children.Add(Ui.At(bannerBtn, 0, 2));
            banner = new Border { Background = Theme.Banner, BorderBrush = Theme.BannerBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 10, 10, 10), Margin = new Thickness(32, 0, 32, 12), Child = bg, Visibility = Visibility.Collapsed };
            contentHost.Children.Add(Ui.At(banner, 3, 0));

            // Плашка «доступно обновление»
            var ug = Ui.Cols("auto", "*", "auto", "auto", "auto");
            ug.Children.Add(Ui.At(new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(9), Background = Theme.Accent, Child = Ui.Icon("", 14, Theme.OnAccent) }, 0, 0));
            updText = Ui.Txt("", 13, Theme.Text);
            updText.Margin = new Thickness(12, 0, 12, 0);
            updText.TextWrapping = TextWrapping.Wrap;
            ug.Children.Add(Ui.At(updText, 0, 1));
            var notes = Ui.Btn(L.T("Что нового"), Theme.Second, ShowUpdateNotes);
            notes.Margin = new Thickness(0, 0, 8, 0);
            ug.Children.Add(Ui.At(notes, 0, 2));
            updBtn = Ui.Btn(L.Pick("Обновить", "Update"), Theme.Accent, () => InstallUpdate(found, false));
            updBtn.Margin = new Thickness(0, 0, 8, 0);
            ug.Children.Add(Ui.At(updBtn, 0, 3));
            ug.Children.Add(Ui.At(Ui.Btn(L.T("Позже"), Theme.Second, () => updBar.Visibility = Visibility.Collapsed), 0, 4));
            updBar = new Border { Background = Theme.Banner, BorderBrush = Theme.BannerBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 10, 10), Margin = new Thickness(32, 0, 32, 12), Child = ug, Visibility = Visibility.Collapsed };
            // Плашка «Windows вернула настройки»
            var rg = Ui.Cols("auto", "*", "auto", "auto", "auto");
            rg.Children.Add(Ui.At(new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(9), Background = Theme.Yellow, Child = Ui.Icon("", 14, Brushes.Black) }, 0, 0));
            resetText = Ui.Txt("", 13, Theme.Text);
            resetText.Margin = new Thickness(12, 0, 12, 0);
            resetText.TextWrapping = TextWrapping.Wrap;
            rg.Children.Add(Ui.At(resetText, 0, 1));
            var rBack = Ui.Btn(L.T("Вернуть…"), Theme.Accent, RestoreReset);
            rBack.Margin = new Thickness(0, 0, 8, 0);
            rg.Children.Add(Ui.At(rBack, 0, 2));
            var rForget = Ui.Btn(L.T("Не следить"), Theme.Second, ForgetReset);
            rForget.Margin = new Thickness(0, 0, 8, 0);
            rForget.ToolTip = L.T("Больше не напоминать про эти твики — оставить как сейчас");
            rg.Children.Add(Ui.At(rForget, 0, 3));
            rg.Children.Add(Ui.At(Ui.Btn(L.T("Позже"), Theme.Second, () => { resetSteps = null; resetBar.Visibility = Visibility.Collapsed; }), 0, 4));
            resetBar = new Border { Background = Theme.Banner, BorderBrush = Theme.BannerBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 10, 10), Margin = new Thickness(32, 0, 32, 12), Child = rg, Visibility = Visibility.Collapsed };

            var bars = new StackPanel();
            bars.Children.Add(updBar);
            bars.Children.Add(resetBar);
            contentHost.Children.Add(Ui.At(bars, 2, 0));
            if (resetSteps != null && resetSteps.Count > 0) ShowResetBar(false);   // окно пересобрано (смена темы)

            content = new StackPanel { Margin = new Thickness(32, 0, 26, 28) };
            scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = content, Focusable = false, Padding = new Thickness(0, 0, 6, 0) };
            contentHost.Children.Add(Ui.At(scroll, 4, 0));
            rootGrid.Children.Add(contentHost);

            // ── Боковое меню ──
            rail = BuildRail();
            rootGrid.Children.Add(rail);

            Content = rootGrid;
            OnState();
        }

        Button CaptionBtn(string glyph, string style, Action click)
        {
            var b = new Button { Style = Theme.S(style), Content = Ui.Icon(glyph, 10, Theme.Text) };
            b.Click += (s, e) => click();
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            return b;
        }

        Border BuildRail()
        {
            bool wide = Theme.Cfg.Menu == "expanded";
            var dock = new DockPanel { LastChildFill = true };

            // Логотип
            var logo = new Grid { Height = 64 };
            logo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RailNarrow) });
            logo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var mark = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(11), Background = Theme.LogoBrush(), Child = Ui.Icon("", 17, Brushes.White) };
            logo.Children.Add(Ui.At(mark, 0, 0));
            var name = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontFamily = Theme.Display, FontSize = 19 };
            name.Inlines.Add(new System.Windows.Documents.Run("btw") { FontWeight = FontWeights.Bold, Foreground = Theme.Text });
            name.Inlines.Add(new System.Windows.Documents.Run("Tweaker") { Foreground = Theme.Sub });
            logo.Children.Add(Ui.At(name, 0, 1));
            DockPanel.SetDock(logo, Dock.Top);
            dock.Children.Add(logo);

            // Внизу: «Параметры» и версия
            var bottom = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            bottom.Children.Add(NavItem(Entries.Count - 1));
            DockPanel.SetDock(bottom, Dock.Bottom);
            dock.Children.Add(bottom);

            var nav = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            for (int i = 0; i < Entries.Count - 1; i++)
            {
                if (Entries[i].Key == "journal") nav.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(18, 6, 18, 6) });
                nav.Children.Add(NavItem(i));
                if (Entries[i].Key == "favorites") nav.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(18, 6, 18, 6) });
            }
            dock.Children.Add(new ScrollViewer { Content = nav, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Focusable = false });

            var r = new Border
            {
                Width = wide ? RailWide : RailNarrow, HorizontalAlignment = HorizontalAlignment.Left,
                Background = Theme.Side, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 1, 0),
                ClipToBounds = true, Child = dock
            };
            Panel.SetZIndex(r, 10);

            if (Theme.Cfg.Menu == "hover")
            {
                var shadow = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0, Color = Colors.Black };
                r.Effect = shadow;
                r.MouseEnter += (s, e) =>
                {
                    r.BeginAnimation(WidthProperty, new DoubleAnimation(RailWide, Theme.Anim) { BeginTime = TimeSpan.FromMilliseconds(Theme.Cfg.Animations ? 120 : 0), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    shadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, new DoubleAnimation(Theme.Light ? 0.18 : 0.5, Theme.Anim));
                };
                r.MouseLeave += (s, e) =>
                {
                    r.BeginAnimation(WidthProperty, new DoubleAnimation(RailNarrow, Theme.Anim) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    shadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, new DoubleAnimation(0, Theme.Anim));
                };
            }
            return r;
        }

        Border NavItem(int i)
        {
            var en = Entries[i];
            var g = new Grid { Height = 42 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RailNarrow - 16) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RailWide - RailNarrow) });
            var icon = Ui.Icon(en.Icon, 16, Theme.Text);
            g.Children.Add(Ui.At(icon, 0, 0));
            var label = Ui.Txt(en.Title, 14, Theme.Text);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.Margin = new Thickness(10, 0, 0, 0);   // в узком меню подпись целиком за краем
            g.Children.Add(Ui.At(label, 0, 1));
            var bar = new Border { Width = 3, Height = 18, CornerRadius = new CornerRadius(2), Background = Theme.Accent, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0), Visibility = Visibility.Hidden };
            g.Children.Add(Ui.At(bar, 0, 0));

            var b = new Border { Child = g, CornerRadius = new CornerRadius(10), Margin = new Thickness(8, 2, 8, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent, Tag = new object[] { icon, label, bar } };
            if (Theme.Cfg.Menu == "icons") b.ToolTip = en.Title;
            b.MouseEnter += (s, e) => { if (!IsCurrent(i)) b.Background = Theme.Second; };
            b.MouseLeave += (s, e) => UpdateNav();
            b.MouseLeftButtonUp += (s, e) => Navigate(i);
            navItems.Add(b);
            return b;
        }

        bool IsCurrent(int i) { return i == current && search.Text.Length == 0; }

        void UpdateNav()
        {
            // navItems: сначала разделы (0..N-2), «Параметры» добавлен первым при построении нижней части
            foreach (var b in navItems)
            {
                int i = IndexOf(b);
                bool on = IsCurrent(i);
                var parts = (object[])b.Tag;
                b.Background = on ? Theme.NavActive : Brushes.Transparent;
                ((TextBlock)parts[0]).Foreground = on ? Theme.Accent : Theme.Text;
                ((TextBlock)parts[1]).FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
                ((Border)parts[2]).Visibility = on ? Visibility.Visible : Visibility.Hidden;
            }
        }

        int IndexOf(Border b)
        {
            // «Параметры» строятся раньше остальных (нижняя часть), поэтому ищем по подписи
            var label = (TextBlock)((object[])b.Tag)[1];
            for (int i = 0; i < Entries.Count; i++) if (Entries[i].Title == label.Text) return i;
            return -1;
        }

        void OnState()
        {
            if (rootGrid == null) return;
            // Развёрнутое окно с WindowChrome выходит за край экрана — компенсируем отступом
            rootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            if (maxGlyph != null) maxGlyph.Text = WindowState == WindowState.Maximized ? "" : "";
        }

        // ───────── Навигация ─────────

        public void Navigate(int i)
        {
            if (search.Text.Length > 0)
            {
                current = i;
                search.Text = "";   // очистка поиска сама перерисует страницу
                scroll.ScrollToTop();
                return;
            }
            ShowPage(i);
        }

        // В каком разделе твик (для журнала)
        public string SectionOf(Tweak t)
        {
            foreach (var s in Sections)
                foreach (var g in s.Groups)
                    if (g.Tweaks.Contains(t)) return s.Title;
            return L.T("Выпиливание");   // Teams — карточка из «Выпиливания»
        }

        public void NavigateKey(string key)
        {
            for (int i = 0; i < Entries.Count; i++) if (Entries[i].Key == key) { Navigate(i); return; }
        }

        public void NavigateSection(Section s)
        {
            for (int i = 0; i < Entries.Count; i++) if (Entries[i].Sec == s) { Navigate(i); return; }
        }

        public void ShowPage(int i)
        {
            if (i < 0 || i >= Entries.Count) i = 0;   // неверный номер — на «Главную»
            current = i;
            Render(true);
            scroll.ScrollToTop();
        }

        static int Count(Section s)
        {
            int n = 0;
            foreach (var g in s.Groups) n += g.Tweaks.Count;
            return n;
        }

        public static string Tweaks(int n)
        {
            return L.N(n, "твик", "твика", "твиков", "tweak", "tweaks");
        }

        // Перерисовать страницу или результаты поиска
        void Render(bool animate)
        {
            content.Children.Clear();
            cards.Clear();
            UpdateNav();
            string q = search.Text.Trim();

            if (q.Length > 0)
            {
                pageTitle.Text = L.T("Поиск");
                int found = 0;
                foreach (var sec in Sections)
                    foreach (var g in sec.Groups)
                    {
                        var hits = g.Tweaks.FindAll(t => Has(t.Title, q) || Has(t.Description, q) || Has(g.Title, q));
                        if (hits.Count == 0) continue;
                        AddGroup(sec.Title + " · " + g.Title, hits);
                        found += hits.Count;
                    }
                pageSub.Text = found > 0 ? L.T("Найдено: ") + Tweaks(found) : "";
                if (found == 0) content.Children.Add(Placeholder(L.T("Ничего не найдено"), L.T("Попробуйте другое слово.")));
                return;
            }

            var en = Entries[current];
            if (en.Key == "home")
            {
                pageTitle.Text = L.T("Привет, ") + Environment.UserName + "!";
                pageSub.Text = "btwTweaker " + AppMain.Version + " · " + Sys.OsName;
                content.Children.Add(new HomePage(this));
            }
            else if (en.Key == "favorites")
            {
                pageTitle.Text = L.T("Мои твики");
                int total = 0;
                foreach (var sec in Sections)
                    foreach (var g in sec.Groups)
                    {
                        var favs = g.Tweaks.FindAll(Favorites.Is);
                        if (favs.Count == 0) continue;
                        AddGroup(sec.Title + " · " + g.Title, favs);
                        total += favs.Count;
                    }
                pageSub.Text = total > 0 ? Tweaks(total) + L.T(" в избранном") : L.T("Избранные твики из всех разделов");
                if (total == 0) content.Children.Add(Placeholder(L.T("Пока пусто"), L.T("Нажмите на звёздочку у любого твика, чтобы добавить его сюда."), ""));
            }
            else if (en.Key == "journal")
            {
                pageTitle.Text = L.T("Журнал");
                pageSub.Text = L.T("Что и когда менял btwTweaker — с откатом по одному действию");
                content.Children.Add(new JournalPage(this));
            }
            else if (en.Key == "settings")
            {
                pageTitle.Text = L.T("Параметры");
                pageSub.Text = L.T("Внешний вид, резервная копия и сведения о программе");
                content.Children.Add(new SettingsPage(this));
            }
            else
            {
                var sec = en.Sec;
                pageTitle.Text = sec.Title;
                if (sec.Page == "power")
                {
                    pageSub.Text = L.T("Схемы электропитания Windows");
                    content.Children.Add(new PowerPage(this));
                }
                else if (sec.Page == "startup")
                {
                    pageSub.Text = L.T("Программы, задания и службы, которые запускаются вместе с Windows");
                    content.Children.Add(new StartupPage(this));
                }
                else if (sec.Page == "debloat")
                {
                    pageSub.Text = L.T("Встроенные программы Windows, OneDrive и Edge");
                    content.Children.Add(new DebloatPage(this));
                }
                else if (sec.Page == "nvidia")
                {
                    pageSub.Text = L.T("Настройки драйвера видеокарты");
                    content.Children.Add(new NvidiaPage(this));
                }
                else if (sec.Page == "clean")
                {
                    pageSub.Text = L.T("Временные файлы, кэши и следы активности");
                    content.Children.Add(new CleanPage(this));
                }
                else
                {
                    pageSub.Text = Tweaks(Count(sec)) + L.T(" · групп: ") + sec.Groups.Count;
                    foreach (var g in sec.Groups) AddGroup(g.Title, g.Tweaks);
                }
            }
            if (animate) Ui.FadeIn(content);
        }

        static bool Has(string s, string q) { return s != null && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0; }

        void AddGroup(string title, List<Tweak> tweaks)
        {
            var h = Ui.Txt(title, 15, Theme.Text, FontWeights.SemiBold);
            h.Margin = new Thickness(2, content.Children.Count == 0 ? 0 : 18, 0, 10);
            content.Children.Add(h);
            foreach (var t in tweaks)
            {
                var c = new TweakCard(t, this);
                cards.Add(c);
                content.Children.Add(c);
            }
        }

        FrameworkElement Placeholder(string title, string text)
        {
            return Placeholder(title, text, null);
        }

        FrameworkElement Placeholder(string title, string text, string glyph)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 90, 0, 0) };
            sp.Children.Add(Ui.Icon(glyph ?? "", 38, Theme.Sub));
            var t = Ui.Txt(title, 17, Theme.Text, FontWeights.SemiBold);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 16, 0, 6);
            sp.Children.Add(t);
            var d = Ui.Txt(text, 13, Theme.Sub);
            d.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(d);
            return sp;
        }

        // ───────── Оформление ─────────

        // Применить новые настройки внешнего вида: пересчитать палитру и пересобрать окно
        public void Rebuild()
        {
            string q = search != null ? search.Text : "";
            Theme.Apply();
            BuildUi();
            ApplyBackdrop();
            var p = pending;
            pending = Restart.None;
            if (p != Restart.None) NeedRestart(p);
            if (found != null) ShowUpdateBar();
            ShowPage(current);
            if (q.Length > 0) search.Text = q;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [StructLayout(LayoutKind.Sequential)]
        struct MARGINS { public int L, R, T, B; }

        [DllImport("dwmapi.dll")]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);

        // Тёмная/светлая рамка, скруглённые углы и фон Mica (Windows 11)
        void ApplyBackdrop()
        {
            var h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero) return;
            int dark = Theme.Light ? 0 : 1;
            if (DwmSetWindowAttribute(h, 20, ref dark, 4) != 0) DwmSetWindowAttribute(h, 19, ref dark, 4);
            int round = 2;
            DwmSetWindowAttribute(h, 33, ref round, 4);
            var src = HwndSource.FromHwnd(h);
            if (Sys.Build >= 22621)
            {
                int backdrop = Theme.Mica ? 2 : 1;
                DwmSetWindowAttribute(h, 38, ref backdrop, 4);
            }
            var m = Theme.Mica ? new MARGINS { L = -1, R = -1, T = -1, B = -1 } : new MARGINS();
            DwmExtendFrameIntoClientArea(h, ref m);
            if (src != null && src.CompositionTarget != null)
                src.CompositionTarget.BackgroundColor = Theme.Mica ? Colors.Transparent : Theme.BaseColor;
        }

        // ───────── Перезапуск / перезагрузка ─────────

        public void NeedRestart(Restart r)
        {
            if (r <= pending) return;
            pending = r;
            if (r == Restart.Explorer) { bannerText.Text = L.T("Чтобы изменения применились, нужно перезапустить Проводник."); bannerBtn.Content = L.T("Перезапустить Проводник"); }
            else if (r == Restart.SignOut) { bannerText.Text = L.T("Часть изменений применится после выхода из системы или перезагрузки."); bannerBtn.Content = L.T("Перезагрузить"); }
            else if (r == Restart.Reboot) { bannerText.Text = L.T("Часть изменений применится только после перезагрузки компьютера."); bannerBtn.Content = L.T("Перезагрузить"); }
            banner.Visibility = Visibility.Visible;
            Ui.FadeIn(banner);
        }

        void DoRestart()
        {
            if (pending == Restart.Explorer)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try { Sys.RestartExplorer(); } finally { Mouse.OverrideCursor = null; }
                pending = Restart.None;
                banner.Visibility = Visibility.Collapsed;
                return;
            }
            if (Dialog.Ask(this, L.T("Перезагрузить компьютер сейчас?"), L.T("Сохраните открытые файлы в других программах — компьютер перезагрузится сразу."), L.T("Перезагрузить"), L.T("Позже"), true))
                Sys.Reboot();
        }

        // ───────── Точка восстановления и откат ─────────

        public async void CreateRestorePoint(bool firstRun)
        {
            if (!firstRun && !Dialog.Ask(this, L.T("Создать точку восстановления?"), L.T("Windows запомнит текущее состояние системы. Если что-то пойдёт не так, к нему можно вернуться через «Восстановление системы». Это займёт до минуты."), L.T("Создать"), L.T("Отмена")))
                return;
            Mouse.OverrideCursor = Cursors.Wait;
            IsEnabled = false;
            bool ok = false;
            string msg;
            try { msg = await Task.Run(() => Sys.CreateRestorePoint(out ok)); }
            catch (Exception ex) { msg = ex.Message; }
            Mouse.OverrideCursor = null;
            IsEnabled = true;
            Dialog.Info(this, ok ? L.T("Готово") : L.T("Точка восстановления"), msg);
        }

        public void RollbackAll()
        {
            int n = Backup.Count;
            if (n == 0)
            {
                Dialog.Info(this, L.T("Нечего откатывать"), L.T("btwTweaker ещё ничего не менял на этом компьютере."));
                return;
            }
            if (!Dialog.Ask(this, L.T("Откатить все изменения?"), L.T("Все настройки, которые менял btwTweaker (") + n + L.T(" знач.), вернутся к исходным значениям — как было до первого изменения."), L.T("Откатить"), L.T("Отмена"), true))
                return;
            var errors = new List<string>();
            int done;
            Mouse.OverrideCursor = Cursors.Wait;
            try { done = Backup.RestoreAll(errors); Watch.Clear(); }
            finally { Mouse.OverrideCursor = null; }
            Render(false);
            NeedRestart(Restart.Reboot);
            Dialog.Info(this, L.T("Откат выполнен"), L.T("Восстановлено значений: ") + done + "." + (errors.Count > 0 ? L.T("\n\nОшибки:\n") + string.Join("\n", errors) : "") + L.T("\n\nДля полного применения перезагрузите компьютер."));
        }

        public void OpenBackupFolder()
        {
            Directory.CreateDirectory(Backup.Dir);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Backup.Dir + "\"") { UseShellExecute = true });
        }

        // ───────── Профили ─────────

        public void SaveProfile()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = L.T("Сохранить профиль btwTweaker"),
                Filter = L.T("Профиль btwTweaker (*") + Profiles.Ext + ")|*" + Profiles.Ext,
                FileName = "btwTweaker-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyy-MM-dd") + Profiles.Ext,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                ProfileFile p;
                Mouse.OverrideCursor = Cursors.Wait;
                try { p = Profiles.Capture(Sections, Path.GetFileNameWithoutExtension(dlg.FileName)); }
                finally { Mouse.OverrideCursor = null; }
                Profiles.Save(dlg.FileName, p);
                Dialog.Info(this, L.T("Профиль сохранён"), L.T("Записано ") + Tweaks(p.Tweaks.Count) + L.T(" с текущим состоянием.\n\n") + dlg.FileName +
                    L.T("\n\nПеренесите файл на другой компьютер и откройте его в btwTweaker: Параметры → «Применить профиль»."));
            }
            catch (Exception ex) { Dialog.Info(this, L.T("Не получилось сохранить профиль"), ex.Message); }
        }

        public void ApplyProfile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = L.T("Открыть профиль btwTweaker"),
                Filter = L.T("Профиль btwTweaker (*") + Profiles.Ext + ")|*" + Profiles.Ext + L.T("|Все файлы (*.*)|*.*"),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog(this) != true) return;
            ApplyProfile(dlg.FileName);
        }

        public async void ApplyProfile(string path)
        {
            ProfileFile p;
            List<ProfileStep> steps;
            var notFound = new List<string>();
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                p = Profiles.Load(path);
                var pf = p;
                steps = await Task.Run(() => Profiles.Diff(Sections, pf, notFound));
            }
            catch (Exception ex) { Mouse.OverrideCursor = null; Dialog.Info(this, L.T("Не получилось открыть профиль"), ex.Message); return; }
            Mouse.OverrideCursor = null;

            string head = L.T("Профиль «") + p.Name + L.T("»") + (string.IsNullOrEmpty(p.Created) ? "" : L.T(" от ") + p.Created) +
                (string.IsNullOrEmpty(p.Pc) ? "" : L.T(", компьютер ") + p.Pc) + ".";
            string extra = notFound.Count > 0 ? L.T("\n\nВ этой версии программы нет твиков: ") + notFound.Count + L.T(" — они пропущены.") : "";
            if (steps.Count == 0)
            {
                Dialog.Info(this, L.T("Всё уже как в профиле"), head + L.T("\n\nНа этом компьютере все твики из профиля уже стоят так же.") + extra);
                return;
            }

            int skipped = steps.FindAll(s => s.Skip != null).Count;
            string text = head + L.T(" Отличается от этого компьютера: ") + (steps.Count - skipped) + (skipped > 0 ? L.T(", недоступно здесь: ") + skipped : "") + "." +
                L.T("\nОтметьте, что применить. Пункты, которые снижают защиту или требуют внимания, не отмечены — включите их сами, если нужно.") +
                L.T("\nКаждое изменение попадёт в «Журнал», его можно откатить.") + extra;
            ApplySteps(steps, p.Name, L.T("Применить профиль"), text, L.T("Применить"), null);
        }

        // Окно с галочками и применение выбранных шагов (профиль или возврат сброшенных твиков).
        // name — имя профиля для журнала (null — возврат сброса: тогда рискованные пункты отмечены, их выбирали сами).
        async void ApplySteps(List<ProfileStep> steps, string name, string title, string text, string yes, Action done)
        {
            bool reset = name == null;
            var items = new List<PickItem>();
            foreach (var st in steps)
            {
                var it = new PickItem { Group = st.Section, Text = st.Tweak.Title, Detail = st.From + " → " + st.To, Tag = st, Checked = true };
                if (st.Skip != null) { it.Enabled = false; it.Checked = false; it.Note = L.T("Пропуск: ") + st.Skip; }
                else if (st.Risky)
                {
                    it.Checked = reset;
                    it.Note = st.Tweak.Security != null ? L.T("Снижает защиту: ") + st.Tweak.Security : L.T("Внимание: ") + st.Tweak.Caution;
                    it.NoteColor = st.Tweak.Security != null ? Theme.Red : Theme.Yellow;
                }
                else if (st.Index >= 0)
                {
                    string ask = ((ChoiceTweak)st.Tweak).Confirm(st.Index);
                    if (ask != null) { it.Checked = reset; it.Note = ask; it.NoteColor = Theme.Yellow; }
                }
                items.Add(it);
            }
            if (!Dialog.Pick(this, title, text, items, yes)) return;
#if TESTHOOKS
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BTW_SNAP"))) return;   // в тестах ничего не применяем
#endif

            var chosen = new List<ProfileStep>();
            foreach (var it in items) if (it.Checked && it.Enabled) chosen.Add((ProfileStep)it.Tag);
            if (chosen.Count == 0) return;

            var errors = new List<string>();
            Restart need = Restart.None;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                await Task.Run(() =>
                {
                    foreach (var st in chosen)
                    {
                        var s = st;
                        try
                        {
                            Journal.Do(s.Section, (reset ? L.T("Возврат после сброса: ") : L.T("Профиль «") + name + L.T("»: ")) + s.Tweak.Title + " → " + s.To, () => Profiles.Apply(s), s.Tweak);
                            if (s.Tweak.Restart > need) need = s.Tweak.Restart;
                        }
                        catch (Exception ex) { errors.Add(s.Tweak.Title + ": " + ex.Message); }
                    }
                });
            }
            finally { Mouse.OverrideCursor = null; }
            if (done != null) done();
            Render(false);
            if (need != Restart.None) NeedRestart(need);
            int ok = chosen.Count - errors.Count;
            Dialog.Info(this, errors.Count == 0 ? L.T("Готово") : L.T("Готово, но не всё"),
                L.T("Изменено: ") + Tweaks(ok) + "." + (errors.Count > 0 ? L.T("\n\nНе получилось:\n") + string.Join("\n", errors) : "") +
                L.T("\n\nОткатить можно в «Журнале» — по одному или всё сразу."));
        }

        // ───────── Проверка сброса твиков ─────────

        // При запуске: не вернула ли Windows (или другая программа) настройки, которые меняли в btwTweaker
        async void CheckReset()
        {
            // Сборка Windows — чтобы сказать, что причина, скорее всего, обновление
            string build = Sys.Build + "." + Sys.Ubr;
            string last = Sys.Pref("LastBuild");
#if TESTHOOKS
            string fake = Environment.GetEnvironmentVariable("BTW_SNAP_LASTBUILD");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BTW_SNAP"))) last = fake; else
#endif
            Sys.SetPref("LastBuild", build);
            if (Sys.Pref("ResetCheck") == "0") return;
            List<ProfileStep> steps;
            try { steps = await Task.Run(() => Watch.Check(Sections)); }
            catch (Exception ex) { AppMain.Log(L.T("Проверка сброса"), ex); return; }
            if (steps.Count == 0) return;
            resetSteps = steps;
            resetBuildNote = !string.IsNullOrEmpty(last) && last != build ? last + " → " + build : null;
            ShowResetBar(true);
        }

        void ShowResetBar(bool animate)
        {
            int n = resetSteps.Count;
            string what = L.N(n, "настройку", "настройки", "настроек", "setting", "settings");
            if (L.En)
                resetText.Text = resetBuildNote != null
                    ? "After a Windows update (build " + resetBuildNote + "), " + what + " changed in btwTweaker " + (n == 1 ? "was" : "were") + " reverted."
                    : "Windows or another program reverted " + what + " you changed in btwTweaker.";
            else
                resetText.Text = resetBuildNote != null
                    ? "После обновления Windows (сборка " + resetBuildNote + ") вернулось назад " + what + " btwTweaker."
                    : "Windows или другая программа вернула назад " + what + ", которые вы меняли в btwTweaker.";
            resetBar.Visibility = Visibility.Visible;
            if (animate) Ui.FadeIn(resetBar);
        }

        void RestoreReset()
        {
            if (resetSteps == null) return;
            var steps = resetSteps;
            ApplySteps(steps, null, L.T("Вернуть настройки"),
                L.T("Эти твики сейчас стоят не так, как вы выбирали в btwTweaker. Отметьте, что вернуть. Каждое изменение попадёт в «Журнал»."),
                L.T("Вернуть"), () => { resetSteps = null; resetBar.Visibility = Visibility.Collapsed; });
        }

        void ForgetReset()
        {
            if (resetSteps == null) return;
            if (!Dialog.Ask(this, L.T("Не следить за этими твиками?"), L.T("btwTweaker перестанет напоминать о них и оставит как сейчас. Если снова измените твик в программе, слежение включится заново."), L.T("Не следить"), L.T("Отмена"))) return;
            var ids = new List<string>();
            foreach (var s in resetSteps) ids.Add(s.Tweak.Id);
            Watch.Forget(ids);
            resetSteps = null;
            resetBar.Visibility = Visibility.Collapsed;
        }

        void OnFirstShow()
        {
#if TESTHOOKS
            if (Snap()) return;
#endif
            if (Sys.Pref("UpdateCheck") != "0") CheckForUpdates(false);
            CheckReset();
            if (Sys.Pref("FirstRunDone") == "1") return;
            Sys.SetPref("FirstRunDone", "1");
            if (Dialog.Ask(this, L.T("Добро пожаловать в btwTweaker"),
                L.T("Перед изменениями рекомендуем создать точку восстановления — так любую настройку можно будет вернуть даже без программы.\n\nКроме того, btwTweaker сам запоминает исходные значения: кнопка «Откатить всё» вернёт всё как было."),
                L.T("Создать точку"), L.T("Пропустить")))
                CreateRestorePoint(true);
        }

        // ───────── Обновления ─────────

        // manual — нажата кнопка «Проверить сейчас»: тогда сообщаем и об отсутствии обновлений, и об ошибках
        public async void CheckForUpdates(bool manual)
        {
            if (updating) return;
            if (manual) Mouse.OverrideCursor = Cursors.Wait;
            ReleaseInfo r = null;
            Exception err = null;
            try { r = await Task.Run(() => Updater.Latest()); }
            catch (Exception ex) { err = ex; }
            if (manual) Mouse.OverrideCursor = null;

            if (err != null)
            {
                AppMain.Log(L.T("Проверка обновлений"), err);
                if (manual) Dialog.Info(this, L.T("Не удалось проверить обновления"), L.T("GitHub недоступен или не отвечает.\n\n") + err.Message);
                return;
            }
            if (r.Version <= Updater.Current)
            {
                found = null;
                if (updBar != null) updBar.Visibility = Visibility.Collapsed;
                if (manual) Dialog.Info(this, L.T("Обновлений нет"), L.T("У вас последняя версия btwTweaker ") + Updater.Current.ToString(3) + ".");
                return;
            }
            found = r;
            ShowUpdateBar();
            if (!manual && Sys.Pref("UpdateAuto") == "1") InstallUpdate(r, true);
        }

        void ShowUpdateBar()
        {
            updText.Text = L.T("Доступна новая версия btwTweaker ") + found.Version.ToString(3) + L.T(" (у вас ") + Updater.Current.ToString(3) + ").";
            updBtn.IsEnabled = !updating;
            if (updBar.Visibility != Visibility.Visible)
            {
                updBar.Visibility = Visibility.Visible;
                Ui.FadeIn(updBar);
            }
        }

        void ShowUpdateNotes()
        {
            if (found == null) return;
            string notes = string.IsNullOrWhiteSpace(found.Notes) ? L.T("Описание этой версии не указано.") : PlainNotes(found.Notes);
            Dialog.Info(this, L.T("Что нового в btwTweaker ") + found.Version.ToString(3), notes);
        }

        // Markdown из описания релиза — в простой текст
        static string PlainNotes(string md)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var raw in md.Replace("\r", "").Split('\n'))
            {
                string l = raw.TrimEnd();
                if (l.StartsWith("#")) l = l.TrimStart('#', ' ');
                else if (l.StartsWith("- ") || l.StartsWith("* ")) l = "•  " + l.Substring(2);
                l = l.Replace("**", "").Replace("`", "");
                sb.AppendLine(l);
            }
            return sb.ToString().Trim();
        }

        // Скачать и установить; silent — без вопроса (включено «Устанавливать автоматически»)
        public async void InstallUpdate(ReleaseInfo r, bool silent)
        {
            if (r == null || updating) return;
#if TESTHOOKS
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BTW_SNAP"))) return;   // снимки никогда ничего не ставят
#endif
            string cur = Updater.Current.ToString(3), next = r.Version.ToString(3);
            if (!silent && !Dialog.Ask(this, L.T("Обновить btwTweaker до ") + next + "?",
                L.T("Сейчас установлена версия ") + cur + L.T(". Программа скачает новую версию с GitHub, проверит контрольную сумму и перезапустится.\n\nРезервная копия и настройки сохранятся, прошлую версию можно будет вернуть в «Параметрах»."),
                L.T("Обновить"), L.T("Отмена")))
                return;
            updating = true;
            updBtn.IsEnabled = false;
            updText.Text = L.T("Скачиваю btwTweaker ") + next + "…";
            Exception err = null;
            try { await Task.Run(() => Updater.DownloadAndInstall(r, Updater.ExePath)); }
            catch (Exception ex) { err = ex; }
            if (err != null)
            {
                updating = false;
                AppMain.Log(L.T("Обновление"), err);
                ShowUpdateBar();
                Dialog.Info(this, L.T("Не удалось обновить"), err.Message);
                return;
            }
            try { Updater.StartNew(); }
            catch (Exception ex)
            {
                Dialog.Info(this, L.T("Обновление установлено"), L.T("Новая версия уже на месте, но перезапустить программу не получилось — откройте её заново.\n\n") + ex.Message);
            }
            Application.Current.Shutdown();
        }

        public void RollbackVersion()
        {
            string prev = Updater.PrevVersion();
            if (prev == null) return;
            if (!Dialog.Ask(this, L.T("Вернуть версию ") + prev + "?", L.T("Программа заменит себя прошлой версией и перезапустится. Текущую версию потом тоже можно будет вернуть."), L.T("Вернуть"), L.T("Отмена")))
                return;
            try { Updater.Rollback(); Updater.StartNew(); }
            catch (Exception ex) { Dialog.Info(this, L.T("Не получилось"), ex.Message); return; }
            Application.Current.Shutdown();
        }

#if TESTHOOKS
        // Тестовый режим: BTW_SNAP=путь.png — снимок окна и выход.
        // BTW_SNAP_PAGE=номер пункта меню (0 — Главная), BTW_SNAP_SEARCH, BTW_SNAP_EXPAND=1, BTW_SNAP_BANNER=1,
        // BTW_SNAP_RAIL=1 — раскрыть меню, BTW_SNAP_UI=тема;акцент;фон;меню
        bool Snap()
        {
            string path = Environment.GetEnvironmentVariable("BTW_SNAP");
            if (string.IsNullOrEmpty(path)) return false;
            int page;
            if (int.TryParse(Environment.GetEnvironmentVariable("BTW_SNAP_PAGE"), out page)) ShowPage(page);
            string q = Environment.GetEnvironmentVariable("BTW_SNAP_SEARCH");
            if (!string.IsNullOrEmpty(q)) search.Text = q;
            if (Environment.GetEnvironmentVariable("BTW_SNAP_EXPAND") == "1") foreach (var c in cards) c.ToggleDesc(true);
            if (Environment.GetEnvironmentVariable("BTW_SNAP_BANNER") == "1") NeedRestart(Restart.Reboot);
            if (Environment.GetEnvironmentVariable("BTW_SNAP_RAIL") == "1") { rail.Width = RailWide; rail.Effect = null; }
            string favs = Environment.GetEnvironmentVariable("BTW_SNAP_FAVS");
            if (!string.IsNullOrEmpty(favs)) { Favorites.TestSet(favs); ShowPage(current); }
            if (Environment.GetEnvironmentVariable("BTW_SNAP_UPDATE") == "1") CheckForUpdates(false);
            string prof = Environment.GetEnvironmentVariable("BTW_SNAP_PROFILE");   // только показать диалог, применять нечего: снимок закроет программу
            if (!string.IsNullOrEmpty(prof)) ApplyProfile(prof);
            if (Environment.GetEnvironmentVariable("BTW_SNAP_RESET") == "1") CheckReset();          // нужен BTW_DATA_DIR с watch.json
            if (Environment.GetEnvironmentVariable("BTW_SNAP_RESET") == "2") { CheckReset(); Dispatcher.BeginInvoke(new Action(async () => { await Task.Delay(1500); RestoreReset(); })); }
            int delay;
            int.TryParse(Environment.GetEnvironmentVariable("BTW_SNAP_DELAY"), out delay);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(delay, 0) + 0.6) };
            timer.Tick += (s, e) => { timer.Stop(); SaveSnap(path); };
            timer.Start();
            return true;
        }

        void SaveSnap(string path)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var el = (FrameworkElement)Content;
                var dpi = VisualTreeHelper.GetDpi(this);
                var bmp = new RenderTargetBitmap((int)(el.ActualWidth * dpi.DpiScaleX), (int)(el.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    // Mica на снимке не видна — подкладываем базовый цвет
                    dc.DrawRectangle(new SolidColorBrush(Theme.Mica ? Theme.BaseColor : Colors.Transparent), null, new Rect(0, 0, el.ActualWidth, el.ActualHeight));
                    dc.DrawRectangle(new VisualBrush(el), null, new Rect(0, 0, el.ActualWidth, el.ActualHeight));
                }
                bmp.Render(dv);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var f = File.Create(path)) enc.Save(f);
                if (L.En) File.WriteAllLines(path + ".miss.txt", L.Misses(), new System.Text.UTF8Encoding(false));
                Application.Current.Shutdown();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
#endif
    }
}
