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
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace SourceGen.Views {
    /// <summary>
    /// Non-modal find dialog: enter text, press Find Next, keep pressing to step through
    /// matches.
    ///
    /// The dialog owns no search state; the caller supplies a callback that performs the
    /// search and reports the outcome, so the window keeps the "last search" for F3.
    /// </summary>
    public partial class FindDialog : Window {
        /// <summary>
        /// Search text currently in the box.
        /// </summary>
        public string SearchText {
            get { return this.FindControl<TextBox>("searchBox").Text ?? string.Empty; }
        }

        /// <summary>
        /// True if the Match case box is ticked.
        /// </summary>
        public bool MatchCase {
            get { return this.FindControl<CheckBox>("matchCaseCheck").IsChecked == true; }
        }

        /// <summary>
        /// Invoked when the user asks for the next match.  Returns a status message to show.
        /// </summary>
        public Func<string, bool, string> OnFind { get; set; }

        /// <summary>
        /// Invoked when the user asks for the previous match.
        /// </summary>
        public Func<string, bool, string> OnFindPrevious { get; set; }

        public FindDialog() {
            InitializeComponent();

            this.FindControl<Button>("findButton").Click += (sender, e) => {
                if (string.IsNullOrEmpty(SearchText)) {
                    this.FindControl<TextBlock>("statusText").Text = "Enter some text to find.";
                    return;
                }
                string status = OnFind?.Invoke(SearchText, MatchCase);
                this.FindControl<TextBlock>("statusText").Text = status ?? string.Empty;
            };
            this.FindControl<Button>("findPreviousButton").Click += (sender, e) => {
                if (string.IsNullOrEmpty(SearchText)) {
                    this.FindControl<TextBlock>("statusText").Text = "Enter some text to find.";
                    return;
                }
                string status = OnFindPrevious?.Invoke(SearchText, MatchCase);
                this.FindControl<TextBlock>("statusText").Text = status ?? string.Empty;
            };
            this.FindControl<Button>("closeButton").Click += (sender, e) => Close();

            Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("searchBox").Focus());
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Pre-fills the dialog with the previous search.
        /// </summary>
        public void Configure(string initialText, bool matchCase) {
            this.FindControl<TextBox>("searchBox").Text = initialText ?? string.Empty;
            this.FindControl<CheckBox>("matchCaseCheck").IsChecked = matchCase;
        }
    }
}
