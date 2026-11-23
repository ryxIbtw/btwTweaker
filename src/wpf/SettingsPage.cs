using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace BtwTweaker
{
    // Параметры: внешний вид, резервная копия, о программе
    public class SettingsPage : StackPanel
    {
        readonly MainWindow owner;
        Appearance Cfg { get { return Theme.Cfg; } }

        public SettingsPage(MainWindow w)
        {
            owner = w;

            Children.Add(Header("Внешний вид", true));

            string[] modes = { "dark", "light", "system" };
            Children.Add(Row("", "Тема", "Светлое или тёмное оформление программы.",
                Ui.Segmented(new[] { "Тёмная", "Светлая", "Как в Windows" }, Array.IndexOf(modes, Cfg.Mode), i => Set(() => Cfg.Mode = modes[i]))));

            Children.Add(AccentRow());

            string[] backs = { "gradient", "solid", "mica" };
            bool mica = Theme.MicaSupported;
            Children.Add(Row("", "Фон окна", "Градиент с мягким свечением акцента, однотонный или полупрозрачный Mica — сквозь окно слегка видны обои.",
                Ui.Segmented(new[] { "Градиент", "Однотонный", "Mica" }, Array.IndexOf(backs, Cfg.Backdrop), i => Set(() => Cfg.Backdrop = backs[i]),
                    new[] { false, false, !mica }, new[] { null, null, mica ? null : "Mica доступна в Windows 11 22H2 и новее" })));

            var anim = new ToggleButton { Style = Theme.S("Switch"), IsChecked = Cfg.Animations };
            anim.Click += (s, e) => Set(() => Cfg.Animations = anim.IsChecked == true);
            Children.Add(Row("", "Анимации", "Плавные переключатели, появление страниц, выезд меню и подсветка карточек.", anim));

            string[] menus = { "hover", "expanded", "icons" };
            Children.Add(Row("", "Боковое меню", "Раскрывать при наведении, держать раскрытым или показывать только значки.",
                Ui.Segmented(new[] { "При наведении", "Всегда раскрыто", "Только значки" }, Array.IndexOf(menus, Cfg.Menu), i => Set(() => Cfg.Menu = menus[i]))));

            var reset = Ui.Btn("Сбросить", Theme.Second, () => Set(() =>
            {
                var d = new Appearance();
                Cfg.Mode = d.Mode; Cfg.Accent = d.Accent; Cfg.Backdrop = d.Backdrop; Cfg.Animations = d.Animations; Cfg.Menu = d.Menu;
            }));
            Children.Add(Row("", "Оформление по умолчанию", "Вернуть тёмную тему, синий акцент, градиентный фон и меню при наведении.", reset));

            // ── Обновления ──
            Children.Add(Header("Обновления", false));
            var check = Ui.IconBtn("", "Проверить сейчас", Theme.Accent, Theme.OnAccent, () => owner.CheckForUpdates(true));
            Children.Add(Row("", "Версия " + Updater.Current.ToString(3), "Новые версии выходят на GitHub. Программа скачивает их сама и проверяет контрольную сумму.", check));

            var auto = new ToggleButton { Style = Theme.S("Switch"), IsChecked = Sys.Pref("UpdateCheck") != "0" };
            auto.Click += (s, e) => Sys.SetPref("UpdateCheck", auto.IsChecked == true ? "1" : "0");
            Children.Add(Row("", "Проверять при запуске", "При каждом запуске узнавать, не вышла ли новая версия.", auto));

            var silent = new ToggleButton { Style = Theme.S("Switch"), IsChecked = Sys.Pref("UpdateAuto") == "1" };
            silent.Click += (s, e) => Sys.SetPref("UpdateAuto", silent.IsChecked == true ? "1" : "0");
            Children.Add(Row("", "Устанавливать автоматически", "Найденное обновление скачивается и ставится без вопроса, затем программа перезапускается.", silent));

            string prev = Updater.PrevVersion();
            if (prev != null)
                Children.Add(Row("", "Прошлая версия " + prev, "Если с новой версией что-то не так, можно вернуться к прошлой.", Ui.Btn("Вернуть", Theme.Second, owner.RollbackVersion)));

            var gh = Ui.IconBtn("", "Открыть", Theme.Second, Theme.Text, () =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Updater.RepoUrl) { UseShellExecute = true }); } catch { }
            });
            Children.Add(Row("", "btwTweaker на GitHub", Updater.RepoUrl, gh));

            // ── Резервная копия ──
            Children.Add(Header("Резервная копия и восстановление", false));
            var b1 = Ui.IconBtn("", "Создать", Theme.Accent, Theme.OnAccent, () => owner.CreateRestorePoint(false));
            Children.Add(Row("", "Точка восстановления Windows", "Снимок системы, к которому можно вернуться через «Восстановление системы» даже без btwTweaker.", b1));
            var b2 = Ui.IconBtn("", "Откатить всё", Theme.Second, Theme.Danger, owner.RollbackAll);
            Children.Add(Row("", "Откатить все изменения", "Вернуть исходные значения всех настроек, которые менял btwTweaker (изменено: " + Backup.Count + ").", b2));
            var b3 = Ui.IconBtn("", "Открыть", Theme.Second, Theme.Text, owner.OpenBackupFolder);
            Children.Add(Row("", "Папка резервной копии", Backup.Dir, b3));

            // ── О программе ──
            Children.Add(Header("О программе", false));
            var about = new StackPanel();
            var name = new TextBlock { FontFamily = Theme.Display, FontSize = 20 };
            name.Inlines.Add(new System.Windows.Documents.Run("btw") { FontWeight = FontWeights.Bold, Foreground = Theme.Text });
            name.Inlines.Add(new System.Windows.Documents.Run("Tweaker") { Foreground = Theme.Sub });
            about.Children.Add(name);
            about.Children.Add(Ui.Txt("Версия " + AppMain.Version + " · " + Sys.OsName, 13, Theme.Sub));
            about.Children.Add(new TextBlock { Text = "Бесплатный твикер для Windows 10 и 11. Все изменения запоминаются и откатываются.", FontSize = 13, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            Children.Add(Ui.Card(about, new Thickness(20, 16, 20, 16), false));
        }

        // Изменить настройку, сохранить и пересобрать окно
        void Set(Action change)
        {
            change();
            Cfg.Save();
            owner.Rebuild();
        }

        static TextBlock Header(string text, bool first)
        {
            var t = Ui.Txt(text, 15, Theme.Text, FontWeights.SemiBold);
            t.Margin = new Thickness(2, first ? 0 : 22, 0, 10);
            return t;
        }

        // Строка настройки: значок, название, пояснение и элемент управления справа (или снизу)
        static Border Row(string icon, string title, string desc, FrameworkElement control, bool below = false)
        {
            var g = Ui.Cols("auto", "*", "auto");
            var circle = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = Theme.AccentSoft, VerticalAlignment = VerticalAlignment.Top, Child = Ui.Icon(icon, 15, Theme.Accent) };
            g.Children.Add(Ui.At(circle, 0, 0));
            var sp = new StackPanel { Margin = new Thickness(14, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(Ui.Txt(title, 14, Theme.Text, FontWeights.SemiBold));
            sp.Children.Add(new TextBlock { Text = desc, FontSize = 12.5, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            if (below) { control.Margin = new Thickness(0, 12, 0, 0); sp.Children.Add(control); }
            g.Children.Add(Ui.At(sp, 0, 1));
            if (!below) { control.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(Ui.At(control, 0, 2)); }
            var card = Ui.Card(g, new Thickness(18, 14, 18, 14), false);
            card.Margin = new Thickness(0, 0, 0, 8);
            return card;
        }

        // Цвет акцента: готовые цвета, «как в Windows» и свой #HEX
        Border AccentRow()
        {
            var wrap = new WrapPanel();
            foreach (var hex in Theme.AccentPresets) wrap.Children.Add(Swatch(hex, Theme.C(hex), null));
            wrap.Children.Add(Swatch("system", Theme.SystemAccent(), ""));

            var custom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var box = new TextBox { Style = Theme.S("Search"), Width = 120, Height = 32, Padding = new Thickness(10, 4, 10, 4), Text = Cfg.Accent == "system" ? Theme.Hex(Theme.SystemAccent()) : Cfg.Accent, MaxLength = 7 };
            custom.Children.Add(box);
            var apply = Ui.Btn("Применить", Theme.Second, () =>
            {
                string t = box.Text.Trim();
                if (!t.StartsWith("#")) t = "#" + t;
                try { Theme.C(t); }
                catch { Dialog.Info(owner, "Неверный цвет", "Введите цвет в формате #RRGGBB, например #4F6BFF."); return; }
                if (t.Length != 7) { Dialog.Info(owner, "Неверный цвет", "Введите цвет в формате #RRGGBB, например #4F6BFF."); return; }
                Set(() => Cfg.Accent = t.ToUpperInvariant());
            });
            apply.Margin = new Thickness(8, 0, 0, 0);
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) apply.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); };
            custom.Children.Add(apply);
            custom.Children.Add(new TextBlock { Text = "свой цвет", FontSize = 12.5, Foreground = Theme.Sub, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });

            var sp = new StackPanel();
            sp.Children.Add(wrap);
            sp.Children.Add(custom);
            return Row("", "Цвет акцента", "Цвет кнопок, переключателей и подсветки. Можно взять цвет из Windows.", sp, true);
        }

        FrameworkElement Swatch(string value, Color color, string glyph)
        {
            bool on = string.Equals(Cfg.Accent, value, StringComparison.OrdinalIgnoreCase);
            var inner = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = new SolidColorBrush(color) };
            if (glyph != null) inner.Child = Ui.Icon(glyph, 12, Brushes.White);
            else if (on) inner.Child = Ui.Icon("", 12, Brushes.White);
            var ring = new Border
            {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Margin = new Thickness(0, 0, 8, 0),
                BorderThickness = new Thickness(2), BorderBrush = on ? new SolidColorBrush(color) : Brushes.Transparent,
                Child = inner, Cursor = Cursors.Hand, Background = Brushes.Transparent,
                ToolTip = value == "system" ? "Как в Windows" : value
            };
            if (!on)
            {
                ring.MouseEnter += (s, e) => ring.BorderBrush = Theme.Line;
                ring.MouseLeave += (s, e) => ring.BorderBrush = Brushes.Transparent;
                ring.MouseLeftButtonUp += (s, e) => Set(() => Cfg.Accent = value);
            }
            return ring;
        }
    }
}
