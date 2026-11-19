using System;
using System.Linq;
using System.Collections.Generic;
using CinzasDaMare.Client.Visuals;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Ícones de item "de verdade" (estilo MU/Diablo): cada modelo 3D do jogo (arma, elmo, escudo, armadura vestida no
/// manequim, poção, tomo, material) é fotografado uma vez numa cena à parte (SubViewport com luz de estúdio) e vira uma
/// textura guardada. Um por quadro, em segundo plano; enquanto não fica pronto, a mochila desenha o ícone simples.
/// </summary>
public sealed partial class ItemIconRenderer : Node
{
    private static ItemIconRenderer? _instance;
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static readonly Queue<(string Key, ItemInfo? Info, string Definition)> Pending = new();
    private static readonly HashSet<string> Requested = new();
    /// <summary>Um ícone novo ficou pronto (as células redesenham).</summary>
    public static event Action? IconReady;

    private SubViewport _viewport = null!;
    private Node3D _stage = null!;
    private Camera3D _camera = null!;
    private string? _current;
    private int _wait;

    public static void Init(Node parent)
    {
        if (_instance is not null) return;
        _instance = new ItemIconRenderer { Name = "IconesDeItem" };
        parent.AddChild(_instance);
    }

    /// <summary>17/11 noite: variante de skin/asa/capa ("asas_fogo__guarda") usa o ícone da base.</summary>
    public static string BaseDaVariante(string id) => id.IndexOf("__", StringComparison.Ordinal) is var i and > 0 ? id[..i] : id;

    public static string KeyFor(ItemInfo? info, string definition) => KeyForBase(info, BaseDaVariante(definition));

    private static string KeyForBase(ItemInfo? info, string definition)
        // 11/11 ("tudo virou flor"): materiais dividem o mesmo modelo genérico (mat_ash) — a chave era o modelo, então o
        // primeiro ícone pintado (a erva) ficava para todos. Material agora tem chave pelo próprio id.
        // 15/11 ("cada poção com frasco e cor diferente — está tudo com o mesmo ícone"): as poções dividem o modelo "potion"
        // / "potion_mana", e a primeira foto ficava para todas; quem tem desenho próprio tem chave pelo próprio id
        => definition.StartsWith("tomo_") ? "tome_" + TomeClass(definition)
            : HasOwnIcon(definition) || Visuals.IconesUnicos.Tem(definition) || Visuals.GearFactory.LotTwoDrop(info?.Model, definition) is not null || definition.StartsWith("anel_") || definition.StartsWith("colar_") ? definition
            // 17/11 (regra: ícone único por item): só equipamento divide foto pelo modelo; apito/montaria/pet etc. pelo id
            : definition.StartsWith("apito_") || definition.StartsWith("montaria_") || definition.StartsWith("pet_") ? definition
            : info?.Model is { Length: > 0 } m && !m.StartsWith("mat_") && info.Category != "material" ? m : definition;

    /// <summary>Textura pronta do item (null = ainda não; pede para fotografar).</summary>
    public static Texture2D? Get(ItemInfo? info, string definition)
    {
        definition = BaseDaVariante(definition);
        var key = KeyFor(info, definition);
        if (Cache.TryGetValue(key, out var tex)) return tex;
        // 19/11 (dono: "altera o ícone de todas as skins — versões diferentes usando a mesma caveira"): o bracelete mostra o
        // ROSTO da própria skin (retrato tirado do boneco: vitrine "retratos", Tools/gerar_icones_skins.sh)
        if (definition.StartsWith("bracelete_", StringComparison.Ordinal) && VisualAssets.Load("ico_" + definition + ".png") is { } retrato)
        {
            Cache[key] = retrato;
            return retrato;
        }
        // ícone pintado (ico_*.png) quando existe: mais bonito que a foto do modelo
        // 17/11 noite (dono: "não era pra mexer no que já tava daora — os ícones novos parecem massinha; só a joia da brasa
        // ficou boa"): a arte PINTADA volta a valer para todos. Itens que dividem o mesmo desenho (13 plantas na "erva") ficam
        // diferentes pela COR do próprio desenho (Tingido) — o dono da arte (ArteDona) mantém o original
        if (!definition.StartsWith("tomo_") && (!HasOwnIcon(definition) || Visuals.GearFactory.ArteDaPrancha(definition)) && Visuals.GearFactory.PaintedIcon(info?.Model, definition) is { } painted
            && VisualAssets.Load(painted) is { } png)
        {
            var pintada = Visuals.GearFactory.LotTwoDrop(info?.Model, definition) is { } drop && !ArteDona(drop, definition) ? Tingido(png, definition) : png;
            Cache[key] = pintada;
            return pintada;
        }
        if (_instance is not null && Requested.Add(key)) Pending.Enqueue((key, info, definition));
        return null;
    }

    public override void _Ready()
    {
        _viewport = new SubViewport
        {
            Size = new Vector2I(128, 128), TransparentBg = true, OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Msaa4X,
        };
        AddChild(_viewport);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor, AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.52f, 0.5f), AmbientLightEnergy = 0.9f, TonemapMode = Godot.Environment.ToneMapper.Aces,
        };
        _viewport.AddChild(new WorldEnvironment { Environment = env });
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40, 35, 0), LightEnergy = 1.6f, LightColor = new Color(1f, 0.93f, 0.82f) });
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-10, -140, 0), LightEnergy = 0.7f, LightColor = new Color(0.6f, 0.7f, 1f) });
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 1f, Current = true };
        _viewport.AddChild(_camera);
        _stage = new Node3D { Name = "Palco" };
        _viewport.AddChild(_stage);
    }

    public override void _Process(double delta)
    {
        if (_current is not null)
        {
            if (--_wait > 0) return;
            var image = _viewport.GetTexture().GetImage();
            if (image is not null && !image.IsEmpty()) Cache[_current] = ImageTexture.CreateFromImage(image);
            foreach (var c in _stage.GetChildren()) c.QueueFree();
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            _current = null;
            IconReady?.Invoke();
            return;
        }
        if (Pending.Count == 0) return;
        var (key, info, definition) = Pending.Dequeue();
        var model = Build(info, definition, out var mannequin);
        if (model is null) return;   // sem modelo 3D: fica o ícone desenhado
        _stage.AddChild(model);
        mannequin?.Update(new VisualFrame { Alive = true, Action = "" }, 0.016f);
        Frame(model, mannequin);
        _current = key;
        _wait = 3;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
    }

    /// <summary>Enquadra o modelo: caixa de todos os pedaços visíveis, câmera de frente, um pouco de cima.</summary>
    private void Frame(Node3D model, MannequinVisual? mannequin)
    {
        var box = new Aabb();
        var first = true;
        foreach (var mi in AllMeshes(model))
        {
            var b = mi.GlobalTransform * mi.GetAabb();
            box = first ? b : box.Merge(b);
            first = false;
        }
        if (first) box = new Aabb(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.One);
        if (mannequin is not null)
        {
            // só o tronco vestido: do meio da coxa até o alto da cabeça (pelos ossos — a caixa da malha com pele não serve)
            var top = mannequin.BoneWorld("Head") + new Vector3(0, 0.24f, 0);
            var low = mannequin.BoneWorld("LeftUpLeg").Lerp(mannequin.BoneWorld("LeftLeg"), 0.5f);
            var hgt = Mathf.Max(0.3f, top.Y - low.Y);
            box = new Aabb(new Vector3(top.X - hgt * 0.45f, low.Y, top.Z - 0.3f), new Vector3(hgt * 0.9f, hgt, 0.6f));
        }
        var center = box.GetCenter();
        var extent = Mathf.Max(box.Size.X, box.Size.Y);
        _camera.Size = extent * 1.12f;
        _camera.Position = center + new Vector3(0, 0, 6f);
        _camera.LookAt(center);
        if (OS.GetEnvironment("ICONE_DEBUG") == "1") GD.Print($"icone manequim={mannequin is not null} caixa={box} tamanho={_camera.Size} topo={mannequin?.BoneWorld("Head")} quadril={mannequin?.BoneWorld("Hips")} pe={mannequin?.BoneWorld("LeftFoot")}");
    }

    private static IEnumerable<MeshInstance3D> AllMeshes(Node n)
    {
        if (n is MeshInstance3D mi && mi.Visible) yield return mi;
        foreach (var c in n.GetChildren())
            foreach (var m in AllMeshes(c)) yield return m;
    }

    private static Node3D? Build(ItemInfo? info, string definition, out MannequinVisual? mannequin)
    {
        mannequin = null;
        var model = info?.Model ?? "";
        if (definition.StartsWith("tomo_")) return Tome(definition);
        if (OwnIcon(definition) is { } own) return own;   // desenho próprio vence a categoria (máscara é "helmet")
        // 17/11 noite (dono: "os ícones novos parecem massinha"): o modelo procedural por item (IconesUnicos) saiu do ícone
        var cat = info?.Category ?? "";
        if (cat == "weapon" || GearFactory.IsBow(model) || model.StartsWith("talisman_") || model.StartsWith("staff_"))
        {
            var w = model.StartsWith("talisman_") ? MannequinVisual.TalismanModel(model) : GearFactory.Weapon(model);
            if (w is null) return null;
            var holder = new Node3D();
            holder.AddChild(w);
            // em diagonal, como nos jogos de mochila
            w.RotationDegrees = model.StartsWith("talisman_") ? new Vector3(0, 25, 0) : new Vector3(0, 20, -45);
            return holder;
        }
        if (cat == "helmet" || model.StartsWith("head_"))
        {
            var h = GearFactory.Helmet(model);
            if (h is null) return null;
            h.RotationDegrees = new Vector3(8, 28, 0);
            return Wrap(h);
        }
        if (cat == "offhand" || model.StartsWith("shield_"))
        {
            var s = GearFactory.Shield(model);
            if (s is null) return null;
            s.RotationDegrees = new Vector3(0, 22, 0);
            return Wrap(s);
        }
        if (cat == "armor" || model.StartsWith("body_"))
        {
            var cls = model.Contains("witch") ? "bruxa" : model.Contains("ranger") ? "arqueiro" : model.Contains("robe") ? "mago" : "guerreiro";
            var mv = MannequinVisual.TryCreate(cls, cls == "bruxa");
            if (mv is null) return null;
            mv.SetGear(null, null, model);
            mannequin = mv;
            mv.Root.RotationDegrees = new Vector3(0, 18, 0);
            return mv.Root;
        }
        // 13/11 ("os ícones do Ramires estão todos bugados"): decoração, caixa, relíquia e bônus da casa apareciam como poção
        if (definition.StartsWith("decor_"))
        {
            var d = new Node3D();
            if (CasaDecor.Build(d, definition, (_, _) => Vector3.Zero, icone: true)) return d;
        }
        if (definition.StartsWith("caixa_") && OwnIcon(definition) is { } caixa) return caixa;
        if (definition.StartsWith("pet_"))   // 13/11: foto do bicho (antes todos usavam o desenho da Joia da Brasa)
        {
            var pet = VisualFactory.Pet(definition[4..]);
            pet.Update(new VisualFrame { Alive = true, Action = "" }, 0.016f);
            pet.Root.RotationDegrees = new Vector3(0, 30, 0);
            return Wrap(pet.Root);
        }
        if (definition.StartsWith("apito_") || definition.StartsWith("montaria_"))   // 13/11: montaria como item = foto do bicho
        {
            var k = definition[(definition.IndexOf('_') + 1)..];
            var mont = VisualFactory.Mount(k);
            mont.Update(new VisualFrame { Alive = true, Action = "" }, 0.016f);
            mont.Root.RotationDegrees = new Vector3(0, 30, 0);
            return Wrap(mont.Root);
        }
        if (definition.StartsWith("reliquia_"))
        {
            var r = new Node3D();
            VisualKit.Part(r, VisualKit.Cylinder(0.05f, 0.18f, 0.12f, 6), VisualKit.Mat(new Color(0.3f, 0.25f, 0.2f), 0.6f, 0.6f), new Vector3(0, -0.22f, 0));
            VisualKit.Part(r, VisualKit.Sphere(0.2f), VisualKit.Glow(new Color(0.65f, 0.25f, 1f), 1.6f), new Vector3(0, 0.02f, 0));
            VisualKit.Part(r, new TorusMesh { InnerRadius = 0.24f, OuterRadius = 0.28f }, VisualKit.Mat(new Color(0.95f, 0.75f, 0.3f), 0.3f, 0.9f), Vector3.Zero, new Vector3(90, 0, 0));
            return r;
        }
        if (definition.StartsWith("buff_casa_")) return Flask(new Color(1f, 0.75f, 0.25f));
        if (model is "potion" or "potion_mana" || definition.StartsWith("consumable_")) return Flask(model == "potion_mana" ? new Color(0.25f, 0.45f, 1f) : new Color(0.9f, 0.15f, 0.2f));
        if (model == "food") return Food();
        if (model.StartsWith("mat_") || cat == "material") return Material(model, definition);
        return GearFactory.GroundItem(model, definition);
    }

    private static Node3D Wrap(Node3D n) { var r = new Node3D(); r.AddChild(n); return r; }

    private static Node3D Flask(Color liquid)
    {
        var root = new Node3D();
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.9f, 1f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, MetallicSpecular = 1f };
        // frasco cilíndrico de vidro (pedido do usuário): corpo reto, ombro curto, gargalo e rolha; sem etiqueta
        VisualKit.Part(root, VisualKit.Cylinder(0.15f, 0.15f, 0.36f, 18), glass, new Vector3(0, -0.04f, 0));
        VisualKit.Part(root, VisualKit.Cylinder(0.135f, 0.135f, 0.28f, 18), VisualKit.Glow(liquid.Darkened(0.35f), 0.8f), new Vector3(0, -0.07f, 0));
        VisualKit.Part(root, VisualKit.Cylinder(0.065f, 0.15f, 0.06f, 18), glass, new Vector3(0, 0.17f, 0));
        VisualKit.Part(root, VisualKit.Cylinder(0.055f, 0.06f, 0.1f, 12), glass, new Vector3(0, 0.24f, 0));
        VisualKit.Part(root, VisualKit.Cylinder(0.062f, 0.055f, 0.07f, 10), VisualKit.Mat(new Color(0.45f, 0.30f, 0.18f), 0.9f), new Vector3(0, 0.31f, 0));
        return root;
    }

    private static Node3D Food()
    {
        var root = new Node3D();
        VisualKit.Part(root, VisualKit.Cylinder(0.28f, 0.22f, 0.12f, 14), VisualKit.Mat(new Color(0.45f, 0.30f, 0.18f), 0.9f), Vector3.Zero);
        VisualKit.Part(root, VisualKit.Sphere(0.16f), VisualKit.Mat(new Color(0.6f, 0.3f, 0.15f), 0.7f), new Vector3(0.05f, 0.12f, 0), null, new Vector3(1.3f, 0.7f, 1f));
        VisualKit.Part(root, VisualKit.Cylinder(0.025f, 0.025f, 0.22f, 6), VisualKit.Mat(new Color(0.9f, 0.88f, 0.8f), 0.6f), new Vector3(-0.15f, 0.15f, 0), new Vector3(0, 0, 60));
        return root;
    }

    public static string TomeClass(string definition)
        => definition.Contains("_bruxa_") ? "bruxa" : definition.Contains("_mago_") ? "mago" : definition.Contains("_arqueiro_") ? "arqueiro" : "guerreiro";

    /// <summary>Objeto de aprender poder, um por classe: esfera (Guerreiro), livro (Mago), grimório (Bruxa), pergaminho (Arqueiro).</summary>
    private static Node3D Tome(string definition)
    {
        var brass = VisualKit.Mat(new Color(0.8f, 0.62f, 0.3f), 0.35f, 0.8f);
        var root = new Node3D { RotationDegrees = new Vector3(15, 25, -8) };
        switch (TomeClass(definition))
        {
            case "guerreiro":
            {
                // esfera de fogo-vivo presa num aro de bronze com garras
                root.RotationDegrees = new Vector3(10, 20, 0);
                VisualKit.Part(root, VisualKit.Sphere(0.27f), VisualKit.Glow(new Color(1f, 0.35f, 0.15f), 1.6f), new Vector3(0, 0.05f, 0));
                VisualKit.Part(root, VisualKit.Sphere(0.16f), VisualKit.Glow(new Color(1f, 0.85f, 0.45f), 2.6f), new Vector3(-0.04f, 0.09f, 0.08f));
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.26f, OuterRadius = 0.31f, Rings = 24 }, brass, new Vector3(0, 0.05f, 0), new Vector3(90, 0, 0));
                for (var i = 0; i < 3; i++)
                {
                    var a = i * Mathf.Tau / 3f;
                    VisualKit.Part(root, VisualKit.Cylinder(0.0f, 0.05f, 0.24f, 5), brass, new Vector3(Mathf.Sin(a) * 0.22f, -0.2f, Mathf.Cos(a) * 0.22f), new Vector3(Mathf.Cos(a) * 25f, 0, -Mathf.Sin(a) * 25f));
                }
                VisualKit.Part(root, VisualKit.Cylinder(0.2f, 0.24f, 0.08f, 12), brass, new Vector3(0, -0.3f, 0));
                break;
            }
            case "arqueiro":
            {
                // pergaminho enrolado com fita e lacre
                root.RotationDegrees = new Vector3(20, 15, 35);
                var paper = VisualKit.Mat(new Color(0.88f, 0.80f, 0.60f), 0.9f);
                VisualKit.Part(root, VisualKit.Cylinder(0.13f, 0.13f, 0.72f, 18), paper, Vector3.Zero);
                foreach (var y in new[] { -0.38f, 0.38f })
                    VisualKit.Part(root, VisualKit.Cylinder(0.05f, 0.05f, 0.08f, 10), VisualKit.Mat(new Color(0.35f, 0.22f, 0.12f), 0.6f), new Vector3(0, y, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.14f, 0.14f, 0.07f, 18), VisualKit.Mat(new Color(0.20f, 0.42f, 0.18f), 0.7f), new Vector3(0, 0.05f, 0));
                VisualKit.Part(root, VisualKit.Sphere(0.06f), VisualKit.Mat(new Color(0.65f, 0.1f, 0.08f), 0.4f), new Vector3(0, 0.05f, 0.14f), null, new Vector3(1f, 1f, 0.5f));
                VisualKit.Part(root, VisualKit.Sphere(0.035f), VisualKit.Glow(new Color(1f, 0.75f, 0.25f), 1.8f), new Vector3(0, 0.05f, 0.17f));
                break;
            }
            default:
            {
                // livro (Mago, violeta com gema azul) ou grimório (Bruxa, verde com fecho e olho de esmeralda)
                var witch = TomeClass(definition) == "bruxa";
                var cover = witch ? new Color(0.12f, 0.30f, 0.18f) : new Color(0.25f, 0.16f, 0.42f);
                var gem = witch ? new Color(0.3f, 1f, 0.55f) : new Color(0.5f, 0.65f, 1f);
                VisualKit.Part(root, VisualKit.Box(0.5f, 0.66f, 0.14f), VisualKit.Mat(cover, 0.8f), Vector3.Zero);
                VisualKit.Part(root, VisualKit.Box(0.46f, 0.62f, 0.12f), VisualKit.Mat(new Color(0.9f, 0.85f, 0.72f), 0.9f), new Vector3(0.03f, 0, 0));
                foreach (var (x, y) in new[] { (-0.22f, 0.3f), (0.22f, 0.3f), (-0.22f, -0.3f), (0.22f, -0.3f) })
                    VisualKit.Part(root, VisualKit.Box(0.08f, 0.08f, 0.16f), brass, new Vector3(x, y, 0));
                VisualKit.Part(root, VisualKit.Sphere(0.07f), VisualKit.Glow(gem, 2f), new Vector3(0, 0.02f, 0.08f));
                if (witch)
                {
                    VisualKit.Part(root, VisualKit.Box(0.08f, 0.14f, 0.18f), brass, new Vector3(0.27f, 0, 0));   // fecho
                    VisualKit.Part(root, VisualKit.Cylinder(0.1f, 0.1f, 0.01f, 16), brass, new Vector3(0, 0.02f, 0.075f), new Vector3(90, 0, 0));
                }
                else
                {
                    VisualKit.Part(root, VisualKit.Box(0.3f, 0.02f, 0.01f), brass, new Vector3(0, 0.16f, 0.075f));
                    VisualKit.Part(root, VisualKit.Box(0.3f, 0.02f, 0.01f), brass, new Vector3(0, -0.12f, 0.075f));
                }
                break;
            }
        }
        return root;
    }

    // 11/11 ("semente, frasco vazio, larva, tudo com a mesma arte"; "rebite é uma flor, liga de geada... tudo flor"):
    // materiais que caíam todos no ícone genérico de essência ganham um desenho próprio, feito de peças
    private static readonly string[] OwnIconIds =
    {
        "semente_", "mat_frasco", "mat_residuo_larva", "mat_rebites", "mat_liga_", "mat_placa_chapa", "mat_carne_javali", "mat_carne_maior", "caixa_cinzas_", "caixa_pandora_",
        "mat_couro", "mat_resina", "mat_cera", "mat_chave_", "mat_pluma_neve", "mat_reagente", "mat_regador",
        "mat_sela", "mat_molde_ferreiro", "mat_espada_quebrada", "mat_estrutura_asas", "mat_estandarte",
        // 13/11 ("corrija algum ícone errado"): bandagem/ataduras eram poção vermelha; água benta, panaceia e antídoto também;
        // mapa do tesouro e braceletes de skin caíam numa caixa genérica
        "consumable_bandagem", "consumable_c06_ataduras", "consumable_agua_benta", "consumable_panaceia", "consumable_c07_antidoto",
        "mapa_tesouro", "bracelete_",
        // 13/11 ("muita coisa usando a mesma arte — ressonador, insígnia, atadura... a Máscara do Pandemônio sem arte")
        "mascara_pandemonio", "mat_ressonador", "mat_insignia", "mat_carta_perdida", "mat_memoria_selada", "mat_fragmento_", "mat_base_reliquia",
        "mat_foco_juramento", "mat_refeicao_vinculo", "mat_nucleo_", "mat_brasa_vinculo", "mat_alimento_brasa", "mat_cinza_", "selo_",
        "mat_sangue_lunar", "mat_madeira_verde", "mat_gorro_saci", "food_", "mat_ficha_mercado", "joia_forja",
        // 13/11 ("o tônico fraco parece um tônico grande; deixa delicado, tipo um tubo de ensaio")
        "consumable_c01_tonico_fraco",
        // 14/11 ("cada poção tem que ter seu ícone 3D próprio, com o seu tamanho")
        "consumable_c02_tonico_medio", "consumable_c05_tonico_forte", "consumable_c03_frasco_mana", "consumable_c04_frasco_mana_grande",
        "consumable_c08_frasco_foco", "consumable_c09_frasco_foco_grande",   // 14/11: Foco do Arqueiro (laranja)
        "esfera_macaco_",   // 14/11: esfera do macaco virou item
        "mat_chifre_cabra",   // 14/11: a cabra larga chifre (não carne de javali)
        "chave_",   // 18/11: Chave de Cofre (baú trancado)
    };

    public static bool HasOwnIcon(string definition) => Array.Exists(OwnIconIds, definition.StartsWith);

    /// <summary>17/11 noite: quem fica com o desenho pintado original (o item que a arte retrata).</summary>
    private static bool ArteDona(string drop, string id) => drop switch
    {
        "item_carapaca" => id is "mat_casca_cal" or "mat_carapaca",
        "item_erva" => id == "mat_erva_tonico",
        "item_minerio" => id == "mat_ferro_raso",
        "item_osso" => id == "mat_osso_antigo",
        "item_presa" => id is "mat_presa" or "mat_presa_lobo",
        "item_seda" => id == "mat_seda",
        "item_essencia" => id is "mat_essencia" or "mat_cinza_viva",
        "item_pocao_vida" or "item_pocao_mana" or "item_moedas" or "item_bolsa" or "item_pinca_caranguejo" => true,
        _ => id.Contains(drop["item_".Length..]),   // joias: cada uma tem o seu desenho
    };

    /// <summary>18/11 19h: a cor do material (a mesma do ícone) — o veio/a moita no mapa usa para combinar com o que dá.</summary>
    public static Color CorDe(string id)
    {
        if (CorDoItem.TryGetValue(id, out var c)) return c;
        var h = 0u; foreach (var ch in id) h = h * 31 + ch;
        return Color.FromHsv((h % 360) / 360f, 0.55f, 0.9f);
    }

    /// <summary>Cor de cada material que divide desenho (o resto: cor tirada do próprio id, estável).</summary>
    private static readonly Dictionary<string, Color> CorDoItem = new()
    {
        ["mat_folha_amarga"] = new(0.62f, 0.62f, 0.18f), ["mat_folha_urtiga"] = new(0.55f, 0.35f, 0.75f), ["mat_erva_inverno"] = new(0.55f, 0.8f, 0.95f),
        ["mat_cogumelo_cinza"] = new(0.75f, 0.55f, 0.4f), ["mat_raiz_vigilia"] = new(0.7f, 0.45f, 0.22f), ["mat_flor_vinculo"] = new(0.95f, 0.5f, 0.75f),
        ["mat_ferro_eco"] = new(0.45f, 0.7f, 1f), ["mat_ferro_geada"] = new(0.75f, 0.9f, 1f), ["mat_basalto"] = new(0.35f, 0.3f, 0.32f), ["mat_bronze"] = new(0.85f, 0.55f, 0.3f),
        ["mat_liga_geada"] = new(0.6f, 0.85f, 1f), ["mat_liga_lendaria"] = new(1f, 0.8f, 0.3f), ["mat_quitina"] = new(0.4f, 0.3f, 0.55f), ["mat_casca_serpente"] = new(0.45f, 0.65f, 0.3f),
        ["mat_osso_lunar"] = new(0.7f, 0.8f, 1f), ["mat_ferrao"] = new(0.9f, 0.7f, 0.2f), ["mat_presa_vampira"] = new(0.85f, 0.2f, 0.25f), ["mat_dente_dentuco"] = new(0.95f, 0.92f, 0.8f),
        // 18/11: itens de região
        ["mat_erva_serra"] = new(0.5f, 0.75f, 0.35f), ["mat_erva_enforcado"] = new(0.35f, 0.45f, 0.25f), ["mat_cardo_canion"] = new(0.75f, 0.45f, 0.75f),
        ["mat_flor_noturna"] = new(0.45f, 0.4f, 0.95f), ["mat_alga_salgada"] = new(0.2f, 0.65f, 0.55f), ["mat_erva_cinza_antiga"] = new(0.75f, 0.72f, 0.6f),
        ["mat_ferro_serra"] = new(0.7f, 0.62f, 0.5f), ["mat_ferro_musgo"] = new(0.45f, 0.62f, 0.4f), ["mat_sucata_automato"] = new(0.8f, 0.6f, 0.35f),
        ["mat_couro"] = new(0.6f, 0.4f, 0.22f), ["mat_retalho"] = new(0.7f, 0.25f, 0.25f), ["mat_membrana"] = new(0.7f, 0.85f, 1f),
    };

    /// <summary>O desenho pintado recolorido para este item: a luz e a forma são as mesmas (o traço do artista fica), só a cor
    /// muda — nada de modelo de massinha. Mistura 70% da cor nova com 30% do original para não ficar chapado.</summary>
    private static Texture2D Tingido(Texture2D png, string id)
    {
        try
        {
            var img = png.GetImage();
            if (img.IsCompressed()) img.Decompress();
            img = (Image)img.Duplicate();
            img.Convert(Image.Format.Rgba8);
            Color alvo;
            if (!CorDoItem.TryGetValue(id, out alvo))
            {
                var h = 0u; foreach (var ch in id) h = h * 31 + ch;
                alvo = Color.FromHsv((h % 360) / 360f, 0.55f, 0.9f);
            }
            for (var y = 0; y < img.GetHeight(); y++)
                for (var x = 0; x < img.GetWidth(); x++)
                {
                    var c = img.GetPixel(x, y);
                    if (c.A < 0.02f) continue;
                    var lum = c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
                    var nova = new Color(alvo.R * lum * 1.35f, alvo.G * lum * 1.35f, alvo.B * lum * 1.35f, c.A);
                    img.SetPixel(x, y, c.Lerp(nova, 0.7f) with { A = c.A });
                }
            return ImageTexture.CreateFromImage(img);
        }
        catch { return png; }
    }

    /// <summary>14/11 ("cada poção com o seu tamanho"): escala do desenho próprio quando o item está no chão.</summary>
    public static float TamanhoNoChao(string id) => id switch
    {
        "consumable_c01_tonico_fraco" => 0.6f,
        "consumable_c03_frasco_mana" or "consumable_c08_frasco_foco" => 0.7f,
        "consumable_c02_tonico_medio" => 0.85f,
        "consumable_c05_tonico_forte" or "consumable_c04_frasco_mana_grande" or "consumable_c09_frasco_foco_grande" => 1.05f,
        "mapa_tesouro" => 1.0f,
        _ when id.StartsWith("esfera_macaco_") => 0.6f,
        _ => 0.8f,
    };

    /// <summary>13/11: o mesmo desenho do ícone, para o item no chão (caixa das cinzas).</summary>
    public static Node3D? OwnIconNode(string id) => OwnIcon(id);

    /// <summary>17/11 noite: Esfera do Macaco — bola vermelha brilhante com o número dourado na frente (ícone e chão).</summary>
    public static void EsferaNumerada(Node3D root, int numero, float raio)
    {
        var vidro = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.06f, 0.05f), Roughness = 0.12f, Metallic = 0.15f,
            EmissionEnabled = true, Emission = new Color(0.6f, 0.05f, 0.03f), EmissionEnergyMultiplier = 0.9f, RimEnabled = true, Rim = 0.6f,
        };
        VisualKit.Part(root, VisualKit.Sphere(raio), vidro, Vector3.Zero);
        VisualKit.Part(root, VisualKit.Sphere(raio * 0.16f), VisualKit.Glow(new Color(1f, 0.9f, 0.85f), 2f), new Vector3(-raio * 0.42f, raio * 0.48f, raio * 0.72f));   // brilho
        root.AddChild(new Label3D
        {
            Text = numero.ToString(), FontSize = 160, OutlineSize = 28, PixelSize = raio * 0.0072f, Modulate = new Color(1f, 0.85f, 0.3f),
            OutlineModulate = new Color(0.35f, 0.08f, 0.02f), Position = new Vector3(0, -raio * 0.05f, raio * 1.01f), Shaded = false,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled, NoDepthTest = false,
        });
    }

    /// <summary>13/11: um desenho para cada material/peça que dividia a mesma arte.</summary>
    private static bool Distinto(Node3D root, string id, StandardMaterial3D iron, StandardMaterial3D leather)
    {
        StandardMaterial3D M(Color c, float r = 0.6f, float met = 0f) => VisualKit.Mat(c, r, met);
        var ouro = M(new Color(0.95f, 0.75f, 0.3f), 0.3f, 0.9f);
        if (id.StartsWith("esfera_macaco_") && int.TryParse(id[^1..], out var estrelas))
        {
            // 17/11 noite (dono: "as esferas do macaco: vermelhas, com números, 1 até o 5")
            EsferaNumerada(root, estrelas, 0.33f);
            return true;
        }
        if (id == "mat_chifre_cabra")
        {
            // chifre curvo: gomos que afinam e giram em arco, cor de osso escurecendo na ponta
            for (var i = 0; i < 7; i++)
            {
                var t = i / 6f;
                var a = Mathf.Lerp(-0.6f, 2.3f, t);
                var pos = new Vector3(Mathf.Cos(a) * 0.22f, Mathf.Sin(a) * 0.22f - 0.02f, 0);
                var cor = new Color(0.86f, 0.8f, 0.66f).Lerp(new Color(0.35f, 0.3f, 0.24f), t);
                VisualKit.Part(root, VisualKit.Cylinder(0.07f * (1f - t * 0.75f), 0.09f * (1f - t * 0.7f), 0.13f, 10), M(cor, 0.55f), pos, new Vector3(0, 0, Mathf.RadToDeg(a)));
            }
            return true;
        }
        switch (id)
        {
            case "mascara_pandemonio":
            {
                // máscara de madeira verde rindo: rosto oval, olhos vazados, sorriso largo e chifrinhos
                var verde = M(new Color(0.25f, 0.6f, 0.2f), 0.7f);
                var esc = M(new Color(0.05f, 0.08f, 0.03f), 0.9f);
                VisualKit.Part(root, VisualKit.Sphere(0.3f), verde, Vector3.Zero, null, new Vector3(1f, 1.2f, 0.35f));
                foreach (var x in new[] { -0.11f, 0.11f })
                {
                    VisualKit.Part(root, VisualKit.Sphere(0.06f), esc, new Vector3(x, 0.08f, 0.09f), null, new Vector3(1.2f, 0.7f, 0.4f));
                    VisualKit.Part(root, VisualKit.Cylinder(0f, 0.04f, 0.16f, 6), M(new Color(0.5f, 0.8f, 0.3f)), new Vector3(x * 1.6f, 0.36f, 0), new Vector3(0, 0, -x * 140f));
                }
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.135f }, esc, new Vector3(0, -0.12f, 0.08f), new Vector3(90, 0, 0), new Vector3(1.3f, 1f, 0.45f));
                VisualKit.Part(root, VisualKit.Box(0.3f, 0.06f, 0.05f), verde, new Vector3(0, -0.06f, 0.11f));   // tampa o arco de cima: vira sorriso
                return true;
            }
            case "mat_ressonador":
                // diapasão de cristal sobre base de bronze
                VisualKit.Part(root, VisualKit.Cylinder(0.12f, 0.15f, 0.08f, 12), ouro, new Vector3(0, -0.3f, 0));
                foreach (var x in new[] { -0.07f, 0.07f })
                    VisualKit.Part(root, VisualKit.Box(0.04f, 0.5f, 0.04f), VisualKit.Glow(new Color(0.55f, 0.85f, 1f), 1.4f), new Vector3(x, 0.02f, 0));
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.22f }, VisualKit.Glow(new Color(0.55f, 0.85f, 1f), 0.8f), new Vector3(0, 0.05f, 0), new Vector3(90, 0, 0));
                return true;
            case "mat_insignia":
                // brasão: escudinho de metal com estrela
                VisualKit.Part(root, VisualKit.Cylinder(0.26f, 0.26f, 0.05f, 6), iron, Vector3.Zero, new Vector3(90, 0, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.14f, 0.14f, 0.06f, 5), ouro, new Vector3(0, 0, 0.02f), new Vector3(90, 0, 0));
                VisualKit.Part(root, VisualKit.Box(0.12f, 0.24f, 0.02f), M(new Color(0.6f, 0.1f, 0.1f)), new Vector3(0, -0.33f, 0));
                return true;
            case "mat_carta_perdida":
                VisualKit.Part(root, VisualKit.Box(0.55f, 0.36f, 0.02f), M(new Color(0.9f, 0.84f, 0.68f), 0.95f), Vector3.Zero, new Vector3(0, 0, -8));
                VisualKit.Part(root, VisualKit.Cylinder(0.06f, 0.06f, 0.03f, 12), M(new Color(0.7f, 0.08f, 0.06f)), new Vector3(0, -0.02f, 0.02f), new Vector3(90, 0, 0));
                return true;
            case "mat_memoria_selada":
                VisualKit.Part(root, VisualKit.Cylinder(0.1f, 0.1f, 0.6f, 14), M(new Color(0.85f, 0.78f, 0.6f), 0.9f), Vector3.Zero, new Vector3(0, 0, 70));
                VisualKit.Part(root, VisualKit.Box(0.1f, 0.22f, 0.22f), VisualKit.Glow(new Color(0.6f, 0.35f, 1f), 1.5f), new Vector3(0, 0, 0));
                return true;
            case "mat_base_reliquia":
                VisualKit.Part(root, VisualKit.Cylinder(0.22f, 0.3f, 0.18f, 8), M(new Color(0.55f, 0.52f, 0.5f), 0.8f), new Vector3(0, -0.2f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.12f, 0.16f, 0.3f, 8), M(new Color(0.65f, 0.6f, 0.55f), 0.8f), new Vector3(0, 0.03f, 0));
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.14f, OuterRadius = 0.17f }, ouro, new Vector3(0, 0.2f, 0));
                return true;
            case "mat_foco_juramento":
                VisualKit.Part(root, VisualKit.Sphere(0.22f), VisualKit.Glow(new Color(1f, 0.85f, 0.4f), 1.6f), new Vector3(0, 0.05f, 0));
                for (var i = 0; i < 3; i++)
                    VisualKit.Part(root, VisualKit.Cylinder(0f, 0.035f, 0.2f, 4), iron, new Vector3(Mathf.Sin(i * 2.1f) * 0.2f, -0.18f, Mathf.Cos(i * 2.1f) * 0.2f), new Vector3(0, 0, 180));
                return true;
            case "mat_refeicao_vinculo" or "mat_alimento_brasa":
                VisualKit.Part(root, VisualKit.Cylinder(0.3f, 0.2f, 0.16f, 16), M(new Color(0.5f, 0.32f, 0.18f), 0.85f), Vector3.Zero);
                VisualKit.Part(root, VisualKit.Cylinder(0.27f, 0.27f, 0.02f, 16), VisualKit.Glow(id == "mat_alimento_brasa" ? new Color(1f, 0.45f, 0.1f) : new Color(0.6f, 1f, 0.5f), 0.8f), new Vector3(0, 0.07f, 0));
                return true;
            case "mat_brasa_vinculo":
                VisualKit.Part(root, VisualKit.Sphere(0.16f), VisualKit.Glow(new Color(1f, 0.4f, 0.1f), 2.4f), Vector3.Zero, null, new Vector3(1f, 1.4f, 1f));
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.22f, OuterRadius = 0.25f }, ouro, Vector3.Zero, new Vector3(60, 0, 0));
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.22f, OuterRadius = 0.25f }, ouro, Vector3.Zero, new Vector3(-60, 0, 0));
                return true;
            case "mat_sangue_lunar":
                VisualKit.Part(root, VisualKit.Cylinder(0.08f, 0.12f, 0.36f, 12), new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.9f, 1f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha }, Vector3.Zero);
                VisualKit.Part(root, VisualKit.Cylinder(0.07f, 0.1f, 0.26f, 12), VisualKit.Glow(new Color(0.75f, 0.05f, 0.15f), 0.9f), new Vector3(0, -0.04f, 0));
                VisualKit.Part(root, VisualKit.Sphere(0.06f), VisualKit.Glow(new Color(0.85f, 0.9f, 1f), 2f), new Vector3(0.18f, 0.2f, 0));   // luazinha
                return true;
            case "mat_madeira_verde":
                VisualKit.Part(root, VisualKit.Cylinder(0.14f, 0.14f, 0.6f, 12), M(new Color(0.45f, 0.32f, 0.18f), 0.9f), Vector3.Zero, new Vector3(0, 0, 80));
                VisualKit.Part(root, VisualKit.Sphere(0.09f), M(new Color(0.3f, 0.65f, 0.25f)), new Vector3(0.1f, 0.15f, 0), null, new Vector3(1.4f, 0.6f, 1f));
                return true;
            case "mat_gorro_saci":
                VisualKit.Part(root, VisualKit.Cylinder(0f, 0.24f, 0.5f, 16), M(new Color(0.85f, 0.08f, 0.08f), 0.8f), new Vector3(0, 0.05f, 0), new Vector3(0, 0, -18));
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.19f, OuterRadius = 0.26f }, M(new Color(0.7f, 0.05f, 0.05f), 0.8f), new Vector3(0.04f, -0.18f, 0));
                return true;
            case "mat_ficha_mercado":
                VisualKit.Part(root, VisualKit.Cylinder(0.25f, 0.25f, 0.05f, 6), M(new Color(0.2f, 0.45f, 0.75f)), Vector3.Zero, new Vector3(80, 0, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.12f, 0.12f, 0.06f, 16), ouro, new Vector3(0, 0, 0.01f), new Vector3(80, 0, 0));
                return true;
            case "joia_forja":
                VisualKit.Part(root, VisualKit.Sphere(0.2f), VisualKit.Glow(new Color(0.95f, 0.95f, 1f), 1.4f), Vector3.Zero, null, new Vector3(1f, 1.3f, 1f));
                VisualKit.Part(root, VisualKit.Box(0.5f, 0.06f, 0.1f), iron, new Vector3(0, -0.24f, 0));   // bigorninha embaixo
                return true;
        }
        if (id.StartsWith("mat_fragmento_"))
        {
            var cor = id.Contains("fornalha") ? new Color(1f, 0.45f, 0.1f) : id.Contains("reliquia") ? new Color(0.75f, 0.5f, 1f) : id.Contains("abismo") ? new Color(0.35f, 0.08f, 0.55f) : new Color(0.5f, 0.75f, 0.9f);
            var pedra = id.Contains("veio") ? M(new Color(0.45f, 0.42f, 0.4f), 0.9f) : VisualKit.Glow(cor, 1.2f);
            VisualKit.Part(root, VisualKit.Cylinder(0f, 0.16f, 0.45f, 5), pedra, new Vector3(-0.06f, 0, 0), new Vector3(0, 0, 14));
            VisualKit.Part(root, VisualKit.Cylinder(0f, 0.11f, 0.3f, 5), pedra, new Vector3(0.15f, -0.07f, 0.03f), new Vector3(0, 0, -25));
            if (id.Contains("veio")) VisualKit.Part(root, VisualKit.Box(0.04f, 0.3f, 0.02f), VisualKit.Glow(new Color(0.4f, 0.9f, 1f), 1.6f), new Vector3(-0.05f, 0, 0.1f), new Vector3(0, 0, 14));
            return true;
        }
        if (id.StartsWith("mat_nucleo_"))
        {
            var cor = id.Contains("lendario") ? new Color(1f, 0.8f, 0.25f) : id.Contains("superior") ? new Color(0.7f, 0.4f, 1f) : new Color(0.4f, 0.9f, 0.7f);
            VisualKit.Part(root, VisualKit.Sphere(0.15f), VisualKit.Glow(cor, 2.2f), Vector3.Zero);
            VisualKit.Part(root, VisualKit.Box(0.34f, 0.34f, 0.34f), new StandardMaterial3D { AlbedoColor = cor with { A = 0.25f }, Transparency = BaseMaterial3D.TransparencyEnum.Alpha }, Vector3.Zero, new Vector3(35, 45, 0));
            return true;
        }
        if (id.StartsWith("mat_cinza_"))
        {
            var cor = id.Contains("vampir") ? new Color(0.55f, 0.08f, 0.12f) : new Color(0.55f, 0.52f, 0.5f);
            VisualKit.Part(root, VisualKit.Sphere(0.3f), M(cor, 1f), new Vector3(0, -0.12f, 0), null, new Vector3(1.2f, 0.5f, 1.2f));
            VisualKit.Part(root, VisualKit.Sphere(0.05f), VisualKit.Glow(new Color(1f, 0.5f, 0.15f), 2.5f), new Vector3(0.08f, 0.02f, 0.1f));
            return true;
        }
        if (id.StartsWith("selo_"))
        {
            var cor = id.Contains("geada") ? new Color(0.45f, 0.7f, 1f) : id.Contains("escoria") ? new Color(1f, 0.45f, 0.1f) : new Color(0.6f, 0.15f, 0.2f);
            VisualKit.Part(root, VisualKit.Cylinder(0.26f, 0.28f, 0.07f, 18), M(cor, 0.5f), Vector3.Zero, new Vector3(75, 0, 0));
            VisualKit.Part(root, VisualKit.Box(0.1f, 0.22f, 0.04f), ouro, new Vector3(0, 0, 0.05f), new Vector3(-15, 0, 0));
            VisualKit.Part(root, VisualKit.Box(0.22f, 0.06f, 0.04f), ouro, new Vector3(0, 0.03f, 0.05f), new Vector3(-15, 0, 0));
            return true;
        }
        if (id.StartsWith("food_"))
        {
            var cor = id.Contains("caldo") ? new Color(0.55f, 0.5f, 0.45f) : id.Contains("ensopado") ? new Color(0.6f, 0.35f, 0.15f) : id.Contains("banquete") ? new Color(0.85f, 0.55f, 0.2f) : new Color(0.5f, 0.6f, 0.3f);
            VisualKit.Part(root, VisualKit.Cylinder(0.3f, 0.2f, 0.16f, 16), M(new Color(0.45f, 0.3f, 0.18f), 0.85f), Vector3.Zero);
            VisualKit.Part(root, VisualKit.Cylinder(0.27f, 0.27f, 0.02f, 16), M(cor, 0.5f), new Vector3(0, 0.07f, 0));
            if (id.Contains("banquete")) VisualKit.Part(root, VisualKit.Sphere(0.12f), M(new Color(0.7f, 0.35f, 0.15f)), new Vector3(0, 0.12f, 0), null, new Vector3(1.4f, 0.8f, 1f));
            VisualKit.Part(root, VisualKit.Cylinder(0.02f, 0.02f, 0.3f, 6), M(new Color(0.85f, 0.82f, 0.75f)), new Vector3(0.18f, 0.14f, 0), new Vector3(0, 0, 55));
            return true;
        }
        return false;
    }

    private static Node3D? OwnIcon(string id)
    {
        if (!HasOwnIcon(id)) return null;
        var root = new Node3D { RotationDegrees = new Vector3(18, 25, 0) };
        var iron = VisualKit.Mat(new Color(0.55f, 0.56f, 0.58f), 0.35f, 0.9f);
        var leather = VisualKit.Mat(new Color(0.45f, 0.28f, 0.15f), 0.85f);
        if (Distinto(root, id, iron, leather)) return root;
        if (id is "consumable_bandagem" or "consumable_c06_ataduras")
        {
            // 17/11 (dono: "atadura é um papel higiênico? kkk"): nada de rolo branco em pé — faixa de linho cru enrolada
            // achatada e meio desenrolada em curva, presa com alfinete; a bandagem tem a cruz vermelha e a de seda é dourada
            var seda = id == "consumable_c06_ataduras";
            var pano = VisualKit.Mat(seda ? new Color(0.95f, 0.86f, 0.62f) : new Color(0.86f, 0.8f, 0.68f), seda ? 0.45f : 0.95f);
            var dobra = VisualKit.Mat(seda ? new Color(0.82f, 0.7f, 0.45f) : new Color(0.7f, 0.63f, 0.5f), 0.9f);
            VisualKit.Part(root, VisualKit.Cylinder(0.17f, 0.17f, 0.12f, 20), pano, new Vector3(-0.1f, 0.06f, 0), new Vector3(70, 0, 0));
            for (var i = 0; i < 3; i++)
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.09f + i * 0.03f, OuterRadius = 0.1f + i * 0.03f }, dobra, new Vector3(-0.1f, 0.06f, 0.06f), new Vector3(-20, 0, 0));
            // faixa solta descendo em curva
            for (var i = 0; i < 6; i++)
            {
                var t = i / 5f;
                VisualKit.Part(root, VisualKit.Box(0.13f, 0.11f, 0.012f), pano, new Vector3(0.05f + t * 0.22f, -0.08f - Mathf.Sin(t * 2.4f) * 0.14f, 0.04f), new Vector3(0, 0, -20 - t * 50));
            }
            VisualKit.Part(root, VisualKit.Box(0.12f, 0.02f, 0.02f), VisualKit.Mat(new Color(0.75f, 0.76f, 0.8f), 0.3f, 0.9f), new Vector3(-0.1f, 0.06f, 0.13f), new Vector3(0, 0, 30));   // alfinete
            if (!seda)
            {
                var verm = VisualKit.Mat(new Color(0.8f, 0.08f, 0.08f), 0.6f);
                VisualKit.Part(root, VisualKit.Box(0.04f, 0.12f, 0.01f), verm, new Vector3(-0.1f, 0.06f, 0.125f), new Vector3(-20, 0, 0));
                VisualKit.Part(root, VisualKit.Box(0.12f, 0.04f, 0.01f), verm, new Vector3(-0.1f, 0.06f, 0.125f), new Vector3(-20, 0, 0));
            }
            return root;
        }
        if (id == "consumable_c01_tonico_fraco")
        {
            // tubo de ensaio fino e inclinado, com um dedo de líquido vermelho e rolha de cortiça
            root.RotationDegrees = new Vector3(10, 25, -22);
            var vidro = new StandardMaterial3D { AlbedoColor = new Color(0.88f, 0.96f, 1f, 0.28f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.04f };
            VisualKit.Part(root, VisualKit.Cylinder(0.075f, 0.075f, 0.62f, 16), vidro, new Vector3(0, 0.02f, 0));
            VisualKit.Part(root, VisualKit.Sphere(0.075f), vidro, new Vector3(0, -0.29f, 0));
            var sangue = VisualKit.Glow(new Color(0.85f, 0.1f, 0.12f), 0.7f);
            VisualKit.Part(root, VisualKit.Cylinder(0.064f, 0.064f, 0.24f, 14), sangue, new Vector3(0, -0.17f, 0));
            VisualKit.Part(root, VisualKit.Sphere(0.064f), sangue, new Vector3(0, -0.29f, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.088f, 0.088f, 0.025f, 16), vidro, new Vector3(0, 0.33f, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.068f, 0.058f, 0.09f, 10), VisualKit.Mat(new Color(0.72f, 0.55f, 0.35f), 0.95f), new Vector3(0, 0.37f, 0));
            return root;
        }
        if (id is "consumable_c02_tonico_medio" or "consumable_c05_tonico_forte" or "consumable_c03_frasco_mana" or "consumable_c04_frasco_mana_grande"
            or "consumable_c08_frasco_foco" or "consumable_c09_frasco_foco_grande")
        {
            var vidro = new StandardMaterial3D { AlbedoColor = new Color(0.88f, 0.96f, 1f, 0.28f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.04f };
            var cortica = VisualKit.Mat(new Color(0.72f, 0.55f, 0.35f), 0.95f);
            var ouro = VisualKit.Mat(new Color(0.95f, 0.78f, 0.3f), 0.3f, 0.9f);
            var prata = VisualKit.Mat(new Color(0.8f, 0.82f, 0.86f), 0.25f, 0.9f);
            var vermelho = VisualKit.Glow(new Color(0.85f, 0.1f, 0.12f), 0.8f);
            var azul = VisualKit.Glow(new Color(0.15f, 0.35f, 1f), 0.9f);
            var laranja = VisualKit.Glow(new Color(1f, 0.5f, 0.08f), 0.95f);
            switch (id)
            {
                case "consumable_c02_tonico_medio":
                    // balão de laboratório: barriga redonda média, gargalo comprido, meio cheio de vermelho
                    VisualKit.Part(root, VisualKit.Sphere(0.2f), vidro, new Vector3(0, -0.12f, 0));
                    VisualKit.Part(root, VisualKit.Sphere(0.175f), vermelho, new Vector3(0, -0.15f, 0), null, new Vector3(1f, 0.8f, 1f));
                    VisualKit.Part(root, VisualKit.Cylinder(0.05f, 0.06f, 0.28f, 14), vidro, new Vector3(0, 0.16f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.062f, 0.052f, 0.08f, 10), cortica, new Vector3(0, 0.33f, 0));
                    break;
                case "consumable_c05_tonico_forte":
                    // garrafa grande e quadrada, vermelho escuro, cinta de ouro e tampa de ouro
                    VisualKit.Part(root, VisualKit.Box(0.34f, 0.42f, 0.22f), vidro, new Vector3(0, -0.08f, 0));
                    VisualKit.Part(root, VisualKit.Box(0.3f, 0.36f, 0.18f), VisualKit.Glow(new Color(0.65f, 0.03f, 0.08f), 1f), new Vector3(0, -0.1f, 0));
                    VisualKit.Part(root, VisualKit.Box(0.36f, 0.05f, 0.24f), ouro, new Vector3(0, 0.02f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.07f, 0.09f, 0.14f, 12), vidro, new Vector3(0, 0.2f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.085f, 0.085f, 0.07f, 12), ouro, new Vector3(0, 0.3f, 0));
                    VisualKit.Part(root, VisualKit.Sphere(0.05f), ouro, new Vector3(0, 0.35f, 0));
                    break;
                case "consumable_c03_frasco_mana":
                    // frasco cônico (erlenmeyer) pequeno, azul
                    VisualKit.Part(root, VisualKit.Cylinder(0.06f, 0.2f, 0.32f, 18), vidro, new Vector3(0, -0.12f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.09f, 0.18f, 0.18f, 18), azul, new Vector3(0, -0.19f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.05f, 0.05f, 0.14f, 12), vidro, new Vector3(0, 0.1f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.058f, 0.05f, 0.07f, 10), cortica, new Vector3(0, 0.2f, 0));
                    break;
                case "consumable_c08_frasco_foco":
                    // 14/11: o mesmo frasco cônico da mana, laranja (Foco do Arqueiro)
                    VisualKit.Part(root, VisualKit.Cylinder(0.06f, 0.2f, 0.32f, 18), vidro, new Vector3(0, -0.12f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.09f, 0.18f, 0.18f, 18), laranja, new Vector3(0, -0.19f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.05f, 0.05f, 0.14f, 12), vidro, new Vector3(0, 0.1f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.058f, 0.05f, 0.07f, 10), cortica, new Vector3(0, 0.2f, 0));
                    break;
                case "consumable_c09_frasco_foco_grande":
                    // garrafa redonda grande laranja, tampa de bronze com gema âmbar
                    VisualKit.Part(root, VisualKit.Sphere(0.27f), vidro, new Vector3(0, -0.1f, 0));
                    VisualKit.Part(root, VisualKit.Sphere(0.24f), VisualKit.Glow(new Color(0.95f, 0.42f, 0.05f), 1.1f), new Vector3(0, -0.12f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.07f, 0.09f, 0.16f, 14), vidro, new Vector3(0, 0.22f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.095f, 0.095f, 0.05f, 14), ouro, new Vector3(0, 0.31f, 0));
                    VisualKit.Part(root, VisualKit.Sphere(0.045f), VisualKit.Glow(new Color(1f, 0.75f, 0.3f), 1.6f), new Vector3(0, 0.36f, 0));
                    break;
                default:   // consumable_c04_frasco_mana_grande
                    // garrafa redonda grande, azul profundo, gargalo e tampa de prata com uma gema
                    VisualKit.Part(root, VisualKit.Sphere(0.27f), vidro, new Vector3(0, -0.1f, 0));
                    VisualKit.Part(root, VisualKit.Sphere(0.24f), VisualKit.Glow(new Color(0.12f, 0.25f, 0.95f), 1.1f), new Vector3(0, -0.12f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.07f, 0.09f, 0.16f, 14), vidro, new Vector3(0, 0.22f, 0));
                    VisualKit.Part(root, VisualKit.Cylinder(0.095f, 0.095f, 0.05f, 14), prata, new Vector3(0, 0.31f, 0));
                    VisualKit.Part(root, VisualKit.Sphere(0.045f), VisualKit.Glow(new Color(0.5f, 0.85f, 1f), 1.6f), new Vector3(0, 0.36f, 0));
                    break;
            }
            return root;
        }
        if (id is "consumable_agua_benta" or "consumable_panaceia" or "consumable_c07_antidoto")
        {
            // frasco redondo (não é a poção de vida): água benta azul-clara com cruz dourada; panaceia dourada; antídoto verde
            var cor = id == "consumable_agua_benta" ? new Color(0.6f, 0.85f, 1f) : id == "consumable_panaceia" ? new Color(1f, 0.8f, 0.3f) : new Color(0.3f, 0.85f, 0.35f);
            var vidro = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.95f, 1f, 0.3f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f };
            VisualKit.Part(root, VisualKit.Sphere(0.22f), vidro, new Vector3(0, -0.08f, 0));
            VisualKit.Part(root, VisualKit.Sphere(0.19f), VisualKit.Glow(cor.Darkened(0.2f), 0.9f), new Vector3(0, -0.1f, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.05f, 0.06f, 0.16f, 12), vidro, new Vector3(0, 0.18f, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.065f, 0.055f, 0.07f, 10), leather, new Vector3(0, 0.28f, 0));
            if (id == "consumable_agua_benta")
            {
                var ouro = VisualKit.Mat(new Color(0.95f, 0.78f, 0.3f), 0.3f, 0.9f);
                VisualKit.Part(root, VisualKit.Box(0.03f, 0.16f, 0.02f), ouro, new Vector3(0, -0.06f, 0.21f));
                VisualKit.Part(root, VisualKit.Box(0.11f, 0.03f, 0.02f), ouro, new Vector3(0, -0.03f, 0.21f));
            }
            return root;
        }
        if (id == "mapa_tesouro")
        {
            // pergaminho aberto com trilha tracejada e um X vermelho
            root.RotationDegrees = new Vector3(55, 0, -10);
            VisualKit.Part(root, VisualKit.Box(0.62f, 0.02f, 0.5f), VisualKit.Mat(new Color(0.86f, 0.76f, 0.52f), 0.95f), Vector3.Zero);
            foreach (var x in new[] { -0.33f, 0.33f })
                VisualKit.Part(root, VisualKit.Cylinder(0.04f, 0.04f, 0.52f, 10), VisualKit.Mat(new Color(0.78f, 0.66f, 0.42f), 0.9f), new Vector3(x, 0.02f, 0), new Vector3(90, 0, 0));
            var tinta = VisualKit.Mat(new Color(0.3f, 0.2f, 0.12f), 0.9f);
            for (var i = 0; i < 6; i++) VisualKit.Part(root, VisualKit.Box(0.05f, 0.012f, 0.015f), tinta, new Vector3(-0.22f + i * 0.07f, 0.015f, 0.12f - i * 0.035f), new Vector3(0, 25, 0));
            var x1 = VisualKit.Mat(new Color(0.8f, 0.08f, 0.06f), 0.6f);
            VisualKit.Part(root, VisualKit.Box(0.14f, 0.014f, 0.03f), x1, new Vector3(0.2f, 0.016f, -0.1f), new Vector3(0, 45, 0));
            VisualKit.Part(root, VisualKit.Box(0.14f, 0.014f, 0.03f), x1, new Vector3(0.2f, 0.016f, -0.1f), new Vector3(0, -45, 0));
            return root;
        }
        if (id.StartsWith("bracelete_"))
        {
            // pulseira de couro com placas de metal e uma pedra na cor da fantasia
            var pedra = id switch
            {
                "bracelete_coelho" => new Color(1f, 0.75f, 0.85f), "bracelete_gravata" => new Color(0.3f, 0.4f, 0.9f),
                "bracelete_cartola" => new Color(0.6f, 0.6f, 0.65f), "bracelete_palhaco" => new Color(1f, 0.25f, 0.2f),
                "bracelete_chef" => new Color(0.97f, 0.97f, 0.95f), "bracelete_pirata" => new Color(0.75f, 0.1f, 0.08f), "bracelete_abobora" => new Color(1f, 0.5f, 0.1f), "bracelete_lata" => new Color(0.7f, 0.72f, 0.75f),
                "bracelete_ouro" => new Color(1f, 0.8f, 0.25f), "bracelete_cristal" => new Color(0.7f, 0.45f, 1f), "bracelete_samurai" => new Color(0.3f, 0.55f, 1f),
                "bracelete_sem_cabeca" => new Color(0.35f, 0.5f, 1f), "bracelete_demonio" => new Color(0.85f, 0.05f, 0.05f), "bracelete_peste" => new Color(0.4f, 1f, 0.3f),
                "bracelete_corvo" => new Color(0.6f, 0.25f, 0.9f), "bracelete_esqueleto" => new Color(0.92f, 0.9f, 0.84f), "bracelete_gnomo" => new Color(0.45f, 0.75f, 0.25f),
                "bracelete_sapo" => new Color(0.35f, 0.7f, 0.25f), "bracelete_peixe" => new Color(0.5f, 0.9f, 1f), "bracelete_pedra" => new Color(0.3f, 0.7f, 1f),
                _ => Color.FromHsv((id.Sum(ch => (int)ch) * 37 % 360) / 360f, 0.7f, 1f),
            };
            root.RotationDegrees = new Vector3(60, 20, 0);
            VisualKit.Part(root, new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.28f, Rings = 28, RingSegments = 10 }, leather, Vector3.Zero);
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.Tau / 6f;
                VisualKit.Part(root, VisualKit.Box(0.07f, 0.09f, 0.03f), iron, new Vector3(Mathf.Sin(a) * 0.28f, 0, Mathf.Cos(a) * 0.28f), new Vector3(0, Mathf.RadToDeg(a), 0));
            }
            VisualKit.Part(root, VisualKit.Sphere(0.07f), VisualKit.Glow(pedra, 1.4f), new Vector3(0, 0, 0.31f));
            return root;
        }
        if (id.StartsWith("caixa_pandora_"))
        {
            // 14/11: Caixa de Pandora +N (Caixa do Kundun do MU) — cubo escuro com quinas de ouro, runas e uma fresta de luz que
            // vaza da tampa; a luz muda com o +N (verde, azul, roxo, laranja, vermelho)
            var t = id[^1] - '0';
            var luz = t switch { 1 => new Color(0.4f, 1f, 0.5f), 2 => new Color(0.35f, 0.7f, 1f), 3 => new Color(0.75f, 0.4f, 1f), 4 => new Color(1f, 0.6f, 0.15f), _ => new Color(1f, 0.2f, 0.15f) };
            var corpo = VisualKit.Mat(new Color(0.12f, 0.08f, 0.14f), 0.45f, 0.3f);
            var ouro = VisualKit.Mat(new Color(1f, 0.78f, 0.3f), 0.25f, 0.95f);
            var brilho = VisualKit.Glow(luz, 2.4f + 0.4f * t);
            VisualKit.Part(root, VisualKit.Box(0.46f, 0.4f, 0.46f), corpo, new Vector3(0, -0.06f, 0), new Vector3(0, 35, 0));
            VisualKit.Part(root, VisualKit.Box(0.5f, 0.1f, 0.5f), corpo, new Vector3(0, 0.2f, 0), new Vector3(0, 35, 0));   // tampa
            VisualKit.Part(root, VisualKit.Box(0.47f, 0.025f, 0.47f), brilho, new Vector3(0, 0.145f, 0), new Vector3(0, 35, 0));   // fresta de luz
            for (var i = 0; i < 4; i++)
            {
                var a = Mathf.DegToRad(35 + 45 + i * 90);
                VisualKit.Part(root, VisualKit.Box(0.05f, 0.52f, 0.05f), ouro, new Vector3(Mathf.Sin(a) * 0.32f, 0.02f, Mathf.Cos(a) * 0.32f));
            }
            VisualKit.Part(root, VisualKit.Sphere(0.06f), brilho, new Vector3(0, 0.29f, 0));   // olho no topo
            for (var k = 0; k < 3; k++) VisualKit.Part(root, VisualKit.Box(0.04f, 0.14f - k * 0.03f, 0.01f), brilho, new Vector3(-0.1f + k * 0.1f, -0.06f, 0.25f), new Vector3(0, 35, k * 25 - 25));   // runas
            return root;
        }
        if (id.StartsWith("caixa_cinzas_"))
        {
            // 13/11: Caixa das Cinzas +N — baú com cantos de metal; a cor sobe com o +N (madeira → bronze → prata → ouro → brasa)
            var t = id[^1] - '0';
            var cor = t switch { 1 => new Color(0.55f, 0.36f, 0.2f), 2 => new Color(0.75f, 0.48f, 0.25f), 3 => new Color(0.72f, 0.76f, 0.82f), 4 => new Color(0.95f, 0.75f, 0.25f), _ => new Color(0.9f, 0.3f, 0.12f) };
            var corpo = VisualKit.Mat(cor, 0.6f, t >= 3 ? 0.6f : 0.1f);
            var metal = VisualKit.Mat(t >= 4 ? new Color(1f, 0.85f, 0.4f) : new Color(0.35f, 0.33f, 0.3f), 0.3f, 0.9f);
            VisualKit.Part(root, VisualKit.Box(0.62f, 0.38f, 0.42f), corpo, new Vector3(0, -0.06f, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.21f, 0.21f, 0.62f, 14), corpo, new Vector3(0, 0.13f, 0), new Vector3(0, 0, 90), new Vector3(1f, 1f, 0.55f));
            foreach (var x in new[] { -0.27f, 0.27f }) VisualKit.Part(root, VisualKit.Box(0.05f, 0.62f, 0.44f), metal, new Vector3(x, 0, 0));
            VisualKit.Part(root, VisualKit.Box(0.1f, 0.12f, 0.05f), metal, new Vector3(0, 0.08f, 0.23f));
            var brilho = new StandardMaterial3D { AlbedoColor = cor.Lightened(0.4f), EmissionEnabled = true, Emission = cor.Lightened(0.3f), EmissionEnergyMultiplier = 0.4f + 0.4f * t };
            VisualKit.Part(root, VisualKit.Sphere(0.05f + 0.01f * t), brilho, new Vector3(0, 0.08f, 0.26f));
            return root;
        }
        if (id.StartsWith("semente_"))
        {
            // saquinho de pano aberto com sementes caindo; a cor da semente diz a planta
            var seed = id switch
            {
                "semente_erva" => new Color(0.45f, 0.62f, 0.25f), "semente_folha" => new Color(0.55f, 0.55f, 0.2f),
                "semente_cogumelo" => new Color(0.6f, 0.58f, 0.55f), "semente_flor" => new Color(0.85f, 0.45f, 0.55f),
                _ => new Color(0.55f, 0.36f, 0.2f),
            };
            var sack = VisualKit.Mat(new Color(0.78f, 0.68f, 0.5f), 0.95f);
            VisualKit.Part(root, VisualKit.Sphere(0.24f), sack, new Vector3(-0.08f, 0, 0), null, new Vector3(1f, 0.9f, 1f));
            VisualKit.Part(root, VisualKit.Cylinder(0.16f, 0.12f, 0.12f, 12), sack, new Vector3(-0.08f, 0.22f, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.125f, 0.125f, 0.03f, 12), VisualKit.Mat(seed.Darkened(0.3f), 0.8f), new Vector3(-0.08f, 0.27f, 0));
            var sm = VisualKit.Mat(seed, 0.6f);
            foreach (var (x, z, a) in new[] { (0.22f, 0.12f, 20f), (0.3f, -0.05f, -40f), (0.16f, 0.22f, 70f), (0.34f, 0.15f, 5f), (0.12f, -0.18f, -15f) })
                VisualKit.Part(root, VisualKit.Sphere(0.045f), sm, new Vector3(x, -0.2f, z), new Vector3(0, a, 0), new Vector3(1.7f, 0.9f, 1f));
            return root;
        }
        switch (id)
        {
            case "mat_frasco":
            {
                // frasco vazio: só o vidro e a rolha, sem líquido
                var glass = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.92f, 1f, 0.4f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, MetallicSpecular = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
                VisualKit.Part(root, VisualKit.Cylinder(0.15f, 0.15f, 0.36f, 18), glass, new Vector3(0, -0.04f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.065f, 0.15f, 0.06f, 18), glass, new Vector3(0, 0.17f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.055f, 0.06f, 0.1f, 12), glass, new Vector3(0, 0.24f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.062f, 0.055f, 0.07f, 10), VisualKit.Mat(new Color(0.45f, 0.30f, 0.18f), 0.9f), new Vector3(0, 0.31f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.12f, 0.12f, 0.01f, 18), VisualKit.Mat(new Color(0.9f, 0.95f, 1f), 0.1f), new Vector3(0, -0.21f, 0));
                return root;
            }
            case "mat_residuo_larva":
            {
                // gosma verde-amarelada em poça com bolhas
                var goo = VisualKit.Glow(new Color(0.55f, 0.75f, 0.15f), 0.5f);
                VisualKit.Part(root, VisualKit.Sphere(0.3f), goo, new Vector3(0, -0.08f, 0), null, new Vector3(1.2f, 0.4f, 1f));
                VisualKit.Part(root, VisualKit.Sphere(0.16f), goo, new Vector3(0.05f, 0.04f, 0.02f), null, new Vector3(1f, 0.8f, 1f));
                foreach (var (x, z, r) in new[] { (-0.18f, 0.1f, 0.06f), (0.2f, -0.08f, 0.05f), (0.12f, 0.16f, 0.04f) })
                    VisualKit.Part(root, VisualKit.Sphere(r), VisualKit.Glow(new Color(0.8f, 0.95f, 0.35f), 0.8f), new Vector3(x, -0.02f, z));
                return root;
            }
            case "mat_rebites":
                // quatro rebites de ferro: cabeça redonda e haste
                foreach (var (x, z, tilt) in new[] { (-0.16f, -0.08f, 0f), (0.1f, -0.14f, 70f), (0.16f, 0.12f, 85f), (-0.08f, 0.16f, 20f) })
                {
                    var r = new Node3D { Position = new Vector3(x, 0, z), RotationDegrees = new Vector3(tilt, x * 300f, 0) };
                    root.AddChild(r);
                    VisualKit.Part(r, VisualKit.Sphere(0.08f), iron, new Vector3(0, 0.08f, 0), null, new Vector3(1f, 0.55f, 1f));
                    VisualKit.Part(r, VisualKit.Cylinder(0.03f, 0.03f, 0.18f, 8), iron, new Vector3(0, -0.02f, 0));
                }
                return root;
            case "mat_liga_geada" or "mat_liga_lendaria":
            {
                // lingote (tronco de pirâmide) — azul-gelo ou dourado
                var lend = id == "mat_liga_lendaria";
                var bar = VisualKit.Mat(lend ? new Color(0.95f, 0.72f, 0.25f) : new Color(0.6f, 0.82f, 0.95f), 0.25f, 0.9f);
                foreach (var (y, yaw) in new[] { (-0.1f, 0f), (0.08f, 12f) })
                    VisualKit.Part(root, VisualKit.Cylinder(0.2f, 0.28f, 0.16f, 4), bar, new Vector3(0, y, 0), new Vector3(0, 45 + yaw, 0), new Vector3(1.6f, 1f, 0.8f));
                VisualKit.Part(root, VisualKit.Sphere(0.05f), VisualKit.Glow(lend ? new Color(1f, 0.8f, 0.4f) : new Color(0.7f, 0.95f, 1f), 2f), new Vector3(0.1f, 0.17f, 0.05f));
                return root;
            }
            case "mat_placa_chapa":
                VisualKit.Part(root, VisualKit.Box(0.6f, 0.04f, 0.45f), iron, Vector3.Zero, new Vector3(0, 0, 8));
                foreach (var (x, z) in new[] { (-0.25f, -0.18f), (0.25f, -0.18f), (-0.25f, 0.18f), (0.25f, 0.18f) })
                    VisualKit.Part(root, VisualKit.Sphere(0.03f), iron, new Vector3(x, 0.03f + x * 0.14f, z));
                return root;
            case "mat_carne_maior":   // 13/11: pinça grande de caranguejo, carne branca saindo da casca
                VisualKit.Part(root, VisualKit.Sphere(0.24f), VisualKit.Mat(new Color(0.85f, 0.32f, 0.18f), 0.5f), new Vector3(-0.1f, 0, 0), null, new Vector3(1.5f, 0.7f, 0.9f));
                VisualKit.Part(root, VisualKit.Sphere(0.18f), VisualKit.Mat(new Color(0.97f, 0.93f, 0.88f), 0.7f), new Vector3(0.22f, 0.02f, 0), null, new Vector3(1.3f, 0.7f, 0.8f));
                VisualKit.Part(root, VisualKit.Cylinder(0.035f, 0.02f, 0.3f, 8), VisualKit.Mat(new Color(0.9f, 0.4f, 0.2f), 0.5f), new Vector3(-0.38f, 0.08f, 0.1f), new Vector3(0, 30, 70));
                return root;
            case "mat_carne_javali":
                VisualKit.Part(root, VisualKit.Sphere(0.26f), VisualKit.Mat(new Color(0.7f, 0.22f, 0.2f), 0.6f), Vector3.Zero, null, new Vector3(1.3f, 0.6f, 1f));
                VisualKit.Part(root, VisualKit.Sphere(0.2f), VisualKit.Mat(new Color(0.95f, 0.85f, 0.8f), 0.7f), new Vector3(0.05f, 0.04f, 0), null, new Vector3(1.2f, 0.5f, 0.8f));
                VisualKit.Part(root, VisualKit.Cylinder(0.04f, 0.04f, 0.3f, 8), VisualKit.Mat(new Color(0.95f, 0.93f, 0.85f), 0.6f), new Vector3(-0.38f, 0, 0), new Vector3(0, 0, 90));
                return root;
            case "mat_couro":
                VisualKit.Part(root, VisualKit.Box(0.6f, 0.05f, 0.45f), leather, Vector3.Zero, new Vector3(0, 0, 5));
                VisualKit.Part(root, VisualKit.Cylinder(0.08f, 0.08f, 0.45f, 12), leather, new Vector3(0.3f, 0.05f, 0), new Vector3(90, 0, 0));
                return root;
            case "mat_resina" or "mat_cera":
            {
                var c = id == "mat_cera" ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.9f, 0.55f, 0.15f);
                var m = new StandardMaterial3D { AlbedoColor = c with { A = 0.85f }, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.15f, EmissionEnabled = true, Emission = c.Darkened(0.6f) };
                if (id == "mat_cera") VisualKit.Part(root, VisualKit.Cylinder(0.24f, 0.24f, 0.22f, 6), m, Vector3.Zero);
                else { VisualKit.Part(root, VisualKit.Sphere(0.22f), m, Vector3.Zero, null, new Vector3(1f, 1.3f, 0.9f)); VisualKit.Part(root, VisualKit.Sphere(0.12f), m, new Vector3(0.2f, -0.12f, 0.05f)); }
                return root;
            }
            case "chave_cofre":
            {
                // 18/11: chave de ferro enferrujada, cabeça em argola com um dente torto
                var ferro = VisualKit.Mat(new Color(0.42f, 0.33f, 0.26f), 0.6f, 0.75f);
                var ferrugem = VisualKit.Mat(new Color(0.55f, 0.28f, 0.12f), 0.9f);
                root.RotationDegrees = new Vector3(10, 20, -40);
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.09f, OuterRadius = 0.16f, Rings = 18 }, ferro, new Vector3(0, 0.27f, 0), new Vector3(90, 0, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.04f, 0.04f, 0.5f, 8), ferro, new Vector3(0, -0.06f, 0));
                VisualKit.Part(root, VisualKit.Box(0.12f, 0.06f, 0.045f), ferro, new Vector3(0.07f, -0.26f, 0));
                VisualKit.Part(root, VisualKit.Box(0.08f, 0.05f, 0.045f), ferro, new Vector3(0.06f, -0.15f, 0), new Vector3(0, 0, 12));
                VisualKit.Part(root, VisualKit.Sphere(0.045f), ferrugem, new Vector3(-0.1f, 0.33f, 0.03f));
                VisualKit.Part(root, VisualKit.Sphere(0.03f), ferrugem, new Vector3(0.02f, 0.05f, 0.03f));
                return root;
            }
            case "mat_chave_bronze" or "mat_chave_ferro" or "mat_chave_prata" or "mat_chave_ouro":
            {
                // 16/11: a chave de ferro usava o desenho do minério
                var bronze = VisualKit.Mat(id switch { "mat_chave_ferro" => new Color(0.55f, 0.56f, 0.6f), "mat_chave_prata" => new Color(0.85f, 0.86f, 0.9f), "mat_chave_ouro" => new Color(0.95f, 0.78f, 0.3f), _ => new Color(0.75f, 0.5f, 0.25f) }, 0.35f, 0.85f);
                root.RotationDegrees = new Vector3(10, 20, -40);
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.14f, Rings = 18 }, bronze, new Vector3(0, 0.25f, 0), new Vector3(90, 0, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.035f, 0.035f, 0.45f, 8), bronze, new Vector3(0, -0.05f, 0));
                VisualKit.Part(root, VisualKit.Box(0.1f, 0.05f, 0.04f), bronze, new Vector3(0.06f, -0.22f, 0));
                VisualKit.Part(root, VisualKit.Box(0.07f, 0.05f, 0.04f), bronze, new Vector3(0.05f, -0.12f, 0));
                return root;
            }
            case "mat_pluma_neve":
                root.RotationDegrees = new Vector3(10, 10, -35);
                VisualKit.Part(root, VisualKit.Sphere(0.2f), VisualKit.Mat(new Color(0.92f, 0.95f, 1f), 0.9f), new Vector3(0, 0.05f, 0), null, new Vector3(0.45f, 1.6f, 0.1f));
                VisualKit.Part(root, VisualKit.Cylinder(0.012f, 0.02f, 0.6f, 6), VisualKit.Mat(new Color(0.8f, 0.82f, 0.85f), 0.6f), Vector3.Zero);
                return root;
            case "mat_reagente":
                return Flask(new Color(0.65f, 0.3f, 0.95f));
            case "mat_regador":
            {
                var brass = VisualKit.Mat(new Color(0.8f, 0.62f, 0.3f), 0.35f, 0.8f);
                VisualKit.Part(root, VisualKit.Cylinder(0.18f, 0.2f, 0.32f, 16), brass, Vector3.Zero);
                VisualKit.Part(root, VisualKit.Cylinder(0.03f, 0.05f, 0.4f, 8), brass, new Vector3(0.28f, 0.05f, 0), new Vector3(0, 0, -55));
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.13f, Rings = 16 }, brass, new Vector3(-0.2f, 0.06f, 0), new Vector3(90, 0, 90));
                return root;
            }
            case "mat_sela":
                VisualKit.Part(root, VisualKit.Sphere(0.3f), leather, Vector3.Zero, null, new Vector3(1.2f, 0.45f, 0.9f));
                VisualKit.Part(root, VisualKit.Cylinder(0.06f, 0.09f, 0.14f, 10), leather, new Vector3(-0.22f, 0.13f, 0));
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.05f, OuterRadius = 0.07f, Rings = 12 }, iron, new Vector3(0.05f, -0.25f, 0.24f));
                return root;
            case "mat_molde_ferreiro":
                VisualKit.Part(root, VisualKit.Box(0.6f, 0.12f, 0.36f), VisualKit.Mat(new Color(0.35f, 0.33f, 0.3f), 0.9f), Vector3.Zero);
                VisualKit.Part(root, VisualKit.Box(0.42f, 0.02f, 0.08f), VisualKit.Mat(new Color(0.15f, 0.14f, 0.13f), 0.9f), new Vector3(0, 0.06f, 0));
                return root;
            case "mat_espada_quebrada":
                root.RotationDegrees = new Vector3(0, 20, -45);
                VisualKit.Part(root, VisualKit.Box(0.08f, 0.42f, 0.02f), iron, new Vector3(0, 0.12f, 0), new Vector3(0, 0, 4));
                VisualKit.Part(root, VisualKit.Box(0.26f, 0.05f, 0.06f), VisualKit.Mat(new Color(0.6f, 0.45f, 0.2f), 0.4f, 0.8f), new Vector3(0, -0.1f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.03f, 0.03f, 0.18f, 8), leather, new Vector3(0, -0.22f, 0));
                return root;
            case "mat_estandarte":
                VisualKit.Part(root, VisualKit.Cylinder(0.02f, 0.02f, 0.75f, 8), VisualKit.Mat(new Color(0.5f, 0.35f, 0.2f), 0.8f), new Vector3(-0.18f, 0, 0));
                VisualKit.Part(root, VisualKit.Box(0.32f, 0.42f, 0.02f), VisualKit.Mat(new Color(0.65f, 0.12f, 0.12f), 0.8f), new Vector3(0, 0.1f, 0));
                VisualKit.Part(root, VisualKit.Sphere(0.05f), VisualKit.Glow(new Color(1f, 0.8f, 0.3f), 1.5f), new Vector3(0, 0.12f, 0.02f));
                return root;
            default:   // estrutura das asas: armação de varetas
                for (var i = 0; i < 4; i++)
                    VisualKit.Part(root, VisualKit.Cylinder(0.015f, 0.025f, 0.6f, 6), VisualKit.Mat(new Color(0.6f, 0.82f, 0.95f), 0.3f, 0.8f), new Vector3(0.05f * i, 0, 0), new Vector3(0, 0, -20 - i * 22f), null);
                return root;
        }
    }

    private static Node3D Material(string model, string definition)
    {
        var root = new Node3D();
        if (definition.Contains("tabua"))
        {
            var wood = VisualKit.Mat(new Color(0.6f, 0.45f, 0.28f), 0.85f);
            for (var i = 0; i < 3; i++)
                VisualKit.Part(root, VisualKit.Box(0.75f, 0.08f, 0.2f), wood, new Vector3(0, i * 0.09f, 0), new Vector3(0, i * 12 - 12, 0));
            return root;
        }
        if (definition.Contains("erva") || definition.Contains("raiz") || definition.Contains("folha"))
        {
            var leaf = VisualKit.Mat(new Color(0.25f, 0.55f, 0.25f), 0.8f);
            for (var i = 0; i < 5; i++)
                VisualKit.Part(root, VisualKit.Box(0.08f, 0.45f, 0.02f), leaf, new Vector3((i - 2) * 0.05f, 0.1f, 0), new Vector3(0, 0, (i - 2) * 18));
            return root;
        }
        var (c, glow) = model switch
        {
            "mat_shell" => (new Color(0.9f, 0.88f, 0.8f), false),
            "mat_cloth" => (new Color(0.45f, 0.18f, 0.2f), false),
            "mat_ash" => (new Color(1f, 0.5f, 0.15f), true),
            "mat_wood" => (new Color(0.6f, 0.45f, 0.28f), false),
            _ => (new Color(0.4f, 0.85f, 1f), true),
        };
        var m = glow ? VisualKit.Glow(c, 1.6f) : VisualKit.Mat(c, 0.7f);
        VisualKit.Part(root, VisualKit.Box(0.4f, 0.3f, 0.3f), m, Vector3.Zero, new Vector3(25, 35, 10));
        VisualKit.Part(root, VisualKit.Box(0.25f, 0.2f, 0.2f), m, new Vector3(0.25f, -0.1f, 0.05f), new Vector3(-15, 10, 30));
        return root;
    }
}
