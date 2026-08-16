using System.Collections.Generic;
using Lens.Compiler;

namespace Lens
{
    /// <summary>
    /// A list of options to tweak the compiler.
    /// </summary>
    public class LensCompilerOptions
    {

#if NET_CLASSIC
        /// <summary>
        /// Checks whether the generated assembly can be saved to disk.
        /// Default = false.
        /// </summary>
        public bool AllowSave = false;
#endif

        /// <summary>
        /// Checks whether the compiler should auto-include default assemblies for type and extension method resolvers.
        /// Default = true.
        /// </summary>
        public bool UseDefaultAssemblies = true;

        /// <summary>
        /// Checks whether the compiler should auto-include a bunch of common namespaces.
        /// Default = true.
        /// </summary>
        public bool UseDefaultNamespaces = true;

        /// <summary>
        /// Checks whether extension methods are allowed. Can be disabled to speed up compilation.
        /// Default = true.
        /// </summary>
        public bool AllowExtensionMethods = true;

        /// <summary>
        /// Checks whether LENS standard library should be registered.
        /// Default = true.
        /// </summary>
        public bool LoadStandardLibrary = true;

        /// <summary>
        /// Checks whether the generated assembly must be saved as a console executable.
        /// Depends on AllowSave.
        /// Default = false.
        /// </summary>
        public bool SaveAsExe = false;

        /// <summary>
        /// Specifies the file name for generated assembly.
        /// Depends on AllowSave.
        /// Default = none.
        /// </summary>
        public string FileName = string.Empty;

        /// <summary>
        /// Checks if operations on constants must be performed at compile time.
        /// Default = true.
        /// </summary>
        public bool UnrollConstants = true;

        /// <summary>
        /// Checks whether the script should be compiled in a sandbox environment.
        /// Default = Disabled
        /// </summary>
        public SafeMode SafeMode = SafeMode.Disabled;

        /// <summary>
        /// The list of types that form a blacklist or a whitelist depending on safe mode.
        /// </summary>
        public List<string> SafeModeExplicitTypes = new List<string>();

        /// <summary>
        /// The list of namespaces that form a blacklist or a whitelist depending on safe mode.
        /// </summary>
        public List<string> SafeModeExplicitNamespaces = new List<string>();

        /// <summary>
        /// The whitelisted or blacklisted subsystems for safe mode (networking, IO, etc).
        /// </summary>
        public SafeModeSubsystem SafeModeExplicitSubsystems = SafeModeSubsystem.None;

        /// <summary>
        /// The folder that a relative path in a 'declare reference' entry is resolved against.
        /// A host that compiles a script it read from disk should point this at the script's own
        /// folder, so that the script names its libraries the way its author saw them.
        /// Default = none, which resolves relative paths against the host's folder.
        /// </summary>
        public string ScriptDirectory;

        /// <summary>
        /// What a 'declare' block means to this compilation.
        /// Default = Verify.
        /// </summary>
        public DeclarationMode DeclarationMode = DeclarationMode.Verify;

        /// <summary>
        /// Checks whether compilation stage times must be measured.
        /// Default = false
        /// </summary>
        public bool MeasureTime;

        /// <summary>
        /// Runs the lowering pass over every method body, not only the ones a state machine is
        /// built from.
        ///
        /// This exists so that the pass can be checked on its own: a script that contains no yield
        /// and no await must behave identically whether its control flow was flattened or not.
        /// Nothing outside the test suite has a reason to turn it on.
        /// </summary>
        internal bool LowerAllFunctions;

        /// <summary>
        /// Makes a copy, so that a consumer can adjust the options it was handed without writing
        /// through to the caller's object. The list fields are shared, and are read-only in practice.
        /// </summary>
        internal LensCompilerOptions Copy()
        {
            return (LensCompilerOptions) MemberwiseClone();
        }
    }
}