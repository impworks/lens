using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lens.Compiler;
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
		private Type _CachedExpressionType;

		#endregion

		#region Properties

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

		#region Main pass stages

		/// <summary>
		/// Calculates the type of expression represented by current node.
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

		protected virtual Type resolve(Context ctx, bool mustReturn)
		{
			return typeof (Unit);
		}

		/// <summary>
		/// Enables recursive children resolution & expansion.
		/// </summary>
		public virtual void Transform(Context ctx, bool mustReturn)
		{
			var children = GetChildren().ToArray();
			foreach (var child in children)
			{
				if (child == null || child.Node == null)
					continue;

				child.Node.Resolve(ctx, mustReturn);
				var sub = child.Node.Expand(ctx, mustReturn);
				if (sub != null)
				{
					child.Setter(sub);
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
		/// </summary>
		/// <returns>
		/// Null if no expansion is suitable, a NodeBase object instance otherwise.
		/// </returns>
		public virtual NodeBase Expand(Context ctx, bool mustReturn)
		{
			return null;
		}

		/// <summary>
		/// Processes closures.
		/// </summary>
		public virtual void ProcessClosures(Context ctx)
		{
			foreach (var child in GetChildren())
				if (child != null && child.Node != null)
					child.Node.ProcessClosures(ctx);
		}

		#endregion

		/// <summary>
		/// Generates the IL for this node.
		/// </summary>
		/// <param name="ctx">Pointer to current context.</param>
		/// <param name="mustReturn">Flag indicating the node should return a value.</param>
		public void Emit(Context ctx, bool mustReturn)
		{
			if (IsConstant && ctx.Options.UnrollConstants)
			{
				if (mustReturn)
					emitConstant(ctx);
			}
			else
			{
				emitCode(ctx, mustReturn);
			}
		}

		protected virtual void emitCode(Context ctx, bool mustReturn)
		{
			throw new InvalidOperationException(
				string.Format(
					"Node '{0}' neither has a body nor was expanded!",
					GetType()
				)
			);
		}

		/// <summary>
		/// Gets the list of child nodes.
		/// </summary>
		public virtual IEnumerable<NodeChild> GetChildren()
		{
			yield break;
		}

		#region Private helpers

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
		/// Emit the value of current node as a constant.
		/// </summary>
		private void emitConstant(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;
			var value = ConstantValue;

			if (value is bool)
				gen.EmitConstant((bool)value);
			else if (value is int)
				gen.EmitConstant((int)value);
			else if (value is long)
				gen.EmitConstant((long)value);
			else if (value is double)
				gen.EmitConstant((double)value);
			else if (value is string)
				gen.EmitConstant((string)value);
			else
				emitCode(ctx, true);
		}

		#endregion
	}
}
