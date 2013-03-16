using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// The main context class that stores information about currently compiled Assembly.
	/// </summary>
	public partial class Context
	{
		#region Constants

		/// <summary>
		/// The name of the main type in the assembly.
		/// </summary>
		private const string RootTypeName = "<ScriptRootType>";

		/// <summary>
		/// The name of the entry point of the assembly.
		/// </summary>
		private const string RootMethodName = "Run";

		/// <summary>
		/// The default size of a method's IL Generator stream.
		/// </summary>
		public const int ILStreamSize = 1024;

		#endregion

		private Context()
		{
			var an = new AssemblyName(getAssemblyName());

			_TypeResolver = new TypeResolver();
			_DefinedTypes = new Dictionary<string, TypeEntity>();

			MethodResolutionStack = new Stack<MethodEntity>();

			MainAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave);
			MainModule = MainAssembly.DefineDynamicModule(an.Name, an.Name + ".dll");

			MainType = CreateType(RootTypeName);
			MainType.Interfaces = new[] {typeof (IScript)};
			MainMethod = MainType.CreateMethod(RootMethodName, Type.EmptyTypes, false, true);
			MainMethod.ReturnType = typeof (object);
		}

		/// <summary>
		/// Creates the context from a stream of nodes.
		/// </summary>
		public static Context CreateFromNodes(IEnumerable<NodeBase> nodes)
		{
			var ctx = new Context();

			foreach (var currNode in nodes)
			{
				if (currNode is TypeDefinitionNode)
					ctx.DeclareType(currNode as TypeDefinitionNode);
				else if (currNode is RecordDefinitionNode)
					ctx.DeclareRecord(currNode as RecordDefinitionNode);
				else if (currNode is FunctionNode)
					ctx.DeclareFunction(currNode as FunctionNode);
				else if (currNode is UsingNode)
					ctx.DeclareOpenNamespace(currNode as UsingNode);
				else
					ctx.DeclareScriptNode(currNode);
			}

			return ctx;
		}

		public IScript Compile()
		{
			prepareEntities();
			processClosures();
			prepareEntities();
			compileCore();
			finalizeAssembly();

			var inst = Activator.CreateInstance(ResolveType(RootTypeName));
			return inst as IScript;
		}

		/// <summary>
		/// Throws a new error.
		/// </summary>
		[ContractAnnotation("=> halt")]
		public void Error(string msg, params object[] args)
		{
			throw new LensCompilerException(string.Format(msg, args));
		}

		#region Properties

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
		/// The current most nested loop.
		/// </summary>
		internal LoopNode CurrentLoop { get; set; }

		/// <summary>
		/// The current most nested try block.
		/// </summary>
		internal TryNode CurrentTryBlock { get; set; }

		/// <summary>
		/// The current most nested catch block.
		/// </summary>
		internal CatchNode CurrentCatchClause { get; set; }

		internal Stack<MethodEntity> MethodResolutionStack { get; private set; }

		/// <summary>
		/// The lexical scope of the current scope.
		/// </summary>
		internal Scope CurrentScope
		{
			get { return CurrentMethod == null ? null : CurrentMethod.Scope; }
		}

		/// <summary>
		/// Gets an IL Generator for current method.
		/// </summary>
		internal ILGenerator CurrentILGenerator
		{
			get { return CurrentMethod == null ? null : CurrentMethod.Generator; }
		}

		/// <summary>
		/// An ID for closure types.
		/// </summary>
		internal int ClosureId;

		#endregion

		#region Fields

		/// <summary>
		/// The counter that allows multiple assemblies.
		/// </summary>
		private static int _AssemblyId;

		/// <summary>
		/// A helper that resolves built-in .NET types by their string signatures.
		/// </summary>
		private readonly TypeResolver _TypeResolver;

		/// <summary>
		/// The root of type lookup.
		/// </summary>
		private readonly Dictionary<string, TypeEntity> _DefinedTypes;

		#endregion
	}
}
