using System;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class EventsTest : TestBase
    {
        [Test]
        public void BasicEvent()
        {
            var src = @"
var count = 0
let obj = new Lens.Test.Features.EventSample ()
var handler = ((s e) -> count += 1) as EventHandler
obj.Basic += handler
obj.RaiseBasic ()
obj.RaiseBasic ()
obj.Basic -= handler
obj.RaiseBasic ()
count
";
            Test(src, 2);
        }

        [Test]
        public void SpecificEvent()
        {
            var src = @"
var count = 0
let obj = new Lens.Test.Features.EventSample ()
var handler = ((s e) -> count += e.Value) as Lens.Test.Features.SpecificEventHandler
obj.Specific += handler
obj.RaiseSpecific 1
obj.RaiseSpecific 2
obj.Specific -= handler
obj.RaiseSpecific 3
count
";
            Test(src, 3);
        }

        [Test]
        public void GenericEvent()
        {
            var src = @"
var str = """"
let obj = new Lens.Test.Features.GenericEventSample<int> 0
var handler = ((s e) -> str += e.Value) as Lens.Test.Features.GenericEventHandler<string>
obj.SpecifiedGeneric += handler
obj.RaiseSpecifiedGeneric ""a""
obj.RaiseSpecifiedGeneric ""b""
obj.SpecifiedGeneric -= handler
obj.RaiseSpecifiedGeneric ""c""
str
";

            Test(src, "ab");
        }

        [Test]
        public void SpecifiedGenericInference()
        {
            var src = @"
var result:string
let obj = new Lens.Test.Features.GenericEventSample<int> 2
obj.SpecifiedGeneric += (s e) -> result = ""value = "" + e.Value
obj.RaiseSpecifiedGeneric ""omg""
result
";

            Test(src, "value = omg");
        }

        [Test]
        public void FullGenericInference()
        {
            var src = @"
var result:string
let obj = new Lens.Test.Features.GenericEventSample<string> ""a""
obj.FullGeneric += (s e) -> result = ""value = "" + e.Value
obj.RaiseFullGeneric ()
result
";

            Test(src, "value = a");
        }

        [Test]
        public void HandlerArgsCountError1()
        {
            var src = @"
let obj = new Lens.Test.Features.EventSample ()
obj.Basic += (s) -> print 1
";

            TestError(src, CompilerMessages.LambdaArgumentsCountMismatch);
        }

        [Test]
        public void HandlerArgsCountError2()
        {
            var src = @"
let obj = new Lens.Test.Features.EventSample ()
obj.Basic += (s e x) -> print 1
";

            TestError(src, CompilerMessages.LambdaArgumentsCountMismatch);
        }

        [Test]
        public void HandlerArgsTypesError()
        {
            var src = @"
let obj = new Lens.Test.Features.EventSample ()
obj.Basic += (s:object e:int) -> print 1
";

            TestError(src, CompilerMessages.CastDelegateArgTypesMismatch);
        }

        [Test]
        public void HandlerReturnTypeError()
        {
            var src = @"
let obj = new Lens.Test.Features.EventSample ()
obj.Basic += (s e) -> 1
";

            TestError(src, CompilerMessages.CastDelegateReturnTypesMismatch);
        }

        [Test]
        public void EventAsExpressionError()
        {
            var src = @"
let obj = new Lens.Test.Features.EventSample ()
obj.Basic + 1
";

            TestError(src, CompilerMessages.EventAsExpr);
        }
    }

    /// <summary>
    /// Sample class for basic events.
    /// </summary>
    public class EventSample
    {
        public event EventHandler Basic = delegate { };

        public void RaiseBasic()
        {
            Basic(this, new EventArgs());
        }

        public event SpecificEventHandler Specific = delegate { };

        public void RaiseSpecific(int value)
        {
            Specific(this, new SpecificEventArgs {Value = value});
        }
    }

    /// <summary>
    /// Sample class for generic events.
    /// </summary>
    public class GenericEventSample<T>
    {
        public GenericEventSample(T value)
        {
            _defaultValue = value;
        }

        private readonly T _defaultValue;

        public event GenericEventHandler<T> FullGeneric = delegate { };

        public void RaiseFullGeneric()
        {
            FullGeneric(this, new GenericEventArgs<T> {Value = _defaultValue});
        }

        public event GenericEventHandler<string> SpecifiedGeneric = delegate { };

        public void RaiseSpecifiedGeneric(string value)
        {
            SpecifiedGeneric(this, new GenericEventArgs<string> {Value = value});
        }
    }

    public delegate void SpecificEventHandler(object sender, SpecificEventArgs args);

    public delegate void GenericEventHandler<T>(object sender, GenericEventArgs<T> args);

    public class SpecificEventArgs : EventArgs
    {
        public int Value;
    }

    public class GenericEventArgs<T> : EventArgs
    {
        public T Value;
    }
}