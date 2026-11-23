using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace BtwTweaker
{
    // Карточка одного твика: значок, название, стрелка с описанием, значки предупреждений, состояние и переключатель
    public class TweakCard : Border
    {
        readonly Tweak t;
        readonly MainWindow owner;
        readonly TextBlock status = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        readonly TextBlock desc;
        readonly TextBlock chev;
        ToggleButton sw;
        Button pill;
        TextBlock pillText;
        bool busy;

        public TweakCard(Tweak tweak, MainWindow w)
        {
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

            // Заголовок: клик раскрывает описание
            var head = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            head.Children.Add(Ui.Txt(t.Title, 14, Theme.Text));
            chev = Ui.Icon("", 9, Theme.Sub);
            chev.Margin = new Thickness(7, 2, 0, 0);
            chev.RenderTransformOrigin = new Point(0.5, 0.5);
            chev.RenderTransform = new RotateTransform(0);
            head.Children.Add(chev);
            if (t.Security != null) head.Children.Add(Badge("", Theme.Red, "Снижает защиту: " + t.Security, true));
            if (t.Caution != null) head.Children.Add(Badge("!", Theme.Yellow, "Внимание: " + t.Caution, false));
            if (t.Restart == Restart.Reboot) head.Children.Add(Badge("", Theme.Sub, "Нужна перезагрузка", true));
            head.MouseLeftButtonUp += (s, e) => ToggleDesc();
            g.Children.Add(Ui.At(head, 0, 1));

            desc = new TextBlock
            {
                Text = BuildDesc(),
                TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Foreground = Theme.Sub, LineHeight = 18,
                Margin = new Thickness(0, 8, 16, 2), Visibility = Visibility.Collapsed
            };
            g.Children.Add(Ui.At(desc, 1, 1));

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(status);
            if (t is ToggleTweak)
            {
                sw = new ToggleButton { Style = Theme.S("Switch") };
                sw.Click += (s, e) => OnToggle();
                right.Children.Add(sw);
            }
            else if (t is ChoiceTweak)
            {
                pillText = Ui.Txt("", 12.5, Theme.Accent, FontWeights.SemiBold);
                var pc = new StackPanel { Orientation = Orientation.Horizontal };
                pc.Children.Add(pillText);
                var pchev = Ui.Icon("", 8, Theme.Accent);
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

        string BuildDesc()
        {
            string s = t.Description ?? "";
            if (t.Caution != null) s += "\n\nВнимание: " + t.Caution;
            if (t.Security != null) s += "\n\nЗащита: " + t.Security;
            if (t.Restart == Restart.Reboot) s += "\n\nИзменение применится после перезагрузки.";
            else if (t.Restart == Restart.SignOut) s += "\n\nИзменение применится после выхода из системы.";
            else if (t.Restart == Restart.Explorer) s += "\n\nНужен перезапуск Проводника.";
            return s;
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

        public void ToggleDesc(bool? open = null)
        {
            bool show = open ?? desc.Visibility != Visibility.Visible;
            desc.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ((RotateTransform)chev.RenderTransform).Angle = show ? 180 : 0;
        }

        bool Unsupported { get { return (t.Win11Only && !Sys.IsWin11) || Sys.Build < t.MinBuild || !t.IsSupported(); } }

        // Прочитать реальное состояние из системы и показать его
        public void Refresh()
        {
            busy = true;
            try
            {
                if (Unsupported)
                {
                    SetStatus(t.Win11Only ? "Только Windows 11" : "Не поддерживается", Theme.Sub);
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
                    SetStatus(on ? "Включено" : "Отключено", Theme.Accent);
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
                SetStatus("Ошибка чтения", Theme.Red);
                status.ToolTip = ex.Message;
                status.Visibility = Visibility.Visible;
                if (sw != null) sw.IsEnabled = false;
                if (pill != null) pill.IsEnabled = false;
            }
            finally { busy = false; }
        }

        void SetStatus(string s, Brush b)
        {
            status.Text = s;
            status.Foreground = b;
        }

        void OnToggle()
        {
            if (busy) return;
            var tt = (ToggleTweak)t;
            bool want = sw.IsChecked == true;

            string blocker = t.Blocker(want);
            if (blocker != null)
            {
                Dialog.Info(owner, "Сейчас нельзя изменить «" + t.Title + "»", blocker);
                Refresh();
                return;
            }

            // Отключение защитной или рискованной функции — одно подтверждение со всеми предупреждениями
            if (!want && (t.Security != null || t.Caution != null))
            {
                string text = "";
                if (t.Security != null) text += "Защита: " + t.Security + "\n";
                if (t.Caution != null) text += "Внимание: " + t.Caution + "\n";
                text += "\nВернуть можно этим же переключателем или кнопкой «Откатить всё».";
                if (!Dialog.Ask(owner, "Отключить «" + t.Title + "»?", text, "Отключить", "Отмена", t.Security != null))
                {
                    Refresh();
                    return;
                }
            }
            Run(() => tt.SetEnabled(want));
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
                    if (ask != null && !Dialog.Ask(owner, t.Title + ": «" + ct.Options[idx] + "»?", ask, "Продолжить", "Отмена", true)) return;
                    Run(() => ct.SetIndex(idx));
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
        async void Run(Action a)
        {
            busy = true;
            if (sw != null) sw.IsEnabled = false;
            if (pill != null) pill.IsEnabled = false;
            SetStatus("Применяю…", Theme.Sub);
            status.Visibility = Visibility.Visible;
            Cursor = Cursors.Wait;
            Exception err = null;
            try { await Task.Run(a); }
            catch (Exception ex) { err = ex; }
            Cursor = null;
            if (err == null) owner.NeedRestart(t.Restart);
            else Dialog.Info(owner, "Не получилось изменить «" + t.Title + "»", err.Message);
            Refresh();
        }
    }
}
