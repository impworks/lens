using System;
using System.Collections.Generic;
using System.Reflection;
using Lens.Compiler;
using Lens.Resolver;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    /// <summary>
    /// A generic parameter declared in LENS code answers from the compiler's constraint model, not
    /// from its GenericTypeParameterBuilder - which refuses to report its constraints until the
    /// declaration that owns it has been created.
    /// </summary>
    [TestFixture]
    internal class GenericParameterEntryTest : TestBase
    {
        private static Context Declare(string src)
        {
            var ctx = new Context(new LensCompilerOptions());
            ctx.Compile(Parse(src));
            return ctx;
        }

        private static TypeEntry ParameterOf(Context ctx, string typeName)
        {
            return ctx.ResolveType(typeName).GenericDefinition.GenericArguments[0];
        }

        [Test]
        public void ADeclaredParameterIsRecognised()
        {
            var ctx = Declare("record Box<T>\n    Value : T\n\n1");
            var parameter = ParameterOf(ctx, "Box<int>");

            Assert.IsInstanceOf<GenericParameterEntry>(parameter);
            Assert.IsTrue(parameter.IsGenericParameter);
            Assert.IsTrue(parameter.IsDeclared);
            Assert.IsTrue(ctx.Resolver.IsDeclaredTypeParameter(parameter));
            Assert.AreEqual("T", parameter.Name);

            // an unsubstituted parameter that leaked in from an imported generic is not one of ours
            var imported = TypeEntryCache.Of(typeof(List<>).GetGenericArguments()[0]);
            Assert.IsFalse(ctx.Resolver.IsDeclaredTypeParameter(imported));
        }

        [Test]
        public void AnUnconstrainedParameterBehavesLikeAReferenceType()
        {
            var ctx = Declare("record Box<T>\n    Value : T\n\n1");
            var parameter = ParameterOf(ctx, "Box<int>");

            Assert.IsFalse(parameter.IsValueType);
            Assert.IsTrue(parameter.IsClass);
            Assert.IsFalse(parameter.IsInterface);
            Assert.AreEqual(TypeEntryCache.Of<object>(), parameter.BaseType);
            Assert.AreEqual(GenericParameterAttributes.None, parameter.GenericParameterAttributes);
            Assert.IsEmpty(parameter.GenericParameterConstraints);
        }

        /// <summary>
        /// The parameter of a declared generic function, taken from the declaration rather than from
        /// the generic scope, which only lives while the declaration is being bound.
        /// </summary>
        private static TypeEntry FunctionParameterOf(Context ctx, string functionName)
        {
            return ctx.MainType.ResolveMethodGroup(functionName)[0].GenericParameters[0].TypeInfo;
        }

        [Test]
        public void KeywordConstraintsAreReported()
        {
            var ctx = Declare("fun make<T = class & new>:string (x:T) -> x.ToString ()\n1");
            var parameter = FunctionParameterOf(ctx, "make");

            var attrs = parameter.GenericParameterAttributes;
            Assert.IsTrue(attrs.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint));
            Assert.IsTrue(attrs.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint));
            Assert.IsFalse(attrs.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint));
        }

        [Test]
        public void AStructConstrainedParameterExtendsValueType()
        {
            var ctx = Declare("fun show<T = struct>:string (x:T) -> x.ToString ()\n1");
            var parameter = FunctionParameterOf(ctx, "show");

            Assert.IsTrue(parameter.IsValueType);
            Assert.IsFalse(parameter.IsClass);
            Assert.AreEqual(TypeEntryCache.Of<ValueType>(), parameter.BaseType);
        }

        [Test]
        public void ATypeConstraintBecomesTheBaseTypeAndInterfacesShowThrough()
        {
            var ctx = Declare("fun cmp<T = Exception & IComparable>:string (x:T) -> x.ToString ()\n1");
            var parameter = FunctionParameterOf(ctx, "cmp");

            Assert.AreEqual(TypeEntryCache.Of<Exception>(), parameter.BaseType);
            Assert.Contains(TypeEntryCache.Of<IComparable>(), parameter.GetInterfaces(ctx.Resolver));
            Assert.Contains(TypeEntryCache.Of<Exception>(), parameter.GenericParameterConstraints);
            Assert.Contains(TypeEntryCache.Of<IComparable>(), parameter.GenericParameterConstraints);
        }

        [Test]
        public void AParameterIsNotAGenericDefinition()
        {
            var ctx = Declare("record Box<T>\n    Value : T\n\n1");
            var parameter = ParameterOf(ctx, "Box<int>");

            Assert.IsFalse(parameter.IsGenericType);
            Assert.IsFalse(parameter.IsGenericTypeDefinition);
            Assert.Throws<InvalidOperationException>(
                () => parameter.MakeGeneric(ctx.Resolver, TypeEntryCache.Of<int>())
            );
        }

        [Test]
        public void AParameterAcceptsOnlyItself()
        {
            var ctx = Declare("record Box<T>\n    Value : T\n\n1");
            var parameter = ParameterOf(ctx, "Box<int>");

            Assert.IsTrue(parameter.IsAssignableFrom(ctx.Resolver, parameter));
            Assert.IsFalse(parameter.IsAssignableFrom(ctx.Resolver, TypeEntryCache.Of<int>()));
            Assert.IsFalse(parameter.IsAssignableFrom(ctx.Resolver, null));
        }

        [Test]
        public void TheEntryForAParameterIsCanonical()
        {
            var ctx = Declare("record Box<T>\n    Value : T\n\n1");

            var first = ParameterOf(ctx, "Box<int>");
            var second = ParameterOf(ctx, "Box<string>");

            // both instantiations share one definition, so they share one parameter
            Assert.AreSame(first, second);

            // and the builder resolves back to the same entry rather than to a bare wrapper
            Assert.AreSame(first, TypeEntryCache.Of(first.Materialize()));
        }

        [Test]
        public void ConstrainedGenericsStillCompileAndRun()
        {
            // the end-to-end guard: constraint checking now reads the entry rather than the
            // builder-keyed constraint map
            Test("fun firstOf<T>:T (items:T[]) -> items[0]\nfirstOf (new[3; 2; 1])", 3);
        }
    }
}
