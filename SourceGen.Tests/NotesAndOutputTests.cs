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
using System.IO;
using Xunit;

using SourceGen.Tui;

namespace SourceGen.Tests {
    /// <summary>
    /// Tests for notes and for the two output writers (HTML listing and label file).
    /// </summary>
    public class NotesAndOutputTests {
        private static string ExamplePath {
            get {
                return Path.Combine(AppContext.BaseDirectory, "Examples", "A2-Zippy",
                    "ZIPPY#ff2000" + ProjectFile.FILENAME_EXT);
            }
        }

        private static int FindEditableLine(AppController controller, int skip = 0) {
            for (int i = 0; i < controller.LineCount; i++) {
                int offset = controller.GetFileOffset(i);
                if (offset >= 0 && controller.GetNoteAt(offset).Length == 0) {
                    if (skip-- <= 0) {
                        return i;
                    }
                }
            }
            throw new InvalidOperationException("no suitable line found");
        }

        private static string NewTempDir(string tag) {
            string dir = Path.Combine(Path.GetTempPath(), tag + "-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ---- notes ----

        [Fact]
        public void Note_SetReadAndClear() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int line = FindEditableLine(controller);
            int offset = controller.GetFileOffset(line);

            Assert.True(controller.SetNote(offset, "analyst note", out error), error);
            Assert.Equal("analyst note", controller.GetNoteAt(offset));

            Assert.True(controller.SetNote(offset, string.Empty, out error), error);
            Assert.Equal(string.Empty, controller.GetNoteAt(offset));
        }

        [Fact]
        public void Note_UndoRestoresPrevious() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int offset = controller.GetFileOffset(FindEditableLine(controller));
            Assert.True(controller.SetNote(offset, "first", out error), error);
            Assert.True(controller.SetNote(offset, "second", out error), error);

            Assert.True(controller.Undo(out error), error);
            Assert.Equal("first", controller.GetNoteAt(offset));
        }

        [Fact]
        public void Note_RequiresOpenProject() {
            using AppController controller = new AppController();
            Assert.False(controller.SetNote(0, "x", out string error));
            Assert.Contains("No project", error);
        }

        [Fact]
        public void Note_IsSeparateFromLongComment() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int offset = controller.GetFileOffset(FindEditableLine(controller));
            Assert.True(controller.SetLongComment(offset, "a rare long comment", out error),
                error);

            // Setting a note must not disturb the long comment, which is emitted into the
            // generated output, unlike a note.
            Assert.True(controller.SetNote(offset, "private note", out error), error);
            Assert.Equal("a rare long comment", controller.GetLongCommentAt(offset));
            Assert.Equal("private note", controller.GetNoteAt(offset));
        }

        // ---- HTML export ----

        [Fact]
        public void ExportHtml_WritesAListing() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            string dir = NewTempDir("sg-html");
            try {
                string outPath = Path.Combine(dir, "listing.html");
                Assert.True(controller.ExportHtml(outPath, true, out error), error);
                Assert.True(File.Exists(outPath), "no HTML file was written");

                string text = File.ReadAllText(outPath);
                Assert.Contains("<html", text, StringComparison.OrdinalIgnoreCase);
                Assert.True(text.Length > 2000, "listing looks too short to be real");
                // The project's own symbols should appear in the listing.
                Assert.Contains("lda", text, StringComparison.OrdinalIgnoreCase);
            } finally {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void ExportHtml_RequiresOpenProject() {
            using AppController controller = new AppController();
            Assert.False(controller.ExportHtml(Path.Combine(Path.GetTempPath(), "x.html"),
                true, out string error));
            Assert.Contains("No project", error);
        }

        // ---- label file ----

        [Fact]
        public void GenerateLabels_WritesSymbolLines() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            string dir = NewTempDir("sg-labels");
            try {
                string outPath = Path.Combine(dir, "zippy.sym65");
                Assert.True(controller.GenerateLabelFile(outPath, false, out error), error);
                Assert.True(File.Exists(outPath), "no label file was written");

                string[] lines = File.ReadAllLines(outPath);
                Assert.NotEmpty(lines);
                Assert.Contains(lines, l => l.StartsWith("al ", StringComparison.Ordinal));
            } finally {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void GenerateLabels_RequiresOpenProject() {
            using AppController controller = new AppController();
            Assert.False(controller.GenerateLabelFile(
                Path.Combine(Path.GetTempPath(), "x.sym65"), false, out string error));
            Assert.Contains("No project", error);
        }

        // ---- terminal UI wrappers ----

        [Fact]
        public void TuiNotesAndOutput_WorkThroughModel() {
            using AppController controller = new AppController();
            TuiModel model = new TuiModel(controller);
            Assert.True(model.OpenProject(ExamplePath, out string error), error);

            model.SetSelectedLine(FindEditableLine(controller));
            Assert.Equal(string.Empty, model.GetSelectedNote());
            Assert.True(model.SetSelectedNote("tui note", out error), error);
            Assert.Equal("tui note", model.GetSelectedNote());

            string dir = NewTempDir("sg-tui-out");
            try {
                Assert.True(model.ExportHtml(Path.Combine(dir, "out.html"), out error), error);
                Assert.True(model.GenerateLabels(Path.Combine(dir, "out.sym65"), false,
                    out error), error);
                Assert.True(File.Exists(Path.Combine(dir, "out.html")));
                Assert.True(File.Exists(Path.Combine(dir, "out.sym65")));
            } finally {
                Directory.Delete(dir, true);
            }
        }
    }
}
