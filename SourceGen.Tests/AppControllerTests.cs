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

using Asm65;
using SourceGen.AsmGen;

namespace SourceGen.Tests {
    /// <summary>
    /// Tests for the workspace controller: this is the path the GUI takes when a project is
    /// opened, so covering it headlessly verifies that opening a real project produces a
    /// disassembly even though the window itself cannot be exercised without a display.
    /// </summary>
    public class AppControllerTests {
        /// <summary>
        /// A real completed project from the examples: Zippy Race for the Apple II.
        /// </summary>
        private static string TutorialPath {
            get {
                return Path.Combine(AppContext.BaseDirectory, "Examples", "A2-Zippy",
                    "ZIPPY#ff2000" + ProjectFile.FILENAME_EXT);
            }
        }

        [Fact]
        public void OpenProject_ProducesADisassembly() {
            Assert.True(File.Exists(TutorialPath), "missing test project " + TutorialPath);

            using AppController controller = new AppController();
            Assert.False(controller.HasProject);

            bool ok = controller.OpenProject(TutorialPath, out string error);

            Assert.True(ok, "OpenProject failed: " + error);
            Assert.Null(error);
            Assert.True(controller.HasProject);

            // The code list must actually be populated, and rows must be formatted: the
            // ListBox binds straight to these string properties.
            Assert.True(controller.CodeList.Count > 0, "code list is empty");

            // The first rows are the header comment, which carries no address, so look for
            // the first line that actually maps to one.
            DisplayList.FormattedParts firstAddr = null;
            foreach (DisplayList.FormattedParts parts in controller.CodeList) {
                if (!string.IsNullOrEmpty(parts.Addr)) {
                    firstAddr = parts;
                    break;
                }
            }
            Assert.NotNull(firstAddr);
            Assert.False(string.IsNullOrEmpty(firstAddr.Offset));

            // At least some lines must decode to instructions with a mnemonic.
            bool sawOpcode = false;
            foreach (DisplayList.FormattedParts parts in controller.CodeList) {
                if (!string.IsNullOrEmpty(parts.Opcode)) {
                    sawOpcode = true;
                    break;
                }
            }
            Assert.True(sawOpcode, "no instruction lines in the code list");

            Assert.False(string.IsNullOrEmpty(controller.StatusText));
            Assert.NotEqual("Ready", controller.StatusText);
        }

        [Fact]
        public void OpenProject_PopulatesSymbolsAndMessages() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(TutorialPath, out string error), error);

            // The example project defines labels, so the symbol pane must have content.
            Assert.True(controller.Symbols.Count > 0, "symbol list is empty");
            Assert.False(string.IsNullOrEmpty(controller.MessageSummary));
        }

        [Fact]
        public void CloseProject_ResetsState() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(TutorialPath, out string error), error);
            Assert.True(controller.HasProject);

            controller.CloseProject();

            Assert.False(controller.HasProject);
            Assert.Empty(controller.CodeList);
            Assert.Empty(controller.Symbols);
            Assert.Empty(controller.Messages);
            Assert.Equal("Ready", controller.StatusText);
        }

        [Fact]
        public void OpenProject_ReportsMissingDataFile() {
            // A project file with no binary beside it cannot be analysed; the controller
            // must report that rather than throwing.
            string bogus = Path.Combine(Path.GetTempPath(), "no-such-project" +
                ProjectFile.FILENAME_EXT);
            File.WriteAllText(bogus, "### 6502bench SourceGen dis65 v1.0 ###{}");
            try {
                using AppController controller = new AppController();
                bool ok = controller.OpenProject(bogus, out string error);

                Assert.False(ok);
                Assert.False(string.IsNullOrEmpty(error));
                Assert.Contains("Data file not found", error);
                Assert.False(controller.HasProject);
            } finally {
                File.Delete(bogus);
            }
        }

        [Fact]
        public void OpenProject_ReportsUnreadableProject() {
            string bogus = Path.Combine(Path.GetTempPath(), "garbage" +
                ProjectFile.FILENAME_EXT);
            File.WriteAllText(bogus, "not a project file at all");
            // Give it a companion file so the data-file check passes first.
            string data = bogus.Substring(0, bogus.Length -
                ProjectFile.FILENAME_EXT.Length);
            File.WriteAllBytes(data, new byte[] { 0xea });
            try {
                using AppController controller = new AppController();
                bool ok = controller.OpenProject(bogus, out string error);

                Assert.False(ok);
                Assert.False(string.IsNullOrEmpty(error));
                Assert.False(controller.HasProject);
            } finally {
                File.Delete(bogus);
                File.Delete(data);
            }
        }

        [Theory]
        [InlineData(AssemblerInfo.Id.Tass64)]
        [InlineData(AssemblerInfo.Id.Acme)]
        [InlineData(AssemblerInfo.Id.Cc65)]
        [InlineData(AssemblerInfo.Id.Merlin32)]
        public void GenerateAssembly_WritesSourcesForEachAssembler(AssemblerInfo.Id asmId) {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(TutorialPath, out string openError), openError);

            string outDir = Path.Combine(Path.GetTempPath(), "sg-gen-" + asmId);
            Directory.CreateDirectory(outDir);

            bool ok = controller.GenerateAssembly(asmId, outDir, out List<string> files,
                out string error);

            Assert.True(ok, "GenerateAssembly failed: " + error);
            Assert.NotEmpty(files);
            foreach (string path in files) {
                Assert.True(File.Exists(path), "expected generated file " + path);
                Assert.True(new FileInfo(path).Length > 0, "generated file is empty: " + path);
            }

            // Sanity-check the content: a real listing has many lines and the generator
            // reports internal failures inline rather than throwing.
            string text = File.ReadAllText(files[0]);
            Assert.True(text.Split('\n').Length > 5, "generated source looks too short");
            Assert.DoesNotContain("Internal error", text);
        }

        [Fact]
        public void GenerateAssembly_RequiresAnOpenProject() {
            using AppController controller = new AppController();
            string outDir = Path.Combine(Path.GetTempPath(), "sg-gen-none");
            Directory.CreateDirectory(outDir);

            bool ok = controller.GenerateAssembly(AssemblerInfo.Id.Tass64, outDir, out _, out string error);

            Assert.False(ok);
            Assert.Contains("No project", error);
        }

        /// <summary>
        /// Picks an offset in the code list that currently has no user label, so an edit can
        /// be applied and observed.
        /// </summary>
        private static int FindUnlabelledOffset(AppController controller) {
            for (int i = 0; i < controller.LineCount; i++) {
                int offset = controller.GetFileOffset(i);
                if (offset >= 0 && !controller.HasUserLabelAt(offset) &&
                        controller.GetAddressAt(offset) != Address.NON_ADDR) {
                    return offset;
                }
            }
            throw new InvalidOperationException("no editable offset found");
        }

        [Fact]
        public void SetLabel_AddsLabelAndAppearsInListing() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(TutorialPath, out string error), error);

            int offset = FindUnlabelledOffset(controller);
            Assert.True(controller.SetLabel(offset, "MyTestLabel", out error), error);

            Assert.Equal("MyTestLabel", controller.GetLabelAt(offset));
            Assert.True(controller.HasUserLabelAt(offset));
            Assert.True(controller.CanUndo);

            // The change must be reflected in the rendered listing, not just the model.
            bool sawLabel = false;
            foreach (DisplayList.FormattedParts parts in controller.CodeList) {
                if (parts.Label != null && parts.Label.Contains("MyTestLabel")) {
                    sawLabel = true;
                    break;
                }
            }
            Assert.True(sawLabel, "new label did not appear in the code list");
        }

        [Fact]
        public void SetLabel_UndoAndRedo() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(TutorialPath, out string error), error);

            int offset = FindUnlabelledOffset(controller);
            Assert.True(controller.SetLabel(offset, "UndoMe", out error), error);
            Assert.Equal("UndoMe", controller.GetLabelAt(offset));

            Assert.True(controller.Undo(out error), error);
            Assert.False(controller.HasUserLabelAt(offset));
            Assert.True(controller.CanRedo);

            Assert.True(controller.Redo(out error), error);
            Assert.Equal("UndoMe", controller.GetLabelAt(offset));
        }

        [Fact]
        public void SetLabel_RemoveWithEmptyString() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(TutorialPath, out string error), error);

            int offset = FindUnlabelledOffset(controller);
            Assert.True(controller.SetLabel(offset, "TempLabel", out error), error);
            Assert.True(controller.SetLabel(offset, string.Empty, out error), error);

            Assert.False(controller.HasUserLabelAt(offset));
        }

        [Fact]
        public void SetLabel_RejectsInvalidLabel() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(TutorialPath, out string error), error);

            int offset = FindUnlabelledOffset(controller);
            bool ok = controller.SetLabel(offset, "not a valid label!", out error);

            Assert.False(ok);
            Assert.Contains("not a valid label", error);
            Assert.False(controller.HasUserLabelAt(offset));
        }

        [Fact]
        public void SetComment_AddsUndoesAndRedoes() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(TutorialPath, out string error), error);

            int offset = FindUnlabelledOffset(controller);
            Assert.Equal(string.Empty, controller.GetCommentAt(offset));

            Assert.True(controller.SetComment(offset, "a test comment", out error), error);
            Assert.Equal("a test comment", controller.GetCommentAt(offset));

            bool sawComment = false;
            foreach (DisplayList.FormattedParts parts in controller.CodeList) {
                if (parts.Comment != null && parts.Comment.Contains("a test comment")) {
                    sawComment = true;
                    break;
                }
            }
            Assert.True(sawComment, "comment did not appear in the code list");

            Assert.True(controller.Undo(out error), error);
            Assert.Equal(string.Empty, controller.GetCommentAt(offset));
        }

        [Fact]
        public void Editing_RequiresAnOpenProject() {
            using AppController controller = new AppController();
            Assert.False(controller.SetLabel(0, "X", out string error));
            Assert.Contains("No project", error);
            Assert.False(controller.SetComment(0, "text", out error));
            Assert.Contains("No project", error);
            Assert.False(controller.CanUndo);
        }

        [Fact]
        public void EditsSurviveSaveAndReload() {
            // Work on a copy so the checked-in example is untouched.
            string tempDir = Path.Combine(Path.GetTempPath(),
                "sg-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string sourceData = TutorialPath.Substring(0,
                TutorialPath.Length - ProjectFile.FILENAME_EXT.Length);
            string projectPath = Path.Combine(tempDir, Path.GetFileName(TutorialPath));
            string dataPath = projectPath.Substring(0,
                projectPath.Length - ProjectFile.FILENAME_EXT.Length);
            File.Copy(TutorialPath, projectPath);
            File.Copy(sourceData, dataPath);

            try {
                int offset;
                using (AppController controller = new AppController()) {
                    Assert.True(controller.OpenProject(projectPath, out string error), error);
                    offset = FindUnlabelledOffset(controller);

                    Assert.True(controller.SetLabel(offset, "SavedLabel", out error), error);
                    Assert.True(controller.SetComment(offset, "saved comment", out error), error);
                    Assert.True(controller.IsDirty, "edits should mark the project dirty");

                    Assert.True(controller.SaveProject(out error), error);
                    Assert.False(controller.IsDirty, "save should clear the dirty flag");
                }

                // Re-open from scratch: the edits must have been persisted, which also
                // exercises the project-file serializer end to end.
                using (AppController reopened = new AppController()) {
                    Assert.True(reopened.OpenProject(projectPath, out string error), error);
                    Assert.Equal("SavedLabel", reopened.GetLabelAt(offset));
                    Assert.Equal("saved comment", reopened.GetCommentAt(offset));
                }
            } finally {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
