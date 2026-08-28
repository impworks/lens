using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Lens.Playground
{
    /// <summary>
    /// The scripts the samples menu offers.
    ///
    /// They are embedded rather than fetched, so that the menu works the moment the page does and
    /// keeps working in a container with no outbound network. The file name carries the order and
    /// the title: "01-hello-world.lns" becomes "Hello world", first in the list.
    /// </summary>
    internal static class SampleLibrary
    {
        #region Fields

        private static List<SampleDto> _samples;

        #endregion

        #region Methods

        /// <summary>
        /// Every sample, in the order their names put them.
        /// </summary>
        public static List<SampleDto> All()
        {
            return _samples ?? (_samples = Read());
        }

        /// <summary>
        /// The source of one sample, or null when there is no such sample.
        /// </summary>
        public static string SourceOf(string name)
        {
            return All().FirstOrDefault(x => x.Name == name)?.Source;
        }

        #endregion

        #region Helpers

        private static List<SampleDto> Read()
        {
            var assembly = typeof(SampleLibrary).Assembly;

            return assembly.GetManifestResourceNames()
                           .Where(x => x.EndsWith(".lns", StringComparison.OrdinalIgnoreCase))
                           .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                           .Select(x => new SampleDto
                           {
                               Name = NameOf(x),
                               Title = TitleOf(x),
                               Source = Contents(assembly, x)
                           })
                           .ToList();
        }

        /// <summary>
        /// The file's own name, without the folder it was embedded from or its extension.
        /// </summary>
        private static string NameOf(string resource)
        {
            var parts = resource.Split('.');

            // the last part is the extension, the one before it is the name
            return parts.Length >= 2 ? parts[parts.Length - 2] : resource;
        }

        /// <summary>
        /// The name as it is shown: the ordering prefix dropped, the hyphens turned back into
        /// spaces, and the first letter capitalised.
        /// </summary>
        private static string TitleOf(string resource)
        {
            var name = NameOf(resource);
            var dash = name.IndexOf('-');

            if (dash >= 0 && name.Substring(0, dash).All(char.IsDigit))
                name = name.Substring(dash + 1);

            name = name.Replace('-', ' ');

            return name.Length == 0
                ? name
                : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static string Contents(Assembly assembly, string resource)
        {
            using (var stream = assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                    return string.Empty;

                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }

        #endregion
    }
}
