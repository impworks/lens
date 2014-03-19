using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A base node for all operators.
	/// </summary>
	internal abstract class OperatorNodeBase : NodeBase
	{
		/// <summary>
		/// A textual operator representation for error reporting.
		/// </summary>
		protected abstract string OperatorRepresentation { get; }

		/// <summary>
		/// The name of the method that C# compiler uses for method overloading.
		/// </summary>
		protected virtual string OverloadedMethodName { get { return null; } }

		/// <summary>
		/// The pointer to overloaded version of the operator.
		/// </summary>
		protected MethodWrapper m_OverloadedMethod;
	}
}
