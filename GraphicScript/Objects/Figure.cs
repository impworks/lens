using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GraphicScript.Objects
{
	public abstract class Figure
	{
		public int X { get; set; }
		public int Y { get; set; }

		public Tuple<int, int> Position
		{
			get { return new Tuple<int, int>(X, Y);}
			set { X = value.Item1; Y = value.Item2; }
		}

		public Color Fill;
		public Color Outline;
		public int Thickness;

		public Shape Shape { get; protected set; }

		public Action UpdateAction;

		private Dispatcher m_Dispatcher;

		public Figure()
		{
			Fill = Colors.White;
			Outline = Colors.Black;
			Thickness = 2;
		}

		public void Register(Canvas cvs, Dispatcher disp)
		{
			m_Dispatcher = disp;
			Shape = createShape();
			cvs.Children.Add(Shape);
		}

		public void Update()
		{
			if (UpdateAction != null)
				UpdateAction();

			m_Dispatcher.Invoke(
				new Action(() =>
				{
					updateShape();
					Shape.Stroke = new SolidColorBrush(Outline);
					Shape.Fill = new SolidColorBrush(Fill);
					Shape.StrokeThickness = Thickness;

					Canvas.SetLeft(Shape, X);
					Canvas.SetTop(Shape, Y);
				}
			));
		}

		protected abstract Shape createShape();
		protected abstract void updateShape();
	}
}
