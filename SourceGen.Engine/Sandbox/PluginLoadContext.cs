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
using System.Reflection;
using System.Runtime.Loader;

namespace SourceGen.Sandbox {
    /// <summary>
    /// Collectible load context for compiled extension scripts.
    ///
    /// This replaces the per-project AppDomain.  The important property is that the context
    /// can be unloaded, so the script DLLs are released when a project is closed.
    ///
    /// Shared contract assemblies (PluginCommon, CommonUtil, Asm65) must NOT be loaded into
    /// this context, or the IPlugin instances would be of a different type than the ones the
    /// host expects.  Load() returns null for everything, which hands resolution back to the
    /// default context, where the host already has them loaded.
    /// </summary>
    public class PluginLoadContext : AssemblyLoadContext {
        private readonly Dictionary<string, Assembly> mLoaded =
            new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        public PluginLoadContext() : base("PluginLoadContext", isCollectible: true) {
        }

        /// <summary>
        /// Loads a compiled script assembly into this context.
        /// </summary>
        /// <param name="dllPath">Full path to the script DLL.</param>
        /// <returns>The loaded assembly.</returns>
        public Assembly LoadScriptAssembly(string dllPath) {
            Assembly asm = LoadFromAssemblyPath(dllPath);
            mLoaded[dllPath] = asm;
            return asm;
        }

        protected override Assembly Load(AssemblyName assemblyName) {
            // Defer to the default context so the plugin sees the same PluginCommon /
            // CommonUtil / Asm65 types as the host.  Returning null is what does that.
            return null;
        }
    }
}
