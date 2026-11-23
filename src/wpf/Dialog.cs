using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BtwTweaker
{
    // Тёмное окно-вопрос в стиле программы
    public class Dialog : Window
    {
        bool result;

        Dialog(Window owner, string title, string text, string yes, string no, bool danger) : this(owner, title, text, yes, no, danger, null) { }

        Dialog(Window owner, string title, string text, string yes, string no, bool danger, System.Collections.Generic.IList<string> items)
        {
            Owner = owner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.Height;
            Width = 440;
            WindowStartupLocation = owner != null && owner.IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            FontFamily = Theme.Font;

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Theme.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
            sp.Children.Add(new TextBlock { Text = text, FontSize = 13, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, LineHeight = 19 });
            if (items != null && items.Count > 0)
            {
                Width = 560;
                var lines = new TextBlock { Text = string.Join("\n", items), FontSize = 12, Foreground = Theme.Text, LineHeight = 18, TextWrapping = TextWrapping.Wrap };
                sp.Children.Add(new Border
                {
                    Background = Theme.ListBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 6, 8), Margin = new Thickness(0, 12, 0, 0),
                    Child = new ScrollViewer { Content = lines, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
                });
            }

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            if (no != null)
            {
                var n = Ui.Btn(no, Theme.Second, () => { result = false; Close(); });
                n.Margin = new Thickness(0, 0, 8, 0);
                btns.Children.Add(n);
            }
            btns.Children.Add(Ui.Btn(yes, danger ? Theme.Red : Theme.Accent, () => { result = true; Close(); }));
            sp.Children.Add(btns);

            Content = new Border
            {
                Background = Theme.Popup,
                BorderBrush = Theme.PopupBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24, 20, 24, 20),
                Margin = new Thickness(16),
                Child = sp,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 28, ShadowDepth = 4, Opacity = Theme.Light ? 0.2 : 0.55, Color = Colors.Black }
            };
            Loaded += (s, e) => Ui.FadeIn((FrameworkElement)Content);
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) { result = false; Close(); } };
        }

        public static bool Ask(Window owner, string title, string text, string yes = null, string no = null, bool danger = false)
        {
            var d = new Dialog(owner, title, text, yes ?? L.T("Да"), no ?? L.T("Отмена"), danger);
            d.ShowDialog();
            return d.result;
        }

        public static bool AskList(Window owner, string title, string text, System.Collections.Generic.IList<string> items, string yes, bool danger)
        {
            var d = new Dialog(owner, title, text, yes, L.T("Отмена"), danger, items);
            d.ShowDialog();
            return d.result;
        }

        public static void Info(Window owner, string title, string text)
        {
            new Dialog(owner, title, text, L.T("Понятно"), null, false).ShowDialog();
        }

        // Список с галочками, сгруппированный по Group. Возвращает true, если нажали «yes»; выбор — в PickItem.Checked.
        public static bool Pick(Window owner, string title, string text, System.Collections.Generic.IList<PickItem> items, string yes)
        {
            var d = new Dialog(owner, title, text, yes, L.T("Отмена"), false, null);
            d.Width = 600;
            var sp = (StackPanel)((Border)d.Content).Child;
            var btns = sp.Children[sp.Children.Count - 1];
            var yesBtn = (UIElement)((StackPanel)btns).Children[((StackPanel)btns).Children.Count - 1];
            sp.Children.Remove(btns);

            var list = new StackPanel();
            Action update = null;
            var counter = new TextBlock { FontSize = 12, Foreground = Theme.Sub, VerticalAlignment = VerticalAlignment.Center };
            string group = null;
            foreach (var it in items)
            {
                if (it.Group != group)
                {
                    group = it.Group;
                    list.Children.Add(new TextBlock { Text = group, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Theme.Sub, Margin = new Thickness(4, list.Children.Count == 0 ? 2 : 10, 0, 4) });
                }
                list.Children.Add(PickRow(it, () => update()));
            }

            // Отметить всё / снять всё
            var all = Ui.Btn(L.T("Все"), Theme.Second, () => { foreach (var it in items) if (it.Enabled) it.Checked = true; foreach (var r in list.Children) { var b = r as Border; if (b != null && b.Tag is Action) ((Action)b.Tag)(); } update(); });
            var none = Ui.Btn(L.T("Ничего"), Theme.Second, () => { foreach (var it in items) it.Checked = false; foreach (var r in list.Children) { var b = r as Border; if (b != null && b.Tag is Action) ((Action)b.Tag)(); } update(); });
            all.Padding = none.Padding = new Thickness(10, 4, 10, 4);
            none.Margin = new Thickness(6, 0, 12, 0);
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            tools.Children.Add(all); tools.Children.Add(none); tools.Children.Add(counter);
            sp.Children.Add(tools);

            sp.Children.Add(new Border
            {
                Background = Theme.ListBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 6, 2, 6), Margin = new Thickness(0, 8, 0, 0),
                Child = new ScrollViewer { Content = list, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
            });
            sp.Children.Add(btns);

            update = () =>
            {
                int n = 0, total = 0;
                foreach (var it in items) { if (it.Enabled) total++; if (it.Checked) n++; }
                counter.Text = L.T("Выбрано: ") + n + L.T(" из ") + total;
                yesBtn.IsEnabled = n > 0;
                yesBtn.Opacity = n > 0 ? 1 : 0.5;
            };
            update();
#if TESTHOOKS
            // Снимок самого диалога (BTW_SNAP + BTW_SNAP_PROFILE): сохранить и закрыть программу
            string snap = Environment.GetEnvironmentVariable("BTW_SNAP");
            if (!string.IsNullOrEmpty(snap))
                d.ContentRendered += (s, e) => System.Threading.Tasks.Task.Delay(800).ContinueWith(_ => d.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var el = (FrameworkElement)d.Content;
                    var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)d.ActualWidth, (int)d.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    var dv = new DrawingVisual();
                    using (var dc = dv.RenderOpen())
                    {
                        dc.DrawRectangle(new SolidColorBrush(Theme.BaseColor), null, new Rect(0, 0, d.ActualWidth, d.ActualHeight));
                        dc.DrawRectangle(new VisualBrush(el), null, new Rect(el.Margin.Left, el.Margin.Top, el.ActualWidth, el.ActualHeight));
                    }
                    bmp.Render(dv);
                    var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                    using (var f = System.IO.File.Create(snap)) enc.Save(f);
                    Application.Current.Shutdown();
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle));
#endif
            d.ShowDialog();
            return d.result;
        }

        static Border PickRow(PickItem it, Action changed)
        {
            var g = Ui.Cols("auto", "*");
            var mark = Ui.Icon("", 11, Theme.OnAccent);
            var box = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1.5), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 10, 0), Child = mark };
            g.Children.Add(Ui.At(box, 0, 0));
            var sp = new StackPanel();
            var title = new TextBlock { FontSize = 13, Foreground = it.Enabled ? Theme.Text : Theme.Sub, TextWrapping = TextWrapping.Wrap };
            title.Inlines.Add(new System.Windows.Documents.Run(it.Text));
            if (it.Detail != null) title.Inlines.Add(new System.Windows.Documents.Run("   " + it.Detail) { Foreground = Theme.AccentText, FontSize = 12 });
            sp.Children.Add(title);
            if (it.Note != null) sp.Children.Add(new TextBlock { Text = it.Note, FontSize = 11.5, Foreground = it.NoteColor ?? Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            g.Children.Add(Ui.At(sp, 0, 1));

            var row = new Border { Padding = new Thickness(8, 6, 8, 6), CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, Child = g, Cursor = it.Enabled ? Cursors.Hand : null };
            Action paint = () =>
            {
                box.Background = it.Checked ? Theme.Accent : Brushes.Transparent;
                box.BorderBrush = it.Checked ? Theme.Accent : (it.Enabled ? Theme.Sub : Theme.Line);
                mark.Visibility = it.Checked ? Visibility.Visible : Visibility.Hidden;
                row.Opacity = it.Enabled ? 1 : 0.6;
            };
            row.Tag = paint;
            paint();
            if (it.Enabled)
            {
                row.MouseEnter += (s, e) => row.Background = Theme.Second;
                row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonUp += (s, e) => { it.Checked = !it.Checked; paint(); changed(); e.Handled = true; };
                row.MouseLeftButtonDown += (s, e) => e.Handled = true;   // не перетаскивать окно
            }
            return row;
        }
    }

    public class PickItem
    {
        public string Group;
        public string Text;
        public string Detail;      // «было → станет»
        public string Note;        // пояснение снизу (предупреждение, почему недоступно)
        public Brush NoteColor;
        public bool Checked;
        public bool Enabled = true;
        public object Tag;
    }
}
