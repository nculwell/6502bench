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
using System.Collections.Generic;
using System.IO;
using Xunit;

using SourceGen.AsmGen;
using SourceGen.Tui;

namespace SourceGen.Tests {
    /// <summary>
    /// Tests for the terminal UI's presentation logic.
    ///
    /// TuiModel carries everything the screen displays and everything a keypress does, with
    /// no terminal dependency, so the TUI can be verified without a TTY -- the same approach
    /// used for the graphical application's controller.
    /// </summary>
    public class TuiModelTests {
        private static string ExamplePath {
            get {
                return Path.Combine(AppContext.BaseDirectory, "Examples", "A2-Zippy",
                    "ZIPPY#ff2000" + ProjectFile.FILENAME_EXT);
            }
        }

        private static TuiModel OpenModel(AppController controller) {
            TuiModel model = new TuiModel(controller);
            Assert.True(model.OpenProject(ExamplePath, out string error), error);
            return model;
        }


        /// <summary>
        /// Index of a code list line that maps to a real file offset and has an address.
        /// The first lines of a listing are header/comment lines with no offset.
        /// </summary>
        private static int FindEditableLine(AppController controller, bool requireAddress) {
            for (int i = 0; i < controller.LineCount; i++) {
                int offset = controller.GetFileOffset(i);
                if (offset < 0) {
                    continue;
                }
                if (requireAddress &&
                        controller.GetAddressAt(offset) == Asm65.Address.NON_ADDR) {
                    continue;
                }
                return i;
            }
            throw new InvalidOperationException("no editable line found");
        }

        [Fact]
        public void OpenProject_ShowsTheListing() {
            using AppController controller = new AppController();
            TuiModel model = new TuiModel(controller);

            int emptyFirst = 0;
            Assert.False(model.HasProject);
            Assert.Empty(model.GetCodeLines(80, 20, ref emptyFirst));

            Assert.True(model.OpenProject(ExamplePath, out string error), error);
            Assert.True(model.HasProject);
            Assert.Equal(0, model.SelectedLine);

            int first = 0;
            List<string> lines = model.GetCodeLines(80, 20, ref first);
            Assert.NotEmpty(lines);
            Assert.Equal(20, lines.Count);
            // Columns are padded, so every row is exactly the requested width.
            Assert.All(lines, line => Assert.Equal(80, line.Length));
        }

        [Fact]
        public void MoveSelection_ClampsToTheListing() {
            using AppController controller = new AppController();
            TuiModel model = OpenModel(controller);

            model.MoveSelection(-5);
            Assert.Equal(0, model.SelectedLine);

            model.MoveToEnd(true);
            Assert.Equal(controller.LineCount - 1, model.SelectedLine);

            model.MoveSelection(10);
            Assert.Equal(controller.LineCount - 1, model.SelectedLine);

            model.MoveToEnd(false);
            Assert.Equal(0, model.SelectedLine);
        }

        [Fact]
        public void GetCodeLines_ScrollsToKeepSelectionVisible() {
            using AppController controller = new AppController();
            TuiModel model = OpenModel(controller);

            int first = 0;
            model.SetSelectedLine(50);
            List<string> lines = model.GetCodeLines(80, 10, ref first);

            Assert.True(first > 0, "view should have scrolled");
            Assert.True(model.SelectedLine >= first && model.SelectedLine < first + 10,
                "selection must be inside the visible window");
            Assert.Equal(10, lines.Count);
        }

        [Fact]
        public void EditLabel_ChangesTheDisplayedListing() {
            using AppController controller = new AppController();
            TuiModel model = OpenModel(controller);

            model.SetSelectedLine(FindEditableLine(controller, requireAddress: true));

            Assert.True(model.SetSelectedLabel("TuiLabel", out string error), error);

            // Scan the whole listing: the label is rendered on the line that owns the offset.
            int first = 0;
            List<string> lines = model.GetCodeLines(120, controller.LineCount, ref first);
            Assert.Contains(lines, l => l.Contains("TuiLabel"));
        }

        [Fact]
        public void EditComment_AndUndoRoundTrip() {
            using AppController controller = new AppController();
            TuiModel model = OpenModel(controller);
            model.SetSelectedLine(FindEditableLine(controller, requireAddress: false));

            Assert.True(model.SetSelectedComment("terminal comment", out string error), error);
            Assert.Equal("terminal comment", model.GetSelectedComment());
            Assert.Contains("[modified]", model.GetStatusLine());

            Assert.True(model.Undo(out error), error);
            Assert.Equal(string.Empty, model.GetSelectedComment());
        }

        [Fact]
        public void Save_ClearsModifiedMarker() {
            using AppController controller = new AppController();
            TuiModel model = OpenModel(controller);
            model.SetSelectedLine(FindEditableLine(controller, requireAddress: false));
            Assert.True(model.SetSelectedComment("saved via tui", out string error), error);

            // Work on a copy so the shared example project is not modified.
            string tempDir = Path.Combine(Path.GetTempPath(),
                "sg-tui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string dataPath = ExamplePath.Substring(0,
                ExamplePath.Length - ProjectFile.FILENAME_EXT.Length);
            string copyProject = Path.Combine(tempDir, Path.GetFileName(ExamplePath));
            string copyData = copyProject.Substring(0,
                copyProject.Length - ProjectFile.FILENAME_EXT.Length);
            File.Copy(ExamplePath, copyProject);
            File.Copy(dataPath, copyData);

            try {
                Assert.True(model.OpenProject(copyProject, out error), error);
                model.SetSelectedLine(FindEditableLine(controller, requireAddress: false));
                Assert.True(model.SetSelectedComment("saved via tui", out error), error);
                Assert.Contains("[modified]", model.GetStatusLine());

                Assert.True(model.Save(out error), error);
                Assert.DoesNotContain("[modified]", model.GetStatusLine());
            } finally {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void GenerateAssembly_ProducesFiles() {
            using AppController controller = new AppController();
            TuiModel model = OpenModel(controller);

            string outDir = Path.Combine(Path.GetTempPath(),
                "sg-tui-gen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outDir);
            try {
                Assert.True(model.GenerateAssembly(AssemblerInfo.Id.Tass64, outDir,
                    out List<string> files, out string error), error);
                Assert.NotEmpty(files);
                Assert.All(files, f => Assert.True(File.Exists(f)));
            } finally {
                Directory.Delete(outDir, true);
            }
        }

        [Fact]
        public void StatusAndMessages_ReflectProjectState() {
            using AppController controller = new AppController();
            TuiModel model = new TuiModel(controller);

            Assert.Contains("No project", model.GetStatusLine());

            Assert.True(model.OpenProject(ExamplePath, out string error), error);
            Assert.DoesNotContain("No project", model.GetStatusLine());
            Assert.NotEmpty(model.GetSymbolLines(10));
            Assert.Equal("Code  1/" + controller.LineCount, model.GetCodeTitle());
        }
    }
}
