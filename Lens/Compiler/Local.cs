using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Lens.Compiler
{
	/// <summary>
	/// A class representing info about a local variable.
	/// </summary>
	internal class Local
	{
		#region Constructors

		public Local(string name, Type type, bool isConst = false, bool isRefArg = false)
		{
			Name = name;
			Type = type;
			IsImmutable = isConst;
			IsRefArgument = isRefArg;
		}

		private Local(Local other, int dist = 0)
		{
			Name = other.Name;
			Type = other.Type;
			IsImmutable = other.IsImmutable;
			IsRefArgument = other.IsRefArgument;

			IsClosured = other.IsClosured;
			ClosureFieldName = other.ClosureFieldName;

			IsConstant = other.IsConstant;
			ConstantValue = other.ConstantValue;

			LocalBuilder = other.LocalBuilder;
			ArgumentId = other.ArgumentId;

			ClosureDistance = dist;
		}

		#endregion

		#region Fields

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
		public readonly bool IsImmutable;

		/// <summary>
		/// Does the variable represent a function argument that is passed by ref?
		/// </summary>
		public readonly bool IsRefArgument;

		/// <summary>
		/// The ID of the argument if this name represents one.
		/// </summary>
		public int? ArgumentId;

		/// <summary>
		/// Is the name referenced in nested scopes?
		/// </summary>
		public bool IsClosured;

		/// <summary>
		/// The distance between the current scope and the scope that owns this variable.
		/// </summary>
		public int? ClosureDistance;

		/// <summary>
		/// The name of the field in closured class.
		/// </summary>
		public string ClosureFieldName;

		/// <summary>
		/// The local builder identifier.
		/// </summary>
		public LocalBuilder LocalBuilder;

		/// <summary>
		/// Checks if the current local name represents a constant.
		/// Must also be immutable!
		/// </summary>
		public bool IsConstant;

		/// <summary>
		/// The compile-time constant value for current local name.
		/// </summary>
		public dynamic ConstantValue;

		#endregion

		#region Methods

		/// <summary>
		/// Create a copy of the name information and bind it to the distance.
		/// </summary>
		public Local GetClosuredCopy(int distance)
		{
			return new Local(this, distance);
		}

		#endregion

		#region Debug

		public override string ToString()
		{
			var entities = new List<string>();
			if(IsClosured) entities.Add("closured");
			if(IsRefArgument) entities.Add("ref");
			if(IsImmutable) entities.Add("immutable");
			if(IsConstant) entities.Add("const");
			if(ArgumentId != null) entities.Add(string.Format("arg({0})", ArgumentId));
			return string.Format("{0}:{1} ({2})", Name, Type.Name, string.Join(", ", entities));
		}

		#endregion
	}
}
