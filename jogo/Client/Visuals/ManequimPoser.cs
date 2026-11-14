using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>Modificador do esqueleto: aplica as poses de ação do manequim depois da animação, a cada quadro.</summary>
public sealed partial class ManequimPoser : SkeletonModifier3D
{
    public MannequinVisual? Manequim;

    public override void _ProcessModificationWithDelta(double delta) => Manequim?.ModifyPose();
}
