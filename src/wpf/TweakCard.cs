using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BtwTweaker
{
    // Карточка твика: значок, название, стрелка, значки предупреждений, состояние и переключатель.
    // Раскрытая карточка: описание, метки, предупреждения, «Что меняется» и принудительное применение.
    public class TweakCard : Border
    {
        readonly Tweak t;
        readonly MainWindow owner;
        readonly TextBlock status = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        readonly StackPanel details = new StackPanel { Margin = new Thickness(0, 12, 4, 4), Visibility = Visibility.Collapsed };
        readonly TextBlock chev;
        ToggleButton sw;
        Button pill;
        TextBlock pillText;
        bool busy, built, changesOpen;

        public TweakCard(Tweak tweak, MainWindow w)
        {
#if TESTHOOKS
            changesOpen = Environment.GetEnvironmentVariable("BTW_SNAP_CHANGES") == "1";
#endif
            t = tweak;
            owner = w;
            CornerRadius = new CornerRadius(14);
            Padding = new Thickness(16, 12, 16, 12);
            Margin = new Thickness(0, 0, 0, 8);
            BorderBrush = Theme.Line;
            BorderThickness = new Thickness(1);
            Ui.Hover(this);

            var g = Ui.Cols("34", "*", "auto");
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var icon = Ui.Icon(t.Icon, 17, t.IconColor != null ? Theme.B(t.IconColor) : Theme.Text);
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(Ui.At(icon, 0, 0));

            // Заголовок: клик раскрывает подробности
            var head = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            head.Children.Add(Ui.Txt(t.Title, 14, Theme.Text));
            chev = Ui.Icon("", 9, Theme.Sub);
            chev.Margin = new Thickness(7, 2, 0, 0);
            chev.RenderTransformOrigin = new Point(0.5, 0.5);
            chev.RenderTransform = new RotateTransform(0);
            head.Children.Add(chev);
            if (t.Security != null) head.Children.Add(Badge("", Theme.Red, L.T("Снижает защиту: ") + t.Security, true));
            if (t.Caution != null) head.Children.Add(Badge("!", Theme.Yellow, L.T("Внимание: ") + t.Caution, false));
            if (t.Restart == Restart.Reboot) head.Children.Add(Badge("", Theme.Sub, L.T("Нужна перезагрузка"), true));
            head.MouseLeftButtonUp += (s, e) => ToggleDesc();
            g.Children.Add(Ui.At(head, 0, 1));

            Grid.SetColumnSpan(details, 2);
            g.Children.Add(Ui.At(details, 1, 1));

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(Star());
            right.Children.Add(status);
            if (t is ToggleTweak)
            {
                status.MinWidth = 76;
                status.TextAlignment = TextAlignment.Right;
                sw = new ToggleButton { Style = Theme.S("Switch") };
                sw.Click += (s, e) => Apply(sw.IsChecked == true);
                right.Children.Add(sw);
            }
            else if (t is ChoiceTweak)
            {
                pillText = Ui.Txt("", 12.5, Theme.AccentText, FontWeights.SemiBold);
                var pc = new StackPanel { Orientation = Orientation.Horizontal };
                pc.Children.Add(pillText);
                var pchev = Ui.Icon("", 8, Theme.AccentText);
                pchev.Margin = new Thickness(6, 1, 0, 0);
                pc.Children.Add(pchev);
                pill = Ui.Btn(pc, Theme.AccentSoft, ShowChoices);
                pill.Padding = new Thickness(10, 5, 10, 5);
                right.Children.Add(pill);
                status.Visibility = Visibility.Collapsed;
            }
            g.Children.Add(Ui.At(right, 0, 2));

            Child = g;
            Refresh();
        }

        // Звёздочка «Мои твики»
        FrameworkElement Star()
        {
            var icon = Ui.Icon("", 15, Theme.Sub);
            var b = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0), Child = icon };
            Action paint = () =>
            {
                bool on = Favorites.Is(t);
                icon.Text = on ? "" : "";
                icon.Foreground = on ? Theme.Yellow : Theme.Sub;
                b.ToolTip = on ? L.T("Убрать из «Моих твиков»") : L.T("Добавить в «Мои твики»");
                b.Opacity = on ? 1 : 0.7;
            };
            paint();
            b.MouseEnter += (s, e) => { b.Background = Theme.Second; b.Opacity = 1; };
            b.MouseLeave += (s, e) => { b.Background = Brushes.Transparent; paint(); };
            b.MouseLeftButtonUp += (s, e) =>
            {
                Favorites.Toggle(t);
                paint();
                b.Opacity = 1;
                if (Theme.Cfg.Animations)
                {
                    var st = new ScaleTransform(1, 1);
                    icon.RenderTransformOrigin = new Point(0.5, 0.5);
                    icon.RenderTransform = st;
                    var a = new DoubleAnimation(1.35, 1, new Duration(TimeSpan.FromMilliseconds(260))) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut } };
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
                }
                e.Handled = true;
            };
            return b;
        }

        FrameworkElement Badge(string glyph, Brush color, string tip, bool icon)
        {
            FrameworkElement el;
            if (icon) el = Ui.Icon(glyph, 14, color);
            else
                el = new Border
                {
                    Background = color, CornerRadius = new CornerRadius(3), Width = 16, Height = 16,
                    Child = new TextBlock { Text = glyph, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = Theme.B("#1A1300"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
            el.Margin = new Thickness(8, 0, 0, 0);
            el.VerticalAlignment = VerticalAlignment.Center;
            el.ToolTip = tip;
            return el;
        }

        // ───────── Раскрытие ─────────

        public void ToggleDesc(bool? open = null)
        {
            bool show = open ?? details.Visibility != Visibility.Visible;
            if (show && !built) BuildDetails();
            details.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ((RotateTransform)chev.RenderTransform).BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(show ? 180 : 0, Theme.Anim) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            if (show) Ui.FadeIn(details);
        }

        void BuildDetails()
        {
            built = true;
            details.Children.Clear();

            // Описание
            if (!string.IsNullOrEmpty(t.Description))
                details.Children.Add(new TextBlock { Text = t.Description, TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Theme.Sub, LineHeight = 20, Margin = new Thickness(0, 0, 40, 0) });

            // Метки
            var chips = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            if (t.Restart == Restart.Reboot) chips.Children.Add(Chip("", L.T("Нужна перезагрузка")));
            else if (t.Restart == Restart.SignOut) chips.Children.Add(Chip("", L.T("После выхода из системы")));
            else if (t.Restart == Restart.Explorer) chips.Children.Add(Chip("", L.T("Перезапуск Проводника")));
            else chips.Children.Add(Chip("", L.T("Применяется сразу")));
            if (t.Win11Only) chips.Children.Add(Chip("", L.T("Только Windows 11")));
            if (t.MinBuild > 0) chips.Children.Add(Chip("", L.T("Windows сборки ") + t.MinBuild + "+"));
            if (t is ChoiceTweak) chips.Children.Add(Chip("", L.T("Вариантов: ") + ((ChoiceTweak)t).Options.Length));
            details.Children.Add(chips);

            // Предупреждения
            if (t.Security != null) details.Children.Add(Callout("", Theme.Red, L.T("Снижает защиту"), Cap(t.Security)));
            if (t.Caution != null) details.Children.Add(Callout("", Theme.Yellow, L.T("Учтите"), Cap(t.Caution)));

            // Что меняется
            List<ChangeInfo> changes = null;
            try { changes = t.Changes(); } catch { }
            if (changes != null && changes.Count > 0) details.Children.Add(ChangesBlock(changes));

            // Принудительное применение
            if (t is ToggleTweak)
            {
                var acts = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
                var on = Ui.IconBtn("", L.T("Принудительно включить"), Theme.Second, Theme.Text, () => Apply(true));
                on.Padding = new Thickness(12, 6, 12, 6);
                on.FontSize = 12.5;
                on.ToolTip = L.T("Записать все значения «Вкл», даже если функция уже включена");
                var off = Ui.IconBtn("", L.T("Принудительно отключить"), Theme.Second, Theme.Text, () => Apply(false));
                off.Padding = new Thickness(12, 6, 12, 6);
                off.FontSize = 12.5;
                off.Margin = new Thickness(8, 0, 0, 0);
                off.ToolTip = L.T("Записать все значения «Откл», даже если функция уже отключена");
                acts.Children.Add(on);
                acts.Children.Add(off);
                details.Children.Add(acts);
            }
        }

        static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1); }

        static Border Chip(string glyph, string text)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var i = Ui.Icon(glyph, 11, Theme.AccentText);
            i.Margin = new Thickness(0, 1, 6, 0);
            sp.Children.Add(i);
            sp.Children.Add(Ui.Txt(text, 12, Theme.AccentText, FontWeights.SemiBold));
            return new Border { Background = Theme.Done, CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 3, 10, 3), Margin = new Thickness(0, 0, 6, 6), Child = sp };
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
                CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 4, 40, 4), Child = g
            };
        }

        // «Что меняется»: заголовок-переключатель и список изменений
        FrameworkElement ChangesBlock(List<ChangeInfo> changes)
        {
            var box = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            var list = new StackPanel { Visibility = changesOpen ? Visibility.Visible : Visibility.Collapsed };

            var hdr = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left };
            hdr.Children.Add(Ui.Icon("", 12, Theme.AccentText));
            var ht = Ui.Txt(L.T("Что меняется"), 13, Theme.AccentText, FontWeights.SemiBold);
            ht.Margin = new Thickness(8, 0, 6, 0);
            hdr.Children.Add(ht);
            hdr.Children.Add(new Border { Background = Theme.Done, CornerRadius = new CornerRadius(7), Padding = new Thickness(6, 0, 6, 1), Child = Ui.Txt(changes.Count.ToString(), 11, Theme.AccentText, FontWeights.SemiBold) });
            var hc = Ui.Icon("", 8, Theme.AccentText);
            hc.Margin = new Thickness(8, 1, 0, 0);
            hc.RenderTransformOrigin = new Point(0.5, 0.5);
            hc.RenderTransform = new RotateTransform(changesOpen ? 180 : 0);
            hdr.Children.Add(hc);
            hdr.MouseLeftButtonUp += (s, e) =>
            {
                changesOpen = !changesOpen;
                list.Visibility = changesOpen ? Visibility.Visible : Visibility.Collapsed;
                ((RotateTransform)hc.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(changesOpen ? 180 : 0, Theme.Anim));
                if (changesOpen) Ui.FadeIn(list);
            };
            box.Children.Add(hdr);

            foreach (var c in changes) list.Children.Add(ChangeRow(c));
            box.Children.Add(list);
            return box;
        }

        static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas");

        FrameworkElement ChangeRow(ChangeInfo c)
        {
            var sp = new StackPanel();

            var top = Ui.Cols("auto", "*", "auto");
            var kind = new Border { Background = Theme.AccentSoft, CornerRadius = new CornerRadius(6), Padding = new Thickness(7, 2, 7, 2), VerticalAlignment = VerticalAlignment.Top, Child = Ui.Txt(c.Kind, 11, Theme.AccentText, FontWeights.SemiBold) };
            top.Children.Add(Ui.At(kind, 0, 0));
            var title = new TextBlock { Text = c.Title, FontFamily = Mono, FontSize = 12, Foreground = Theme.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 1, 10, 0) };
            top.Children.Add(Ui.At(title, 0, 1));
            if (c.RegPath != null)
            {
                var open = new Button { Style = Theme.S("Btn"), Background = Brushes.Transparent, Padding = new Thickness(6, 3, 6, 3), Content = Ui.Icon("", 12, Theme.Sub), ToolTip = L.T("Открыть в редакторе реестра"), VerticalAlignment = VerticalAlignment.Top };
                string path = c.RegPath;
                open.Click += (s, e) =>
                {
                    try { Sys.OpenRegedit(path); }
                    catch (Exception ex) { Dialog.Info(owner, L.T("Не удалось открыть редактор реестра"), ex.Message); }
                };
                top.Children.Add(Ui.At(open, 0, 2));
            }
            sp.Children.Add(top);

            if (!string.IsNullOrEmpty(c.Detail))
                sp.Children.Add(new TextBlock { Text = c.Detail, FontSize = 12, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });

            var vars = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < c.Variants.Count; i++)
            {
                bool cur = i == c.Current;
                var v = new StackPanel { Orientation = Orientation.Horizontal };
                v.Children.Add(Ui.Txt(c.Variants[i][0], 11.5, cur ? Theme.AccentText : Theme.Sub, FontWeights.SemiBold));
                var val = new TextBlock { Text = c.Variants[i][1], FontFamily = Mono, FontSize = 12, Foreground = Theme.Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                v.Children.Add(val);
                if (cur)
                {
                    var now = new Border { Background = Theme.Accent, CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 0, 6, 1), Margin = new Thickness(8, 0, 0, 0), Child = Ui.Txt(L.T("сейчас"), 10.5, Theme.OnAccent, FontWeights.SemiBold) };
                    v.Children.Add(now);
                }
                vars.Children.Add(new Border
                {
                    Background = cur ? Theme.Done : Brushes.Transparent, BorderBrush = cur ? Theme.Accent : Theme.Line, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 6), Child = v
                });
            }
            sp.Children.Add(vars);

            return new Border { Background = Theme.ListBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 10, 4), Margin = new Thickness(0, 8, 0, 0), Child = sp };
        }

        bool Unsupported { get { return !t.Usable(); } }

        // ───────── Состояние ─────────

        public void Refresh()
        {
            busy = true;
            try
            {
                if (Unsupported)
                {
                    SetStatus(t.Win11Only ? L.T("Только Windows 11") : L.T("Не поддерживается"), Theme.Sub);
                    status.Visibility = Visibility.Visible;
                    if (sw != null) sw.IsEnabled = false;
                    if (pill != null) pill.IsEnabled = false;
                    return;
                }
                var tt = t as ToggleTweak;
                if (tt != null)
                {
                    bool on = tt.ReadEnabled();
                    sw.IsChecked = on;
                    sw.IsEnabled = true;
                    SetStatus(on ? L.T("Включено") : L.T("Отключено"), Theme.AccentText);
                }
                var ct = t as ChoiceTweak;
                if (ct != null)
                {
                    int i = ct.ReadIndex();
                    pillText.Text = i >= 0 ? ct.Options[i] : ct.UnknownText();
                    pill.IsEnabled = true;
                    status.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                SetStatus(L.T("Ошибка чтения"), Theme.Red);
                status.ToolTip = ex.Message;
                status.Visibility = Visibility.Visible;
                if (sw != null) sw.IsEnabled = false;
                if (pill != null) pill.IsEnabled = false;
            }
            finally { busy = false; }
            // Обновить «Что меняется» (отметки «сейчас»), если подробности уже построены
            if (built)
            {
                bool vis = details.Visibility == Visibility.Visible;
                BuildDetails();
                details.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        void SetStatus(string s, Brush b)
        {
            status.Text = s;
            status.Foreground = b;
        }

        // Включить / отключить (переключатель и кнопки «принудительно»)
        void Apply(bool want)
        {
            if (busy || Unsupported) return;
            var tt = (ToggleTweak)t;

            string blocker = t.Blocker(want);
            if (blocker != null)
            {
                Dialog.Info(owner, L.T("Сейчас нельзя изменить «") + t.Title + L.T("»"), blocker);
                Refresh();
                return;
            }

            // Отключение защитной или рискованной функции — одно подтверждение со всеми предупреждениями
            if (!want && (t.Security != null || t.Caution != null))
            {
                string text = "";
                if (t.Security != null) text += L.T("Защита: ") + t.Security + "\n";
                if (t.Caution != null) text += L.T("Внимание: ") + t.Caution + "\n";
                text += L.T("\nВернуть можно этим же переключателем или кнопкой «Откатить всё».");
                if (!Dialog.Ask(owner, L.T("Отключить «") + t.Title + L.T("»?"), text, L.T("Отключить"), L.T("Отмена"), t.Security != null))
                {
                    Refresh();
                    return;
                }
            }
            Run(() => tt.SetEnabled(want), t.Title + " → " + (want ? L.T("Включено") : L.T("Отключено")));
        }

        void ShowChoices()
        {
            var ct = (ChoiceTweak)t;
            int cur = -1;
            try { cur = ct.ReadIndex(); } catch { }
            var list = new StackPanel();
            var pop = new Popup
            {
                PlacementTarget = pill, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true,
                VerticalOffset = 4, PopupAnimation = PopupAnimation.Fade
            };
            for (int i = 0; i < ct.Options.Length; i++)
            {
                int idx = i;
                var item = new Border
                {
                    Padding = new Thickness(12, 7, 16, 7), CornerRadius = new CornerRadius(7), Cursor = Cursors.Hand,
                    Background = i == cur ? Theme.NavActive : Brushes.Transparent,
                    Child = Ui.Txt(ct.Options[i], 13, i == cur ? Theme.Text : Theme.Sub)
                };
                item.MouseEnter += (s, e) => item.Background = Theme.Second;
                item.MouseLeave += (s, e) => item.Background = idx == cur ? Theme.NavActive : Brushes.Transparent;
                item.MouseLeftButtonUp += (s, e) =>
                {
                    pop.IsOpen = false;
                    if (idx == cur) return;
                    string ask = ct.Confirm(idx);
                    if (ask != null && !Dialog.Ask(owner, t.Title + L.T(": «") + ct.Options[idx] + L.T("»?"), ask, L.T("Продолжить"), L.T("Отмена"), true)) return;
                    Run(() => ct.SetIndex(idx), t.Title + " → " + ct.Options[idx]);
                };
                list.Children.Add(item);
            }
            pop.Child = new Border
            {
                Background = Theme.Popup, BorderBrush = Theme.PopupBorder, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(4), MinWidth = 150, Child = list,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = Theme.Light ? 0.15 : 0.45, Color = Colors.Black }
            };
            pop.IsOpen = true;
        }

        // Применить в фоне (остановка служб может занять несколько секунд), потом перечитать состояние
        async void Run(Action a, string what)
        {
            string where = owner.SectionOf(t);
            busy = true;
            if (sw != null) sw.IsEnabled = false;
            if (pill != null) pill.IsEnabled = false;
            SetStatus(L.T("Применяю…"), Theme.Sub);
            status.Visibility = Visibility.Visible;
            Cursor = Cursors.Wait;
            Exception err = null;
            try { await Task.Run(() => Journal.Do(where, what, a, t)); }
            catch (Exception ex) { err = ex; }
            Cursor = null;
            if (err == null) owner.NeedRestart(t.Restart);
            else Dialog.Info(owner, L.T("Не получилось изменить «") + t.Title + L.T("»"), err.Message);
            Refresh();
        }
    }
}
