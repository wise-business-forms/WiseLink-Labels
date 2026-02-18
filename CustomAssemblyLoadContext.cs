using System.Reflection;
using System.Runtime.Loader;

namespace WiseLabels
{
    /// <summary>
    /// Custom assembly load context for loading assemblies and unmanaged libraries.
    /// </summary>
    public class CustomAssemblyLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Loads an unmanaged library from the specified absolute path.
        /// </summary>
        /// <param name="absolutePath">The absolute path to the unmanaged library.</param>
        /// <returns>A handle to the loaded unmanaged library.</returns>
        public IntPtr LoadUnmanagedLibrary(string absolutePath)
        {
            return LoadUnmanagedDll(absolutePath);
        }

        /// <summary>
        /// Loads an unmanaged DLL with the specified name.
        /// </summary>
        /// <param name="unmanagedDllName">The name or path of the unmanaged DLL to load.</param>
        /// <returns>A handle to the loaded unmanaged DLL.</returns>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            return LoadUnmanagedDllFromPath(unmanagedDllName);
        }

        /// <summary>
        /// Loads a managed assembly with the specified name.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly to load.</param>
        /// <returns>The loaded assembly.</returns>
        /// <exception cref="NotImplementedException">This method is not implemented.</exception>
        protected override Assembly Load(AssemblyName assemblyName)
        {
            throw new NotImplementedException();
        }
    }
}
