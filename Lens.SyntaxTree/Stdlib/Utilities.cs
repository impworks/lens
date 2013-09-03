using System;
using System.Collections.Generic;
using System.Linq;

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

		public static int ClampInt(int value, int min, int max)
		{
			return value < min ? min : (value > max ? max : value);
		}

		public static float ClampFloat(float value, float min, float max)
		{
			return value < min ? min : (value > max ? max : value);
		}

		public static double ClampDouble(double value, double min, double max)
		{
			return value < min ? min : (value > max ? max : value);
		}

		public static long ClampLong(long value, long min, long max)
		{
			return value < min ? min : (value > max ? max : value);
		}

		#endregion

		#region Range

		public static IEnumerable<int> RangeInt(int from, int to)
		{
			return RangeIntStep(from, to, 1);
		}

		public static IEnumerable<int> RangeIntStep(int from, int to, int step)
		{
			if(step <= 0)
				throw new ArgumentException("step");

			if (from < to)
				for (var i = from; i <= to; i += step)
					yield return i;

			else if (from > to)
				for (var i = from; i >= to; i -= step)
					yield return i;
		}

		public static IEnumerable<string> RangeString(string from, string to)
		{
			return RangeStringStep(from, to, 1);
		}

		public static IEnumerable<string> RangeStringStep(string from, string to, int step)
		{
			if (from.Length != 1) throw new ArgumentException("from");
			if (to.Length != 1) throw new ArgumentException("to");
			if (step <= 0) throw new ArgumentException("step");

			var fromChar = from[0];
			var toChar = to[0];

			if (fromChar < toChar)
				for (var i = fromChar; i <= toChar; i = (char)(i + step))
					yield return i.ToString();

			else if (fromChar > toChar)
				for (var i = fromChar; i >= toChar; i = (char)(i - step))
					yield return i.ToString();
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
