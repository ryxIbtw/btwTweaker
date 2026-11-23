using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace BtwTweaker
{
    // «Автозагрузки»: реестр, папки автозагрузки, задания и сторонние службы
    public class StartupPage : StackPanel
    {
        readonly MainWindow owner;
        readonly StackPanel body = new StackPanel();
        readonly TextBox search;
        readonly TextBlock progress = new TextBlock { FontSize = 12.5, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        List<StartupItem> items = new List<StartupItem>();
        readonly Dictionary<string, ImageSource> icons = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        Button junk, add, reload;
        bool busy;
        static int filter;   // 0 все, 1 реестр, 2 файлы, 3 задачи, 4 службы

        static readonly string[] KindNames = { L.T("Ключи реестра"), L.T("Файлы"), L.T("Задачи"), L.T("Службы") };

        public StartupPage(MainWindow w)
        {
            owner = w;

            // Панель действий
            var g = Ui.Cols("auto", "*", "auto", "auto", "auto", "auto");
            g.Children.Add(Ui.At(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Theme.AccentSoft, Child = Ui.Icon("", 17, Theme.AccentText) }, 0, 0));
            var sp = new StackPanel { Margin = new Thickness(14, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(Ui.Txt(L.T("Что запускается вместе с Windows"), 14, Theme.Text, FontWeights.SemiBold));
            sp.Children.Add(new TextBlock { Text = L.T("Отключение и удаление ключей и заданий возвращает «Откатить всё», удалённые файлы попадают в Корзину. Службы можно только отключить — не удалить."), FontSize = 12.5, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            g.Children.Add(Ui.At(sp, 0, 1));
            junk = Ui.IconBtn("", L.T("Отключить ненужное"), Theme.Accent, Theme.OnAccent, DisableJunk);
            junk.Margin = new Thickness(0, 0, 8, 0);
            junk.ToolTip = L.T("Отключить апдейтеры, сборщики сбоев и телеметрию сторонних программ. Сначала покажу список.");
            g.Children.Add(Ui.At(junk, 0, 2));
            add = Ui.IconBtn("", L.T("Добавить"), Theme.Second, Theme.Text, AddProgram);
            add.Margin = new Thickness(0, 0, 8, 0);
            g.Children.Add(Ui.At(add, 0, 3));
            var export = Ui.IconBtn("", L.T("Экспорт"), Theme.Second, Theme.Text, Export);
            export.Margin = new Thickness(0, 0, 8, 0);
            export.ToolTip = L.T("Сохранить список автозагрузки в файл CSV (открывается в Excel)");
            g.Children.Add(Ui.At(export, 0, 4));
            reload = Ui.IconBtn("", L.T("Обновить"), Theme.Second, Theme.Text, Load);
            g.Children.Add(Ui.At(reload, 0, 5));

            var sg = new Grid { Margin = new Thickness(54, 12, 0, 0), Height = 32 };
            search = new TextBox { Style = Theme.S("Search"), Padding = new Thickness(32, 4, 10, 4) };
            var hint = new TextBlock { Text = L.T("Поиск по названию, команде или издателю"), Foreground = Theme.Sub, FontSize = 13, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(34, 0, 0, 0) };
            var si = Ui.Icon("", 12, Theme.Sub);
            si.HorizontalAlignment = HorizontalAlignment.Left;
            si.Margin = new Thickness(12, 0, 0, 0);
            si.IsHitTestVisible = false;
            sg.Children.Add(search); sg.Children.Add(hint); sg.Children.Add(si);
            search.TextChanged += (s, e) => { hint.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Render(); };

            var top = new StackPanel();
            top.Children.Add(g);
            top.Children.Add(sg);
            top.Children.Add(progress);
            var card = Ui.Card(top, new Thickness(18, 14, 14, 14), false);
            card.Margin = new Thickness(0, 0, 0, 6);
            Children.Add(card);
            Children.Add(missingBar);

            var seg = Ui.Segmented(new[] { L.T("Все"), L.T("Реестр"), L.T("Файлы"), L.T("Задачи"), L.T("Службы") }, filter, i => { filter = i; RebuildFilter(); });
            seg.Margin = new Thickness(0, 4, 0, 4);
            segHost.Child = seg;
            Children.Add(segHost);
            Children.Add(body);
            Load();
        }

        readonly Border segHost = new Border();
        readonly Border missingBar = new Border { Visibility = Visibility.Collapsed };

        // Плашка «записи с удалённой программой»
        void UpdateMissingBar()
        {
            var broken = items.FindAll(i => i.Missing && Startup.CanDelete(i));
            if (broken.Count == 0) { missingBar.Visibility = Visibility.Collapsed; return; }
            var g = Ui.Cols("auto", "*", "auto");
            g.Children.Add(Ui.At(Ui.Icon("", 16, Theme.Yellow), 0, 0));
            var t = Ui.Txt(L.T("Записей, которые ссылаются на удалённую программу: ") + broken.Count + L.T(". Они ничего не запускают и только засоряют автозагрузку."), 13, Theme.Text);
            t.TextWrapping = TextWrapping.Wrap;
            t.Margin = new Thickness(12, 0, 12, 0);
            g.Children.Add(Ui.At(t, 0, 1));
            g.Children.Add(Ui.At(Ui.Btn(L.T("Убрать битые"), Theme.Accent, RemoveBroken), 0, 2));
            missingBar.Child = g;
            missingBar.Background = Theme.Banner;
            missingBar.BorderBrush = Theme.BannerBorder;
            missingBar.BorderThickness = new Thickness(1);
            missingBar.CornerRadius = new CornerRadius(12);
            missingBar.Padding = new Thickness(16, 10, 10, 10);
            missingBar.Margin = new Thickness(0, 0, 0, 6);
            missingBar.Visibility = Visibility.Visible;
        }

        void RebuildFilter()
        {
            var seg = Ui.Segmented(new[] { L.T("Все"), L.T("Реестр"), L.T("Файлы"), L.T("Задачи"), L.T("Службы") }, filter, i => { filter = i; RebuildFilter(); });
            seg.Margin = new Thickness(0, 4, 0, 4);
            segHost.Child = seg;
            Render();
        }

        void SetBusy(bool on, string text)
        {
            busy = on;
            junk.IsEnabled = add.IsEnabled = reload.IsEnabled = !on;
            progress.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
            progress.Text = text ?? "";
            progress.Foreground = Theme.AccentText;
            Mouse.OverrideCursor = on ? Cursors.Wait : null;
        }

        async void Load()
        {
            if (busy) return;
            SetBusy(true, null);
            body.Children.Clear();
            body.Children.Add(Ui.Txt(L.T("Читаю автозагрузку…"), 13, Theme.Sub));
            Exception err = null;
            try { items = await Task.Run(() => Startup.List()); }
            catch (Exception ex) { err = ex; items = new List<StartupItem>(); }
            SetBusy(false, null);
            if (err != null) { body.Children.Clear(); body.Children.Add(Ui.Txt(err.Message, 13, Theme.Red)); return; }
            UpdateMissingBar();
            Render();
            Ui.FadeIn(body);
            LoadExtras();
        }

        // Иконки и издатели — в фоне
        async void LoadExtras()
        {
            var ui = Dispatcher;
            var list = new List<StartupItem>(items);
            await Task.Run(() =>
            {
                foreach (var it in list)
                {
                    it.Publisher = Startup.PublisherOf(it.Exe);
                    if (it.Exe != null && !icons.ContainsKey(it.Exe)) { var ic = IconOf(it.Exe); lock (icons) icons[it.Exe] = ic; }
                }
            });
            if (!ui.HasShutdownStarted) Render();
        }

        static ImageSource IconOf(string exe)
        {
            try
            {
                if (!System.IO.File.Exists(exe)) return null;
                using (var ic = System.Drawing.Icon.ExtractAssociatedIcon(exe))
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
            }
            catch { return null; }
        }

        bool Match(StartupItem it)
        {
            if (filter > 0 && (int)it.Kind != filter - 1) return false;
            string q = search.Text.Trim();
            if (q.Length == 0) return true;
            return Has(it.Name, q) || Has(it.Command, q) || Has(it.Publisher, q);
        }

        static bool Has(string s, string q) { return s != null && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0; }

        void Render()
        {
            body.Children.Clear();
            int shown = 0;
            for (int k = 0; k < 4; k++)
            {
                var part = items.FindAll(i => (int)i.Kind == k && Match(i));
                if (part.Count == 0) continue;
                part.Sort((a, b) => b.Enabled != a.Enabled ? (b.Enabled ? 1 : -1) : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
                var h = Ui.Txt(KindNames[k] + " · " + part.Count, 15, Theme.Text, FontWeights.SemiBold);
                h.Margin = new Thickness(2, 16, 0, 10);
                body.Children.Add(h);
                foreach (var it in part) body.Children.Add(Row(it));
                shown += part.Count;
            }
            if (shown == 0) body.Children.Add(new TextBlock { Text = items.Count == 0 ? L.T("Автозагрузка пуста.") : L.T("Ничего не найдено."), FontSize = 13, Foreground = Theme.Sub, Margin = new Thickness(2, 16, 0, 0) });
        }

        // ───────── Строка ─────────

        FrameworkElement Row(StartupItem it)
        {
            var g = Ui.Cols("auto", "*", "auto");
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var logo = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = Theme.Second };
            ImageSource img = null;
            if (it.Exe != null) lock (icons) icons.TryGetValue(it.Exe, out img);
            logo.Child = img != null ? (UIElement)new Image { Source = img, Width = 24, Height = 24 } : Ui.Icon(KindGlyph(it.Kind), 15, Theme.Sub);
            g.Children.Add(Ui.At(logo, 0, 0));

            var names = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Cursor = Cursors.Hand };
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            var t = Ui.Txt(it.Name, 14, Theme.Text);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            top.Children.Add(t);
            var chev = Ui.Icon("", 9, Theme.Sub);
            chev.Margin = new Thickness(7, 2, 0, 0);
            chev.RenderTransformOrigin = new Point(0.5, 0.5);
            chev.RenderTransform = new RotateTransform(0);
            top.Children.Add(chev);
            if (it.Missing) top.Children.Add(Chip(L.T("файл не найден"), Theme.Red));
            if (it.Delayed != null) top.Children.Add(Chip(L.T("отложено"), null));
            if (it.Impact != null && (it.Enabled || it.Delayed != null))
                top.Children.Add(Chip(L.T("влияние: ") + it.Impact.ToLowerInvariant(), it.Impact == L.T("Высокое") ? Theme.Red : it.Impact == L.T("Среднее") ? Theme.Yellow : null));
            if (it.Recommended && it.Enabled) top.Children.Add(Chip(L.T("можно отключить"), null));
            names.Children.Add(top);
            names.Children.Add(new TextBlock { Text = it.Publisher ?? L.T("Издатель не указан"), FontSize = 11.5, Foreground = Theme.Sub, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) });
            g.Children.Add(Ui.At(names, 0, 1));

            // Подробности
            var det = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(48, 10, 4, 2) };
            det.Children.Add(Detail(L.T("Команда"), it.Command));
            det.Children.Add(Detail(L.T("Откуда"), it.Location));
            if (it.Kind == StartKind.Service) det.Children.Add(Detail(L.T("Тип запуска"), it.StartType == 2 ? L.T("Автоматически") : it.StartType == 3 ? L.T("Вручную") : it.StartType == 4 ? L.T("Отключена") : it.StartType.ToString()));
            det.Children.Add(Detail(L.T("Влияние"), it.Impact != null ? it.Impact + L.T(" (замер Windows при последних запусках)") : L.T("нет данных — Windows ещё не замеряла или пункт не запускался")));
            if (it.Delayed != null) det.Children.Add(Detail(L.T("Отложено"), L.T("запускается заданием ") + it.Delayed));

            // Действия
            var acts = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            if (Startup.CanDelay(it))
            {
                if (it.Delayed == null)
                {
                    Button delay = null;
                    delay = SmallBtn("", L.T("Отложить запуск"), () => ShowDelayMenu(it, delay));
                    delay.ToolTip = L.T("Запускать программу через некоторое время после входа — рабочий стол появится быстрее");
                    acts.Children.Add(delay);
                }
                else acts.Children.Add(SmallBtn("", L.T("Не откладывать"), () => DoUndelay(it)));
            }
            if (!string.IsNullOrEmpty(it.Exe) && System.IO.File.Exists(it.Exe))
                acts.Children.Add(SmallBtn("", L.T("Открыть папку"), () => OpenFolder(it.Exe)));
            acts.Children.Add(SmallBtn("", L.T("Найти в интернете"), () => SearchWeb(it)));
            det.Children.Add(acts);
            Grid.SetColumnSpan(det, 3);
            g.Children.Add(Ui.At(det, 1, 0));
            names.MouseLeftButtonUp += (s, e) =>
            {
                bool show = det.Visibility != Visibility.Visible;
                det.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                ((RotateTransform)chev.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(show ? 180 : 0, Theme.Anim));
                if (show) Ui.FadeIn(det);
            };

            // Состояние, переключатель, удаление
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var state = Ui.Txt(it.Delayed != null ? L.T("Отложено") : it.Enabled ? L.T("Включено") : L.T("Отключено"), 13, Theme.AccentText, FontWeights.SemiBold);
            state.Margin = new Thickness(0, 0, 12, 0);
            right.Children.Add(state);
            var sw = new ToggleButton { Style = Theme.S("Switch"), IsChecked = it.Enabled };
            right.Children.Add(sw);
            sw.Click += (s, e) => Toggle(it, sw.IsChecked == true, sw, state);
            if (Startup.CanDelete(it))
            {
                var del = Ui.Btn(L.T("Удалить"), Brushes.Transparent, () => Remove(it));
                del.BorderBrush = Theme.Red;
                del.Foreground = Theme.Danger;
                del.BorderThickness = new Thickness(1.5);
                del.Padding = new Thickness(12, 4, 12, 4);
                del.FontSize = 12.5;
                del.Margin = new Thickness(12, 0, 0, 0);
                right.Children.Add(del);
            }
            else right.Children.Add(new Border { Width = 88 });   // выравнивание со строками с кнопкой
            g.Children.Add(Ui.At(right, 0, 2));

            var card = Ui.Card(g, new Thickness(12, 9, 14, 9));
            card.Margin = new Thickness(0, 0, 0, 6);
            return card;
        }

        static Border Chip(string text, Brush color)
        {
            var fg = color ?? Theme.AccentText;
            var bg = color == null ? Theme.Done : new SolidColorBrush(Theme.A(((SolidColorBrush)color).Color, 0x26));
            return new Border { Background = bg, CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 1, 8, 2), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ui.Txt(text, 11, fg, FontWeights.SemiBold) };
        }

        static Button SmallBtn(string glyph, string text, Action click)
        {
            var b = Ui.IconBtn(glyph, text, Theme.Second, Theme.Text, click);
            b.Padding = new Thickness(11, 5, 12, 5);
            b.FontSize = 12.5;
            b.Margin = new Thickness(0, 0, 6, 6);
            return b;
        }

        void ShowDelayMenu(StartupItem it, Button anchor)
        {
            var list = new StackPanel();
            var pop = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 4, PopupAnimation = PopupAnimation.Fade };
            var opts = new[] { new KeyValuePair<string, int>(L.T("через 30 секунд"), 30), new KeyValuePair<string, int>(L.T("через 1 минуту"), 60), new KeyValuePair<string, int>(L.T("через 2 минуты"), 120), new KeyValuePair<string, int>(L.T("через 5 минут"), 300) };
            foreach (var o in opts)
            {
                var opt = o;
                var item = new Border { Padding = new Thickness(12, 7, 16, 7), CornerRadius = new CornerRadius(7), Cursor = Cursors.Hand, Background = Brushes.Transparent, Child = Ui.Txt(opt.Key, 13, Theme.Text) };
                item.MouseEnter += (s, e) => item.Background = Theme.Second;
                item.MouseLeave += (s, e) => item.Background = Brushes.Transparent;
                item.MouseLeftButtonUp += (s, e) => { pop.IsOpen = false; DoDelay(it, opt.Value); };
                list.Children.Add(item);
            }
            pop.Child = new Border { Background = Theme.Popup, BorderBrush = Theme.PopupBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(4), MinWidth = 170, Child = list };
            pop.IsOpen = true;
        }

        async void DoDelay(StartupItem it, int seconds)
        {
            if (busy) return;
            SetBusy(true, null);
            Exception err = null;
            try { await Task.Run(() => Journal.Do(L.T("Автозагрузки"), it.Name + L.T(" → отложен на ") + (seconds < 60 ? seconds + L.T(" с") : seconds / 60 + L.T(" мин")), () => Startup.Delay(it, seconds))); }
            catch (Exception ex) { err = ex; }
            SetBusy(false, null);
            if (err != null) Dialog.Info(owner, L.T("Не получилось отложить запуск"), err.Message);
            Render();
        }

        async void DoUndelay(StartupItem it)
        {
            if (busy) return;
            SetBusy(true, null);
            Exception err = null;
            try { await Task.Run(() => Journal.Do(L.T("Автозагрузки"), it.Name + L.T(" → не откладывать"), () => Startup.Undelay(it))); }
            catch (Exception ex) { err = ex; }
            SetBusy(false, null);
            if (err != null) Dialog.Info(owner, L.T("Не получилось"), err.Message);
            Render();
        }

        static void OpenFolder(string exe)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + exe + "\"") { UseShellExecute = true }); } catch { }
        }

        static void SearchWeb(StartupItem it)
        {
            string q = it.Name + (string.IsNullOrEmpty(it.Exe) ? "" : " " + System.IO.Path.GetFileName(it.Exe)) + L.T(" автозагрузка что это");
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString(q)) { UseShellExecute = true }); } catch { }
        }

        void Export()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Title = L.T("Сохранить список автозагрузки"), Filter = "CSV (*.csv)|*.csv", FileName = L.T("Автозагрузка ") + DateTime.Now.ToString("yyyy-MM-dd") + ".csv" };
            if (dlg.ShowDialog(owner) != true) return;
            try { System.IO.File.WriteAllText(dlg.FileName, Startup.ToCsv(items), new System.Text.UTF8Encoding(true)); }
            catch (Exception ex) { Dialog.Info(owner, L.T("Не удалось сохранить"), ex.Message); return; }
            Dialog.Info(owner, L.T("Сохранено"), L.T("Список автозагрузки (") + items.Count + L.T(" шт.) сохранён:\n") + dlg.FileName);
        }

        async void RemoveBroken()
        {
            if (busy) return;
            var broken = items.FindAll(i => i.Missing && Startup.CanDelete(i));
            if (broken.Count == 0) return;
            var names = broken.ConvertAll(i => i.Name + "  —  " + i.Exe);
            if (!Dialog.AskList(owner, L.T("Убрать ") + broken.Count + L.T(" битых записей?"), L.T("Программы, на которые они ссылаются, уже удалены. Записи реестра и задания вернёт «Откатить всё», файлы уйдут в Корзину."), names, L.T("Убрать"), false)) return;
            SetBusy(true, L.T("Убираю битые записи…"));
            foreach (var it in broken)
            {
                try { await Task.Run(() => DeleteLogged(it)); items.Remove(it); } catch { }
            }
            SetBusy(false, null);
            UpdateMissingBar();
            Render();
        }

        // Удаление с записью в журнал: реестр и задания откатываются, файл — только из Корзины
        static void DeleteLogged(StartupItem it)
        {
            if (it.Kind == StartKind.File)
            {
                Startup.Delete(it);
                Journal.Info(L.T("Автозагрузки"), L.T("Удалено из автозагрузки: ") + it.Name, L.T("Файл перемещён в Корзину — восстановить можно оттуда."));
                return;
            }
            Journal.Do(L.T("Автозагрузки"), L.T("Удалено из автозагрузки: ") + it.Name, () => Startup.Delete(it));
        }

        static string KindGlyph(StartKind k)
        {
            return k == StartKind.Registry ? "" : k == StartKind.File ? "" : k == StartKind.Task ? "" : "";
        }

        static FrameworkElement Detail(string label, string value)
        {
            var g = Ui.Cols("90", "*");
            g.Margin = new Thickness(0, 0, 0, 4);
            g.Children.Add(Ui.At(Ui.Txt(label, 12, Theme.Sub), 0, 0));
            g.Children.Add(Ui.At(new TextBlock { Text = value ?? "—", FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12, Foreground = Theme.Text, TextWrapping = TextWrapping.Wrap }, 0, 1));
            return g;
        }

        // ───────── Действия ─────────

        async void Toggle(StartupItem it, bool on, ToggleButton sw, TextBlock state)
        {
            if (busy) { sw.IsChecked = it.Enabled; return; }
            if (on && it.Delayed != null) { DoUndelay(it); return; }   // иначе запустится дважды: и записью, и заданием
            if (!on && it.Kind == StartKind.Service &&
                !Dialog.Ask(owner, L.T("Отключить службу «") + it.Name + L.T("»?"), L.T("Служба не будет запускаться ни вместе с Windows, ни по запросу программы. Программа, которой она нужна (например, обновления или античит), может перестать работать.\n\nВернуть можно этим же переключателем — восстановится прежний тип запуска — или кнопкой «Откатить всё»."), L.T("Отключить"), L.T("Отмена"), true))
            {
                sw.IsChecked = it.Enabled;
                return;
            }
            SetBusy(true, null);
            Exception err = null;
            try { await Task.Run(() => Journal.Do(L.T("Автозагрузки"), it.Name + " → " + (on ? L.T("Включено") : L.T("Отключено")), () => Startup.SetEnabled(it, on))); }
            catch (Exception ex) { err = ex; }
            SetBusy(false, null);
            if (err != null) Dialog.Info(owner, L.T("Не получилось изменить «") + it.Name + L.T("»"), err.Message);
            sw.IsChecked = it.Enabled;
            state.Text = it.Enabled ? L.T("Включено") : L.T("Отключено");
        }

        async void Remove(StartupItem it)
        {
            if (busy) return;
            string how = it.Kind == StartKind.Registry ? L.T("Запись будет удалена из реестра — вернуть её можно кнопкой «Откатить всё».")
                : it.Kind == StartKind.File ? L.T("Ярлык или файл будет перемещён в Корзину — оттуда его можно восстановить.")
                : L.T("Задание будет удалено из планировщика. Его копия сохранится — вернуть можно кнопкой «Откатить всё».");
            if (!Dialog.Ask(owner, L.T("Удалить «") + it.Name + L.T("» из автозагрузки?"), how + L.T("\n\nСама программа не удаляется."), L.T("Удалить"), L.T("Отмена"), true)) return;
            SetBusy(true, null);
            Exception err = null;
            try { await Task.Run(() => DeleteLogged(it)); }
            catch (Exception ex) { err = ex; }
            SetBusy(false, null);
            if (err != null) { Dialog.Info(owner, L.T("Не удалось удалить"), err.Message); return; }
            items.Remove(it);
            UpdateMissingBar();
            Render();
        }

        async void DisableJunk()
        {
            if (busy) return;
            var todo = items.FindAll(i => i.Recommended && i.Enabled);
            if (todo.Count == 0) { Dialog.Info(owner, L.T("Уже чисто"), L.T("Апдейтеры и подобные задачи и службы уже отключены.")); return; }
            var names = todo.ConvertAll(i => (i.Kind == StartKind.Service ? L.T("Служба: ") : L.T("Задача: ")) + i.Name);
            if (!Dialog.AskList(owner, L.T("Отключить ") + todo.Count + L.T(" шт.?"), L.T("Задания и службы будут отключены. Всё вернёт «Откатить всё»."), names, L.T("Отключить"), false)) return;
            var failed = new List<string>();
            for (int i = 0; i < todo.Count; i++)
            {
                var it = todo[i];
                SetBusy(true, L.T("Отключаю ") + (i + 1) + L.T(" из ") + todo.Count + ": " + it.Name + "…");
                try { await Task.Run(() => Journal.Do(L.T("Автозагрузки"), it.Name + L.T(" → Отключено"), () => Startup.SetEnabled(it, false))); }
                catch { failed.Add(it.Name); }
            }
            SetBusy(false, null);
            Render();
            Dialog.Info(owner, L.T("Готово"), L.T("Отключено: ") + (todo.Count - failed.Count) + L.T(" из ") + todo.Count + "." + (failed.Count > 0 ? L.T("\n\nНе удалось: ") + string.Join(", ", failed) : ""));
        }

        void AddProgram()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = L.T("Программа для автозагрузки"), Filter = L.T("Программы (*.exe)|*.exe") };
            if (dlg.ShowDialog(owner) != true) return;
            try { Journal.Do(L.T("Автозагрузки"), L.T("Добавлено: ") + System.IO.Path.GetFileNameWithoutExtension(dlg.FileName), () => Startup.Add(dlg.FileName)); }
            catch (Exception ex) { Dialog.Info(owner, L.T("Не получилось добавить"), ex.Message); return; }
            Load();
        }
    }
}
