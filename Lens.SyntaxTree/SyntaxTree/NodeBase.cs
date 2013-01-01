using System;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree
{
	/// <summary>
	/// The base class for all syntax tree nodes.
	/// </summary>
	public abstract class NodeBase : LocationEntity
	{
		/// <summary>
		/// The type of the expression represented by this node.
		/// </summary>
		/// <param name="ctx"></param>
		public virtual Type GetExpressionType(Context ctx)
		{
			return typeof (Unit);
		}

		/// <summary>
		/// Generates the IL for this node.
		/// </summary>
		/// <param name="ctx"></param>
		/// <param name="mustReturn"></param>
		public abstract void Compile(Context ctx, bool mustReturn);

		/// <summary>
		/// Validates the node parameters.
		/// </summary>
		protected virtual void Validate()
		{
		}

		/// <summary>
		/// Reports an error to the compiler.
		/// </summary>
		/// <param name="message">Error message.</param>
		/// <param name="args">Optional error arguments.</param>
		protected void Error(string message, params object[] args)
		{
			var msg = string.Format(message, args);
			throw new ParseException(msg, StartLocation, EndLocation);
		}

		protected void LocationSetError()
		{
			throw new InvalidOperationException(string.Format("Location for entity '{0}' should not be set manually!", GetType().Name));
		}

		/// <summary>
		/// A cached version for expression type.
		/// </summary>
		protected Type m_ExpressionType;
	}
}
