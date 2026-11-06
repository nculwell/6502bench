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
using System.Threading.Tasks;
using Avalonia.Controls;

namespace SourceGen.Services {
    /// <summary>
    /// Message dialogs.
    ///
    /// This is the replacement for WPF's MessageBox.  Call sites that used
    /// MessageBox.Show(...) become await Dialogs.*Async(...): Avalonia has no nested
    /// modal message loop, so the dialog has to be awaited rather than blocked on.
    /// </summary>
    public static class Dialogs {
        /// <summary>
        /// Shows an informational or error message with a single dismiss button.
        /// </summary>
        public static async Task ShowAsync(Window owner, string message, string caption) {
            await ShowAsync(owner, message, caption, false, null, null);
        }

        /// <summary>
        /// Shows a message with OK/Cancel, or OK alone.
        /// </summary>
        /// <returns>True if OK was pressed, false if the dialog was cancelled.</returns>
        public static async Task<bool> ShowAsync(Window owner, string message, string caption,
                bool showCancel, string okText, string cancelText) {
            MessageDialog dlg = new MessageDialog();
            dlg.Configure(caption, message, showCancel, okText, cancelText);

            if (owner != null) {
                return await dlg.ShowDialog<bool>(owner);
            }

            // No owner (e.g. called before the main window exists): still show the dialog.
            dlg.Show();
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            dlg.Closed += (sender, e) => tcs.TrySetResult(false);
            return await tcs.Task;
        }

        /// <summary>
        /// Yes/No confirmation.
        /// </summary>
        public static async Task<bool> ConfirmAsync(Window owner, string message,
                string caption) {
            return await ShowAsync(owner, message, caption, true, "Yes", "No");
        }

        /// <summary>
        /// OK/Cancel confirmation.
        /// </summary>
        public static async Task<bool> OkCancelAsync(Window owner, string message,
                string caption) {
            return await ShowAsync(owner, message, caption, true, null, null);
        }
    }
}
