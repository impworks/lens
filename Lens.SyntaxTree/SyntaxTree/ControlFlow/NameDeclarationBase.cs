using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A base class for variable and constant declarations.
	/// </summary>
	public class NameDeclarationBase : NodeBase
	{
		/// <summary>
		/// The name of the variable.
		/// </summary>
		public string Name
		{
			get { return VariableInfo.Name; }
			set { VariableInfo.Name = value; }
		}

		/// <summary>
		/// The value to assign to the variable.
		/// </summary>
		public NodeBase Value { get; set; }

		/// <summary>
		/// Variable information.
		/// </summary>
		public VariableInfo VariableInfo { get; protected set; }

		public override LexemLocation EndLocation
		{
			get { return Value.EndLocation; }
			set { LocationSetError(); }
		}

		public override Type GetExpressionType()
		{
			return typeof (void);
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
