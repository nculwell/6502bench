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
using System.Text;

namespace SourceGen.Tui {
    /// <summary>
    /// Presentation logic for the terminal UI.
    ///
    /// This holds everything the screen shows and everything a keypress does, with no
    /// dependency on Terminal.Gui or on a real terminal.  Keeping it separate means the TUI
    /// can be tested headlessly: the tests drive this class directly and assert on the text
    /// it produces, exactly as the view would render it.
    /// </summary>
    public class TuiModel {
        /// <summary>
        /// Workspace state: the open project, code list, symbols, messages.
        /// </summary>
        public AppController Controller { get; }

        /// <summary>
        /// Index of the selected line in the code list.
        /// </summary>
        public int SelectedLine { get; private set; } = -1;

        /// <summary>
        /// True when a project is open.
        /// </summary>
        public bool HasProject {
            get { return Controller.HasProject; }
        }

        public TuiModel(AppController controller) {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        /// <summary>
        /// Opens a project, resetting the selection to the first line.
        /// </summary>
        public bool OpenProject(string projectPath, out string error) {
            bool ok = Controller.OpenProject(projectPath, out error);
            SelectedLine = ok && Controller.LineCount > 0 ? 0 : -1;
            return ok;
        }

        public void CloseProject() {
            Controller.CloseProject();
            SelectedLine = -1;
        }

        /// <summary>
        /// Moves the selection by the given number of lines, clamping at both ends.
        /// </summary>
        public void MoveSelection(int delta) {
            if (!HasProject || Controller.LineCount == 0) {
                SelectedLine = -1;
                return;
            }
            int target = SelectedLine + delta;
            if (target < 0) {
                target = 0;
            }
            if (target >= Controller.LineCount) {
                target = Controller.LineCount - 1;
            }
            SelectedLine = target;
        }

        /// <summary>
        /// Sets the selected line directly, clamping to the listing.
        /// </summary>
        public void SetSelectedLine(int line) {
            if (!HasProject || Controller.LineCount == 0) {
                SelectedLine = -1;
                return;
            }
            if (line < 0) {
                line = 0;
            }
            if (line >= Controller.LineCount) {
                line = Controller.LineCount - 1;
            }
            SelectedLine = line;
        }

        /// <summary>
        /// Scrolls to the top or bottom of the listing.
        /// </summary>
        public void MoveToEnd(bool end) {
            if (!HasProject || Controller.LineCount == 0) {
                SelectedLine = -1;
                return;
            }
            SelectedLine = end ? Controller.LineCount - 1 : 0;
        }

        /// <summary>
        /// File offset of the selected line, or -1.
        /// </summary>
        public int SelectedOffset {
            get { return SelectedLine < 0 ? -1 : Controller.GetFileOffset(SelectedLine); }
        }

        /// <summary>
        /// Renders the code list columns for one line, padded and truncated to width.
        /// </summary>
        object Column(string text, int width) {
            text = text ?? string.Empty;
            if (text.Length > width) {
                return text.Substring(0, width);
            }
            return text.PadRight(width);
        }

        /// <summary>
        /// Builds the visible code list lines.
        /// </summary>
        /// <param name="width">Available width in columns.</param>
        /// <param name="height">Number of rows to produce.</param>
        /// <param name="firstLine">
        /// Index of the first line to show; adjusted so the selection stays visible.
        /// </param>
        /// <returns>One string per row.</returns>
        public List<string> GetCodeLines(int width, int height, ref int firstLine) {
            List<string> rows = new List<string>(height);
            if (!HasProject || Controller.LineCount == 0) {
                return rows;
            }

            // Keep the selection on screen.
            if (firstLine < 0) {
                firstLine = 0;
            }
            if (height > 0 && SelectedLine >= 0) {
                if (SelectedLine < firstLine) {
                    firstLine = SelectedLine;
                } else if (SelectedLine >= firstLine + height) {
                    firstLine = SelectedLine - height + 1;
                }
            }
            int maxFirst = Math.Max(0, Controller.LineCount - height);
            if (firstLine > maxFirst) {
                firstLine = maxFirst;
            }

            for (int i = firstLine; i < firstLine + height && i < Controller.LineCount; i++) {
                DisplayList.FormattedParts parts = Controller.CodeList[i];
                StringBuilder sb = new StringBuilder(width + 1);
                sb.Append(Column(parts.Offset, 7));
                sb.Append(Column(parts.Addr, 6));
                sb.Append(Column(parts.Label, 20));
                sb.Append(Column(parts.Opcode, 6));
                sb.Append(Column(parts.Operand, 22));
                sb.Append(Column(parts.Comment, Math.Max(0, width - 61)));
                string line = sb.ToString();
                rows.Add(line.Length > width ? line.Substring(0, width) : line);
            }
            return rows;
        }

        /// <summary>
        /// Builds the symbol pane contents, one "TYPE VALUE NAME" line each.
        /// </summary>
        public List<string> GetSymbolLines(int maxRows) {
            List<string> rows = new List<string>(maxRows);
            if (!HasProject) {
                return rows;
            }
            int count = 0;
            foreach (SymbolRow sym in Controller.Symbols) {
                if (count++ >= maxRows) {
                    break;
                }
                rows.Add((sym.Type + " " + sym.Value + " " + sym.Name));
            }
            return rows;
        }

        /// <summary>
        /// Builds the message pane contents, one line per problem.
        /// </summary>
        public List<string> GetMessageLines(int maxRows) {
            List<string> rows = new List<string>(maxRows);
            if (!HasProject) {
                return rows;
            }
            int count = 0;
            foreach (MessageListItem msg in Controller.Messages) {
                if (count++ >= maxRows) {
                    break;
                }
                rows.Add(msg.Severity + " +" + msg.Offset + " " + msg.Type);
            }
            return rows;
        }

        /// <summary>
        /// Heading for the code pane: file name and position.
        /// </summary>
        public string GetCodeTitle() {
            if (!HasProject) {
                return "No project";
            }
            string name = System.IO.Path.GetFileName(Controller.StatusText);
            return "Code  " + (SelectedLine + 1) + "/" + Controller.LineCount;
        }

        /// <summary>
        /// One-line status summary.
        /// </summary>
        public string GetStatusLine() {
            if (!HasProject) {
                return "No project open -- File > Open to begin";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(Controller.StatusText);
            sb.Append("  |  ").Append(Controller.MessageSummary);
            if (Controller.IsDirty) {
                sb.Append("  |  [modified]");
            }
            if (Controller.CanUndo) {
                sb.Append("  |  undo available");
            }
            return sb.ToString();
        }

        // ---- editing commands ----

        /// <summary>
        /// Label currently in effect on the selected line.
        /// </summary>
        public string GetSelectedLabel() {
            int offset = SelectedOffset;
            return offset < 0 ? string.Empty : Controller.GetLabelAt(offset);
        }

        /// <summary>
        /// Sets the label on the selected line.
        /// </summary>
        public bool SetSelectedLabel(string label, out string error) {
            int offset = SelectedOffset;
            if (offset < 0) {
                error = "No line is selected.";
                return false;
            }
            bool ok = Controller.SetLabel(offset, label, out error);
            if (ok) {
                RefreshSelection();
            }
            return ok;
        }

        /// <summary>
        /// Comment currently on the selected line.
        /// </summary>
        public string GetSelectedComment() {
            int offset = SelectedOffset;
            return offset < 0 ? string.Empty : Controller.GetCommentAt(offset);
        }

        /// <summary>
        /// Sets the comment on the selected line.
        /// </summary>
        public bool SetSelectedComment(string comment, out string error) {
            int offset = SelectedOffset;
            if (offset < 0) {
                error = "No line is selected.";
                return false;
            }
            bool ok = Controller.SetComment(offset, comment, out error);
            if (ok) {
                RefreshSelection();
            }
            return ok;
        }

        /// <summary>
        /// Re-clamps the selection after the listing was rebuilt by an edit.
        /// </summary>
        private void RefreshSelection() {
            if (!HasProject || Controller.LineCount == 0) {
                SelectedLine = -1;
                return;
            }
            if (SelectedLine >= Controller.LineCount) {
                SelectedLine = Controller.LineCount - 1;
            }
        }

        /// <summary>
        /// Multi-line comment attached to the selected line.
        /// </summary>
        public string GetSelectedLongComment() {
            int offset = SelectedOffset;
            return offset < 0 ? string.Empty : Controller.GetLongCommentAt(offset);
        }

        /// <summary>
        /// Sets or clears the multi-line comment on the selected line.
        /// </summary>
        public bool SetSelectedLongComment(string text, out string error) {
            int offset = SelectedOffset;
            if (offset < 0) {
                error = "No line is selected.";
                return false;
            }
            bool ok = Controller.SetLongComment(offset, text, out error);
            if (ok) {
                RefreshSelection();
            }
            return ok;
        }

        // ---- notes ----

        /// <summary>
        /// Note attached to the selected line.
        /// </summary>
        public string GetSelectedNote() {
            int offset = SelectedOffset;
            return offset < 0 ? string.Empty : Controller.GetNoteAt(offset);
        }

        /// <summary>
        /// Sets or clears the note on the selected line.
        /// </summary>
        public bool SetSelectedNote(string text, out string error) {
            int offset = SelectedOffset;
            if (offset < 0) {
                error = "No line is selected.";
                return false;
            }
            bool ok = Controller.SetNote(offset, text, out error);
            if (ok) {
                RefreshSelection();
            }
            return ok;
        }

        // ---- output ----

        /// <summary>
        /// Writes an HTML listing.
        /// </summary>
        public bool ExportHtml(string pathName, out string error) {
            return Controller.ExportHtml(pathName, true, out error);
        }

        /// <summary>
        /// Writes a VICE label file.
        /// </summary>
        public bool GenerateLabels(string pathName, bool includeAutoLabels,
                out string error) {
            return Controller.GenerateLabelFile(pathName, includeAutoLabels, out error);
        }

        // ---- search ----

        /// <summary>
        /// Last text searched for; empty until a search has run.
        /// </summary>
        public string LastSearchText { get; private set; } = string.Empty;

        /// <summary>
        /// Whether the last search was case-sensitive.
        /// </summary>
        public bool LastSearchMatchCase { get; private set; }

        /// <summary>
        /// Direction of the last search (true = forward).  Find Again repeats it.
        /// </summary>
        public bool LastSearchForward { get; private set; } = true;

        /// <summary>
        /// Searches forward from the current selection.
        /// </summary>
        /// <param name="needle">Text to find.</param>
        /// <param name="matchCase">Case-sensitive comparison if set.</param>
        /// <param name="error">Set when nothing matched.</param>
        /// <returns>True if a match was found and selected.</returns>
        public bool FindNext(string needle, bool matchCase, out string error) {
            error = null;
            LastSearchText = needle ?? string.Empty;
            LastSearchMatchCase = matchCase;
            LastSearchForward = true;
            return RunSearch(out error);
        }

        /// <summary>
        /// Searches backwards from the current selection.
        /// </summary>
        public bool FindPrevious(string needle, bool matchCase, out string error) {
            error = null;
            LastSearchText = needle ?? string.Empty;
            LastSearchMatchCase = matchCase;
            LastSearchForward = false;
            return RunSearch(out error);
        }

        /// <summary>
        /// Repeats the previous search.
        /// </summary>
        public bool FindAgain(out string error) {
            error = null;
            if (string.IsNullOrEmpty(LastSearchText)) {
                error = "No search has been performed yet.";
                return false;
            }
            return RunSearch(out error);
        }

        private bool RunSearch(out string error) {
            error = null;
            if (!HasProject) {
                error = "No project is open.";
                return false;
            }

            int match = LastSearchForward
                ? Controller.FindNext(LastSearchText, SelectedLine, LastSearchMatchCase,
                    out bool wrapped)
                : Controller.FindPrevious(LastSearchText, SelectedLine, LastSearchMatchCase,
                    out wrapped);
            if (match < 0) {
                error = "Not found: " + LastSearchText;
                return false;
            }

            SelectedLine = match;
            if (wrapped) {
                error = null;       // wrapped around; still a successful find
            }
            return true;
        }

        public bool Undo(out string error) {
            return Controller.Undo(out error);
        }

        public bool Redo(out string error) {
            return Controller.Redo(out error);
        }

        public bool Save(out string error) {
            return Controller.SaveProject(out error);
        }

        /// <summary>
        /// Generates assembly source into a directory.
        /// </summary>
        public bool GenerateAssembly(AsmGen.AssemblerInfo.Id asmId, string outputDir,
                out List<string> files, out string error) {
            return Controller.GenerateAssembly(asmId, outputDir, out files, out error);
        }
    }
}
