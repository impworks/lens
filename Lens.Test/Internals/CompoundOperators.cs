#if NET_CLASSIC
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// The compiler stamps this on every member whose declaration needs a feature a reader might
    /// not know, and .NET Framework has no such type in the box - so the classic leg brings its own
    /// rather than losing the operators the suite is here to check.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }

        public bool IsOptional { get; set; }
    }
}
#endif

namespace Lens.Test.Internals
{
    // Types whose own instance operators say what a compound assignment to them means, updating the
    // target in place instead of being read, combined and written back.

    /// <summary>
    /// A reference type with an in-place operator for every shorthand that has one.
    /// </summary>
    public class Bag
    {
        public int Total;

        public Bag()
        {
        }

        public Bag(int total)
        {
            Total = total;
        }

        public void operator +=(int x) => Total += x;
        public void operator -=(int x) => Total -= x;
        public void operator *=(int x) => Total *= x;
        public void operator /=(int x) => Total /= x;
        public void operator %=(int x) => Total %= x;
        public void operator <<=(int x) => Total <<= x;
        public void operator >>=(int x) => Total >>= x;
        public void operator ^=(int x) => Total ^= x;
        public void operator &=(int x) => Total &= x;
        public void operator |=(int x) => Total |= x;

        public override string ToString() => "Bag(" + Total + ")";

        /// <summary>
        /// Named like an operator but returning a value, so it is an ordinary method and the
        /// shorthand it looks like must still mean the read-modify-write.
        /// </summary>
        public int op_MultiplicationAssignment(string x) => Total;
    }

    /// <summary>
    /// A value type, whose operator has to reach the caller's own storage rather than a copy of it.
    /// </summary>
    public struct Tally
    {
        public int Value;

        public void operator +=(int x) => Value += x;

        public override string ToString() => "Tally(" + Value + ")";
    }

    /// <summary>
    /// A holder, so that the shorthand can name a member or an index rather than a local.
    /// </summary>
    public class Shelf
    {
        public Bag Slot = new Bag();
        public Tally Count;
        public Bag[] Row = {new Bag(), new Bag()};

        public static Bag Shared = new Bag();

        public Shelf Self() => this;
    }

    /// <summary>
    /// A classic static operator on a type that has no in-place one, so that the shorthand it is
    /// declared for still expands into a read, a combine and a write back.
    /// </summary>
    public class Accum
    {
        public int Total;

        public Accum(int total)
        {
            Total = total;
        }

        public static Accum operator +(Accum left, int right) => new Accum(left.Total + right);

        public override string ToString() => "Accum(" + Total + ")";
    }
}
