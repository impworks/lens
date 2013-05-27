using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GraphicScript.Objects
{
	public abstract class Figure
	{
		public int X { get; set; }
		public int Y { get; set; }

		public Color FillColor;
		public Color StrokeColor;
		public int StrokeThickness;

		protected Shape m_Shape;

		public Action<Figure> UpdateAction;

		public Figure()
		{
			FillColor = Colors.White;
			StrokeColor = Colors.Black;
			StrokeThickness = 1;
		}

		public void Register(Canvas cvs)
		{
			m_Shape = createShape();
			cvs.Children.Add(m_Shape);
		}

		public void Update()
		{
			UpdateAction(this);

			m_Shape.Stroke = new SolidColorBrush(StrokeColor);
			m_Shape.Fill = new SolidColorBrush(FillColor);
			m_Shape.StrokeThickness = StrokeThickness;

			Canvas.SetLeft(m_Shape, X);
			Canvas.SetTop(m_Shape, Y);
		}

		protected abstract Shape createShape();
		protected abstract void updateShape();
	}
}
