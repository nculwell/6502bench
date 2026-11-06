# 6502bench Source Code Notes # 

All of the code is written in C# .NET, using the (free to download) Visual
Studio Community 2022 IDE as the primary development environment.  The user
interface uses the WPF API, targeted at the final release of .NET
Framework (4.8.1).  To build the sources, clone the git repository and open
"WorkBench.sln" in Visual Studio 2019 or later.  The Solution file is called
"WorkBench.sln" rather than "6502bench.sln" because some things in
Visual Studio got weird when it didn't start with a letter.

When installing Visual Studio, be sure to include ".NET Desktop Development".
You may also need to install the .NET Framework 4.8.1 "Dev Pack" (as a
separate download, or via the "individual components" tab in the
Visual Studio Installer).

The code style is closer to what Android uses than "standard" C#.  Lines
are folded to fit 100 columns.


## SourceGen Points of Interest ##

Places to start...

The main window UI is in WpfGui/MainWindow.xaml[.cs].  Much of the
implementation lives in MainController.cs.

The top-level object for the project data is DisasmProject.cs.  The
Analyze() method drives the code and data analysis process.  ApplyChanges()
is the heart of the undo/redo system.

Source code generation and assembler execution is routed through
AsmGen/AssemblerInfo.cs.  If you want to add support for a new
cross-assembler, start by adding new entries to the enum and data
tables there.

Nothing specific to a target system is baked into the main application.  The
SourceGen/RuntimeData directory has the system definitions used for the
"new project" list, along with subdirectories with symbol files and extension
scripts.  The [README file there](SourceGen/RuntimeData/README.md)
explains a bit more.

## Publishing a New Release ##

Steps:

 1. Update the version number in `SourceGen/App.xaml.cs`.
 2. Run Debug &gt; Source Generation Tests to verify that the code generation
    tests pass.  This requires that all cross-assemblers be installed and
    configured.
 3. Remove any existing `DIST_Release` directory from the top level.
 4. In Visual Studio, change the build configuration to Release, and the
	startup project to MakeDist.
 5. Do a full clean build.
 6. Hit F5 to start MakeDist.  Click "Build" to generate a release build.  The
	files will be copied into `DIST_Release`.
 7. Create an empty ZIP file (e.g. `6502bench1_10_0-alpha1.zip`).
 8. Copy all files from `DIST_Release` into it.
 9. Submit all changes to git, push them to the server.
 10. Create a new release on github.  Drag the ZIP file into it.
 11. Update/close any issues that have been addressed by the new release.
	 Add the change notes to the wiki page.

Version numbers should follow the semantic versioning scheme: v1.2.3,
v1.2.3-dev1, etc.


## .NET 10 / Linux Port ##

The application is being moved off .NET Framework and WPF.  The current layout is:

| Project | Contents |
| --- | --- |
| `Asm65` | CPU definitions, opcode table, formatting helpers |
| `CommonUtil` | Shared utilities (CRC-32, range sets, address map, shell) |
| `PluginCommon` | Extension-script interfaces shared with plugins |
| `SourceGen/RuntimeData` | System definitions and symbol files |
| `SourceGen.Engine` | Everything headless: project model, analyzer, code generators, extension-script host |
| `SourceGen.App` | Avalonia 11 desktop application |
| `SourceGen.Tests` | xunit test suite |

The WPF projects (`SourceGen/WpfGui`, `CommonWPF`, `MakeDist`) are being replaced by
`SourceGen.App` and are kept only as the reference for the remaining UI work.  They are
no longer in the build.

Build and test:

    dotnet build WorkBench.slnx
    dotnet test SourceGen.Tests/SourceGen.Tests.csproj

`WorkBench.slnx` is the solution for the ported tree (the .NET 10 SDK writes the XML
solution format).  The original `WorkBench.sln` referenced the WPF projects, which no
longer build, and has been replaced.

To produce a distributable build:

    ./MakeDist/publish.sh linux-x64 Release      # framework-dependent
    ./MakeDist/publish.sh linux-x64 Release --self-contained

This replaces the old MakeDist WPF utility.  `RuntimeData` is copied as part of the
project, and the script adds the licence, notice and help pages; the result is directly
runnable (`DIST_Release/<rid>/SourceGen`).

### Running the assembler regression step ###

`SourceGen.Tests` drives the same generate-and-assemble suite as the GUI's "Source
Generation Tests" window.  Generating sources always runs; the assemble-and-compare step
only runs for assemblers that are configured.  The tests discover cross-assemblers from
the environment so no machine-specific paths are compiled in:

    SOURCEGEN_CC65_PATH=/path/to/cc65/bin \
    SOURCEGEN_MERLIN32_PATH=/path/to/Merlin32 \
    dotnet test SourceGen.Tests/SourceGen.Tests.csproj

Each variable may name either the directory containing the tool or the executable itself.
On Unix the tool must have the execute bit set.  ACME and 64tass are also supported by the
same mechanism (`SOURCEGEN_ACME_PATH`, `SOURCEGEN_64TASS_PATH`) but are normally found on
the PATH already, so no variable is needed for them.

### Known upstream issues (not port regressions) ###

Assembling with cc65 V2.19 (`ca65`/`cl65`) surfaces two problems that exist in the
current generated output, not in the port.  They are reproducible by assembling the
checked-in `SGTestData/Expected` sources directly, and the test suite classifies them as
toolchain disagreements so they show up as a NOTE rather than a failure:

1. **W65C02 projects emit the wrong CPU directive for cc65.**  The generator writes
   `.setcpu "65C02"`, but `wai` and `stp` (and WDC extensions generally) require
   `.setcpu "w65c02"`.  Modern `ca65` therefore rejects the source.  Verified: changing
   that one line to `.setcpu "w65c02"` makes the affected cases assemble cleanly.
   Affects test cases `10003-allops-value-W65C02` and `10013-allops-zero-W65C02`.
   Tracked as a known issue; deliberately not changed yet because it alters generated
   output and the reference fixtures.

2. **All-ops fixtures exercise the 6502 indirect-JMP page-wrap bug.**  Case
   `10000-allops-value-6502` contains `jmp ($feff)`, which newer `ca65` rejects with
   "jmp (abs) across page border" instead of silently wrapping.  This is assembler
   version drift over a deliberate edge case, not a generator defect.

Note that comparing generated output against the reference `Expected` sources proves
fidelity to upstream behaviour, not correctness: issue 1 passes that comparison.


### Assembler-specific findings (Merlin 32) ###

Testing with Merlin 32 v1.2 beta 2 added three more version-related differences.
None of them are port regressions; each is reproducible from the checked-in
`SGTestData/Expected` sources.

3. **Merlin 32 v1.2 requires `DSK`/`TYP` directives.**  GenMerlin32 emits them when it
   detects v1.2 or later, so with v1.2 installed every Merlin32 output gains a two-line
   header (placed after any leading comment block) that the fixtures do not have.  The
   test suite treats that specific difference as expected and compares the remainder of
   the file, which matches exactly for all 57 cases.

4. **Merlin 32 v1.2 beta 2 cannot assemble `jml [addr]`.**  It reports
   "Impossible to decode address mode for instruction 'jml [Lxxxx]'" and produces no
   output.  This affects 5 cases: `10002-allops-value-65816`, `10012-allops-zero-65816`,
   `20052-branches-and-banks`, `20102-label-dp`, `20222-data-bank`.

5. **Merlin 32 v1.2 exits 0 even when it reports errors**, signalling failure only by not
   writing the output binary.  Anything that judges an assembly run by exit status alone
   will wrongly conclude success; the test harness therefore also requires the output to
   exist and to match the source binary byte-for-byte.

Two further cases (`20032-labels-and-symbols`, `20252-nested-regions`) assemble without
error but to a binary that differs from the reference (for `20032`, offset +000113 is
$dc where $5c is expected).  The same difference appears when assembling the Expected
source directly, so it too is a tool-version discrepancy rather than a generator defect.

### Assemblers verified end to end ###

With all four assemblers installed, the suite runs 228 assemble-and-compare pairs:

| Assembler | Version | Byte-identical |
| --- | --- | --- |
| ACME | 0.97 | 57 / 57 |
| 64tass | 1.59.3120 | 57 / 57 |
| cc65 | 2.19 | 54 / 57 |
| Merlin 32 | 1.2 beta 2 | 50 / 57 |

ACME and 64tass reproduce every case exactly, so they are the cleanest evidence that the
generated sources are correct.  The cc65 and Merlin 32 shortfalls are the tool-version
issues described above; the suite names each one rather than skipping silently.


## What is and is not ported ##

Working today: the engine (analysis, code generation, project files, extension scripts)
and a GUI that can open a project and display it.  `AppController` loads a project, runs
the analyzer, and fills the code, symbol, message and note panes; that path is covered by
`AppControllerTests`, which opens a real example project and asserts a disassembly is
produced.  This is verified headlessly because the CI container has no display.

Supported by both front ends: labels, end-of-line comments, multi-line (long) comments,
notes, undo/redo, save, listing search (find / find next / find previous with a match-case
option), HTML export, VICE label-file generation, and assembly-source generation for all
four cross-assemblers.  Long comments and notes use the same MultiLineComment type the
original application uses, so they round-trip with existing project files.

Not yet ported: operand formatting, project properties, the tools (hex dump, instruction
and ASCII charts, file slicer/concatenator), the visualization editors and the
assemble-and-verify dialog.  Those remain only in the WPF sources, and the corresponding
menu commands report "Not Yet Ported".  Notes and visualizations are not included in HTML
export from the ported front ends; the exporter is invoked with image generation off,
because rendering needs a platform that headless environments do not have.


## Terminal UI ##

`SourceGen.Tui` is a terminal front end for the same engine, built on Terminal.Gui (which
uses the ncurses-backed driver on Unix).  It covers what the graphical application does
today: open a project, browse the listing, edit labels and comments, undo/redo, save, and
generate assembly source.

    dotnet run --project SourceGen.Tui/SourceGen.Tui.csproj [path/to/project.dis65]

Keys: arrows/PgUp/PgDn/Home/End move, Ctrl+L edits the label, Ctrl+; the comment, Ctrl+K
the multi-line comment, Ctrl+N the note, Ctrl+F finds (F3 forward, Shift+F3 backward),
Ctrl+Z/Y undo/redo, Ctrl+S saves, Ctrl+O opens, Ctrl+G generates assembly, Ctrl+Q quits.
Export HTML and Generate Labels are on the File menu.  The menus expose the same commands.

Design note: all behaviour lives in `TuiModel`, which knows nothing about terminals.  The
Terminal.Gui layer in `Program.cs` only wires widgets to it.  That keeps the TUI testable
without a TTY -- `TuiModelTests` drives the model and asserts on the exact text the screen
would show, including column padding, scroll-to-selection and edit results.

Because the terminal has no native file dialog, the TUI prompts for paths as text, and it
takes an optional project path on the command line.
