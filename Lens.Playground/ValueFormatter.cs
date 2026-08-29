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
        /// The name of a value's type, for the line shown under the result.
        /// </summary>
        public static string TypeNameOf(object obj)
        {
            return obj?.GetType().ToString();
        }
    }
}
