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
        /// <summary>
        /// Environment variable that can point at cc65's cl65.
        /// </summary>
        public const string Cc65EnvVar = "SOURCEGEN_CC65_PATH";

        /// <summary>
        /// Environment variable that can point at Merlin32.
        /// </summary>
        public const string Merlin32EnvVar = "SOURCEGEN_MERLIN32_PATH";

        /// <summary>
        /// Environment variable that can point at ACME.
        /// </summary>
        public const string AcmeEnvVar = "SOURCEGEN_ACME_PATH";

        /// <summary>
        /// Environment variable that can point at 64tass.
        /// </summary>
        public const string Tass64EnvVar = "SOURCEGEN_64TASS_PATH";

        /// <summary>
        /// Configures every assembler that can be found.
        /// </summary>
        /// <returns>Map of assembler id to the executable path that was configured.</returns>
        public static Dictionary<AssemblerInfo.Id, string> ConfigureAvailable() {
            Dictionary<AssemblerInfo.Id, string> configured =
                new Dictionary<AssemblerInfo.Id, string>();

            string cc65 = TryConfigure(AssemblerInfo.Id.Cc65, Cc65EnvVar, "cl65");
            if (cc65 != null) {
                configured[AssemblerInfo.Id.Cc65] = cc65;
            }
            string merlin = TryConfigure(AssemblerInfo.Id.Merlin32, Merlin32EnvVar,
                "Merlin32");
            if (merlin != null) {
                configured[AssemblerInfo.Id.Merlin32] = merlin;
            }
            string acme = TryConfigure(AssemblerInfo.Id.Acme, AcmeEnvVar, "acme");
            if (acme != null) {
                configured[AssemblerInfo.Id.Acme] = acme;
            }
            string tass64 = TryConfigure(AssemblerInfo.Id.Tass64, Tass64EnvVar, "64tass");
            if (tass64 != null) {
                configured[AssemblerInfo.Id.Tass64] = tass64;
            }
            return configured;
        }

        /// <summary>
        /// Configures one assembler.  Returns the executable path used, or null if the tool
        /// was not found.
        /// </summary>
        private static string TryConfigure(AssemblerInfo.Id id, string envVar,
                string exeName) {
            string path = FindExecutable(envVar, exeName);
            if (path == null) {
                return null;
            }

            // Start from the assembler's own defaults (column widths and so on) and only
            // override the executable location.
            IAssembler asm = AssemblerInfo.GetAssembler(id);
            AssemblerConfig config = asm.GetDefaultConfig();
            config.ExecutablePath = path;
            AssemblerConfig.SetConfig(AppSettings.Global, id, config);

            // Fail loudly here rather than silently skipping the assemble step later.
            AssemblerVersion version = asm.QueryVersion();
            if (version == null) {
                throw new InvalidOperationException(
                    "Found '" + path + "' for " + id + " but could not query its version; " +
                    "the configured path may be wrong or the tool may be unusable.");
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
                return ResolveCandidate(overridePath, exeName);
            }

            string pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) {
                return null;
            }
            foreach (string dir in pathVar.Split(Path.PathSeparator)) {
                if (string.IsNullOrEmpty(dir)) {
                    continue;
                }
                string candidate = ResolveCandidate(dir, exeName);
                if (candidate != null) {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>
        /// Interprets a configured path as either a directory or an executable.
        /// </summary>
        private static string ResolveCandidate(string path, string exeName) {
            if (Directory.Exists(path)) {
                string candidate = Path.Combine(path, exeName);
                if (File.Exists(candidate) && IsExecutable(candidate)) {
                    return candidate;
                }
                string exeCandidate = Path.Combine(path, exeName + ".exe");
                return File.Exists(exeCandidate) ? exeCandidate : null;
            }
            if (File.Exists(path)) {
                // Allow pointing directly at the executable, whichever name it has.
                return IsExecutable(path) ? path : null;
            }
            return null;
        }

        /// <summary>
        /// Reports whether a file has the user-execute permission bit.  On Windows this is
        /// meaningless, so only apply the check on Unix-like systems.
        /// </summary>
        private static bool IsExecutable(string path) {
            if (OperatingSystem.IsWindows()) {
                return true;
            }
            try {
                UnixFileMode mode = File.GetUnixFileMode(path);
                return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherExecute)) != 0;
            } catch (Exception) {
                return true;        // platform without mode support; assume usable
            }
        }
    }
}
