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
    internal class Program
	{
	    private static void Main()
		{
			PrintPreamble();

			var timer = false;
			while (RequestInput(out var source, ref timer))
			{
				Console.WriteLine();
				try
				{
					var lc = new LensCompiler(new LensCompilerOptions { AllowSave = true, MeasureTime = timer });
					var res = lc.Run(source);
					PrintObject(res);

					if (timer)
						PrintMeasurements(lc.Measurements);
				}
				catch (LensCompilerException ex)
				{
					PrintError(source, ex);
				}
				catch (Exception ex)
				{
					PrintException("An unexpected error has occured!", ex.Message + Environment.NewLine + ex.StackTrace);
				}
			}
		}

	    private static bool RequestInput(out string input, ref bool timer)
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
						input = BuildString(lines);
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
							input = BuildString(lines);
							return true;
						}

						if (line == "#clr")
						{
							lines = new List<string>();
							Console.Clear();
							PrintPreamble();
							continue;
						}

						if (line.StartsWith("#timer"))
						{
							var param = line.Substring("#timer".Length).Trim().ToLowerInvariant();
							if (param == "on")
							{
								timer = true;
								PrintHint("Timer enabled.");
								continue;
							}
							if (param == "off")
							{
								timer = false;
								PrintHint("Timer disabled.");
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
								PrintHint(string.Format("File '{0}' could not be loaded!", param));
								continue;
							}
						}

						if (line == "#oops")
						{
							if (lines.Count > 0)
								lines.RemoveAt(lines.Count - 1);
							continue;
						}

						PrintHelp();
						continue;
					}

					#endregion
				}

				prefix = GetIdent(line);
				lines.Add(line.TrimEnd());
			}
		}

	    private static string BuildString(ICollection<string> lines)
		{
			var sb = new StringBuilder(lines.Count);

			foreach (var curr in lines)
				sb.AppendLine(curr);

			return sb.ToString();
		}

	    private static void PrintPreamble()
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

	    private static void PrintException(string msg, string details)
		{
			using (new OutputColor(ConsoleColor.Yellow))
			{
				Console.WriteLine(msg);
				Console.WriteLine();
				Console.WriteLine(details);
				Console.WriteLine();
			}
		}

	    private static void PrintError(string src, LensCompilerException ex)
		{
			using (new OutputColor(ConsoleColor.Red))
			{
				Console.WriteLine("Error {0}", ex.Message);
				Console.WriteLine();
			}

			if (ex.StartLocation == null)
				return;

			var loc = ex.StartLocation.Value;
			var line = src.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[loc.Line - 1].TrimEnd();
			var len = ex.EndLocation != null && ex.EndLocation.Value.Line == loc.Line
				? ex.EndLocation.Value.Offset - loc.Offset
				: line.Length - loc.Offset + 1;

			using (new OutputColor(ConsoleColor.DarkGray))
				Console.Write("> {0}", line.Substring(0, loc.Offset - 1));

			using (new OutputColor(ConsoleColor.White, ConsoleColor.Red))
				Console.Write("{0}", line.Substring(loc.Offset - 1, len));

			if(len < line.Length - 1)
				using (new OutputColor(ConsoleColor.DarkGray))
					Console.Write("{0}", line.Substring(loc.Offset + len - 1));

			Console.WriteLine();
			Console.WriteLine();
		}

	    private static void PrintHint(string hint)
		{
			using (new OutputColor(ConsoleColor.DarkGray))
			{
				Console.WriteLine();
				Console.WriteLine(hint);
				Console.WriteLine();
			}
		}

	    private static void PrintHelp()
		{
			using (new OutputColor(ConsoleColor.DarkGray))
			{
				Console.WriteLine();
				Console.WriteLine("====================================");
				Console.WriteLine("=        LENS Compiler v4.0        =");
				Console.WriteLine("= https://github.com/impworks/lens =");
				Console.WriteLine("====================================");
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

	    private static void PrintObject(dynamic obj)
		{
			Console.WriteLine();
			Console.WriteLine(GetStringRepresentation(obj));

			if ((object) obj != null)
				using(new OutputColor(ConsoleColor.DarkGray))
					Console.WriteLine("({0})", obj.GetType());

			Console.WriteLine();
		}

	    private static void PrintMeasurements(Dictionary<string, TimeSpan> measures)
		{
			using (new OutputColor(ConsoleColor.DarkGray))
			{
				foreach(var curr in measures)
					Console.WriteLine("{0}: {1:0,00} ms.", curr.Key, curr.Value.TotalMilliseconds);

				Console.WriteLine();
			}
		}

	    private static string GetStringRepresentation(dynamic obj)
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
				{
					list.Add(
						string.Format(
							"{0} => {1}",
							GetStringRepresentation(currKey),
							GetStringRepresentation(obj[currKey])
						)
					);
				}

				return string.Format("{{ {0} }}", string.Join("; ", list));
			}

			if (obj is IEnumerable)
			{
				var list = new List<string>();

				foreach(var curr in obj)
					list.Add(GetStringRepresentation(curr));

				return string.Format("[ {0} ]", string.Join("; ", list));
			}

			return obj is double || obj is float
				? obj.ToString(CultureInfo.InvariantCulture)
				: obj.ToString();
		}

	    private static int GetIdent(string line)
		{
			var idx = 0;

			while (idx < line.Length && line[idx] == ' ')
				idx++;

			if (ShouldIdent(line))
				idx += 4;

			return idx;
		}

		private static readonly Regex[] LineFeeds =
		{
			new Regex(@"^(type|record)\s+[_a-z][_a-z0-9]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"\bif\b.+\bthen$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(while|for|using)\b.+\bdo$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"^(try|finally|else)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"new\s*(\(|\[\[?|\{)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"^catch\s+(\([_a-][_a-z0-9]*(\s+[_a-][_a-z0-9]*)?\))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
		};

	    private static bool ShouldIdent(string line)
		{
			var trim = line.Trim();
			return trim.EndsWith("->") || LineFeeds.Any(curr => curr.IsMatch(trim));
		}
	}
}
