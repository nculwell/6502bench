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

using SourceGen.AsmGen;

namespace SourceGen.Views {
    /// <summary>
    /// Asks which assembler dialect to generate.
    ///
    /// Closes with the selected <see cref="AssemblerInfo.Id"/>, or
    /// <see cref="AssemblerInfo.Id.Unknown"/> if cancelled.
    /// </summary>
    public partial class AssemblerPickerDialog : Window {
        public AssemblerPickerDialog() {
            InitializeComponent();

            this.FindControl<Button>("okButton").Click +=
                (sender, e) => Close(GetSelection());
            this.FindControl<Button>("cancelButton").Click +=
                (sender, e) => Close(AssemblerInfo.Id.Unknown);
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        public AssemblerInfo.Id GetSelection() {
            if (this.FindControl<RadioButton>("acmeOption").IsChecked == true) {
                return AssemblerInfo.Id.Acme;
            }
            if (this.FindControl<RadioButton>("cc65Option").IsChecked == true) {
                return AssemblerInfo.Id.Cc65;
            }
            if (this.FindControl<RadioButton>("merlinOption").IsChecked == true) {
                return AssemblerInfo.Id.Merlin32;
            }
            return AssemblerInfo.Id.Tass64;
        }
    }
}
