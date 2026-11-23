using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BtwTweaker
{
    // Короткие помощники для построения интерфейса из кода
    public static class Ui
    {
        public static TextBlock Txt(string text, double size, Brush fg, FontWeight? weight = null)
        {
            var t = new TextBlock { Text = text, FontSize = size, Foreground = fg, VerticalAlignment = VerticalAlignment.Center };
            if (weight.HasValue) t.FontWeight = weight.Value;
            return t;
        }

        public static TextBlock Icon(string glyph, double size, Brush fg)
        {
            return new TextBlock
            {
                Text = glyph, FontFamily = Theme.Icons, FontSize = size, Foreground = fg,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        // Цвет текста под фон кнопки
        static Brush TextFor(Brush bg)
        {
            if (bg == Theme.Accent) return Theme.OnAccent;
            if (bg == Theme.Red) return Brushes.White;
            return Theme.Text;
        }

        public static Button Btn(object content, Brush bg, Action click)
        {
            var b = new Button { Content = content, Style = Theme.S("Btn"), Background = bg, Foreground = TextFor(bg) };
            b.Click += (s, e) => click();
            return b;
        }

        // Кнопка «значок + текст»
        public static Button IconBtn(string glyph, string text, Brush bg, Brush fg, Action click)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = glyph, FontFamily = Theme.Icons, FontSize = 13, Foreground = fg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            sp.Children.Add(new TextBlock { Text = text, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
            var b = Btn(sp, bg, click);
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            return b;
        }

        // Плавная подсветка карточки при наведении
        public static void Hover(Border b)
        {
            var brush = new SolidColorBrush(Theme.CardColor);
            b.Background = brush;
            b.MouseEnter += (s, e) => brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Theme.CardHoverColor, Theme.Anim));
            b.MouseLeave += (s, e) => brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Theme.CardColor, Theme.Anim));
        }

        // Карточка-основа
        public static Border Card(UIElement child, Thickness padding, bool hover = true)
        {
            var b = new Border { CornerRadius = new CornerRadius(14), Padding = padding, Child = child, BorderBrush = Theme.Line, BorderThickness = new Thickness(1) };
            if (hover) Hover(b); else b.Background = Theme.Card;
            return b;
        }

        // Появление элемента: прозрачность и лёгкий сдвиг снизу
        public static void FadeIn(FrameworkElement el, double delayMs = 0)
        {
            if (!Theme.Cfg.Animations) return;
            var tt = new TranslateTransform(0, 10);
            el.RenderTransform = tt;
            el.Opacity = 0;
            var begin = TimeSpan.FromMilliseconds(delayMs);
            var fade = new DoubleAnimation(0, 1, Theme.AnimSlow) { BeginTime = begin };
            var move = new DoubleAnimation(10, 0, Theme.AnimSlow) { BeginTime = begin, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            el.BeginAnimation(UIElement.OpacityProperty, fade);
            tt.BeginAnimation(TranslateTransform.YProperty, move);
        }

        // Сегментный переключатель (несколько вариантов в одной «таблетке»)
        public static Border Segmented(string[] labels, int selected, Action<int> pick, bool[] disabled = null, string[] tips = null)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                bool on = i == selected;
                bool off = disabled != null && disabled[i];
                var item = new Border
                {
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(2),
                    Background = on ? Theme.Accent : Brushes.Transparent,
                    Cursor = off ? null : System.Windows.Input.Cursors.Hand,
                    Opacity = off ? 0.4 : 1,
                    Child = Txt(labels[i], 13, on ? Theme.OnAccent : Theme.Text)
                };
                if (tips != null && tips[i] != null) item.ToolTip = tips[i];
                if (!on && !off)
                {
                    item.MouseEnter += (s, e) => item.Background = Theme.Second;
                    item.MouseLeave += (s, e) => item.Background = Brushes.Transparent;
                    item.MouseLeftButtonUp += (s, e) => pick(idx);
                }
                sp.Children.Add(item);
            }
            return new Border { Background = Theme.Field, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(2), Child = sp, HorizontalAlignment = HorizontalAlignment.Left };
        }

        public static T At<T>(T el, int row, int col) where T : UIElement
        {
            Grid.SetRow(el, row);
            Grid.SetColumn(el, col);
            return el;
        }

        public static Grid Cols(params string[] widths)
        {
            var g = new Grid();
            foreach (var w in widths)
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = Len(w) });
            return g;
        }

        public static Grid Rows(params string[] heights)
        {
            var g = new Grid();
            foreach (var h in heights)
                g.RowDefinitions.Add(new RowDefinition { Height = Len(h) });
            return g;
        }

        static GridLength Len(string s)
        {
            if (s == "auto") return GridLength.Auto;
            if (s.EndsWith("*")) return new GridLength(s == "*" ? 1 : double.Parse(s.TrimEnd('*'), System.Globalization.CultureInfo.InvariantCulture), GridUnitType.Star);
            return new GridLength(double.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
