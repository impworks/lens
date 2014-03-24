using System;

namespace Lens.Utils
{
	internal class MethodLookupResult<T>
	{
		public readonly T Method;
		public readonly int Distance;
		public readonly Type[] ArgumentTypes;

		public MethodLookupResult(T method, int dist, Type[] args)
		{
			Method = method;
			Distance = dist;
			ArgumentTypes = args;
		}
	}
}
