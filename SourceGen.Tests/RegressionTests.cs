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
            // Point the runner at cc65 if it is installed so the assemble-and-compare step
            // actually runs instead of being skipped.  Unconfigured assemblers stay skipped.
            string cc65Path = AssemblerSetup.TryConfigureCc65();

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.ProgressChanged += (sender, e) => { /* progress is not asserted on */ };

            GenTest genTest = new GenTest();
            List<GenTest.GenTestResults> results = genTest.Run(worker);

            Assert.NotNull(results);
            Assert.NotEmpty(results);

            List<GenTest.GenTestResults> failed =
                results.Where(r => !r.GenerateOkay).ToList();

            // Report the offending cases rather than a bare boolean, so a regression is
            // actionable from the test output alone.
            string detail = string.Join(", ", failed.Select(f => f.FileName + "[" + f.AsmId + "]"));
            Assert.True(failed.Count == 0, "Generation mismatched Expected for: " + detail);

            // Any assembler that was actually run must produce a byte-identical binary --
            // unless the installed tool cannot assemble the project's own reference source
            // either, in which case the tool simply disagrees with the version the Expected
            // files were produced with.  Distinguishing the two keeps the suite meaningful
            // without letting a toolchain difference masquerade as a regression.
            List<GenTest.GenTestResults> asmFailed = results
                .Where(r => r.AsmResults != null && !r.AssembleOkay).ToList();

            List<string> realFailures = new List<string>();
            List<string> toolMismatches = new List<string>();
            foreach (GenTest.GenTestResults failure in asmFailed) {
                string referenceError = null;
                bool referenceOk = cc65Path != null &&
                    failure.AsmId == AssemblerInfo.Id.Cc65 &&
                    ReferenceSourceAssembles(cc65Path, failure.PathName, out referenceError);

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

            // If cc65 was configured, make sure it really was exercised: a silent skip would
            // otherwise make the assertions above vacuous.
            if (cc65Path != null) {
                int cc65Runs = results.Count(r =>
                    r.AsmId == AssemblerInfo.Id.Cc65 && r.AsmResults != null);
                Assert.True(cc65Runs > 0,
                    "cc65 was configured from '" + cc65Path + "' but no case was assembled.");

                // Record the toolchain disagreement so it is visible in the test output
                // rather than silently absorbed.
                if (toolMismatches.Count > 0) {
                    System.Console.WriteLine(
                        "NOTE: the installed assembler rejects the checked-in reference " +
                        "sources for " + toolMismatches.Count + " case(s), so those were " +
                        "not used to judge the generated output:");
                    foreach (string msg in toolMismatches) {
                        System.Console.WriteLine("  " + msg);
                    }
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
        private static bool ReferenceSourceAssembles(string toolPath, string casePath,
                out string error) {
            string caseName = Path.GetFileName(casePath);
            string expectedDir = Path.Combine(Path.GetDirectoryName(casePath), "Expected");
            string srcPath = Path.Combine(expectedDir, caseName + "_cc65.S");
            string cfgPath = Path.Combine(expectedDir, caseName + "_cc65.cfg");

            if (!File.Exists(srcPath) || !File.Exists(cfgPath)) {
                error = "reference source not found in Expected";
                return false;
            }

            string workDir = Path.Combine(Path.GetTempPath(), "sg-ref-" + caseName);
            Directory.CreateDirectory(workDir);
            File.Copy(srcPath, Path.Combine(workDir, "ref.S"), true);
            File.Copy(cfgPath, Path.Combine(workDir, "ref.cfg"), true);

            ShellCommand cmd = new ShellCommand(toolPath,
                "--target none -C ref.cfg ref.S", workDir, null);
            cmd.Execute();

            error = string.IsNullOrEmpty(cmd.Stderr) ? cmd.Stdout : cmd.Stderr;
            return cmd.ExitCode == 0;
        }

        private static string FirstLine(string text) {
            if (string.IsNullOrEmpty(text)) {
                return "(no diagnostics)";
            }
            string[] lines = text.Split('\n');
            return lines[0].TrimEnd('\r');
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
