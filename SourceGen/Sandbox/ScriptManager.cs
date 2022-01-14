﻿/*
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
using System.Linq;
using System.Reflection;
using System.Text;

using CommonUtil;
using PluginCommon;

namespace SourceGen.Sandbox
{

    public interface IScriptManager
    {
        //DomainManager DomainMgr { get; }
        //bool UseMainAppDomain { get; }

        //void Cleanup();
        void Clear();
        string DebugGetLoadedScriptInfo();
        IReadOnlyDictionary<string, IPlugin> GetActivePlugins();
        List<IPlugin> GetAllInstances();
        IPlugin GetInstance(string scriptIdent);
        bool IsLabelSignificant(Symbol before, Symbol after);
        bool LoadPlugin(string scriptIdent, out FileLoadReport report);
        void PrepareScripts(IApplication appRef);
        bool RebootSandbox();
        void UnprepareScripts();
    }

    /// <summary>
    /// Maintains a collection of IPlugin instances, or communicates with the remote
    /// PluginManager that holds the collection.  Whether the plugins are instantiated
    /// locally depends on how the class is constructed.
    ///
    /// One of these will be instantiated when the DisasmProject is created.
    /// </summary>
    public class ScriptManager : IScriptManager
    {
        public const string FILENAME_EXT = ".cs";
        public static readonly string FILENAME_FILTER = Res.Strings.FILE_FILTER_CS;

        /// <summary>
        /// If true, the DomainManager will use the keep-alive timer hack.
        /// </summary>
        public static bool UseKeepAliveHack { get; set; }

        ///// <summary>
        ///// If true, this ScriptManager is not using a DomainManager.
        ///// </summary>
        //public bool UseMainAppDomain
        //{
        //    get { return DomainMgr == null; }
        //}

        ///// <summary>
        ///// Reference to DomainManager, if we're using one.
        ///// </summary>
        //public DomainManager DomainMgr { get; private set; }

        /// <summary>
        /// Collection of loaded plugins, if we're not using a DomainManager.
        /// </summary>
        private Dictionary<string, IPlugin> mActivePlugins;

        /// <summary>
        /// Reference to project, from which we can get the file data and project path name.
        /// </summary>
        private DisasmProject mProject;

        private class LoadedPluginPath
        {
            public string ScriptIdent { get; private set; }
            public string DllPath { get; private set; }

            public LoadedPluginPath(string scriptIdent, string dllPath)
            {
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
        public ScriptManager(DisasmProject proj)
        {
            mProject = proj;

            //if (!proj.UseMainAppDomainForPlugins)
            //{
            //    CreateDomainManager();
            //}
            //else
            //{
            mActivePlugins = new Dictionary<string, IPlugin>();
            //}
        }

        //private void CreateDomainManager()
        //{
        //    // The project's UseMainAppDomainForPlugins value is theoretically mutable, so
        //    // don't try to assert it here.
        //    DomainMgr = new DomainManager(UseKeepAliveHack);
        //    DomainMgr.CreateDomain("Plugin Domain", PluginDllCache.GetPluginDirPath());
        //    DomainMgr.PluginMgr.SetFileData(mProject.FileData);
        //}

        /// <summary>
        /// Cleans up, discarding the AppDomain if one was created.  Do not continue to use
        /// the object after calling this.
        /// </summary>
        public void Cleanup()
        {
            //if (DomainMgr != null)
            //{
            //    DomainMgr.Dispose();
            //    DomainMgr = null;
            //}
            mActivePlugins = null;
            mProject = null;
        }

        /// <summary>
        /// Clears the list of plugins.  This does not unload assemblies.  Call this when
        /// the list of extension scripts configured into the project has changed.
        /// </summary>
        public void Clear()
        {
            //if (DomainMgr == null)
            //{
            //    mActivePlugins.Clear();
            //}
            //else
            //{
            //    CheckHealth();
            //    DomainMgr.PluginMgr.ClearPluginList();
            //}
            mLoadedPlugins.Clear();
        }

        /// <summary>
        /// Attempts to load the specified plugin.  If the plugin is already loaded, this
        /// does nothing.  If not, the assembly is loaded and an instance is created.
        /// </summary>
        /// <param name="scriptIdent">Script identifier.</param>
        /// <param name="report">Report with errors and warnings.</param>
        /// <returns>True on success.</returns>
        public bool LoadPlugin(string scriptIdent, out FileLoadReport report)
        {
            // Make sure the most recent version is compiled.
            string dllPath = PluginDllCache.GenerateScriptDll(scriptIdent,
                mProject.ProjectPathName, out report);
            if (dllPath == null)
            {
                return false;
            }

            //if (DomainMgr == null)
            //{
            if (mActivePlugins.ContainsKey(scriptIdent))
                return true;
            Assembly asm = Assembly.LoadFile(dllPath);
            IPlugin plugin = PluginDllCache.ConstructIPlugin(asm);
            mActivePlugins.Add(scriptIdent, plugin);
            report = new FileLoadReport(dllPath);       // empty report
            return true;
            //}
            //else
            //{
            //    CheckHealth();
            //    IPlugin plugin = DomainMgr.PluginMgr.LoadPlugin(dllPath, scriptIdent,
            //        out string failMsg);
            //    if (plugin == null)
            //    {
            //        report.Add(FileLoadItem.Type.Error, "Failed loading plugin: " + failMsg);
            //    }
            //    else
            //    {
            //        mLoadedPlugins.Add(new LoadedPluginPath(scriptIdent, dllPath));
            //    }
            //    return plugin != null;
            //}
        }

        /// <summary>
        /// Reboots the sandbox by discarding the old DomainManager, creating a new one, and
        /// reloading all of the plugins.
        /// </summary>
        /// <returns>True if no problems were encountered.</returns>
        public bool RebootSandbox()
        {
            //if (DomainMgr == null)
            //{
            return false;
            //}
            //Debug.WriteLine("Rebooting sandbox...");

            //// Discard existing DomainManager, and create a new one.
            //DomainMgr.Dispose();
            //CreateDomainManager();

            //bool failed = false;

            //// Reload plugins.
            //foreach (LoadedPluginPath lpp in mLoadedPlugins)
            //{
            //    IPlugin plugin = DomainMgr.PluginMgr.LoadPlugin(lpp.DllPath, lpp.ScriptIdent,
            //        out string failMsg);
            //    if (plugin == null)
            //    {
            //        // This is unexpected; we're opening a DLL that we recently had open.
            //        // Not a lot we can do to recover, and we're probably too deep to report
            //        // a failure to the user.
            //        Debug.WriteLine("Failed to reopen '" + lpp.DllPath + "': " + failMsg);
            //        failed = true;
            //        // continue on to the next one
            //    }
            //    else
            //    {
            //        Debug.WriteLine("  Reloaded " + lpp.ScriptIdent);
            //    }
            //}

            //return failed;
        }

        ///// <summary>
        ///// Checks the health of the sandbox, and reboots it if it seems unhealthy.  Call this
        ///// before making any calls into plugins via DomainMgr.
        ///// </summary>
        ///// <remarks>
        ///// We're relying on the idea that, if the ping succeeds, the PluginManager instance
        ///// will continue to exist for a while.  There is some evidence to the contrary -- the
        ///// ping issued immediately after the machine wakes up succeeds right before the remote
        ///// objects get discarded -- but I'm hoping that's due to a race condition that won't
        ///// happen in normal circumstances (because of the keep-alives we send).
        ///// </remarks>
        //private void CheckHealth()
        //{
        //    Debug.Assert(DomainMgr != null);
        //    try
        //    {
        //        DomainMgr.PluginMgr.Ping(111);
        //    }
        //    catch (Exception re)
        //    {
        //        Debug.WriteLine("Health check failed: " + re.Message);
        //        RebootSandbox();
        //        DomainMgr.PluginMgr.Ping(112);
        //    }
        //}

        public IPlugin GetInstance(string scriptIdent)
        {
            //if (DomainMgr == null)
            //{
            if (mActivePlugins.TryGetValue(scriptIdent, out IPlugin? plugin) && plugin != null)
            {
                return plugin;
            }
            Debug.Assert(false);
            return null;
            //}
            //else
            //{
            //    CheckHealth();
            //    return DomainMgr.PluginMgr.GetPlugin(scriptIdent);
            //}
        }

        /// <summary>
        /// Generates a list of references to instances of loaded plugins.
        /// </summary>
        /// <returns>Newly-created list of plugin references.</returns>
        public List<IPlugin> GetAllInstances()
        {
            IReadOnlyDictionary<string, IPlugin> dict;
            //if (DomainMgr == null)
            //{
            dict = mActivePlugins;
            //}
            //else
            //{
            //    CheckHealth();
            //    dict = DomainMgr.PluginMgr.GetActivePlugins();
            //}
            return dict.Values.ToList();
        }

        /// <summary>
        /// Prepares all active scripts for action.
        /// </summary>
        /// <param name="appRef">Reference to object providing app services.</param>
        public void PrepareScripts(IApplication appRef)
        {
            List<PlSymbol> plSyms = GeneratePlSymbolList();

            //if (DomainMgr == null)
            //{
            AddressTranslate addrTrans = new AddressTranslate(mProject.AddrMap);
            foreach (IPlugin plugin in mActivePlugins.Values)
            {
                plugin.Prepare(appRef, mProject.FileData, addrTrans);
                var symbolList = plugin as IPlugin_SymbolList;
                if (symbolList != null)
                {
                    symbolList.UpdateSymbolList(plSyms);
                }
            }
            //}
            //else
            //{
            //    CheckHealth();
            //    int spanLength;
            //    List<AddressMap.AddressMapEntry> addrEnts =
            //        mProject.AddrMap.GetEntryList(out spanLength);
            //    // TODO: if Prepare() throws an exception, we should catch it and report
            //    //   it to the user.
            //    DomainMgr.PluginMgr.PreparePlugins(appRef, spanLength, addrEnts, plSyms);
            //}
        }

        /// <summary>
        /// Puts scripts back to sleep.
        /// </summary>
        public void UnprepareScripts()
        {
            //if (DomainMgr == null)
            //{
            foreach (IPlugin plugin in mActivePlugins.Values)
            {
                plugin.Unprepare();
            }
            //}
            //else
            //{
            //    CheckHealth();
            //    DomainMgr.PluginMgr.UnpreparePlugins();
            //}
        }


        /// <summary>
        /// Returns true if any of the plugins report that the before or after label is
        /// significant.
        /// </summary>
        /// <remarks>
        /// This is called when a label is edited, so DisasmProject can decide whether it
        /// needs to re-run the code analyzer.
        /// </remarks>
        public bool IsLabelSignificant(Symbol before, Symbol after)
        {
            string labelBefore = before?.Label ?? "";
            string labelAfter = after?.Label ?? "";
            //if (DomainMgr == null)
            //{
            foreach (IPlugin plugin in mActivePlugins.Values)
            {
                if (plugin is IPlugin_SymbolList &&
                        ((IPlugin_SymbolList)plugin).IsLabelSignificant(labelBefore,
                            labelAfter))
                {
                    return true;
                }
            }
            return false;
            //}
            //else
            //{
            //    CheckHealth();
            //    return DomainMgr.PluginMgr.IsLabelSignificant(labelBefore, labelAfter);
            //}
        }

        /// <summary>
        /// Gathers a list of symbols from the project's symbol table.
        /// </summary>
        /// <remarks>
        /// Remember that we need to set this up before code analysis runs, so many of the
        /// secondary data structures (like Anattribs) won't be available.
        /// </remarks>
        private List<PlSymbol> GeneratePlSymbolList()
        {
            List<PlSymbol> plSymbols = new List<PlSymbol>();
            SymbolTable symTab = mProject.SymbolTable;

            // UserLabels maps offset to Symbol.  Create the reverse mapping.
            Dictionary<Symbol, int> symbolOffsets =
                new Dictionary<Symbol, int>(mProject.UserLabels.Count);
            foreach (KeyValuePair<int, Symbol> kvp in mProject.UserLabels)
            {
                symbolOffsets[kvp.Value] = kvp.Key;
            }

            // Add in the address region pre-labels.
            IEnumerator<AddressMap.AddressChange> addrIter = mProject.AddrMap.AddressChangeIterator;
            while (addrIter.MoveNext())
            {
                AddressMap.AddressChange change = addrIter.Current;
                if (!change.IsStart)
                {
                    continue;
                }
                if (change.Region.HasValidPreLabel)
                {
                    Symbol newSym = new Symbol(change.Region.PreLabel,
                        change.Region.PreLabelAddress, Symbol.Source.AddrPreLabel,
                        Symbol.Type.ExternalAddr, Symbol.LabelAnnotation.None);
                    symbolOffsets[newSym] = change.Region.Offset;
                }
            }

            foreach (Symbol sym in symTab)
            {
                PlSymbol.Source plsSource;
                int symOff, offset = -1;
                switch (sym.SymbolSource)
                {
                    case Symbol.Source.User:
                        plsSource = PlSymbol.Source.User;
                        if (symbolOffsets.TryGetValue(sym, out symOff))
                        {
                            offset = symOff;
                        }
                        break;
                    case Symbol.Source.AddrPreLabel:
                        plsSource = PlSymbol.Source.AddrPreLabel;
                        if (symbolOffsets.TryGetValue(sym, out symOff))
                        {
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
                switch (sym.SymbolType)
                {
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
                if (sym is DefSymbol)
                {
                    DefSymbol defSym = sym as DefSymbol;
                    width = defSym.DataDescriptor.Length;
                    tag = defSym.Tag;
                }

                plSymbols.Add(new PlSymbol(sym.Label, sym.Value, width, plsSource, plsType, tag,
                    offset));
            }

            return plSymbols;
        }

#if false
        public delegate bool CheckMatch(IPlugin plugin);
        public IPlugin GetMatchingScript(CheckMatch check) {
            Dictionary<string, IPlugin> plugins;
            if (DomainMgr == null) {
                plugins = mActivePlugins;
            } else {
                plugins = DomainMgr.PluginMgr.GetActivePlugins();
            }
            foreach (IPlugin plugin in plugins.Values) {
                if (check(plugin)) {
                    return plugin;
                }
            }
            return null;
        }
#endif

        /// <summary>
        /// Returns a list of loaded plugins.  Callers should not retain this list, as the
        /// set can change due to user activity.
        /// </summary>
        public IReadOnlyDictionary<string, IPlugin> GetActivePlugins()
        {
            //if (DomainMgr == null)
            //{
            // copy the contents
            var pdict = new Dictionary<string, IPlugin>(mActivePlugins);
            return pdict;
            //}
            //else
            //{
            //    CheckHealth();
            //    return DomainMgr.PluginMgr.GetActivePlugins();
            //}
        }

        /// <summary>
        /// For debugging purposes, get some information about the currently loaded
        /// extension scripts.
        /// </summary>
        public string DebugGetLoadedScriptInfo()
        {
            StringBuilder sb = new StringBuilder();
            //if (DomainMgr == null)
            //{
            foreach (IPlugin plugin in mActivePlugins.Values)
            {
                string loc = plugin.GetType().Assembly.Location;
                sb.Append("[main] ");
                sb.Append(loc);
                sb.Append("\r\n  ");
                DebugGetScriptInfo(plugin, sb);
            }
            //}
            //else
            //{
            //    CheckHealth();
            //    var plugins = DomainMgr.PluginMgr.GetActivePlugins();
            //    foreach (IPlugin plugin in plugins.Values)
            //    {
            //        string loc = DomainMgr.PluginMgr.GetPluginAssemblyLocation(plugin);
            //        sb.AppendFormat("[sub {0}] ", DomainMgr.Id);
            //        sb.Append(loc);
            //        sb.Append("\r\n  ");
            //        DebugGetScriptInfo(plugin, sb);
            //    }
            //}

            return sb.ToString();
        }

        private void DebugGetScriptInfo(IPlugin plugin, StringBuilder sb)
        {
            sb.Append(plugin.Identifier);
            sb.Append(":");

            // The plugin is actually a MarshalByRefObject, so we can't use reflection
            // to gather the list of interfaces.
            // TODO(maybe): add a call that does a reflection query on the remote side
            if (plugin is PluginCommon.IPlugin_SymbolList)
            {
                sb.Append(" SymbolList");
            }
            if (plugin is PluginCommon.IPlugin_InlineJsr)
            {
                sb.Append(" InlineJsr");
            }
            if (plugin is PluginCommon.IPlugin_InlineJsl)
            {
                sb.Append(" InlineJsl");
            }
            if (plugin is PluginCommon.IPlugin_InlineBrk)
            {
                sb.Append(" InlineBrk");
            }
            if (plugin is PluginCommon.IPlugin_Visualizer_v2)
            {
                sb.Append(" Visualizer2");
            }
            else if (plugin is PluginCommon.IPlugin_Visualizer)
            {
                sb.Append(" Visualizer");
            }
            sb.Append("\r\n");
        }
    }
}
