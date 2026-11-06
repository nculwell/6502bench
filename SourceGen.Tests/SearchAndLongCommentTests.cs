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
    /// Tests for multi-line comments and listing search, covering both the engine-facing
    /// controller and the terminal UI's wrapper over it.
    /// </summary>
    public class SearchAndLongCommentTests {
        private static string ExamplePath {
            get {
                return Path.Combine(AppContext.BaseDirectory, "Examples", "A2-Zippy",
                    "ZIPPY#ff2000" + ProjectFile.FILENAME_EXT);
            }
        }

        private static int FindEditableLine(AppController controller) {
            for (int i = 0; i < controller.LineCount; i++) {
                if (controller.GetFileOffset(i) >= 0) {
                    return i;
                }
            }
            throw new InvalidOperationException("no editable line found");
        }

        /// <summary>
        /// A line whose offset has no long comment yet.  The example project already carries
        /// a header comment, so the first editable line is not a blank slate.
        /// </summary>
        private static int FindLineWithoutLongComment(AppController controller) {
            for (int i = 0; i < controller.LineCount; i++) {
                int offset = controller.GetFileOffset(i);
                if (offset >= 0 && controller.GetLongCommentAt(offset).Length == 0) {
                    return i;
                }
            }
            throw new InvalidOperationException("every line already has a long comment");
        }

        // ---- long comments ----

        [Fact]
        public void LongComment_AddEditAndRemove() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int line = FindLineWithoutLongComment(controller);
            int offset = controller.GetFileOffset(line);
            Assert.Equal(string.Empty, controller.GetLongCommentAt(offset));

            Assert.True(controller.SetLongComment(offset, "first line\nsecond line", out error),
                error);
            Assert.Equal("first line\nsecond line", controller.GetLongCommentAt(offset));

            // Editing replaces the whole comment.
            Assert.True(controller.SetLongComment(offset, "replaced", out error), error);
            Assert.Equal("replaced", controller.GetLongCommentAt(offset));

            // The comment must be rendered into the listing as its own line(s).
            bool sawInListing = false;
            foreach (DisplayList.FormattedParts parts in controller.CodeList) {
                if (parts.Comment != null && parts.Comment.Contains("replaced")) {
                    sawInListing = true;
                    break;
                }
            }
            Assert.True(sawInListing, "long comment did not appear in the listing");

            // Empty text removes it.
            Assert.True(controller.SetLongComment(offset, string.Empty, out error), error);
            Assert.Equal(string.Empty, controller.GetLongCommentAt(offset));
        }

        [Fact]
        public void LongComment_UndoRestoresPreviousValue() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int offset = controller.GetFileOffset(FindLineWithoutLongComment(controller));
            Assert.True(controller.SetLongComment(offset, "keep me", out error), error);
            Assert.True(controller.SetLongComment(offset, "changed", out error), error);

            Assert.True(controller.Undo(out error), error);
            Assert.Equal("keep me", controller.GetLongCommentAt(offset));

            Assert.True(controller.Undo(out error), error);
            Assert.Equal(string.Empty, controller.GetLongCommentAt(offset));
        }

        [Fact]
        public void LongComment_RequiresOpenProject() {
            using AppController controller = new AppController();
            Assert.False(controller.SetLongComment(0, "text", out string error));
            Assert.Contains("No project", error);
            Assert.Equal(string.Empty, controller.GetLongCommentAt(0));
        }

        // ---- search ----

        [Fact]
        public void FindNext_LocatesMnemonics() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            // Every 6502 listing has LDA instructions.
            int match = controller.FindNext("lda", -1, matchCase: false, out bool wrapped);
            Assert.True(match >= 0, "expected to find an LDA instruction");
            Assert.False(wrapped, "first match should be found before wrapping");
        }

        [Fact]
        public void FindNext_WrapsAroundTheEnd() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            // Start from the last line: the next match is necessarily above it.
            int match = controller.FindNext("lda", controller.LineCount - 1, matchCase: false,
                out bool wrapped);
            Assert.True(match >= 0);
            Assert.True(wrapped, "search should have wrapped to the top");
        }

        [Fact]
        public void FindNext_RespectsCaseSensitivity() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            // The listing renders mnemonics in one case; the other case must not match.
            int insensitive = controller.FindNext("LDA", -1, matchCase: false, out _);
            Assert.True(insensitive >= 0);

            int sensitive = controller.FindNext("LDA", -1, matchCase: true, out _);
            Assert.Equal(-1, sensitive);
        }

        [Fact]
        public void FindNext_ReportsNoMatch() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int match = controller.FindNext("ZZZNotPresentZZZ", -1, matchCase: false, out _);
            Assert.Equal(-1, match);
        }

        [Fact]
        public void FindNext_RequiresOpenProject() {
            using AppController controller = new AppController();
            Assert.Equal(-1, controller.FindNext("lda", 0, false, out _));
        }

        // ---- terminal UI wrappers ----

        [Fact]
        public void TuiSearch_SelectsMatchAndRepeats() {
            using AppController controller = new AppController();
            TuiModel model = new TuiModel(controller);
            Assert.True(model.OpenProject(ExamplePath, out string error), error);

            Assert.True(model.FindNext("lda", matchCase: false, out error), error);
            Assert.True(model.SelectedLine > 0, "selection should have moved to the match");
            int firstMatch = model.SelectedLine;

            // Find Again continues from the current selection, so it should move on.
            Assert.True(model.FindAgain(out string repeatError),
                "a second LDA should still be found: " + repeatError);
            Assert.NotEqual(firstMatch, model.SelectedLine);
        }

        [Fact]
        public void TuiSearch_ReportsMissingText() {
            using AppController controller = new AppController();
            TuiModel model = new TuiModel(controller);
            Assert.True(model.OpenProject(ExamplePath, out string error), error);

            Assert.False(model.FindNext("ZZZNotPresentZZZ", matchCase: false, out error));
            Assert.Contains("Not found", error);
        }

        [Fact]
        public void TuiLongComment_EditsSelection() {
            using AppController controller = new AppController();
            TuiModel model = new TuiModel(controller);
            Assert.True(model.OpenProject(ExamplePath, out string error), error);

            model.SetSelectedLine(FindLineWithoutLongComment(controller));
            Assert.Equal(string.Empty, model.GetSelectedLongComment());

            Assert.True(model.SetSelectedLongComment("tui comment", out error), error);
            Assert.Equal("tui comment", model.GetSelectedLongComment());

            Assert.True(model.SetSelectedLongComment(string.Empty, out error), error);
            Assert.Equal(string.Empty, model.GetSelectedLongComment());
        }
    }
}
