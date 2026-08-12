using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.Utils;

namespace Lens.Compiler
{
    /// <summary>
    /// The main context class that stores information about currently compiled Assembly.
    /// </summary>
    internal partial class Context
    {
        #region Constants

        /// <summary>
        /// The default size of a method's IL Generator stream.
        /// </summary>
        public const int IlStreamSize = 16384;

        #endregion

        #region Constructor

        public Context(LensCompilerOptions options = null)
        {
            Options = options ?? new LensCompilerOptions();

            _definedTypes = new Dictionary<string, TypeEntity>();
            _definedProperties = new Dictionary<string, GlobalPropertyInfo>();

            Diagnostics = new DiagnosticBag();

            Resolver = new TypeResolutionContext();

            Unique = new UniqueNameGenerator();

            if (Options.UseDefaultNamespaces)
            {
                Namespaces.Add("System", true);
                Namespaces.Add("System.Linq", true);
                Namespaces.Add("System.Text.RegularExpressions", true);
            }

            AssemblyCache = new ReferencedAssemblyCache(Options.UseDefaultAssemblies);
            _extensionResolver = new ExtensionMethodResolver(Namespaces, AssemblyCache);
            _typeResolver = new TypeResolver(Resolver, Namespaces, AssemblyCache)
            {
                ExternalLookup = LookupTypeForResolver
            };

            AssemblyName an;
            lock (typeof(Context))
                an = new AssemblyName(Unique.AssemblyName());

#if NET_CLASSIC
            if (Options.AllowSave)
            {
                if (string.IsNullOrEmpty(Options.FileName))
                    Options.FileName = an.Name + (Options.SaveAsExe ? ".exe" : ".dll");

                MainAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave);
                MainModule = MainAssembly.DefineDynamicModule(an.Name, Options.FileName);
            }
            else
            {
                MainAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
                MainModule = MainAssembly.DefineDynamicModule(an.Name);
            }
#else
            MainAssembly = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            MainModule = MainAssembly.DefineDynamicModule(an.Name);
#endif

            ContextId = GlobalPropertyHelper.RegisterContext();

            MainType = CreateType(EntityNames.MainTypeName, prepare: false);
            MainType.Kind = TypeEntityKind.Main;
            MainType.Interfaces = new[] {TypeEntryCache.Of<IScript>()};
            MainMethod = MainType.CreateMethod(EntityNames.RunMethodName, TypeEntryCache.Of<object>(), new TypeEntry[0], false, true, false);

            if (Options.LoadStandardLibrary)
                InitStdlib();

            InitSafeMode();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Context ID for imported properties.
        /// </summary>
        public int ContextId { get; }

        /// <summary>
        /// Compiler options.
        /// </summary>
        internal LensCompilerOptions Options { get; }

        /// <summary>
        /// Everything that has gone wrong so far.
        /// The compiler keeps analysing after an error, so this may hold more than one entry.
        /// </summary>
        internal DiagnosticBag Diagnostics { get; }

        /// <summary>
        /// The state of type resolution for the current compilation:
        /// memoization caches and the generic parameters that are currently in scope.
        /// </summary>
        internal TypeResolutionContext Resolver { get; }

        /// <summary>
        /// The assembly that's being currently built.
        /// </summary>
        public AssemblyBuilder MainAssembly { get; }

        /// <summary>
        /// The main module of the current assembly.
        /// </summary>
        public ModuleBuilder MainModule { get; }

        /// <summary>
        /// The main type in which all "global" functions are stored.
        /// </summary>
        internal TypeEntity MainType { get; }

        /// <summary>
        /// The function that is the body of the script.
        /// </summary>
        internal MethodEntity MainMethod { get; }

        /// <summary>
        /// Type that is currently processed.
        /// </summary>
        internal TypeEntity CurrentType { get; set; }

        /// <summary>
        /// Method that is currently processed.
        /// </summary>
        internal MethodEntityBase CurrentMethod { get; set; }

        /// <summary>
        /// The current scope frame in which all local variables are registered and searched for.
        /// </summary>
        internal Scope Scope => _scopeStack.Count > 0 ? _scopeStack.Peek() : null;

        /// <summary>
        /// The current most nested try block.
        /// </summary>
        internal TryNode CurrentTryBlock
        {
            get => CurrentMethod.CurrentTryBlock;
            set => CurrentMethod.CurrentTryBlock = value;
        }

        /// <summary>
        /// The current most nested catch block.
        /// </summary>
        internal CatchNode CurrentCatchBlock
        {
            get => CurrentMethod.CurrentCatchBlock;
            set => CurrentMethod.CurrentCatchBlock = value;
        }

        /// <summary>
        /// The list of namespaces to only look in when resolving a type or an extension method.
        /// </summary>
        internal Dictionary<string, bool> Namespaces = new Dictionary<string, bool>();

        internal readonly UniqueNameGenerator Unique;

        internal readonly List<TypeEntity> UnpreparedTypes = new List<TypeEntity>();
        internal readonly List<TypeContentsBase> UnpreparedTypeContents = new List<TypeContentsBase>();
        internal readonly List<MethodEntityBase> UnprocessedMethods = new List<MethodEntityBase>();

        #endregion

        #region Fields

        /// <summary>
        /// A helper that resolves built-in .NET types by their string signatures.
        /// </summary>
        private readonly TypeResolver _typeResolver;

        /// <summary>
        /// A helper that resolves extension methods by type and arguments.
        /// </summary>
        private readonly ExtensionMethodResolver _extensionResolver;

        /// <summary>
        /// The root of type lookup.
        /// </summary>
        private readonly Dictionary<string, TypeEntity> _definedTypes;

        /// <summary>
        /// The lookup table for imported properties.
        /// </summary>
        private readonly Dictionary<string, GlobalPropertyInfo> _definedProperties;

        /// <summary>
        /// The stack of currently processed scopes.
        /// </summary>
        private readonly Stack<Scope> _scopeStack = new Stack<Scope>();

        /// <summary>
        /// The list of assemblies referenced by current script.
        /// </summary>
        internal readonly ReferencedAssemblyCache AssemblyCache;

        #endregion

        #region Type lookup for the signature resolver

        /// <summary>
        /// Resolves a bare type name for the built-in type resolver: a generic parameter that is
        /// currently in scope, or a type declared in the script.
        /// Locally declared generic types are emitted under an arity-mangled name, but LENS refers
        /// to them by their plain name, so both spellings are accepted here.
        /// </summary>
        private Type LookupTypeForResolver(string name)
        {
            var typeParam = Resolver.FindTypeParameter(name);
            if (typeParam?.Builder != null)
                return typeParam.Builder;

            var lensName = name;
            var arity = 0;

            var tick = name.IndexOf('`');
            if (tick >= 0)
            {
                lensName = name.Substring(0, tick);
                if (!int.TryParse(name.Substring(tick + 1), out arity))
                    return null;
            }

            if (!_definedTypes.TryGetValue(lensName, out var ent))
                return null;

            return ent.GenericParameterCount == arity ? ent.TypeBuilder : null;
        }

        #endregion

        #region Error reporting methods

        /// <summary>
        /// Throws a new error.
        /// </summary>
        [ContractAnnotation("=> halt")]
        [DebuggerStepThrough]
        public static void Error(string msg, params object[] args)
        {
            throw new LensCompilerException(string.Format(msg, args));
        }

        /// <summary>
        /// Throws a new error bound to a location.
        /// </summary>
        [ContractAnnotation("=> halt")]
        [DebuggerStepThrough]
        public static void Error(LocationEntity ent, string msg, params object[] args)
        {
            throw new LensCompilerException(string.Format(msg, args), ent);
        }

        #endregion
    }
}