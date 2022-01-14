using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace PluginCommon
{
    public class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver mResolver;

        public PluginAssemblyLoadContext()
        //: base(isCollectible: true)
        {
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            mResolver = new AssemblyDependencyResolver(thisAssemblyPath);
        }

        protected override Assembly Load(AssemblyName name)
        {
            string assemblyPath = mResolver.ResolveAssemblyToPath(name);
            if (assemblyPath == null)
                throw new InvalidOperationException("Assembly path is null: " + name.FullName);
            return LoadFromAssemblyPath(assemblyPath);
        }

    }

}
