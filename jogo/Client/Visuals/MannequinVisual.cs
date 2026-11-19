using CinzasDaMare.Shared.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>Visual de jogador com extras (cabeça, montaria, asas, lendário). Boneco antigo e manequim implementam.</summary>
public interface IPlayerVisual : IEntityVisual
{
    Vector3 MoveDirLocal { get; set; }
    void SetHead(string? model);
    void SetMounted(bool mounted);
    void SetWings(string? kind);
    void SetLegendary(bool on);
    /// <summary>11/11: luvas e botas (peças de conjunto em 3D); null = sem.</summary>
    void SetLimbs(string? gloves, string? boots) { }
}

/// <summary>
/// Jogador com o manequim articulado (Assets/Models/manequim_masc|fem.glb, esqueleto Mixamo). Todas as classes têm o
/// mesmo corpo-base (só muda masculino/feminino); a classe se lê pelas roupas e armas, que vêm do equipamento:
/// - roupas: camadas da própria malha com o shader de roupa (máscara de região gravada no .glb por
///   Tools/preparar_manequim.py) — acompanham qualquer movimento; saias são um cone preso ao quadril;
/// - armas, elmos e escudos: as mesmas peças do GearFactory, presas aos ossos da mão/cabeça/antebraço;
/// - andar/correr/parado: animações do Mixamo; golpes, magias, tiros, esquiva, dano e morte: poses feitas em código
///   por cima da animação (fases preparação → golpe → recuperação vindas do servidor).
/// Nada aqui decide regra. Se o .glb faltar, VisualFactory volta ao boneco de formas simples.
/// </summary>
public sealed partial class MannequinVisual : IPlayerVisual   // 14/11 partial: poses dos poderes em Client/Efeitos/PosesPoderes.cs
{
    private readonly PassoLateral _passoLateral = new();   // 15/11 (Missão Terça)
    // regiões gravadas em COLOR_0.r (ver preparar_manequim.py)
    private const int Head = 1 << 0, Chest = 1 << 1, Belly = 1 << 2, Arm = 1 << 3, Fore = 1 << 4, Hand = 1 << 5,
        Thigh = 1 << 6, Shin = 1 << 7, Foot = 1 << 8;

    public Node3D Root { get; } = new() { Name = "Manequim" };
    public float Height => 1.75f * _heightScale;
    public Vector3 MoveDirLocal { get; set; } = Vector3.Back;

    private readonly string _classId;
    private readonly Node3D _model, _body;
    private readonly Skeleton3D _skel;
    private readonly MeshInstance3D _skin;
    private readonly AnimationPlayer? _anim;
    private readonly string _idle, _walk, _run;
    private readonly Dictionary<string, int> _bones = new();
    private readonly List<Node> _outfit = new();
    private readonly List<BaseMaterial3D> _baseMats = new();
    private readonly Node3D _handR, _handL, _headSocket, _foreL, _hips;
    private Node3D? _weapon, _shield, _helmet, _wings, _focus;
    /// <summary>10/11: escudo aparecia virado para dentro do corpo; no 1º quadro com ele na cena, vira a frente (+Z) para fora.</summary>
    private bool _shieldCheck;
    private Node3D? _segundaArma;   // 15/11: arma da mão esquerda (duas armas do Guerreiro)
    private float _skid, _lastDelta = 0.016f;
    private Vector3 _shieldScale = Vector3.One;
    private OmniLight3D? _legendLight;
    private string? _weaponModel, _offhandModel, _bodyModel, _headModel, _wingsKind;
    private bool _mounted, _wasAlive = true, _stealth;
    /// <summary>Relógio da animação (o mapa usa para soltar faíscas no tempo da martelada).</summary>
    public float AnimTime => _time;
    private float _time, _hitT = 1f, _deathT, _blendT, _clipSpeed = 1f, _stealthFade = 1f;
    private Vector3 _hitDir;
    private string _clip = "";
    private string _lastAction = "";
    private float _castFlash;

    private static Shader? _clothShader;

    public static MannequinVisual? TryCreate(string classId, bool female)
    {
        var model = GlbCache.Instance(female ? "manequim_fem" : "manequim_masc");
        if (model is null) return null;
        try { return new MannequinVisual(classId, model); }
        catch (Exception e) { GD.PrintErr("manequim: " + e.Message); model.QueueFree(); return null; }
    }

    /// <summary>
    /// Inimigo humanoide com corpo próprio (lote 3: goblin Chefe e Arqueiro, já vestidos e armados, mesmo esqueleto do
    /// manequim): usa as poses de ataque/guarda/morte do manequim, sem músculo, sem roupas por região e sem arma extra.
    /// </summary>
    public static MannequinVisual? MonsterBody(string file, float height)
    {
        if (!Godot.FileAccess.FileExists("res://Assets/Models/" + file + ".glb")) return null;
        var model = GlbCache.Instance(file);
        if (model is null) return null;
        try
        {
            _rawBody = true;
            var v = new MannequinVisual("monstro", model);
            v._mon = new HumanoidStyle { Scale = height / 1.8f };
            v._heightScale = height / 1.8f;
            v._bodyModel = "#";
            // 13/11 ("o Guardião da Galeria ainda está pequeno, deveria ter uns 4 m"): o corpo próprio não era escalado —
            // agora ele fica com a altura pedida, medindo a malha
            if (v._skin.Mesh is { } mm)
            {
                var sy = 1f;
                for (Node? n = v._skin; n is not null && !ReferenceEquals(n, v._body); n = n.GetParent())
                    if (n is Node3D n3) sy *= Mathf.Abs(n3.Scale.Y);
                var sz = mm.GetAabb().Size;
                var maior = Mathf.Max(sz.Y, Mathf.Max(sz.X, sz.Z));
                var natural = (sz.Y >= 0.5f * maior ? sz.Y : maior) * sy;   // Y é a altura (se o arquivo veio com Z para cima, a maior medida)
                if (natural > 0.3f && Mathf.Abs(height / natural - 1f) > 0.08f) v._body.Scale = Vector3.One * (height / natural);
            }
            return v;
        }
        catch (Exception e) { GD.PrintErr("corpo " + file + ": " + e.Message); model.QueueFree(); return null; }
        finally { _rawBody = false; }
    }

    [ThreadStatic] private static bool _rawBody;

    private MannequinVisual(string classId, Node3D model)
    {
        var raw = _rawBody;
        _classId = classId;
        _body = new Node3D { Name = "Corpo" };
        Root.AddChild(_body);
        _model = model;
        _body.AddChild(_model);
        _skel = GlbCache.Find<Skeleton3D>(_model) ?? throw new InvalidOperationException("sem esqueleto");
        _skin = GlbCache.Find<MeshInstance3D>(_model) ?? throw new InvalidOperationException("sem malha");
        _skin.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        _skelToRoot = Relative(_skel, Root);
        _toSkel = _skelToRoot.Orthonormalized().Inverse();
        // o importador pode trocar "mixamorig:Hips" por "mixamorig_Hips": guarda pelo nome curto
        for (var i = 0; i < _skel.GetBoneCount(); i++)
        {
            var full = _skel.GetBoneName(i);
            var cut = full.LastIndexOfAny(new[] { ':', '_' });
            _bones[full.StartsWith("mixamorig") && cut >= 0 ? full[(cut + 1)..] : full] = i;
        }

        // material do corpo: a cor vem da textura (o .glb tem COLOR_0 só para máscara de roupa)
        for (var s = 0; s < _skin.Mesh.GetSurfaceCount(); s++)
            if (_skin.Mesh.SurfaceGetMaterial(s) is BaseMaterial3D m)
            {
                var copy = (BaseMaterial3D)m.Duplicate();
                copy.VertexColorUseAsAlbedo = false;
                copy.Roughness = Math.Max(copy.Roughness, 0.55f);
                _skin.SetSurfaceOverrideMaterial(s, copy);
                _baseMats.Add(copy);
            }

        _anim = GlbCache.Find<AnimationPlayer>(_model);
        var names = _anim?.GetAnimationList() ?? Array.Empty<string>();
        string Pick(string prefix, string fallback) { foreach (var n in names) if (n.StartsWith(prefix)) return n; return fallback; }
        _idle = Pick("Idle", "");
        _walk = Pick("Walking", _idle);
        _run = Pick("Running", _walk);
        if (_anim is not null)
        {
            _anim.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            foreach (var n in names) _anim.GetAnimation(n).LoopMode = Animation.LoopModeEnum.Linear;
            PlayClip(_idle, 0f);
        }

        // poses de ação: aplicadas por um modificador do esqueleto, depois da animação de cada quadro
        _skel.AddChild(new ManequimPoser { Manequim = this });
        LimiteCorpo.Ligar(_skel);   // 14/11: braço não atravessa o corpo
        _handR = Socket("RightHand");
        _handL = Socket("LeftHand");
        _foreL = Socket("LeftForeArm");
        _headSocket = Socket("Head");
        _hips = Socket("Hips");
        if (classId == "bruxa") _hair = Hair();
        Face();
        // corpo base: Guerreiro bem mais forte (peito, braços, pernas); os outros só um pouco menos finos
        Bulk = raw ? 0f : classId switch { "guerreiro" => 0.045f, "bruxa" => 0f, "npc" => 0.012f, _ => 0.015f };
        BuildOutfit(null);
        SetWeapon(null);
    }

    private readonly Dictionary<Node3D, Basis> _socketBase = new();

    /// <summary>
    /// 12/11 ("a mão fica aberta segurando a arma — siga a vida real"): o boneco não tem dedos; a mão que segura algo
    /// "fecha" (o osso da mão encurta no comprimento dos dedos e engrossa, virando punho). O encaixe da arma desfaz essa
    /// escala, então a arma continua do tamanho e no ângulo certos.
    /// </summary>
    private void FecharMaos()
    {
        var dir = _weapon is not null;
        var esq = _weapon is not null && (GearFactory.IsTwoHanded(_weaponModel) || GearFactory.IsBow(_weaponModel) == true) || _shield is not null;
        Punho("RightHand", _handR, dir);
        Punho("LeftHand", _handL, esq);
    }

    private static readonly Vector3 EscalaPunho = new(1.08f, 0.58f, 1.14f);

    private void Punho(string bone, Node3D? holder, bool fechada)
    {
        var b = Bone(bone);
        if (b < 0) return;
        var sc = fechada ? EscalaPunho : Vector3.One;
        _skel.SetBonePoseScale(b, sc);
        if (holder is null) return;
        if (!_socketBase.TryGetValue(holder, out var base0)) _socketBase[holder] = base0 = holder.Transform.Basis;
        holder.Transform = new Transform3D(Basis.FromScale(new Vector3(1f / sc.X, 1f / sc.Y, 1f / sc.Z)) * base0, holder.Transform.Origin);
    }

    /// <summary>Ponto preso ao osso, sem a escala da armadura (o Mixamo usa 0,01) — peças em metros.</summary>
    private Node3D Socket(string bone)
    {
        var attach = new BoneAttachment3D { BoneName = _skel.GetBoneName(Bone(bone)) };
        _skel.AddChild(attach);
        var holder = new Node3D { Name = "Encaixe" + bone };
        attach.AddChild(holder);
        // eixos do encaixe = eixos do personagem na pose de referência (T): peças são feitas em metros com +Y cima e
        // +Z frente; a armadura do Mixamo costuma ter escala 0,01, que aqui é desfeita
        var s = _skelToRoot.Scale.X;
        var rest = _skel.GetBoneGlobalRest(Bone(bone)).Basis.Orthonormalized();
        var inRoot = _skelToRoot.Orthonormalized() * rest;
        holder.Transform = new Transform3D(inRoot.Inverse().Scaled(Vector3.One / (s > 1e-5f ? s : 1f)), Vector3.Zero);
        return holder;
    }

    /// <summary>
    /// Veste o pacote de destaque do NPC (Cal Lote 2: Assets/Models/npcs/npc_&lt;id&gt;.glb — chapéu, avental, rede, livro…):
    /// cada nó do arquivo tem o nome de um osso e já está no espaço do encaixe. false se o arquivo faltar.
    /// </summary>
    public bool VestirPacote(string id)
    {
        if (GlbCache.Instance("npcs/npc_" + id) is not { } pack) return false;
        var any = false;
        foreach (var ch in pack.GetChildren())
            if (ch is Node3D n3 && Bone(n3.Name) >= 0)
            {
                pack.RemoveChild(n3);
                Socket(n3.Name).AddChild(n3);
                n3.Transform = Transform3D.Identity;
                foreach (var mi in n3.FindChildren("*", nameof(MeshInstance3D), true, false))
                    if (mi is MeshInstance3D m3) m3.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
                any = true;
            }
        pack.QueueFree();
        return any;
    }

    private readonly Basis _skelToRoot, _toSkel;

    /// <summary>Base do nó "from" vista do ancestral "to" (sem precisar estar na árvore).</summary>
    private static Basis Relative(Node3D from, Node3D to)
    {
        var b = Basis.Identity;
        Node? n = from;
        while (n is Node3D n3 && n != to) { b = n3.Transform.Basis * b; n = n.GetParent(); }
        return b;
    }

    private int Bone(string name) => _bones.TryGetValue(name, out var i) ? i : -1;

    // ------------------------------------------------------------ equipamento

    // 16/11: orbe flutua acima da palma esquerda (ajuste fino pela vitrine "maoesquerda")
    internal static Vector3 OrbPos = new(0.30f, 0.06f, 0.02f), OrbRot = new(0, 0, -90);

    public void SetGear(string? weapon, string? offhand, string? body)
    {
        if (weapon != _weaponModel) SetWeapon(weapon);
        if (offhand != _offhandModel)
        {
            _offhandModel = offhand;
            _shield?.QueueFree();
            _segundaArma?.QueueFree();
            _segundaArma = null;
            _shield = GearFactory.Shield(offhand);
            if (_shield is not null)
            {
                _foreL.AddChild(_shield);
                _shield.Position = GearFactory.IsOrb(offhand) ? OrbPos : new Vector3(0.12f, 0.0f, 0.06f);
                _shield.RotationDegrees = GearFactory.IsOrb(offhand) ? OrbRot : new Vector3(0, 0, -90);
                _shieldCheck = true;
            }
            else if (offhand is not null && !offhand.StartsWith("shield_") && !offhand.StartsWith("orb") && !offhand.Contains("aljava")
                     && !GearFactory.IsBow(offhand) && !GearFactory.IsStaff(offhand) && GearFactory.Weapon(offhand) is { } segunda)
            {
                // 15/11 (duas armas do Guerreiro): a 2ª arma vai na mão esquerda, espelhando o encaixe da direita
                _segundaArma = segunda;
                _handL.AddChild(segunda);
                segunda.RotationDegrees = new Vector3(90, -40, 0);
                segunda.Position = new Vector3(0.08f, -0.02f, 0.02f);
            }
        }
        if (body != _bodyModel) BuildOutfit(body);
    }

    public void SetHead(string? model)
    {
        if (model == _headModel) return;
        _headModel = model;
        _helmet?.QueueFree();
        _helmet = null;
        // 12/11 ("tirei o capacete e ficou com uma cabecinha minúscula"): o capacete escondia o cabelo e, ao tirar, ele não
        // voltava — a cabeça ficava careca e pequena. Agora o cabelo volta (a não ser que a cabeça de bicho o esconda).
        if (_hair is not null && !_hiddenForAnimal.Contains(_hair)) _hair.Visible = true;
        // 11/11: capacete de conjunto (Meshy, com pele no esqueleto)
        SetArmorPart("capacete", IsArmorPack(model) ? model : null);
        if (IsArmorPack(model))
        {
            if (_hair is not null) _hair.Visible = false;
            return;
        }
        // 12/11: Máscara do Pandemônio (item de cabeça, qualquer classe): máscara de madeira verde no rosto, olhos e sorriso
        _helmet = model == "mascara_pandemonio" ? MascaraPandemonio() : GearFactory.Helmet(model);
        if (_helmet is null) return;
        if (_helmet.Name == "Mascara")
        {
            _headSocket.AddChild(_helmet);
            return;
        }
        _headSocket.AddChild(_helmet);
        // o elmo do boneco antigo foi feito para uma cabeça de ~0,30 m com centro em y=0
        // cabeça do manequim: centro ~0,10 m acima do osso, raio ~0,10 m (a calota do cabelo usa as mesmas medidas);
        // os elmos foram feitos para raio 0,15 com centro em 0,14 → escala 0,72 e sem deslocar para cima
        _helmet.Position = new Vector3(0, -0.005f, 0.004f);
        _helmet.Scale = Vector3.One * 0.72f;
        if (_hair is not null) _hair.Visible = model != "head_hood" && model is not ("head_iron" or "head_guardian");
        // capuz do Arqueiro: verde-floresta (o do Mago é violeta)
        if (_classId == "arqueiro" && model == "head_hood") Recolor(_helmet, new Color(0.20f, 0.30f, 0.17f));
    }

    /// <summary>
    /// 15/11 (dono mandou a referência certa: máscara de TRONCO — um pedaço de casca de árvore retangular, mais alto que largo,
    /// casca cinza-escura de um lado e marrom-clara do outro com uma rachadura no meio, dois buracos quadrados nos olhos e uma
    /// fenda deitada na boca, as bordas do corte cor de laranja). Tiras verticais de casca com relevo dão a textura.
    /// </summary>
    private static Node3D MascaraPandemonio()
    {
        var m = new Node3D { Name = "Mascara", Position = new Vector3(0, 0.085f, 0.078f) };
        Material Casca(Color c, float r = 0.95f) => new StandardMaterial3D { AlbedoColor = c, Roughness = r };
        var corte = Casca(new Color(0.86f, 0.5f, 0.22f), 0.8f);       // madeira exposta (borda laranja)
        var oco = Casca(new Color(0.03f, 0.025f, 0.02f), 1f);
        const float W = 0.19f, H = 0.27f, D = 0.03f;
        // miolo de madeira (aparece nas bordas e nos furos)
        VisualKit.Part(m, VisualKit.Box(W, H, D * 0.6f), corte, new Vector3(0, 0, -0.004f));
        // tiras de casca: lado esquerdo cinza-escuro, direito marrom-claro; alturas e relevos variados
        var rng = new RandomNumberGenerator { Seed = 1511 };
        const int tiras = 9;
        for (var i = 0; i < tiras; i++)
        {
            var x = -W / 2 + (i + 0.5f) * W / tiras;
            var esquerda = i < 4;
            var cor = esquerda ? new Color(0.30f, 0.30f, 0.29f).Lerp(new Color(0.48f, 0.47f, 0.43f), rng.Randf())
                               : new Color(0.55f, 0.45f, 0.33f).Lerp(new Color(0.70f, 0.60f, 0.46f), rng.Randf());
            var segs = 3 + rng.RandiRange(0, 2);
            var y = -H / 2;
            for (var k = 0; k < segs; k++)
            {
                var h = k == segs - 1 ? H / 2 - y : H / segs * rng.RandfRange(0.7f, 1.25f);
                if (y + h > H / 2) h = H / 2 - y;
                var prof = D * 0.5f + rng.RandfRange(0.002f, 0.012f);
                VisualKit.Part(m, VisualKit.Box(W / tiras * rng.RandfRange(0.85f, 1.05f), h * 0.97f, prof),
                    Casca(cor.Darkened(rng.RandfRange(0f, 0.15f))), new Vector3(x + rng.RandfRange(-0.002f, 0.002f), y + h / 2, D * 0.25f));
                y += h;
                if (y >= H / 2 - 0.001f) break;
            }
        }
        // rachadura laranja do meio (de cima até a boca, levemente torta)
        for (var k = 0; k < 6; k++)
            VisualKit.Part(m, VisualKit.Box(0.008f, 0.03f, 0.006f), corte, new Vector3(-0.005f + 0.004f * Mathf.Sin(k * 1.3f), 0.12f - k * 0.03f, D * 0.5f + 0.008f), new Vector3(0, 0, 8f * Mathf.Sin(k)));
        // olhos: buracos quadrados (fundo escuro + moldura de corte laranja)
        foreach (var (ex, ey, ew, eh) in new[] { (-0.045f, 0.035f, 0.045f, 0.033f), (0.05f, 0.045f, 0.05f, 0.033f) })
        {
            VisualKit.Part(m, VisualKit.Box(ew + 0.01f, eh + 0.01f, 0.006f), corte, new Vector3(ex, ey, D * 0.5f + 0.006f));
            VisualKit.Part(m, VisualKit.Box(ew, eh, 0.008f), oco, new Vector3(ex, ey, D * 0.5f + 0.008f));
        }
        // boca: fenda deitada um pouco abaixo e à direita do centro
        VisualKit.Part(m, VisualKit.Box(0.12f, 0.024f, 0.006f), corte, new Vector3(0.012f, -0.07f, D * 0.5f + 0.006f));
        VisualKit.Part(m, VisualKit.Box(0.11f, 0.014f, 0.008f), oco, new Vector3(0.012f, -0.07f, D * 0.5f + 0.008f));
        // leve curva para abraçar o rosto
        m.RotationDegrees = new Vector3(-4f, 0, 0);
        return m;
    }

    private static void Recolor(Node n, Color c)
    {
        if (n is MeshInstance3D mi)
        {
            var src = mi.MaterialOverride ?? (mi.Mesh?.GetSurfaceCount() > 0 ? mi.GetActiveMaterial(0) : null);
            if (src is StandardMaterial3D sm && sm.EmissionEnabled == false && sm.AlbedoColor.B > sm.AlbedoColor.G)
            {
                var copy = (StandardMaterial3D)sm.Duplicate();
                copy.AlbedoColor = new Color(c.R * (0.8f + sm.AlbedoColor.V), c.G * (0.8f + sm.AlbedoColor.V), c.B * (0.8f + sm.AlbedoColor.V));
                mi.MaterialOverride = copy;
            }
        }
        foreach (var ch in n.GetChildren()) Recolor(ch, c);
    }

    /// <summary>NPC: altura e largura do corpo (1 = manequim padrão). Gordo = girth &gt; 1.</summary>
    public void SetBuild(float height, float girth) => _body.Scale = new Vector3(girth, height, girth * (0.92f + 0.08f * girth));

    /// <summary>NPC: cabelo (0 = careca/capuz, 1 = longo, 2 = coque, 3 = curto) na cor dada.</summary>
    public void SetHairStyle(int style, Color color)
    {
        _hair?.QueueFree();
        _hair = null;
        if (style <= 0) return;
        var hair = new Node3D { Name = "Cabelo" };
        _headSocket.AddChild(hair);
        var m = VisualKit.Mat(color, 0.8f);
        var cap = VisualKit.Part(hair, VisualKit.Sphere(0.105f), m, new Vector3(0, 0.1f, -0.012f));
        cap.Scale = new Vector3(1.0f, style == 3 ? 0.9f : 1.05f, 1.08f);
        if (style == 1)
        {
            var back = VisualKit.Part(hair, VisualKit.Capsule(0.085f, 0.42f), m, new Vector3(0, -0.04f, -0.07f), new Vector3(12, 0, 0));
            back.Scale = new Vector3(1.15f, 1f, 0.55f);
        }
        else if (style == 2) VisualKit.Part(hair, VisualKit.Sphere(0.06f), m, new Vector3(0, 0.16f, -0.09f));
        _hair = hair;
    }

    /// <summary>12/11 (Capitão Fallen): barba curta e cheia (queixo, mandíbula e bigode), na cor dada.</summary>
    public void SetBarbaCurta(Color cor)
    {
        var m = VisualKit.Mat(cor, 0.85f);
        var queixo = VisualKit.Part(_headSocket, VisualKit.Sphere(0.042f), m, new Vector3(0, -0.035f, 0.07f));
        queixo.Scale = new Vector3(1.2f, 0.8f, 0.6f);
        foreach (var lado in new[] { -1f, 1f })
        {
            var jaw = VisualKit.Part(_headSocket, VisualKit.Capsule(0.018f, 0.085f), m, new Vector3(lado * 0.064f, -0.005f, 0.045f), new Vector3(-15, 0, lado * -22));
            jaw.Scale = new Vector3(1f, 1f, 0.85f);
            VisualKit.Part(_headSocket, VisualKit.Capsule(0.012f, 0.06f), m, new Vector3(lado * 0.024f, 0.012f, 0.1f), new Vector3(0, 0, lado * 75));   // bigode
        }
    }

    /// <summary>Fantasma (NPCs espectrais): pele e roupa azuladas e translúcidas, com brilho fraco.</summary>
    public void SetGhost()
    {
        foreach (var m in _baseMats)
        {
            m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            m.AlbedoColor = new Color(0.55f, 0.75f, 1f, 0.55f);
            m.EmissionEnabled = true;
            m.Emission = new Color(0.2f, 0.4f, 0.7f);
        }
        _ghost = true;
        BuildOutfit(_bodyModel);
    }
    private bool _ghost;

    /// <summary>Cabelo solto (o manequim feminino é careca): calota para trás da testa, mechas atrás das orelhas e
    /// cabelo longo nas costas; sem tiara (feedback: a franja cobria o rosto).</summary>
    /// <summary>13/11: traços das inimigas (medusa, esfinge, harpia, vampira) no manequim feminino.</summary>
    private void FemininaFeatures(HumanoidStyle s)
    {
        if (s.HairColor is { } hc && !s.SnakeHair)
        {
            var h = Hair();
            foreach (var mi in h.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                mi.MaterialOverride = VisualKit.Mat(hc, 0.7f);
        }
        if (s.SnakeHair)
        {
            // serpentes: 9 cobrinhas saindo da cabeça, cada uma curvada para fora, com cabeça e olhinhos amarelos
            var verde = VisualKit.Mat(new Color(0.22f, 0.42f, 0.18f), 0.5f);
            var olho = VisualKit.Glow(new Color(1f, 0.85f, 0.1f), 2f);
            for (var i = 0; i < 9; i++)
            {
                var a = i / 9f * Mathf.Tau;
                var raiz = new Node3D { Position = new Vector3(Mathf.Sin(a) * 0.075f, 0.16f, Mathf.Cos(a) * 0.06f - 0.02f),
                    RotationDegrees = new Vector3(Mathf.Cos(a) * 55f, 0, -Mathf.Sin(a) * 55f) };
                _headSocket.AddChild(raiz);
                var seg1 = VisualKit.Part(raiz, VisualKit.Cylinder(0.014f, 0.02f, 0.12f, 6), verde, new Vector3(0, 0.06f, 0));
                var seg2 = VisualKit.Part(seg1, VisualKit.Cylinder(0.011f, 0.014f, 0.1f, 6), verde, new Vector3(0, 0.1f, 0.02f), new Vector3(35, 0, 0));
                var cab = VisualKit.Part(seg2, VisualKit.Sphere(0.022f), verde, new Vector3(0, 0.06f, 0.01f), null, new Vector3(0.9f, 0.7f, 1.4f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.006f), olho, new Vector3(sd * 0.012f, 0.008f, 0.015f));
            }
        }
        if (s.Nemes)
        {
            // toucado de faraó: listras azul e ouro caindo atrás das orelhas até os ombros, cobra de ouro na testa
            var ouro = VisualKit.Mat(new Color(0.9f, 0.7f, 0.25f), 0.3f, 0.9f);
            var azul = VisualKit.Mat(new Color(0.12f, 0.2f, 0.55f), 0.5f);
            VisualKit.Part(_headSocket, VisualKit.Sphere(0.118f), ouro, new Vector3(0, 0.125f, -0.02f), null, new Vector3(1.05f, 0.8f, 1.1f));
            for (var i = 0; i < 3; i++)   // listras azuis em volta do toucado
                VisualKit.Part(_headSocket, new TorusMesh { InnerRadius = 0.105f - i * 0.012f, OuterRadius = 0.118f - i * 0.012f, Rings = 18, RingSegments = 6 }, azul,
                    new Vector3(0, 0.12f + i * 0.035f, -0.02f), new Vector3(-8, 0, 0));
            foreach (var sd in new[] { -1f, 1f })
            {
                var aba = VisualKit.Part(_headSocket, VisualKit.Box(0.08f, 0.24f, 0.05f), ouro, new Vector3(sd * 0.11f, -0.02f, 0.02f), new Vector3(0, 0, sd * 8f));
                for (var i = 0; i < 4; i++) VisualKit.Part(aba, VisualKit.Box(0.085f, 0.02f, 0.055f), azul, new Vector3(0, -0.08f + i * 0.055f, 0));
            }
            VisualKit.Part(_headSocket, VisualKit.Cylinder(0.006f, 0.012f, 0.06f, 6), ouro, new Vector3(0, 0.2f, 0.11f), new Vector3(20, 0, 0));
        }
        if (s.Talons)
        {
            var garra = VisualKit.Mat(new Color(0.15f, 0.13f, 0.1f), 0.4f);
            var pena = VisualKit.Mat(s.Cloth, 0.85f);
            foreach (var side in new[] { "Left", "Right" })
            {
                var pe = Socket(side + "Foot");
                for (var i = -1; i <= 1; i++)
                    VisualKit.Part(pe, VisualKit.Cylinder(0.0f, 0.014f, 0.09f, 5), garra, new Vector3(i * 0.03f, 0.0f, 0.12f), new Vector3(100, i * 12f, 0));
                var braco = Socket(side + "ForeArm");
                for (var i = 0; i < 4; i++)
                    VisualKit.Part(braco, VisualKit.Box(0.02f, 0.16f, 0.06f), pena, new Vector3(0, 0.04f + i * 0.05f, -0.05f), new Vector3(-30, 0, 0));
            }
        }
    }

    private Node3D Hair()
    {
        var hair = new Node3D { Name = "Cabelo" };
        _headSocket.AddChild(hair);
        var m = VisualKit.Mat(new Color(0.28f, 0.12f, 0.07f), 0.75f);
        var cap = VisualKit.Part(hair, VisualKit.Sphere(0.108f), m, new Vector3(0, 0.13f, -0.05f));
        cap.Scale = new Vector3(1.0f, 1.0f, 1.0f);
        var back = VisualKit.Part(hair, VisualKit.Capsule(0.085f, 0.42f), m, new Vector3(0, -0.04f, -0.08f), new Vector3(12, 0, 0));
        back.Scale = new Vector3(1.15f, 1f, 0.55f);
        // mechas laterais atrás das orelhas, longe do rosto
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(hair, VisualKit.Capsule(0.028f, 0.22f), m, new Vector3(side * 0.092f, 0.02f, -0.045f), new Vector3(8, 0, side * 6));
        return hair;
    }

    /// <summary>Traços do rosto por classe: barba branca no Mago, nariz e queixo pontudo no Arqueiro.</summary>
    private void Face()
    {
        // mesmo material da pele do manequim (cor e brilho iguais aos da cabeça)
        Material skin = _baseMats.Count > 0 ? _baseMats[0] : VisualKit.Mat(new Color(0.5f, 0.33f, 0.21f), 0.5f);
        switch (_classId)
        {
            case "mago":
            {
                var white = VisualKit.Mat(new Color(0.90f, 0.89f, 0.86f), 0.95f);
                // barba branca de mago: nasce na mandíbula (abaixo da boca), volume no queixo e ponta longa caindo sobre
                // o peito; bigode fino logo acima da boca. Nada acima da linha da boca (o rosto fica livre).
                // (feedback: a barba ficava no meio da cara) → tudo abaixo da boca, rente ao queixo e caindo no peito
                var chin = VisualKit.Part(_headSocket, VisualKit.Sphere(0.05f), white, new Vector3(0, -0.035f, 0.07f));
                chin.Scale = new Vector3(1.15f, 1.05f, 0.65f);
                var tip = VisualKit.Part(_headSocket, VisualKit.Capsule(0.04f, 0.2f), white, new Vector3(0, -0.12f, 0.08f), new Vector3(14, 0, 0));
                tip.Scale = new Vector3(1.1f, 1f, 0.6f);
                foreach (var side in new[] { -1f, 1f })
                {
                    // costeleta fina pela linha da mandíbula, atrás e abaixo da bochecha
                    var jaw = VisualKit.Part(_headSocket, VisualKit.Capsule(0.018f, 0.08f), white, new Vector3(side * 0.06f, -0.01f, 0.045f), new Vector3(-20, 0, side * -28));
                    jaw.Scale = new Vector3(1f, 1f, 0.8f);
                    // bigode fino caindo dos cantos da boca
                    VisualKit.Part(_headSocket, VisualKit.Capsule(0.007f, 0.045f), white, new Vector3(side * 0.02f, 0.005f, 0.098f), new Vector3(0, 0, side * 70));
                }
                break;
            }
            case "arqueiro":
                // nariz fino para a frente e queixo pontudo
                // nariz: cunha vertical (prisma de 3 lados) com a aresta para a frente; queixo em ponta para baixo e à frente
                var nose = VisualKit.Part(_headSocket, VisualKit.Cylinder(0.003f, 0.02f, 0.055f, 3), skin, new Vector3(0, 0.072f, 0.096f), new Vector3(-14, 0, 0));
                nose.RotateObjectLocal(Vector3.Up, Mathf.Pi);
                var point = VisualKit.Part(_headSocket, VisualKit.Cylinder(0.04f, 0.008f, 0.06f, 6), skin, new Vector3(0, -0.035f, 0.055f), new Vector3(14, 0, 0));
                point.Scale = new Vector3(1f, 1f, 0.8f);
                break;
        }
    }

    private Node3D? _hair;

    private Node3D? _quiver;

    /// <summary>11/11 noite (complemento do pacote): aljava nas costas do arqueiro quando ele está de arco (qualidade do arco).</summary>
    private void SetQuiver(string? model)
    {
        _quiver?.QueueFree();
        _quiver = null;
        if (model is null || !GearFactory.IsBow(model)) return;
        var q = model.Contains("excelente") ? "excelente" : model.Contains("refinad") ? "refinada" : "comum";
        if (GlbCache.Instance("armas/arma_aljava_" + q) is not { } aljava) return;
        // origem do modelo = alça: presa no alto das costas, inclinada (boca para cima, sobre o ombro direito)
        _quiver = new Node3D { Name = "Aljava", Position = new Vector3(-0.08f, 0.02f, -0.13f), RotationDegrees = new Vector3(8f, 0f, 28f) };
        _quiver.AddChild(aljava);
        Socket("Spine2").AddChild(_quiver);
    }

    private void SetWeapon(string? model)
    {
        SetQuiver(model);
        _weaponRest = null; _carry = 0f;
        _weaponModel = model;
        _weapon?.QueueFree();
        _weapon = null;
        _focus?.QueueFree();
        _focus = null;
        if (model is not null && GearFactory.IsBow(model)) _weapon = Bow(model);
        else if (model is not null && model.StartsWith("talisman_")) _weapon = Talisman(model);
        else _weapon = GearFactory.Weapon(model);
        if (_weapon is not null)
        {
            // encaixes na pose T (braço esticado para o lado): o que fica ao longo do braço vira vertical com o braço baixo
            var bow = GearFactory.IsBow(model);
            var talisman = model!.StartsWith("talisman_");
            (bow ? _handL : _handR).AddChild(_weapon);
            if (bow) { _bowInHand = true; SlingBow(false); }
            else if (GearFactory.IsStaff(model) || model!.Contains("lanca") || model.StartsWith("spear") || model.StartsWith("arma_lend_foice") || model.StartsWith("arma_lend_garfo")) { _weapon.RotationDegrees = new Vector3(0, 0, -90); _weapon.Position = new Vector3(-0.08f, 0, 0.03f); }
            else if (talisman) { _weapon.RotationDegrees = new Vector3(0, 0, 90); _weapon.Position = new Vector3(-0.06f, 0, 0); }
            // 14/11 (dono: "a manopla tem que entrar na mão, tipo a do Thanos — não segurar como item de bater"): vestida,
            // ao longo da mão, cobrindo os dedos e o punho
            else if (model.StartsWith("arma_lend_manopla")) { _weapon.RotationDegrees = new Vector3(0, 0, 90); _weapon.Position = new Vector3(0, 0.02f, 0); _weapon.Scale = Vector3.One * 1.15f; }
            else { _weapon.RotationDegrees = new Vector3(90, 40, 0); _weapon.Position = new Vector3(-0.08f, -0.02f, 0.02f); }

            _weaponSpan = bow || talisman ? Vector2.Zero : MeasureSpan(_weapon);
            _handRot = _weapon.RotationDegrees; _handPos = _weapon.Position; _sheathed = false;
        }
        // Bruxa sem cajado: névoa esmeralda e dourada nas mãos (mais forte com talismã)
        if (_classId == "bruxa")
        {
            _focus = new Node3D { Name = "Nevoa" };
            _handL.AddChild(_focus);
            _focus.Position = new Vector3(0, 0.1f, 0.03f);
            var glow = VisualKit.Glow(new Color(0.30f, 0.95f, 0.55f), _weapon is null ? 1.2f : 2.2f);
            var gold = VisualKit.Glow(new Color(1f, 0.80f, 0.35f), 1.6f);
            for (var i = 0; i < 4; i++)
            {
                var a = i * Mathf.Tau / 4f;
                VisualKit.Part(_focus, VisualKit.Sphere(i % 2 == 0 ? 0.022f : 0.015f), i % 2 == 0 ? glow : gold,
                    new Vector3(Mathf.Cos(a) * 0.07f, Mathf.Sin(a) * 0.05f, Mathf.Sin(a) * 0.07f));
            }
        }
        if (_legendLight is not null) { _legendLight.Reparent(_handR, false); }
    }

    private bool _bowInHand;
    private float _meleeHold = 6f;
    private bool _sheathed;
    private Vector3 _handRot, _handPos;

    private void SheatheMelee(bool back)
    {
        if (_weapon is null || _classId == "monstro" || GearFactory.IsBow(_weaponModel) || _weaponModel?.StartsWith("talisman_") == true
            || _weaponModel?.StartsWith("arma_lend_manopla") == true || back == _sheathed) return;
        if (!back && _weapon.GetParent() != _back && _weapon.GetParent() != _handR) return;
        _sheathed = back;
        _sheathT = SheathLen;   // 11/11 noite: o braço vai até o ombro buscar/guardar a arma
        _back ??= Socket("Spine2");
        _weapon.GetParent()?.RemoveChild(_weapon);
        if (back)
        {
            _back.AddChild(_weapon);
            // atravessada nas costas, cabo para cima perto do ombro direito
            _weapon.RotationDegrees = new Vector3(0, 0, 155);
            _weapon.Position = new Vector3(0.12f, 0.32f, -0.14f);
        }
        else
        {
            _handR.AddChild(_weapon);
            _weapon.RotationDegrees = _handRot;
            _weapon.Position = _handPos;
        }
        if (_legendLight is not null) _legendLight.Reparent(_weapon.GetParent()!, false);
    }
    private Node3D? _back;
    private float _sheathT;
    private const float SheathLen = 0.45f;

    /// <summary>Arco nas costas (andando) ou na mão esquerda, em pé, na hora de atirar.</summary>
    private void SlingBow(bool inHand)
    {
        if (_weapon is null || GearFactory.IsBow(_weaponModel) != true || inHand == _bowInHand) return;
        _bowInHand = inHand;
        _back ??= Socket("Spine2");
        _weapon.GetParent()?.RemoveChild(_weapon);
        if (inHand)
        {
            _handL.AddChild(_weapon);
            _weapon.RotationDegrees = new Vector3(0, 90, 0);      // em pé, barriga do arco para a frente do braço
            _weapon.Position = new Vector3(0.08f, 0, 0.02f);
        }
        else
        {
            _back.AddChild(_weapon);
            _weapon.RotationDegrees = new Vector3(0, 180, 35);    // atravessado nas costas, colado (antes ficava "voando")
            _weapon.Position = new Vector3(0, 0.05f, -0.1f);
        }
    }

    public static Node3D BowModel(string model) => Bow(model);

    /// <summary>Posição (no mundo) de um osso — para enquadrar o boneco no ícone da armadura.</summary>
    public Vector3 BoneWorld(string bone)
    {
        var b = Bone(bone);
        return b < 0 ? Root.GlobalPosition : _skel.GlobalTransform * _skel.GetBoneGlobalPose(b).Origin;
    }
    public static Node3D TalismanModel(string model) => Talisman(model);

    /// <summary>11/11 (arsenal da noite): arcos e talismãs esculpidos; null = fica o desenho de formas simples.</summary>
    internal static string? ArsenalFile(string model) => model switch
        {
            "bow_twig" => "arma_arco_curto_enferrujada",
            "bow_short" => "arma_arco_curto_comum",
            "bow_bone" => "arma_arco_curto_refinada",
            "bow_yew" => "arma_arco_longo_comum",
            "bow_vein" => "arma_arco_longo_excelente",
            "talisman_shell" or "talisman_bone" => "arma_talisma_bruxa_n1",
            "talisman_coral" or "talisman_moon" => "arma_talisma_bruxa_n2",
            "talisman_vein" => "arma_talisma_bruxa_n3",
            _ when model.StartsWith("arma_arco_") || model.StartsWith("arma_lend_arco_") => model,
            _ => null,
        };

    private static Node3D? ArsenalGlb(string model, Vector3 offset, Vector3 rotationDeg)
    {
        var file = ArsenalFile(model);
        if (file is null || GlbCache.Instance("armas/" + file) is not { } glb) return null;
        var root = new Node3D { Name = file };
        root.AddChild(glb);
        glb.Position = offset;
        glb.RotationDegrees = rotationDeg;
        return root;
    }

    private static Node3D Bow(string model)
    {
        // o arco esculpido tem a pegada na origem; o de formas simples, 14 cm à frente (barriga do arco)
        if (ArsenalGlb(model, new Vector3(0, 0, 0.14f), Vector3.Zero) is { } sculpted) return sculpted;
        var root = new Node3D { Name = "Arco" };
        var wood = VisualKit.Mat(model switch
        {
            "bow_vein" => new Color(0.18f, 0.20f, 0.24f), "bow_yew" => new Color(0.42f, 0.26f, 0.13f), "bow_bone" => new Color(0.86f, 0.82f, 0.70f),
            _ => new Color(0.48f, 0.36f, 0.22f),
        }, 0.7f, model == "bow_vein" ? 0.4f : 0f);
        var grip = VisualKit.Mat(new Color(0.20f, 0.12f, 0.08f), 0.9f);
        var length = model is "bow_twig" or "bow_short" ? 1.1f : 1.35f;
        // arco curvo: segmentos em arco (corda reta presa nas pontas)
        const int n = 8;
        var prev = Vector3.Zero;
        for (var i = 0; i <= n; i++)
        {
            var t = i / (float)n * 2f - 1f;                 // -1..1
            var p = new Vector3(0, t * length / 2f, (1f - t * t) * 0.16f - 0.02f);
            if (i > 0)
            {
                var mid = (p + prev) / 2f;
                var seg = VisualKit.Part(root, VisualKit.Cylinder(0.014f + 0.01f * (1f - Mathf.Abs(t)), 0.014f + 0.01f * (1f - Mathf.Abs(t)), (p - prev).Length() * 1.05f, 6), wood, mid);
                seg.LookAtFromPosition(mid, p, Vector3.Right);
                seg.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);
            }
            prev = p;
        }
        VisualKit.Part(root, VisualKit.Cylinder(0.026f, 0.026f, 0.13f, 8), grip, new Vector3(0, 0, 0.14f));
        VisualKit.Part(root, VisualKit.Cylinder(0.003f, 0.003f, length, 4), VisualKit.Mat(new Color(0.85f, 0.82f, 0.72f), 0.6f), new Vector3(0, 0, -0.02f));
        if (model == "bow_vein")
            VisualKit.Part(root, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(0.35f, 0.85f, 0.95f), 2f), new Vector3(0, 0.08f, 0.15f));
        return root;
    }

    private static Node3D Talisman(string model)
    {
        // o talismã esculpido fica pendurado da mão como o antigo (de ponta-cabeça: o cordão para cima)
        if (ArsenalGlb(model, Vector3.Zero, new Vector3(180, 0, 0)) is { } sculpted) return sculpted;
        var root = new Node3D { Name = "Talisma" };
        var cord = VisualKit.Mat(new Color(0.25f, 0.17f, 0.10f), 0.9f);
        var charm = model switch
        {
            "talisman_coral" => VisualKit.Mat(new Color(0.85f, 0.42f, 0.36f), 0.5f),
            "talisman_shell" => VisualKit.Mat(new Color(0.88f, 0.84f, 0.74f), 0.5f),
            "talisman_vein" => VisualKit.Glow(new Color(0.30f, 0.95f, 0.60f), 1.8f),
            "talisman_moon" => VisualKit.Glow(new Color(0.75f, 0.85f, 1f), 2.2f),
            _ => VisualKit.Mat(new Color(0.80f, 0.76f, 0.64f), 0.7f),
        };
        VisualKit.Part(root, VisualKit.Cylinder(0.004f, 0.004f, 0.16f, 4), cord, new Vector3(0, -0.08f, 0));
        VisualKit.Part(root, VisualKit.Sphere(0.035f), charm, new Vector3(0, -0.17f, 0));
        VisualKit.Part(root, VisualKit.Box(0.012f, 0.06f, 0.012f), VisualKit.Mat(new Color(0.85f, 0.68f, 0.28f), 0.4f, 0.7f), new Vector3(0, -0.23f, 0));
        return root;
    }

    // ------------------------------------------------------------ roupas (camadas na malha)

    private sealed record Layer(int Regions, float YMin, float YMax, Color Cloth, Color Trim, float Grow, float Rough = 0.85f,
        float Metal = 0f, int Pattern = 0, Color? Emit = null, Color? Camisa = null);
    private const float SapoOsso = 0.72f;

    /// <summary>Fio liso (bigode, lábio): cilindros entre os pontos + bolinha em cada junta.</summary>
    private static void Corda(Node3D pai, IReadOnlyList<Vector3> pts, Func<int, float> raio, Material mat)
    {
        for (var i = 0; i < pts.Count; i++)
        {
            VisualKit.Part(pai, VisualKit.Sphere(raio(i)), mat, pts[i]);
            if (i == 0) continue;
            var d = pts[i] - pts[i - 1];
            if (d.Length() < 1e-5f) continue;
            var cil = VisualKit.Cylinder(raio(i), raio(i - 1), d.Length() * 1.05f, 12);
            cil.CapTop = false; cil.CapBottom = false;
            var mi = VisualKit.Part(pai, cil, mat, (pts[i] + pts[i - 1]) * 0.5f);
            var yv = d.Normalized();
            var xv = yv.Cross(Mathf.Abs(yv.Z) < 0.9f ? Vector3.Back : Vector3.Right).Normalized();
            var zv = xv.Cross(yv).Normalized();
            mi.Transform = new Transform3D(new Basis(xv, yv, zv), mi.Position);
        }
    }

    /// <summary>Rosto do skin sapo (referência do dono 18/11 21h). Cabeça = elipsoide; tudo é posto na superfície dela.</summary>
    private static void SapoRosto(Node3D cab)
    {
        static StandardMaterial3D Mt(Color c, float r) => VisualKit.Mat(c, r, 0f);
        var verde = Mt(new Color(0.36f, 0.6f, 0.24f), 0.6f);
        var verdeEsc = Mt(new Color(0.2f, 0.38f, 0.14f), 0.65f);
        var papo = Mt(new Color(0.8f, 0.83f, 0.55f), 0.7f);
        var escuro = Mt(new Color(0.07f, 0.05f, 0.04f), 0.55f);
        var preto = Mt(new Color(0.02f, 0.02f, 0.02f), 0.35f);
        const float cz = 0.02f, ax = 0.1755f, by = 0.1107f, cz2 = 0.155f;
        // focinho: o queixo do boneco fica mais à frente que a cabeça redonda — o focinho largo cobre o queixo
        static float Elip(float x, float y, Vector3 c, Vector3 r) { var k = 1f - (x - c.X) * (x - c.X) / (r.X * r.X) - (y - c.Y) * (y - c.Y) / (r.Y * r.Y); return k <= 0 ? -1f : c.Z + r.Z * Mathf.Sqrt(k); }
        static float Sup(float x, float y) => Mathf.Max(Mathf.Max(Elip(x, y, new Vector3(0, 0, cz), new Vector3(ax, by, cz2)), Elip(x, y, new Vector3(0, -0.04f, 0.1f), new Vector3(0.15f, 0.062f, 0.118f))), 0.05f);
        VisualKit.Part(cab, VisualKit.Sphere(0.135f), verde, new Vector3(0, 0, cz), null, new Vector3(1.3f, 0.82f, 1.15f));        // cabeça larga
        VisualKit.Part(cab, VisualKit.Sphere(0.125f), verdeEsc, new Vector3(0, 0.02f, -0.03f), null, new Vector3(1.25f, 0.8f, 1f));   // nuca mais escura
        VisualKit.Part(cab, VisualKit.Sphere(0.1f), verde, new Vector3(0, -0.04f, 0.1f), null, new Vector3(1.5f, 0.62f, 1.18f));       // focinho largo
        VisualKit.Part(cab, VisualKit.Sphere(0.12f), papo, new Vector3(0, -0.094f, 0.07f), null, new Vector3(1.22f, 0.42f, 1.2f));    // papo claro
        var miudo = new Node3D { Name = "SemEncaixe" }; cab.AddChild(miudo);
        cab = miudo;   // daqui para baixo (olhos, boca, bigode) fica onde foi desenhado
        // boca: lábio grosso de orelha a orelha, cantos caídos (cara emburrada)
        var lab = new List<Vector3>();
        for (var i = 0; i <= 30; i++)
        {
            var x = -0.14f + i * 0.28f / 30f;
            var y = -0.05f - 0.04f * Mathf.Pow(Mathf.Abs(x) / 0.14f, 2.0f);
            lab.Add(new Vector3(x, y, Sup(x, y) + 0.002f));
        }
        Corda(cab, lab, k => 0.0048f * (1f - 0.35f * Mathf.Abs(k - 15) / 15f), escuro);
        foreach (var sd in new[] { -1f, 1f })
        {
            // olhão saltado no alto, íris amarelo-esverdeada
            var olho = new Vector3(sd * 0.088f, 0.078f, 0.085f);
            VisualKit.Part(cab, VisualKit.Sphere(0.052f), verde, olho - new Vector3(0, 0.004f, 0.012f));                                      // calombo
            VisualKit.Part(cab, VisualKit.Sphere(0.047f), Mt(new Color(0.9f, 0.84f, 0.32f), 0.2f), olho);                                   // íris
            VisualKit.Part(cab, VisualKit.Sphere(0.016f), preto, olho + new Vector3(sd * 0.004f, -0.008f, 0.043f), null, new Vector3(1.6f, 0.55f, 0.5f));   // pupila deitada
            VisualKit.Part(cab, VisualKit.Sphere(0.005f), VisualKit.Glow(Colors.White, 1.2f), olho + new Vector3(-sd * 0.012f, 0.004f, 0.045f));          // brilho
            // pálpebra pesada: meia esfera verde inclinada para a frente cobre a metade de cima do olho
            var lid = new SphereMesh { Radius = 0.051f, Height = 0.051f, IsHemisphere = true, RadialSegments = 20, Rings = 8 };
            VisualKit.Part(cab, lid, verde, olho, new Vector3(-6, 0, sd * -10));
            VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.046f, OuterRadius = 0.054f, Rings = 20, RingSegments = 6 }, verdeEsc, olho, new Vector3(-6, 0, sd * -10));   // borda da pálpebra
            // narina
            VisualKit.Part(cab, VisualKit.Sphere(0.007f), escuro, new Vector3(sd * 0.024f, 0.012f, Sup(sd * 0.024f, 0.012f)));
            // bigode preto: desce da narina para a bochecha e enrola a ponta para cima
            var big = new List<Vector3>();
            var raios = new List<float>();
            for (var i = 0; i <= 28; i++)
            {
                var t = i / 28f;
                float x, y;
                if (t < 0.75f) { var u = t / 0.75f; x = 0.012f + 0.105f * u; y = -0.014f - 0.018f * Mathf.Sin(u * Mathf.Pi * 0.85f) + 0.014f * u * u; }
                else { var u = (t - 0.75f) / 0.25f; var a = -Mathf.Pi * 0.5f + u * Mathf.Pi * 1.5f; x = 0.117f + 0.014f * Mathf.Cos(a) - 0.0f; y = -0.004f + 0.014f * Mathf.Sin(a) + 0.006f; }
                var p = new Vector3(sd * x, y, Sup(x, y) + 0.006f);
                big.Add(p); raios.Add(0.0125f * (1f - 0.7f * t));
            }
            for (var it = 0; it < 2; it++)   // suaviza a profundidade (o focinho e a cabeça se encontram num vinco)
                for (var k = 1; k < big.Count - 1; k++)
                    big[k] = new Vector3(big[k].X, big[k].Y, Mathf.Max(big[k].Z, (big[k - 1].Z + big[k + 1].Z) * 0.5f));
            Corda(cab, big, k => raios[k], preto);
        }
    }

    // 17/11 (dono: "a roupa ainda tá voando, desgolada — sapo, gravata, cartola"): Pattern 5 = terno com camisa em V e gravata,
    // 6 = fraque com peitilho e gravata-borboleta. PINTADOS na própria roupa (que deforma com o corpo): nunca descolam.
    private const int PTerno = 5, PFraque = 6;

    private static readonly Color Leather = new(0.30f, 0.19f, 0.12f), LeatherDark = new(0.17f, 0.11f, 0.07f), Iron = new(0.48f, 0.51f, 0.56f),
        Bordo = new(0.39f, 0.18f, 0.22f), Bronze = new(0.66f, 0.48f, 0.26f), Violet = new(0.30f, 0.18f, 0.38f), Moss = new(0.16f, 0.30f, 0.20f),
        Forest = new(0.19f, 0.27f, 0.15f), Linen = new(0.62f, 0.55f, 0.42f), Gold = new(0.85f, 0.68f, 0.30f), Emerald = new(0.12f, 0.36f, 0.24f);

    private void BuildOutfit(string? body)
    {
        _bodyModel = body;
        foreach (var n in _outfit) n.QueueFree();
        _outfit.Clear();
        _cape = null;
        _skirt = null;
        // 11/11: peitoral de conjunto (Meshy): a malha 3D com pele substitui as camadas de roupa
        var vestido = _skinId is null ? body : null;   // 13/11: com skin, a roupa inteira é a da skin
        SetArmorPart("peitoral", IsArmorPack(vestido) ? vestido : null);
        if (IsArmorPack(vestido)) { ReapplyItemCape(); return; }
        var layers = new List<Layer>();
        string? extras = null;   // 13/11: peças 3D por cima das camadas (alamares do gibão...)
        float skirt = 0f; Color skirtColor = default, skirtTrim = default;
        (Color Cloth, Color Trim, bool Glow)? mantle = null;
        (Color Cloth, Color Trim)? robe = null;
        var vein = body?.EndsWith("_vein") == true;
        switch (_skinId is not null ? "skin_" + _skinId : body)
        {
            // 13/11 (pedido do dono: "a skin passa a ser a aparência INTEIRA — pelado ou de armadura, aparece só a skin"):
            // roupa de corpo inteiro em camadas sobre o NOSSO boneco; os enfeites (orelhas, chapéu...) vêm do SetSkin
            case "skin_coelho":
            {
                var pelo = new Color(0.94f, 0.93f, 0.91f);
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, pelo, new Color(0.86f, 0.84f, 0.82f), 0.014f, 1f, Pattern: 1));
                // (barriga: disco claro no SetSkin — a faixa rosada parecia cinto)
                break;
            }
            case "skin_gravata":
            {
                var terno = new Color(0.13f, 0.15f, 0.24f);
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, terno, new Color(0.72f, 0.06f, 0.1f), 0.012f, 0.55f, Pattern: PTerno, Camisa: new Color(0.95f, 0.94f, 0.9f)));
                layers.Add(Boots(new Color(0.05f, 0.05f, 0.05f)));
                break;
            }
            case "skin_cartola":
            {
                var fraque = new Color(0.07f, 0.07f, 0.08f);
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, fraque, new Color(0.04f, 0.04f, 0.05f), 0.012f, 0.45f, Pattern: PFraque, Camisa: new Color(0.96f, 0.95f, 0.92f)));
                layers.Add(new Layer(Hand, 0f, 1f, new Color(0.97f, 0.97f, 0.95f), new Color(0.9f, 0.9f, 0.88f), 0.01f, 0.7f));   // luvas brancas
                // 17/11: a faixa caía na virilha, serrilhada (dono: "detalhes não encaixados") — saiu
                // layers.Add(new Layer(Belly, 0.545f, 0.575f, new Color(0.55f, 0.05f, 0.08f), Gold, 0.02f, 0.4f));                  // faixa
                layers.Add(Boots(new Color(0.04f, 0.04f, 0.04f)));
                break;
            }
            case "skin_palhaco":
            {
                layers.Add(new Layer(Head, 0f, 1f, new Color(0.98f, 0.97f, 0.96f), new Color(0.95f, 0.9f, 0.9f), 0.006f, 0.5f));   // cara branca
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.98f, 0.82f, 0.12f), new Color(0.2f, 0.45f, 1f), 0.016f, 0.9f, Pattern: 2));
                layers.Add(new Layer(Hand, 0f, 1f, new Color(0.97f, 0.97f, 0.95f), Gold, 0.012f, 0.7f));
                layers.Add(Boots(new Color(0.85f, 0.08f, 0.08f)));
                break;
            }
            // 14/11 ("gostei do bracelete, tem que ter mais skins")
            case "skin_chef":
            {
                var dolma = new Color(0.97f, 0.97f, 0.95f);
                layers.Add(new Layer(Chest | Belly | Arm | Fore, 0f, 1f, dolma, new Color(0.88f, 0.88f, 0.86f), 0.014f, 0.8f));
                layers.Add(new Layer(Thigh | Shin, 0f, 1f, new Color(0.15f, 0.15f, 0.17f), new Color(0.9f, 0.9f, 0.9f), 0.012f, 0.6f, Pattern: 2));   // calça xadrez
                layers.Add(Boots(new Color(0.08f, 0.08f, 0.08f)));
                break;
            }
            case "skin_pirata":
            {
                layers.Add(new Layer(Chest | Belly | Arm | Fore, 0f, 1f, new Color(0.95f, 0.93f, 0.86f), new Color(0.85f, 0.82f, 0.75f), 0.012f, 0.8f));   // camisa
                layers.Add(new Layer(Chest | Belly, 0f, 1f, new Color(0.32f, 0.18f, 0.1f), new Color(0.2f, 0.11f, 0.06f), 0.02f, 0.7f));                    // colete
                layers.Add(new Layer(Thigh | Shin, 0f, 1f, new Color(0.9f, 0.88f, 0.82f), new Color(0.65f, 0.1f, 0.1f), 0.014f, 0.8f, Pattern: 2));        // calça listrada
                // 17/11: faixa na virilha — saiu  layers.Add(new Layer(Belly, 0.52f, 0.58f, new Color(0.7f, 0.08f, 0.08f), Gold, 0.024f, 0.7f));                                               // faixa
                layers.Add(Boots(new Color(0.12f, 0.08f, 0.05f)));
                break;
            }
            case "skin_abobora":
            {
                var terno = new Color(0.12f, 0.1f, 0.12f);
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, terno, new Color(0.35f, 0.2f, 0.08f), 0.014f, 0.8f, Pattern: 1));
                layers.Add(new Layer(Hand, 0f, 1f, new Color(0.45f, 0.3f, 0.12f), new Color(0.3f, 0.2f, 0.1f), 0.012f, 0.9f));   // luvas de palha
                layers.Add(Boots(new Color(0.18f, 0.12f, 0.06f)));
                break;
            }
            case "skin_lata":
            {
                var lata = new Color(0.62f, 0.64f, 0.66f);
                layers.Add(new Layer(Arm | Fore | Thigh | Shin, 0f, 1f, lata, new Color(0.45f, 0.47f, 0.5f), 0.016f, 0.35f));
                layers.Add(new Layer(Chest | Belly, 0f, 1f, new Color(0.4f, 0.3f, 0.2f), new Color(0.3f, 0.22f, 0.14f), 0.014f, 0.9f));   // gibão por baixo da panela
                layers.Add(Boots(new Color(0.5f, 0.52f, 0.55f)));
                break;
            }
            // ---------------- 14/11: as 24 skins da prancha do dono (roupa de corpo inteiro; enfeites no SetSkin)
            case "skin_executor":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.95f, 0.94f, 0.92f), new Color(0.85f, 0.84f, 0.82f), 0.014f, 1f, Pattern: 1));
                // 17/11: faixa na virilha — saiu  layers.Add(new Layer(Belly, 0.52f, 0.58f, new Color(0.25f, 0.15f, 0.1f), Bronze, 0.024f, 0.6f));
                mantle = (new Color(0.62f, 0.06f, 0.08f), new Color(0.35f, 0.03f, 0.05f), false);
                break;
            case "skin_gnomo":
                // 14/11 tarde (dono: "o Yong parece de vestido; o certo é sobretudo roxo abotoado e gravatinha borboleta
                // vermelha"): sobretudo roxo até o joelho (sem faixa na cintura), calça escura; botões e gravata no SetSkin
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.42f, 0.66f, 0.25f), new Color(0.3f, 0.5f, 0.18f), 0.006f, 0.7f));
                layers.Add(new Layer(Shin, 0f, 1f, new Color(0.14f, 0.1f, 0.18f), new Color(0.1f, 0.07f, 0.12f), 0.012f, 0.7f, Pattern: 1));   // calça
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh, 0f, 1f, new Color(0.38f, 0.16f, 0.55f), new Color(0.26f, 0.1f, 0.4f), 0.02f, 0.6f, Pattern: 1));   // sobretudo
                layers.Add(Boots(new Color(0.3f, 0.19f, 0.08f)));   // botina marrom (como na arte)
                _outfit.Add(AbasDoSobretudo(new Color(0.38f, 0.16f, 0.55f), new Color(0.24f, 0.09f, 0.36f)));
                break;
            case "skin_sem_cabeca":
                layers.Add(new Layer(Head, 0f, 1f, new Color(0.02f, 0.02f, 0.03f), new Color(0.05f, 0.05f, 0.1f), 0.004f, 0.4f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.16f, 0.16f, 0.2f), new Color(0.35f, 0.35f, 0.42f), 0.02f, 0.3f, 0.8f, Pattern: 1));
                mantle = (new Color(0.22f, 0.1f, 0.32f), new Color(0.1f, 0.05f, 0.16f), false);
                break;
            case "skin_corvo":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Thigh | Shin | Foot, 0f, 1f, new Color(0.06f, 0.06f, 0.08f), new Color(0.2f, 0.12f, 0.3f), 0.016f, 0.8f, Pattern: 1));
                layers.Add(new Layer(Hand, 0f, 1f, new Color(0.15f, 0.12f, 0.1f), new Color(0.1f, 0.08f, 0.07f), 0.01f, 0.7f));
                mantle = (new Color(0.07f, 0.06f, 0.1f), new Color(0.4f, 0.15f, 0.6f), true);
                break;
            case "skin_mafioso":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.1f, 0.08f, 0.07f), new Color(0.05f, 0.04f, 0.03f), 0.005f, 0.6f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.08f, 0.08f, 0.1f), new Color(0.7f, 0.06f, 0.08f), 0.012f, 0.5f, Pattern: PTerno, Camisa: new Color(0.85f, 0.85f, 0.82f)));
                layers.Add(Boots(new Color(0.03f, 0.03f, 0.03f)));
                break;
            case "skin_esqueleto":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.92f, 0.9f, 0.84f), new Color(0.7f, 0.68f, 0.6f), 0.006f, 0.7f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.1f, 0.08f, 0.14f), new Color(0.3f, 0.2f, 0.45f), 0.012f, 0.55f, Pattern: 1));
                layers.Add(Boots(new Color(0.06f, 0.05f, 0.06f)));
                mantle = (new Color(0.32f, 0.14f, 0.5f), Gold, false);
                break;
            case "skin_ouro":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(1f, 0.78f, 0.3f), new Color(0.85f, 0.6f, 0.18f), 0.012f, 0.2f, 1f, Emit: new Color(0.35f, 0.25f, 0.05f)));
                break;
            case "skin_magico":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.06f, 0.05f, 0.06f), new Color(0.03f, 0.03f, 0.03f), 0.005f, 0.6f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.14f, 0.07f, 0.22f), Gold, 0.014f, 0.5f, Pattern: 1));
                layers.Add(Boots(new Color(0.05f, 0.04f, 0.05f)));
                mantle = (new Color(0.2f, 0.08f, 0.3f), Gold, true);
                break;
            case "skin_espantalho":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.62f, 0.5f, 0.32f), new Color(0.45f, 0.35f, 0.2f), 0.008f, 1f, Pattern: 1));   // saco de estopa
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.42f, 0.32f, 0.2f), new Color(0.3f, 0.45f, 0.25f), 0.018f, 1f, Pattern: 2));
                layers.Add(Boots(new Color(0.25f, 0.18f, 0.1f)));
                break;
            case "skin_demonio":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.55f, 0.05f, 0.06f), new Color(0.3f, 0.02f, 0.03f), 0.006f, 0.5f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.08f, 0.07f, 0.09f), new Color(0.7f, 0.06f, 0.08f), 0.012f, 0.5f, Pattern: PTerno, Camisa: new Color(0.85f, 0.85f, 0.82f)));
                layers.Add(Boots(new Color(0.03f, 0.03f, 0.03f)));
                break;
            case "skin_marionete":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.62f, 0.44f, 0.26f), new Color(0.45f, 0.3f, 0.16f), 0.008f, 0.85f, Pattern: 1));
                break;
            case "skin_peste":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.12f, 0.1f, 0.09f), new Color(0.06f, 0.05f, 0.05f), 0.006f, 0.6f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.08f, 0.07f, 0.07f), new Color(0.18f, 0.15f, 0.12f), 0.016f, 0.6f, Pattern: 1));
                layers.Add(Boots(new Color(0.06f, 0.05f, 0.05f)));
                robe = (new Color(0.07f, 0.06f, 0.06f), new Color(0.2f, 0.17f, 0.12f));
                break;
            case "skin_samurai":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.12f, 0.16f, 0.38f), new Color(0.3f, 0.5f, 1f), 0.018f, 0.4f, 0.4f, Pattern: 1, Emit: new Color(0.1f, 0.25f, 0.8f)));
                break;
            case "skin_pirata_morto":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.9f, 0.88f, 0.8f), new Color(0.65f, 0.62f, 0.55f), 0.006f, 0.7f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore, 0f, 1f, new Color(0.5f, 0.08f, 0.08f), Gold, 0.018f, 0.7f, Pattern: 1));   // casaca
                layers.Add(new Layer(Thigh | Shin, 0f, 1f, new Color(0.12f, 0.1f, 0.09f), new Color(0.3f, 0.25f, 0.2f), 0.014f, 0.8f));
                layers.Add(Boots(new Color(0.1f, 0.07f, 0.05f)));
                break;
            case "skin_palhaco_sinistro":
                layers.Add(new Layer(Head, 0f, 1f, new Color(0.97f, 0.95f, 0.93f), new Color(0.85f, 0.8f, 0.8f), 0.006f, 0.5f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.6f, 0.05f, 0.08f), new Color(0.95f, 0.92f, 0.9f), 0.016f, 0.8f, Pattern: 2));
                layers.Add(new Layer(Hand, 0f, 1f, new Color(0.95f, 0.95f, 0.95f), Gold, 0.012f, 0.7f));
                layers.Add(Boots(new Color(0.55f, 0.04f, 0.06f)));
                break;
            case "skin_cozinheiro_insano":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.15f, 0.12f, 0.1f), new Color(0.08f, 0.06f, 0.05f), 0.005f, 0.6f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore, 0f, 1f, new Color(0.95f, 0.94f, 0.92f), new Color(0.6f, 0.08f, 0.08f), 0.014f, 0.8f, Pattern: 1));
                layers.Add(new Layer(Thigh | Shin, 0f, 1f, new Color(0.12f, 0.12f, 0.14f), new Color(0.3f, 0.3f, 0.3f), 0.012f, 0.6f));
                layers.Add(Boots(new Color(0.05f, 0.05f, 0.05f)));
                break;
            case "skin_sapo":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.35f, 0.62f, 0.25f), new Color(0.25f, 0.45f, 0.15f), 0.006f, 0.6f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.08f, 0.08f, 0.1f), new Color(0.7f, 0.06f, 0.08f), 0.012f, 0.5f, Pattern: PTerno, Camisa: new Color(0.85f, 0.85f, 0.82f)));
                layers.Add(Boots(new Color(0.03f, 0.03f, 0.03f)));
                break;
            case "skin_rei":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.1f, 0.08f, 0.07f), new Color(0.05f, 0.04f, 0.03f), 0.005f, 0.6f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.07f, 0.07f, 0.09f), new Color(0.2f, 0.2f, 0.24f), 0.012f, 0.5f, Pattern: 1));
                layers.Add(Boots(new Color(0.03f, 0.03f, 0.03f)));
                mantle = (new Color(0.6f, 0.05f, 0.08f), new Color(0.95f, 0.93f, 0.88f), false);
                break;
            case "skin_pedra":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.42f, 0.42f, 0.44f), new Color(0.3f, 0.6f, 0.9f), 0.02f, 0.95f, Pattern: 1, Emit: new Color(0.02f, 0.06f, 0.1f)));
                break;
            case "skin_cristal":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.55f, 0.35f, 1f), new Color(0.85f, 0.75f, 1f), 0.01f, 0.08f, 0.3f, Pattern: 1, Emit: new Color(0.35f, 0.2f, 0.8f)));
                break;
            case "skin_minotauro":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.38f, 0.24f, 0.15f), new Color(0.25f, 0.15f, 0.08f), 0.008f, 0.9f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.3f, 0.2f, 0.13f), new Color(0.18f, 0.12f, 0.07f), 0.016f, 0.85f, Pattern: 1));
                layers.Add(Belt());
                layers.Add(Boots(new Color(0.2f, 0.13f, 0.08f)));
                break;
            case "skin_mumia":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.86f, 0.8f, 0.66f), new Color(0.6f, 0.52f, 0.38f), 0.016f, 1f, Pattern: 2));
                break;
            case "skin_peixe":
                layers.Add(new Layer(Head | Hand, 0f, 1f, new Color(0.18f, 0.25f, 0.42f), new Color(0.1f, 0.15f, 0.3f), 0.006f, 0.4f));
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.12f, 0.14f, 0.22f), new Color(0.3f, 0.45f, 0.75f), 0.016f, 0.4f, 0.3f, Pattern: 1));
                layers.Add(Boots(new Color(0.06f, 0.07f, 0.1f)));
                break;
            case "skin_urso":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.62f, 0.42f, 0.24f), new Color(0.48f, 0.3f, 0.16f), 0.02f, 1f, Pattern: 1));
                layers.Add(new Layer(Chest | Belly, 0.45f, 0.72f, new Color(0.82f, 0.66f, 0.45f), new Color(0.62f, 0.42f, 0.24f), 0.024f, 1f));   // barriga clara
                break;
            // ---------------- 14/11 noite: 6 skins da 2ª prancha do dono
            case "skin_macaco_vermelho":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.62f, 0.08f, 0.07f), new Color(0.85f, 0.18f, 0.12f), 0.02f, 1f, Pattern: 1));
                layers.Add(new Layer(Chest | Belly, 0.5f, 0.76f, new Color(0.86f, 0.66f, 0.5f), new Color(0.7f, 0.5f, 0.38f), 0.024f, 0.9f));   // peito pelado
                layers.Add(new Layer(Hand | Foot, 0f, 1f, new Color(0.86f, 0.66f, 0.5f), new Color(0.7f, 0.5f, 0.38f), 0.008f, 0.9f));
                break;
            case "skin_caveira":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.93f, 0.89f, 0.8f), new Color(0.55f, 0.5f, 0.42f), 0.006f, 0.75f, Pattern: 2));
                break;
            case "skin_alien":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.56f, 0.78f, 0.32f), new Color(0.42f, 0.62f, 0.22f), 0.006f, 0.45f));
                break;
            case "skin_tubarao":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.3f, 0.36f, 0.62f), new Color(0.22f, 0.27f, 0.5f), 0.01f, 0.4f));
                layers.Add(new Layer(Chest | Belly, 0.46f, 0.76f, new Color(0.9f, 0.9f, 0.86f), new Color(0.75f, 0.75f, 0.72f), 0.016f, 0.5f, Pattern: 1));   // barriga branca
                break;
            case "skin_fantasma":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.93f, 0.91f, 0.95f), new Color(0.8f, 0.78f, 0.84f), 0.012f, 1f));
                robe = (new Color(0.94f, 0.92f, 0.96f), new Color(0.82f, 0.8f, 0.86f));
                break;
            case "skin_lava":
                layers.Add(new Layer(Head | Chest | Belly | Arm | Fore | Hand | Thigh | Shin | Foot, 0f, 1f, new Color(0.14f, 0.09f, 0.08f), new Color(1f, 0.45f, 0.08f), 0.02f, 0.95f, Pattern: 1, Emit: new Color(0.42f, 0.11f, 0.02f)));
                break;
            case "skin_bobo":
            {
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh | Shin, 0f, 1f, new Color(0.72f, 0.07f, 0.12f), new Color(0.95f, 0.75f, 0.1f), 0.014f, 0.8f, Pattern: 2));
                layers.Add(new Layer(Arm | Thigh, 0.0f, 1f, new Color(0.95f, 0.75f, 0.1f), new Color(0.72f, 0.07f, 0.12f), 0.02f, 0.8f, Pattern: 2));   // mangas e coxas amarelas (as duas cores)
                layers.Add(Boots(new Color(0.95f, 0.75f, 0.1f)));
                break;
            }
            case "body_rags":
                layers.Add(new Layer(Chest | Belly | Thigh, 0.40f, 0.78f, new Color(0.40f, 0.35f, 0.28f), new Color(0.28f, 0.24f, 0.19f), 0.008f, Pattern: 1));
                break;
            case "body_gambeson":
                // 13/11 ("o gibão acolchoado está simples, só uma textura colada — dá uma melhorada, com detalhe e profundidade"):
                // linho acolchoado em gomos, ombreiras de enchimento mais grossas, gola e barra de couro, cinto, e por cima (3D)
                // a carreira de alamares de madeira com tiras de couro no peito
                layers.Add(new Layer(Chest | Belly | Arm | Thigh, 0.42f, 0.86f, Linen, new Color(0.45f, 0.38f, 0.28f), 0.012f, Pattern: 2));
                layers.Add(new Layer(Arm | Chest, 0.76f, 0.86f, Linen.Darkened(0.12f), new Color(0.40f, 0.33f, 0.24f), 0.026f, 0.9f, Pattern: 2));   // ombreiras
                layers.Add(new Layer(Chest, 0.835f, 0.865f, Leather, LeatherDark, 0.03f, 0.7f));                                              // gola
                layers.Add(new Layer(Belly | Thigh, 0.42f, 0.455f, Leather, LeatherDark, 0.022f, 0.7f));                                     // barra
                layers.Add(Belt());
                layers.Add(Boots());
                extras = "gambeson";
                break;
            case "body_mail":
                layers.Add(new Layer(Chest | Belly | Arm | Thigh, 0.40f, 0.86f, Iron, IronTrim, 0.012f, 0.45f, 0.75f, 3));
                layers.Add(new Layer(Chest | Belly | Thigh, 0.36f, 0.80f, Bordo, Bronze, 0.02f, Pattern: 1));      // sobreveste
                layers.Add(Belt());
                layers.Add(Boots());
                break;
            case "body_plate":
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh, 0.38f, 0.86f, Iron, IronTrim, 0.012f, 0.45f, 0.75f, 3));
                layers.Add(new Layer(Chest | Belly, 0.50f, 0.84f, new Color(0.56f, 0.59f, 0.64f), Bronze, 0.03f, 0.35f, 0.85f, 4));   // peitoral
                layers.Add(new Layer(Arm, 0.70f, 0.86f, new Color(0.56f, 0.59f, 0.64f), Bronze, 0.03f, 0.35f, 0.85f, 4));          // ombreiras
                layers.Add(new Layer(Fore | Shin, 0.0f, 1f, new Color(0.52f, 0.55f, 0.60f), Bronze, 0.022f, 0.35f, 0.85f, 4));     // braçadeiras e grevas
                layers.Add(Belt());
                layers.Add(Boots());
                break;
            case "body_robe":
            case "body_robe_vein":
            {
                // mago (referência do usuário): túnica BRANCA longa até os pés com mangas largas, cinto de corda e
                // CAPA roxa de capuz, larga e esvoaçante por cima
                var white = new Color(0.88f, 0.86f, 0.82f);
                var unit = 1f / Mathf.Max(1e-4f, Relative(_skin, Root).Scale.X);
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh, 0f, 1f, white, white, 0.012f, Pattern: 1, Emit: vein ? new Color(0.12f, 0.10f, 0.18f) : null));
                layers.Add(new Layer(Fore, 0.0f, 0.75f, white, new Color(0.75f, 0.72f, 0.66f), 0.045f * unit, Pattern: 1));   // manga larga (boca de sino)
                layers.Add(new Layer(Belly, 0.53f, 0.585f, new Color(0.62f, 0.52f, 0.36f), new Color(0.45f, 0.36f, 0.24f), 0.024f, 0.9f, Pattern: 2)); // corda
                // túnica de pano mole (ClothSkirt): cai reta da cintura até a canela, solta do corpo, e as pernas
                // empurram o pano ao andar (pedido: "roupa descendo reta do peito, não justa"; o cone duro parecia abajur)
                layers.Add(new Layer(Shin, 0f, 1f, new Color(0.80f, 0.78f, 0.74f), white, 0.012f, Pattern: 1));
                layers.Add(Boots(LeatherDark));
                robe = (white, vein ? Gold : new Color(0.75f, 0.72f, 0.66f));
                mantle = (Violet, vein ? Gold : new Color(0.24f, 0.13f, 0.30f), vein);
                break;
            }
            case "body_merlin":
            case "body_merlin_sombra":
            {
                // 13/11 (Merlin): túnica ROXA longa até os pés, mangas largas, cinto dourado e capa roxa mais escura
                var sombra = body == "body_merlin_sombra";
                var roxo = sombra ? new Color(0.13f, 0.07f, 0.22f) : new Color(0.40f, 0.17f, 0.64f);
                var escuro = roxo.Darkened(0.35f);
                var unit = 1f / Mathf.Max(1e-4f, Relative(_skin, Root).Scale.X);
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh, 0f, 1f, roxo, roxo, 0.012f, Pattern: 1, Emit: sombra ? new Color(0.1f, 0.03f, 0.2f) : null));
                layers.Add(new Layer(Fore, 0.0f, 0.75f, roxo, Gold, 0.045f * unit, Pattern: 1));
                layers.Add(new Layer(Belly, 0.53f, 0.585f, Gold, Gold, 0.024f, 0.5f, 0.6f));
                layers.Add(new Layer(Shin, 0f, 1f, roxo.Darkened(0.1f), roxo, 0.012f, Pattern: 1));
                layers.Add(Boots(LeatherDark));
                robe = (roxo, Gold);
                mantle = (escuro, Gold, true);
                break;
            }
            case "body_robe_star":
            {
                var white = new Color(0.86f, 0.87f, 0.92f);
                var unit = 1f / Mathf.Max(1e-4f, Relative(_skin, Root).Scale.X);
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh, 0f, 1f, white, white, 0.012f, Pattern: 1, Emit: new Color(0.05f, 0.08f, 0.2f)));
                layers.Add(new Layer(Fore, 0.0f, 0.75f, white, Gold, 0.045f * unit, Pattern: 1));
                layers.Add(new Layer(Belly, 0.53f, 0.585f, Gold, Gold, 0.024f, 0.5f, 0.6f));
                layers.Add(new Layer(Shin, 0f, 1f, new Color(0.78f, 0.79f, 0.84f), white, 0.012f, Pattern: 1));
                layers.Add(Boots(LeatherDark));
                robe = (white, Gold);
                mantle = (new Color(0.10f, 0.14f, 0.38f), Gold, true);
                break;
            }
            case "body_witch":
            case "body_witch_vein":
                layers.Add(new Layer(Chest | Belly, 0.52f, 0.80f, Emerald, Gold, 0.012f, 0.6f, Pattern: 2, Emit: vein ? new Color(0.05f, 0.25f, 0.12f) : null));  // corpete
                layers.Add(new Layer(Arm | Fore, 0.0f, 1f, Moss, Gold, 0.01f, Pattern: 1));                                                    // mangas
                layers.Add(new Layer(Belly, 0.545f, 0.575f, LeatherDark, Gold, 0.02f, 0.5f));                                               // cinto
                skirt = 0.42f; skirtColor = Emerald; skirtTrim = Gold;
                layers.Add(Boots(LeatherDark));
                break;
            case "body_crimson":
                layers.Add(new Layer(Chest | Belly | Arm | Thigh, 0.40f, 0.86f, Iron, IronTrim, 0.012f, 0.45f, 0.75f, 3));
                layers.Add(new Layer(Chest | Belly | Thigh, 0.34f, 0.82f, new Color(0.62f, 0.08f, 0.10f), Gold, 0.022f, 0.6f, Pattern: 1));
                layers.Add(new Layer(Arm, 0.72f, 0.86f, new Color(0.85f, 0.72f, 0.40f), Gold, 0.03f, 0.3f, 0.9f, 4));
                layers.Add(Belt());
                layers.Add(Boots(LeatherDark));
                break;
            case "body_witch_rose":
                layers.Add(new Layer(Chest | Belly, 0.52f, 0.80f, new Color(0.48f, 0.08f, 0.18f), Gold, 0.012f, 0.6f, Pattern: 2));
                layers.Add(new Layer(Arm | Fore, 0.0f, 1f, new Color(0.30f, 0.06f, 0.12f), Gold, 0.01f, Pattern: 1));
                layers.Add(new Layer(Belly, 0.545f, 0.575f, LeatherDark, Gold, 0.02f, 0.5f));
                skirt = 0.45f; skirtColor = new Color(0.48f, 0.08f, 0.18f); skirtTrim = Gold;
                layers.Add(Boots(LeatherDark));
                break;
            case "body_ranger_crimson":
                layers.Add(new Layer(Chest | Belly | Arm | Thigh, 0.44f, 0.84f, new Color(0.50f, 0.10f, 0.10f), LeatherDark, 0.012f, Pattern: 1));
                layers.Add(new Layer(Thigh | Shin, 0.08f, 0.52f, new Color(0.12f, 0.10f, 0.09f), LeatherDark, 0.008f, Pattern: 1));
                layers.Add(new Layer(Belly | Thigh, 0.42f, 0.60f, new Color(0.12f, 0.10f, 0.09f), LeatherDark, 0.014f, Pattern: 1));
                layers.Add(new Layer(Chest, 0.62f, 0.84f, new Color(0.12f, 0.10f, 0.09f), Gold, 0.02f, 0.5f, Pattern: 2));
                layers.Add(new Layer(Fore, 0f, 1f, new Color(0.12f, 0.10f, 0.09f), Gold, 0.016f, 0.5f, Pattern: 2));
                layers.Add(Belt());
                layers.Add(Boots(new Color(0.12f, 0.10f, 0.09f)));
                break;
            case "body_ranger":
            case "body_ranger_vein":
                layers.Add(new Layer(Chest | Belly | Arm | Thigh, 0.44f, 0.84f, Forest, Leather, 0.012f, Pattern: 1, Emit: vein ? new Color(0.06f, 0.16f, 0.18f) : null)); // túnica
                layers.Add(new Layer(Thigh | Shin, 0.08f, 0.52f, new Color(0.33f, 0.26f, 0.18f), LeatherDark, 0.008f, Pattern: 1));              // calça
                layers.Add(new Layer(Belly | Thigh, 0.42f, 0.60f, new Color(0.30f, 0.23f, 0.16f), LeatherDark, 0.014f, Pattern: 1));             // cós (cobre a cueca)
                layers.Add(new Layer(Chest, 0.62f, 0.84f, Leather, LeatherDark, 0.02f, 0.6f, Pattern: 2));                                         // peitilho de couro
                layers.Add(new Layer(Fore, 0f, 1f, Leather, LeatherDark, 0.016f, 0.6f, Pattern: 2));                                              // braçadeiras
                layers.Add(Belt());
                layers.Add(Boots());
                break;
        }
        if (body?.StartsWith("npc:") == true) NpcOutfit(body, layers, ref skirt, ref skirtColor, ref skirtTrim);
        if (body == "monstro" && _mon is { } monStyle) MonsterOutfit(monStyle, layers, ref mantle);
        if (Bulk <= 0) ApplyMuscle(0f);
        if (Bulk > 0)
        {
            // o "grow" da camada é na unidade da malha (o Mixamo usa centímetros: escala 0,01 no esqueleto)
            var unit = 1f / Mathf.Max(1e-4f, Relative(_skin, Root).Scale.X);
            // músculo por baixo da roupa: peito/ombros mais largos, braços e coxas mais grossos (antebraço e canela menos)
            // roupas por cima do músculo: crescem junto
            var bulk = Bulk * unit;   // as roupas copiam a malha já engrossada: não precisam crescer junto
            // músculo no PRÓPRIO corpo (feedback: a casca separada parecia uma armadura): a pele do manequim incha
            // por região no shader, com a mesma textura — peito/ombros/braços/coxas mais grossos, mãos/cabeça/pés iguais
            ApplyMuscle(_ghost ? 0f : Bulk);   // a malha em si está em metros (o "unit" vale para o grow do shader de roupa)
        }
        if (_ghost)
            for (var i = 0; i < layers.Count; i++)
                layers[i] = layers[i] with { Cloth = Ghostly(layers[i].Cloth), Trim = Ghostly(layers[i].Trim), Emit = new Color(0.08f, 0.16f, 0.3f) };
        foreach (var l in layers) _outfit.Add(AddLayer(l));
        if (skirt > 0) _outfit.Add(AddSkirt(skirt, _ghost ? Ghostly(skirtColor) : skirtColor, _ghost ? Ghostly(skirtTrim) : skirtTrim, mantle is null ? 0.32f : 0.12f));
        if (robe is { } rb) _outfit.Add(AddRobe(_ghost ? Ghostly(rb.Cloth) : rb.Cloth, _ghost ? Ghostly(rb.Trim) : rb.Trim));
        _outfitMantle = mantle is not null;
        if (mantle is { } mt && _wingsKind?.StartsWith("capa:") != true) _outfit.Add(AddMantle(mt.Cloth, mt.Trim, mt.Glow));   // 14/11: a capa equipada toma o lugar do manto da roupa
        if (extras == "gambeson" && !_ghost) _outfit.Add(AlamaresGibao());
    }

    /// <summary>13/11: frente do gibão — tira de couro no meio do peito com quatro alamares de madeira e laçadas.</summary>
    private Node3D AlamaresGibao()
    {
        var peito = Socket("Spine2");
        var node = new Node3D { Name = "Alamares" };
        peito.AddChild(node);
        var sc = _body.Scale.X;
        var couro = VisualKit.Mat(LeatherDark, 0.65f);
        var madeira = VisualKit.Mat(new Color(0.42f, 0.28f, 0.15f), 0.6f);
        var frente = 0.135f * sc + Bulk * 0.6f;
        VisualKit.Part(node, VisualKit.Box(0.035f, 0.30f, 0.01f), couro, new Vector3(0, -0.06f, frente), new Vector3(-8, 0, 0));
        for (var i = 0; i < 4; i++)
        {
            var y = 0.07f - i * 0.075f;
            var z = frente + 0.008f + i * 0.004f;
            VisualKit.Part(node, VisualKit.Capsule(0.011f, 0.055f), madeira, new Vector3(0, y, z), new Vector3(0, 0, 90));
            foreach (var sd in new[] { -1f, 1f })
                VisualKit.Part(node, VisualKit.Box(0.035f, 0.008f, 0.006f), couro, new Vector3(sd * 0.03f, y, z - 0.002f), new Vector3(0, 0, sd * 12));
        }
        return node;
    }

    /// <summary>Túnica longa de pano mole presa na cintura (ClothSkirt), com as pernas como obstáculo.</summary>
    private Node3D AddRobe(Color cloth, Color trim)
    {
        var hips = Socket("Hips");
        var mat = VisualKit.Hide(cloth, "pele", 0.95f, 5f);   // trama do tecido (relevo fino)
        var sc = _body.Scale.X;
        _skirt = new ClothSkirt(hips, Root, mat, VisualKit.Mat(trim, 0.6f, 0.3f), 0.92f * sc, 0.17f * sc, 0.3f * sc, 0.24f * sc) { Name = "Tunica" };
        hips.AddChild(_skirt);
        var legBones = new[] { ("LeftUpLeg", "LeftLeg", 0.10f), ("LeftLeg", "LeftFoot", 0.075f), ("RightUpLeg", "RightLeg", 0.10f), ("RightLeg", "RightFoot", 0.075f) };
        var legs = new List<(Vector3, Vector3, float)>();
        _skirt.Limbs = () =>
        {
            legs.Clear();
            foreach (var (a, b, r) in legBones) legs.Add((BoneWorld(a), BoneWorld(b), r * sc));
            return legs;
        };
        return _skirt;
    }

    private ClothSkirt? _skirt;

    /// <summary>
    /// Capa de mago: tecido mole simulado (ClothCape) preso nos ombros e na nuca, emendando com o capuz caído;
    /// balança e fica para trás quando o personagem anda/corre/vira.
    /// </summary>
    private Node3D AddMantle(Color cloth, Color trim, bool glow, float comprimento = 1.3f, float largura = 1.15f, float brilho = 0.6f, Material? pano = null, bool semBarra = false)
    {
        _back ??= Socket("Spine2");
        var t = glow ? VisualKit.Glow(trim, brilho) : VisualKit.Mat(trim, 0.5f, 0.4f);
        _cape = new ClothCape(_back, cloth, t, comprimento, 0.5f, largura, pano) { Name = "Capa", SemBarra = semBarra };
        _back.AddChild(_cape);
        var limbBones = new[] { ("LeftUpLeg", "LeftLeg", 0.11f), ("LeftLeg", "LeftFoot", 0.08f), ("RightUpLeg", "RightLeg", 0.11f), ("RightLeg", "RightFoot", 0.08f),
                                ("LeftArm", "LeftForeArm", 0.07f), ("LeftForeArm", "LeftHand", 0.06f), ("RightArm", "RightForeArm", 0.07f), ("RightForeArm", "RightHand", 0.06f) };
        var limbs = new List<(Vector3, Vector3, float)>();
        _cape.Limbs = () =>
        {
            limbs.Clear();
            var sc = _body.Scale.X;
            foreach (var (a, b, r) in limbBones) limbs.Add((BoneWorld(a), BoneWorld(b), r * sc));
            return limbs;
        };
        // presilha nos ombros (gola dobrada) — tampa a emenda do pano com o corpo
        var gola = VisualKit.Part(_back, VisualKit.Cylinder(0.1f, 0.15f, 0.06f, 16), VisualKit.Mat(cloth, 0.9f), new Vector3(0, 0.21f, -0.07f));
        var node = new Node3D { Name = "Manto" };
        _back.AddChild(node);
        gola.Reparent(node, false);
        // gola de pano em volta do pescoço e "véu" sobre os ombros (capinha curta): é dali que a capa cai
        var clothMat = VisualKit.Mat(cloth, 0.9f);
        // 17/11 (dono: skins "tortas" — esqueleto, mágico e corvo pareciam usar uma boia no pescoço): gola fina e véu raso
        VisualKit.Part(node, new TorusMesh { InnerRadius = 0.112f, OuterRadius = 0.13f, Rings = 20, RingSegments = 10 }, clothMat, new Vector3(0, 0.13f, -0.025f), null, new Vector3(1f, 0.8f, 0.85f));
        VisualKit.Part(node, VisualKit.Sphere(0.25f), clothMat, new Vector3(0, 0.06f, -0.04f), null, new Vector3(0.98f, 0.16f, 0.62f));
        _outfit.Add(node);
        return _cape;
    }

    private ClothCape? _cape;
    private bool _outfitMantle;   // 14/11: a roupa atual tem manto próprio (some com capa equipada)

    // ------------------------------------------------------------ conjuntos em peças (11/11)
    private readonly Dictionary<string, MeshInstance3D> _armorParts = new();
    private float _balancoYaw;
    private string? _glovesModel, _bootsModel;

    public void SetLimbs(string? gloves, string? boots)
    {
        if (gloves != _glovesModel) { _glovesModel = gloves; SetArmorPart("luvas", IsArmorPack(gloves) ? gloves : null); }
        if (boots != _bootsModel) { _bootsModel = boots; SetArmorPart("botas", IsArmorPack(boots) ? boots : null); }
    }

    private string? _pantsModel;

    /// <summary>11/11: calça (coxas e saia) — a 5ª parte da armadura, como no MU.</summary>
    public void SetPants(string? pants)
    {
        if (pants != _pantsModel) { _pantsModel = pants; SetArmorPart("calca", IsArmorPack(pants) ? pants : null); }
        ApplyArmorGlow();
    }

    // 12/11 ("quanto mais pica, mais dourado é o set"): brilho dourado por cima das peças de armadura, pela variação média
    private int _armorGlow;
    private ShaderMaterial? _glowMat;
    private static Shader? _glowShader;

    // 12/11 (forma do Pandemônio): pele com brilho verde-limão e a máscara de madeira no rosto
    private Node3D? _mascara;
    private OmniLight3D? _mascaraLuz;
    private static ShaderMaterial? _verdeMat;

    public void SetPandemonio(bool on)
    {
        _verdeMat ??= new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, shadows_disabled;
void fragment() {
    float f = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 1.6);
    ALBEDO = vec3(0.35, 1.0, 0.25) * (0.35 + f) * (0.8 + 0.2 * sin(TIME * 6.0));
}" } };
        foreach (var mi in Root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            mi.MaterialOverlay = on ? _verdeMat : (_armorParts.ContainsValue(mi) && _armorGlow >= 2 ? _glowMat : null);
        // a máscara já vem do equipamento (capacete); aqui o corpo cresce um pouco e acende a aura verde
        if (on && _mascaraLuz is null)
        {
            _mascaraLuz = new OmniLight3D { LightColor = new Color(0.4f, 1f, 0.3f), LightEnergy = 1.4f, OmniRange = 3f, Position = new Vector3(0, 1.7f, 0.3f) };
            Root.AddChild(_mascaraLuz);
        }
        SetBuild(on ? 1.08f : 1f, on ? 1.14f : 1f);
        if (_mascara is not null) _mascara.Visible = on;
        if (_mascaraLuz is not null) _mascaraLuz.Visible = on;
    }

    public void SetArmorGlow(int variation)
    {
        if (variation == _armorGlow) return;
        _armorGlow = variation;
        ApplyArmorGlow();
    }

    private void ApplyArmorGlow()
    {
        // 12/11 (pedido "conjunto mais dourado no corpo"): além do brilho de borda, a armadura vestida ganha banho de metal
        // na cor da variação (dourado nas altas) — cada vez mais forte; V1 não muda nada
        var v = _armorGlow;
        var banho = v <= 1 ? 0f : v <= 3 ? 0.14f : v <= 6 ? 0.24f : v <= 9 ? 0.34f : v <= 12 ? 0.46f : v <= 14 ? 0.56f : 0.66f;
        var borda = v < 4 ? 0.05f : v <= 6 ? 0.12f : v <= 9 ? 0.22f : v <= 12 ? 0.40f : v <= 14 ? 0.52f : 0.65f;
        if (banho <= 0f)
        {
            foreach (var mi in _armorParts.Values) if (GodotObject.IsInstanceValid(mi)) mi.MaterialOverlay = null;
            return;
        }
        _glowShader ??= new Shader { Code = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, shadows_disabled;
uniform vec3 cor = vec3(1.0, 0.8, 0.3);
uniform float banho = 0.3;
uniform float forca = 0.4;
void fragment() {
    float f = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.2);
    float brilho = 0.75 + 0.25 * sin(TIME * 2.2 + VERTEX.y * 5.0);
    ALBEDO = cor;
    METALLIC = 0.85;
    ROUGHNESS = 0.3;
    EMISSION = cor * forca * (0.1 + f) * brilho;
    ALPHA = clamp(banho + f * forca * 0.5, 0.0, 0.9);
}" };
        _glowMat ??= new ShaderMaterial { Shader = _glowShader };
        var (r, g, b) = CinzasDaMare.Shared.Content.Variacao.Color(v);
        _glowMat.SetShaderParameter("cor", new Vector3(r, g, b));
        _glowMat.SetShaderParameter("banho", banho);
        _glowMat.SetShaderParameter("forca", borda);
        foreach (var mi in _armorParts.Values) if (GodotObject.IsInstanceValid(mi)) mi.MaterialOverlay = _glowMat;
    }

    /// <summary>
    /// Peça de conjunto ("arm_cavaleiro:peitoral"): o .glb Models/armaduras/arm_cavaleiro.glb tem o esqueleto do manequim e
    /// 4 malhas com pele (capacete, peitoral, luvas, botas; Tools/arte/armadura_meshy.py). A malha da peça é ligada ao
    /// esqueleto deste boneco e anima junto (cotovelo, joelho...).
    /// </summary>
    /// <summary>Peça de armadura em malha com esqueleto: conjuntos "arm_*" e lendárias "armadura_*".</summary>
    internal static bool IsArmorPack(string? m) => m is not null && (m.StartsWith("arm_") || m.StartsWith("armadura_"));

    private static readonly Dictionary<ulong, Color> MeanColors = new();

    /// <summary>Cor média de uma textura (1 pixel), guardada por textura.</summary>
    private static Color MeanColor(Texture2D? tex)
    {
        if (tex is null) return Colors.White;
        if (MeanColors.TryGetValue(tex.GetInstanceId(), out var c)) return c;
        c = Colors.Gray;
        try
        {
            if (tex.GetImage() is { } img)
            {
                if (img.IsCompressed()) img.Decompress();
                img.Resize(1, 1, Image.Interpolation.Bilinear);
                c = img.GetPixel(0, 0);
            }
        }
        catch (Exception) { }
        MeanColors[tex.GetInstanceId()] = c;
        return c;
    }

    /// <summary>
    /// 13/11 (pacote do trabalhador: as 4 transformações em 3D): o boneco veste a forma inteira (5 peças "forma_x_&lt;peça&gt;");
    /// enquanto transformado, o equipamento não troca as peças. Devolve false se o arquivo da forma faltar.
    /// </summary>
    public bool SetForma(string pack)
    {
        if (!Godot.FileAccess.FileExists($"res://Assets/Models/armaduras/{pack}.glb")) return false;
        _formaPack = pack;
        foreach (var part in new[] { "capacete", "peitoral", "luvas", "calca", "botas" }) SetArmorPart(part, $"{pack}:{part}");
        return _armorParts.Count > 0;
    }

    private string? _formaPack;

    private void SetArmorPart(string part, string? model)
    {
        if (_formaPack is not null && (model is null || !model.StartsWith(_formaPack, StringComparison.Ordinal))) return;   // transformado: a forma manda
        foreach (var key in _armorParts.Keys.Where(k => k == part || k.StartsWith(part + "#")).ToList())
            if (_armorParts.Remove(key, out var old) && GodotObject.IsInstanceValid(old)) old.QueueFree();
        if (model is null) return;
        var cut = model.IndexOf(':');
        var file = cut > 0 ? model[..cut] : model;
        var pieces = (cut > 0 ? model[(cut + 1)..] : part).Split('+');
        if (GlbCache.Instance("armaduras/" + file) is not { } pack) return;
        // 11/11: lendárias do trabalhador têm a malha "<arquivo>_<peça>"; o peitoral junta braçadeiras e perneiras ("a+b+c")
        for (var i = 0; i < pieces.Length; i++)
        {
            if ((pack.FindChild(pieces[i], true, false) ?? pack.FindChild(file + "_" + pieces[i], true, false)) is not MeshInstance3D mi) continue;
            // 11/11 noite (armaduras novas do trabalhador "vestindo" o capacete na perna): a ordem dos ossos do arquivo
            // não é a do nosso boneco — a pele é religada osso a osso PELO NOME antes de trocar de esqueleto
            if (mi.Skin is { } srcSkin && mi.GetNodeOrNull<Skeleton3D>(mi.Skeleton) is { } packSkel)
            {
                var skin = (Skin)srcSkin.Duplicate();
                for (var b = 0; b < skin.GetBindCount(); b++)
                {
                    var bb = skin.GetBindBone(b);
                    var bname = bb >= 0 && bb < packSkel.GetBoneCount() ? packSkel.GetBoneName(bb) : skin.GetBindName(b).ToString();
                    var ours = _skel.FindBone(bname);
                    if (ours >= 0) { skin.SetBindBone(b, ours); skin.SetBindName(b, bname); }
                }
                mi.Skin = skin;
            }
            mi.GetParent().RemoveChild(mi);
            _skin.GetParent().AddChild(mi);
            mi.Transform = _skin.Transform;
            mi.Skeleton = mi.GetPathTo(_skel);
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            // 11/11 noite (encaixe: "manchas escuras" nas costas): as peças geradas (malha reduzida) têm dezenas de furos;
            // pelo furo aparecia o lado de dentro da peça, na sombra = preto. Agora o lado de dentro é desenhado numa
            // segunda passada, sem sombra, com a própria textura um pouco mais escura — o furo some na cor da peça.
            if (mi.Mesh is { } am)
                for (var sIdx = 0; sIdx < am.GetSurfaceCount(); sIdx++)
                    if ((mi.GetSurfaceOverrideMaterial(sIdx) ?? am.SurfaceGetMaterial(sIdx)) is StandardMaterial3D bm && bm.NextPass is null)
                    {
                        var front = (StandardMaterial3D)bm.Duplicate();
                        // 18/11 17h (dono: "a armadura está sem textura"): no PC (reflexo alto) o metal cheio vira espelho cromado
                        // e some a pintura da peça; metal no máximo 0,6 e um mínimo de aspereza deixam a textura aparecer
                        front.Metallic = Mathf.Min(front.Metallic, 0.6f);
                        front.Roughness = Mathf.Max(front.Roughness, 0.45f);
                        front.NextPass = new StandardMaterial3D
                        {
                            CullMode = BaseMaterial3D.CullModeEnum.Front, DisableReceiveShadows = true,
                            AlbedoColor = (bm.AlbedoColor * MeanColor(bm.AlbedoTexture)), Roughness = 0.7f, Metallic = 0.3f,   // cor média lisa, com luz e sem sombra
                        };
                        mi.SetSurfaceOverrideMaterial(sIdx, front);
                    }
            Balanco.Preparar(mi, GD.Randf() * 10f);   // 13/11 (pacote do trabalhador): manto/capa/véu/asa balançam
            _armorParts[i == 0 ? part : part + "#" + i] = mi;
        }
        pack.QueueFree();
        RefreshBodyMask();
    }

    // ------------------------------------------------------------ o boneco "vira" a armadura (11/11 noite)
    // Pedido do dono: em vez de enfiar o boneco dentro da peça (a pele furava a bota, o joelho saía pela calça), a parte
    // do corpo coberta some e a peça é o corpo ali. Só a cabeça fica sempre (o rosto aparece por baixo do elmo).
    [System.Flags] private enum BodyRegion { None = 0, Torso = 1, Fore = 2, Hips = 4, Legs = 8, Head = 16 }
    private Mesh? _unmasked;
    private static readonly Dictionary<(ulong, int), ArrayMesh> MaskCache = new();

    private static BodyRegion RegionOfBone(string b)
    {
        if (b.Contains("ForeArm") || b.Contains("Hand")) return BodyRegion.Fore;
        if (b.Contains("UpLeg") || b == "Hips" || b.EndsWith(":Hips")) return BodyRegion.Hips;
        if (b.Contains("Leg") || b.Contains("Foot") || b.Contains("Toe")) return BodyRegion.Legs;
        if (b.Contains("Spine") || b.Contains("Shoulder") || b.Contains("Arm")) return BodyRegion.Torso;
        if (b.EndsWith("Head") || b.Contains("HeadTop")) return BodyRegion.Head;   // 11/11 noite: só recua sob o capacete
        return BodyRegion.None;   // pescoço: nunca some
    }

    private void RefreshBodyMask()
    {
        if (_mon is not null || _skin.Skin is null || OS.GetEnvironment("CDM_SEM_MASCARA") == "1") return;
        _unmasked ??= _skin.Mesh;
        var mask = BodyRegion.None;
        if (_armorParts.ContainsKey("peitoral")) mask |= BodyRegion.Torso;
        if (_armorParts.ContainsKey("luvas")) mask |= BodyRegion.Fore;
        if (_armorParts.ContainsKey("calca")) mask |= BodyRegion.Hips;
        if (_armorParts.ContainsKey("botas")) mask |= BodyRegion.Legs;
        if (_armorParts.ContainsKey("capacete")) mask |= BodyRegion.Head;
        Mesh want = _unmasked!;
        if (mask != BodyRegion.None && _unmasked is ArrayMesh src)
        {
            var key = (src.GetInstanceId(), (int)mask);
            if (!MaskCache.TryGetValue(key, out var masked)) MaskCache[key] = masked = MaskedMesh(src, mask);
            want = masked;
        }
        AtualizarCinta(mask);
        if (_skin.Mesh == want) return;
        _skin.Mesh = want;
        var n = Math.Min(want.GetSurfaceCount(), _muscleMats.Count > 0 ? _muscleMats.Count : _baseMats.Count);
        for (var sIdx = 0; sIdx < n; sIdx++) _skin.SetSurfaceOverrideMaterial(sIdx, _muscleMats.Count > 0 ? _muscleMats[sIdx] : _baseMats[sIdx]);
    }

    // 16/11 (pendência "buracos nas armaduras — faixa escura nas costas/cintura, Padded e outros"): entre o peitoral e a
    // calça das armaduras geradas sobra uma fresta na cintura e, por ela, aparecia o escuro do corpo/avesso. Com peitoral E
    // calça de conjunto vestidos, uma cinta (forro) na cor média do peitoral fica por baixo das peças: só aparece nas frestas.
    private MeshInstance3D? _cinta;

    private void AtualizarCinta(BodyRegion mask)
    {
        var peit = _armorParts.FirstOrDefault(kv => kv.Key == "peitoral" || kv.Key.StartsWith("peitoral#")).Value;
        var precisa = (mask & BodyRegion.Torso) != 0 && (mask & BodyRegion.Hips) != 0 && peit is not null
            && Bone("Spine") >= 0 && Bone("Hips") >= 0 && OS.GetEnvironment("CDM_SEM_CINTA") != "1";
        if (!precisa)
        {
            if (_cinta is not null && GodotObject.IsInstanceValid(_cinta)) _cinta.QueueFree();
            _cinta = null;
            return;
        }
        var cor = CorMedia(peit!);
        if (_cinta is null || !GodotObject.IsInstanceValid(_cinta))
        {
            var yHips = (_skelToRoot * _skel.GetBoneGlobalRest(Bone("Hips")).Origin).Y;
            var ySpine = (_skelToRoot * _skel.GetBoneGlobalRest(Bone("Spine")).Origin).Y;
            _cinta = new MeshInstance3D
            {
                Name = "Cinta",
                // 18/11 17h (dono: "a armadura está sem textura, com partes rasgadas" — no Cavaleiro Azul a cinta saía para fora
                // da cintura como uma caixa preta): mais fina, fica por dentro das peças e só tapa as frestas
                Mesh = VisualKit.Cylinder(0.106f, 0.102f, 0.18f, 20),
                Position = new Vector3(0, yHips + 0.08f - ySpine, -0.004f),
                Scale = new Vector3(1f, 1f, 0.72f),
            };
            Socket("Spine").AddChild(_cinta);
        }
        // mesmo brilho/metal do peitoral (parece parte da armadura), sem a textura (que é recortada para a malha dela)
        var baseMat = (peit!.MaterialOverride ?? (peit.Mesh is { } pm && pm.GetSurfaceCount() > 0 ? peit.GetActiveMaterial(0) : null)) as BaseMaterial3D;
        var forro = baseMat?.Duplicate() as BaseMaterial3D ?? new StandardMaterial3D { Roughness = 0.75f };
        forro.AlbedoTexture = null; forro.NormalEnabled = false; forro.NormalTexture = null; forro.EmissionEnabled = false;
        forro.MetallicTexture = null; forro.RoughnessTexture = null; forro.AOEnabled = false; forro.AlbedoColor = cor;
        forro.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
        forro.Metallic = 0.2f; forro.Roughness = 0.8f;   // sem espelho escuro
        _cinta.MaterialOverride = forro;
    }

    /// <summary>Cor média da peça (cor do material × média da textura, amostrada pequena).</summary>
    private static Color CorMedia(MeshInstance3D mi)
    {
        var mat = mi.MaterialOverride ?? (mi.Mesh is { } m && m.GetSurfaceCount() > 0 ? mi.GetActiveMaterial(0) : null);
        if (mat is not BaseMaterial3D b) return new Color(0.35f, 0.3f, 0.25f);
        var c = b.AlbedoColor;
        if (b.AlbedoTexture is { } tex)
        {
            try
            {
                var img = tex.GetImage();
                if (img is not null)
                {
                    if (img.IsCompressed()) img.Decompress();
                    img.Resize(8, 8, Image.Interpolation.Bilinear);
                    var soma = new Color(0, 0, 0, 0);
                    for (var y = 0; y < 8; y++) for (var x = 0; x < 8; x++) soma += img.GetPixel(x, y);
                    var media = soma / 64f;
                    c = new Color(c.R * media.R, c.G * media.G, c.B * media.B);
                }
            }
            catch { }
        }
        return c;
    }

    private const float MaskInset = 0.035f;
    private static Vector3I KeyOf(Vector3 v) => new((int)Mathf.Round(v.X * 200f), (int)Mathf.Round(v.Y * 200f), (int)Mathf.Round(v.Z * 200f));

    private ArrayMesh MaskedMesh(ArrayMesh src, BodyRegion mask)
    {
        var skin = _skin.Skin!;
        var regionOfBind = new BodyRegion[skin.GetBindCount()];
        var insetOfBind = new float[skin.GetBindCount()];
        var dropOfBind = new bool[skin.GetBindCount()];
        for (var b = 0; b < regionOfBind.Length; b++)
        {
            var bone = skin.GetBindBone(b);
            var name = bone >= 0 ? _skel.GetBoneName(bone) : skin.GetBindName(b).ToString();
            regionOfBind[b] = RegionOfBone(name);
            // tronco (Spine) é largo: recua 6 cm; braço fino recua só 3,5 cm (mais que o raio dele viraria do avesso)
            insetOfBind[b] = name.Contains("Spine") ? 0.06f : regionOfBind[b] == BodyRegion.Head ? 0.04f
                : name.Contains("Hand") ? 0.006f : name.Contains("ForeArm") ? 0.015f : MaskInset;   // 14/11 (Missão Terça): mão recua 6 mm
            dropOfBind[b] = name.Contains("Spine") || name.Contains("Shoulder") || name.Contains("Neck");
        }
        // normal média por posição (vértices repetidos nas quinas andam juntos: sem rasgo)
        var avg = new Dictionary<Vector3I, Vector3>();
        for (var sIdx = 0; sIdx < src.GetSurfaceCount(); sIdx++)
        {
            var a0 = src.SurfaceGetArrays(sIdx);
            var v0 = a0[(int)Mesh.ArrayType.Vertex].AsVector3Array(); var n0 = a0[(int)Mesh.ArrayType.Normal].AsVector3Array();
            for (var i = 0; i < v0.Length && i < n0.Length; i++) { var k = KeyOf(v0[i]); avg[k] = avg.GetValueOrDefault(k) + n0[i]; }
        }
        foreach (var k in avg.Keys.ToList()) avg[k] = avg[k].Normalized();
        var outMesh = new ArrayMesh();
        for (var sIdx = 0; sIdx < src.GetSurfaceCount(); sIdx++)
        {
            var arr = src.SurfaceGetArrays(sIdx);
            var vs = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var bones = arr[(int)Mesh.ArrayType.Bones].AsInt32Array();
            var weights = arr[(int)Mesh.ArrayType.Weights].AsFloat32Array();
            var idx = arr[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? null : arr[(int)Mesh.ArrayType.Index].AsInt32Array();
            if (bones.Length == 0 || vs.Length == 0) { outMesh.AddSurfaceFromArrays(src.SurfaceGetPrimitiveType(sIdx), arr); outMesh.SurfaceSetMaterial(sIdx, src.SurfaceGetMaterial(sIdx)); continue; }
            var stride = bones.Length / vs.Length;
            var hidden = new bool[vs.Length];
            var inset = new float[vs.Length];
            var drop = new bool[vs.Length];
            for (var v = 0; v < vs.Length; v++)
            {
                var best = 0; var bw = -1f;
                for (var k = 0; k < stride; k++) if (weights[v * stride + k] > bw) { bw = weights[v * stride + k]; best = bones[v * stride + k]; }
                var reg = best >= 0 && best < regionOfBind.Length ? regionOfBind[best] : BodyRegion.None;
                hidden[v] = reg != BodyRegion.None && (mask & reg) != 0;
                // 11/11 noite (encaixe: "manchas escuras" no peito = corpo furando a peça; cabeça saindo do elmo):
                // tronco recua mais (6 cm), cabeça recua 4 cm sob o capacete (o rosto continua aparecendo nas aberturas)
                inset[v] = best >= 0 && best < insetOfBind.Length ? insetOfBind[best] : MaskInset;
                drop[v] = hidden[v] && best >= 0 && best < dropOfBind.Length && dropOfBind[best];
            }
            // teste (11/11 noite): tirar a parte coberta deixava a peça oca (buracos entre as peças, o avesso aparecendo).
            // Então a parte coberta ENCOLHE para dentro da peça — some debaixo dela, mas fecha as frestas por dentro.
            var ns = arr[(int)Mesh.ArrayType.Normal].AsVector3Array();
            for (var v = 0; v < vs.Length; v++)
                if (hidden[v]) { var k = KeyOf(vs[v]); vs[v] -= avg[k] * inset[v]; }
            arr[(int)Mesh.ArrayType.Vertex] = vs;
            // 11/11 noite (manchas escuras nas costas = o corpo visto pelos furos das peças geradas): o tronco debaixo do
            // peitoral sai de vez; pelo furo aparece o lado de dentro da peça (2ª passada com a textura dela)
            if (idx is not null && OS.GetEnvironment("CDM_TRONCO_FICA") != "1")
            {
                var keep = new List<int>(idx.Length);
                for (var t = 0; t + 2 < idx.Length; t += 3)
                    if (!(drop[idx[t]] && drop[idx[t + 1]] && drop[idx[t + 2]])) { keep.Add(idx[t]); keep.Add(idx[t + 1]); keep.Add(idx[t + 2]); }
                arr[(int)Mesh.ArrayType.Index] = keep.ToArray();
            }
            outMesh.AddSurfaceFromArrays(src.SurfaceGetPrimitiveType(sIdx), arr, new Godot.Collections.Array<Godot.Collections.Array>(), new Godot.Collections.Dictionary(), src.SurfaceGetFormat(sIdx) & Mesh.ArrayFormat.FlagUse8BoneWeights);
            outMesh.SurfaceSetMaterial(sIdx, src.SurfaceGetMaterial(sIdx));
        }
        return outMesh;
    }

    private readonly List<Node3D> _itemCape = new();

    /// <summary>Capa (item no espaço de asas, 11/11): o mesmo pano mole do manto, na cor da capa.</summary>
    private void SetItemCape(string? kind)
    {
        if (_cape is not null && _itemCape.Contains(_cape)) _cape = null;   // a capa anterior era a do item (troca capa → capa)
        foreach (var n in _itemCape) if (GodotObject.IsInstanceValid(n)) n.QueueFree();
        _itemCape.Clear();
        if (kind is null || _cape is not null) return;   // a roupa já tem manto próprio
        var e = CapaEstilo(kind);
        var before = _outfit.Count;
        // 14/11 noite ("capa: dá pra colocar textura, recorte, cores, objetos — pegando fogo, com penas"): pano desenhado
        // por shader (estampa + recorte da barra) e um enfeite 3D nas costas
        Material? pano = null;
        if (e.Padrao > 0 || e.Recorte > 0 || e.Ouro > 0)
        {
            var sm = new ShaderMaterial { Shader = _capaShader ??= new Shader { Code = CapaShader } };
            sm.SetShaderParameter("pano", new Vector3(e.Pano.R, e.Pano.G, e.Pano.B));
            var c2 = e.Cor2 ?? e.Barra;
            sm.SetShaderParameter("cor2", new Vector3(c2.R, c2.G, c2.B));
            sm.SetShaderParameter("borda", new Vector3(e.Barra.R, e.Barra.G, e.Barra.B));
            sm.SetShaderParameter("padrao", e.Padrao);
            sm.SetShaderParameter("recorte", e.Recorte);
            sm.SetShaderParameter("ouro", e.Ouro);
            sm.SetShaderParameter("brilho", e.Brilho);
            pano = sm;
        }
        var c = AddMantle(e.Pano, e.Barra, e.Brilho > 0, e.Comprimento, e.Largura, Math.Max(0.1f, e.Brilho), pano, e.Recorte > 0);
        _itemCape.AddRange(_outfit.Skip(before).OfType<Node3D>());
        _outfit.RemoveRange(before, _outfit.Count - before);
        _itemCape.Add(c);
        if (e.Emblema is { } em && _back is not null && Emblema(em) is { } no)
        {
            _back.AddChild(no);
            no.Position = new Vector3(0, 0.15f, -0.17f);
            no.Scale = Vector3.One * 1.7f;
            no.RotationDegrees = new Vector3(0, 180, 0);
            _itemCape.Add(no);
        }
        // 14/11 ("mais capas: de fogo, roxas, com design diferente"): faíscas/brasas ou poeira de estrelas saindo das costas
        if (e.Particula is { } pc && _back is not null)
        {
            var folha = e.Padrao == 4;
            var fx = new CpuParticles3D
            {
                Amount = e.Fogo ? 22 : folha ? 6 : 12, Lifetime = e.Fogo ? 1.1f : 2.2f, Position = new Vector3(0, -0.35f, -0.22f),
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.28f, 0.45f, 0.06f),
                Direction = e.Fogo ? Vector3.Up : Vector3.Down, Spread = 25f, Gravity = e.Fogo ? new Vector3(0, 1.4f, 0) : new Vector3(0, folha ? -0.4f : -0.15f, 0),
                InitialVelocityMin = 0.15f, InitialVelocityMax = e.Fogo ? 0.6f : 0.25f, ScaleAmountMin = folha ? 0.04f : 0.015f, ScaleAmountMax = e.Fogo ? 0.045f : folha ? 0.06f : 0.03f,
                LocalCoords = false,
                Mesh = folha
                    ? new BoxMesh { Size = new Vector3(1f, 0.08f, 0.6f), Material = new StandardMaterial3D { AlbedoColor = pc, Roughness = 0.9f } }
                    : new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3,
                        Material = new StandardMaterial3D { AlbedoColor = pc, EmissionEnabled = true, Emission = pc, EmissionEnergyMultiplier = 3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
            };
            if (folha) { fx.AngularVelocityMin = -180; fx.AngularVelocityMax = 180; }
            _back.AddChild(fx);
            _itemCape.Add(fx);
            if (e.Fogo)
            {
                var luz = new OmniLight3D { LightColor = pc, LightEnergy = 0.35f, OmniRange = 1.6f, Position = new Vector3(0, -0.3f, -0.35f), ShadowEnabled = false };
                _back.AddChild(luz);
                _itemCape.Add(luz);
            }
        }
    }

    private static Shader? _capaShader;

    /// <summary>
    /// 14/11 noite: pano da capa desenhado. UV: x = de um lado ao outro, y = da gola (0) até a barra (1).
    /// padrao: 1 estrelas e lua · 2 escamas · 3 penas · 4 folhas · 5 runas acesas · 6 lava rachada · 7 cristais de gelo ·
    /// 8 arminho (gola branca de pintas) · 9 caveira e ossos · 10 raios de sol · 11 engrenagens · 12 céu de galáxia · 13 fumaça.
    /// recorte: 1 rasgada (com furos) · 2 pontas grandes · 3 pontinhas (penas, escamas). ouro: filete dourado na borda.
    /// </summary>
    internal const string CapaShader = @"
shader_type spatial;
render_mode cull_disabled;
uniform vec3 pano = vec3(0.5, 0.1, 0.1);
uniform vec3 cor2 = vec3(1.0, 0.8, 0.3);
uniform vec3 borda = vec3(1.0, 0.8, 0.3);
uniform int padrao = 0;
uniform int recorte = 0;
uniform float ouro = 0.0;
uniform float brilho = 0.0;
float h1(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
vec2 h2(vec2 p) { return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453); }
float ns(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(h1(i), h1(i + vec2(1, 0)), f.x), mix(h1(i + vec2(0, 1)), h1(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return ns(p) * 0.55 + ns(p * 2.1) * 0.28 + ns(p * 4.3) * 0.17; }
vec3 voro(vec2 p) { vec2 i = floor(p), f = fract(p); float d1 = 9.0, d2 = 9.0; vec2 id = vec2(0);
    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) { vec2 g = vec2(float(x), float(y)); vec2 o = h2(i + g);
        float d = length(g + o - f); if (d < d1) { d2 = d1; d1 = d; id = i + g; } else if (d < d2) d2 = d; }
    return vec3(d1, d2 - d1, h1(id)); }
float estrela(vec2 p, float r) { p = abs(p); return smoothstep(r, 0.0, p.x * 4.0 + p.y) + smoothstep(r, 0.0, p.y * 4.0 + p.x); }
float caps(vec2 p, vec2 a, vec2 b, float r) { vec2 pa = p - a, ba = b - a; float t = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0); return length(pa - ba * t) - r; }
void fragment() {
    vec2 uv = UV;
    // ---- recorte da barra
    if (recorte == 1) {
        float n = fbm(vec2(uv.x * 9.0, 2.0)) * 0.22 + ns(vec2(uv.x * 37.0, 5.0)) * 0.07;
        if (uv.y > 1.0 - n) discard;
        if (uv.y > 0.5 && fbm(uv * vec2(7.0, 6.0) + 3.0) > 0.72) discard;
    } else if (recorte == 2) {
        float z = abs(fract(uv.x * 5.0) - 0.5) * 2.0;
        if (uv.y > 0.8 + 0.2 * (1.0 - z)) discard;
    } else if (recorte == 3) {
        float z = abs(fract(uv.x * 11.0 + step(0.5, fract(uv.y * 0.5)) * 0.5) - 0.5) * 2.0;
        if (uv.y > 0.9 + 0.1 * (1.0 - z)) discard;
    }
    vec3 c = pano;
    vec3 e = vec3(0.0);
    float dobra = 0.88 + 0.12 * sin(uv.x * 26.0);
    if (padrao == 1 || padrao == 12) {
        if (padrao == 12) {
            float neb = fbm(uv * vec2(3.0, 4.0) + vec2(0.0, TIME * 0.03));
            c = mix(pano, cor2, smoothstep(0.35, 0.8, neb));
            e += cor2 * smoothstep(0.6, 0.9, neb) * 0.4;
        }
        vec2 g = uv * vec2(9.0, 11.0); vec2 id = floor(g); vec2 f = fract(g) - 0.5 + (h2(id) - 0.5) * 0.5;
        float s = estrela(f, 0.2 + 0.14 * h1(id + 3.0)) * step(0.45, h1(id));
        vec3 sc = padrao == 12 ? vec3(1.0, 0.95, 0.85) : borda;
        c = mix(c, sc, clamp(s, 0.0, 1.0));
        e += sc * s * (0.6 + 0.4 * sin(TIME * 2.0 + h1(id) * 6.0)) * 0.8;
        if (padrao == 1) { vec2 p = (uv - vec2(0.68, 0.62)) * vec2(1.0, 0.8); float lua = step(length(p), 0.09) * step(0.075, length(p - vec2(0.04, 0.02)));
            c = mix(c, borda, lua); e += borda * lua * 0.6; }
    } else if (padrao == 2 || padrao == 3 || padrao == 4) {
        vec2 sz = padrao == 2 ? vec2(8.0, 11.0) : padrao == 3 ? vec2(7.0, 9.0) : vec2(6.0, 8.0);
        vec2 g = uv * sz; float row = floor(g.y); g.x += mod(row, 2.0) * 0.5; vec2 id = floor(g); vec2 f = fract(g);
        vec2 q = f - vec2(0.5, 0.0);
        float forma = padrao == 2 ? length(q * vec2(1.0, 0.9)) : length(q * vec2(1.6, 0.75));
        float dentro = smoothstep(0.62, 0.5, forma);
        float tom = 0.75 + 0.35 * h1(id);
        vec3 base = padrao == 4 && h1(id + 7.0) > 0.5 ? cor2 : pano;
        c = mix(pano * 0.45, base * tom * (0.85 + 0.25 * (1.0 - f.y)), dentro);
        if (padrao != 2) { float veia = smoothstep(0.035, 0.0, abs(q.x)) * dentro * step(0.15, f.y); c = mix(c, padrao == 4 ? base * 1.4 : cor2, veia * 0.7); }
        else c += cor2 * smoothstep(0.5, 0.6, forma) * dentro * 0.25;
    } else if (padrao == 5) {
        vec2 g = vec2(uv.x * 6.0, uv.y * 12.0); vec2 id = floor(g); vec2 f = fract(g);
        float faixa = step(abs(uv.x - 0.5), 0.22) * step(0.12, uv.y) * step(uv.y, 0.85);
        float r = 0.0; float a1 = h1(id), a2 = h1(id + 9.0);
        r += step(abs(f.x - (0.3 + 0.4 * a1)), 0.06) * step(0.15, f.y) * step(f.y, 0.85);
        r += step(abs(f.y - (0.3 + 0.4 * a2)), 0.07) * step(0.2, f.x) * step(f.x, 0.8) * step(0.4, a2);
        r += step(abs(f.x - f.y), 0.06) * step(0.7, a1);
        r *= faixa * step(0.25, h1(id + 2.0));
        c = mix(c * (0.8 + 0.3 * fbm(uv * 8.0)), cor2, r);
        e += cor2 * r * (1.6 + 0.6 * sin(TIME * 2.5 + uv.y * 8.0));
    } else if (padrao == 6) {
        vec3 v = voro(uv * vec2(6.0, 8.0));
        float trinca = smoothstep(0.09, 0.0, v.y);
        c = mix(pano * (0.6 + 0.4 * v.z), cor2, trinca);
        e += cor2 * trinca * (1.4 + 0.6 * sin(TIME * 3.0 + v.z * 6.0)) + cor2 * 0.06 * smoothstep(0.5, 1.0, uv.y);
    } else if (padrao == 7) {
        vec3 v = voro(uv * vec2(7.0, 6.0) + vec2(0.0, 0.5));
        c = mix(pano * (0.8 + 0.4 * v.z), cor2, smoothstep(0.12, 0.0, v.y));
        c += vec3(0.12) * (1.0 - v.x);
        float brilhinho = step(0.985, h1(floor(uv * 60.0) + floor(TIME * 2.0)));
        e += cor2 * (smoothstep(0.08, 0.0, v.y) * 0.5 + brilhinho * 2.0);
    } else if (padrao == 8) {
        float gola = step(uv.y, 0.2);
        vec2 g = uv * vec2(14.0, 30.0); vec2 id = floor(g);
        float pinta = step(length(fract(g) - 0.5), 0.18) * step(0.6, h1(id));
        c = mix(c, mix(vec3(0.97, 0.96, 0.92), vec3(0.04), pinta), gola);
        c = mix(c, vec3(0.97, 0.96, 0.92), step(0.94, uv.y));
    } else if (padrao == 9) {
        vec2 p = (uv - vec2(0.5, 0.45)) * vec2(1.0, 0.75);
        float osso = min(caps(p, vec2(-0.16, -0.12), vec2(0.16, 0.12), 0.022), caps(p, vec2(-0.16, 0.12), vec2(0.16, -0.12), 0.022));
        float cranio = min(length(p - vec2(0.0, -0.02)) - 0.09, caps(p, vec2(-0.04, 0.06), vec2(0.04, 0.06), 0.04));
        float olhos = min(length(p - vec2(-0.035, -0.02)) - 0.025, length(p - vec2(0.035, -0.02)) - 0.025);
        float branco = step(min(osso, cranio), 0.0) * step(0.0, olhos);
        c = mix(c * (0.8 + 0.25 * fbm(uv * 9.0)), vec3(0.93, 0.9, 0.84), branco);
    } else if (padrao == 10) {
        vec2 p = uv - vec2(0.5, -0.05); float a = atan(p.x, p.y);
        float raio = step(0.5, fract(a * 4.0 / 3.14159));
        c = mix(pano, cor2, raio * 0.45);
        e += cor2 * (0.25 + 0.2 * raio) * smoothstep(0.9, 0.0, length(p));
    } else if (padrao == 11) {
        float m = 0.0;
        for (int i = 0; i < 4; i++) { vec2 cc = vec2(0.25 + 0.5 * h1(vec2(float(i), 3.0)), 0.25 + 0.6 * h1(vec2(float(i), 8.0)));
            vec2 p = uv - cc; float r = length(p * vec2(1.0, 0.8)); float a = atan(p.y, p.x);
            float dente = step(0.5, fract(a * 10.0 / 6.2832)) * 0.02;
            m = max(m, step(abs(r - 0.08 - dente), 0.012) + step(abs(r - 0.03), 0.008)); }
        c = mix(c * (0.75 + 0.3 * fbm(uv * 7.0)), cor2, clamp(m, 0.0, 1.0) * 0.8);
    } else if (padrao == 13) {
        float fum = fbm(uv * vec2(4.0, 3.0) + vec2(0.0, -TIME * 0.08));
        c = mix(pano * 0.55, pano * 1.25, fum);
        e += cor2 * smoothstep(0.65, 0.9, fum) * 0.6;
    }
    // ---- filete dourado na borda (laterais e barra)
    if (ouro > 0.0) {
        float lado = min(uv.x, 1.0 - uv.x);
        float fio = step(lado, 0.045) * step(0.02, lado) + step(abs(uv.y - (recorte > 0 ? 0.72 : 0.9)), 0.012) + step(0.95, uv.y) * step(float(recorte), 0.5);
        c = mix(c, borda, clamp(fio, 0.0, 1.0) * ouro);
        e += borda * clamp(fio, 0.0, 1.0) * 0.35 * ouro;
    }
    ALBEDO = c * dobra;
    EMISSION = e * max(brilho, 0.6);
    ROUGHNESS = padrao == 7 ? 0.35 : 0.85;
    METALLIC = padrao == 7 ? 0.2 : 0.0;
}";

    private sealed record EstiloCapa(Color Pano, Color Barra, float Brilho, float Comprimento, float Largura, Color? Particula, bool Fogo,
        int Padrao = 0, int Recorte = 0, Color? Cor2 = null, float Ouro = 0, string? Emblema = null);

    private static Color Rgb(float r, float g, float b) => new(r, g, b);

    /// <summary>14/11: cada capa com cor, barra, tamanho e efeito próprios (o subtipo do item escolhe).</summary>
    private static EstiloCapa CapaEstilo(string kind) => kind switch
    {
        "capa:rubra" => new(Rgb(0.52f, 0.08f, 0.07f), Rgb(0.29f, 0.04f, 0.04f), 0, 1.3f, 1.15f, null, false),
        "capa:chamas" => new(Rgb(0.55f, 0.12f, 0.03f), Rgb(1f, 0.55f, 0.1f), 1.4f, 1.35f, 1.2f, Rgb(1f, 0.55f, 0.15f), true, 6, 1, Rgb(1f, 0.5f, 0.1f)),
        "capa:brasa_viva" => new(Rgb(0.08f, 0.05f, 0.05f), Rgb(1f, 0.35f, 0.05f), 2.0f, 1.5f, 1.3f, Rgb(1f, 0.4f, 0.08f), true, 6, 2, Rgb(1f, 0.38f, 0.06f)),
        "capa:violeta" => new(Rgb(0.32f, 0.12f, 0.5f), Rgb(0.95f, 0.78f, 0.3f), 0, 1.3f, 1.15f, null, false, 0, 0, null, 1f),
        "capa:abismo" => new(Rgb(0.16f, 0.05f, 0.28f), Rgb(0.75f, 0.35f, 1f), 1.6f, 1.55f, 1.35f, Rgb(0.8f, 0.5f, 1f), false, 13, 1, Rgb(0.7f, 0.3f, 1f)),
        "capa:sombra" => new(Rgb(0.06f, 0.06f, 0.08f), Rgb(0.25f, 0.25f, 0.3f), 0, 1.05f, 1.0f, null, false, 13, 1, Rgb(0.2f, 0.2f, 0.25f)),
        "capa:folhas" => new(Rgb(0.2f, 0.42f, 0.18f), Rgb(0.55f, 0.8f, 0.3f), 0.5f, 1.35f, 1.2f, Rgb(0.4f, 0.65f, 0.22f), false, 4, 2, Rgb(0.32f, 0.58f, 0.2f), 0, "flor"),
        "capa:gelo" => new(Rgb(0.55f, 0.75f, 0.95f), Rgb(0.9f, 0.97f, 1f), 1.0f, 1.4f, 1.2f, Rgb(0.9f, 0.97f, 1f), false, 7, 2, Rgb(0.9f, 0.97f, 1f), 0, "floco"),
        "capa:real" => new(Rgb(0.6f, 0.05f, 0.08f), Rgb(0.96f, 0.82f, 0.35f), 0, 1.45f, 1.3f, null, false, 8, 0, null, 1f, "coroa"),
        "capa:dourada" => new(Rgb(0.85f, 0.65f, 0.2f), Rgb(1f, 0.9f, 0.5f), 1.6f, 1.45f, 1.3f, Rgb(1f, 0.85f, 0.4f), true, 10, 0, Rgb(1f, 0.92f, 0.6f), 1f),
        "capa:mare" => new(Rgb(0.08f, 0.3f, 0.42f), Rgb(0.5f, 0.9f, 1f), 0.9f, 1.3f, 1.15f, Rgb(0.6f, 0.95f, 1f), false, 3, 3, Rgb(0.4f, 0.8f, 0.95f)),
        // ---- 14/11 noite: as 15 capas da prancha do dono
        "capa:heroi" => new(Rgb(0.6f, 0.07f, 0.08f), Rgb(0.98f, 0.75f, 0.28f), 0, 1.4f, 1.25f, null, false, 0, 1, null, 1f, "leao"),
        "capa:mago" => new(Rgb(0.28f, 0.12f, 0.5f), Rgb(0.98f, 0.8f, 0.35f), 0.6f, 1.4f, 1.25f, null, false, 1, 0, null, 1f, "gema"),
        "capa:sombria" => new(Rgb(0.16f, 0.15f, 0.2f), Rgb(0.35f, 0.33f, 0.4f), 0, 1.35f, 1.2f, null, false, 13, 1, Rgb(0.3f, 0.28f, 0.38f), 0, "caveira"),
        "capa:corvo" => new(Rgb(0.1f, 0.11f, 0.24f), Rgb(0.4f, 0.35f, 0.7f), 0, 1.35f, 1.25f, null, false, 3, 3, Rgb(0.45f, 0.45f, 0.75f), 0, "corvo"),
        "capa:amaldicoada" => new(Rgb(0.5f, 0.56f, 0.62f), Rgb(0.3f, 0.95f, 1f), 1.0f, 1.4f, 1.2f, Rgb(0.4f, 0.95f, 1f), false, 5, 1, Rgb(0.3f, 0.95f, 1f), 0, "gema_azul"),
        "capa:lava" => new(Rgb(0.12f, 0.08f, 0.07f), Rgb(1f, 0.45f, 0.08f), 1.8f, 1.45f, 1.25f, Rgb(1f, 0.45f, 0.1f), true, 6, 2, Rgb(1f, 0.42f, 0.06f)),
        "capa:pirata" => new(Rgb(0.12f, 0.1f, 0.12f), Rgb(0.9f, 0.7f, 0.25f), 0, 1.35f, 1.25f, null, false, 9, 1, null, 0.8f),
        "capa:celestial" => new(Rgb(0.08f, 0.1f, 0.35f), Rgb(1f, 0.82f, 0.35f), 1.0f, 1.5f, 1.35f, Rgb(1f, 0.95f, 0.75f), false, 12, 0, Rgb(0.55f, 0.2f, 0.75f), 1f, "lua"),
        "capa:dragao" => new(Rgb(0.6f, 0.08f, 0.07f), Rgb(1f, 0.75f, 0.3f), 0, 1.45f, 1.3f, null, false, 2, 3, Rgb(1f, 0.55f, 0.3f), 0, "dragao"),
        "capa:luz" => new(Rgb(0.95f, 0.85f, 0.6f), Rgb(1f, 0.82f, 0.3f), 1.4f, 1.45f, 1.3f, Rgb(1f, 0.92f, 0.55f), false, 10, 2, Rgb(1f, 0.9f, 0.55f), 1f, "sol"),
        "capa:morte" => new(Rgb(0.32f, 0.1f, 0.48f), Rgb(0.75f, 0.35f, 1f), 1.2f, 1.45f, 1.3f, Rgb(0.65f, 0.3f, 1f), false, 13, 1, Rgb(0.7f, 0.35f, 1f), 0, "caveira_roxa"),
        "capa:tempo" => new(Rgb(0.32f, 0.2f, 0.14f), Rgb(0.95f, 0.72f, 0.3f), 0.6f, 1.45f, 1.3f, null, false, 11, 1, Rgb(0.85f, 0.62f, 0.25f), 0.6f, "relogio"),
        _ => new(Rgb(0.34f, 0.26f, 0.18f), Rgb(0.19f, 0.14f, 0.1f), 0, 1.3f, 1.15f, null, false),   // viajante
    };

    /// <summary>14/11 noite: enfeite 3D da capa, preso nas costas logo abaixo da gola (leão, caveira, relógio...).</summary>
    private static Node3D? Emblema(string tipo)
    {
        if (EmblemasGlb.Criar(tipo) is { } novo) return novo;   // 15/11: enfeite modelado (Visuals/EmblemasGlb.cs)
        var n = new Node3D { Name = "Emblema" };
        var ouro = VisualKit.Mat(new Color(1f, 0.78f, 0.3f), 0.25f, 0.95f);
        var osso = VisualKit.Mat(new Color(0.93f, 0.9f, 0.83f), 0.6f);
        var preto = VisualKit.Mat(new Color(0.03f, 0.03f, 0.03f), 0.6f);
        switch (tipo)
        {
            case "leao":
                VisualKit.Part(n, new TorusMesh { InnerRadius = 0.035f, OuterRadius = 0.085f, Rings = 16, RingSegments = 10 }, ouro, Vector3.Zero, new Vector3(90, 0, 0), new Vector3(1f, 1f, 0.6f));   // juba
                VisualKit.Part(n, VisualKit.Sphere(0.05f), ouro, new Vector3(0, 0, 0.02f));
                VisualKit.Part(n, VisualKit.Sphere(0.025f), ouro, new Vector3(0, -0.02f, 0.06f), null, new Vector3(1.2f, 0.8f, 1f));   // focinho
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(n, VisualKit.Sphere(0.008f), preto, new Vector3(sd * 0.02f, 0.015f, 0.065f));
                break;
            case "gema":
            case "gema_azul":
            {
                var cor = tipo == "gema" ? new Color(0.7f, 0.3f, 1f) : new Color(0.3f, 0.95f, 1f);
                VisualKit.Part(n, VisualKit.Sphere(0.045f), ouro, Vector3.Zero, null, new Vector3(1.3f, 1f, 0.4f));
                VisualKit.Part(n, VisualKit.Box(0.045f, 0.045f, 0.03f), VisualKit.Glow(cor, 2.2f), new Vector3(0, 0, 0.02f), new Vector3(0, 0, 45));
                n.AddChild(new OmniLight3D { LightColor = cor, LightEnergy = 0.3f, OmniRange = 0.8f, ShadowEnabled = false });
                break;
            }
            case "caveira":
            case "caveira_roxa":
            {
                var roxo = tipo == "caveira_roxa";
                var m = roxo ? VisualKit.Mat(new Color(0.55f, 0.25f, 0.8f), 0.4f) : osso;
                VisualKit.Part(n, VisualKit.Sphere(0.055f), m, Vector3.Zero, null, new Vector3(1f, 1.05f, 0.8f));
                VisualKit.Part(n, VisualKit.Box(0.05f, 0.03f, 0.04f), m, new Vector3(0, -0.045f, 0.01f));
                var olho = roxo ? VisualKit.Glow(new Color(0.85f, 0.5f, 1f), 3f) : preto;
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(n, VisualKit.Sphere(0.016f), olho, new Vector3(sd * 0.02f, 0.005f, 0.04f));
                for (var i = 0; i < 6; i++)   // correntes caindo dos lados
                    foreach (var sd in new[] { -1f, 1f })
                        VisualKit.Part(n, new TorusMesh { InnerRadius = 0.008f, OuterRadius = 0.016f, Rings = 8, RingSegments = 4 }, VisualKit.Mat(new Color(0.45f, 0.45f, 0.48f), 0.4f, 0.9f),
                            new Vector3(sd * (0.06f + i * 0.025f), 0.02f - i * 0.012f, 0), new Vector3(0, i % 2 == 0 ? 0 : 90, 0));
                if (roxo) n.AddChild(new OmniLight3D { LightColor = new Color(0.7f, 0.35f, 1f), LightEnergy = 0.35f, OmniRange = 1f, ShadowEnabled = false });
                break;
            }
            case "coroa":
                VisualKit.Part(n, VisualKit.Cylinder(0.05f, 0.05f, 0.03f, 12), ouro, Vector3.Zero, new Vector3(90, 0, 0), new Vector3(1f, 1f, 0.5f));
                for (var i = 0; i < 5; i++) VisualKit.Part(n, VisualKit.Cylinder(0f, 0.012f, 0.04f, 4), ouro, new Vector3((i - 2) * 0.022f, 0.035f, 0));
                VisualKit.Part(n, VisualKit.Sphere(0.012f), VisualKit.Glow(new Color(1f, 0.15f, 0.3f), 2f), new Vector3(0, 0, 0.02f));
                break;
            case "corvo":
                VisualKit.Part(n, VisualKit.Sphere(0.045f), VisualKit.Mat(new Color(0.85f, 0.83f, 0.78f), 0.5f), Vector3.Zero, null, new Vector3(1f, 1f, 0.9f));
                VisualKit.Part(n, VisualKit.Cylinder(0f, 0.022f, 0.08f, 6), VisualKit.Mat(new Color(0.6f, 0.58f, 0.55f), 0.4f, 0.5f), new Vector3(0, -0.01f, 0.06f), new Vector3(100, 0, 0));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(n, VisualKit.Sphere(0.011f), preto, new Vector3(sd * 0.022f, 0.012f, 0.035f));
                break;
            case "floco":
                for (var i = 0; i < 3; i++)
                    VisualKit.Part(n, VisualKit.Box(0.12f, 0.012f, 0.012f), VisualKit.Glow(new Color(0.85f, 0.95f, 1f), 1.6f), Vector3.Zero, new Vector3(0, 0, i * 60));
                VisualKit.Part(n, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(0.85f, 0.95f, 1f), 2f), Vector3.Zero);
                break;
            case "flor":
                for (var i = 0; i < 5; i++)
                {
                    var a = i / 5f * Mathf.Tau;
                    VisualKit.Part(n, VisualKit.Sphere(0.02f), VisualKit.Mat(new Color(0.98f, 0.97f, 0.93f), 0.8f), new Vector3(Mathf.Cos(a) * 0.025f, Mathf.Sin(a) * 0.025f, 0), null, new Vector3(1f, 1f, 0.4f));
                }
                VisualKit.Part(n, VisualKit.Sphere(0.013f), VisualKit.Mat(new Color(1f, 0.8f, 0.2f), 0.6f), new Vector3(0, 0, 0.008f));
                VisualKit.Part(n, VisualKit.Sphere(0.04f), VisualKit.Mat(new Color(0.45f, 0.3f, 0.15f), 0.8f), new Vector3(0, 0, -0.02f), null, new Vector3(1.4f, 1f, 0.4f));   // laço de cipó
                break;
            case "sol":
                VisualKit.Part(n, VisualKit.Sphere(0.04f), VisualKit.Glow(new Color(1f, 0.85f, 0.35f), 2.4f), Vector3.Zero, null, new Vector3(1f, 1f, 0.5f));
                for (var i = 0; i < 8; i++) VisualKit.Part(n, VisualKit.Cylinder(0f, 0.012f, 0.05f, 4), ouro, new Vector3(Mathf.Cos(i / 8f * Mathf.Tau) * 0.06f, Mathf.Sin(i / 8f * Mathf.Tau) * 0.06f, 0), new Vector3(0, 0, i * 45 - 90));
                n.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.85f, 0.4f), LightEnergy = 0.4f, OmniRange = 1.2f, ShadowEnabled = false });
                break;
            case "lua":
                VisualKit.Part(n, new TorusMesh { InnerRadius = 0.035f, OuterRadius = 0.05f, Rings = 16, RingSegments = 6 }, ouro, Vector3.Zero, new Vector3(90, 0, 30), new Vector3(1f, 1f, 1f));
                VisualKit.Part(n, VisualKit.Sphere(0.012f), VisualKit.Glow(new Color(1f, 0.95f, 0.7f), 2.4f), new Vector3(0.0f, -0.07f, 0.01f));
                break;
            case "dragao":
                VisualKit.Part(n, VisualKit.Sphere(0.05f), ouro, Vector3.Zero, null, new Vector3(1.1f, 0.9f, 0.6f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(n, VisualKit.Cylinder(0f, 0.018f, 0.08f, 6), ouro, new Vector3(sd * 0.045f, 0.05f, -0.01f), new Vector3(0, 0, sd * -35));   // chifres
                    VisualKit.Part(n, VisualKit.Box(0.02f, 0.008f, 0.01f), VisualKit.Glow(new Color(1f, 0.3f, 0.1f), 2.5f), new Vector3(sd * 0.02f, 0.005f, 0.035f), new Vector3(0, 0, sd * -20));
                }
                break;
            case "relogio":
                VisualKit.Part(n, VisualKit.Cylinder(0.06f, 0.06f, 0.015f, 20), ouro, Vector3.Zero, new Vector3(90, 0, 0));
                VisualKit.Part(n, VisualKit.Cylinder(0.05f, 0.05f, 0.017f, 20), VisualKit.Mat(new Color(0.95f, 0.92f, 0.82f), 0.5f), new Vector3(0, 0, 0.002f), new Vector3(90, 0, 0));
                VisualKit.Part(n, VisualKit.Box(0.004f, 0.04f, 0.004f), preto, new Vector3(0, 0.015f, 0.012f));
                VisualKit.Part(n, VisualKit.Box(0.03f, 0.004f, 0.004f), preto, new Vector3(0.012f, 0, 0.012f));
                VisualKit.Part(n, new TorusMesh { InnerRadius = 0.02f, OuterRadius = 0.035f, Rings = 10, RingSegments = 4 }, ouro, new Vector3(0.05f, -0.09f, 0), new Vector3(90, 0, 0));   // engrenagem
                for (var i = 0; i < 4; i++) VisualKit.Part(n, new TorusMesh { InnerRadius = 0.007f, OuterRadius = 0.013f, Rings = 8, RingSegments = 4 }, ouro, new Vector3(0.02f + i * 0.008f, -0.06f - i * 0.018f, 0), new Vector3(0, i % 2 == 0 ? 0 : 90, 0));
                break;
            default: n.Free(); return null;
        }
        return n;
    }

    private void ReapplyItemCape()
    {
        if (_wingsKind?.StartsWith("capa:") == true) SetItemCape(_wingsKind);
    }

    // ------------------------------------------------------------ inimigos humanoides no mesmo boneco
    // Pedido do usuário: goblins, orcs, gigantes, magos inimigos… todos a partir do manequim (não dos "palitinhos"):
    // muda o corpo (altura, largura, músculo), a cor da pele, e acrescenta orelha pontuda, presas, chifres, coroa,
    // olhos brilhando, capuz, capa, roupa e arma própria. A caracterização é por estilo (HumanoidStyle).

    private HumanoidStyle? _mon;
    private float _heightScale = 1f;

    /// <summary>Inimigo humanoide feito do manequim. null = modelo não carregou (quem chama usa o boneco antigo).</summary>
    public static MannequinVisual? Monster(HumanoidStyle style)
    {
        var v = TryCreate("monstro", style.Female);
        v?.ConfigureMonster(style);
        if (v is not null && style.Wings is { } asas) v.SetWings(asas);
        return v;
    }

    private void ConfigureMonster(HumanoidStyle s)
    {
        _mon = s;
        var thin = s.Robe || s.CasterHands;
        var girth = s.Girth > 0 ? s.Girth : s.Bulky ? 1.16f : thin ? 0.94f : 1f;
        _heightScale = s.Scale;
        _body.Scale = new Vector3(s.Scale * girth, s.Scale, s.Scale * girth * 0.96f);
        // pele: a textura do manequim tingida pela cor do monstro (goblin verde, troll musgo, gigante pedra…)
        foreach (var m in _baseMats)
        {
            var c = s.Skin;
            m.AlbedoColor = new Color(Mathf.Min(1f, c.R * 2.1f + 0.05f), Mathf.Min(1f, c.G * 2.1f + 0.05f), Mathf.Min(1f, c.B * 2.1f + 0.05f));
            if (s.BodyGlow is { } g) { m.EmissionEnabled = true; m.Emission = g; m.EmissionEnergyMultiplier = 0.6f; }
            if (s.Bone) { m.AlbedoTexture = null; m.AlbedoColor = new Color(0.80f, 0.76f, 0.66f); m.Roughness = 0.75f; }
        }
        if (s.Bone && _back is null) _back = Socket("Spine2");
        if (s.Bone) BuildSkeletonBones();
        Bulk = s.Bulky ? 0.06f : thin ? 0.004f : 0.022f;
        MonsterFeatures(s);
        _bodyModel = "#";
        BuildOutfit("monstro");
        SetWeapon(s.FixedWeapon);
        // a arma do monstro cresce com o corpo, mas não com a largura; marreta/clava de gigante era desenhada grande
        if (_weapon is not null)
            _weapon.Scale = Vector3.One / Mathf.Max(0.8f, girth) * (s.FixedWeapon is "maul" or "club_driftwood" ? 0.62f : 1f);
        if (s.Hood) { SetHead("head_hood"); if (_helmet is not null) Recolor(_helmet, s.Cloth); }
        if (s.CasterHands)
            foreach (var hand in new[] { _handL, _handR })
            {
                VisualKit.Part(hand, VisualKit.Sphere(0.045f), VisualKit.Glow(s.Accent.Lightened(0.2f), 2.6f), new Vector3(0, 0.09f, 0.02f));
                hand.AddChild(new OmniLight3D { LightColor = s.Accent, LightEnergy = 0.7f, OmniRange = 2.2f, Position = new Vector3(0, 0.1f, 0) });
            }
    }

    /// <summary>
    /// Esqueleto de verdade (pedido: "mais magro, que pareça esqueleto, com costelas e buracos"): o corpo liso some e no
    /// lugar entram ossos presos a cada osso do esqueleto animado — crânio com órbitas e mandíbula, coluna de vértebras,
    /// caixa de costelas com vãos, bacia, úmero/rádio, fêmur/tíbia finos, mãos e pés. Tudo segue a animação.
    /// </summary>
    private void BuildSkeletonBones()
    {
        _skin.Visible = false;
        var bone = VisualKit.Hide(new Color(0.86f, 0.82f, 0.70f), "pele", 0.7f, 8f);
        var dark = VisualKit.Mat(new Color(0.04f, 0.035f, 0.03f), 1f);
        Vector3 Rest(string b) => Bone(b) < 0 ? Vector3.Zero : _skelToRoot * _skel.GetBoneGlobalRest(Bone(b)).Origin;
        // osso comprido entre duas juntas (preso na de cima): cápsula fina com "cabeças" nas pontas
        void Long(string from, string to, float r)
        {
            if (Bone(from) < 0 || Bone(to) < 0) return;
            var a = Rest(from); var d = Rest(to) - a; var len = d.Length();
            if (len < 0.02f) return;
            var holder = Socket(from);
            var piece = new Node3D();
            holder.AddChild(piece);
            piece.Position = d * 0.5f;
            var up = d / len;
            var x = up.Cross(Vector3.Forward).LengthSquared() > 1e-3f ? up.Cross(Vector3.Forward).Normalized() : up.Cross(Vector3.Right).Normalized();
            piece.Basis = new Basis(x, up, x.Cross(up).Normalized());
            VisualKit.Part(piece, VisualKit.Capsule(r, len * 0.92f), bone, Vector3.Zero);
            VisualKit.Part(piece, VisualKit.Sphere(r * 1.7f), bone, new Vector3(0, len * 0.46f, 0));
            VisualKit.Part(piece, VisualKit.Sphere(r * 1.5f), bone, new Vector3(0, -len * 0.46f, 0));
        }
        foreach (var side in new[] { "Left", "Right" })
        {
            Long(side + "Arm", side + "ForeArm", 0.022f);
            Long(side + "ForeArm", side + "Hand", 0.017f);
            Long(side + "UpLeg", side + "Leg", 0.028f);
            Long(side + "Leg", side + "Foot", 0.022f);
            Long(side + "Foot", side + "ToeBase", 0.02f);
            Long(side + "Shoulder", side + "Arm", 0.016f);      // clavícula
            var hand = Socket(side + "Hand");
            for (var f = 0; f < 3; f++)                           // dedos ossudos
                VisualKit.Part(hand, VisualKit.Capsule(0.008f, 0.07f), bone, new Vector3((f - 1) * 0.018f, 0.06f, 0.01f));
        }
        Long("Spine", "Spine1", 0.02f);
        Long("Spine1", "Spine2", 0.02f);
        Long("Neck", "Head", 0.018f);
        // vértebras (anéis na coluna)
        foreach (var b in new[] { "Spine", "Spine1", "Spine2", "Neck" })
            if (Bone(b) >= 0) VisualKit.Part(Socket(b), VisualKit.Box(0.05f, 0.025f, 0.045f), bone, new Vector3(0, 0, -0.02f));
        // bacia
        var hips = Socket("Hips");
        VisualKit.Part(hips, new TorusMesh { InnerRadius = 0.07f, OuterRadius = 0.1f, Rings = 14, RingSegments = 6 }, bone, new Vector3(0, -0.03f, 0), new Vector3(15, 0, 0), new Vector3(1.25f, 1.6f, 0.85f));
        // caixa de costelas: arcos abertos atrás (vão entre uma costela e outra), esterno na frente
        var chest = _back ?? Socket("Spine2");
        for (var i = 0; i < 5; i++)
        {
            var y = 0.07f - i * 0.045f;
            var w = 0.115f - Mathf.Abs(i - 1.5f) * 0.012f;
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(chest, new TorusMesh { InnerRadius = w - 0.012f, OuterRadius = w, Rings = 12, RingSegments = 5 }, bone,
                    new Vector3(side * 0.012f, y, 0.02f), new Vector3(-12, side * 35f, 0), new Vector3(1f, 0.55f, 0.9f));
        }
        VisualKit.Part(chest, VisualKit.Box(0.03f, 0.2f, 0.02f), bone, new Vector3(0, -0.02f, 0.115f));
        // crânio: caixa redonda, mandíbula, órbitas fundas e buraco do nariz
        VisualKit.Part(_headSocket, VisualKit.Sphere(0.095f), bone, new Vector3(0, 0.1f, 0.005f), null, new Vector3(0.95f, 1f, 1.05f));
        VisualKit.Part(_headSocket, VisualKit.Box(0.11f, 0.035f, 0.08f), bone, new Vector3(0, 0.015f, 0.035f));
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(_headSocket, VisualKit.Sphere(0.026f), dark, new Vector3(side * 0.035f, 0.105f, 0.075f), null, new Vector3(1f, 1.1f, 0.6f));
        VisualKit.Part(_headSocket, VisualKit.Cylinder(0.0f, 0.014f, 0.025f, 3), dark, new Vector3(0, 0.068f, 0.092f), new Vector3(180, 0, 0));
        for (var t = -2; t <= 2; t++)
            VisualKit.Part(_headSocket, VisualKit.Box(0.012f, 0.016f, 0.01f), bone, new Vector3(t * 0.016f, 0.045f, 0.088f));
    }

    /// <summary>Traços da cabeça: olhos, orelhas pontudas, nariz/presas, chifres, coroa, barba.</summary>
    private void MonsterFeatures(HumanoidStyle s)
    {
        var skin = (Material)_baseMats[0];
        FemininaFeatures(s);
        var eye = s.EyesGlow ? VisualKit.Glow(s.Eyes, 3f) : VisualKit.Mat(s.Eyes, 0.3f);
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(_headSocket, VisualKit.Sphere(0.016f), eye, new Vector3(side * 0.034f, 0.105f, 0.088f), null, new Vector3(1.2f, 0.75f, 0.6f));
        var bone = VisualKit.Mat(new Color(0.85f, 0.80f, 0.68f), 0.5f);
        if (s.Ears)
            foreach (var side in new[] { -1f, 1f })
            {
                // orelha de goblin: folha comprida e pontuda para os lados e para trás
                var ear = VisualKit.Part(_headSocket, VisualKit.Cylinder(0.0f, 0.055f, 0.26f, 8), skin, new Vector3(side * 0.17f, 0.13f, -0.02f), new Vector3(0, side * 25f, side * -68f));
                ear.Scale = new Vector3(1f, 1f, 0.3f);
            }
        if (s.Nose)
        {
            var nose = VisualKit.Part(_headSocket, VisualKit.Cylinder(0.0f, 0.022f, 0.08f, 6), skin, new Vector3(0, 0.075f, 0.12f), new Vector3(78, 0, 0));
            nose.Scale = new Vector3(1f, 1f, 0.8f);
        }
        if (s.Tusks)
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(_headSocket, VisualKit.Cylinder(0.0f, 0.012f, 0.06f, 6), bone, new Vector3(side * 0.035f, 0.03f, 0.085f), new Vector3(-15, 0, side * -12f));
        if (s.Horns)
            foreach (var side in new[] { -1f, 1f })
            {
                var horn = VisualKit.Part(_headSocket, VisualKit.Cylinder(0.0f, 0.035f, 0.2f, 8), bone, new Vector3(side * 0.08f, 0.2f, -0.01f), new Vector3(-20, 0, side * -38f));
                VisualKit.Part(horn, VisualKit.Cylinder(0.0f, 0.02f, 0.1f, 6), bone, new Vector3(0, 0.12f, -0.02f), new Vector3(-35, 0, 0));
            }
        if (s.Crown)
        {
            var gold = VisualKit.Mat(new Color(0.85f, 0.66f, 0.28f), 0.3f, 0.9f);
            VisualKit.Part(_headSocket, VisualKit.Cylinder(0.11f, 0.105f, 0.04f, 16), gold, new Vector3(0, 0.19f, -0.005f));
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.Tau / 6f;
                VisualKit.Part(_headSocket, VisualKit.Cylinder(0f, 0.02f, 0.06f, 4), gold, new Vector3(Mathf.Sin(a) * 0.1f, 0.24f, Mathf.Cos(a) * 0.1f));
            }
        }
        if (s.Beard)
        {
            var b = VisualKit.Mat(s.Accent.Lightened(0.4f), 0.95f);
            var tip = VisualKit.Part(_headSocket, VisualKit.Capsule(0.045f, 0.24f), b, new Vector3(0, -0.11f, 0.075f), new Vector3(14, 0, 0));
            tip.Scale = new Vector3(1.1f, 1f, 0.6f);
        }
    }

    /// <summary>Roupa do inimigo: túnica/calça, manto longo (conjuradores), ombreiras (fortes), capa mole (saqueador).</summary>
    private void MonsterOutfit(HumanoidStyle s, List<Layer> layers, ref (Color Cloth, Color Trim, bool Glow)? mantle)
    {
        var unit = 1f / Mathf.Max(1e-4f, Relative(_skin, Root).Scale.X);
        var dark = s.Cloth.Darkened(0.35f);
        if (s.Robe)
        {
            layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh, 0f, 1f, s.Cloth, s.Accent, 0.012f, Pattern: 1));
            layers.Add(new Layer(Fore, 0.0f, 0.75f, s.Cloth, s.Accent, 0.04f * unit, Pattern: 1));
            layers.Add(new Layer(Thigh, 0.3f, 0.6f, s.Cloth, s.Accent, 0.02f * unit, Pattern: 1));
            layers.Add(new Layer(Shin, 0f, 1f, dark, s.Accent, 0.012f, Pattern: 1));
            layers.Add(new Layer(Belly, 0.53f, 0.585f, s.Accent, s.Accent, 0.024f, 0.5f, 0.4f));
            mantle = (dark, s.Accent, s.CasterHands);
        }
        else if (s.Bone)
        {
            // esqueleto: só a tanga em farrapos (costelas à mostra)
            layers.Add(new Layer(Belly | Thigh, 0.40f, 0.56f, dark, LeatherDark, 0.014f, Pattern: 1));
        }
        else
        {
            layers.Add(new Layer(Chest | Belly | Thigh, 0.40f, 0.84f, s.Cloth, dark, 0.012f, Pattern: 1));          // túnica
            layers.Add(new Layer(Thigh | Shin, 0.10f, 0.52f, dark, LeatherDark, 0.008f, Pattern: 1));             // calça
            layers.Add(new Layer(Belly | Thigh, 0.42f, 0.60f, dark, LeatherDark, 0.014f, Pattern: 2));            // tanga/cós (cobre a cueca)
            layers.Add(Belt());
            layers.Add(Boots(LeatherDark));
            if (s.Bulky)
            {
                var metal = s.Accent;
                layers.Add(new Layer(Arm, 0.70f, 0.86f, metal, Bronze, 0.03f, 0.4f, 0.75f, 4));                   // ombreiras
                layers.Add(new Layer(Fore, 0.4f, 1f, metal.Darkened(0.2f), Bronze, 0.02f, 0.45f, 0.7f, 4));        // braçadeiras
            }
            if (s.Cape) mantle = (s.Cloth, s.Accent, false);
        }
    }

    private static Color Ghostly(Color c) => new Color(0.35f, 0.5f, 0.7f).Lerp(c, 0.35f);

    /// <summary>
    /// Roupas dos NPCs: "npc:papel:variante:feminino[:avental]". Cada papel tem um corte; a variante troca as cores
    /// (assim dois aldeões nunca saem iguais).
    /// </summary>
    private static void NpcOutfit(string body, List<Layer> layers, ref float skirt, ref Color skirtColor, ref Color skirtTrim)
    {
        var parts = body.Split(':');
        var role = parts.Length > 1 ? parts[1] : "aldeao";
        var v = parts.Length > 2 && int.TryParse(parts[2], out var n) ? n : 0;
        var fem = parts.Length > 3 && parts[3] == "f";
        var apron = parts.Length > 4 && parts[4] == "avental";
        Color Pick(params Color[] cs) => cs[Math.Abs(v) % cs.Length];
        var tunics = new[] { new Color(0.42f, 0.30f, 0.20f), new Color(0.28f, 0.33f, 0.40f), new Color(0.30f, 0.36f, 0.22f),
            new Color(0.55f, 0.42f, 0.22f), new Color(0.45f, 0.22f, 0.20f), new Color(0.36f, 0.32f, 0.38f) };
        var trousers = new[] { new Color(0.22f, 0.18f, 0.14f), new Color(0.30f, 0.26f, 0.20f), new Color(0.18f, 0.19f, 0.22f) };
        var dresses = new[] { new Color(0.40f, 0.20f, 0.36f), new Color(0.48f, 0.16f, 0.18f), new Color(0.30f, 0.36f, 0.24f),
            new Color(0.56f, 0.44f, 0.24f), new Color(0.22f, 0.30f, 0.42f) };
        switch (role)
        {
            case "ferreiro":
                layers.Add(new Layer(Chest | Belly | Arm, 0.44f, 0.86f, Pick(Linen, new Color(0.45f, 0.42f, 0.38f)), LeatherDark, 0.008f, Pattern: 1)); // camisa
                layers.Add(new Layer(Thigh | Shin, 0.10f, 0.54f, Pick(trousers), LeatherDark, 0.008f, Pattern: 1));
                layers.Add(new Layer(Chest | Belly | Thigh, 0.28f, 0.80f, Leather, LeatherDark, 0.024f, 0.65f, Pattern: 2));             // avental de couro
                layers.Add(new Layer(Fore, 0.5f, 1f, LeatherDark, Bronze, 0.014f, 0.6f, Pattern: 2));                                    // luvas grossas
                layers.Add(Boots(LeatherDark));
                break;
            case "curandeira":
                layers.Add(new Layer(Chest | Belly, 0.50f, 0.82f, Pick(dresses), Gold, 0.012f, Pattern: 2));                              // corpete
                layers.Add(new Layer(Arm | Fore, 0.0f, 1f, Linen, Bronze, 0.008f, Pattern: 1));                                           // mangas
                layers.Add(Belt());
                skirt = 0.5f; skirtColor = Pick(dresses); skirtTrim = Bronze;
                if (apron) layers.Add(new Layer(Belly | Thigh, 0.28f, 0.58f, new Color(0.82f, 0.78f, 0.68f), Linen, 0.03f, Pattern: 1));
                layers.Add(Boots(LeatherDark));
                break;
            case "capita":
                layers.Add(new Layer(Chest | Belly | Arm | Thigh, 0.40f, 0.86f, Iron, IronTrim, 0.012f, 0.45f, 0.75f, 3));
                layers.Add(new Layer(Chest | Belly | Thigh, 0.34f, 0.82f, Bordo, Gold, 0.022f, 0.6f, Pattern: 1));
                layers.Add(new Layer(Arm, 0.72f, 0.86f, Bronze, Gold, 0.03f, 0.35f, 0.85f, 4));
                layers.Add(Belt());
                layers.Add(Boots(LeatherDark));
                break;
            case "guarda":
                layers.Add(new Layer(Chest | Belly | Arm | Thigh, 0.42f, 0.86f, Linen, new Color(0.45f, 0.38f, 0.28f), 0.012f, Pattern: 2)); // gibão
                layers.Add(new Layer(Chest | Belly | Thigh, 0.36f, 0.82f, Pick(new Color(0.24f, 0.30f, 0.40f), Bordo, new Color(0.26f, 0.32f, 0.24f)), Bronze, 0.02f, Pattern: 1));
                layers.Add(new Layer(Thigh | Shin, 0.10f, 0.50f, Pick(trousers), LeatherDark, 0.008f, Pattern: 1));
                layers.Add(Belt());
                layers.Add(Boots());
                break;
            case "batedora":
                layers.Add(new Layer(Chest | Belly | Arm | Thigh, 0.44f, 0.84f, Pick(Forest, new Color(0.36f, 0.30f, 0.22f)), Leather, 0.012f, Pattern: 1));
                layers.Add(new Layer(Thigh | Shin, 0.08f, 0.52f, new Color(0.33f, 0.26f, 0.18f), LeatherDark, 0.008f, Pattern: 1));
                layers.Add(new Layer(Chest, 0.62f, 0.84f, Leather, LeatherDark, 0.02f, 0.6f, Pattern: 2));
                layers.Add(Belt());
                layers.Add(Boots());
                break;
            case "mentor":
                layers.Add(new Layer(Chest | Belly | Arm | Fore | Thigh, 0.38f, 0.86f, Iron, IronTrim, 0.012f, 0.45f, 0.75f, 3));
                layers.Add(new Layer(Chest | Belly, 0.50f, 0.84f, new Color(0.40f, 0.42f, 0.46f), Bronze, 0.03f, 0.35f, 0.85f, 4));
                layers.Add(new Layer(Arm, 0.70f, 0.86f, new Color(0.40f, 0.42f, 0.46f), Bronze, 0.03f, 0.35f, 0.85f, 4));
                layers.Add(new Layer(Chest | Belly | Thigh, 0.30f, 0.62f, Pick(Bordo, new Color(0.22f, 0.24f, 0.28f)), Gold, 0.04f, Pattern: 1)); // saia de tecido
                layers.Add(Belt());
                layers.Add(Boots(LeatherDark));
                break;
            case "arcanista":
                layers.Add(new Layer(Chest | Belly | Arm | Fore, 0.40f, 0.86f, Pick(Violet, new Color(0.18f, 0.22f, 0.40f), new Color(0.36f, 0.30f, 0.24f)), Gold, 0.012f, Pattern: 1));
                layers.Add(Belt());
                skirt = 0.32f; skirtColor = Pick(Violet, new Color(0.18f, 0.22f, 0.40f), new Color(0.36f, 0.30f, 0.24f)); skirtTrim = Gold;
                layers.Add(Boots(LeatherDark));
                break;
            default: // aldeão: túnica, calça, cinto e botas (mulheres de vestido simples)
                if (fem)
                {
                    layers.Add(new Layer(Chest | Belly | Arm, 0.48f, 0.84f, Pick(dresses), Linen, 0.010f, Pattern: 1));
                    layers.Add(Belt());
                    skirt = 0.46f; skirtColor = Pick(dresses); skirtTrim = Linen;
                }
                else
                {
                    layers.Add(new Layer(Chest | Belly | Arm | Thigh, 0.40f, 0.84f, Pick(tunics), LeatherDark, 0.010f, Pattern: 1));
                    layers.Add(new Layer(Thigh | Shin, 0.10f, 0.52f, Pick(trousers), LeatherDark, 0.008f, Pattern: 1));
                    layers.Add(Belt());
                }
                if (apron) layers.Add(new Layer(Belly | Thigh, 0.28f, 0.58f, new Color(0.82f, 0.78f, 0.68f), Linen, 0.03f, Pattern: 1));
                layers.Add(Boots(Pick(Leather, LeatherDark)));
                break;
        }
    }

    private static readonly Color IronTrim = new(0.30f, 0.31f, 0.34f);
    private static Layer Boots(Color? c = null) => new(Shin | Foot, 0f, 0.20f, c ?? Leather, LeatherDark, 0.016f, 0.7f, Pattern: 2);
    private static Layer Belt() => new(Belly, 0.545f, 0.575f, LeatherDark, Bronze, 0.024f, 0.55f);

    private MeshInstance3D AddLayer(Layer l)
    {
        var mi = (MeshInstance3D)_skin.Duplicate();
        mi.Name = "Roupa";
        mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _skin.GetParent().AddChild(mi);
        mi.Skeleton = mi.GetPathTo(_skel);
        mi.Skin = _skin.Skin;
        var mat = new ShaderMaterial { Shader = _clothShader ??= new Shader { Code = ClothCode } };
        mat.SetShaderParameter("cloth", l.Cloth);
        mat.SetShaderParameter("trim", l.Trim);
        mat.SetShaderParameter("grow", l.Grow);
        mat.SetShaderParameter("regions", l.Regions);
        mat.SetShaderParameter("y_min", l.YMin);
        mat.SetShaderParameter("y_max", l.YMax);
        mat.SetShaderParameter("rough", l.Rough);
        mat.SetShaderParameter("metal", l.Metal);
        mat.SetShaderParameter("pattern", l.Pattern);
        mat.SetShaderParameter("emit", l.Emit ?? Colors.Black);
        mat.SetShaderParameter("camisa", l.Camisa ?? Colors.White);
        if (l.Pattern >= PTerno && MalhaComPosicao() is { } mp) mi.Mesh = mp;
        for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++) mi.SetSurfaceOverrideMaterial(s, null);
        mi.MaterialOverride = mat;
        return mi;
    }

    /// <summary>
    /// 17/11: cópia da malha do corpo com a posição de repouso (x e z, no espaço do boneco, divididos pela altura) gravada na
    /// cor do vértice (b, a) — a roupa "pintada" (camisa, gravata) sabe onde fica a frente do peito em qualquer pose.
    /// </summary>
    private ArrayMesh? MalhaComPosicao()
    {
        if (_malhaPos is not null) return _malhaPos;
        try
        {
            var src = _skin.Mesh;
            var nova = new ArrayMesh();
            for (var sfc = 0; sfc < src.GetSurfaceCount(); sfc++)
            {
                var arr = src.SurfaceGetArrays(sfc);
                var vs = VerticesEmRepouso(_skin, arr);
                float ymin = float.MaxValue, ymax = float.MinValue;
                foreach (var v in vs) { ymin = Mathf.Min(ymin, v.Y); ymax = Mathf.Max(ymax, v.Y); }
                var alt = Mathf.Max(0.3f, ymax - ymin);
                var cs = arr[(int)Mesh.ArrayType.Color].AsColorArray();
                if (cs is null || cs.Length != vs.Length) return null;
                for (var i = 0; i < vs.Length; i++)
                {
                    var v = vs[i];
                    cs[i] = new Color(cs[i].R, cs[i].G, 0.5f + v.X / alt * 2f, 0.5f + v.Z / alt * 2f);
                }
                arr[(int)Mesh.ArrayType.Color] = cs;
                if (OS.GetEnvironment("VITRINE_DEBUG_MALHA") == "1") { float bmin = 9, bmax = -9, amin = 9, amax = -9; foreach (var c in cs) { bmin = Mathf.Min(bmin, c.B); bmax = Mathf.Max(bmax, c.B); amin = Mathf.Min(amin, c.A); amax = Mathf.Max(amax, c.A); } GD.Print($"MALHA alt={alt} b={bmin}..{bmax} a={amin}..{amax} n={vs.Length}"); }
                var am = src as ArrayMesh;
                var fmt = am is null ? 0 : am.SurfaceGetFormat(sfc) & Mesh.ArrayFormat.FlagUse8BoneWeights;
                nova.AddSurfaceFromArrays(am?.SurfaceGetPrimitiveType(sfc) ?? Mesh.PrimitiveType.Triangles, arr, null, null, fmt);
            }
            return _malhaPos = nova;
        }
        catch (Exception e) { GD.PrintErr($"malha com posição: {e.Message}"); return null; }
    }
    private ArrayMesh? _malhaPos;

    /// <summary>Saia/manto: cone preso ao quadril (as pernas andam por dentro).</summary>
    /// <summary>
    /// 14/11 tarde (dono, com a arte do Yong: "sobretudo solto, com volume"): abas do sobretudo saindo da cintura até a
    /// canela, abertas na frente e abrindo para fora (painéis em leque; o vento/passo não as prende às pernas).
    /// </summary>
    private Node3D AbasDoSobretudo(Color pano, Color forro)
    {
        var node = new Node3D { Name = "Sobretudo" };
        _hips.AddChild(node);
        var m = VisualKit.Mat(pano, 0.75f); m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var f = VisualKit.Mat(forro, 0.8f);
        const int n = 11;
        for (var i = 0; i < n; i++)
        {
            // de 35° a 325° (0° = frente): a frente fica aberta
            var a = Mathf.DegToRad(35f + 290f * i / (n - 1));
            var giro = new Node3D { RotationDegrees = new Vector3(0, Mathf.RadToDeg(a), 0) };
            node.AddChild(giro);
            var costas = Mathf.Abs(Mathf.Cos(a / 2f)) < 0.5f;   // atrás é mais comprido (rabo do sobretudo)
            var compr = costas ? 0.58f : 0.48f;
            var aba = new Node3D { Position = new Vector3(0, 0.04f, 0.15f), RotationDegrees = new Vector3(costas ? 26f : 20f, 0, 0) };
            giro.AddChild(aba);
            VisualKit.Part(aba, VisualKit.Box(0.105f, compr, 0.012f), m, new Vector3(0, -compr / 2f, 0));
            VisualKit.Part(aba, VisualKit.Box(0.105f, 0.02f, 0.016f), f, new Vector3(0, -compr + 0.01f, 0));   // barra
        }
        // lapelas e gola levantada
        var gola = new Node3D { Name = "GolaSobretudo" };
        Socket("Spine2").AddChild(gola);
        _outfit.Add(gola);
        foreach (var sd in new[] { -1f, 1f })
            VisualKit.Part(gola, VisualKit.Box(0.06f, 0.16f, 0.012f), f, new Vector3(sd * 0.055f, 0.13f, 0.1f), new Vector3(-12, sd * -18, sd * 18));
        return node;
    }

    private Node3D AddSkirt(float length, Color cloth, Color trim, float flare = 0.32f)
    {
        var node = new Node3D { Name = "Saia" };
        _hips.AddChild(node);
        var m = VisualKit.Mat(cloth, 0.9f);
        m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        VisualKit.Part(node, VisualKit.Cylinder(0.16f, 0.16f + length * flare, length, 16), m, new Vector3(0, -length / 2f + 0.06f, 0));
        VisualKit.Part(node, VisualKit.Cylinder(0.16f + length * flare, 0.165f + length * flare, 0.03f, 16), VisualKit.Mat(trim, 0.5f, 0.3f),
            new Vector3(0, -length + 0.07f, 0));
        return node;
    }

    private const string ClothCode = @"
shader_type spatial;
render_mode cull_back;
uniform vec4 cloth : source_color = vec4(0.4, 0.3, 0.2, 1.0);
uniform vec4 trim : source_color = vec4(0.2, 0.15, 0.1, 1.0);
uniform vec4 emit : source_color = vec4(0.0);
uniform float grow = 0.01;
uniform int regions = 0;
uniform float y_min = 0.0;
uniform float y_max = 1.0;
uniform float rough = 0.85;
uniform float metal = 0.0;
uniform int pattern = 0;
uniform vec4 camisa : source_color = vec4(1.0);
varying float v_region;
varying float v_h;
varying vec3 v_obj;
varying vec2 v_xz;
void vertex() {
    v_region = COLOR.r * 15.0;
    v_h = COLOR.g;
    v_xz = vec2((COLOR.b - 0.5) * 0.5, (COLOR.a - 0.5) * 0.5);
    VERTEX += NORMAL * grow;
    // 17/11 (dono: «o cartola e o gravata tão parecendo mulher»): o paletó enche a cintura — a silhueta fica reta, de terno
    if (pattern >= 5) {
        int rg = int(v_region + 0.5);
        float cint = smoothstep(0.54, 0.61, v_h) * (1.0 - smoothstep(0.71, 0.77, v_h));
        if (rg == 1 || rg == 2) VERTEX += NORMAL * cint * 0.017;
    }
    v_obj = VERTEX;
}
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
void fragment() {
    int r = int(v_region + 0.5);
    if (((regions >> r) & 1) == 0 || v_h < y_min || v_h > y_max) discard;
    float edge = min(v_h - y_min, y_max - v_h);
    float band = (y_min > 0.001 || y_max < 0.999) ? 1.0 - smoothstep(0.004, 0.014, edge) : 0.0;
    vec3 c = cloth.rgb;
    float n = 0.0;
    if (pattern == 1) {        // tecido: trama fina
        n = (sin(UV.x * 1800.0) * sin(UV.y * 1800.0)) * 0.05 + (hash(floor(UV * 400.0)) - 0.5) * 0.06;
    } else if (pattern == 2) { // couro/acolchoado: costuras
        n = (step(0.92, fract(v_h * 60.0)) * -0.12) + (hash(floor(UV * 200.0)) - 0.5) * 0.05;
    } else if (pattern == 3) { // malha de anéis
        vec2 g = fract(UV * 900.0) - 0.5;
        n = (0.18 - length(g)) * 0.6;
    } else if (pattern == 4) { // placas: frisos horizontais
        n = step(0.85, fract(v_h * 22.0)) * -0.15;
    }
    c *= 1.0 + n;
    // 17/11: terno (5) e fraque (6) — camisa e gravata pintadas na frente do peito (x e z de repouso, em fração da altura)
    if (pattern >= 5) {
        n = (sin(UV.x * 1800.0) * sin(UV.y * 1800.0)) * 0.05;
        c = cloth.rgb * (1.0 + n);
        float ax = abs(v_xz.x);
        bool frente = v_xz.y > -0.004 && (r == 1 || r == 2);
        if (frente && pattern == 5) {
            float v = (v_h - 0.70) / 0.125;                       // 0 no esterno, 1 na base do pescoço
            if (v_h > 0.69 && v_h < 0.845 && ax < 0.007 + v * 0.032) c = camisa.rgb;      // camisa em V
            if (v_h > 0.615 && v_h < 0.84 && ax < (v_h > 0.81 ? 0.009 : 0.0065 + (0.81 - v_h) * 0.014)) c = trim.rgb;   // gravata (nó no alto, alarga embaixo)
            if (v_h > 0.615 && v_h < 0.625 && ax < 0.009) c = trim.rgb * 0.7;
        }
        if (frente && pattern == 6) {
            if (v_h > 0.66 && v_h < 0.845 && ax < 0.017) c = camisa.rgb;                    // peitilho
            if (v_h > 0.815 && v_h < 0.835 && ax < 0.016 - abs(v_h - 0.825) * 0.6) c = trim.rgb;   // gravata-borboleta
            if (v_h > 0.666 && v_h < 0.80 && ax < 0.0025 && fract(v_h * 45.0) < 0.35) c = vec3(0.1);   // botões
        }
    }
    c = mix(c, trim.rgb, pattern >= 5 ? 0.0 : band);
    ALBEDO = c;
    ROUGHNESS = rough - (pattern == 3 ? n * 0.5 : 0.0);
    METALLIC = mix(metal, 0.6, band * step(0.5, metal));
    EMISSION = emit.rgb * (0.6 + band);
}
";

    // ------------------------------------------------------------ animação

    private void PlayClip(string name, float blend)
    {
        if (_anim is null || name.Length == 0 || name == _clip) return;
        _clip = name;
        _anim.Play(name, blend);
    }

    public void Hit(Vector3 fromDirection)
    {
        _hitT = 0f;
        _hitDir = fromDirection;
    }

    /// <summary>Passos Fantasma: corpo quase transparente (o próprio jogador se vê um pouco mais).</summary>
    public void SetStealth(bool on, bool isMe)
    {
        _stealth = on;
        _stealthTarget = on ? (isMe ? 0.35f : 0.12f) : 1f;
    }
    private float _stealthTarget = 1f;

    public void Update(in VisualFrame f, float delta)
    {
        _lastDelta = delta;
        UpdateWings(f, delta);
        AlinharCintura();
        // 13/11: pano das armaduras novas — velocidade e curva (rad/s) do boneco
        var yaw = Root.GlobalRotation.Y;
        var giro = delta > 0 ? Mathf.AngleDifference(_balancoYaw, yaw) / delta : 0f;
        _balancoYaw = yaw;
        if (_armorParts.Count > 0) Balanco.Atualizar(_armorParts.Values, f.Speed, Mathf.Clamp(giro, -6f, 6f));
        UpdateGuardFx(f.Guarding, delta);
        // 10/11 (print do usuário): o escudo ficava deitado na barriga. Agora fica em pé, no lado do braço esquerdo,
        // com a frente para a frente e um pouco para fora (como na referência); só a posição acompanha o antebraço.
        if (_shield is { } sh && sh.IsInsideTree() && _foreL.IsInsideTree())
        {
            if (_shieldCheck) { _shieldScale = sh.GlobalBasis.Scale; _shieldCheck = false; }
            var up = Vector3.Up;
            var arm = _foreL.GlobalPosition;
            var left = arm - Root.GlobalPosition;
            left.Y = 0;
            if (left.LengthSquared() < 1e-4f) left = Root.GlobalBasis.X;
            left = left.Normalized();
            var forward = left.Cross(up).Normalized();
            // o escudo fica paralelo ao antebraço (a mão passa por trás, como numa alça) e a frente aponta para fora
            var hand = _handL.IsInsideTree() ? _handL.GlobalPosition : arm;
            var dir = hand - arm;
            dir.Y = 0;
            var front = left;
            if (dir.LengthSquared() > 1e-4f)
            {
                front = up.Cross(dir).Normalized();
                if (front.Dot(left) < 0) front = -front;
            }
            front = (front + forward * 0.35f).Normalized();
            var basis = Basis.LookingAt(-front, up).Scaled(_shieldScale);
            sh.GlobalTransform = new Transform3D(basis, arm.Lerp(hand, 0.7f) + front * 0.06f);   // punho no meio, colado no escudo
        }
        _time += delta;
        // ---- clipe base: parado / andando / correndo (velocidade do passo acompanha a do boneco)
        // 11/11 (pedido): com asas equipadas o personagem voa — flutua um pouco acima do chão batendo as asas
        var flying = _wingPivots.Count > 0 && f.Alive && !_mounted;
        // voo de verdade (tecla G, servidor deixa passar por cima de quase tudo): bem alto; só com asas: flutua baixo
        _flyLift = Mathf.MoveToward(_flyLift, flying ? (Flying ? FlyHeight : 0.85f) : 0f, delta * (Flying ? 2.4f : 1.6f));
        if (flying)
        {
            PlayClip(_idle, 0.3f);   // pernas soltas no ar (nada de "andar no vento")
            _clipSpeed = 0.6f;
        }
        else if (f.Alive && !_mounted)
        {
            // corrida: exatamente como era (pedido do usuário — a corrida original estava boa); só o andar é novo
            if (f.Speed > 3.4f) { PlayClip(_run, 0.18f); _clipSpeed = Mathf.Clamp(f.Speed / 5.2f, 0.7f, 1.5f); }
            else if (f.Speed > 0.35f) { PlayClip(_walk, 0.2f); _clipSpeed = Mathf.Clamp(f.Speed / 1.6f, 0.6f, 1.8f); }
            else { PlayClip(_idle, 0.3f); _clipSpeed = 1f; }
        }
        else if (_mounted) { PlayClip(_idle, 0.2f); _clipSpeed = 0.5f; }
        // 15/11: andando de lado ou recuando — clipe ao contrário e quadril virado para o passo (Visuals/PassoLateral.cs)
        var (sentidoPasso, giroPasso) = _passoLateral.Medir(Root, f.Speed, f.Alive && !_mounted && !flying, delta);
        _anim?.Advance(delta * _clipSpeed * sentidoPasso);
        if (giroPasso != 0f) { Pose("Hips", AxUp, giroPasso, 1f); Pose("Spine", AxUp, -giroPasso * 0.85f, 1f); }
        CarryWeapon(f, delta);
        if (_cape is { } cape && GodotObject.IsInstanceValid(cape) && !cape.IsQueuedForDeletion()) cape.Step(delta);
        foreach (var ic in _itemCape)
            if (ic is ClothCape icc && !ReferenceEquals(icc, _cape) && GodotObject.IsInstanceValid(icc) && !icc.IsQueuedForDeletion()) icc.Step(delta);
        if (_skirt is { } sk && GodotObject.IsInstanceValid(sk) && !sk.IsQueuedForDeletion()) sk.Step(delta);
        if (_animal is not null)   // classe evoluída: nada da cabeça humana aparece (elmo/capuz/cabelo trocados depois também)
            foreach (var ch in _headSocket.GetChildren())
                if (ch is Node3D n3 && n3 != _animal && n3 != _helmet && n3.Visible) { n3.Visible = false; _hiddenForAnimal.Add(n3); }

        // ---- transparência (furtividade) e clarão de dano
        _stealthFade = VisualKit.Approach(_stealthFade, _stealthTarget, 4f, delta);
        ApplyFade(_stealthFade);

        // ---- morte: cai de joelhos e fica (pedido do usuário: antes caía de costas e "corria deitado", porque o clipe de
        // corrida continuava tocando). Some afundando depois, como os monstros (Main cuida do sumiço).
        if (!f.Alive)
        {
            if (_wasAlive) _deathT = 0f;
            _wasAlive = false;
            PlayClip(_idle, 0.15f);
            _deathT = Mathf.Min(1f, _deathT + delta / 0.55f);
            var kneel = Mathf.SmoothStep(0f, 1f, _deathT);
            _body.Rotation = Vector3.Zero;
            _flyLift = Mathf.MoveToward(_flyLift, 0f, delta * 3f);   // morreu voando: cai
            _body.Position = new Vector3(0, -0.42f * kneel * _body.Scale.Y + _shownLift + _flyLift, 0);
            _clipSpeed = 0.3f;
            return;
        }
        _wasAlive = true;
        // voando e andando: o corpo inclina para a frente (como quem plana)
        // 18/11 16h (dono: "o boneco não parece que está voando de fato, parece que tá duro no ar"): voando, o corpo inteiro
        // vive — inclina com a velocidade (até 30°), dá um tranco para trás a cada batida (a asa empurra), deita de lado na
        // curva (como quem plana) e, parado no ar, fica levemente para a frente balançando
        var yawAgora = Root.GlobalRotation.Y;
        var giroVel = delta > 0f ? Mathf.AngleDifference(_yawAnt, yawAgora) / delta : 0f;
        _yawAnt = yawAgora;
        var vooK = Mathf.Min(1f, _flyLift / 0.85f);
        _bank = Mathf.Lerp(_bank, Flying ? Mathf.Clamp(-giroVel * 0.22f, -0.5f, 0.5f) * Mathf.Clamp(f.Speed / 4f, 0.3f, 1f) : 0f, 1f - Mathf.Exp(-5f * delta));
        var tranco = Flying ? Mathf.Sin(_wingT + 1.2f) * 4f : 0f;
        _body.Rotation = _flyLift > 0.01f ? new Vector3(Mathf.DegToRad((Flying ? 6f + Mathf.Clamp(f.Speed * 3.2f, 0f, 24f) + tranco : Mathf.Clamp(f.Speed * 3.5f, 0f, 22f)) * vooK), 0, _bank * vooK) : Vector3.Zero;
        _shownLift = Mathf.Lerp(_shownLift, _groundLift, 1f - Mathf.Exp(-14f * delta));

        if (f.Action != _lastAction)
        {
            _blendT = 0f;
            if (f.Action.StartsWith("active", StringComparison.Ordinal)) _castFlash = 1f;
        }
        _lastAction = f.Action;
        _blendT = Mathf.Min(1f, _blendT + delta * 8f);
        _castFlash = Mathf.Max(0f, _castFlash - delta * 3f);

        _frame = f;
        _frame.Action ??= "";
        var acting = _frame.Action.Contains(':') && PoseKind(_frame.Action[(_frame.Action.IndexOf(':') + 1)..]) == "arco";
        if (acting) _bowHold = 0.3f; else _bowHold = Mathf.Max(0f, _bowHold - delta);   // terminou o tiro: volta logo para as costas (braço solto deixava o arco "voando")
        SlingBow(_bowHold > 0f);
        // 11/11 (roteiro AAA: "arma nas costas fora de combate"): sem golpe/guarda por 6 s, espada/machado/cajado vão para
        // as costas — some o braço solto atravessando a lâmina; o primeiro golpe traz de volta para a mão na hora
        if (_frame.Action.Contains(':') || _frame.Action == "guard" || _frame.Action.StartsWith("cast", StringComparison.Ordinal)) _meleeHold = 6f;
        else _meleeHold = Mathf.Max(0f, _meleeHold - delta);
        SheatheMelee(_meleeHold <= 0f);
        if (_hitT < 1f) _hitT = Mathf.Min(1f, _hitT + delta / 0.3f);
        // agachar (esquiva / Passos Fantasma) mexe no corpo inteiro; o resto é pose de osso (ModifyPose)
        if (f.Action == "dodge" && _dodgeSeq != f.ActionSeq) { _dodgeSeq = f.ActionSeq; _dodgeStart = _time; _dodgeBack = false; }
        // direção da esquiva: para trás = pulinho para trás; frente/lados/diagonal = deslizada (pedido do usuário)
        if (f.Action == "dodge" && _time - _dodgeStart < 0.12f && f.Speed > 2f) _dodgeBack = MoveDirLocal.Z < -0.5f;
        if (f.Action != "dodge") _dodgeSeq = -1;
        var crouch = _attackCrouch + (f.Action == "dodge" && !_mounted ? (_dodgeBack ? -HopHeight() : SlideWeight() * 0.8f)
            : f.Action.Contains("passos_fantasma") ? 0.2f : f.Action == "gather" ? 0.3f : f.Action == "sleep" ? 0.62f : f.Action == "sit" ? 0.36f : f.Action == "amarrado" ? 0.8f : f.Action == "pose_chao" ? 0.8f : f.Action == "perch" ? 0.38f : 0f);
        if (!SwingAlive() && !(f.Action ?? "").StartsWith("windup", StringComparison.Ordinal)) _attackCrouch = 0f;
        var bob = _flyLift > 0.01f ? Mathf.Sin(_wingT) * (Flying ? 0.07f : 0.03f) * Mathf.Min(1f, _flyLift / 0.85f) : 0f;   // sobe e desce com cada batida
        _body.Position = new Vector3(0, -crouch - 0.015f + _shownLift + _flyLift + bob, 0);   // sola encostada no chão (+ o que faltar para nada afundar)
        // esquiva = deslizada no chão (pedido: a cambalhota parecia "a bolinha do Sonic com quadros faltando")
        Malabares();
        if (_baloes is not null && GodotObject.IsInstanceValid(_baloes) && _baloes.IsInsideTree())
        {
            var esc = Root.GlobalBasis.Scale;
            _baloes.GlobalBasis = new Basis(new Vector3(0, 1, 0), _time * 0.3f).Rotated(Vector3.Right, Mathf.Sin(_time * 1.3f) * 0.06f)
                .Rotated(Vector3.Forward, Mathf.Sin(_time * 0.9f) * 0.05f).Scaled(esc);
        }
        if (_focus is not null)
        {
            _focus.Rotation = new Vector3(0, _time * 2.2f, 0);
            _focus.Scale = Vector3.One * (1f + _castFlash * 0.8f + Mathf.Sin(_time * 5f) * 0.08f);
        }
    }

    /// <summary>Depuração (VITRINE_MEDIR=1): velocidade "natural" dos clipes de andar/correr pelo passo do pé.</summary>
    public void MeasureClips()
    {
        if (_anim is null) return;
        var foot = Bone("LeftFoot");
        foreach (var clip in new[] { _walk, _run })
        {
            var len = (float)_anim.GetAnimation(clip).Length;
            _anim.Play(clip);
            float zmin = 1e9f, zmax = -1e9f;
            for (var i = 0; i <= 80; i++)
            {
                _anim.Seek(len * i / 80f, true, true); _anim.Advance(0);
                var gp = _skel.GetBonePose(foot);
                for (var pb = _skel.GetBoneParent(foot); pb >= 0; pb = _skel.GetBoneParent(pb)) gp = _skel.GetBonePose(pb) * gp;
                var z = (_skelToRoot * gp.Origin).Z;
                zmin = Mathf.Min(zmin, z); zmax = Mathf.Max(zmax, z);
            }
            GD.Print($"[clipe] {clip}: duração {len:0.000} s, pé vai de {zmin:0.000} a {zmax:0.000} m → ~{2f * (zmax - zmin) / len:0.00} m/s");
        }
    }

    private float _carry;   // 0 = arma na pose da mão; 1 = carregada para trás (andando/correndo sem atacar)
    private Transform3D? _weaponRest;

    /// <summary>
    /// Andando/correndo sem atacar, a arma vai apontada para trás e para baixo (como quem carrega): no clipe de corrida
    /// a mão sobe até o peito e, com a pegada fixa da mão, a lâmina/porrete atravessava a cabeça.
    /// </summary>
    private float _moveMix;

    private void CarryWeapon(in VisualFrame f, float delta)
    {
        if (_weapon is null || GearFactory.IsBow(_weaponModel) == true || _weaponModel?.StartsWith("arma_lend_manopla") == true || _weapon.GetParent() != _handR || !_handR.IsInsideTree()) return;
        _weaponRest ??= _weapon.Transform;
        // 11/11 noite ("a arma fica torta, virada para o lado errado, atravessando o boneco; ninguém segura assim"):
        // fora do golpe a arma nunca fica de pé na mão (subia por trás do braço e cortava o tronco). Parado: lâmina para
        // a frente e para baixo, um pouco para fora do corpo (guarda baixa); andando: ponta para trás e para baixo, rente
        // à perna. Cajado e lança continuam de pé (apoiados, como se segura de verdade).
        var upright = GearFactory.IsStaff(_weaponModel) || (_weaponModel?.Contains("lanca") ?? false) || (_weaponModel?.StartsWith("spear") ?? false)
                      || (_weaponModel?.StartsWith("talisman") ?? false);
        var attacking = (f.Action ?? "").Contains(':') || SwingAlive();
        var moving = f.Alive && f.Speed > 0.35f;
        // 11/11 noite (quadro: "correndo a arma já está na mão, mas segura de um jeito estranho, como adaga"): cajado,
        // lança e talismã também são guiados fora do golpe — de pé, ponta para cima e um pouco à frente (nunca invertido)
        _carry = Mathf.MoveToward(_carry, f.Alive && !attacking ? 1f : 0f, delta * 6f);
        _moveMix = Mathf.MoveToward(_moveMix, moving ? 1f : 0f, delta * 4f);
        if (_carry <= 0.001f) { _weapon.Transform = _weaponRest.Value; return; }
        var bone = Bone("RightHand");
        if (bone < 0) return;
        // transform global do encaixe da mão calculado da pose atual (o BoneAttachment só atualiza depois)
        var holderGlobal = _skel.GlobalTransform * _skel.GetBoneGlobalPose(bone) * _handR.Transform;
        var root = Root.GlobalTransform.Basis.Orthonormalized();
        // +Y da arma = cabo → ponta. Corpo: +Z = frente, −X = lado de fora do braço direito
        var idleDir = new Vector3(-0.28f, -0.55f, 0.79f).Normalized();     // frente, baixo e para fora
        // 11/11 noite ("correndo, a arma fica como adaga de ninja, ao contrário"): andando a ponta continua à frente,
        // só mais alta que parado (não arrasta no chão nem bate na perna)
        var walkDir = new Vector3(-0.32f, -0.30f, 0.90f).Normalized();
        var dir = idleDir.Slerp(walkDir, _moveMix).Normalized();
        if (upright) dir = new Vector3(-0.12f, 0.93f, 0.25f).Lerp(new Vector3(-0.1f, 0.88f, 0.42f), _moveMix).Normalized();
        var y = (root * dir).Normalized();
        var side = (root * Vector3.Up).Cross(y);                          // fio da lâmina na vertical (corta de cima para baixo)
        if (side.LengthSquared() < 1e-4f) side = root * Vector3.Right;
        var x = side.Normalized();
        var z = x.Cross(y).Normalized();
        var want = new Basis(x, y, z);
        var local = holderGlobal.Basis.Orthonormalized().Inverse() * want;
        var scale = _weaponRest.Value.Basis.Scale;
        var rest = _weaponRest.Value.Basis.Orthonormalized();
        var b = new Basis(rest.GetRotationQuaternion().Slerp(local.Orthonormalized().GetRotationQuaternion(), _carry)).Scaled(scale);
        _weapon.Transform = new Transform3D(b, _weaponRest.Value.Origin);
    }

    private VisualFrame _frame = new() { Action = "", Alive = true };
    private float _bowHold;

    /// <summary>Chamado pelo modificador do esqueleto depois da animação (as poses locais estão frescas).</summary>
    /// <summary>Musculatura: uma camada da cor da pele "inflada" sobre peito, braços e pernas (mesmo truque das roupas,
    /// que crescem pela normal). Guerreiro bem mais forte; os outros um pouco menos finos.</summary>
    public float Bulk { get; private set; }

    internal void ModifyPose()
    {
        if (!_frame.Alive)
        {
            // de joelhos: coxas para a frente, canelas deitadas para trás, tronco caído, cabeça baixa, braços largados
            var k = Mathf.SmoothStep(0f, 1f, _deathT);
            Pose("LeftUpLeg", AxL, -0.25f, k);
            Pose("RightUpLeg", AxL, -0.35f, k);
            Pose("LeftLeg", AxL, 1.95f, k);
            Pose("RightLeg", AxL, 1.95f, k);
            Pose("Spine", AxL, 0.45f, k);
            Pose("Spine1", AxL, 0.2f, k);
            Pose("Neck", AxL, 0.55f, k);
            AimArm("Left", new(0.2f, -0.95f, 0.25f), new(0.05f, -0.95f, 0.3f), k);
            AimArm("Right", new(0.2f, -0.95f, 0.25f), new(0.05f, -0.95f, 0.3f), k);
            KeepAboveGround();
            return;
        }
        if (_animal is not null && _mon is null && Bone("Head") is var ehb and >= 0)
        {
            // 12/11 ("como a cabeça é grande, ao colocar capacete é só trocar pela cabeçona"): de capacete, a cabeça do
            // bicho sai e o capacete aparece no tamanho dela (osso da cabeça aumentado); sem capacete, a do bicho volta
            var big = HelmOn;
            _animal.Visible = !big;
            _skel.SetBonePoseScale(ehb, Vector3.One * (big ? BigHelmScale : EvolvedHeadShrink));
            if (_helmet is not null) _helmet.Visible = big && _stealthFade > 0.5f;
            foreach (var (part, mi) in _armorParts)
                if (part.StartsWith("capacete", System.StringComparison.Ordinal)) mi.Visible = big;
        }
        // 12/11 ("tirei o capacete e ficou com uma cabecinha minúscula"): com armadura de conjunto (ombreiras e peito
        // volumosos) a cabeça do boneco parecia de alfinete; o osso da cabeça cresce 22% (o elmo, que é da mesma pele, junto)
        else if (_animal is null && _mon is null && Bone("Head") is var nhb and >= 0)
            // 18/11 17h (dono: "olha a cabecinha" com a armadura do Cavaleiro / "com o capacete fica gigante"): de armadura e SEM
            // capacete a cabeça cresce mais (1,22) para acompanhar os ombros; COM capacete de conjunto o osso fica normal —
            // o capacete já é largo e crescia junto
            _skel.SetBonePoseScale(nhb, Vector3.One * (_armorParts.Count == 0 ? 1f : _armorParts.Keys.Any(k => k.StartsWith("capacete", StringComparison.Ordinal)) || _helmet is not null ? 1.0f : 1.22f));
        // 14/11 tarde (dono: "classe 3 mais forte, tipo o leão da arte"): braços e coxas mais grossos (osso engrossa de
        // lado, não cresce no comprimento); a mão volta ao normal para a arma não engordar junto
        if (Leao3 && _mon is null)
            foreach (var (osso, k) in new[] { ("LeftArm", 1.5f), ("RightArm", 1.5f), ("LeftUpLeg", 1.3f), ("RightUpLeg", 1.3f), ("LeftHand", 1f / 1.5f), ("RightHand", 1f / 1.5f), ("LeftFoot", 1f / 1.3f), ("RightFoot", 1f / 1.3f) })
                if (Bone(osso) is var ob and >= 0) _skel.SetBonePoseScale(ob, new Vector3(k, 1f, k));
        if (_mon is { HeadScale: > 0 } hsc && Bone("Head") is var hb and >= 0)
            _skel.SetBonePoseScale(hb, Vector3.One * hsc.HeadScale);   // cabeção de goblin
        // 16/11: skin "sem cabeça" — a cabeça do boneco aparecia por dentro da chama azul; o osso encolhe até sumir
        if (_skinId == "sem_cabeca" && _mon is null && Bone("Head") is var semc and >= 0)
            _skel.SetBonePoseScale(semc, Vector3.One * 0.02f);
        // 18/11 21h: skin sapo — o rosto do boneco furava a cabeça de sapo (queixo saindo pelo focinho); o osso encolhe e a
        // cabeça de sapo (no encaixe da cabeça) é aumentada na mesma proporção, então fica do mesmo tamanho
        if (_skinId == "sapo" && _mon is null && Bone("Head") is var sapb and >= 0)
            _skel.SetBonePoseScale(sapb, Vector3.One * SapoOsso);
        if (_mon is { Hunch: > 0 } hs)
        {
            // corcunda permanente (goblin, saqueador, carniçal), cabeça erguida para olhar à frente
            Pose("Spine", AxL, hs.Hunch * 0.9f, 1f);
            Pose("Neck", AxL, -hs.Hunch * 0.7f, 1f);
        }
        if (_frame.Action == "morto")
        {
            // 13/11 (decoração: "corpo morto de enfeite"): deitado (o nó de fora gira o corpo), braços abertos, cabeça de lado
            AimArm("Left", new(0.85f, -0.35f, 0.15f), new(0.75f, -0.2f, 0.55f), 1f);
            AimArm("Right", new(0.8f, -0.5f, 0.05f), new(0.6f, -0.7f, 0.3f), 1f);
            Pose("LeftUpLeg", AxL, -0.12f, 1f); Pose("RightUpLeg", AxL, 0.08f, 1f);
            Pose("RightLeg", AxL, 0.35f, 1f);
            Pose("Head", AxUp, 0.7f, 1f);
            return;
        }
        if (_frame.Action == "sleep")
        {
            // 11/11 noite (quadro: "dormindo estão atravessando o chão"): o corpo descia sem dobrar as pernas — agora
            // sentado no chão de joelhos dobrados para cima (as canelas ficam por cima do chão)
            foreach (var side in new[] { "Left", "Right" })
            {
                Pose(side + "UpLeg", AxL, -1.75f, 1f);
                Pose(side + "Leg", AxL, 2.1f, 1f);
            }
            Pose("Spine", AxL, 0.55f, 1f);
            Pose("Neck", AxL, 0.6f + 0.04f * Mathf.Sin(_time * 1.5f), 1f);
            AimArm("Left", new(0.2f, -0.9f, 0.3f), new(0.1f, -0.6f, 0.8f), 1f);
            AimArm("Right", new(0.2f, -0.9f, 0.3f), new(0.1f, -0.6f, 0.8f), 1f);
            return;
        }
        // 10/11 (referência do usuário): com escudo, o braço esquerdo segura de guarda — antebraço à frente, levemente
        // erguido, punho no meio das costas do escudo (os golpes/defesa em ApplyActionPose sobrepõem)
        if (_shield is not null) AimArm("Left", new(0.25f, -0.9f, 0.25f), new(0.1f, 0.2f, 1f), 0.9f);
        ApplyActionPose(_frame);
        // 13/11 ("o boneco em animação normal parece estar com os braços para trás; deixa os braços normais"): parado e
        // sem fazer nada, os braços descem soltos ao lado do corpo, um tiquinho à frente (o clipe de espera os jogava para trás)
        if ((_frame.Action ?? "").Length == 0 && _frame.Speed < 0.35f && !_mounted && !Flying && _mon is null)
        {
            var resp = 0.03f * Mathf.Sin(_time * 1.4f);
            if (_shield is null && _baloes is null) AimArm("Left", new(0.16f, -0.97f, 0.1f + resp), new(0.06f, -0.93f, 0.33f), 0.75f);
            if (_meleeHold <= 0f && _bowHold <= 0f) AimArm("Right", new(0.16f, -0.97f, 0.1f + resp), new(0.06f, -0.93f, 0.33f), 0.75f);
        }
        if (_baloes is not null && _frame.Action is not ("sit" or "sleep"))   // braço do buquê de balões: sempre erguido segurando
            AimArm("Left", new(0.3f, -0.55f, 0.55f), new(0.1f, 0.85f, 0.45f), 1f);
        if (_sheathT > 0f)
        {
            // pegar/guardar a arma nas costas: a mão sobe por cima do ombro direito e volta
            var k = Mathf.Sin(Mathf.Pi * (1f - _sheathT / SheathLen));
            AimArm("Right", new(0.25f, 0.8f, -0.35f), new(-0.55f, 0.55f, -0.55f), k);
            _sheathT = Mathf.Max(0f, _sheathT - _lastDelta);
        }
        // 11/11 noite ("ao voar, pés juntos e com a ponta para baixo, como se estivesse flutuando"): voando, as pernas
        // largam o passo do clipe, ficam retas e juntas, e os pés apontam para baixo; balançam de leve com a batida
        var flyW = Flying ? Mathf.Clamp(_flyLift / 0.85f, 0f, 1f) : Hover ? 1f : Mathf.Clamp(_flyLift / 0.85f, 0f, 1f);   // 18/11 19h: flutuando de asas também   // Hover: fantasma de cal (pernas de voo, sem asas)
        if (flyW > 0.01f)
        {
            foreach (var bone in new[] { "LeftUpLeg", "LeftLeg", "RightUpLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase" })
            {
                var b = Bone(bone);
                if (b >= 0) _skel.SetBonePoseRotation(b, _skel.GetBonePoseRotation(b).Slerp(_skel.GetBoneRest(b).Basis.GetRotationQuaternion(), flyW));
            }
            // referência do usuário (anjo flutuando): uma perna esticada, a outra com o joelho um pouco dobrado à frente,
            // coxas juntas (quase cruzando), pés esticados com a ponta para baixo
            var sway = 0.05f * Mathf.Sin(_wingT + 0.6f);
            if (Flying)
            {
                // 15/11 noite (dono: "não fez a pose voando igual o print" — o boneco amarelo de asas): NÃO é sentado. Corpo
                // em pé, pernas penduradas para baixo, coxas só um pouco à frente e juntas, joelhos dobrados com as canelas
                // para trás (uma mais que a outra), pés esticados; braços abertos para os lados e para baixo (~45°),
                // cotovelos levemente dobrados, mãos abertas — e tudo balança junto com a batida das asas
                var bal = Mathf.Sin(_wingT + 0.4f);
                // 18/11 16h: coluna e pescoço acompanham a batida (onda que sobe do quadril à cabeça) e as pernas balançam
                // mais solto — o corpo deixa de ser uma tábua pendurada nas asas
                Pose("Spine", AxL, 0.06f * Mathf.Sin(_wingT + 0.9f), flyW);
                Pose("Spine1", AxL, 0.05f * Mathf.Sin(_wingT + 0.3f), flyW);
                Pose("Neck", AxL, -0.07f * Mathf.Sin(_wingT - 0.2f) - 0.08f, flyW);   // a cabeça olha à frente
                Pose("Hips", AxL, 0.05f * Mathf.Sin(_wingT + 1.4f), flyW);
                bal *= 2.6f;
                Pose("LeftUpLeg", AxL, -0.42f + 0.05f * bal, flyW);
                Pose("RightUpLeg", AxL, -0.28f + 0.05f * bal, flyW);
                Pose("LeftLeg", AxL, 0.62f - 0.06f * bal, flyW);
                Pose("RightLeg", AxL, 0.95f - 0.06f * bal, flyW);
                Pose("LeftUpLeg", AxFwd, -0.06f, flyW);
                Pose("RightUpLeg", AxFwd, 0.08f, flyW);
                Pose("LeftFoot", AxL, 0.7f, flyW);
                Pose("RightFoot", AxL, 0.8f, flyW);
                if ((_frame.Action ?? "").Length == 0)
                {
                    // braços: o ombro sobe um pouco quando a asa desce (equilíbrio)
                    var ab = 0.08f * bal;
                    // 2ª referência (gárgula voando): braços pendurados um pouco abertos, cotovelos dobrados, mãos à frente
                    AimArm("Left", new(0.5f, -0.8f + ab, 0.2f), new(0.28f, -0.6f + ab, 0.75f), flyW);
                    AimArm("Right", new(0.5f, -0.8f + ab, 0.2f), new(0.28f, -0.6f + ab, 0.75f), flyW);
                }
            }
            else
            {
            // 18/11 19h (dono: "os pés precisam estar dobrados, como se estivesse flutuando"): os dois joelhos dobrados
            Pose("LeftUpLeg", AxL, -0.12f + sway, flyW);          // positivo = pé vai para trás
            Pose("RightUpLeg", AxL, -0.3f + sway, flyW);          // joelho direito um pouco mais à frente
            Pose("LeftLeg", AxL, 0.7f, flyW);
            Pose("RightLeg", AxL, 0.95f, flyW);                   // dobra o joelho (canela para trás)
            Pose("LeftUpLeg", AxFwd, -0.05f, flyW);               // coxas fechando uma na outra
            Pose("RightUpLeg", AxFwd, 0.05f, flyW);
            Pose("LeftFoot", AxL, 1.0f, flyW);                    // ponta do pé para baixo
            Pose("RightFoot", AxL, 1.0f, flyW);
            }
        }
        // 10/11 ("virando de um lado pro outro o boneco patina"): troca brusca de direção correndo vira uma derrapada —
        // corpo jogado para trás, pernas abertas firmando no chão — até o corpo terminar de virar
        var skidTarget = _frame.Speed > 2.5f && MoveDirLocal.Z < -0.2f && (_frame.Action ?? "").Length == 0 && !_mounted ? 1f : 0f;
        _skid = Mathf.MoveToward(_skid, skidTarget, _lastDelta * (skidTarget > _skid ? 10f : 5f));
        if (_skid > 0.01f)
        {
            Pose("Spine", AxL, -0.38f, _skid);
            Pose("Hips", AxL, -0.15f, _skid);
            Pose("LeftUpLeg", AxL, -0.55f, _skid);
            Pose("RightUpLeg", AxL, 0.35f, _skid);
            Pose("LeftLeg", AxL, 0.55f, _skid);
            Pose("RightLeg", AxL, 0.25f, _skid);
            AimArm("Left", new(0.8f, -0.1f, 0.4f), new(0.7f, 0.1f, 0.6f), _skid * 0.8f);
            AimArm("Right", new(0.8f, -0.1f, 0.4f), new(0.7f, 0.1f, 0.6f), _skid * 0.8f);
        }
        // 10/11 ("item de duas mãos: segurar com as duas mãos"): machado/martelo grande — a mão esquerda vai ao cabo
        // 12/11 ("segura arma de duas mãos com as mãos para trás, dentro do corpo — tem que ser à frente, cotovelos dobrados,
        // igual na vida real"): parado, andando ou correndo (sem golpe), guarda de duas mãos: braço direito desce rente ao
        // corpo com o cotovelo dobrado e o antebraço vem para a frente e para o meio; a lâmina sobe na diagonal à frente
        // (ponta na altura dos olhos); a mão esquerda segura o cabo logo abaixo da direita, no prolongamento do cabo.
        if (_weapon is not null && GearFactory.IsTwoHanded(_weaponModel) && !(_frame.Action ?? "").StartsWith("dodge") && !_mounted && !CarregandoBarril)
        {
            var semGolpe = (_frame.Action ?? "").Length == 0 && !Flying;
            var lanca = _weaponModel?.StartsWith("arma_lanca") == true;
            if (semGolpe)
            {
                var corre = Mathf.Clamp(_frame.Speed / 5f, 0f, 1f);
                AimArm("Right", new(0.18f, -0.86f, 0.42f + 0.1f * corre), new(-0.55f, 0.18f, 0.82f), 1f);
                Pose("Spine1", AxL, 0.08f, 1f);   // ombros um pouco à frente, sobre a arma
                _bladeDir = lanca ? new Vector3(-0.15f, 0.35f, 0.92f).Normalized() : new Vector3(-0.18f, 0.72f, 0.66f).Normalized();
                _bladeW = 1f;
            }
            ReachLeftHandToRight(1f, semGolpe ? new Vector3(-_bladeDir.X, _bladeDir.Y, _bladeDir.Z) : null);
        }
        if (_mounted)
        {
            Pose("LeftUpLeg", AxL, -1.35f, 1f);
            Pose("RightUpLeg", AxL, -1.35f, 1f);
            Pose("LeftLeg", AxL, 1.4f, 1f);
            Pose("RightLeg", AxL, 1.4f, 1f);
        }
        // reação a golpe: tranco do tronco na direção oposta à pancada
        if (_hitT < 1f)
        {
            var k = Mathf.Sin(_hitT * Mathf.Pi) * 0.35f;
            var local = Root.IsInsideTree() ? Root.GlobalTransform.Basis.Inverse() * _hitDir : _hitDir;
            var axis = new Vector3(local.Z, 0, -local.X);
            if (axis.LengthSquared() > 1e-4f) Pose("Spine1", axis.Normalized(), -k, 1f);
        }
        // 15/11: barril de pólvora abraçado — cotovelos abertos e para a frente, antebraços por baixo e em volta do barril,
        // tronco jogado um pouco para trás (contrapeso), joelhos meio dobrados e passo curto; balança com o passo
        _barrilW = Mathf.MoveToward(_barrilW, CarregandoBarril ? 1f : 0f, _lastDelta * 4f);
        if (_barrilW > 0.01f)
        {
            // arma e escudo vão para as costas (somem das mãos) enquanto carrega
            var passo = Mathf.Clamp(_frame.Speed / 2.5f, 0f, 1f) * Mathf.Sin(_time * 7f);
            // 15/11 (dono: "segurar o barril com as mãos embaixo dele"): cotovelos junto do corpo, antebraços para a frente
            // quase na horizontal, palmas para cima — o barril vai apoiado em cima das mãos
            AimArm("Left", new(0.22f, -0.95f, 0.2f), new(-0.18f, -0.08f, 0.98f), _barrilW);
            AimArm("Right", new(0.22f, -0.95f, 0.2f), new(-0.18f, -0.08f, 0.98f), _barrilW);
            Pose("LeftHand", AxFwd, 1.2f, _barrilW);    // palmas viradas para cima, sob o fundo do barril
            Pose("RightHand", AxFwd, -1.2f, _barrilW);
            Pose("Spine", AxL, -0.16f + 0.02f * passo, _barrilW);
            Pose("Spine1", AxL, -0.06f, _barrilW);
            Pose("Head", AxL, 0.12f, _barrilW);   // olha um pouco para baixo, por cima do barril
            Pose("LeftLeg", AxL, 0.18f, _barrilW * 0.6f);
            Pose("RightLeg", AxL, 0.18f, _barrilW * 0.6f);
        }
        var semArma = _barrilW > 0.5f;
        if (semArma || semArma != _armaGuardadaPeloBarril)
        {
            _armaGuardadaPeloBarril = semArma;
            foreach (var n in new[] { _weapon, _shield, _focus, _segundaArma }) if (n is not null && GodotObject.IsInstanceValid(n)) n.Visible = !semArma;
        }
        FecharMaos();
        KeepArmsOutOfBody();
        if (_barrilW < 0.5f) { AimBladeAlongSwing(); KeepWeaponOutOfBody(); }   // 15/11: carregando barril a arma está guardada
        FeedTrail();
        KeepAboveGround();
    }

    private float _groundLift, _shownLift;
    private int _dodgeSeq = -1;
    private float _dodgeStart;
    private const float SlideSeconds = 0.38f;
    private bool _dodgeBack;

    /// <summary>Altura do pulinho para trás (arco de 0,32 m durante a esquiva).</summary>
    private float HopHeight()
    {
        if (_dodgeSeq < 0) return 0f;
        var e = Mathf.Clamp((_time - _dodgeStart) / SlideSeconds, 0f, 1f);
        return Mathf.Sin(e * Mathf.Pi) * 0.32f;
    }

    /// <summary>Peso da pose de deslizada: entra rápido (0,07 s), segura e sai no último quarto.</summary>
    private float SlideWeight()
    {
        if (_dodgeSeq < 0) return 0f;
        var e = (_time - _dodgeStart) / SlideSeconds;
        if (e < 0.18f) return Mathf.SmoothStep(0f, 1f, e / 0.18f);
        if (e > 0.75f) return Mathf.SmoothStep(1f, 0f, Mathf.Clamp((e - 0.75f) / 0.35f, 0f, 1f));
        return 1f;
    }
    private static readonly (string Bone, float R)[] GroundBones =
    {
        ("LeftFoot", 0.04f), ("RightFoot", 0.04f), ("LeftToeBase", 0.03f), ("RightToeBase", 0.03f), ("LeftLeg", 0.06f), ("RightLeg", 0.06f),
        ("LeftHand", 0.04f), ("RightHand", 0.04f), ("Hips", 0.12f), ("Head", 0.1f),
    };

    /// <summary>
    /// Nada do corpo entra no chão (feedback: "os bonecos atravessam o chão"): depois da pose final mede o ponto mais
    /// baixo (pés, joelhos, mãos, quadril, cabeça) e, se afundou, ergue o corpo inteiro o que faltou (suave).
    /// </summary>
    private void KeepAboveGround()
    {
        if (!Root.IsInsideTree() || _mounted) { _groundLift = 0; return; }
        var ground = Root.GlobalPosition.Y;
        var sc = _body.Scale.Y;
        var lowest = float.MaxValue;
        foreach (var (bone, r) in GroundBones)
            if (Bone(bone) >= 0) lowest = Mathf.Min(lowest, BoneWorld(bone).Y - r * sc);
        if (lowest == float.MaxValue) return;
        var need = _shownLift + (ground - lowest);
        _groundLift = need > 0.02f ? Mathf.Min(need, 0.8f) : 0f;
    }

    /// <summary>Rastro da arma no ar enquanto o corte varre (o "vento cortado").</summary>
    private void FeedTrail()
    {
        var swinging = _swinging && (_frame.Action ?? "").Contains(':');
        if (!swinging) _swinging = false;
        if (_weapon is null || _weaponSpan == Vector2.Zero || _weapon.GetParent() != _handR || !_handR.IsInsideTree())
        {
            _trail?.Stop();
            return;
        }
        if (_trail is null || !GodotObject.IsInstanceValid(_trail))
        {
            _trail = new WeaponTrail(_classId == "guerreiro" ? new Color(1f, 0.96f, 0.88f) : new Color(0.85f, 0.9f, 1f));
            Root.CallDeferred(Node.MethodName.AddChild, _trail);   // dentro do modificador do esqueleto não pode adicionar na hora
        }
        var hand = Bone("RightHand");
        if (hand < 0) return;
        var w = _skel.GlobalTransform * _skel.GetBoneGlobalPose(hand) * _handR.Transform * _weapon.Transform;
        var len = _weaponSpan.Y;
        var a = w * new Vector3(0, len * 0.35f, 0);
        var b = w * new Vector3(0, len * 1.02f, 0);
        if (swinging) _trail.Push(a, b);
        else _trail.Stop();
    }

    private Vector2 _weaponSpan;
    private const float SwingSeconds = 0.2f;
    private int _swingSeq = -1, _swingCombo;
    private float _swingStart, _swingStrike = -1f, _swingE, _attackCrouch;
    private bool _swinging;

    /// <summary>Quadro-chave do golpe: braço da arma (direções no espaço do corpo: +X direita, +Y cima, +Z frente),
    /// giro do tronco, inclinação, perna da frente (ângulo; negativo = à frente) e quanto o corpo abaixa.</summary>
    private readonly record struct SwingKey(Vector3 Upper, Vector3 Fore, float Twist, float Bend, float Lead, float Drop);

    // Três golpes que se encadeiam (o fim de um é perto do começo do próximo), como um ciclo de combo de verdade:
    // 0) de revés, da ESQUERDA para a DIREITA, na altura do peito (pedido do usuário) — passo com a perna direita;
    // 1) diagonal de cima para baixo, da direita para a esquerda — passo com a esquerda;
    // 2) golpe pesado de cima, arma por trás da cabeça, desce no meio com passo longo e corpo baixo.
    // Cada um: [0] pronto, [1] armado (fim da preparação), [2] fim do corte (impacto já passou), [3] continuação.
    private static readonly SwingKey[][] Combos =
    {
        new SwingKey[]
        {
            new(new(0.3f, -0.6f, 0.5f), new(0.2f, -0.2f, 0.95f), 0f, 0.05f, 0f, 0f),
            new(new(-0.75f, 0.25f, 0.55f), new(-0.35f, 0.55f, -0.7f), 0.55f, 0.08f, 0.15f, 0.02f),
            new(new(0.7f, 0f, 0.7f), new(0.85f, 0.05f, 0.5f), -0.5f, 0.22f, -0.55f, 0.09f),
            new(new(0.6f, -0.35f, 0.45f), new(0.75f, -0.1f, 0f), -0.6f, 0.16f, -0.45f, 0.06f),
        },
        new SwingKey[]
        {
            new(new(0.6f, -0.35f, 0.45f), new(0.75f, -0.1f, 0f), -0.4f, 0.1f, 0f, 0.03f),
            new(new(0.8f, 0.55f, -0.1f), new(0.3f, 0.9f, -0.3f), -0.55f, 0f, 0.1f, 0.02f),
            new(new(-0.45f, -0.25f, 0.85f), new(-0.75f, -0.35f, 0.55f), 0.5f, 0.22f, -0.55f, 0.1f),
            new(new(-0.6f, -0.55f, 0.45f), new(-0.7f, -0.65f, 0.1f), 0.6f, 0.2f, -0.45f, 0.08f),
        },
        new SwingKey[]
        {
            new(new(-0.6f, -0.55f, 0.45f), new(-0.7f, -0.65f, 0.1f), 0.3f, 0.15f, 0f, 0.04f),
            new(new(0.25f, 0.95f, -0.15f), new(0f, 0.6f, -0.8f), -0.15f, -0.12f, 0.2f, 0f),
            new(new(0.1f, -0.1f, 1f), new(0f, -0.45f, 0.9f), 0.05f, 0.35f, -0.7f, 0.14f),
            new(new(0.1f, -0.45f, 0.85f), new(0f, -0.75f, 0.6f), 0f, 0.3f, -0.6f, 0.1f),
        },
    };

    private const float SwingWindup = 0.18f, SwingCut = 0.11f, SwingFollow = 0.22f, SwingFade = 0.2f;

    // ================================================================ golpes capturados (Meshy/Mixamo)

    /// <summary>Trecho de um clipe para um golpe: começo (preparação), instante do impacto e fim (continuação), em s.</summary>
    private readonly record struct HitSeg(string Clip, float Start, float Strike, float End);

    // Combo de 3 golpes do Meshy ("Triple Combo Attack", do usuário): impactos medidos pela velocidade da mão (0,73 / 1,50 / 2,20 s)
    private static readonly HitSeg[] WarriorHits =
    {
        new("combo3", 0.30f, 0.73f, 1.08f),
        new("combo3", 1.08f, 1.50f, 1.82f),
        new("combo3", 1.82f, 2.20f, 2.80f),
    };

    private sealed class MocapLib
    {
        public readonly Dictionary<string, (Animation Anim, List<(string Bone, int Track, bool Pos)> Tracks)> Clips = new();
    }

    private static readonly Dictionary<string, MocapLib?> _libs = new();

    // Soco sem arma (10/11, "Punch Combo" do Meshy/Mixamo do usuário): jab de esquerda (impacto ~0,70 s) e direto de
    // direita (~1,32 s), medidos pela extensão da mão; alterna a cada golpe
    private static readonly HitSeg[] PunchHits =
    {
        new("soco", 0.25f, 0.70f, 0.95f),
        new("soco", 0.95f, 1.32f, 1.75f),
    };

    private static readonly Dictionary<string, string> MeshyBoneNames = new()
    {
        ["Spine01"] = "Spine1", ["Spine02"] = "Spine2", ["neck"] = "Neck", ["head_end"] = "HeadTop_End",
    };

    private static MocapLib? WarriorLib() => Lib("anim_guerreiro");

    /// <summary>Animações capturadas de um .glb leve (Tools/importar_animacoes.py), lidas uma vez e guardadas.</summary>
    private static MocapLib? Lib(string file)
    {
        if (_libs.TryGetValue(file, out var cached)) return cached;
        _libs[file] = null;
        var scene = GlbCache.Instance(file);
        var player = scene is null ? null : GlbCache.Find<AnimationPlayer>(scene);
        if (player is null) { scene?.QueueFree(); return null; }
        var lib = new MocapLib();
        foreach (var name in player.GetAnimationList())
        {
            var anim = player.GetAnimation(name);
            var tracks = new List<(string, int, bool)>();
            for (var t = 0; t < anim.GetTrackCount(); t++)
            {
                var type = anim.TrackGetType(t);
                if (type is not (Animation.TrackType.Rotation3D or Animation.TrackType.Position3D)) continue;
                var path = anim.TrackGetPath(t).ToString();
                var bone = path[(path.LastIndexOfAny(new[] { '/', ':' }) + 1)..];
                var cut = bone.LastIndexOfAny(new[] { ':', '_' });
                if (bone.StartsWith("mixamorig") && cut >= 0) bone = bone[(cut + 1)..];
                if (MeshyBoneNames.TryGetValue(bone, out var mapped)) bone = mapped;
                var pos = type == Animation.TrackType.Position3D;
                if (pos && bone != "Hips") continue;
                tracks.Add((bone, t, pos));
            }
            lib.Clips[name.Contains('/') ? name[(name.LastIndexOf('/') + 1)..] : name] = (anim, tracks);
        }
        scene!.QueueFree();
        var result = lib.Clips.Count > 0 ? lib : null;
        _libs[file] = result;
        GD.Print($"animações ({file}): {string.Join(", ", lib.Clips.Keys)}");
        return result;
    }

    /// <summary>Qual golpe capturado usar agora: soco (Guerreiro e Arqueiro sem arma; Mago e Bruxa soltam faísca) ou o combo do guerreiro armado.</summary>
    private (MocapLib Lib, HitSeg[] Hits)? CurrentMocap()
    {
        if (_mon is not null) return null;
        if (_weapon is null && _classId is "guerreiro" or "arqueiro" && Lib("anim_soco") is { } punch && punch.Clips.ContainsKey("soco")) return (punch, PunchHits);
        if (_classId == "guerreiro" && _weapon is not null && WarriorLib() is { } l && l.Clips.ContainsKey("combo3")) return (l, WarriorHits);
        return null;
    }

    private bool UseMocap() => CurrentMocap() is not null;

    /// <summary>
    /// Golpe com movimento capturado (pedido do usuário: "busque movimentos já prontos"): toca o trecho do combo do
    /// Meshy no relógio do jogo — a preparação do clipe é comprimida no tempo de preparação do servidor e o impacto do
    /// clipe cai exatamente quando o servidor aplica o dano; depois a continuação. Cada golpe da sequência usa um dos
    /// três golpes do combo. O deslocamento para a frente do clipe foi tirado (quem anda é o servidor).
    /// </summary>
    private void MocapSwing()
    {
        if (CurrentMocap() is { } mc) MocapSwing(mc.Lib, mc.Hits);
    }

    private void MocapSwing(MocapLib lib, HitSeg[] hits)
    {
        var seg = hits[_swingCombo % hits.Length];
        if (!lib.Clips.TryGetValue(seg.Clip, out var clip)) return;
        float tc, w;
        if (_swingStrike < 0f)
        {
            var p = Mathf.Clamp((_time - _swingStart) / SwingWindup, 0f, 1f);
            tc = Mathf.Lerp(seg.Start, seg.Strike, p);
            w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp((_time - _swingStart) / 0.06f, 0f, 1f));
            _swinging = false;
        }
        else
        {
            var e = _time - _swingStrike;
            var follow = SwingCut + SwingFollow;
            tc = Mathf.Lerp(seg.Strike, seg.End, Mathf.Clamp(e / follow, 0f, 1f));
            _swinging = e < 0.14f;
            var fade = e - follow;
            w = fade <= 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp(fade / SwingFade, 0f, 1f));
        }
        _swingE = w;
        _attackCrouch = 0f;
        foreach (var (bone, track, pos) in clip.Tracks)
        {
            var b = Bone(bone);
            if (b < 0) continue;
            if (pos)
            {
                var cur = _skel.GetBonePosePosition(b);
                var v = clip.Anim.PositionTrackInterpolate(track, tc);
                _skel.SetBonePosePosition(b, new Vector3(cur.X, Mathf.Lerp(cur.Y, v.Y, w), cur.Z));
            }
            else
            {
                var q = clip.Anim.RotationTrackInterpolate(track, tc);
                _skel.SetBonePoseRotation(b, _skel.GetBonePoseRotation(b).Slerp(q.Normalized(), w));
            }
        }
    }

    private bool SwingAlive() => _swingStrike >= 0f && _time - _swingStrike < SwingCut + SwingFollow + SwingFade;

    private static float EaseInOut(float x) => x * x * (3f - 2f * x);
    private static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

    private static SwingKey Mix(in SwingKey a, in SwingKey b, float k) => new(
        a.Upper.Normalized().Lerp(b.Upper.Normalized(), k).Normalized(), a.Fore.Normalized().Lerp(b.Fore.Normalized(), k).Normalized(),
        Mathf.Lerp(a.Twist, b.Twist, k), Mathf.Lerp(a.Bend, b.Bend, k), Mathf.Lerp(a.Lead, b.Lead, k), Mathf.Lerp(a.Drop, b.Drop, k));

    /// <summary>
    /// Golpe animado por quadros-chave num relógio próprio (liso, não depende das 10 fotos/s do servidor):
    /// preparação devagar e com antecipação (o corpo "carrega"), corte rápido, continuação que desacelera e volta.
    /// Perna da frente dá o passo; o corpo abaixa no impacto; o escudo recolhe na frente do peito.
    /// </summary>
    private void ApplySwing()
    {
        var keys = Combos[_swingCombo % Combos.Length];
        SwingKey k;
        float w;
        if (_swingStrike < 0f)
        {
            var p = Mathf.Clamp((_time - _swingStart) / SwingWindup, 0f, 1f);
            k = Mix(keys[0], keys[1], EaseInOut(p));
            w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp((_time - _swingStart) / 0.07f, 0f, 1f));
            _swinging = false;
        }
        else
        {
            var e = _time - _swingStrike;
            if (e < SwingCut) { k = Mix(keys[1], keys[2], EaseOut(e / SwingCut)); _swinging = true; }
            else { k = Mix(keys[2], keys[3], EaseOut(Mathf.Clamp((e - SwingCut) / SwingFollow, 0f, 1f))); _swinging = e < SwingCut + 0.06f; }
            var fade = e - SwingCut - SwingFollow;
            w = fade <= 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp(fade / SwingFade, 0f, 1f));
        }
        _swingE = w;
        AimArm("Right", k.Upper, k.Fore, w);
        Pose("Hips", AxUp, k.Twist * 0.35f, w);             // o quadril abre o giro, o tronco completa
        Pose("Spine1", AxUp, k.Twist * 0.75f, w);
        Pose("Spine", AxL, k.Bend, w);
        Pose("Neck", AxUp, -k.Twist * 0.55f, w);           // a cabeça segue o alvo enquanto o tronco gira
        var lead = _swingCombo % Combos.Length == 0 ? "Right" : "Left";
        var back = lead == "Right" ? "Left" : "Right";
        Pose(lead + "UpLeg", AxL, k.Lead, w);
        Pose(lead + "Leg", AxL, Mathf.Abs(k.Lead) * 0.55f, w);
        Pose(back + "UpLeg", AxL, -k.Lead * 0.45f, w);
        Pose(back + "Leg", AxL, Mathf.Abs(k.Lead) * 0.35f, w);
        _attackCrouch = k.Drop * w;
        _bladeDir = (k.Fore * 1.0f + k.Upper * 0.35f + new Vector3(0, 0, 0.15f)).Normalized();
        _bladeW = w;
    }

    private Vector3 _bladeDir;
    private float _bladeW;

    /// <summary>
    /// Durante o golpe a lâmina vira a continuação do braço (punho firme), como numa espadada de verdade: sem isso
    /// ela ficava de lado em relação ao antebraço e cruzava na frente do peito. O fio aponta no sentido do corte.
    /// </summary>
    private void AimBladeAlongSwing()
    {
        if (_bladeW <= 0.01f || _weapon is null || _weaponSpan == Vector2.Zero || _weapon.GetParent() != _handR || !_handR.IsInsideTree()) return;
        var hand = Bone("RightHand");
        if (hand < 0) return;
        var holder = (_skel.GlobalTransform * _skel.GetBoneGlobalPose(hand) * _handR.Transform).Basis.Orthonormalized();
        var root = Root.GlobalTransform.Basis.Orthonormalized();
        // as direções dos quadros-chave usam a convenção do AimArm (+X = para fora do braço); para o braço direito,
        // "para fora" no espaço do corpo é −X (o boneco olha para +Z)
        var y = (root * new Vector3(-_bladeDir.X, _bladeDir.Y, _bladeDir.Z)).Normalized();
        var side = y.Cross(Vector3.Up);
        if (side.LengthSquared() < 1e-4f) side = root * Vector3.Right;
        var x = side.Normalized();
        var z = x.Cross(y).Normalized();
        var want = new Basis(x, y, z);
        var local = holder.Inverse() * want;
        var cur = _weapon.Transform.Basis;
        var scale = cur.Scale;
        var q = cur.Orthonormalized().GetRotationQuaternion().Slerp(local.Orthonormalized().GetRotationQuaternion(), _bladeW);
        _weapon.Transform = new Transform3D(new Basis(q).Scaled(scale), _weapon.Transform.Origin);
        _bladeW = 0f;
    }
    private WeaponTrail? _trail;   // (menor Y, maior Y) da arma no próprio espaço: cabo → ponta

    /// <summary>Extensão da arma ao longo do seu eixo Y (cabo → ponta), somando todas as malhas.</summary>
    private static Vector2 MeasureSpan(Node3D weapon)
    {
        float lo = 0, hi = 0;
        void Walk(Node n, Transform3D t)
        {
            foreach (var c in n.GetChildren())
            {
                if (c is not Node3D c3) continue;
                var ct = t * c3.Transform;
                if (c3 is MeshInstance3D mi && mi.Mesh is not null)
                {
                    var box = mi.Mesh.GetAabb();
                    for (var i = 0; i < 8; i++) { var y = (ct * box.GetEndpoint(i)).Y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
                }
                Walk(c3, ct);
            }
        }
        Walk(weapon, Transform3D.Identity);
        return new Vector2(lo, hi);
    }

    /// <summary>
    /// Arma não atravessa o corpo (pedido do usuário): depois da pose final, mede pontos ao longo do cabo/lâmina contra
    /// cápsulas do tronco, da cabeça e das pernas; se algum ponto entrar, gira a arma em volta da mão até sair (com folga).
    /// Recomeça do encaixe de cada quadro (CarryWeapon), então não acumula.
    /// </summary>
    /// <summary>
    /// 13/11 ("a forma em que o personagem segura as armas está quebrada, com os braços atravessando o personagem toda
    /// hora"): depois da pose final, mede o cotovelo e a mão de cada lado contra o tronco (cápsula do quadril ao pescoço);
    /// se entrarem, abre o braço pelo ombro só o necessário para sair, com folga. Não acumula (a pose recomeça a cada quadro).
    /// </summary>
    private void KeepArmsOutOfBody()
    {
        if (!Root.IsInsideTree() || Bone("Hips") < 0 || _frame.Action is "sleep" or "morto" or "amarrado") return;
        var sc = _body.Scale.X * (1f + Bulk * 0.25f);
        var a = BoneWorld("Hips") + Vector3.Up * 0.06f * sc;
        var b = BoneWorld(Bone("Neck") >= 0 ? "Neck" : "Spine2");
        var r = 0.16f * sc;
        var inv = Root.GlobalBasis.Orthonormalized().Inverse();
        foreach (var side in new[] { "Left", "Right" })
        {
            var arm = side + "Arm";
            if (Bone(arm) < 0) continue;
            for (var it = 0; it < 2; it++)
            {
                var shoulder = BoneWorld(arm);
                float worst = 0; Vector3 pior = default, empurra = default;
                foreach (var pt in new[] { side + "ForeArm", side + "Hand" })
                {
                    if (Bone(pt) < 0) continue;
                    var p = BoneWorld(pt);
                    var ab = b - a;
                    var t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(1e-6f, ab.LengthSquared()), 0f, 1f);
                    var d = p - (a + ab * t);
                    d.Y = 0;
                    var len = d.Length();
                    var fundo = r + 0.035f - len;
                    if (fundo > worst)
                    {
                        worst = fundo; pior = p;
                        // para fora do lado do braço (e um pouco para a frente) quando está bem no meio
                        var fora = Root.GlobalBasis.X.Normalized() * (side == "Left" ? 1f : -1f);
                        empurra = len > 0.02f ? (d / len + fora * 0.5f).Normalized() : fora;
                    }
                }
                if (worst <= 0.005f) break;
                var de = pior - shoulder; var para = pior + empurra * worst - shoulder;
                if (de.LengthSquared() < 1e-6f || para.LengthSquared() < 1e-6f) break;
                var eixo = de.Cross(para);
                if (eixo.LengthSquared() < 1e-10f) break;
                Pose(arm, (inv * eixo).Normalized(), de.AngleTo(para), 1f);
            }
        }
    }

    private void KeepWeaponOutOfBody()
    {
        if (_weapon is null || _weaponSpan == Vector2.Zero || _weapon.GetParent() != _handR || !_handR.IsInsideTree()) return;
        var hand = Bone("RightHand");
        if (hand < 0 || Bone("Hips") < 0) return;
        var sc = _body.Scale.X * (1f + Bulk * 0.25f);
        Span<(Vector3 A, Vector3 B, float R)> body = stackalloc (Vector3, Vector3, float)[6];
        var n = 0;
        var hips = BoneWorld("Hips"); var neck = BoneWorld(Bone("Neck") >= 0 ? "Neck" : "Spine2");
        body[n++] = (hips, neck, 0.15f * sc);
        if (Bone("Head") >= 0) { var h = BoneWorld("Head"); body[n++] = (h + Vector3.Up * 0.04f * sc, h + Vector3.Up * 0.14f * sc, 0.12f * sc); }
        foreach (var (a, b) in new[] { ("LeftUpLeg", "LeftLeg"), ("RightUpLeg", "RightLeg"), ("LeftLeg", "LeftFoot"), ("RightLeg", "RightFoot") })
            if (Bone(a) >= 0 && Bone(b) >= 0) body[n++] = (BoneWorld(a), BoneWorld(b), (a.EndsWith("UpLeg") ? 0.085f : 0.065f) * sc);
        var holder = _skel.GlobalTransform * _skel.GetBoneGlobalPose(hand) * _handR.Transform;
        var w = holder * _weapon.Transform;
        var pivot = w.Origin;
        var grip = 0.14f;   // perto da mão não conta (a mão encosta no corpo sem problema)
        for (var it = 0; it < 3; it++)
        {
            float worst = 0; Vector3 worstP = default, push = default;
            for (var k = 0; k <= 10; k++)
            {
                var y = Mathf.Lerp(_weaponSpan.X, _weaponSpan.Y, k / 10f);
                if (Mathf.Abs(y) < grip) continue;
                var p = w * new Vector3(0, y, 0);
                for (var c = 0; c < n; c++)
                {
                    var (a, b, r) = body[c];
                    var ab = b - a;
                    var t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(1e-6f, ab.LengthSquared()), 0f, 1f);
                    var q = a + ab * t;
                    var d = p - q;
                    var len = d.Length();
                    var depth = r + 0.03f - len;
                    if (depth > worst)
                    {
                        worst = depth; worstP = p;
                        push = len > 1e-4f ? d / len : Root.GlobalBasis.X.Normalized();
                    }
                }
            }
            if (worst <= 0) break;
            var from = worstP - pivot;
            var to = worstP + push * worst - pivot;
            if (from.LengthSquared() < 1e-6f || to.LengthSquared() < 1e-6f) break;
            var axis = from.Cross(to);
            if (axis.LengthSquared() < 1e-10f) break;
            var ang = from.AngleTo(to);
            var rot = new Basis(axis.Normalized(), ang);
            w = new Transform3D(rot * w.Basis, pivot);
        }
        // de volta para o espaço do encaixe da mão (mantém a escala da arma)
        var local = holder.AffineInverse() * w;
        _weapon.Transform = local;
    }

    private readonly List<ShaderMaterial> _muscleMats = new();

    private Mesh? _origMesh;
    private static readonly Dictionary<(ulong, int), ArrayMesh> BulkCache = new();

    /// <summary>
    /// Musculatura no PRÓPRIO corpo: a malha do manequim é engrossada uma vez (pose de ligação) — cada vértice anda pela
    /// normal MÉDIA da sua posição (vértices duplicados nas quinas andam juntos: sem frestas nem "lascas"), com peso
    /// contínuo por região (peito/ombros/braços/coxas; mãos, pés e cabeça iguais). Mesma textura e material.
    /// </summary>
    private void ApplyMuscle(float bulk)
    {
        _origMesh ??= _skin.Mesh;
        if (bulk <= 0f || _origMesh is not ArrayMesh src) { _skin.Mesh = _origMesh; _unmasked = _origMesh; RefreshBodyMask(); return; }
        var key = (src.GetInstanceId(), (int)(bulk * 1000));
        if (!BulkCache.TryGetValue(key, out var mesh))
        {
            mesh = BulkMesh(src, bulk);
            BulkCache[key] = mesh;
        }
        _skin.Mesh = mesh;
        for (var sIdx = 0; sIdx < _baseMats.Count && sIdx < mesh.GetSurfaceCount(); sIdx++) _skin.SetSurfaceOverrideMaterial(sIdx, _baseMats[sIdx]);
        _unmasked = mesh;
        RefreshBodyMask();
    }

    private static ArrayMesh BulkMesh(ArrayMesh src, float bulk)
    {
        var box = src.GetAabb();
        var halfSpan = Mathf.Max(Mathf.Abs(box.Position.X), Mathf.Abs(box.End.X));
        float S(float e0, float e1, float x) { var t = Mathf.Clamp((x - e0) / (e1 - e0), 0f, 1f); return t * t * (3f - 2f * t); }
        float Weight(Vector3 v)
        {
            var h = (v.Y - box.Position.Y) / box.Size.Y;
            var ax = Mathf.Abs(v.X) / halfSpan;
            var knee = S(0.23f, 0.27f, h) * (1f - S(0.29f, 0.33f, h));   // joelho (bola do manequim) quase não engrossa
            var legs = S(0.08f, 0.16f, h) * (0.45f + 0.55f * S(0.30f, 0.36f, h)) * (1f - S(0.45f, 0.5f, h)) * (1f - 0.8f * knee);
            var torso = (1f - S(0.16f, 0.24f, ax)) * S(0.46f, 0.52f, h) * (0.55f + 0.45f * S(0.6f, 0.68f, h)) * (1f - S(0.79f, 0.83f, h));
            var arm = S(0.14f, 0.22f, ax) * S(0.6f, 0.66f, h) * (1f - 0.45f * S(0.4f, 0.52f, ax)) * (1f - S(0.7f, 0.78f, ax));
            return Mathf.Max(legs, Mathf.Max(torso, arm));
        }
        var sum = new Dictionary<Vector3I, Vector3>();
        Vector3I K(Vector3 v) => new((int)Mathf.Round(v.X * 20f), (int)Mathf.Round(v.Y * 20f), (int)Mathf.Round(v.Z * 20f));
        var all = new List<Godot.Collections.Array>();
        for (var sIdx = 0; sIdx < src.GetSurfaceCount(); sIdx++)
        {
            var arr = src.SurfaceGetArrays(sIdx);
            all.Add(arr);
            var vs = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var ns = arr[(int)Mesh.ArrayType.Normal].AsVector3Array();
            for (var i = 0; i < vs.Length; i++) { var k = K(vs[i]); sum[k] = sum.GetValueOrDefault(k) + ns[i]; }
        }
        var outMesh = new ArrayMesh();
        for (var sIdx = 0; sIdx < all.Count; sIdx++)
        {
            var arr = all[sIdx];
            var vs = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            for (var i = 0; i < vs.Length; i++) vs[i] += sum[K(vs[i])].Normalized() * bulk * Weight(vs[i]);
            arr[(int)Mesh.ArrayType.Vertex] = vs;
            outMesh.AddSurfaceFromArrays(src.SurfaceGetPrimitiveType(sIdx), arr, new Godot.Collections.Array<Godot.Collections.Array>(), new Godot.Collections.Dictionary(), src.SurfaceGetFormat(sIdx) & Mesh.ArrayFormat.FlagUse8BoneWeights);   // sem a compressão da importação
            outMesh.SurfaceSetMaterial(sIdx, src.SurfaceGetMaterial(sIdx));
        }
        return outMesh;
    }

    private void ApplyFade(float a)
    {
        // translúcido (Passos Fantasma): volta ao material original, que sabe ficar transparente
        if (_muscleMats.Count > 0)
            for (var sIdx = 0; sIdx < _muscleMats.Count && sIdx < _baseMats.Count; sIdx++)
                _skin.SetSurfaceOverrideMaterial(sIdx, a < 0.99f ? _baseMats[sIdx] : _muscleMats[sIdx]);
        var transparent = a < 0.99f;
        foreach (var m in _baseMats)
        {
            m.Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled;
            var c = m.AlbedoColor;
            m.AlbedoColor = new Color(c.R, c.G, c.B, a);
        }
        foreach (var n in _outfit) if (n is Node3D n3) n3.Visible = a > 0.5f;
        if (_weapon is not null) _weapon.Visible = a > 0.3f;
        if (_helmet is not null) _helmet.Visible = a > 0.5f && (_animal is null || HelmOn);
    }

    /// <summary>
    /// Gira um osso por um eixo do personagem (Root: +Z frente, +X esquerda do boneco, +Y cima), por cima da animação.
    /// </summary>
    /// <summary>
    /// 12/11 ("a perna faz movimentos que não condizem com a vida real"): a pose da deslizada era SOMADA em cima do clipe de
    /// corrida — a cada quadro a perna estava numa fase diferente da passada e a deslizada saía diferente (às vezes encolhida
    /// no ar). Antes de posar, as pernas e o tronco voltam para a pose de descanso (com o mesmo peso da deslizada).
    /// </summary>
    private void Neutral(string bone, float weight)
    {
        var b = Bone(bone);
        if (b < 0 || weight <= 0f) return;
        var rest = _skel.GetBoneRest(b).Basis.GetRotationQuaternion();
        _skel.SetBonePoseRotation(b, _skel.GetBonePoseRotation(b).Slerp(rest, Mathf.Clamp(weight, 0f, 1f)));
    }

    private void NeutralLegs(float weight)
    {
        foreach (var bone in new[] { "Hips", "Spine", "Spine1", "Spine2", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" })
            Neutral(bone, weight);
    }

    private void Pose(string bone, Vector3 axisRoot, float angle, float weight)
    {
        var b = Bone(bone);
        if (b < 0 || Mathf.IsZeroApprox(angle * weight)) return;
        var axis = (_toSkel * axisRoot).Normalized();
        // pose global montada aqui a partir das poses locais (a do esqueleto pode estar com o quadro anterior)
        var parent = _skel.GetBoneParent(b);
        var pg = parent >= 0 ? GlobalRotation(parent) : Basis.Identity;
        var g = pg * new Basis(_skel.GetBonePoseRotation(b));
        var newG = new Basis(axis, angle * weight) * g;
        var local = (pg.Inverse() * newG).Orthonormalized();
        _skel.SetBonePoseRotation(b, local.GetRotationQuaternion());
    }

    private Basis GlobalRotation(int bone)
    {
        var basis = new Basis(_skel.GetBonePoseRotation(bone));
        for (var p = _skel.GetBoneParent(bone); p >= 0; p = _skel.GetBoneParent(p))
            basis = new Basis(_skel.GetBonePoseRotation(p)) * basis;
        return basis.Orthonormalized();
    }

    // eixos do personagem: X = esquerda, Y = cima, Z = frente
    private static readonly Vector3 AxL = Vector3.Right, AxUp = Vector3.Up, AxFwd = Vector3.Back;

    /// <summary>Braço para a frente (ângulo positivo levanta à frente); lado = +1 esquerdo, -1 direito.</summary>

    /// <summary>
    /// Aponta o osso (o eixo Y dele, que vai até o filho) para uma direção do personagem, misturando com a animação pelo
    /// peso. Direção com x = "para fora" do lado do osso (espelhado entre esquerda e direita), y = cima, z = frente.
    /// </summary>
    private void Aim(string side, string bone, Vector3 dirOut, float w)
    {
        var b = Bone(side + bone);
        if (b < 0 || w <= 0.001f) return;
        var target = new Vector3(side == "Left" ? dirOut.X : -dirOut.X, dirOut.Y, dirOut.Z).Normalized();
        target = (_toSkel * target).Normalized();
        var parent = _skel.GetBoneParent(b);
        var pg = parent >= 0 ? GlobalRotation(parent) : Basis.Identity;
        var g = pg * new Basis(_skel.GetBonePoseRotation(b));
        var current = (g * Vector3.Up).Normalized();
        var dot = Mathf.Clamp(current.Dot(target), -1f, 1f);
        if (dot > 0.9999f) return;
        var axis = current.Cross(target);
        if (axis.LengthSquared() < 1e-8f) axis = g * Vector3.Right;
        var newG = new Basis(axis.Normalized(), Mathf.Acos(dot) * Mathf.Clamp(w, 0f, 1f)) * g;
        _skel.SetBonePoseRotation(b, (pg.Inverse() * newG).Orthonormalized().GetRotationQuaternion());
    }

    /// <summary>Posição do osso no espaço do esqueleto, pela pose atual (sem esperar o esqueleto atualizar).</summary>
    private Vector3 BonePos(int bone)
    {
        var parent = _skel.GetBoneParent(bone);
        var local = _skel.GetBonePosePosition(bone);
        return parent < 0 ? local : BonePos(parent) + GlobalRotation(parent) * local;
    }

    /// <summary>
    /// IK de dois ossos: leva a mão esquerda para logo abaixo da direita (no cabo da arma), cotovelo dobrando para baixo
    /// e para fora. Direções voltam para o espaço do personagem e passam pelo Aim de sempre.
    /// </summary>
    /// <param name="bladeChar">direção da lâmina no espaço do corpo (X do corpo, não a convenção do braço): a mão esquerda vai
    /// para o lado oposto, descendo o cabo. Sem ela, um palmo abaixo da mão direita.</param>
    private void ReachLeftHandToRight(float w, Vector3? bladeChar = null)
    {
        int arm = Bone("LeftArm"), fore = Bone("LeftForeArm"), hand = Bone("LeftHand"), right = Bone("RightHand");
        if (arm < 0 || fore < 0 || hand < 0 || right < 0) return;
        var s0 = BonePos(arm);
        var a = (BonePos(fore) - s0).Length();
        var b = (BonePos(hand) - BonePos(fore)).Length();
        if (a < 1e-4f || b < 1e-4f) return;
        var down = (_toSkel * Vector3.Down).Normalized();
        var target = bladeChar is { } bc
            ? BonePos(right) - (_toSkel * bc).Normalized() * (a + b) * 0.2f   // no cabo, abaixo da mão direita
            : BonePos(right) + down * (a + b) * 0.22f;                         // um palmo abaixo da mão direita
        var toT = target - s0;
        var d = Mathf.Clamp(toT.Length(), 0.05f, (a + b) * 0.999f);
        var dir = toT.Normalized();
        var cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
        var alpha = Mathf.Acos(cosA);
        var pole = (_toSkel * new Vector3(0.35f, -1f, 0f)).Normalized();   // para baixo e para fora (X = esquerda)
        var perp = (pole - dir * pole.Dot(dir));
        if (perp.LengthSquared() < 1e-6f) return;
        perp = perp.Normalized();
        var upper = dir * Mathf.Cos(alpha) + perp * Mathf.Sin(alpha);
        var elbow = s0 + upper * a;
        var foreDir = (s0 + dir * d - elbow).Normalized();
        var toChar = _toSkel.Inverse();
        Aim("Left", "Arm", toChar * upper, w);
        Aim("Left", "ForeArm", toChar * foreDir, w);
    }

    private void AimArm(string side, Vector3 upper, Vector3 fore, float w)
    {
        BracoForaDoCorpo(ref upper, ref fore);
        Aim(side, "Arm", upper, w);
        Aim(side, "ForeArm", fore, w);
    }

    /// <summary>
    /// 13/11 (regra do dono: "os BRAÇOS não entram no corpo — isso não existe na vida real"): antes de dobrar o braço,
    /// confere onde o cotovelo, o meio do antebraço e a mão vão parar em relação ao tronco (uma caixa do ombro ao quadril,
    /// com folga). Cotovelo dentro → o braço abre para fora; mão dentro → o antebraço vai para a frente do peito (se já
    /// estava indo para a frente) ou para fora. Direções no espaço do corpo, com X = para fora do lado do braço.
    /// </summary>
    private static void BracoForaDoCorpo(ref Vector3 upper, ref Vector3 fore)
    {
        const float LUp = 0.28f, LFore = 0.27f, Folga = 0.035f;
        // tronco visto do ombro: para dentro (x de -0,38 a 0), frente/trás (z de -0,13 a 0,13), do ombro ao quadril (y de -0,58 a 0,06)
        // 13/11 ("o boneco parado parece com os braços para trás"): o braço pendurado reto (x 0) contava como DENTRO do tronco
        // e era empurrado; o tronco começa 6 cm para dentro do ombro
        static bool Dentro(Vector3 p) => p.X < -0.06f + Folga && p.X > -0.38f - Folga && p.Z > -0.13f - Folga && p.Z < 0.13f + Folga && p.Y < 0.06f && p.Y > -0.58f;
        var u = upper.Normalized();
        for (var i = 0; i < 6 && Dentro(u * LUp); i++) { u.X += 0.18f; u = u.Normalized(); }
        var cotovelo = u * LUp;
        var f = fore.Normalized();
        for (var i = 0; i < 10; i++)
        {
            var meio = cotovelo + f * (LFore * 0.5f);
            var mao = cotovelo + f * LFore;
            if (!Dentro(meio) && !Dentro(mao)) break;
            if (f.Z >= 0.05f) f.Z += 0.2f; else f.X += 0.2f;   // vai para a frente do peito ou para fora
            f = f.Normalized();
        }
        upper = u; fore = f;
    }

    private void ApplyActionPose(in VisualFrame f)
    {
        var action = f.Action ?? "";
        if (_mounted && action == "dodge") return;   // 13/11: montado, quem dá o bote é o bicho (Main) — o cavaleiro fica na sela
        var t = Mathf.Clamp(f.ActionT, 0f, 1f);
        if (action == "dodge" && _dodgeBack)
        {
            // pulinho para trás: joelhos dobram no impulso e na aterrissagem, tronco levemente à frente, braços à frente
            var k = SlideWeight();
            NeutralLegs(k);
            var air = HopHeight() / 0.32f;
            Pose("Spine", AxL, 0.25f, k);
            Pose("LeftUpLeg", AxL, -0.45f - 0.35f * air, k);
            Pose("RightUpLeg", AxL, -0.25f - 0.35f * air, k);
            Pose("LeftLeg", AxL, 0.7f + 0.5f * air, k);
            Pose("RightLeg", AxL, 0.6f + 0.5f * air, k);
            AimArm("Left", new(0.4f, -0.3f, 0.85f), new(0.1f, 0.1f, 1f), k);
            AimArm("Right", new(0.4f, -0.3f, 0.85f), new(0.1f, 0.1f, 1f), k);
            return;
        }
        if (action == "dodge")
        {
            // deslizada de verdade (feedback: "com a coxa no chão"): corpo bem baixo, perna da frente esticada rente ao
            // chão, a de trás dobrada por baixo com a coxa deitada, tronco jogado para trás e a mão de trás apoiada no chão.
            var k = SlideWeight();
            NeutralLegs(k);
            Pose("Hips", AxL, -0.45f, k);
            Pose("Spine", AxL, -0.35f, k);
            Pose("Neck", AxL, 0.55f, k);
            Pose("RightUpLeg", AxL, -1.15f, k);              // perna da frente esticada rente ao chão (não chute alto)
            Pose("RightLeg", AxL, 0.08f, k);
            Pose("LeftUpLeg", AxL, -0.55f, k);
            Pose("LeftUpLeg", AxFwd, 0.5f, k);              // coxa de trás deitada para o lado
            Pose("LeftLeg", AxL, 2.2f, k);
            AimArm("Left", new(0.35f, -0.85f, -0.4f), new(0.2f, -0.95f, -0.15f), k);
            AimArm("Right", new(0.45f, -0.35f, 0.8f), new(0.3f, -0.2f, 0.93f), k);   // braço da frente baixo (arma não sobe)
            return;
        }
        if (action == "perch")
        {
            // 11/11 noite: gárgula agachada no alto do muro, mãos apoiadas na borda, cabeça baixa vigiando
            foreach (var side in new[] { "Left", "Right" })
            {
                Pose(side + "UpLeg", AxL, -1.7f, 1f);
                Pose(side + "Leg", AxL, 2.1f, 1f);
                Pose(side + "Foot", AxL, -0.4f, 1f);
            }
            Pose("Spine", AxL, 0.45f, 1f);
            Pose("Spine1", AxL, 0.25f, 1f);
            Pose("Neck", AxL, -0.35f + 0.08f * Mathf.Sin(_time * 0.7f), 1f);
            AimArm("Left", new(0.25f, -0.8f, 0.55f), new(0.1f, -0.9f, 0.4f), 1f);
            AimArm("Right", new(0.25f, -0.8f, 0.55f), new(0.1f, -0.9f, 0.4f), 1f);
            return;
        }
        if (action == "dive")
        {
            // mergulho: corpo deitado para a frente, braços para trás e garras à frente no fim
            var k = Mathf.Clamp(f.ActionT, 0f, 1f);
            Pose("Spine", AxL, 0.9f * (1f - 0.4f * k), 1f);
            foreach (var side in new[] { "Left", "Right" })
            {
                Pose(side + "UpLeg", AxL, 0.5f, 1f);
                Pose(side + "Leg", AxL, 0.6f, 1f);
            }
            var claws = Mathf.SmoothStep(0.5f, 1f, k);
            AimArm("Left", new(0.4f, -0.2f * (1 - claws), -0.9f + 1.8f * claws), new(0.1f, -0.1f, -0.9f + 1.9f * claws), 1f);
            AimArm("Right", new(0.4f, -0.2f * (1 - claws), -0.9f + 1.8f * claws), new(0.1f, -0.1f, -0.9f + 1.9f * claws), 1f);
            return;
        }
        // 13/11 (pedido: "botão para poses — dar tchau, cruzar os braços, ficar de boa, sentar no chão"): poses do jogador
        if (action.StartsWith("pose_", StringComparison.Ordinal))
        {
            switch (action)
            {
                case "pose_aceno":
                    AimArm("Right", new(0.75f, 0.65f, 0.1f), new(0.2f + 0.45f * Mathf.Sin(_time * 9f), 1f, 0.15f), 1f);
                    break;
                case "pose_bracos":
                    // braços cruzados: cotovelos um pouco à frente e para fora, antebraços na horizontal, um por cima do outro
                    AimArm("Left", new(0.3f, -0.85f, 0.4f), new(-0.9f, 0.02f, 0.42f), 1f);
                    AimArm("Right", new(0.3f, -0.8f, 0.45f), new(-0.9f, 0.1f, 0.4f), 1f);
                    Pose("Head", AxL, -0.08f, 1f);
                    break;
                case "pose_deboa":
                    // de boa: mãos na cintura (cotovelo para fora, mão apoiada no quadril pelo lado), peso numa perna
                    AimArm("Left", new(0.8f, -0.6f, -0.05f), new(-0.35f, -0.85f, 0.15f), 1f);
                    AimArm("Right", new(0.8f, -0.6f, -0.05f), new(-0.35f, -0.85f, 0.15f), 1f);
                    Pose("Hips", AxFwd, 0.08f, 1f);
                    Pose("LeftLeg", AxL, 0.18f, 1f);
                    Pose("Head", AxFwd, 0.08f * Mathf.Sin(_time * 1.3f), 1f);
                    break;
                case "pose_chao":
                    // sentado no chão relaxado: pernas para a frente com os joelhos meio dobrados, tronco um pouco para
                    // trás e as mãos apoiadas no chão atrás do quadril
                    Pose("LeftUpLeg", AxL, -1.5f, 1f); Pose("LeftLeg", AxL, 0.45f, 1f);
                    Pose("RightUpLeg", AxL, -1.5f, 1f); Pose("RightLeg", AxL, 0.15f, 1f);
                    Pose("Spine", AxL, -0.18f + 0.02f * Mathf.Sin(_time * 1.6f), 1f);
                    Pose("Head", AxL, 0.12f, 1f);
                    AimArm("Left", new(0.3f, -0.7f, -0.6f), new(0.15f, -0.95f, -0.2f), 1f);
                    AimArm("Right", new(0.3f, -0.7f, -0.6f), new(0.15f, -0.95f, -0.2f), 1f);
                    break;
                case "pose_danca":
                {
                    var b = Mathf.Sin(_time * 6f); var c = Mathf.Cos(_time * 3f);
                    AimArm("Left", new(0.55f, 0.6f + 0.25f * b, 0.2f), new(0.1f + 0.3f * c, 0.95f, 0.2f), 1f);
                    AimArm("Right", new(0.55f, 0.6f - 0.25f * b, 0.2f), new(0.1f - 0.3f * c, 0.95f, 0.2f), 1f);
                    Pose("Spine1", AxUp, 0.25f * b, 1f);
                    Pose("LeftUpLeg", AxL, -0.25f - 0.2f * Mathf.Max(0f, b), 1f);
                    Pose("RightUpLeg", AxL, -0.25f - 0.2f * Mathf.Max(0f, -b), 1f);
                    Pose("LeftLeg", AxL, 0.4f * Mathf.Max(0f, b), 1f);
                    Pose("RightLeg", AxL, 0.4f * Mathf.Max(0f, -b), 1f);
                    break;
                }
            }
            return;
        }
        if (action == "amarrado")
        {
            // 13/11: sentado no chão, pernas esticadas para a frente, mãos amarradas atrás do poste, cabeça baixa
            foreach (var side in new[] { "Left", "Right" })
            {
                Pose(side + "UpLeg", AxL, -1.5f, 1f);
                Pose(side + "Leg", AxL, 0.15f + 0.05f * Mathf.Sin(_time * 0.7f), 1f);
            }
            Pose("Spine", AxL, -0.12f, 1f);
            Pose("Head", AxL, 0.28f + 0.06f * Mathf.Sin(_time * 0.9f), 1f);
            AimArm("Left", new(0.3f, -0.55f, -0.75f), new(-0.35f, -0.25f, -0.9f), 1f);
            AimArm("Right", new(-0.3f, -0.55f, -0.75f), new(0.35f, -0.25f, -0.9f), 1f);
            return;
        }
        if (action == "sit")
        {
            // 13/11 ("sentar no banco como na vida real: pernas para a frente, bunda no assento, costas no encosto"):
            // coxas na horizontal, canelas na vertical, tronco reto encostado (um pouco para trás), mãos nas coxas
            foreach (var side in new[] { "Left", "Right" })
            {
                Pose(side + "UpLeg", AxL, -1.57f, 1f);
                Pose(side + "Leg", AxL, 1.57f, 1f);
            }
            Pose("Spine", AxL, -0.06f, 1f);
            Pose("Head", AxL, 0.05f, 1f);
            AimArm("Left", new(0.25f, -0.85f, 0.45f), new(0.15f, -0.35f, 0.92f), 1f);
            AimArm("Right", new(0.25f, -0.85f, 0.45f), new(0.15f, -0.35f, 0.92f), 1f);
            return;
        }
        if (action == "gather")
        {
            // ajoelhado mexendo no chão (colher, cortar, minerar)
            var k = Mathf.Min(1f, t * 4f);
            Pose("Spine", AxL, 0.45f, k);
            AimArm("Left", new(0.25f, -0.55f, 0.8f), new(0.0f, -0.7f, 0.7f), k);
            AimArm("Right", new(0.25f, -0.4f, 0.9f), new(-0.1f, -0.6f + 0.25f * Mathf.Sin(_time * 9f), 0.8f), k);
            return;
        }
        if (f.Guarding)
        {
            AimArm("Left", new(0.35f, -0.25f, 0.9f), new(-0.7f, 0.25f, 0.6f), 1f);
            return;
        }
        var colon = action.IndexOf(':');
        if (colon < 0 && SwingAlive()) { if (UseMocap()) MocapSwing(); else ApplySwing(); return; }
        if (colon < 0) { IdleHands(); return; }
        var phase = action[..colon];
        var skill = action[(colon + 1)..];
        // peso da pose: sobe na preparação, máximo no golpe, desce na recuperação
        var w = phase switch
        {
            "windup" => Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.6f)),
            "active" => 1f,
            "recover" => 1f - Mathf.SmoothStep(0f, 1f, t),
            _ => 0f,
        };
        var strike = phase == "active" || (phase == "recover" && t < 0.3f);
        if (PosePoder(PoseKind(skill), phase, w, strike)) return;   // gancho VFX 14/11 (Client/Efeitos/PosesPoderes.cs)
        switch (PoseKind(skill))
        {
            case "arco":
                // ombro esquerdo à frente, braço do arco esticado; mão da corda puxando até o queixo e soltando no tiro
                Pose("Spine1", AxUp, -0.55f, w);
                AimArm("Left", new(0.1f, 0.1f, 1f), new(0.05f, 0.12f, 1f), w);
                if (strike) AimArm("Right", new(0.8f, 0.1f, -0.55f), new(0.7f, 0.3f, -0.6f), w);
                else AimArm("Right", new(0.8f, 0.1f, -0.5f), new(-0.85f, 0.08f, 0.5f), w);
                break;
            case "cura":
                AimArm("Left", new(0.3f, -0.05f, 0.95f), new(0.05f, 0.3f, 0.95f), w);
                AimArm("Right", new(0.3f, -0.05f, 0.95f), new(0.05f, 0.3f, 0.95f), w);
                Pose("Spine1", AxL, 0.12f, w);
                break;
            case "barreira":
                AimArm("Left", new(0.9f, 0.25f, 0.4f), new(0.55f, 0.7f, 0.45f), w);
                AimArm("Right", new(0.9f, 0.25f, 0.4f), new(0.55f, 0.7f, 0.45f), w);
                break;
            case "aura":
                if (strike)
                {
                    AimArm("Left", new(0.95f, 0.05f, 0.3f), new(0.95f, 0.15f, 0.25f), w);
                    AimArm("Right", new(0.95f, 0.05f, 0.3f), new(0.95f, 0.15f, 0.25f), w);
                }
                else
                {
                    AimArm("Left", new(0.5f, -0.8f, 0.3f), new(0.6f, -0.6f, 0.5f), w);
                    AimArm("Right", new(0.5f, -0.8f, 0.3f), new(0.6f, -0.6f, 0.5f), w);
                }
                Pose("Neck", AxL, -0.2f, w);
                break;
            case "olhar":
                AimArm("Right", new(0.35f, 0.45f, 0.8f), new(-0.75f, 0.35f, -0.1f), w);
                Pose("Neck", AxUp, 0.4f * Mathf.Sin(_time * 3f), w);
                break;
            case "sombras":
                Pose("Hips", AxL, 0.35f, w);
                Pose("Spine", AxL, 0.45f, w);
                AimArm("Left", new(0.3f, -0.6f, 0.6f), new(0.1f, -0.2f, 1f), w);
                AimArm("Right", new(0.3f, -0.6f, 0.6f), new(0.1f, -0.2f, 1f), w);
                break;
            case "magia":
            {
                // empurra a mão para a frente (mão esquerda da Bruxa, direita do Mago com cajado)
                var hand = _classId == "bruxa" ? "Left" : "Right";
                if (strike) AimArm(hand, new(0.1f, 0.2f, 1f), new(0.0f, 0.2f, 1f), w);
                else AimArm(hand, new(0.5f, 0.0f, 0.6f), new(-0.6f, 0.55f, 0.5f), w);
                Pose("Spine1", AxUp, hand == "Left" ? -0.3f : 0.3f, w);
                break;
            }
            case "giro":
            {
                // braços abertos com a arma estendida; no golpe o tronco dá a volta inteira (o corpo gira junto)
                AimArm("Right", new(0.95f, 0.05f, 0.3f), new(0.95f, 0.0f, 0.3f), w);
                AimArm("Left", new(0.95f, 0.05f, 0.3f), new(0.9f, 0.1f, 0.4f), w * 0.8f);
                // 17/11: o Furacão do Pandemônio gira sem parar enquanto o servidor mantiver a fúria (não para no fim do "t")
                var turn = phase == "windup" ? -0.6f * t : phase == "active" ? (skill.Contains("furacao") || skill.Contains("giro") || skill == "skill_arma_onda" ? Mathf.PosMod(_time * 11f, Mathf.Tau) - Mathf.Pi : -0.6f + Mathf.Tau * t) : 0f;
                Pose("Hips", AxUp, turn, w);
                Pose("Spine", AxL, 0.15f, w);
                break;
            }
            case "cunha":
                // golpe de cima com as duas mãos: arma bem alta atrás da cabeça, depois desce rachando
                if (phase == "windup")
                {
                    AimArm("Right", new(0.15f, 1f, -0.2f), new(0.0f, 0.6f, -0.8f), w);
                    AimArm("Left", new(-0.1f, 1f, -0.15f), new(0.1f, 0.6f, -0.8f), w);
                    Pose("Spine1", AxL, -0.3f, w);
                }
                else
                {
                    AimArm("Right", new(0.05f, -0.4f, 0.9f), new(0.0f, -0.7f, 0.7f), w);
                    AimArm("Left", new(0.0f, -0.4f, 0.9f), new(0.05f, -0.7f, 0.7f), w);
                    Pose("Spine1", AxL, 0.45f, w);
                    Pose("Spine", AxL, 0.2f, w);
                }
                break;
            case "salto":
                // agacha, sobe com a arma no alto e desce batendo no chão
                if (phase == "windup") { Pose("Hips", AxL, 0.3f, w); Pose("LeftUpLeg", AxL, -0.6f, w); Pose("RightUpLeg", AxL, -0.6f, w); Pose("LeftLeg", AxL, 0.9f, w); Pose("RightLeg", AxL, 0.9f, w); AimArm("Right", new(0.3f, -0.6f, -0.5f), new(0.2f, -0.3f, -0.9f), w); }
                else if (strike) { AimArm("Right", new(0.1f, -0.5f, 0.85f), new(0.0f, -0.85f, 0.5f), w); AimArm("Left", new(-0.4f, -0.3f, 0.8f), new(-0.3f, -0.5f, 0.8f), w); Pose("Spine1", AxL, 0.5f, w); }
                else { AimArm("Right", new(0.2f, 1f, 0.1f), new(0.1f, 0.9f, -0.3f), w); Pose("Spine1", AxL, -0.2f, w); }
                break;
            case "estocadas":
            {
                // 13/11 ("o goblin dá 3 golpes mas não mexe a mão conforme o ataque, só vai pra frente — sem impulso nem
                // corrida"): preparação = recua o tronco, agacha e engatilha o braço atrás (o impulso); golpe = corre
                // inclinado para a frente dando uma estocada por golpe, braços alternados, o tronco torcendo junto
                if (phase == "windup")
                {
                    Pose("Spine", AxL, -0.18f * w, 1f);
                    Pose("Spine1", AxUp, -0.45f, w);
                    AimArm("Right", new(0.45f, -0.2f, -0.75f), new(0.1f, 0.35f, -0.9f), w);
                    AimArm("Left", new(-0.15f, -0.35f, 0.9f), new(0.2f, 0.25f, 0.9f), w);
                    _attackCrouch = Mathf.Max(_attackCrouch, 0.16f * w);
                    break;
                }
                var golpes = 3f;
                var tt = phase == "active" ? t : 1f;
                var i = Mathf.Min((int)(tt * golpes), (int)golpes - 1);
                var k = tt * golpes - i;                         // 0..1 dentro de cada golpe
                var estica = phase == "active" ? Mathf.Sin(Mathf.Clamp(k / 0.55f, 0f, 1f) * Mathf.Pi) : 0f;
                var mao = i % 2 == 0 ? "Right" : "Left";
                var outra = mao == "Right" ? "Left" : "Right";
                var lado = mao == "Right" ? 1f : -1f;
                Pose("Spine", AxL, 0.32f, w);                    // corrida inclinada
                Pose("Spine1", AxUp, lado * (0.5f * estica - 0.2f), w);
                Pose("Hips", AxUp, -lado * 0.15f * estica, w);
                AimArm(mao, new(0.15f, -0.05f, 1f), new(0.0f, 0.02f, 1f), w * Mathf.Max(0.35f, estica));
                AimArm(outra, new(0.35f, -0.5f, -0.5f), new(0.2f, 0.3f, -0.9f), w);
                Pose("Head", AxL, -0.15f, w);
                break;
            }
            case "investida":
                // corpo baixo e para a frente, arma apontada como lança
                Pose("Spine", AxL, 0.35f, w);
                Pose("Spine1", AxL, 0.15f, w);
                AimArm("Right", new(0.25f, -0.1f, 1f), new(0.05f, 0.0f, 1f), w);
                AimArm("Left", new(-0.3f, -0.6f, -0.6f), new(-0.2f, -0.5f, -0.8f), w);
                break;
            case "soco" when _mon is null && Lib("anim_soco") is { } punch && punch.Clips.ContainsKey("soco"):
                // 10/11: soco capturado ("Punch Combo" do usuário) em vez da pose montada à mão: jab e direto alternados
                if (phase == "windup" && _swingSeq != f.ActionSeq) { _swingSeq = f.ActionSeq; _swingStart = _time; _swingStrike = -1f; _swingCombo = f.ActionSeq % 2; }
                if (phase != "windup" && _swingStrike < 0f)
                {
                    if (_swingSeq != f.ActionSeq) { _swingSeq = f.ActionSeq; _swingStart = _time - 0.18f; _swingCombo = f.ActionSeq % 2; }
                    _swingStrike = _time;
                }
                MocapSwing(punch, PunchHits);
                break;
            case "soco":
                if (phase == "windup") { Pose("Spine1", AxUp, -0.5f, w); AimArm("Right", new(0.4f, -0.4f, -0.6f), new(0.0f, 0.5f, 0.8f), w); AimArm("Left", new(-0.2f, -0.3f, 0.9f), new(0.3f, 0.6f, 0.6f), w); }
                else { Pose("Spine1", AxUp, 0.45f, w); AimArm("Right", new(-0.05f, 0.05f, 1f), new(-0.05f, 0.05f, 1f), w); AimArm("Left", new(-0.3f, -0.4f, 0.7f), new(0.4f, 0.6f, 0.5f), w); }
                break;
            case "varrida":
                // arco largo da direita para a esquerda, bem aberto
                if (phase == "windup") { Pose("Spine1", AxUp, 0.7f, w); AimArm("Right", new(0.95f, 0.1f, -0.3f), new(0.9f, 0.1f, -0.4f), w); }
                else { Pose("Spine1", AxUp, -0.7f, w); AimArm("Right", new(-0.6f, 0.0f, 0.8f), new(-0.85f, 0.0f, 0.5f), w); }
                break;
            case "arco_ceu":
                // chuva de flechas: arco apontado para o céu
                Pose("Spine1", AxUp, -0.4f, w);
                Pose("Spine1", AxL, -0.3f, w);
                AimArm("Left", new(0.05f, 0.75f, 0.65f), new(0.0f, 0.8f, 0.6f), w);
                if (strike) AimArm("Right", new(0.7f, 0.5f, -0.5f), new(0.6f, 0.6f, -0.5f), w);
                else AimArm("Right", new(0.6f, 0.6f, -0.3f), new(-0.6f, 0.6f, 0.5f), w);
                break;
            case "arremesso":
                // arremesso: braço vai atrás da cabeça e lança por cima
                if (phase == "windup") { Pose("Spine1", AxUp, -0.4f, w); AimArm("Right", new(0.4f, 0.8f, -0.4f), new(0.1f, 0.5f, -0.85f), w); }
                else { Pose("Spine1", AxUp, 0.35f, w); Pose("Spine1", AxL, 0.25f, w); AimArm("Right", new(0.1f, 0.2f, 1f), new(0.0f, -0.1f, 1f), w); }
                break;
            case "chao":
                // espinhos: abaixa e espalma as mãos no chão
                Pose("Hips", AxL, 0.35f, w);
                Pose("Spine", AxL, 0.5f, w);
                Pose("LeftUpLeg", AxL, -0.7f, w); Pose("RightUpLeg", AxL, -0.7f, w);
                Pose("LeftLeg", AxL, 1.0f, w); Pose("RightLeg", AxL, 1.0f, w);
                AimArm("Left", new(0.2f, -0.6f, 0.75f), new(0.1f, -0.95f, 0.3f), w);
                AimArm("Right", new(0.2f, -0.6f, 0.75f), new(0.1f, -0.95f, 0.3f), w);
                break;
            case "duas_maos":
                // lança de gelo: junta as duas mãos no peito e empurra para a frente
                if (strike) { AimArm("Left", new(0.05f, 0.15f, 1f), new(-0.1f, 0.15f, 1f), w); AimArm("Right", new(0.05f, 0.15f, 1f), new(0.1f, 0.15f, 1f), w); }
                else { AimArm("Left", new(0.4f, -0.5f, 0.6f), new(-0.8f, 0.3f, 0.4f), w); AimArm("Right", new(0.4f, -0.5f, 0.6f), new(0.8f, 0.3f, 0.4f), w); }
                Pose("Spine1", AxL, strike ? 0.15f : -0.1f, w);
                break;
            case "bracos":
                // braços cruzados no peito (guarda de plantão)
                AimArm("Left", new(0.25f, -0.75f, 0.45f), new(-0.85f, 0.15f, 0.45f), w);
                AimArm("Right", new(0.25f, -0.75f, 0.5f), new(-0.85f, 0.25f, 0.4f), w);
                break;
            case "malabar":
            {
                // malabares: as mãos sobem e descem alternadas, em ritmo
                var a = Mathf.Sin(_time * 7f);
                AimArm("Left", new(0.3f, -0.4f, 0.8f), new(0.1f, 0.35f + 0.45f * a, 0.85f), w);
                AimArm("Right", new(0.3f, -0.4f, 0.8f), new(0.1f, 0.35f - 0.45f * a, 0.85f), w);
                Pose("Neck", AxL, -0.25f, w);
                if (w > 0.5f) _malabarAte = _time + 0.15f;   // 13/11 ("por que o bobo não faz malabares?"): bolinhas no ar (Update)
                break;
            }
            case "danca":
            {
                // 13/11 (NPC dançarino): braços ondulando acima da cabeça, quadril e tronco balançando no ritmo, joelhos dobrando
                var b = Mathf.Sin(_time * 6f); var c = Mathf.Cos(_time * 3f);
                AimArm("Left", new(0.55f, 0.6f + 0.25f * b, 0.2f), new(0.1f + 0.3f * c, 0.95f, 0.2f), w);
                AimArm("Right", new(0.55f, 0.6f - 0.25f * b, 0.2f), new(0.1f - 0.3f * c, 0.95f, 0.2f), w);
                Pose("Spine", AxFwd, 0.18f * c, w);
                Pose("Spine1", AxUp, 0.25f * b, w);
                Pose("LeftUpLeg", AxL, -0.25f - 0.2f * Mathf.Max(0f, b), w);
                Pose("RightUpLeg", AxL, -0.25f - 0.2f * Mathf.Max(0f, -b), w);
                Pose("LeftLeg", AxL, 0.4f * Mathf.Max(0f, b), w);
                Pose("RightLeg", AxL, 0.4f * Mathf.Max(0f, -b), w);
                Pose("Head", AxFwd, 0.2f * c, w);
                break;
            }
            case "aceno":
                // NPC acena: braço direito levantado, antebraço balançando
                AimArm("Right", new(0.75f, 0.65f, 0.1f), new(0.2f + 0.45f * Mathf.Sin(_time * 9f), 1f, 0.15f), w);
                break;
            case "martelar":
            {
                // braço direito à frente e para fora do corpo; o antebraço sobe e desce batendo na bigorna (à frente)
                var beat = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(_time * 5.5f), 2f);
                AimArm("Right", new(0.35f, -0.35f, 0.85f), new(0.15f, -0.55f + 1.1f * beat, 0.8f), w);
                AimArm("Left", new(0.3f, -0.7f, 0.6f), new(0.05f, -0.35f, 0.95f), w * 0.8f);   // segura a peça
                Pose("Spine1", AxL, 0.18f, w);
                Pose("Neck", AxL, 0.3f, w);
                break;
            }
            case "saudacao":
                // mão no peito e uma leve reverência
                AimArm("Right", new(0.2f, -0.7f, 0.5f), new(-0.85f, 0.25f, 0.45f), w);
                Pose("Spine1", AxL, 0.28f * Mathf.Sin(Mathf.Clamp(t, 0f, 1f) * Mathf.Pi), w);
                Pose("Neck", AxL, 0.25f, w);
                break;
            case "aceno_cabeca":
                // acena com a cabeça (sim) e ergue a mão aberta na altura do ombro
                Pose("Neck", AxL, 0.22f * Mathf.Abs(Mathf.Sin(_time * 6f)), w);
                AimArm("Left", new(0.6f, -0.3f, 0.5f), new(0.1f, 0.9f, 0.3f), w * 0.8f);
                break;
            case "mao_alta":
                // braço esquerdo erguido parado (oi de longe)
                AimArm("Left", new(-0.3f, 0.9f, 0.2f), new(-0.1f, 1f, 0.1f), w);
                break;
            case "maos_quadril":
                // mãos na cintura
                AimArm("Left", new(0.75f, -0.55f, -0.1f), new(-0.55f, -0.45f, 0.15f), w);
                AimArm("Right", new(0.75f, -0.55f, -0.1f), new(-0.55f, -0.45f, 0.15f), w);
                break;
            case "explica":
                // uma mão aponta para longe, a outra aberta para cima
                AimArm("Right", new(0.35f, -0.1f, 0.9f), new(0.3f, 0.05f, 1f), w);
                AimArm("Left", new(0.3f, -0.7f, 0.5f), new(0.1f, -0.1f + 0.15f * Mathf.Sin(_time * 2.5f), 1f), w * 0.8f);
                break;
            case "coca_cabeca":
                // coça a cabeça, pensativo
                AimArm("Right", new(0.5f, 0.6f, 0.3f), new(-0.6f, 0.4f + 0.1f * Mathf.Sin(_time * 10f), 0.2f), w);
                Pose("Neck", AxFwd, 0.15f, w);
                break;
            case "conta_dedos":
                // as duas mãos à frente, uma tocando a outra (contando nos dedos)
                AimArm("Left", new(0.3f, -0.6f, 0.6f), new(-0.4f, 0.1f, 0.9f), w);
                AimArm("Right", new(0.3f, -0.6f, 0.6f), new(-0.45f, 0.15f + 0.08f * Mathf.Sin(_time * 4f), 0.85f), w);
                Pose("Neck", AxL, 0.18f, w);
                break;
            case "ombros":
                // encolhe os ombros, palmas para cima
                AimArm("Left", new(0.6f, -0.55f, 0.3f), new(0.6f, 0.1f, 0.6f), w);
                AimArm("Right", new(0.6f, -0.55f, 0.3f), new(0.6f, 0.1f, 0.6f), w);
                Pose("Neck", AxFwd, 0.2f * Mathf.Sin(_time * 1.5f), w);
                break;
            case "conversa":
                // NPC gesticulando enquanto fala: mãos à frente, alternando
                AimArm("Right", new(0.3f, -0.6f, 0.6f), new(-0.1f, 0.15f + 0.25f * Mathf.Sin(_time * 3.1f), 1f), w);
                AimArm("Left", new(0.3f, -0.65f, 0.55f), new(-0.1f, 0.1f + 0.2f * Mathf.Sin(_time * 2.3f + 1f), 1f), w * 0.7f);
                break;
            case "area":
                if (strike)
                {
                    AimArm("Left", new(0.2f, -0.2f, 1f), new(0.1f, -0.3f, 1f), w);
                    AimArm("Right", new(0.2f, -0.2f, 1f), new(0.1f, -0.3f, 1f), w);
                    Pose("Spine1", AxL, 0.3f, w);
                }
                else
                {
                    AimArm("Left", new(0.2f, 1f, 0.1f), new(0.1f, 1f, 0.2f), w);
                    AimArm("Right", new(0.2f, 1f, 0.1f), new(0.1f, 1f, 0.2f), w);
                    Pose("Spine1", AxL, -0.2f, w);
                }
                break;
            default:
            {
                // golpe de arma em sequência animada por quadros-chave (ver ApplySwing): preparação → corte → continuação
                if (phase == "windup" && _swingSeq != f.ActionSeq) { _swingSeq = f.ActionSeq; _swingStart = _time; _swingStrike = -1f; _swingCombo = f.ActionSeq % 3; }
                if (phase != "windup" && _swingStrike < 0f)
                {
                    if (_swingSeq != f.ActionSeq) { _swingSeq = f.ActionSeq; _swingStart = _time - 0.18f; _swingCombo = f.ActionSeq % 3; }
                    _swingStrike = _time;
                }
                if (UseMocap()) MocapSwing(); else ApplySwing();
                if (_mon is not null) CorpoDoGolpeMonstro(phase, t);
                break;
            }
        }
    }

    /// <summary>
    /// 13/11 ("arruma a animação de combate dos monstros"): o golpe do monstro usava só os braços. Agora o corpo todo
    /// trabalha: na preparação ele recua o tronco, torce para trás e agacha (o aviso que dá para ler); no golpe joga o peso
    /// para a frente torcendo o tronco junto com a arma; na recuperação fica um instante curvado, cansado, e volta.
    /// </summary>
    private void CorpoDoGolpeMonstro(string phase, float t)
    {
        switch (phase)
        {
            case "windup":
            {
                var k = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.3f));
                Pose("Spine", AxL, -0.16f * k, 1f);
                Pose("Spine1", AxUp, -0.42f * k, 1f);
                Pose("Neck", AxUp, 0.25f * k, 1f);   // a cabeça continua olhando o alvo
                _attackCrouch = Mathf.Max(_attackCrouch, 0.12f * k);
                break;
            }
            case "active":
            {
                var k = Mathf.Sin(Mathf.Min(1f, t * 1.6f) * Mathf.Pi * 0.5f);
                Pose("Spine", AxL, 0.28f * k, 1f);
                Pose("Spine1", AxUp, -0.42f + 0.85f * k, 1f);
                Pose("Hips", AxUp, 0.18f * k, 1f);
                _attackCrouch = Mathf.Max(_attackCrouch, 0.08f);
                break;
            }
            case "recover":
            {
                var k = 1f - Mathf.SmoothStep(0f, 1f, t);
                Pose("Spine", AxL, 0.22f * k, 1f);
                Pose("Spine1", AxUp, 0.43f * k, 1f);
                break;
            }
        }
    }

    /// <summary>Parada: a Bruxa mantém a mão esquerda aberta (névoa); o Arqueiro segura o arco baixo ao lado.</summary>
    private void IdleHands()
    {
        if (_classId == "bruxa") AimArm("Left", new(0.35f, -0.85f, 0.35f), new(0.3f, -0.1f, 0.95f), 1f);
    }

    private string PoseKind(string skill)
    {
        if (FamiliaPoder(skill) is { } poder) return poder;   // gancho VFX 14/11 (Client/Efeitos/PosesPoderes.cs)
        if (_mon is { } ms && !skill.StartsWith("skill_"))
        {
            bool Has(params string[] k) { foreach (var x in k) if (skill.Contains(x)) return true; return false; }
            if (Has("flech", "disparo", "tiro") && GearFactory.IsBow(_weaponModel) == true) return "arco";
            if (Has("punhalad", "estocad", "botes", "cutucad")) return "estocadas";   // 13/11: sequência de golpes avançando
            if (Has("grito", "repuls", "pulso", "badalad", "espinhos", "onda", "chuva", "correntes", "rochas", "assombro", "enraizar", "circulo", "terremoto", "tremor"))
                return ms.CasterHands || _weapon is null ? "area" : "golpe";
            if (Has("bola", "cinza", "lament", "raio", "rajada", "olhar", "teia", "cuspe", "almas", "gelo", "escoria", "sopro", "labareda", "brasa"))
                return ms.CasterHands || _weapon is null ? "magia" : "golpe";
            return "golpe";
        }
        if (skill == "npc_aceno") return "aceno";
        if (skill == "npc_magia") return "magia";
        if (skill == "npc_conversa") return "conversa";
        if (skill == "npc_olhar") return "olhar";
        if (skill == "npc_bracos") return "bracos";
        if (skill == "npc_malabar") return "malabar";
        if (skill == "npc_danca") return "danca";
        if (skill.Contains("passos_fantasma") || skill.Contains("passo_etereo")) return "sombras";
        // poses próprias por habilidade (pendência "cada habilidade com pose própria, braço da arma certo")
        if (skill.Contains("giro") || skill.Contains("furacao") || skill == "skill_arma_onda") return "giro";   // 17/11: Furacão da máscara gira o corpo
        if (skill.Contains("golpe_de_cunha")) return "cunha";
        if (skill.Contains("salto")) return "salto";
        if (skill.Contains("investida") || skill.Contains("corte_avancante")) return "investida";
        if (skill.Contains("soco")) return "soco";
        if (skill == "skill_arma_arco") return "varrida";
        if (skill.Contains("arqueiro_chuva") || skill.Contains("batedor_chuva")) return "arco_ceu";
        if (skill.Contains("arremesso")) return "arremesso";
        if (skill.Contains("espinhos")) return "chao";
        if (skill.Contains("lanca_gelo") || skill.Contains("arcanista_lanca")) return "duas_maos";
        if (skill.Contains("olhar")) return "olhar";
        if (skill.Contains("arqueiro") || skill.Contains("batedor")) return skill.Contains("chuva") ? "arco" : "arco";
        if (skill.Contains("toque") || skill.Contains("renovo") || skill.Contains("floresta")) return "cura";
        if (skill.Contains("meteoros") || skill.Contains("cometa") || skill.Contains("furia_colosso")) return "area";
        if (skill.Contains("barreira")) return "barreira";
        if (skill.Contains("aura")) return "aura";
        if (skill.Contains("explosao") || skill.Contains("investida")) return skill.Contains("explosao") ? "area" : "golpe";
        if (skill.Contains("bruxa") || skill.Contains("mago") || skill.Contains("arcanista") || skill.Contains("vitrine_magia")) return "magia";
        if (skill.StartsWith("skill_arma_")) return _classId == "mago" ? "magia" : "golpe";
        if (skill == "vitrine") return _classId switch { "arqueiro" => "arco", "bruxa" or "mago" => "magia", _ => "golpe" };
        return "golpe";
    }

    // ------------------------------------------------------------ montaria, asas, lendário

    public void SetMounted(bool mounted) => _mounted = mounted;

    /// <summary>15/11 ("arrumar a animação de segurar o barril, pra ficar mais real"): abraça o barril na frente da barriga.</summary>
    public bool CarregandoBarril { get; set; }
    private float _barrilW;
    private bool _armaGuardadaPeloBarril;

    private readonly List<(Node3D Pivot, float Side)> _wingPivots = new();
    private float _wingLift;   // 15/11 noite: 0 = asa dobrada (chão), 1 = asa subida em V (voando)
    private float _wingT, _wingFlap, _flyLift;
    /// <summary>11/11: voando de verdade (PlayerView.Flying).</summary>
    public bool Flying { get; set; }
    /// <summary>11/11: flutua (fantasma de cal) — pernas juntas, pés para baixo, como voando de asa.</summary>
    public bool Hover { get; set; }
    public const float FlyHeight = 3.6f;

    /// <summary>
    /// Asas (11/11, item no espaço "asas"): duas asas presas nas costas por uma dobradiça cada; paradas quase fechadas e
    /// balançando devagar, correndo batem forte, no impulso (wing_dash) abrem e batem rápido.
    /// </summary>
    public void SetWings(string? kind)
    {
        if (kind == _wingsKind) return;
        var eraCapa = _wingsKind?.StartsWith("capa:") == true;
        _wingsKind = kind;
        _wings?.QueueFree();
        _wings = null;
        _wingPivots.Clear();
        SetArmorPart("asaspack", null);
        // 14/11 (dono: "tira as asas dos sets e cria asas próprias"): asas que vieram presas nos conjuntos lendários
        // (Tools/separar_asas_dos_sets.py) — malha com pele presa nas costas, como a peça de armadura
        if (kind is not null && kind.StartsWith("pack:")) { SetArmorPart("asaspack", kind[5..] + ":peitoral"); return; }
        var ehCapa = kind?.StartsWith("capa:") == true;
        if ((eraCapa || ehCapa) && _bodyModel is not null && _outfitMantle) BuildOutfit(_bodyModel);   // 14/11: manto da roupa sai/volta
        SetItemCape(ehCapa ? kind : null);
        if (kind is null || ehCapa) return;
        var spine = Socket("Spine2");
        _wings = new Node3D { Name = "Asas" };
        spine.AddChild(_wings);
        _wings.Position = new Vector3(0, 0.08f, -0.14f);
        // 14/11 noite: as 25 asas da prancha do dono (comuns a lendárias) — Visuals/AsasNovas.cs
        if (AsasNovas.Conhece(kind)) { _wingPivots.AddRange(AsasNovas.Montar(_wings, kind)); if (_wingPivots.Count > 0) return; }
        // 11/11: asas de 2º nível da classe (modelos do usuário pintados com a referência): duas metades com a dobradiça
        // no ombro — o mesmo giro das asas desenhadas (dobram para trás paradas, abrem e batem forte correndo/voando)
        // 11/11 noite ("olha a asa feia"): as asas antigas desenhadas em código (ferro, neve) também usam os modelos novos,
        // um pouco menores (nível mais baixo); qualquer outra cai na do dragão em vez do desenho de palitos
        var file = kind switch { "dragao" => "asas/asa_guerreiro", "abismo" or "ferro" => "asas/asa_mago", "aguia" => "asas/asa_arqueiro",
            "cristal" or "neve" => "asas/asa_bruxa", _ => "asas/asa_guerreiro" };
        var tier = kind is "dragao" or "abismo" or "aguia" or "cristal" ? 1f : 0.8f;
        _wings.Scale = Vector3.One * tier;
        if (file is not null && GlbCache.Instance(file) is { } pack)
        {
            foreach (var (name, side) in new[] { ("asa_dir", 1f), ("asa_esq", -1f) })
            {
                if (pack.FindChild(name, true, false) is not Node3D half) continue;
                var pivot = new Node3D { Name = name, Position = new Vector3(side * 0.05f, 0.05f, -0.02f) };
                _wings.AddChild(pivot);
                half.GetParent().RemoveChild(half);
                pivot.AddChild(half);
                half.Position = Vector3.Zero;   // a origem da metade já é a dobradiça
                _wingPivots.Add((pivot, side));
            }
            pack.QueueFree();
            // 17/11 (dono: "asa do abismo rúnico tá igual à asa de ferro?"): ferro e neve usam o mesmo molde das de 2º nível,
            // mas com cara própria — ferro: aço frio, metálico e fosco; neve: gelo claro com brilho azulado
            if (kind is "ferro" or "neve")
                foreach (var n in _wings.FindChildren("*", nameof(MeshInstance3D), true, false))
                    if (n is MeshInstance3D mi && mi.Mesh is not null)
                        for (var i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                            if ((mi.GetSurfaceOverrideMaterial(i) ?? mi.Mesh.SurfaceGetMaterial(i)) is StandardMaterial3D sm)
                            {
                                var c = (StandardMaterial3D)sm.Duplicate();
                                if (kind == "ferro")
                                {
                                    c.AlbedoTexture = null; c.AlbedoColor = new Color(0.46f, 0.49f, 0.53f);
                                    c.Metallic = 0.85f; c.Roughness = 0.38f; c.EmissionEnabled = false;
                                }
                                else
                                {
                                    c.AlbedoColor = sm.AlbedoColor * new Color(0.82f, 0.95f, 1.15f);
                                    c.EmissionEnabled = true; c.Emission = new Color(0.35f, 0.62f, 0.95f); c.EmissionEnergyMultiplier = 0.35f;
                                    c.Roughness = 0.2f;
                                }
                                mi.SetSurfaceOverrideMaterial(i, c);
                            }
            if (_wingPivots.Count > 0) return;
        }
        var neve = kind == "neve";
        var frame = VisualKit.Mat(neve ? new Color(0.70f, 0.78f, 0.88f) : new Color(0.20f, 0.20f, 0.22f), 0.35f, neve ? 0.2f : 0.9f);
        var membrane = neve ? VisualKit.Mat(new Color(0.90f, 0.93f, 0.98f), 0.7f) : VisualKit.Mat(new Color(0.34f, 0.16f, 0.20f), 0.85f, 0.3f);
        foreach (var side in new[] { 1f, -1f })
        {
            var pivot = new Node3D { Position = new Vector3(side * 0.06f, 0, 0) };
            _wings.AddChild(pivot);
            _wingPivots.Add((pivot, side));
            // osso de cima (braço da asa) e três "dedos" com a membrana/penas em leque
            VisualKit.Part(pivot, VisualKit.Box(0.85f, 0.05f, 0.05f), frame, new Vector3(side * 0.42f, 0.22f, 0), new Vector3(0, 0, side * 18));
            for (var i = 0; i < 3; i++)
            {
                var len = 0.75f - i * 0.12f;
                var ang = side * (-10f - i * 22f);
                VisualKit.Part(pivot, VisualKit.Box(len, 0.03f, 0.03f), frame, new Vector3(side * (0.55f + i * 0.06f), 0.05f - i * 0.1f, -0.01f), new Vector3(0, 0, ang));
                VisualKit.Part(pivot, VisualKit.Box(len * 0.95f, 0.26f, 0.015f), membrane, new Vector3(side * (0.5f + i * 0.05f), -0.02f - i * 0.12f, -0.02f), new Vector3(0, 0, ang * 0.6f));
            }
        }
    }

    private MeshInstance3D? _guardFx;
    private float _guardFxK;

    /// <summary>11/11 ("usei a Guarda de Escudo e nada aconteceu"): enquanto a guarda vale, um escudo de luz azul na frente.</summary>
    private void UpdateGuardFx(bool guarding, float delta)
    {
        _guardFxK = Mathf.MoveToward(_guardFxK, guarding ? 1f : 0f, delta * 6f);
        if (_guardFxK <= 0.001f) { if (_guardFx is not null) _guardFx.Visible = false; return; }
        if (_guardFx is null)
        {
            _guardFx = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.75f, Height = 1.5f, IsHemisphere = false, RadialSegments = 24, Rings = 12 },
                Scale = new Vector3(1f, 1.15f, 0.35f), Position = new Vector3(0, 1.05f, 0.45f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.45f, 0.75f, 1f, 0.25f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    EmissionEnabled = true, Emission = new Color(0.4f, 0.7f, 1f), EmissionEnergyMultiplier = 1.2f,
                },
            };
            Root.AddChild(_guardFx);
        }
        _guardFx.Visible = true;
        if (_guardFx.MaterialOverride is StandardMaterial3D m) m.AlbedoColor = new Color(0.45f, 0.75f, 1f, 0.25f * _guardFxK);
    }

    private void UpdateWings(in VisualFrame f, float delta)
    {
        if (_wingPivots.Count == 0) return;
        var dash = f.Action == "wing_dash";
        // 12/11 ("bater de asas bem sutil"): no chão quase só respiram; voando batem, mas devagar e curto
        // 13/11 ("as asas ainda estão muito duras, não estão batendo"): batida de verdade — sobe e desce em volta do eixo
        // da frente (o que faz a ponta subir acima da cabeça e descer abaixo da cintura), com a descida mais rápida que a
        // subida (força para baixo) e a varredura para trás defasada; parado, respiram devagar; voando, batem forte
        // 18/11 19h (dono: "a asa só bate quando anda pra frente — coloca que ela sempre bate, como o bicho fica no ar?"):
        // no ar (voando ou flutuando de asas) a batida é sempre forte, parado ou andando para qualquer lado
        var noAr = Flying || _flyLift > 0.3f;
        var target = dash ? 1f : Flying ? 1f : noAr ? 0.8f : f.Speed > 4.5f ? 0.35f : f.Speed > 0.5f ? 0.25f : 0.12f;
        _wingFlap = Mathf.MoveToward(_wingFlap, target, delta * 3f);
        _wingT += delta * (dash ? 15f : 2.4f + 9f * _wingFlap);
        var open = 25f + 40f * _wingFlap;                      // quanto abre para trás e para os lados
        var ciclo = Mathf.PosMod(_wingT, Mathf.Tau) / Mathf.Tau;
        var bate = ciclo < 0.4f ? Mathf.Cos(ciclo / 0.4f * Mathf.Pi) : -Mathf.Cos((ciclo - 0.4f) / 0.6f * Mathf.Pi);   // desce em 40% do tempo
        // 15/11 noite (dono: "a asa não bate corretamente" + referência do boneco amarelo voando): os modelos de asa são
        // painéis altos (1,6 m) com a dobradiça a 60% da altura — girando em volta dela, a parte de baixo cruzava por baixo
        // do corpo para o outro lado e a de cima passava por cima da cabeça. Voando, o painel sobe até a dobradiça ficar
        // no PÉ da asa (ela nasce do ombro e sobe) e abre em V para fora; a batida acontece em volta desse V:
        // em cima ~20° do vertical, embaixo quase deitada (~85°). No chão continua como antes (dobrada, pendurada).
        _wingLift = Mathf.MoveToward(_wingLift, Flying ? 1f : 0f, delta * 2.5f);
        var abre = Mathf.Lerp(Mathf.Lerp(0f, 8f, _wingFlap), 52f, _wingLift);
        var amp = Mathf.Lerp(6f + 38f * _wingFlap, 34f, _wingLift);
        var roll = abre - bate * amp;
        var sweep = Mathf.Sin(_wingT - 0.9f) * (3f + 12f * _wingFlap);
        // 18/11 16h (dono: "as asas ainda não estão batendo pra frente e pra trás"): voando, a batida é de pássaro — desce
        // indo para a FRENTE e sobe voltando para TRÁS (oito deitado: a varredura vem um quarto de ciclo antes da altura)
        var remada = Mathf.Sin(_wingT + 0.6f) * 34f * _wingLift;
        var giro = Mathf.Lerp(-60f + open + sweep, 8f + sweep * 0.5f, _wingLift) + remada;   // voando: plano da asa de frente para trás (um pouco para trás)
        foreach (var (pivot, side) in _wingPivots)
        {
            pivot.RotationDegrees = new Vector3(0, side * giro, -side * roll);
            if (pivot.GetChildCount() > 0 && pivot.GetChild(0) is Node3D metade)
            {
                if (metade.HasMeta("procedural")) metade.RotationDegrees = new Vector3(0, 0, side * abre);   // desenhada deitada: sem o V
                else if (metade.Name.ToString().StartsWith("asa_")) metade.Position = new Vector3(0, 0.9f * _wingLift, 0);   // metade do modelo
            }
        }
    }

    private Node3D? _aura;

    /// <summary>13/11 (análise — itens: "aura do conjunto completo"): anel dourado no chão, faíscas subindo e uma luz suave.</summary>
    private static Shader? _runas;
    internal const string RunasShader = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec3 cor = vec3(1.0, 0.72, 0.25);
uniform float forca = 0.55;
uniform float suave = 0.0;   // 13/11: círculo grande (zona segura): bordas em degradê e um véu fraco por dentro
float hash(float n) { return fract(sin(n * 91.345) * 47453.5453); }
void fragment() {
    vec2 c = (UV - 0.5) * 2.0;
    float r = length(c);
    float a = atan(c.y, c.x) + TIME * 0.12;
    float aro = smoothstep(0.012, 0.0, abs(r - 0.94)) + smoothstep(0.010, 0.0, abs(r - 0.74));
    // faixa de runas: 26 casas em volta; cada uma desenha 1-3 riscos (barra, gancho, ponto) escolhidos por sorteio fixo
    float n = 26.0;
    float s = floor((a / 6.28318 + 0.5) * n);
    float u = fract((a / 6.28318 + 0.5) * n);
    float v = (r - 0.77) / 0.14;
    float runa = 0.0;
    if (v > 0.0 && v < 1.0) {
        float h1 = hash(s), h2 = hash(s + 17.0), h3 = hash(s + 41.0);
        float linha = 0.07;
        runa += step(abs(u - (0.3 + 0.4 * h1)), linha) * step(0.15, v) * step(v, 0.85);            // barra
        runa += step(abs(v - (0.25 + 0.5 * h2)), linha * 1.4) * step(0.25, u) * step(u, 0.75) * step(0.4, h3);   // traço deitado
        runa += step(length(vec2(u - 0.5, v - 0.85) * vec2(1.0, 1.4)), 0.09) * step(0.7, h1);      // ponto
        runa += step(abs(length(vec2(u - 0.5, v - 0.5)) - 0.28), linha) * step(h2, 0.3);          // gancho
    }
    float valor = clamp(aro + runa, 0.0, 1.0) * smoothstep(1.0, 0.95, r);
    valor *= mix(1.0, 0.35 + 0.65 * smoothstep(1.0, 0.72, r), suave);
    valor += suave * 0.06 * smoothstep(0.98, 0.25, r) * (0.7 + 0.3 * sin(r * 18.0 - TIME * 1.2));
    float pulso = 0.85 + 0.15 * sin(TIME * 1.6);
    ALBEDO = cor;
    ALPHA = valor * forca * pulso;
}";

    public void SetAura(bool on)
    {
        if (on && _aura is null)
        {
            _aura = new Node3D { Name = "AuraConjunto" };
            Root.AddChild(_aura);
            // 13/11 (referência do dono: "círculo de runas douradas; no chão, sutil, meio transparente, leve"): disco no chão
            // com dois aros finos e uma faixa de runas entre eles, girando bem devagar
            var anel = new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(1.7f, 1.7f) }, Position = new Vector3(0, 0.025f, 0),
                MaterialOverride = new ShaderMaterial { Shader = _runas ??= new Shader { Code = RunasShader } }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _aura.AddChild(anel);
            _aura.AddChild(new CpuParticles3D
            {
                Amount = 6, Lifetime = 1.6f, Position = new Vector3(0, 0.1f, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring,
                EmissionRingRadius = 0.55f, EmissionRingInnerRadius = 0.45f, EmissionRingHeight = 0.05f, EmissionRingAxis = Vector3.Up,
                Direction = Vector3.Up, Spread = 8f, Gravity = new Vector3(0, 0.6f, 0), InitialVelocityMin = 0.4f, InitialVelocityMax = 0.9f,
                ScaleAmountMin = 0.025f, ScaleAmountMax = 0.05f, LocalCoords = false,
                Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3,
                    Material = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.85f, 0.4f), EmissionEnabled = true, Emission = new Color(1f, 0.75f, 0.3f), EmissionEnergyMultiplier = 3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
            });
            _aura.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.78f, 0.35f), LightEnergy = 0.25f, OmniRange = 2f, Position = new Vector3(0, 0.6f, 0), ShadowEnabled = false });
        }
        if (_aura is not null) _aura.Visible = on;
    }

    // ================================================================ skins do bracelete (13/11)
    private Node3D? _skinNo;

    /// <summary>16/11: peça curva e lisa (chifre, cauda, tentáculo) — cilindros afinando entre os pontos, com bolinhas nas
    /// juntas para não abrir fresta na curva.</summary>
    private static void Segmentos(Node3D pai, Vector3[] pts, float r0, float r1, Material mat)
    {
        for (var i = 0; i < pts.Length - 1; i++)
        {
            var a = pts[i]; var b = pts[i + 1];
            var ta = (float)i / (pts.Length - 1); var tb = (float)(i + 1) / (pts.Length - 1);
            var ra = Mathf.Lerp(r0, r1, ta); var rb = Mathf.Lerp(r0, r1, tb);
            var d = b - a; var len = d.Length();
            if (len < 1e-4f) continue;
            var no = new MeshInstance3D { Mesh = VisualKit.Cylinder(rb, ra, len, 10), MaterialOverride = mat, Position = (a + b) * 0.5f };
            no.Quaternion = new Quaternion(Vector3.Up, d / len);
            pai.AddChild(no);
            if (i > 0) VisualKit.Part(pai, VisualKit.Sphere(ra), mat, a);
        }
    }
    private Node3D? _skinCintura, _skinPeito;

    /// <summary>16/11 madrugada (dono: "skins com arte descolada"): o quadril do boneco gira bastante na pose parada (peso numa
    /// perna) e no passo, e o que fica preso nele (avental, colher, cutelo, frasco) ia parar de lado, longe da barriga. A
    /// cintura da skin segue a posição do quadril, mas gira 70% como o peito — o avental fica na frente do corpo.</summary>
    private void AlinharCintura()
    {
        if (_skinCintura is null || _skinPeito is null || !GodotObject.IsInstanceValid(_skinCintura) || !GodotObject.IsInstanceValid(_skinPeito)) return;
        if (_skinCintura.GetParent() is not Node3D pai || _skinPeito.GetParent() is not Node3D paiPeito) return;
        if (!_skinCintura.IsInsideTree()) return;
        var qh = pai.GlobalBasis.GetRotationQuaternion();
        var qp = paiPeito.GlobalBasis.GetRotationQuaternion();
        var alvo = qh.Slerp(qp, 0.7f);
        _skinCintura.Basis = new Basis((qh.Inverse() * alvo).Normalized()) * Basis.FromScale(new Vector3(1f, 1f, 0.85f));
    }
    private string? _skinId;

    /// <summary>
    /// 13/11 (pedido do dono e do Barto: "skins engraçadas — coelho, chapéu, gravata, palhaço, bobo da corte"; fechado:
    /// "skin é só a aparência por cima da armadura e vem num BRACELETE, com um bônus pequeno"). Peças presas na cabeça,
    /// no peito e no quadril do NOSSO boneco; a armadura continua por baixo.
    /// </summary>
    private static readonly Dictionary<string, (string Tipo, Color Cor)> RostoDaSkin = new()
    {
        ["cartola"] = ("humano", new Color(0.25f, 0.18f, 0.12f)), ["gravata"] = ("humano", new Color(0.2f, 0.3f, 0.45f)),
        ["chef"] = ("humano", new Color(0.3f, 0.2f, 0.1f)), ["magico"] = ("humano", new Color(0.25f, 0.15f, 0.4f)),
        ["rei"] = ("humano", new Color(0.2f, 0.35f, 0.5f)), ["cozinheiro_insano"] = ("louco", new Color(0.6f, 0.1f, 0.1f)),
        ["palhaco"] = ("palhaco", new Color(0.1f, 0.4f, 0.9f)), 
        ["bobo"] = ("humano", new Color(0.35f, 0.2f, 0.1f)), ["macaco_vermelho"] = ("humano", new Color(0.35f, 0.18f, 0.05f)),
        ["demonio"] = ("brilho", new Color(1f, 0.15f, 0.05f)), ["lava"] = ("brilho", new Color(1f, 0.6f, 0.1f)),
        ["pedra"] = ("brilho", new Color(0.4f, 0.85f, 1f)), ["cristal"] = ("brilho", new Color(0.6f, 0.9f, 1f)), ["ouro"] = ("brilho", new Color(1f, 0.85f, 0.3f)),
        ["minotauro"] = ("brilho", new Color(1f, 0.25f, 0.1f)), ["mumia"] = ("brilho", new Color(1f, 0.8f, 0.2f)),
        ["samurai"] = ("brilho", new Color(0.4f, 0.75f, 1f)), ["peste"] = ("brilho", new Color(0.5f, 1f, 0.4f)),
        ["fantasma"] = ("buraco", Colors.Black), ["caveira"] = ("buraco", Colors.Black), ["espantalho"] = ("costura", new Color(0.15f, 0.1f, 0.05f)),
    };

    /// <summary>Rosto da skin, na frente da cabeça (encaixe Head: a boca fica na altura 0, os olhos ~6 cm acima).</summary>
    private void Rosto(Node3D cab, string tipo, Color cor)
    {
        var preto = VisualKit.Mat(new Color(0.03f, 0.03f, 0.04f), 0.3f);
        foreach (var sd in new[] { -1f, 1f })
        {
            switch (tipo)
            {
                case "humano" or "louco" or "palhaco":
                {
                    // 17/11 (dono: "o olho branco com preto ficou medonho — usa essa config de olho"): olho de conta — preto
                    // brilhante, aro escuro avermelhado e um pontinho de luz no alto (como o do sapo/alien do print)
                    var esc = tipo == "louco" ? 1.2f : 1f;
                    var olho = VisualKit.Part(cab, VisualKit.Sphere(0.0135f * esc), new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.015f, 0.02f), Roughness = 0.08f, Metallic = 0.2f },
                        new Vector3(sd * 0.036f, 0.058f, 0.094f), null, new Vector3(1.05f, 1f, 0.55f));
                    VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.0125f * esc, OuterRadius = 0.0155f * esc, Rings = 14, RingSegments = 5 }, VisualKit.Mat(new Color(0.28f, 0.05f, 0.05f), 0.4f),
                        new Vector3(sd * 0.036f, 0.058f, 0.094f), new Vector3(90, 0, 0), new Vector3(1f, 1f, 0.5f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.0035f), VisualKit.Glow(new Color(1f, 1f, 1f), 1.6f), new Vector3(sd * 0.036f - 0.004f, 0.063f, 0.1015f));
                    VisualKit.Part(cab, VisualKit.Box(0.034f, 0.007f, 0.01f), tipo == "palhaco" ? VisualKit.Mat(new Color(0.1f, 0.3f, 0.8f)) : preto,
                        new Vector3(sd * 0.037f, 0.083f, 0.098f), new Vector3(0, 0, tipo == "louco" ? sd * 20 : sd * -8));
                    if (tipo == "palhaco")   // losango pintado em volta do olho
                        VisualKit.Part(cab, VisualKit.Box(0.03f, 0.03f, 0.004f), VisualKit.Mat(new Color(0.15f, 0.35f, 0.95f)), new Vector3(sd * 0.036f, 0.058f, 0.091f), new Vector3(0, 0, 45));
                    _ = olho;
                    break;
                }
                case "brilho":
                    VisualKit.Part(cab, VisualKit.Sphere(0.017f), VisualKit.Glow(cor, 3.5f), new Vector3(sd * 0.036f, 0.058f, 0.095f), null, new Vector3(1.3f, 0.7f, 0.5f));
                    break;
                case "buraco":
                    VisualKit.Part(cab, VisualKit.Sphere(0.024f), preto, new Vector3(sd * 0.036f, 0.056f, 0.09f), null, new Vector3(1f, 1.25f, 0.45f));
                    break;
                case "costura":
                    foreach (var g in new[] { 45f, -45f })
                        VisualKit.Part(cab, VisualKit.Box(0.03f, 0.006f, 0.006f), VisualKit.Mat(cor), new Vector3(sd * 0.036f, 0.058f, 0.1f), new Vector3(0, 0, g));
                    break;
            }
        }
        switch (tipo)
        {
            case "humano":
                VisualKit.Part(cab, VisualKit.Sphere(0.016f), _baseMats.Count > 0 ? _baseMats[0] : VisualKit.Mat(new Color(0.5f, 0.33f, 0.21f)), new Vector3(0, 0.03f, 0.105f), null, new Vector3(0.8f, 1.1f, 0.9f));   // nariz
                VisualKit.Part(cab, VisualKit.Capsule(0.0035f, 0.032f), VisualKit.Mat(new Color(0.16f, 0.07f, 0.05f), 0.7f), new Vector3(0, 0.0f, 0.1f), new Vector3(0, 0, 90));   // boca (17/11: traço fino escuro — o vermelho parecia batom)
                break;
            case "louco":   // sorriso largo de dentes
                VisualKit.Part(cab, VisualKit.Box(0.07f, 0.018f, 0.01f), VisualKit.Mat(new Color(0.95f, 0.93f, 0.85f), 0.3f), new Vector3(0, 0.0f, 0.1f));
                VisualKit.Part(cab, VisualKit.Box(0.074f, 0.004f, 0.012f), preto, new Vector3(0, 0.0f, 0.101f));
                break;
            case "buraco":
                VisualKit.Part(cab, VisualKit.Sphere(0.012f), preto, new Vector3(0, 0.025f, 0.1f), null, new Vector3(0.8f, 1.1f, 0.5f));
                break;
            case "costura":
                VisualKit.Part(cab, VisualKit.Box(0.06f, 0.004f, 0.006f), VisualKit.Mat(cor), new Vector3(0, 0.0f, 0.102f));
                for (var k = -2; k <= 2; k++) VisualKit.Part(cab, VisualKit.Box(0.004f, 0.016f, 0.006f), VisualKit.Mat(cor), new Vector3(k * 0.013f, 0.0f, 0.103f));
                break;
        }
    }

    public void SetSkin(string? skin)
    {
        if (skin == _skinId) return;
        _skinId = string.IsNullOrEmpty(skin) ? null : skin;
        _skinNo?.QueueFree();
        _skinNo = null;
        BuildOutfit(_bodyModel);   // a roupa inteira passa a ser a da skin (ou volta a armadura)
        _perfil = null;   // 17/11: a pele mudou (roupa da skin) — mede de novo
        // 14/11 tarde (dono: "a skin do bracelete não aplica, fica 'abaixo' da classe 2"): com skin, a cabeça de bicho da
        // classe 2/3 sai e a skin aparece inteira; tirou a skin, o bicho volta
        SetEvolved(_evolvedWanted);
        if (_hair is not null && !_hiddenForAnimal.Contains(_hair)) _hair.Visible = _skinId is null or "chef" or "pirata";   // 17/11: gravata sem o cabelo comprido (parecia mulher)   // 14/11: cartola/abóbora/balde cobrem o cabelo (o cabelo vazava pela aba, quadriculado)
        if (_skinId is null) return;
        _skinNo = new Node3D { Name = "Skin_" + skin };
        var cab = new Node3D(); _headSocket.AddChild(cab);
        // 14/11 tarde (dono: "as skins estão feias, não encaixadas — os enfeites ficam voando na frente"): o peito do boneco
        // é mais raso do que as peças supunham (~0,15 m); o encaixe aproxima tudo do corpo na profundidade
        // 15/11 noite (dono: "skins com arte descolada, detalhes de qualquer jeito"): as peças do peito foram desenhadas como se
        // o encaixe Spine2 ficasse no meio do peito, mas ele fica na linha dos ombros (1,37 m) — a gola ia parar no queixo e o
        // peitilho na boca. O encaixe desce 12 cm para o meio do peito (a gola cai no pescoço, os botões no peito)
        var peito = new Node3D { Scale = new Vector3(1f, 1f, 0.78f), Position = new Vector3(0, -0.12f, 0) }; Socket("Spine2").AddChild(peito);
        var quadril = new Node3D { Scale = new Vector3(1f, 1f, 0.85f) }; _hips.AddChild(quadril);
        _skinCintura = quadril; _skinPeito = peito;
        // 16/11: barriga — o que fica NA FRENTE da cintura (avental, mancha da barriga, frasco, colher, cutelo, orbe) vai no osso
        // Spine, que já gira junto com o tronco; preso no quadril ia parar de lado quando o boneco joga o peso numa perna.
        // As medidas continuam as do quadril (a origem desce até a altura do osso Hips)
        var barriga = new Node3D { Scale = new Vector3(1f, 1f, 0.85f) };
        if (Bone("Spine") >= 0)
        {
            var yHips = (_skelToRoot * _skel.GetBoneGlobalRest(Bone("Hips")).Origin).Y;
            var ySpine = (_skelToRoot * _skel.GetBoneGlobalRest(Bone("Spine")).Origin).Y;
            barriga.Position = new Vector3(0, yHips - ySpine, 0);
            Socket("Spine").AddChild(barriga);
        }
        else quadril.AddChild(barriga);
        // os três pedaços somem junto com a skin
        _skinNo.TreeExiting += () => { cab.QueueFree(); peito.QueueFree(); quadril.QueueFree(); if (GodotObject.IsInstanceValid(barriga) && barriga.GetParent() != quadril) barriga.QueueFree(); };
        Root.AddChild(_skinNo);
        StandardMaterial3D M(Color c, float r = 0.6f) => VisualKit.Mat(c, r);
        var branco = M(new Color(0.96f, 0.95f, 0.92f));
        switch (skin)
        {
            case "coelho":
            {
                var pelo = M(new Color(0.93f, 0.92f, 0.9f), 0.9f); var rosa = M(new Color(1f, 0.62f, 0.72f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    var orelha = VisualKit.Part(cab, VisualKit.Sphere(0.05f), pelo, new Vector3(sd * 0.055f, 0.29f, -0.01f), new Vector3(-8, 0, sd * -12), new Vector3(0.75f, 3.4f, 0.45f));
                    VisualKit.Part(orelha, VisualKit.Sphere(0.04f), rosa, new Vector3(0, 0, 0.012f), null, new Vector3(0.7f, 0.88f, 0.5f));
                }
                // 14/11 tarde (dono: "o coelho foi a única maneira; falta o rabinho de algodão, a barriga redonda clara e
                // ajustar o dente, que está voando; põe os olhos"): focinho e bochechas, dentes rentes ao focinho, olhos
                var creme = M(new Color(0.99f, 0.97f, 0.93f), 0.95f);
                // (sem focinho/bochechas — pedido do dono 14/11 18h)
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Box(0.016f, 0.024f, 0.006f), branco, new Vector3(sd * 0.0095f, -0.052f, 0.094f), new Vector3(-10, 0, 0));   // dentinhos rentes ao rosto
                VisualKit.Part(cab, VisualKit.Sphere(0.011f), rosa, new Vector3(0, -0.008f, 0.1f), null, new Vector3(1.3f, 0.9f, 0.6f));   // narizinho
                foreach (var sd in new[] { -1f, 1f })
                {
                    // 18/11 19h (dono: "deixa o olho do coelho assim, pretão e com um risquinho só branco dentro")
                    VisualKit.Part(cab, VisualKit.Sphere(0.019f), M(new Color(0.03f, 0.03f, 0.035f), 0.15f), new Vector3(sd * 0.034f, 0.035f, 0.09f), null, new Vector3(0.85f, 1.25f, 0.45f));   // olho preto
                    VisualKit.Part(cab, VisualKit.Box(0.0035f, 0.013f, 0.002f), VisualKit.Glow(new Color(1f, 1f, 1f), 1.2f), new Vector3(sd * 0.031f, 0.039f, 0.0985f), new Vector3(0, 0, sd * 12f));   // risquinho branco
                }
                // barriga redonda e clara
                // só um círculo rosa na barriga, chapado (o coelho continua musculoso — "sem a pança")
                VisualKit.Part(barriga, VisualKit.Cylinder(0.075f, 0.075f, 0.004f, 24), rosa, new Vector3(0, 0.17f, 0.1f), new Vector3(90, 0, 0), new Vector3(1f, 1f, 1.25f));
                // rabinho de algodão (tufos)
                foreach (var (dx, dy, dz, r) in new[] { (0f, 0.0f, -0.15f, 0.055f), (0.03f, 0.025f, -0.16f, 0.04f), (-0.03f, 0.02f, -0.16f, 0.04f), (0f, -0.03f, -0.155f, 0.04f) })
                    VisualKit.Part(quadril, VisualKit.Sphere(r), creme, new Vector3(dx, dy, dz));
                break;
            }
            case "cartola":
            {
                var preto = M(new Color(0.06f, 0.06f, 0.07f), 0.4f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.17f, 0.17f, 0.015f, 20), preto, new Vector3(0, 0.17f, -0.01f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.1f, 0.11f, 0.24f, 20), preto, new Vector3(0, 0.29f, -0.01f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.112f, 0.112f, 0.04f, 20), M(new Color(0.6f, 0.05f, 0.08f)), new Vector3(0, 0.2f, -0.01f));
                VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.022f, OuterRadius = 0.028f, Rings = 18, RingSegments = 6 }, VisualKit.Mat(new Color(0.95f, 0.8f, 0.35f), 0.25f, 0.9f), new Vector3(0.036f, 0.058f, 0.1f), new Vector3(90, 0, 0));   // monóculo (17/11: aro dourado em volta do olho, não um disco branco)
                // 17/11: peitilho e gravata-borboleta pintados na roupa (PFraque) — não descolam mais do peito
                foreach (var sd in new[] { -1f, 1f })   // abas do fraque
                    VisualKit.Part(quadril, VisualKit.Box(0.1f, 0.42f, 0.02f), preto, new Vector3(sd * 0.06f, -0.2f, -0.13f), new Vector3(8, 0, sd * 4));
                break;
            }
            case "gravata":
            {
                var verm = M(new Color(0.72f, 0.06f, 0.1f), 0.5f);
                // 17/11 (dono: "a roupa ainda tá voando, desgolada"): camisa em V e gravata pintadas na roupa (PTerno) — seguem o corpo
                _ = verm;
                VisualKit.Part(cab, VisualKit.Box(0.05f, 0.025f, 0.03f), M(new Color(0.08f, 0.06f, 0.05f)), new Vector3(0, -0.035f, 0.11f));   // bigode
                break;
            }
            case "palhaco":
            {
                VisualKit.Part(cab, VisualKit.Sphere(0.03f), M(new Color(0.9f, 0.05f, 0.05f), 0.25f), new Vector3(0, 0.0f, 0.125f));
                var cores = new[] { new Color(0.95f, 0.2f, 0.2f), new Color(0.2f, 0.5f, 1f), new Color(1f, 0.85f, 0.1f), new Color(0.25f, 0.85f, 0.3f), new Color(0.9f, 0.3f, 0.9f) };
                for (var i = 0; i < 10; i++)
                {
                    var a = -1.6f + i * 0.36f;
                    VisualKit.Part(cab, VisualKit.Sphere(0.065f), M(cores[i % cores.Length], 0.95f), new Vector3(Mathf.Sin(a) * 0.11f, 0.1f + Mathf.Cos(a * 0.7f) * 0.04f, -0.02f - Mathf.Cos(a) * 0.04f));
                }
                for (var i = 0; i < 12; i++)   // golinha de babado
                {
                    var a = i / 12f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Sphere(0.05f), branco, new Vector3(Mathf.Sin(a) * 0.13f, 0.23f, Mathf.Cos(a) * 0.1f), null, new Vector3(1f, 0.45f, 1f));
                }
                foreach (var y in new[] { 0.1f, -0.02f }) VisualKit.Part(peito, VisualKit.Sphere(0.03f), M(cores[0]), new Vector3(0, y, 0.15f));   // botões
                var bolinhas = new[] { (-0.08f, 0.14f), (0.09f, 0.05f), (-0.06f, -0.04f), (0.07f, 0.17f) };
                for (var i = 0; i < bolinhas.Length; i++)
                    VisualKit.Part(peito, VisualKit.Sphere(0.028f), M(cores[(i + 1) % cores.Length]), new Vector3(bolinhas[i].Item1, bolinhas[i].Item2, 0.14f), null, new Vector3(1f, 1f, 0.3f));
                break;
            }
            case "chef":
            {
                // chapéu alto e fofo, lenço vermelho, avental e a colher de pau no quadril
                VisualKit.Part(cab, VisualKit.Cylinder(0.11f, 0.105f, 0.09f, 20), branco, new Vector3(0, 0.17f, -0.01f));
                VisualKit.Part(cab, VisualKit.Sphere(0.15f), branco, new Vector3(0, 0.33f, -0.02f), null, new Vector3(1f, 0.85f, 1f));
                foreach (var sd in new[] { -1f, 0f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.08f), branco, new Vector3(sd * 0.08f, 0.3f, -0.02f + Mathf.Abs(sd) * 0.02f));
                var lenco = M(new Color(0.8f, 0.08f, 0.08f));
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.07f, OuterRadius = 0.1f, Rings = 16, RingSegments = 8 }, lenco, new Vector3(0, 0.2f, 0.0f), new Vector3(8, 0, 0));
                VisualKit.Part(peito, VisualKit.Box(0.05f, 0.06f, 0.02f), lenco, new Vector3(0, 0.16f, 0.14f), new Vector3(0, 0, 45));
                foreach (var y in new[] { 0.12f, 0.04f, -0.04f }) foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Sphere(0.014f), M(new Color(0.2f, 0.2f, 0.22f)), new Vector3(sd * 0.04f, y, 0.15f));
                VisualKit.Part(barriga, VisualKit.Box(0.3f, 0.42f, 0.012f), branco, new Vector3(0, -0.14f, 0.15f), new Vector3(-4, 0, 0));   // avental
                var colher = new Node3D { Position = new Vector3(0.17f, 0.02f, 0.06f), RotationDegrees = new Vector3(0, 0, -12) };
                barriga.AddChild(colher);
                var madeira = M(new Color(0.6f, 0.42f, 0.22f), 0.85f);
                VisualKit.Part(colher, VisualKit.Cylinder(0.012f, 0.012f, 0.32f, 6), madeira, new Vector3(0, -0.1f, 0));
                VisualKit.Part(colher, VisualKit.Sphere(0.04f), madeira, new Vector3(0, -0.28f, 0), null, new Vector3(1f, 1.4f, 0.4f));
                break;
            }
            case "pirata":
            {
                var bandana = M(new Color(0.75f, 0.06f, 0.06f), 0.8f);
                VisualKit.Part(cab, VisualKit.Sphere(0.125f), bandana, new Vector3(0, 0.1f, -0.01f), null, new Vector3(1.04f, 0.62f, 1.08f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Box(0.03f, 0.12f, 0.02f), bandana, new Vector3(sd * 0.03f, 0.03f, -0.13f), new Vector3(15, 0, sd * 18));   // pontas do nó
                var preto = M(new Color(0.04f, 0.04f, 0.04f), 0.5f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.022f, 0.022f, 0.006f, 14), preto, new Vector3(0.036f, 0.058f, 0.1f), new Vector3(90, 0, 0));   // tapa-olho (17/11: em cima do olho, do tamanho do olho)
                VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.118f, OuterRadius = 0.124f, Rings = 20, RingSegments = 4 }, preto, new Vector3(0, 0.05f, 0), new Vector3(0, 0, -14));   // cordão
                var barba = M(new Color(0.18f, 0.1f, 0.06f), 0.9f);
                // 17/11 (dono: "o rosto ficou medonho"): barba só no queixo, bigode fino — o rosto aparece
                VisualKit.Part(cab, VisualKit.Sphere(0.05f), barba, new Vector3(0, -0.085f, 0.06f), null, new Vector3(1.05f, 0.7f, 0.6f));
                VisualKit.Part(cab, VisualKit.Box(0.05f, 0.012f, 0.012f), barba, new Vector3(0, -0.018f, 0.104f));   // bigode
                VisualKit.Part(cab, VisualKit.Cylinder(0.006f, 0.016f, 0.08f, 6), barba, new Vector3(0, -0.15f, 0.07f));   // trancinha
                VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.012f, OuterRadius = 0.02f, Rings = 10, RingSegments = 6 }, M(new Color(1f, 0.8f, 0.3f), 0.25f), new Vector3(-0.115f, -0.02f, 0), new Vector3(0, 0, 90));   // brinco
                break;
            }
            case "abobora":
            {
                if (_hair is not null) _hair.Visible = false;
                var laranja = M(new Color(0.95f, 0.48f, 0.08f), 0.7f);
                for (var i = 0; i < 8; i++)   // gomos da abóbora em volta da cabeça
                {
                    var a = i / 8f * Mathf.Tau;
                    VisualKit.Part(cab, VisualKit.Sphere(0.1f), laranja, new Vector3(Mathf.Sin(a) * 0.07f, 0.06f, Mathf.Cos(a) * 0.07f), null, new Vector3(0.8f, 1.05f, 0.8f));
                }
                VisualKit.Part(cab, VisualKit.Cylinder(0.02f, 0.03f, 0.08f, 6), M(new Color(0.25f, 0.35f, 0.1f), 0.9f), new Vector3(0.01f, 0.2f, 0), new Vector3(0, 0, 12));   // talo
                var luz = VisualKit.Glow(new Color(1f, 0.75f, 0.2f), 2.2f);
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.03f, 0.04f, 3), luz, new Vector3(sd * 0.05f, 0.09f, 0.155f), new Vector3(90, 0, 0));   // olhos triangulares
                VisualKit.Part(cab, VisualKit.Box(0.11f, 0.022f, 0.01f), luz, new Vector3(0, 0.0f, 0.158f));   // boca
                foreach (var sd in new[] { -1f, 0f, 1f }) VisualKit.Part(cab, VisualKit.Box(0.02f, 0.02f, 0.01f), luz, new Vector3(sd * 0.035f, -0.018f, 0.158f));   // dentes
                cab.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.2f), LightEnergy = 0.35f, OmniRange = 1.2f, Position = new Vector3(0, 0.06f, 0.25f), ShadowEnabled = false });
                break;
            }
            case "lata":
            {
                if (_hair is not null) _hair.Visible = false;
                var lata = VisualKit.Mat(new Color(0.66f, 0.68f, 0.7f), 0.35f, 0.85f);
                var escuro = M(new Color(0.05f, 0.05f, 0.06f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.11f, 0.135f, 0.26f, 18), lata, new Vector3(0, 0.09f, -0.005f), new Vector3(4, 0, -6));   // o balde (de boca para baixo)
                VisualKit.Part(cab, VisualKit.Box(0.14f, 0.025f, 0.02f), escuro, new Vector3(0, 0.07f, 0.13f));   // fresta da viseira
                VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.12f, OuterRadius = 0.13f, Rings = 18, RingSegments = 4 }, lata, new Vector3(0, -0.035f, 0));
                VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.11f, Rings = 18, RingSegments = 4 }, lata, new Vector3(0, 0.24f, 0), new Vector3(0, 0, 70));   // alça do balde
                VisualKit.Part(cab, VisualKit.Sphere(0.03f), M(new Color(0.85f, 0.1f, 0.1f)), new Vector3(0, 0.26f, 0));   // penacho
                VisualKit.Part(peito, VisualKit.Cylinder(0.17f, 0.17f, 0.05f, 22), lata, new Vector3(0, 0.07f, 0.12f), new Vector3(90, 0, 0));   // panela de peitoral
                VisualKit.Part(peito, VisualKit.Box(0.04f, 0.2f, 0.03f), lata, new Vector3(0.21f, 0.07f, 0.13f), new Vector3(0, 0, -90));   // cabo da panela
                var colher = new Node3D { Position = new Vector3(-0.17f, 0.02f, 0.06f), RotationDegrees = new Vector3(0, 0, 14) };
                quadril.AddChild(colher);
                var madeira = M(new Color(0.6f, 0.42f, 0.22f), 0.85f);
                VisualKit.Part(colher, VisualKit.Cylinder(0.012f, 0.012f, 0.36f, 6), madeira, new Vector3(0, -0.12f, 0));
                VisualKit.Part(colher, VisualKit.Sphere(0.045f), madeira, new Vector3(0, -0.31f, 0), null, new Vector3(1f, 1.4f, 0.4f));
                break;
            }
            // ---------------- 14/11: enfeites das 24 skins da prancha
            case "executor":
            {
                var pelo = M(new Color(0.95f, 0.94f, 0.92f), 0.9f); var rosa = M(new Color(1f, 0.6f, 0.7f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    var orelha = VisualKit.Part(cab, VisualKit.Sphere(0.05f), pelo, new Vector3(sd * 0.06f, 0.3f, -0.01f), new Vector3(-8, 0, sd * -18), new Vector3(0.75f, 3.6f, 0.45f));
                    VisualKit.Part(orelha, VisualKit.Sphere(0.04f), rosa, new Vector3(0, 0, 0.012f), null, new Vector3(0.7f, 0.88f, 0.5f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(1f, 0.1f, 0.05f), 2.5f), new Vector3(sd * 0.045f, 0.03f, 0.105f));   // olhos vermelhos
                }
                // martelão nas costas
                var cabo = new Node3D { Position = new Vector3(0.05f, 0.05f, -0.2f), RotationDegrees = new Vector3(0, 0, 35) };
                peito.AddChild(cabo);
                VisualKit.Part(cabo, VisualKit.Cylinder(0.02f, 0.02f, 0.9f, 8), M(new Color(0.45f, 0.3f, 0.15f)), new Vector3(0, 0, 0));
                VisualKit.Part(cabo, VisualKit.Box(0.32f, 0.2f, 0.2f), VisualKit.Mat(new Color(0.3f, 0.3f, 0.32f), 0.4f, 0.8f), new Vector3(0, 0.48f, 0));
                VisualKit.Part(cabo, VisualKit.Box(0.2f, 0.06f, 0.21f), VisualKit.Glow(new Color(1f, 0.3f, 0.1f), 1.5f), new Vector3(0, 0.48f, 0));
                break;
            }
            case "gnomo":
            {
                var verde = M(new Color(0.42f, 0.66f, 0.25f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.05f, 0.2f, 6), verde, new Vector3(sd * 0.15f, 0.04f, -0.01f), new Vector3(0, 0, sd * -75), new Vector3(1f, 1f, 0.45f));   // orelhas pontudas
                    // olhos pequenos e rentes ao rosto (antes: bolas amarelas brilhando, "esbugalhados")
                    VisualKit.Part(cab, VisualKit.Sphere(0.011f), M(new Color(0.05f, 0.04f, 0.03f), 0.2f), new Vector3(sd * 0.04f, 0.025f, 0.097f), null, new Vector3(1f, 1.2f, 0.5f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.0035f), branco, new Vector3(sd * 0.04f + 0.004f, 0.03f, 0.102f));
                }
                // gravatinha borboleta vermelha e duas fileiras de botões dourados no sobretudo
                var gv = M(new Color(0.75f, 0.06f, 0.08f), 0.5f);
                foreach (var sd in new[] { -1f, 1f })
                    VisualKit.Part(peito, VisualKit.Cylinder(0.004f, 0.028f, 0.05f, 8), gv, new Vector3(sd * 0.027f, 0.19f, 0.11f), new Vector3(0, 0, sd * 90), new Vector3(1f, 1f, 0.5f));
                VisualKit.Part(peito, VisualKit.Sphere(0.012f), gv, new Vector3(0, 0.19f, 0.115f));
                var ouro = M(new Color(0.9f, 0.72f, 0.25f), 0.25f);
                foreach (var sd in new[] { -1f, 1f })
                    for (var k = 0; k < 3; k++)
                        VisualKit.Part(peito, VisualKit.Sphere(0.011f), ouro, new Vector3(sd * 0.035f, 0.12f - k * 0.07f, 0.128f - k * 0.004f));
                var roxo = M(new Color(0.38f, 0.16f, 0.55f), 0.5f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.17f, 0.17f, 0.015f, 20), roxo, new Vector3(0, 0.15f, -0.01f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.1f, 0.115f, 0.28f, 20), roxo, new Vector3(0, 0.29f, -0.01f), new Vector3(0, 0, -6));
                VisualKit.Part(cab, VisualKit.Cylinder(0.116f, 0.116f, 0.04f, 20), M(new Color(0.75f, 0.1f, 0.1f)), new Vector3(0, 0.19f, -0.01f));
                break;
            }
            case "sem_cabeca":
            {
                // sem cabeça: a gola da armadura e uma chama azul no lugar
                VisualKit.Part(peito, VisualKit.Cylinder(0.12f, 0.16f, 0.1f, 14), VisualKit.Mat(new Color(0.2f, 0.2f, 0.25f), 0.3f, 0.9f), new Vector3(0, 0.26f, 0));
                for (var i = 0; i < 5; i++)
                    VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.07f - i * 0.008f, 0.2f + i * 0.04f, 7), VisualKit.Glow(new Color(0.3f, 0.5f, 1f), 2.2f - i * 0.2f),
                        new Vector3(Mathf.Sin(i * 1.3f) * 0.04f, 0.4f + i * 0.02f, Mathf.Cos(i * 1.3f) * 0.03f), new Vector3(0, i * 40, (i - 2) * 6));
                peito.AddChild(new OmniLight3D { LightColor = new Color(0.4f, 0.55f, 1f), LightEnergy = 0.6f, OmniRange = 2f, Position = new Vector3(0, 0.45f, 0), ShadowEnabled = false });
                cab.Visible = false;
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Sphere(0.11f), VisualKit.Mat(new Color(0.22f, 0.22f, 0.27f), 0.3f, 0.9f), new Vector3(sd * 0.2f, 0.2f, 0), null, new Vector3(1f, 0.7f, 1f));   // ombreiras
                break;
            }
            case "corvo":
            {
                var preto = M(new Color(0.06f, 0.06f, 0.08f), 0.6f);
                VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.06f, 0.24f, 8), M(new Color(0.12f, 0.12f, 0.12f), 0.3f), new Vector3(0, -0.01f, 0.2f), new Vector3(100, 0, 0));   // bico
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.022f), VisualKit.Glow(new Color(0.75f, 0.35f, 1f), 2.4f), new Vector3(sd * 0.06f, 0.04f, 0.095f));
                for (var i = 0; i < 6; i++)   // penas do alto da cabeça
                    VisualKit.Part(cab, VisualKit.Box(0.03f, 0.13f, 0.01f), preto, new Vector3((i - 2.5f) * 0.03f, 0.15f, -0.04f), new Vector3(-40, 0, (i - 2.5f) * 12));
                for (var i = 0; i < 10; i++)   // capa de penas nos ombros
                {
                    var a = i / 10f * Mathf.Pi - Mathf.Pi / 2;
                    VisualKit.Part(peito, VisualKit.Box(0.07f, 0.2f, 0.012f), preto, new Vector3(Mathf.Sin(a) * 0.2f, 0.12f, -Mathf.Cos(a) * 0.12f - 0.02f), new Vector3(10, Mathf.RadToDeg(a), 0));
                }
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.13f, Rings = 16, RingSegments = 8 }, M(new Color(0.38f, 0.12f, 0.55f)), new Vector3(0, 0.23f, 0.02f));   // cachecol roxo
                break;
            }
            case "mafioso":
            case "sapo":
            {
                var feltro = M(skin == "sapo" ? new Color(0.25f, 0.18f, 0.12f) : new Color(0.12f, 0.11f, 0.12f), 0.6f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.18f, 0.18f, 0.015f, 22), feltro, new Vector3(0, 0.14f, -0.01f), new Vector3(-6, 0, 0));
                VisualKit.Part(cab, VisualKit.Cylinder(0.1f, 0.12f, 0.14f, 20), feltro, new Vector3(0, 0.21f, -0.01f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.121f, 0.121f, 0.03f, 20), M(new Color(0.06f, 0.06f, 0.06f)), new Vector3(0, 0.16f, -0.01f));
                if (skin == "sapo")
                {
                    // 18/11 21h (dono mandou a referência: sapo ranzinza de charuto): cabeça larga e achatada, olhões amarelos
                    // com pálpebra pesada caída (cara de tédio), pupila deitada, bocão de canto caído e bigode preto enrolado.
                    cab.Scale = Vector3.One / SapoOsso;
                    SapoRosto(cab);
                }
                else foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(1f, 0.7f, 0.15f), 2.4f), new Vector3(sd * 0.045f, 0.03f, 0.105f));
                // charuto aceso (no sapo, no canto da boca, como na referência)
                if (skin == "sapo")
                {
                    var semE = cab.GetNodeOrNull<Node3D>("SemEncaixe") ?? cab;
                    var c0 = new Vector3(0.112f, -0.078f, 0.165f);
                    var dirC = new Vector3(0.8f, -0.12f, 0.5f).Normalized();
                    Corda(semE, new[] { c0, c0 + dirC * 0.02f }, _ => 0.0125f, M(new Color(0.75f, 0.2f, 0.12f)));                      // anel do charuto
                    Corda(semE, new[] { c0 + dirC * 0.02f, c0 + dirC * 0.095f }, _ => 0.0115f, M(new Color(0.42f, 0.24f, 0.12f)));
                    VisualKit.Part(semE, VisualKit.Sphere(0.0118f), VisualKit.Glow(new Color(1f, 0.4f, 0.1f), 2.5f), c0 + dirC * 0.1f);   // brasa
                }
                else
                {
                    VisualKit.Part(cab, VisualKit.Cylinder(0.012f, 0.014f, 0.12f, 8), M(new Color(0.35f, 0.2f, 0.1f)), new Vector3(0.04f, -0.04f, 0.16f), new Vector3(80, 20, 0));
                    VisualKit.Part(cab, VisualKit.Sphere(0.014f), VisualKit.Glow(new Color(1f, 0.4f, 0.1f), 2.5f), new Vector3(0.06f, -0.03f, 0.22f));
                }
                // camisa, gravata vermelha
                // 17/11 (dono: "a roupa ainda tá voando, desgolada"): camisa e gravata agora são pintadas na roupa (PTerno)
                break;
            }
            case "esqueleto":
            case "pirata_morto":
            {
                var osso = M(new Color(0.92f, 0.9f, 0.84f), 0.7f);
                VisualKit.Part(cab, VisualKit.Sphere(0.12f), osso, new Vector3(0, 0.02f, 0), null, new Vector3(1f, 1.08f, 1f));
                VisualKit.Part(cab, VisualKit.Box(0.12f, 0.06f, 0.1f), osso, new Vector3(0, -0.09f, 0.04f));   // queixo
                var olho = skin == "esqueleto" ? VisualKit.Glow(new Color(0.4f, 0.7f, 1f), 2.6f) : VisualKit.Glow(new Color(0.3f, 1f, 0.5f), 2.4f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Sphere(0.035f), M(new Color(0.02f, 0.02f, 0.02f)), new Vector3(sd * 0.045f, 0.03f, 0.1f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.015f), olho, new Vector3(sd * 0.045f, 0.03f, 0.125f));
                }
                for (var i = 0; i < 6; i++) VisualKit.Part(cab, VisualKit.Box(0.014f, 0.025f, 0.01f), osso, new Vector3((i - 2.5f) * 0.018f, -0.075f, 0.105f));   // dentes
                if (skin == "esqueleto")
                    for (var i = 0; i < 10; i++)   // golinha de babado branca
                    {
                        var a = i / 10f * Mathf.Tau;
                        VisualKit.Part(peito, VisualKit.Sphere(0.045f), M(new Color(0.96f, 0.95f, 0.92f)), new Vector3(Mathf.Sin(a) * 0.12f, 0.23f, Mathf.Cos(a) * 0.09f), null, new Vector3(1f, 0.45f, 1f));
                    }
                else
                {
                    // tricórnio com caveira e gancho na mão esquerda (preso no quadril)
                    var feltro = M(new Color(0.06f, 0.05f, 0.05f), 0.6f);
                    VisualKit.Part(cab, VisualKit.Cylinder(0.2f, 0.2f, 0.02f, 3), feltro, new Vector3(0, 0.15f, -0.01f), new Vector3(0, 30, 0));
                    VisualKit.Part(cab, VisualKit.Cylinder(0.11f, 0.12f, 0.12f, 16), feltro, new Vector3(0, 0.22f, -0.01f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.03f), osso, new Vector3(0, 0.23f, 0.12f), null, new Vector3(1f, 1f, 0.4f));
                    var gancho = new Node3D { Position = new Vector3(-0.22f, -0.05f, 0.08f) };
                    quadril.AddChild(gancho);
                    VisualKit.Part(gancho, new TorusMesh { InnerRadius = 0.04f, OuterRadius = 0.055f, Rings = 14, RingSegments = 6 }, VisualKit.Mat(new Color(0.75f, 0.76f, 0.8f), 0.25f, 0.9f), Vector3.Zero, new Vector3(90, 0, 0));
                }
                break;
            }
            case "ouro":
            {
                cab.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.35f), LightEnergy = 0.5f, OmniRange = 2.2f, Position = new Vector3(0, -0.4f, 0.3f), ShadowEnabled = false });
                VisualKit.Part(cab, VisualKit.Sphere(0.125f), VisualKit.Mat(new Color(1f, 0.78f, 0.3f), 0.15f, 1f), Vector3.Zero);   // cabeça lisa de bola
                break;
            }
            case "magico":
            {
                var preto = M(new Color(0.06f, 0.05f, 0.06f), 0.4f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.17f, 0.17f, 0.015f, 20), preto, new Vector3(0, 0.16f, -0.01f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.1f, 0.11f, 0.24f, 20), preto, new Vector3(0, 0.28f, -0.01f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.112f, 0.112f, 0.04f, 20), M(new Color(0.4f, 0.12f, 0.6f)), new Vector3(0, 0.19f, -0.01f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(0.95f, 0.95f, 1f), 2.6f), new Vector3(sd * 0.045f, 0.03f, 0.105f));
                for (var i = 0; i < 10; i++)
                {
                    var a = i / 10f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Sphere(0.045f), M(new Color(0.96f, 0.95f, 0.92f)), new Vector3(Mathf.Sin(a) * 0.12f, 0.23f, Mathf.Cos(a) * 0.09f), null, new Vector3(1f, 0.45f, 1f));
                }
                var orbe = VisualKit.Part(barriga, VisualKit.Sphere(0.06f), VisualKit.Glow(new Color(0.8f, 0.4f, 1f), 2.5f), new Vector3(0.28f, 0.1f, 0.1f));
                orbe.AddChild(new OmniLight3D { LightColor = new Color(0.8f, 0.45f, 1f), LightEnergy = 0.5f, OmniRange = 1.5f, ShadowEnabled = false });
                break;
            }
            case "espantalho":
            {
                var palha = M(new Color(0.85f, 0.7f, 0.35f), 1f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.24f, 0.24f, 0.02f, 18), palha, new Vector3(0, 0.15f, -0.01f), new Vector3(-6, 0, 4));
                VisualKit.Part(cab, VisualKit.Cylinder(0.02f, 0.12f, 0.24f, 14), palha, new Vector3(0, 0.27f, -0.02f), new Vector3(-10, 0, 8));
                var luz = VisualKit.Glow(new Color(1f, 0.6f, 0.15f), 2.4f);
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.025f, 0.035f, 3), luz, new Vector3(sd * 0.045f, 0.04f, 0.115f), new Vector3(90, 0, 0));
                VisualKit.Part(cab, VisualKit.Box(0.1f, 0.018f, 0.01f), luz, new Vector3(0, -0.03f, 0.118f), new Vector3(0, 0, 0));
                for (var i = 0; i < 12; i++)   // tufos de palha saindo da gola, mangas e calça
                {
                    var a = i / 12f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.02f, 0.12f, 4), palha, new Vector3(Mathf.Sin(a) * 0.12f, 0.24f, Mathf.Cos(a) * 0.09f), new Vector3(Mathf.Cos(a) * 60, 0, -Mathf.Sin(a) * 60));
                }
                VisualKit.Part(peito, VisualKit.Box(0.06f, 0.06f, 0.01f), M(new Color(0.3f, 0.5f, 0.3f)), new Vector3(0.07f, 0.02f, 0.15f));   // remendo
                break;
            }
            case "demonio":
            {
                var chifre = M(new Color(0.12f, 0.03f, 0.03f), 0.4f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.035f, 0.16f, 8), chifre, new Vector3(sd * 0.08f, 0.15f, 0), new Vector3(0, 0, sd * -25));
                    VisualKit.Part(cab, VisualKit.Sphere(0.022f), VisualKit.Glow(new Color(1f, 0.1f, 0.05f), 2.6f), new Vector3(sd * 0.045f, 0.03f, 0.105f));
                    // asinhas de morcego nas costas
                    var asa = new Node3D { Position = new Vector3(sd * 0.06f, 0.1f, -0.15f), RotationDegrees = new Vector3(0, sd * -40, sd * -25) };
                    peito.AddChild(asa);
                    var membrana = M(new Color(0.5f, 0.05f, 0.06f), 0.6f);
                    for (var k = 0; k < 3; k++)   // asinha recortada em 3 pontas, rente às costas
                        VisualKit.Part(asa, VisualKit.Cylinder(0f, 0.06f, 0.2f - k * 0.04f, 3), membrana, new Vector3(sd * (0.06f + k * 0.05f), 0.04f - k * 0.03f, 0), new Vector3(0, 0, sd * (-70 + k * 25)), new Vector3(1f, 1f, 0.15f));
                }
                // 17/11: camisa/gravata agora pintadas na roupa (Pattern PTerno) — VisualKit.Part(peito, VisualKit.Box(0.12f, 0.3f, 0.01f), M(new Color(0.85f, 0.85f, 0.82f)), new Vector3(0, 0.04f, 0.15f));
                // 17/11: pintada — VisualKit.Part(peito, VisualKit.Box(0.045f, 0.28f, 0.012f), M(new Color(0.7f, 0.06f, 0.08f)), new Vector3(0, 0.01f, 0.16f));
                var rabo = new Node3D { Position = new Vector3(0, -0.05f, -0.14f), RotationDegrees = new Vector3(-40, 0, 0) };
                quadril.AddChild(rabo);
                VisualKit.Part(rabo, VisualKit.Cylinder(0.01f, 0.018f, 0.45f, 6), M(new Color(0.55f, 0.05f, 0.06f)), new Vector3(0, -0.2f, 0));
                VisualKit.Part(rabo, VisualKit.Cylinder(0f, 0.05f, 0.08f, 3), M(new Color(0.55f, 0.05f, 0.06f)), new Vector3(0, -0.45f, 0), new Vector3(180, 0, 0));
                break;
            }
            case "marionete":
            {
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.022f), VisualKit.Glow(new Color(1f, 0.75f, 0.2f), 2.2f), new Vector3(sd * 0.045f, 0.03f, 0.105f));
                VisualKit.Part(cab, VisualKit.Box(0.09f, 0.006f, 0.01f), M(new Color(0.15f, 0.08f, 0.04f)), new Vector3(0, -0.04f, 0.112f));   // boca costurada
                VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.12f, Rings = 16, RingSegments = 8 }, M(new Color(0.6f, 0.08f, 0.08f)), new Vector3(0, 0.23f, 0.02f));
                // cruzeta com os fios por cima da cabeça
                var cruz = new Node3D { Position = new Vector3(0, 0.9f, 0) };
                cab.AddChild(cruz);
                var madeira = M(new Color(0.4f, 0.27f, 0.15f));
                VisualKit.Part(cruz, VisualKit.Box(0.5f, 0.03f, 0.03f), madeira, Vector3.Zero);
                VisualKit.Part(cruz, VisualKit.Box(0.03f, 0.03f, 0.4f), madeira, Vector3.Zero);
                var fio = M(new Color(0.85f, 0.82f, 0.7f));
                foreach (var (x, z) in new[] { (-0.24f, 0f), (0.24f, 0f), (0f, 0.19f), (0f, -0.19f) })
                    VisualKit.Part(cruz, VisualKit.Cylinder(0.002f, 0.002f, 0.85f, 3), fio, new Vector3(x * 0.8f, -0.43f, z * 0.6f));
                break;
            }
            case "peste":
            {
                var couro = M(new Color(0.12f, 0.1f, 0.09f), 0.5f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.005f, 0.07f, 0.28f, 10), couro, new Vector3(0, -0.04f, 0.22f), new Vector3(105, 0, 0));   // bico
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Cylinder(0.035f, 0.035f, 0.02f, 14), VisualKit.Mat(new Color(0.75f, 0.55f, 0.2f), 0.3f, 0.8f), new Vector3(sd * 0.045f, 0.04f, 0.11f), new Vector3(90, 0, 0));
                    VisualKit.Part(cab, VisualKit.Sphere(0.022f), VisualKit.Glow(new Color(1f, 0.85f, 0.4f), 2f), new Vector3(sd * 0.045f, 0.04f, 0.12f));
                }
                VisualKit.Part(cab, VisualKit.Cylinder(0.22f, 0.22f, 0.015f, 22), couro, new Vector3(0, 0.15f, -0.01f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.1f, 0.11f, 0.14f, 20), couro, new Vector3(0, 0.22f, -0.01f));
                var frasco = VisualKit.Part(barriga, VisualKit.Cylinder(0.035f, 0.045f, 0.12f, 10), VisualKit.Glow(new Color(0.4f, 1f, 0.3f), 1.6f), new Vector3(0.18f, -0.05f, 0.1f));
                frasco.AddChild(new OmniLight3D { LightColor = new Color(0.4f, 1f, 0.3f), LightEnergy = 0.3f, OmniRange = 1f, ShadowEnabled = false });
                break;
            }
            case "samurai":
            {
                var azul = VisualKit.Glow(new Color(0.25f, 0.45f, 1f), 1.4f);
                VisualKit.Part(cab, VisualKit.Sphere(0.135f), VisualKit.Mat(new Color(0.12f, 0.16f, 0.38f), 0.35f, 0.6f), new Vector3(0, 0.04f, -0.01f), null, new Vector3(1.05f, 0.9f, 1.05f));   // kabuto
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.03f, 0.22f, 6), azul, new Vector3(sd * 0.09f, 0.2f, 0.06f), new Vector3(-20, 0, sd * -30));   // chifres
                    VisualKit.Part(cab, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(0.6f, 0.85f, 1f), 3f), new Vector3(sd * 0.045f, 0.02f, 0.11f));
                }
                VisualKit.Part(cab, VisualKit.Box(0.2f, 0.08f, 0.02f), VisualKit.Mat(new Color(0.08f, 0.1f, 0.25f), 0.35f, 0.6f), new Vector3(0, -0.06f, 0.11f));   // máscara
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Box(0.15f, 0.1f, 0.17f), VisualKit.Mat(new Color(0.12f, 0.16f, 0.38f), 0.35f, 0.6f), new Vector3(sd * 0.21f, 0.17f, -0.01f), new Vector3(0, 0, sd * -25));   // sode
                var fx = new CpuParticles3D
                {
                    Amount = 16, Lifetime = 1.0f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.25f, 0.4f, 0.15f),
                    Direction = Vector3.Up, Spread = 20f, Gravity = new Vector3(0, 1.2f, 0), InitialVelocityMin = 0.1f, InitialVelocityMax = 0.4f,
                    ScaleAmountMin = 0.02f, ScaleAmountMax = 0.05f, LocalCoords = false, Position = new Vector3(0, -0.2f, 0),
                    Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3, Material = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.6f, 1f), EmissionEnabled = true, Emission = new Color(0.3f, 0.5f, 1f), EmissionEnergyMultiplier = 3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
                };
                peito.AddChild(fx);
                break;
            }
            case "palhaco_sinistro":
            {
                var cabelo = M(new Color(0.75f, 0.06f, 0.06f), 0.95f);
                foreach (var sd in new[] { -1f, 1f })
                {
                    for (var i = 0; i < 3; i++) VisualKit.Part(cab, VisualKit.Sphere(0.07f), cabelo, new Vector3(sd * (0.11f + i * 0.02f), 0.06f + i * 0.04f, -0.02f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.03f), M(new Color(0.05f, 0.05f, 0.05f)), new Vector3(sd * 0.045f, 0.03f, 0.1f), null, new Vector3(1f, 1.4f, 0.5f));
                    // 18/11 17h (dono: "esse palhaço ficou maneiro, tira o olho que tá atrás e deixa só esse pretão medonho"):
                    // sem o olho de conta por trás e sem o pontinho vermelho — só o buraco preto
                }
                VisualKit.Part(cab, VisualKit.Sphere(0.03f), M(new Color(0.85f, 0.05f, 0.05f), 0.25f), new Vector3(0, -0.005f, 0.125f));
                VisualKit.Part(cab, VisualKit.Box(0.13f, 0.02f, 0.01f), M(new Color(0.6f, 0.02f, 0.04f)), new Vector3(0, -0.055f, 0.112f), new Vector3(0, 0, 0));   // sorriso
                for (var i = 0; i < 12; i++)
                {
                    var a = i / 12f * Mathf.Tau;
                    // 18/11 17h (dono: "deixa esse babado no pescoço um pouco mais abaixo, tá muito no queixo — tem que ser nos ombros")
                    VisualKit.Part(peito, VisualKit.Sphere(0.055f), M(new Color(0.95f, 0.94f, 0.92f)), new Vector3(Mathf.Sin(a) * 0.165f, 0.165f, Mathf.Cos(a) * 0.12f), null, new Vector3(1f, 0.42f, 1f));
                }
                break;
            }
            case "cozinheiro_insano":
            {
                var branco2 = M(new Color(0.96f, 0.96f, 0.94f), 0.9f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.11f, 0.105f, 0.09f, 20), branco2, new Vector3(0, 0.17f, -0.01f));
                VisualKit.Part(cab, VisualKit.Sphere(0.15f), branco2, new Vector3(0, 0.33f, -0.02f), null, new Vector3(1f, 0.85f, 1f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(1f, 0.75f, 0.2f), 2.4f), new Vector3(sd * 0.045f, 0.03f, 0.105f));
                VisualKit.Part(barriga, VisualKit.Box(0.3f, 0.42f, 0.012f), branco2, new Vector3(0, -0.14f, 0.15f), new Vector3(-4, 0, 0));
                foreach (var (x, y) in new[] { (-0.06f, -0.05f), (0.05f, -0.15f), (-0.02f, -0.25f) })
                    VisualKit.Part(barriga, VisualKit.Sphere(0.03f), M(new Color(0.55f, 0.02f, 0.03f)), new Vector3(x, y, 0.158f), null, new Vector3(1.4f, 1f, 0.2f));   // manchas
                var cutelo = new Node3D { Position = new Vector3(0.2f, 0f, 0.05f), RotationDegrees = new Vector3(0, 0, -10) };
                barriga.AddChild(cutelo);
                VisualKit.Part(cutelo, VisualKit.Box(0.03f, 0.14f, 0.03f), M(new Color(0.3f, 0.2f, 0.1f)), new Vector3(0, 0.02f, 0));
                VisualKit.Part(cutelo, VisualKit.Box(0.14f, 0.18f, 0.01f), VisualKit.Mat(new Color(0.75f, 0.76f, 0.78f), 0.2f, 0.9f), new Vector3(0.05f, -0.13f, 0));
                break;
            }
            case "rei":
            {
                var ouro = VisualKit.Mat(new Color(1f, 0.78f, 0.25f), 0.2f, 1f);
                VisualKit.Part(cab, VisualKit.Cylinder(0.12f, 0.12f, 0.07f, 16), ouro, new Vector3(0, 0.16f, -0.01f));
                for (var i = 0; i < 8; i++)
                {
                    var a = i / 8f * Mathf.Tau;
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.025f, 0.08f, 4), ouro, new Vector3(Mathf.Sin(a) * 0.11f, 0.23f, Mathf.Cos(a) * 0.11f));
                    if (i % 2 == 0) VisualKit.Part(cab, VisualKit.Sphere(0.014f), VisualKit.Glow(new Color(0.9f, 0.1f, 0.15f), 1.5f), new Vector3(Mathf.Sin(a) * 0.122f, 0.16f, Mathf.Cos(a) * 0.122f));
                }
                for (var i = 0; i < 12; i++)   // gola de arminho
                {
                    var a = i / 12f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Sphere(0.06f), M(new Color(0.96f, 0.95f, 0.92f), 1f), new Vector3(Mathf.Sin(a) * 0.15f, 0.22f, Mathf.Cos(a) * 0.11f), null, new Vector3(1f, 0.55f, 1f));
                    if (i % 3 == 0) VisualKit.Part(peito, VisualKit.Sphere(0.012f), M(new Color(0.05f, 0.05f, 0.05f)), new Vector3(Mathf.Sin(a) * 0.17f, 0.24f, Mathf.Cos(a) * 0.13f));
                }
                VisualKit.Part(peito, VisualKit.Box(0.04f, 0.18f, 0.012f), M(new Color(0.7f, 0.06f, 0.08f)), new Vector3(0, 0.07f, 0.16f));   // 17/11: faixa só no peito
                break;
            }
            case "pedra":
            case "cristal":
            {
                var cristal = skin == "cristal";
                var mat = cristal ? new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.4f, 1f, 0.75f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, EmissionEnabled = true, Emission = new Color(0.45f, 0.25f, 1f), EmissionEnergyMultiplier = 1.2f }
                                  : VisualKit.Mat(new Color(0.42f, 0.42f, 0.44f), 0.95f);
                VisualKit.Part(cab, VisualKit.Box(0.22f, 0.24f, 0.22f), mat, new Vector3(0, 0.02f, 0), cristal ? new Vector3(10, 30, 8) : null);   // cabeça de bloco / gema
                var runa = VisualKit.Glow(cristal ? new Color(0.9f, 0.8f, 1f) : new Color(0.3f, 0.7f, 1f), 2.4f);
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Box(0.04f, 0.03f, 0.01f), runa, new Vector3(sd * 0.05f, 0.04f, 0.115f));
                VisualKit.Part(peito, VisualKit.Box(0.06f, 0.06f, 0.01f), runa, new Vector3(0, 0.1f, 0.155f), new Vector3(0, 0, 45));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Sphere(0.09f), mat, new Vector3(sd * 0.2f, 0.19f, -0.01f), new Vector3(0, 0, sd * -20), new Vector3(1.25f, 0.75f, 1.15f));   // 17/11: ombreira redonda colada no ombro (era um caixote)
                if (cristal) peito.AddChild(new OmniLight3D { LightColor = new Color(0.6f, 0.4f, 1f), LightEnergy = 0.6f, OmniRange = 2f, ShadowEnabled = false });
                break;
            }
            case "minotauro":
            {
                var chifre = M(new Color(0.85f, 0.8f, 0.68f), 0.6f);
                // 17/11 (dono: "detalhes que não fazem jus"): focinho de touro arredondado com narinas, não um caixote na cara
                var focinho = M(new Color(0.36f, 0.23f, 0.14f), 0.85f);
                VisualKit.Part(cab, VisualKit.Sphere(0.075f), focinho, new Vector3(0, -0.035f, 0.1f), null, new Vector3(1.25f, 0.9f, 1.05f));
                VisualKit.Part(cab, VisualKit.Sphere(0.05f), M(new Color(0.48f, 0.32f, 0.24f), 0.8f), new Vector3(0, -0.05f, 0.155f), null, new Vector3(1.4f, 0.75f, 0.5f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.012f), M(new Color(0.05f, 0.03f, 0.02f), 0.6f), new Vector3(sd * 0.022f, -0.045f, 0.176f), null, new Vector3(1f, 1.3f, 0.5f));
                VisualKit.Part(cab, new TorusMesh { InnerRadius = 0.02f, OuterRadius = 0.03f, Rings = 12, RingSegments = 6 }, VisualKit.Mat(new Color(0.85f, 0.7f, 0.3f), 0.3f, 0.9f), new Vector3(0, -0.085f, 0.172f), new Vector3(0, 0, 0));   // argola
                foreach (var sd in new[] { -1f, 1f })
                {
                    // 16/11: chifre liso e afinando (antes eram 6 bolinhas e parecia uma lagarta)
                    var pts = new Vector3[8];
                    for (var i = 0; i < 8; i++)
                    {
                        var t = i / 7f;
                        pts[i] = new Vector3(sd * (0.1f + t * 0.17f), 0.09f + Mathf.Sin(t * 2.4f) * 0.13f, -0.02f + t * 0.06f);
                    }
                    Segmentos(cab, pts, 0.042f, 0.006f, chifre);
                    VisualKit.Part(cab, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(1f, 0.3f, 0.1f), 2f), new Vector3(sd * 0.05f, 0.04f, 0.11f));
                }
                for (var i = 0; i < 12; i++)   // pele de pelo nos ombros
                {
                    var a = i / 12f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Sphere(0.07f), M(new Color(0.35f, 0.25f, 0.18f), 1f), new Vector3(Mathf.Sin(a) * 0.17f, 0.2f, Mathf.Cos(a) * 0.12f), null, new Vector3(1f, 0.6f, 1f));
                }
                break;
            }
            case "mumia":
            {
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.022f), VisualKit.Glow(new Color(1f, 0.85f, 0.2f), 2.6f), new Vector3(sd * 0.045f, 0.03f, 0.108f));
                var faixa = M(new Color(0.86f, 0.8f, 0.66f), 1f);
                for (var i = 0; i < 5; i++)   // faixas soltas
                {
                    var no = i < 2 ? cab : i < 4 ? peito : quadril;
                    VisualKit.Part(no, VisualKit.Box(0.04f, 0.2f, 0.005f), faixa, new Vector3((i - 2) * 0.06f, -0.08f, -0.12f), new Vector3(15, 0, (i - 2) * 12));
                }
                break;
            }
            case "peixe":
            {
                var escama = VisualKit.Mat(new Color(0.18f, 0.25f, 0.42f), 0.3f, 0.3f);
                VisualKit.Part(cab, VisualKit.Sphere(0.14f), escama, new Vector3(0, 0.0f, 0.03f), null, new Vector3(1.05f, 1f, 1.15f));
                VisualKit.Part(cab, VisualKit.Box(0.2f, 0.05f, 0.08f), M(new Color(0.05f, 0.05f, 0.08f)), new Vector3(0, -0.06f, 0.13f));   // bocarra
                var dente = M(new Color(0.95f, 0.93f, 0.85f), 0.4f);
                for (var i = 0; i < 7; i++) VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.012f, 0.05f, 4), dente, new Vector3((i - 3) * 0.028f, -0.045f, 0.165f), new Vector3(180, 0, 0));
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Sphere(0.03f), M(new Color(0.9f, 0.9f, 0.8f), 0.2f), new Vector3(sd * 0.07f, 0.05f, 0.12f));
                    VisualKit.Part(cab, VisualKit.Box(0.12f, 0.1f, 0.01f), escama, new Vector3(sd * 0.15f, 0.02f, -0.02f), new Vector3(0, sd * 40, sd * -20));   // barbatanas
                }
                // a isca de luz pendurada na frente
                var haste = new Node3D { Position = new Vector3(0, 0.13f, 0.06f), RotationDegrees = new Vector3(45, 0, 0) };
                cab.AddChild(haste);
                VisualKit.Part(haste, VisualKit.Cylinder(0.006f, 0.008f, 0.25f, 5), escama, new Vector3(0, 0.12f, 0));
                var isca = VisualKit.Part(haste, VisualKit.Sphere(0.035f), VisualKit.Glow(new Color(0.5f, 0.9f, 1f), 3f), new Vector3(0, 0.26f, 0.04f));
                isca.AddChild(new OmniLight3D { LightColor = new Color(0.5f, 0.9f, 1f), LightEnergy = 0.6f, OmniRange = 2f, ShadowEnabled = false });
                break;
            }
            case "urso":
            {
                var pelo = M(new Color(0.62f, 0.42f, 0.24f), 1f);
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Sphere(0.05f), pelo, new Vector3(sd * 0.1f, 0.13f, -0.01f), null, new Vector3(1f, 1f, 0.6f));   // orelhas
                VisualKit.Part(cab, VisualKit.Sphere(0.06f), M(new Color(0.82f, 0.66f, 0.45f), 1f), new Vector3(0, -0.04f, 0.1f), null, new Vector3(1.1f, 0.8f, 0.7f));   // focinho
                VisualKit.Part(cab, VisualKit.Sphere(0.018f), M(new Color(0.08f, 0.05f, 0.04f)), new Vector3(0, -0.02f, 0.15f));
                VisualKit.Part(cab, VisualKit.Cylinder(0.025f, 0.025f, 0.01f, 10), M(new Color(0.08f, 0.05f, 0.04f)), new Vector3(-0.045f, 0.04f, 0.108f), new Vector3(90, 0, 0));   // olho de botão
                foreach (var r in new[] { 45f, -45f }) VisualKit.Part(cab, VisualKit.Box(0.05f, 0.008f, 0.005f), M(new Color(0.75f, 0.1f, 0.1f)), new Vector3(0.045f, 0.04f, 0.112f), new Vector3(0, 0, r));   // X costurado
                VisualKit.Part(cab, VisualKit.Sphere(0.02f), VisualKit.Glow(new Color(1f, 0.75f, 0.2f), 2.4f), new Vector3(0.045f, 0.04f, 0.1f));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Sphere(0.035f), M(new Color(0.7f, 0.05f, 0.08f)), new Vector3(sd * 0.03f, 0.2f, 0.15f), null, new Vector3(1.3f, 0.8f, 0.5f));   // gravatinha
                for (var i = 0; i < 4; i++) VisualKit.Part(peito, VisualKit.Box(0.004f, 0.04f, 0.004f), M(new Color(0.3f, 0.2f, 0.1f)), new Vector3(-0.05f + i * 0.012f, 0.03f, 0.155f), new Vector3(0, 0, 70));   // costura
                break;
            }
            // ---------------- 14/11 noite: 6 skins da 2ª prancha
            case "macaco_vermelho":
            {
                var pelo = M(new Color(0.7f, 0.1f, 0.08f), 1f);
                var cara = M(new Color(0.88f, 0.68f, 0.52f), 0.8f);
                VisualKit.Part(cab, VisualKit.Sphere(0.1f), cara, new Vector3(0, -0.02f, 0.06f), null, new Vector3(1f, 1f, 0.7f));   // cara pelada
                VisualKit.Part(cab, VisualKit.Sphere(0.06f), cara, new Vector3(0, -0.06f, 0.12f), null, new Vector3(1.2f, 0.8f, 0.8f));   // focinho
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Sphere(0.022f), VisualKit.Glow(new Color(1f, 0.85f, 0.2f), 2.6f), new Vector3(sd * 0.04f, 0.035f, 0.115f));
                    VisualKit.Part(cab, VisualKit.Box(0.05f, 0.012f, 0.01f), M(new Color(0.25f, 0.03f, 0.03f)), new Vector3(sd * 0.045f, 0.065f, 0.118f), new Vector3(0, 0, sd * -18));   // sobrancelha brava
                    VisualKit.Part(cab, VisualKit.Sphere(0.04f), cara, new Vector3(sd * 0.125f, 0.03f, 0f), null, new Vector3(0.5f, 1f, 0.9f));   // orelhas
                }
                for (var i = 0; i < 9; i++)   // juba espetada
                {
                    var a = (i / 8f - 0.5f) * 2.4f;
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.045f, 0.18f, 6), pelo, new Vector3(Mathf.Sin(a) * 0.08f, 0.12f + Mathf.Cos(a) * 0.03f, -0.04f - Mathf.Abs(a) * 0.02f), new Vector3(-30 - Mathf.Abs(a) * 10, 0, -a * 40));
                }
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Sphere(0.08f), pelo, new Vector3(sd * 0.2f, 0.18f, 0), null, new Vector3(1f, 0.7f, 1f));   // tufos nos ombros
                var cauda = new Node3D { Position = new Vector3(0, -0.02f, -0.12f) };
                quadril.AddChild(cauda);
                for (var i = 0; i < 12; i++)   // rabo comprido que enrola na ponta
                {
                    var t = i / 11f;
                    var ang = t * 4.2f;
                    VisualKit.Part(cauda, VisualKit.Sphere(0.04f - t * 0.012f), pelo, new Vector3(Mathf.Sin(ang) * 0.12f * t, -0.1f + Mathf.Cos(ang) * 0.18f * t - t * 0.15f, -0.05f - t * 0.35f));
                }
                break;
            }
            case "caveira":
            {
                var osso = M(new Color(0.94f, 0.9f, 0.82f), 0.7f);
                var oco = M(new Color(0.08f, 0.05f, 0.04f));
                VisualKit.Part(cab, VisualKit.Sphere(0.13f), osso, new Vector3(0, 0.03f, 0), null, new Vector3(1.05f, 1.05f, 1.05f));
                VisualKit.Part(cab, VisualKit.Box(0.11f, 0.06f, 0.09f), osso, new Vector3(0, -0.09f, 0.05f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Sphere(0.04f), oco, new Vector3(sd * 0.048f, 0.03f, 0.1f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.017f), VisualKit.Glow(new Color(1f, 0.6f, 0.1f), 3f), new Vector3(sd * 0.048f, 0.03f, 0.13f));
                }
                VisualKit.Part(cab, VisualKit.Box(0.02f, 0.025f, 0.01f), oco, new Vector3(0, -0.02f, 0.13f));   // nariz
                for (var i = 0; i < 6; i++) VisualKit.Part(cab, VisualKit.Box(0.014f, 0.025f, 0.01f), osso, new Vector3((i - 2.5f) * 0.018f, -0.075f, 0.108f));
                for (var i = 0; i < 4; i++)   // costelas
                    VisualKit.Part(peito, new TorusMesh { InnerRadius = 0.13f - i * 0.008f, OuterRadius = 0.145f - i * 0.008f, Rings = 18, RingSegments = 6 }, osso, new Vector3(0, 0.14f - i * 0.06f, 0.02f), new Vector3(90, 0, 0), new Vector3(1f, 1f, 0.75f));
                VisualKit.Part(peito, VisualKit.Box(0.03f, 0.24f, 0.03f), osso, new Vector3(0, 0.06f, 0.14f));   // esterno
                break;
            }
            case "alien":
            {
                var pele = VisualKit.Mat(new Color(0.56f, 0.8f, 0.32f), 0.4f);
                VisualKit.Part(cab, VisualKit.Sphere(0.15f), pele, new Vector3(0, 0.08f, 0), null, new Vector3(1.05f, 1.35f, 1.05f));   // cabeção
                VisualKit.Part(cab, VisualKit.Sphere(0.08f), pele, new Vector3(0, -0.07f, 0.04f), null, new Vector3(0.9f, 1f, 0.9f));    // queixo fino
                var olho = new StandardMaterial3D { AlbedoColor = new Color(0.03f, 0.03f, 0.05f), Roughness = 0.05f, Metallic = 0.3f };
                foreach (var sd in new[] { -1f, 1f })
                    VisualKit.Part(cab, VisualKit.Sphere(0.05f), olho, new Vector3(sd * 0.06f, 0.04f, 0.115f), new Vector3(0, 0, sd * -30), new Vector3(1.4f, 0.8f, 0.5f));   // olhos amendoados
                VisualKit.Part(cab, VisualKit.Box(0.03f, 0.005f, 0.005f), M(new Color(0.25f, 0.4f, 0.12f)), new Vector3(0, -0.07f, 0.12f));
                break;
            }
            case "tubarao":
            {
                var azul = VisualKit.Mat(new Color(0.32f, 0.38f, 0.66f), 0.4f);
                var branca = M(new Color(0.93f, 0.93f, 0.9f), 0.5f);
                VisualKit.Part(cab, VisualKit.Sphere(0.14f), azul, new Vector3(0, 0.03f, 0.04f), null, new Vector3(1f, 0.95f, 1.35f));   // focinho comprido
                VisualKit.Part(cab, VisualKit.Sphere(0.11f), branca, new Vector3(0, -0.06f, 0.08f), null, new Vector3(1f, 0.55f, 1.2f));   // queixo branca
                VisualKit.Part(cab, VisualKit.Box(0.18f, 0.04f, 0.06f), M(new Color(0.35f, 0.04f, 0.06f)), new Vector3(0, -0.045f, 0.17f));   // bocarra
                var dente = M(new Color(0.98f, 0.97f, 0.92f), 0.3f);
                for (var i = 0; i < 8; i++)
                {
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.012f, 0.035f, 4), dente, new Vector3((i - 3.5f) * 0.022f, -0.03f, 0.19f), new Vector3(180, 0, 0));
                    VisualKit.Part(cab, VisualKit.Cylinder(0f, 0.012f, 0.03f, 4), dente, new Vector3((i - 3.5f) * 0.022f, -0.06f, 0.185f));
                }
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Sphere(0.02f), M(new Color(0.95f, 0.95f, 0.9f), 0.2f), new Vector3(sd * 0.08f, 0.07f, 0.12f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.011f), M(new Color(0.02f, 0.02f, 0.02f)), new Vector3(sd * 0.082f, 0.07f, 0.138f));
                    VisualKit.Part(cab, VisualKit.Box(0.05f, 0.012f, 0.01f), azul, new Vector3(sd * 0.075f, 0.1f, 0.13f), new Vector3(0, 0, sd * -20));   // cenho bravo
                    for (var g = 0; g < 3; g++) VisualKit.Part(cab, VisualKit.Box(0.004f, 0.05f, 0.01f), M(new Color(0.15f, 0.18f, 0.32f)), new Vector3(sd * (0.12f + g * 0.012f), -0.02f, 0.02f - g * 0.02f));   // guelras
                }
                VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.12f, 0.3f, 3), azul, new Vector3(0, 0.15f, -0.2f), new Vector3(-35, 0, 0), new Vector3(0.25f, 1f, 1f));   // barbatana das costas
                var rabo = new Node3D { Position = new Vector3(0, -0.05f, -0.15f), RotationDegrees = new Vector3(-50, 0, 0) };
                quadril.AddChild(rabo);
                VisualKit.Part(rabo, VisualKit.Cylinder(0.03f, 0.08f, 0.35f, 8), azul, new Vector3(0, -0.17f, 0));
                VisualKit.Part(rabo, VisualKit.Cylinder(0f, 0.14f, 0.28f, 3), azul, new Vector3(0, -0.4f, 0.06f), new Vector3(-30, 0, 0), new Vector3(0.2f, 1f, 1f));
                VisualKit.Part(rabo, VisualKit.Cylinder(0f, 0.1f, 0.2f, 3), azul, new Vector3(0, -0.4f, -0.06f), new Vector3(200, 0, 0), new Vector3(0.2f, 1f, 1f));
                break;
            }
            case "fantasma":
            {
                var lencol = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.93f, 0.97f), Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
                VisualKit.Part(cab, VisualKit.Sphere(0.16f), lencol, new Vector3(0, 0.03f, 0), null, new Vector3(1.05f, 1.1f, 1.05f));
                VisualKit.Part(peito, VisualKit.Cylinder(0.17f, 0.3f, 0.5f, 14), lencol, new Vector3(0, -0.05f, 0));   // lençol caindo dos ombros
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(cab, VisualKit.Sphere(0.035f), M(new Color(0.05f, 0.03f, 0.05f)), new Vector3(sd * 0.055f, 0.04f, 0.135f), new Vector3(0, 0, sd * 20), new Vector3(1.2f, 0.75f, 0.4f));
                    VisualKit.Part(cab, VisualKit.Sphere(0.017f), VisualKit.Glow(new Color(1f, 0.6f, 0.12f), 3f), new Vector3(sd * 0.055f, 0.04f, 0.15f));
                }
                cab.AddChild(new OmniLight3D { LightColor = new Color(0.85f, 0.85f, 1f), LightEnergy = 0.25f, OmniRange = 1.6f, ShadowEnabled = false });
                break;
            }
            case "lava":
            {
                var rocha = VisualKit.Mat(new Color(0.16f, 0.1f, 0.09f), 0.95f);
                var magma = VisualKit.Glow(new Color(1f, 0.45f, 0.08f), 2.6f);
                VisualKit.Part(cab, VisualKit.Box(0.22f, 0.22f, 0.22f), rocha, new Vector3(0, 0.02f, 0), new Vector3(6, 12, 4));
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(cab, VisualKit.Box(0.05f, 0.025f, 0.01f), VisualKit.Glow(new Color(1f, 0.8f, 0.2f), 3.2f), new Vector3(sd * 0.05f, 0.04f, 0.118f), new Vector3(0, 0, sd * -15));
                VisualKit.Part(cab, VisualKit.Box(0.08f, 0.012f, 0.01f), magma, new Vector3(0, -0.05f, 0.118f));
                for (var i = 0; i < 5; i++) VisualKit.Part(peito, VisualKit.Box(0.11f, 0.1f, 0.06f), rocha, new Vector3((i % 3 - 1) * 0.12f, 0.16f - (i / 3) * 0.12f, 0.13f), new Vector3(0, 0, i * 17));   // placas de rocha
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(peito, VisualKit.Sphere(0.09f), rocha, new Vector3(sd * 0.2f, 0.19f, -0.01f), new Vector3(0, 0, sd * -20), new Vector3(1.25f, 0.75f, 1.15f));   // 17/11: ombreira redonda
                for (var i = 0; i < 7; i++)   // veios de magma entre as placas
                    VisualKit.Part(peito, VisualKit.Box(0.012f, 0.14f, 0.01f), magma, new Vector3(-0.15f + i * 0.05f, 0.08f + (i % 2) * 0.05f, 0.165f), new Vector3(0, 0, -30 + i * 11));
                peito.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.45f, 0.1f), LightEnergy = 0.7f, OmniRange = 2.2f, ShadowEnabled = false });
                peito.AddChild(new CpuParticles3D
                {
                    Amount = 14, Lifetime = 1f, Position = new Vector3(0, 0.2f, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.25f, 0.1f, 0.12f),
                    Direction = Vector3.Up, Spread = 20f, Gravity = new Vector3(0, 1.2f, 0), InitialVelocityMin = 0.2f, InitialVelocityMax = 0.5f,
                    ScaleAmountMin = 0.015f, ScaleAmountMax = 0.035f, LocalCoords = false,
                    Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3, Material = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.5f, 0.1f), EmissionEnabled = true, Emission = new Color(1f, 0.45f, 0.1f), EmissionEnergyMultiplier = 3f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
                });
                break;
            }
            case "bobo":
            {
                var verm = M(new Color(0.75f, 0.08f, 0.12f), 0.7f); var amar = M(new Color(0.95f, 0.75f, 0.1f), 0.7f);
                var ouro = VisualKit.Mat(new Color(1f, 0.8f, 0.3f), 0.25f, 0.9f);
                VisualKit.Part(cab, VisualKit.Sphere(0.12f), verm, new Vector3(0, 0.12f, -0.02f), null, new Vector3(1.05f, 0.7f, 1.1f));
                var pontas = new[] { (-1f, verm), (0f, amar), (1f, verm) };
                foreach (var (sd, mat) in pontas)
                {
                    var raiz = new Node3D { Position = new Vector3(sd * 0.09f, 0.17f, -0.02f), RotationDegrees = new Vector3(sd == 0 ? -35 : 0, 0, sd * -55) };
                    cab.AddChild(raiz);
                    var p1 = VisualKit.Part(raiz, VisualKit.Cylinder(0.025f, 0.05f, 0.16f, 8), mat, new Vector3(0, 0.07f, 0));
                    var p2 = VisualKit.Part(p1, VisualKit.Cylinder(0.008f, 0.025f, 0.14f, 8), mat, new Vector3(0, 0.12f, 0), new Vector3(0, 0, sd == 0 ? 0 : sd * 50));
                    VisualKit.Part(p2, VisualKit.Sphere(0.025f), ouro, new Vector3(0, 0.08f, 0));   // guizo
                }
                for (var i = 0; i < 3; i++)   // losangos no peito
                    VisualKit.Part(peito, VisualKit.Box(0.07f, 0.07f, 0.01f), i % 2 == 0 ? amar : verm, new Vector3(-0.06f + i * 0.06f, 0.06f, 0.15f), new Vector3(0, 0, 45));
                for (var i = 0; i < 8; i++)   // gola de pontas
                {
                    var a = i / 8f * Mathf.Tau;
                    VisualKit.Part(peito, VisualKit.Cylinder(0f, 0.05f, 0.1f, 4), i % 2 == 0 ? verm : amar, new Vector3(Mathf.Sin(a) * 0.15f, 0.2f, Mathf.Cos(a) * 0.11f), new Vector3(Mathf.Cos(a) * 110, 0, -Mathf.Sin(a) * 110));
                }
                break;
            }
        }
        // 15/11 noite ("rosto, olhos"): skins de cara humana ganham rosto (olhos com íris, sobrancelhas, nariz, boca); as de
        // bicho/criatura, olhos acesos ou buracos — só as que ainda não tinham olhos próprios
        if (RostoDaSkin.TryGetValue(skin, out var rosto)) Rosto(cab, rosto.Tipo, rosto.Cor);
        // 17/11 (dono: "as skins ainda estão tortas, os detalhes não estão encaixados — só a do coelho é boa"): cada enfeite
        // fino (peitilho, gravata, botões, bolinhas, olhos, bigode…) é encostado na PELE de verdade do boneco — medida na malha
        // em pose de referência —, em vez de ficar na profundidade chutada (voando na frente ou enterrado no peito)
        if (skin != "coelho")
        {
            EncostarNaPele(peito, "Spine2", peito.Position);
            EncostarNaPele(barriga, barriga.GetParent() == quadril ? "Hips" : "Spine", barriga.GetParent() == quadril ? Vector3.Zero : barriga.Position);
            EncostarNaPele(quadril, "Hips", Vector3.Zero);
            EncostarNaPele(cab, "Head", Vector3.Zero, soFrente: true);
        }
    }

    // ------------------------------------------------------------------ 17/11: enfeites da skin encostados na pele
    private Dictionary<(int, int), (float Frente, float Tras)>? _perfil;
    private const float CelulaPerfil = 0.015f;

    private static Transform3D RelativoT(Node3D from, Node3D to)
    {
        var t = Transform3D.Identity;
        Node? n = from;
        while (n is Node3D n3 && n != to) { t = n3.Transform * t; n = n.GetParent(); }
        return t;
    }

    /// <summary>
    /// 17/11: posição de REPOUSO de cada vértice de uma malha com esqueleto, no espaço do Root (a malha crua fica em outra
    /// escala/orientação — quem manda é o osso: pose de repouso × inversa do bind, pesada pelos pesos do vértice).
    /// </summary>
    private Vector3[] VerticesEmRepouso(MeshInstance3D mi, Godot.Collections.Array arr)
    {
        var vs = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var skin = mi.Skin;
        var bonesV = arr[(int)Mesh.ArrayType.Bones];
        var weightsV = arr[(int)Mesh.ArrayType.Weights];
        if (skin is null || bonesV.VariantType == Variant.Type.Nil || weightsV.VariantType == Variant.Type.Nil)
        {
            var t0 = RelativoT(mi, Root);
            for (var i = 0; i < vs.Length; i++) vs[i] = t0 * vs[i];
            return vs;
        }
        var ossos = bonesV.AsInt32Array();
        var pesos = weightsV.AsFloat32Array();
        var por = vs.Length > 0 ? ossos.Length / vs.Length : 4;
        var sk = RelativoT(_skel, Root);
        var mats = new Transform3D[skin.GetBindCount()];
        for (var b = 0; b < mats.Length; b++)
        {
            var bone = skin.GetBindBone(b);
            if (bone < 0) bone = _skel.FindBone(skin.GetBindName(b));
            mats[b] = sk * (bone >= 0 ? _skel.GetBoneGlobalRest(bone) : Transform3D.Identity) * skin.GetBindPose(b);
        }
        var res = new Vector3[vs.Length];
        for (var i = 0; i < vs.Length; i++)
        {
            var acc = Vector3.Zero; var tot = 0f;
            for (var k = 0; k < por; k++)
            {
                var w = pesos[i * por + k];
                if (w <= 0f) continue;
                var bi = ossos[i * por + k];
                if (bi < 0 || bi >= mats.Length) continue;
                acc += w * (mats[bi] * vs[i]); tot += w;
            }
            res[i] = tot > 0 ? acc / tot : vs[i];
        }
        return res;
    }

    /// <summary>Frente (maior z) e costas (menor z) da pele, por célula (x, y) de 1,5 cm, na pose de referência (espaço do Root).</summary>
    private Dictionary<(int, int), (float Frente, float Tras)> Perfil()
    {
        if (_perfil is not null) return _perfil;
        _perfil = new();
        foreach (var n in Root.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            if (n is not MeshInstance3D mi || mi.Mesh is not ArrayMesh am || !mi.IsVisibleInTree()) continue;
            if (mi.Skin is null && mi.Skeleton.IsEmpty) continue;   // só a pele e a roupa que deformam com o esqueleto; enfeites ficam fora
            for (var sfc = 0; sfc < am.GetSurfaceCount(); sfc++)
            {
                var arr = am.SurfaceGetArrays(sfc);
                foreach (var v in VerticesEmRepouso(mi, arr))
                {
                    var k = ((int)Mathf.Floor(v.X / CelulaPerfil), (int)Mathf.Floor(v.Y / CelulaPerfil));
                    _perfil[k] = _perfil.TryGetValue(k, out var c) ? (Mathf.Max(c.Frente, v.Z), Mathf.Min(c.Tras, v.Z)) : (v.Z, v.Z);
                }
            }
        }
        return _perfil;
    }

    /// <summary>Pele mais à frente / mais atrás sob toda a área (x0..x1, y0..y1) da peça — peça comprida não afunda na barriga.</summary>
    private (float Frente, float Tras)? PeleEm(float x0, float x1, float y0, float y1, Dictionary<(int, int), (float Frente, float Tras)>? extra = null)
    {
        var p = extra ?? Perfil();
        float f = float.MinValue, b = float.MaxValue; var achou = false;
        for (var kx = (int)Mathf.Floor(x0 / CelulaPerfil); kx <= (int)Mathf.Floor(x1 / CelulaPerfil); kx++)
            for (var ky = (int)Mathf.Floor(y0 / CelulaPerfil); ky <= (int)Mathf.Floor(y1 / CelulaPerfil); ky++)
                if (p.TryGetValue((kx, ky), out var c)) { f = Mathf.Max(f, c.Frente); b = Mathf.Min(b, c.Tras); achou = true; }
        return achou ? (f, b) : null;
    }

    private void EncostarNaPele(Node3D grupo, string osso, Vector3 desvio, bool soFrente = false)
    {
        if (!GodotObject.IsInstanceValid(grupo) || Bone(osso) < 0) return;
        try
        {
            // o encaixe tem os eixos do Root na pose de referência; a origem é a do osso
            var origem = RelativoT(_skel, Root) * _skel.GetBoneGlobalRest(Bone(osso)).Origin;
            var restGrupo = new Transform3D(Basis.Identity, origem) * new Transform3D(Basis.FromScale(grupo.Scale), desvio);
            Transform3D RestDe(MeshInstance3D mi) => (mi.GetParent() is Node3D pai && pai != grupo ? restGrupo * RelativoT(pai, grupo) : restGrupo) * mi.Transform;
            bool Fino(Aabb cx) => cx.Size.Z <= 0.05f || Mathf.Max(cx.Size.X, Mathf.Max(cx.Size.Y, cx.Size.Z)) <= 0.075f;
            // as peças GRANDES da própria skin (abóbora, balde, panela do peito, focinho) também são "pele": o olho da abóbora
            // encosta na abóbora, não na cabeça escondida lá dentro
            var perfil = new Dictionary<(int, int), (float Frente, float Tras)>(Perfil());
            foreach (var n in grupo.FindChildren("*", nameof(MeshInstance3D), true, false))
            {
                if (n is not MeshInstance3D mg || mg.Mesh is null) continue;
                var tg = RestDe(mg);
                if (Fino(tg * mg.GetAabb())) continue;
                for (var sfc = 0; sfc < mg.Mesh.GetSurfaceCount(); sfc++)
                {
                    if (mg.Mesh.SurfaceGetArrays(sfc)[(int)Mesh.ArrayType.Vertex].AsVector3Array() is not { } vs) continue;
                    foreach (var v0 in vs)
                    {
                        var v = tg * v0;
                        var k = ((int)Mathf.Floor(v.X / CelulaPerfil), (int)Mathf.Floor(v.Y / CelulaPerfil));
                        perfil[k] = perfil.TryGetValue(k, out var c0) ? (Mathf.Max(c0.Frente, v.Z), Mathf.Min(c0.Tras, v.Z)) : (v.Z, v.Z);
                    }
                }
            }
            foreach (var n in grupo.FindChildren("*", nameof(MeshInstance3D), true, false))
            {
                if (n is not MeshInstance3D mi || mi.Mesh is null) continue;
                var pai = mi.GetParent() as Node3D;
                if (pai is null) continue;
                if (pai.Name == "SemEncaixe") continue;   // 18/11 21h: rosto montado ponto a ponto (bigode do sapo) — cada pedaço descia sozinho e virava tracejado
                var restPai = pai == grupo ? restGrupo : restGrupo * RelativoT(pai, grupo);
                var caixa = restPai * mi.Transform * mi.GetAabb();
                var fino = caixa.Size.Z <= 0.05f || Mathf.Max(caixa.Size.X, Mathf.Max(caixa.Size.Y, caixa.Size.Z)) <= 0.075f;
                if (!fino) continue;
                var c = caixa.GetCenter();
                if (PeleEm(caixa.Position.X, caixa.End.X, caixa.Position.Y, caixa.End.Y, perfil) is not { } pele) continue;
                float dz;
                var meio = (pele.Frente + pele.Tras) / 2f;
                if (c.Z >= meio) dz = pele.Frente + 0.004f - caixa.Position.Z;      // frente: o fundo da peça encosta na pele
                else if (!soFrente) dz = pele.Tras - 0.004f - caixa.End.Z;          // costas: a face da peça encosta na pele
                else continue;
                if (Mathf.Abs(dz) > 0.09f || Mathf.Abs(dz) < 0.002f) continue;   // longe demais = é de propósito (colher, aba do fraque)
                var local = restPai.Basis.Inverse() * new Vector3(0, 0, dz);
                mi.Position += local;
            }
        }
        catch (Exception e) { GD.PrintErr($"skin encaixe {osso}: {e.Message}"); }
    }

    /// <summary>
    /// 13/11 (pedido do Barto: "NPC vendedor de balão, dentista, açougueiro, careca de cavanhaque, dançarino"): o
    /// enfeite do ofício no NOSSO boneco (balões na mão, escova gigante e espelho na testa, cutelo e avental sujo,
    /// óculos e cavanhaque com o frasco verde, chapéu de penas).
    /// </summary>
    /// <summary>13/11: enfeite próprio de cabeça para NPCs que eram cópias de outros (cocar, careca, chapéu).</summary>
    public void EnfeiteCabeca(string tipo)
    {
        switch (tipo)
        {
            case "careca":
                if (_hair is not null) _hair.Visible = false;
                break;
            case "cocar":
            {
                var cores = new[] { new Color(0.85f, 0.1f, 0.1f), new Color(1f, 0.75f, 0.1f), new Color(0.1f, 0.55f, 0.3f), new Color(0.15f, 0.35f, 0.85f) };
                var faixa = VisualKit.Mat(new Color(0.55f, 0.3f, 0.12f), 0.8f);
                VisualKit.Part(_headSocket, new TorusMesh { InnerRadius = 0.105f, OuterRadius = 0.125f, Rings = 16, RingSegments = 6 }, faixa, new Vector3(0, 0.1f, 0));
                for (var i = 0; i < 11; i++)
                {
                    var a = Mathf.Lerp(-1.3f, 1.3f, i / 10f);
                    var pena = VisualKit.Part(_headSocket, VisualKit.Box(0.035f, 0.22f, 0.008f), VisualKit.Mat(cores[i % cores.Length], 0.7f),
                        new Vector3(Mathf.Sin(a) * 0.12f, 0.2f, -Mathf.Cos(a) * 0.12f + 0.02f), new Vector3(-15, Mathf.RadToDeg(-a), 0));
                    pena.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                }
                break;
            }
            case "chapeu":
            {
                var feltro = VisualKit.Mat(new Color(0.12f, 0.1f, 0.1f), 0.6f);
                VisualKit.Part(_headSocket, VisualKit.Cylinder(0.17f, 0.17f, 0.015f, 20), feltro, new Vector3(0, 0.15f, 0));
                VisualKit.Part(_headSocket, VisualKit.Cylinder(0.1f, 0.11f, 0.17f, 20), feltro, new Vector3(0, 0.24f, 0));
                VisualKit.Part(_headSocket, VisualKit.Cylinder(0.112f, 0.112f, 0.03f, 20), VisualKit.Mat(new Color(0.6f, 0.1f, 0.1f), 0.5f), new Vector3(0, 0.175f, 0));
                break;
            }
        }
    }

    /// <summary>
    /// 13/11 (conteúdo "Era Uma Vez" e arredores): peças extras por cima do manequim para os personagens e monstros novos —
    /// todos continuam sendo o NOSSO boneco; muda só o que vai por cima (chapéu, trança, rabo, cogumelo...).
    /// </summary>
    /// <summary>Estrela de 5 pontas achatada (de frente para +Z do nó).</summary>
    private static void Estrela(Node3D no, Material m, float r)
    {
        for (var i = 0; i < 5; i++)
        {
            var ponta = VisualKit.Part(no, VisualKit.Cylinder(0.0f, r * 0.42f, r, 3), m, Vector3.Zero, new Vector3(0, 0, i * 72f));
            ponta.Position = new Vector3(Mathf.Sin(Mathf.DegToRad(-i * 72f)) * r * 0.45f, Mathf.Cos(Mathf.DegToRad(i * 72f)) * r * 0.45f, 0.002f);
            ponta.Scale = new Vector3(1f, 1f, 0.25f);
        }
        VisualKit.Part(no, VisualKit.Sphere(r * 0.32f), m, Vector3.Zero, null, new Vector3(1f, 1f, 0.3f));
    }

    /// <summary>Lua crescente achatada (de frente para +Z do nó).</summary>
    private static void Lua(Node3D no, Material m, float r)
    {
        for (var i = 0; i < 7; i++)
        {
            var a = Mathf.DegToRad(-110 + i * 36.6f);
            var gomo = VisualKit.Part(no, VisualKit.Sphere(r * (0.34f - Mathf.Abs(i - 3) * 0.06f)), m, new Vector3(Mathf.Cos(a) * r * 0.7f, Mathf.Sin(a) * r * 0.7f, 0), null, new Vector3(1f, 1f, 0.3f));
            gomo.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    public void Enfeite(string tipo, Color cor)
    {
        StandardMaterial3D M(Color c, float r = 0.7f) => VisualKit.Mat(c, r);
        var h = _headSocket;
        switch (tipo)
        {
            case "chapeu_bobo":
            {
                // chapéu de bobo de três pontas caídas, cada uma com guizo; cor = cor das pontas (a outra metade é a cor escura)
                var a = M(cor); var b = M(cor.Darkened(0.55f)); var ouro = VisualKit.Mat(new Color(0.95f, 0.78f, 0.28f), 0.3f, 0.9f);
                VisualKit.Part(h, VisualKit.Cylinder(0.125f, 0.13f, 0.08f, 16), b, new Vector3(0, 0.13f, 0));
                for (var i = 0; i < 3; i++)
                {
                    var ang = (i - 1) * 62f;
                    var ponta = VisualKit.Joint(h, new Vector3(0, 0.17f, 0));
                    ponta.RotationDegrees = new Vector3(0, ang, 0);
                    VisualKit.Part(ponta, VisualKit.Cylinder(0.02f, 0.07f, 0.32f, 8), i % 2 == 0 ? a : b, new Vector3(0, 0.1f, -0.09f), new Vector3(-58, 0, 0));
                    VisualKit.Part(ponta, VisualKit.Sphere(0.03f), ouro, new Vector3(0, 0.15f, -0.25f));
                }
                break;
            }
            case "coroa":
            {
                var ouro = VisualKit.Mat(cor, 0.25f, 0.9f);
                VisualKit.Part(h, VisualKit.Cylinder(0.115f, 0.115f, 0.05f, 16), ouro, new Vector3(0, 0.19f, 0));
                for (var i = 0; i < 6; i++)
                {
                    var a = i * Mathf.Tau / 6f;
                    VisualKit.Part(h, VisualKit.Cylinder(0f, 0.025f, 0.07f, 4), ouro, new Vector3(Mathf.Sin(a) * 0.11f, 0.245f, Mathf.Cos(a) * 0.11f));
                }
                break;
            }
            case "tranca_longa":
            {
                // trança grossa que desce pelas costas até a dobra do joelho (a mãe "daquela família")
                var cabelo = M(cor, 0.85f);
                VisualKit.Part(h, VisualKit.Sphere(0.125f), cabelo, new Vector3(0, 0.05f, -0.02f), null, new Vector3(1.05f, 1.0f, 1.1f));
                for (var i = 0; i < 9; i++)
                    VisualKit.Part(h, VisualKit.Sphere(0.06f - i * 0.002f), cabelo, new Vector3(i % 2 == 0 ? 0.012f : -0.012f, -0.05f - i * 0.11f, -0.12f - i * 0.012f), null, new Vector3(1f, 1.3f, 1f));
                VisualKit.Part(h, VisualKit.Cylinder(0.03f, 0.03f, 0.04f, 8), M(new Color(0.85f, 0.2f, 0.2f)), new Vector3(0, -1.04f, -0.23f));
                break;
            }
            case "marias_chiquinhas":
            {
                var cabelo = M(cor, 0.85f);
                VisualKit.Part(h, VisualKit.Sphere(0.12f), cabelo, new Vector3(0, 0.055f, -0.015f), null, new Vector3(1.05f, 0.95f, 1.08f));
                foreach (var sd in new[] { -1f, 1f })
                {
                    VisualKit.Part(h, VisualKit.Capsule(0.04f, 0.2f), cabelo, new Vector3(sd * 0.15f, -0.02f, -0.02f), new Vector3(0, 0, sd * 25));
                    VisualKit.Part(h, VisualKit.Sphere(0.025f), M(new Color(0.95f, 0.4f, 0.6f)), new Vector3(sd * 0.12f, 0.06f, -0.02f));
                }
                break;
            }
            case "cabelo_bagunca":
            {
                // menino: cabelo desgrenhado (tufos tortos pra todo lado, sem pontas de estrela) e bonezinho virado
                var cabelo = M(cor, 0.9f);
                VisualKit.Part(h, VisualKit.Sphere(0.122f), cabelo, new Vector3(0, 0.06f, -0.01f), null, new Vector3(1.06f, 0.9f, 1.08f));
                var rnd = new System.Random(5);
                for (var i = 0; i < 9; i++)
                    VisualKit.Part(h, VisualKit.Sphere(0.045f), cabelo, new Vector3((float)rnd.NextDouble() * 0.2f - 0.1f, 0.12f + (float)rnd.NextDouble() * 0.04f, (float)rnd.NextDouble() * 0.18f - 0.1f));
                var bone = M(new Color(0.2f, 0.45f, 0.25f));
                VisualKit.Part(h, VisualKit.Sphere(0.118f), bone, new Vector3(0, 0.1f, 0), null, new Vector3(1f, 0.55f, 1f));
                VisualKit.Part(h, VisualKit.Box(0.14f, 0.012f, 0.1f), bone, new Vector3(0, 0.12f, -0.13f), new Vector3(10, 0, 0));
                break;
            }
            case "touca_bebe":
            {
                var pano = M(cor, 0.9f);
                VisualKit.Part(h, VisualKit.Sphere(0.135f), pano, new Vector3(0, 0.05f, -0.02f), null, new Vector3(1.05f, 1f, 1.1f));
                VisualKit.Part(h, new TorusMesh { InnerRadius = 0.11f, OuterRadius = 0.135f, Rings = 18, RingSegments = 6 }, M(cor.Lightened(0.4f)), new Vector3(0, 0.03f, 0.06f), new Vector3(80, 0, 0));
                break;
            }
            case "topete":
                if (_hair is not null) _hair.Visible = false;
                VisualKit.Part(h, VisualKit.Cylinder(0.01f, 0.03f, 0.1f, 6), M(cor, 0.9f), new Vector3(0, 0.2f, 0.03f), new Vector3(-20, 0, 15));
                break;
            case "barriga":
            {
                // barrigão de dar dó (camiseta regata manchada por cima)
                var regata = M(cor, 0.95f);
                var peito = Socket("Spine1");
                // o encaixe já vem com a largura do corpo (girth): a bola é pequena e fica colada na frente da barriga
                VisualKit.Part(peito, VisualKit.Sphere(0.15f), regata, new Vector3(0, -0.06f, 0.06f), null, new Vector3(0.9f, 1f, 1.05f));
                VisualKit.Part(peito, VisualKit.Sphere(0.035f), M(cor.Darkened(0.35f)), new Vector3(0.05f, -0.02f, 0.2f), null, new Vector3(1.4f, 0.8f, 0.3f));
                break;
            }
            // 13/11 (Merlin): chapéu de mago alto e pontudo, roxo, a ponta caindo um pouco, com estrelas e luas amarelas
            case "chapeu_merlin":
            {
                var roxo = M(cor, 0.85f); var amarelo = VisualKit.Glow(new Color(1f, 0.85f, 0.25f), 0.45f);
                VisualKit.Part(h, VisualKit.Cylinder(0.24f, 0.25f, 0.018f, 24), roxo, new Vector3(0, 0.145f, 0));   // aba larga
                VisualKit.Part(h, VisualKit.Cylinder(0.118f, 0.13f, 0.03f, 20), M(new Color(0.95f, 0.78f, 0.28f), 0.4f), new Vector3(0, 0.165f, 0));   // fita dourada
                var cone = VisualKit.Joint(h, new Vector3(0, 0.16f, 0));
                VisualKit.Part(cone, VisualKit.Cylinder(0.075f, 0.125f, 0.26f, 20), roxo, new Vector3(0, 0.13f, 0));
                var ponta = VisualKit.Joint(cone, new Vector3(0, 0.26f, 0));
                ponta.RotationDegrees = new Vector3(-24, 0, 8);
                VisualKit.Part(ponta, VisualKit.Cylinder(0.004f, 0.075f, 0.24f, 16), roxo, new Vector3(0, 0.11f, 0));
                for (var i = 0; i < 7; i++)
                {
                    var a = i * 0.9f + 0.3f; var y = 0.05f + (i % 3) * 0.08f; var rr = 0.122f - y * 0.18f;
                    var no = VisualKit.Joint(cone, new Vector3(Mathf.Sin(a) * rr, y, Mathf.Cos(a) * rr));
                    no.RotationDegrees = new Vector3(0, Mathf.RadToDeg(a), 0);
                    if (i % 2 == 0) Estrela(no, amarelo, 0.022f); else Lua(no, amarelo, 0.02f);
                }
                break;
            }
            case "oculos":
            {
                // óculos redondos de aro dourado na ponta do nariz
                var aro = VisualKit.Mat(new Color(0.9f, 0.75f, 0.3f), 0.3f, 0.9f);
                var lente = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.92f, 1f, 0.25f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, MetallicSpecular = 1f };
                foreach (var lado in new[] { -1f, 1f })
                {
                    VisualKit.Part(h, VisualKit.Ring(0.03f, 0.006f), aro, new Vector3(lado * 0.036f, 0.065f, 0.105f), new Vector3(90, 0, 0));
                    VisualKit.Part(h, VisualKit.Cylinder(0.028f, 0.028f, 0.003f, 16), lente, new Vector3(lado * 0.036f, 0.065f, 0.106f), new Vector3(90, 0, 0));
                    VisualKit.Part(h, VisualKit.Box(0.004f, 0.004f, 0.09f), aro, new Vector3(lado * 0.068f, 0.068f, 0.06f));   // haste
                }
                VisualKit.Part(h, VisualKit.Box(0.016f, 0.004f, 0.004f), aro, new Vector3(0, 0.07f, 0.108f));   // ponte
                break;
            }
            case "barba_longa":
            {
                // barba branca comprida, em ondas, até a barriga (por cima da barba do mago)
                var branca = M(cor, 0.95f);
                for (var i = 0; i < 4; i++)
                {
                    var seg = VisualKit.Part(h, VisualKit.Capsule(0.055f - i * 0.009f, 0.14f), branca, new Vector3(Mathf.Sin(i * 1.7f) * 0.01f, -0.1f - i * 0.1f, 0.085f + i * 0.018f), new Vector3(10 + i * 4, 0, 0));
                    seg.Scale = new Vector3(1.25f, 1f, 0.6f);
                }
                break;
            }
            case "manto_estrelado":
            {
                // estrelas e luas amarelas costuradas no manto (peito, costas, mangas e barra)
                var amarelo = VisualKit.Glow(cor, 0.35f);
                var pontos = new (string Osso, Vector3 P, bool Estrela)[]
                {
                    ("Spine2", new(0.07f, 0.06f, 0.13f), true), ("Spine2", new(-0.08f, -0.02f, 0.13f), false), ("Spine1", new(0.02f, 0.0f, 0.14f), true),
                    ("Spine2", new(0.05f, 0.05f, -0.13f), false), ("Spine1", new(-0.06f, 0.0f, -0.14f), true), ("Spine", new(0.08f, 0.0f, 0.13f), false),
                    ("LeftArm", new(0.0f, 0.12f, 0.05f), true), ("RightArm", new(0.0f, 0.12f, 0.05f), false), ("LeftForeArm", new(0.0f, 0.1f, 0.05f), true),
                    ("RightForeArm", new(0.0f, 0.1f, 0.05f), true), ("LeftUpLeg", new(0.03f, 0.25f, 0.1f), true), ("RightUpLeg", new(-0.03f, 0.2f, 0.1f), false),
                    ("LeftLeg", new(0.02f, 0.15f, 0.09f), false), ("RightLeg", new(0.0f, 0.2f, 0.09f), true),
                };
                foreach (var (osso, pos, est) in pontos)
                {
                    Node3D enc;
                    try { enc = Socket(osso); } catch { continue; }
                    var no = VisualKit.Joint(enc, pos);
                    if (est) Estrela(no, amarelo, 0.03f); else Lua(no, amarelo, 0.028f);
                }
                break;
            }
            case "cogumelo_chapeu":
            {
                // chapéu de cogumelo enorme, roxo com pintas claras (Dona Fungilda) — brilha de leve
                var cap = VisualKit.Glow(cor, 0.35f);
                VisualKit.Part(h, VisualKit.Sphere(0.3f), cap, new Vector3(0, 0.2f, 0), null, new Vector3(1.1f, 0.5f, 1.1f));
                VisualKit.Part(h, VisualKit.Cylinder(0.3f, 0.31f, 0.02f, 20), M(cor.Darkened(0.5f)), new Vector3(0, 0.15f, 0));
                var pinta = M(new Color(0.95f, 0.92f, 0.85f));
                for (var i = 0; i < 7; i++)
                {
                    var a = i * 2.4f;
                    VisualKit.Part(h, VisualKit.Sphere(0.035f), pinta, new Vector3(Mathf.Sin(a) * 0.2f, 0.3f - (i % 3) * 0.02f, Mathf.Cos(a) * 0.2f), null, new Vector3(1f, 0.4f, 1f));
                }
                break;
            }
            case "cogumelo":
            {
                // cogumelinho roxo brilhando na cabeça (o feitiço dos gnomos)
                VisualKit.Part(h, VisualKit.Cylinder(0.02f, 0.025f, 0.08f, 8), M(new Color(0.9f, 0.88f, 0.8f)), new Vector3(0.05f, 0.25f, 0));
                VisualKit.Part(h, VisualKit.Sphere(0.07f), VisualKit.Glow(cor, 1.6f), new Vector3(0.05f, 0.3f, 0), null, new Vector3(1.2f, 0.55f, 1.2f));
                h.AddChild(new OmniLight3D { LightColor = cor, LightEnergy = 0.6f, OmniRange = 2.2f, Position = new Vector3(0.05f, 0.32f, 0), ShadowEnabled = false });
                break;
            }
            case "rabo_macaco":
            {
                var pelo = M(cor, 0.95f);
                var p = VisualKit.Joint(_hips, new Vector3(0, -0.02f, -0.12f));
                for (var i = 0; i < 7; i++)
                    VisualKit.Part(p, VisualKit.Sphere(0.035f), pelo, new Vector3(0, 0.06f * i - 0.02f * i * i * 0.1f, -0.06f * i + 0.005f * i * i), null, new Vector3(1f, 1.4f, 1f));
                VisualKit.Part(p, VisualKit.Sphere(0.04f), pelo, new Vector3(0, 0.42f, -0.18f));
                break;
            }
            case "focinho_macaco":
            {
                var pele = M(cor, 0.85f);
                VisualKit.Part(h, VisualKit.Sphere(0.08f), pele, new Vector3(0, -0.02f, 0.11f), null, new Vector3(1.2f, 0.8f, 0.8f));
                foreach (var sd in new[] { -1f, 1f })
                    VisualKit.Part(h, VisualKit.Cylinder(0.05f, 0.05f, 0.015f, 12), pele, new Vector3(sd * 0.13f, 0.04f, 0), new Vector3(0, 0, 90));
                break;
            }
            case "coroa_espinhos":
            {
                // a Dama Urtiga: aro de galho com espinhos e flores roxas, folhas caindo como cabelo (visual próprio)
                var galho = M(new Color(0.3f, 0.2f, 0.12f), 0.95f);
                VisualKit.Part(h, new TorusMesh { InnerRadius = 0.11f, OuterRadius = 0.135f, Rings = 18, RingSegments = 6 }, galho, new Vector3(0, 0.15f, 0));
                for (var i = 0; i < 10; i++)
                {
                    var a = i * Mathf.Tau / 10f;
                    VisualKit.Part(h, VisualKit.Cylinder(0f, 0.012f, 0.08f, 4), galho, new Vector3(Mathf.Sin(a) * 0.125f, 0.19f, Mathf.Cos(a) * 0.125f), new Vector3(Mathf.Cos(a) * 25, 0, -Mathf.Sin(a) * 25));
                    if (i % 3 == 0) VisualKit.Part(h, VisualKit.Sphere(0.03f), VisualKit.Glow(new Color(0.65f, 0.3f, 0.85f), 0.8f), new Vector3(Mathf.Sin(a) * 0.13f, 0.16f, Mathf.Cos(a) * 0.13f));
                }
                var folha = M(cor, 0.8f);
                for (var i = 0; i < 12; i++)
                {
                    var a = Mathf.Lerp(1.9f, 4.4f, i / 11f);
                    VisualKit.Part(h, VisualKit.Box(0.06f, 0.22f + (i % 3) * 0.06f, 0.01f), folha, new Vector3(Mathf.Sin(a) * 0.13f, -0.02f - (i % 3) * 0.03f, Mathf.Cos(a) * 0.13f), new Vector3(0, Mathf.RadToDeg(a), 0));
                }
                break;
            }
            case "algas":
            {
                // tritão: barba e cabelo de alga e uma crista de barbatana na cabeça; lodo negro escorrendo
                var alga = M(cor, 0.6f);
                for (var i = 0; i < 9; i++)
                {
                    var a = Mathf.Lerp(-1.2f, 1.2f, i / 8f);
                    VisualKit.Part(h, VisualKit.Box(0.03f, 0.22f, 0.01f), alga, new Vector3(Mathf.Sin(a) * 0.09f, -0.15f, 0.09f + Mathf.Cos(a) * 0.02f), new Vector3(-10, Mathf.RadToDeg(a), 0));
                }
                VisualKit.Part(h, VisualKit.Box(0.015f, 0.14f, 0.26f), M(new Color(0.2f, 0.55f, 0.6f), 0.4f), new Vector3(0, 0.2f, -0.03f));
                var lodo = new StandardMaterial3D { AlbedoColor = new Color(0.03f, 0.04f, 0.02f), Roughness = 0.1f, Metallic = 0.3f, EmissionEnabled = true, Emission = new Color(0.1f, 0.35f, 0.05f), EmissionEnergyMultiplier = 0.6f };
                var peito = Socket("Spine2");
                for (var i = 0; i < 6; i++)
                    VisualKit.Part(peito, VisualKit.Sphere(0.05f), lodo, new Vector3(-0.15f + i * 0.06f, -0.05f - (i % 3) * 0.08f, 0.14f), null, new Vector3(1f, 1.8f, 0.6f));
                break;
            }
            case "rabeca":
            {
                var madeira = M(new Color(0.5f, 0.25f, 0.1f), 0.5f);
                VisualKit.Part(_handL, VisualKit.Sphere(0.09f), madeira, new Vector3(0, 0.05f, 0.05f), null, new Vector3(0.75f, 1.25f, 0.35f));
                VisualKit.Part(_handL, VisualKit.Box(0.025f, 0.2f, 0.02f), M(new Color(0.12f, 0.08f, 0.06f)), new Vector3(0, 0.23f, 0.05f));
                VisualKit.Part(_handR, VisualKit.Cylinder(0.005f, 0.005f, 0.4f, 4), M(new Color(0.85f, 0.8f, 0.7f)), new Vector3(0, 0.1f, 0.05f), new Vector3(0, 0, 70));
                break;
            }
            case "estilingue":
            {
                var madeira = M(new Color(0.45f, 0.3f, 0.15f), 0.8f);
                VisualKit.Part(_handR, VisualKit.Cylinder(0.012f, 0.014f, 0.12f, 6), madeira, new Vector3(0, 0.06f, 0.03f));
                foreach (var sd in new[] { -1f, 1f })
                    VisualKit.Part(_handR, VisualKit.Cylinder(0.009f, 0.011f, 0.08f, 6), madeira, new Vector3(sd * 0.025f, 0.15f, 0.03f), new Vector3(0, 0, -sd * 25));
                break;
            }
            case "chocalho":
                VisualKit.Part(_handR, VisualKit.Cylinder(0.01f, 0.01f, 0.1f, 6), M(new Color(0.95f, 0.85f, 0.4f)), new Vector3(0, 0.05f, 0.03f));
                VisualKit.Part(_handR, VisualKit.Sphere(0.045f), M(new Color(0.4f, 0.75f, 0.95f)), new Vector3(0, 0.12f, 0.03f));
                break;
            case "tridente":
            {
                var bronze = VisualKit.Mat(cor, 0.35f, 0.85f);
                var t = VisualKit.Joint(_handR, new Vector3(0, 0, 0.03f));
                VisualKit.Part(t, VisualKit.Cylinder(0.022f, 0.022f, 1.9f, 8), bronze, new Vector3(0, 0.3f, 0));
                VisualKit.Part(t, VisualKit.Box(0.28f, 0.03f, 0.03f), bronze, new Vector3(0, 1.25f, 0));
                foreach (var x in new[] { -0.13f, 0f, 0.13f })
                    VisualKit.Part(t, VisualKit.Cylinder(0f, 0.025f, 0.26f, 6), bronze, new Vector3(x, 1.4f, 0));
                break;
            }
            case "facas":
                foreach (var hand in new[] { _handL, _handR })
                    VisualKit.Part(hand, VisualKit.Box(0.02f, 0.24f, 0.005f), VisualKit.Mat(new Color(0.75f, 0.76f, 0.8f), 0.2f, 0.9f), new Vector3(0, 0.14f, 0.03f));
                break;
            case "cetro_guizo":
            {
                var cabo = VisualKit.Joint(_handR, new Vector3(0, 0, 0.03f));
                VisualKit.Part(cabo, VisualKit.Cylinder(0.02f, 0.02f, 0.6f, 8), M(new Color(0.3f, 0.15f, 0.35f)), new Vector3(0, 0.15f, 0));
                VisualKit.Part(cabo, VisualKit.Sphere(0.08f), M(cor), new Vector3(0, 0.48f, 0));
                for (var i = 0; i < 3; i++)
                    VisualKit.Part(cabo, VisualKit.Sphere(0.03f), VisualKit.Mat(new Color(0.95f, 0.78f, 0.28f), 0.3f, 0.9f), new Vector3((i - 1) * 0.07f, 0.57f, 0));
                break;
            }
        }
    }

    private Node3D? _baloes;
    private float _malabarAte = -1f;
    private readonly List<MeshInstance3D> _bolinhas = new();

    /// <summary>Três bolinhas em cascata entre as mãos (sobe por um lado, cruza no alto, desce pelo outro).</summary>
    private void Malabares()
    {
        var ativo = _time < _malabarAte && _handL.IsInsideTree() && _handR.IsInsideTree() && Root.IsInsideTree();
        if (!ativo) { foreach (var b in _bolinhas) b.Visible = false; return; }
        if (_bolinhas.Count == 0)
        {
            var cores = new[] { new Color(0.95f, 0.2f, 0.15f), new Color(1f, 0.85f, 0.15f), new Color(0.25f, 0.55f, 1f) };
            foreach (var c in cores)
            {
                var b = VisualKit.Part(Root, VisualKit.Sphere(0.055f), VisualKit.Mat(c, 0.3f), Vector3.Zero);
                b.TopLevel = true;
                b.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                _bolinhas.Add(b);
            }
        }
        var l = _handL.GlobalPosition; var r = _handR.GlobalPosition;
        var alto = 0.55f * Root.GlobalBasis.Scale.Y;
        for (var i = 0; i < _bolinhas.Count; i++)
        {
            var fase = Mathf.PosMod(_time * 1.1f + i / 3f, 1f);
            var (de, para, h) = fase < 0.5f ? (l, r, alto) : (r, l, alto * 0.35f);
            var k = (fase < 0.5f ? fase : fase - 0.5f) * 2f;
            var p = de.Lerp(para, k) + new Vector3(0, 4f * h * k * (1f - k) + 0.06f, 0);
            _bolinhas[i].Visible = true;
            _bolinhas[i].GlobalPosition = p;
        }
    }

    public void SetOficio(string oficio)
    {
        StandardMaterial3D M(Color c, float r = 0.6f) => VisualKit.Mat(c, r);
        var branco = M(new Color(0.96f, 0.96f, 0.94f), 0.5f);
        switch (oficio)
        {
            case "balao":
            {
                var cores = new[] { new Color(0.95f, 0.15f, 0.2f), new Color(0.2f, 0.55f, 1f), new Color(1f, 0.85f, 0.1f), new Color(0.3f, 0.85f, 0.35f), new Color(0.9f, 0.35f, 0.9f) };
                var fio = M(new Color(0.9f, 0.9f, 0.9f));
                // 13/11 ("os balões estão duros, parecendo uma arma; deixa sempre para cima, como na vida real, segurando
                // tipo um buquê"): o maço fica na mão ESQUERDA (o braço segura erguido o tempo todo — ModifyPose) e o nó dos
                // balões é endireitado a cada quadro (Update): os fios sobem da mão, abertos em leque, e balançam de leve
                _baloes = new Node3D { Name = "Baloes" };
                _handL.AddChild(_baloes);
                for (var i = 0; i < 5; i++)
                {
                    var a = i * 1.25f;
                    var topo = new Vector3(Mathf.Cos(a) * 0.22f, 0.95f + (i % 2) * 0.2f, Mathf.Sin(a) * 0.18f);
                    var meio = topo * 0.5f;
                    var f = VisualKit.Part(_baloes, VisualKit.Cylinder(0.003f, 0.003f, topo.Length(), 3), fio, meio);
                    f.LookAtFromPosition(meio, topo, Vector3.Forward); f.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2);
                    var bal = VisualKit.Part(_baloes, VisualKit.Sphere(0.13f), VisualKit.Mat(cores[i], 0.15f), topo + new Vector3(0, 0.12f, 0), null, new Vector3(0.9f, 1.15f, 0.9f));
                    bal.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                }
                VisualKit.Part(_headSocket, VisualKit.Cylinder(0.11f, 0.12f, 0.1f, 16), M(new Color(0.2f, 0.45f, 0.9f)), new Vector3(0, 0.17f, -0.01f));   // chapéu de feira
                break;
            }
            case "dentista":
            {
                VisualKit.Part(_headSocket, VisualKit.Cylinder(0.045f, 0.045f, 0.008f, 16), M(new Color(0.85f, 0.85f, 0.9f), 0.05f), new Vector3(0, 0.16f, 0.1f), new Vector3(70, 0, 0));   // espelho na testa
                VisualKit.Part(_headSocket, new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.112f }, M(new Color(0.2f, 0.2f, 0.2f)), new Vector3(0, 0.14f, 0), new Vector3(-15, 0, 0));
                var cabo = VisualKit.Part(_handR, VisualKit.Box(0.04f, 0.04f, 0.7f), M(new Color(0.3f, 0.75f, 1f), 0.3f), new Vector3(0, 0, 0.3f));
                VisualKit.Part(cabo, VisualKit.Box(0.07f, 0.12f, 0.12f), branco, new Vector3(0, 0.07f, 0.32f));   // escova gigante
                VisualKit.Part(Socket("Spine2"), VisualKit.Box(0.3f, 0.4f, 0.24f), branco, new Vector3(0, -0.02f, 0.0f));   // jaleco
                break;
            }
            case "acougueiro":
            {
                var lam = VisualKit.Mat(new Color(0.75f, 0.76f, 0.8f), 0.25f, 0.9f);
                var cut = VisualKit.Part(_handR, VisualKit.Box(0.03f, 0.03f, 0.14f), M(new Color(0.25f, 0.15f, 0.08f)), new Vector3(0, 0, 0.08f));
                VisualKit.Part(cut, VisualKit.Box(0.012f, 0.13f, 0.2f), lam, new Vector3(0, 0.05f, 0.16f));   // cutelo
                var peito = Socket("Spine2");
                VisualKit.Part(peito, VisualKit.Box(0.3f, 0.44f, 0.02f), branco, new Vector3(0, -0.05f, 0.14f));   // avental
                foreach (var (x, y, r) in new[] { (0.05f, 0.05f, 0.03f), (-0.07f, -0.1f, 0.04f), (0.02f, -0.18f, 0.025f) })
                    VisualKit.Part(peito, VisualKit.Sphere(r), M(new Color(0.55f, 0.04f, 0.04f), 0.2f), new Vector3(x, y, 0.15f), null, new Vector3(1f, 1f, 0.15f));
                VisualKit.Part(_headSocket, VisualKit.Cylinder(0.1f, 0.11f, 0.08f, 16), branco, new Vector3(0, 0.16f, -0.01f));   // gorro
                break;
            }
            case "quimico":
            {
                if (_hair is not null) _hair.Visible = false;   // careca
                var preto = M(new Color(0.05f, 0.05f, 0.05f), 0.3f);
                foreach (var sd in new[] { -1f, 1f })
                    VisualKit.Part(_headSocket, new TorusMesh { InnerRadius = 0.02f, OuterRadius = 0.026f, Rings = 12, RingSegments = 6 }, preto, new Vector3(sd * 0.04f, 0.04f, 0.105f), new Vector3(90, 0, 0));
                VisualKit.Part(_headSocket, VisualKit.Box(0.03f, 0.006f, 0.006f), preto, new Vector3(0, 0.04f, 0.11f));
                VisualKit.Part(_headSocket, VisualKit.Box(0.05f, 0.05f, 0.03f), M(new Color(0.25f, 0.2f, 0.15f), 0.9f), new Vector3(0, -0.08f, 0.09f));   // cavanhaque
                VisualKit.Part(_headSocket, VisualKit.Box(0.06f, 0.012f, 0.02f), M(new Color(0.25f, 0.2f, 0.15f), 0.9f), new Vector3(0, -0.035f, 0.11f));  // bigodinho
                var frasco = VisualKit.Part(_handR, VisualKit.Sphere(0.05f), VisualKit.Glow(new Color(0.3f, 1f, 0.5f), 1.5f), new Vector3(0, 0.02f, 0.08f));
                VisualKit.Part(frasco, VisualKit.Cylinder(0.015f, 0.015f, 0.06f, 8), M(new Color(0.8f, 0.9f, 0.85f), 0.1f), new Vector3(0, 0.06f, 0));
                break;
            }
            case "dancarino":
            {
                var ouro = VisualKit.Mat(new Color(1f, 0.8f, 0.3f), 0.2f, 0.9f);
                VisualKit.Part(_headSocket, VisualKit.Cylinder(0.1f, 0.12f, 0.05f, 16), M(new Color(0.6f, 0.1f, 0.5f)), new Vector3(0, 0.16f, -0.01f));
                for (var i = 0; i < 3; i++)
                    VisualKit.Part(_headSocket, VisualKit.Box(0.03f, 0.25f, 0.01f), M(new Color(0.95f, 0.3f + i * 0.2f, 0.6f)), new Vector3(-0.04f + i * 0.04f, 0.3f, -0.05f), new Vector3(-15, 0, -20 + i * 20));   // penas
                var peito = Socket("Spine2");
                for (var i = 0; i < 9; i++)   // lantejoulas
                    VisualKit.Part(peito, VisualKit.Sphere(0.015f), ouro, new Vector3(-0.1f + (i % 3) * 0.1f, 0.12f - (i / 3) * 0.1f, 0.15f));
                break;
            }
        }
    }

    public void SetLegendary(bool on)
    {
        if (on && _legendLight is null)
        {
            _legendLight = new OmniLight3D { LightColor = new Color(1f, 0.72f, 0.32f), LightEnergy = 0.9f, OmniRange = 2.2f };
            _handR.AddChild(_legendLight);
        }
        if (_legendLight is not null) _legendLight.Visible = on;
    }

    // ================================================================ brilho do refino (11/11, estilo MU)

    private int _glowPlus = -1;
    private bool _glowExc;
    private OmniLight3D? _plusLight;
    private CpuParticles3D? _plusSparks;

    /// <summary>
    /// Arma +7 em diante brilha (azul +7/+8, dourado +9 a +12, brasa +13 a +15); Excelente puxa para o verde. Do +9 sobem
    /// faíscas da arma. Uma camada aditiva por cima da arma pulsa no Update.
    /// </summary>
    public void SetWeaponGlow(int plus, bool excellent)
    {
        if (plus == _glowPlus && excellent == _glowExc && (_weapon is null || _weapon.HasMeta("glow") == (plus >= 7 || excellent))) return;
        _glowPlus = plus; _glowExc = excellent;
        _plusLight?.QueueFree(); _plusLight = null;
        _plusSparks?.QueueFree(); _plusSparks = null;
        if (_weapon is null || (plus < 7 && !excellent)) return;
        var c = GlowColor(plus, excellent);
        var overlay = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new Color(c.R, c.G, c.B, 0.25f + 0.04f * Math.Max(0, plus - 7)),
        };
        foreach (var mi in _weapon.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()) mi.MaterialOverlay = overlay;
        if (_weapon is MeshInstance3D self) self.MaterialOverlay = overlay;
        _weapon.SetMeta("glow", true);
        _plusLight = new OmniLight3D { LightColor = c, LightEnergy = 0.6f + 0.12f * Math.Max(0, plus - 6), OmniRange = 2.4f };
        _handR.AddChild(_plusLight);
        if (plus >= 9 || excellent)
        {
            _plusSparks = new CpuParticles3D
            {
                Amount = 10 + 3 * Math.Max(0, plus - 9), Lifetime = 0.9f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.25f,
                Direction = Vector3.Up, Spread = 30f, Gravity = new Vector3(0, 0.8f, 0), InitialVelocityMin = 0.2f, InitialVelocityMax = 0.6f,
                ScaleAmountMin = 0.5f, ScaleAmountMax = 1.1f, Position = new Vector3(0, -0.35f, 0),
                Mesh = new SphereMesh { Radius = 0.018f, Height = 0.036f, RadialSegments = 4, Rings = 2, Material = VisualKit.Glow(c, 5f) },
            };
            _handR.AddChild(_plusSparks);
        }
    }

    private static Color GlowColor(int plus, bool excellent)
    {
        var baseC = plus >= 13 ? new Color(1f, 0.42f, 0.15f) : plus >= 9 ? new Color(1f, 0.82f, 0.3f) : new Color(0.4f, 0.75f, 1f);
        return excellent ? (plus >= 7 ? baseC.Lerp(new Color(0.25f, 1f, 0.55f), 0.5f) : new Color(0.25f, 1f, 0.55f)) : baseC;
    }

    // ================================================================ classe evoluída (troca de classe estilo MU)

    private Node3D? _animal;
    private bool _classe3;
    private OmniLight3D? _furia;

    /// <summary>
    /// 12/11 (pedido do dono: "classe 3 no 55/60 — você vira um leão maior, mais forte e mais furioso, tipo um chefe;
    /// a arte própria de cada classe vem do dono"): cabeça de leão para qualquer classe, corpo 20% maior e um brilho de fúria no peito.
    /// </summary>
    public void SetClasse3(bool on)
    {
        if (on == _classe3) return;
        var wanted = _evolvedWanted;
        _classe3 = false;
        if (_animal is not null) SetEvolved(false);
        _evolvedWanted = wanted;
        _classe3 = on;
        SetEvolved(on || wanted);
        if (on && _furia is null)
        {
            _furia = new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.15f), LightEnergy = 0.9f, OmniRange = 3.2f, Position = new Vector3(0, 1.5f, 0.35f), ShadowEnabled = false };
            Root.AddChild(_furia);
        }
        if (_furia is not null) _furia.Visible = on;
        // 14/11 tarde (dono, com a arte do leão-guerreiro: "15% maior, mais forte, tipo um leão mais forte"): cauda com
        // tufo, faixas de couro nos punhos e garras nas mãos
        if (on && _leaoExtras is null && _classId == "guerreiro")
        {
            _leaoExtras = new List<Node3D>();
            var pelo = VisualKit.Hide(new Color(0.78f, 0.6f, 0.3f), "pele", 0.85f, 6f);
            var juba = VisualKit.Hide(new Color(0.42f, 0.22f, 0.09f), "pele", 0.95f, 5f);
            var couro = VisualKit.Hide(new Color(0.3f, 0.2f, 0.13f), "couro", 0.8f, 4f);
            var garra = VisualKit.Mat(new Color(0.88f, 0.84f, 0.74f), 0.35f);
            var cauda = new Node3D { Name = "CaudaLeao", Position = new Vector3(0, -0.02f, -0.13f) };
            Socket("Hips").AddChild(cauda); _leaoExtras.Add(cauda);
            Node3D pai = cauda;
            for (var i = 0; i < 6; i++)   // curva que desce e sobe (como na arte)
            {
                // desce rente às pernas e sobe na ponta (como na arte)
                var giro = i switch { 0 => -62f, 1 => -12f, 2 => 14f, _ => 24f };
                var seg = new Node3D { Position = i == 0 ? Vector3.Zero : new Vector3(0, 0, -0.1f), RotationDegrees = new Vector3(giro, 0, 0) };
                pai.AddChild(seg);
                VisualKit.Part(seg, VisualKit.Capsule(0.024f - i * 0.002f, 0.13f), pelo, new Vector3(0, 0, -0.05f), new Vector3(90, 0, 0));
                pai = seg;
            }
            VisualKit.Part(pai, VisualKit.Sphere(0.045f), juba, new Vector3(0, 0, -0.12f), null, new Vector3(0.8f, 0.8f, 1.5f));   // tufo
            foreach (var (osso, lado) in new[] { ("LeftForeArm", -1f), ("RightForeArm", 1f) })
            {
                if (Bone(osso) < 0) continue;
                var faixa = new Node3D { Name = "FaixaLeao" };
                Socket(osso).AddChild(faixa); _leaoExtras.Add(faixa);
                for (var k = 0; k < 3; k++)
                    VisualKit.Part(faixa, VisualKit.Cylinder(0.048f, 0.048f, 0.03f, 12), couro, new Vector3(lado * (0.12f + k * 0.035f), 0, 0), new Vector3(0, 0, 90));
            }
            foreach (var mao in new[] { "LeftHand", "RightHand" })
            {
                if (Bone(mao) < 0) continue;
                var gs = new Node3D { Name = "GarrasLeao" };
                Socket(mao).AddChild(gs); _leaoExtras.Add(gs);
                var sd = mao.StartsWith("Left") ? -1f : 1f;
                for (var k = 0; k < 4; k++)
                    VisualKit.Part(gs, VisualKit.Cylinder(0.001f, 0.009f, 0.05f, 6), garra, new Vector3(sd * 0.1f, -0.01f, -0.03f + k * 0.02f), new Vector3(0, 0, sd * -90));
            }
        }
        if (_leaoExtras is not null) foreach (var n in _leaoExtras) if (GodotObject.IsInstanceValid(n)) n.Visible = on;
    }
    private List<Node3D>? _leaoExtras;
    /// <summary>3ª classe do Guerreiro: o Leão (corpo maior, braços e coxas grossos, cauda).</summary>
    private bool Leao3 => _classe3 && _classId == "guerreiro";
    /// <summary>Corpo da 3ª classe: era 1,20 × 1,28; +15% (pedido do dono 14/11).</summary>
    private const float C3Altura = 1.38f, C3Largura = 1.47f;
    private bool _evolvedWanted;
    // 18/11 16h (dono: "a cabecinha do personagem tá ficando pequena e o capacete muito grande"): o gato, o corvo e a
    // serpente ficavam a 70% (pareciam cabeça de alfinete); o leão já é volumoso e continua como estava
    private float EvolvedHeadShrink => _classId == "guerreiro" ? 0.72f : 0.86f;
    private bool HelmOn => _helmet is not null || _armorParts.Keys.Any(k => k.StartsWith("capacete", System.StringComparison.Ordinal));
    /// <summary>Cabeçona de capacete (classe evoluída): do tamanho da cabeça do bicho de cada classe.</summary>
    // 18/11 16h: o capacete crescia até 150% para cobrir a cabeça do bicho — mas a cabeça do bicho some de capacete, então o
    // capacete fica no tamanho de uma cabeça normal de quem usa armadura (o mesmo 1,1 do boneco sem classe evoluída)
    private float BigHelmScale => _classId == "guerreiro" ? 1.16f : 1.12f;
    private readonly HashSet<Node3D> _hiddenForAnimal = new();
    private float _yawAnt, _bank;   // 18/11 16h: inclinação de lado na curva voando

    /// <summary>
    /// Pedido do usuário: depois da troca de classe, o Guerreiro fica com cara de leão, o Mago de corvo, o Arqueiro de
    /// serpente e a Bruxa de gato. A cabeça do bicho envolve a cabeça do boneco (o rosto, cabelo e elmo somem).
    /// </summary>
    public void SetEvolved(bool on)
    {
        if (!_classe3) _evolvedWanted = on;
        if (_classe3) on = true;
        if (_skinId is not null) on = false;   // skin de bracelete por cima da classe (ver SetSkin)
        if (on == (_animal is not null)) return;
        if (!on)
        {
            _animal!.QueueFree(); _animal = null;
            if (Bone("Head") is var rhb and >= 0) _skel.SetBonePoseScale(rhb, Vector3.One);
            if (_classId == "guerreiro") SetBuild(1f, 1f);
            if (Leao3) SetBuild(C3Altura, C3Largura);
            foreach (var n in _hiddenForAnimal) if (GodotObject.IsInstanceValid(n)) n.Visible = true;
            _hiddenForAnimal.Clear();
            return;
        }
        foreach (var ch in _headSocket.GetChildren())
            if (ch is Node3D n3 && n3.Visible) { n3.Visible = false; _hiddenForAnimal.Add(n3); }
        // cabeças esculpidas (Meshy, recortadas pelo artista em 10/11): leão, corvo, cobra e gata; sem o arquivo, as de código
        // 11/11 noite ("a cabeça de leão está em cima da cabeça, como um chapéu, e não saindo do tronco"): a cabeça humana
        // encolhe para dentro do bicho (osso Head a 70%; ver ApplyEvolvedHead), e a do bicho desce até o pescoço, maior
        if (_classId == "guerreiro") SetBuild(1.05f, 1.14f);   // o Campeão ganha corpo de guerreiro (mais largo e alto)
        // 14/11 tarde (dono: "são 4 cabeças, cada um com seu corpo: leão = guerreiro, o mais forte; cobra = arqueiro e corvo
        // = mago, boneco normal; gato = bruxa"): a 3ª classe mantém o bicho da classe; só o Leão ganha o corpão
        var headCls = _classId;
        if (Leao3) SetBuild(C3Altura, C3Largura);
        if (CabecaAnimalGlb.Anexar(_headSocket, headCls) is { } sculpted)
        {
            _animal = sculpted;
            var (k, dy, dz) = headCls switch
            {
                // 12/11 ("as cabeças ficaram muito grandes"): uns 17% menores
                "guerreiro" => (1.0f, -0.075f, -0.01f),
                "mago" => (0.96f, -0.035f, 0f),
                "arqueiro" => (1.2f, -0.06f, 0.0f),
                _ => (0.85f, 0f, 0f),
            };
            sculpted.Scale = Vector3.One * (k / EvolvedHeadShrink);
            sculpted.Position = CabecaAnimalGlb.Ajuste + new Vector3(0, dy, dz) / EvolvedHeadShrink;
            return;
        }
        _animal = new Node3D { Name = "CabecaAnimal", Position = new Vector3(0, 0.1f, 0), Scale = Vector3.One * 1.15f };   // centro da cabeça; um pouco maior que a humana
        _headSocket.AddChild(_animal);
        switch (_classId)
        {
            case "guerreiro": Lion(_animal); break;
            case "mago": Crow(_animal); break;
            case "arqueiro": Snake(_animal); break;
            default: Cat(_animal); break;
        }
    }

    private static MeshInstance3D P(Node3D n, Mesh m, Material mat, float x, float y, float z, Vector3? rot = null, Vector3? sc = null)
        => VisualKit.Part(n, m, mat, new Vector3(x, y, z), rot, sc);

    private static void Eyes(Node3D h, Color c, float y, float z, float dx, Vector3 scale)
    {
        var glow = VisualKit.Glow(c, 2.4f);
        var black = VisualKit.Mat(new Color(0.02f, 0.02f, 0.02f), 0.3f);
        foreach (var sd in new[] { -1f, 1f })
        {
            P(h, VisualKit.Sphere(0.018f), glow, sd * dx, y, z, null, scale);
            P(h, VisualKit.Sphere(0.008f), black, sd * dx, y, z + 0.012f, null, new Vector3(0.45f, 1.6f, 0.5f));   // pupila em fenda
        }
    }

    private static void Lion(Node3D h)
    {
        var fur = VisualKit.Hide(new Color(0.80f, 0.58f, 0.27f), "pele", 0.85f, 6f);
        var mane = VisualKit.Hide(new Color(0.42f, 0.22f, 0.09f), "pele", 0.95f, 5f);
        var light = VisualKit.Hide(new Color(0.93f, 0.80f, 0.58f), "pele", 0.85f, 6f);
        // juba: volume atrás e uma coroa de mechas em volta do rosto
        P(h, VisualKit.Sphere(0.17f), mane, 0, 0.0f, -0.05f, null, new Vector3(1.15f, 1.15f, 0.9f));
        for (var i = 0; i < 16; i++)
        {
            var a = i / 16f * Mathf.Tau;
            var (cx, cy) = (Mathf.Sin(a), Mathf.Cos(a));
            P(h, VisualKit.Capsule(0.045f, 0.2f), mane, cx * 0.15f, cy * 0.15f - 0.01f, 0.0f,
                new Vector3(-25, 0, -Mathf.RadToDeg(a)), new Vector3(1f, 1f, 0.7f));
        }
        P(h, VisualKit.Sphere(0.115f), fur, 0, 0.0f, 0.03f, null, new Vector3(1f, 1.02f, 1.05f));          // rosto
        P(h, VisualKit.Sphere(0.06f), light, 0, -0.05f, 0.11f, null, new Vector3(1.25f, 0.85f, 1f));       // focinho
        P(h, VisualKit.Box(0.05f, 0.03f, 0.03f), VisualKit.Mat(new Color(0.18f, 0.1f, 0.08f), 0.4f), 0, -0.02f, 0.165f);   // nariz
        P(h, VisualKit.Box(0.13f, 0.025f, 0.05f), fur, 0, 0.035f, 0.115f, new Vector3(-10, 0, 0));          // sobrancelha
        P(h, VisualKit.Sphere(0.045f), light, 0, -0.1f, 0.09f, null, new Vector3(1f, 0.6f, 0.9f));          // queixo
        foreach (var sd in new[] { -1f, 1f })
            P(h, VisualKit.Sphere(0.035f), fur, sd * 0.085f, 0.11f, 0.0f, null, new Vector3(1f, 1f, 0.5f));  // orelhas redondas
        Eyes(h, new Color(1f, 0.7f, 0.15f), 0.012f, 0.128f, 0.045f, new Vector3(1.2f, 0.8f, 0.6f));
    }

    private static void Crow(Node3D h)
    {
        var feather = VisualKit.Hide(new Color(0.13f, 0.13f, 0.20f), "escama", 0.35f, 9f, 0.35f);   // preto-azulado com brilho
        var beak = VisualKit.Mat(new Color(0.42f, 0.40f, 0.38f), 0.25f, 0.55f);   // cinza-chumbo polido (lê de frente)
        P(h, VisualKit.Sphere(0.12f), feather, 0, 0.0f, 0.0f, null, new Vector3(1f, 1.05f, 1.12f));
        // bico longo e curvo para baixo
        P(h, VisualKit.Cylinder(0.0f, 0.042f, 0.19f, 8), beak, 0, -0.025f, 0.2f, new Vector3(100, 0, 0), new Vector3(1f, 1f, 0.75f));
        P(h, VisualKit.Cylinder(0.0f, 0.03f, 0.13f, 8), beak, 0, -0.05f, 0.17f, new Vector3(95, 0, 0), new Vector3(1f, 1f, 0.6f));
        // penas da nuca arrepiadas (crista para trás)
        for (var i = 0; i < 5; i++)
            P(h, VisualKit.Box(0.03f, 0.008f, 0.14f), feather, (i - 2) * 0.025f, 0.07f - Mathf.Abs(i - 2) * 0.012f, -0.13f,
                new Vector3(-25, (i - 2) * 10, 0));
        foreach (var sd in new[] { -1f, 1f })
            P(h, VisualKit.Box(0.02f, 0.11f, 0.1f), feather, sd * 0.11f, -0.04f, -0.02f, new Vector3(15, 0, sd * 12));   // bochechas
        Eyes(h, new Color(0.55f, 0.75f, 1f), 0.02f, 0.105f, 0.055f, new Vector3(1f, 1f, 0.6f));
    }

    private static void Snake(Node3D h)
    {
        var scales = VisualKit.Hide(new Color(0.24f, 0.42f, 0.18f), "escama", 0.5f, 10f, 0.1f);
        var belly = VisualKit.Hide(new Color(0.78f, 0.74f, 0.45f), "escama", 0.6f, 10f);
        var hood = VisualKit.Hide(new Color(0.14f, 0.28f, 0.12f), "escama", 0.55f, 8f, 0.1f);
        // capelo de naja abrindo atrás da cabeça
        P(h, VisualKit.Sphere(0.16f), hood, 0, -0.06f, -0.06f, null, new Vector3(1.45f, 1.5f, 0.32f));
        foreach (var sd in new[] { -1f, 1f })
            P(h, VisualKit.Sphere(0.03f), belly, sd * 0.13f, -0.02f, -0.02f, null, new Vector3(1f, 1.3f, 0.4f));   // marcas do capelo
        P(h, VisualKit.Sphere(0.115f), scales, 0, 0.0f, 0.02f, null, new Vector3(1f, 0.9f, 1.25f));           // cabeça achatada e comprida
        P(h, VisualKit.Sphere(0.07f), scales, 0, -0.03f, 0.13f, null, new Vector3(1.15f, 0.65f, 1.1f));        // focinho
        P(h, VisualKit.Sphere(0.06f), belly, 0, -0.07f, 0.1f, null, new Vector3(1.1f, 0.4f, 1.2f));            // mandíbula clara
        foreach (var sd in new[] { -1f, 1f })
            P(h, VisualKit.Cylinder(0.0f, 0.007f, 0.04f, 6), VisualKit.Mat(new Color(0.95f, 0.93f, 0.85f), 0.3f), sd * 0.03f, -0.09f, 0.17f, new Vector3(180, 0, 0));   // presas
        Eyes(h, new Color(1f, 0.9f, 0.2f), 0.025f, 0.115f, 0.06f, new Vector3(1.3f, 0.9f, 0.6f));
    }

    private static void Cat(Node3D h)
    {
        var fur = VisualKit.Hide(new Color(0.20f, 0.17f, 0.22f), "pele", 0.85f, 6f);
        var light = VisualKit.Hide(new Color(0.62f, 0.58f, 0.62f), "pele", 0.85f, 6f);
        var pink = VisualKit.Mat(new Color(0.85f, 0.5f, 0.55f), 0.5f);
        P(h, VisualKit.Sphere(0.12f), fur, 0, 0.0f, 0.0f, null, new Vector3(1.05f, 1f, 1.02f));
        P(h, VisualKit.Sphere(0.05f), light, 0, -0.055f, 0.1f, null, new Vector3(1.3f, 0.8f, 0.9f));     // focinho
        P(h, VisualKit.Cylinder(0.0f, 0.012f, 0.015f, 3), pink, 0, -0.025f, 0.142f, new Vector3(180, 0, 0)); // nariz
        foreach (var sd in new[] { -1f, 1f })
        {
            // orelhas pontudas (pirâmide) com miolo rosado
            P(h, VisualKit.Cylinder(0.0f, 0.055f, 0.1f, 4), fur, sd * 0.07f, 0.13f, -0.01f, new Vector3(-8, 45, sd * -18), new Vector3(1f, 1f, 0.55f));
            P(h, VisualKit.Cylinder(0.0f, 0.032f, 0.065f, 4), pink, sd * 0.07f, 0.125f, 0.012f, new Vector3(-8, 45, sd * -18), new Vector3(1f, 1f, 0.35f));
            for (var k = -1; k <= 1; k++)   // bigodes
                P(h, VisualKit.Box(0.11f, 0.003f, 0.003f), light, sd * 0.09f, -0.05f + k * 0.012f, 0.12f, new Vector3(0, sd * -12, sd * k * 8));
        }
        Eyes(h, new Color(0.45f, 1f, 0.35f), 0.015f, 0.105f, 0.05f, new Vector3(1.25f, 1f, 0.6f));
    }

    public void Dispose() => Root.QueueFree();
}
