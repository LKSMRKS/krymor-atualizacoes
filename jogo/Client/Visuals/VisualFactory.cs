using System.Linq;
using System.Collections.Generic;
using System;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Único lugar que decide a aparência de cada entidade. Recebe IDs (classe do jogador, definição do monstro) e
/// devolve um <see cref="IEntityVisual"/>. Para usar modelos 3D no futuro, troque o retorno aqui por um visual
/// que carregue .glb — combate, inventário e rede não mudam.
/// </summary>
public static class VisualFactory
{
    /// <summary>
    /// Jogador: boneco de manequim de cueca (E9); o que veste vem do equipamento (SetGear/SetHead). Mago se distingue
    /// pelos olhos azuis acesos; a cor da cueca separa você (azul) dos outros (verde).
    /// </summary>
    public static IEntityVisual Player(string classId, bool isMe)
    {
        // manequim articulado (mesmo corpo para todas as classes; Bruxa usa o corpo feminino). Sem o .glb: boneco antigo.
        if (UseMannequin && MannequinVisual.TryCreate(classId, FemaleBody(classId)) is { } mannequin) return mannequin;
        var style = new HumanoidStyle
        {
            Underwear = true,
            UnderwearColor = isMe ? new Color(0.13f, 0.13f, 0.15f) : new Color(0.20f, 0.14f, 0.16f),
            Accent = new Color(0.80f, 0.66f, 0.30f),
        };
        // mago e guerreiro: o mesmo corpo-base (prancha 07); a classe se lê pelo equipamento, não pelo corpo
        return new HumanoidVisual(style);
    }

    /// <summary>Jogadores com o manequim .glb (false: boneco de formas simples — opção --boneco-classico).</summary>
    public static bool UseMannequin { get; set; } = true;

    public static bool FemaleBody(string classId) => classId == "bruxa";

    /// <summary>NPC: mentores das classes novas usam o manequim já vestido; os outros, o boneco de formas simples.</summary>
    public static IEntityVisual NpcVisual(string look, string id = "", string name = "")
    {
        if (look == "quadro") return NoticeBoard();
        // 12/11: personagens recorrentes
        if (UseMannequin && look == "fallen" && MannequinVisual.TryCreate("npc", false) is { } fallen)
        {
            // Capitão Fallen: casaco de capitão, chapéu, cabelo e barba ruivos (barba curta) e o rifle na mão
            fallen.VestirPacote("capita");
            fallen.SetGear("rifle_fallen", null, "npc:capita:3:m");
            fallen.SetHairStyle(3, new Color(0.55f, 0.20f, 0.07f));
            fallen.SetBarbaCurta(new Color(0.60f, 0.23f, 0.08f));
            fallen.SetBuild(1.05f, 1.02f);
            return fallen;
        }
        if (look == "vacho") return Vacho();
        if (FabulaVisuals.Npc(look, id) is { } fabNpc) return fabNpc;   // 13/11: gnomos curados, Agostinho e Malaquias
        if (UseMannequin && look is "bruxa" or "arqueiro" && MannequinVisual.TryCreate(look, look == "bruxa") is { } m)
        {
            // 11/11 noite ("Sibila e Hazel são iguais"): a Vidente usa manto rosado, capuz e amuleto de lua, sem o chapéu
            // da Hazel, e é um pouco mais alta e magra
            if (look == "bruxa" && name.Contains("Sibila")) { m.SetGear("talisman_moon", null, "body_witch_rose"); m.SetHead("head_hood"); m.SetBuild(1.04f, 0.9f); }
            else if (look == "bruxa") { m.SetGear("talisman_vein", null, "body_witch_vein"); m.VestirPacote("bruxa"); }
            else if (m.VestirPacote(name.Contains("Lia") ? "batedora" : "batedor")) m.SetGear("bow_yew", null, "body_ranger");
            else { m.SetGear("bow_yew", null, "body_ranger"); m.SetHead("head_hood"); }
            return m;
        }
        if (UseMannequin && NpcMannequin(look, id, name) is { } npc) return npc;
        return Npc(look);
    }

    /// <summary>
    /// NPCs no mesmo manequim dos jogadores: gênero pelo nome ("Odete, a Curandeira"), corte de roupa pelo papel,
    /// cores/altura/largura sorteadas pelo id (estáveis: o mesmo NPC sai sempre igual).
    /// </summary>
    private static MannequinVisual? NpcMannequin(string look, string id, string name)
    {
        var fem = name.Contains(", a ") || name.StartsWith("Capitã") || name.Contains("Viúva") || name.Contains("Abadessa");
        var h = 17u;
        foreach (var ch in id + name) h = h * 31 + ch;
        float R(int salt) => ((h >> (salt * 3)) % 1000) / 999f;
        var role = look switch
        {
            "guarda" when name.Contains("Batedor") => "batedora",
            "guarda" when name.Contains("Tratador") || name.Contains("Carpinteiro") => "aldeao",
            "curandeira" when !fem => "aldeao",
            _ => look,
        };
        var cls = look == "arcanista" ? "mago" : "npc";
        var m = MannequinVisual.TryCreate(cls, fem);
        if (m is null) return null;
        // pacote de destaque do Cal Lote 2 (chapéu, avental, rede, livro, gato no ombro…) pelo papel do NPC
        // 13/11 ("vários NPCs são cópias idênticas de outros — ex.: Tupã-Iara igual ao cara que vende poder na praça"): o
        // pacote de roupa de destaque fica só com o NPC titular de cada papel (Dorval, Brás, Ilma, Isaura...); os outros do
        // mesmo papel ganham roupa comum na cor sorteada, cabelo, barba e um enfeite próprio
        var pack = NpcPack(look, role, name);
        if (pack is "mentor" or "ferreiro" or "curandeira" or "arcanista" or "capita" or "guarda" && !Titular(pack, name)) pack = null;
        var dressed = pack is not null && m.VestirPacote(pack);
        var packTool = dressed && pack is "ferreiro" or "guarda" or "agricultora" or "mineiro";   // o pacote já traz a ferramenta
        var packHat = dressed && pack is "capita" or "pescador" or "bruxa" or "bobo" or "mineiro" or "guarda" or "batedor" or "mentor" or "mascate";
        var apron = name.Contains("Cozinh") || name.Contains("Agricult") || name.Contains("Costur") ? ":avental" : "";
        m.SetGear(packTool ? null : look switch
        {
            "ferreiro" when !name.Contains("Cozinh") && !name.Contains("Mineiro") => "hatchet",
            "ferreiro" when name.Contains("Mineiro") => "pick",
            "guarda" when role == "guarda" => "spear_watch",
            "capita" => "sword_ornate",
            "mentor" when name.Contains("Dorval") || name.Contains("Brutus") => "maul",
            "arcanista" => "staff_crystal",
            _ => null,
        }, null, $"npc:{role}:{(int)(R(1) * 6)}:{(fem ? "f" : "m")}{apron}");
        if (packHat) { }
        else if (role is "guarda" && name.Contains("Sentinela")) m.SetHead("head_iron");
        else if (role == "arcanista" || name.Contains("Espectral")) m.SetHead("head_hood");
        else if (role == "batedora") m.SetHead("head_hood_travel");
        // corpo: altura 0,9–1,1 (mulheres um pouco menores); ferreiros e cozinheiros mais largos; uns gordos, outros magros
        var height = (fem ? 0.93f : 1f) * (0.92f + 0.17f * R(2));
        var girth = 0.92f + 0.22f * R(3) + (name.Contains("Cozinh") || look == "ferreiro" ? 0.14f : 0f) + (look == "mentor" ? 0.08f : 0f);
        m.SetBuild(height, girth);
        var hairColors = new[] { new Color(0.12f, 0.09f, 0.07f), new Color(0.32f, 0.18f, 0.09f), new Color(0.55f, 0.40f, 0.22f),
            new Color(0.62f, 0.60f, 0.56f), new Color(0.40f, 0.12f, 0.06f) };
        var hair = hairColors[(int)(R(4) * 4.99f)];
        if (!packHat && !(role == "arcanista" || name.Contains("Espectral") || name.Contains("Sentinela") || role == "batedora"))
            m.SetHairStyle(fem ? (R(5) > 0.5f ? 1 : 2) : (R(5) > 0.25f ? 3 : 0), hair);
        if (name.Contains("Espectral") || name.Contains("alma")) m.SetGhost();
        // identidade: barba para metade dos homens sem pacote; alguns com marca própria
        if (!fem && !dressed && R(6) > 0.5f) m.SetBarbaCurta(hair);
        if (name.Contains("Tupã")) m.EnfeiteCabeca("cocar");
        else if (name.Contains("Wu")) { m.SetHairStyle(0, new Color(0.85f, 0.85f, 0.82f)); m.SetBarbaCurta(new Color(0.9f, 0.9f, 0.88f)); m.EnfeiteCabeca("careca"); }
        else if (name.Contains("Caronte")) m.SetHead("head_hood");
        else if (name.Contains("Brutus")) { m.SetBuild(1.08f, 1.22f); m.EnfeiteCabeca("careca"); }
        else if (name.Contains("Harald")) m.SetHead("head_iron");
        else if (name.Contains("Belmiro")) m.EnfeiteCabeca("chapeu");
        if (id.StartsWith("oficio_", StringComparison.Ordinal)) m.SetOficio(id[7..].Split('_')[0]);   // 13/11: NPCs de ofício (balão, dentista...)
        return m;
    }

    private static bool Titular(string pack, string name) => pack switch
    {
        "mentor" => name.Contains("Dorval"),
        "ferreiro" => name.Contains("Brás") || name.Contains("Gunnar"),
        "curandeira" => name.Contains("Ilma") || name.Contains("Odete"),
        "arcanista" => name.Contains("Isaura"),
        "capita" => name.Contains("Ondina"),
        "guarda" => name.Contains("Sargento") || name.Contains("Rui") || name.Contains("Sentinela"),
        _ => true,
    };

    /// <summary>Qual pacote de destaque (Models/npcs/npc_&lt;id&gt;.glb) combina com o NPC; null = nenhum.</summary>
    private static string? NpcPack(string look, string role, string name)
    {
        if (name.Contains("Pescador") || name.Contains("Barqueiro")) return "pescador";
        if (name.Contains("Agricult")) return "agricultora";
        if (name.Contains("Mineiro")) return "mineiro";
        if (name.Contains("Mascate")) return "mascate";
        if (name.Contains("Bobo")) return "bobo";
        if (name.Contains("Batedora")) return "batedora";
        if (name.Contains("Batedor")) return "batedor";
        if (name.Contains("Cozinh") || name.Contains("Espectral")) return null;
        return role switch
        {
            "ferreiro" => "ferreiro", "curandeira" => "curandeira", "capita" => "capita", "mentor" => "mentor",
            "arcanista" => "arcanista", "guarda" => "guarda", "bruxa" => "bruxa", "batedora" => "batedora", "bobo" => "bobo",
            _ => null,
        };
    }

    /// <summary>
    /// 12/11: Vacho, o moleque do barril — o NOSSO boneco padrão (manequim, tamanho de criança) dentro de um barril de
    /// verdade, da cintura para cima de fora; acena, olha para quem chega e afunda/espia de vez em quando.
    /// </summary>
    private static IEntityVisual Vacho()
    {
        var root = new Node3D { Name = "Vacho" };
        var madeira = VisualKit.Mat(new Color(0.45f, 0.29f, 0.15f), 0.85f);
        var aro = VisualKit.Mat(new Color(0.22f, 0.22f, 0.24f), 0.4f, 0.8f);
        // 13/11 (pedido: "o barril tem que ser oco"): aduelas de verdade em volta, por dentro mais escuro e um fundo — sem tampa
        var dentro = VisualKit.Mat(new Color(0.24f, 0.15f, 0.08f), 0.95f);
        const int aduelas = 18;
        for (var i = 0; i < aduelas; i++)
        {
            var a = i * Mathf.Tau / aduelas;
            var r = 0.345f;
            var largura = 2f * r * Mathf.Sin(Mathf.Pi / aduelas) + 0.012f;
            var tom = i % 3 == 0 ? VisualKit.Mat(new Color(0.40f, 0.26f, 0.13f), 0.85f) : madeira;
            VisualKit.Part(root, VisualKit.Box(largura, 0.95f, 0.035f), tom, new Vector3(Mathf.Sin(a) * r, 0.475f, Mathf.Cos(a) * r), new Vector3(0, Mathf.RadToDeg(a), 0));
            VisualKit.Part(root, VisualKit.Box(largura, 0.95f, 0.01f), dentro, new Vector3(Mathf.Sin(a) * (r - 0.022f), 0.475f, Mathf.Cos(a) * (r - 0.022f)), new Vector3(0, Mathf.RadToDeg(a), 0));
        }
        VisualKit.Part(root, VisualKit.Cylinder(0.33f, 0.33f, 0.03f, 18), dentro, new Vector3(0, 0.03f, 0));   // fundo
        foreach (var y in new[] { 0.12f, 0.48f, 0.84f })
            VisualKit.Part(root, new TorusMesh { InnerRadius = (y == 0.48f ? 0.355f : 0.345f) - 0.012f, OuterRadius = (y == 0.48f ? 0.355f : 0.345f) + 0.02f, Rings = 24 }, aro, new Vector3(0, y, 0));
        IEntityVisual? menino = UseMannequin ? MannequinVisual.TryCreate("npc", false) : null;
        if (menino is MannequinVisual mv)
        {
            mv.SetGear(null, null, "npc:aldeao:2:m");
            mv.SetHairStyle(3, new Color(0.25f, 0.14f, 0.07f));
            mv.SetBuild(0.62f, 0.92f);
        }
        menino ??= new HumanoidVisual(new HumanoidStyle { Scale = 0.62f, Cloth = new Color(0.62f, 0.52f, 0.30f), Accent = new Color(0.4f, 0.3f, 0.2f) });
        root.AddChild(menino.Root);
        return new BarrilVisual(root, menino);
    }

    /// <summary>Quadro de contratos: tábuas, dois postes, papéis pregados e um telhadinho.</summary>
    private static IEntityVisual NoticeBoard()
    {
        var root = new Node3D { Name = "Quadro" };
        var wood = VisualKit.Mat(new Color(0.36f, 0.24f, 0.14f), 0.9f);
        var dark = VisualKit.Mat(new Color(0.22f, 0.14f, 0.08f), 0.9f);
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(root, VisualKit.Box(0.12f, 2.1f, 0.12f), dark, new Vector3(side * 0.85f, 1.05f, 0));
        VisualKit.Part(root, VisualKit.Box(1.6f, 1.0f, 0.06f), wood, new Vector3(0, 1.35f, 0));
        VisualKit.Part(root, VisualKit.Box(1.9f, 0.06f, 0.4f), dark, new Vector3(0, 2.12f, 0.02f), new Vector3(-12, 0, 0));
        var paper = VisualKit.Mat(new Color(0.86f, 0.80f, 0.64f), 0.95f);
        var papers = new[] { (-0.5f, 1.55f, 4f), (-0.05f, 1.45f, -6f), (0.45f, 1.6f, 3f), (-0.35f, 1.12f, -3f), (0.3f, 1.15f, 8f) };
        foreach (var (x, y, rot) in papers)
        {
            VisualKit.Part(root, VisualKit.Box(0.32f, 0.38f, 0.01f), paper, new Vector3(x, y, 0.04f), new Vector3(0, 0, rot));
            VisualKit.Part(root, VisualKit.Sphere(0.018f), VisualKit.Mat(new Color(0.6f, 0.12f, 0.1f), 0.5f), new Vector3(x, y + 0.15f, 0.05f));
        }
        return new PropVisual(root, 2.2f);
    }

    /// <summary>NPC da cidade/acampamento (só visual; parado, animação de espera).</summary>
    public static HumanoidVisual Npc(string look)
    {
        var style = look switch
        {
            "ferreiro" => new HumanoidStyle { Cloth = new Color(0.30f, 0.21f, 0.14f), Accent = new Color(0.35f, 0.38f, 0.42f), Bulky = true, FixedWeapon = "hatchet" },
            "curandeira" => new HumanoidStyle { Cloth = new Color(0.33f, 0.21f, 0.38f), Accent = new Color(0.66f, 0.50f, 0.28f), Robe = true, Hood = true },
            "capita" => new HumanoidStyle { Cloth = new Color(0.39f, 0.23f, 0.28f), Accent = new Color(0.62f, 0.45f, 0.24f), FixedWeapon = "spear_watch" },
            "guarda" => new HumanoidStyle { Cloth = new Color(0.35f, 0.38f, 0.42f), Accent = new Color(0.39f, 0.23f, 0.28f), FixedWeapon = "spear_watch" },
            "mentor" => new HumanoidStyle { Cloth = new Color(0.24f, 0.25f, 0.28f), Accent = new Color(0.62f, 0.45f, 0.24f), Bulky = true, FixedWeapon = "maul" },
            "arcanista" => new HumanoidStyle { Cloth = new Color(0.30f, 0.17f, 0.38f), Accent = new Color(0.56f, 0.41f, 0.75f), Robe = true, Hood = true, CasterHands = true, Eyes = new Color(0.7f, 0.5f, 1f), EyesGlow = true },
            // Bobo da praça: roupa vermelha e amarela, capuz de guizos (chifrinhos dourados), um pouco curvado de tanto rir
            "bobo" => new HumanoidStyle { Cloth = new Color(0.70f, 0.12f, 0.12f), Accent = new Color(0.95f, 0.78f, 0.20f), Hood = true, Horns = true, Hunch = 0.12f, Scale = 0.92f },
            _ => new HumanoidStyle { Cloth = new Color(0.33f, 0.27f, 0.22f), Accent = new Color(0.39f, 0.23f, 0.28f), Hunch = 0.08f },
        };
        if (!style.EyesGlow) style.Eyes = new Color(0.1f, 0.1f, 0.12f);
        return new HumanoidVisual(style);
    }

    /// <summary>
    /// 13/11 (pacote do trabalhador, 8 montarias 3D com idle/walk/run): arquivo e altura da sela (m) de cada uma.
    /// Corcel = cavalo de guerra, lobo = lobo do norte; as outras seis também podem ser montadas (Laboratório).
    /// </summary>
    public static readonly Dictionary<string, (string File, float Sela, string Nome)> Montarias3D = new()
    {
        ["corcel"] = ("montaria_cavalo", 1.56f, "Cavalo de Guerra"), ["lobo"] = ("montaria_lobo", 1.31f, "Lobo do Norte"),
        ["urso"] = ("montaria_urso", 1.54f, "Urso de Guerra"), ["leao"] = ("montaria_leao", 1.38f, "Leão Dourado"),
        ["lince"] = ("montaria_lince", 1.30f, "Lince das Cinzas"), ["dinossauro"] = ("montaria_dinossauro", 1.35f, "Raptor de Guerra"),
        ["avestruz"] = ("montaria_avestruz", 1.45f, "Avestruz de Corrida"), ["condor"] = ("montaria_condor", 1.55f, "Condor Gigante"),
        ["javali"] = ("montaria_javali", 1.36f, "Javali de Guerra"), ["grifo"] = ("montaria_grifo", 1.40f, "Grifo"),
    };

    /// <summary>14/11 ("mais montarias"): variações tingidas dos modelos 3D (mesma animação, outra pelagem).</summary>
    public static readonly Dictionary<string, (string Base, Color Tinta)> VariacoesMontaria = new()
    {
        ["corcel_negro"] = ("corcel", new Color(0.32f, 0.26f, 0.28f)), ["lobo_sombrio"] = ("lobo", new Color(0.55f, 0.4f, 0.85f)),
        ["pantera"] = ("leao", new Color(0.2f, 0.2f, 0.24f)), ["raptor_rubro"] = ("dinossauro", new Color(1f, 0.42f, 0.38f)),
    };

    /// <summary>Altura que o cavaleiro sobe na montaria (sela − quadril do boneco); 1 m nas antigas.</summary>
    public static float AlturaSela(string kind)
    {
        if (VariacoesMontaria.TryGetValue(kind, out var v)) kind = v.Base;
        return Montarias3D.TryGetValue(kind, out var m) && Godot.FileAccess.FileExists($"res://Assets/Models/montarias/{m.File}.glb") ? m.Sela - 0.62f : 1.0f;
    }

    public static IEntityVisual Mount(string kind)
    {
        Color? tinta = null;
        if (VariacoesMontaria.TryGetValue(kind, out var v)) { kind = v.Base; tinta = v.Tinta; }
        return Montarias3D.TryGetValue(kind, out var m) && Glb("montarias/" + m.File, m.Sela, null, 1f, tinta) is { } glb ? glb : MountAntiga(kind);
    }

    /// <summary>Montaria (corcel de estrada: angular, sóbrio, arreios simples).</summary>
    public static BeastVisual MountAntiga(string kind) => kind switch
    {
        // 12/11: montarias por nível — lobo cinzento (25), javali de guerra (40), grifo dourado (55)
        "lobo" => new(new BeastStyle { Head = "lobo", Body = new Color(0.40f, 0.40f, 0.42f), Belly = new Color(0.62f, 0.60f, 0.58f), Eyes = new Color(0.95f, 0.75f, 0.25f),
            LegLength = 0.9f, BodyLength = 1.35f, BodyHeight = 0.55f, Saddle = true, Mane = true, Scale = 1.1f }),
        "javali" => new(new BeastStyle { Head = "javali", Body = new Color(0.30f, 0.20f, 0.15f), Belly = new Color(0.42f, 0.32f, 0.24f), Eyes = new Color(0.8f, 0.15f, 0.1f),
            LegLength = 0.8f, BodyLength = 1.4f, BodyHeight = 0.7f, Saddle = true, Mane = true, Scale = 1.2f }),
        "grifo" => new(new BeastStyle { Head = "leao", Body = new Color(0.78f, 0.60f, 0.28f), Belly = new Color(0.92f, 0.84f, 0.62f), Eyes = new Color(1f, 0.85f, 0.3f),
            LegLength = 1.0f, BodyLength = 1.45f, BodyHeight = 0.6f, Saddle = true, Mane = true, Scale = 1.25f }),
        _ => new(new BeastStyle
        {
            Head = "cavalo", Body = new Color(0.24f, 0.20f, 0.18f), Belly = new Color(0.36f, 0.30f, 0.26f),
            Eyes = new Color(0.1f, 0.1f, 0.1f), LegLength = 0.95f, BodyLength = 1.3f, BodyHeight = 0.55f, Saddle = true, Mane = true, Scale = 1.05f,
        }),
    };

    /// <summary>Companheiro: pequeno draco (pedra escura + bronze) ou fênix (brasa contida).</summary>
    public static IEntityVisual Pet(string kind) => PetGlbVisual.TryCreate(kind) is { } petNovo ? petNovo   // 14/11: pets novos (pets/fera_*.glb); sem o arquivo = visual antigo
        : kind == "vagalume" ? new FireflyVisual()
        // 14/11 ("mais pets"): morceguinho, lobinho, sapinho dourado e corvo
        : kind == "morcego" ? new FlyerVisual("morcego", 0.38f, new Color(0.18f, 0.14f, 0.16f), new Color(0.35f, 0.22f, 0.25f))
        : kind == "corvo" ? new FlyerVisual("morcego", 0.42f, new Color(0.06f, 0.06f, 0.08f), new Color(0.45f, 0.2f, 0.65f))
        : kind == "lobinho" ? new BeastVisual(new BeastStyle { Head = "lobo", Fur = true, Body = new Color(0.45f, 0.43f, 0.42f), Belly = new Color(0.7f, 0.68f, 0.64f), Eyes = new Color(0.95f, 0.8f, 0.3f), Scale = 0.4f })
        : kind == "sapinho" ? new BeastVisual(new BeastStyle { Head = "sapo", Body = new Color(0.95f, 0.75f, 0.2f), Eyes = new Color(0.1f, 0.1f, 0.1f), LegLength = 0.3f, BodyLength = 0.6f, BodyHeight = 0.5f, LongTail = false, Scale = 0.35f })
        // 13/11: mascotes 3D do pacote de arte (a águia fica de fora: ela voa no ombro com o desenho atual)
        : kind is "tartaruga" or "coelho" or "grilo" or "macaco" && Glb("pets/pet_" + kind, 0.4f) is { } petGlb ? petGlb
        : kind is "tartaruga" or "coelho" or "grilo" or "macaco" or "aguia" or "raposa" or "coruja" ? new MascoteVisual(kind)   // 12/11: mascotes novos
        : kind == "dragao_anciao" && UseMannequin && DracoVisual.TryCreate(1.45f) is { } anciao ? anciao   // 12/11: dragão de verdade (nível 55)
        : kind != "fenix" && UseMannequin && DracoVisual.TryCreate() is { } draco ? draco : FlyerPet(kind);

    private static FlyerVisual FlyerPet(string kind) => kind == "fenix"
        ? new FlyerVisual("fenix", 0.75f, new Color(1f, 0.45f, 0.12f), new Color(1f, 0.62f, 0.2f), glows: true)
        : new FlyerVisual("draco", 0.7f, new Color(0.30f, 0.40f, 0.30f), new Color(0.66f, 0.48f, 0.26f));

    private static IEntityVisual Hum(float scale, Color skin, Color cloth, Color accent, Color eyes, bool glowEyes = true, bool bulky = false,
        float hunch = 0, string? weapon = null, bool robe = false, bool hood = false, bool horns = false, bool crown = false, Color? bodyGlow = null,
        bool caster = false, Color? head = null, bool visualTusks = false, float girth = 0f)
        => Enemy(new HumanoidStyle
        {
            Scale = scale, Skin = skin, Cloth = cloth, Accent = accent, Eyes = eyes, EyesGlow = glowEyes, Bulky = bulky, Hunch = hunch,
            Tusks = visualTusks, Girth = girth,
            FixedWeapon = weapon, Robe = robe, Hood = hood, Horns = horns, Crown = crown, BodyGlow = bodyGlow, CasterHands = caster,
            LimbThickness = bulky ? 1f : 0.85f, HeadColor = head ?? new Color(0.74f, 0.67f, 0.55f),
        });

    /// <summary>Inimigo humanoide: o manequim do jogador com corpo e traços próprios; o boneco antigo só se o modelo faltar.</summary>
    private static IEntityVisual Enemy(HumanoidStyle style)
        => UseMannequin && MannequinVisual.Monster(style) is { } m ? m : new HumanoidVisual(style);

    /// <summary>Criaturas da expansão até o midgame (por visual_asset_id). null = não é deste grupo.</summary>
    private static IEntityVisual? Midgame(string visual, bool boss)
    {
        var bone = new Color(0.78f, 0.73f, 0.64f);
        var bordo = new Color(0.39f, 0.18f, 0.22f);
        var spectral = new Color(0.40f, 0.65f, 0.70f);
        switch (visual)
        {
            // 11/11: cão sarnento (missão do Bento): lobo pequeno, pelo marrom ralo, olho amarelado
            case "dog" when Glb("criaturas/cao_sarnento", 0.7f, BoarClips) is { } cao: return cao;   // 13/11: pacote do trabalhador
            case "goat":   // 13/11 ("a Cabra-da-Serra está como javali"): cabra — focinho comprido, chifres curvos, barbicha, pelo claro
                return new BeastVisual(new BeastStyle { Head = "cabra", Horns = false, Fur = true, LongTail = false, Body = new Color(0.72f, 0.66f, 0.56f),   // 14/11: cabeça de cabra com chifrões
                    Belly = new Color(0.9f, 0.86f, 0.78f), Accent = new Color(0.35f, 0.3f, 0.24f), Eyes = new Color(0.85f, 0.65f, 0.2f),
                    LegLength = 0.62f, BodyLength = 0.85f, BodyHeight = 0.4f, Scale = 0.85f });
            case "dog": return new BeastVisual(new BeastStyle { Spikes = false, Fur = true, Body = new Color(0.42f, 0.30f, 0.20f), Belly = new Color(0.60f, 0.50f, 0.38f),
                Eyes = new Color(0.95f, 0.8f, 0.3f), Scale = 0.72f });
            case "wolf": return new BeastVisual(new BeastStyle { Spikes = true, Body = new Color(0.28f, 0.27f, 0.27f), Scale = 1.0f });
            case "wolf_white": return new BeastVisual(new BeastStyle { Spikes = true, Body = new Color(0.82f, 0.82f, 0.84f), Eyes = new Color(0.4f, 0.8f, 1f), Scale = 1.15f });
            case "wolf_ice": return new BeastVisual(new BeastStyle { Spikes = true, Body = new Color(0.55f, 0.66f, 0.76f), Eyes = new Color(0.5f, 0.85f, 1f), Scale = 1.1f });
            case "boar" when Glb("criaturas/javali_da_duna", 1.1f, BoarClips) is { } boarGlb: return boarGlb;
            case "boar_big" when Glb("criaturas/javali_da_duna", 1.6f, BoarClips, 1.45f) is { } bigBoarGlb: return bigBoarGlb;
            case "boar": return new BeastVisual(new BeastStyle { Head = "javali", Tusks = true, Plates = true, Body = new Color(0.36f, 0.20f, 0.20f), LongTail = false, BodyHeight = 0.55f, Scale = 1.1f });
            case "boar_big": return new BeastVisual(new BeastStyle { Head = "javali", Tusks = true, Plates = true, Spikes = true, Body = new Color(0.30f, 0.16f, 0.17f), LongTail = false, BodyHeight = 0.6f, Scale = 1.6f });
            case "lizard": return new BeastVisual(new BeastStyle { Head = "lagarto", Body = new Color(0.35f, 0.14f, 0.08f), Eyes = new Color(1f, 0.6f, 0.1f), LegLength = 0.35f, BodyLength = 1.4f, Spikes = true, Scale = 1.2f });
            case "basilisk": return new BeastVisual(new BeastStyle { Head = "lagarto", Body = new Color(0.20f, 0.30f, 0.22f), Eyes = new Color(1f, 0.9f, 0.2f), LegLength = 0.4f, BodyLength = 1.8f, Spikes = true, Plates = true, Scale = 1.5f });
            case "frog": return new BeastVisual(new BeastStyle { Head = "sapo", Body = new Color(0.28f, 0.36f, 0.24f), LegLength = 0.3f, BodyLength = 0.6f, BodyHeight = 0.5f, LongTail = false, Scale = 1.0f });
            case "frog_gold": return new BeastVisual(new BeastStyle { Head = "sapo", Body = new Color(0.9f, 0.7f, 0.2f), LegLength = 0.3f, BodyLength = 0.6f, BodyHeight = 0.5f, LongTail = false, Scale = 0.9f });
            case "centaur": return new BeastVisual(new BeastStyle { Head = "cavalo", Rider = true, Body = new Color(0.48f, 0.36f, 0.22f), LegLength = 0.9f, BodyLength = 1.2f, BodyHeight = 0.5f, Scale = 1.1f });
            case "chimera": return new BeastVisual(new BeastStyle { Head = "leao", Horns = true, Spikes = true, Plates = true, Body = new Color(0.48f, 0.36f, 0.22f), Accent = bone, LegLength = 0.9f, BodyLength = 1.7f, BodyHeight = 0.8f, Scale = 2.0f });
            case "spider": return new InsectVisual("aranha", 0.95f, new Color(0.18f, 0.15f, 0.17f), bordo);
            case "spider_weaver": return new InsectVisual("aranha", 1.15f, new Color(0.22f, 0.20f, 0.26f), new Color(0.56f, 0.41f, 0.75f));
            case "spider_queen": return new InsectVisual("aranha", 1.9f, new Color(0.15f, 0.12f, 0.16f), bordo);
            case "scorpion": return new InsectVisual("escorpiao", 1.0f, new Color(0.45f, 0.30f, 0.18f), bone);
            case "scorpion_big": return new InsectVisual("escorpiao", 1.7f, new Color(0.35f, 0.20f, 0.14f), bone);
            case "beetle": return new InsectVisual("besouro", 1.1f, new Color(0.18f, 0.12f, 0.10f), new Color(1f, 0.4f, 0.1f));
            case "bat": return new FlyerVisual("morcego", 0.8f, new Color(0.15f, 0.13f, 0.14f), new Color(0.25f, 0.18f, 0.2f));
            case "bat_white": return new FlyerVisual("morcego", 1.0f, new Color(0.85f, 0.85f, 0.88f), new Color(0.75f, 0.72f, 0.75f));
            case "harpy": return new FlyerVisual("harpia", 1.1f, new Color(0.35f, 0.28f, 0.24f), new Color(0.48f, 0.36f, 0.22f));
            case "leech": return new LarvaVisual(1.1f);
            case "serpent": return new LarvaVisual(1.0f);
            case "raider": return Hum(1.08f, new Color(0.10f, 0.10f, 0.11f), bordo, new Color(0.35f, 0.30f, 0.28f), new Color(1f, 0.3f, 0.1f), hunch: 0.28f, weapon: "cleaver");
            case "root": return Hum(1.25f, new Color(0.25f, 0.18f, 0.12f), new Color(0.30f, 0.22f, 0.15f), new Color(0.40f, 0.48f, 0.30f), new Color(0.6f, 1f, 0.3f), hunch: 0.25f, head: new Color(0.30f, 0.22f, 0.15f));
            case "root_boss": return Hum(2.4f, new Color(0.22f, 0.16f, 0.11f), new Color(0.28f, 0.20f, 0.14f), new Color(0.40f, 0.48f, 0.30f), new Color(0.6f, 1f, 0.3f), bulky: true, hunch: 0.2f, horns: true, head: new Color(0.28f, 0.20f, 0.14f));
            case "skeleton": return Enemy(new HumanoidStyle { Scale = 1.0f, Skin = bone, Cloth = new Color(0.28f, 0.26f, 0.24f), Accent = bone, Eyes = new Color(0.4f, 0.85f, 1f), EyesGlow = true, FixedWeapon = "sword_p1_placeholder", Bone = true, Girth = 0.85f });
            case "ghost": return Hum(1.0f, spectral, spectral.Darkened(0.3f), spectral, new Color(0.7f, 1f, 1f), robe: true, hood: true, caster: true, bodyGlow: spectral.Darkened(0.4f));
            case "ghost_knight": return Hum(1.15f, spectral.Darkened(0.4f), new Color(0.24f, 0.26f, 0.30f), spectral, new Color(0.7f, 1f, 1f), bulky: true, weapon: "spear_watch", bodyGlow: spectral.Darkened(0.55f));
            case "knight": return Hum(1.25f, new Color(0.20f, 0.20f, 0.22f), new Color(0.30f, 0.31f, 0.34f), bordo, new Color(1f, 0.4f, 0.2f), bulky: true, weapon: "maul");
            case "bellguard": return Hum(2.2f, new Color(0.20f, 0.20f, 0.22f), new Color(0.30f, 0.31f, 0.34f), new Color(0.55f, 0.40f, 0.22f), new Color(1f, 0.6f, 0.2f), bulky: true, weapon: "maul", crown: true);
            case "troll": return Hum(1.6f, new Color(0.30f, 0.34f, 0.26f), new Color(0.30f, 0.22f, 0.16f), bone, new Color(1f, 0.8f, 0.2f), bulky: true, hunch: 0.3f, weapon: "club_driftwood", head: new Color(0.36f, 0.40f, 0.30f), visualTusks: true, girth: 1.3f);
            case "troll_ice": return Hum(1.7f, new Color(0.55f, 0.62f, 0.70f), new Color(0.40f, 0.42f, 0.48f), bone, new Color(0.5f, 0.85f, 1f), bulky: true, hunch: 0.3f, weapon: "club_driftwood", head: new Color(0.62f, 0.68f, 0.75f), visualTusks: true, girth: 1.3f);
            case "giant": return Hum(2.3f, new Color(0.72f, 0.70f, 0.68f), new Color(0.42f, 0.36f, 0.30f), bone, new Color(0.5f, 0.85f, 1f), bulky: true, hunch: 0.15f, weapon: "maul", head: new Color(0.76f, 0.74f, 0.72f), girth: 1.25f);
            case "giant_boss": return Hum(3.0f, new Color(0.70f, 0.72f, 0.78f), new Color(0.35f, 0.38f, 0.45f), new Color(0.2f, 0.2f, 0.22f), new Color(0.5f, 0.85f, 1f), bulky: true, hunch: 0.1f, weapon: "maul", crown: true, girth: 1.25f);
            case "draugr": return Hum(1.15f, new Color(0.36f, 0.42f, 0.48f), new Color(0.26f, 0.28f, 0.32f), new Color(0.55f, 0.58f, 0.62f), new Color(0.5f, 0.85f, 1f), weapon: "axe_quarry", head: new Color(0.45f, 0.52f, 0.58f));
            case "draugr_king": return Hum(2.2f, new Color(0.32f, 0.38f, 0.45f), new Color(0.22f, 0.24f, 0.28f), new Color(0.62f, 0.45f, 0.24f), new Color(0.5f, 0.9f, 1f), bulky: true, weapon: "axe_quarry", crown: true, horns: true);
            case "drowned": return Hum(1.0f, new Color(0.28f, 0.38f, 0.38f), new Color(0.20f, 0.28f, 0.28f), spectral, new Color(0.6f, 1f, 0.9f), hunch: 0.2f, head: new Color(0.45f, 0.55f, 0.52f));
            case "ghoul": return Hum(1.0f, new Color(0.40f, 0.38f, 0.36f), new Color(0.22f, 0.20f, 0.20f), bone, new Color(1f, 0.85f, 0.3f), hunch: 0.45f, head: new Color(0.48f, 0.45f, 0.42f));
            case "necro": return Hum(1.05f, new Color(0.18f, 0.16f, 0.20f), new Color(0.24f, 0.14f, 0.30f), new Color(0.56f, 0.41f, 0.75f), new Color(0.75f, 0.5f, 1f), robe: true, hood: true, caster: true);
            case "necro_boss": return Hum(2.0f, new Color(0.18f, 0.16f, 0.20f), new Color(0.24f, 0.14f, 0.30f), new Color(0.56f, 0.41f, 0.75f), new Color(0.75f, 0.5f, 1f), robe: true, hood: true, caster: true, crown: true, bulky: true);
            case "charred": return Hum(1.1f, new Color(0.08f, 0.07f, 0.07f), new Color(0.18f, 0.10f, 0.08f), new Color(1f, 0.4f, 0.1f), new Color(1f, 0.5f, 0.1f), hunch: 0.15f, weapon: "hammer_foreman");
            case "elemental": return Hum(1.3f, new Color(0.9f, 0.35f, 0.08f), new Color(0.3f, 0.12f, 0.06f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.9f, 0.4f), caster: true, bodyGlow: new Color(0.9f, 0.32f, 0.06f), head: new Color(1f, 0.5f, 0.15f));
            case "elemental_boss": return Hum(2.4f, new Color(0.9f, 0.35f, 0.08f), new Color(0.3f, 0.12f, 0.06f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.9f, 0.4f), bulky: true, caster: true, bodyGlow: new Color(1f, 0.38f, 0.08f), crown: true);
            case "golem": return Hum(1.8f, new Color(0.22f, 0.21f, 0.21f), new Color(0.30f, 0.28f, 0.27f), new Color(1f, 0.4f, 0.1f), new Color(1f, 0.5f, 0.1f), bulky: true, hunch: 0.1f, head: new Color(0.28f, 0.27f, 0.27f));
            case "golem_boss": return Hum(3.0f, new Color(0.20f, 0.19f, 0.19f), new Color(0.28f, 0.26f, 0.25f), new Color(1f, 0.4f, 0.1f), new Color(1f, 0.5f, 0.1f), bulky: true, crown: true, head: new Color(0.26f, 0.25f, 0.25f));
            case "bronze_guard": return Hum(1.35f, new Color(0.48f, 0.36f, 0.22f), new Color(0.42f, 0.32f, 0.20f), bordo, new Color(1f, 0.7f, 0.3f), bulky: true, weapon: "spear_watch", head: new Color(0.55f, 0.42f, 0.25f));
            case "minotaur": return Hum(1.6f, new Color(0.30f, 0.22f, 0.18f), new Color(0.35f, 0.25f, 0.18f), bone, new Color(1f, 0.3f, 0.1f), bulky: true, hunch: 0.25f, horns: true, weapon: "axe_quarry", head: new Color(0.32f, 0.24f, 0.2f));
            case "minotaur_big": return Hum(2.4f, new Color(0.28f, 0.20f, 0.16f), new Color(0.32f, 0.22f, 0.16f), bone, new Color(1f, 0.3f, 0.1f), bulky: true, hunch: 0.25f, horns: true, weapon: "axe_quarry", head: new Color(0.30f, 0.22f, 0.18f));
        }
        return null;
    }

    /// <summary>
    /// Bestiário pedido pelo usuário (09/11): rato, meleca, planta carnívora, saci, anões, lobisomem, zumbi, chupa-cabra,
    /// múmia, verme, alien, sapo gigante, vampiro, a Morte, wendigo, ogro da montanha, demônio, cão de três cabeças,
    /// dragão, anjo e gênio. Humanoides saem do manequim (corpo, cabeça e traços próprios); bichos dos kits de criatura.
    /// </summary>
    private static IEntityVisual? Bestiary(string visual, bool boss)
    {
        var bone = new Color(0.78f, 0.73f, 0.64f);
        switch (visual)
        {
            // 11/11 noite (pacote do trabalhador): modelos esculpidos; o de código fica de reserva
            case "rat" when Glb("criaturas/rato_do_cais", 0.5f, BoarClips) is { } rato: return rato;
            case "rat_big" when Glb("criaturas/rato_gigante", 0.9f, BoarClips) is { } ratao: return ratao;
            case "rat": return new BeastVisual(new BeastStyle { Body = new Color(0.30f, 0.27f, 0.25f), Belly = new Color(0.55f, 0.48f, 0.44f), Eyes = new Color(1f, 0.15f, 0.1f),
                Fur = true, LegLength = 0.18f, BodyLength = 0.8f, BodyHeight = 0.3f, Scale = 0.6f, Head = "rato" });
            case "rat_big": return new BeastVisual(new BeastStyle { Body = new Color(0.22f, 0.20f, 0.19f), Belly = new Color(0.50f, 0.42f, 0.40f), Eyes = new Color(1f, 0.15f, 0.1f),
                Fur = true, Tusks = true, LegLength = 0.22f, BodyLength = 0.95f, BodyHeight = 0.34f, Scale = 1.15f, Head = "rato" });
            case "slime" when Glb("criaturas/meleca", 0.7f) is { } mel: return mel;   // 13/11
            case "slime": return new SlimeVisual(1.0f, new Color(0.45f, 0.85f, 0.35f));
            case "slime_bog": return new SlimeVisual(1.35f, new Color(0.40f, 0.42f, 0.20f));
            case "slime_big": return new SlimeVisual(2.2f, new Color(0.55f, 0.25f, 0.75f));
            case "plant": return new PlantVisual(1.0f, new Color(0.24f, 0.45f, 0.18f), new Color(0.72f, 0.14f, 0.16f));
            case "plant_bog": return new PlantVisual(1.3f, new Color(0.22f, 0.30f, 0.16f), new Color(0.55f, 0.20f, 0.45f));
            case "worm_giant": return new LarvaVisual(1.9f, new Color(0.62f, 0.42f, 0.40f), new Color(0.42f, 0.26f, 0.24f));
            case "frog_giant": return new BeastVisual(new BeastStyle { Head = "sapo", Body = new Color(0.30f, 0.42f, 0.22f), Belly = new Color(0.75f, 0.70f, 0.45f),
                Eyes = new Color(1f, 0.8f, 0.1f), LegLength = 0.16f, BodyLength = 0.7f, BodyHeight = 0.75f, LongTail = false, Scale = 1.9f });
            case "chupacabra": return new BeastVisual(new BeastStyle { Head = "lagarto", Body = new Color(0.30f, 0.33f, 0.30f), Belly = new Color(0.45f, 0.45f, 0.40f),
                Eyes = new Color(1f, 0.1f, 0.1f), Spikes = true, Fur = true, Tusks = true, LegLength = 0.6f, BodyLength = 0.9f, BodyHeight = 0.38f, Scale = 1.1f });
            case "cerberus" when Glb("criaturas/cao_tres_cabecas", 2.2f, BoarClips) is { } cer: return cer;   // 12/11: modelo do pacote Vila
            case "cerberus": return new BeastVisual(new BeastStyle { Heads = 3, Body = new Color(0.12f, 0.10f, 0.10f), Belly = new Color(0.25f, 0.15f, 0.12f),
                Eyes = new Color(1f, 0.45f, 0.05f), Spikes = true, Mane = true, LegLength = 0.75f, BodyLength = 1.4f, BodyHeight = 0.6f, Scale = boss ? 2.2f : 1.6f });
            case "dragon": return (UseMannequin ? DracoVisual.TryCreate(boss ? 3.2f : 2.4f, monster: true) : null)
                ?? (IEntityVisual)new FlyerVisual("draco", boss ? 2.4f : 1.8f, new Color(0.45f, 0.12f, 0.08f), new Color(0.66f, 0.48f, 0.26f));
            case "saci" when MannequinVisual.MonsterBody("humanoides/mo_saci", 1.3f) is { } saci: return saci;
            case "dwarf" when MannequinVisual.MonsterBody("humanoides/mo_anao_minerador", 1.2f) is { } anao: return anao;
            case "saci":   // diabrete das matas: pequeno, ágil, gorro vermelho, olhos acesos, rodeado de fumaça
                return Enemy(new HumanoidStyle { Scale = 0.72f, Skin = new Color(0.18f, 0.12f, 0.10f), HeadColor = new Color(0.22f, 0.15f, 0.12f), Cloth = new Color(0.62f, 0.10f, 0.08f),
                    Accent = new Color(0.62f, 0.10f, 0.08f), Eyes = new Color(1f, 0.75f, 0.2f), EyesGlow = true, Hood = true, Hunch = 0.1f, LimbThickness = 0.8f, Girth = 0.8f });
            case "dwarf":
                return Enemy(new HumanoidStyle { Scale = 0.74f, Skin = new Color(0.70f, 0.52f, 0.40f), HeadColor = new Color(0.74f, 0.56f, 0.44f), Cloth = new Color(0.40f, 0.30f, 0.22f),
                    Accent = new Color(0.55f, 0.56f, 0.58f), Eyes = new Color(0.1f, 0.1f, 0.1f), Bulky = true, Beard = true, Girth = 1.35f, FixedWeapon = "axe_quarry", HeadScale = 1.15f, Nose = true });
            case "dwarf_mountain":
                return Enemy(new HumanoidStyle { Scale = 0.8f, Skin = new Color(0.66f, 0.50f, 0.40f), HeadColor = new Color(0.70f, 0.54f, 0.42f), Cloth = new Color(0.30f, 0.34f, 0.40f),
                    Accent = new Color(0.62f, 0.45f, 0.24f), Eyes = new Color(0.4f, 0.85f, 1f), EyesGlow = true, Bulky = true, Beard = true, Horns = true, Girth = 1.4f,
                    FixedWeapon = "hammer_foreman", HeadScale = 1.15f, Nose = true });
            case "werewolf":
                return Enemy(new HumanoidStyle { Scale = 1.35f, Skin = new Color(0.30f, 0.24f, 0.20f), HeadColor = new Color(0.34f, 0.27f, 0.22f), Cloth = new Color(0.22f, 0.18f, 0.15f),
                    Accent = bone, Eyes = new Color(1f, 0.85f, 0.2f), EyesGlow = true, Hunch = 0.42f, Ears = true, Nose = true, Tusks = true, Girth = 1.25f, Bulky = true, LimbThickness = 1.05f });
            case "zombie":
                return Enemy(new HumanoidStyle { Scale = 1.0f, Skin = new Color(0.36f, 0.42f, 0.32f), HeadColor = new Color(0.42f, 0.48f, 0.36f), Cloth = new Color(0.26f, 0.24f, 0.22f),
                    Accent = new Color(0.40f, 0.18f, 0.16f), Eyes = new Color(0.9f, 0.95f, 0.5f), EyesGlow = true, Hunch = 0.38f, Girth = 0.92f });
            case "mummy":
                return Enemy(new HumanoidStyle { Scale = 1.05f, Skin = new Color(0.72f, 0.66f, 0.52f), HeadColor = new Color(0.76f, 0.70f, 0.55f), Cloth = new Color(0.66f, 0.60f, 0.46f),
                    Accent = new Color(0.62f, 0.45f, 0.24f), Eyes = new Color(0.3f, 1f, 0.6f), EyesGlow = true, Hunch = 0.2f, Girth = 0.88f, Robe = true });
            case "alien":
                return Enemy(new HumanoidStyle { Scale = 0.95f, Skin = new Color(0.55f, 0.60f, 0.62f), HeadColor = new Color(0.60f, 0.66f, 0.68f), Cloth = new Color(0.30f, 0.34f, 0.38f),
                    Accent = new Color(0.3f, 1f, 0.6f), Eyes = new Color(0.2f, 1f, 0.5f), EyesGlow = true, HeadScale = 1.65f, Girth = 0.72f, LimbThickness = 0.7f, CasterHands = true });
            // 13/11 ("mobs femininas: harpias, vampiras, esfinge, medusa"): corpo feminino do manequim + traços próprios
            case "harpy_f":
                return Enemy(new HumanoidStyle { Female = true, Scale = 1.0f, Skin = new Color(0.62f, 0.52f, 0.44f), HeadColor = new Color(0.66f, 0.56f, 0.48f),
                    Cloth = new Color(0.36f, 0.26f, 0.18f), Accent = new Color(0.55f, 0.42f, 0.26f), Eyes = new Color(1f, 0.75f, 0.15f), EyesGlow = true,
                    HairColor = new Color(0.16f, 0.12f, 0.1f), Wings = "aguia", Talons = true, Hunch = 0.18f, Girth = 0.82f, LimbThickness = 0.72f });
            case "medusa":
                return Enemy(new HumanoidStyle { Female = true, Scale = 1.12f, Skin = new Color(0.30f, 0.42f, 0.28f), HeadColor = new Color(0.34f, 0.46f, 0.30f),
                    Cloth = new Color(0.62f, 0.56f, 0.40f), Accent = new Color(0.85f, 0.66f, 0.28f), Eyes = new Color(1f, 0.9f, 0.1f), EyesGlow = true,
                    SnakeHair = true, Girth = 0.86f });
            case "vampire_f":
                return Enemy(new HumanoidStyle { Female = true, Scale = 1.04f, Skin = new Color(0.80f, 0.78f, 0.80f), HeadColor = new Color(0.88f, 0.86f, 0.88f),
                    Cloth = new Color(0.32f, 0.03f, 0.06f), Accent = new Color(0.08f, 0.06f, 0.08f), Eyes = new Color(1f, 0.1f, 0.15f), EyesGlow = true,
                    HairColor = new Color(0.06f, 0.05f, 0.06f), Cape = true, Tusks = true, Girth = 0.84f, Wings = "abismo" });
            case "sphinx":
                return Enemy(new HumanoidStyle { Female = true, Scale = 1.6f, Skin = new Color(0.78f, 0.60f, 0.36f), HeadColor = new Color(0.82f, 0.64f, 0.40f),
                    Cloth = new Color(0.12f, 0.20f, 0.52f), Accent = new Color(0.9f, 0.7f, 0.25f), Eyes = new Color(0.3f, 0.8f, 1f), EyesGlow = true,
                    Nemes = true, Wings = "aguia", Bulky = true, Girth = 1.05f, Talons = true });
            case "vampire":
                return Enemy(new HumanoidStyle { Scale = 1.08f, Skin = new Color(0.78f, 0.76f, 0.78f), HeadColor = new Color(0.86f, 0.84f, 0.86f), Cloth = new Color(0.10f, 0.08f, 0.10f),
                    Accent = new Color(0.55f, 0.06f, 0.10f), Eyes = new Color(1f, 0.1f, 0.15f), EyesGlow = true, Cape = true, Ears = true, Tusks = true, Girth = 0.9f, FixedWeapon = "sword_ornate" });
            case "death":   // a Morte: manto preto com capuz, ossos, olhos de brasa azul, foice longa
                return Enemy(new HumanoidStyle { Scale = boss ? 2.0f : 1.4f, Skin = bone, Cloth = new Color(0.05f, 0.05f, 0.06f), Accent = new Color(0.20f, 0.20f, 0.22f),
                    Eyes = new Color(0.4f, 0.8f, 1f), EyesGlow = true, Bone = true, Robe = true, Hood = true, Hunch = 0.12f, Girth = 0.8f, FixedWeapon = "spear_watch",
                    BodyGlow = new Color(0.05f, 0.08f, 0.12f) });
            case "wendigo":   // magro e alto, chifres de cervo, pele pálida colada ao osso
                return Enemy(new HumanoidStyle { Scale = 1.55f, Skin = new Color(0.62f, 0.60f, 0.56f), HeadColor = bone, Cloth = new Color(0.30f, 0.26f, 0.22f), Accent = bone,
                    Eyes = new Color(0.6f, 0.95f, 1f), EyesGlow = true, Horns = true, Hunch = 0.35f, Girth = 0.68f, LimbThickness = 0.7f, Bone = true });
            case "ogre":
                return Enemy(new HumanoidStyle { Scale = 1.9f, Skin = new Color(0.52f, 0.44f, 0.34f), HeadColor = new Color(0.56f, 0.48f, 0.38f), Cloth = new Color(0.34f, 0.26f, 0.18f),
                    Accent = new Color(0.45f, 0.45f, 0.48f), Eyes = new Color(1f, 0.6f, 0.2f), Bulky = true, Tusks = true, Nose = true, Hunch = 0.22f, Girth = 1.45f,
                    FixedWeapon = "club_driftwood", HeadScale = 0.9f });
            case "demon":
                return Enemy(new HumanoidStyle { Scale = boss ? 2.4f : 1.5f, Skin = new Color(0.45f, 0.08f, 0.06f), HeadColor = new Color(0.50f, 0.10f, 0.07f), Cloth = new Color(0.10f, 0.06f, 0.05f),
                    Accent = new Color(0.12f, 0.10f, 0.10f), Eyes = new Color(1f, 0.85f, 0.2f), EyesGlow = true, Horns = true, Tusks = true, Bulky = true, Girth = 1.2f,
                    FixedWeapon = "cleaver", BodyGlow = new Color(0.35f, 0.06f, 0.02f) });
            case "angel":   // guardião alado: veste branca, armadura dourada, brilho, coroa (auréola)
                return Enemy(new HumanoidStyle { Scale = 1.3f, Skin = new Color(0.86f, 0.80f, 0.70f), HeadColor = new Color(0.90f, 0.84f, 0.74f), Cloth = new Color(0.90f, 0.88f, 0.82f),
                    Accent = new Color(0.95f, 0.78f, 0.35f), Eyes = new Color(1f, 0.95f, 0.7f), EyesGlow = true, Robe = true, Crown = true, Cape = true, FixedWeapon = "spear_watch",
                    BodyGlow = new Color(0.45f, 0.40f, 0.25f) });
            case "genie":   // gênio: pele azul, mãos acesas, manto de seda, turbante (capuz)
                return Enemy(new HumanoidStyle { Scale = 1.5f, Skin = new Color(0.20f, 0.40f, 0.85f), HeadColor = new Color(0.24f, 0.46f, 0.92f), Cloth = new Color(0.45f, 0.18f, 0.55f),
                    Accent = new Color(0.95f, 0.78f, 0.35f), Eyes = new Color(1f, 0.9f, 0.4f), EyesGlow = true, Robe = true, Hood = true, CasterHands = true, Bulky = true, Beard = true,
                    BodyGlow = new Color(0.10f, 0.20f, 0.55f) });
        }
        return null;
    }

    /// <summary>Criatura animada do lote de arte; null se o arquivo faltar (cai no visual antigo).</summary>
    /// <summary>14/11: multiplica a cor de todas as malhas (titãs).</summary>
    private static void Tingir(Node3D raiz, Color c)
    {
        foreach (var n in raiz.FindChildren("*", nameof(MeshInstance3D), true, false))
            if (n is MeshInstance3D mi && mi.Mesh is not null)
                for (var i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                    if ((mi.GetSurfaceOverrideMaterial(i) ?? mi.Mesh.SurfaceGetMaterial(i)) is StandardMaterial3D sm)
                    {
                        var copia = (StandardMaterial3D)sm.Duplicate();
                        copia.AlbedoColor = sm.AlbedoColor * c;
                        mi.SetSurfaceOverrideMaterial(i, copia);
                    }
    }

    private static IEntityVisual? Glb(string file, float height, Func<string, string, string?>? clips = null, float scale = 1f, Color? tint = null)
        => Godot.FileAccess.FileExists("res://Assets/Models/" + file + ".glb") ? new GlbCreatureVisual(file, height, scale, clips, tint) : null;

    /// <summary>Lote 3 (criaturas/*.glb, texturas PBR, uma cor por espécie); se faltar, o do lote 2 (cr_*.glb).</summary>
    private static IEntityVisual? Crab(string name, float height, Func<string, string, string?>? clips = null, float scale = 1f, Color? tint = null)
        => Glb("criaturas/" + name, height, clips, scale, tint) ?? Glb("cr_" + name, height, clips, scale, tint);

    /// <summary>Javali (lote 3): investida e carga usam os clipes próprios.</summary>
    private static string? BoarClips(string phase, string atk)
        => atk.Contains("investida") || atk.Contains("carga") || atk.Contains("charge") || atk.Contains("disparada")
            ? phase switch { "windup" => "charge_windup", "active" => "charge", _ => null } : null;

    /// <summary>Rei das Pinças (lote 3): varridas usam o "sweep"; a fúria da maré começa com o urro da 2ª fase.</summary>
    private static string? KingClips(string phase, string atk)
    {
        if (atk.Contains("varrida") || atk.Contains("lateral") || atk.Contains("volta") || atk.Contains("sweep"))
            return phase == "active" ? "sweep" : null;
        if (atk.Contains("furia")) return phase == "windup" ? "phase2" : null;
        return CrabClips(phase, atk);
    }

    /// <summary>Ataque do servidor → clipe próprio da espécie (o resto usa attack_windup/strike/recover).</summary>
    private static string? CrabClips(string phase, string atk)
    {
        // Ermitão: entra no barril (defesa de frente), investe rolando e sai do barril (brecha)
        if (atk == "barril_explosivo")
            return phase switch { "windup" => "barrel_throw_windup", "active" => "barrel_throw", _ => null };
        if (atk == "investida_barril")
            return phase switch { "windup" => "hide", "active" => "ram_strike", "recover" => "unhide", _ => null };
        // Pedra: fecha a carapaça (quase imune), solta os espinhos e levanta exposto
        if (atk == "chuva_de_espinhos")
            return phase switch { "windup" => "hunker_down", "active" => "spike_volley", "recover" => "rise_exposed", _ => null };
        // Geodo: fecha em pedra (como o de Pedra) e arremessa geodos (como a areia)
        if (atk == "casca_de_pedra")
            return phase switch { "windup" => "hunker_down", "active" => "spike_volley", "recover" => "rise_exposed", _ => null };
        if (atk.Contains("areia") || atk.Contains("conchas") || atk.Contains("geodo"))
            return phase switch { "windup" => "sand_windup", "active" => "sand_throw", _ => null };
        if (atk is "bote" or "investida" or "carga" || atk.Contains("ram"))
            return phase switch { "windup" => "ram_windup", "active" => "ram_strike", _ => null };
        if (atk.Contains("lateral") || atk.Contains("varrida") || atk.Contains("sweep"))
            return phase == "active" ? "sweep" : null;
        if (atk.Contains("espinho") || atk.Contains("spike"))
            return phase is "windup" or "active" ? "spike_volley" : null;
        if (atk.Contains("emerg") || atk.Contains("escav"))
            return phase switch { "windup" => "burrow", "active" => "emerge_attack", "recover" => "emerge_dazed", _ => null };
        return null;
    }

    private static string? TyrantClips(string phase, string atk)
    {
        if (atk.Contains("guarda"))
            return phase switch { "windup" => "guard_start", "active" => "guard_end", "recover" => "smash_recover", _ => null };
        if (atk.Contains("mare_furiosa"))
            return phase switch { "windup" => "roar", "active" => "smash_strike", "recover" => "smash_recover", _ => null };
        var sweep = atk.Contains("varrida") || atk.Contains("lateral") || atk.Contains("volta") || atk.Contains("sweep");
        var pre = sweep ? "sweep" : "smash";
        return phase switch { "windup" => pre + "_windup", "active" => pre + "_strike", "recover" => pre + "_recover", _ => null };
    }

    /// <summary>
    /// 11/11 ("goblins como variações de skin por enquanto"): cada goblin sorteia (pelo id dele, sempre igual) entre o
    /// boneco de formas simples e o modelo novo no corpo do personagem (mo_goblin_*). skin: 0 = sorteia, 1 = simples, 2 = modelo.
    /// </summary>
    private static bool GoblinModelSkin(string seed, int skin)
    {
        if (skin != 0) return skin == 2;
        var h = 17;
        foreach (var ch in seed) h = h * 31 + ch;
        return (h & 1) == 0;
    }

    /// <summary>Fantasma de Calcário: poeira translúcida (alpha, sem cull), como pede o guia do trabalhador.</summary>
    private static void Translucido(Node n, float alpha)
    {
        if (n is MeshInstance3D mi && mi.Mesh is { } mesh)
            for (var i = 0; i < mesh.GetSurfaceCount(); i++)
                if ((mi.GetSurfaceOverrideMaterial(i) ?? mesh.SurfaceGetMaterial(i)) is BaseMaterial3D bm)
                {
                    var m = (BaseMaterial3D)bm.Duplicate();
                    m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                    m.AlbedoColor = m.AlbedoColor with { A = Mathf.Min(m.AlbedoColor.A, alpha) };
                    mi.SetSurfaceOverrideMaterial(i, m);
                }
        foreach (var c in n.GetChildren()) Translucido(c, alpha);
    }

    public static IEntityVisual Monster(string definitionId, bool boss, bool rare, string visual = "", string seed = "", int skin = 0)
    {
        if (visual == "boneco_treino") return new BonecoTreinoVisual();   // 13/11: boneco do Mestre Dorval (apanha e balança)
        if (MonstrosNovos.Criar(visual, boss, seed, definitionId) is { } novo) return novo;   // 14/11: monstros novos sem emenda (Visuals/MonstrosNovos.cs, Assets/Models/monstros/catalogo.json)
        if (visual.StartsWith("fab_", StringComparison.Ordinal) && FabulaVisuals.Criar(visual, boss) is { } fab) return fab;   // 13/11: Era Uma Vez e arredores
        // 14/11 (dono: "dois monstros ultra gigantes, impossíveis de matar"): os titãs — o golem de cal com 13 m (pedra escura e
        // brasa no peito) e o dragão com 8× o tamanho, em brasa
        if (UseMannequin && visual == "tita_colosso" && MannequinVisual.MonsterBody("humanoides/mo_golem_cal_viva", 13f) is { } colosso)
        {
            Tingir(colosso.Root, new Color(0.45f, 0.42f, 0.5f));
            colosso.Root.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.5f, 0.2f), LightEnergy = 4f, OmniRange = 14f, Position = new Vector3(0, 8f, 1.5f), ShadowEnabled = false });
            return colosso;
        }
        if (UseMannequin && visual == "tita_magma" && DracoVisual.TryCreate(8f, monster: true) is { } magma)
        {
            Tingir(magma.Root, new Color(1f, 0.55f, 0.35f));
            magma.Root.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.45f, 0.15f), LightEnergy = 5f, OmniRange = 18f, Position = new Vector3(0, 4f, 0), ShadowEnabled = false });
            return magma;
        }
        if (Bestiary(visual, boss) is { } bv) return bv;
        if (Midgame(visual, boss) is { } mg) return mg;
        if (UseMannequin && visual is "goblin" or "goblin_archer" or "goblin_chief" && GoblinModelSkin(seed, skin))
        {
            var (file, height) = visual switch
            {
                "goblin" => ("humanoides/mo_goblin_saqueador", 1.23f),
                "goblin_archer" => ("humanoides/mo_goblin_arqueiro", 1.19f),
                _ => ("humanoides/mo_goblin_chefe", 1.79f),
            };
            if (MannequinVisual.MonsterBody(file, height) is { } body) return body;
        }
        // 12/11: Cael, o Rebelde — marinheiro de verdade (corpo de gente, chapéu de capitão, gibão de couro, sabre), não o saqueador
        if (UseMannequin && visual == "cael_rebelde" && NpcVisual("capita", "cael_rebelde", "Cael, o Rebelde") is { } cael) return cael;
        // 11/11 (pacote da noite): mortos de cal no corpo do manequim (mesmo esqueleto: usam as animações que já existem)
        if (UseMannequin && visual is "carnical_cal" or "fantasma_cal")
        {
            var ghost = visual == "fantasma_cal";
            if (MannequinVisual.MonsterBody(ghost ? "humanoides/mo_fantasma_cal" : "humanoides/mo_carnical_cal", ghost ? 1.8f : 1.75f) is { } dead)
            {
                if (ghost) Translucido(dead.Root, 0.55f);
                return dead;
            }
            visual = ghost ? "ghost" : "zombie";   // sem o modelo: boneco de formas simples
        }
        // 11/11 (pacote "Monstros e Armaduras"): mais quatro no corpo do manequim, e dois com esqueleto próprio
        if (UseMannequin && visual is "rastejante_gesso" or "gargulito" or "golem_cal_viva" or "espantalho")
        {
            var (mfile, mh) = visual switch
            {
                "rastejante_gesso" => ("humanoides/mo_rastejante_gesso", 1.4f), "gargulito" => ("humanoides/mo_gargulito", 1.1f),
                // 12/11 ("o chefe da Galeria está pequeno, deveria ser grande"): como chefe, o golem tem 4,3 m
                "golem_cal_viva" => ("humanoides/mo_golem_cal_viva", boss ? 4.3f : 2.6f), _ => ("humanoides/mo_espantalho", 1.9f),
            };
            if (MannequinVisual.MonsterBody(mfile, mh) is { } mbody)
            {
                if (visual == "golem_cal_viva")   // cal viva quente: brilho laranja no peito
                    mbody.Root.AddChild(new OmniLight3D { LightColor = new Color("#ff8a20"), LightEnergy = boss ? 1.6f : 0.8f, OmniRange = boss ? 7f : 4f, Position = new Vector3(0, boss ? 2.7f : 1.6f, 0.3f), ShadowEnabled = false });
                return mbody;
            }
            visual = visual switch { "golem_cal_viva" => "guardian", "espantalho" => "zombie", _ => "goblin" };
        }
        if (visual == "verme_broca" && Glb("criaturas/verme_broca", 1.2f, null, 0.5f) is { } verme) return verme;
        if (visual == "inseto_lodo" && Glb("criaturas/inseto_lodo", 0.5f, null, 0.35f) is { } inseto) return inseto;
        switch (visual)
        {
            // goblins: baixinhos verdes, curvados, olhos amarelos; o chefe é maior, com ombreiras e machado
            case "goblin":
                return Enemy(new HumanoidStyle
                {
                    Scale = 0.72f, Skin = new Color(0.30f, 0.42f, 0.20f), HeadColor = new Color(0.36f, 0.50f, 0.24f), Cloth = new Color(0.36f, 0.26f, 0.16f),
                    Accent = new Color(0.45f, 0.30f, 0.18f), Eyes = new Color(1f, 0.85f, 0.2f), EyesGlow = true, Hunch = 0.32f, FixedWeapon = "hatchet",
                    LimbThickness = 0.9f, Ears = true, Nose = true, HeadScale = 1.4f,
                });
            // esqueleto de goblin: o mesmo goblin, só osso (pele cor de osso), trapos escuros, olhos azuis acesos, machadinha
            // 10/11: gnomo do areal — baixinho, gorro pontudo vermelho, barba branca, atira pedrinhas
            // 16/11 (dono: "coloca no game as variantes do esqueleto de gnomo, um que usamos por um bom tempo, e também do gnomo
            // do areal com roupa azul"): metade do bando usa o gnomo de túnica azul (modelo novo do catálogo) e a outra metade
            // o gnomo pequeno no nosso esqueleto (mo_gnomo_areal) — ver "manter_antigo" do gnome em monstros/catalogo.json
            case "gnome" when MannequinVisual.MonsterBody("humanoides/mo_gnomo_areal", 0.62f) is { } gnomo:
                gnomo.SetBuild(0.65f, 0.68f);   // 12/11: "deixa ele menorzinho" — gnomo na altura da cintura do jogador
                return gnomo;
            case "gnome":
                return Enemy(new HumanoidStyle
                {
                    Scale = 0.52f, Skin = new Color(0.78f, 0.60f, 0.48f), HeadColor = new Color(0.86f, 0.68f, 0.55f), Cloth = new Color(0.62f, 0.14f, 0.12f),
                    Accent = new Color(0.30f, 0.45f, 0.62f), Eyes = new Color(0.1f, 0.1f, 0.1f), Hood = true, Beard = true, Nose = true, HeadScale = 1.35f,
                    Girth = 1.1f, Hunch = 0.12f, CasterHands = false,
                });
            case "goblin_skeleton":
                return Enemy(new HumanoidStyle
                {
                    Scale = 0.7f, Skin = new Color(0.40f, 0.38f, 0.33f), Cloth = new Color(0.20f, 0.17f, 0.14f), Accent = new Color(0.35f, 0.32f, 0.28f),
                    Eyes = new Color(0.35f, 0.85f, 1f), EyesGlow = true, Hunch = 0.38f, FixedWeapon = "hatchet", Ears = true, HeadScale = 1.4f, Girth = 0.82f, Bone = true,
                });
            // lote 3: Arqueiro e Chefe com modelo próprio (já vestidos e armados); o goblin pequeno e o esqueleto ficam como
            // estão (pedido do usuário: ele gosta deles)
            case "goblin_archer":
                return Enemy(new HumanoidStyle
                {
                    Scale = 0.7f, Skin = new Color(0.28f, 0.40f, 0.22f), HeadColor = new Color(0.34f, 0.48f, 0.26f), Cloth = new Color(0.24f, 0.30f, 0.18f),
                    Accent = new Color(0.40f, 0.28f, 0.16f), Eyes = new Color(1f, 0.85f, 0.2f), EyesGlow = true, Hunch = 0.25f, Hood = true,
                    FixedWeapon = "bow_short", LimbThickness = 0.85f, Ears = true, Nose = true, HeadScale = 1.4f,
                });
            case "goblin_chief":
                return Enemy(new HumanoidStyle
                {
                    Scale = 1.05f, Skin = new Color(0.26f, 0.38f, 0.18f), HeadColor = new Color(0.32f, 0.45f, 0.22f), Cloth = new Color(0.39f, 0.18f, 0.16f),
                    Accent = new Color(0.55f, 0.50f, 0.42f), Eyes = new Color(1f, 0.4f, 0.1f), EyesGlow = true, Hunch = 0.2f, Bulky = true, Horns = true,
                    FixedWeapon = "axe_quarry", Ears = true, Tusks = true, HeadScale = 1.2f,
                });
            case "eye_winged" when Glb("criaturas/olho_alado", 1.9f, null, boss ? 1.8f : 1f) is { } olho: return olho;   // 13/11
            case "eye_winged": return new EyeVisual(boss ? 1.8f : 1f);
            // família de caranguejos do lote 2 de arte (modelos animados do usuário); o antigo de formas simples fica de reserva
            case "crab_small": return Crab("caranguejinho", 0.55f) ?? new CrabVisual(0.62f, golden: false);
            // 13/11 (fatia vertical: a Caranguejo-Mãe, primeiro chefe da Costa, era do mesmo tamanho dos filhotes): chefe = 2,2× e mais escura
            case "monster_crab_placeholder" when boss || definitionId == "monster_costa_caranguejo_mae":
                return Crab("caranguejo_de_cal", 2.0f, CrabClips, 1f, new Color(0.82f, 0.78f, 0.72f)) ?? new CrabVisual(2.2f, golden: false);
            case "monster_crab_placeholder": return Crab("caranguejo_de_cal", 0.9f, CrabClips) ?? new CrabVisual(1f, golden: false);
            // 13/11 ("trocar os 5 visuais provisórios"): os nomes "placeholder" já tinham modelo, menos quem os usava com outro id —
            // o Ermitão da Serra caía no boneco de formas simples; agora todos usam o modelo pelo visual
            case "monster_raider_placeholder" when MannequinVisual.MonsterBody("humanoides/mo_saqueador_vigilia", definitionId == "monster_monte_ermitao" ? 1.75f : 1.85f) is { } rp:
                if (definitionId == "monster_monte_ermitao") rp.SetBuild(0.95f, 0.85f);
                return rp;
            case "monster_acolyte_placeholder" when MannequinVisual.MonsterBody("humanoides/mo_acolito_cinzas", 1.8f) is { } ap: return ap;
            case "monster_foreman_placeholder" when MannequinVisual.MonsterBody("humanoides/mo_capataz_pedreira", 2.0f) is { } fp: return fp;
            // raro: o Caranguejo de Cal pintado de ouro (lote 3 não tem um próprio)
            case "monster_crab_gold_placeholder" when Crab("caranguejo_de_cal", 0.9f, CrabClips, 0.9f, new Color(1.25f, 0.95f, 0.35f)) is { } gold: return gold;
            case "crab_armored": return Crab("caranguejo_pinca_de_ferro", 1.6f, CrabClips) ?? new CrabVisual(1.35f, golden: false, tint: new Color(0.35f, 0.33f, 0.38f));
            case "crab_stone": return Crab("caranguejo_de_pedra", 1.3f, CrabClips) ?? new CrabVisual(1.3f, golden: false, tint: new Color(0.45f, 0.45f, 0.42f));
            case "crab_geode": return new GeodeCrabVisual(Crab("caranguejo_de_pedra", 1.5f, CrabClips, 1f, new Color(0.62f, 0.55f, 0.72f))
                                                          ?? new CrabVisual(1.5f, golden: false, tint: new Color(0.42f, 0.36f, 0.5f)));
            case "crab_hermit": return Crab("caranguejo_ermitao_barril", 1.2f, CrabClips) ?? new CrabVisual(1.2f, golden: false);
            case "crab_digger": return Crab("caranguejo_escavador", 1.4f, CrabClips) ?? new CrabVisual(1.5f, golden: false);
            case "crab_tyrant" when boss && Glb("criaturas/tirano_da_mare", 4.2f, TyrantClips, 1.35f) is { } tir: return tir;   // 13/11: PBR novo
            case "crab_tyrant": return Glb("cr_tirano_da_mare", 2.8f, TyrantClips) ?? new CrabVisual(2.6f, golden: false);
            case "crab_king": return Glb("criaturas/rei_das_pincas", 2.0f, KingClips) ?? new CrabVisual(2.3f, golden: false, tint: new Color(0.75f, 0.35f, 0.25f));
            case "crab_larva" when Glb("criaturas/larva_de_cal", 0.35f) is { } larva: return larva;
            case "crab_venom" when Crab("rainha_das_cascas", 1.3f, CrabClips) is { } rainha: return rainha;
            case "raider_digger" when MannequinVisual.MonsterBody("humanoides/mo_escavador", 1.75f) is { } esc: return esc;
            case "sentinel" when MannequinVisual.MonsterBody("humanoides/mo_sentinela", 2.4f) is { } sen: return sen;
            case "guardian" when MannequinVisual.MonsterBody("humanoides/mo_guardiao_galeria", 3.6f) is { } gua: return gua;
            case "crab_larva": return new LarvaVisual(0.9f);
            case "crab_venom": return new CrabVisual(1.9f, golden: false, tint: new Color(0.35f, 0.55f, 0.25f));
            case "raider_digger":
                return Enemy(new HumanoidStyle
                {
                    Scale = 1.05f, Skin = new Color(0.22f, 0.20f, 0.18f), Cloth = new Color(0.40f, 0.33f, 0.22f),
                    Accent = new Color(0.4f, 0.85f, 1f), Eyes = new Color(0.4f, 0.85f, 1f), EyesGlow = true, Hunch = 0.3f, FixedWeapon = "pick",
                });
            case "sentinel":
                return Enemy(new HumanoidStyle
                {
                    Scale = 1.4f, Skin = new Color(0.70f, 0.68f, 0.62f), Cloth = new Color(0.55f, 0.53f, 0.48f),
                    Accent = new Color(0.4f, 0.85f, 1f), Eyes = new Color(0.4f, 0.85f, 1f), EyesGlow = true, Bulky = true, FixedWeapon = "maul",
                });
            case "guardian":
                return Enemy(new HumanoidStyle
                {
                    Scale = 2.2f, Skin = new Color(0.62f, 0.62f, 0.60f), Cloth = new Color(0.25f, 0.45f, 0.52f),
                    Accent = new Color(0.4f, 0.85f, 1f), Eyes = new Color(0.4f, 0.9f, 1f), EyesGlow = true, Bulky = true, Hunch = 0.1f,
                    FixedWeapon = "spear_watch",
                });
        }
        switch (definitionId)
        {
            case "monster_r1_saqueador_da_vigilia" when MannequinVisual.MonsterBody("humanoides/mo_saqueador_vigilia", 1.85f) is { } saq: return saq;
            case "monster_r1_acolito_das_cinzas" when MannequinVisual.MonsterBody("humanoides/mo_acolito_cinzas", 1.8f) is { } aco: return aco;
            case "monster_r1_capataz_da_pedreira" when MannequinVisual.MonsterBody("humanoides/mo_capataz_pedreira", 2.0f) is { } cap: return cap;
            case "monster_r1_saqueador_da_vigilia":
                return Enemy(new HumanoidStyle
                {
                    Scale = 1.08f,
                    Skin = new Color(0.16f, 0.14f, 0.14f),     // boneco escuro com capa bordô desfiada (prancha 04)
                    Cloth = new Color(0.39f, 0.18f, 0.22f),
                    Cape = true,
                    Accent = new Color(0.35f, 0.30f, 0.28f),
                    Eyes = new Color(1f, 0.3f, 0.1f),
                    EyesGlow = true,
                    Hunch = 0.28f,
                    FixedWeapon = "cleaver",
                });
            case "monster_r1_acolito_das_cinzas":
                return Enemy(new HumanoidStyle
                {
                    Scale = 1.04f,
                    Skin = new Color(0.62f, 0.60f, 0.58f),
                    Cloth = new Color(0.36f, 0.35f, 0.34f),
                    Accent = new Color(0.75f, 0.35f, 0.12f),
                    Eyes = new Color(1f, 0.55f, 0.15f),
                    EyesGlow = true,
                    Robe = true,
                    Hood = true,
                    CasterHands = true,
                });
            case "monster_r1_capataz_da_pedreira":
                return Enemy(new HumanoidStyle
                {
                    Scale = 2.0f,
                    Skin = new Color(0.20f, 0.19f, 0.18f),
                    Cloth = new Color(0.30f, 0.20f, 0.14f),
                    Accent = new Color(0.45f, 0.40f, 0.36f),
                    Eyes = new Color(1f, 0.2f, 0.05f),
                    EyesGlow = true,
                    Bulky = true,
                    Hunch = 0.12f,
                    FixedWeapon = "maul",
                });
            case "monster_r1_caranguejo_dourado":
                return new CrabVisual(0.85f, golden: true);
            default:
                return new CrabVisual(boss ? 2f : 1f, golden: rare);
        }
    }
}

/// <summary>
/// Marca no chão do ataque em preparação (vermelha, cada vez mais forte), desenhada a partir dos dados do servidor
/// (AimX/AimY/AimRadius/AimArc). Leque = golpe frontal; círculo = área; faixa = bote.
/// </summary>
public sealed class TelegraphView
{
    private readonly MeshInstance3D _fill, _edge;
    private readonly StandardMaterial3D _fillMat, _edgeMat;
    private string _key = "";

    public Node3D Root { get; } = new() { Name = "Marca" };

    // 13/11 ("deixa a marcação do ataque no chão mais AAA, está muito sólida"): o preenchimento virou um shader —
    // quase transparente no meio, mais forte na borda, uma frente que avança do monstro para fora com a preparação,
    // listras finas correndo para fora e um pouco de ruído; no golpe acende vermelho e some suave
    private const string MarcaShader = @"
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled;
uniform float raio = 3.0;
uniform float progresso = 0.0;
uniform float ativo = 0.0;
uniform float linha = 0.0;
varying vec3 local;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
void vertex() { local = VERTEX; }
void fragment() {
    float d = mix(length(local.xz), local.z, linha) / max(raio, 0.01);   // 0 no monstro, 1 na borda
    float frente = smoothstep(progresso + 0.02, progresso - 0.04, d);   // parte já ""carregada""
    float borda = smoothstep(0.86, 1.0, d) * smoothstep(1.02, 0.97, d);
    float listras = 0.5 + 0.5 * sin(d * 34.0 - TIME * 7.0);
    float ruido = hash(floor(local.xz * 6.0) + floor(TIME * 8.0)) * 0.15;
    float base = 0.05 + 0.10 * d;
    float a = base + frente * (0.10 + 0.12 * listras * d) + borda * (0.35 + 0.5 * progresso) + ruido * frente * 0.3;
    vec3 cor = mix(vec3(1.0, 0.55, 0.35), vec3(1.0, 0.12, 0.05), progresso);
    cor = mix(cor, vec3(1.0, 0.05, 0.02), ativo);
    a = mix(a, 0.35 + 0.45 * borda + 0.15 * listras, ativo);
    ALBEDO = cor * (1.0 + borda * 0.6);
    ALPHA = clamp(a, 0.0, 0.92);
}";
    private readonly ShaderMaterial _shader = new() { Shader = new Shader { Code = MarcaShader } };

    public TelegraphView()
    {
        _fillMat = VisualKit.Unshaded(new Color(1f, 0.15f, 0.08f, 0.2f));
        _edgeMat = VisualKit.Unshaded(new Color(1f, 0.35f, 0.15f, 0.6f), additive: true);
        // 13/11 ("a marcação vermelha do ataque fica dentro da terra nos morros"): sobe para a altura do chão no centro e é
        // desenhada por cima do relevo (sem teste de profundidade), então a parte que cai sobre a rampa continua visível
        _fillMat.NoDepthTest = true; _edgeMat.NoDepthTest = true;
        _fill = new MeshInstance3D { MaterialOverride = _shader, Position = new Vector3(0, 0.04f, 0) };
        _edge = new MeshInstance3D { MaterialOverride = _edgeMat, Position = new Vector3(0, 0.05f, 0) };
        Root.AddChild(_fill);
        Root.AddChild(_edge);
        Root.Visible = false;
    }

    private readonly List<(MeshInstance3D Lane, MeshInstance3D Core)> _rays = new();
    private static StandardMaterial3D? _laneMat, _coreMat;

    /// <summary>
    /// 10/11 ("3 pedras: mostrar 3 riscos e até onde vão; arte mais sutil"): um risco por projétil, largo como o acerto
    /// (bola + miolo do corpo), do monstro até o fim do voo — faixa quase transparente e um fio claro no meio que
    /// "corre" do monstro para fora conforme a preparação termina.
    /// </summary>
    private void ShowRays(int count, float spreadDeg, float radius, float hitWidth, float progress, bool active)
    {
        _fill.Visible = _edge.Visible = false;
        if (_laneMat is null) { _laneMat = VisualKit.Unshaded(new Color(1f, 0.25f, 0.12f, 0.12f)); _laneMat.NoDepthTest = true; }
        if (_coreMat is null) { _coreMat = VisualKit.Unshaded(new Color(1f, 0.55f, 0.3f, 0.7f), additive: true); _coreMat.NoDepthTest = true; }
        while (_rays.Count < count)
        {
            var lane = new MeshInstance3D { MaterialOverride = _laneMat, Position = new Vector3(0, 0.04f, 0) };
            var core = new MeshInstance3D { MaterialOverride = _coreMat, Position = new Vector3(0, 0.05f, 0) };
            Root.AddChild(lane); Root.AddChild(core);
            _rays.Add((lane, core));
        }
        for (var i = 0; i < _rays.Count; i++)
        {
            var (lane, core) = _rays[i];
            var on = i < count;
            lane.Visible = core.Visible = on;
            if (!on) continue;
            var off = count == 1 ? 0f : (i / (float)(count - 1) - 0.5f) * spreadDeg;
            // o servidor gira para a esquerda com ângulo positivo no plano (x, y); aqui o eixo é (x, z) com yaw invertido
            var rot = new Vector3(0, Mathf.DegToRad(-off), 0);
            lane.Mesh = new PlaneMesh { Size = new Vector2(hitWidth, radius), CenterOffset = new Vector3(0, 0, radius / 2f) };
            lane.Rotation = rot;
            var len = active ? radius : Mathf.Max(0.3f, radius * progress);
            core.Mesh = new PlaneMesh { Size = new Vector2(0.06f, len), CenterOffset = new Vector3(0, 0, len / 2f) };
            core.Rotation = rot;
        }
        _laneMat.AlbedoColor = new Color(1f, 0.25f, 0.12f, active ? 0.22f : 0.06f + 0.12f * progress);
        _coreMat.AlbedoColor = new Color(1f, 0.55f, 0.3f, active ? 0.9f : 0.35f + 0.45f * progress);
    }

    /// <summary>shape: arc &gt; 0 leque, arc == 0 círculo, arc &lt; 0 faixa. progress 0..1 da preparação. kind "raiosN:r" = N projéteis.</summary>
    public void Show(Vector3 at, float yaw, float radius, float arc, float progress, bool active, string kind = "")
    {
        if (radius <= 0.05f) { Root.Visible = false; return; }
        if (kind.StartsWith("raios", StringComparison.Ordinal))
        {
            var parts = kind[5..].Split(':');
            var n = int.TryParse(parts[0], out var c) ? Math.Max(1, c) : 1;
            var r = parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rr) ? rr : 0.3f;
            Root.Visible = true;
            Root.Position = at + new Vector3(0, EscadaCasa.Altura(at.X, at.Z), 0);
            Root.Rotation = new Vector3(0, yaw, 0);
            ShowRays(n, arc, radius, 2f * (r + 0.22f), progress, active);
            foreach (var (f, e, nn) in _extras) f.Visible = e.Visible = nn.Visible = false;
            if (_num is not null) _num.Visible = false;
            _key = "";
            return;
        }
        foreach (var (lane, core) in _rays) lane.Visible = core.Visible = false;
        _fill.Visible = _edge.Visible = true;
        // 13/11: "golpesN:passo:feitos" — uma marca por golpe que ainda falta, cada uma um passo à frente da outra
        var golpes = 0; var passo = 0f; var feitos = 0;
        if (kind.StartsWith("golpes", StringComparison.Ordinal))
        {
            var gp = kind[6..].Split(':');
            int.TryParse(gp[0], out golpes);
            if (gp.Length > 1) float.TryParse(gp[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out passo);
            if (gp.Length > 2) int.TryParse(gp[2], out feitos);
        }
        var key = $"{radius:0.00}|{arc:0.0}";
        if (key != _key)
        {
            _key = key;
            if (arc > 0f)
            {
                _fill.Mesh = VisualKit.Sector(radius, arc);
                _edge.Mesh = VisualKit.Sector(radius, arc, 28, radius - 0.08f);
            }
            else if (arc == 0f)
            {
                _fill.Mesh = VisualKit.Sector(radius, 360f, 40);
                _edge.Mesh = VisualKit.Ring(radius, 0.1f);
            }
            else
            {
                var width = -arc;
                _fill.Mesh = new PlaneMesh { Size = new Vector2(width, radius), CenterOffset = new Vector3(0, 0, radius / 2f) };
                _edge.Mesh = new PlaneMesh { Size = new Vector2(width + 0.1f, 0.12f), CenterOffset = new Vector3(0, 0, radius) };
            }
        }
        Root.Visible = true;
        Root.Position = at + new Vector3(0, EscadaCasa.Altura(at.X, at.Z), 0);
        Root.Rotation = new Vector3(0, yaw, 0);
        // 10/11 ("arte do ataque mais sutil, estilo AAA"): área quase transparente que enche com a preparação e borda fina
        // 11/11 noite (pedido: "o arco fica mais claro na preparação e TOTALMENTE vermelho NA hora do golpe, para dar o
        // tempo da esquiva"): preparação = vermelho claro e fraco que vai esquentando; golpe = vermelho cheio e forte
        var k = Mathf.Pow(Mathf.Clamp(progress, 0f, 1f), 1.4f);
        var fill = active ? new Color(0.95f, 0.04f, 0.02f, 0.62f) : new Color(1f, 0.72f - 0.42f * k, 0.66f - 0.48f * k, 0.06f + 0.16f * k);
        _fillMat.AlbedoColor = fill;
        _shader.SetShaderParameter("raio", radius);
        _shader.SetShaderParameter("progresso", Mathf.Clamp(progress, 0f, 1f));
        _shader.SetShaderParameter("ativo", active ? 1f : 0f);
        _shader.SetShaderParameter("linha", arc < 0f ? 1f : 0f);
        _edgeMat.AlbedoColor = active ? new Color(1f, 0.15f, 0.08f, 0.85f) : new Color(1f, 0.8f - 0.4f * k, 0.75f - 0.5f * k, 0.12f + 0.35f * k);
        // a borda "fecha" do centro para fora conforme a preparação avança (dá a contagem do tempo)
        _edge.Scale = arc == 0f && !active ? Vector3.One * Mathf.Max(0.15f, progress) : Vector3.One;
        var faltam = golpes > 1 && passo > 0f ? Math.Max(0, golpes - feitos - 1) : 0;
        while (_extras.Count < faltam)
        {
            var f = new MeshInstance3D { MaterialOverride = _shaderFantasma, Position = new Vector3(0, 0.035f, 0) };
            var e = new MeshInstance3D { MaterialOverride = _edgeMat, Position = new Vector3(0, 0.045f, 0) };
            var n = new Label3D { FontSize = 64, OutlineSize = 14, PixelSize = 0.006f, Modulate = new Color(1f, 0.85f, 0.7f, 0.9f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled, RotationDegrees = new Vector3(-90, 180, 0), NoDepthTest = true };
            Root.AddChild(f); Root.AddChild(e); Root.AddChild(n);
            _extras.Add((f, e, n));
        }
        _shaderFantasma.SetShaderParameter("raio", radius);
        _shaderFantasma.SetShaderParameter("progresso", Mathf.Clamp(progress, 0f, 1f) * 0.7f);
        _shaderFantasma.SetShaderParameter("linha", arc < 0f ? 1f : 0f);
        for (var i = 0; i < _extras.Count; i++)
        {
            var (f, e, n) = _extras[i];
            var on = i < faltam;
            f.Visible = e.Visible = n.Visible = on;
            if (!on) continue;
            f.Mesh = _fill.Mesh; e.Mesh = _edge.Mesh;
            var z = (i + 1) * passo;
            f.Position = new Vector3(0, 0.035f - 0.002f * i, z);
            e.Position = new Vector3(0, 0.045f, z);
            n.Text = (feitos + i + 2).ToString();
            n.Position = new Vector3(0, 0.06f, z + radius * 0.55f);
        }
        if (golpes > 1 && passo > 0f) { _num ??= NovoNumero(); _num.Visible = true; _num.Text = (feitos + 1).ToString(); _num.Position = new Vector3(0, 0.06f, radius * 0.55f); }
        else if (_num is not null) _num.Visible = false;
    }

    private readonly List<(MeshInstance3D Fill, MeshInstance3D Edge, Label3D Num)> _extras = new();
    private readonly ShaderMaterial _shaderFantasma = new() { Shader = new Shader { Code = MarcaShader } };
    private Label3D? _num;
    private Label3D NovoNumero()
    {
        var n = new Label3D { FontSize = 72, OutlineSize = 14, PixelSize = 0.006f, Modulate = new Color(1f, 0.9f, 0.75f, 1f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled, RotationDegrees = new Vector3(-90, 180, 0), NoDepthTest = true };
        Root.AddChild(n);
        return n;
    }

    public void Hide() => Root.Visible = false;
}

/// <summary>Objeto parado no lugar de um NPC (quadro de contratos).</summary>
/// <summary>Vacho no barril: o barril balança de leve; o menino (manequim) sobe e desce lá dentro, espiando, e segue os gestos do NPC.</summary>
public sealed class BarrilVisual : IEntityVisual
{
    private float _t;
    private readonly IEntityVisual _menino;
    private const float Fundo = 0.5f;    // pés dentro do barril: da cintura para cima aparece
    public BarrilVisual(Node3D root, IEntityVisual menino) { Root = root; _menino = menino; }
    public Node3D Root { get; }
    public float Height => 1.4f;
    public void Update(in VisualFrame frame, float delta)
    {
        // 13/11 ("o pé do Vacho atravessa o barril: deixa ele PARADO lá dentro, grudado, mexendo com o barril; às vezes abaixa e
        // se esconde — sem sair nenhum pedaço do corpo"): o menino nunca anda nem faz pose (velocidade 0, sem ação); fica no
        // centro, pés acima do fundo. A cada ~10 s ele se esconde: afunda e se encolhe até a cabeça ficar abaixo da borda
        // (borda 0,95 m; pés nunca abaixo de 0,04 m), espera e volta espiando.
        _t += delta;
        var ciclo = Mathf.PosMod(_t, 10f);
        float esconde = ciclo switch
        {
            < 6.5f => 0f,
            < 7.3f => Mathf.SmoothStep(0f, 1f, (ciclo - 6.5f) / 0.8f),
            < 8.8f => 1f,
            _ => 1f - Mathf.SmoothStep(0f, 1f, (ciclo - 8.8f) / 1.2f),
        };
        var espia = 0.012f * Mathf.Sin(_t * 6f) * (1f - esconde);
        _menino.Root.Position = new Vector3(0, Mathf.Lerp(Fundo, 0.05f, esconde) + espia, 0);
        _menino.Root.Scale = new Vector3(1f, Mathf.Lerp(1f, 0.74f, esconde), 1f);
        var parado = frame;
        parado.Speed = 0f;
        parado.Action = "";
        _menino.Update(parado, delta);
        // ele mexe o barril: balança mais quando está escondido (se ajeitando lá dentro)
        Root.Rotation = Root.Rotation with { Z = (0.03f + 0.03f * esconde) * Mathf.Sin(_t * 2.3f), X = 0.015f * esconde * Mathf.Sin(_t * 3.1f) };
    }
    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) => _menino.Hit(fromDirection);
    public void Dispose() => Root.QueueFree();
}

public sealed class PropVisual : IEntityVisual
{
    public PropVisual(Node3D root, float height) { Root = root; Height = height; }
    public Node3D Root { get; }
    public float Height { get; }
    public void Update(in VisualFrame frame, float delta) { }
    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) { }
    public void Dispose() => Root.QueueFree();
}

/// <summary>13/11: boneco de treino que apanha — balança para trás a cada golpe e volta devagar (mola).</summary>
public sealed class BonecoTreinoVisual : IEntityVisual
{
    private readonly Node3D _corpo;
    private Vector3 _tombo;   // eixo * ângulo
    private float _vel;
    private float _ang;
    public BonecoTreinoVisual()
    {
        Root = new Node3D { Name = "BonecoTreino" };
        _corpo = new Node3D();
        Root.AddChild(_corpo);
        MapKit.TryBuild(null!, _corpo, "boneco_treino", VisualKit.Mat(new Color(0.45f, 0.30f, 0.18f), 0.9f));
    }
    public Node3D Root { get; }
    public float Height => 2.1f;
    public void Update(in VisualFrame frame, float delta)
    {
        // mola amortecida: o ângulo volta a zero balançando
        _vel += (-60f * _ang - 5f * _vel) * delta;
        _ang += _vel * delta;
        if (_tombo.LengthSquared() > 1e-6f) _corpo.Basis = new Basis(_tombo.Normalized(), _ang);
    }
    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection)
    {
        var d = fromDirection; d.Y = 0;
        if (d.LengthSquared() < 1e-4f) d = Vector3.Forward;
        _tombo = Vector3.Up.Cross(d.Normalized());
        _vel += 3.2f;
    }
    public void Dispose() => Root.QueueFree();
}
