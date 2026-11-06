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
using System.Windows.Input;

namespace SourceGen.Commands {
    /// <summary>
    /// Minimal ICommand implementation.
    ///
    /// This replaces the WPF RoutedUICommand/CommandBinding pair.  Avalonia has no command
    /// routing, so each menu item and key binding points directly at one of these and the
    /// window owns the handlers plus the CanExecute state.
    /// </summary>
    public class RelayCommand : ICommand {
        private readonly Action<object> mExecute;
        private readonly Func<object, bool> mCanExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null) {
            mExecute = execute ?? throw new ArgumentNullException(nameof(execute));
            mCanExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
            : this(_ => execute(), canExecute == null ? (Func<object, bool>)null : _ => canExecute()) {
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) {
            return mCanExecute == null || mCanExecute(parameter);
        }

        public void Execute(object parameter) {
            mExecute(parameter);
        }

        /// <summary>
        /// Raises CanExecuteChanged so bound controls re-query their enabled state.  WPF did
        /// this automatically for routed commands via CommandManager.RequerySuggested.
        /// </summary>
        public void RaiseCanExecuteChanged() {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
