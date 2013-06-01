using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Threading;
using System.Windows.Input;
using GraphicScript.Misc;
using GraphicScript.Objects;
using Lens;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Utils;

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
			m_Manager = new FigureManager();
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
				notify(() => EditDisabled);
			}
		}

		public bool EditDisabled
		{
			get { return Status == Status.Compiling || Status == Status.Success; }
		}

		#endregion

		#region Commands

		protected override void OnClosing(CancelEventArgs e)
		{
			if(Status == Status.Success)
				Stop();

			base.OnClosing(e);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);

			if (e.Key == Key.F5 && Status.IsAnyOf(Status.Error, Status.Ready))
				Run();

			if (e.Key == Key.Escape && Status == Status.Success)
				Stop();
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

				m_Manager.Draw(Canvas, Dispatcher);
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
		}

		private void MarkErrorLocation(LensCompilerException ex)
		{
			var start = FlattenLocation(ex.StartLocation.Line, ex.StartLocation.Offset);
			var end = FlattenLocation(ex.EndLocation.Line, ex.EndLocation.Offset);
			CodeEditor.Select(start, end - start);
		}

		private int FlattenLocation(int line, int offset)
		{
			var lines = Code.Split('\n');
			var pos = 0;
			for (var idx = 0; idx < line-1; idx++)
				pos += lines[idx].Length;

			return pos + offset-1;
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
