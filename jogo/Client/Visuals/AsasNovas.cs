using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 14/11 noite (prancha "Coleção de Asas" do dono: 25 asas, de comum a lendária). Cada asa é uma de duas coisas:
///  * um dos 4 modelos de asa já feitos (morcego/dragão, membrana rúnica, penas, cristal) repintado por shader — cor
///    nova tirada do claro/escuro da textura, brilho, fogo pulsando, veias de raio, céu estrelado — e enfeites;
///  * ou uma asa montada em código (inseto, borboleta, folhas, vapor, ossos, raios).
/// Devolve as metades (pivô na dobradiça, lado +1/-1) para o MannequinVisual bater como as outras.
/// </summary>
public static class AsasNovas
{
    private sealed record Estilo(string? Modelo, Color Tinta, Color Tinta2, float Manter, Color Brilho, float Forca, int Fx, float Escala,
        string? Extra = null, Color? TintaEsq = null, Color? Particula = null);

    private static Color C(float r, float g, float b) => new(r, g, b);

    private const string Morcego = "asas/asa_guerreiro", Runica = "asas/asa_mago", Penas = "asas/asa_arqueiro", Cristal = "asas/asa_bruxa";

    private static readonly Dictionary<string, Estilo> Estilos = new()
    {
        // ---- comuns (0,75)
        ["asa_pano"] = new(Runica, C(0.72f, 0.58f, 0.4f), C(0.3f, 0.22f, 0.14f), 0f, C(0, 0, 0), 0, 0, 0.75f),
        ["asa_penas"] = new(Penas, C(0.6f, 0.4f, 0.22f), C(0.22f, 0.13f, 0.07f), 0f, C(0, 0, 0), 0, 0, 0.75f),
        ["asa_morcego"] = new(Morcego, C(0.75f, 0.12f, 0.1f), C(0.2f, 0.03f, 0.03f), 0f, C(0, 0, 0), 0, 0, 0.75f),
        ["asa_inseto"] = new(null, C(0.55f, 0.7f, 1f), C(0.3f, 0.4f, 0.9f), 0, C(0.5f, 0.7f, 1f), 0.4f, 0, 0.75f, "inseto"),
        ["asa_anjo"] = new(Penas, C(0.97f, 0.96f, 0.95f), C(0.62f, 0.62f, 0.68f), 0f, C(0, 0, 0), 0, 0, 0.75f),
        // ---- incomuns (0,85)
        ["asa_folhas"] = new(null, C(0.35f, 0.62f, 0.2f), C(0.2f, 0.42f, 0.12f), 0, C(0, 0, 0), 0, 0, 0.85f, "folhas", null, C(0.45f, 0.7f, 0.25f)),
        ["asa_cristal"] = new(Cristal, C(0.55f, 0.85f, 1f), C(0.15f, 0.35f, 0.75f), 0.15f, C(0.4f, 0.8f, 1f), 0.9f, 0, 0.85f),
        ["asa_vapor"] = new(null, C(0.8f, 0.82f, 0.9f), C(0.55f, 0.58f, 0.7f), 0, C(0, 0, 0), 0, 0, 0.85f, "vapor"),
        ["asa_borboleta"] = new(null, C(1f, 0.55f, 0.1f), C(0.25f, 0.1f, 0.35f), 0, C(1f, 0.6f, 0.2f), 0.3f, 0, 0.85f, "borboleta"),
        ["asa_ossos"] = new(null, C(0.9f, 0.85f, 0.72f), C(0.6f, 0.55f, 0.45f), 0, C(0, 0, 0), 0, 0, 0.85f, "ossos"),
        // ---- raras (0,95)
        ["asa_tempestade"] = new(null, C(0.4f, 0.65f, 1f), C(0.15f, 0.25f, 0.8f), 0, C(0.5f, 0.75f, 1f), 3f, 0, 0.95f, "raios", null, C(0.6f, 0.85f, 1f)),
        ["asa_gelo"] = new(Penas, C(0.65f, 0.88f, 1f), C(0.2f, 0.45f, 0.85f), 0f, C(0.5f, 0.85f, 1f), 0.8f, 3, 0.95f, null, null, C(0.85f, 0.95f, 1f)),
        ["asa_fogo"] = new(Penas, C(1f, 0.62f, 0.12f), C(0.75f, 0.12f, 0.03f), 0f, C(1f, 0.5f, 0.1f), 1.6f, 1, 0.95f, null, null, C(1f, 0.55f, 0.15f)),
        ["asa_sombrias"] = new(Penas, C(0.55f, 0.25f, 0.85f), C(0.08f, 0.03f, 0.15f), 0f, C(0.6f, 0.3f, 1f), 0.7f, 1, 0.95f, null, null, C(0.6f, 0.3f, 1f)),
        ["asa_celestiais"] = new(Penas, C(1f, 0.93f, 0.75f), C(0.85f, 0.62f, 0.25f), 0f, C(1f, 0.85f, 0.4f), 0.8f, 0, 0.95f, "aureola"),
        // ---- épicas (1,05)
        ["asa_draconicas"] = new(Morcego, C(0.95f, 0.3f, 0.08f), C(0.35f, 0.04f, 0.02f), 0.25f, C(1f, 0.45f, 0.1f), 0.5f, 1, 1.05f),
        ["asa_demoniacas"] = new(Runica, C(0.7f, 0.08f, 0.06f), C(0.05f, 0.02f, 0.02f), 0f, C(1f, 0.15f, 0.08f), 1.4f, 2, 1.05f, "sigilo"),
        ["asa_vazio"] = new(Morcego, C(0.45f, 0.25f, 0.95f), C(0.05f, 0.02f, 0.15f), 0f, C(0.7f, 0.5f, 1f), 1f, 4, 1.05f, "buraco_negro", null, C(0.7f, 0.5f, 1f)),
        ["asa_runicas"] = new(Runica, C(0.35f, 0.4f, 0.45f), C(0.06f, 0.08f, 0.1f), 0f, C(0.2f, 1f, 1f), 1.8f, 2, 1.05f),
        ["asa_arcanjo"] = new(Penas, C(1f, 0.96f, 0.88f), C(0.9f, 0.7f, 0.3f), 0f, C(1f, 0.85f, 0.45f), 1f, 0, 1.1f, "aureola_gema", null, C(1f, 0.9f, 0.6f)),
        // ---- lendárias (1,15)
        ["asa_dragao_anciao"] = new(Morcego, C(0.3f, 0.06f, 0.05f), C(0.04f, 0.02f, 0.02f), 0f, C(1f, 0.4f, 0.05f), 2.2f, 2, 1.18f, null, null, C(1f, 0.45f, 0.1f)),
        ["asa_fenrir"] = new(Penas, C(0.55f, 0.82f, 1f), C(0.12f, 0.3f, 0.7f), 0f, C(0.45f, 0.85f, 1f), 1.2f, 3, 1.15f, "lobos", null, C(0.8f, 0.95f, 1f)),
        ["asa_eclipse"] = new(Penas, C(1f, 0.88f, 0.6f), C(0.75f, 0.5f, 0.2f), 0f, C(1f, 0.75f, 0.3f), 1f, 0, 1.15f, "eclipse", C(0.45f, 0.2f, 0.75f)),
        ["asa_tempo"] = new(Penas, C(0.95f, 0.8f, 0.45f), C(0.15f, 0.3f, 0.6f), 0f, C(0.4f, 0.8f, 1f), 1f, 2, 1.15f, "relogio"),
        ["asa_criacao"] = new(Penas, C(0.45f, 0.95f, 1f), C(0.95f, 0.75f, 0.25f), 0f, C(0.6f, 1f, 1f), 1.6f, 4, 1.2f, "criacao", null, C(1f, 0.95f, 0.7f)),
    };

    public static bool Conhece(string? kind) => kind is not null && Estilos.ContainsKey(kind);

    private static Shader? _tinta;

    private const string TintaShader = @"
shader_type spatial;
render_mode cull_disabled;
uniform sampler2D tex : source_color;
uniform vec3 tinta = vec3(1.0);
uniform vec3 tinta2 = vec3(0.2);
uniform float manter = 0.0;
uniform vec3 brilho = vec3(0.0);
uniform float forca = 0.0;
uniform int fx = 0;
float h1(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float ns(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(h1(i), h1(i + vec2(1, 0)), f.x), mix(h1(i + vec2(0, 1)), h1(i + vec2(1, 1)), f.x), f.y); }
void fragment() {
    vec4 t = texture(tex, UV);
    float l = clamp(dot(t.rgb, vec3(0.3, 0.59, 0.11)) * 1.5, 0.0, 1.0);
    vec3 c = mix(mix(tinta2, tinta, l), t.rgb, manter);
    vec3 e = brilho * forca * l * l;
    if (fx == 1) e *= 0.6 + 0.4 * sin(TIME * 4.0 + (UV.x + UV.y) * 18.0);
    if (fx == 2) { float n = ns(UV * 14.0 + TIME * 0.15); float veia = smoothstep(0.05, 0.0, abs(n - 0.5)); e = brilho * forca * veia * (0.7 + 0.3 * sin(TIME * 3.0)); c = mix(c, brilho, veia * 0.6); }
    if (fx == 3) e += brilho * step(0.992, h1(floor(UV * 90.0) + floor(TIME * 3.0))) * 3.0;
    if (fx == 4) { vec2 g = floor(UV * 70.0); float s = step(0.985, h1(g)) * (0.6 + 0.4 * sin(TIME * 3.0 + h1(g + 1.0) * 9.0)); c = mix(c, vec3(1.0), s); e += vec3(1.0) * s * 2.0; }
    ALBEDO = c;
    EMISSION = e;
    ROUGHNESS = 0.7;
}";

    /// <summary>Monta a asa nova debaixo de <paramref name="raiz"/>; devolve as metades (pivô, lado). Vazio = falhou.</summary>
    public static List<(Node3D Pivot, float Side)> Montar(Node3D raiz, string kind)
    {
        var lista = new List<(Node3D, float)>();
        if (!Estilos.TryGetValue(kind, out var e)) return lista;
        raiz.Scale = Vector3.One * e.Escala;
        if (e.Modelo is not null && GlbCache.Instance(e.Modelo) is { } pack)
        {
            foreach (var (name, side) in new[] { ("asa_dir", 1f), ("asa_esq", -1f) })
            {
                if (pack.FindChild(name, true, false) is not Node3D half) continue;
                var pivot = new Node3D { Name = name, Position = new Vector3(side * 0.05f, 0.05f, -0.02f) };
                raiz.AddChild(pivot);
                half.GetParent().RemoveChild(half);
                pivot.AddChild(half);
                half.Position = Vector3.Zero;
                var esq = side < 0 && e.TintaEsq is not null;
                Pintar(half, e, esq ? e.TintaEsq!.Value : e.Tinta, esq ? e.TintaEsq!.Value * 0.3f : e.Tinta2);
                lista.Add((pivot, side));
            }
            pack.QueueFree();
        }
        else
        {
            foreach (var side in new[] { 1f, -1f })
            {
                var pivot = new Node3D { Name = side > 0 ? "asa_dir" : "asa_esq", Position = new Vector3(side * 0.06f, 0.05f, -0.02f) };
                raiz.AddChild(pivot);
                Procedural(pivot, e, side);
                lista.Add((pivot, side));
            }
        }
        Extras(raiz, e);
        if (e.Particula is { } pc)
        {
            raiz.AddChild(new CpuParticles3D
            {
                Amount = 16, Lifetime = 1.6f, Position = new Vector3(0, 0.2f, -0.1f), EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(0.9f, 0.4f, 0.1f), Direction = Vector3.Down, Spread = 40f, Gravity = new Vector3(0, -0.3f, 0),
                InitialVelocityMin = 0.05f, InitialVelocityMax = 0.25f, ScaleAmountMin = 0.015f, ScaleAmountMax = 0.035f, LocalCoords = false,
                Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3,
                    Material = new StandardMaterial3D { AlbedoColor = pc, EmissionEnabled = true, Emission = pc, EmissionEnergyMultiplier = 3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
            });
        }
        return lista;
    }

    private static void Pintar(Node3D half, Estilo e, Color tinta, Color tinta2)
    {
        _tinta ??= new Shader { Code = TintaShader };
        foreach (var mi in half.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Append(half as MeshInstance3D).Where(m => m is not null))
        {
            if (mi!.Mesh is null) continue;
            for (var i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                var orig = mi.GetActiveMaterial(i) as BaseMaterial3D;
                var sm = new ShaderMaterial { Shader = _tinta };
                if (orig?.AlbedoTexture is { } tex) sm.SetShaderParameter("tex", tex);
                sm.SetShaderParameter("tinta", new Vector3(tinta.R, tinta.G, tinta.B));
                sm.SetShaderParameter("tinta2", new Vector3(tinta2.R, tinta2.G, tinta2.B));
                sm.SetShaderParameter("manter", e.Manter);
                sm.SetShaderParameter("brilho", new Vector3(e.Brilho.R, e.Brilho.G, e.Brilho.B));
                sm.SetShaderParameter("forca", e.Forca);
                sm.SetShaderParameter("fx", e.Fx);
                mi.SetSurfaceOverrideMaterial(i, sm);
            }
        }
    }

    // ------------------------------------------------------------------ asas montadas em código
    private static void Procedural(Node3D pivo, Estilo e, float s)
    {
        // as montadas em código no mesmo tamanho dos modelos (~1 m de envergadura por lado)
        // 15/11 noite: marcada — o MannequinVisual abre as asas de modelo em V; estas (desenhadas deitadas) ele compensa
        var p = new Node3D { Scale = Vector3.One * 1.9f, Position = new Vector3(0, -0.08f, 0) };
        p.SetMeta("procedural", true);
        pivo.AddChild(p);
        switch (e.Extra)
        {
            case "inseto":
            case "borboleta":
            {
                var borb = e.Extra == "borboleta";
                Material Painel(Color c1, Color c2) => borb
                    ? new ShaderMaterial { Shader = _borb ??= new Shader { Code = BorbShader }, ResourceLocalToScene = true }
                    : new StandardMaterial3D { AlbedoColor = new Color(c1.R, c1.G, c1.B, 0.45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 0.1f, Metallic = 0.3f, EmissionEnabled = true, Emission = c1 * 0.3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel };
                var cima = Painel(e.Tinta, e.Tinta2); var baixo = Painel(e.Tinta, e.Tinta2);
                if (borb) { ((ShaderMaterial)cima).SetShaderParameter("cor", new Vector3(e.Tinta.R, e.Tinta.G, e.Tinta.B)); ((ShaderMaterial)baixo).SetShaderParameter("cor", new Vector3(e.Tinta.R * 0.9f, e.Tinta.G * 0.7f, e.Tinta.B)); }
                VisualKit.Part(p, VisualKit.Sphere(0.32f), cima, new Vector3(s * 0.34f, 0.2f, -0.03f), new Vector3(0, 0, s * 25), new Vector3(1.15f, 0.62f, 0.03f));
                VisualKit.Part(p, VisualKit.Sphere(0.24f), baixo, new Vector3(s * 0.26f, -0.12f, -0.03f), new Vector3(0, 0, s * -30), new Vector3(0.95f, 0.6f, 0.03f));
                var veia = VisualKit.Mat(borb ? new Color(0.08f, 0.05f, 0.05f) : new Color(0.25f, 0.4f, 0.8f), 0.4f);
                for (var i = 0; i < 4; i++)
                    VisualKit.Part(p, VisualKit.Box(0.55f - i * 0.08f, 0.008f, 0.008f), veia, new Vector3(s * (0.28f - i * 0.01f), 0.16f - i * 0.08f, -0.02f), new Vector3(0, 0, s * (35 - i * 18)));
                break;
            }
            case "folhas":
            {
                var verde = VisualKit.Mat(e.Tinta, 0.8f); var escuro = VisualKit.Mat(e.Tinta2, 0.85f);
                var galho = VisualKit.Mat(new Color(0.35f, 0.22f, 0.1f), 0.9f);
                VisualKit.Part(p, VisualKit.Cylinder(0.012f, 0.02f, 0.7f, 6), galho, new Vector3(s * 0.3f, 0.12f, -0.03f), new Vector3(0, 0, s * -70));
                for (var i = 0; i < 9; i++)
                {
                    var t = i / 8f;
                    VisualKit.Part(p, VisualKit.Sphere(0.11f), i % 2 == 0 ? verde : escuro,
                        new Vector3(s * (0.08f + t * 0.6f), 0.05f + Mathf.Sin(t * 2.6f) * 0.2f - t * 0.05f, -0.03f - (i % 3) * 0.01f), new Vector3(0, 0, s * (60 - t * 120)), new Vector3(1.6f, 0.55f, 0.08f));
                }
                foreach (var t in new[] { 0.3f, 0.65f })   // florzinhas brancas
                    for (var k = 0; k < 5; k++)
                    {
                        var a = k / 5f * Mathf.Tau;
                        VisualKit.Part(p, VisualKit.Sphere(0.018f), VisualKit.Mat(new Color(0.98f, 0.97f, 0.93f), 0.8f), new Vector3(s * (0.08f + t * 0.6f) + Mathf.Cos(a) * 0.022f, 0.12f + Mathf.Sin(t * 2.6f) * 0.2f + Mathf.Sin(a) * 0.022f, 0.0f));
                    }
                break;
            }
            case "vapor":
            {
                var nuvem = new StandardMaterial3D { AlbedoColor = new Color(e.Tinta.R, e.Tinta.G, e.Tinta.B, 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1f };
                var sombra = new StandardMaterial3D { AlbedoColor = new Color(e.Tinta2.R, e.Tinta2.G, e.Tinta2.B, 0.75f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1f };
                for (var i = 0; i < 8; i++)
                {
                    var t = i / 7f;
                    var r = 0.09f + 0.06f * Mathf.Sin(t * 3f);
                    VisualKit.Part(p, VisualKit.Sphere(r), i % 3 == 0 ? sombra : nuvem, new Vector3(s * (0.1f + t * 0.62f), 0.1f + Mathf.Sin(t * 3.4f) * 0.16f, -0.04f));
                }
                VisualKit.Part(p, new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.12f, Rings = 16, RingSegments = 8 }, nuvem, new Vector3(s * 0.62f, 0.22f, -0.04f), new Vector3(90, 0, 0), new Vector3(1f, 1f, 0.5f));   // redemoinho
                break;
            }
            case "ossos":
            {
                var osso = VisualKit.Mat(e.Tinta, 0.6f);
                VisualKit.Part(p, VisualKit.Cylinder(0.02f, 0.028f, 0.42f, 6), osso, new Vector3(s * 0.2f, 0.12f, -0.03f), new Vector3(0, 0, s * -65));   // úmero
                VisualKit.Part(p, VisualKit.Sphere(0.035f), osso, new Vector3(s * 0.4f, 0.2f, -0.03f));
                for (var i = 0; i < 4; i++)   // dedos da asa
                {
                    var ang = s * (-20f - i * 28f);
                    var dedo = new Node3D { Position = new Vector3(s * 0.4f, 0.2f, -0.03f), RotationDegrees = new Vector3(0, 0, ang) };
                    p.AddChild(dedo);
                    var len = 0.55f - i * 0.08f;
                    VisualKit.Part(dedo, VisualKit.Cylinder(0.008f, 0.016f, len, 6), osso, new Vector3(s * len * 0.5f, 0, 0), new Vector3(0, 0, 90));
                    VisualKit.Part(dedo, VisualKit.Cylinder(0f, 0.012f, 0.06f, 4), osso, new Vector3(s * len, 0, 0), new Vector3(0, 0, s * -90));   // garra
                }
                break;
            }
            case "raios":
            {
                var raio = VisualKit.Glow(e.Brilho, e.Forca);
                var rnd = new RandomNumberGenerator { Seed = (ulong)(s > 0 ? 7 : 13) };
                for (var b = 0; b < 4; b++)
                {
                    var ang = Mathf.DegToRad(30f - b * 26f);
                    var pos = Vector3.Zero;
                    for (var k = 0; k < 6; k++)
                    {
                        var dir = new Vector3(s * Mathf.Cos(ang + rnd.RandfRange(-0.5f, 0.5f)), Mathf.Sin(ang + rnd.RandfRange(-0.5f, 0.5f)), 0) * (0.12f - b * 0.01f);
                        var meio = pos + dir * 0.5f;
                        var seg = VisualKit.Part(p, VisualKit.Box(dir.Length(), 0.014f, 0.014f), raio, meio);
                        seg.Rotation = new Vector3(0, 0, Mathf.Atan2(dir.Y, dir.X));
                        pos += dir;
                    }
                }
                p.AddChild(new OmniLight3D { LightColor = e.Brilho, LightEnergy = 0.6f, OmniRange = 1.6f, Position = new Vector3(s * 0.4f, 0.1f, 0), ShadowEnabled = false });
                break;
            }
        }
    }

    private static Shader? _borb;
    private const string BorbShader = @"
shader_type spatial;
render_mode cull_disabled;
uniform vec3 cor = vec3(1.0, 0.55, 0.1);
void fragment() {
    vec2 p = UV - 0.5;
    float r = length(p * vec2(1.0, 1.6));
    vec3 c = mix(cor, vec3(0.06, 0.03, 0.05), smoothstep(0.36, 0.42, r));
    float pinta = step(0.5, fract(atan(p.y, p.x) * 3.0)) * smoothstep(0.42, 0.44, r) * step(r, 0.48);
    c = mix(c, vec3(0.98), pinta);
    c = mix(c, vec3(0.45, 0.15, 0.6), smoothstep(0.15, 0.0, r) * 0.6);
    ALBEDO = c;
    EMISSION = cor * 0.15;
}";

    private static void Extras(Node3D raiz, Estilo e)
    {
        var ouro = VisualKit.Mat(new Color(1f, 0.78f, 0.3f), 0.25f, 0.95f);
        switch (e.Extra)
        {
            case "aureola":
            case "aureola_gema":
            case "criacao":
            {
                var halo = VisualKit.Glow(e.Extra == "criacao" ? new Color(1f, 0.9f, 0.5f) : new Color(1f, 0.85f, 0.45f), 2.2f);
                VisualKit.Part(raiz, new TorusMesh { InnerRadius = 0.13f, OuterRadius = 0.15f, Rings = 28, RingSegments = 6 }, halo, new Vector3(0, 0.62f, -0.08f), new Vector3(90, 0, 0));
                if (e.Extra != "aureola")
                {
                    var orbe = VisualKit.Glow(e.Extra == "criacao" ? new Color(1f, 0.95f, 0.75f) : new Color(0.4f, 0.75f, 1f), 3f);
                    VisualKit.Part(raiz, VisualKit.Sphere(0.06f), orbe, new Vector3(0, 0.3f, -0.12f));
                    raiz.AddChild(new OmniLight3D { LightColor = e.Brilho, LightEnergy = 0.5f, OmniRange = 1.8f, Position = new Vector3(0, 0.3f, -0.15f), ShadowEnabled = false });
                }
                break;
            }
            case "sigilo":
            {
                var verm = VisualKit.Glow(new Color(1f, 0.12f, 0.08f), 2.6f);
                VisualKit.Part(raiz, new TorusMesh { InnerRadius = 0.07f, OuterRadius = 0.085f, Rings = 24, RingSegments = 4 }, verm, new Vector3(0, 0.25f, -0.14f), new Vector3(90, 0, 0));
                VisualKit.Part(raiz, VisualKit.Sphere(0.03f), verm, new Vector3(0, 0.25f, -0.14f), null, new Vector3(1f, 1.6f, 0.5f));
                break;
            }
            case "buraco_negro":
            case "eclipse":
            {
                VisualKit.Part(raiz, VisualKit.Sphere(0.1f), VisualKit.Mat(new Color(0.01f, 0.01f, 0.02f), 0.2f), new Vector3(0, 0.35f, -0.15f));
                var anel = VisualKit.Glow(e.Extra == "eclipse" ? new Color(1f, 0.75f, 0.3f) : new Color(0.7f, 0.45f, 1f), 2.8f);
                VisualKit.Part(raiz, new TorusMesh { InnerRadius = 0.105f, OuterRadius = 0.125f, Rings = 28, RingSegments = 6 }, anel, new Vector3(0, 0.35f, -0.15f), new Vector3(90, 0, 0));
                break;
            }
            case "relogio":
            {
                var no = new Node3D { Position = new Vector3(0, 0.3f, -0.14f) };
                raiz.AddChild(no);
                VisualKit.Part(no, VisualKit.Cylinder(0.1f, 0.1f, 0.02f, 24), ouro, Vector3.Zero, new Vector3(90, 0, 0));
                VisualKit.Part(no, VisualKit.Cylinder(0.085f, 0.085f, 0.022f, 24), VisualKit.Mat(new Color(0.95f, 0.92f, 0.82f), 0.5f), new Vector3(0, 0, -0.002f), new Vector3(90, 0, 0));
                VisualKit.Part(no, VisualKit.Box(0.005f, 0.065f, 0.005f), VisualKit.Mat(new Color(0.05f, 0.05f, 0.05f)), new Vector3(0, 0.025f, -0.015f));
                foreach (var sd in new[] { -1f, 1f })
                    VisualKit.Part(raiz, new TorusMesh { InnerRadius = 0.04f, OuterRadius = 0.065f, Rings = 12, RingSegments = 4 }, ouro, new Vector3(sd * 0.22f, 0.22f, -0.1f), new Vector3(90, 0, 0));
                break;
            }
            case "lobos":
            {
                var pelo = VisualKit.Mat(new Color(0.6f, 0.85f, 1f), 0.4f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    var cab = new Node3D { Position = new Vector3(sd * 0.32f, 0.3f, -0.08f), RotationDegrees = new Vector3(0, sd * 70, 0) };
                    raiz.AddChild(cab);
                    VisualKit.Part(cab, VisualKit.Sphere(0.09f), pelo, Vector3.Zero, null, new Vector3(1f, 0.9f, 1.1f));
                    VisualKit.Part(cab, VisualKit.Box(0.08f, 0.06f, 0.12f), pelo, new Vector3(0, -0.02f, 0.1f));
                    foreach (var o in new[] { -1f, 1f })
                    {
                        VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.03f, 0.08f, 4), pelo, new Vector3(o * 0.05f, 0.09f, -0.01f));
                        VisualKit.Part(cab, VisualKit.Sphere(0.013f), VisualKit.Glow(new Color(0.6f, 0.95f, 1f), 3f), new Vector3(o * 0.035f, 0.03f, 0.08f));
                    }
                }
                break;
            }
        }
    }
}
