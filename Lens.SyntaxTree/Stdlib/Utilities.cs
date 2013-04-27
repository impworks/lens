using System;

namespace Lens.SyntaxTree.Stdlib
{
	public static class Utilities
	{
		#region Misc

		public static void FailWith(string msg)
		{
			throw new Exception(msg);
		}

		public static void TimesIndex(int t, Action<int> action)
		{
			for (var idx = 0; idx < t; idx++)
				action(idx);
		}

		public static void Times(int t, Action action)
		{
			for (var idx = 0; idx < t; idx++)
				action();
		}

		#endregion

		#region Formatting

		public static string Format1(string format, object obj1)
		{
			return string.Format(format, obj1);
		}

		public static string Format2(string format, object obj1, object obj2)
		{
			return string.Format(format, obj1, obj2);
		}

		public static string Format3(string format, object obj1, object obj2, object obj3)
		{
			return string.Format(format, obj1, obj2, obj3);
		}

		public static string Format4(string format, object obj1, object obj2, object obj3, object obj4)
		{
			return string.Format(format, obj1, obj2, obj3, obj4);
		}

		public static string Format5(string format, object obj1, object obj2, object obj3, object obj4, object obj5)
		{
			return string.Format(format, obj1, obj2, obj3, obj4, obj5);
		}

		public static string Format6(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6)
		{
			return string.Format(format, obj1, obj2, obj3, obj4, obj5, obj6);
		}

		public static string Format7(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7)
		{
			return string.Format(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7);
		}

		public static string Format8(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7, object obj8)
		{
			return string.Format(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8);
		}

		public static string Format9(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7, object obj8, object obj9)
		{
			return string.Format(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8, obj9);
		}

		public static string Format10(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7, object obj8, object obj9, object obj10)
		{
			return string.Format(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8, obj9, obj10);
		}

		#endregion
	}
}
