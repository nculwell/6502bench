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
    /// Edits the multi-line comment attached to a line.
    ///
    /// Closes with true on OK; <see cref="CommentText"/> holds the new text, or an empty
    /// string to remove the comment.
    /// </summary>
    public partial class EditLongCommentDialog : Window {
        public string CommentText { get; private set; } = string.Empty;

        public EditLongCommentDialog() {
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
        /// Pre-fills the editor.
        /// </summary>
        public void Configure(string location, string text) {
            Title = "Edit Long Comment -- " + location;
            TextBox box = this.FindControl<TextBox>("commentBox");
            box.Text = text ?? string.Empty;
            Dispatcher.UIThread.Post(() => box.Focus());
        }
    }
}
