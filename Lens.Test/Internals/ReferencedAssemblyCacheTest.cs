using System.Linq;
using System.Text.RegularExpressions;
using Lens.Resolver;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    [TestFixture]
    public class ReferencedAssemblyCacheTest
    {
        [Test]
        public void DefaultAssembliesAreAvailable()
        {
            var cache = new ReferencedAssemblyCache();

            CollectionAssert.IsEmpty(
                cache.MissingDefaultAssemblies.ToArray(),
                "Some of the assemblies referenced by default could not be loaded."
            );
        }

        /// <summary>
        /// On .NET Core the BCL is split into many lazily loaded assemblies, so the ones backing the
        /// default namespaces must be pulled in explicitly rather than assumed to be loaded already.
        /// </summary>
        [Test]
        public void AssembliesBackingDefaultNamespacesAreReferenced()
        {
            var cache = new ReferencedAssemblyCache();
            var assemblies = cache.Assemblies.ToArray();

            CollectionAssert.Contains(assemblies, typeof(Enumerable).Assembly);
            CollectionAssert.Contains(assemblies, typeof(Regex).Assembly);
        }

        [Test]
        public void ImplementationAssembliesAreNotReferenced()
        {
            var cache = new ReferencedAssemblyCache();

            Assert.IsFalse(
                cache.Assemblies.Any(x => x.FullName.StartsWith("System.Private.")),
                "The runtime's private implementation assemblies must stay invisible to scripts."
            );
        }

        [Test]
        public void NoDefaultAssembliesWhenDisabled()
        {
            var cache = new ReferencedAssemblyCache(false);

            CollectionAssert.IsEmpty(cache.Assemblies.ToArray());
        }
    }
}
