/*
 * Copyright 2019 faddenSoft
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace SourceGen.Services {
    /// <summary>
    /// Threading and clipboard helpers.
    ///
    /// Replaces WPF's Application.Current.Dispatcher and System.Windows.Clipboard.  Avalonia
    /// equivalent: Dispatcher.UIThread, and TopLevel.Clipboard which requires the TopLevel
    /// (usually the main window) rather than being a global.
    /// </summary>
    public static class Ui {
        /// <summary>
        /// True if the calling thread is the UI thread.
        /// </summary>
        public static bool IsOnUiThread { get { return Dispatcher.UIThread.CheckAccess(); } }

        /// <summary>
        /// Queues work to run on the UI thread.  Safe to call from any thread.
        /// </summary>
        public static void Post(Action action) {
            Dispatcher.UIThread.Post(action);
        }

        /// <summary>
        /// Runs work on the UI thread and awaits it.  Safe to call from any thread.
        /// </summary>
        public static Task InvokeAsync(Action action) {
            return Dispatcher.UIThread.InvokeAsync(action).GetTask();
        }

        /// <summary>
        /// Runs work on the UI thread and awaits its result.
        /// </summary>
        public static Task<T> InvokeAsync<T>(Func<T> func) {
            return Dispatcher.UIThread.InvokeAsync(func).GetTask();
        }

        /// <summary>
        /// Copies plain text to the system clipboard.
        /// </summary>
        public static async Task CopyTextAsync(TopLevel topLevel, string text) {
            if (topLevel == null || topLevel.Clipboard == null) {
                return;
            }
            await topLevel.Clipboard.SetTextAsync(text ?? string.Empty);
        }

        /// <summary>
        /// Reads plain text from the system clipboard, or null if it holds none.
        /// </summary>
        public static async Task<string> PasteTextAsync(TopLevel topLevel) {
            if (topLevel == null || topLevel.Clipboard == null) {
                return null;
            }
            return await topLevel.Clipboard.GetTextAsync();
        }
    }
}
