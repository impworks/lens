using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Lens;

namespace ConsoleHost
{
	class Program
	{
		static void Main()
		{
			printPreamble();

			string source;
			var timer = false;
			while (RequestInput(out source, ref timer))
			{
				Console.WriteLine();
				try
				{
					var lc = new LensCompiler(new LensCompilerOptions { AllowSave = true });

					Func<object> fx = null;
					object res = null;

					var compileTime = measureTime(() => fx = lc.Compile(source));
					var runTime = measureTime(() => res = fx());

					printObject(res);

					printInfo(timer, compileTime, runTime);
				}
				catch (LensCompilerException ex)
				{
					printError("Error compiling the script!", ex.FullMessage);
				}
				catch (Exception ex)
				{
					printError("An unexpected error has occured!", ex.Message + Environment.NewLine + ex.StackTrace, ConsoleColor.Yellow);
				}
			}
		}

		static bool RequestInput(out string input, ref bool timer)
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

					#region Commands
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

						if (line.StartsWith("#timer"))
						{
							var param = line.Substring("#timer".Length).Trim().ToLowerInvariant();
							if (param == "on")
							{
								timer = true;
								printHint("Timer enabled.");
								continue;
							}
							if (param == "off")
							{
								timer = false;
								printHint("Timer disabled.");
								continue;
							}
						}

						if (line.StartsWith("#load"))
						{
							var param = line.Substring("#load".Length).Trim().ToLowerInvariant();
							try
							{
								using (var fs = new FileStream(param, FileMode.Open, FileAccess.Read))
								using (var sr = new StreamReader(fs))
								{
									input = sr.ReadToEnd();
									return true;
								}
							}
							catch
							{
								printHint(string.Format("File '{0}' could not be loaded!", param));
								continue;
							}
						}

						if (line == "#oops")
						{
							if (lines.Count > 0)
								lines.RemoveAt(lines.Count - 1);
							continue;
						}

						printHelp();
						continue;
					}

					#endregion
				}

				prefix = getIdent(line);
				lines.Add(line.TrimEnd());
			}
		}

		static string buildString(ICollection<string> lines)
		{
			var sb = new StringBuilder(lines.Count);
			foreach (var curr in lines)
				sb.AppendLine(curr);
			return sb.ToString();
		}

		static void printPreamble()
		{
			using (new OutputColor(ConsoleColor.DarkGray))
			{
				Console.WriteLine("=====================");
				Console.WriteLine("  LENS Console Host");
				Console.WriteLine("=====================");
				Console.WriteLine("(type #help for help)");
				Console.WriteLine();
			}
		}

		static void printError(string msg, string details, ConsoleColor clr = ConsoleColor.Red)
		{
			using (new OutputColor(clr))
			{
				Console.WriteLine(msg);
				Console.WriteLine();
				Console.WriteLine(details);
				Console.WriteLine();
				Console.ResetColor();
			}
		}

		static void printHint(string hint)
		{
			using (new OutputColor(ConsoleColor.DarkGray))
			{
				Console.WriteLine();
				Console.WriteLine(hint);
				Console.WriteLine();
			}
		}

		static void printHelp()
		{
			using (new OutputColor(ConsoleColor.DarkGray))
			{
				Console.WriteLine();
				Console.WriteLine("To enter a script, just type it line by line.");
				Console.WriteLine("Finish the line with # to execute the script.");
				Console.WriteLine();
				Console.WriteLine("Available interpreter commands:");
				Console.WriteLine();
				Console.WriteLine("  #exit - close the interpreter");
				Console.WriteLine("  #run  - execute the script and print the output");
				Console.WriteLine("  #oops - cancel last line");
				Console.WriteLine("  #clr  - clear the console");
				Console.WriteLine();
				Console.WriteLine("  #timer (on|off)  - enable/disable time measurement");
				Console.WriteLine("  #load <filename> - load file and execute its contents");
				Console.WriteLine();
			}
		}

		static double measureTime(Action act)
		{
			var tStart = DateTime.Now;
			act();
			var tEnd = DateTime.Now;
			return (tEnd - tStart).TotalMilliseconds;
		}

		static void printInfo(bool printTime, double compileTime, double runTime)
		{
			if (!printTime)
				return;

			using (new OutputColor(ConsoleColor.DarkGray))
			{
				Console.WriteLine("Compilation: {0} msec.", compileTime);
				Console.WriteLine("Execution: {0} msec.", runTime);
				Console.WriteLine();
			}
		}

		static void printObject(dynamic obj)
		{
			Console.WriteLine();
			Console.WriteLine(getStringRepresentation(obj));
			if ((object) obj != null)
				using(new OutputColor(ConsoleColor.DarkGray))
					Console.WriteLine("({0})", obj.GetType());

			Console.WriteLine();
		}

		static string getStringRepresentation(dynamic obj)
		{
			if ((object)obj == null)
				return "(null)";

			if (obj is bool)
				return obj ? "true" : "false";

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

			return obj is double || obj is float
				? obj.ToString(CultureInfo.InvariantCulture)
				: obj.ToString();
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

		private static readonly Regex[] LineFeeds = new[]
		{
			new Regex(@"^(type|record)\s*[_a-z][_a-z0-9]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"^(if|while)\s*\(.+\)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"^(try|else)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"^catch(\([_a-][_a-z0-9]*(\s+[_a-][_a-z0-9]*)?\))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
		};

		static bool shouldIdent(string line)
		{
			var trim = line.Trim();
			return trim.EndsWith("->") || LineFeeds.Any(curr => curr.IsMatch(trim));
		}
	}
}
