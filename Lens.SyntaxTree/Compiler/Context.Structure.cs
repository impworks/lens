using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;

namespace Lens.SyntaxTree.Compiler
{
	public partial class Context
	{
		#region Methods

		/// <summary>
		/// Resolves a type by it's string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// </summary>
		public Type ResolveType(string signature)
		{
			// todo: error reporting!
			TypeEntity type;
			return _DefinedTypes.TryGetValue(signature, out type)
				? type.TypeBuilder
				: _TypeResolver.ResolveType(signature);
		}

		/// <summary>
		/// Resolves a method by it's name and agrument list.
		/// </summary>
		/// <param name="type">Type to search in.</param>
		/// <param name="method">Method name.</param>
		/// <param name="args">A list of argument types.</param>
		/// <returns></returns>
		public MethodInfo ResolveMethod(string type, string method, string[] args = null)
		{
			// todo: error reporting!
			var argTypes = args.Select(ResolveType).ToArray();
			var t = ResolveType(type);
			if (t is TypeBuilder)
			{
				var meth = findMethodByArgs(_DefinedTypes[type].Methods[method], argTypes);
				return (meth as MethodEntity).MethodBuilder;
			}

			return t.GetMethod(method, argTypes);
		}

		/// <summary>
		/// Resolves a field by it's name.
		/// </summary>
		/// <param name="type">Type to search in.</param>
		/// <param name="field">Field name.</param>
		/// <returns></returns>
		public FieldInfo ResolveField(string type, string field)
		{
			// todo: error reporting!
			var t = ResolveType(type);
			return t is TypeBuilder
				? _DefinedTypes[type].Fields[field].FieldBuilder
				: t.GetField(field);
		}

		/// <summary>
		/// Resolves a constructor by it's argument list.
		/// </summary>
		/// <param name="type">Type to search in.</param>
		/// <param name="args">A list of argument types.</param>
		/// <returns></returns>
		public ConstructorInfo ResolveConstructor(string type, string[] args = null)
		{
			// todo: error reporting!
			var argTypes = args.Select(ResolveType).ToArray();
			var t = ResolveType(type);
			if (t is TypeBuilder)
			{
				var ctor = findMethodByArgs(_DefinedTypes[type].Constructors, argTypes);
				return (ctor as ConstructorEntity).ConstructorBuilder;
			}

			return t.GetConstructor(argTypes);
		}

		/// <summary>
		/// Declares a new type.
		/// </summary>
		public void DeclareType(TypeDefinitionNode node)
		{
			
		}

		/// <summary>
		/// Declares a new record.
		/// </summary>
		public void DeclareRecord(RecordDefinitionNode node)
		{
			
		}

		/// <summary>
		/// Declares a new function.
		/// </summary>
		public void DeclareFunction(FunctionNode node)
		{
			
		}

		/// <summary>
		/// Opens a new namespace for current script.
		/// </summary>
		public void DeclareOpenNamespace(UsingNode node)
		{
			_TypeResolver.AddNamespace(node.Namespace);
		}

		/// <summary>
		/// Adds a new node to the main script's body.
		/// </summary>
		public void DeclareScriptNode(NodeBase node)
		{
			_ScriptBody.Body.Add(node);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Generates a unique assembly name.
		/// </summary>
		private static string getAssemblyName()
		{
			lock (typeof(Context))
				_AssemblyId++;
			return "_CompiledAssembly" + _AssemblyId;
		}

		/// <summary>
		/// Traverses the syntactic tree, searching for closures and curried methods.
		/// </summary>
		private void processClosures()
		{
			
		}

		/// <summary>
		/// Prepares the assembly entities for the type list.
		/// </summary>
		private void prepareEntities()
		{
			// prepare types first
			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareSelf(this);

			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareMembers(this);
		}

		/// <summary>
		/// Compiles the source code for all the declared classes.
		/// </summary>
		private void compileInternal()
		{
			foreach (var curr in _DefinedTypes)
				curr.Value.Compile(this);
		}

		/// <summary>
		/// Finalizes the assembly.
		/// </summary>
		private void finalizeAssembly()
		{
			var ep = ResolveMethod(RootTypeName, RootMethodName);
			MainAssembly.SetEntryPoint(ep, PEFileKinds.ConsoleApplication);
			foreach (var curr in _DefinedTypes)
				curr.Value.TypeBuilder.CreateType();
		}

		/// <summary>
		/// Declare the root type of the assembly.
		/// </summary>
		private void declareRootType()
		{
			var type = new TypeEntity
			{
				Name = RootTypeName,
				Parent = typeof (object),
				GenerateDefaultConstructor = true
			};

			var me = new MethodEntity
			{
				Name = RootMethodName,
				IsStatic = true,
				IsVirtual = false,
			};

			type.AddEntity(me);
		}

		/// <summary>
		/// Resolve the best-matching method of the list.
		/// </summary>
		/// <param name="methods">Source list of methods.</param>
		/// <param name="argTypes">List of argument types.</param>
		/// <returns></returns>
		private MethodEntityBase findMethodByArgs(IEnumerable<MethodEntityBase> methods, Type[] argTypes)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
