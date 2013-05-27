using System.Windows.Shapes;

namespace GraphicScript.Objects
{
	public class Circle : Figure
	{
		public int Radius;

		public Circle()
		{
			Radius = 50;
		}

		protected override Shape createShape()
		{
			return new Ellipse();
		}

		protected override void updateShape()
		{
			m_Shape.Width = m_Shape.Height = Radius;
		}
	}
}
