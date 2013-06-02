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
			m_Figures = new List<Figure>();
			m_Continuations = new List<Action>();
		}

		private bool m_IsActive;
		public Canvas Canvas { get; private set; }
		public Dispatcher Dispatcher { get; private set; }

		private List<Figure> m_Figures;
		private List<Action> m_Continuations;

		public void Add(Figure fig)
		{
			m_Continuations.Add(() =>
				{
					fig.Register(this);
					m_Figures.Add(fig);
				}
			);
		}

		public void Remove(Figure fig)
		{
			m_Continuations.Add(() =>
				{
					fig.Unregister();
					m_Figures.Remove(fig);
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
			m_IsActive = true;
			new Thread(drawLoop).Start();
		}

		public void StopDrawing()
		{
			if (!m_IsActive)
				return;

			Canvas.Children.Clear();
			m_Figures = new List<Figure>();
			m_IsActive = false;
		}

		private void drawLoop()
		{
			while (m_IsActive)
			{
				foreach (var curr in m_Figures)
				{
					if (!m_IsActive) return;
					curr.UpdateObject();
				}

				if (m_Continuations.Count > 0)
				{
					foreach (var curr in m_Continuations)
						Dispatcher.Invoke(curr);

					m_Continuations.Clear();
				}

				Thread.Sleep(10);
			}
		}
	}
}
