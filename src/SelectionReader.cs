using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WordAss
{
    internal sealed class SelectionReader
    {
        private const int MaxConcurrentReads = 4;
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(8);
        private readonly HashSet<uint> reading = new HashSet<uint>();

        public Task<string> ReadAsync(uint processId, Point start, Point end, IntPtr window)
        {
            return ReadAsync(processId, () =>
            {
                string text = Selection.Read(start, end, window);
                if (!string.IsNullOrWhiteSpace(text)) return text;
                // Some providers publish their final selection after the mouse-up event.
                Thread.Sleep(120);
                return Selection.Read(start, end, window);
            }, ReadTimeout);
        }

        internal async Task<string> ReadAsync(uint processId, Func<string> read, TimeSpan timeout)
        {
            lock (reading)
            {
                // A timed-out automation call can still be running in the target process.
                if (processId == 0 || reading.Count >= MaxConcurrentReads || !reading.Add(processId)) return null;
            }

            var pending = Task.Run(() =>
            {
                try { return read(); }
                catch (Exception error)
                {
                    Trace.TraceWarning("WordAss could not read the selection: {0}", error.GetType().Name);
                    return null;
                }
                finally
                {
                    lock (reading) reading.Remove(processId);
                }
            });
            if (await Task.WhenAny(pending, Task.Delay(timeout)).ConfigureAwait(false) != pending) return null;
            return await pending.ConfigureAwait(false);
        }
    }
}
