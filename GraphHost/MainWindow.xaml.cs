using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lens;
using Lens.SyntaxTree;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;

namespace GraphHost
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : INotifyPropertyChanged
	{
		private LineGraph m_PreviousGraph;

		public MainWindow()
		{
			InitializeComponent();
		    Samples = new List<Sample>
	        {
	            new Sample("Константа", "y = 10"),
	            new Sample("Наклонная", "y = 2*x"),
	            new Sample("Парабола", "y = x**2"),
	            new Sample("Тригонометрия 1", "y = Math::Sin x"),
	            new Sample("Тригонометрия 2", "y = Math::Sin x / Math::Cos x"),
	            new Sample("Случайные числа", "y = rand x 20"),
	            new Sample("Шагообразный график", "y = if ((x as int) % 2 == 0) x as int else 0"),
	        };

            DataContext = this;
		}

	    public List<Sample> Samples { get; set; }

		private void Run_OnClick(object sender, RoutedEventArgs e)
		{
			run();
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
				run();

			base.OnKeyDown(e);
		}

		private void run()
		{
			var lens = new LensCompiler();

			var currX = getDouble(StartPos, -10);
			var endX = getDouble(EndPos, 10);
			var currY = 0.0;
			var step = getDouble(Step, 0.1);

			var obs = new ObservableDataSource<Point>();
			obs.SetXYMapping(p => p);

			if (m_PreviousGraph != null)
				m_PreviousGraph.Remove();

			m_PreviousGraph = Chart.AddLineGraph(obs, Colors.Green, 2, "Graph");

			lens.RegisterProperty("x", () => currX);
			lens.RegisterProperty("y", () => currY, y => currY = y);

			try
			{
				var fx = lens.Compile(Func.Text);

				while (currX < endX)
				{
				    try
				    {
				        fx();
				    }
				    catch
				    {
				        currY = 0;
				    }
				    obs.AppendAsync(Chart.Dispatcher, new Point(currX, currY));
					currX += step;
				}
			}
			catch (LensCompilerException ex)
			{
				MessageBox.Show(
					ex.FullMessage,
					"Compilation Error",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
			}
		}

		private double getDouble(TextBox tb, double def)
		{
			double val;
			if (double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
				return val;

			tb.Text = def.ToString(CultureInfo.InvariantCulture);
			return def;
        }

        #region NotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void notify<T>(Expression<Func<T>> ptyAccessor)
        {
            var name = ((MemberExpression)ptyAccessor.Body).Member.Name;
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }

        #endregion

	    private void Help_OnClick(object sender, RoutedEventArgs e)
	    {
	        (sender as Button).ContextMenu.IsOpen = true;
	    }

	    private void MenuItem_OnClick(object sender, RoutedEventArgs e)
	    {
	        Func.Text = (sender as MenuItem).Tag as string;
	    }
	}

    public class Sample
    {
        public Sample(string caption, string graph)
        {
            Caption = caption;
            Graph = graph;
        }

        public string Caption { get; set; }
        public string Graph { get; set; }
    }
}
