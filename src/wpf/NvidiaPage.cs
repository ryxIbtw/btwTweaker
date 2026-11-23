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
    // «Панель NVIDIA»: настройки глобального профиля драйвера
    public class NvidiaPage : StackPanel
    {
        readonly MainWindow owner;
        readonly List<NvGroup> groups = NvCatalog.Build();
        readonly StackPanel body = new StackPanel();
        readonly List<Action> refreshers = new List<Action>();
        Dictionary<uint, NvValue> values = new Dictionary<uint, NvValue>();
        Button optimal, reload;
        bool busy;

        public NvidiaPage(MainWindow w)
        {
            owner = w;
            string err;
            if (!NvDrs.Available(out err))
            {
                Children.Add(Ui.Card(Empty(L.T("Видеокарта NVIDIA не найдена"), L.T("Этот раздел работает только с драйвером NVIDIA. ") + err), new Thickness(24), false));
                return;
            }
            Children.Add(Toolbar());
            Children.Add(body);
            body.Children.Add(Ui.Txt(L.T("Читаю настройки драйвера…"), 13, Theme.Sub));
            Load(false);
        }

        static FrameworkElement Empty(string title, string text)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(Ui.Icon("", 34, Theme.Sub));
            var t = Ui.Txt(title, 16, Theme.Text, FontWeights.SemiBold);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 12, 0, 4);
            sp.Children.Add(t);
            sp.Children.Add(new TextBlock { Text = text, FontSize = 13, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            return sp;
        }

        FrameworkElement Toolbar()
        {
            var g = Ui.Cols("auto", "*", "auto", "auto");
            g.Children.Add(Ui.At(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Theme.AccentSoft, Child = Ui.Icon("", 17, Theme.AccentText) }, 0, 0));
            var sp = new StackPanel { Margin = new Thickness(14, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(Ui.Txt(L.T("Глобальный профиль драйвера"), 14, Theme.Text, FontWeights.SemiBold));
            sp.Children.Add(new TextBlock { Text = L.T("Настройки действуют во всех играх, у которых нет своего профиля. Изменения применяются сразу — игру нужно перезапустить."), FontSize = 12.5, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            g.Children.Add(Ui.At(sp, 0, 1));
            optimal = Ui.IconBtn("", L.T("Оптимальное"), Theme.Accent, Theme.OnAccent, ApplyOptimal);
            optimal.Margin = new Thickness(0, 0, 8, 0);
            optimal.ToolTip = L.T("Набор значений для игр: низкая задержка, максимальная производительность. Перед применением покажу список изменений.");
            g.Children.Add(Ui.At(optimal, 0, 2));
            reload = Ui.IconBtn("", L.T("Обновить"), Theme.Second, Theme.Text, () => Load(false));
            g.Children.Add(Ui.At(reload, 0, 3));
            var card = Ui.Card(g, new Thickness(18, 14, 14, 14), false);
            card.Margin = new Thickness(0, 0, 0, 6);
            return card;
        }

        async void Load(bool keepScroll)
        {
            if (busy) return;
            busy = true;
            SetButtons(false);
            Exception err = null;
            Dictionary<uint, NvValue> v = null;
            try { v = await Task.Run(() => NvDrs.Read(NvCatalog.AllIds(groups))); }
            catch (Exception ex) { err = ex; }
            busy = false;
            SetButtons(true);
            if (err != null)
            {
                body.Children.Clear();
                body.Children.Add(Ui.Txt(L.T("Не удалось прочитать настройки драйвера: ") + err.Message, 13, Theme.Red));
                return;
            }
            values = v;
            if (refreshers.Count == 0) Build();
            else foreach (var r in refreshers) r();
        }

        void SetButtons(bool on)
        {
            if (optimal != null) optimal.IsEnabled = on;
            if (reload != null) reload.IsEnabled = on;
        }

        void Build()
        {
            body.Children.Clear();
            foreach (var g in groups)
            {
                var h = Ui.Txt(g.Title, 15, Theme.Text, FontWeights.SemiBold);
                h.Margin = new Thickness(2, 18, 0, 10);
                body.Children.Add(h);
                foreach (var it in g.Items) body.Children.Add(Row(it));
            }
            Ui.FadeIn(body);
        }

        // ───────── Строка настройки ─────────

        FrameworkElement Row(NvItem it)
        {
            var g = Ui.Cols("*", "auto", "auto");
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            var title = Ui.Txt(it.Title, 14, Theme.Text);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            head.Children.Add(title);
            var chev = Ui.Icon("", 9, Theme.Sub);
            chev.Margin = new Thickness(7, 2, 0, 0);
            chev.RenderTransformOrigin = new Point(0.5, 0.5);
            chev.RenderTransform = new RotateTransform(0);
            head.Children.Add(chev);
            g.Children.Add(Ui.At(head, 0, 0));

            var state = Ui.Txt("", 12.5, Theme.Sub, FontWeights.SemiBold);
            state.Margin = new Thickness(12, 0, 12, 0);
            g.Children.Add(Ui.At(state, 0, 1));

            // Подробности: описание и технические сведения
            var det = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 2) };
            det.Children.Add(new TextBlock { Text = it.Description, TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Theme.Sub, LineHeight = 20, Margin = new Thickness(0, 0, 40, 0) });
            var ids = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            foreach (var id in it.Ids())
            {
                var chip = new Border { Background = Theme.Done, CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 3, 10, 3), Margin = new Thickness(0, 0, 6, 6) };
                var cs = new StackPanel { Orientation = Orientation.Horizontal };
                cs.Children.Add(Ui.Txt(L.T("Драйвер"), 11.5, Theme.AccentText, FontWeights.SemiBold));
                cs.Children.Add(new TextBlock { Text = "0x" + id.ToString("X8"), FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12, Foreground = Theme.Text, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                chip.Child = cs;
                ids.Children.Add(chip);
            }
            det.Children.Add(ids);
            Grid.SetColumnSpan(det, 3);
            g.Children.Add(Ui.At(det, 1, 0));
            head.MouseLeftButtonUp += (s, e) =>
            {
                bool show = det.Visibility != Visibility.Visible;
                det.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                ((RotateTransform)chev.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(show ? 180 : 0, Theme.Anim));
                if (show) Ui.FadeIn(det);
            };

            FrameworkElement control;
            Action refresh;
            if (it.IsNumber) control = NumberBox(it, out refresh);
            else control = Choice(it, out refresh);
            g.Children.Add(Ui.At(control, 0, 2));

            Action all = () =>
            {
                bool edited = NvCatalog.Edited(it, values);
                state.Text = edited ? L.T("Изменено") : L.T("По умолчанию");
                state.Foreground = edited ? Theme.AccentText : Theme.Sub;
                refresh();
            };
            all();
            refreshers.Add(all);

            var card = Ui.Card(g, new Thickness(16, 10, 12, 10));
            card.Margin = new Thickness(0, 0, 0, 8);
            return card;
        }

        FrameworkElement Choice(NvItem it, out Action refresh)
        {
            var text = Ui.Txt("", 12.5, Theme.AccentText, FontWeights.SemiBold);
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            var pc = Ui.Cols("*", "auto");
            pc.Children.Add(Ui.At(text, 0, 0));
            var ch = Ui.Icon("", 8, Theme.AccentText);
            ch.Margin = new Thickness(8, 1, 0, 0);
            pc.Children.Add(Ui.At(ch, 0, 1));
            Button pill = null;
            pill = Ui.Btn(pc, Theme.AccentSoft, () => ShowOptions(it, pill));
            pill.Width = 210;
            pill.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            pill.Padding = new Thickness(12, 6, 10, 6);
            refresh = () =>
            {
                int i = NvCatalog.CurrentIndex(it, values);
                text.Text = i >= 0 ? it.Options[i].Label : L.T("Своё значение");
                pill.ToolTip = text.Text;
            };
            return pill;
        }

        void ShowOptions(NvItem it, Button anchor)
        {
            int cur = NvCatalog.CurrentIndex(it, values);
            var list = new StackPanel();
            var pop = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 4, PopupAnimation = PopupAnimation.Fade };
            for (int i = 0; i < it.Options.Count; i++)
            {
                int idx = i;
                var item = new Border
                {
                    Padding = new Thickness(12, 7, 16, 7), CornerRadius = new CornerRadius(7), Cursor = Cursors.Hand,
                    Background = i == cur ? Theme.NavActive : Brushes.Transparent,
                    Child = Ui.Txt(it.Options[i].Label + (i == it.Optimal ? L.T("   · оптимально") : ""), 13, i == cur ? Theme.Text : Theme.Sub)
                };
                item.MouseEnter += (s, e) => item.Background = Theme.Second;
                item.MouseLeave += (s, e) => item.Background = idx == cur ? Theme.NavActive : Brushes.Transparent;
                item.MouseLeftButtonUp += (s, e) =>
                {
                    pop.IsOpen = false;
                    if (idx != cur) Write(it.Options[idx].Set, it.Title + " → " + it.Options[idx].Label);
                };
                list.Children.Add(item);
            }
            pop.Child = new Border
            {
                Background = Theme.Popup, BorderBrush = Theme.PopupBorder, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(4), MinWidth = 210, Child = list,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = Theme.Light ? 0.15 : 0.45, Color = Colors.Black }
            };
            pop.IsOpen = true;
        }

        FrameworkElement NumberBox(NvItem it, out Action refresh)
        {
            var grid = new Grid { Width = 210 };
            var box = new TextBox { Style = Theme.S("Search"), Height = 30, Padding = new Thickness(12, 4, 44, 4), MaxLength = 4, HorizontalContentAlignment = HorizontalAlignment.Left };
            var unit = Ui.Txt(it.Unit, 12, Theme.Sub);
            unit.HorizontalAlignment = HorizontalAlignment.Right;
            unit.Margin = new Thickness(0, 0, 12, 0);
            unit.IsHitTestVisible = false;
            grid.Children.Add(box);
            grid.Children.Add(unit);
            grid.ToolTip = L.T("Введите число и нажмите Enter. 0 — без ограничения.");
            Action commit = () =>
            {
                uint n;
                if (!uint.TryParse(box.Text.Trim(), out n) || n > 1000)
                {
                    Dialog.Info(owner, L.T("Неверное значение"), L.T("Введите число от 0 до 1000. 0 — без ограничения."));
                    box.Text = NvCatalog.Effective(values, it.NumberId).ToString();
                    return;
                }
                if (n != NvCatalog.Effective(values, it.NumberId)) Write(new Dictionary<uint, uint> { { it.NumberId, n } }, it.Title + " → " + (n == 0 ? L.T("без ограничения") : n + " " + it.Unit));
            };
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) commit(); };
            box.LostKeyboardFocus += (s, e) => { if (box.Text.Trim() != NvCatalog.Effective(values, it.NumberId).ToString()) commit(); };
            refresh = () => box.Text = NvCatalog.Effective(values, it.NumberId).ToString();
            return grid;
        }

        // ───────── Запись ─────────

        async void Write(Dictionary<uint, uint> set, string what)
        {
            if (busy) return;
            busy = true;
            SetButtons(false);
            Mouse.OverrideCursor = Cursors.Wait;
            Exception err = null;
            try { await Task.Run(() => Journal.Do(L.T("Панель NVIDIA"), what, () => NvDrs.Write(set))); }
            catch (Exception ex) { err = ex; }
            Mouse.OverrideCursor = null;
            busy = false;
            if (err != null) Dialog.Info(owner, L.T("Не получилось изменить настройку драйвера"), err.Message);
            Load(true);
        }

        void ApplyOptimal()
        {
            var set = new Dictionary<uint, uint>();
            var lines = new List<string>();
            foreach (var g in groups)
                foreach (var it in g.Items)
                {
                    if (it.Optimal < 0) continue;
                    int cur = NvCatalog.CurrentIndex(it, values);
                    if (cur == it.Optimal) continue;
                    lines.Add(it.Title + ":  " + (cur >= 0 ? it.Options[cur].Label : L.T("своё")) + "  →  " + it.Options[it.Optimal].Label);
                    foreach (var kv in it.Options[it.Optimal].Set) set[kv.Key] = kv.Value;
                }
            if (lines.Count == 0)
            {
                Dialog.Info(owner, L.T("Уже оптимально"), L.T("Все настройки из набора «Оптимальное» уже стоят."));
                return;
            }
            if (!Dialog.AskList(owner, L.T("Применить «Оптимальное»?"), L.T("Будет изменено настроек: ") + lines.Count + L.T(". Прежние значения запомнятся — их вернёт «Откатить всё»."), lines, L.T("Применить"), false))
                return;
            Write(set, L.T("Оптимальное: изменено настроек — ") + lines.Count);
        }
    }
}
