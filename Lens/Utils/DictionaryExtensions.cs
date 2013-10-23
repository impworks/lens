using System.Collections.Generic;

namespace Lens.Utils
{
	internal static class DictionaryExtensions
	{
		/// <summary>
		/// Compare two dictionaries by their key value pairs.
		/// </summary>
		public static bool DictionaryEquals<T1, T2>(this IDictionary<T1, T2> dic1, IDictionary<T1, T2> dic2)
		{
			return dic1.Count == dic2.Count && dic1.ContainsAllItemsFrom(dic2) && dic2.ContainsAllItemsFrom(dic1);
		}

		/// <summary>
		/// Check if a dictionary contains all the items from another dictionary.
		/// </summary>
		public static bool ContainsAllItemsFrom<T1, T2>(this IDictionary<T1, T2> dic1, IDictionary<T1, T2> dic2)
		{
			foreach (var curr1 in dic1)
			{
				if (!dic2.ContainsKey(curr1.Key))
					return false;

				var curr2 = dic2[curr1.Key];
				if (!curr1.Equals(curr2))
					return false;
			}

			return true;
		}
	}
}
