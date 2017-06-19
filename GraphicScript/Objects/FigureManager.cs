using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GraphicScript.Objects
{
	public class FigureManager
	{
		public FigureManager(Canvas cvs, Dispatcher disp)
		{
			Canvas = cvs;
			Dispatcher = disp;
			_figures = new List<Figure>();
			_continuations = new List<Action>();
		}

		private bool _isActive;
		public Canvas Canvas { get; private set; }
		public Dispatcher Dispatcher { get; private set; }

		private List<Figure> _figures;
		private List<Action> _continuations;

		public void Add(Figure fig)
		{
			_continuations.Add(() =>
				{
					fig.Register(this);
					_figures.Add(fig);
				}
			);
		}

		public void Remove(Figure fig)
		{
			_continuations.Add(() =>
				{
					fig.Unregister();
					_figures.Remove(fig);
				}
			);
		}

		public void Add(IEnumerable<Figure> figs)
		{
			foreach(var curr in figs)
				Add(curr);
		}

		public void Draw()
		{
			_isActive = true;
			new Thread(DrawLoop).Start();
		}

		public void StopDrawing()
		{
			if (!_isActive)
				return;

			Canvas.Children.Clear();
			_figures = new List<Figure>();
			_isActive = false;
		}

		private void DrawLoop()
		{
			while (_isActive)
			{
				foreach (var curr in _figures)
				{
					if (!_isActive) return;
					curr.UpdateObject();
				}

				if (_continuations.Count > 0)
				{
					foreach (var curr in _continuations)
						Dispatcher.Invoke(curr);

					_continuations.Clear();
				}

				Thread.Sleep(10);
			}
		}
	}
}
