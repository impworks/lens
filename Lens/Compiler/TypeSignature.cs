using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lens.SyntaxTree;

namespace Lens.Compiler
{
	/// <summary>
	/// A cache-friendly version of type signature.
	/// </summary>
	internal class TypeSignature : LocationEntity
	{
		private TypeSignature(string name, string postfix, params TypeSignature[] args)
		{
			Name = name;
			Arguments = args.Length > 0 ? args : null;
			Postfix = postfix;
			FullSignature = getSignature(name, postfix, args);
		}

		#region Fields

		public readonly string Name;
		public readonly TypeSignature[] Arguments;
		public readonly string Postfix;

		public readonly string FullSignature;

		#endregion

		#region Methods

		private static string getSignature(string name, string postfix, TypeSignature[] args)
		{
			if (args.Length == 0)
				return name;

			// postfix case: name is null, and the single element of `args` contains all the signature tree
			if (!string.IsNullOrEmpty(postfix))
				return args[0].FullSignature + postfix;

			var sb = new StringBuilder(name);
			sb.Append("<");

			var idx = 0;
			foreach (var curr in args)
			{
				if (idx > 0)
					sb.Append(", ");

				sb.Append(curr.FullSignature);

				idx++;
			}

			sb.Append(">");

			return sb.ToString();
		}

		/// <summary>
		/// Initializes a type signature with it's string representation.
		/// </summary>
		public static implicit operator TypeSignature(string type)
		{
			return type == null ? null : Parse(type);
		}

		public override string ToString()
		{
			return FullSignature;
		}

		#endregion

		#region Static constructors

		private static List<string> _Postfixes = new List<string> {"[]", "?", "~"};

		/// <summary>
		/// Creates a new TypeSignature in the form "T&lt;...&gt;"
		/// </summary>
		public static TypeSignature FromName(string name, params TypeSignature[] args)
		{
			return new TypeSignature(name, null, args);
		}

		/// <summary>
		/// Creates a new TypeSignature in the form "T?"
		/// </summary>
		public static TypeSignature FromPostfix(string postfix, params TypeSignature[] args)
		{
			return new TypeSignature(null, postfix, args);
		}

		/// <summary>
		/// Parses the type signature.
		/// </summary>
		public static TypeSignature Parse(string signature)
		{
			if(signature[0] == ' ' || signature[signature.Length - 1] == ' ')
				signature = signature.Trim();

			foreach (var postfix in _Postfixes)
				if (signature.EndsWith(postfix))
					return new TypeSignature(null, postfix, Parse(signature.Substring(0, signature.Length - postfix.Length)));

			var open = signature.IndexOf('<');
			if(open == -1)
				return FromName(signature);

			var close = signature.LastIndexOf('>');
			var args = parseTypeArgs(signature.Substring(open + 1, close - open - 1)).ToArray();
			var typeName = signature.Substring(0, open);

			return FromName(typeName, args);
		}

		/// <summary>
		/// Parses out the list of generic type arguments delimited by commas.
		/// </summary>
		private static IEnumerable<TypeSignature> parseTypeArgs(string args)
		{
			var depth = 0;
			var start = 0;
			var len = args.Length;
			for (var idx = 0; idx < len; idx++)
			{
				if (args[idx] == '<') depth++;
				if (args[idx] == '>') depth--;
				if (depth == 0 && args[idx] == ',')
				{
					yield return Parse(args.Substring(start, idx - start));
					start = idx + 1;
				}
			}

			yield return Parse(args.Substring(start, args.Length - start));
		}

		#endregion

		#region Equality members

		protected bool Equals(TypeSignature other)
		{
			var basic = string.Equals(Name, other.Name)
			            && string.Equals(Postfix, other.Postfix);

			if (!basic || (Arguments == null ^ other.Arguments == null))
				return false;

			return Arguments == null || Arguments.SequenceEqual(other.Arguments);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeSignature)obj);
		}

		public override int GetHashCode()
		{
			return FullSignature.GetHashCode();
		}

		#endregion
	}
}
