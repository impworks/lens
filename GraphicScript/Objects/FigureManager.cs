using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GraphicScript.Objects
{
	public class FigureManager
	{
		public FigureManager()
		{
			Clear();
		}

		private Canvas m_Canvas;
		private List<Figure> m_Figures;

		public void Clear()
		{
			m_Figures = new List<Figure>();
			m_Canvas = null;
		}

		public void Register(Figure fig)
		{
			m_Figures.Add(fig);
		}

		public void Draw(Canvas cvs)
		{
			foreach (var curr in m_Figures)
				curr.Register(cvs);

			m_Canvas = cvs;
			new Thread(drawLoop).Start();
		}

		public void StopDrawing()
		{
			if (m_Canvas == null)
				return;

			m_Canvas.Children.Clear();
			m_Canvas = null;
		}

		private void drawLoop()
		{
			while (m_Canvas != null)
			{
				foreach (var curr in m_Figures)
				{
					if (m_Canvas == null)
						return;
					Dispatcher.CurrentDispatcher.Invoke(new Action(curr.Update));
				}

				Thread.Sleep(100);
			}
		}
	}
}
