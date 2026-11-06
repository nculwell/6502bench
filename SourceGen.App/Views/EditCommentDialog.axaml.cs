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
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace SourceGen.Views {
    /// <summary>
    /// Edits the end-of-line comment for a line.
    ///
    /// Closes with true on OK; <see cref="CommentText"/> holds the new text.
    /// </summary>
    public partial class EditCommentDialog : Window {
        /// <summary>
        /// Text entered by the user.  Only meaningful when the dialog returned true.
        /// </summary>
        public string CommentText { get; private set; } = string.Empty;

        public EditCommentDialog() {
            InitializeComponent();

            this.FindControl<Button>("okButton").Click += (sender, e) => {
                CommentText = this.FindControl<TextBox>("commentBox").Text ?? string.Empty;
                Close(true);
            };
            this.FindControl<Button>("cancelButton").Click += (sender, e) => Close(false);
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Pre-fills the dialog.
        /// </summary>
        public void Configure(string location, string initialComment) {
            this.FindControl<TextBlock>("locationText").Text = location;
            TextBox box = this.FindControl<TextBox>("commentBox");
            box.Text = initialComment ?? string.Empty;

            // Focus and select so typing replaces the old text immediately.
            Dispatcher.UIThread.Post(() => {
                box.Focus();
                box.SelectAll();
            });
        }
    }
}
