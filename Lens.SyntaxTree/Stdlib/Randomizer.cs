using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.SyntaxTree.Stdlib
{
	public static class Randomizer
	{
		public static readonly Random m_Random = new Random();

		public static double Random()
		{
			return m_Random.NextDouble();
		}

		public static int Random(int max)
		{
			return m_Random.Next(max);
		}

		public static int Random(int min, int max)
		{
			return m_Random.Next(min, max);
		}

		public static T Random<T>(IList<T> src)
		{
			return src[Random(src.Count)-1];
		}

		public static T Random<T>(IList<T> src, Func<T, double> weighter)
		{
			var rnd = m_Random.NextDouble();
			var weight = src.Sum(weighter);
			if (weight < 0.000001)
				throw new ArgumentException("src");

			var delta = 1.0/weight;
			var prob = 0.0;
			foreach (var curr in src)
			{
				prob += weighter(curr) * delta;
				if (rnd <= prob)
					return curr;
			}

			throw new ArgumentException("src");
		}
	}
}
