using System.Windows.Shapes;

namespace GraphicScript.Objects
{
	public class Rect : Figure
	{
		public int Width;
		public int Height;

		public Rect()
		{
			Width = Height = 50;
		}

		protected override Shape createShape()
		{
			return new Rectangle();
		}

		protected override void updateShape()
		{
			Shape.Width = Width;
			Shape.Height = Height;
		}
	}
}
