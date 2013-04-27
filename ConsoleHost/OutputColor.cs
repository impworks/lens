using System;

namespace ConsoleHost
{
	public class OutputColor : IDisposable
	{
		public OutputColor(ConsoleColor clr)
		{
			Console.ForegroundColor = clr;
		}

		public void Dispose()
		{
			Console.ResetColor();
		}
	}
}
