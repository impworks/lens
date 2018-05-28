using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    [TestFixture]
    internal class TranslationsTest
    {
        private string TranslationsFolder
        {
            get
            {
                var currFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(currFolder, @"..\..\..\Lens\Translations\");
            }
        }

        [Test]
        [TestCaseSource(nameof(TranslationComponents))]
        public void LocaleContentsIdentical(string component, string localeFrom, string localeTo)
        {
            var query = from locale in new[] {localeFrom, localeTo}
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

        [Test]
        [TestCaseSource(nameof(TranslationComponents))]
        public void MessagePrefixCorrectness(string component, string localeFrom, string localeTo)
        {
            var query = from locale in new[] {localeFrom, localeTo}
                        let path = string.Format("{0}{1}{2}.resx", TranslationsFolder, component, locale == null ? null : "." + locale)
                        let doc = XDocument.Load(path)
                        let dataEntries = doc.Element("root").Elements("data")
                        select dataEntries.ToDictionary(
                            x => (string) x.Attribute("name"),
                            x => x.Element("value").Value
                        );

            var lookups = query.ToArray();
            var unmatched = lookups[0].Keys.Where(k => lookups[0][k].Substring(0, 6) != lookups[1][k].Substring(0, 6)).ToArray();
            if (unmatched.Any())
            {
                Assert.Fail(
                    "The following have different IDs in translations '{0}' and '{1}':\n{2}",
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
                foreach (var component in new[] {"CompilerMessages", "ParserMessages", "LexerMessages"})
                {
                    yield return new[] {component, null, "ru"};
                    yield return new[] {component, "ru", null};
                }
            }
        }
    }
}