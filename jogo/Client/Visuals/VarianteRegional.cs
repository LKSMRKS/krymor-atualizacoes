using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 19/11 (dono: "variedade de mobs igual à Costa do Cal"): o mesmo modelo aparece em várias regiões (8 bichos usam o
/// fantasma, 8 a harpia...). Aqui cada um ganha a cara da região onde vive: a cor puxa para o tema (gelo azulado, brasa,
/// lodo esverdeado, poeira do cânion...), um pouco de diferença de tom entre espécies, e uma "aura" leve de partículas do
/// tema (neve caindo do corpo, brasas, gotas de lodo, poeira, faíscas de luz). Costa do Cal, Porto e Vale ficam como estão.
/// </summary>
public static class VarianteRegional
{
    private static readonly HashSet<string> Intocados = new() { "costa_do_cal", "porto_das_cinzas", "vale_das_cinzas" };

    private record Tema(Color Tom, float Forca, Color Particula, Vector3 Gravidade, float Vel, bool Brilha);

    private static readonly Dictionary<string, Tema> Temas = new()
    {
        ["neve"] = new(new Color(0.78f, 0.9f, 1.18f), 0.55f, new Color(0.92f, 0.97f, 1f), new Vector3(0, -0.6f, 0), 0.25f, false),
        ["lava"] = new(new Color(1.2f, 0.8f, 0.62f), 0.55f, new Color(1f, 0.55f, 0.15f), new Vector3(0, 0.9f, 0), 0.4f, true),
        ["brejo"] = new(new Color(0.78f, 0.95f, 0.66f), 0.6f, new Color(0.35f, 0.45f, 0.18f), new Vector3(0, -2.5f, 0), 0.1f, false),
        ["fantasma"] = new(new Color(0.82f, 0.86f, 1.12f), 0.5f, new Color(0.6f, 0.9f, 1f), new Vector3(0, 0.5f, 0), 0.2f, true),
        ["canion"] = new(new Color(1.12f, 0.9f, 0.72f), 0.5f, new Color(0.85f, 0.6f, 0.4f), new Vector3(0.3f, -0.2f, 0), 0.5f, false),
        ["mata"] = new(new Color(0.84f, 1.06f, 0.8f), 0.45f, new Color(0.75f, 1f, 0.45f), new Vector3(0, 0.25f, 0), 0.25f, true),
        ["acropole"] = new(new Color(1.08f, 1.0f, 0.84f), 0.45f, new Color(1f, 0.85f, 0.45f), new Vector3(0, 0.3f, 0), 0.2f, true),
        ["ceu"] = new(new Color(1.08f, 1.08f, 1.15f), 0.45f, new Color(1f, 1f, 0.9f), new Vector3(0, 0.4f, 0), 0.2f, true),
        ["lua"] = new(new Color(0.8f, 0.84f, 1.12f), 0.55f, new Color(0.7f, 0.8f, 1f), new Vector3(0, 0.2f, 0), 0.15f, true),
        ["praia"] = new(new Color(0.85f, 1.0f, 1.08f), 0.35f, new Color(0.7f, 0.9f, 1f), new Vector3(0, -1.5f, 0), 0.1f, false),
        ["vale"] = new(new Color(1.02f, 0.98f, 0.92f), 0.3f, new Color(0.8f, 0.75f, 0.6f), new Vector3(0, -0.2f, 0), 0.3f, false),
    };

    public static void Aplicar(Node3D raiz, string definitionId, string mapaId, string tema, bool chefe)
    {
        if (Intocados.Contains(mapaId) || definitionId.StartsWith("monster_cal_", StringComparison.Ordinal) || definitionId.EndsWith("_dourado", StringComparison.Ordinal)) return;
        var t = Temas.GetValueOrDefault(mapaId == "reino_inferno" ? "lava" : mapaId == "fundo_triangulo" ? "praia" : tema);
        if (t is null) return;
        // cada espécie com o seu tom (o mesmo bicho sempre igual; espécies diferentes do mesmo modelo, diferentes)
        var h = (uint)definitionId.GetHashCode();
        var desvio = ((h & 0xff) / 255f - 0.5f) * 0.18f;
        var forca = Mathf.Min(1f, t.Forca * 1.35f * (0.8f + ((h >> 8) & 0xff) / 255f * 0.4f));
        var tom = new Color(1, 1, 1).Lerp(t.Tom, forca);
        tom = new Color(tom.R * (1 + desvio), tom.G * (1 - desvio * 0.5f), tom.B * (1 - desvio));
        Tingir(raiz, tom);
        // pequenas variações de corpo por espécie (dono: "pequenas variações, não precisa ir muito além"): um pouco mais alto
        // ou baixo, mais largo ou magro — no modelo de dentro (a escala da raiz é do servidor: tamanho pelo nível)
        if (!chefe)
        {
            var alt = 1f + (((h >> 16) & 0xff) / 255f - 0.5f) * 0.16f;     // ±8% de altura
            var larg = 1f + (((h >> 24) & 0xff) / 255f - 0.5f) * 0.2f;     // ±10% de largura
            foreach (var filho in raiz.GetChildren())
                if (filho is Node3D n3 && filho is not GpuParticles3D && filho is not CpuParticles3D && filho is not Light3D)
                    n3.Scale = new Vector3(n3.Scale.X * larg, n3.Scale.Y * alt, n3.Scale.Z * larg);
        }
        if (GraphicsQuality.Current == GraphicsQuality.Level.Baixo) return;
        // aura do tema: poucas partículas, sem luz (barato com 80 monstros no mapa)
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = t.Particula, VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        if (t.Brilha) { mat.EmissionEnabled = true; mat.Emission = t.Particula; mat.EmissionEnergyMultiplier = 1.6f; }
        var p = new CpuParticles3D
        {
            Name = "AuraRegional", Amount = chefe ? 22 : 8, Lifetime = 1.6f, Preprocess = 1.6f, Explosiveness = 0f,
            Mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3, Material = mat }, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.45f, 0.7f, 0.45f) * (chefe ? 2f : 1f), Position = new Vector3(0, chefe ? 1.6f : 0.9f, 0),
            Direction = Vector3.Up, Spread = 60f, InitialVelocityMin = t.Vel * 0.5f, InitialVelocityMax = t.Vel, Gravity = t.Gravidade,
            ScaleAmountMin = 0.6f, ScaleAmountMax = 1.3f,
            ColorRamp = new Gradient { Colors = new[] { t.Particula with { A = 0f }, t.Particula, t.Particula with { A = 0f } }, Offsets = new[] { 0f, 0.3f, 1f } },
        };
        raiz.AddChild(p);
    }

    private static void Tingir(Node3D raiz, Color c)
    {
        foreach (var n in raiz.FindChildren("*", nameof(MeshInstance3D), true, false))
            if (n is MeshInstance3D mi && mi.Mesh is not null)
                for (var i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                    if ((mi.GetSurfaceOverrideMaterial(i) ?? mi.Mesh.SurfaceGetMaterial(i)) is BaseMaterial3D sm)
                    {
                        var copia = (BaseMaterial3D)sm.Duplicate();
                        copia.AlbedoColor = new Color(Mathf.Clamp(sm.AlbedoColor.R * c.R, 0, 1.6f), Mathf.Clamp(sm.AlbedoColor.G * c.G, 0, 1.6f), Mathf.Clamp(sm.AlbedoColor.B * c.B, 0, 1.6f), sm.AlbedoColor.A);
                        mi.SetSurfaceOverrideMaterial(i, copia);
                    }
    }
}
