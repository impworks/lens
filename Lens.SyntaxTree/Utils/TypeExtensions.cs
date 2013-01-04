using System;

namespace Lens.SyntaxTree.Utils
{
	public static class TypeExtensions
	{
		/// <summary>
		/// Checks whether the type implements the interface
		/// </summary>
		/// <typeparam name="IType">Interface type.</typeparam>
		/// <param name="type">Type to check.</param>
		public static bool Implements<IType>(this Type type)
		{
			var info = typeof (IType);
			return info.IsInterface && info.IsAssignableFrom(type);
		}
	}
}
