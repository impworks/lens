using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.Stdlib
{
	/// <summary>
	/// Standard library randomizer methods.
	/// </summary>
	public static class Randomizer
	{
		#region Fields

		/// <summary>
		/// Random seed.
		/// </summary>
		public static readonly Random _random = new Random();

		#endregion

		#region Methods

		/// <summary>
		/// Gets a random floating point value between 0.0 and 1.0.
		/// </summary>
		/// <returns></returns>
		public static double Random()
		{
			return _random.NextDouble();
		}

		/// <summary>
		/// Gets a random integer value between 0 and MAX.
		/// </summary>
		public static int Random(int max)
		{
			return _random.Next(max);
		}

		/// <summary>
		/// Gets a random integer value between MIN and MAX.
		/// </summary>
		public static int Random(int min, int max)
		{
			return _random.Next(min, max);
		}

		/// <summary>
		/// Gets a random element from the list.
		/// </summary>
		public static T Random<T>(IList<T> src)
		{
			var max = src.Count - 1;
			return src[Random(max)];
		}

		/// <summary>
		/// Gets a random element from the list using a weighter function.
		/// </summary>
		public static T Random<T>(IList<T> src, Func<T, double> weighter)
		{
			var rnd = _random.NextDouble();
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

		#endregion
	}
}
