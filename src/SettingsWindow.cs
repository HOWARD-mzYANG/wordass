using System;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace WordAss
{
    internal sealed class SettingsWindow : Window
    {
        public SettingsWindow(ApiSettings current, LocalStore store, ChatApi api, StartupRegistration startup,
            Action<ApiSettings> saved)
        {
            Views.Style(this);
            Title = "WordAss · 设置";
            Width = 460;
            MaxHeight = SystemParameters.WorkArea.Height * 0.94;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var body = new StackPanel { Margin = new Thickness(23) };
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            var icon = Views.IconTile(Icons.Key);
            icon.Margin = new Thickness(0, 0, 9, 0);
            heading.Children.Add(icon);
            var title = Views.Text("设置", 26, bold: true);
            title.VerticalAlignment = VerticalAlignment.Center;
            heading.Children.Add(title);
            body.Children.Add(heading);

            var startupCard = StartupCard(startup);
            startupCard.Margin = new Thickness(0, 0, 0, 10);
            body.Children.Add(startupCard);
            body.Children.Add(ApiCard(current, store, api, saved));
            Content = Views.Scroll(body);
            PreviewKeyDown += (sender, args) => { if (args.Key == Key.Escape) Close(); };
        }

        private static Border StartupCard(StartupRegistration startup)
        {
            var card = Views.Card(14);
            var layout = new DockPanel();
            var toggle = Views.Switch("开机启动");
            toggle.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(toggle, Dock.Right);
            layout.Children.Add(toggle);
            var labels = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            labels.Children.Add(Views.Text("开机启动", bold: true));
            var status = Views.Text("登录 Windows 后在托盘运行", 12, Views.Muted);
            labels.Children.Add(status);
            layout.Children.Add(labels);
            card.Child = layout;

            try { toggle.IsChecked = startup.IsEnabled(); }
            catch (Exception ex)
            {
                if (!Program.IsLocalFileError(ex)) throw;
                toggle.IsEnabled = false;
                status.Text = "无法读取 Windows 启动设置。";
            }
            toggle.Click += (sender, args) =>
            {
                bool enabled = toggle.IsChecked == true;
                try
                {
                    startup.SetEnabled(enabled);
                    status.Text = "登录 Windows 后在托盘运行";
                }
                catch (Exception ex)
                {
                    if (!Program.IsLocalFileError(ex)) throw;
                    toggle.IsChecked = !enabled;
                    status.Text = "无法修改 Windows 启动设置。";
                }
            };
            return card;
        }

        private Border ApiCard(ApiSettings current, LocalStore store, ChatApi api, Action<ApiSettings> saved)
        {
            var card = Views.Card(14);
            var form = new StackPanel();
            form.Children.Add(Views.Text("词典与翻译 API", 17, bold: true));
            var help = Views.Text("点击「翻译」时，选中文字会发送给所选 API。", 12, Views.Muted);
            help.Margin = new Thickness(0, 4, 0, 8);
            form.Children.Add(help);

            bool deepSeek = current == null || current.IsDeepSeek;
            var provider = new UniformGrid { Rows = 1 };
            var defaultOption = Views.Choice("DeepSeek（默认）", deepSeek);
            defaultOption.Margin = new Thickness(0, 0, 6, 0);
            var customOption = Views.Choice("自定义 OpenAI 兼容 API", !deepSeek);
            provider.Children.Add(defaultOption);
            provider.Children.Add(customOption);
            var providerLabel = Views.Text("服务", 12, bold: true);
            providerLabel.Margin = new Thickness(0, 5, 0, 4);
            form.Children.Add(providerLabel);
            form.Children.Add(provider);

            var address = Views.TextInput(deepSeek ? "" : current.BaseUrl.TrimEnd('/'));
            var addressField = AddField(form, "API 基础地址（例如 https://example.com/v1）", address);
            var model = Views.TextInput(current == null ? ApiSettings.DeepSeekModel : current.Model);
            AddField(form, "模型名称", model);

            var noKey = new CheckBox
            {
                Content = "此服务无需 API Key", IsChecked = !deepSeek && string.IsNullOrEmpty(current.Key),
                Margin = new Thickness(0, 9, 0, 1)
            };
            form.Children.Add(noKey);
            var input = Views.PasswordInput();
            var keyField = AddField(form, "API Key", input);
            var status = Views.Text("", 12, Views.Muted);
            status.Margin = new Thickness(0, 8, 0, 5);
            status.MinHeight = 17;
            form.Children.Add(status);

            Action updateFields = () =>
            {
                addressField.Visibility = noKey.Visibility = deepSeek ? Visibility.Collapsed : Visibility.Visible;
                keyField.Visibility = !deepSeek && noKey.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
                if (keyField.Visibility == Visibility.Collapsed) input.Clear();
            };
            string deepSeekModel = deepSeek ? model.Text : ApiSettings.DeepSeekModel;
            string customModel = deepSeek ? "" : model.Text;
            updateFields();
            Action<bool> selectProvider = useDeepSeek =>
            {
                if (deepSeek) deepSeekModel = model.Text;
                else customModel = model.Text;
                deepSeek = useDeepSeek;
                model.Text = deepSeek ? deepSeekModel : customModel;
                input.Clear();
                status.Text = "";
                updateFields();
            };
            defaultOption.Checked += (sender, args) => selectProvider(true);
            customOption.Checked += (sender, args) => selectProvider(false);
            RoutedEventHandler updateKey = (sender, args) => { status.Text = ""; updateFields(); };
            noKey.Checked += updateKey;
            noKey.Unchecked += updateKey;
            if (current != null && !string.IsNullOrEmpty(current.Key))
                status.Text = "已配置 Key。同一地址下留空可沿用。";

            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(Views.Button("关闭", Close));
            var save = Views.Button("验证并保存", null, true, Icons.Check);
            save.IsDefault = true;
            save.Margin = new Thickness(0);
            actions.Children.Add(save);
            form.Children.Add(actions);
            card.Child = form;

            var cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            Closed += (sender, args) =>
            {
                input.Clear();
                cancellation.Cancel();
                cancellation.Dispose();
            };
            save.Click += async (sender, args) =>
            {
                ApiSettings candidate;
                try
                {
                    string endpoint = deepSeek ? ApiSettings.DeepSeekUrl : ApiSettings.NormalizeBaseUrl(address.Text);
                    bool anonymous = !deepSeek && noKey.IsChecked == true;
                    string key = anonymous ? "" : input.Password.Trim();
                    if (!anonymous && key.Length == 0 && current != null && current.BaseUrl == endpoint) key = current.Key;
                    if (!anonymous && string.IsNullOrEmpty(key))
                        throw new InvalidOperationException("请输入 API Key，或勾选此服务无需 API Key。");
                    candidate = ApiSettings.Create(deepSeek, endpoint, model.Text, key);
                }
                catch (InvalidOperationException ex) { status.Text = ex.Message; return; }

                provider.IsEnabled = address.IsEnabled = model.IsEnabled = input.IsEnabled = noKey.IsEnabled = save.IsEnabled = false;
                status.Text = "正在发送测试请求…";
                try
                {
                    await api.ValidateAsync(candidate, token);
                    if (token.IsCancellationRequested) return;
                    store.SaveApiSettings(candidate);
                    saved(candidate);
                    Close();
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested) return;
                    status.Text = Program.IsLocalFileError(ex) ? "无法保存 API 配置，请检查本地目录权限。" : Program.RequestError(ex);
                    provider.IsEnabled = address.IsEnabled = model.IsEnabled = input.IsEnabled = noKey.IsEnabled = save.IsEnabled = true;
                }
            };
            return card;
        }

        private static StackPanel AddField(StackPanel form, string label, Control control)
        {
            var field = new StackPanel { Margin = new Thickness(0, 5, 0, 4) };
            var name = Views.Text(label, 12, bold: true);
            name.Margin = new Thickness(0, 0, 0, 4);
            AutomationProperties.SetName(control, label);
            field.Children.Add(name);
            field.Children.Add(control);
            form.Children.Add(field);
            return field;
        }
    }
}
