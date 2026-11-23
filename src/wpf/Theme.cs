using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace BtwTweaker
{
    // Настройки внешнего вида (HKCU\Software\BtwTweaker)
    public class Appearance
    {
        public string Mode = "dark";        // dark | light | system
        public string Accent = "#4F6BFF";   // #RRGGBB | system
        public string Backdrop = "gradient"; // gradient | solid | mica
        public bool Animations = true;
        public string Menu = "hover";       // hover | expanded | icons

        public static Appearance Load()
        {
            var a = new Appearance();
            a.Mode = Sys.Pref("UiMode") ?? a.Mode;
            a.Accent = Sys.Pref("UiAccent") ?? a.Accent;
            a.Backdrop = Sys.Pref("UiBackdrop") ?? a.Backdrop;
            a.Animations = Sys.Pref("UiAnimations") != "0";
            a.Menu = Sys.Pref("UiMenu") ?? a.Menu;
#if TESTHOOKS
            // Для снимков: BTW_SNAP_UI=mode;accent;backdrop;menu — без записи в реестр
            var env = Environment.GetEnvironmentVariable("BTW_SNAP_UI");
            if (!string.IsNullOrEmpty(env))
            {
                var p = env.Split(';');
                if (p.Length > 0 && p[0].Length > 0) a.Mode = p[0];
                if (p.Length > 1 && p[1].Length > 0) a.Accent = p[1];
                if (p.Length > 2 && p[2].Length > 0) a.Backdrop = p[2];
                if (p.Length > 3 && p[3].Length > 0) a.Menu = p[3];
            }
#endif
            return a;
        }

        public void Save()
        {
#if TESTHOOKS
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BTW_SNAP"))) return;
#endif
            Sys.SetPref("UiMode", Mode);
            Sys.SetPref("UiAccent", Accent);
            Sys.SetPref("UiBackdrop", Backdrop);
            Sys.SetPref("UiAnimations", Animations ? "1" : "0");
            Sys.SetPref("UiMenu", Menu);
        }
    }

    // Палитра, шрифты и стили. Пересчитывается при смене настроек (Apply), после чего окно пересобирается.
    public static class Theme
    {
        public static readonly FontFamily Font = new FontFamily("Segoe UI Variable Text, Segoe UI");
        public static readonly FontFamily Display = new FontFamily("Segoe UI Variable Display, Segoe UI");
        public static readonly FontFamily Icons = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        public static Appearance Cfg = Appearance.Load();
        public static bool Light;
        public static bool Mica;          // фон Mica реально включён (Windows 11 22H2+)
        public static Color AccentColor;
        public static Color BaseColor, CardColor, CardHoverColor;

        public static Brush Text, Sub, Accent, AccentSoft, OnAccent, Card, CardHover, Line, Field, Second,
                            Side, NavActive, Popup, PopupBorder, Banner, BannerBorder, Danger, ListBg, Track, Knob,
                            AccentText, Done, DoneBorder;
        public static readonly Brush Yellow = B("#F5C542");
        public static readonly Brush Red = B("#E5484D");

        public static readonly string[] AccentPresets = { "#4F6BFF", "#8B5CF6", "#06B6D4", "#14B8A6", "#22C55E", "#F59E0B", "#F97316", "#EC4899", "#EF4444" };

        public static Duration Anim { get { return new Duration(TimeSpan.FromMilliseconds(Cfg.Animations ? 180 : 0)); } }
        public static Duration AnimSlow { get { return new Duration(TimeSpan.FromMilliseconds(Cfg.Animations ? 260 : 0)); } }

        public static SolidColorBrush B(string hex)
        {
            var b = new SolidColorBrush(C(hex));
            b.Freeze();
            return b;
        }

        static SolidColorBrush B(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        public static Color A(Color c, byte alpha) { return Color.FromArgb(alpha, c.R, c.G, c.B); }
        public static string Hex(Color c) { return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }

        static Color Mix(Color a, Color b, double t)
        {
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        public static bool SystemLight()
        {
            int? v = Reg.GetInt(Reg.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme");
            return v.HasValue && v.Value == 1;
        }

        public static Color SystemAccent()
        {
            int? v = Reg.GetInt(Reg.HKCU, @"Software\Microsoft\Windows\DWM", "AccentColor");   // 0xAABBGGRR
            if (!v.HasValue) return C("#4F6BFF");
            uint u = (uint)v.Value;
            return Color.FromRgb((byte)(u & 0xFF), (byte)((u >> 8) & 0xFF), (byte)((u >> 16) & 0xFF));
        }

        public static bool MicaSupported { get { return Sys.Build >= 22621; } }

        static bool IsHex(string s)
        {
            if (s == null || s.Length != 7 || s[0] != '#') return false;
            for (int i = 1; i < 7; i++) if (Uri.IsHexDigit(s[i]) == false) return false;
            return true;
        }

        // Пересчитать палитру и переустановить стили
        public static void Apply()
        {
            Light = Cfg.Mode == "light" || (Cfg.Mode == "system" && SystemLight());
            AccentColor = Cfg.Accent == "system" ? SystemAccent() : IsHex(Cfg.Accent) ? C(Cfg.Accent) : C("#4F6BFF");
            Mica = Cfg.Backdrop == "mica" && MicaSupported;

            // Текст на акценте: белый, если акцент тёмный, иначе почти чёрный
            double lum = (0.299 * AccentColor.R + 0.587 * AccentColor.G + 0.114 * AccentColor.B) / 255;
            OnAccent = B(lum > 0.72 ? C("#111322") : Colors.White);
            Accent = B(AccentColor);

            if (!Light)
            {
                BaseColor = Mix(C("#0A0B1A"), AccentColor, 0.04);
                CardColor = Mica ? A(C("#0E1026"), 0xD9) : Mix(C("#0E1026"), AccentColor, 0.02);
                CardHoverColor = Mica ? A(C("#181B3E"), 0xE6) : Mix(C("#151838"), AccentColor, 0.05);
                Text = B("#ECEEFF"); Sub = B("#8E93B8");
                Line = B(A(C("#FFFFFF"), 0x14)); Field = B(A(C("#FFFFFF"), 0x0D)); Second = B(A(C("#FFFFFF"), 0x12));
                Side = B(Mica ? A(C("#0A0B1A"), 0xE6) : Mix(C("#08091A"), AccentColor, 0.03));
                Popup = B(C("#14163A")); PopupBorder = B(A(C("#FFFFFF"), 0x1F));
                ListBg = B(A(C("#000000"), 0x33));
                Track = B(A(C("#FFFFFF"), 0x1A)); Knob = B("#C3C7E6");
                Danger = B("#FF8A8D");
                AccentSoft = B(A(AccentColor, 0x33));
                NavActive = B(A(AccentColor, 0x2E));
                Banner = B(A(AccentColor, 0x26)); BannerBorder = B(A(AccentColor, 0x55));
            }
            else
            {
                BaseColor = Mix(C("#F2F3F9"), AccentColor, 0.04);
                CardColor = Mica ? A(Colors.White, 0xCC) : Colors.White;
                CardHoverColor = Mica ? A(C("#F6F7FD"), 0xE6) : Mix(C("#F6F7FD"), AccentColor, 0.04);
                Text = B("#15172B"); Sub = B("#5E6386");
                Line = B(A(C("#000000"), 0x14)); Field = B(Colors.White); Second = B(A(C("#000000"), 0x0A));
                Side = B(Mica ? A(Colors.White, 0xE6) : Colors.White);
                Popup = B(Colors.White); PopupBorder = B(A(C("#000000"), 0x1A));
                ListBg = B(A(C("#000000"), 0x0A));
                Track = B(A(C("#000000"), 0x1F)); Knob = B(Colors.White);
                Danger = B("#D93036");
                AccentSoft = B(A(AccentColor, 0x26));
                NavActive = B(A(AccentColor, 0x1F));
                Banner = B(A(AccentColor, 0x1A)); BannerBorder = B(A(AccentColor, 0x4D));
            }
            Card = B(CardColor);
            CardHover = B(CardHoverColor);
            // Акцент для надписей: в тёмной теме светлее, в светлой — темнее, чтобы хорошо читался на мягком фоне
            AccentText = B(Light ? Mix(AccentColor, C("#000000"), 0.28) : Mix(AccentColor, Colors.White, 0.3));
            Done = B(A(AccentColor, (byte)(Light ? 0x1F : 0x2B)));
            DoneBorder = B(A(AccentColor, (byte)(Light ? 0x40 : 0x4D)));

            var app = Application.Current;
            app.Resources.MergedDictionaries.Clear();
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(BuildXaml()));
        }

        // Слои фона окна: база + мягкое свечение акцента (для режима «Градиент»)
        public static UIElement BackgroundLayer()
        {
            var g = new System.Windows.Controls.Grid { IsHitTestVisible = false };
            if (Mica) return g;
            g.Children.Add(new System.Windows.Controls.Border { Background = B(BaseColor) });
            if (Cfg.Backdrop == "gradient" || (Cfg.Backdrop == "mica" && !Mica))
            {
                var top = new RadialGradientBrush { Center = new Point(0.85, -0.05), GradientOrigin = new Point(0.85, -0.05), RadiusX = 0.75, RadiusY = 0.9 };
                top.GradientStops.Add(new GradientStop(A(AccentColor, (byte)(Light ? 0x30 : 0x48)), 0));
                top.GradientStops.Add(new GradientStop(A(AccentColor, 0), 1));
                g.Children.Add(new System.Windows.Controls.Border { Background = top });
                var bottom = new RadialGradientBrush { Center = new Point(0.05, 1.05), GradientOrigin = new Point(0.05, 1.05), RadiusX = 0.6, RadiusY = 0.7 };
                bottom.GradientStops.Add(new GradientStop(A(AccentColor, (byte)(Light ? 0x18 : 0x26)), 0));
                bottom.GradientStops.Add(new GradientStop(A(AccentColor, 0), 1));
                g.Children.Add(new System.Windows.Controls.Border { Background = bottom });
            }
            return g;
        }

        public static Brush WindowBackground() { return Mica ? (Brush)Brushes.Transparent : B(BaseColor); }

        public static Brush LogoBrush()
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            g.GradientStops.Add(new GradientStop(Mix(AccentColor, Colors.White, 0.15), 0));
            g.GradientStops.Add(new GradientStop(Mix(AccentColor, C("#7C3AED"), 0.55), 1));
            g.Freeze();
            return g;
        }

        public static Brush HeroBrush()
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            g.GradientStops.Add(new GradientStop(Mix(AccentColor, C("#000000"), Light ? 0.0 : 0.15), 0));
            g.GradientStops.Add(new GradientStop(Mix(AccentColor, C("#7C3AED"), 0.6), 1));
            g.Freeze();
            return g;
        }

        public static Style S(string key) { return (Style)Application.Current.Resources[key]; }

        public static void Install(Application app) { Apply(); }

        static string BuildXaml()
        {
            string dur = Cfg.Animations ? "0:0:0.18" : "0:0:0";
            return Xaml
                .Replace("%ACCENT%", Hex(AccentColor))
                .Replace("%ONACCENT%", Argb(OnAccent))
                .Replace("%TRACK%", Argb(Track)).Replace("%KNOB%", Argb(Knob))
                .Replace("%TEXT%", Argb(Text)).Replace("%SUB%", Argb(Sub))
                .Replace("%FIELD%", Argb(Field)).Replace("%LINE%", Argb(Line))
                .Replace("%POPUP%", Argb(Popup)).Replace("%POPUPBORDER%", Argb(PopupBorder))
                .Replace("%THUMB%", Light ? "#33000000" : "#33FFFFFF")
                .Replace("%DUR%", dur);
        }

        static string Argb(Brush b)
        {
            var c = ((SolidColorBrush)b).Color;
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
        }

        const string Xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>

  <!-- Переключатель: бегунок плавно едет вправо, когда включено -->
  <Style x:Key='Switch' TargetType='ToggleButton'>
    <Setter Property='Width' Value='44'/>
    <Setter Property='Height' Value='24'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ToggleButton'>
          <Grid Background='Transparent'>
            <Border x:Name='T' CornerRadius='12' Background='%TRACK%'/>
            <Border x:Name='K' Width='18' Height='18' CornerRadius='9' Background='%KNOB%' HorizontalAlignment='Left' Margin='3,0'>
              <Border.RenderTransform><TranslateTransform x:Name='KX' X='0'/></Border.RenderTransform>
              <TextBlock x:Name='C' Text='&#xE73E;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='10' FontWeight='Bold'
                         Foreground='%ACCENT%' HorizontalAlignment='Center' VerticalAlignment='Center' Opacity='0'/>
            </Border>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='T' Property='Background' Value='%ACCENT%'/>
              <Setter TargetName='K' Property='Background' Value='White'/>
              <Trigger.EnterActions>
                <BeginStoryboard>
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName='KX' Storyboard.TargetProperty='X' To='20' Duration='%DUR%'>
                      <DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetName='C' Storyboard.TargetProperty='Opacity' To='1' Duration='%DUR%'/>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard>
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName='KX' Storyboard.TargetProperty='X' To='0' Duration='%DUR%'>
                      <DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetName='C' Storyboard.TargetProperty='Opacity' To='0' Duration='%DUR%'/>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='T' Property='Opacity' Value='0.88'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.35'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Кнопка со скруглением; цвет — через Background -->
  <Style x:Key='Btn' TargetType='Button'>
    <Setter Property='Foreground' Value='%ONACCENT%'/>
    <Setter Property='Background' Value='%ACCENT%'/>
    <Setter Property='BorderBrush' Value='Transparent'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Padding' Value='14,7'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' CornerRadius='9' Padding='{TemplateBinding Padding}'
                  BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'>
            <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Opacity' Value='0.85'/></Trigger>
            <Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='Opacity' Value='0.7'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Кнопки окна (свернуть / развернуть / закрыть) -->
  <Style x:Key='Caption' TargetType='Button'>
    <Setter Property='Foreground' Value='%TEXT%'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Width' Value='46'/>
    <Setter Property='Height' Value='34'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='%LINE%'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key='CaptionClose' TargetType='Button' BasedOn='{StaticResource Caption}'>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='Transparent'>
            <ContentPresenter x:Name='P' HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#E81123'/>
              <Setter Property='Foreground' Value='White'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Поле ввода (поиск, свой цвет) -->
  <Style x:Key='Search' TargetType='TextBox'>
    <Setter Property='Foreground' Value='%TEXT%'/>
    <Setter Property='CaretBrush' Value='%TEXT%'/>
    <Setter Property='SelectionBrush' Value='%ACCENT%'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border x:Name='B' Background='%FIELD%' CornerRadius='10' BorderBrush='%LINE%' BorderThickness='1'>
            <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='%ACCENT%'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Тонкая полоса прокрутки -->
  <Style TargetType='ScrollBar'>
    <Setter Property='Width' Value='6'/>
    <Setter Property='MinWidth' Value='6'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Border Background='Transparent' CornerRadius='3'>
            <Track x:Name='PART_Track' IsDirectionReversed='True'>
              <Track.Thumb>
                <Thumb>
                  <Thumb.Template>
                    <ControlTemplate TargetType='Thumb'>
                      <Border CornerRadius='3' Background='%THUMB%'/>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
            </Track>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ToolTip'>
    <Setter Property='Foreground' Value='%TEXT%'/>
    <Setter Property='Background' Value='%POPUP%'/>
    <Setter Property='BorderBrush' Value='%POPUPBORDER%'/>
    <Setter Property='Padding' Value='10,6'/>
    <Setter Property='MaxWidth' Value='340'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ToolTip'>
          <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='8' Padding='{TemplateBinding Padding}'>
            <ContentPresenter>
              <ContentPresenter.Resources>
                <Style TargetType='TextBlock'><Setter Property='TextWrapping' Value='Wrap'/></Style>
              </ContentPresenter.Resources>
            </ContentPresenter>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";
    }
}
