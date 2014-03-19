using System;
using System.Collections.Generic;
using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// The base class for literal nodes.
	/// </summary>
	internal abstract class LiteralNodeBase<T> : NodeBase
	{
		/// <summary>
		/// The literal value.
		/// </summary>
		public T Value { get; set; }

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			return typeof (T);
		}

		public override bool IsConstant { get { return true; } }
		public override object ConstantValue { get { return Value; } }

		#region Equality members

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

		#endregion

		public override string ToString()
		{
			return string.Format("{0}({1})", typeof (T).Name, Value);
		}
	}
}
