using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>Atalhos para montar bonecos e objetos com formas básicas (sem modelos externos).</summary>
public static class VisualKit
{
    public static StandardMaterial3D Mat(Color color, float roughness = 0.8f, float metallic = 0f)
        => new() { AlbedoColor = color, Roughness = roughness, Metallic = metallic };

    /// <summary>
    /// Material de bicho com textura e relevo (regra: todo objeto com textura): "quitina" (casco), "pele", "escama".
    /// A cor do material tinge a textura cinza; triplanar, então serve em qualquer forma primitiva.
    /// </summary>
    public static StandardMaterial3D Hide(Color color, string kind, float roughness = 0.75f, float scale = 2.2f, float metallic = 0f)
    {
        var m = Mat(color, roughness, metallic);
        var file = "gen_" + kind + ".png";
        if (CinzasDaMare.Client.VisualAssets.Load(file) is { } tex)
        {
            m.AlbedoTexture = tex;
            m.Uv1Triplanar = true;
            m.Uv1Scale = Vector3.One * scale;
            CinzasDaMare.Client.VisualAssets.AddRelief(m, file, 0.8f);
        }
        // casco molhado brilha um pouco (caranguejos/larvas da costa)
        if (kind == "quitina") { m.ClearcoatEnabled = true; m.Clearcoat = 0.35f; m.ClearcoatRoughness = 0.35f; }
        return m;
    }

    public static StandardMaterial3D Glow(Color color, float energy = 2f)
        => new()
        {
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = energy,
            Roughness = 0.6f,
        };

    public static StandardMaterial3D Unshaded(Color color, bool additive = false)
        => new()
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = false,
        };

    public static MeshInstance3D Part(Node3D parent, Mesh mesh, Material material, Vector3 position,
        Vector3? rotationDegrees = null, Vector3? scale = null)
    {
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = position };
        if (rotationDegrees is { } r) node.RotationDegrees = r;
        if (scale is { } s) node.Scale = s;
        parent.AddChild(node);
        return node;
    }

    public static Node3D Joint(Node3D parent, Vector3 position, string name = "")
    {
        var joint = new Node3D { Position = position };
        if (name.Length > 0) joint.Name = name;
        parent.AddChild(joint);
        return joint;
    }

    public static CapsuleMesh Capsule(float radius, float height)
        => new() { Radius = radius, Height = Mathf.Max(height, radius * 2f + 0.001f), RadialSegments = 12, Rings = 4 };

    public static SphereMesh Sphere(float radius)
        => new() { Radius = radius, Height = radius * 2f, RadialSegments = 14, Rings = 8 };

    public static BoxMesh Box(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };

    public static CylinderMesh Cylinder(float top, float bottom, float height, int segments = 12)
        => new() { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = segments, Rings = 1 };

    /// <summary>Malha plana no chão (leque, círculo ou faixa) para marcas de ataque e mira.</summary>
    public static ArrayMesh Sector(float radius, float arcDegrees, int segments = 28, float inner = 0f)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var half = Mathf.DegToRad(arcDegrees) / 2f;
        for (var i = 0; i < segments; i++)
        {
            var a0 = -half + 2f * half * i / segments;
            var a1 = -half + 2f * half * (i + 1) / segments;
            var o0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
            var o1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
            var i0 = o0 * inner;
            var i1 = o1 * inner;
            st.SetNormal(Vector3.Up); st.AddVertex(i0);
            st.SetNormal(Vector3.Up); st.AddVertex(o0 * radius);
            st.SetNormal(Vector3.Up); st.AddVertex(o1 * radius);
            if (inner > 0f)
            {
                st.SetNormal(Vector3.Up); st.AddVertex(i0);
                st.SetNormal(Vector3.Up); st.AddVertex(o1 * radius);
                st.SetNormal(Vector3.Up); st.AddVertex(i1);
            }
        }
        return st.Commit();
    }

    public static ArrayMesh Ring(float radius, float width, int segments = 40) => Sector(radius, 360f, segments, radius - width);

    /// <summary>Guarda os materiais de um boneco para piscar (golpe) e brilhar (preparação) sem afetar outros.</summary>
    public sealed class FlashGroup
    {
        private readonly List<(StandardMaterial3D Material, Color Base, bool HadEmission, Color BaseEmission, float BaseEnergy)> _items = new();

        public StandardMaterial3D Add(StandardMaterial3D m)
        {
            _items.Add((m, m.AlbedoColor, m.EmissionEnabled, m.Emission, m.EmissionEnergyMultiplier));
            return m;
        }

        /// <summary>flash 0..1 = branco do golpe; glow 0..1 = vermelho de preparação; fade 0..1 = some na morte.</summary>
        public void Apply(float flash, float glow, Color glowColor, float fade = 0f)
        {
            foreach (var (m, baseColor, had, baseEmission, baseEnergy) in _items)
            {
                var c = baseColor.Lerp(Colors.White, flash * 0.3f);
                if (fade > 0f)
                {
                    c = c.Darkened(fade * 0.6f);
                    c.A = 1f - fade;
                    m.Transparency = fade > 0.01f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled;
                }
                m.AlbedoColor = c;
                // preparação: brilho moderado (a pose e o brilho juntos avisam; o corpo não vira uma mancha laranja)
                var strength = Mathf.Max(flash * 0.45f, glow * 0.22f);
                if (strength > 0.01f)
                {
                    m.EmissionEnabled = true;
                    m.Emission = flash * 0.45f >= glow * 0.22f ? new Color(1f, 0.92f, 0.8f) : glowColor;
                    m.EmissionEnergyMultiplier = had ? Mathf.Max(baseEnergy, strength * 1.6f) : strength * 1.6f;
                }
                else
                {
                    m.EmissionEnabled = had;
                    m.Emission = baseEmission;
                    m.EmissionEnergyMultiplier = baseEnergy;
                }
            }
        }
    }

    public static float Approach(float current, float target, float rate, float delta)
        => Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * delta));

    public static Vector3 Approach(Vector3 current, Vector3 target, float rate, float delta)
        => current.Lerp(target, 1f - Mathf.Exp(-rate * delta));

    /// <summary>Curva de golpe: sobe rápido e volta devagar (0..1 → 0..1..0).</summary>
    public static float Snap(float t, float peak = 0.25f)
        => t < peak ? Mathf.SmoothStep(0f, 1f, t / peak) : 1f - Mathf.SmoothStep(0f, 1f, (t - peak) / (1f - peak));
}
