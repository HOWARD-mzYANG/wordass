using System;
using System.IO;
using Microsoft.Win32;

namespace WordAss
{
    internal sealed class StartupRegistration
    {
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Entry = "WordAss";
        private readonly string command;

        public StartupRegistration(string executablePath)
        {
            command = "\"" + Path.GetFullPath(executablePath) + "\" --background";
        }

        private static string ReadCommand()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunPath))
                return key == null ? null : key.GetValue(Entry, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }

        public bool IsEnabled() { return ReadCommand() != null; }

        public void RefreshPath()
        {
            string previous = ReadCommand();
            if (previous != null && !string.Equals(previous, command, StringComparison.OrdinalIgnoreCase)) SetEnabled(true);
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (command.Length > 260) throw new IOException("应用路径太长，无法设为开机启动。");
                using (var key = Registry.CurrentUser.CreateSubKey(RunPath))
                {
                    if (key == null) throw new IOException("无法打开启动设置。");
                    key.SetValue(Entry, command, RegistryValueKind.String);
                }
            }
            else
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunPath, true))
                    if (key != null) key.DeleteValue(Entry, false);
            }
        }
    }
}
