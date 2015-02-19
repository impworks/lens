using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.Utils
{
	/// <summary>
	/// Extension methods for value sequences.
	/// </summary>
	internal static class EnumerableExtensions
	{
		/// <summary>
		/// Checks if the value is contained in the list.
		/// </summary>
		public static bool IsAnyOf<T>(this T obj, params T[] list)
		{
			for (var idx = list.Length - 1; idx >= 0; idx--)
				if (list[idx].Equals(obj))
					return true;

			return false;
		}

		/// <summary>
		/// Orders the list by a boolean flag: items with 'True' value go first.
		/// </summary>
		public static IEnumerable<T> Prefer<T>(this IEnumerable<T> seq, Func<T, bool> flag)
		{
			return seq.OrderByDescending(x => flag(x) ? 1 : 0);
		}

		/// <summary>
		/// Checks if the sequence is null or empty.
		/// </summary>
		public static bool IsNullOrEmpty<T>(this IEnumerable<T> seq)
		{
			return seq == null || !seq.Any();
		}
	}
}
