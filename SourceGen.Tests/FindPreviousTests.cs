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
    /// Tests for backward search (Find Previous), which mirrors the forward search covered in
    /// <see cref="SearchAndLongCommentTests"/>.
    /// </summary>
    public class FindPreviousTests {
        private static string ExamplePath {
            get {
                return Path.Combine(AppContext.BaseDirectory, "Examples", "A2-Zippy",
                    "ZIPPY#ff2000" + ProjectFile.FILENAME_EXT);
            }
        }

        [Fact]
        public void FindPrevious_FindsEarlierMatch() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int match = controller.FindPrevious("lda", controller.LineCount - 1, matchCase: false,
                out bool wrapped);

            Assert.True(match >= 0, "expected to find an LDA instruction");
            Assert.False(wrapped, "starting at the bottom, the match is found before wrapping");
            Assert.True(match < controller.LineCount - 1);
        }

        [Fact]
        public void FindPrevious_WrapsToTheBottom() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            // Nothing precedes line 0, so the only way to match is to wrap.
            int match = controller.FindPrevious("lda", 0, matchCase: false, out bool wrapped);

            Assert.True(match >= 0);
            Assert.True(wrapped, "search should have wrapped to the bottom");
            Assert.True(match > 0);
        }

        [Fact]
        public void FindPrevious_RespectsCaseSensitivity() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int insensitive = controller.FindPrevious("LDA", controller.LineCount - 1,
                matchCase: false, out _);
            Assert.True(insensitive >= 0);

            int sensitive = controller.FindPrevious("LDA", controller.LineCount - 1,
                matchCase: true, out _);
            Assert.Equal(-1, sensitive);
        }

        [Fact]
        public void FindPrevious_ReportsNoMatch() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int match = controller.FindPrevious("ZZZNotPresentZZZ", controller.LineCount - 1,
                matchCase: false, out _);
            Assert.Equal(-1, match);
        }

        [Fact]
        public void FindPrevious_RequiresOpenProject() {
            using AppController controller = new AppController();
            Assert.Equal(-1, controller.FindPrevious("lda", 0, false, out _));
        }

        [Fact]
        public void ForwardAndBackward_VisitDifferentMatches() {
            using AppController controller = new AppController();
            Assert.True(controller.OpenProject(ExamplePath, out string error), error);

            int forward = controller.FindNext("lda", -1, matchCase: false, out _);
            Assert.True(forward >= 0);

            // Searching backwards from that match must land on a different line.
            int backward = controller.FindPrevious("lda", forward, matchCase: false,
                out bool wrapped);
            Assert.True(backward >= 0);
            Assert.NotEqual(forward, backward);
            // Either an earlier match, or wrapped past the end.
            Assert.True(backward < forward || wrapped);
        }

        // ---- terminal UI wrappers ----

        [Fact]
        public void TuiFindPrevious_SetsDirectionAndRepeats() {
            using AppController controller = new AppController();
            TuiModel model = new TuiModel(controller);
            Assert.True(model.OpenProject(ExamplePath, out string error), error);

            model.MoveToEnd(true);
            Assert.True(model.FindPrevious("lda", matchCase: false, out error), error);
            Assert.False(model.LastSearchForward, "direction should now be backward");
            Assert.True(model.SelectedLine >= 0);

            // Find Again must repeat in the same (backward) direction.
            Assert.True(model.FindAgain(out string repeatError),
                "a further LDA should be found: " + repeatError);
            Assert.False(model.LastSearchForward);
        }

        [Fact]
        public void TuiFindPrevious_ReportsMissingText() {
            using AppController controller = new AppController();
            TuiModel model = new TuiModel(controller);
            Assert.True(model.OpenProject(ExamplePath, out string error), error);

            Assert.False(model.FindPrevious("ZZZNotPresentZZZ", matchCase: false, out error));
            Assert.Contains("Not found", error);
        }
    }
}
