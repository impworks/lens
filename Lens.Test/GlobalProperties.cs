using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class GlobalProperties
	{
		[Test]
		public void Getter()
		{
			var lc = new LensCompiler();
			lc.RegisterProperty("half", HalfValue);
			var fx = lc.Compile("half * 2");
			Assert.AreEqual(42, fx());
		}

		[Test]
		public void GetAndSet()
		{
			SetX(1337);
			var lc = new LensCompiler();
			lc.RegisterProperty("x", GetX);
			lc.RegisterProperty("y", GetY, SetY);
			lc.Run("y = x - 337");
			Assert.AreEqual(1000, GetY());
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
