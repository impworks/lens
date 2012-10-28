using System.Collections.Generic;

namespace Lens.SyntaxTree.Utils
{
	public static class ListExtensions
	{
		/// <summary>
		/// Compare two sequences by values.
		/// </summary>
		public static bool DeepEquals<T>(this IEnumerable<T> list1, IEnumerable<T> list2)
		{
			if (list1 == null && list2 == null)
				return true;

			if (list1 == null || list2 == null)
				return false;

			var iter1 = list1.GetEnumerator();
			var iter2 = list2.GetEnumerator();
			while (true)
			{
				var ok1 = iter1.MoveNext();
				var ok2 = iter2.MoveNext();

				// lists are of different length: not equal
				if (ok1 ^ ok2)
					return false;

				// both lists ended: equal!
				if (!ok1)
					return true;

				var curr1 = iter1.Current;
				var curr2 = iter2.Current;
				if (!curr1.Equals(curr2))
					return false;

			}
		}

		/// <summary>
		/// Compare two dictionaries by their key value pairs.
		/// </summary>
		public static bool DeepEquals<T1, T2>(this Dictionary<T1, T2> dic1, Dictionary<T1, T2> dic2)
		{
			return dic1.Keys.DeepEquals(dic2.Keys) && dic1.Values.DeepEquals(dic2.Values);
		}
	}
}
