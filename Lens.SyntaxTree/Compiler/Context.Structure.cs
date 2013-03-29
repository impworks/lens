using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Utils;
using Lens.Utils;

namespace Lens.SyntaxTree.Compiler
{
	public partial class Context
	{
		#region Methods

		/// <summary>
		/// Imports an existing external type with given name.
		/// </summary>
		public void ImportType(string name, Type type)
		{
			if(Options.AllowSave)
				Error("Entities cannot be imported into a saveable assembly!");

			if (_DefinedTypes.ContainsKey(name))
				Error("Type '{0}' has already been defined!", name);

			var te = new TypeEntity(this, true)
			{
				Name = name,
				TypeInfo = type
			};
			_DefinedTypes.Add(name, te);
		}

		/// <summary>
		/// Imports an existing external method with given name.
		/// </summary>
		public void ImportFunction(string name, Delegate method)
		{
			if (Options.AllowSave)
				Error("Entities cannot be imported into a saveable assembly!");

			_DefinedTypes[RootTypeName].ImportMethod(name, method);
		}

		/// <summary>
		/// Imports a property registered in GlobalPropertyHelper into the lookup.
		/// </summary>
		public void ImportProperty<T>(string name, Func<T> getter, Action<T> setter = null)
		{
			if (Options.AllowSave)
				Error("Entities cannot be imported into a saveable assembly!");

			if(_DefinedProperties.ContainsKey(name))
				Error("Property '{0}' has already been imported!", name);

			var ent = GlobalPropertyHelper.RegisterProperty(ContextId, getter, setter);
			_DefinedProperties.Add(name, ent);
		}

		/// <summary>
		/// Creates a new type entity with given name.
		/// </summary>
		internal TypeEntity CreateType(string name, string parent = null, bool isSealed = false, bool defaultCtor = true, bool prepare = false)
		{
			var te = createTypeCore(name, isSealed, defaultCtor, prepare);
			te.ParentSignature = parent;
			return te;
		}

		/// <summary>
		/// Creates a new type entity with given name and a resolved type for parent.
		/// </summary>
		internal TypeEntity CreateType(string name, Type parent, bool isSealed = false, bool defaultCtor = true, bool prepare = false)
		{
			var te = createTypeCore(name, isSealed, defaultCtor, prepare);
			te.Parent = parent;
			return te;
		}

		/// <summary>
		/// Declares a new type.
		/// </summary>
		public void DeclareType(TypeDefinitionNode node)
		{
			var mainType = CreateType(node.Name, prepare: true);
			mainType.Kind = TypeEntityKind.Type;

			foreach (var curr in node.Entries)
			{
				var tagName = curr.Name;
				var labelType = CreateType(tagName, node.Name, isSealed: true, prepare: true);
				labelType.Kind = TypeEntityKind.TypeLabel;

				var ctor = labelType.CreateConstructor();
				if (curr.IsTagged)
				{
					labelType.CreateField("Tag", curr.TagType);

					var args = new HashList<FunctionArgument> { { "value", new FunctionArgument("value", curr.TagType) } };

					var staticCtor = RootType.CreateMethod(tagName, tagName, new string[0], true);
					ctor.Arguments = staticCtor.Arguments = args;

					ctor.Body.Add(
						Expr.SetMember(Expr.This(), "Tag", Expr.Get("value"))
					);

					staticCtor.Body.Add(
						Expr.New(tagName, Expr.Get("value"))
					);
				}
				else
				{
					var staticCtor = labelType.CreateMethod(tagName, tagName, new string[0], true);
					staticCtor.Body.Add(Expr.New(tagName));

					var pty = GlobalPropertyHelper.RegisterProperty(ContextId, labelType.TypeInfo, staticCtor, null);
					_DefinedProperties.Add(tagName, pty);
				}
			}
		}

		/// <summary>
		/// Declares a new record.
		/// </summary>
		public void DeclareRecord(RecordDefinitionNode node)
		{
			var recType = CreateType(node.Name, isSealed: true);
			recType.Kind = TypeEntityKind.Record;

			var recCtor = recType.CreateConstructor();

			foreach (var curr in node.Entries)
			{
				var field = recType.CreateField(curr.Name, curr.Type);
				var argName = "_" + field.Name.ToLowerInvariant();

				recCtor.Arguments.Add(argName, new FunctionArgument(argName, curr.Type));
				recCtor.Body.Add(
					Expr.SetMember(Expr.This(), field.Name, Expr.Get(argName))
				);
			}
		}

		/// <summary>
		/// Declares a new function.
		/// </summary>
		public void DeclareFunction(FunctionNode node)
		{
			var method = MainType.CreateMethod(node.Name, node.ReturnTypeSignature, node.Arguments, true);
			method.Body = node.Body;
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
			MainMethod.Body.Add(node);
		}

		/// <summary>
		/// Checks if the expression returns a value and has a specified type.
		/// </summary>
		public void CheckTypedExpression(NodeBase node, Type calculatedType = null, bool allowNull = false)
		{
			var type = calculatedType ?? node.GetExpressionType(this);

			if(!allowNull && type == typeof(NullType))
				Error(node, "Expression type cannot be inferred! Please use type casting to specify actual type.");

			if(type.IsVoid())
				Error(node, "Expression that returns a value is expected!");
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
			// prepare types first
			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareSelf();

			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareMembers();
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
		private TypeEntity createTypeCore(string name, bool isSealed, bool defaultCtor, bool prepare)
		{
			if (_DefinedTypes.ContainsKey(name))
				Error("Type '{0}' has already been defined!", name);

			var te = new TypeEntity(this)
			{
				Name = name,
				IsSealed = isSealed,
			};
			_DefinedTypes.Add(name, te);

			if (defaultCtor)
				te.CreateConstructor();

			if(prepare)
				te.PrepareSelf();

			return te;
		}

		/// <summary>
		/// Finalizes the assembly.
		/// </summary>
		private void finalizeAssembly()
		{
//			var ep = ResolveMethod(RootTypeName, RootMethodName);
//			MainAssembly.SetEntryPoint(ep, PEFileKinds.ConsoleApplication);

			foreach (var curr in _DefinedTypes)
				curr.Value.TypeBuilder.CreateType();

			if(Options.AllowSave)
				MainAssembly.Save("_MainModule.dll");
		}

		#endregion
	}
}