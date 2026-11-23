using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BtwTweaker
{
    // «Журнал»: что и когда менял btwTweaker, откат по одному действию
    public class JournalPage : StackPanel
    {
        readonly MainWindow owner;
        readonly StackPanel body = new StackPanel();
        bool busy;

        public JournalPage(MainWindow w)
        {
            owner = w;

            var g = Ui.Cols("auto", "*", "auto", "auto");
            g.Children.Add(Ui.At(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Theme.AccentSoft, Child = Ui.Icon("", 17, Theme.AccentText) }, 0, 0));
            var sp = new StackPanel { Margin = new Thickness(14, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(Ui.Txt(L.T("Журнал изменений"), 14, Theme.Text, FontWeights.SemiBold));
            sp.Children.Add(new TextBlock { Text = L.T("Каждое действие btwTweaker и состояние перед ним. «Откатить» возвращает то, что было перед этим действием, — остальные изменения не трогаются."), FontSize = 12.5, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            g.Children.Add(Ui.At(sp, 0, 1));
            var all = Ui.IconBtn("", L.T("Откатить всё"), Theme.Second, Theme.Danger, () => { owner.RollbackAll(); Render(); });
            all.Margin = new Thickness(0, 0, 8, 0);
            g.Children.Add(Ui.At(all, 0, 2));
            g.Children.Add(Ui.At(Ui.IconBtn("", L.T("Очистить журнал"), Theme.Second, Theme.Text, ClearJournal), 0, 3));
            var card = Ui.Card(g, new Thickness(18, 14, 14, 14), false);
            card.Margin = new Thickness(0, 0, 0, 6);
            Children.Add(card);
            Children.Add(body);
            Render();
        }

        void Render()
        {
            body.Children.Clear();
            var list = Journal.All();
            if (list.Count == 0)
            {
                var e = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60, 0, 0) };
                e.Children.Add(Ui.Icon("", 34, Theme.Sub));
                var t = Ui.Txt(L.T("Журнал пуст"), 16, Theme.Text, FontWeights.SemiBold);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(0, 12, 0, 4);
                e.Children.Add(t);
                e.Children.Add(Ui.Txt(L.T("Здесь появятся изменения, которые вы сделаете в btwTweaker."), 13, Theme.Sub));
                body.Children.Add(e);
                return;
            }
            string day = null;
            foreach (var en in list)
            {
                string d = DayName(en.Time.Date);
                if (d != day)
                {
                    day = d;
                    var h = Ui.Txt(d, 15, Theme.Text, FontWeights.SemiBold);
                    h.Margin = new Thickness(2, 16, 0, 10);
                    body.Children.Add(h);
                }
                body.Children.Add(Row(en));
            }
        }

        static string DayName(DateTime d)
        {
            if (d == DateTime.Today) return L.T("Сегодня");
            if (d == DateTime.Today.AddDays(-1)) return L.T("Вчера");
            return d.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
        }

        FrameworkElement Row(JournalEntry e)
        {
            var g = Ui.Cols("auto", "*", "auto");
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var time = new TextBlock { Text = e.Time.ToString("HH:mm"), FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12.5, Foreground = Theme.Sub, VerticalAlignment = VerticalAlignment.Center, Width = 46 };
            g.Children.Add(Ui.At(time, 0, 0));

            var names = new StackPanel { Margin = new Thickness(6, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Cursor = e.Lines.Count > 0 ? Cursors.Hand : null };
            var top = new WrapPanel();
            var title = Ui.Txt(e.Title, 14, e.RolledBack ? Theme.Sub : Theme.Text);
            top.Children.Add(title);
            TextBlock chev = null;
            if (e.Lines.Count > 0)
            {
                chev = Ui.Icon("", 9, Theme.Sub);
                chev.Margin = new Thickness(7, 2, 0, 0);
                chev.RenderTransformOrigin = new Point(0.5, 0.5);
                chev.RenderTransform = new RotateTransform(0);
                top.Children.Add(chev);
            }
            if (e.RolledBack) top.Children.Add(Chip(L.T("откачено"), null));
            if (e.Irreversible) top.Children.Add(Chip(L.T("не откатывается"), Theme.Yellow));
            names.Children.Add(top);
            string sub = e.Where + (e.Lines.Count > 0 ? L.T(" · значений: ") + e.Lines.Count : "") + (string.IsNullOrEmpty(e.Note) ? "" : " · " + e.Note);
            names.Children.Add(new TextBlock { Text = sub, FontSize = 12, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            g.Children.Add(Ui.At(names, 0, 1));

            if (!e.Irreversible && !e.RolledBack && e.Lines.Count > 0)
            {
                var rb = Ui.IconBtn("", L.T("Откатить"), Brushes.Transparent, Theme.AccentText, () => DoRollback(e));
                rb.BorderBrush = Theme.Accent;
                rb.BorderThickness = new Thickness(1.5);
                rb.Padding = new Thickness(11, 4, 12, 4);
                rb.FontSize = 12.5;
                rb.VerticalAlignment = VerticalAlignment.Center;
                g.Children.Add(Ui.At(rb, 0, 2));
            }

            // Подробности: что было до действия
            if (e.Lines.Count > 0)
            {
                var det = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(52, 10, 4, 2) };
                det.Children.Add(Ui.Txt(L.T("Было до этого действия:"), 12, Theme.Sub));
                foreach (var l in e.Lines)
                {
                    var r = Ui.Cols("*", "auto");
                    r.Margin = new Thickness(0, 6, 0, 0);
                    r.Children.Add(Ui.At(new TextBlock { Text = Backup.Describe(l), FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11.5, Foreground = Theme.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) }, 0, 0));
                    r.Children.Add(Ui.At(new Border { Background = Theme.Done, CornerRadius = new CornerRadius(7), Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = Backup.ValueText(l), FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11.5, Foreground = Theme.AccentText } }, 0, 1));
                    det.Children.Add(r);
                }
                Grid.SetColumnSpan(det, 3);
                g.Children.Add(Ui.At(det, 1, 0));
                names.MouseLeftButtonUp += (s, ev) =>
                {
                    bool show = det.Visibility != Visibility.Visible;
                    det.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                    ((RotateTransform)chev.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(show ? 180 : 0, Theme.Anim));
                    if (show) Ui.FadeIn(det);
                };
            }

            var card = Ui.Card(g, new Thickness(14, 10, 12, 10));
            card.Margin = new Thickness(0, 0, 0, 6);
            if (e.RolledBack || e.Irreversible) card.Opacity = 0.85;
            return card;
        }

        static Border Chip(string text, Brush color)
        {
            var fg = color ?? Theme.AccentText;
            var bg = color == null ? Theme.Done : new SolidColorBrush(Theme.A(((SolidColorBrush)color).Color, 0x26));
            return new Border { Background = bg, CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 1, 8, 2), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ui.Txt(text, 11, fg, FontWeights.SemiBold) };
        }

        async void DoRollback(JournalEntry e)
        {
            if (busy) return;
            if (!Dialog.Ask(owner, L.T("Откатить «") + e.Title + L.T("»?"),
                L.T("Вернётся состояние, которое было перед этим действием (") + e.Time.ToString("d MMM, HH:mm", new System.Globalization.CultureInfo("ru-RU")) + L.T("). Если после него вы меняли те же настройки, они тоже вернутся к тому моменту. Остальные изменения не трогаются."),
                L.T("Откатить"), L.T("Отмена")))
                return;
            busy = true;
            Mouse.OverrideCursor = Cursors.Wait;
            var errors = new List<string>();
            int n = 0;
            Exception err = null;
            try { n = await Task.Run(() => Journal.Rollback(e.Id, errors)); }
            catch (Exception ex) { err = ex; }
            Mouse.OverrideCursor = null;
            busy = false;
            Render();
            if (err != null) { Dialog.Info(owner, L.T("Не получилось откатить"), err.Message); return; }
            Dialog.Info(owner, L.T("Откачено"), L.T("Восстановлено значений: ") + n + "." + (errors.Count > 0 ? L.T("\n\nОшибки:\n") + string.Join("\n", errors) : "") +
                L.T("\n\nНекоторым настройкам нужен перезапуск Проводника или перезагрузка, чтобы изменения стали видны."));
        }

        void ClearJournal()
        {
            if (Journal.Count == 0) return;
            if (!Dialog.Ask(owner, L.T("Очистить журнал?"), L.T("Записи журнала будут удалены, и откатывать по одному станет нельзя. Резервная копия и кнопка «Откатить всё» продолжат работать."), L.T("Очистить"), L.T("Отмена"), true)) return;
            Journal.Clear();
            Render();
        }
    }
}
