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
using System.Diagnostics;
using System.Text;

using CommonUtil;
using PluginCommon;

namespace SourceGen.Sandbox {
    /// <summary>
    /// Maintains a collection of IPlugin instances.
    ///
    /// History: extension scripts used to be loaded into a separate AppDomain so that they
    /// could be unloaded and so that code-access security could be applied.  Neither the
    /// AppDomain API nor CAS exists on .NET Core, so scripts are now compiled with Roslyn
    /// and loaded into a collectible AssemblyLoadContext.  Unloadability is preserved;
    /// the hard security boundary is not.  Scripts run with the privileges of the process.
    ///
    /// One of these will be instantiated when the DisasmProject is created.
    /// </summary>
    public class ScriptManager {
        public const string FILENAME_EXT = ".cs";
        public static readonly string FILENAME_FILTER = Res.Strings.FILE_FILTER_CS;

        /// <summary>
        /// Retained for source compatibility; the keep-alive hack existed to work around
        /// AppDomain lease expiry, which no longer applies.
        /// </summary>
        public static bool UseKeepAliveHack { get; set; }

        /// <summary>
        /// Always true: plugins run in-process now.
        /// </summary>
        public bool UseMainAppDomain { get { return true; } }

        /// <summary>
        /// Collection of loaded plugins, keyed by script identifier.
        /// </summary>
        private Dictionary<string, IPlugin> mActivePlugins;

        /// <summary>
        /// Load context holding the compiled script assemblies.  Collectible, so that
        /// discarding a project actually releases the script DLLs.
        /// </summary>
        private PluginLoadContext mLoadContext;

        /// <summary>
        /// Reference to project, from which we can get the file data and project path name.
        /// </summary>
        private DisasmProject mProject;

        private class LoadedPluginPath {
            public string ScriptIdent { get; private set; }
            public string DllPath { get; private set; }

            public LoadedPluginPath(string scriptIdent, string dllPath) {
                ScriptIdent = scriptIdent;
                DllPath = dllPath;
            }
        }

        /// <summary>
        /// List of paths to loaded plugins.  Used if we need to "reboot" the sandbox.
        /// </summary>
        private List<LoadedPluginPath> mLoadedPlugins = new List<LoadedPluginPath>();


        /// <summary>
        /// Constructor.
        /// </summary>
        public ScriptManager(DisasmProject proj) {
            mProject = proj;
            mActivePlugins = new Dictionary<string, IPlugin>();
            mLoadContext = new PluginLoadContext();
        }

        /// <summary>
        /// Cleans up, unloading the extension-script assemblies.  Do not continue to use
        /// the object after calling this.
        /// </summary>
        public void Cleanup() {
            UnloadScriptAssemblies();
            mActivePlugins = null;
            mProject = null;
        }

        /// <summary>
        /// Clears the list of plugins.  This does not unload assemblies.  Call this when
        /// the list of extension scripts configured into the project has changed.
        /// </summary>
        public void Clear() {
            mActivePlugins.Clear();
            mLoadedPlugins.Clear();
        }

        /// <summary>
        /// Unloads the script assemblies and prepares a fresh load context.
        /// </summary>
        private void UnloadScriptAssemblies() {
            mActivePlugins.Clear();
            mLoadContext.Unload();
            mLoadContext = new PluginLoadContext();
        }

        /// <summary>
        /// Attempts to load the specified plugin.  If the plugin is already loaded, this
        /// does nothing.  If not, the assembly is loaded and an instance is created.
        /// </summary>
        /// <param name="scriptIdent">Script identifier.</param>
        /// <param name="report">Report with errors and warnings.</param>
        /// <returns>True on success.</returns>
        public bool LoadPlugin(string scriptIdent, out FileLoadReport report) {
            // Make sure the most recent version is compiled.
            string dllPath = PluginDllCache.GenerateScriptDll(scriptIdent,
                mProject.ProjectPathName, out report);
            if (dllPath == null) {
                return false;
            }

            if (mActivePlugins.TryGetValue(scriptIdent, out IPlugin plugin)) {
                return true;
            }

            System.Reflection.Assembly asm = mLoadContext.LoadScriptAssembly(dllPath);
            plugin = PluginDllCache.ConstructIPlugin(asm);
            if (plugin == null) {
                report.Add(FileLoadItem.Type.Error, "Failed loading plugin: no IPlugin found");
                return false;
            }
            mActivePlugins.Add(scriptIdent, plugin);
            mLoadedPlugins.Add(new LoadedPluginPath(scriptIdent, dllPath));
            report = new FileLoadReport(dllPath);       // empty report
            return true;
        }

        /// <summary>
        /// Discards the loaded script assemblies, then reloads all of the previously active
        /// plugins.
        /// </summary>
        /// <returns>True if no problems were encountered.</returns>
        public bool RebootSandbox() {
            Debug.WriteLine("Rebooting sandbox...");

            List<LoadedPluginPath> reload = new List<LoadedPluginPath>(mLoadedPlugins);
            UnloadScriptAssemblies();

            // Force the unloaded context to be collected before we reload, so the new
            // assemblies are actually distinct from the old ones.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            bool failed = false;
            foreach (LoadedPluginPath lpp in reload) {
                if (!LoadPlugin(lpp.ScriptIdent, out FileLoadReport report)) {
                    Debug.WriteLine("Failed to reopen '" + lpp.DllPath + "'");
                    failed = true;
                    // continue on to the next one
                } else {
                    Debug.WriteLine("  Reloaded " + lpp.ScriptIdent);
                }
            }
            return failed;
        }

        public IPlugin GetInstance(string scriptIdent) {
            if (mActivePlugins.TryGetValue(scriptIdent, out IPlugin plugin)) {
                return plugin;
            }
            Debug.Assert(false);
            return null;
        }

        /// <summary>
        /// Generates a list of references to instances of loaded plugins.
        /// </summary>
        /// <returns>Newly-created list of plugin references.</returns>
        public List<IPlugin> GetAllInstances() {
            List<IPlugin> list = new List<IPlugin>(mActivePlugins.Count);
            foreach (KeyValuePair<string, IPlugin> kvp in mActivePlugins) {
                list.Add(kvp.Value);
            }
            return list;
        }

        /// <summary>
        /// Prepares all active scripts for action.
        /// </summary>
        /// <param name="appRef">Reference to object providing app services.</param>
        public void PrepareScripts(IApplication appRef) {
            List<PlSymbol> plSyms = GeneratePlSymbolList();

            AddressTranslate addrTrans = new AddressTranslate(mProject.AddrMap);
            foreach (KeyValuePair<string, IPlugin> kvp in mActivePlugins) {
                IPlugin ipl = kvp.Value;
                ipl.Prepare(appRef, mProject.FileData, addrTrans);
                if (ipl is IPlugin_SymbolList) {
                    ((IPlugin_SymbolList)ipl).UpdateSymbolList(plSyms);
                }
            }
        }

        /// <summary>
        /// Puts scripts back to sleep.
        /// </summary>
        public void UnprepareScripts() {
            foreach (KeyValuePair<string, IPlugin> kvp in mActivePlugins) {
                IPlugin ipl = kvp.Value;
                ipl.Unprepare();
            }
        }


        /// <summary>
        /// Returns true if any of the plugins report that the before or after label is
        /// significant.
        /// </summary>
        /// <remarks>
        /// This is called when a label is edited, so DisasmProject can decide whether it
        /// needs to re-run the code analyzer.
        /// </remarks>
        public bool IsLabelSignificant(Symbol before, Symbol after) {
            string labelBefore = (before == null) ? string.Empty : before.Label;
            string labelAfter = (after == null) ? string.Empty : after.Label;
            foreach (KeyValuePair<string, IPlugin> kvp in mActivePlugins) {
                IPlugin ipl = kvp.Value;
                if (ipl is IPlugin_SymbolList &&
                        ((IPlugin_SymbolList)ipl).IsLabelSignificant(labelBefore,
                            labelAfter)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gathers a list of symbols from the project's symbol table.
        /// </summary>
        /// <remarks>
        /// Remember that we need to set this up before code analysis runs, so many of the
        /// secondary data structures (like Anattribs) won't be available.
        /// </remarks>
        private List<PlSymbol> GeneratePlSymbolList() {
            List<PlSymbol> plSymbols = new List<PlSymbol>();
            SymbolTable symTab = mProject.SymbolTable;

            // UserLabels maps offset to Symbol.  Create the reverse mapping.
            Dictionary<Symbol, int> symbolOffsets =
                new Dictionary<Symbol, int>(mProject.UserLabels.Count);
            foreach (KeyValuePair<int, Symbol> kvp in mProject.UserLabels) {
                symbolOffsets[kvp.Value] = kvp.Key;
            }

            // Add in the address region pre-labels.
            IEnumerator<AddressMap.AddressChange> addrIter = mProject.AddrMap.AddressChangeIterator;
            while (addrIter.MoveNext()) {
                AddressMap.AddressChange change = addrIter.Current;
                if (!change.IsStart) {
                    continue;
                }
                if (change.Region.HasValidPreLabel) {
                    Symbol newSym = new Symbol(change.Region.PreLabel,
                        change.Region.PreLabelAddress, Symbol.Source.AddrPreLabel,
                        Symbol.Type.ExternalAddr, Symbol.LabelAnnotation.None);
                    symbolOffsets[newSym] = change.Region.Offset;
                }
            }

            foreach (Symbol sym in symTab) {
                PlSymbol.Source plsSource;
                int symOff, offset = -1;
                switch (sym.SymbolSource) {
                    case Symbol.Source.User:
                        plsSource = PlSymbol.Source.User;
                        if (symbolOffsets.TryGetValue(sym, out symOff)) {
                            offset = symOff;
                        }
                        break;
                    case Symbol.Source.AddrPreLabel:
                        plsSource = PlSymbol.Source.AddrPreLabel;
                        if (symbolOffsets.TryGetValue(sym, out symOff)) {
                            offset = symOff;
                        }
                        break;
                    case Symbol.Source.Project:
                        plsSource = PlSymbol.Source.Project;
                        break;
                    case Symbol.Source.Platform:
                        plsSource = PlSymbol.Source.Platform;
                        break;
                    case Symbol.Source.Auto:
                    case Symbol.Source.Variable:
                        // don't forward these to plugins
                        continue;
                    default:
                        Debug.Assert(false);
                        continue;
                }
                PlSymbol.Type plsType;
                switch (sym.SymbolType) {
                    case Symbol.Type.NonUniqueLocalAddr:
                        // don't forward these to plugins
                        continue;
                    case Symbol.Type.LocalOrGlobalAddr:
                    case Symbol.Type.GlobalAddr:
                    case Symbol.Type.GlobalAddrExport:
                    case Symbol.Type.ExternalAddr:
                        plsType = PlSymbol.Type.Address;
                        break;
                    case Symbol.Type.Constant:
                        plsType = PlSymbol.Type.Constant;
                        break;
                    default:
                        Debug.Assert(false);
                        continue;
                }

                int width = -1;
                string tag = string.Empty;
                if (sym is DefSymbol) {
                    DefSymbol defSym = sym as DefSymbol;
                    width = defSym.DataDescriptor.Length;
                    tag = defSym.Tag;
                }

                plSymbols.Add(new PlSymbol(sym.Label, sym.Value, width, plsSource, plsType, tag,
                    offset));
            }

            return plSymbols;
        }

        /// <summary>
        /// Returns a list of loaded plugins.  Callers should not retain this list, as the
        /// set can change due to user activity.
        /// </summary>
        public Dictionary<string, IPlugin> GetActivePlugins() {
            // copy the contents
            Dictionary<string, IPlugin> pdict = new Dictionary<string, IPlugin>();
            foreach (KeyValuePair<string, IPlugin> kvp in mActivePlugins) {
                pdict.Add(kvp.Key, kvp.Value);
            }
            return pdict;
        }

        /// <summary>
        /// For debugging purposes, get some information about the currently loaded
        /// extension scripts.
        /// </summary>
        public string DebugGetLoadedScriptInfo() {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, IPlugin> kvp in mActivePlugins) {
                string loc = kvp.Value.GetType().Assembly.Location;
                sb.Append("[main] ");
                sb.Append(loc);
                sb.Append("\r\n  ");
                DebugGetScriptInfo(kvp.Value, sb);
            }
            return sb.ToString();
        }
        private void DebugGetScriptInfo(IPlugin plugin, StringBuilder sb) {
            sb.Append(plugin.Identifier);
            sb.Append(":");

            if (plugin is PluginCommon.IPlugin_SymbolList) {
                sb.Append(" SymbolList");
            }
            if (plugin is PluginCommon.IPlugin_InlineJsr) {
                sb.Append(" InlineJsr");
            }
            if (plugin is PluginCommon.IPlugin_InlineJsl) {
                sb.Append(" InlineJsl");
            }
            if (plugin is PluginCommon.IPlugin_InlineBrk) {
                sb.Append(" InlineBrk");
            }
            if (plugin is PluginCommon.IPlugin_Visualizer_v2) {
                sb.Append(" Visualizer2");
            } else if (plugin is PluginCommon.IPlugin_Visualizer) {
                sb.Append(" Visualizer");
            }
            sb.Append("\r\n");
        }
    }
}
