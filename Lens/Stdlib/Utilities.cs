using System;
using System.Collections.Generic;

namespace Lens.Stdlib
{
    /// <summary>
    /// Miscelaneous handy functions.
    /// </summary>
    public static class Utilities
    {
        #region Misc

        /// <summary>
        /// Throws an error from a string.
        /// </summary>
        /// <param name="msg"></param>
        public static void FailWith(string msg)
        {
            throw new Exception(msg);
        }

        /// <summary>
        /// Calls an action N times.
        /// </summary>
        public static void Times(int t, Action action)
        {
            for (var idx = 0; idx < t; idx++)
                action();
        }

        /// <summary>
        /// Calls an action N times and passes an argument.
        /// </summary>
        public static void Times(int t, Action<int> action)
        {
            for (var idx = 0; idx < t; idx++)
                action(idx);
        }

        /// <summary>
        /// Calls an action for a 2D loop with indices.
        /// </summary>
        public static void Times(Tuple<int, int> ts, Action<int, int> action)
        {
            for (var idx = 0; idx < ts.Item1; idx++)
            for (var idx2 = 0; idx2 < ts.Item2; idx2++)
                action(idx, idx2);
        }

        /// <summary>
        /// Calls an action for a 3D loop with indices.
        /// </summary>
        public static void Times(Tuple<int, int, int> ts, Action<int, int, int> action)
        {
            for (var idx = 0; idx < ts.Item1; idx++)
            for (var idx2 = 0; idx2 < ts.Item2; idx2++)
            for (var idx3 = 0; idx3 < ts.Item3; idx3++)
                action(idx, idx2, idx3);
        }

        /// <summary>
        /// Calls an action for a 4D loop with indices.
        /// </summary>
        public static void Times(Tuple<int, int, int, int> ts, Action<int, int, int, int> action)
        {
            for (var idx = 0; idx < ts.Item1; idx++)
            for (var idx2 = 0; idx2 < ts.Item2; idx2++)
            for (var idx3 = 0; idx3 < ts.Item3; idx3++)
            for (var idx4 = 0; idx4 < ts.Item4; idx4++)
                action(idx, idx2, idx3, idx4);
        }

        /// <summary>
        /// Limits the value between two points.
        /// </summary>
        public static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        /// <summary>
        /// Limits the value between two points.
        /// </summary>
        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        /// <summary>
        /// Limits the value between two points.
        /// </summary>
        public static double Clamp(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        /// <summary>
        /// Limits the value between two points.
        /// </summary>
        public static long Clamp(long value, long min, long max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        /// <summary>
        /// Checks if the value is odd.
        /// </summary>
        public static bool Odd(int value)
        {
            return value % 2 != 0;
        }

        /// <summary>
        /// Checks if the value is even.
        /// </summary>
        public static bool Even(int value)
        {
            return value % 2 == 0;
        }

        /// <summary>
        /// Checks if the value is odd.
        /// </summary>
        public static bool Odd(long value)
        {
            return value % 2 != 0;
        }

        /// <summary>
        /// Checks if the value is even.
        /// </summary>
        public static bool Even(long value)
        {
            return value % 2 == 0;
        }

        #endregion

        #region Range

        /// <summary>
        /// Creates a range from X to Y (inclusive) with step 1.
        /// </summary>
        public static IEnumerable<int> Range(int from, int to)
        {
            return Range(from, to, 1);
        }

        /// <summary>
        /// Creates a range from X to Y (inclusive) with given step.
        /// </summary>
        public static IEnumerable<int> Range(int from, int to, int step)
        {
            if (step <= 0)
                throw new ArgumentException("step");

            if (from < to)
                for (var i = from; i <= to; i += step)
                    yield return i;

            else if (from > to)
                for (var i = from; i >= to; i -= step)
                    yield return i;
        }

        /// <summary>
        /// Creates a range of characters with step 1.
        /// </summary>
        public static IEnumerable<char> Range(char from, char to)
        {
            return Range(from, to, 1);
        }

        /// <summary>
        /// Creates a range of characters with given step.
        /// </summary>
        public static IEnumerable<char> Range(char from, char to, int step)
        {
            if (step <= 0) throw new ArgumentException("step");

            if (from < to)
                for (var i = from; i <= to; i = (char) (i + step))
                    yield return i;

            else if (from > to)
                for (var i = from; i >= to; i = (char) (i - step))
                    yield return i;
        }

        #endregion
    }
}