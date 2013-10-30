using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		/// <summary>
		/// Checks if the current node is a constant.
		/// </summary>
		public virtual bool IsConstant { get { return false; } }

		public virtual dynamic ConstantValue { get { throw new InvalidOperationException("Not a constant!"); } }

		/// <summary>
		/// The cached expression type.
		/// </summary>
		private Type m_ExpressionType;

		/// <summary>
		/// Calculates the type of expression represented by current node.
		/// </summary>
		[DebuggerStepThrough]
		public Type GetExpressionType(Context ctx, bool mustReturn = true)
		{
			if (m_ExpressionType == null)
			{
				m_ExpressionType = resolveExpressionType(ctx, mustReturn);
				SafeModeCheckType(ctx, m_ExpressionType);
			}

			return m_ExpressionType;
		}

		protected virtual Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return typeof (Unit);
		}

		/// <summary>
		/// Generates the IL for this node.
		/// </summary>
		/// <param name="ctx">Pointer to current context.</param>
		/// <param name="mustReturn">Flag indicating the node should return a value.</param>
		public void Compile(Context ctx, bool mustReturn)
		{
			if (IsConstant && ctx.Options.UnrollConstants)
			{
				if(mustReturn)
					emitConstant(ctx);
			}
			else
			{
				compile(ctx, mustReturn);
			}
		}

		protected abstract void compile(Context ctx, bool mustReturn);

		/// <summary>
		/// Emit the value of current node as a constant.
		/// </summary>
		private void emitConstant(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
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
				compile(ctx, true);
		}

		/// <summary>
		/// Gets the list of child nodes.
		/// </summary>
		public virtual IEnumerable<NodeBase> GetChildNodes()
		{
			return null;
		}

		/// <summary>
		/// Processes closures.
		/// </summary>
		public virtual void ProcessClosures(Context ctx)
		{
			var children = GetChildNodes();
			if (children == null)
				return;

			foreach (var child in children)
				if (child != null)
					child.ProcessClosures(ctx);
		}

		/// <summary>
		/// Performs pre-analysis.
		/// Is required for yield statement detection.
		/// </summary>
		public virtual void Analyze(Context ctx)
		{
			var children = GetChildNodes();
			if (children == null)
				return;

			foreach (var child in children)
				if(child != null)
					child.Analyze(ctx);
		}

		/// <summary>
		/// Reports an error to the compiler.
		/// </summary>
		/// <param name="message">Error message.</param>
		/// <param name="args">Optional error arguments.</param>
		[ContractAnnotation("=> halt")]
		[DebuggerStepThrough]
		public void Error(string message, params object[] args)
		{
			Error(this, message, args);
		}

		/// <summary>
		/// Reports an error to the compiler.
		/// </summary>
		/// <param name="message">Error message.</param>
		/// <param name="args">Optional error arguments.</param>
		[ContractAnnotation("=> halt")]
		[DebuggerStepThrough]
		public static void Error(LocationEntity entity, string message, params object[] args)
		{
			var msg = string.Format(message, args);
			throw new LensCompilerException(msg, entity);
		}

		/// <summary>
		/// Throws a generic error for incorrect location setting.
		/// </summary>
		[ContractAnnotation("=> halt")]
		[DebuggerStepThrough]
		protected void LocationSetError()
		{
			throw new InvalidOperationException(string.Format("Location for entity '{0}' should not be set manually!", GetType().Name));
		}

		/// <summary>
		/// Throws an error that the current type is not alowed in safe mode.
		/// </summary>
		protected void SafeModeCheckType(Context ctx, Type type)
		{
			if(!ctx.IsTypeAllowed(type))
				Error(CompilerMessages.SafeModeIllegalType, type.FullName);
		}

		/// <summary>
		/// Saves the result of arbitrary code block to a local temp variable.
		/// </summary>
		protected void SaveToTempLocal(Context ctx, LocalName name, Action act)
		{
			var gen = ctx.CurrentILGenerator;

			if (name.Mapping == LocalNameMapping.Field)
				gen.EmitLoadArgument(0);

			act();

			if(name.Mapping == LocalNameMapping.Field)
				gen.EmitSaveField(ctx.CurrentType.ResolveField(name.BackingFieldName).FieldBuilder);
			else
				gen.EmitSaveLocal(name);
		}

		/// <summary>
		/// Loads the value of a local temp variable onto a stack.
		/// </summary>
		protected void LoadFromTempLocal(Context ctx, LocalName name, bool pointer = false)
		{
			var gen = ctx.CurrentILGenerator;

			if (name.Mapping == LocalNameMapping.Field)
			{
				gen.EmitLoadArgument(0);
				gen.EmitLoadField(ctx.CurrentType.ResolveField(name.BackingFieldName).FieldBuilder, pointer);
			}
			else
			{
				gen.EmitLoadLocal(name, pointer);
			}
		}
	}
}
