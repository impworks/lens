using System.Windows.Shapes;

namespace GraphicScript.Objects
{
	public class Rect : Figure
	{
		public int Width;
		public int Height;

		public Rect()
		{
			Width = Height = 100;
		}

		protected override Shape createShape()
		{
			return new Ellipse();
		}

		protected override void updateShape()
		{
			m_Shape.Width = Width;
			m_Shape.Height = Height;
		}
	}
}
