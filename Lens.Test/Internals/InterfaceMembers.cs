using System;

namespace Lens.Test.Internals
{
    // Types for the member lookups that go through an interface rather than through the type a
    // script names: default interface implementations, and the static abstract members that make
    // generic math work. Neither exists on .NET Framework - static abstract interface members need
    // a runtime that can dispatch them - so the whole file is compiled out of the net47 leg.
#if !NET_CLASSIC

    /// <summary>
    /// An interface with one abstract member and one default implementation on top of it.
    /// </summary>
    public interface IGreeter
    {
        string Name();

        string Greet() => "hello, " + Name();
    }

    /// <summary>
    /// Implements IGreeter without overriding Greet, so Greet is only reachable through the
    /// interface.
    /// </summary>
    public class Bob : IGreeter
    {
        public string Name() => "bob";
    }

    /// <summary>
    /// Overrides the default implementation: the class member must win over the interface's.
    /// </summary>
    public class Rob : IGreeter
    {
        public string Name() => "rob";

        public string Greet() => "hi, " + Name();
    }

    /// <summary>
    /// Inherits the default implementation through a base class rather than directly.
    /// </summary>
    public class Bobby : Bob
    {
    }

    /// <summary>
    /// A second interface offering a default Greet of its own, unrelated to IGreeter's.
    /// </summary>
    public interface IShouter
    {
        string Greet() => "HELLO";
    }

    /// <summary>
    /// Implements both, overriding neither: the call is ambiguous and must be reported as such.
    /// </summary>
    public class Loud : IGreeter, IShouter
    {
        public string Name() => "loud";
    }

    /// <summary>
    /// A static abstract factory, the simplest shape that needs a constrained. prefix.
    /// </summary>
    public interface IZeroed<T> where T : IZeroed<T>
    {
        static abstract T Make(int value);
    }

    public class Num : IZeroed<Num>
    {
        public int Value;

        public static Num Make(int value) => new Num {Value = value};

        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// A read-only instance property, the property counterpart of IGreeter: it is only reachable
    /// through the interface when the receiver is a constrained type parameter.
    /// </summary>
    public interface INamed
    {
        string Title { get; }
    }

    public class Knight : INamed
    {
        public string Title => "sir";
    }

    /// <summary>
    /// A settable instance property, so that writing through a constraint is covered as well as
    /// reading.
    /// </summary>
    public interface ICounted
    {
        int Count { get; set; }
    }

    public class Counter : ICounted
    {
        public int Count { get; set; }
    }

    /// <summary>
    /// A static abstract property - the T::Zero half of generic math, as opposed to the T::Make
    /// method IZeroed declares.
    /// </summary>
    public interface IHasZero<T> where T : IHasZero<T>
    {
        static abstract T Zero { get; }
    }

    public class Zed : IHasZero<Zed>
    {
        public static Zed Zero => new Zed();

        public override string ToString() => "zed";
    }

    /// <summary>
    /// Declares the operator itself, as opposed to inheriting it the way INumber does.
    /// </summary>
    public interface IAddable<T> where T : IAddable<T>
    {
        static abstract T operator +(T left, T right);
    }

    public class Money : IAddable<Money>
    {
        public int Amount;

        public static Money operator +(Money left, Money right) => new Money {Amount = left.Amount + right.Amount};

        public override string ToString() => Amount.ToString();
    }

#endif
}
