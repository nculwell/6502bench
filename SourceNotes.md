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

    dotnet build SourceGen.App/SourceGen.App.csproj
    dotnet test SourceGen.Tests/SourceGen.Tests.csproj

`WorkBench.sln` has not been updated for the new projects yet; build per-project.

### Running the assembler regression step ###

`SourceGen.Tests` drives the same generate-and-assemble suite as the GUI's "Source
Generation Tests" window.  Generating sources always runs; the assemble-and-compare step
only runs for assemblers that are configured.  The tests discover cross-assemblers from
the environment so no machine-specific paths are compiled in:

    SOURCEGEN_CC65_PATH=/path/to/cc65/bin dotnet test SourceGen.Tests/SourceGen.Tests.csproj

The variable may name either the directory containing `cl65` or the executable itself.
Other assemblers can be added the same way as support is wired up.

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
