using System;
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using System.Windows.Input;
using GraphicScript.Objects;
using Lens;
using Lens.SyntaxTree;
using Microsoft.Win32;
using Rect = GraphicScript.Objects.Rect;

namespace GraphicScript
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : INotifyPropertyChanged
	{
		public MainWindow()
		{
			InitializeComponent();
			DataContext = this;

			Status = Status.Ready;
			_manager = new FigureManager(Canvas, Dispatcher);

			CodeEditor.PreviewKeyDown += (s, e) =>
			{
				if (e.Key == Key.Tab)
				{
					CodeEditor.SelectedText = "    ";
					CodeEditor.SelectionStart += 4;
					CodeEditor.SelectionLength = 0;
					e.Handled = true;
				}
			};

			RegisterCommands();
		}

		#region Properties

		private FigureManager _manager;

		private string _code;
		public string Code
		{
			get => _code;
		    set
			{
				_code = value;
				Notify(() => Code);
			}
		}

		private string _errorMessage;
		public string ErrorMessage
		{
			get => _errorMessage;
		    set
			{
				_errorMessage = value;
				Notify(() => ErrorMessage);
			}
		}

		private Status _status;
		public Status Status
		{
			get => _status;
		    set
			{
				_status = value;
				Notify(() => Status);
				Notify(() => EditEnabled);
			}
		}

		public bool EditEnabled => Status == Status.Ready || Status == Status.Error;

	    #endregion

		#region Commands

		public ICommand LoadCommand { get; set; }
		public ICommand RunCommand { get; set; }
		public ICommand StopCommand { get; set; }

		protected override void OnClosing(CancelEventArgs e)
		{
			if(Status == Status.Success)
				Stop();

			base.OnClosing(e);
		}

		private void Load()
		{
			var dlg = new OpenFileDialog
			{
				CheckFileExists = true,
				InitialDirectory = Path.Combine(Environment.CurrentDirectory, "Examples"),
				DefaultExt = ".lns",
				Multiselect = false,
				Title = "Select a script to load:"
			};

			var result = dlg.ShowDialog();
			if (result == true)
			{
				using (var fs = new FileStream(dlg.FileName, FileMode.Open, FileAccess.Read))
				using (var sr = new StreamReader(fs))
					Code = sr.ReadToEnd();
			}
		}

		public void Run()
		{
			var lc = new LensCompiler();

			lc.RegisterType(typeof(Figure));
			lc.RegisterType("Rect", typeof(Rect));
			lc.RegisterType("Circle", typeof(Circle));
			lc.RegisterProperty("Screen", () => _manager);

			try
			{
				var fx = lc.Compile(Code);
				fx();
				
				Status = Status.Success;

				_manager.Draw();
			}
			catch (LensCompilerException ex)
			{
				Status = Status.Error;
				ErrorMessage = ex.FullMessage;
				MarkErrorLocation(ex);
			}
		}

		public void Stop()
		{
			Status = Status.Ready;
			_manager.StopDrawing();
			CodeEditor.Focus();
		}

		private void MarkErrorLocation(LensCompilerException ex)
		{
			if (ex.StartLocation == null)
				return;

			var stLoc = ex.StartLocation.Value;
			var endLoc = ex.EndLocation ?? new LexemLocation {Line = stLoc.Line, Offset = stLoc.Offset + 1 };

			var start = FlattenLocation(stLoc.Line, stLoc.Offset);
			var end = FlattenLocation(endLoc.Line, endLoc.Offset);
			CodeEditor.Select(start, end - start);
		}

		private int FlattenLocation(int line, int offset)
		{
			var lines = Code.Split('\n');
			var pos = 0;
			for (var idx = 0; idx < line-1; idx++)
				pos += lines[idx].Length + 1;

			return pos + offset-1;
		}

		private void RegisterCommands()
		{
			LoadCommand = new RoutedCommand("LoadCommand", GetType(), new InputGestureCollection { new KeyGesture(Key.O, ModifierKeys.Control) } );
			RunCommand = new RoutedCommand("RunCommand", GetType(), new InputGestureCollection { new KeyGesture(Key.F5) });
			StopCommand = new RoutedCommand("StopCommand", GetType(), new InputGestureCollection { new KeyGesture(Key.Escape) });

			CommandBindings.Add(new CommandBinding(LoadCommand, (s, e) => Load(), (s, e) => e.CanExecute = EditEnabled));
			CommandBindings.Add(new CommandBinding(RunCommand, (s, e) => Run(), (s, e) => e.CanExecute = EditEnabled && !string.IsNullOrEmpty(Code)));
			CommandBindings.Add(new CommandBinding(StopCommand, (s, e) => Stop(), (s, e) => e.CanExecute = Status == Status.Success));
		}

		#endregion

		#region NotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		private void Notify<T>(Expression<Func<T>> ptyAccessor)
		{
			var name = ((MemberExpression)ptyAccessor.Body).Member.Name;
			var handler = PropertyChanged;
			if (handler != null)
				handler(this, new PropertyChangedEventArgs(name));
		}

		#endregion
	}
}
