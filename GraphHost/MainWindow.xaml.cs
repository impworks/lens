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

        /// <summary>
        /// Plots the formula, one point per step.
        ///
        /// The formula is evaluated through the asynchronous door because this runs on the UI thread:
        /// a formula that awaits would post its continuation back here, and a thread waiting for it
        /// would be the thread that has to run it. A formula that awaits nothing - which is every
        /// formula worth plotting - completes inline and never yields, so the loop costs nothing.
        /// </summary>
        private async void Run()
        {
            var lens = new LensCompiler();

            var currX = GetDouble(StartPos, -10);
            var endX = GetDouble(EndPos, 10);
            var currY = 0.0;
            var step = GetDouble(Step, 0.1);

            lens.RegisterProperty("x", () => currX);
            lens.RegisterProperty("y", () => currY, y => currY = y);

            try
            {
                var fx = lens.CompileAsync(Func.Text);
                var values = new List<(double x, double y)>();

                while (currX < endX)
                {
                    await fx();
                    values.Add((currX, currY));
                    currX += step;
                }

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