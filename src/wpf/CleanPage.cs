using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BtwTweaker
{
    // Страница «Очистка»: группы пунктов, размер каждого и кнопки «Очистить»
    public class CleanPage : StackPanel
    {
        readonly Window owner;

        public CleanPage(Window w)
        {
            owner = w;
            foreach (var g in CleanCatalog.Build())
            {
                var rows = new List<CleanRow>();
                var head = Ui.Cols("*", "auto");
                head.Margin = new Thickness(2, Children.Count == 0 ? 2 : 16, 0, 10);
                head.Children.Add(Ui.At(Ui.Txt(g.Title, 16, Theme.Text, FontWeights.SemiBold), 0, 0));
                if (g.CleanAll)
                {
                    string title = g.Title;
                    var all = Ui.Btn(L.T("Очистить"), Theme.Accent, () => CleanGroup(title, rows));
                    all.Padding = new Thickness(12, 4, 12, 4);
                    all.FontSize = 12.5;
                    head.Children.Add(Ui.At(all, 0, 1));
                }
                Children.Add(head);
                foreach (var c in g.Items)
                {
                    var row = new CleanRow(c, owner);
                    rows.Add(row);
                    Children.Add(row);
                }
            }
        }

        async void CleanGroup(string title, List<CleanRow> rows)
        {
            if (!Dialog.Ask(owner, L.T("Очистить группу «") + title + L.T("»?"), L.T("Будут очищены все пункты группы. Открытые программы, чьи файлы заняты, пропустятся."), L.T("Очистить"), L.T("Отмена")))
                return;
            long freed = 0;
            int failed = 0;
            foreach (var r in rows)
            {
                var res = await r.Run(false);
                if (res != null) { freed += res.Freed; failed += res.Failed; }
            }
            Dialog.Info(owner, L.T("Готово"), L.T("Освобождено: ") + Fs.Fmt(freed) + (failed > 0 ? L.T("\nПропущено занятых файлов: ") + failed : ""));
        }
    }

    public class CleanRow : Border
    {
        readonly Cleaner c;
        readonly Window owner;
        readonly TextBlock size = new TextBlock { FontSize = 12.5, Foreground = Theme.Sub, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        readonly TextBlock desc;
        readonly TextBlock chev;
        readonly Button btn;
        readonly Border done;
        ScanResult last;
        bool busy;

        public CleanRow(Cleaner cleaner, Window w)
        {
            c = cleaner;
            owner = w;
            CornerRadius = new CornerRadius(14);
            Padding = new Thickness(16, 10, 12, 10);
            Margin = new Thickness(0, 0, 0, 8);
            BorderBrush = Theme.Line;
            BorderThickness = new Thickness(1);
            Ui.Hover(this);

            var g = Ui.Cols("34", "*", "auto");
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var icon = Ui.Icon(c.Icon, 17, c.IconColor != null ? Theme.B(c.IconColor) : Theme.Text);
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(Ui.At(icon, 0, 0));

            var head = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            head.Children.Add(Ui.Txt(c.Title, 14, Theme.Text));
            chev = Ui.Icon("", 9, Theme.Sub);
            chev.Margin = new Thickness(7, 2, 0, 0);
            chev.RenderTransformOrigin = new Point(0.5, 0.5);
            chev.RenderTransform = new RotateTransform(0);
            head.Children.Add(chev);
            if (c.Caution != null)
            {
                var badge = new Border
                {
                    Background = Theme.Yellow, CornerRadius = new CornerRadius(3), Width = 16, Height = 16, Margin = new Thickness(8, 0, 0, 0), ToolTip = L.T("Внимание: ") + c.Caution,
                    Child = new TextBlock { Text = "!", FontWeight = FontWeights.Bold, FontSize = 11, Foreground = Theme.B("#1A1300"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                head.Children.Add(badge);
            }
            head.MouseLeftButtonUp += (s, e) => ToggleDesc();
            g.Children.Add(Ui.At(head, 0, 1));

            desc = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Foreground = Theme.Sub, LineHeight = 18,
                Margin = new Thickness(0, 8, 16, 2), Visibility = Visibility.Collapsed
            };
            g.Children.Add(Ui.At(desc, 1, 1));

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(size);
            // Плашка «Очищено» — когда очищать нечего
            var doneRow = new StackPanel { Orientation = Orientation.Horizontal };
            var check = Ui.Icon("", 11, Theme.AccentText);
            check.FontWeight = FontWeights.Bold;
            check.Margin = new Thickness(0, 1, 6, 0);
            doneRow.Children.Add(check);
            doneRow.Children.Add(Ui.Txt(L.T("Очищено"), 12.5, Theme.AccentText, FontWeights.SemiBold));
            done = new Border
            {
                Background = Theme.Done, BorderBrush = Theme.DoneBorder, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 3, 12, 3), Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center, Child = doneRow, Visibility = Visibility.Collapsed
            };
            right.Children.Add(done);
            btn = Ui.Btn(L.T("Очистить"), Brushes.Transparent, () => { var t = Run(true); });
            btn.BorderBrush = Theme.Accent;
            btn.Foreground = Theme.Accent;
            btn.BorderThickness = new Thickness(1.5);
            btn.Padding = new Thickness(12, 4, 12, 4);
            btn.FontSize = 12.5;
            right.Children.Add(btn);
            g.Children.Add(Ui.At(right, 0, 2));

            Child = g;
            UpdateDesc();
            Rescan();
        }

        void UpdateDesc()
        {
            string s = c.Description ?? "";
            if (c.Caution != null) s += L.T("\n\nВнимание: ") + c.Caution;
            if (last != null && last.Items.Count > 0)
            {
                int n = Math.Min(last.Items.Count, 15);
                s += "\n\n" + string.Join("\n", last.Items.GetRange(0, n));
                if (last.Items.Count > n) s += L.T("\n… и ещё ") + (last.Items.Count - n);
            }
            desc.Text = s;
        }

        public void ToggleDesc()
        {
            bool show = desc.Visibility != Visibility.Visible;
            desc.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ((RotateTransform)chev.RenderTransform).Angle = show ? 180 : 0;
        }

        async void Rescan()
        {
            bool wasDone = done.Visibility == Visibility.Visible;
            done.Visibility = Visibility.Collapsed;
            size.Visibility = Visibility.Visible;
            size.Text = L.T("Считаю…");
            btn.IsEnabled = false;
            try { last = await Task.Run(() => c.Scan()); }
            catch (Exception ex) { last = null; size.Text = L.T("Ошибка"); size.ToolTip = ex.Message; return; }

            // Очищать нечего: 0 Б / ничего не найдено
            bool empty = last.Count >= 0 ? last.Count == 0 : last.Bytes == 0;
            if (empty)
            {
                size.Visibility = Visibility.Collapsed;
                done.Visibility = Visibility.Visible;
                if (!wasDone) Ui.FadeIn(done);
                btn.IsEnabled = false;
                btn.ToolTip = L.T("Очищать нечего");
            }
            else
            {
                if (last.Count >= 0)
                    size.Text = (c is TracesCleaner ? L.T("Можно очистить ") : L.T("Найдено: ")) + last.Count + (last.Bytes > 0 ? " · " + Fs.Fmt(last.Bytes) : "");
                else
                    size.Text = Fs.Fmt(last.Bytes);
                btn.IsEnabled = !busy;
                btn.ToolTip = null;
            }
            UpdateDesc();
        }

        // single = нажата кнопка у самого пункта (а не у группы)
        public async Task<CleanResult> Run(bool single)
        {
            if (busy) return null;
            string proc = c.RunningProcess();
            if (proc != null)
            {
                Dialog.Info(owner, L.T("Закройте ") + proc, L.T("Чтобы очистить «") + c.Title + L.T("», закройте ") + proc + L.T(" и нажмите «Очистить» ещё раз."));
                return null;
            }
            if (c.Confirm)
            {
                var scan = last ?? await Task.Run(() => c.Scan());
                if (scan.Count == 0) { Rescan(); return null; }
                if (!Dialog.AskList(owner, c.Title + "?", L.T("Будет удалено:"), scan.Items, L.T("Очистить"), true)) return null;
            }
            else if (single && c.Caution != null && !Dialog.Ask(owner, L.T("Очистить «") + c.Title + L.T("»?"), c.Caution, L.T("Очистить"), L.T("Отмена")))
                return null;

            busy = true;
            btn.IsEnabled = false;
            size.Text = L.T("Очищаю…");
            CleanResult res = null;
            try { res = await Task.Run(() => c.Clean()); }
            catch (Exception ex) { Dialog.Info(owner, L.T("Не получилось очистить «") + c.Title + L.T("»"), ex.Message); }
            if (res != null && (res.Freed > 0 || res.Removed > 0))
                Journal.Info(L.T("Очистка"), c.Title + (res.Freed > 0 ? L.T(" — освобождено ") + Fs.Fmt(res.Freed) : L.T(" — удалено записей: ") + res.Removed), L.T("Удалённые файлы не восстанавливаются."));
            busy = false;
            Rescan();
            if (single && res != null)
            {
                string msg = res.Freed > 0 ? L.T("Освобождено: ") + Fs.Fmt(res.Freed) : L.T("Удалено записей: ") + res.Removed;
                if (res.Failed > 0) msg += L.T("\nПропущено (заняты или нет доступа): ") + res.Failed;
                Dialog.Info(owner, c.Title, msg);
            }
            return res;
        }
    }
}
