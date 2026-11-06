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
using System.Diagnostics;
using System.IO;

using CommonUtil;

namespace SourceGen.Tests {
    /// <summary>
    /// Headless runner for the source-generation regression tests.
    ///
    /// This drives the same GenTest logic the GUI's "Source Generation Tests" window uses,
    /// so the engine can be exercised on a build machine with no display.  Test cases whose
    /// cross-assembler is not installed are counted as skipped rather than failed.
    ///
    /// Exit code 0 means every test case generated the expected output.
    /// </summary>
    public static class Program {
        public static int Main(string[] args) {
            bool verbose = Array.IndexOf(args, "-v") >= 0;

            int rtIndex = Array.IndexOf(args, "--roundtrip");
            if (rtIndex >= 0) {
                string root = (rtIndex + 1 < args.Length) ? args[rtIndex + 1] : ".";
                return RunRoundTrip(root);
            }

            string runtimeDir = RuntimeDataAccess.GetDirectory();
            if (runtimeDir == null) {
                Console.Error.WriteLine("Unable to locate the RuntimeData directory.");
                Console.Error.WriteLine("Run this from the build output directory.");
                return 2;
            }
            Console.WriteLine("RuntimeData: " + runtimeDir);

            GenTest genTest = new GenTest();

            // GenTest reports progress through a BackgroundWorker.  We consume the events
            // but stay on the calling thread, which is fine for a console app.
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += (sender, e) => { };
            worker.ProgressChanged += (sender, e) => {
                string msg;
                if (e.UserState is ProgressMessage pm) {
                    msg = pm.Text;
                } else {
                    msg = e.UserState as string;
                }
                if (verbose && msg != null) {
                    Console.Write(msg);
                }
            };

            List<GenTest.GenTestResults> results;
            try {
                results = genTest.Run(worker);
            } catch (Exception ex) {
                Console.Error.WriteLine("Test run threw an exception: " + ex);
                return 2;
            }

            if (results == null) {
                Console.Error.WriteLine("Test run failed to produce results.");
                return 2;
            }

            int genOk = 0, genFail = 0, asmOk = 0, asmFail = 0, asmSkip = 0;
            foreach (GenTest.GenTestResults res in results) {
                if (res.GenerateOkay) {
                    genOk++;
                } else {
                    genFail++;
                }

                bool configured = res.AsmResults != null;
                if (!configured) {
                    asmSkip++;
                } else if (res.AssembleOkay) {
                    asmOk++;
                } else {
                    asmFail++;
                }

                if (!res.GenerateOkay || (configured && !res.AssembleOkay)) {
                    Console.WriteLine("FAIL  {0}  [{1}]", res.FileName, res.AsmId);
                    DumpReport(res);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Cases:    {0}", results.Count);
            Console.WriteLine("Generate: {0} ok, {1} failed", genOk, genFail);
            Console.WriteLine("Assemble: {0} ok, {1} failed, {2} skipped (assembler not configured)",
                asmOk, asmFail, asmSkip);

            // Generation correctness is what this harness is for.  Assembler failures only
            // matter for assemblers that were actually run.
            return (genFail == 0 && asmFail == 0) ? 0 : 1;
        }

        /// <summary>
        /// Verifies project-file JSON compatibility: every .dis65 in the tree must load,
        /// re-serialize to a stable representation, and reproduce the original content.
        ///
        /// The checked-in files are hand-formatted (blank lines between sections), so the
        /// comparison ignores line breaks but is otherwise exact.
        /// </summary>
        private static int RunRoundTrip(string root) {
            List<string> files = new List<string>();
            string[] searchDirs = new string[] {
                Path.Combine(root, "SourceGen", "SGTestData"),
                Path.Combine(root, "SourceGen", "Examples"),
            };
            foreach (string dir in searchDirs) {
                if (Directory.Exists(dir)) {
                    files.AddRange(Directory.GetFiles(dir, "*" + ProjectFile.FILENAME_EXT,
                        SearchOption.AllDirectories));
                }
            }
            files.Sort();

            if (files.Count == 0) {
                Console.Error.WriteLine("No project files found under " + root);
                return 2;
            }

            int loadFail = 0, unstable = 0, differs = 0, noData = 0, contentUpgraded = 0;
            foreach (string path in files) {
              try {
                string original = File.ReadAllText(path);

                DisasmProject proj = new DisasmProject();
                FileLoadReport report = new FileLoadReport(path);
                if (!ProjectFile.DeserializeFromFile(path, proj, out report)) {
                    Console.WriteLine("LOAD FAIL  {0}", path);
                    Console.WriteLine("           {0}", report.Format().Trim());
                    loadFail++;
                    continue;
                }

                // The project JSON records the data length, so the binary has to be attached
                // for serialization to reproduce it.
                byte[] fileData = null;
                string dataPath = path.Substring(0,
                    path.Length - ProjectFile.FILENAME_EXT.Length);
                if (File.Exists(dataPath)) {
                    fileData = File.ReadAllBytes(dataPath);
                    FileLoadReport dataReport = new FileLoadReport(dataPath);
                    proj.SetFileData(fileData, Path.GetFileName(dataPath), ref dataReport);
                }
                if (fileData == null) {
                    Console.WriteLine("NO DATA    {0}", path);
                    noData++;
                    continue;
                }

                string pass1 = SerializableProjectFile1.SerializeProject(proj);

                // Re-load our own output and serialize again: the result must be identical,
                // otherwise saving a project would produce a different file each time.
                DisasmProject proj2 = new DisasmProject();
                FileLoadReport report2 = new FileLoadReport(path);
                string pass1Body = pass1.StartsWith(SerializableProjectFile1.MAGIC)
                    ? pass1.Substring(SerializableProjectFile1.MAGIC.Length) : pass1;
                if (!SerializableProjectFile1.DeserializeProject(pass1Body, proj2, report2)) {
                    Console.WriteLine("RELOAD FAIL {0}", path);
                    Console.WriteLine("            {0}", report2.Format().Trim());
                    loadFail++;
                    continue;
                }
                if (fileData != null) {
                    FileLoadReport dataReport2 = new FileLoadReport(dataPath);
                    proj2.SetFileData(fileData, Path.GetFileName(dataPath), ref dataReport2);
                }
                string pass2 = SerializableProjectFile1.SerializeProject(proj2);

                bool failed = false;
                if (Flatten(pass1) != Flatten(pass2)) {
                    Console.WriteLine("UNSTABLE   {0}", path);
                    unstable++;
                    failed = true;
                }
                if (Comparable(pass1) != Comparable(original)) {
                    Console.WriteLine("DIFFERS    {0}", path);
                    Console.WriteLine("           {0}", DescribeFirstDifference(
                        Comparable(original), Comparable(pass1)));
                    differs++;
                    failed = true;
                } else if (Flatten(pass1) != Flatten(original)) {
                    contentUpgraded++;
                }
                if (!failed) {
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-v") >= 0) {
                        Console.WriteLine("ok         {0}", path);
                    }
                }
              } catch (Exception ex) {
                Console.WriteLine("EXCEPTION  {0}", path);
                Console.WriteLine("           {0}", ex.Message);
                loadFail++;
              }
            }

            Console.WriteLine();
            Console.WriteLine("Project files: {0}", files.Count);
            Console.WriteLine("  load failures:      {0}", loadFail);
            Console.WriteLine("  unstable on resave: {0}", unstable);
            Console.WriteLine("  content differs:    {0}", differs);
            Console.WriteLine("  no data file:       {0}", noData);
            Console.WriteLine("  version upgraded:   {0}", contentUpgraded);

            return (loadFail == 0 && unstable == 0 && differs == 0) ? 0 : 1;
        }

        /// <summary>
        /// Removes line breaks.  The checked-in project files are pretty-printed between
        /// sections; JSON strings cannot contain raw newlines, so this is safe.
        /// </summary>
        private static string Flatten(string text) {
            return text.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        /// <summary>
        /// Flattened text with the content-version field normalised.  A project saved by an
        /// older release is upgraded to the current content version when it is loaded, so
        /// that one field is expected to change and everything else must not.
        /// </summary>
        private static string Comparable(string text) {
            return System.Text.RegularExpressions.Regex.Replace(
                Flatten(text), "\"_ContentVersion\":[0-9]+", "\"_ContentVersion\":*");
        }

        private static string DescribeFirstDifference(string expected, string actual) {
            int limit = Math.Min(expected.Length, actual.Length);
            int i = 0;
            while (i < limit && expected[i] == actual[i]) {
                i++;
            }
            int from = Math.Max(0, i - 40);
            string exp = expected.Substring(from, Math.Min(80, expected.Length - from));
            string act = (i <= actual.Length)
                ? actual.Substring(Math.Min(from, actual.Length),
                    Math.Min(80, actual.Length - Math.Min(from, actual.Length)))
                : string.Empty;
            return string.Format("at char {0}\n              expected: {1}\n              actual  : {2}",
                i, exp, act);
        }

        private static void DumpReport(GenTest.GenTestResults res) {
            if (res.ProjectLoadReport != null) {
                foreach (FileLoadItem item in res.ProjectLoadReport) {
                    Console.WriteLine("      {0}: {1}", item.MsgType, item.Message);
                }
            }
            if (res.AsmResults != null) {
                Console.WriteLine("      assembler exit code {0}", res.AsmResults.ExitCode);
                if (!string.IsNullOrEmpty(res.AsmResults.Stderr)) {
                    Console.WriteLine("      stderr: {0}",
                        res.AsmResults.Stderr.Trim().Replace("\n", "\n              "));
                }
            }
            Debug.WriteLine("failed case: " + res.PathName);
        }
    }
}
