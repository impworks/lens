using System;
using System.ComponentModel;
using System.Linq.Expressions;

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

		private string m_State;
		public string State
		{
			get { return m_State; }
			set
			{
				m_State = value;
				notify(() => State);
			}
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
