using System;
using System.ComponentModel;
using System.Linq.Expressions;
using GraphicScript.Misc;

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
		}

		#region Properties

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
			}
		}

		#endregion

		#region Commands



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
