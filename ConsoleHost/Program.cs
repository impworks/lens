using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Lens;
using Lens.SyntaxTree;

namespace ConsoleHost
{
	class Program
	{
		static void Main()
		{
			printPreamble();

			string source;
			while (RequestInput(out source))
			{
				Console.WriteLine();
				try
				{
					var lc = new LensCompiler();
					var res = lc.Run(source);

					Console.ForegroundColor = ConsoleColor.DarkGreen;
					Console.WriteLine("Compiled successfully!");
					Console.ResetColor();

					Console.WriteLine(res ?? "(null)");
					Console.WriteLine();
				}
				catch (LensCompilerException ex)
				{
					printError("Error compiling the script!", ex.FullMessage);
				}
				catch (Exception ex)
				{
					printError("Error parsing the script!", ex.Message);
				}
			}
		}

		static bool RequestInput(out string input)
		{
			var sb = new StringBuilder();
			var prefix = 0;

			while (true)
			{
				Console.Write("> ");

				for (var idx = 0; idx < prefix; idx++)
					SendKeys.SendWait(" ");

				var line = Console.ReadLine();
				if (line == null)
					continue;

				if (line.Length > 0 && line[0] == '#')
				{
					if (line == "#exit")
					{
						input = null;
						return false;
					}

					if (line == "#run")
					{
						input = sb.ToString();
						return true;
					}

					if (line == "#clr")
					{
						Console.Clear();
						printPreamble();
						continue;
					}

					printHelp();
				}

				prefix = getIdent(line);
				sb.AppendLine(line.TrimEnd());
			}
		}

		static void printPreamble()
		{
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine("=====================");
			Console.WriteLine("  LENS Console Host");
			Console.WriteLine("=====================");
			Console.WriteLine();
			Console.ResetColor();
		}

		static void printError(string msg, string details)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(msg);
			Console.WriteLine();
			Console.WriteLine(details);
			Console.ResetColor();
			Console.WriteLine();
		}

		static void printHelp()
		{
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.WriteLine();
			Console.WriteLine("Available commands:");
			Console.WriteLine();
			Console.WriteLine("  #exit - close the interpreter");
			Console.WriteLine("  #run  - execute the script and print the output");
			Console.WriteLine("  #clr  - clear the console");
			Console.WriteLine();
			Console.ResetColor();
		}

		static int getIdent(string line)
		{
			var idx = 0;
			while (idx < line.Length && line[idx] == ' ')
				idx++;

			if (shouldIdent(line))
				idx += 4;
			return idx;
		}

		private static Regex[] LineFeeds = new[]
		{
			new Regex(@"^type [_a-z][_a-z0-9]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"^record [_a-z][_a-z0-9]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"^(if|while)\s*\(.+\)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"^try$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"^catch(\([_a-][_a-z0-9]*(\s+[_a-][_a-z0-9]*)?\))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
		};

		static bool shouldIdent(string line)
		{
			var trim = line.Trim();
			if (trim.EndsWith("->"))
				return true;

			foreach (var curr in LineFeeds)
				if (curr.IsMatch(trim))
					return true;

			return false;
		}
	}
}
