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
		/// Creates a new type entity with given name.
		/// </summary>
		internal TypeEntity CreateType(string name, string parent = null, bool isSealed = false)
		{
			if(_DefinedTypes.ContainsKey(name))
				Error("Type '{0}' has already been defined!", name);

			var te = new TypeEntity(this)
			{
				Name = name,
				ParentSignature = parent,
				IsSealed = isSealed
			};
			_DefinedTypes.Add(name, te);
			return te;
		}

		/// <summary>
		/// Resolves a type by its string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// </summary>
		public Type ResolveType(string signature)
		{
			try
			{
				TypeEntity type;
				return _DefinedTypes.TryGetValue(signature, out type)
					       ? type.TypeBuilder
					       : _TypeResolver.ResolveType(signature);
			}
			catch (ArgumentException ex)
			{
				throw new LensCompilerException(ex.Message);
			}
		}

		/// <summary>
		/// Tries to search for a declared type.
		/// </summary>
		internal TypeEntity FindType(string name)
		{
			TypeEntity entity;
			_DefinedTypes.TryGetValue(name, out entity);
			return entity;
		}

		/// <summary>
		/// Resolves a method by it's name and agrument list.
		/// </summary>
		public MethodInfo ResolveMethod(string typeName, string methodName, Type[] args = null)
		{
			if(args == null)
				args = new Type[0];

			var te = FindType(typeName);
			if (te != null)
				return te.ResolveMethod(methodName, args);

			var type = ResolveType(typeName);
			return type.GetMethod(methodName, args);
		}

		/// <summary>
		/// Resolves a field by it's name.
		/// </summary>
		public FieldInfo ResolveField(string typeName, string fieldName)
		{
			var te = FindType(typeName);
			if (te != null)
				return te.ResolveField(fieldName);

			var type = ResolveType(typeName);
			return type.GetField(fieldName);
		}

		/// <summary>
		/// Resolves a constructor by it's argument list.
		/// </summary>
		public ConstructorInfo ResolveConstructor(string typeName, Type[] args = null)
		{
			if (args == null)
				args = new Type[0];

			var te = FindType(typeName);
			if (te != null)
				return te.ResolveConstructor(args);

			var type = ResolveType(typeName);
			return type.GetConstructor(args);
		}

		/// <summary>
		/// Resolves a type by its signature.
		/// </summary>
		/// <param name="signature"></param>
		/// <returns></returns>
		public Type ResolveType(TypeSignature signature)
		{
			try
			{
				return ResolveType(signature.Signature);
			}
			catch (LensCompilerException ex)
			{
				ex.BindToLocation(signature);
				throw;
			}
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
			var types = _DefinedTypes.ToArray();

			// ProcessClosures() usually processes new types, hence the caching to array
			foreach (var currType in types)
				currType.Value.ProcessClosures();
		}

		/// <summary>
		/// Prepares the assembly entities for the type list.
		/// </summary>
		private void prepareEntities()
		{
			// prepare types first
			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareSelf();

			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareMembers();
		}

		/// <summary>
		/// Compiles the source code for all the declared classes.
		/// </summary>
		private void compileInternal()
		{
			foreach (var curr in _DefinedTypes)
				curr.Value.Compile();
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
//			var type = new TypeEntity
//			{
//				Name = RootTypeName,
//				GenerateDefaultConstructor = true
//			};
//
//			var me = new MethodEntity
//			{
//				Name = RootMethodName,
//				IsStatic = true,
//				IsVirtual = false,
//			};
		}

		#endregion
	}
}
 