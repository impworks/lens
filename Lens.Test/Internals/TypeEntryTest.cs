using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Resolver;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    /// <summary>
    /// The TypeEntry model is what the compiler will reason about instead of System.Type. These
    /// pin down its behaviour for host-provided types, which is the baseline the declared kinds of
    /// entry have to match.
    /// </summary>
    [TestFixture]
    internal class TypeEntryTest
    {
        private static TypeEntry Of<T>() => TypeEntryCache.Of(typeof(T));

        private static TypeEntry Of(Type type) => TypeEntryCache.Of(type);

        [Test]
        public void OneEntryPerType()
        {
            Assert.AreSame(Of<int>(), Of<int>());
            Assert.AreSame(Of<List<string>>(), Of<List<string>>());
            Assert.AreNotSame(Of<int>(), Of<long>());
        }

        [Test]
        public void EqualityIsByTypeNotByReference()
        {
            Assert.IsTrue(Of<int>() == Of<int>());
            Assert.IsFalse(Of<int>() == Of<long>());
            Assert.IsTrue(Of<int>() != Of<long>());

            Assert.IsFalse(Of<int>() == null);
            Assert.IsTrue((TypeEntry) null == null);
        }

        [Test]
        public void ClassificationMatchesReflection()
        {
            Assert.IsTrue(Of<int>().IsValueType);
            Assert.IsFalse(Of<string>().IsValueType);
            Assert.IsTrue(Of<string>().IsClass);
            Assert.IsTrue(Of<IDisposable>().IsInterface);
            Assert.IsTrue(Of<DayOfWeek>().IsEnum);
            Assert.IsTrue(Of<string>().IsSealed);
            Assert.IsTrue(Of<Array>().IsAbstract);
        }

        [Test]
        public void NamesMatchReflection()
        {
            Assert.AreEqual("Int32", Of<int>().Name);
            Assert.AreEqual("System.Int32", Of<int>().FullName);
            Assert.AreEqual("System", Of<int>().Namespace);
        }

        [Test]
        public void BaseTypeChainIsWalkable()
        {
            var chain = Of<ArgumentNullException>().SelfAndBaseTypes().ToArray();

            Assert.AreEqual(Of<ArgumentNullException>(), chain[0]);
            Assert.AreEqual(Of<ArgumentException>(), chain[1]);
            Assert.AreEqual(Of<object>(), chain[chain.Length - 1]);
            Assert.IsNull(Of<object>().BaseType);
        }

        [Test]
        public void ArraysRoundTrip()
        {
            var array = Of<int>().MakeArray();

            Assert.IsTrue(array.IsArray);
            Assert.AreEqual(Of<int>(), array.ElementType);
            Assert.AreEqual(Of<int[]>(), array);
            Assert.IsNull(Of<int>().ElementType);
        }

        [Test]
        public void ByRefRoundTrips()
        {
            var byRef = Of<int>().MakeByRef();

            Assert.IsTrue(byRef.IsByRef);
            Assert.AreEqual(Of<int>(), byRef.ElementType);
        }

        [Test]
        public void GenericInstantiationIsCanonical()
        {
            var resolver = new TypeResolutionContext();
            var definition = Of(typeof(List<>));

            var first = definition.MakeGeneric(resolver, new[] {Of<int>()});
            var second = definition.MakeGeneric(resolver, new[] {Of<int>()});

            Assert.AreSame(first, second);
            Assert.AreEqual(Of<List<int>>(), first);
        }

        [Test]
        public void GenericStructureIsReported()
        {
            var list = Of<List<int>>();

            Assert.IsTrue(list.IsGenericType);
            Assert.IsFalse(list.IsGenericTypeDefinition);
            Assert.AreEqual(Of(typeof(List<>)), list.GenericDefinition);
            Assert.AreEqual(new[] {Of<int>()}, list.GenericArguments);

            Assert.IsTrue(Of(typeof(List<>)).IsGenericTypeDefinition);
            Assert.IsEmpty(Of<int>().GenericArguments);
            Assert.IsNull(Of<int>().GenericDefinition);
        }

        [Test]
        public void InterfacesAreReported()
        {
            var resolver = new TypeResolutionContext();
            var ifaces = Of<List<int>>().GetInterfaces(resolver);

            Assert.Contains(Of<IEnumerable<int>>(), ifaces);
        }

        [Test]
        public void AssignabilityFollowsTheClr()
        {
            var resolver = new TypeResolutionContext();

            Assert.IsTrue(Of<object>().IsAssignableFrom(resolver, Of<string>()));
            Assert.IsFalse(Of<string>().IsAssignableFrom(resolver, Of<object>()));
            Assert.IsTrue(Of<IEnumerable<int>>().IsAssignableFrom(resolver, Of<List<int>>()));
            Assert.IsTrue(Of<int>().IsAssignableFrom(resolver, Of<int>()));
            Assert.IsFalse(Of<int>().IsAssignableFrom(resolver, null));
        }

        [Test]
        public void SubclassOfWalksTheChain()
        {
            Assert.IsTrue(Of<ArgumentNullException>().IsSubclassOf(Of<Exception>()));
            Assert.IsFalse(Of<Exception>().IsSubclassOf(Of<ArgumentNullException>()));
            Assert.IsFalse(Of<Exception>().IsSubclassOf(Of<Exception>()));
        }

        [Test]
        public void GenericParameterConstraintsAreReadable()
        {
            var parameter = Of(typeof(ConstrainedHolder<>).GetGenericArguments()[0]);

            Assert.IsTrue(parameter.IsGenericParameter);
            Assert.Contains(Of<IDisposable>(), parameter.GenericParameterConstraints);
            Assert.AreNotEqual(
                System.Reflection.GenericParameterAttributes.None,
                parameter.GenericParameterAttributes & System.Reflection.GenericParameterAttributes.DefaultConstructorConstraint
            );
        }

        [Test]
        public void MaterializeReturnsTheUnderlyingType()
        {
            Assert.AreSame(typeof(int), Of<int>().Materialize());
            Assert.AreSame(typeof(int[]), Of<int>().MakeArray().Materialize());
        }

        private class ConstrainedHolder<T>
            where T : class, IDisposable, new()
        {
        }
    }
}
