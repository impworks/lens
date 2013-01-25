using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree
{
	/// <summary>
	/// The base class for all syntax tree nodes.
	/// </summary>
	public abstract class NodeBase : LocationEntity
	{
		public Type GetExpressionType(Context ctx)
		{
			if (m_ExpressionType == null)
				m_ExpressionType = resolveExpressionType(ctx);

			return m_ExpressionType;
		}

		/// <summary>
		/// The type of the expression represented by this node.
		/// </summary>
		protected virtual Type resolveExpressionType(Context ctx)
		{
			return typeof (Unit);
		}

		/// <summary>
		/// Generates the IL for this node.
		/// </summary>
		/// <param name="ctx">Pointer to current context.</param>
		/// <param name="mustReturn">Flag indicating the node should return a value.</param>
		public abstract void Compile(Context ctx, bool mustReturn);

		/// <summary>
		/// Validates the node parameters.
		/// </summary>
		protected virtual void Validate()
		{ }

		/// <summary>
		/// Gets the list of child nodes.
		/// </summary>
		public virtual IEnumerable<NodeBase> GetChildNodes()
		{
			return new NodeBase[0];
		}

		/// <summary>
		/// Processes closures.
		/// </summary>
		public virtual void ProcessClosures(Context ctx)
		{
			foreach(var curr in GetChildNodes())
				curr.ProcessClosures(ctx);
		}

		/// <summary>
		/// Reports an error to the compiler.
		/// </summary>
		/// <param name="message">Error message.</param>
		/// <param name="args">Optional error arguments.</param>
		protected void Error(string message, params object[] args)
		{
			var msg = string.Format(message, args);
			throw new LensCompilerException(msg, StartLocation, EndLocation);
		}

		/// <summary>
		/// Throw a generic error for incorrect location setting.
		/// </summary>
		protected void LocationSetError()
		{
			throw new InvalidOperationException(string.Format("Location for entity '{0}' should not be set manually!", GetType().Name));
		}

		/// <summary>
		/// A cached version for expression type.
		/// </summary>
		private Type m_ExpressionType;
	}
}
