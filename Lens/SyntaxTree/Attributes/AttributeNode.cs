using Lens.Compiler;
using Lens.SyntaxTree.Expressions;
using System;
using System.Collections.Generic;
using Lens.Resolver;

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

		public override bool Equals(object obj)
		{
			return base.Equals(obj) && Name == ((AttributeNode)obj).Name;
		}

		protected override CallableWrapperBase _Wrapper
		{
			get { throw new NotImplementedException(); }
		}

		protected override InvocationNodeBase recreateSelfWithArgs(IEnumerable<NodeBase> newArgs)
		{
			throw new NotImplementedException();
		}
	}
}
