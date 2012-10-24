using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree
{
	/// <summary>
	/// The base class for all syntax tree nodes.
	/// </summary>
	public abstract class NodeBase
	{
		/// <summary>
		/// Current node's starting position (for error reporting).
		/// </summary>
		public virtual LexemLocation StartLocation { get; set; }

		/// <summary>
		/// Current node's ending position (for error reporting).
		/// </summary>
		public virtual LexemLocation EndLocation { get; set; }

		/// <summary>
		/// The type of the expression represented by this node.
		/// </summary>
		public abstract Type GetExpressionType();

		/// <summary>
		/// Generates the IL for this node.
		/// </summary>
		public abstract void Compile();

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

		protected static void LocationSetError()
		{
			throw new InvalidOperationException("Compound node's location cannot be set manually!");
		}

		/// <summary>
		/// A cached version for expression type.
		/// </summary>
		protected Type m_ExpressionType;
	}
}
