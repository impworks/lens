using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lens;

namespace GraphHost
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
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

            lens.RegisterProperty("x", () => currX);
            lens.RegisterProperty("y", () => currY, y => currY = y);

            IEnumerable<(double x, double y)> GenerateValues()
            {
                var fx = lens.Compile(Func.Text);

                while (currX < endX)
                {
                    fx();
                    yield return (currX, currY);
                    currX += step;
                }
            }

            try
            {
                var values = GenerateValues().ToList();
                Graph.Plot(values.Select(v => v.x), values.Select(v => v.y));
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
            if (double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                return val;

            tb.Text = def.ToString(CultureInfo.InvariantCulture);
            return def;
        }
    }
}