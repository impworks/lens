using System;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class GlobalProperties : TestBase
    {
        [Test]
        public void Getter()
        {
            TestConfigured(
                ctx => { ctx.RegisterProperty("half", HalfValue); },
                "half * 2",
                42
            );
        }

        [Test]
        public void Statics()
        {
            SetX(1337);
            TestConfigured(
                ctx =>
                {
                    ctx.RegisterProperty("x", GetX);
                    ctx.RegisterProperty("y", GetY, SetY);
                },
                "y = x - 337",
                null
            );

            Assert.AreEqual(1000, _y);
        }

        [Test]
        public void Lambdas()
        {
            var x = 10;
            var y = 0;
            TestConfigured(
                ctx =>
                {
                    ctx.RegisterProperty("x", () => x, nx => x = nx);
                    ctx.RegisterProperty("y", () => y, ny => y = ny);
                },
                "y = x + 32",
                null
            );

            Assert.AreEqual(42, y);
        }

        [Test]
        public void ImportFunction()
        {
            Func<int, int> doubler = x => x * 2;
            TestConfigured(
                ctx =>
                {
                    ctx.RegisterFunction("doubler", doubler);
                },
                "doubler 21",
                42
            );
        }

        public static int HalfValue() => 21;

        private static int _x;
        private static int _y;

        public static int GetX() => _x;
        public static void SetX(int x) => _x = x;

        public static int GetY() => _y;
        public static void SetY(int y) => _y = y;
    }
}