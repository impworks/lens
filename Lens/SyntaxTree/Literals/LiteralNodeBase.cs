using System;
using System.Collections.Generic;
using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// The base class for literal nodes.
	/// </summary>
	internal abstract class LiteralNodeBase<T> : NodeBase, ILiteralNode
	{
		#region Fields

		/// <summary>
		/// The literal value.
		/// </summary>
		protected T Value { get; set; }

		#endregion

		#region Constant checkers

		public override bool IsConstant => true;
	    public override object ConstantValue => Value;

	    #endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			return typeof (T);
		}

		public Type LiteralType => typeof (T);

	    #endregion

		#region Debug

		protected bool Equals(LiteralNodeBase<T> other)
		{
			return EqualityComparer<T>.Default.Equals(Value, other.Value);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((LiteralNodeBase<T>)obj);
		}

		public override int GetHashCode()
		{
			return EqualityComparer<T>.Default.GetHashCode(Value);
		}

		public override string ToString()
		{
			return string.Format("{0}({1})", typeof (T).Name, Value);
		}

		#endregion
	}

	/// <summary>
	/// Marker interface for all literal expressions.
	/// </summary>
	internal interface ILiteralNode
	{
		Type LiteralType { get; }
	}
}
