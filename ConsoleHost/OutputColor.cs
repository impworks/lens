using System;

namespace ConsoleHost
{
	public class OutputColor : IDisposable
	{
		public OutputColor(ConsoleColor clr, ConsoleColor background = ConsoleColor.Black)
		{
			Console.ForegroundColor = clr;
			Console.BackgroundColor = background;
		}

		public void Dispose()
		{
			Console.ResetColor();
		}
	}
}
