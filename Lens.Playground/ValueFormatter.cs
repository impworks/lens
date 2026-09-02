using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Lens.Playground
{
    /// <summary>
    /// Renders the value a script returned the way the console host renders it.
    ///
    /// The two hosts show the same scripts to the same people, so a list has to come out as
    /// [ 1; 2; 3 ] in both. This is the console host's own rules, kept in step with it: strings
    /// quoted, booleans lowercase, floats invariant, and long sequences cut off rather than
    /// filling the pane.
    /// </summary>
    internal static class ValueFormatter
    {
        /// <summary>
        /// How many entries of a sequence or dictionary are shown before it is elided.
        /// </summary>
        private const int MaxItems = 50;

        /// <summary>
        /// The text for a value, or "(null)" when there is none.
        /// </summary>
        public static string Format(object obj)
        {
            if (obj == null)
                return "(null)";

            if (obj is bool flag)
                return flag ? "true" : "false";

            if (obj is string str)
                return "\"" + str + "\"";

            if (obj is IDictionary dict)
            {
                var entries = new List<string>();
                var count = 0;

                foreach (DictionaryEntry entry in dict)
                {
                    if (count >= MaxItems)
                    {
                        entries.Add("...");
                        break;
                    }

                    entries.Add(Format(entry.Key) + " => " + Format(entry.Value));
                    count++;
                }

                return "{ " + string.Join("; ", entries) + " }";
            }

            // before the sequence case: walking a rank > 1 array as a sequence hands back its
            // cells in one flat run, which says nothing about the shape they are actually in
            if (obj is Array array && array.Rank > 1)
                return FormatArray(array, new int[array.Rank], 0);

            if (obj is IEnumerable seq)
            {
                var entries = new List<string>();
                var count = 0;

                foreach (var item in seq)
                {
                    if (count >= MaxItems)
                    {
                        entries.Add("...");
                        break;
                    }

                    entries.Add(Format(item));
                    count++;
                }

                return "[ " + string.Join("; ", entries) + " ]";
            }

            if (obj is double dbl)
                return dbl.ToString(CultureInfo.InvariantCulture);

            if (obj is float flt)
                return flt.ToString(CultureInfo.InvariantCulture);

            return obj.ToString();
        }

        /// <summary>
        /// Renders a multidimensional array one dimension at a time, so that its shape shows: a
        /// 2x2 comes out as [ [ 1; 2 ]; [ 3; 4 ] ], the way its literal is written.
        /// </summary>
        private static string FormatArray(Array array, int[] indexes, int dimension)
        {
            var entries = new List<string>();
            var isLast = dimension == array.Rank - 1;
            var upper = array.GetUpperBound(dimension);

            for (var idx = array.GetLowerBound(dimension); idx <= upper; idx++)
            {
                if (entries.Count >= MaxItems)
                {
                    entries.Add("...");
                    break;
                }

                indexes[dimension] = idx;

                entries.Add(
                    isLast
                        ? Format(array.GetValue(indexes))
                        : FormatArray(array, indexes, dimension + 1)
                );
            }

            return "[ " + string.Join("; ", entries) + " ]";
        }

        /// <summary>
        /// The name of a value's type, for the line shown under the result.
        /// </summary>
        public static string TypeNameOf(object obj)
        {
            return obj?.GetType().ToString();
        }
    }
}
