using Lens.SyntaxTree;

namespace Lens.Lexer
{
	/// <summary>
	/// A single lexem parsed from the source by lexer.
	/// </summary>
	internal class Lexem : LocationEntity
	{
		#region Constructor

		public Lexem(LexemType type, LexemLocation start, LexemLocation end, string value = null)
		{
			Type = type;
			Value = value;
			StartLocation = start;
			EndLocation = end;
		}

		#endregion

		#region Fields

		public readonly LexemType Type;
		public readonly string Value;

		#endregion

		#region Equality members

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((Lexem)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (Type.GetHashCode() * 397) ^ (Value != null ? Value.GetHashCode() : 0);
			}
		}

		private bool Equals(Lexem other)
		{
			return Type.Equals(other.Type) && string.Equals(Value, other.Value);
		}

		#endregion

		#region Debug

		public override string ToString()
		{
			var format = string.IsNullOrEmpty(Value) ? "{0}" : "{0}({1})";
			return string.Format(format, Type, Value);
		}

		#endregion
	}
}
