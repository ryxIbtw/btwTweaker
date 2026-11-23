using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace BtwTweaker
{
    // Страница «Электропитание»: список схем, выбрать / удалить / добавить
    public class PowerPage : StackPanel
    {
        readonly Window owner;
        readonly StackPanel list = new StackPanel();

        public PowerPage(Window w)
        {
            owner = w;

            var head = Ui.Cols("auto", "*", "auto");
            head.Margin = new Thickness(2, 2, 0, 6);
            head.Children.Add(Ui.At(Ui.Txt("Ваши схемы", 16, Theme.Text, FontWeights.SemiBold), 0, 0));
            Button add = null;
            add = Ui.IconBtn("", "Добавить схему", Theme.Second, Theme.Text, () => ShowAddMenu(add));
            add.Padding = new Thickness(12, 6, 12, 6);
            head.Children.Add(Ui.At(add, 0, 2));
            Children.Add(head);

            var tip = new TextBlock
            {
                Text = "Для игр и настольного ПК — «Максимальная» или «Высокая производительность». Для ноутбука от батареи — «Сбалансированная».",
                FontSize = 12.5, Foreground = Theme.Sub, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 0, 12)
            };
            Children.Add(tip);
            Children.Add(list);
            Reload();
        }

        void Reload()
        {
            list.Children.Clear();
            try
            {
                foreach (var s in Power.List()) list.Children.Add(Row(s));
            }
            catch (Exception ex)
            {
                list.Children.Add(Ui.Txt("Не удалось прочитать схемы электропитания: " + ex.Message, 13, Theme.Red));
            }
        }

        FrameworkElement Row(PowerScheme s)
        {
            var g = Ui.Cols("*", "auto", "auto", "auto");
            var name = Ui.Txt(s.Name, 14, Theme.Text);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            g.Children.Add(Ui.At(name, 0, 0));

            if (s.Active)
            {
                var a = Ui.Txt("Активная схема", 13, Theme.Accent, FontWeights.SemiBold);
                a.Margin = new Thickness(12, 0, 14, 0);
                g.Children.Add(Ui.At(a, 0, 1));
            }

            var pick = Ui.Btn("Выбрать", Theme.Accent, () => Do(() => Power.SetActive(s.Id), null));
            pick.Padding = new Thickness(14, 5, 14, 5);
            pick.IsEnabled = !s.Active;
            pick.Margin = new Thickness(0, 0, 8, 0);
            g.Children.Add(Ui.At(pick, 0, 2));

            var del = Ui.Btn("Удалить", Theme.Red, () =>
            {
                if (Dialog.Ask(owner, "Удалить схему «" + s.Name + "»?",
                    "Схема и её настройки будут удалены. Кнопка «Откатить всё» её не вернёт, но стандартные схемы можно добавить заново через «Добавить схему».",
                    "Удалить", "Отмена", true))
                    Do(() => Power.Delete(s.Id), null);
            });
            del.Padding = new Thickness(14, 5, 14, 5);
            del.IsEnabled = !s.Active;
            if (s.Active) del.ToolTip = "Активную схему удалить нельзя — сначала выберите другую.";
            g.Children.Add(Ui.At(del, 0, 3));

            var card = Ui.Card(g, new Thickness(18, 10, 12, 10));
            card.Margin = new Thickness(0, 0, 0, 8);
            return card;
        }

        void ShowAddMenu(Button anchor)
        {
            var items = new StackPanel();
            var pop = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 4 };
            for (int i = 0; i < Power.Templates.Length; i++)
            {
                int idx = i;
                var it = new Border { Padding = new Thickness(12, 7, 16, 7), CornerRadius = new CornerRadius(7), Cursor = Cursors.Hand, Background = Brushes.Transparent, Child = Ui.Txt(Power.TemplateNames[i], 13, Theme.Text) };
                it.MouseEnter += (o, e) => it.Background = Theme.Second;
                it.MouseLeave += (o, e) => it.Background = Brushes.Transparent;
                it.MouseLeftButtonUp += (o, e) =>
                {
                    pop.IsOpen = false;
                    bool exists = false;
                    try { exists = Power.List().Exists(s => s.Id == Power.Templates[idx] || s.Name == Power.TemplateNames[idx]); } catch { }
                    if (exists && !Dialog.Ask(owner, "Такая схема уже есть", "Схема «" + Power.TemplateNames[idx] + "» уже в списке. Добавить ещё одну копию?", "Добавить", "Отмена"))
                        return;
                    Do(() => Power.Duplicate(Power.Templates[idx]),"Схема «" + Power.TemplateNames[idx] + "» добавлена. Нажмите «Выбрать», чтобы включить её.");
                };
                items.Children.Add(it);
            }
            pop.Child = new Border
            {
                Background = Theme.Popup, BorderBrush = Theme.PopupBorder, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(4), Child = items
            };
            pop.IsOpen = true;
        }

        void Do(Action a, string done)
        {
            try
            {
                a();
                Reload();
                if (done != null) Dialog.Info(owner, "Готово", done);
            }
            catch (Exception ex)
            {
                Reload();
                Dialog.Info(owner, "Не получилось", ex.Message);
            }
        }
    }
}
