using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Lens;
using Lens.Analysis;
using Lens.Compiler;

namespace Lens.Playground
{
    /// <summary>
    /// The environment a script gets in the playground, in one place.
    ///
    /// The browser is the real sandbox: there is no file system to reach, no process to start and
    /// no assembly to load from disk, so a script that names those APIs fails at run time whatever
    /// the compiler thinks. The blacklist below exists so that it fails at compile time instead,
    /// with a sentence that says which API is unavailable rather than a
    /// PlatformNotSupportedException from somewhere inside the BCL.
    ///
    /// It is deliberately a list of types rather than of namespaces: blacklisting System.IO whole
    /// would take Stream and TextReader with it, and those are what HttpClient hands back.
    ///
    /// Reflection, assembly loading and the compiler's own types are not listed here because they
    /// are not the playground's to allow: any safe mode denies those outright. See
    /// docs/safe-mode.md.
    /// </summary>
    internal static class PlaygroundOptions
    {
        /// <summary>
        /// Types a script has no business naming here, each because the browser cannot honour it.
        /// </summary>
        private static readonly List<string> ForbiddenTypes = new List<string>
        {
            // there is no file system behind a browser tab
            "System.IO.File",
            "System.IO.FileInfo",
            "System.IO.Directory",
            "System.IO.DirectoryInfo",
            "System.IO.DriveInfo",
            "System.IO.FileStream",
            "System.IO.Path",

            // nothing here can start a process or read the host's environment
            "System.Diagnostics.Process",
            "System.Diagnostics.ProcessStartInfo",
            "System.Environment",
            "System.AppDomain",
            "System.AppDomainManager"
        };

        /// <summary>
        /// Assemblies the compiler does not reference on its own, but that a playground script has
        /// every reason to expect.
        ///
        /// LINQ, the collections and the regular expressions come as defaults. Talking to the
        /// network and reading what comes back do not, and those are the two things a script in a
        /// browser can actually do.
        /// </summary>
        private static readonly Type[] ExtraAssemblyAnchors =
        {
            typeof(HttpClient), // System.Net.Http
            typeof(Uri), // System.Private.Uri, reached through its facade
            typeof(JsonSerializer) // System.Text.Json
        };

        /// <summary>
        /// The options every playground compilation uses.
        ///
        /// A fresh copy each time: the compiler is handed one per script, and the lists inside are
        /// shared but never written to.
        /// </summary>
        public static LensCompilerOptions Create()
        {
            return new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitTypes = ForbiddenTypes,
                MeasureTime = true
            };
        }

        /// <summary>
        /// Points a compiler at the assemblies above, so that a script may name what is in them.
        /// </summary>
        public static void Reference(LensCompiler compiler)
        {
            foreach (var assembly in Assemblies())
                compiler.RegisterAssembly(assembly);
        }

        /// <summary>
        /// The same for the analyser, so that the editor offers what the compiler accepts.
        ///
        /// The two lists have to agree: a completion list that offers HttpClient while the compiler
        /// cannot resolve it is worse than one that offers nothing.
        /// </summary>
        public static void Reference(ScriptAnalyzer analyzer)
        {
            foreach (var assembly in Assemblies())
                analyzer.AddReference(assembly);
        }

        /// <summary>
        /// An analyser configured the way every compilation is.
        /// </summary>
        public static ScriptAnalyzer CreateAnalyzer()
        {
            var analyzer = new ScriptAnalyzer(Create());
            Reference(analyzer);

            return analyzer;
        }

        /// <summary>
        /// A compiler configured the way every run is.
        /// </summary>
        public static LensCompiler CreateCompiler()
        {
            var compiler = new LensCompiler(Create());
            Reference(compiler);

            return compiler;
        }

        private static IEnumerable<Assembly> Assemblies()
        {
            foreach (var anchor in ExtraAssemblyAnchors)
                yield return anchor.Assembly;
        }
    }
}
