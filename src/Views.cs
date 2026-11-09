using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WordAss
{
    internal static class Views
    {
        public static readonly Brush Ink = ColorBrush(25, 34, 55);
        public static readonly Brush Muted = ColorBrush(82, 94, 118);
        public static readonly Brush Accent = ColorBrush(68, 66, 201);
        public static readonly Brush AccentLine = ColorBrush(159, 157, 231);
        public static readonly Brush Danger = ColorBrush(164, 48, 65);
        public static readonly Brush Tint = ColorBrush(237, 238, 255);
        public static readonly Brush Surface = ColorBrush(245, 247, 252);
        public static readonly Brush Line = ColorBrush(220, 225, 236);
        public static ImageSource WindowIcon { get; set; }
        private static readonly Brush AccentHover = ColorBrush(55, 52, 178);
        private static readonly Brush AccentPressed = ColorBrush(47, 43, 155);

        private static Brush ColorBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        public static void Style(Window window)
        {
            window.FontFamily = new FontFamily("Segoe UI Variable, Microsoft YaHei UI, Segoe UI");
            window.FontSize = 14;
            window.Foreground = Ink;
            window.Background = Surface;
            window.Icon = WindowIcon;
            window.UseLayoutRounding = true;
        }

        public static TextBlock Text(string text, double size = 14, Brush color = null, bool bold = false)
        {
            return new TextBlock
            {
                Text = text, FontSize = size, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal
            };
        }

        public static Button Button(string text, Action click, bool primary = false, Geometry icon = null)
        {
            var template = RoundedContent(typeof(Button), 8, HorizontalAlignment.Center);
            template.Triggers.Add(BackgroundOn(UIElement.IsMouseOverProperty, primary ? AccentHover : Surface));
            template.Triggers.Add(BackgroundOn(ButtonBase.IsPressedProperty, primary ? AccentPressed : Tint));
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55, "Frame"));
            template.Triggers.Add(disabled);
            var button = new Button
            {
                Content = icon == null ? (object)text : IconLabel(text, icon, primary ? Brushes.White : Ink),
                Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0),
                MinHeight = 34, Background = primary ? Accent : Brushes.White,
                Foreground = primary ? Brushes.White : Ink, BorderBrush = primary ? Brushes.Transparent : Line,
                BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand,
                Template = template
            };
            AutomationProperties.SetName(button, text);
            if (click != null) button.Click += (sender, args) => click();
            return button;
        }

        public static Button ToolButton(string label, Geometry icon, Action click)
        {
            var button = new Button { Content = Icons.Draw(icon, Muted) };
            StyleTool(button, label);
            button.Click += (sender, args) => click();
            return button;
        }

        public static ToggleButton PinButton()
        {
            var button = new ToggleButton();
            StyleTool(button, "固定窗口");
            button.Content = Icons.Draw(Icons.Pin, Muted);
            button.Checked += (sender, args) =>
            {
                button.Content = Icons.Draw(Icons.Pin, Accent);
                button.ToolTip = "取消固定";
                AutomationProperties.SetName(button, "取消固定");
            };
            button.Unchecked += (sender, args) =>
            {
                button.Content = Icons.Draw(Icons.Pin, Muted);
                button.ToolTip = "固定窗口";
                AutomationProperties.SetName(button, "固定窗口");
            };
            return button;
        }

        private static void StyleTool(ButtonBase button, string label)
        {
            var template = RoundedContent(button.GetType(), 7, HorizontalAlignment.Center);
            template.Triggers.Add(BackgroundOn(UIElement.IsMouseOverProperty, Surface));
            template.Triggers.Add(BackgroundOn(ButtonBase.IsPressedProperty, Tint));
            if (button is ToggleButton) template.Triggers.Add(BackgroundOn(ToggleButton.IsCheckedProperty, Tint));
            button.Template = template;
            button.Width = button.Height = 30;
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.ToolTip = label;
            button.Cursor = Cursors.Hand;
            AutomationProperties.SetName(button, label);
        }

        public static StackPanel IconLabel(string text, Geometry icon, Brush brush, double size = 14)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var image = Icons.Draw(icon, brush);
            image.Margin = new Thickness(0, 0, 6, 0);
            panel.Children.Add(image);
            var label = Text(text, size, brush, true);
            label.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(label);
            return panel;
        }

        public static Border IconTile(Geometry icon)
        {
            return new Border
            {
                Width = 36, Height = 36, Background = Tint, CornerRadius = new CornerRadius(9),
                HorizontalAlignment = HorizontalAlignment.Left, Child = Icons.Draw(icon, Accent, 20)
            };
        }

        public static Style WordRowStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            var template = RoundedContent(typeof(ListBoxItem), 8, HorizontalAlignment.Stretch);
            template.Triggers.Add(BackgroundOn(UIElement.IsMouseOverProperty, Surface));
            template.Triggers.Add(BackgroundOn(ListBoxItem.IsSelectedProperty, Tint));
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 10, 8)));
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            return style;
        }

        private static ControlTemplate RoundedContent(Type target, double radius, HorizontalAlignment alignment)
        {
            var border = TemplateBorder(radius);
            border.SetBinding(Border.PaddingProperty, FromControl("Padding"));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, alignment);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            border.AppendChild(content);
            return new ControlTemplate(target) { VisualTree = border };
        }

        public static PasswordBox PasswordInput()
        {
            return new PasswordBox
            {
                Template = InputTemplate(typeof(PasswordBox)), Background = Brushes.White, Foreground = Ink, BorderBrush = Line,
                BorderThickness = new Thickness(1), Padding = new Thickness(11, 4, 11, 4), MinHeight = 36, MaxLength = 512
            };
        }

        public static TextBox TextInput(string value)
        {
            return new TextBox
            {
                Text = value, Template = InputTemplate(typeof(TextBox)), Background = Brushes.White, Foreground = Ink, BorderBrush = Line,
                BorderThickness = new Thickness(1), Padding = new Thickness(11, 4, 11, 4), MinHeight = 36
            };
        }

        private static ControlTemplate InputTemplate(Type target)
        {
            var border = TemplateBorder(9);
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            scroll.SetBinding(FrameworkElement.MarginProperty, FromControl("Padding"));
            scroll.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            scroll.SetValue(Control.FocusableProperty, false);
            border.AppendChild(scroll);
            var template = new ControlTemplate(target) { VisualTree = border };
            var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focused.Setters.Add(new Setter(Border.BorderBrushProperty, Accent, "Frame"));
            template.Triggers.Add(focused);
            return template;
        }

        public static ToggleButton Switch(string label)
        {
            var track = new FrameworkElementFactory(typeof(Border), "Track");
            track.SetValue(Border.BackgroundProperty, Line);
            track.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            track.SetValue(Border.PaddingProperty, new Thickness(3));
            var knob = new FrameworkElementFactory(typeof(Border), "Knob");
            knob.SetValue(Border.BackgroundProperty, Brushes.White);
            knob.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            knob.SetValue(FrameworkElement.WidthProperty, 16.0);
            knob.SetValue(FrameworkElement.HeightProperty, 16.0);
            knob.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            track.AppendChild(knob);
            var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = track };
            var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            on.Setters.Add(new Setter(Border.BackgroundProperty, Accent, "Track"));
            on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "Knob"));
            template.Triggers.Add(on);
            var toggle = new ToggleButton { Width = 40, Height = 22, Template = template, Cursor = Cursors.Hand };
            AutomationProperties.SetName(toggle, label);
            return toggle;
        }

        public static RadioButton Choice(string text, bool selected)
        {
            var template = RoundedContent(typeof(RadioButton), 7, HorizontalAlignment.Center);
            template.Triggers.Add(BackgroundOn(UIElement.IsMouseOverProperty, Surface));
            template.Triggers.Add(BackgroundOn(ToggleButton.IsCheckedProperty, Tint));
            var choice = new RadioButton
            {
                Content = text, IsChecked = selected, MinHeight = 34, Background = Brushes.White,
                Foreground = Ink, BorderBrush = Line, BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4), Cursor = Cursors.Hand, Template = template
            };
            AutomationProperties.SetName(choice, text);
            return choice;
        }

        private static FrameworkElementFactory TemplateBorder(double radius)
        {
            var border = new FrameworkElementFactory(typeof(Border), "Frame");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty, FromControl("Background"));
            border.SetBinding(Border.BorderBrushProperty, FromControl("BorderBrush"));
            border.SetBinding(Border.BorderThicknessProperty, FromControl("BorderThickness"));
            return border;
        }

        private static Binding FromControl(string property)
        {
            return new Binding(property) { RelativeSource = RelativeSource.TemplatedParent };
        }

        private static Trigger BackgroundOn(DependencyProperty property, Brush brush)
        {
            var trigger = new Trigger { Property = property, Value = true };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, brush, "Frame"));
            return trigger;
        }

        public static Border Card(double padding, double radius = 14)
        {
            return new Border
            {
                Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius), Padding = new Thickness(padding)
            };
        }

        public static Border Badge(string text)
        {
            return new Border
            {
                Background = Tint, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                Child = Text(text, 12, Accent, true)
            };
        }

        public static StackPanel EntryDetails(Entry entry, Action<string> speak)
        {
            var panel = new StackPanel();
            var title = new DockPanel();
            var audio = Button("发音", () => speak(entry.Word), icon: Icons.Speaker);
            audio.Margin = new Thickness(12, 0, 0, 0);
            audio.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(audio, Dock.Right);
            title.Children.Add(audio);
            title.Children.Add(Text(entry.Word, 26, bold: true));
            panel.Children.Add(title);
            var summary = Text(entry.Summary, 15, Muted);
            summary.Margin = new Thickness(0, 3, 0, 0);
            panel.Children.Add(summary);
            foreach (var sense in entry.Senses)
            {
                var content = new StackPanel();
                content.Children.Add(Badge(sense.PartOfSpeech));
                var definition = Text(sense.Definition, 14, bold: true);
                definition.Margin = new Thickness(0, 6, 0, 5);
                content.Children.Add(definition);
                content.Children.Add(Text(sense.Example));
                content.Children.Add(Text(sense.ExampleTranslation, 13, Muted));
                panel.Children.Add(new Border
                {
                    Background = Surface, CornerRadius = new CornerRadius(9), Padding = new Thickness(11),
                    Margin = new Thickness(0, 9, 0, 0), Child = content
                });
            }
            return panel;
        }

        public static ScrollViewer Scroll(UIElement content, double maxHeight = double.PositiveInfinity)
        {
            return new ScrollViewer { Content = content, MaxHeight = maxHeight, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }

    internal sealed class LookupWindow : Window
    {
        private readonly Point anchor;
        private readonly StackPanel panel = new StackPanel { Margin = new Thickness(14) };
        private readonly Grid resultSurface = new Grid { Margin = new Thickness(9) };
        private readonly Border card;
        private readonly ToggleButton pin = Views.PinButton();
        private readonly TextBlock headerLabel = Views.Text("", 12, Views.Muted, true);
        private readonly DockPanel header;
        private bool moved;

        public event Action<LookupWindow> PinChanged;

        public LookupWindow(Point anchor)
        {
            this.anchor = anchor;
            Views.Style(this);
            Title = "划词翻译";
            ShowActivated = false;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            SizeToContent = SizeToContent.WidthAndHeight;
            var shadow = Views.Card(0, 12);
            shadow.IsHitTestVisible = false;
            shadow.Effect = new DropShadowEffect { Color = Color.FromRgb(28, 38, 65), BlurRadius = 13, ShadowDepth = 2, Opacity = 0.22 };
            resultSurface.Children.Add(shadow);
            card = Views.Card(0, 12);
            card.Child = panel;
            resultSurface.Children.Add(card);
            Content = resultSurface;
            header = CreateHeader();
            pin.Checked += NotifyPinChanged;
            pin.Unchecked += NotifyPinChanged;
            SizeChanged += (sender, args) => Reposition();
            PreviewKeyDown += (sender, args) => { if (args.Key == Key.Escape) Close(); };
        }

        public IntPtr Handle { get { return new WindowInteropHelper(this).Handle; } }
        public bool IsPinned { get { return pin.IsChecked == true; } }

        public bool Contains(Point point)
        {
            if (!IsVisible || Handle == IntPtr.Zero) return false;
            var hitArea = Content == resultSurface ? card : (FrameworkElement)Content;
            if (PresentationSource.FromVisual(hitArea) == null) return false;
            var start = hitArea.PointToScreen(new Point());
            var end = hitArea.PointToScreen(new Point(hitArea.ActualWidth, hitArea.ActualHeight));
            return new Rect(start, end).Contains(point);
        }

        private DockPanel CreateHeader()
        {
            var bar = new DockPanel { Margin = new Thickness(0, -5, -4, 7) };
            var close = Views.ToolButton("关闭窗口", Icons.Close, Close);
            DockPanel.SetDock(close, Dock.Right);
            bar.Children.Add(close);
            DockPanel.SetDock(pin, Dock.Right);
            bar.Children.Add(pin);

            var grip = new DockPanel { LastChildFill = true, Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            grip.ToolTip = "拖动此处移动窗口";
            var mark = Icons.Draw(Icons.Grip, Views.Muted, 15);
            mark.Margin = new Thickness(0, 0, 6, 0);
            grip.Children.Add(mark);
            headerLabel.VerticalAlignment = VerticalAlignment.Center;
            grip.Children.Add(headerLabel);
            grip.MouseLeftButtonDown += (sender, args) =>
            {
                if (args.ButtonState != MouseButtonState.Pressed) return;
                moved = true;
                args.Handled = true;
                DragMove();
            };
            bar.Children.Add(grip);
            return bar;
        }

        private void NotifyPinChanged(object sender, RoutedEventArgs args)
        {
            var changed = PinChanged;
            if (changed != null) changed(this);
        }

        public void Reposition()
        {
            if (moved || !IsVisible || Handle == IntPtr.Zero) return;
            var prompt = Content as Button;
            if (prompt == null)
            {
                Selection.PlacePopup(Handle, anchor);
                return;
            }

            var source = PresentationSource.FromVisual(prompt);
            if (source == null || prompt.ActualWidth == 0 || prompt.ActualHeight == 0) return;
            var size = source.CompositionTarget.TransformToDevice.Transform(new Vector(prompt.ActualWidth, prompt.ActualHeight));
            Selection.PlacePopup(Handle, anchor, new Size(size.X, size.Y));
        }

        public void Prompt(Action run)
        {
            var translate = Views.Button("翻译", run);
            translate.Margin = new Thickness(0);
            translate.HorizontalAlignment = HorizontalAlignment.Left;
            translate.VerticalAlignment = VerticalAlignment.Top;
            translate.Padding = new Thickness(8, 2, 8, 2);
            translate.MinWidth = 46;
            translate.MinHeight = 26;
            translate.FontSize = 13;
            translate.Background = Views.Tint;
            translate.BorderBrush = Views.AccentLine;
            translate.Foreground = Views.Accent;
            Content = translate;
        }

        public void Status(string message, Action retry = null, string button = "重试")
        {
            ResetResult("查译");
            panel.Children.Add(Views.Text(message, color: Views.Muted));
            if (retry != null)
            {
                var action = Views.Button(button, retry);
                action.HorizontalAlignment = HorizontalAlignment.Left;
                action.Margin = new Thickness(0, 9, 0, 0);
                panel.Children.Add(action);
            }
        }

        public void Translation(string translation)
        {
            ResetResult("译文", true);
            var box = new TextBox
            {
                Text = translation, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Foreground = Views.Ink, Padding = new Thickness(0)
            };
            panel.Children.Add(box);
        }

        public void Word(Entry entry, Action<string> speak, Func<string> save)
        {
            ResetResult("词条", true);
            panel.Children.Add(Views.Scroll(Views.EntryDetails(entry, speak), 290));
            var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var status = Views.Text("", 13, Views.Accent);
            status.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(Views.Button("加入单词本", () => status.Text = save(), true, Icons.Bookmark));
            footer.Children.Add(status);
            panel.Children.Add(footer);
        }

        private void ResetResult(string title, bool canPin = false)
        {
            panel.Width = 350;
            panel.Margin = new Thickness(16);
            panel.Children.Clear();
            headerLabel.Text = title;
            pin.Visibility = canPin ? Visibility.Visible : Visibility.Collapsed;
            panel.Children.Add(header);
            Content = resultSurface;
        }
    }

    internal sealed class WordbookWindow : Window
    {
        private readonly LocalStore store;
        private readonly Action<string> speak;
        private readonly ListBox list = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        private readonly Border details = Views.Card(16);
        private readonly Border count = Views.Badge("");
        private readonly TextBlock empty = Views.Text("", 14, Views.Muted);

        public WordbookWindow(LocalStore store, Action<string> speak)
        {
            this.store = store;
            this.speak = speak;
            Views.Style(this);
            Title = "单词本";
            Width = 620;
            Height = 540;
            MinWidth = 420;
            MinHeight = 370;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var layout = new DockPanel { Margin = new Thickness(23) };
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 15) };
            count.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(count, Dock.Right);
            header.Children.Add(count);
            var heading = new StackPanel { Orientation = Orientation.Horizontal };
            var bookIcon = Views.IconTile(Icons.Book);
            bookIcon.Margin = new Thickness(0, 0, 10, 0);
            heading.Children.Add(bookIcon);
            var title = Views.Text("单词本", 27, bold: true);
            title.VerticalAlignment = VerticalAlignment.Center;
            heading.Children.Add(title);
            header.Children.Add(heading);
            DockPanel.SetDock(header, Dock.Top);
            layout.Children.Add(header);
            DockPanel.SetDock(details, Dock.Bottom);
            details.Margin = new Thickness(0, 10, 0, 0);
            layout.Children.Add(details);
            layout.SizeChanged += (sender, args) =>
            {
                const double minimumListArea = 167;
                details.MaxHeight = Math.Min(255, Math.Max(0, args.NewSize.Height - minimumListArea));
            };
            var card = Views.Card(7);
            var table = new DockPanel();
            var columns = WordRow("英文", "中文释义", true);
            columns.Margin = new Thickness(12, 9, 10, 7);
            DockPanel.SetDock(columns, Dock.Top);
            table.Children.Add(columns);
            var rule = new Border { Height = 1, Background = Views.Line, Margin = new Thickness(10, 0, 10, 4) };
            DockPanel.SetDock(rule, Dock.Top);
            table.Children.Add(rule);
            var area = new Grid();
            list.ItemContainerStyle = Views.WordRowStyle();
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            list.SelectionChanged += (sender, args) => ShowDetails();
            area.Children.Add(list);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.TextAlignment = TextAlignment.Center;
            empty.Margin = new Thickness(16);
            area.Children.Add(empty);
            table.Children.Add(area);
            card.Child = table;
            layout.Children.Add(card);
            Content = layout;
            Reload();
        }

        public void Reload()
        {
            var selected = list.SelectedItem as ListBoxItem;
            var previous = selected == null ? null : (Entry)selected.Tag;
            list.Items.Clear();
            details.Visibility = Visibility.Collapsed;
            try
            {
                var entries = store.ReadEntries();
                ((TextBlock)count.Child).Text = entries.Count + " 个单词";
                empty.Text = "还没有收藏\n划词查询后，点击「加入单词本」。";
                empty.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                foreach (var entry in entries)
                {
                    var item = new ListBoxItem { Content = WordRow(entry.Word, entry.Summary, false), Tag = entry };
                    list.Items.Add(item);
                    if (previous != null && entry.Key == previous.Key) list.SelectedItem = item;
                }
            }
            catch (Exception ex)
            {
                if (!Program.IsLocalFileError(ex)) throw;
                ((TextBlock)count.Child).Text = "读取失败";
                empty.Text = "读取单词本失败，请检查本地 XML 文件。";
                empty.Visibility = Visibility.Visible;
            }
        }

        private static Grid WordRow(string english, string chinese, bool header)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            var word = Views.Text(english, header ? 12 : 15, header ? Views.Muted : Views.Ink, true);
            word.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(word);
            var summary = Views.Text(chinese, header ? 12 : 14, Views.Muted, header);
            Grid.SetColumn(summary, 1);
            row.Children.Add(summary);
            if (!header)
            {
                var arrow = Icons.Draw(Icons.Chevron, Views.Muted, 13);
                Grid.SetColumn(arrow, 2);
                row.Children.Add(arrow);
            }
            return row;
        }

        private void ShowDetails()
        {
            var item = list.SelectedItem as ListBoxItem;
            details.Child = item == null ? null : WordDetails((Entry)item.Tag);
            details.Visibility = item == null ? Visibility.Collapsed : Visibility.Visible;
        }

        private UIElement WordDetails(Entry entry)
        {
            var layout = new DockPanel();
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Margin = new Thickness(0, 6, 0, 0);
            DockPanel.SetDock(actions, Dock.Bottom);
            layout.Children.Add(actions);
            layout.Children.Add(Views.Scroll(Views.EntryDetails(entry, speak)));

            var remove = Views.ToolButton("从单词本删除", Icons.Trash, () =>
            {
                actions.Children.Clear();
                var prompt = Views.Text("删除这个词条？", 12, Views.Muted);
                prompt.VerticalAlignment = VerticalAlignment.Center;
                prompt.Margin = new Thickness(0, 0, 8, 0);
                actions.Children.Add(prompt);
                actions.Children.Add(Views.Button("取消", ShowDetails));
                var confirm = Views.Button("删除", () =>
                {
                    try
                    {
                        store.Remove(entry);
                        Reload();
                    }
                    catch (Exception ex)
                    {
                        if (!Program.IsLocalFileError(ex)) throw;
                        prompt.Text = "删除失败，请重试。";
                    }
                });
                confirm.Margin = new Thickness(0);
                confirm.Foreground = Views.Danger;
                actions.Children.Add(confirm);
                confirm.Focus();
            });
            actions.Children.Add(remove);
            return layout;
        }
    }
}
