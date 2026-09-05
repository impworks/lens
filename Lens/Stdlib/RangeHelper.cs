using System;
using System.Collections.Generic;

namespace Lens.Stdlib
{
    /// <summary>
    /// The runtime support for indexing and slicing by System.Index and System.Range.
    ///
    /// Not one signature here mentions either type, and deliberately so: the library is built for
    /// target frameworks that have never heard of them, while a script that writes '^1' or '1..3'
    /// runs wherever the types themselves exist. Everything the compiler cannot express as
    /// arithmetic - the bounds check, the counting, the way a list grows - lives here instead.
    /// </summary>
    public static class RangeHelper
    {
        #region Slicing

        /// <summary>
        /// Returns a copy of the given segment of an array.
        /// </summary>
        public static T[] GetArrayRange<T>(T[] source, int offset, int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            CheckSegment(source.Length, offset, count);

            var result = new T[count];
            Array.Copy(source, offset, result, 0, count);
            return result;
        }

        /// <summary>
        /// Returns a copy of the given segment of a list.
        /// </summary>
        public static List<T> GetListRange<T>(IList<T> source, int offset, int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            CheckSegment(source.Count, offset, count);

            var result = new List<T>(count);
            for (var idx = 0; idx < count; idx++)
                result.Add(source[offset + idx]);

            return result;
        }

        #endregion

        #region Replacing

        /// <summary>
        /// Overwrites a segment of an array with the given values.
        ///
        /// There must be exactly as many of them as the segment holds: an array is of the length it
        /// was created with, so there is nowhere for a longer sequence to go and nothing to put in
        /// the gap a shorter one would leave.
        /// </summary>
        public static void ReplaceArrayRange<T>(T[] target, int offset, int count, IEnumerable<T> values)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            CheckSegment(target.Length, offset, count);

            var items = AsList(values);
            if (items.Count != count)
            {
                throw new ArgumentException(
                    string.Format(
                        "Cannot replace {0} element(s) of an array with {1} value(s): an array cannot change its length!",
                        count,
                        items.Count
                    )
                );
            }

            for (var idx = 0; idx < count; idx++)
                target[offset + idx] = items[idx];
        }

        /// <summary>
        /// Replaces a segment of a list with the given values, however many there are: the ones
        /// that fit are overwritten in place, and the list then shrinks or grows by the difference.
        /// </summary>
        public static void ReplaceListRange<T>(IList<T> target, int offset, int count, IEnumerable<T> values)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            CheckSegment(target.Count, offset, count);

            var items = AsList(values);
            var shared = Math.Min(count, items.Count);

            for (var idx = 0; idx < shared; idx++)
                target[offset + idx] = items[idx];

            // the tail no value reached is dropped back to front, so that nothing after it is
            // shifted more than once
            for (var idx = count - 1; idx >= shared; idx--)
                target.RemoveAt(offset + idx);

            for (var idx = shared; idx < items.Count; idx++)
                target.Insert(offset + idx, items[idx]);
        }

        #endregion

        #region Iteration

        /// <summary>
        /// Returns the value of one of a range's bounds, which has to be counted from the start:
        /// nothing says how far from the end of what a loop over a range on its own would begin.
        /// </summary>
        public static int RequireStartBased(int value, bool fromEnd)
        {
            if (fromEnd)
                throw new InvalidOperationException("A range whose bounds are counted from the end cannot be iterated over!");

            return value;
        }

        /// <summary>
        /// Walks the integers between two bounds, exactly as a 'for' loop over the two of them
        /// does: the lower bound is visited, the upper one is not, and a range that runs backwards
        /// is walked backwards.
        /// </summary>
        public static IEnumerable<int> Enumerate(int start, int end)
        {
            var step = Math.Sign(end - start);
            for (var curr = start; curr != end; curr += step)
                yield return curr;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Reads the values to be stored, once: a sequence may well be one that can only be walked
        /// through the one time, and it is walked here before anything is overwritten.
        /// </summary>
        private static IList<T> AsList<T>(IEnumerable<T> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            return values as IList<T> ?? new List<T>(values);
        }

        /// <summary>
        /// Checks that the segment lies inside a sequence of the given length.
        /// </summary>
        private static void CheckSegment(int length, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    string.Format(
                        "The range [{0}..{1}) is outside the bounds of a sequence of {2} element(s)!",
                        offset,
                        offset + count,
                        length
                    )
                );
            }
        }

        #endregion
    }
}
