using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lens.SyntaxTree;

namespace Lens.Compiler
{
	/// <summary>
	/// A cache-friendly version of type signature.
	/// </summary>
	public class TypeSignature : LocationEntity, IStartLocationTrackingEntity, IEndLocationTrackingEntity
	{
		public TypeSignature(string name, params TypeSignature[] args)
			: this(name, false, args)
		{ }

		public TypeSignature(string name, bool isArray, params TypeSignature[] args)
		{
			Name = name;
			Arguments = args.Length > 0 ? args : null;
			IsArray = isArray;
			FullSignature = getSignature(name, args);
		}

		#region Fields

		public readonly string Name;
		public readonly TypeSignature[] Arguments;
		public bool IsArray;

		public readonly string FullSignature;

		#endregion

		#region Methods

		private string getSignature(string name, TypeSignature[] args)
		{
			if (args.Length == 0)
				return name;

			if (IsArray)
				return Arguments[0].FullSignature + "[]";

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
			return new TypeSignature(type);
		}

		#endregion

		#region Static constructors

		private static Dictionary<string, string> _Postfixes = new Dictionary<string, string>
		{
			{"?", "System.Nullable"},
			{"~", "System.Collections.IEnumerable"}
		};

		/// <summary>
		/// Parses the type signature.
		/// </summary>
		public static TypeSignature Parse(string signature)
		{
			if(signature.EndsWith("[]"))
				return new TypeSignature(null, true, Parse(signature.Substring(0, signature.Length - 2)));

			foreach(var postfix in _Postfixes)
				if(signature.EndsWith(postfix.Key))
					return new TypeSignature(postfix.Value, Parse(signature.Substring(0, signature.Length - postfix.Key.Length)));

			var open = signature.IndexOf('<');
			if(open == -1)
				return new TypeSignature(signature);

			var close = signature.LastIndexOf('>');
			var args = parseTypeArgs(signature.Substring(open + 1, close - open - 1)).ToArray();
			var typeName = signature.Substring(0, open) + '`' + args.Length;

			return new TypeSignature(typeName, args);
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
			return string.Equals(FullSignature, other.FullSignature);
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
