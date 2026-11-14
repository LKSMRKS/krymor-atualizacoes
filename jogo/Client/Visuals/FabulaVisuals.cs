using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 13/11 — visuais do conteúdo novo ("Era Uma Vez", macaco, dentuço, Dama Urtiga, Vlakor, tritão). Gente e bicho de pé
/// são o NOSSO manequim com peças por cima (regra do dono); o dentuço (bípede de cabeção) tem esqueleto de formas próprio.
/// Inspirações viraram desenhos ORIGINAIS: a família é cinza-esverdeada, sem pele amarela nem penteados conhecidos; a
/// Dama Urtiga é casca e folha, não ruiva de pele verde; o dentuço é turquesa, de focinho curto e placas nas costas.
/// </summary>
public static class FabulaVisuals
{
    private static readonly Color PeleFamilia = new(0.42f, 0.47f, 0.38f);   // cinza-esverdeado de quem não toma sol

    public static IEntityVisual? Criar(string visual, bool boss)
    {
        switch (visual)
        {
            case "fab_merlin_sombra": return Npc("merlin_sombra", "");   // 13/11: o Reflexo de Merlin (chefe da torre)
            case "fab_dentuco": return new DinoVisual(false);
            case "fab_dentuco_pai": return new DinoVisual(true);
            case "fab_gnomo" when MannequinVisual.MonsterBody("humanoides/mo_gnomo_areal", 0.62f) is { } gnomo:
                gnomo.SetBuild(0.65f, 0.68f);
                gnomo.Enfeite("cogumelo", new Color(0.7f, 0.3f, 1f));   // o feitiço: cogumelinho roxo aceso na cabeça
                return gnomo;
        }
        if (!VisualFactory.UseMannequin) return null;
        MannequinVisual? m = visual switch
        {
            "fab_pai" => Mon(new HumanoidStyle { Scale = 1.12f, Skin = PeleFamilia, HeadColor = PeleFamilia.Lightened(0.08f), Cloth = new Color(0.78f, 0.76f, 0.7f),
                Accent = new Color(0.3f, 0.3f, 0.34f), Eyes = new Color(0.08f, 0.08f, 0.08f), Girth = 1.7f, Bulky = true, Nose = true, HeadScale = 1.1f, LimbThickness = 1.05f }),
            "fab_mae" => Mon(new HumanoidStyle { Female = true, Scale = 1.08f, Skin = PeleFamilia, HeadColor = PeleFamilia.Lightened(0.1f), Cloth = new Color(0.25f, 0.42f, 0.3f),
                Accent = new Color(0.85f, 0.2f, 0.2f), Eyes = new Color(0.08f, 0.08f, 0.08f), Robe = true, Girth = 0.82f, Nose = true }),
            "fab_filho" => Mon(new HumanoidStyle { Scale = 0.62f, Skin = PeleFamilia, HeadColor = PeleFamilia.Lightened(0.1f), Cloth = new Color(0.75f, 0.35f, 0.15f),
                Accent = new Color(0.2f, 0.3f, 0.6f), Eyes = new Color(0.08f, 0.08f, 0.08f), HeadScale = 1.25f, Ears = true }),
            "fab_filha" => Mon(new HumanoidStyle { Female = true, Scale = 0.58f, Skin = PeleFamilia, HeadColor = PeleFamilia.Lightened(0.1f), Cloth = new Color(0.5f, 0.2f, 0.45f),
                Accent = new Color(0.95f, 0.85f, 0.4f), Eyes = new Color(0.08f, 0.08f, 0.08f), HeadScale = 1.25f }),
            "fab_bebe" => Mon(new HumanoidStyle { Scale = 0.38f, Skin = PeleFamilia, HeadColor = PeleFamilia.Lightened(0.12f), Cloth = new Color(0.85f, 0.85f, 0.8f),
                Accent = new Color(0.4f, 0.75f, 0.95f), Eyes = new Color(0.08f, 0.08f, 0.08f), HeadScale = 1.6f, Girth = 1.3f }),
            "fab_fungilda" => Mon(new HumanoidStyle { Female = true, Scale = boss ? 1.18f : 1.05f, Skin = new Color(0.62f, 0.55f, 0.6f), HeadColor = new Color(0.68f, 0.6f, 0.66f),
                Cloth = new Color(0.32f, 0.16f, 0.4f), Accent = new Color(0.65f, 0.3f, 0.85f), Eyes = new Color(0.8f, 0.4f, 1f), EyesGlow = true, Robe = true, CasterHands = true,
                HairColor = new Color(0.82f, 0.85f, 0.78f), Hunch = 0.15f, Nose = true, Girth = 0.85f }),
            "fab_soldadinho" => Mon(new HumanoidStyle { Scale = 0.9f, Skin = new Color(0.48f, 0.5f, 0.55f), HeadColor = new Color(0.56f, 0.58f, 0.62f), Cloth = new Color(0.62f, 0.1f, 0.1f),
                Accent = new Color(0.9f, 0.75f, 0.3f), Eyes = new Color(0.05f, 0.05f, 0.05f), FixedWeapon = "spear_watch", Girth = 0.95f }),
            "fab_bobo_mau" => Bobo(new Color(0.12f, 0.1f, 0.12f), new Color(0.7f, 0.08f, 0.1f), 1.0f, false),
            "fab_grao_bobo" => Bobo(new Color(0.35f, 0.12f, 0.45f), new Color(0.95f, 0.75f, 0.2f), boss ? 1.55f : 1.3f, true),
            "fab_macaco" => Mon(new HumanoidStyle { Scale = 0.75f, Skin = new Color(0.32f, 0.2f, 0.12f), HeadColor = new Color(0.36f, 0.23f, 0.14f), Cloth = new Color(0.3f, 0.2f, 0.12f),
                Accent = new Color(0.6f, 0.45f, 0.3f), Eyes = new Color(0.1f, 0.06f, 0.02f), Hunch = 0.38f, LimbThickness = 0.95f, HeadScale = 1.15f, Girth = 1.05f, Underwear = true }),
            "fab_urtiga" => Mon(new HumanoidStyle { Female = true, Scale = boss ? 1.25f : 1.05f, Skin = new Color(0.38f, 0.28f, 0.18f), HeadColor = new Color(0.44f, 0.33f, 0.22f),
                Cloth = new Color(0.18f, 0.32f, 0.14f), Accent = new Color(0.62f, 0.3f, 0.85f), Eyes = new Color(0.75f, 1f, 0.35f), EyesGlow = true, Robe = true, CasterHands = true,
                Girth = 0.8f }),
            "fab_vamp_barao" => Mon(new HumanoidStyle { Scale = 1.15f, Skin = new Color(0.76f, 0.74f, 0.78f), HeadColor = new Color(0.84f, 0.82f, 0.86f), Cloth = new Color(0.08f, 0.06f, 0.1f),
                Accent = new Color(0.5f, 0.05f, 0.1f), Eyes = new Color(1f, 0.15f, 0.15f), EyesGlow = true, Cape = true, Ears = true, Tusks = true, Beard = true, Girth = 1.0f, FixedWeapon = "sword_ornate" }),
            "fab_vamp_jovem" => Mon(new HumanoidStyle { Scale = 0.95f, Skin = new Color(0.8f, 0.78f, 0.82f), HeadColor = new Color(0.88f, 0.86f, 0.9f), Cloth = new Color(0.14f, 0.1f, 0.22f),
                Accent = new Color(0.45f, 0.1f, 0.5f), Eyes = new Color(1f, 0.2f, 0.4f), EyesGlow = true, Cape = true, Ears = true, Tusks = true, Girth = 0.82f }),
            "fab_vamp_jovem_f" => Mon(new HumanoidStyle { Female = true, Scale = 0.95f, Skin = new Color(0.8f, 0.78f, 0.82f), HeadColor = new Color(0.88f, 0.86f, 0.9f), Cloth = new Color(0.14f, 0.1f, 0.22f),
                Accent = new Color(0.45f, 0.1f, 0.5f), Eyes = new Color(1f, 0.2f, 0.4f), EyesGlow = true, Cape = true, Tusks = true, HairColor = new Color(0.5f, 0.48f, 0.52f), Girth = 0.8f }),
            "fab_tritao" => Mon(new HumanoidStyle { Scale = boss ? 1.7f : 1.3f, Skin = new Color(0.2f, 0.42f, 0.42f), HeadColor = new Color(0.24f, 0.48f, 0.47f), Cloth = new Color(0.12f, 0.25f, 0.22f),
                Accent = new Color(0.55f, 0.75f, 0.3f), Eyes = new Color(0.45f, 1f, 0.2f), EyesGlow = true, Bulky = true, Girth = 1.15f, Crown = true,
                BodyGlow = new Color(0.04f, 0.12f, 0.02f) }),
            _ => null,
        };
        if (m is null) return null;
        switch (visual)
        {
            case "fab_pai": m.Enfeite("topete", new Color(0.15f, 0.12f, 0.1f)); m.Enfeite("barriga", new Color(0.82f, 0.8f, 0.72f)); break;
            case "fab_mae": m.Enfeite("tranca_longa", new Color(0.35f, 0.08f, 0.06f)); break;
            case "fab_filho": m.Enfeite("cabelo_bagunca", new Color(0.18f, 0.12f, 0.08f)); m.Enfeite("estilingue", Colors.White); break;
            case "fab_filha": m.Enfeite("marias_chiquinhas", new Color(0.1f, 0.08f, 0.08f)); m.Enfeite("rabeca", Colors.White); break;
            case "fab_bebe": m.Enfeite("touca_bebe", new Color(0.55f, 0.8f, 0.95f)); m.Enfeite("chocalho", Colors.White); break;
            case "fab_fungilda": m.Enfeite("cogumelo_chapeu", new Color(0.55f, 0.2f, 0.75f)); break;
            case "fab_macaco": m.Enfeite("rabo_macaco", new Color(0.32f, 0.2f, 0.12f)); m.Enfeite("focinho_macaco", new Color(0.72f, 0.55f, 0.42f)); break;
            case "fab_urtiga": m.Enfeite("coroa_espinhos", new Color(0.3f, 0.55f, 0.22f)); break;
            case "fab_vamp_barao": m.Enfeite("coroa", new Color(0.55f, 0.55f, 0.6f)); break;
            case "fab_tritao": m.Enfeite("algas", new Color(0.18f, 0.45f, 0.2f)); m.Enfeite("tridente", new Color(0.55f, 0.6f, 0.35f)); break;
            case "fab_soldadinho": m.Enfeite("coroa", new Color(0.12f, 0.12f, 0.14f)); break;   // barretina preta
        }
        return m;
    }

    private static MannequinVisual? Mon(HumanoidStyle s) => MannequinVisual.Monster(s);

    /// <summary>NPCs do conteúdo novo com cara própria (null = segue o NPC comum).</summary>
    public static IEntityVisual? Npc(string look, string id)
    {
        if (!VisualFactory.UseMannequin) return null;
        // 13/11: Merlin, o mago — o nosso boneco (corpo do Mago, com a barba branca), manto roxo com estrelas e luas
        // amarelas, barba longa, óculos redondos, chapéu pontudo e cajado. "merlin_sombra" = o reflexo (chefe da torre).
        if (look is "merlin" or "merlin_sombra" && MannequinVisual.TryCreate("mago", false) is { } mer)
        {
            var sombra = look == "merlin_sombra";
            // 13/11: pacote próprio do Merlin (npcs/npc_merlin.glb: chapéu estrelado, óculos, barba, manto e cajado) quando existe
            if (!sombra)
            {
                mer.SetGear(null, null, "npc:arcanista:0:m");
                if (mer.VestirPacote("merlin")) { mer.SetBuild(0.97f, 0.95f); return mer; }
            }
            var roxo = sombra ? new Color(0.12f, 0.06f, 0.2f) : new Color(0.38f, 0.16f, 0.62f);
            mer.SetGear("staff_crystal", null, sombra ? "body_merlin_sombra" : "body_merlin");
            mer.Enfeite("chapeu_merlin", roxo);
            mer.Enfeite("barba_longa", sombra ? new Color(0.35f, 0.3f, 0.45f) : new Color(0.93f, 0.92f, 0.9f));
            mer.Enfeite("oculos", Colors.White);
            mer.Enfeite("manto_estrelado", sombra ? new Color(0.6f, 0.3f, 1f) : new Color(1f, 0.85f, 0.25f));
            mer.SetBuild(sombra ? 1.15f : 0.98f, 0.92f);
            return mer;
        }
        if (look == "gnomo" && MannequinVisual.MonsterBody("humanoides/mo_gnomo_areal", 0.62f) is { } g) { g.SetBuild(0.65f, 0.68f); return g; }
        if (id is "agostinho_bobo" or "bobo_malaquias" && MannequinVisual.TryCreate("npc", false) is { } b)
        {
            var triste = id == "agostinho_bobo";
            b.SetGear(null, null, triste ? "npc:morador:4:m" : "npc:morador:1:m");
            b.Enfeite("chapeu_bobo", triste ? new Color(0.25f, 0.45f, 0.9f) : new Color(0.75f, 0.08f, 0.1f));
            if (!triste) b.Enfeite("facas", Colors.White);
            b.SetBuild(triste ? 0.95f : 1.02f, triste ? 0.95f : 0.85f);
            return b;
        }
        return null;
    }

    /// <summary>Bobo da corte inimigo: manequim com o pacote do bobo e cores próprias, chapéu de guizos, facas ou cetro.</summary>
    private static MannequinVisual? Bobo(Color escuro, Color vivo, float escala, bool lider)
    {
        var m = Mon(new HumanoidStyle { Scale = escala, Skin = new Color(0.86f, 0.82f, 0.78f), HeadColor = new Color(0.92f, 0.9f, 0.88f), Cloth = escuro, Accent = vivo,
            Eyes = lider ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.15f, 0.1f), EyesGlow = true, Hunch = lider ? 0.05f : 0.18f, Girth = lider ? 1.35f : 0.85f, Bulky = lider, Nose = true });
        if (m is null) return null;
        m.Enfeite("chapeu_bobo", vivo);
        if (lider) { m.Enfeite("coroa", new Color(0.95f, 0.78f, 0.25f)); m.Enfeite("cetro_guizo", vivo); }
        else m.Enfeite("facas", Colors.White);
        return m;
    }
}

/// <summary>
/// 13/11: Dentuço — dinossauro-mirim bípede (desenho original): corpo de pera inclinado, cabeção com focinho curto e
/// dentes de serrote, bracinhos curtos, rabo grosso, placas nas costas. Preparação da corrida ("esfrega_pata"): abaixa e
/// raspa uma pata no chão para trás várias vezes; corrida: inclina para a frente; mordidas laterais: a cabeça vai de um
/// lado para o outro. O pai é maior, mais escuro e cheio de espinhos.
/// </summary>
public sealed class DinoVisual : IEntityVisual
{
    private readonly VisualKit.FlashGroup _flash = new();
    private readonly Node3D _corpo, _cabeca, _queixo, _rabo, _pernaE, _pernaD, _canelaE, _canelaD;
    private readonly bool _pai;
    private float _fase, _t, _hitT = 1f, _morteT, _fade;
    private bool _vivo = true;
    private Vector3 _rot;

    public Node3D Root { get; } = new() { Name = "Dentuco" };
    public float Height { get; }

    public DinoVisual(bool pai)
    {
        _pai = pai;
        var esc = pai ? 1.75f : 1f;
        Height = 1.25f * esc;
        var raiz = VisualKit.Joint(Root, Vector3.Zero);
        raiz.Scale = Vector3.One * esc;
        var couro = _flash.Add(VisualKit.Hide(pai ? new Color(0.12f, 0.36f, 0.36f) : new Color(0.2f, 0.62f, 0.58f), "escama", 0.75f, 3f));
        var barriga = _flash.Add(VisualKit.Mat(new Color(0.92f, 0.86f, 0.66f), 0.8f));
        var osso = _flash.Add(VisualKit.Mat(new Color(0.95f, 0.92f, 0.82f), 0.5f));
        var placa = _flash.Add(VisualKit.Mat(pai ? new Color(0.65f, 0.25f, 0.12f) : new Color(0.95f, 0.55f, 0.2f), 0.6f));
        var olho = VisualKit.Glow(new Color(1f, 0.85f, 0.2f), 2f);
        _corpo = VisualKit.Joint(raiz, new Vector3(0, 0.62f, 0), "Corpo");
        VisualKit.Part(_corpo, VisualKit.Sphere(0.36f), couro, Vector3.Zero, null, new Vector3(1f, 1.05f, 1.2f));
        VisualKit.Part(_corpo, VisualKit.Sphere(0.3f), barriga, new Vector3(0, -0.06f, 0.12f), null, new Vector3(0.85f, 0.9f, 0.9f));
        // placas nas costas (o pai tem mais, e espinhos)
        var n = pai ? 7 : 4;
        for (var i = 0; i < n; i++)
        {
            var z = 0.25f - i * (0.75f / n);
            VisualKit.Part(_corpo, VisualKit.Cylinder(0f, pai ? 0.08f : 0.07f, pai ? 0.28f : 0.18f, 4), placa, new Vector3(0, 0.33f - i * 0.02f, z), new Vector3(-20, 45, 0));
            if (pai) foreach (var sd in new[] { -1f, 1f })
                VisualKit.Part(_corpo, VisualKit.Cylinder(0f, 0.035f, 0.16f, 4), osso, new Vector3(sd * 0.25f, 0.18f, z), new Vector3(0, 0, sd * -60));
        }
        // bracinhos curtos
        foreach (var sd in new[] { -1f, 1f })
        {
            VisualKit.Part(_corpo, VisualKit.Capsule(0.04f, 0.2f), couro, new Vector3(sd * 0.22f, 0.02f, 0.32f), new Vector3(60, 0, sd * 15));
            VisualKit.Part(_corpo, VisualKit.Cylinder(0f, 0.02f, 0.05f, 4), osso, new Vector3(sd * 0.24f, -0.06f, 0.4f), new Vector3(150, 0, 0));
        }
        // cabeça grande, focinho curto, dentes de serrote
        _cabeca = VisualKit.Joint(_corpo, new Vector3(0, 0.3f, 0.36f), "Cabeca");
        VisualKit.Part(_cabeca, VisualKit.Sphere(0.27f), couro, new Vector3(0, 0.05f, 0.12f), null, new Vector3(1f, 0.85f, 1.15f));
        VisualKit.Part(_cabeca, VisualKit.Box(0.34f, 0.12f, 0.2f), couro, new Vector3(0, -0.02f, 0.32f));
        foreach (var sd in new[] { -1f, 1f })
        {
            VisualKit.Part(_cabeca, VisualKit.Sphere(0.06f), osso, new Vector3(sd * 0.14f, 0.14f, 0.24f));
            VisualKit.Part(_cabeca, VisualKit.Sphere(0.03f), olho, new Vector3(sd * 0.15f, 0.15f, 0.28f));
            VisualKit.Part(_cabeca, VisualKit.Sphere(0.05f), couro, new Vector3(sd * 0.12f, 0.2f, 0.2f), null, new Vector3(1.2f, 0.5f, 1f));   // sobrancelha brava
        }
        for (var i = 0; i < 6; i++)
            VisualKit.Part(_cabeca, VisualKit.Cylinder(0f, 0.022f, 0.06f, 4), osso, new Vector3(-0.14f + i * 0.056f, -0.09f, 0.4f), new Vector3(180, 0, 0));
        _queixo = VisualKit.Joint(_cabeca, new Vector3(0, -0.08f, 0.12f), "Queixo");
        VisualKit.Part(_queixo, VisualKit.Box(0.3f, 0.07f, 0.3f), couro, new Vector3(0, -0.04f, 0.16f));
        for (var i = 0; i < 5; i++)
            VisualKit.Part(_queixo, VisualKit.Cylinder(0f, 0.02f, 0.05f, 4), osso, new Vector3(-0.11f + i * 0.055f, 0.01f, 0.28f));
        // rabo grosso
        _rabo = VisualKit.Joint(_corpo, new Vector3(0, -0.02f, -0.34f), "Rabo");
        VisualKit.Part(_rabo, VisualKit.Cylinder(0.05f, 0.2f, 0.7f, 10), couro, new Vector3(0, -0.05f, -0.3f), new Vector3(-80, 0, 0));
        if (pai) for (var i = 0; i < 3; i++) VisualKit.Part(_rabo, VisualKit.Cylinder(0f, 0.04f, 0.14f, 4), osso, new Vector3(0, 0.08f, -0.3f - i * 0.15f), new Vector3(-30, 0, 0));
        // pernas fortes
        (_pernaE, _canelaE) = Perna(raiz, -1f, couro, osso);
        (_pernaD, _canelaD) = Perna(raiz, 1f, couro, osso);
    }

    private static (Node3D, Node3D) Perna(Node3D raiz, float sd, StandardMaterial3D couro, StandardMaterial3D osso)
    {
        var coxa = VisualKit.Joint(raiz, new Vector3(sd * 0.2f, 0.55f, -0.05f));
        VisualKit.Part(coxa, VisualKit.Capsule(0.11f, 0.36f), couro, new Vector3(0, -0.14f, 0.03f));
        var canela = VisualKit.Joint(coxa, new Vector3(0, -0.3f, 0.04f));
        VisualKit.Part(canela, VisualKit.Capsule(0.06f, 0.3f), couro, new Vector3(0, -0.12f, -0.02f));
        VisualKit.Part(canela, VisualKit.Box(0.16f, 0.05f, 0.22f), couro, new Vector3(0, -0.24f, 0.05f));
        for (var i = -1; i <= 1; i++)
            VisualKit.Part(canela, VisualKit.Cylinder(0f, 0.02f, 0.06f, 4), osso, new Vector3(i * 0.05f, -0.25f, 0.17f), new Vector3(90, 0, 0));
        return (coxa, canela);
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) => _hitT = 0f;

    public void Update(in VisualFrame f, float delta)
    {
        _t += delta;
        if (!f.Alive)
        {
            if (_vivo) { _morteT = 0f; _fade = 0f; }
            _vivo = false;
            _morteT = Mathf.Min(1f, _morteT + delta / 0.6f);
            var k = Mathf.SmoothStep(0f, 1f, _morteT);
            _corpo.Rotation = new Vector3(0, 0, Mathf.Pi * 0.5f * k);
            _corpo.Position = new Vector3(0, 0.62f - 0.35f * k, 0);
            if (_morteT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.4f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_vivo) { _vivo = true; Root.Visible = true; }
        var colon = f.Action.IndexOf(':');
        var fase = colon < 0 ? f.Action : f.Action[..colon];
        var golpe = colon < 0 ? "" : f.Action[(colon + 1)..];
        var t = f.ActionT;
        _fase += f.Speed * delta * (_pai ? 4.5f : 6.5f);
        var amp = Mathf.Clamp(f.Speed / 3f, 0f, 1.2f);
        var passo = Mathf.Sin(_fase);
        float pe = 0.6f * passo * amp, pd = -0.6f * passo * amp, ce = -0.5f * Mathf.Max(0, Mathf.Cos(_fase)) * amp, cd = -0.5f * Mathf.Max(0, -Mathf.Cos(_fase)) * amp;
        var rot = new Vector3(0.2f + 0.05f * Mathf.Sin(_fase * 2f) * amp, 0, 0.06f * passo * amp);
        float y = 0.62f + 0.03f * Mathf.Abs(passo) * amp, z = 0f, queixo = 0.05f + 0.04f * Mathf.Sin(_t * 1.6f), cabYaw = 0f, cabPitch = 0f;
        switch (fase)
        {
            case "windup" when golpe == "esfrega_pata":
            {
                // abaixa, olha fixo e raspa a pata direita no chão para trás (três, quatro vezes): sinal claro de que vai correr
                rot.X = 0.35f; y -= 0.1f; z = -0.1f; cabPitch = -0.25f;
                var r = Mathf.Sin(_t * 15f);
                pd = 0.25f + 0.75f * Mathf.Max(0, -r); cd = -0.2f - 0.4f * Mathf.Max(0, r);
                pe = -0.25f; ce = -0.35f;
                rot.Z = 0.05f * r;
                break;
            }
            case "windup":
                rot.X = 0.3f * t; y -= 0.08f * t; z = -0.12f * t; queixo = 0.5f * t; cabPitch = 0.2f * t;
                break;
            case "active" when golpe == "mordidas_laterais":
                // mordidas para os lados: a cabeça varre de um lado para o outro abrindo e fechando a boca
                cabYaw = 0.9f * Mathf.Sin(_t * 14f); queixo = 0.15f + 0.5f * Mathf.Max(0, Mathf.Sin(_t * 28f)); rot.X = 0.35f; z = 0.15f;
                rot.Y = 0.25f * Mathf.Sin(_t * 14f);
                break;
            case "active" when golpe is "esfrega_pata" or "cabecada":
                rot.X = 0.65f; z = 0.3f; cabPitch = 0.45f; queixo = 0.05f;
                pe = 0.9f * Mathf.Sin(_t * 26f); pd = -pe; ce = -0.6f; cd = -0.6f;
                break;
            case "active":
                rot.X = 0.4f; z = 0.3f; queixo = t < 0.5f ? 0.7f : 0.05f; cabPitch = 0.2f;
                break;
            case "recover":
                rot.X = 0.1f * (1f - t); y -= 0.05f * (1f - t); rot.Z = 0.06f * Mathf.Sin(_t * 8f) * (1f - t);
                break;
        }
        if (_hitT < 1f) { _hitT = Mathf.Min(1f, _hitT + delta / 0.25f); rot.X -= 0.2f * VisualKit.Snap(_hitT, 0.2f); }
        _rot = VisualKit.Approach(_rot, rot, fase == "active" ? 26f : 12f, delta);
        _corpo.Rotation = _rot;
        _corpo.Position = new Vector3(0, y, VisualKit.Approach(_corpo.Position.Z, z, 14f, delta));
        _cabeca.Rotation = new Vector3(cabPitch - _rot.X * 0.6f, cabYaw, 0);
        _queixo.Rotation = new Vector3(queixo, 0, 0);
        _rabo.Rotation = new Vector3(-_rot.X * 0.5f, 0.35f * Mathf.Sin(_t * 3f + _fase) - _rot.Y, 0);
        _pernaE.Rotation = new Vector3(pe, 0, 0); _pernaD.Rotation = new Vector3(pd, 0, 0);
        _canelaE.Rotation = new Vector3(ce, 0, 0); _canelaD.Rotation = new Vector3(cd, 0, 0);
        _flash.Apply(_hitT < 1f ? 1f - _hitT : 0f, f.WindupGlow, new Color(1f, 0.25f, 0.1f));
    }

    public void Dispose() => Root.QueueFree();
}
