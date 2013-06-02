using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GraphicScript.Objects
{
	public abstract class Figure
	{
		public double X { get; set; }
		public double Y { get; set; }

		public Tuple<double, double> Position
		{
			get { return new Tuple<double, double>(X, Y); }
			set { X = value.Item1; Y = value.Item2; }
		}

		public Color Fill;
		public Color Outline;
		public double Thickness;

		public Shape Shape { get; protected set; }

		public Action Update;
		public Action Focus;
		public Action Blur;
		public Action Click;

		private FigureManager m_Manager;

		public Figure()
		{
			Fill = Colors.White;
			Outline = Colors.Black;
			Thickness = 1;
		}

		public void Register(FigureManager manager)
		{
			m_Manager = manager;

			Shape = createShape();

			Shape.MouseEnter += (s, e) => { if (Focus != null) Focus(); };
			Shape.MouseLeave += (s, e) => { if (Blur != null) Blur(); };
			Shape.MouseLeftButtonDown += (s, e) => { if (Click != null) Click(); };

			m_Manager.Canvas.Children.Add(Shape);
		}

		public void Unregister()
		{
			m_Manager.Dispatcher.Invoke(new Action(() => m_Manager.Canvas.Children.Remove(Shape)));
		}

		public void UpdateObject()
		{
			if (Update != null)
				Update();

			m_Manager.Dispatcher.Invoke(
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
