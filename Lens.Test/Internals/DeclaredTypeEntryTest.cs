using System.Linq;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Resolver;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    /// <summary>
    /// A type the script declares is represented by an entry that answers from the declaration, not
    /// from its TypeBuilder. That is what removes the NotSupportedException class of bugs, and what
    /// eventually lets a script be analysed without an assembly.
    /// </summary>
    [TestFixture]
    internal class DeclaredTypeEntryTest : TestBase
    {
        private static Context Declare(string src)
        {
            var ctx = new Context(new LensCompilerOptions());
            ctx.Compile(Parse(src));
            return ctx;
        }

        private const string Records = @"
record Point
    X : int
    Y : int

type Shape
    Circle of Point
    Empty

1";

        [Test]
        public void ADeclaredTypeIsRecognisedAsDeclared()
        {
            var ctx = Declare(Records);

            var point = ctx.ResolveType("Point");
            Assert.IsTrue(point.IsDeclared);
            Assert.IsInstanceOf<TypeEntityEntry>(point);

            Assert.IsFalse(ctx.ResolveType("int").IsDeclared);
            Assert.IsFalse(TypeEntryCache.Of<string>().IsDeclared);
        }

        [Test]
        public void ADeclaredTypeReportsItselfWithoutReflection()
        {
            var ctx = Declare(Records);
            var point = ctx.ResolveType("Point");

            Assert.AreEqual("Point", point.Name);
            Assert.IsTrue(point.IsClass);
            Assert.IsFalse(point.IsValueType);
            Assert.IsFalse(point.IsInterface);
            Assert.IsTrue(point.IsSealed);
            Assert.IsFalse(point.IsGenericType);
        }

        [Test]
        public void ALabelKnowsItsParentType()
        {
            var ctx = Declare(Records);

            var shape = ctx.ResolveType("Shape");
            var circle = ctx.ResolveType("Circle");

            Assert.AreEqual(shape, circle.BaseType);
            Assert.IsTrue(circle.IsSubclassOf(shape));
            Assert.IsFalse(shape.IsSubclassOf(circle));
        }

        [Test]
        public void AssignabilityBetweenDeclaredTypesNeedsNoReflection()
        {
            var ctx = Declare(Records);

            var shape = ctx.ResolveType("Shape");
            var circle = ctx.ResolveType("Circle");

            Assert.IsTrue(shape.IsAssignableFrom(ctx.Resolver, circle));
            Assert.IsFalse(circle.IsAssignableFrom(ctx.Resolver, shape));
            Assert.IsTrue(TypeEntryCache.Of<object>().IsAssignableFrom(ctx.Resolver, circle));
        }

        [Test]
        public void TheEntryForADeclarationIsCanonical()
        {
            var ctx = Declare(Records);

            var first = ctx.ResolveType("Point");
            var second = ctx.ResolveType("Point");

            Assert.AreSame(first, second);

            // and a builder arriving back from reflection resolves to the same entry rather than to
            // a bare wrapper, which is what keeps type comparison honest
            Assert.AreSame(first, TypeEntryCache.Of(first.Materialize()));
        }

        [Test]
        public void AGenericDeclarationReportsItsArityAndParameters()
        {
            var ctx = Declare(@"
record Pair<A, B>
    First : A
    Second : B

1");

            var pair = ctx.ResolveType("Pair<int, string>");

            Assert.IsTrue(pair.IsGenericType);
            Assert.IsFalse(pair.IsGenericTypeDefinition);
            Assert.IsTrue(pair.GenericDefinition.IsDeclared);
            Assert.AreEqual("Pair`2", pair.GenericDefinition.Name);
            Assert.AreEqual(
                new[] {TypeEntryCache.Of<int>(), TypeEntryCache.Of<string>()},
                pair.GenericArguments
            );
        }

        [Test]
        public void AnInstantiationOfADeclarationResolvesBackToTheDeclaration()
        {
            var ctx = Declare(@"
record Box<T>
    Value : T

1");

            var box = ctx.ResolveType("Box<int>");

            // this is the path that used to require testing 'is TypeBuilder' and looking the
            // declaration up again by its arity-mangled emitted name
            Assert.IsTrue(ctx.IsDeclaredType(box));
            Assert.IsTrue(ctx.IsDeclaredType(ctx.ResolveType("Box<string>")));
            Assert.IsFalse(ctx.IsDeclaredType(TypeEntryCache.Of<System.Collections.Generic.List<int>>()));
        }

        [Test]
        public void FieldsOfAConstructedDeclarationCarryTheSubstitutedType()
        {
            var ctx = Declare(@"
record Box<T>
    Value : T

1");

            var intBox = ctx.ResolveField(ctx.ResolveType("Box<int>"), "Value");
            var stringBox = ctx.ResolveField(ctx.ResolveType("Box<string>"), "Value");

            Assert.IsTrue(intBox.FieldType.Is<int>());
            Assert.IsTrue(stringBox.FieldType.Is<string>());
        }

        [Test]
        public void TheEntryExistsBeforeTheDeclarationIsPrepared()
        {
            // a bare entity with no builder: the whole reason this class exists is that it can be
            // asked questions in this state, which a TypeBuilder cannot because there is not one
            var ctx = new Context(new LensCompilerOptions());
            var entity = ctx.CreateType("Standalone", isSealed: true, prepare: false);

            var entry = entity.TypeInfo;

            Assert.IsNull(entity.TypeBuilder);
            Assert.IsTrue(entry.IsDeclared);
            Assert.AreEqual("Standalone", entry.Name);
            Assert.IsTrue(entry.IsSealed);
            Assert.IsTrue(entry.IsClass);
            Assert.AreEqual(TypeEntryCache.Of<object>(), entry.BaseType);
            Assert.IsEmpty(entry.GetInterfaces(ctx.Resolver));
        }
    }
}
