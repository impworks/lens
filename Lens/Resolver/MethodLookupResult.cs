using System;

namespace Lens.Resolver
{
	/// <summary>
	/// The single result of a method lookup operation.
	/// </summary>
	internal class MethodLookupResult<T>
	{
		#region Constructor

		public MethodLookupResult(T method, int dist, Type[] args)
		{
			Method = method;
			Distance = dist;
			ArgumentTypes = args;
		}

		#endregion

		#region Fields

		/// <summary>
		/// Reference to method (or constructor).
		/// </summary>
		public readonly T Method;

		/// <summary>
		/// Calculated total distance of all arguments.
		/// </summary>
		public readonly int Distance;

		/// <summary>
		/// Inferred or evident argument types.
		/// </summary>
		public readonly Type[] ArgumentTypes;

		#endregion
	}
}
