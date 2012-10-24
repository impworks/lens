using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new tuple declaration.
	/// </summary>
	public class NewTupleNode : NodeBase
	{
		public NewTupleNode()
		{
			Expressions = new List<NodeBase>();
		}

		private Type m_TupleType;

		/// <summary>
		/// The list of items in the array.
		/// </summary>
		public List<NodeBase> Expressions { get; set; }

		public override Type GetExpressionType()
		{
			if (m_TupleType == null)
			{
				if (Expressions.Count == 0)
					Error("Tuple must contain at least one object!");

				if (Expressions.Count > 8)
					Error("Tuples cannot contain more than 8 objects. Use a structure or a nested tuple instead!");

				var tupleType = getTupleType();
				m_TupleType = tupleType.MakeGenericType(Expressions.Select(x => x.GetExpressionType()).ToArray());
			}

			return m_TupleType;
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}

		#region Helpers

		/// <summary>
		/// Detecting tuple type by the number of arguments.
		/// </summary>
		/// <returns></returns>
		private Type getTupleType()
		{
			switch(Expressions.Count)
			{
				case 1: return typeof (Tuple<>);
				case 2: return typeof (Tuple<,>);
				case 3: return typeof (Tuple<,,>);
				case 4: return typeof (Tuple<,,,>);
				case 5: return typeof (Tuple<,,,,>);
				case 6: return typeof (Tuple<,,,,,>);
				case 7: return typeof (Tuple<,,,,,,>);
				case 8: return typeof (Tuple<,,,,,,,>);
			}

			throw new InvalidOperationException("Tuples cannot have more than 8 objects!");
		}

		#endregion
	}
}
