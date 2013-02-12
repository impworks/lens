using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Operators;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new object creation.
	/// </summary>
	public class NewObjectNode : InvocationNodeBase
	{
		public NewObjectNode(string type = null)
		{
			Type = type;
		}

		/// <summary>
		/// The type of the object to create.
		/// </summary>
		public TypeSignature Type { get; set; }

		protected override Type resolveExpressionType(Context ctx)
		{
			return ctx.ResolveType(Type.Signature);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Arguments;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			if(Arguments.Count == 0)
				Error("A parameterless constructor must be invoked by applying a () to it.");

			var isParameterless = Arguments.Count == 1 && Arguments[0].GetExpressionType(ctx) == typeof (Unit);

			var argTypes = isParameterless
				? System.Type.EmptyTypes
				: Arguments.Select(a => a.GetExpressionType(ctx)).ToArray();

			var ctor = ctx.ResolveConstructor(Type.Signature, argTypes);
			if(ctor == null)
				Error("Type {0} does not have a constructor that accepts {1} arguments of given types.", Type.Signature, argTypes.Length);

			if (!isParameterless)
			{
				var destTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
				for (var idx = 0; idx < Arguments.Count; idx++)
				{
					var castNode = new CastOperatorNode
					{
						Expression = Arguments[idx],
						Type = destTypes[idx]
					};

					castNode.Compile(ctx, true);
				}
			}

			gen.EmitCreateObject(ctor);
		}

		#region Equality members

		protected bool Equals(NewObjectNode other)
		{
			return base.Equals(other) && Equals(Type, other.Type);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NewObjectNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (Type != null ? Type.GetHashCode() : 0);
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("new({0}, args: {1})", Type, string.Join(";", Arguments));
		}
	}
}
