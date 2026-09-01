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

    /// <summary>
    /// Reports what a lambda literal was actually compiled into, for the tests that care about the
    /// shape of the delegate rather than about the value it produces.
    /// </summary>
    public static class DelegateShape
    {
        /// <summary>
        /// The name of the delegate type a lambda arrived as.
        /// </summary>
        public static string NameOf(Converter<int, int> converter)
        {
            return converter.GetType().Name;
        }

        /// <summary>
        /// Whether the delegate calls the lambda's body directly, rather than calling another
        /// delegate that calls it. A lambda converted from a Func has that Func as its target.
        /// </summary>
        public static bool IsDirectConverter(Converter<int, int> converter)
        {
            return !(converter.Target is Delegate);
        }

        public static bool IsDirectPredicate(Predicate<int> predicate)
        {
            return !(predicate.Target is Delegate);
        }
    }

    /// <summary>
    /// A location of a delegate type that is neither a Func nor an Action, for the tests about what
    /// a lambda literal becomes when it is assigned to one.
    /// </summary>
    public class ConverterHolder
    {
        public Converter<int, int> Convert;
    }

    public interface IRestriction<T> { }

    public class RestrictionTest: IRestriction<int>, IRestriction<KeyValuePair<int, int>> { }

    public class RestrictionAcceptor
    {
        public int AcceptInstance<T>(IRestriction<KeyValuePair<int, T>> rst)
        {
            return 1;
        }

        public static int AcceptStatic<T>(IRestriction<KeyValuePair<int, T>> rst)
        {
            return 2;
        }
    }
}