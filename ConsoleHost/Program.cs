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
					printError("An unexpected error has occured!", ex.Message, ConsoleColor.Yellow);
				}
			}
		}

		static bool RequestInput(out string input)
		{
			var lines = new List<string>();
			var prefix = 0;

			while (true)
			{
				Console.Write("> ");

				for (var idx = 0; idx < prefix; idx++)
					SendKeys.SendWait(" ");

				var line = Console.ReadLine();
				if (line == null)
					continue;

				if (line.Length > 0)
				{
					if (line.Length > 1 && line[line.Length - 1] == '#')
					{
						lines.Add(line.Substring(0, line.Length - 1));
						input = buildString(lines);
						return true;
					}

					if (line[0] == '#')
					{
						if (line == "#exit")
						{
							input = null;
							return false;
						}

						if (line == "#run")
						{
							input = buildString(lines);
							return true;
						}

						if (line == "#clr")
						{
							lines = new List<string>();
							Console.Clear();
							printPreamble();
							continue;
						}

						if (line == "#oops")
						{
							if (lines.Count > 0)
								lines.RemoveAt(lines.Count - 1);
							continue;
						}

						printHelp();
					}
				}

				prefix = getIdent(line);
				lines.Add(line.TrimEnd());
			}
		}

		static string buildString(List<string> lines)
		{
			var sb = new StringBuilder(lines.Count);
			foreach (var curr in lines)
				sb.AppendLine(curr);
			return sb.ToString();
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

		static void printError(string msg, string details, ConsoleColor clr = ConsoleColor.Red)
		{
			Console.ForegroundColor = clr;
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
			Console.WriteLine("To enter a script, just type it line by line.");
			Console.WriteLine();
			Console.WriteLine("Available interpreter commands:");
			Console.WriteLine();
			Console.WriteLine("  #exit - close the interpreter");
			Console.WriteLine("  #run  - execute the script and print the output");
			Console.WriteLine("  #oops - cancel last line");
			Console.WriteLine("  #clr  - clear the console");
			Console.WriteLine();
			Console.ResetColor();
		}

		static void printObject(dynamic obj)
		{
			Console.WriteLine(getStringRepresentation(obj));
			if((object)obj != null)
				Console.WriteLine("({0})", obj.GetType());
			Console.WriteLine();
		}

		static string getStringRepresentation(dynamic obj)
		{
			if ((object)obj == null)
				return "(null)";

			if (obj is string)
				return string.Format(@"""{0}""", obj);

			if (obj is IDictionary)
			{
				var list = new List<string>();
				foreach (var currKey in obj.Keys)
					list.Add(string.Format(
						"{0} => {1}",
						getStringRepresentation(currKey),
						getStringRepresentation(obj[currKey])
					));
				return string.Format("{{ {0} }}", string.Join("; ", list));
			}

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
