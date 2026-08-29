using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;

namespace Lens.Rider.Backend.Psi.Tree;

[Language(typeof(LensLanguage))]
public class LensElementType : INodeTypesInitializer
{
    public static readonly CompositeNodeType LENS_FILE = new LensFileNodeType("LENS_FILE", 1);

    private sealed class LensFileNodeType(string s, int index) : CompositeNodeType(s, index, typeof(LensFile))
    {
        public override CompositeElement Create() => new LensFile();
    }
}
