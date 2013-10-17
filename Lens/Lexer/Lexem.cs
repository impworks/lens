using Lens.SyntaxTree;

namespace Lens.Lexer
{
	public class Lexem : LocationEntity
	{
		public readonly LexemType Type;

		public readonly string Value;

		public Lexem(LexemType type, LexemLocation start, LexemLocation end, string value = null)
		{
			Type = type;
			Value = value;
			StartLocation = start;
			EndLocation = end;
		}

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

		public override string ToString()
		{
			var format = string.IsNullOrEmpty(Value) ? "{0}" : "{0}({1})";
			return string.Format(format, Type, Value);
		}

		private bool Equals(Lexem other)
		{
			return Type.Equals(other.Type) && string.Equals(Value, other.Value);
		}
	}
}
