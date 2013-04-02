using System;
using System.Collections;
using System.Collections.Generic;
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
					printObject(res);
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
						sb = new StringBuilder();
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
			Console.WriteLine("(type #help for help)");
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

		static void printObject(dynamic obj)
		{
			Console.WriteLine(getStringRepresentation(obj));
			Console.WriteLine();
		}

		static string getStringRepresentation(dynamic obj)
		{
			if (obj is string)
				return string.Format(@"""{0}""", obj);

			if (obj is IEnumerable)
			{
				var list = new List<string>();
				foreach(var curr in obj)
					list.Add(getStringRepresentation(curr));
				return string.Format("[ {0} ]", string.Join("; ", list));
			}

			return obj.ToString();
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
