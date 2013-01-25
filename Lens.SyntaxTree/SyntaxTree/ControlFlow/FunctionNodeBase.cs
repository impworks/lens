using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// An anonymous function definition node.
	/// </summary>
	public abstract class FunctionNodeBase : NodeBase, IStartLocationTrackingEntity
	{
		protected FunctionNodeBase()
		{
			Arguments = new Dictionary<string, FunctionArgument>();
			Body = new CodeBlockNode();
		}

		/// <summary>
		/// Function arguments.
		/// </summary>
		public Dictionary<string, FunctionArgument> Arguments { get; set; }

		/// <summary>
		/// Function body.
		/// </summary>
		public CodeBlockNode Body { get; set; }

		/// <summary>
		/// The method builder for the current function.
		/// </summary>
		public MethodBuilder MethodBuilder { get; protected set; }

		public override LexemLocation EndLocation
		{
			get { return Body.EndLocation; }
			set { LocationSetError(); }
		}

		protected override Type resolveExpressionType(Context ctx)
		{
			return Body.GetExpressionType(ctx);
		}

		public abstract void PrepareSelf(Context ctx);

		#region Equality members

		protected bool Equals(FunctionNodeBase other)
		{
			return Arguments.SequenceEqual(other.Arguments) && Equals(Body, other.Body);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((FunctionNodeBase)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Arguments != null ? Arguments.GetHashCode() : 0) * 397) ^ (Body != null ? Body.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
