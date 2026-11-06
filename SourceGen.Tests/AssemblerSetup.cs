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

using SourceGen.AsmGen;

namespace SourceGen.Tests {
    /// <summary>
    /// Locates and configures cross-assemblers for the regression tests.
    ///
    /// SourceGen only runs an assembler that has an ExecutablePath configured, and the
    /// regression suite treats an unconfigured assembler as "skipped" rather than failed.
    /// That keeps the suite green on machines without the tools, but it also means the
    /// assemble-and-compare step silently does nothing.  These helpers find what is actually
    /// installed so that step runs whenever it can.
    ///
    /// Discovery order for each tool:
    ///   1. a SOURCEGEN_&lt;ID&gt;_PATH environment variable (full path to the executable,
    ///      or the directory containing it),
    ///   2. the system PATH.
    /// No machine-specific paths are baked in.
    /// </summary>
    public static class AssemblerSetup {
        public static string Cc65EnvironmentVariable {
            get { return "SOURCEGEN_CC65_PATH"; }
        }

        /// <summary>
        /// Configures cc65's assembler if it can be found, so the regression tests exercise
        /// the assemble-and-compare step.  Returns the executable path used, or null.
        /// </summary>
        public static string TryConfigureCc65() {
            string path = FindExecutable(Cc65EnvironmentVariable, "cl65");
            if (path == null) {
                return null;
            }

            // Start from the assembler's own defaults (column widths and so on) and only
            // override the executable location.
            AssemblerConfig config =
                AssemblerInfo.GetAssembler(AssemblerInfo.Id.Cc65).GetDefaultConfig();
            config.ExecutablePath = path;
            AssemblerConfig.SetConfig(AppSettings.Global, AssemblerInfo.Id.Cc65, config);

            // Fail loudly here rather than silently skipping the assemble step later.
            AssemblerVersion version =
                AssemblerInfo.GetAssembler(AssemblerInfo.Id.Cc65).QueryVersion();
            if (version == null) {
                throw new InvalidOperationException(
                    "Found '" + path + "' but could not query its version; the configured " +
                    "path may be wrong.");
            }
            return path;
        }

        /// <summary>
        /// Resolves an executable from an environment variable or the PATH.
        /// </summary>
        /// <param name="envVar">Name of the override variable.</param>
        /// <param name="exeName">Executable file name, without extension.</param>
        /// <returns>Full path, or null if not found.</returns>
        public static string FindExecutable(string envVar, string exeName) {
            string overridePath = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(overridePath)) {
                // Accept either the directory or the executable itself.
                if (Directory.Exists(overridePath)) {
                    string candidate = Path.Combine(overridePath, exeName);
                    if (File.Exists(candidate)) {
                        return candidate;
                    }
                    string exeCandidate = Path.Combine(overridePath, exeName + ".exe");
                    if (File.Exists(exeCandidate)) {
                        return exeCandidate;
                    }
                    return null;
                }
                return File.Exists(overridePath) ? overridePath : null;
            }

            string pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) {
                return null;
            }
            foreach (string dir in pathVar.Split(Path.PathSeparator)) {
                if (string.IsNullOrEmpty(dir)) {
                    continue;
                }
                string candidate = Path.Combine(dir, exeName);
                if (File.Exists(candidate)) {
                    return candidate;
                }
                string exeCandidate = Path.Combine(dir, exeName + ".exe");
                if (File.Exists(exeCandidate)) {
                    return exeCandidate;
                }
            }
            return null;
        }
    }
}
