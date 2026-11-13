using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace WordAss
{
    internal sealed class Selection : IDisposable
    {
        private const int MouseHook = 14;
        private const int LeftDown = 0x0201;
        private const int LeftUp = 0x0202;
        private const int HotKeyMessage = 0x0312;
        private const int HotKeyId = 1;
        private const uint NoRepeat = 0x4000;
        private const uint Control = 0x2;
        private const uint Shift = 0x4;
        private const uint Alt = 0x1;
        private const uint LetterD = 0x44;
        private const uint Root = 2;

        private readonly HookProcedure procedure;
        private readonly HwndSource messages;
        private readonly Dispatcher uiDispatcher;
        private readonly Thread hookThread;
        private IntPtr hook;
        private volatile bool disposed;
        private Point down;
        private Point previousClick;
        private uint previousClickTime;
        private IntPtr previousClickWindow;
        private IntPtr origin;

        public event Action<Point> MousePressed;
        public event Action<Point, Point, IntPtr> Selected;
        public event Action<Point> ClipboardRequested;
        public string Shortcut { get; private set; }

        public Selection()
        {
            messages = new HwndSource(new HwndSourceParameters("WordAss messages") { ParentWindow = new IntPtr(-3) });
            uiDispatcher = messages.Dispatcher;
            messages.AddHook(WindowMessage);
            if (RegisterHotKey(messages.Handle, HotKeyId, NoRepeat | Control | Alt, LetterD)) Shortcut = "Ctrl+Alt+D";
            else if (RegisterHotKey(messages.Handle, HotKeyId, NoRepeat | Control | Alt | Shift, LetterD)) Shortcut = "Ctrl+Alt+Shift+D";
            procedure = MouseMessage;
            hookThread = new Thread(Listen) { IsBackground = true, Name = "WordAss mouse listener" };
            hookThread.Start();
        }

        private void Listen()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            if (disposed) return;

            // Windows can silently remove a timed-out low-level hook. Register a fresh one periodically.
            var refresh = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = TimeSpan.FromSeconds(30) };
            refresh.Tick += (sender, args) => RefreshHook();
            RefreshHook();
            refresh.Start();
            try { Dispatcher.Run(); }
            finally
            {
                refresh.Stop();
                if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
            }
        }

        private void RefreshHook()
        {
            if (disposed) return;
            IntPtr next = SetWindowsHookEx(MouseHook, procedure, GetModuleHandle(null), 0);
            if (next == IntPtr.Zero)
            {
                Trace.TraceWarning("WordAss mouse hook registration failed (Win32 {0}).", Marshal.GetLastWin32Error());
                return;
            }
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
            hook = next;
        }

        private IntPtr MouseMessage(int code, IntPtr message, IntPtr data)
        {
            if (!disposed && code == 0 && (message.ToInt32() == LeftDown || message.ToInt32() == LeftUp))
            {
                var mouse = (MouseData)Marshal.PtrToStructure(data, typeof(MouseData));
                uiDispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => ProcessMouse(message, mouse)));
            }
            return CallNextHookEx(IntPtr.Zero, code, message, data);
        }

        private void ProcessMouse(IntPtr message, MouseData mouse)
        {
            if (disposed) return;
            var point = new Point(mouse.Position.X, mouse.Position.Y);
            if (message.ToInt32() == LeftDown)
            {
                down = point;
                origin = GetAncestor(WindowFromPoint(mouse.Position), Root);
                var pressed = MousePressed;
                if (pressed != null) pressed(point);
                return;
            }
            if (origin == IntPtr.Zero) return;

            IntPtr window = origin;
            origin = IntPtr.Zero;
            bool drag = Math.Abs(point.X - down.X) >= Math.Max(5, GetSystemMetrics(68)) ||
                Math.Abs(point.Y - down.Y) >= Math.Max(5, GetSystemMetrics(69));
            bool doubleClick = !drag && window == previousClickWindow &&
                unchecked(mouse.Time - previousClickTime) <= GetDoubleClickTime() &&
                Math.Abs(point.X - previousClick.X) <= GetSystemMetrics(36) &&
                Math.Abs(point.Y - previousClick.Y) <= GetSystemMetrics(37);
            previousClick = point;
            previousClickTime = mouse.Time;
            previousClickWindow = drag || doubleClick ? IntPtr.Zero : window;

            var selected = Selected;
            if ((drag || doubleClick) && selected != null) selected(down, point, window);
        }

        private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == HotKeyMessage && wParam.ToInt32() == HotKeyId)
            {
                handled = true;
                var requested = ClipboardRequested;
                if (requested != null) requested(CursorPosition());
            }
            return IntPtr.Zero;
        }

        public static string Read(Point start, IntPtr window)
        {
            try
            {
                if (window != GetForegroundWindow()) return null;
                var focused = AutomationElement.FocusedElement;
                if (focused != null && focused.Current.IsPassword) return null;

                var hit = AutomationElement.FromPoint(start);
                int processId = hit == null ? 0 : hit.Current.ProcessId;
                for (var element = hit; element != null; element = TreeWalker.RawViewWalker.GetParent(element))
                {
                    if (element.Current.IsPassword || element.Current.ProcessId != processId) break;
                    string value = ReadText(element);
                    if (value != null) return GetForegroundWindow() == window ? value : null;
                    if (element.Current.NativeWindowHandle == window.ToInt32()) break;
                }

                if (focused == null || focused.Current.ProcessId != processId || GetForegroundWindow() != window)
                    return null;
                string focusedText = ReadText(focused);
                return GetForegroundWindow() == window ? focusedText : null;
            }
            catch (ElementNotAvailableException) { return null; }
            catch (InvalidOperationException) { return null; }
            catch (COMException) { return null; }
        }

        public static uint ProcessId(IntPtr window)
        {
            uint id;
            GetWindowThreadProcessId(window, out id);
            return id;
        }

        private static string ReadText(AutomationElement element)
        {
            object pattern;
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out pattern)) return null;
            var ranges = ((TextPattern)pattern).GetSelection();
            if (ranges == null) return null;
            string text = string.Join("\n", ranges.Select(range => range.GetText(ChatApi.SelectionLimit + 1))).Trim();
            return text.Length == 0 ? null : text;
        }

        public static Point CursorPosition()
        {
            NativePoint point;
            GetCursorPos(out point);
            return new Point(point.X, point.Y);
        }

        public static void PlacePopup(IntPtr hwnd, Point point, Size? promptSize = null)
        {
            NativeRect rect;
            if (!GetWindowRect(hwnd, out rect)) return;
            var area = Forms.Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y)).WorkingArea;
            bool isPrompt = promptSize.HasValue;
            int width = isPrompt ? (int)Math.Ceiling(promptSize.Value.Width) : rect.Right - rect.Left;
            int height = isPrompt ? (int)Math.Ceiling(promptSize.Value.Height) : rect.Bottom - rect.Top;
            int left = isPrompt ? (int)point.X - width / 2 : (int)point.X + 12;
            int x = Math.Max(area.Left, Math.Min(left, area.Right - width));
            int above = (int)point.Y - height - (isPrompt ? 8 : 12);
            int below = (int)point.Y + 18;
            bool placeAbove = isPrompt ? above >= area.Top : below + height > area.Bottom;
            int y = placeAbove ? above : below;
            y = Math.Max(area.Top, Math.Min(y, area.Bottom - height));
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, 0x0015);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var dispatcher = Dispatcher.FromThread(hookThread);
            if (dispatcher != null) dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            if (Shortcut != null) UnregisterHotKey(messages.Handle, HotKeyId);
            messages.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseData
        {
            public NativePoint Position;
            public uint Data, Flags, Time;
            public IntPtr ExtraInfo;
        }

        private delegate IntPtr HookProcedure(int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProcedure callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    }
}
