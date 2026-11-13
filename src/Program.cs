using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Xml;
using Forms = System.Windows.Forms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

namespace WordAss
{
    internal sealed class Program : Application
    {
        private readonly ChatApi api;
        private readonly StartupRegistration startup;
        private readonly string dataDirectory;
        private readonly SelectionReader selectionReader = new SelectionReader();
        private LocalStore store;
        private Selection selection;
        private Forms.NotifyIcon tray;
        private System.Drawing.Icon trayIcon;
        private LookupWindow transientLookup;
        private WordbookWindow wordbook;
        private SettingsWindow settings;
        private SpeechSynthesizer speech;
        private ApiSettings apiSettings;
        private int captureVersion;

        internal Program(string dataDirectory, ChatApi api, StartupRegistration startup)
        {
            this.dataDirectory = dataDirectory;
            this.api = api;
            this.startup = startup;
        }

        [STAThread]
        public static void Main(string[] args)
        {
            bool first;
            using (var instance = new Mutex(true, @"Local\WordAss.Application", out first))
            {
                if (!first)
                {
                    if (!args.Contains("--background")) MessageBox.Show("WordAss 已在运行。请从系统托盘打开单词本。", "WordAss");
                    return;
                }
                ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WordAss");
                var startup = new StartupRegistration(System.Reflection.Assembly.GetExecutingAssembly().Location);
                new Program(directory, new ChatApi(), startup) { ShutdownMode = ShutdownMode.OnExplicitShutdown }.Run();
            }
        }

        protected override void OnStartup(StartupEventArgs args)
        {
            base.OnStartup(args);
            try
            {
                store = new LocalStore(dataDirectory);
            }
            catch (Exception ex)
            {
                if (!IsLocalFileError(ex)) throw;
                MessageBox.Show("无法创建本地数据目录，请检查文件权限。", "WordAss");
                Shutdown(1);
                return;
            }
            Appearance appearance = Appearance.Modern;
            try { appearance = store.ReadAppearance(); }
            catch (Exception ex) { if (!IsLocalFileError(ex)) throw; }
            Theme.Apply(appearance);

            try { apiSettings = store.ReadApiSettings(); }
            catch (Exception ex) { if (!IsLocalFileError(ex)) throw; }
            try { startup.RefreshPath(); }
            catch (Exception ex) { if (!IsLocalFileError(ex)) throw; }

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开单词本", null, (sender, eventArgs) => OpenWordbook());
            var clipboardItem = menu.Items.Add("查译剪贴板", null, (sender, eventArgs) => FromClipboard(Selection.CursorPosition()));
            menu.Items.Add("设置", null, (sender, eventArgs) => OpenSettings());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, (sender, eventArgs) => Shutdown());
            trayIcon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (trayIcon != null)
            {
                Views.WindowIcon = Imaging.CreateBitmapSourceFromHIcon(trayIcon.Handle, Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                Views.WindowIcon.Freeze();
            }
            tray = new Forms.NotifyIcon
            {
                Icon = trayIcon ?? System.Drawing.SystemIcons.Application, Text = "WordAss 单词本", ContextMenuStrip = menu, Visible = true
            };
            tray.MouseDoubleClick += (sender, eventArgs) => { if (eventArgs.Button == Forms.MouseButtons.Left) OpenWordbook(); };

            try
            {
                selection = new Selection();
                selection.MousePressed += MousePressed;
                selection.Selected += CaptureAsync;
                selection.ClipboardRequested += FromClipboard;
                if (selection.Shortcut != null) clipboardItem.Text += "  (" + selection.Shortcut + ")";
            }
            catch (Win32Exception) { Notify("无法启动全局划词。可以从托盘菜单查译剪贴板。"); }

            if (apiSettings == null && !args.Args.Contains("--background")) OpenSettings();
        }

        private void MousePressed(Point point)
        {
            ++captureVersion;
            if (transientLookup != null && !transientLookup.Contains(point)) transientLookup.Close();
        }

        private async void CaptureAsync(Point start, Point end, IntPtr window)
        {
            if (settings != null || Windows.OfType<LookupWindow>().Any(popup => popup.Handle == window) ||
                (wordbook != null && new WindowInteropHelper(wordbook).Handle == window)) return;

            int version = ++captureVersion;
            await Task.Delay(90);
            if (version != captureVersion) return;
            string text = await selectionReader.ReadAsync(Selection.ProcessId(window), start, window);
            if (version == captureVersion && settings == null && !string.IsNullOrWhiteSpace(text)) ShowLookup(text, end);
        }

        private void FromClipboard(Point point)
        {
            if (settings != null) return;
            ++captureVersion;
            try
            {
                string text = Clipboard.GetText().Trim();
                if (text.Length > 0) ShowLookup(text, point);
                else Notify("请先复制要查译的文字。");
            }
            catch (ExternalException) { Notify("剪贴板正在使用，请重试。"); }
        }

        private void ShowLookup(string text, Point anchor)
        {
            if (transientLookup != null) transientLookup.Close();
            var window = new LookupWindow(anchor);
            var cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            window.Closed += (sender, args) =>
            {
                if (transientLookup == window) transientLookup = null;
                cancellation.Cancel();
                cancellation.Dispose();
            };
            window.PinChanged += UpdatePin;
            transientLookup = window;
            if (text.Length > ChatApi.SelectionLimit)
                window.Status("最多支持 6,000 个字符，请缩短选区后重试。");
            else
                window.Prompt(() => QueryAsync(window, text, token));
            window.Show();
            window.Reposition();
        }

        private void UpdatePin(LookupWindow window)
        {
            ++captureVersion;
            if (window.IsPinned)
            {
                if (transientLookup == window) transientLookup = null;
                return;
            }

            if (transientLookup != null && transientLookup != window) transientLookup.Close();
            transientLookup = window;
        }

        private async void QueryAsync(LookupWindow window, string text, CancellationToken token)
        {
            var config = apiSettings;
            if (config == null)
            {
                window.Status("请先配置词典与翻译 API。", OpenSettings, "打开设置");
                return;
            }
            window.Status("查询中…");
            try
            {
                string word = Entry.SelectedWord(text);
                if (word == null)
                {
                    string translation = await api.TranslateAsync(text, config, token);
                    if (!token.IsCancellationRequested) window.Translation(translation);
                }
                else
                {
                    Entry entry = await api.LookupAsync(word, config, token);
                    if (!token.IsCancellationRequested) window.Word(entry, Speak, () => Save(entry));
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested) window.Status(RequestError(ex), () => QueryAsync(window, text, token));
            }
        }

        private string Save(Entry entry)
        {
            try
            {
                bool added = store.Add(entry);
                if (added && wordbook != null) wordbook.Reload();
                return added ? "已收藏" : "已在单词本中";
            }
            catch (Exception ex)
            {
                if (!IsLocalFileError(ex) && !(ex is ArgumentException)) throw;
                return "保存失败，请检查本地 XML 文件";
            }
        }

        private void OpenWordbook()
        {
            if (wordbook == null)
            {
                wordbook = new WordbookWindow(store, Speak);
                wordbook.Closed += (sender, args) => wordbook = null;
                wordbook.Show();
            }
            if (wordbook.WindowState == WindowState.Minimized) wordbook.WindowState = WindowState.Normal;
            wordbook.Activate();
        }

        private void OpenSettings()
        {
            if (settings != null) { settings.Activate(); return; }
            ++captureVersion;
            if (transientLookup != null) transientLookup.Close();
            settings = new SettingsWindow(apiSettings, store, api, startup, updated =>
            {
                apiSettings = updated;
                Notify("API 已连接。选中文字后，点击「翻译」。");
            });
            settings.Closed += (sender, args) => settings = null;
            settings.Show();
        }

        private void Speak(string word)
        {
            try
            {
                if (speech == null)
                {
                    using (var candidate = new SpeechSynthesizer())
                    {
                        var voice = candidate.GetInstalledVoices().FirstOrDefault(v => v.Enabled &&
                            v.VoiceInfo.Culture.TwoLetterISOLanguageName == "en");
                        if (voice == null) { Notify("请在 Windows 语言设置中安装英文语音。"); return; }
                        var synthesizer = new SpeechSynthesizer();
                        try { synthesizer.SelectVoice(voice.VoiceInfo.Name); }
                        catch { synthesizer.Dispose(); throw; }
                        speech = synthesizer;
                    }
                }
                speech.SpeakAsyncCancelAll();
                speech.SpeakAsync(word);
            }
            catch (InvalidOperationException) { Notify("Windows 英文语音当前不可用。"); }
            catch (COMException) { Notify("Windows 英文语音当前不可用。"); }
        }

        private void Notify(string message)
        {
            if (tray != null) tray.ShowBalloonTip(2500, "WordAss", message, Forms.ToolTipIcon.Info);
        }

        internal static bool IsLocalFileError(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is CryptographicException ||
                ex is XmlException || ex is InvalidDataException || ex is SecurityException;
        }

        internal static string RequestError(Exception ex)
        {
            if (ex is OperationCanceledException) return "请求超时，请重试。";
            if (ex is HttpRequestException) return "无法连接 API，请检查基础地址和网络。";
            if (ex is FormatException) return "请求格式有误，请检查 API Key。";
            if (ex is InvalidOperationException) return ex.Message;
            return "查询失败，请重试。";
        }

        protected override void OnExit(ExitEventArgs args)
        {
            if (selection != null) selection.Dispose();
            if (tray != null)
            {
                tray.Visible = false;
                tray.ContextMenuStrip.Dispose();
                tray.Dispose();
            }
            if (trayIcon != null) trayIcon.Dispose();
            if (speech != null) speech.Dispose();
            api.Dispose();
            base.OnExit(args);
        }
    }
}
