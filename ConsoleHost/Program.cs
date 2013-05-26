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
using Lens.SyntaxTree;

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
					var lc = new LensCompiler();

					Func<object> fx = null;
					object res = null;

					var compileTime = measureTime(() => fx = lc.Compile(source));
					var runTime = measureTime(() => res = fx());

					printObject(res);

					printInfo(timer, compileTime, runTime);
				}
				catch (LensCompilerException ex)
				{
					printError("Ошибка компиляции!", ex.FullMessage);
				}
				catch (Exception ex)
				{
					printError("Неизвестная ошибка!", ex.Message + Environment.NewLine + ex.StackTrace, ConsoleColor.Yellow);
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
								printHint("Таймер включен.");
								continue;
							}
							if (param == "off")
							{
								timer = false;
								printHint("Таймер выключен.");
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
								printHint(string.Format("Файл '{0}' не может быть загружен!", param));
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
				Console.WriteLine("==================================");
				Console.WriteLine("          Консоль LENS            ");
				Console.WriteLine("==================================");
				Console.WriteLine("(команда #help отображает справку)");
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
				Console.WriteLine("Чтобы выполнить программу, введите ее построчно");
				Console.WriteLine("и поставьте символ # в конце последней строки.");
				Console.WriteLine();
				Console.WriteLine("Доступные команды консоли:");
				Console.WriteLine();
				Console.WriteLine("  #exit - закрыть консоль");
				Console.WriteLine("  #run  - выполнить скрипт и вывести результат");
				Console.WriteLine("  #oops - отменить последнюю строчку");
				Console.WriteLine("  #clr  - очистить консоль");
				Console.WriteLine();
				Console.WriteLine("  #timer (on|off)  - включить/выключить замер времени");
				Console.WriteLine("  #load <file>     - загрузить и выполнить программу из файла");
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
				Console.WriteLine("Компиляция: {0} мсек.", compileTime);
				Console.WriteLine("Выполнение: {0} мсек.", runTime);
				Console.WriteLine();
			}
		}

		static void printObject(dynamic obj)
		{
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
			new Regex(@"^type [_a-z][_a-z0-9]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
			new Regex(@"^record [_a-z][_a-z0-9]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
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
