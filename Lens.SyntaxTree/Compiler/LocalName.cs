using System;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// A class representing info about a local variable.
	/// </summary>
	internal class LocalName
	{
		public LocalName(string name, Type type, bool isConst = false)
		{
			Name = name;
			Type = type;
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
		/// The ID of the variable if it is local.
		/// </summary>
		public int? LocalId;

		/// <summary>
		/// Is the name referenced in nested scopes?
		/// </summary>
		public bool IsClosured;

		/// <summary>
		/// The distance between the current scope and the scope that owns this variable.
		/// </summary>
		public int? ClosureDistance;

		/// <summary>
		/// Create a copy of the name information and bind it to the distance.
		/// </summary>
		/// <param name="distance"></param>
		/// <returns></returns>
		public LocalName GetClosuredCopy(int distance)
		{
			return new LocalName(Name, Type, IsConstant)
			{
				ClosureDistance = distance,
				IsClosured = IsClosured
			};
		}
	}
}
