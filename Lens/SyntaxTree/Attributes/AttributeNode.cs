using Lens.Compiler;
using Lens.SyntaxTree.Expressions;
using System;

namespace Lens.SyntaxTree.Attributes
{
	/// <summary>
	/// Attribute node.
	/// </summary>
	class AttributeNode : InvocationNodeBase
	{
		/// <summary>
		/// Attribute name.
		/// </summary>
		public string Name { get; set; }
		
		protected override void compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}
}
