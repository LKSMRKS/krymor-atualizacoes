using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Rastro da arma ("cortando o vento", pedido do usuário): uma fita entre o meio e a ponta da lâmina, desenhada pelos
/// últimos ~0,16 s de posições. Mais clara e larga na ponta, some pelo tempo e pela borda. Mundo (TopLevel): o rastro
/// fica no ar onde a lâmina passou, não acompanha o corpo.
/// </summary>
public sealed partial class WeaponTrail : MeshInstance3D
{
    private readonly struct Sample
    {
        public readonly Vector3 A, B;
        public readonly double T;
        public Sample(Vector3 a, Vector3 b, double t) { A = a; B = b; T = t; }
    }

    private const double Keep = 0.16;
    private readonly List<Sample> _samples = new();
    private readonly ImmediateMesh _mesh = new();
    private readonly Color _color;

    public WeaponTrail(Color color)
    {
        _color = color;
        TopLevel = true;
        Mesh = _mesh;
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = false,
        };
    }

    private static double Now => Time.GetTicksMsec() / 1000.0;

    public void Push(Vector3 a, Vector3 b)
    {
        // entre dois quadros a lâmina anda muito: preenche com pontos intermediários para a fita ficar curva, não quebrada
        if (_samples.Count > 0)
        {
            var last = _samples[^1];
            var gap = last.B.DistanceTo(b);
            var n = Mathf.Clamp((int)(gap / 0.12f), 0, 6);
            for (var i = 1; i <= n; i++)
            {
                var k = i / (float)(n + 1);
                _samples.Add(new Sample(last.A.Lerp(a, k), last.B.Lerp(b, k), Mathf.Lerp((float)last.T, (float)Now, k)));
            }
        }
        _samples.Add(new Sample(a, b, Now));
    }

    public void Stop() { /* só para de receber pontos; o que já existe some sozinho */ }

    public override void _Process(double delta)
    {
        var now = Now;
        _samples.RemoveAll(s => now - s.T > Keep);
        _mesh.ClearSurfaces();
        if (_samples.Count < 2) return;
        _mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (var i = 0; i < _samples.Count; i++)
        {
            var s = _samples[i];
            var life = 1f - (float)((now - s.T) / Keep);       // 1 = acabou de passar
            var fresh = life * life;
            _mesh.SurfaceSetColor(_color with { A = 0f });
            _mesh.SurfaceAddVertex(s.A);
            _mesh.SurfaceSetColor(_color with { A = 0.8f * fresh });
            _mesh.SurfaceAddVertex(s.B);
        }
        _mesh.SurfaceEnd();
    }
}
