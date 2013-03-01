using System;
using System.Linq;
using System.Reflection;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// The base node for accessing array-like structures by index.
	/// </summary>
	abstract public class IndexNodeBase : AccessorNodeBase
	{
		/// <summary>
		/// The index value.
		/// </summary>
		public NodeBase Index { get; set; }

		protected PropertyInfo findIndexer(Type exprType, Type idxType, bool setter)
		{
			Func<PropertyInfo, bool> check;
			if(setter)
				check = p => p.GetSetMethod() != null;
			else
				check = p => p.GetGetMethod() != null;

			var ptys = exprType.GetProperties()
							   .Select(p => new { Property = p, Args = p.GetIndexParameters() })
							   .Where(pi => pi.Args.Length == 1 && check(pi.Property))
							   .Select(p => new { p.Property, ArgType = p.Args[0].ParameterType, Distance = p.Args[0].ParameterType.DistanceFrom(idxType)})
							   .OrderBy(pt => pt.Distance)
							   .ToArray();

			if (ptys.Length == 0 || ptys[0].Distance == int.MaxValue)
				Error(
					"Type '{0}' has no {1} that accepts an index of type '{1}'!",
					exprType,
					setter ? "setter" : "getter",
					idxType
				);

			if (ptys.Length > 1 && ptys[0].Distance == ptys[1].Distance)
				Error(
					"Indexer is ambigious, at least two cases apply:" + Environment.NewLine + "{0}[{1}]" + Environment.NewLine + "{0}[{2}]", 
					exprType,
					ptys[0].ArgType,
					ptys[1].ArgType
				);

			return ptys[0].Property;
		}

		#region Equality members

		protected bool Equals(IndexNodeBase other)
		{
			return Equals(Expression, other.Expression) && Equals(Index, other.Index);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((IndexNodeBase)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Expression != null ? Expression.GetHashCode() : 0) * 397) ^ (Index != null ? Index.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
