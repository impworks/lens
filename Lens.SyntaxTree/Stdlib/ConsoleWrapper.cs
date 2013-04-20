using System;

namespace Lens.SyntaxTree.Stdlib
{
	public static class ConsoleWrapper
	{
		#region Print

		public static void Print(object obj)
		{
			Console.Write(obj);
		}

		public static void Print1(string format, object obj1)
		{
			Console.Write(format, obj1);
		}

		public static void Print2(string format, object obj1, object obj2)
		{
			Console.Write(format, obj1, obj2);
		}

		public static void Print3(string format, object obj1, object obj2, object obj3)
		{
			Console.Write(format, obj1, obj2, obj3);
		}

		public static void Print4(string format, object obj1, object obj2, object obj3, object obj4)
		{
			Console.Write(format, obj1, obj2, obj3, obj4);
		}

		public static void Print5(string format, object obj1, object obj2, object obj3, object obj4, object obj5)
		{
			Console.Write(format, obj1, obj2, obj3, obj4, obj5);
		}

		public static void Print6(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6)
		{
			Console.Write(format, obj1, obj2, obj3, obj4, obj5, obj6);
		}

		public static void Print7(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7)
		{
			Console.Write(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7);
		}

		public static void Print8(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7, object obj8)
		{
			Console.Write(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8);
		}

		public static void Print9(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7, object obj8, object obj9)
		{
			Console.Write(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8, obj9);
		}

		public static void Print10(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7, object obj8, object obj9, object obj10)
		{
			Console.Write(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8, obj9, obj10);
		}

		public static void PrintLine(object obj)
		{
			Console.WriteLine(obj);
		}

		public static void PrintLine1(string format, object obj1)
		{
			Console.WriteLine(format, obj1);
		}

		public static void PrintLine2(string format, object obj1, object obj2)
		{
			Console.WriteLine(format, obj1, obj2);
		}

		public static void PrintLine3(string format, object obj1, object obj2, object obj3)
		{
			Console.WriteLine(format, obj1, obj2, obj3);
		}

		public static void PrintLine4(string format, object obj1, object obj2, object obj3, object obj4)
		{
			Console.WriteLine(format, obj1, obj2, obj3, obj4);
		}

		public static void PrintLine5(string format, object obj1, object obj2, object obj3, object obj4, object obj5)
		{
			Console.WriteLine(format, obj1, obj2, obj3, obj4, obj5);
		}

		public static void PrintLine6(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6)
		{
			Console.WriteLine(format, obj1, obj2, obj3, obj4, obj5, obj6);
		}

		public static void PrintLine7(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7)
		{
			Console.WriteLine(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7);
		}

		public static void PrintLine8(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7, object obj8)
		{
			Console.WriteLine(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8);
		}

		public static void PrintLine9(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7, object obj8, object obj9)
		{
			Console.WriteLine(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8, obj9);
		}

		public static void PrintLine10(string format, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6, object obj7, object obj8, object obj9, object obj10)
		{
			Console.WriteLine(format, obj1, obj2, obj3, obj4, obj5, obj6, obj7, obj8, obj9, obj10);
		}

		#endregion

		#region Read

		public static int Read()
		{
			return Console.Read();
		}

		public static ConsoleKeyInfo ReadKey()
		{
			return Console.ReadKey();
		}

		public static ConsoleKeyInfo ReadKey(bool flag)
		{
			return Console.ReadKey(flag);
		}

		public static string ReadLine()
		{
			return Console.ReadLine();
		}

		#endregion
	}
}
