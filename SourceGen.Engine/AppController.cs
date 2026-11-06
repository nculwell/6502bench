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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

using Asm65;
using CommonUtil;
using SourceGen.AsmGen;

namespace SourceGen {
    /// <summary>
    /// Row in the symbol list pane.
    /// </summary>
    public class SymbolRow {
        public string Type { get; }
        public string Value { get; }
        public string Name { get; }

        public SymbolRow(string type, string value, string name) {
            Type = type;
            Value = value;
            Name = name;
        }
    }

    /// <summary>
    /// Row in the notes list pane.
    /// </summary>
    public class NoteRow {
        public string Offset { get; }
        public string Note { get; }

        public NoteRow(string offset, string note) {
            Offset = offset;
            Note = note;
        }
    }

    /// <summary>
    /// Drives the disassembly workspace: loads a project, runs analysis, and exposes the
    /// results for binding.
    ///
    /// This replaces the project-management half of the WPF MainController.  Only the
    /// display pipeline is implemented so far -- opening a project, analysing it, and
    /// populating the code/symbol/message/note panes.  Editing operations are still handled
    /// by the not-yet-ported code.
    /// </summary>
    public class AppController : INotifyPropertyChanged, IDisposable {
        /// <summary>
        /// Code list contents.  Replaced when a project is loaded.
        /// </summary>
        public DisplayList CodeList {
            get { return mCodeList; }
            private set {
                mCodeList = value;
                OnPropertyChanged();
            }
        }
        private DisplayList mCodeList = new DisplayList();

        public ObservableCollection<MessageListItem> Messages { get; } =
            new ObservableCollection<MessageListItem>();

        public ObservableCollection<SymbolRow> Symbols { get; } =
            new ObservableCollection<SymbolRow>();

        public ObservableCollection<NoteRow> Notes { get; } =
            new ObservableCollection<NoteRow>();

        /// <summary>
        /// Status bar message.
        /// </summary>
        public string StatusText {
            get { return mStatusText; }
            private set { mStatusText = value; OnPropertyChanged(); }
        }
        private string mStatusText = "Ready";

        /// <summary>
        /// True when a project is open, i.e. the code list should be shown instead of the
        /// launch panel.
        /// </summary>
        public bool HasProject {
            get { return mProject != null; }
        }

        /// <summary>
        /// Message count summary for the status bar button.
        /// </summary>
        public string MessageSummary {
            get { return mMessageSummary; }
            private set { mMessageSummary = value; OnPropertyChanged(); }
        }
        private string mMessageSummary = "0 messages";

        private DisasmProject mProject;
        // The display formatter needs a defined expression mode: the default (Unknown) trips
        // a Debug.Fail as soon as an operand expression has to be rendered.  Common is the
        // neutral, assembler-independent choice for on-screen display.
        private Formatter mFormatter = CreateFormatter();

        private static Formatter CreateFormatter() {
            Formatter.FormatConfig config = new Formatter.FormatConfig();
            config.ExprMode = Formatter.FormatConfig.ExpressionMode.Common;
            return new Formatter(config);
        }
        private readonly PseudoOp.PseudoOpNames mPseudoOpNames = PseudoOp.DefaultPseudoOpNames;
        private LineListGen mLineListGen;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Loads a .dis65 project and its data file, analyses it, and refreshes the panes.
        /// </summary>
        /// <param name="projectPath">Full path to the .dis65 file.</param>
        /// <param name="error">On failure, a message describing the problem.</param>
        /// <returns>True on success.</returns>
        public bool OpenProject(string projectPath, out string error) {
            error = null;

            // Release any previously loaded project first: DisasmProject asserts in its
            // finalizer that Cleanup() was called, so projects must not be abandoned.
            CloseProject();

            // The binary lives alongside the project, named without the extension.
            string dataPath = projectPath.Substring(0,
                projectPath.Length - ProjectFile.FILENAME_EXT.Length);
            if (!File.Exists(dataPath)) {
                error = "Data file not found: " + dataPath;
                return false;
            }

            DisasmProject proj = new DisasmProject();
            FileLoadReport report = new FileLoadReport(projectPath);
            if (!ProjectFile.DeserializeFromFile(projectPath, proj, out report)) {
                error = report.Format();
                return false;
            }

            byte[] fileData;
            try {
                fileData = File.ReadAllBytes(dataPath);
            } catch (Exception ex) {
                error = "Unable to read " + dataPath + ": " + ex.Message;
                return false;
            }

            FileLoadReport dataReport = new FileLoadReport(dataPath);
            proj.SetFileData(fileData, Path.GetFileName(dataPath), ref dataReport);
            proj.ProjectPathName = projectPath;

            // Load any platform symbol files / extension scripts the project references.
            string extMessages = proj.LoadExternalFiles();

            Analyze(proj);

            mProject = proj;
            BuildDisplayPipelines(proj);

            StatusText = Path.GetFileName(dataPath) + "  (" + fileData.Length +
                " bytes, " + mCodeList.Count + " lines)";
            if (!string.IsNullOrEmpty(extMessages)) {
                MessageSummary = extMessages.Length + " external-file messages";
            }
            return true;
        }

        /// <summary>
        /// Runs the analyzer and constructs the code list, symbols and notes.
        /// </summary>
        private void Analyze(DisasmProject proj) {
            DebugLog log = new DebugLog();
            log.SetMinPriority(DebugLog.Priority.Silent);
            TaskTimer timer = new TaskTimer();

            proj.Analyze(UndoableChange.ReanalysisScope.CodeAndData, log, timer);
        }

        private void BuildDisplayPipelines(DisasmProject proj) {
            DisplayList list = new DisplayList();
            mLineListGen = new LineListGen(proj, list, mFormatter, mPseudoOpNames);
            mLineListGen.GenerateAll();
            CodeList = list;

            PopulateMessages(proj);
            PopulateSymbols(proj);
            PopulateNotes(proj);
        }

        private void PopulateMessages(DisasmProject proj) {
            Messages.Clear();
            int warnErrorCount = 0;
            foreach (MessageList.MessageEntry entry in proj.Messages) {
                Messages.Add(MessageList.FormatMessage(entry, mFormatter));
                if (entry.Severity != MessageList.MessageEntry.SeverityLevel.Info) {
                    warnErrorCount++;
                }
            }

            if (proj.Messages.Count == 0) {
                MessageSummary = "0 messages";
            } else if (warnErrorCount == 0) {
                MessageSummary = proj.Messages.Count + " messages";
            } else {
                MessageSummary = proj.Messages.Count + " messages (" + warnErrorCount +
                    " warning/error)";
            }
        }

        private void PopulateSymbols(DisasmProject proj) {
            Symbols.Clear();
            foreach (Symbol sym in proj.SymbolTable) {
                string type;
                switch (sym.SymbolType) {
                    case Symbol.Type.Constant:
                        type = "Cns";
                        break;
                    case Symbol.Type.LocalOrGlobalAddr:
                    case Symbol.Type.GlobalAddr:
                    case Symbol.Type.GlobalAddrExport:
                    case Symbol.Type.ExternalAddr:
                        type = "Addr";
                        break;
                    case Symbol.Type.NonUniqueLocalAddr:
                        type = "Loc";
                        break;
                    default:
                        type = "?";
                        break;
                }
                Symbols.Add(new SymbolRow(type, mFormatter.FormatAddress(sym.Value, false),
                    sym.Label));
            }
        }

        private void PopulateNotes(DisasmProject proj) {
            Notes.Clear();
            List<int> offsets = new List<int>(proj.Notes.Keys);
            offsets.Sort();
            foreach (int offset in offsets) {
                MultiLineComment mlc = proj.Notes[offset];
                Notes.Add(new NoteRow(mFormatter.FormatOffset24(offset), mlc.Text));
            }
        }

        /// <summary>
        /// Releases the loaded project.  Call this (or Dispose) when finished with the
        /// controller, otherwise the project's finalizer will complain.
        /// </summary>
        public void Dispose() {
            CloseProject();
        }

        /// <summary>
        /// Generates assembly source for the open project.
        ///
        /// This is the engine half of the "Generate Assembly" command: it writes the
        /// assembler-specific sources (and any required linker config) into a directory.
        /// Running the cross-assembler to verify the result is a separate concern and is not
        /// done here.
        /// </summary>
        /// <param name="asmId">Target assembler.</param>
        /// <param name="outputDir">Directory to write into; must exist.</param>
        /// <param name="generatedFiles">On success, the files that were written.</param>
        /// <param name="error">On failure, a message describing the problem.</param>
        /// <returns>True on success.</returns>
        public bool GenerateAssembly(AssemblerInfo.Id asmId, string outputDir,
                out List<string> generatedFiles, out string error) {
            generatedFiles = null;
            error = null;

            if (mProject == null) {
                error = "No project is open.";
                return false;
            }
            if (asmId == AssemblerInfo.Id.Unknown) {
                error = "No assembler selected.";
                return false;
            }
            if (!Directory.Exists(outputDir)) {
                error = "Output directory does not exist: " + outputDir;
                return false;
            }

            IGenerator gen = AssemblerInfo.GetGenerator(asmId);
            if (gen == null) {
                error = "No source generator is available for " + asmId + ".";
                return false;
            }

            string fileNameBase = Path.GetFileNameWithoutExtension(mProject.DataFileName);
            try {
                // A null version targets the newest assembler output the generator knows
                // about, which is what a user selecting "just generate the source" wants.
                gen.Configure(mProject, outputDir, fileNameBase, null, AppSettings.Global);

                BackgroundWorker worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                GenerationResults results = gen.GenerateSource(worker);

                generatedFiles = new List<string>(results.PathNames);
                StatusText = "Generated " + generatedFiles.Count + " file(s) for " + asmId +
                    " in " + outputDir;
                return true;
            } catch (Exception ex) {
                error = "Generation failed: " + ex.Message;
                return false;
            }
        }

        // ---- editing ----

        /// <summary>
        /// Number of lines currently in the code list.
        /// </summary>
        public int LineCount {
            get { return mLineListGen == null ? 0 : mLineListGen.Count; }
        }

        /// <summary>
        /// Maps a code list line index to the file offset it represents, or -1.
        /// </summary>
        public int GetFileOffset(int lineIndex) {
            if (mLineListGen == null || lineIndex < 0 || lineIndex >= mLineListGen.Count) {
                return -1;
            }
            return mLineListGen[lineIndex].FileOffset;
        }

        /// <summary>
        /// Current label in effect at the offset (user, auto or none), for pre-filling an
        /// edit dialog.  Empty string when the offset has no label.
        /// </summary>
        public string GetLabelAt(int offset) {
            if (mProject == null || offset < 0 || offset >= mProject.FileDataLength) {
                return string.Empty;
            }
            Symbol sym = mProject.GetAnattrib(offset).Symbol;
            return sym == null ? string.Empty : sym.Label;
        }

        /// <summary>
        /// True if the label at the offset was set by the user (and can therefore be edited
        /// or deleted).  Auto-generated labels cannot be renamed directly.
        /// </summary>
        public bool HasUserLabelAt(int offset) {
            return mProject != null && mProject.UserLabels.ContainsKey(offset);
        }

        /// <summary>
        /// Address of the offset, for display in the label editor.
        /// </summary>
        public int GetAddressAt(int offset) {
            if (mProject == null || offset < 0 || offset >= mProject.FileDataLength) {
                return Address.NON_ADDR;
            }
            Anattrib attr = mProject.GetAnattrib(offset);
            return attr.IsNonAddressable ? Address.NON_ADDR : attr.Address;
        }

        /// <summary>
        /// Sets or clears the user label at a file offset.
        ///
        /// This mirrors the WPF editor: the change is recorded as an undoable
        /// UserLabels edit and the project is re-analysed afterwards.
        /// </summary>
        /// <param name="offset">File offset of the label.</param>
        /// <param name="label">New label, or empty/null to remove the user label.</param>
        /// <param name="error">On failure, a message describing the problem.</param>
        public bool SetLabel(int offset, string label, out string error) {
            error = null;
            if (mProject == null) {
                error = "No project is open.";
                return false;
            }
            if (offset < 0 || offset >= mProject.FileDataLength) {
                error = "Offset is outside the file.";
                return false;
            }

            mProject.UserLabels.TryGetValue(offset, out Symbol oldSymbol);

            Symbol newSymbol = null;
            if (!string.IsNullOrEmpty(label)) {
                if (!Asm65.Label.ValidateLabel(label)) {
                    error = "'" + label + "' is not a valid label.";
                    return false;
                }
                Anattrib attr = mProject.GetAnattrib(offset);
                Symbol.Type type = attr.IsNonAddressable ?
                    Symbol.Type.Constant : Symbol.Type.GlobalAddr;
                newSymbol = new Symbol(label, attr.Address, Symbol.Source.User, type,
                    Symbol.LabelAnnotation.None);
            }

            if (oldSymbol == newSymbol) {
                return true;        // nothing to do
            }

            UndoableChange change =
                UndoableChange.CreateLabelChange(offset, oldSymbol, newSymbol);
            return ApplyChangeSet(new ChangeSet(change), "label", out error);
        }

        /// <summary>
        /// Comment text at an offset, or empty string if none.
        /// </summary>
        public string GetCommentAt(int offset) {
            if (mProject == null || offset < 0 || offset >= mProject.FileDataLength) {
                return string.Empty;
            }
            return mProject.Comments[offset] ?? string.Empty;
        }

        /// <summary>
        /// Sets or clears the end-of-line comment at a file offset.
        /// </summary>
        /// <param name="offset">File offset of the comment.</param>
        /// <param name="comment">New comment text; empty removes it.</param>
        /// <param name="error">On failure, a message describing the problem.</param>
        public bool SetComment(int offset, string comment, out string error) {
            error = null;
            if (mProject == null) {
                error = "No project is open.";
                return false;
            }
            if (offset < 0 || offset >= mProject.FileDataLength) {
                error = "Offset is outside the file.";
                return false;
            }

            string oldComment = mProject.Comments[offset] ?? string.Empty;
            string newComment = comment ?? string.Empty;
            if (oldComment == newComment) {
                return true;
            }

            UndoableChange change = UndoableChange.CreateCommentChange(offset, oldComment,
                newComment);
            return ApplyChangeSet(new ChangeSet(change), "comment", out error);
        }

        /// <summary>
        /// Applies a change set, pushes it onto the undo stack, and refreshes the display.
        /// </summary>
        private bool ApplyChangeSet(ChangeSet changeSet, string what, out string error) {
            error = null;
            try {
                mProject.ApplyChanges(changeSet, false, out RangeSet affectedOffsets);
                mProject.PushChangeSet(changeSet);
            } catch (Exception ex) {
                error = "Unable to apply " + what + " change: " + ex.Message;
                return false;
            }

            // Re-analyse and rebuild: the change can affect labels referenced elsewhere,
            // which in turn changes formatting further down the listing.
            Refresh();
            return true;
        }

        public bool CanUndo {
            get { return mProject != null && mProject.CanUndo; }
        }

        public bool CanRedo {
            get { return mProject != null && mProject.CanRedo; }
        }

        /// <summary>
        /// Undoes the most recent change.
        /// </summary>
        public bool Undo(out string error) {
            error = null;
            if (mProject == null || !mProject.CanUndo) {
                error = "Nothing to undo.";
                return false;
            }
            ChangeSet changeSet = mProject.PopUndoSet();
            mProject.ApplyChanges(changeSet, true, out RangeSet affectedOffsets);
            Refresh();
            return true;
        }

        /// <summary>
        /// Redoes the most recently undone change.
        /// </summary>
        public bool Redo(out string error) {
            error = null;
            if (mProject == null || !mProject.CanRedo) {
                error = "Nothing to redo.";
                return false;
            }
            ChangeSet changeSet = mProject.PopRedoSet();
            mProject.ApplyChanges(changeSet, false, out RangeSet affectedOffsets);
            Refresh();
            return true;
        }

        /// <summary>
        /// Multi-line comment text at an offset, or empty string if none.
        /// </summary>
        public string GetLongCommentAt(int offset) {
            if (mProject == null) {
                return string.Empty;
            }
            if (mProject.LongComments.TryGetValue(offset, out MultiLineComment mlc)) {
                return mlc.Text ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// Sets or clears the multi-line comment at an offset.
        /// </summary>
        /// <param name="offset">File offset the comment is attached to.</param>
        /// <param name="text">New comment text; empty removes the comment.</param>
        /// <param name="error">On failure, a message describing the problem.</param>
        public bool SetLongComment(int offset, string text, out string error) {
            error = null;
            if (mProject == null) {
                error = "No project is open.";
                return false;
            }
            if (offset < 0 || offset > mProject.FileDataLength) {
                error = "Offset is outside the file.";
                return false;
            }

            mProject.LongComments.TryGetValue(offset, out MultiLineComment oldComment);

            MultiLineComment newComment = null;
            if (!string.IsNullOrEmpty(text)) {
                newComment = new MultiLineComment(text);
            }

            if (oldComment == newComment) {
                return true;
            }

            UndoableChange change =
                UndoableChange.CreateLongCommentChange(offset, oldComment, newComment);
            return ApplyChangeSet(new ChangeSet(change), "long comment", out error);
        }

        /// <summary>
        /// Searches the listing for a substring, starting after the given line.
        /// </summary>
        /// <param name="needle">Text to look for.</param>
        /// <param name="startLine">Line to start after.</param>
        /// <param name="matchCase">If set, the comparison is case-sensitive.</param>
        /// <param name="wrapped">Set if the search restarted at the top.</param>
        /// <returns>Line index of the match, or -1.</returns>
        public int FindNext(string needle, int startLine, bool matchCase, out bool wrapped) {
            wrapped = false;
            if (mProject == null || mLineListGen == null || string.IsNullOrEmpty(needle)) {
                return -1;
            }

            int count = mLineListGen.Count;
            if (count == 0) {
                return -1;
            }

            StringComparison comparison = matchCase ? StringComparison.Ordinal :
                StringComparison.OrdinalIgnoreCase;

            // Search to the end, then wrap around to the beginning.
            for (int i = Math.Max(0, startLine + 1); i < count; i++) {
                if (mLineListGen.GetSearchString(i).IndexOf(needle, comparison) >= 0) {
                    return i;
                }
            }
            for (int i = 0; i <= Math.Min(startLine, count - 1); i++) {
                if (mLineListGen.GetSearchString(i).IndexOf(needle, comparison) >= 0) {
                    wrapped = true;
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Writes the project back to its .dis65 file.
        /// </summary>
        public bool SaveProject(out string error) {
            error = null;
            if (mProject == null || string.IsNullOrEmpty(mProject.ProjectPathName)) {
                error = "No project is open.";
                return false;
            }

            try {
                string text = SerializableProjectFile1.SerializeProject(mProject);
                File.WriteAllText(mProject.ProjectPathName, text);
                mProject.ResetDirtyFlag();
                StatusText = "Saved " + Path.GetFileName(mProject.ProjectPathName);
                OnPropertyChanged(nameof(HasProject));
                return true;
            } catch (Exception ex) {
                error = "Unable to save project: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// True when there are unsaved edits.
        /// </summary>
        public bool IsDirty {
            get { return mProject != null && mProject.IsDirty; }
        }

        /// <summary>
        /// Discards the current project.
        /// </summary>
        public void CloseProject() {
            if (mProject == null) {
                return;
            }
            mProject.Cleanup();
            mProject = null;
            mLineListGen = null;

            CodeList = new DisplayList();
            Messages.Clear();
            Symbols.Clear();
            Notes.Clear();
            StatusText = "Ready";
            MessageSummary = "0 messages";
            OnPropertyChanged(nameof(HasProject));
        }

        /// <summary>
        /// Re-runs analysis and rebuilds the display.  Used after edits.
        /// </summary>
        public void Refresh() {
            if (mProject == null) {
                return;
            }
            Analyze(mProject);
            BuildDisplayPipelines(mProject);
            OnPropertyChanged(nameof(HasProject));
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
