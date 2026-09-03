using System;

namespace Lens.Test.Internals
{
    // Types for the instance members a script reaches on a constrained type parameter. What they
    // are built to observe is that the receiver is the parameter itself and not a boxed copy of
    // it: every one of them mutates, so a call that went to a box leaves the original untouched
    // and the script sees the initial value.

    /// <summary>
    /// A mutating method and a way to read the result of it - the simplest shape that tells a call
    /// on the receiver apart from a call on a copy.
    /// </summary>
    public interface IBumpable
    {
        void Bump();

        int Value { get; }
    }

    /// <summary>
    /// A mutable struct, so that a call on a copy of it is observable.
    /// </summary>
    public struct Bumper : IBumpable
    {
        private int _value;

        public void Bump()
        {
            _value++;
        }

        public int Value => _value;
    }

    /// <summary>
    /// The same interface on a reference type: the substitution the 'constrained.' prefix has to
    /// dereference rather than call directly.
    /// </summary>
    public class BumpBox : IBumpable
    {
        private int _value;

        public void Bump()
        {
            _value++;
        }

        public int Value => _value;
    }

    /// <summary>
    /// A settable property behind an interface, so that a write through a constraint is covered as
    /// well as a call.
    /// </summary>
    public interface ITotalled
    {
        int Total { get; set; }
    }

    public struct Totals : ITotalled
    {
        public int Total { get; set; }
    }

    /// <summary>
    /// An indexer behind an interface: an accessor like any other, reached by a different node.
    /// </summary>
    public interface IIndexed
    {
        int this[int index] { get; set; }
    }

    public struct Slots : IIndexed
    {
        private int _first;
        private int _second;

        public int this[int index]
        {
            get => index == 0 ? _first : _second;
            set
            {
                if (index == 0)
                    _first = value;
                else
                    _second = value;
            }
        }
    }

    /// <summary>
    /// An event behind an interface, subscribed to through a constraint.
    /// </summary>
    public interface INotifier
    {
        event EventHandler Notified;

        void Notify();
    }

    public class Notifier : INotifier
    {
        public event EventHandler Notified;

        public void Notify()
        {
            Notified?.Invoke(this, EventArgs.Empty);
        }
    }
}
