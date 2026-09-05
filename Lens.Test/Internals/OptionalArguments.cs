using System.Globalization;
using System.Runtime.InteropServices;

namespace Lens.Test.Internals
{
    // Types for the calls that leave the trailing arguments out and let the callee's own
    // declaration say what is passed for them.

    /// <summary>
    /// An enum, so that a default recorded as the integer behind it is passed as the enum.
    /// </summary>
    public enum Level
    {
        Low = 1,
        High = 7
    }

    /// <summary>
    /// Methods whose trailing parameters declare defaults.
    /// </summary>
    public class Optionals
    {
        public static string Opt(int a, int b = 5, string c = "z")
        {
            return a + "/" + b + "/" + c;
        }

        /// <summary>
        /// Overloaded against the one below: a call that spells every argument must reach this one
        /// rather than the one that would have to fill a default in.
        /// </summary>
        public static string Pick(int a)
        {
            return "one:" + a;
        }

        public static string Pick(int a, int b = 5)
        {
            return "two:" + a + "/" + b;
        }

        /// <summary>
        /// Defaults of every kind a constant can be spelled with.
        /// </summary>
        public static string Kinds(int n = 0, byte b = 3, char c = 'x', Level level = Level.High, double d = 1.5, long l = 7, string s = null, bool flag = true)
        {
            return n + "/" + b + "/" + c + "/" + level + "/" + d.ToString(CultureInfo.InvariantCulture) + "/" + l + "/" + (s ?? "null") + "/" + flag;
        }

        /// <summary>
        /// An '[Optional]' parameter with no value of its own: nothing says what to pass for it, so
        /// the call that leaves it out must not resolve.
        /// </summary>
        public static string NoValue([Optional] int a)
        {
            return "no:" + a;
        }

        public static string Generic<T>(T value, string tag = "t")
        {
            return value + "/" + tag;
        }

        public string Instance(string a, bool flag = true)
        {
            return a + "/" + flag;
        }
    }

    /// <summary>
    /// A constructor whose trailing parameters declare defaults.
    /// </summary>
    public class OptionalCtor
    {
        public OptionalCtor(int a, string b = "def")
        {
            Value = a + "/" + b;
        }

        public readonly string Value;
    }

    /// <summary>
    /// An indexer whose trailing index parameter declares a default.
    /// </summary>
    public class OptionalIndexer
    {
        public string Stored = "-";

        public string this[int a, string b = "d"]
        {
            get { return a + "/" + b + "/" + Stored; }
            set { Stored = a + "/" + b + "/" + value; }
        }
    }

    /// <summary>
    /// An extension method whose trailing parameter declares a default.
    /// </summary>
    public static class OptionalExtensions
    {
        public static string Tagged(this string source, string tag = "!")
        {
            return source + tag;
        }
    }
}
