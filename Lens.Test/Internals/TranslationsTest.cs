using System.IO;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Lens.Test.Internals
{
	[TestFixture]
	class TranslationsTest
	{
		private const string TranslationsFolder = @"..\..\..\Lens\Translations\";

		[Test]
		[TestCaseSource("TranslationComponents")]
		public void LocaleContentsIdentical(string component, string localeFrom, string localeTo)
		{
			var query = from locale in new[] { localeFrom, localeTo }
						let path = string.Format("{0}{1}{2}.resx", TranslationsFolder, component, locale == null ? null : "." + locale)
						let doc = XDocument.Load(path)
						let dataEntries = doc.Element("root").Elements("data")
						select dataEntries.Select(x => (string) x.Attribute("name")).ToArray();

			var identifierLists = query.ToArray();
			var unmatched = identifierLists[0].Except(identifierLists[1]).ToArray();

			if (unmatched.Any())
			{
				Assert.Fail(
					"The following keys exist in '{0}' translation but not in '{1}':\n{2}",
					localeFrom ?? "en",
					localeTo ?? "en",
					string.Join(", ", unmatched)
				);
			}
		}

		public static IEnumerable<string[]> TranslationComponents
		{
			get
			{
				foreach (var component in new[] { "CompilerMessages", "ParserMessages", "LexerMessages" })
				{
					yield return new[] { component, null, "ru" };
					yield return new[] { component, "ru", null };
				}
			}
		}
	}
}
