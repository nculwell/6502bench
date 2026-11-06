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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

using SourceGen.AsmGen;
using SourceGen.Commands;
using SourceGen.Services;

namespace SourceGen.Views {
    /// <summary>
    /// Main window.
    ///
    /// Layout is ported from the WPF MainWindow: a menu bar, a status bar, and a three-panel
    /// workspace (references/notes, code list with message list, symbols/info) separated by
    /// GridSplitters.
    ///
    /// Command wiring: WPF used RoutedUICommand + CommandBinding, which Avalonia does not
    /// have.  Each command is now a RelayCommand exposed as a property and bound from XAML,
    /// with KeyBindings in the window.  Commands that depend on MainController are listed in
    /// mPendingCommands until that class is ported.
    /// </summary>
    public partial class MainWindow : Window {
        // ---- commands that are wired up ----
        public RelayCommand NewProjectCommand { get; private set; }
        public RelayCommand OpenCommand { get; private set; }
        public RelayCommand SaveCommand { get; private set; }
        public RelayCommand SaveAsCommand { get; private set; }
        public RelayCommand CloseCommand { get; private set; }
        public RelayCommand ExitCommand { get; private set; }
        public RelayCommand UndoCommand { get; private set; }
        public RelayCommand RedoCommand { get; private set; }
        public RelayCommand CopyCommand { get; private set; }
        public RelayCommand SelectAllCommand { get; private set; }
        public RelayCommand FindCommand { get; private set; }
        public RelayCommand FindNextCommand { get; private set; }
        public RelayCommand FindPreviousCommand { get; private set; }
        public RelayCommand FindAllCommand { get; private set; }
        public RelayCommand GotoCommand { get; private set; }
        public RelayCommand GotoLastChangeCommand { get; private set; }
        public RelayCommand JumpToOperandCommand { get; private set; }
        public RelayCommand NavigateForwardCommand { get; private set; }
        public RelayCommand NavigateBackwardCommand { get; private set; }
        public RelayCommand ViewAddressMapCommand { get; private set; }
        public RelayCommand AssembleCommand { get; private set; }
        public RelayCommand ExportCommand { get; private set; }
        public RelayCommand GenerateLabelsCommand { get; private set; }
        public RelayCommand ReloadExternalFilesCommand { get; private set; }
        public RelayCommand EditHeaderCommentCommand { get; private set; }
        public RelayCommand ProjectPropertiesCommand { get; private set; }
        public RelayCommand EditAppSettingsCommand { get; private set; }
        public RelayCommand EditOperandCommand { get; private set; }
        public RelayCommand EditLabelCommand { get; private set; }
        public RelayCommand EditCommentCommand { get; private set; }
        public RelayCommand EditLongCommentCommand { get; private set; }
        public RelayCommand EditNoteCommand { get; private set; }
        public RelayCommand EditAddressCommand { get; private set; }
        public RelayCommand EditStatusFlagsCommand { get; private set; }
        public RelayCommand EditDataBankCommand { get; private set; }
        public RelayCommand EditProjectSymbolCommand { get; private set; }
        public RelayCommand EditVisualizationSetCommand { get; private set; }
        public RelayCommand TagAsCodeStartPointCommand { get; private set; }
        public RelayCommand TagAsCodeStopPointCommand { get; private set; }
        public RelayCommand TagAsInlineDataCommand { get; private set; }
        public RelayCommand RemoveAnalyzerTagsCommand { get; private set; }
        public RelayCommand FormatAsWordCommand { get; private set; }
        public RelayCommand RemoveFormattingCommand { get; private set; }
        public RelayCommand DeleteMlcCommand { get; private set; }
        public RelayCommand ShowHexDumpCommand { get; private set; }
        public RelayCommand ShowInstructionChartCommand { get; private set; }
        public RelayCommand ShowAsciiChartCommand { get; private set; }
        public RelayCommand FileSlicerCommand { get; private set; }
        public RelayCommand FileConcatenatorCommand { get; private set; }
        public RelayCommand HelpCommand { get; private set; }
        public RelayCommand AboutCommand { get; private set; }

        /// <summary>
        /// Backing state for the workspace.
        /// </summary>
        private readonly AppController mController = new AppController();

        /// <summary>
        /// Names of commands whose behaviour still lives in the not-yet-ported MainController.
        /// They stay visible but report their status instead of silently doing nothing.
        /// </summary>
        private readonly HashSet<string> mPendingCommands = new HashSet<string>();

        public MainWindow() {
            CreateCommands();
            DataContext = this;

            InitializeComponent();

            this.FindControl<TextBlock>("launchVersionText").Text =
                "Version " + App.ProgramVersion;
            this.FindControl<TextBlock>("statusBarText").Text = "Ready";
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        private void CreateCommands() {
            // ---- wired commands ----
            ExitCommand = new RelayCommand(() => Close());
            HelpCommand = new RelayCommand(() => HelpAccess.ShowHelp(HelpAccess.Topic.Contents));
            AboutCommand = new RelayCommand(() => _ = ShowAboutAsync());

            CopyCommand = new RelayCommand(() => _ = CopySelectionAsync(),
                () => this.FindControl<ListBox>("codeListView")?.SelectedItems?.Count > 0);
            SelectAllCommand = new RelayCommand(SelectAll);

            // ---- commands blocked on the MainController port ----
            NewProjectCommand = Pending("New Project");
            OpenCommand = new RelayCommand(() => _ = OpenProjectAsync());
            SaveCommand = new RelayCommand(() => _ = SaveAsync(), () => mController.HasProject);
            SaveAsCommand = Pending("Save As");
            CloseCommand = new RelayCommand(CloseProject);
            UndoCommand = new RelayCommand(() => _ = UndoAsync(), () => mController.CanUndo);
            RedoCommand = new RelayCommand(() => _ = RedoAsync(), () => mController.CanRedo);
            FindCommand = Pending("Find");
            FindNextCommand = Pending("Find Next");
            FindPreviousCommand = Pending("Find Previous");
            FindAllCommand = Pending("Find All");
            GotoCommand = Pending("Goto");
            GotoLastChangeCommand = Pending("Goto Last Change");
            JumpToOperandCommand = Pending("Jump To Operand");
            NavigateForwardCommand = Pending("Navigate Forward");
            NavigateBackwardCommand = Pending("Navigate Backward");
            ViewAddressMapCommand = Pending("View Address Map");
            AssembleCommand = new RelayCommand(() => _ = GenerateAssemblyAsync());
            ExportCommand = Pending("Export");
            GenerateLabelsCommand = Pending("Generate Labels");
            ReloadExternalFilesCommand = Pending("Reload External Files");
            EditHeaderCommentCommand = Pending("Edit Header Comment");
            ProjectPropertiesCommand = Pending("Project Properties");
            EditAppSettingsCommand = Pending("App Settings");
            EditOperandCommand = Pending("Edit Operand");
            EditLabelCommand = new RelayCommand(() => _ = EditLabelAsync(),
                () => GetSelectedOffset() >= 0);
            EditCommentCommand = new RelayCommand(() => _ = EditCommentAsync(),
                () => GetSelectedOffset() >= 0);
            EditLongCommentCommand = Pending("Edit Long Comment");
            EditNoteCommand = Pending("Edit Note");
            EditAddressCommand = Pending("Edit Address");
            EditStatusFlagsCommand = Pending("Edit Status Flags");
            EditDataBankCommand = Pending("Edit Data Bank");
            EditProjectSymbolCommand = Pending("Edit Project Symbol");
            EditVisualizationSetCommand = Pending("Edit Visualization Set");
            TagAsCodeStartPointCommand = Pending("Tag As Code Start Point");
            TagAsCodeStopPointCommand = Pending("Tag As Code Stop Point");
            TagAsInlineDataCommand = Pending("Tag As Inline Data");
            RemoveAnalyzerTagsCommand = Pending("Remove Analyzer Tags");
            FormatAsWordCommand = Pending("Format As Word");
            RemoveFormattingCommand = Pending("Remove Formatting");
            DeleteMlcCommand = Pending("Delete Long Comment");
            ShowHexDumpCommand = Pending("Hex Dump");
            ShowInstructionChartCommand = Pending("Instruction Chart");
            ShowAsciiChartCommand = Pending("ASCII Chart");
            FileSlicerCommand = Pending("File Slicer");
            FileConcatenatorCommand = Pending("File Concatenator");
        }

        /// <summary>
        /// Creates a command that explains the feature still depends on the MainController
        /// port.  These are replaced one by one as MainController is ported.
        /// </summary>
        private RelayCommand Pending(string displayName) {
            mPendingCommands.Add(displayName);
            return new RelayCommand(() => _ = ShowPendingAsync(displayName));
        }

        private async Task ShowPendingAsync(string displayName) {
            await Dialogs.ShowAsync(this,
                displayName + " still lives in MainController, which has not been ported yet.",
                "Not Yet Ported");
        }

        private async Task ShowAboutAsync() {
            await Dialogs.ShowAsync(this,
                "6502bench SourceGen " + App.ProgramVersion + "\n" +
                "6502/65C02/65816 disassembler.",
                "About SourceGen");
        }

        private void SelectAll() {
            ListBox list = this.FindControl<ListBox>("codeListView");
            if (list == null) {
                return;
            }
            list.SelectAll();
        }

        private async Task CopySelectionAsync() {
            ListBox list = this.FindControl<ListBox>("codeListView");
            if (list == null || list.SelectedItems == null) {
                return;
            }

            // Each row renders as fixed-width columns; copy them back out as text.
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (object item in list.SelectedItems) {
                if (item is DisplayList.FormattedParts parts) {
                    sb.AppendLine(parts.Comment ?? string.Empty);
                }
            }
            await Ui.CopyTextAsync(this, sb.ToString());
        }

        // ---- editing ----

        /// <summary>
        /// File offset of the selected code list line, or -1 if nothing is selected.
        /// </summary>
        private int GetSelectedOffset() {
            ListBox list = this.FindControl<ListBox>("codeListView");
            if (list == null || list.SelectedIndex < 0) {
                return -1;
            }
            return mController.GetFileOffset(list.SelectedIndex);
        }

        private string DescribeOffset(int offset) {
            int addr = mController.GetAddressAt(offset);
            string addrText = (addr == Asm65.Address.NON_ADDR)
                ? "(no address)" : "$" + addr.ToString("x4");
            return "Offset +" + offset.ToString("x6") + ", address " + addrText;
        }

        private async Task EditLabelAsync() {
            int offset = GetSelectedOffset();
            if (offset < 0) {
                await Dialogs.ShowAsync(this, "Select a line first.", "Edit Label");
                return;
            }

            EditLabelDialog dlg = new EditLabelDialog();
            dlg.Configure(DescribeOffset(offset), mController.GetLabelAt(offset),
                mController.HasUserLabelAt(offset), out _);
            if (await dlg.ShowDialog<bool>(this) != true) {
                return;
            }

            if (!mController.SetLabel(offset, dlg.LabelText, out string error)) {
                await Dialogs.ShowAsync(this, error, "Edit Label");
                return;
            }
            RefreshFromController();
        }

        private async Task EditCommentAsync() {
            int offset = GetSelectedOffset();
            if (offset < 0) {
                await Dialogs.ShowAsync(this, "Select a line first.", "Edit Comment");
                return;
            }

            EditCommentDialog dlg = new EditCommentDialog();
            dlg.Configure(DescribeOffset(offset), mController.GetCommentAt(offset));
            if (await dlg.ShowDialog<bool>(this) != true) {
                return;
            }

            if (!mController.SetComment(offset, dlg.CommentText, out string error)) {
                await Dialogs.ShowAsync(this, error, "Edit Comment");
                return;
            }
            RefreshFromController();
        }

        private async Task SaveAsync() {
            if (!mController.SaveProject(out string error)) {
                await Dialogs.ShowAsync(this, error, "Save Project");
                return;
            }
            RefreshFromController();
        }

        private async Task UndoAsync() {
            if (!mController.Undo(out string error)) {
                await Dialogs.ShowAsync(this, error, "Undo");
                return;
            }
            RefreshFromController();
        }

        private async Task RedoAsync() {
            if (!mController.Redo(out string error)) {
                await Dialogs.ShowAsync(this, error, "Redo");
                return;
            }
            RefreshFromController();
        }

        // ---- project lifecycle ----

        private async Task OpenProjectAsync() {
            FilePickerFileType projectType = new FilePickerFileType("SourceGen project") {
                Patterns = new string[] { "*" + ProjectFile.FILENAME_EXT },
            };
            string path = await FilePickers.OpenFileAsync(this, "Open Project",
                new FilePickerFileType[] { projectType });
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            if (!mController.OpenProject(path, out string error)) {
                await Dialogs.ShowAsync(this, error, "Unable to open project");
                return;
            }
            RefreshFromController();
        }

        /// <summary>
        /// Generates assembly source for the open project.  Choosing the dialect and the
        /// output folder is all the prompting there is; running the cross-assembler to
        /// verify the result is not done here.
        /// </summary>
        private async Task GenerateAssemblyAsync() {
            if (!mController.HasProject) {
                await Dialogs.ShowAsync(this, "Open a project first.", "Generate Assembly");
                return;
            }

            AssemblerPickerDialog picker = new AssemblerPickerDialog();
            AssemblerInfo.Id asmId = await picker.ShowDialog<AssemblerInfo.Id>(this);
            if (asmId == AssemblerInfo.Id.Unknown) {
                return;
            }

            string outputDir = await FilePickers.OpenFolderAsync(this, "Select output folder");
            if (string.IsNullOrEmpty(outputDir)) {
                return;
            }

            if (!mController.GenerateAssembly(asmId, outputDir, out List<string> files,
                    out string error)) {
                await Dialogs.ShowAsync(this, error, "Generate Assembly");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Generated " + files.Count + " file(s):");
            foreach (string path in files) {
                sb.AppendLine("  " + System.IO.Path.GetFileName(path));
            }
            await Dialogs.ShowAsync(this, sb.ToString(), "Generate Assembly");
            this.FindControl<TextBlock>("statusBarText").Text = mController.StatusText;
        }

        private void CloseProject() {
            mController.CloseProject();
            RefreshFromController();
        }

        /// <summary>
        /// Pushes controller state into the view.  The panes are bound by hand rather than
        /// through DataContext because the code list is an IList of engine objects.
        /// </summary>
        private void RefreshFromController() {
            bool hasProject = mController.HasProject;

            this.FindControl<Grid>("launchPanel").IsVisible = !hasProject;
            this.FindControl<Grid>("codeListGrid").IsVisible = hasProject;

            this.FindControl<ListBox>("codeListView").ItemsSource = mController.CodeList;
            this.FindControl<DataGrid>("messageGrid").ItemsSource = mController.Messages;
            this.FindControl<DataGrid>("symbolsGrid").ItemsSource = mController.Symbols;
            this.FindControl<DataGrid>("notesGrid").ItemsSource = mController.Notes;

            this.FindControl<DockPanel>("messageListPanel").IsVisible =
                hasProject && mController.Messages.Count > 0;
            this.FindControl<TextBlock>("statusBarText").Text = mController.StatusText;
            this.FindControl<Button>("messageStatusButton").Content = mController.MessageSummary;

            // Editing rebuilds the list, which clears the selection; put it back so the user
            // stays where they were.
            if (hasProject && mSelectedLineIndex >= 0) {
                this.FindControl<ListBox>("codeListView").SelectedIndex =
                    System.Math.Min(mSelectedLineIndex, mController.LineCount - 1);
            }

            // Undo/redo availability changed.
            SaveCommand?.RaiseCanExecuteChanged();
            UndoCommand?.RaiseCanExecuteChanged();
            RedoCommand?.RaiseCanExecuteChanged();
            EditLabelCommand?.RaiseCanExecuteChanged();
            EditCommentCommand?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Line the user last selected, remembered across refreshes.
        /// </summary>
        private int mSelectedLineIndex = -1;

        // ---- event handlers referenced from XAML ----

        private void CodeListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ListBox list = this.FindControl<ListBox>("codeListView");
            if (list != null && list.SelectedIndex >= 0) {
                mSelectedLineIndex = list.SelectedIndex;
            }
            CopyCommand?.RaiseCanExecuteChanged();
            EditLabelCommand?.RaiseCanExecuteChanged();
            EditCommentCommand?.RaiseCanExecuteChanged();
        }

        private void CodeListView_DoubleTapped(object sender, TappedEventArgs e) {
            EditOperandCommand?.Execute(null);
        }

        private void HideMessageList_Click(object sender, RoutedEventArgs e) {
            DockPanel panel = this.FindControl<DockPanel>("messageListPanel");
            if (panel != null) {
                panel.IsVisible = false;
            }
        }

        private void MessageStatusButton_Click(object sender, RoutedEventArgs e) {
            DockPanel panel = this.FindControl<DockPanel>("messageListPanel");
            if (panel != null) {
                panel.IsVisible = !panel.IsVisible;
            }
        }

        private void ReferencesListCopyOut_Click(object sender, RoutedEventArgs e) {
            _ = Ui.CopyTextAsync(this, string.Empty);
        }
    }
}
