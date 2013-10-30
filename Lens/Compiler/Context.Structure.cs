using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Literals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler
{
	internal partial class Context
	{
		#region Methods

		/// <summary>
		/// Imports an existing external type with given name.
		/// </summary>
		public void ImportType(string name, Type type)
		{
			if(Options.AllowSave)
				Error(CompilerMessages.ImportIntoSaveableAssembly);

			if (_DefinedTypes.ContainsKey(name))
				Error(CompilerMessages.TypeDefined, name);

			var te = new TypeEntity(this)
			{
				Name = name,
				TypeInfo = type,
				Kind = TypeEntityKind.Imported
			};
			_DefinedTypes.Add(name, te);
		}

		/// <summary>
		/// Imports a method from a standard library.
		/// </summary>
		public void ImportFunctionUnchecked(string name, MethodInfo method, bool check = false)
		{
			_DefinedTypes[EntityNames.MainTypeName].ImportMethod(name, method, check);
		}

		/// <summary>
		/// Imports an existing external method with given name.
		/// </summary>
		public void ImportFunction(string name, MethodInfo method)
		{
			if (Options.AllowSave)
				Error(CompilerMessages.ImportIntoSaveableAssembly);

			ImportFunctionUnchecked(name, method, true);
		}

		/// <summary>
		/// Imports a property into GlobalPropertyHelper and context lookup tables.
		/// </summary>
		public void ImportProperty<T>(string name, Func<T> getter, Action<T> setter = null)
		{
			if (Options.AllowSave)
				Error(CompilerMessages.ImportIntoSaveableAssembly);

			if(_DefinedProperties.ContainsKey(name))
				Error(CompilerMessages.PropertyImported, name);

			var ent = GlobalPropertyHelper.RegisterProperty(ContextId, getter, setter);
			_DefinedProperties.Add(name, ent);
		}

		/// <summary>
		/// Registers a property declared inside the script.
		/// Is used for type syntactic sugar.
		/// </summary>
		public void RegisterProperty(string name, Type type, MethodEntity getter, MethodEntity setter = null)
		{
			if (Options.AllowSave)
				Error(CompilerMessages.ImportIntoSaveableAssembly);

			if (_DefinedProperties.ContainsKey(name))
				Error(CompilerMessages.PropertyImported, name);

			var pty = GlobalPropertyHelper.RegisterProperty(ContextId, type, getter, setter);
			_DefinedProperties.Add(name, pty);
		}

		/// <summary>
		/// Creates a new type entity with given name.
		/// </summary>
		internal TypeEntity CreateType(string name, string parent = null, bool isSealed = false, bool defaultCtor = true, bool prepare = false)
		{
			var te = createTypeCore(name, isSealed, defaultCtor);
			te.ParentSignature = parent;

			if(prepare)
				te.PrepareSelf();

			return te;
		}

		/// <summary>
		/// Creates a new type entity with given name and a resolved type for parent.
		/// </summary>
		internal TypeEntity CreateType(string name, Type parent, bool isSealed = false, bool defaultCtor = true, bool prepare = false)
		{
			var te = createTypeCore(name, isSealed, defaultCtor);
			te.Parent = parent;

			if (prepare)
				te.PrepareSelf();

			return te;
		}

		/// <summary>
		/// Declares a new type.
		/// </summary>
		public void DeclareType(TypeDefinitionNode node)
		{
			var type = CreateType(node.Name, prepare: true);
			type.Kind = TypeEntityKind.Type;
			type.CreateTypeMembers(node.Entries);
		}

		/// <summary>
		/// Declares a new record.
		/// </summary>
		public void DeclareRecord(RecordDefinitionNode node)
		{
			var rec = CreateType(node.Name, isSealed: true);
			rec.Kind = TypeEntityKind.Record;
			rec.CreateRecordMembers(node.Entries);
		}

		/// <summary>
		/// Declares a new function.
		/// </summary>
		public void DeclareFunction(FunctionNode node)
		{
			validateFunction(node);
			var method = MainType.CreateMethod(node.Name, node.ReturnTypeSignature, node.Arguments, true);
			method.IsPure = node.IsPure;
			method.Body = node.Body;
		}

		/// <summary>
		/// Opens a new namespace for current script.
		/// </summary>
		public void DeclareOpenNamespace(UsingNode node)
		{
			if(!Namespaces.ContainsKey(node.Namespace))
				Namespaces.Add(node.Namespace, true);
		}

		/// <summary>
		/// Adds a new node to the main script's body.
		/// </summary>
		public void DeclareScriptNode(NodeBase node)
		{
			MainMethod.Body.Add(node);
		}

		/// <summary>
		/// Checks if the expression returns a value and has a specified type.
		/// </summary>
		public void CheckTypedExpression(NodeBase node, Type calculatedType = null, bool allowNull = false)
		{
			var type = calculatedType ?? node.GetExpressionType(this);

			if(!allowNull && type == typeof(NullType))
				Error(node, CompilerMessages.ExpressionNull);

			if(type.IsVoid())
				Error(node, CompilerMessages.ExpressionVoid);
		}

		public int GetClosureId()
		{
			return ++_ClosureId;
		}

		/// <summary>
		/// Adds a new method for closure processing.
		/// </summary>
		public void DeclareMethodForProcessing(MethodEntityBase method)
		{
			_UnprocessedMethods.Add(method);
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
		/// Initializes the context from a stream of nodes.
		/// </summary>
		private void loadNodes(IEnumerable<NodeBase> nodes)
		{
			foreach (var currNode in nodes)
			{
				if (currNode is TypeDefinitionNode)
					DeclareType(currNode as TypeDefinitionNode);
				else if (currNode is RecordDefinitionNode)
					DeclareRecord(currNode as RecordDefinitionNode);
				else if (currNode is FunctionNode)
					DeclareFunction(currNode as FunctionNode);
				else if (currNode is UsingNode)
					DeclareOpenNamespace(currNode as UsingNode);
				else
					DeclareScriptNode(currNode);
			}
		}

		/// <summary>
		/// Prepares the assembly entities for the type list.
		/// </summary>
		private void prepareEntities()
		{
			// prepare types first, so members can reference them
			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareSelf();

			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareMembers();
		}

		/// <summary>
		/// Creates iterators and pure wrappers.
		/// </summary>
		private void createMethodRelatedEntities(MethodEntity method)
		{
			if(method.IsPure)
				createPureWrapper(method);

			if(method.YieldStatements.Count > 0 && method.ContainerType.Kind != TypeEntityKind.Iterator)
				createIterator(method);
		}

		/// <summary>
		/// Compiles the source code for all the declared classes.
		/// </summary>
		private void compileCore()
		{
			foreach (var curr in _DefinedTypes)
				curr.Value.Compile();
		}

		/// <summary>
		/// Create a type entry without setting its parent info.
		/// </summary>
		private TypeEntity createTypeCore(string name, bool isSealed, bool defaultCtor)
		{
			if (_DefinedTypes.ContainsKey(name))
				Error(CompilerMessages.TypeDefined, name);

			var te = new TypeEntity(this)
			{
				Name = name,
				IsSealed = isSealed,
			};
			_DefinedTypes.Add(name, te);

			if (defaultCtor)
				te.CreateConstructor();

			return te;
		}

		/// <summary>
		/// Finalizes the assembly.
		/// </summary>
		private void finalizeAssembly()
		{
			foreach (var curr in _DefinedTypes)
			{
				if (!curr.Value.IsImported)
				{
					curr.Value.DefineImplementations();
					curr.Value.TypeBuilder.CreateType();
				}
			}

			if (Options.AllowSave)
			{
				if (Options.SaveAsExe)
				{
					var ep = ResolveMethod(ResolveType(EntityNames.MainTypeName), EntityNames.EntryPointMethodName);
					MainAssembly.SetEntryPoint(ep.MethodInfo, PEFileKinds.ConsoleApplication);
				}

				MainAssembly.Save(Options.FileName);
			}
		}

		/// <summary>
		/// Checks if the function does not collide with internal functions.
		/// </summary>
		private void validateFunction(FunctionNode node)
		{
			if (node.Arguments.Count > 0)
				return;

			if (node.Name == EntityNames.RunMethodName || node.Name == EntityNames.EntryPointMethodName)
				Error(CompilerMessages.ReservedFunctionRedefinition, node.Name);
		}

		#endregion
	}
}