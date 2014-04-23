using System;
using System.Collections.Generic;

namespace Lens.Stdlib
{
	public static class Utilities
	{
		#region Misc

		public static void FailWith(string msg)
		{
			throw new Exception(msg);
		}

        public static void Times(int t, Action action)
        {
            for (var idx = 0; idx < t; idx++)
                action();
        }

		public static void Times(int t, Action<int> action)
		{
			for (var idx = 0; idx < t; idx++)
				action(idx);
		}

        public static void Times(Tuple<int,int> ts, Action<int,int> action)
        {
            for (var idx = 0; idx < ts.Item1; idx++)
                for (var idx2 = 0; idx2 < ts.Item2; idx2++)
                    action(idx, idx2);
        }

        public static void Times(Tuple<int, int, int> ts, Action<int, int, int> action)
        {
            for (var idx = 0; idx < ts.Item1; idx++)
                for (var idx2 = 0; idx2 < ts.Item2; idx2++)
                    for (var idx3 = 0; idx3 < ts.Item3; idx3++)
                        action(idx, idx2, idx3);
        }

        public static void Times(Tuple<int, int, int, int> ts, Action<int, int, int, int> action)
        {
            for (var idx = 0; idx < ts.Item1; idx++)
                for (var idx2 = 0; idx2 < ts.Item2; idx2++)
                    for (var idx3 = 0; idx3 < ts.Item3; idx3++)
                        for (var idx4 = 0; idx4 < ts.Item4; idx4++)
                            action(idx, idx2, idx3, idx4);
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

		public static bool OddInt(int value)
		{
			return value%2 != 0;
		}

		public static bool EvenInt(int value)
		{
			return value % 2 == 0;
		}

		public static bool OddLong(long value)
		{
			return value % 2 != 0;
		}

		public static bool EvenLong(long value)
		{
			return value % 2 == 0;
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
	}
}
