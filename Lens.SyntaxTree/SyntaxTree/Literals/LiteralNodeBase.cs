using System;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// The base class for literal nodes.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class LiteralNodeBase<T> : NodeBase
	{
		/// <summary>
		/// The literal value.
		/// </summary>
		public T Value { get; set; }

		public override Type GetExpressionType()
		{
			return typeof (T);
		}
	}
}
