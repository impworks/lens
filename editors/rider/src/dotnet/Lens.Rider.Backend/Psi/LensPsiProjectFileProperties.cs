using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Impl;

namespace Lens.Rider.Backend.Psi;

public class LensPsiProjectFileProperties(IProjectFile projectFile, IPsiSourceFile sourceFile)
    : DefaultPsiProjectFileProperties(projectFile, sourceFile)
{
    public override bool ShouldBuildPsi => true;

    public override bool IsICacheParticipant => false;

    public override bool ProvidesCodeModel => false;
}
