using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.Test.Internals
{
    /// <summary>
    /// Helper class to test importing capabilities in LENS.
    /// </summary>
    public class ImportableStaticMethods
    {
        public static void DoNothing()
        {
            // nothing here
        }

        public static int AddNumbers(int a, int b)
        {
            return a + b;
        }

        public static int Sum(params int[] numbers)
        {
            return numbers.Sum();
        }

        public static List<T2> Project<T, T2>(IEnumerable<T> source, Func<T, T2> projector)
        {
            return source.Select(projector).ToList();
        }

        public static double OverloadedAdd(double a, double b)
        {
            return a + b;
        }

        public static double OverloadedAdd(double a, double b, double c)
        {
            return a + b + c;
        }

        public static string OverloadedAdd(string a, string b)
        {
            return a + b;
        }

        public int UnimportableMethod()
        {
            // cannot import: method is not static
            return 0;
        }

        private static int UnimportableMethod2()
        {
            // cannot import: method is not public
            return 0;
        }
    }

    public class ImportableClass
    {
        public ImportableClass(string value)
        {
            Value = value;
        }

        public string Value { get; private set; }

        public virtual string VirtualValue { get; set; }
    }
}
