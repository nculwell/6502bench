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
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using SourceGen.Views;

namespace SourceGen {
    /// <summary>
    /// Application class.
    ///
    /// This replaces the WPF App class.  The version number it used to own now lives in
    /// the engine (ProgramInfo.Version) so headless code can reference it.
    /// </summary>
    public partial class App : Application {
        /// <summary>
        /// SourceGen version number.  Re-exported from the engine.
        /// </summary>
        public static CommonUtil.Version ProgramVersion => ProgramInfo.Version;

        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.MainWindow = new MainWindow();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
