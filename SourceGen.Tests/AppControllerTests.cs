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
    }
}
