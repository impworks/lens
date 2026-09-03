using System;

namespace Lens.Test.Internals
{
    /// <summary>
    /// A ref struct written by hand rather than borrowed from the BCL, so that the restrictions
    /// the CLI places on one can be checked on every framework the compiler is built for: Span
    /// does not exist on .NET Framework without an extra package, and the rules do not depend on
    /// which ref struct is being misused.
    /// </summary>
    public ref struct IntWindow
    {
        private readonly int[] _data;

        public IntWindow(int[] data)
        {
            _data = data;
        }

        public int Length => _data.Length;

        /// <summary>
        /// An indexer whose getter returns a managed pointer, and which has no setter at all: the
        /// element is written through the pointer the getter hands back.
        /// </summary>
        public ref int this[int index] => ref _data[index];

        /// <summary>
        /// A property whose getter returns a managed pointer.
        /// </summary>
        public ref int First => ref _data[0];

        public int Sum()
        {
            var result = 0;
            foreach (var curr in _data)
                result += curr;

            return result;
        }
    }

    /// <summary>
    /// The host surface the ref struct and by-ref return tests are written against.
    /// </summary>
    public static class RefStructHost
    {
        public static IntWindow Window(int[] data)
        {
            return new IntWindow(data);
        }

        public static int Total(IntWindow window)
        {
            return window.Sum();
        }

        /// <summary>
        /// A method returning 'ref int'.
        /// </summary>
        public static ref int FirstRef(int[] data)
        {
            return ref data[0];
        }

        /// <summary>
        /// A method returning 'ref readonly int'.
        /// </summary>
        public static ref readonly int FirstReadOnly(int[] data)
        {
            return ref data[0];
        }

        /// <summary>
        /// A method returning a managed pointer to a struct larger than any ldind opcode can load,
        /// so that reading through it needs ldobj.
        /// </summary>
        public static ref DateTime FirstDate(DateTime[] data)
        {
            return ref data[0];
        }

        /// <summary>
        /// A by-ref parameter, to check that a returned pointer can be handed straight on.
        /// </summary>
        public static void Increment(ref int value)
        {
            value++;
        }

#if NET_CORE
        /// <summary>
        /// The ref struct the whole exercise is about, which .NET Framework has no in-box copy of.
        /// </summary>
        public static Span<int> Span(int[] data)
        {
            return data.AsSpan();
        }

        public static ReadOnlySpan<char> Chars(string value)
        {
            return value.AsSpan();
        }

        public static int SpanTotal(Span<int> span)
        {
            var result = 0;
            foreach (var curr in span)
                result += curr;

            return result;
        }
#endif
    }
}
