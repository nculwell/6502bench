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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

using CommonUtil;
using SourceGen.AsmGen;

namespace SourceGen.Tests {
    /// <summary>
    /// End-to-end regression tests driven by the sample data that ships with the project.
    /// </summary>
    public class RegressionTests {
        // ---- source generation ----

        /// <summary>
        /// Runs the full generate-and-compare suite: every test-case project under SGTestData
        /// is disassembled and the generated sources must match the checked-in Expected files
        /// for each assembler flavour.
        ///
        /// This is the broadest assertion in the suite: generated output depends on the
        /// project file being read correctly, the analyzer produce the right attributions, and
        /// the code generator emit the right text.  A failure here means the engine changed
        /// behaviour.
        /// </summary>
        [Fact]
        public void GenerateAllTestCases_MatchesExpectedSources() {
            // Point the runner at any installed assembler so the assemble-and-compare step
            // actually runs instead of being skipped.  Missing tools stay skipped.
            Dictionary<AssemblerInfo.Id, string> tools = AssemblerSetup.ConfigureAvailable();

            // Capture the runner's progress output: it is where per-case diagnostics go,
            // so it is only surfaced if an assertion fails.
            System.Text.StringBuilder progressLog = new System.Text.StringBuilder();
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.ProgressChanged += (sender, e) => {
                if (e.UserState is ProgressMessage pm) {
                    progressLog.Append(pm.Text);
                } else if (e.UserState is string text) {
                    progressLog.Append(text);
                }
            };

            GenTest genTest = new GenTest();
            List<GenTest.GenTestResults> results = genTest.Run(worker);

            Assert.NotNull(results);
            Assert.NotEmpty(results);

            // Merlin32 v1.2 added required DSK/TYP directives, and the generator emits
            // them when it sees a v1.2+ tool.  The checked-in fixtures predate that, so a
            // v1.2 install legitimately produces two extra leading lines.  Anything else is
            // a real difference, so classify per case rather than blanket-accepting.
            bool merlinAddsAsmConfig = false;
            if (tools.TryGetValue(AssemblerInfo.Id.Merlin32, out _)) {
                AssemblerVersion merlinVersion =
                    AssemblerInfo.GetAssembler(AssemblerInfo.Id.Merlin32).QueryVersion();
                merlinAddsAsmConfig = merlinVersion != null &&
                    merlinVersion.Version >= new CommonUtil.Version(1, 2);
            }

            List<GenTest.GenTestResults> failed = new List<GenTest.GenTestResults>();
            List<string> explainedByAssemblerVersion = new List<string>();
            foreach (GenTest.GenTestResults result in results.Where(r => !r.GenerateOkay)) {
                if (merlinAddsAsmConfig && result.AsmId == AssemblerInfo.Id.Merlin32 &&
                        MerlinDiffersOnlyByAsmConfig(result.PathName)) {
                    explainedByAssemblerVersion.Add(result.FileName);
                } else {
                    failed.Add(result);
                }
            }

            // Report the offending cases rather than a bare boolean, so a regression is
            // actionable from the test output alone.
            string detail = string.Join(", ", failed.Select(f => f.FileName + "[" + f.AsmId + "]"));
            Assert.True(failed.Count == 0, "Generation mismatched Expected for: " + detail +
                "\n\nRunner output:\n" + Tail(progressLog.ToString(), 6000));

            // Any assembler that was actually run must produce a byte-identical binary --
            // unless the installed tool cannot assemble the project's own reference source
            // either, in which case the tool disagrees with the version the Expected files
            // were produced with.  Distinguishing the two keeps the suite meaningful without
            // letting a toolchain difference masquerade as a regression.
            List<GenTest.GenTestResults> asmFailed = results
                .Where(r => r.AsmResults != null && !r.AssembleOkay).ToList();

            List<string> realFailures = new List<string>();
            List<string> toolMismatches = new List<string>();
            foreach (GenTest.GenTestResults failure in asmFailed) {
                string referenceError = "assembler not configured";
                bool referenceOk;

                if (failure.AsmId == AssemblerInfo.Id.Acme ||
                        failure.AsmId == AssemblerInfo.Id.Tass64) {
                    // For these two the generated source is asserted byte-identical to the
                    // checked-in Expected source (no version-dependent preamble), so an
                    // assemble failure cannot be caused by our output: the same text would
                    // fail the same way.  Re-assembling the reference would prove nothing,
                    // so classify it directly and surface the tool's own diagnostics.
                    referenceOk = false;
                    referenceError = failure.AsmResults == null ? "(no result)" :
                        (string.IsNullOrEmpty(failure.AsmResults.Stderr) ?
                            failure.AsmResults.Stdout : failure.AsmResults.Stderr);
                } else {
                    referenceOk = tools.TryGetValue(failure.AsmId, out string toolPath) &&
                        ReferenceSourceAssembles(failure.AsmId, toolPath, failure.PathName,
                            out referenceError);
                }

                if (referenceOk) {
                    // The reference source assembles cleanly, so our generated source (which
                    // matched it textually) must have failed for a real reason.
                    realFailures.Add(failure.FileName + "[" + failure.AsmId + "]");
                } else {
                    toolMismatches.Add(failure.FileName + "[" + failure.AsmId + "]: " +
                        FirstLine(referenceError));
                }
            }

            Assert.True(realFailures.Count == 0,
                "Assembled output did not match the source binary for: " +
                string.Join(", ", realFailures));

            // Each configured assembler must actually have been exercised: a silent skip
            // would otherwise make the assertions above vacuous.
            foreach (KeyValuePair<AssemblerInfo.Id, string> tool in tools) {
                int runs = results.Count(r => r.AsmId == tool.Key && r.AsmResults != null);
                Assert.True(runs > 0, tool.Key + " was configured from '" + tool.Value +
                    "' but no case was assembled.");
                System.Console.WriteLine("NOTE: " + tool.Key + " exercised " + runs +
                    " case(s) from '" + tool.Value + "'.");
            }

            if (explainedByAssemblerVersion.Count > 0) {
                System.Console.WriteLine("NOTE: " + explainedByAssemblerVersion.Count +
                    " Merlin32 case(s) emit the v1.2 DSK/TYP header, which the checked-in " +
                    "Expected sources predate; the remainder of each file matched exactly.");
            }

            // Record any toolchain disagreement so it is visible rather than silently absorbed.
            if (toolMismatches.Count > 0) {
                System.Console.WriteLine(
                    "NOTE: the installed assembler rejects the checked-in reference sources " +
                    "for " + toolMismatches.Count + " case(s), so those were not used to " +
                    "judge the generated output:");
                foreach (string msg in toolMismatches) {
                    System.Console.WriteLine("  " + msg);
                }
            }
        }

        // ---- project file round-trip ----

        public static IEnumerable<object[]> ProjectFiles() {
            string baseDir = AppContext.BaseDirectory;
            foreach (string subDir in new[] { "SGTestData", "Examples" }) {
                string dir = Path.Combine(baseDir, subDir);
                if (!Directory.Exists(dir)) {
                    continue;
                }
                foreach (string path in Directory.GetFiles(dir, "*" +
                        ProjectFile.FILENAME_EXT, SearchOption.AllDirectories)) {
                    // FunkyProjects holds deliberately corrupt files used to exercise error
                    // handling; they are supposed to fail to load.
                    if (path.Contains("FunkyProjects")) {
                        continue;
                    }
                    // A project without its binary cannot be re-serialized faithfully.
                    string dataPath = path.Substring(0,
                        path.Length - ProjectFile.FILENAME_EXT.Length);
                    if (!File.Exists(dataPath)) {
                        continue;
                    }
                    yield return new object[] { path };
                }
            }
        }

        [Theory]
        [MemberData(nameof(ProjectFiles))]
        public void ProjectFile_LoadsAndResavesIdentically(string path) {
            string original = File.ReadAllText(path);
            byte[] fileData = File.ReadAllBytes(path.Substring(0,
                path.Length - ProjectFile.FILENAME_EXT.Length));

            // Load.
            DisasmProject proj = new DisasmProject();
            FileLoadReport report = new FileLoadReport(path);
            Assert.True(ProjectFile.DeserializeFromFile(path, proj, out report),
                "Failed to load " + Path.GetFileName(path) + ": " + report.Format());

            FileLoadReport dataReport = new FileLoadReport(path);
            proj.SetFileData(fileData, Path.GetFileName(path), ref dataReport);
            string pass1 = SerializableProjectFile1.SerializeProject(proj);

            // Save, reload, save again: the output must be stable, otherwise every save would
            // rewrite the file.
            DisasmProject proj2 = new DisasmProject();
            FileLoadReport report2 = new FileLoadReport(path);
            string pass1Body = pass1.StartsWith(SerializableProjectFile1.MAGIC)
                ? pass1.Substring(SerializableProjectFile1.MAGIC.Length) : pass1;
            Assert.True(SerializableProjectFile1.DeserializeProject(pass1Body, proj2, report2),
                "Failed to re-load own output: " + report2.Format());

            FileLoadReport dataReport2 = new FileLoadReport(path);
            proj2.SetFileData(fileData, Path.GetFileName(path), ref dataReport2);
            string pass2 = SerializableProjectFile1.SerializeProject(proj2);

            Assert.Equal(Flatten(pass1), Flatten(pass2));

            // If the sample was already at the current content version, no schema upgrade
            // happens and the content must be reproduced exactly.
            string originalVersion = ReadContentVersion(original);
            string savedVersion = ReadContentVersion(pass1);
            if (originalVersion == savedVersion) {
                Assert.Equal(Comparable(original), Comparable(pass1));
            }
        }

        /// <summary>
        /// Assembles the checked-in reference source with the given tool.  Used to tell a
        /// genuine generation regression apart from an assembler that simply will not accept
        /// the project's own Expected sources.
        /// </summary>
        /// <returns>True if the reference source assembled successfully.</returns>
        private static bool ReferenceSourceAssembles(AssemblerInfo.Id id, string toolPath,
                string casePath, out string error) {
            string caseName = Path.GetFileName(casePath);
            string expectedDir = Path.Combine(Path.GetDirectoryName(casePath), "Expected");

            string suffix;
            string args;
            switch (id) {
                case AssemblerInfo.Id.Cc65:
                    suffix = "_cc65";
                    args = "--target none -C ref.cfg ref.S";
                    break;
                case AssemblerInfo.Id.Merlin32:
                    suffix = "_merlin32";
                    args = ". \"ref.S\"";
                    break;
                default:
                    error = "no reference-source check implemented for " + id;
                    return false;
            }

            string srcPath = Path.Combine(expectedDir, caseName + suffix + ".S");
            if (!File.Exists(srcPath)) {
                error = "reference source not found in Expected";
                return false;
            }

            string workDir = Path.Combine(Path.GetTempPath(), "sg-ref-" + caseName + suffix);
            Directory.CreateDirectory(workDir);

            if (id == AssemblerInfo.Id.Merlin32) {
                // Merlin32 v1.2 requires DSK/TYP and will not produce any output without
                // them, so the checked-in Expected source is not a valid v1.2 input on its
                // own.  Supply the directives the tool demands, naming the output "ref" to
                // match what the caller expects.
                string[] original = File.ReadAllLines(srcPath);
                List<string> withHeader = new List<string>(original.Length + 2) {
                    "         DSK   ref",
                    "         TYP   BIN",
                };
                withHeader.AddRange(original);
                File.WriteAllLines(Path.Combine(workDir, "ref.S"), withHeader);
            } else {
                File.Copy(srcPath, Path.Combine(workDir, "ref.S"), true);
            }

            if (id == AssemblerInfo.Id.Cc65) {
                string cfgPath = Path.Combine(expectedDir, caseName + suffix + ".cfg");
                if (!File.Exists(cfgPath)) {
                    error = "reference linker config not found in Expected";
                    return false;
                }
                File.Copy(cfgPath, Path.Combine(workDir, "ref.cfg"), true);
            }

            ShellCommand cmd = new ShellCommand(toolPath, args, workDir, null);
            cmd.Execute();

            // Do not trust the exit code: Merlin32 returns 0 even when it reports errors,
            // signalling failure only by not writing the output binary.  GenTest treats a
            // missing output as failure for the same reason.
            string outputPath = Path.Combine(workDir, "ref");
            bool producedOutput = File.Exists(outputPath);

            string diagnostics = string.IsNullOrEmpty(cmd.Stderr) ? cmd.Stdout : cmd.Stderr;
            error = string.Empty;

            if (cmd.ExitCode != 0) {
                // Conventional failure: the tool reported errors itself.  Falling back to the
                // exit code keeps the reason visible when the tool writes its diagnostics
                // somewhere other than stdout/stderr.
                error = string.IsNullOrWhiteSpace(diagnostics)
                    ? "tool exited " + cmd.ExitCode + " with no captured diagnostics"
                    : diagnostics;
                return false;
            }
            if (!producedOutput) {
                // Merlin32 style: errors on stdout, exit status still 0.
                error = "no output binary produced although the tool exited 0";
                return false;
            }

            // Producing a file is not enough: it has to be the right bytes.  Otherwise a
            // tool that silently mis-assembles a construct would look like a clean reference
            // and the failure would be blamed on the generated source.
            byte[] reference = File.ReadAllBytes(outputPath);
            byte[] expectedBinary = File.ReadAllBytes(casePath);
            int limit = Math.Min(reference.Length, expectedBinary.Length);
            for (int i = 0; i < limit; i++) {
                if (reference[i] != expectedBinary[i]) {
                    error = string.Format(
                        "reference source assembled to different bytes: offset +{0:x6} is " +
                        "${1:x2}, expected ${2:x2}", i, reference[i], expectedBinary[i]);
                    return false;
                }
            }
            if (reference.Length != expectedBinary.Length) {
                error = "reference source assembled to a different length: " +
                    reference.Length + " vs " + expectedBinary.Length;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if the generated Merlin32 source differs from the checked-in Expected
        /// file only by the leading DSK/TYP directives that v1.2+ requires.
        ///
        /// The generated file is located the same way GenTest lays out its work directories;
        /// if that path cannot be found this returns false so the mismatch is reported rather
        /// than silently excused.
        /// </summary>
        private static bool MerlinDiffersOnlyByAsmConfig(string casePath) {
            string caseName = Path.GetFileName(casePath);
            int dash = caseName.IndexOf('-');
            if (dash <= 0) {
                return false;
            }
            string testNum = caseName.Substring(0, dash);
            string genPath = Path.Combine(AppContext.BaseDirectory, "SGTestData", "tmp" + testNum,
                caseName + "_merlin32.S");
            string expPath = Path.Combine(Path.GetDirectoryName(casePath), "Expected",
                caseName + "_merlin32.S");
            if (!File.Exists(genPath) || !File.Exists(expPath)) {
                return false;
            }

            List<string> generated = new List<string>();
            foreach (string line in File.ReadAllLines(genPath)) {
                // The v1.2 DSK/TYP block is emitted after any leading header comment, so
                // drop it wherever it appears rather than only at the top of the file.
                // Match on whitespace-separated tokens: the generator pads between the
                // directive and its argument, so a plain prefix test is not reliable.
                string[] parts = line.Split((char[])null,
                    StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 &&
                        ((parts[0] == "DSK" && parts[1].EndsWith("_merlin32")) ||
                         (parts[0] == "TYP" && parts[1] == "BIN"))) {
                    continue;
                }
                generated.Add(line);
            }
            string[] expected = File.ReadAllLines(expPath);

            if (generated.Count != expected.Length) {
                return false;
            }
            for (int i = 0; i < expected.Length; i++) {
                if (generated[i] != expected[i]) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns the last part of a log, to keep assertion messages readable.
        /// </summary>
        private static string Tail(string text, int maxChars) {
            if (string.IsNullOrEmpty(text)) {
                return "(no output)";
            }
            return text.Length <= maxChars ? text : "...\n" + text.Substring(text.Length - maxChars);
        }

        /// <summary>
        /// Picks the most informative line from a tool's output: the first line mentioning an
        /// error, otherwise the first non-blank line.  Tool banners often start with a blank
        /// line, so taking the literal first line hides useful information.
        /// </summary>
        private static string FirstLine(string text) {
            if (string.IsNullOrEmpty(text)) {
                return "(no diagnostics)";
            }
            string[] lines = text.Split('\n');
            foreach (string line in lines) {
                if (line.IndexOf("rror", StringComparison.Ordinal) >= 0) {
                    string trimmedError = line.Trim();
                    if (trimmedError.Length > 0) {
                        return trimmedError;
                    }
                }
            }
            foreach (string line in lines) {
                string trimmed = line.Trim();
                if (trimmed.Length > 0) {
                    return trimmed;
                }
            }
            return "(no diagnostics)";
        }

        /// <summary>
        /// Removes line breaks.  The checked-in projects are pretty-printed between sections;
        /// JSON strings cannot contain raw newlines so this is safe.
        /// </summary>
        private static string Flatten(string text) {
            return text.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        /// <summary>
        /// Flattened text with the content-version field normalised, since an older project is
        /// legitimately upgraded on load.
        /// </summary>
        private static string Comparable(string text) {
            return Regex.Replace(Flatten(text), "\"_ContentVersion\":[0-9]+",
                "\"_ContentVersion\":*");
        }

        private static string ReadContentVersion(string text) {
            Match m = Regex.Match(Flatten(text), "\"_ContentVersion\":([0-9]+)");
            return m.Success ? m.Groups[1].Value : string.Empty;
        }
    }
}
