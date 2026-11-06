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
using System.Linq;

using NStack;
using Terminal.Gui;

namespace SourceGen.Tui {
    /// <summary>
    /// Terminal user interface for SourceGen.
    ///
    /// This mirrors what the graphical application does today: open a project, browse the
    /// disassembly, edit labels and comments, undo/redo, save, and generate assembly source.
    ///
    /// Terminal.Gui drives the display; on Unix it uses the ncurses-backed driver, so this
    /// runs in any terminal.  All of the actual behaviour lives in <see cref="TuiModel"/>,
    /// which has no terminal dependency and is covered by unit tests.
    /// </summary>
    public static class Program {
        private static TuiModel sModel;
        private static ListView sCodeList;
        private static ListView sSymbolList;
        private static ListView sMessageList;
        private static Label sStatusLabel;
        private static int sFirstLine;
        private static int sLastWidth = -1;
        private static int sLastHeight = -1;

        public static int Main(string[] args) {
            sModel = new TuiModel(new AppController());

            Application.Init();
            try {
                BuildUI();

                // A project path may be supplied on the command line, which is the terminal
                // equivalent of the recent-projects list.
                if (args.Length > 0 && !string.IsNullOrEmpty(args[0])) {
                    OpenProject(Path.GetFullPath(args[0]));
                }
                RefreshView();

                Application.Run();
            } finally {
                sModel.Controller.Dispose();
                Application.Shutdown();
            }
            return 0;
        }

        private static void BuildUI() {
            Toplevel top = Application.Top;

            MenuBar menu = new MenuBar(new MenuBarItem[] {
                new MenuBarItem("_File", new MenuItem[] {
                    new MenuItem("_Open...", "Ctrl+O", () => PromptOpen()),
                    new MenuItem("_Save", "Ctrl+S", () => Save()),
                    new MenuItem("_Generate Assembly...", "Ctrl+G", () => GenerateAssembly()),
                    new MenuItem("_Close", "", () => CloseProject()),
                    null,
                    new MenuItem("_Quit", "Ctrl+Q", () => Application.RequestStop()),
                }),
                new MenuBarItem("_Edit", new MenuItem[] {
                    new MenuItem("_Undo", "Ctrl+Z", () => Undo()),
                    new MenuItem("_Redo", "Ctrl+Y", () => Redo()),
                    null,
                    new MenuItem("Edit _Label...", "Ctrl+L", () => EditLabel()),
                    new MenuItem("Edit _Comment...", "Ctrl+;", () => EditComment()),
                    new MenuItem("Edit _Long Comment...", "Ctrl+K", () => EditLongComment()),
                    null,
                    new MenuItem("_Find...", "Ctrl+F", () => Find(forward: true)),
                    new MenuItem("Find _Next", "F3", () => FindNext()),
                    new MenuItem("Find _Previous", "Shift+F3", () => Find(forward: false)),
                }),
                new MenuBarItem("_Help", new MenuItem[] {
                    new MenuItem("_Keys", "", () => ShowKeys()),
                }),
            });
            top.Add(menu);

            // Layout mirrors the graphical workspace: code list in the middle, symbols on the
            // right, messages along the bottom.
            FrameView codeFrame = new FrameView("Code") {
                X = 0,
                Y = 1,
                Width = Dim.Fill(36),
                Height = Dim.Fill(9),
            };
            sCodeList = new ListView() {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                AllowsMarking = false,
            };
            sCodeList.SelectedItemChanged += args => SyncSelection();
            codeFrame.Add(sCodeList);

            FrameView symbolFrame = new FrameView("Symbols") {
                X = Pos.Right(codeFrame),
                Y = 1,
                Width = 36,
                Height = Dim.Fill(9),
            };
            sSymbolList = new ListView() {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };
            symbolFrame.Add(sSymbolList);

            FrameView messageFrame = new FrameView("Messages") {
                X = 0,
                Y = Pos.Bottom(codeFrame),
                Width = Dim.Fill(),
                Height = 8,
            };
            sMessageList = new ListView() {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };
            messageFrame.Add(sMessageList);

            top.Add(codeFrame);
            top.Add(symbolFrame);
            top.Add(messageFrame);

            sStatusLabel = new Label("") {
                X = 0,
                Y = Pos.Bottom(messageFrame),
                Width = Dim.Fill(),
            };
            top.Add(sStatusLabel);

            // The first RefreshView() happens before the widget sizes are known, so fill the
            // panes again once the layout is complete.  Resizing does the same.
            top.LayoutComplete += e => {
                if (sCodeList.Bounds.Width != sLastWidth ||
                        sCodeList.Bounds.Height != sLastHeight) {
                    sLastWidth = sCodeList.Bounds.Width;
                    sLastHeight = sCodeList.Bounds.Height;
                    RefreshView();
                }
            };

            // Keyboard shortcuts, used when the menu is not open.
            top.KeyPress += args => {
                bool ctrl = args.KeyEvent.IsCtrl;
                switch (args.KeyEvent.Key) {
                    case Key.q when ctrl:
                        Application.RequestStop();
                        args.Handled = true;
                        break;
                    case Key.z when ctrl:
                        Undo();
                        args.Handled = true;
                        break;
                    case Key.y when ctrl:
                        Redo();
                        args.Handled = true;
                        break;
                    case Key.s when ctrl:
                        Save();
                        args.Handled = true;
                        break;
                    case Key.l when ctrl:
                        EditLabel();
                        args.Handled = true;
                        break;
                    case Key.o when ctrl:
                        PromptOpen();
                        args.Handled = true;
                        break;
                    case Key.g when ctrl:
                        GenerateAssembly();
                        args.Handled = true;
                        break;
                    case Key.k when ctrl:
                        EditLongComment();
                        args.Handled = true;
                        break;
                    case Key.f when ctrl:
                        Find(forward: true);
                        args.Handled = true;
                        break;
                    case Key.F3 when args.KeyEvent.IsShift:
                        Find(forward: false);
                        args.Handled = true;
                        break;
                    case Key.F3:
                        FindNext();
                        args.Handled = true;
                        break;
                }
            };
        }

        /// <summary>
        /// Pushes model state into the widgets.
        /// </summary>
        private static void RefreshView() {
            int codeWidth = Math.Max(40, sCodeList.Bounds.Width);
            int codeHeight = Math.Max(1, sCodeList.Bounds.Height);

            List<string> codeLines = sModel.GetCodeLines(codeWidth, codeHeight, ref sFirstLine);
            sCodeList.SetSource(codeLines.Select(s => (ustring)s).ToList());
            if (sModel.SelectedLine >= 0) {
                sCodeList.SelectedItem = sModel.SelectedLine - sFirstLine;
            }

            sSymbolList.SetSource(
                sModel.GetSymbolLines(200).Select(s => (ustring)s).ToList());
            sMessageList.SetSource(
                sModel.GetMessageLines(50).Select(s => (ustring)s).ToList());

            sStatusLabel.Text = sModel.GetStatusLine();
        }

        /// <summary>
        /// Copies the widget selection back into the model.
        /// </summary>
        private static void SyncSelection() {
            if (sCodeList == null || sCodeList.SelectedItem < 0) {
                return;
            }
            sModel.SetSelectedLine(sFirstLine + sCodeList.SelectedItem);
        }

        // ---- commands ----

        private static void PromptOpen() {
            if (!PromptText("Open Project", "Path to .dis65 file:", string.Empty,
                    out string path)) {
                return;
            }
            OpenProject(path);
        }

        private static void OpenProject(string path) {
            if (!sModel.OpenProject(path, out string error)) {
                MessageBox.ErrorQuery("Open Project", error ?? "Unknown error", "OK");
                return;
            }
            sFirstLine = 0;
            RefreshView();
        }

        private static void CloseProject() {
            sModel.CloseProject();
            sFirstLine = 0;
            RefreshView();
        }

        private static void Save() {
            if (!sModel.HasProject) {
                MessageBox.ErrorQuery("Save", "No project is open.", "OK");
                return;
            }
            if (!sModel.Save(out string error)) {
                MessageBox.ErrorQuery("Save", error ?? "Unknown error", "OK");
                return;
            }
            RefreshView();
        }

        private static void Undo() {
            if (!sModel.HasProject) {
                return;
            }
            if (!sModel.Undo(out string error)) {
                MessageBox.ErrorQuery("Undo", error ?? "Unknown error", "OK");
            }
            RefreshView();
        }

        private static void Redo() {
            if (!sModel.HasProject) {
                return;
            }
            if (!sModel.Redo(out string error)) {
                MessageBox.ErrorQuery("Redo", error ?? "Unknown error", "OK");
            }
            RefreshView();
        }

        private static void EditLabel() {
            if (!sModel.HasProject || sModel.SelectedLine < 0) {
                MessageBox.ErrorQuery("Edit Label", "Select a line first.", "OK");
                return;
            }
            string current = sModel.GetSelectedLabel();
            if (!PromptText("Edit Label",
                    "Label (empty removes it). Current: " +
                        (string.IsNullOrEmpty(current) ? "(none)" : current),
                    current, out string label)) {
                return;
            }
            if (!sModel.SetSelectedLabel(label.Trim(), out string error)) {
                MessageBox.ErrorQuery("Edit Label", error ?? "Unknown error", "OK");
            }
            RefreshView();
        }

        private static void EditComment() {
            if (!sModel.HasProject || sModel.SelectedLine < 0) {
                MessageBox.ErrorQuery("Edit Comment", "Select a line first.", "OK");
                return;
            }
            if (!PromptText("Edit Comment", "Comment text (empty removes it):",
                    sModel.GetSelectedComment(), out string comment)) {
                return;
            }
            if (!sModel.SetSelectedComment(comment, out string error)) {
                MessageBox.ErrorQuery("Edit Comment", error ?? "Unknown error", "OK");
            }
            RefreshView();
        }

        /// <summary>
        /// Prompts for a search string and jumps to the next match.
        /// </summary>
        private static void Find(bool forward) {
            if (!sModel.HasProject) {
                MessageBox.ErrorQuery("Find", "No project is open.", "OK");
                return;
            }

            Dialog dlg = new Dialog("Find", 60, 9);
            dlg.Add(new Label("Search for:") { X = 1, Y = 1 });
            TextField field = new TextField(sModel.LastSearchText ?? string.Empty) {
                X = 1,
                Y = 2,
                Width = Dim.Fill(2),
            };
            dlg.Add(field);
            CheckBox matchCase = new CheckBox("Match case", sModel.LastSearchMatchCase) {
                X = 1,
                Y = 3,
            };
            dlg.Add(matchCase);

            bool accepted = false;
            string needle = null;
            bool caseSensitive = false;
            Button ok = new Button(forward ? "Find Next" : "Find Previous", true);
            ok.Clicked += () => {
                accepted = true;
                needle = field.Text?.ToString() ?? string.Empty;
                caseSensitive = matchCase.Checked;
                Application.RequestStop();
            };
            dlg.AddButton(ok);
            dlg.AddButton(new Button("Cancel"));

            field.SetFocus();
            Application.Run(dlg);

            if (!accepted || string.IsNullOrEmpty(needle)) {
                return;
            }
            bool found = forward
                ? sModel.FindNext(needle, caseSensitive, out string error)
                : sModel.FindPrevious(needle, caseSensitive, out error);
            if (!found) {
                MessageBox.ErrorQuery("Find", error ?? "Not found.", "OK");
            }
            RefreshView();
        }

        /// <summary>
        /// Repeats the last search.
        /// </summary>
        private static void FindNext() {
            if (!sModel.HasProject) {
                return;
            }
            if (!sModel.FindAgain(out string error)) {
                MessageBox.ErrorQuery("Find", error ?? "Not found.", "OK");
                return;
            }
            RefreshView();
        }

        /// <summary>
        /// Edits the multi-line comment attached to the selected line.
        /// </summary>
        private static void EditLongComment() {
            if (!sModel.HasProject || sModel.SelectedLine < 0) {
                MessageBox.ErrorQuery("Edit Long Comment", "Select a line first.", "OK");
                return;
            }

            Dialog dlg = new Dialog("Edit Long Comment", 76, 20);
            dlg.Add(new Label("Comment text (empty removes it):") { X = 1, Y = 1 });
            TextView view = new TextView() {
                X = 1,
                Y = 2,
                Width = Dim.Fill(2),
                Height = Dim.Fill(3),
                Text = sModel.GetSelectedLongComment(),
            };
            dlg.Add(view);

            bool accepted = false;
            string text = null;
            Button ok = new Button("OK", true);
            ok.Clicked += () => {
                accepted = true;
                text = view.Text?.ToString() ?? string.Empty;
                Application.RequestStop();
            };
            dlg.AddButton(ok);
            dlg.AddButton(new Button("Cancel"));

            view.SetFocus();
            Application.Run(dlg);

            if (!accepted) {
                return;
            }
            if (!sModel.SetSelectedLongComment(text, out string error)) {
                MessageBox.ErrorQuery("Edit Long Comment", error ?? "Unknown error", "OK");
            }
            RefreshView();
        }

        private static void GenerateAssembly() {
            if (!sModel.HasProject) {
                MessageBox.ErrorQuery("Generate Assembly", "No project is open.", "OK");
                return;
            }

            AsmGen.AssemblerInfo.Id asmId = PickAssembler();
            if (asmId == AsmGen.AssemblerInfo.Id.Unknown) {
                return;
            }

            string defaultDir = Directory.GetCurrentDirectory();
            if (!PromptText("Generate Assembly", "Output directory:", defaultDir,
                    out string outDir)) {
                return;
            }
            if (!Directory.Exists(outDir)) {
                MessageBox.ErrorQuery("Generate Assembly",
                    "Directory does not exist: " + outDir, "OK");
                return;
            }

            if (!sModel.GenerateAssembly(asmId, outDir, out List<string> files,
                    out string error)) {
                MessageBox.ErrorQuery("Generate Assembly", error ?? "Unknown error", "OK");
                return;
            }

            string summary = "Generated " + files.Count + " file(s) for " + asmId + ".";
            MessageBox.Query("Generate Assembly", summary, "OK");
            RefreshView();
        }

        private static AsmGen.AssemblerInfo.Id PickAssembler() {
            AsmGen.AssemblerInfo.Id[] options = new AsmGen.AssemblerInfo.Id[] {
                AsmGen.AssemblerInfo.Id.Tass64,
                AsmGen.AssemblerInfo.Id.Acme,
                AsmGen.AssemblerInfo.Id.Cc65,
                AsmGen.AssemblerInfo.Id.Merlin32,
            };
            string[] labels = options.Select(o => o.ToString()).ToArray();

            Dialog dlg = new Dialog("Generate Assembly", 50, 12);
            dlg.Add(new Label("Assembler dialect:") { X = 1, Y = 1 });
            RadioGroup group = new RadioGroup(labels.Select(l => (ustring)l).ToArray()) { X = 1, Y = 2 };
            dlg.Add(group);

            AsmGen.AssemblerInfo.Id result = AsmGen.AssemblerInfo.Id.Unknown;
            Button ok = new Button("OK", true);
            ok.Clicked += () => {
                int selected = group.SelectedItem;
                result = (selected >= 0 && selected < options.Length)
                    ? options[selected] : AsmGen.AssemblerInfo.Id.Unknown;
                Application.RequestStop();
            };
            dlg.AddButton(ok);
            dlg.AddButton(new Button("Cancel"));

            Application.Run(dlg);
            return result;
        }

        private static void ShowKeys() {
            MessageBox.Query("Keys",
                "Up/Down/PgUp/PgDn/Home/End  move  |  Ctrl+L label  |  Ctrl+; comment\n" +
                "Ctrl+K long comment  |  Ctrl+F find  |  F3 / Shift+F3 next / previous\n" +
                "Ctrl+Z undo  |  Ctrl+Y redo  |  Ctrl+S save  |  Ctrl+O open\n" +
                "Ctrl+G generate assembly  |  Ctrl+Q quit",
                "OK");
        }

        /// <summary>
        /// Prompts for a single line of text.
        /// </summary>
        private static bool PromptText(string title, string prompt, string initial,
                out string result) {
            result = null;

            Dialog dlg = new Dialog(title, 72, 8);
            dlg.Add(new Label(prompt) { X = 1, Y = 1, Width = Dim.Fill(2) });
            TextField field = new TextField(initial ?? string.Empty) {
                X = 1,
                Y = 2,
                Width = Dim.Fill(2),
            };
            dlg.Add(field);

            bool accepted = false;
            string entered = null;
            Button ok = new Button("OK", true);
            ok.Clicked += () => {
                accepted = true;
                entered = field.Text?.ToString() ?? string.Empty;
                Application.RequestStop();
            };
            dlg.AddButton(ok);
            dlg.AddButton(new Button("Cancel"));

            field.SetFocus();
            Application.Run(dlg);
            result = entered;
            return accepted;
        }
    }
}
