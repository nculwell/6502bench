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
    /// Edits the label for a line.
    ///
    /// Closes with true on OK; <see cref="LabelText"/> holds the new label, or an empty
    /// string to remove the label.
    /// </summary>
    public partial class EditLabelDialog : Window {
        public string LabelText { get; private set; } = string.Empty;

        public EditLabelDialog() {
            InitializeComponent();

            this.FindControl<Button>("okButton").Click += (sender, e) => {
                LabelText = (this.FindControl<TextBox>("labelBox").Text ?? string.Empty).Trim();
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
        /// <param name="location">Location line, e.g. offset and address.</param>
        /// <param name="current">Current label in effect, or empty.</param>
        /// <param name="isUserLabel">True if the user set the current label.</param>
        /// <param name="editable">Set to the value to place in the text box.</param>
        public void Configure(string location, string current, bool isUserLabel,
                out string editable) {
            this.FindControl<TextBlock>("locationText").Text = location;

            if (string.IsNullOrEmpty(current)) {
                this.FindControl<TextBlock>("currentText").Text = "This line has no label.";
                editable = string.Empty;
            } else if (isUserLabel) {
                this.FindControl<TextBlock>("currentText").Text =
                    "Current label: " + current;
                editable = current;
            } else {
                this.FindControl<TextBlock>("currentText").Text =
                    "Current label: " + current + " (generated automatically)";
                // The auto label is not what the user would be editing, so start empty
                // rather than offering to duplicate it.
                editable = string.Empty;
            }

            TextBox box = this.FindControl<TextBox>("labelBox");
            box.Text = editable;
            Dispatcher.UIThread.Post(() => {
                box.Focus();
                box.SelectAll();
            });
        }
    }
}
