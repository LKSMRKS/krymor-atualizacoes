using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Vida no ambiente (Cal lote 2), só visual e sem servidor: gaivotas circulando e pousando, siris de enfeite andando de lado
/// (fogem do jogador), rajadas de areia levadas pelo vento, fogueiras com fogo e fumaça, colunas de fumaça que se veem de
/// longe (marco do acampamento) e o feixe giratório do farol à noite.
/// Uso (MapView, mapas abertos):
///   var vida = new VidaAmbiente { Metade = (float)Map.HalfSize, Jogador = () => heroi.GlobalPosition };
///   Root.AddChild(vida);
///   vida.Gaivotas(8); vida.Siris(10); vida.Vento();
///   vida.Fogueira(P(x, y));                      // em cada prop "fogueira"
///   vida.ColunaDeFumaca(P(x, y), grande: true);   // acampamento goblin (marco)
///   vida.FeixeDoFarol(P(o.X, o.Y, h + 1.2f));     // no "farol"
/// Modelos: Assets/Models/costa/vida_gaivota.glb, costa/cenario_fogueira.glb, criaturas/caranguejinho.glb (siri = 0,32x).
/// Texturas: Assets/Art/fx_fumaca.png, fx_poeira.png. Arquivo faltando = aquela parte não aparece (nada quebra).
/// </summary>
public sealed partial class VidaAmbiente : Node3D
{
    public Func<Vector3>? Jogador { get; set; }
    public float Metade { get; set; } = 100f;
    /// <summary>Direção do vento no chão (XZ).</summary>
    public Vector3 Vento3 { get; set; } = new Vector3(1f, 0, 0.35f).Normalized();

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<Ave> _aves = new();
    private readonly List<Siri> _siris = new();
    private readonly List<(OmniLight3D Luz, float Base, float Fase)> _chamas = new();
    private readonly List<Node3D> _feixes = new();
    private static Shader? _feixeShader;
    private CpuParticles3D? _rajada;
    private float _proximaRajada = 4f, _t;
    private int _maxSiris;

    public VidaAmbiente() { Name = "VidaAmbiente"; _rng.Seed = 4242; }

    // ------------------------------------------------------------------ util
    private static AnimationPlayer? Anim(Node3D n, params string[] loops)
    {
        var ap = Visuals.GlbCache.Find<AnimationPlayer>(n);
        if (ap is null) return null;
        foreach (var l in loops)
            if (ap.HasAnimation(l)) ap.GetAnimation(l).LoopMode = Animation.LoopModeEnum.Linear;
        return ap;
    }
    private static void Tocar(AnimationPlayer? ap, string nome, float blend = 0.25f, float vel = 1f)
    {
        if (ap is null || !ap.HasAnimation(nome)) return;
        if (ap.CurrentAnimation != nome) ap.Play(nome, blend);
        ap.SpeedScale = vel;
    }
    private Vector3 Centro() => Jogador?.Invoke() ?? Vector3.Zero;
    internal static StandardMaterial3D Particula(string tex, Color cor, bool aditivo)
    {
        var m = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, VertexColorUseAsAlbedo = true, AlbedoColor = cor,
            BlendMode = aditivo ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        };
        if (VisualAssets.Load(tex) is { } t) m.AlbedoTexture = t;
        return m;
    }
    internal static Gradient Rampa(params (float T, Color C)[] pts)
    {
        var g = new Gradient();
        g.SetColor(0, pts[0].C); g.SetOffset(0, pts[0].T);
        g.SetColor(1, pts[^1].C); g.SetOffset(1, pts[^1].T);
        for (var i = 1; i < pts.Length - 1; i++) g.AddPoint(pts[i].T, pts[i].C);
        return g;
    }
    internal static Curve Crescer(float a, float b)
    {
        var c = new Curve(); c.AddPoint(new Vector2(0, a)); c.AddPoint(new Vector2(1, b)); return c;
    }

    // ------------------------------------------------------------------ gaivotas
    private sealed class Ave
    {
        public Node3D No = null!; public AnimationPlayer? Ap;
        public Vector3 Centro; public float Raio, Altura, Ang, Vel, Tempo; public bool Pousada;
    }

    public void Gaivotas(int n)
    {
        for (var i = 0; i < n; i++)
        {
            if (Visuals.GlbCache.Instance("costa/vida_gaivota") is not { } g) return;
            var no = new Node3D { Name = "Gaivota" };
            no.AddChild(g); AddChild(no);
            var a = new Ave
            {
                No = no, Ap = Anim(g, "fly", "glide", "idle", "walk"),
                Centro = new Vector3(_rng.RandfRange(-Metade, Metade) * 0.8f, 0, _rng.RandfRange(-Metade, Metade) * 0.8f),
                Raio = _rng.RandfRange(8f, 22f), Altura = _rng.RandfRange(7f, 16f), Ang = _rng.RandfRange(0, Mathf.Tau),
                Vel = _rng.RandfRange(0.25f, 0.45f) * (_rng.Randf() < 0.5f ? 1 : -1), Tempo = _rng.RandfRange(10f, 40f),
            };
            _aves.Add(a);
        }
    }

    private void AtualizarAves(float dt)
    {
        foreach (var a in _aves)
        {
            a.Tempo -= dt;
            if (a.Pousada)
            {
                Tocar(a.Ap, "idle");
                var d = a.No.Position.DistanceTo(Centro());
                if (a.Tempo <= 0 || d < 6f) { a.Pousada = false; a.Tempo = _rng.RandfRange(15f, 45f); }   // o heroi chega perto: voa
                continue;
            }
            if (a.Tempo <= 0 && _rng.Randf() < 0.35f)
            {   // pousa num ponto do chao perto do circulo
                a.Pousada = true; a.Tempo = _rng.RandfRange(8f, 20f);
                a.No.Position = a.Centro + new Vector3(Mathf.Cos(a.Ang) * a.Raio, 0, Mathf.Sin(a.Ang) * a.Raio);
                continue;
            }
            if (a.Tempo <= 0) a.Tempo = _rng.RandfRange(10f, 30f);
            a.Ang += a.Vel * dt;
            var alt = a.Altura + Mathf.Sin(_t * 0.4f + a.Raio) * 1.5f;
            var p = a.Centro + new Vector3(Mathf.Cos(a.Ang) * a.Raio, alt, Mathf.Sin(a.Ang) * a.Raio);
            var dir = new Vector3(-Mathf.Sin(a.Ang), 0, Mathf.Cos(a.Ang)) * Mathf.Sign(a.Vel);
            a.No.Position = p;
            a.No.LookAt(p - dir, Vector3.Up);       // frente do modelo = +Z
            // bate asas de vez em quando, plana o resto
            Tocar(a.Ap, Mathf.Sin(_t * 0.3f + a.Raio * 3f) > 0.55f ? "fly" : "glide", 0.4f);
        }
    }

    // ------------------------------------------------------------------ siris de enfeite
    private sealed class Siri { public Node3D No = null!; public AnimationPlayer? Ap; public Vector3 Alvo; public float Espera; public Bicho Tipo = null!; }

    /// <summary>13/11 ("assim como tem caranguejo pequeno de cenário no começo, fazer nos outros lugares"): bichinho de
    /// enfeite por tema — modelo, tamanho, velocidade e se anda de lado (caranguejo). Foge do herói e renasce em volta.</summary>
    private sealed record Bicho(string Modelo, float Escala, float Vel, bool Lateral, Color? Tinta = null);
    private readonly List<Bicho> _bichos = new();
    private static readonly Bicho SiriPadrao = new("criaturas/caranguejinho", 0.32f, 0.9f, true);

    public void Siris(int n) { _maxSiris += n; _bichos.Add(SiriPadrao); }

    /// <summary>Bichinhos de enfeite do tema (rato, inseto do lodo, caranguejinho...).</summary>
    public void Bichinhos(string modelo, float escala, int n, float vel = 1.2f, bool lateral = false, Color? tinta = null)
    {
        _maxSiris += n;
        _bichos.Add(new Bicho(modelo, escala, vel, lateral, tinta));
    }

    private static void Tingir(Node n, Color c)
    {
        foreach (var ch in n.GetChildren())
        {
            if (ch is MeshInstance3D mi) mi.MaterialOverride = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.85f };
            Tingir(ch, c);
        }
    }

    // ------------------------------------------------------------------ vaga-lumes / brasas / folhas
    private CpuParticles3D? _luzinhas;
    private bool _soDeNoite;
    /// <summary>Partículas em volta do herói: vaga-lumes (só à noite), brasas subindo, folhas ou neve caindo, cinzas.</summary>
    public void Luzinhas(string tipo)
    {
        var (cor, qtd, grav, vel, tam, noite) = tipo switch
        {
            "vagalume" => (new Color(0.85f, 1f, 0.35f), 40, new Vector3(0, 0.05f, 0), 0.35f, 0.07f, true),
            "brasa" => (new Color(1f, 0.45f, 0.12f), 60, new Vector3(0, 0.6f, 0), 0.8f, 0.06f, false),
            "folha" => (new Color(0.55f, 0.42f, 0.2f), 24, new Vector3(0.3f, -0.35f, 0.1f), 0.4f, 0.14f, false),
            "neve" => (new Color(0.95f, 0.97f, 1f), 140, new Vector3(0.15f, -0.9f, 0.05f), 0.2f, 0.06f, false),
            // 13/11 ("outras criaturinhas, folhas, coisas que façam sentido com o ambiente, mas nada pesado")
            "borboleta" => (new Color(1f, 0.78f, 0.25f), 12, new Vector3(0, 0.02f, 0), 0.6f, 0.06f, false),
            "mosquito" => (new Color(0.12f, 0.12f, 0.1f), 45, new Vector3(0, 0.03f, 0), 0.5f, 0.025f, false),
            "poeira" => (new Color(1f, 0.92f, 0.7f, 0.6f), 35, new Vector3(0.12f, 0.02f, 0), 0.12f, 0.035f, false),
            "bolha" => (new Color(0.75f, 0.95f, 1f, 0.7f), 50, new Vector3(0, 0.9f, 0), 0.3f, 0.05f, false),
            "petala" => (new Color(1f, 0.75f, 0.85f), 20, new Vector3(0.25f, -0.25f, 0.1f), 0.35f, 0.07f, false),
            _ => (new Color(0.62f, 0.62f, 0.66f), 50, new Vector3(0.1f, -0.15f, 0), 0.25f, 0.05f, false),   // cinza no ar
        };
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = cor, VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            EmissionEnabled = tipo is "vagalume" or "brasa", Emission = cor, EmissionEnergyMultiplier = 2.5f,
        };
        Mesh forma = tipo switch
        {
            "borboleta" or "folha" or "petala" => new QuadMesh { Size = new Vector2(tam * 2.2f, tam * 1.4f), Material = mat },   // plaquinha que gira
            _ => new SphereMesh { Radius = tam, Height = tam * 2f, RadialSegments = 4, Rings = 2, Material = mat },
        };
        _luzinhas = new CpuParticles3D
        {
            Name = "Luzinhas_" + tipo, Amount = qtd, Lifetime = 6f, LocalCoords = false, Preprocess = 6f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(18f, tipo is "neve" or "folha" ? 0.5f : 1.6f, 18f),
            Direction = new Vector3(0, 1, 0), Spread = 180f, InitialVelocityMin = vel * 0.3f, InitialVelocityMax = vel, Gravity = grav,
            ScaleAmountMin = 0.6f, ScaleAmountMax = 1.4f,
            ColorRamp = Rampa((0f, cor with { A = 0f }), (0.2f, cor), (0.8f, cor), (1f, cor with { A = 0f })),
            Mesh = forma,
        };
        if (tipo is "borboleta" or "mosquito") { _luzinhas.EmissionBoxExtents = new Vector3(18f, 0.9f, 18f); _luzinhas.Position = new Vector3(0, 0.9f, 0); }
        if (tipo == "borboleta") { _luzinhas.AngularVelocityMin = -180f; _luzinhas.AngularVelocityMax = 180f; _luzinhas.Lifetime = 9f; }
        if (tipo == "bolha") _luzinhas.EmissionBoxExtents = new Vector3(20f, 0.3f, 20f);
        _soDeNoite = noite;
        AddChild(_luzinhas);
    }

    private void AtualizarSiris(float dt)
    {
        var c = Centro();
        while (_siris.Count < _maxSiris)
        {
            if (_bichos.Count == 0) { _maxSiris = 0; break; }
            var tipo = _bichos[_siris.Count % _bichos.Count];
            if (Visuals.GlbCache.Instance(tipo.Modelo) is not { } m) { _maxSiris = 0; break; }
            if (tipo.Tinta is { } tinta) Tingir(m, tinta);
            var no = new Node3D { Name = "BichoEnfeite", Scale = Vector3.One * tipo.Escala };
            no.AddChild(m); AddChild(no);
            var ang = _rng.RandfRange(0, Mathf.Tau); var r = _rng.RandfRange(9f, 22f);
            var px = c.X + Mathf.Cos(ang) * r; var pz = c.Z + Mathf.Sin(ang) * r;
            no.Position = new Vector3(px, Visuals.EscadaCasa.Altura(px, pz), pz);
            _siris.Add(new Siri { No = no, Ap = Anim(m, "idle", "walk", "run"), Alvo = no.Position, Espera = _rng.RandfRange(0, 3f), Tipo = tipo });
        }
        for (var i = _siris.Count - 1; i >= 0; i--)
        {
            var s = _siris[i];
            var pos = s.No.Position;
            if (pos.DistanceTo(c) > 32f) { s.No.QueueFree(); _siris.RemoveAt(i); continue; }   // ficou para tras: some e nasce outro
            var foge = pos.DistanceTo(c) < 4f;
            if (foge) { var fora = (pos - c) with { Y = 0 }; s.Alvo = pos + fora.Normalized() * 5f; s.Espera = 0; }
            if (s.Espera > 0) { s.Espera -= dt; Tocar(s.Ap, "idle"); continue; }
            var para = (s.Alvo - pos) with { Y = 0 };
            if (para.Length() < 0.2f)
            {
                s.Espera = _rng.RandfRange(1.5f, 5f);
                var ang = _rng.RandfRange(0, Mathf.Tau);
                s.Alvo = pos + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * _rng.RandfRange(1.5f, 4f);
                continue;
            }
            var vel = foge ? s.Tipo.Vel * 2.9f : s.Tipo.Vel;
            var np = pos + para.Normalized() * Mathf.Min(para.Length(), vel * dt);
            np.Y = Visuals.EscadaCasa.Altura(np.X, np.Z);
            s.No.Position = np;
            // caranguejo anda de lado: o "lado" do modelo aponta para onde vai; os outros, de frente (+Z)
            var lado = para.Normalized();
            if (s.Tipo.Lateral) s.No.LookAt(s.No.Position + new Vector3(lado.Z, 0, -lado.X), Vector3.Up);
            else s.No.LookAt(s.No.Position - lado, Vector3.Up);
            Tocar(s.Ap, foge && s.Ap?.HasAnimation("run") == true ? "run" : "walk", 0.15f, foge ? 2.2f : 1f);
        }
    }

    // ------------------------------------------------------------------ vento (rajadas de areia)
    public void Vento()
    {
        _rajada = new CpuParticles3D
        {
            Name = "RajadaDeAreia", Emitting = false, OneShot = false, Amount = 60, Lifetime = 2.6f, LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(16f, 0.25f, 16f),
            Direction = Vento3, Spread = 12f, InitialVelocityMin = 4f, InitialVelocityMax = 8f, Gravity = new Vector3(0, -0.2f, 0),
            ScaleAmountMin = 1.2f, ScaleAmountMax = 2.6f, ScaleAmountCurve = Crescer(0.6f, 1.3f),
            ColorRamp = Rampa((0f, new Color(0.9f, 0.84f, 0.72f, 0f)), (0.25f, new Color(0.9f, 0.84f, 0.72f, 0.32f)), (1f, new Color(0.9f, 0.84f, 0.72f, 0f))),
            Mesh = new QuadMesh { Size = new Vector2(1.6f, 0.4f), Material = Particula("fx_poeira.png", Colors.White, false) },
        };
        AddChild(_rajada);
    }

    // ------------------------------------------------------------------ fogo e fumaca
    /// <summary>Fogueira completa: modelo (se houver), chamas, fagulhas, fumaça e luz que pisca (mais forte à noite).</summary>
    public void Fogueira(Vector3 pos, bool modelo = true)
    {
        var no = new Node3D { Name = "Fogueira", Position = pos };
        AddChild(no);
        if (modelo && Visuals.GlbCache.Instance("costa/cenario_fogueira") is { } m) no.AddChild(m);
        no.AddChild(new CpuParticles3D
        {
            Name = "Chamas", Amount = 26, Lifetime = 0.7f, LocalCoords = false, Position = new Vector3(0, 0.15f, 0),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.18f,
            Direction = Vector3.Up, Spread = 10f, InitialVelocityMin = 0.8f, InitialVelocityMax = 1.6f, Gravity = new Vector3(0, 1.2f, 0),
            ScaleAmountMin = 0.35f, ScaleAmountMax = 0.7f, ScaleAmountCurve = Crescer(1f, 0.2f),
            ColorRamp = Rampa((0f, new Color(1f, 0.9f, 0.5f, 0.9f)), (0.5f, new Color(1f, 0.45f, 0.1f, 0.7f)), (1f, new Color(0.6f, 0.15f, 0.05f, 0f))),
            Mesh = new QuadMesh { Size = Vector2.One, Material = Particula("fx_fumaca.png", Colors.White, true) },
        });
        no.AddChild(new CpuParticles3D
        {
            Name = "Fagulhas", Amount = 10, Lifetime = 1.6f, LocalCoords = false, Position = new Vector3(0, 0.3f, 0),
            Direction = Vector3.Up, Spread = 25f, InitialVelocityMin = 1f, InitialVelocityMax = 2.4f, Gravity = new Vector3(0.3f, 0.4f, 0),
            ScaleAmountMin = 0.04f, ScaleAmountMax = 0.07f,
            ColorRamp = Rampa((0f, new Color(1f, 0.8f, 0.3f, 1f)), (1f, new Color(1f, 0.3f, 0.05f, 0f))),
            Mesh = new QuadMesh { Size = Vector2.One, Material = Particula("fx_fumaca.png", Colors.White, true) },
        });
        ColunaDeFumaca(pos + new Vector3(0, 0.6f, 0), grande: false, pai: no, local: false);
        var luz = new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.25f), LightEnergy = 2.2f, OmniRange = 9f, ShadowEnabled = true, Position = new Vector3(0, 0.7f, 0),
            DistanceFadeEnabled = true, DistanceFadeBegin = 48f, DistanceFadeLength = 10f, DistanceFadeShadow = 22f };   // 13/11: sombra só de perto
        no.AddChild(luz);
        _chamas.Add((luz, 2.2f, _rng.RandfRange(0, 10f)));
    }

    /// <summary>Fumaça subindo. grande = marco visto de longe (acampamento goblin, vila queimando): coluna escura de ~35 m.</summary>
    public void ColunaDeFumaca(Vector3 pos, bool grande, Node3D? pai = null, bool local = true)
    {
        var p = new CpuParticles3D
        {
            Name = grande ? "FumacaMarco" : "Fumaca", Amount = grande ? 70 : 18, Lifetime = grande ? 16f : 5f, LocalCoords = false,
            Position = local || pai is null ? pos : Vector3.Zero + new Vector3(0, 0.6f, 0),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = grande ? 1.2f : 0.2f,
            Direction = Vector3.Up, Spread = grande ? 6f : 12f,
            InitialVelocityMin = grande ? 1.8f : 0.5f, InitialVelocityMax = grande ? 2.6f : 0.9f,
            Gravity = Vento3 * (grande ? 0.25f : 0.12f) + new Vector3(0, 0.05f, 0),
            ScaleAmountMin = grande ? 3f : 0.6f, ScaleAmountMax = grande ? 5f : 1.1f, ScaleAmountCurve = Crescer(0.4f, grande ? 3.2f : 2.4f),
            AngleMin = 0, AngleMax = 360, AngularVelocityMin = -12, AngularVelocityMax = 12,
            ColorRamp = grande
                ? Rampa((0f, new Color(0.18f, 0.16f, 0.15f, 0f)), (0.08f, new Color(0.2f, 0.18f, 0.17f, 0.75f)), (0.6f, new Color(0.38f, 0.36f, 0.36f, 0.45f)), (1f, new Color(0.55f, 0.55f, 0.58f, 0f)))
                : Rampa((0f, new Color(0.5f, 0.48f, 0.45f, 0f)), (0.15f, new Color(0.5f, 0.48f, 0.45f, 0.5f)), (1f, new Color(0.7f, 0.7f, 0.72f, 0f))),
            Mesh = new QuadMesh { Size = Vector2.One, Material = Particula("fx_fumaca.png", Colors.White, false) },
            VisibilityAabb = new Aabb(new Vector3(-30, -2, -30), new Vector3(60, 70, 60)),
        };
        (pai ?? this).AddChild(p);
    }

    // ------------------------------------------------------------------ farol
    /// <summary>
    /// Dois fachos de luz girando NA LANTERNA do farol; fortes à noite, quase invisíveis de dia.
    /// 12/11 ("parece um cone e sai do lugar errado"): o facho nasce no centro da lanterna (sem deslocamento para baixo),
    /// é mais forte junto da luz e some depressa com a distância; brilho redondo (halo) na lanterna para o facho sair dela.
    /// O brilho de cada ponto do facho depende da distância ao EIXO do facho (calculada no shader), não da borda da malha —
    /// assim não aparece a "parede" de um cone, de nenhum ângulo.
    /// </summary>
    public void FeixeDoFarol(Vector3 lanterna, float alcance = 55f)
    {
        var pivo = new Node3D { Name = "FeixeDoFarol", Position = lanterna };
        AddChild(pivo);
        _feixeShader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_front, depth_draw_never, shadows_disabled;
uniform float forca = 0.1;
uniform float altura = 55.0;
uniform float raio_fim = 3.2;
uniform float raio_ini = 0.45;
varying vec3 axis_origin_v;
varying vec3 axis_dir_v;
varying vec3 world_pos;
void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    axis_origin_v = (MODEL_MATRIX * vec4(0.0, -altura * 0.5, 0.0, 1.0)).xyz;
    axis_dir_v = normalize((MODEL_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
}
void fragment() {
    // raio da câmera até este ponto: ponto do raio mais perto do eixo do facho
    vec3 cam = (INV_VIEW_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
    vec3 rd = normalize(world_pos - cam);
    vec3 w0 = cam - axis_origin_v;
    float b = dot(rd, axis_dir_v);
    float d = dot(rd, w0);
    float e = dot(axis_dir_v, w0);
    float den = max(1.0 - b * b, 1e-4);
    float sc = (b * e - d) / den;
    float tc = (e - b * d) / den;
    vec3 p_ray = cam + rd * sc;
    vec3 p_axis = axis_origin_v + axis_dir_v * tc;
    float t = clamp(tc / altura, 0.0, 1.0);
    float r = mix(raio_ini, raio_fim, t);
    float q = clamp(1.0 - length(p_ray - p_axis) / r, 0.0, 1.0);
    float perto = pow(1.0 - t, 2.2);
    ALBEDO = vec3(1.0, 0.88, 0.62) * forca * q * q * perto * (tc > 0.0 ? 1.0 : 0.0);
}" };
        var mat = new ShaderMaterial { Shader = _feixeShader };
        mat.SetShaderParameter("altura", alcance);
        foreach (var lado in new[] { 0f, 180f })
        {
            var cone = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 3.4f, BottomRadius = 0.5f, Height = alcance, RadialSegments = 20, CapTop = true, CapBottom = true },
                MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            // cilindro deitado saindo da lanterna (base no centro dela) para fora, um pouco inclinado para o mar
            var braco = new Node3D { RotationDegrees = new Vector3(-3f, lado, 0) };
            cone.RotationDegrees = new Vector3(90, 0, 0); cone.Position = new Vector3(0, 0, alcance / 2f);
            braco.AddChild(cone); pivo.AddChild(braco);
        }
        // halo na lanterna: o facho nasce de uma luz de verdade
        var halo = new MeshInstance3D
        {
            Name = "Halo", Mesh = new QuadMesh { Size = new Vector2(3.2f, 3.2f) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, shadows_disabled;
uniform float forca = 1.0;
void vertex() { MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]); }
void fragment() { float d = length(UV - 0.5) * 2.0; ALBEDO = vec3(1.0, 0.85, 0.55) * forca * pow(clamp(1.0 - d, 0.0, 1.0), 2.5); }" } },
        };
        pivo.AddChild(halo);
        _feixes.Add(pivo);
    }

    // ------------------------------------------------------------------ laço
    public override void _Process(double delta)
    {
        var dt = (float)delta; _t += dt;
        AtualizarAves(dt);
        AtualizarSiris(dt);
        if (_luzinhas is not null)
        {
            _luzinhas.Position = Centro() + new Vector3(0, 1.4f, 0);
            _luzinhas.Emitting = !_soDeNoite || CicloDoDia.Noite > 0.5f;
        }
        if (_rajada is not null)
        {
            _rajada.Position = Centro() + new Vector3(0, 0.15f, 0) - Vento3 * 10f;
            _proximaRajada -= dt;
            if (_proximaRajada <= 0)
            {
                _rajada.Emitting = !_rajada.Emitting;
                _proximaRajada = _rajada.Emitting ? _rng.RandfRange(2f, 4f) : _rng.RandfRange(5f, 12f);
            }
        }
        var tochas = CicloDoDia.LuzDasTochas;
        _chamas.RemoveAll(c => !GodotObject.IsInstanceValid(c.Luz));
        foreach (var (luz, b, f) in _chamas)
            luz.LightEnergy = b * tochas * (0.8f + 0.2f * Mathf.Sin(_t * 9f + f) + 0.1f * Mathf.Sin(_t * 23f + f * 2f));
        var noite = 0.05f + 0.95f * CicloDoDia.Noite;
        foreach (var fx in _feixes)
        {
            fx.RotateY(dt * 0.45f);
            foreach (var braco in fx.GetChildren())
                if (braco is Node3D b3 && b3.GetChildCount() > 0 && b3.GetChild(0) is MeshInstance3D mi && mi.MaterialOverride is ShaderMaterial sm)
                    sm.SetShaderParameter("forca", 0.35f * noite);
            if (fx.GetNodeOrNull<MeshInstance3D>("Halo")?.MaterialOverride is ShaderMaterial hm) hm.SetShaderParameter("forca", 0.15f + 0.85f * noite);
        }
    }
}
