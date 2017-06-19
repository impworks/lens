using System;
using System.Windows.Shapes;

namespace GraphicScript.Objects
{
	public class Rect : Figure
	{
		public double Width;
		public double Height;

		public Tuple<double, double> Size
		{
			get => new Tuple<double, double>(Width, Height);
		    set { Width = value.Item1; Height = value.Item2; }
		}

		public Rect()
		{
			Width = Height = 50;
		}

		protected override Shape CreateShape()
		{
			return new Rectangle();
		}

		protected override void UpdateShape()
		{
			Shape.Width = Width;
			Shape.Height = Height;
		}
	}
}
