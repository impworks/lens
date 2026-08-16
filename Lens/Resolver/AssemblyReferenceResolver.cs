using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Lens.Resolver
{
    /// <summary>
    /// Turns the string of a 'declare reference' entry into an assembly.
    ///
    /// Two spellings are accepted, because a script has two kinds of assembly to name. One is part
    /// of the platform - "System.Net.Http", or "System.Net.Http.dll" for anyone who prefers to
    /// write the extension - and is found by name, wherever the runtime keeps it; making a script
    /// spell out the path of a shared framework file would tie it to one machine and one patch
    /// level. The other is a library of the host's own, named by a path, absolute or relative to
    /// the script.
    /// </summary>
    internal static class AssemblyReferenceResolver
    {
        #region Methods

        /// <summary>
        /// Finds the assembly a reference entry names.
        /// </summary>
        /// <param name="spec">The string as written in the script.</param>
        /// <param name="baseDirectory">The folder a relative path is resolved against, if known.</param>
        /// <param name="known">The assemblies already referenced, which are preferred over loading a second copy.</param>
        /// <param name="assembly">The assembly found, on success.</param>
        /// <param name="error">Why nothing was found, on failure.</param>
        public static bool TryResolve(string spec, string baseDirectory, IEnumerable<Assembly> known, out Assembly assembly, out string error)
        {
            assembly = null;
            error = null;

            spec = spec?.Trim();
            if (string.IsNullOrEmpty(spec))
            {
                error = "the reference is empty";
                return false;
            }

            var name = SimpleNameOf(spec);
            var isPath = LooksLikePath(spec);

            // an assembly the host has already registered is the one to use: loading the same file
            // a second time would give types that are not equal to the ones the host hands over
            if (!isPath && name != null && known != null)
            {
                assembly = known.FirstOrDefault(x => string.Equals(SimpleNameOf(x), name, StringComparison.OrdinalIgnoreCase));
                if (assembly != null)
                    return true;
            }

            var failures = new List<string>();

            // a name is asked of the runtime first, which knows where the platform keeps its own
            // assemblies and which version of them this process is already using
            if (!isPath && name != null && TryLoadByName(name, failures, out assembly))
                return true;

            foreach (var candidate in CandidatePaths(spec, name, baseDirectory, isPath))
            {
                if (TryLoadFrom(candidate, failures, out assembly))
                    return true;
            }

            error = failures.Count > 0
                ? failures[0]
                : "no file of that name was found";

            return false;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Loads an assembly the way the runtime resolves its own references, by identity.
        /// </summary>
        private static bool TryLoadByName(string name, ICollection<string> failures, out Assembly assembly)
        {
            assembly = null;

            try
            {
                assembly = Assembly.Load(new AssemblyName(name));
                return assembly != null;
            }
            catch (Exception ex)
            {
                // a name that is not an assembly identity at all throws just as one that is missing
                // does, and neither is worth stopping the analysis for
                failures.Add(ex.Message);
                Debug.WriteLine("LENS: assembly '{0}' could not be loaded by name: {1}", name, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Loads an assembly from a file, if there is one at that path.
        /// </summary>
        private static bool TryLoadFrom(string path, ICollection<string> failures, out Assembly assembly)
        {
            assembly = null;

            try
            {
                if (!File.Exists(path))
                    return false;

                assembly = Assembly.LoadFrom(path);
                return assembly != null;
            }
            catch (Exception ex)
            {
                // a file that exists but is not managed, or is built for another architecture
                failures.Add(ex.Message);
                Debug.WriteLine("LENS: assembly '{0}' could not be loaded: {1}", path, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// The files worth looking at for a reference, in the order they are worth looking at in.
        ///
        /// A path is tried where it points and nowhere else. A bare name is looked for beside the
        /// script, beside the host, and finally in the runtime's own folder - which is where a
        /// platform assembly that the process has no other reason to have loaded still lives.
        /// </summary>
        private static IEnumerable<string> CandidatePaths(string spec, string name, string baseDirectory, bool isPath)
        {
            if (isPath && IsRooted(spec))
            {
                yield return spec;
                yield break;
            }

            var fileName = isPath ? spec : (HasAssemblyExtension(spec) ? spec : name + ".dll");

            foreach (var folder in ProbeFolders(baseDirectory))
            {
                if (string.IsNullOrEmpty(folder))
                    continue;

                string candidate;

                try
                {
                    candidate = Path.GetFullPath(Path.Combine(folder, fileName));
                }
                catch (Exception)
                {
                    // an unusable path is a path with nothing at the end of it
                    continue;
                }

                yield return candidate;
            }
        }

        /// <summary>
        /// The folders a reference is looked for in.
        /// </summary>
        private static IEnumerable<string> ProbeFolders(string baseDirectory)
        {
            yield return baseDirectory;
            yield return AppContext.BaseDirectory;
            yield return RuntimeFolder;
        }

        /// <summary>
        /// The folder the platform assemblies live in.
        /// </summary>
        private static string RuntimeFolder
        {
            get
            {
                try
                {
                    return Path.GetDirectoryName(typeof(object).Assembly.Location);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Whether the string is meant as a place on disk rather than as a name.
        /// </summary>
        private static bool LooksLikePath(string spec)
        {
            return spec.IndexOf('/') >= 0 || spec.IndexOf('\\') >= 0 || IsRooted(spec);
        }

        /// <summary>
        /// Whether the path names a place on its own, without a folder to resolve it against.
        /// </summary>
        private static bool IsRooted(string path)
        {
            try
            {
                return Path.IsPathRooted(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether the string ends in the extension of an assembly file.
        /// </summary>
        private static bool HasAssemblyExtension(string spec)
        {
            return spec.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                   || spec.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The identity a reference string names: its file name, without the extension a file has
        /// and an assembly name does not.
        /// </summary>
        private static string SimpleNameOf(string spec)
        {
            try
            {
                var fileName = Path.GetFileName(spec);
                if (string.IsNullOrEmpty(fileName))
                    return null;

                return HasAssemblyExtension(fileName)
                    ? fileName.Substring(0, fileName.Length - 4)
                    : fileName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The identity of an assembly that is already loaded.
        /// </summary>
        private static string SimpleNameOf(Assembly assembly)
        {
            try
            {
                return assembly.GetName().Name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion
    }
}
