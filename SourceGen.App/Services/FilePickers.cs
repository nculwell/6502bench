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
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace SourceGen.Services {
    /// <summary>
    /// File and folder pickers.
    ///
    /// Replaces Microsoft.Win32.OpenFileDialog/SaveFileDialog and the WinForms folder
    /// browser.  Avalonia routes these through TopLevel.StorageProvider, which is backed by
    /// the platform's native dialogs (GTK/portal on Linux, Win32 on Windows, NSOpenPanel on
    /// macOS), so the same code works everywhere.
    ///
    /// Results are returned as full local paths, or null if the user cancelled.
    /// </summary>
    public static class FilePickers {
        /// <summary>
        /// Prompts for a single existing file.
        /// </summary>
        public static async Task<string> OpenFileAsync(Window owner, string title,
                FilePickerFileType[] types) {
            FilePickerOpenOptions options = new FilePickerOpenOptions {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = types,
            };
            IReadOnlyList<IStorageFile> files =
                await owner.StorageProvider.OpenFilePickerAsync(options);
            return (files != null && files.Count > 0) ? files[0].TryGetLocalPath() : null;
        }

        /// <summary>
        /// Prompts for one or more existing files.
        /// </summary>
        public static async Task<string[]> OpenFilesAsync(Window owner, string title,
                FilePickerFileType[] types) {
            FilePickerOpenOptions options = new FilePickerOpenOptions {
                Title = title,
                AllowMultiple = true,
                FileTypeFilter = types,
            };
            IReadOnlyList<IStorageFile> files =
                await owner.StorageProvider.OpenFilePickerAsync(options);
            if (files == null || files.Count == 0) {
                return null;
            }

            List<string> paths = new List<string>(files.Count);
            foreach (IStorageFile file in files) {
                string path = file.TryGetLocalPath();
                if (path != null) {
                    paths.Add(path);
                }
            }
            return paths.ToArray();
        }

        /// <summary>
        /// Prompts for a save destination.
        /// </summary>
        public static async Task<string> SaveFileAsync(Window owner, string title,
                string suggestedName, FilePickerFileType[] types) {
            FilePickerSaveOptions options = new FilePickerSaveOptions {
                Title = title,
                SuggestedFileName = suggestedName,
                FileTypeChoices = types,
                ShowOverwritePrompt = true,
            };
            IStorageFile file = await owner.StorageProvider.SaveFilePickerAsync(options);
            return file?.TryGetLocalPath();
        }

        /// <summary>
        /// Prompts for a folder.
        /// </summary>
        public static async Task<string> OpenFolderAsync(Window owner, string title) {
            IReadOnlyList<IStorageFolder> folders =
                await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                    Title = title,
                    AllowMultiple = false,
                });
            return (folders != null && folders.Count > 0) ? folders[0].TryGetLocalPath() : null;
        }
    }
}
