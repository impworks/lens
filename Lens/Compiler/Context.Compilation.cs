using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using Lens.Compiler.Entities;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler
{
	internal partial class Context
	{
		#region Compilation essentials

		public IScript Compile(IEnumerable<NodeBase> nodes)
		{
			loadTree(nodes);
			transformTree();
			emitCode();
			finalizeAssembly();

			var inst = Activator.CreateInstance(ResolveType(EntityNames.MainTypeName));
			return inst as IScript;
		}

		/// <summary>
		/// Initializes the context from a stream of nodes.
		/// </summary>
		private void loadTree(IEnumerable<NodeBase> nodes)
		{
			foreach (var currNode in nodes)
			{
				if (currNode is TypeDefinitionNode)
					declareType(currNode as TypeDefinitionNode);
				else if (currNode is RecordDefinitionNode)
					declareRecord(currNode as RecordDefinitionNode);
				else if (currNode is FunctionNode)
					declareFunction(currNode as FunctionNode);
				else if (currNode is UsingNode)
					declareOpenNamespace(currNode as UsingNode);
				else
					declareScriptNode(currNode);
			}
		}

		/// <summary>
		/// Traverses all nodes, resolving and transforming them.
		/// </summary>
		private void transformTree()
		{
			if (Options.AllowSave && Options.SaveAsExe)
				createEntryPoint();

			prepareEntities();

			while (_UnprocessedMethods.Count > 0)
			{
				var methods = _UnprocessedMethods.ToArray();
				_UnprocessedMethods.Clear();

				foreach (var curr in methods)
				{
					curr.TransformBody();
					curr.ProcessClosures();
				}

				prepareEntities();
			}
		}

		/// <summary>
		/// Generates the source code for all the declared methods.
		/// </summary>
		private void emitCode()
		{
			foreach (var curr in _DefinedTypes)
				curr.Value.Compile();
		}

		/// <summary>
		/// Finalizes the assembly.
		/// </summary>
		private void finalizeAssembly()
		{
			foreach (var curr in _DefinedTypes)
				if (!curr.Value.IsImported)
					curr.Value.TypeBuilder.CreateType();

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

		#endregion

		#region Compilation helpers

		/// <summary>
		/// Declares a new type.
		/// </summary>
		private void declareType(TypeDefinitionNode node)
		{
			if (node.Name == "_")
				Error(CompilerMessages.UnderscoreName);

			var mainType = CreateType(node.Name);
			mainType.Kind = TypeEntityKind.Type;

			foreach (var curr in node.Entries)
			{
				var tagName = curr.Name;
				var labelType = CreateType(tagName, mainType.TypeInfo, isSealed: true, defaultCtor: false);
				labelType.Kind = TypeEntityKind.TypeLabel;

				var ctor = labelType.CreateConstructor();
				if (curr.IsTagged)
				{
					labelType.CreateField("Tag", curr.TagType, prepare: false);

					var args = new HashList<FunctionArgument> { { "value", new FunctionArgument("value", curr.TagType) } };

					var staticCtor = MainType.CreateMethod(tagName, tagName, new string[0], true);
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
		private void declareRecord(RecordDefinitionNode node)
		{
			if (node.Name == "_")
				Error(CompilerMessages.UnderscoreName);

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
		private void declareFunction(FunctionNode node)
		{
			if (node.Name == "_")
				Error(CompilerMessages.UnderscoreName);

			validateFunction(node);
			var method = MainType.CreateMethod(node.Name, node.ReturnTypeSignature, node.Arguments, true);
			method.IsPure = node.IsPure;
			method.Body = node.Body;
		}

		/// <summary>
		/// Opens a new namespace for current script.
		/// </summary>
		private void declareOpenNamespace(UsingNode node)
		{
			if (!Namespaces.ContainsKey(node.Namespace))
				Namespaces.Add(node.Namespace, true);
		}

		/// <summary>
		/// Adds a new node to the main script's body.
		/// </summary>
		private void declareScriptNode(NodeBase node)
		{
			MainMethod.Body.Add(node);
		}

		/// <summary>
		/// Creates all assembly-level entities for types, fields, methods and constructors.
		/// </summary>
		private void prepareEntities()
		{
			if (_UnpreparedTypes.Count > 0)
			{
				foreach (var curr in _UnpreparedTypes)
					curr.PrepareSelf();

				foreach (var curr in _UnpreparedTypes)
					curr.CreateEntities();

				_UnpreparedTypes.Clear();
			}

			if (_UnpreparedTypeContents.Count > 0)
			{
				foreach (var curr in _UnpreparedTypeContents)
					curr.PrepareSelf();

				_UnpreparedTypeContents.Clear();
			}
		}

		/// <summary>
		/// Creates the entry point for an assembly if it is supposed to be saved.
		/// The entry point method basically calls the Run method and discards the result.
		/// </summary>
		private void createEntryPoint()
		{
			var ep = MainType.CreateMethod(EntityNames.EntryPointMethodName, "Void", args: null, isStatic: true);
			ep.Body = Expr.Block(
				Expr.Invoke(Expr.New(EntityNames.MainTypeName), "Run"),
				Expr.Unit()
			);
		}

		#endregion
	}
}
