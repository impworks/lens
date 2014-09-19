using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree
{
	/// <summary>
	/// The base class for all syntax tree nodes.
	/// </summary>
	internal abstract class NodeBase : LocationEntity
	{
		#region Fields

		/// <summary>
		/// The cached expression type.
		/// </summary>
		protected Type _CachedExpressionType;

		#endregion

		#region Resolve

		/// <summary>
		/// Returns or resolves the type of expression represented by current node.
		/// </summary>
		[DebuggerStepThrough]
		public Type Resolve(Context ctx, bool mustReturn = true)
		{
			if (_CachedExpressionType == null)
			{
				_CachedExpressionType = resolve(ctx, mustReturn);
				checkTypeInSafeMode(ctx, _CachedExpressionType);
			}

			return _CachedExpressionType;
		}

		/// <summary>
		/// Resolves the expression type.
		/// Must be overridden in child types if they represent a meaninful value.
		/// </summary>
		protected virtual Type resolve(Context ctx, bool mustReturn)
		{
			return typeof (UnitType);
		}

		#endregion

		#region Transform & Expand

		/// <summary>
		/// Enables recursive children resolution & expansion.
		/// </summary>
		public virtual void Transform(Context ctx, bool mustReturn)
		{
			var children = getChildren().ToArray();
			foreach (var child in children)
			{
				if (child == null || child.Node == null)
					continue;

				child.Node.Resolve(ctx, mustReturn);
				var sub = child.Node.expand(ctx, mustReturn);
				if (sub != null)
				{
					child.Setter(sub);
					sub.Resolve(ctx, mustReturn);
					sub.Transform(ctx, mustReturn);
				}
				else
				{
					child.Node.Transform(ctx, mustReturn);
				}
			}
		}

		/// <summary>
		/// Checks if current node can be expanded into another node or a set of nodes.
		/// To be overridden in child nodes if required.
		/// </summary>
		/// <returns>
		/// Null if no expansion is suitable, a NodeBase object instance otherwise.
		/// </returns>
		protected virtual NodeBase expand(Context ctx, bool mustReturn)
		{
			return null;
		}

		/// <summary>
		/// Gets the list of child nodes.
		/// </summary>
		protected virtual IEnumerable<NodeChild> getChildren()
		{
			yield break;
		}

		#endregion

		#region Process closures

		/// <summary>
		/// Processes closures for node and its children.
		/// </summary>
		public virtual void ProcessClosures(Context ctx)
		{
			foreach (var child in getChildren())
				if (child != null && child.Node != null)
					child.Node.ProcessClosures(ctx);
		}

		#endregion

		#region Emit

		/// <summary>
		/// Generates the IL for this node.
		/// </summary>
		/// <param name="ctx">Pointer to current context.</param>
		/// <param name="mustReturn">Flag indicating the node should return a value.</param>
		public void Emit(Context ctx, bool mustReturn)
		{
			if (IsConstant && !mustReturn)
				return;

			emitCode(ctx, mustReturn);
		}

		/// <summary>
		/// Emits the IL opcodes that represents the current node.
		/// </summary>
		protected virtual void emitCode(Context ctx, bool mustReturn)
		{
			throw new InvalidOperationException(
				string.Format(
					"Node '{0}' neither has a body nor was expanded!",
					GetType()
				)
			);
		}

		#endregion

		#region Constant checkers

		/// <summary>
		/// Checks if the current node is a constant.
		/// </summary>
		public virtual bool IsConstant
		{
			get { return false; }
		}

		/// <summary>
		/// Returns a constant value corresponding to the current node.
		/// </summary>
		public virtual dynamic ConstantValue
		{
			get { throw new InvalidOperationException("Not a constant!"); }
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Reports an error to the compiler.
		/// </summary>
		/// <param name="message">Error message.</param>
		/// <param name="args">Optional error arguments.</param>
		[ContractAnnotation("=> halt")]
		[DebuggerStepThrough]
		protected void error(string message, params object[] args)
		{
			error(this, message, args);
		}

		/// <summary>
		/// Reports an error to the compiler.
		/// </summary>
		/// <param name="message">Error message.</param>
		/// <param name="args">Optional error arguments.</param>
		[ContractAnnotation("=> halt")]
		[DebuggerStepThrough]
		protected void error(LocationEntity entity, string message, params object[] args)
		{
			var msg = string.Format(message, args);
			throw new LensCompilerException(msg, entity);
		}

		/// <summary>
		/// Throws an error that the current type is not alowed in safe mode.
		/// </summary>
		protected void checkTypeInSafeMode(Context ctx, Type type)
		{
			if (!ctx.IsTypeAllowed(type))
				error(CompilerMessages.SafeModeIllegalType, type.FullName);
		}

		/// <summary>
		/// Re-infers the lambda if argument types were not specified before.
		/// </summary>
		protected static void ensureLambdaInferred(Context ctx, NodeBase canBeLambda, Type delegateType)
		{
			var lambda = canBeLambda as LambdaNode;
			if (lambda == null)
				return;

			var wrapper = ReflectionHelper.WrapDelegate(delegateType);
			if(!wrapper.ReturnType.IsGenericParameter)
				lambda.SetInferredReturnType(wrapper.ReturnType);

			lambda.Resolve(ctx);

			if (lambda.MustInferArgTypes)
				lambda.SetInferredArgumentTypes(wrapper.ArgumentTypes);
		}

		#endregion
	}
}
