using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// An anonymous function definition node.
	/// </summary>
	public class FunctionNode : NodeBase
	{
		#region Equality members

		protected bool Equals(FunctionNode other)
		{
			return Arguments.SequenceEqual(other.Arguments) && Equals(Body, other.Body);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((FunctionNode) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Arguments != null ? Arguments.GetHashCode() : 0)*397) ^ (Body != null ? Body.GetHashCode() : 0);
			}
		}

		#endregion

		/// <summary>
		/// Function arguments.
		/// </summary>
		public Dictionary<string, FunctionArgument> Arguments { get; set; }

		/// <summary>
		/// Function body.
		/// </summary>
		public CodeBlockNode Body { get; set; }

		public override Type GetExpressionType()
		{
			return Body.GetExpressionType();
		}

		public override void Compile()
		{
			registerArgVariables();

			throw new NotImplementedException();
		}

		#region Generic code for all functions, anonymous and named

		/// <summary>
		/// Declare variables for parameters.
		/// </summary>
		protected void registerArgVariables()
		{
			// foreach (var currArg in Arguments)
			//     Body.RegisterVariable(currArg.Value);
		}

		#endregion
	}
}
