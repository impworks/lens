using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	internal class GlobalProperties: TestBase
	{
		[Test]
		public void Getter()
		{
			TestConfigured(
				ctx =>
				{
					ctx.RegisterProperty("half", HalfValue);
				},
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

			Assert.AreEqual(1000, m_Y);
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
		
		public static int HalfValue()
		{
			return 21;
		}

		private static int m_X;
		private static int m_Y;

		public static int GetX() { return m_X; }
		public static void SetX(int x) { m_X = x; }

		public static int GetY() { return m_Y; }
		public static void SetY(int y) { m_Y = y; }
	}
}
