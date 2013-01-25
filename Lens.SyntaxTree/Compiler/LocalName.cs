using System;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// A class representing info about a local variable.
	/// </summary>
	internal class LocalName
	{
		public LocalName(string name, Type type, int id, bool isConst = false)
		{
			Name = name;
			Type = type;
			Id = id;

			IsConstant = isConst;
		}

		/// <summary>
		/// Variable name.
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// Variable type.
		/// </summary>
		public readonly Type Type;

		/// <summary>
		/// Is the name a constant or a variable?
		/// </summary>
		public readonly bool IsConstant;

		/// <summary>
		/// The identifier of the variable.
		/// </summary>
		public readonly int Id;
	}
}
