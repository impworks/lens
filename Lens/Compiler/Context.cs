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
		public const int ILStreamSize = 16384;

		#endregion

		public Context(LensCompilerOptions options = null)
		{
			Options = options ?? new LensCompilerOptions();

			_DefinedTypes = new Dictionary<string, TypeEntity>();
			_DefinedProperties = new Dictionary<string, GlobalPropertyInfo>();

			TypeDetailsLookup = new Dictionary<Type, TypeDetails>();

			Unique = new UniqueNameGenerator();
			Namespaces = new Dictionary<string, bool>();

			if (Options.UseDefaultNamespaces)
			{
				Namespaces.Add("System", true);
				Namespaces.Add("System.Linq", true);
				Namespaces.Add("System.Text.RegularExpressions", true);
			}

			ReflectionResolver = new ReflectionResolver(TypeDetailsLookup);
			_AssemblyCache = new ReferencedAssemblyCache(Options.UseDefaultAssemblies);
			_ExtensionResolver = new ExtensionMethodResolver(ReflectionResolver, Namespaces, _AssemblyCache);
			_TypeResolver = new TypeResolver(ReflectionResolver, Namespaces, _AssemblyCache)
			{
				ExternalLookup = name =>
				{
					TypeEntity ent;
					_DefinedTypes.TryGetValue(name, out ent);
					return ent == null ? null : ent.TypeBuilder;
				}
			};

			AssemblyName an;
			lock(typeof(Context))
				an = new AssemblyName(Unique.AssemblyName());

			if (Options.AllowSave)
			{
				if(string.IsNullOrEmpty(Options.FileName))
					Options.FileName = an.Name + (Options.SaveAsExe ? ".exe" : ".dll");

				MainAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave);
				MainModule = MainAssembly.DefineDynamicModule(an.Name, Options.FileName);
			}
			else
			{
				MainAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
				MainModule = MainAssembly.DefineDynamicModule(an.Name);
			}

			ContextId = GlobalPropertyHelper.RegisterContext();

			MainType = CreateType(EntityNames.MainTypeName, prepare: false);
			MainType.Kind = TypeEntityKind.Main;
			MainType.Interfaces = new[] {typeof (IScript)};
			MainMethod = MainType.CreateMethod(EntityNames.RunMethodName, typeof(object), Type.EmptyTypes, false, true, false);

			if(Options.LoadStandardLibrary)
				initStdlib();

			InitSafeMode();
		}

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

		#region Properties

		/// <summary>
		/// Context ID for imported properties.
		/// </summary>
		public int ContextId { get; set; }

		/// <summary>
		/// Compiler options.
		/// </summary>
		internal LensCompilerOptions Options { get; private set; }

		/// <summary>
		/// The assembly that's being currently built.
		/// </summary>
		public AssemblyBuilder MainAssembly { get; private set; }

		/// <summary>
		/// The main module of the current assembly.
		/// </summary>
		public ModuleBuilder MainModule { get; private set; }

		/// <summary>
		/// The main type in which all "global" functions are stored.
		/// </summary>
		internal TypeEntity MainType { get; private set; }

		/// <summary>
		/// The function that is the body of the script.
		/// </summary>
		internal MethodEntity MainMethod { get; private set; }

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
		internal Scope Scope { get { return _ScopeStack.Count > 0 ? _ScopeStack.Peek() : null; } }

		/// <summary>
		/// The current most nested try block.
		/// </summary>
		internal TryNode CurrentTryBlock
		{
			get { return CurrentMethod.CurrentTryBlock; }
			set { CurrentMethod.CurrentTryBlock = value; }
		}

		/// <summary>
		/// The current most nested catch block.
		/// </summary>
		internal CatchNode CurrentCatchBlock
		{
			get { return CurrentMethod.CurrentCatchBlock; }
			set { CurrentMethod.CurrentCatchBlock = value; }
		}

		/// <summary>
		/// The list of namespaces to only look in when resolving a type or an extension method.
		/// </summary>
		internal Dictionary<string, bool> Namespaces;

		internal readonly UniqueNameGenerator Unique;

		internal readonly ReflectionResolver ReflectionResolver;

		internal readonly List<TypeEntity> UnpreparedTypes = new List<TypeEntity>();
		internal readonly List<TypeContentsBase> UnpreparedTypeContents = new List<TypeContentsBase>();
		internal readonly List<MethodEntityBase> UnprocessedMethods = new List<MethodEntityBase>();

		/// <summary>
		/// Information about types declared in the assembly (user-defined and generic parameters).
		/// </summary>
		internal readonly Dictionary<Type, TypeDetails> TypeDetailsLookup;

		#endregion

		#region Fields

		/// <summary>
		/// A helper that resolves built-in .NET types by their string signatures.
		/// </summary>
		private readonly TypeResolver _TypeResolver;

		/// <summary>
		/// A helper that resolves extension methods by type and arguments.
		/// </summary>
		private readonly ExtensionMethodResolver _ExtensionResolver;

		/// <summary>
		/// The root of type lookup.
		/// </summary>
		private readonly Dictionary<string, TypeEntity> _DefinedTypes;

		/// <summary>
		/// The lookup table for imported properties.
		/// </summary>
		private readonly Dictionary<string, GlobalPropertyInfo> _DefinedProperties;

		/// <summary>
		/// The stack of currently processed scopes.
		/// </summary>
		private readonly Stack<Scope> _ScopeStack = new Stack<Scope>();

		/// <summary>
		/// The list of assemblies referenced by current script.
		/// </summary>
		private readonly ReferencedAssemblyCache _AssemblyCache;

		#endregion
	}
}
