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

        public static bool Ask(Window owner, string title, string text, string yes = "Да", string no = "Отмена", bool danger = false)
        {
            var d = new Dialog(owner, title, text, yes, no, danger);
            d.ShowDialog();
            return d.result;
        }

        public static bool AskList(Window owner, string title, string text, System.Collections.Generic.IList<string> items, string yes, bool danger)
        {
            var d = new Dialog(owner, title, text, yes, "Отмена", danger, items);
            d.ShowDialog();
            return d.result;
        }

        public static void Info(Window owner, string title, string text)
        {
            new Dialog(owner, title, text, "Понятно", null, false).ShowDialog();
        }
    }
}
