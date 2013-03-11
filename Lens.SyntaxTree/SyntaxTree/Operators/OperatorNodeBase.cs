using System.Reflection;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A base node for all operators.
	/// </summary>
	public abstract class OperatorNodeBase : NodeBase
	{
		/// <summary>
		/// A textual operator representation for error reporting.
		/// </summary>
		public abstract string OperatorRepresentation { get; }

		/// <summary>
		/// The name of the method that C# compiler uses for method overloading.
		/// </summary>
		public virtual string OverloadedMethodName { get { return null; } }

		/// <summary>
		/// The pointer to overloaded version of the operator.
		/// </summary>
		protected MethodInfo m_OverloadedMethod;
	}
}
