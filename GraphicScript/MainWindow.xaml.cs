using System;
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Input;
using GraphicScript.Misc;
using GraphicScript.Objects;
using Lens;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Utils;
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
			m_Manager = new FigureManager(Canvas, Dispatcher);

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

		private FigureManager m_Manager;

		private string m_Code;
		public string Code
		{
			get { return m_Code; }
			set
			{
				m_Code = value;
				notify(() => Code);
			}
		}

		private string m_ErrorMessage;
		public string ErrorMessage
		{
			get { return m_ErrorMessage; }
			set
			{
				m_ErrorMessage = value;
				notify(() => ErrorMessage);
			}
		}

		private Status m_Status;
		public Status Status
		{
			get { return m_Status; }
			set
			{
				m_Status = value;
				notify(() => Status);
				notify(() => EditEnabled);
			}
		}

		public bool EditEnabled
		{
			get { return Status == Status.Ready || Status == Status.Error; }
		}

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
				Title = "Выберите скрипт для заргрузки"
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
			lc.RegisterProperty("Screen", () => m_Manager);

			try
			{
				var fx = lc.Compile(Code);
				fx();
				
				Status = Status.Success;

				m_Manager.Draw();
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
			m_Manager.StopDrawing();
			CodeEditor.Focus();
		}

		private void MarkErrorLocation(LensCompilerException ex)
		{
			if (ex.StartLocation.Line == 0 || ex.EndLocation.Line == 0)
				return;

			var start = FlattenLocation(ex.StartLocation.Line, ex.StartLocation.Offset);
			var end = FlattenLocation(ex.EndLocation.Line, ex.EndLocation.Offset);
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

		private void notify<T>(Expression<Func<T>> ptyAccessor)
		{
			var name = ((MemberExpression)ptyAccessor.Body).Member.Name;
			var handler = PropertyChanged;
			if (handler != null)
				handler(this, new PropertyChangedEventArgs(name));
		}

		#endregion
	}
}
