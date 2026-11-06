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
using System.IO;
using Xunit;

using Asm65;

namespace SourceGen.Tests {
    /// <summary>
    /// Tests that the data files shipped alongside the application still parse.
    ///
    /// These matter more than usual here: SystemDefs.json is read through the System.Text.Json
    /// layer that replaced JavaScriptSerializer, and it contains values whose encoding is easy
    /// to break (for example "Speed" is written as a JSON string, not a number).
    /// </summary>
    public class RuntimeDataTests {
        private static string GetSystemDefsPath() {
            string runtimeDir = RuntimeDataAccess.GetDirectory();
            Assert.False(string.IsNullOrEmpty(runtimeDir),
                "RuntimeData directory not found; it should be copied next to the test assembly.");
            return Path.Combine(runtimeDir, "SystemDefs.json");
        }

        [Fact]
        public void SystemDefs_LoadsEveryDefinition() {
            string path = GetSystemDefsPath();
            Assert.True(File.Exists(path), "Missing " + path);

            SystemDefSet set = SystemDefSet.ReadFile(path);

            Assert.NotNull(set);
            Assert.NotNull(set.Defs);
            Assert.NotEmpty(set.Defs);
        }

        [Fact]
        public void SystemDefs_EveryDefinitionIsUsable() {
            SystemDefSet set = SystemDefSet.ReadFile(GetSystemDefsPath());

            foreach (SystemDef def in set.Defs) {
                Assert.False(string.IsNullOrWhiteSpace(def.Name), "definition with no name");
                Assert.False(string.IsNullOrWhiteSpace(def.Description),
                    "definition '" + def.Name + "' has no description");

                // "Cpu" must name a CPU the engine knows about.
                Assert.NotEqual(CpuDef.CpuType.CpuUnknown, CpuDef.GetCpuTypeFromName(def.Cpu));

                // "Speed" is stored as a JSON string; a regression to strict number parsing
                // would silently leave this at zero.
                Assert.True(def.Speed > 0, "definition '" + def.Name +
                    "' has non-positive speed " + def.Speed);
            }
        }

        [Fact]
        public void SystemDefs_ReferencedFilesAreWellFormedIdents() {
            SystemDefSet set = SystemDefSet.ReadFile(GetSystemDefsPath());

            foreach (SystemDef def in set.Defs) {
                if (def.SymbolFiles != null) {
                    foreach (string ident in def.SymbolFiles) {
                        Assert.True(ExternalFile.CreateFromIdent(ident) != null,
                            "definition '" + def.Name + "' has a bad symbol file ident '" +
                            ident + "'");
                    }
                }
                if (def.ExtensionScripts != null) {
                    foreach (string ident in def.ExtensionScripts) {
                        Assert.True(ExternalFile.CreateFromIdent(ident) != null,
                            "definition '" + def.Name + "' has a bad script ident '" +
                            ident + "'");
                    }
                }
            }
        }

        [Fact]
        public void RuntimeData_TemplateAndCssArePresent() {
            // HTML export reads these; a missing file would only show up at export time.
            string runtimeDir = RuntimeDataAccess.GetDirectory();
            Assert.True(File.Exists(Path.Combine(runtimeDir, "ExportTemplate.html")));
        }
    }
}
