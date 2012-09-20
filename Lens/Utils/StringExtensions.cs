using System;
using System.Linq;

namespace Lens.Utils
{
  public static class StringExtensions
  {
    /// <summary>
    /// Return a "safe substring": no exceptions when out of bounds
    /// </summary>
    /// <param name="str">Source string</param>
    /// <param name="offset">Offset</param>
    public static string SafeSubstring(this string str, int offset)
    {
	    return offset >= str.Length ? "" : str.Substring(offset);
    }

	  /// <summary>
    /// Return a "safe substring": no exceptions when out of bounds
    /// </summary>
    /// <param name="str">Source string</param>
    /// <param name="offset">Offset</param>
    /// <param name="length">Substring length</param>
    public static string SafeSubstring(this string str, int offset, int length)
    {
	    return offset >= str.Length ? "" : str.Substring(offset, Math.Min(str.Length - offset, length));
    }

	  /// <summary>
    /// Check if the string is in the given array
    /// </summary>
    /// <param name="str">Needle</param>
    /// <param name="arr">Haystack</param>
    public static bool IsAnyOf(this string str, params string[] arr)
    {
	    return arr.Any(curr => curr == str);
    }

	  /// <summary>
    /// Concatenate an array of strings
    /// </summary>
    /// <param name="arr">Array of strings to be concatenated</param>
    /// <param name="glue">Delimiter</param>
    static public string Join(this string[] arr, string glue = "")
    {
	    return string.Join(glue, arr);
    }
  }
}
