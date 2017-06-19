using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lens;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;

namespace GraphHost
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private LineGraph _previousGraph;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Run_OnClick(object sender, RoutedEventArgs e)
        {
            Run();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Run();

            base.OnKeyDown(e);
        }

        private void Run()
        {
            var lens = new LensCompiler();

            var currX = GetDouble(StartPos, -10);
            var endX = GetDouble(EndPos, 10);
            var currY = 0.0;
            var step = GetDouble(Step, 0.1);

            var obs = new ObservableDataSource<Point>();
            obs.SetXYMapping(p => p);

            if (_previousGraph != null)
                _previousGraph.Remove();

            _previousGraph = Chart.AddLineGraph(obs, Colors.Green, 2, "Graph");

            lens.RegisterProperty("x", () => currX);
            lens.RegisterProperty("y", () => currY, y => currY = y);

            try
            {
                var fx = lens.Compile(Func.Text);

                while (currX < endX)
                {
                    fx();
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

        private double GetDouble(TextBox tb, double def)
        {
            double val;
            if (double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                return val;

            tb.Text = def.ToString(CultureInfo.InvariantCulture);
            return def;
        }
    }
}