using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Client.Visuals;
using CinzasDaMare.Shared.World;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Vitrine visual (sem servidor), para comparar o jogo com as pranchas do pacote visual v1 na mesma luz do jogo.
/// Uso: -- --vitrine personagens|monstros|kit [--vitrine-acao windup|recover|idle] --shots "4=arq.png" --quit-after 5
/// Personagens: o mesmo corpo-base do jogador em 4 estágios (prancha 07). Monstros: silhueta + preparação do ataque (prancha 04).
/// Kit: peças modulares da cidade e do campo (prancha 03). Nada aqui toca regras, rede ou banco.
/// </summary>
public partial class Showcase : Node3D
{
    private readonly List<(IEntityVisual Visual, float Phase)> _actors = new();
    private string _action = "idle";
    private float _time;

    public static Showcase Build(Node parent, Camera3D camera, string kind, string action)
    {
        var s = new Showcase { _action = action };
        parent.AddChild(s);
        var layout = new MapLayout("vitrine", "Vitrine", 30, 0, 0, 40, 1, Array.Empty<MapZone>(), Array.Empty<MapObstacle>(), Array.Empty<MapGate>(),
            Array.Empty<MapChest>(), Array.Empty<MapSpawn>(),
            kind.StartsWith("cassino", StringComparison.Ordinal)
                ? new[] { new MapProp("mesa_vinteum", 0, 0, 0), new MapProp("movel_banqueta", -1.3, 1.3, 135), new MapProp("movel_banqueta", 0, 1.6, 180), new MapProp("movel_banqueta", 1.3, 1.3, 225), new MapProp("mesa_cartas", 3.2, -0.5, 0) }
                : Array.Empty<MapProp>()) { Theme = "cidade", Kind = "cidade" };
        // 12/11: "mapa" = um mapa de verdade do Content (VITRINE_CONTENT = pasta, VITRINE_MAPA = id; VITRINE_CAM = "x,y,z,lx,ly,lz")
        if (kind == "mapa" && OS.GetEnvironment("VITRINE_CONTENT") is { Length: > 0 } dir)
        {
            layout = WorldContentLoader.Load(dir).Maps[OS.GetEnvironment("VITRINE_MAPA")];
            // 13/11: VITRINE_RAIO=x,y,r — só as peças perto do ponto (o mapa inteiro estoura a memória aqui na nuvem)
            if (OS.GetEnvironment("VITRINE_RAIO") is { Length: > 0 } rz && rz.Split(',') is { Length: 3 } rp)
            {
                double Px(int i) => double.Parse(rp[i], System.Globalization.CultureInfo.InvariantCulture);
                var (cx, cy, rr) = (Px(0), Px(1), Px(2));
                bool Perto(double x, double y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) < rr * rr;
                layout = layout with { Props = layout.Props.Where(p => Perto(p.X, p.Y)).ToList(), Obstacles = layout.Obstacles.Where(o => Perto(o.X, o.Y)).ToList() };
            }
        }
        var map = new MapView(layout);
        s.AddChild(map.Root);
        // luz de estúdio: sol baixo vindo de trás da câmera (os bonecos ficam de frente para a luz)
        if (map.Sun is { } sun) sun.RotationDegrees = new Vector3(-30f, 20f, 0);
        switch (kind)
        {
            case "monstros": s.Monsters(camera); break;
            case "humanoides": s.Humanoids(camera); break;
            case "bestiario": s.Bestiary(camera, 0); break;
            case "bestiario2": s.Bestiary(camera, 1); break;
            case "kit": s.Kit(map, camera); break;
            case "glb": s.Glb(camera); break;
            case "classes": s.Classes(camera, dressed: true); break;
            case "menutab": s.MenuTab(camera, OS.GetEnvironment("VITRINE_PAG")); break;
            case "base": s.Classes(camera, dressed: false); break;
            case "pet": s.Pet(camera); break;
            case "arsenal": s.Arsenal(camera); break;
            case "lendarias": s.Lendarias(camera); break;
            case "conjuntos": s.Lendarias(camera, true); break;
            case "capacetes": s.Capacetes(camera); break;
            case "encaixe": s.Encaixe(camera); break;
            case "laboratorio": s.Laboratorio(camera); break;
            case "monstro": s.MonstroLista(camera); break;
            case "vila": s.Vila(camera); break;
            case "mapa":
            {
                var c = (OS.GetEnvironment("VITRINE_CAM") is { Length: > 0 } cs ? cs : "0,14,14,0,0,0").Split(',');
                float F(int i) => float.Parse(c[i], System.Globalization.CultureInfo.InvariantCulture);
                camera.Position = new Vector3(F(0), F(1), F(2));
                camera.LookAt(new Vector3(F(3), F(4), F(5)));
                camera.Fov = 60;
                map.SetViewer(F(3), F(5));
                // 13/11 ("manda a versão final do mapa pra eu criar a arte"): VITRINE_ORTO=lado (m) = vista de cima reta,
                // sem perspectiva, norte em cima, centrada em (lx, lz) — base exata para desenhar a ilustração do mapa
                if (float.TryParse(OS.GetEnvironment("VITRINE_ORTO"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lado) && lado > 0)
                {
                    camera.Projection = Camera3D.ProjectionType.Orthogonal;
                    camera.Size = lado;
                    camera.Far = 1000f;
                    camera.Position = new Vector3(F(3), 40f, F(5)); camera.Near = 0.5f;
                    camera.LookAt(new Vector3(F(3), 0, F(5)), Vector3.Forward);
                    if (map.Sun is { } sol) sol.RotationDegrees = new Vector3(-62f, 25f, 0);
                    // sem névoa nem tom do fim de tarde: cores do dia, para servir de base para a arte
                    foreach (var we in s.GetTree().Root.FindChildren("*", "WorldEnvironment", true, false))
                        if (we is WorldEnvironment w && w.Environment is { } env) { env.FogEnabled = false; env.VolumetricFogEnabled = false; }
                }
                break;
            }
            case "cassino": s.Cassino(camera, false); break;
            case "mascotes":
            {
                // 12/11: os 5 mascotes novos lado a lado (andando no lugar)
                var kinds = new[] { "tartaruga", "coelho", "grilo", "macaco", "aguia" };
                for (var i = 0; i < kinds.Length; i++)
                {
                    var v = VisualFactory.Pet(kinds[i]);
                    v.Root.Position = new Vector3(-2.4f + i * 1.2f, 0, 0);
                    v.Root.RotationDegrees = new Vector3(0, 20, 0);
                    s.AddChild(v.Root);
                    s._actors.Add((v, i * 0.3f));
                }
                camera.Position = new Vector3(0, 1.6f, 3.6f);
                camera.LookAt(new Vector3(0, 0.5f, 0));
                camera.Fov = 50;
                break;
            }
            case "cassino_ui": s.Cassino(camera, true); break;
            case "asas": s.Asas(camera); break;
            case "voo": s.Voo(camera); break;
            case "pegada": s.Pegada(camera, 0f, 0); break;
            case "classe2": s.Classe2(camera, 0f); break;
            case "recorrentes": s.Recorrentes(camera); break;
            case "empunhadura": s.Empunhadura(camera, OS.GetEnvironment("VITRINE_PAG") == "2"); break;
            case "combo": s.Combo(camera, float.TryParse(OS.GetEnvironment("VITRINE_YAW"), out var cy) ? cy : 0f); break;
            case "regiao1": s.Regiao1(camera); break;
            case "classe2_lado": s.Classe2(camera, 90f); break;
            case "pegada2": s.Pegada(camera, 0f, 4); break;
            case "pegada_lado": s.Pegada(camera, 90f, 0); break;
            case "pegada_lado2": s.Pegada(camera, 90f, 4); break;
            case "guerreiros": s.Characters(camera, warrior: true); break;
            case "magos": s.Characters(camera, warrior: false); break;
            default: s.Characters(camera); break;
        }
        return s;
    }

    private void Place(IEntityVisual v, Vector3 at, float yawDeg = 0f, float phase = 0f)
    {
        v.Root.Position = at;
        v.Root.RotationDegrees = new Vector3(0, yawDeg, 0);
        AddChild(v.Root);
        _actors.Add((v, phase));
    }

    private void Label(string text, Vector3 at, float size = 36)
        => AddChild(new Label3D
        {
            Text = text, Position = at, FontSize = (int)size, OutlineSize = 10, PixelSize = 0.006f,
            Modulate = new Color(0.95f, 0.85f, 0.6f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        });

    // prancha 07: início (cueca) → primeiras peças → Ferro de Vigília → conjunto raro
    private void Characters(Camera3D camera, bool? warrior = null)
    {
        var stages = new (string Name, string? Wpn, string? Off, string? Body, string? Head, string? MWpn, string? MBody, string? MHead)[]
        {
            ("Início", null, null, null, null, null, null, null),
            ("Primeiras peças", "club_driftwood", null, "body_gambeson", "head_cap", "staff_twig", "body_rags", null),
            ("Ferro de Vigília", "sword_p1_placeholder", "shield_planks", "body_mail", "head_iron", "staff_ember", "body_robe", "head_hood"),
            ("Raro", "sword_vein", "shield_stone", "body_plate", "head_guardian", "staff_vein", "body_robe_vein", "head_hood"),
        };
        if (warrior is { } onlyWarrior)
        {
            for (var i = 0; i < stages.Length; i++)
            {
                var st = stages[i];
                var x = -3.3f + i * 2.2f;
                var v = (HumanoidVisual)VisualFactory.Player(onlyWarrior ? "guerreiro" : "mago", true);
                if (onlyWarrior) { v.SetGear(st.Wpn, st.Off, st.Body); v.SetHead(st.Head); }
                else { v.SetGear(st.MWpn, null, st.MBody); v.SetHead(st.MHead); }
                Place(v, new Vector3(x, 0, 0), 0, i * 0.7f);
                Label(st.Name, new Vector3(x, 2.25f, 0));
            }
            camera.Position = new Vector3(0, 1.6f, 6.2f);
            camera.LookAt(new Vector3(0, 1.0f, 0));
            camera.Fov = 50;
            return;
        }
        for (var i = 0; i < stages.Length; i++)
        {
            var st = stages[i];
            var x = -4.5f + i * 3f;
            var w = (HumanoidVisual)VisualFactory.Player("guerreiro", true);
            w.SetGear(st.Wpn, st.Off, st.Body);
            w.SetHead(st.Head);
            Place(w, new Vector3(x, 0, -1.6f), 0, i * 0.7f);
            var m = (HumanoidVisual)VisualFactory.Player("mago", true);
            m.SetGear(st.MWpn, null, st.MBody);
            m.SetHead(st.MHead);
            Place(m, new Vector3(x, 0, 1.6f), 0, i * 0.7f + 0.3f);
            Label(st.Name, new Vector3(x, 2.35f, -1.6f));
        }
        Label("GUERREIRO (atrás) · MAGO (frente)", new Vector3(0, 3.0f, -1.6f), 30);
        camera.Position = new Vector3(0, 2.4f, 9.5f);
        camera.LookAt(new Vector3(0, 1.0f, 0));
        camera.Fov = 50;
    }

    // as quatro classes no mesmo corpo-base (manequim): de roupa de baixo ou com um conjunto típico
    // 11/11: conferência do pacote da noite — armas esculpidas na mão (arco, talismã, clava, lança, maça) e os mortos de cal
    private void Arsenal(Camera3D camera)
    {
        var sets = new (string Cls, string Wpn, string Name)[]
        {
            ("arqueiro", "bow_short", "Arco curto"), ("arqueiro", "bow_vein", "Arco longo exc."), ("bruxa", "talisman_shell", "Talismã n1"),
            ("bruxa", "talisman_vein", "Talismã n3"), ("guerreiro", "arma_katana_excelente", "Katana"), ("bruxa", "arma_varinha_bruxa_n2", "Varinha"),
            ("arqueiro", "arma_arco_longo_excelente", "Arco arsenal"), ("guerreiro", "arma_machado_guerra_comum", "Machado guerra"),
        };
        for (var i = 0; i < sets.Length; i++)
        {
            var x = -7.5f + i * 1.9f;
            var v = VisualFactory.Player(sets[i].Cls, true);
            v.SetGear(sets[i].Wpn, null, null);
            Place(v, new Vector3(x, 0, 0), 0, i * 0.5f);
            Label(sets[i].Name, new Vector3(x, 2.25f, 0), 28);
        }
        var a = VisualFactory.Monster("monster_cal_carnical", false, false, "carnical_cal");
        Place(a, new Vector3(-1.2f, 0, -2.5f), 0);
        Label("Carniçal", new Vector3(-1.2f, 2.3f, -2.5f), 28);
        var b = VisualFactory.Monster("monster_cal_fantasma", false, false, "fantasma_cal");
        Place(b, new Vector3(1.2f, 0, -2.5f), 0);
        Label("Fantasma", new Vector3(1.2f, 2.3f, -2.5f), 28);
        camera.Position = new Vector3(0, 1.8f, 7.5f);
        camera.LookAt(new Vector3(0, 1.0f, -0.5f));
        camera.Fov = 60;
    }

    // 11/11: as 6 armaduras lendárias do Meshy (4 peças cada) vestidas no manequim
    private void Capacetes(Camera3D camera)
    {
        var ids = new[] { "armadura_acolchoada", "armadura_dragao", "arm_cavaleiro" };
        for (var i = 0; i < ids.Length; i++)
        {
            var x = -1.2f + i * 1.2f;
            var v = VisualFactory.Player("guerreiro", true);
            if (v is MannequinVisual mv) mv.SetHead(ids[i] + ":capacete");
            Place(v, new Vector3(x, 0, 0), 0, 0);
            Label(ids[i], new Vector3(x, 2.4f, 0), 20);
        }
        camera.Position = new Vector3(0, 1.7f, 3.2f);
        camera.LookAt(new Vector3(0, 1.5f, 0));
        camera.Fov = 50;
    }

    private void Lendarias(Camera3D camera, bool novos = false)
    {
        var sets = novos
            ? new (string Cls, string Id)[] { ("guerreiro", "armadura_acolchoada"), ("guerreiro", "armadura_couro_bronze"), ("guerreiro", "armadura_soldado_ferro"),
                ("arqueiro", "armadura_escamas_verdes"), ("guerreiro", "armadura_barbaro_lobo"), ("guerreiro", "armadura_cavaleiro_aco") }
            : new (string Cls, string Id)[] { ("guerreiro", "armadura_dragao"), ("guerreiro", "armadura_abissal"), ("arqueiro", "armadura_lobo"),
            ("arqueiro", "armadura_fenix"), ("mago", "armadura_anjo"), ("bruxa", "armadura_serpente") };
        for (var i = 0; i < sets.Length; i++)
        {
            var x = -5.5f + i * 2.2f;
            var v = VisualFactory.Player(sets[i].Cls, true);
            v.SetGear(null, null, sets[i].Id + ":peitoral");
            if (v is MannequinVisual mv) mv.SetHead(sets[i].Id + ":capacete");
            if (v is MannequinVisual mv2) { mv2.SetLimbs(sets[i].Id + ":luvas+bracadeiras", sets[i].Id + ":botas"); mv2.SetPants(sets[i].Id + ":perneiras"); }
            Place(v, new Vector3(x, 0, 0), 0, i * 0.5f);
            Label(sets[i].Id.Replace("armadura_", ""), new Vector3(x, 2.4f, 0), 28);
        }
        camera.Position = new Vector3(0, 1.6f, 8.5f);
        camera.LookAt(new Vector3(0, 1.0f, 0));
        camera.Fov = 55;
    }

    // 11/11 noite: conferência de encaixe — 3 conjuntos de perto (CDM_SETS="a,b,c", CDM_YAW graus, CDM_ZOOM cabeça|corpo)
    // 12/11: laboratório (Sandbox) sem servidor — mesmo PlayerSandboxManager do jogo, com um controlador "oficial" de mentira.
    // CDM_LAB=1 liga o combate v2 sozinho e ataca os bonecos (o log mostra estado, acertos e passos de física).
    private void Laboratorio(Camera3D camera)
    {
        var hero = VisualFactory.Player(System.Environment.GetEnvironmentVariable("CDM_CLASSE") ?? "guerreiro", true);
        AddChild(hero.Root);
        var yaw = Mathf.Pi;   // olhando para -Z (para os bonecos)
        hero.Root.Rotation = new Vector3(0, yaw, 0);
        Controllers.PlayerHandoff Snap() => new(hero.Root.Position, Vector3.Zero, yaw, hero, null, null);
        var official = new Controllers.NetworkPlayerController(_ => { }, _ => false, Snap, (_, _) => { }, _ => { }, _ => { });
        AddChild(official);
        var lab = new Sandbox.Core.PlayerSandboxManager(official, () => Snap(), () => "vitrine", () => camera);
        AddChild(lab);
        camera.Position = new Vector3(5.5f, 4.2f, 3.2f);
        camera.LookAt(new Vector3(0, 0.9f, -1.8f));
        camera.Fov = 50;
    }

    // 12/11: lista livre de monstros (CDM_VIS="def:visual,def:visual") lado a lado, com referência de altura (jogador)
    private void MonstroLista(Camera3D camera)
    {
        var list = (System.Environment.GetEnvironmentVariable("CDM_VIS") ?? "x:gnome").Split(',');
        var hero = VisualFactory.Player("guerreiro", false);
        Place(hero, new Vector3(-(list.Length + 1) * 0.8f, 0, 0), 0f);
        for (var i = 0; i < list.Length; i++)
        {
            var parts = list[i].Split(':');
            var v = VisualFactory.Monster(parts[0], false, false, parts.Length > 1 ? parts[1] : "");
            var x = (i - (list.Length - 1) / 2f) * 1.6f;
            Place(v, new Vector3(x, 0, 0), 0f, i * 0.3f);
            Label(parts.Length > 1 && parts[1].Length > 0 ? parts[1] : parts[0], new Vector3(x, 2.4f, 0), 20);
        }
        camera.Position = new Vector3(0, 1.4f, 3.2f + list.Length * 0.9f);
        camera.LookAt(new Vector3(0, 0.9f, 0));
        camera.Fov = 50;
    }

    // 12/11: peças do pacote Vila/Formas/Cartas lado a lado (conferir tamanho e textura)
    private void Vila(Camera3D camera)
    {
        var files = (System.Environment.GetEnvironmentVariable("CDM_GLB") ?? "cenario/cenario_tocha_chao,cenario/cenario_placa,cenario/cenario_tenda_lona,cenario/cenario_moita_erva,cenario/cenario_arvore_tabua,cenario/cenario_veio_ferro,cenario/cenario_redes,cenario/cenario_barco_encalhado,cenario/cenario_mesa_cartas,cenario/cenario_arvore_serpente,criaturas/cao_tres_cabecas,costa/costa_arbusto_a").Split(',');
        var cols = 6;
        for (var i = 0; i < files.Length; i++)
        {
            var x = (i % cols - (cols - 1) / 2f) * 4.2f; var z = -(i / cols) * 6f;
            var n = new Node3D { Position = new Vector3(x, 0, z) };
            AddChild(n);
            MapKit.Glb(n, files[i], Vector3.Zero, 20f, 1f);
            Label(files[i].Split('/')[^1].Replace("cenario_", ""), new Vector3(x, 3.6f, z), 18);
        }
        var hero = VisualFactory.Player("guerreiro", false);
        Place(hero, new Vector3(-14.5f, 0, 0), 0f);
        camera.Position = new Vector3(0, 7f, 15f);
        camera.LookAt(new Vector3(0, 1.0f, -3f));
        camera.Fov = 55;
    }

    private void Encaixe(Camera3D camera)
    {
        var ids = (System.Environment.GetEnvironmentVariable("CDM_SETS") ?? "armadura_cavaleiro_aco").Split(',');
        var yaw = float.TryParse(System.Environment.GetEnvironmentVariable("CDM_YAW"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) ? y : 0f;
        var cls = System.Environment.GetEnvironmentVariable("CDM_CLASSE") ?? "guerreiro";
        for (var i = 0; i < ids.Length; i++)
        {
            var x = (i - (ids.Length - 1) / 2f) * 1.3f;
            var v = VisualFactory.Player(cls, true);
            v.SetGear(null, null, ids[i] + ":peitoral");
            if (v is MannequinVisual mv) { mv.SetHead(ids[i] + ":capacete"); mv.SetLimbs(ids[i] + ":luvas+bracadeiras", ids[i] + ":botas"); mv.SetPants(ids[i] + ":perneiras"); }
            if (v is MannequinVisual gv && int.TryParse(System.Environment.GetEnvironmentVariable("CDM_GLOW"), out var glow)) gv.SetArmorGlow(glow + i * 5);
            Place(v, new Vector3(x, 0, 0), yaw, 0f);
            Label(ids[i].Replace("armadura_", ""), new Vector3(x, 2.15f, 0), 20);
        }
        var head = System.Environment.GetEnvironmentVariable("CDM_ZOOM") == "cabeca";
        camera.Position = head ? new Vector3(0, 1.75f, 2.6f) : new Vector3(0, 1.2f, 4.2f);
        camera.LookAt(head ? new Vector3(0, 1.6f, 0) : new Vector3(0, 1.0f, 0));
        camera.Fov = head ? 40 : 50;
    }

    // 11/11 noite: monstros novos do trabalhador (região 1)
    private void Regiao1(Camera3D camera)
    {
        var mons = new (string Def, string Vis)[] { ("x", "rat"), ("x", "rat_big"), ("x", "saci"), ("x", "dwarf"), ("x", "gnome"), ("x", "crab_larva"),
            ("x", "crab_venom"), ("x", "raider_digger"), ("monster_r1_saqueador_da_vigilia", ""), ("monster_r1_acolito_das_cinzas", ""), ("monster_r1_capataz_da_pedreira", ""), ("x", "sentinel") };
        for (var i = 0; i < mons.Length; i++)
        {
            var x = -8.25f + (i % 6) * 3.3f; var z = i < 6 ? 0f : 5f;
            var v = VisualFactory.Monster(mons[i].Def, false, false, mons[i].Vis);
            Place(v, new Vector3(x, 0, z), 180f, i * 0.2f);
            Label(mons[i].Vis.Length > 0 ? mons[i].Vis : mons[i].Def.Replace("monster_r1_", ""), new Vector3(x, 2.6f, z), 22);
        }
        camera.Position = new Vector3(0, 4.5f, -9.5f);
        camera.LookAt(new Vector3(0, 1.0f, 2.5f));
        camera.Fov = 60;
    }

    // 12/11: Capitão Fallen (rifle, barba ruiva) e Vacho, o moleque do barril — de frente e de três quartos
    private void Recorrentes(Camera3D camera)
    {
        var a = VisualFactory.NpcVisual("fallen", "fallen_costa", "Fallen, o Capitão");
        Place(a, new Vector3(-1.3f, 0, 0), 180f, 0f);
        var b = VisualFactory.NpcVisual("fallen", "fallen_vale", "Fallen, o Capitão");
        Place(b, new Vector3(0.2f, 0, 0), 230f, 0f);
        var c = VisualFactory.NpcVisual("vacho", "vacho_porto", "Vacho, o Moleque do Barril");
        Place(c, new Vector3(1.8f, 0, 0), 180f, 0f);
        camera.Position = new Vector3(0, 1.45f, -3.6f);
        camera.LookAt(new Vector3(0, 1.1f, 0));
        camera.Fov = 50;
    }

    // 12/11 ("segurando a espada pela lâmina, lança de uma mão com duas — siga a vida real"): todas as armas do Guerreiro
    // na mão, de lado, para conferir a pegada (cabo na mão, lâmina para fora)
    private void Empunhadura(Camera3D camera, bool pag2)
    {
        var all = new[] { "sword_p1_placeholder", "sword_claw", "sword_ornate", "sword_vein", "arma_espada_curta_comum", "arma_adaga_comum", "arma_faca_comum", "arma_cutelo_abissal",
            "arma_clava_comum", "arma_maca_comum", "arma_maca_escorpiao", "arma_machado_mao_comum", "hatchet", "club_driftwood", "spear_watch", "arma_graveto_pregos",
            "arma_espada_longa_comum", "arma_katana_comum", "arma_lanca_comum", "arma_lanca_valquiria", "arma_glaive_carmesim", "arma_machado_guerra_comum", "arma_machado_leao_dourado", "arma_machado_lobo_sombrio",
            "arma_machado_presa_javali", "arma_martelo_carneiro_fundido", "arma_martelo_chifre_carneiro", "arma_montante_dragao_rubro", "axe_quarry", "hammer_foreman", "arma_cutelo_abissal", "sword_claw" };
        if (OS.GetEnvironment("VITRINE_ARMAS") is { Length: > 0 } va) all = va.Split(',');   // 14/11: lista própria
        var from = int.TryParse(OS.GetEnvironment("VITRINE_DE"), out var de) ? de : pag2 ? 16 : 0;
        var list = all.Skip(from).Take(4).ToArray();
        for (var i = 0; i < list.Length; i++)
        {
            var row = 0; var x = -2.7f + i * 1.8f;
            var v = VisualFactory.Player("guerreiro", true);
            v.SetGear(list[i], null, null);
            Place(v, new Vector3(x, 0, row * -3.2f), 90f, 0f);
            Label(list[i].Replace("arma_", "").Replace("_comum", ""), new Vector3(x, 2.25f, row * -3.2f), 20);
        }
        camera.Position = new Vector3(0, 1.4f, 4.6f);
        camera.LookAt(new Vector3(0, 1.0f, 0));
        camera.Fov = 55;
    }

    // 12/11 (relato: "uso o acolchoado com o capacete de couro e bronze — pedaços faltando; tirei o capacete e a cabeça ficou
    // minúscula"): 4 guerreiros com Padded completo + elmo Bronzehide; os da direita tiram o capacete depois de 1,5 s
    private void Combo(Camera3D camera, float yaw)
    {
        for (var i = 0; i < 4; i++)
        {
            var x = -2.7f + i * 1.8f;
            var v = VisualFactory.Player("guerreiro", true);
            v.SetGear(null, null, "armadura_acolchoada:peitoral");
            if (v is MannequinVisual mv)
            {
                mv.SetLimbs("armadura_acolchoada:luvas", "armadura_acolchoada:botas");
                mv.SetPants("armadura_acolchoada:perneiras");
                mv.SetEvolved(i % 2 == 0);
                mv.SetHead("armadura_couro_bronze:capacete");
                if (i >= 2) GetTree().CreateTimer(1.5).Timeout += () => mv.SetHead(null);
            }
            Place(v, new Vector3(x, 0, 0), yaw + 180f, 0f);
            Label((i % 2 == 0 ? "evoluído" : "normal") + (i >= 2 ? " sem elmo" : ""), new Vector3(x, 2.3f, 0), 22);
        }
        camera.Position = new Vector3(0, 1.5f, -4.6f);
        camera.LookAt(new Vector3(0, 1.2f, 0));
        camera.Fov = 50;
    }

    // 11/11 noite: classe evoluída (cabeça do bicho) de frente e de lado
    private void Classe2(Camera3D camera, float yaw)
    {
        var cls = new[] { "guerreiro", "mago", "arqueiro", "bruxa" };
        for (var i = 0; i < cls.Length; i++)
        {
            var x = -2.7f + i * 1.8f;
            var v = VisualFactory.Player(cls[i], true);
            if (v is MannequinVisual mv)
            {
                mv.SetEvolved(OS.GetEnvironment("VITRINE_SEMEVO") != "1");
                // 12/11: VITRINE_ELMO=1 — classe evoluída de capacete (a "cabeçona")
                if (OS.GetEnvironment("VITRINE_ELMO") == "1") mv.SetHead(i == 0 ? "armadura_dragao:capacete" : i == 1 ? "head_hood" : i == 3 ? "mascara_pandemonio" : "head_iron");
                if (OS.GetEnvironment("VITRINE_C3") == "1") mv.SetClasse3(true);   // 12/11: 3ª classe (leão)
                if (OS.GetEnvironment("VITRINE_SKINS") is { Length: > 0 } sks) mv.SetSkin(sks.Split(',')[i % sks.Split(',').Length]);   // 14/11: skin por cima da classe
            }
            Place(v, new Vector3(x, 0, 0), yaw + 180f, 0f);
            Label(cls[i], new Vector3(x, 2.3f, 0), 22);
        }
        camera.Position = new Vector3(0, 1.5f, -4.6f);
        camera.LookAt(new Vector3(0, 1.3f, 0));
        camera.Fov = 50;
    }

    // 11/11 noite: como cada tipo de arma fica na mão (parado), de frente e de lado
    private void Pegada(Camera3D camera, float yaw, int from)
    {
        var sets = new (string Cls, string Wpn)[] { ("guerreiro", "sword_claw"), ("guerreiro", "arma_katana_comum"), ("guerreiro", "axe_quarry"),
            ("guerreiro", "arma_machado_mao_comum"), ("arqueiro", "bow_bone"), ("mago", "staff_crystal"), ("guerreiro", "arma_lanca_comum"), ("guerreiro", "arma_montante_dragao_rubro") };
        for (var i = from; i < from + 4; i++)
        {
            var x = -2.7f + (i - from) * 1.8f;
            var v = VisualFactory.Player(sets[i].Cls, true);
            v.SetGear(sets[i].Wpn, null, null);
            Place(v, new Vector3(x, 0, 0), yaw + 180f, i * 0.3f);
            Label(sets[i].Wpn.Replace("arma_", ""), new Vector3(x, 2.3f, 0), 22);
        }
        camera.Position = new Vector3(0, 1.3f, -5.2f);
        camera.LookAt(new Vector3(0, 1.0f, 0));
        camera.Fov = 50;
    }

    // 11/11 noite: voo de lado (pernas juntas, pés apontados) e as asas antigas (ferro, neve) com os modelos novos
    private void Voo(Camera3D camera)
    {
        var sets = new (string Cls, string Kind, bool Fly)[] { ("guerreiro", "ferro", false), ("arqueiro", "dragao", true), ("bruxa", "neve", true) };
        for (var i = 0; i < sets.Length; i++)
        {
            var x = -3.5f + i * 3.5f;
            var v = VisualFactory.Player(sets[i].Cls, true);
            if (v is IPlayerVisual pv) pv.SetWings(sets[i].Kind);
            if (v is MannequinVisual mv) mv.Flying = sets[i].Fly;
            Place(v, new Vector3(x, 0, 0), i == 0 ? 180f : 90f, i * 0.5f);
            Label(sets[i].Kind + (sets[i].Fly ? " (voando)" : ""), new Vector3(x, 3.6f, 0), 28);
        }
        camera.Position = new Vector3(0, 3.0f, -6.5f);
        camera.LookAt(new Vector3(0, 2.6f, 0));
        camera.Fov = 55;
    }

    // 11/11: asas de 2º nível de cada classe, vistas de trás (batendo)
    private void Asas(Camera3D camera)
    {
        var sets = new (string Cls, string Kind)[] { ("guerreiro", "dragao"), ("mago", "abismo"), ("arqueiro", "aguia"), ("bruxa", "cristal") };
        for (var i = 0; i < sets.Length; i++)
        {
            var x = -4.5f + i * 3f;
            var v = VisualFactory.Player(sets[i].Cls, true);
            if (v is IPlayerVisual pv) pv.SetWings(sets[i].Kind);
            Place(v, new Vector3(x, 0, 0), 30f, i * 0.5f);
            Label(sets[i].Kind, new Vector3(x, 2.8f, 0), 28);
        }
        camera.Position = new Vector3(0, 2.0f, -9f);
        camera.LookAt(new Vector3(0, 1.2f, 0));
        camera.Fov = 55;
    }

    private void Classes(Camera3D camera, bool dressed)
    {
        var sets = new (string Cls, string Name, string? Wpn, string? Off, string? Body, string? Head)[]
        {
            ("guerreiro", "Guerreiro", "sword_vein", "shield_stone", "body_plate", "head_iron"),
            ("mago", "Mago", "staff_ember", null, "body_robe", "head_hood"),
            ("bruxa", "Bruxa", "talisman_vein", null, "body_witch", null),
            ("arqueiro", "Arqueiro", "bow_yew", null, "body_ranger", "head_hood"),
        };
        for (var i = 0; i < sets.Length; i++)
        {
            var st = sets[i];
            var x = -3.3f + i * 2.2f;
            var v = VisualFactory.Player(st.Cls, true);
            if (i == 0 && OS.GetEnvironment("VITRINE_MEDIR") == "1" && v is MannequinVisual mv) mv.MeasureClips();
            if (dressed)
            {
                v.SetGear(st.Wpn, st.Off, st.Body);
                if (v is IPlayerVisual pv) pv.SetHead(st.Head);
            }
            var yaw = float.TryParse(OS.GetEnvironment("VITRINE_YAW"), out var y) ? y : 0f;
            Place(v, new Vector3(x, 0, 0), yaw, i * 0.7f);
            if (OS.GetEnvironment("VITRINE_EVO") == "1" && v is MannequinVisual ev) ev.SetEvolved(true);   // cara de bicho (classe evoluída)
            if (OS.GetEnvironment("VITRINE_AURA") == "1" && v is MannequinVisual au) au.SetAura(true);   // 13/11: círculo de runas
            if (OS.GetEnvironment("VITRINE_SKINS") is { Length: > 0 } skins && v is MannequinVisual sk)   // 13/11: skins do bracelete, uma por boneco
            { sk.SetHead(null); sk.SetSkin(skins.Split(',')[i % skins.Split(',').Length]); }
            if (OS.GetEnvironment("VITRINE_ASAS") is { Length: > 0 } asas && v is MannequinVisual wv)   // 14/11 noite: asas/capas (capa:x), uma por boneco
                wv.SetWings(asas.Split(',')[i % asas.Split(',').Length]);
            if (OS.GetEnvironment("VITRINE_VOO") == "1" && v is MannequinVisual fv) fv.Flying = true;   // 14/11: pose de voo
            if (OS.GetEnvironment("VITRINE_BARRIL") == "1" && v is MannequinVisual bv)   // 15/11: barril abraçado
            {
                bv.CarregandoBarril = true;
                var keg = Main.KegNode();
                keg.Position = new Vector3(0, Main.BarrilAltura, 0.34f); keg.RotationDegrees = new Vector3(-8f, 0f, 0f); keg.Scale = Vector3.One * 0.62f;
                v.Root.AddChild(keg);
            }
            Label(st.Name, new Vector3(x, 2.15f, 0));
        }
        camera.Position = new Vector3(0, 1.5f, 6.4f);
        camera.LookAt(new Vector3(0, 1.0f, 0));
        camera.Fov = 50;
        CloseUp(camera);
    }

    /// <summary>VITRINE_ROSTOS=1: close (VITRINE_X/Y/Z) para conferir rosto, cabelo, barba, traços.</summary>
    private static void CloseUp(Camera3D camera)
    {
        if (OS.GetEnvironment("VITRINE_ROSTOS") == "1")
        {
            var cx = float.TryParse(OS.GetEnvironment("VITRINE_X"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vx) ? vx : 0f;
            var cy = float.TryParse(OS.GetEnvironment("VITRINE_Y"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vy) ? vy : 1.55f;
            var cz = float.TryParse(OS.GetEnvironment("VITRINE_Z"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vz) ? vz : 3.2f;
            camera.Position = new Vector3(cx, cy + 0.07f, cz);
            camera.LookAt(new Vector3(cx, cy, 0));
            camera.Fov = 34;
        }
    }

    private void Pet(Camera3D camera)
    {
        var d = VisualFactory.Pet("dragao");
        Place(d, new Vector3(-0.9f, 0, 0), 30);
        var me = VisualFactory.Player("arqueiro", true);
        me.SetGear("bow_yew", null, "body_ranger");
        Place(me, new Vector3(0.6f, 0, 0), -20);
        camera.Position = new Vector3(0, 1.4f, 3.8f);
        camera.LookAt(new Vector3(0, 0.95f, 0));
        camera.Fov = 50;
    }

    // prancha 04: bestiário inicial — os dois monstros refeitos à frente, os outros atrás para comparar escala
    /// <summary>Inimigos humanoides feitos do manequim (goblins, saqueador, acólito, troll, gigante, necromante…).</summary>
    private void Humanoids(Camera3D camera)
    {
        var set = new (string Label, IEntityVisual V)[]
        {
            ("Goblin", VisualFactory.Monster("", false, false, "goblin", skin: 1)),
            ("Goblin (skin 2)", VisualFactory.Monster("", false, false, "goblin", skin: 2)),
            ("Arqueiro goblin", VisualFactory.Monster("", false, false, "goblin_archer", skin: 1)),
            ("Arqueiro goblin (skin 2)", VisualFactory.Monster("", false, false, "goblin_archer", skin: 2)),
            ("Esqueleto de goblin", VisualFactory.Monster("", false, false, "goblin_skeleton")),
            ("Chefe goblin", VisualFactory.Monster("", false, false, "goblin_chief", skin: 1)),
            ("Chefe goblin (skin 2)", VisualFactory.Monster("", false, false, "goblin_chief", skin: 2)),
            ("Saqueador", VisualFactory.Monster("monster_r1_saqueador_da_vigilia", false, false)),
            ("Acólito", VisualFactory.Monster("monster_r1_acolito_das_cinzas", false, false)),
            ("Troll", VisualFactory.Monster("", false, false, "troll")),
            ("Gigante", VisualFactory.Monster("", false, false, "giant")),
            ("Necromante", VisualFactory.Monster("", false, false, "necro")),
        };
        for (var i = 0; i < set.Length; i++)
        {
            var x = -7.7f + i * 2.2f;
            Place(set[i].V, new Vector3(x, 0, 0), float.TryParse(OS.GetEnvironment("VITRINE_YAW"), out var yw) ? yw : 15f, i * 0.4f);
            Label(set[i].Label, new Vector3(x, set[i].V.Height + 0.45f, 0));
        }
        camera.Position = new Vector3(0, 2.2f, 10.5f);
        camera.LookAt(new Vector3(0, 1.3f, 0));
        camera.Fov = 55;
        CloseUp(camera);
    }

    private void Monsters(Camera3D camera)
    {
        var front = new (string Label, IEntityVisual V)[]
        {
            ("Larva de Crosta", VisualFactory.Monster("", false, false, "crab_larva")),
            ("Caranguejo de Cal", VisualFactory.Monster("", false, false, "crab_small")),
            ("Caranguejo Couraçado", VisualFactory.Monster("", false, false, "crab_armored")),
            ("Saqueador", VisualFactory.Monster("monster_r1_saqueador_da_vigilia", false, false)),
        };
        var back = new (string Label, IEntityVisual V)[]
        {
            ("Lobo", VisualFactory.Monster("", false, false, "wolf")),
            ("Javali", VisualFactory.Monster("", false, false, "boar")),
            ("Besouro", VisualFactory.Monster("", false, false, "beetle")),
            ("Aranha", VisualFactory.Monster("", false, false, "spider")),
            ("Olho Alado", VisualFactory.Monster("", false, false, "eye_winged")),
        };
        for (var i = 0; i < front.Length; i++)
        {
            var x = -5.4f + i * 3.6f;
            Place(front[i].V, new Vector3(x, 0, 1.2f), 25, i * 0.4f);
            if (i == 0) front[i].V.Root.Scale = Vector3.One * 1.25f;
            Label(front[i].Label, new Vector3(x, front[i].V.Height + 0.5f, 1.2f));
        }
        for (var i = 0; i < back.Length; i++)
        {
            var x = -5.4f + i * 3.6f;
            Place(back[i].V, new Vector3(x, 0, -3f), 25, i * 0.5f);
            Label(back[i].Label, new Vector3(x, back[i].V.Height + 0.5f, -3f));
        }
        // referência de escala: o jogador de cueca
        var me = VisualFactory.Player("guerreiro", true);
        Place(me, new Vector3(8.6f, 0, 1.2f), -30);
        Label("Jogador", new Vector3(8.6f, 2.3f, 1.2f));
        camera.Position = new Vector3(1.2f, 3.2f, 9.5f);
        camera.LookAt(new Vector3(1.2f, 0.6f, -0.5f));
        camera.Fov = 58;
    }

    // bestiário (09/11): duas pranchas com as criaturas novas, em duas fileiras cada
    private void Bestiary(Camera3D camera, int page)
    {
        var all = new (string Label, string Vis)[]
        {
            ("Rato", "rat"), ("Ratazana", "rat_big"), ("Meleca", "slime"), ("Planta", "plant"), ("Saci", "saci"), ("Anão", "dwarf"),
            ("Zumbi", "zombie"), ("Lobisomem", "werewolf"), ("Chupa-cabra", "chupacabra"), ("Múmia", "mummy"), ("Verme", "worm_giant"), ("Alien", "alien"),
            ("Sapo gigante", "frog_giant"), ("Meleca do brejo", "slime_bog"), ("Boca-de-lodo", "plant_bog"), ("Vampiro", "vampire"), ("A Morte", "death"), ("Wendigo", "wendigo"),
            ("Ogro", "ogre"), ("Anão da montanha", "dwarf_mountain"), ("Demônio", "demon"), ("Cão de 3 cabeças", "cerberus"), ("Dragão", "dragon"), ("Anjo", "angel"), ("Gênio", "genie"), ("Goblin esqueleto", "goblin_skeleton"), ("Esqueleto", "skeleton"), ("Caranguejinho", "crab_small"), ("Caranguejo de Cal", "monster_crab_placeholder"), ("Pinça de Ferro", "crab_armored"),
            ("Caranguejo de Pedra", "crab_stone"), ("Ermitão", "crab_hermit"), ("Caranguejo-Geodo", "crab_geode"), ("Escavador", "crab_digger"), ("Tirano da Maré", "crab_tyrant"),
            ("Rei das Pinças", "crab_king"), ("Javali", "boar"), ("Goblin Chefe", "goblin_chief"), ("Goblin Arqueiro", "goblin_archer"), ("Caranguejo Dourado", "monster_crab_gold_placeholder"), ("Goblin", "goblin"),
        };
        var list = all.Skip(page * 12).Take(page == 0 ? 12 : 13).ToArray();
        if (OS.GetEnvironment("VITRINE_SO") is { Length: > 0 } only)   // só algumas, de perto: VITRINE_SO=cerberus,rat
        {
            var want = only.Split(',');
            list = all.Where(a => want.Contains(a.Vis)).ToArray();
            for (var i = 0; i < list.Length; i++)
            {
                var v = VisualFactory.Monster("", false, false, list[i].Vis);
                var x = (i - (list.Length - 1) / 2f) * 3.2f;
                Place(v, new Vector3(x, 0, 0), float.TryParse(OS.GetEnvironment("VITRINE_YAW"), out var yw) ? yw : 35f, i * 0.37f);
                Label(list[i].Label, new Vector3(x, v.Height + 0.4f, 0));
            }
            camera.Position = new Vector3(0, 1.9f, 7.5f);
            camera.LookAt(new Vector3(0, 0.8f, 0));
            camera.Fov = 50;
            return;
        }
        var row = (list.Length + 1) / 2;
        for (var i = 0; i < list.Length; i++)
        {
            var back = i >= row;
            var k = back ? i - row : i;
            var x = -8f + k * (16f / Math.Max(1, row - 1));
            var z = back ? -3.2f : 1.6f;
            var v = VisualFactory.Monster("", false, false, list[i].Vis);
            Place(v, new Vector3(x, 0, z), 25, i * 0.37f);
            Label(list[i].Label, new Vector3(x, v.Height + 0.5f, z));
        }
        var me = VisualFactory.Player("guerreiro", true);
        Place(me, new Vector3(9.8f, 0, 1.6f), -30);
        Label("Jogador", new Vector3(9.8f, 2.3f, 1.6f));
        camera.Position = new Vector3(1f, 2.6f, 9.5f);
        camera.LookAt(new Vector3(1f, 0.9f, -1.5f));
        camera.Fov = 66;
        CloseUp(camera);
    }

    // prancha 03: kit modular
    private void Kit(MapView map, Camera3D camera)
    {
        var wood = VisualKit.Mat(new Color(0.33f, 0.22f, 0.13f), 0.9f);
        var pieces = OS.GetEnvironment("VITRINE_PECAS") is { Length: > 0 } list ? list.Split(',')
            : new[] { "portao_cidade", "casa_enxaimel", "forja", "barraca", "pinheiros", "rocha", "coluna_quebrada", "poste", "estandarte" };
        for (var i = 0; i < pieces.Length; i++)
        {
            var node = new Node3D { Position = new Vector3(-16f + i * 4f, 0, i % 2 == 0 ? -2f : 2f) };
            map.Root.AddChild(node);
            if (!MapKit.TryBuild(map, node, pieces[i], wood)) Label("(sem peça) " + pieces[i], node.Position + new Vector3(0, 1, 0));
            Label(pieces[i], node.Position + new Vector3(0, 5.6f, 0), 30);
        }
        camera.Position = new Vector3(0, 8f, 20f);
        camera.LookAt(new Vector3(0, 2f, 0));
        camera.Fov = 60;
    }

    // 12/11: mesa de Vinte-e-Um (3D) e, com ui, as telas novas (mesa, troca e chat) com estados de exemplo
    private void Cassino(Camera3D camera, bool ui)
    {
        camera.Position = new Vector3(0, 3.2f, 4.2f);
        camera.LookAt(new Vector3(0, 0.6f, 0));
        camera.Fov = 50;
        if (!ui) return;
        var layer = new CanvasLayer();
        AddChild(layer);
        var model = new CinzasDaMare.Shared.Client.ClientModel();
        var mesa = new VinteUmPanel();
        layer.AddChild(mesa.Root);
        mesa.Handle(new CinzasDaMare.Shared.Net.VinteUmStateMessage(true, "dado_crupie",
            new[] { new CinzasDaMare.Shared.Net.VinteUmHand(new[] { "10C", "6E" }, 16, 40, "pendente") }, new[] { "KO", "??" }, null, 0, false,
            new[] { "pedir", "parar", "dobrar" }, 0, 1260, 10, 500));
        var chat = new ChatView();
        layer.AddChild(chat.Root);
        model.Apply(new CinzasDaMare.Shared.Net.ChatMessage("Magreka", "bora na dg do sino?", "global"));
        model.Apply(new CinzasDaMare.Shared.Net.ChatMessage("Barto", "partiu, to no porto", "global"));
        model.Apply(new CinzasDaMare.Shared.Net.ChatMessage("Magreka", "me passa a joia depois", "privado", "Barto"));
        model.Apply(new CinzasDaMare.Shared.Net.ChatMessage("", "\"Zeca\" não está online.", "sistema"));
        var troca = new TrocaPanel();
        layer.AddChild(troca.Root);
        troca.Handle(new CinzasDaMare.Shared.Net.TradeStateMessage("aberta", "Magreka",
            new[] { new CinzasDaMare.Shared.Net.InventoryItemView("i1", "joia_mare", "comum", -1, 2), new CinzasDaMare.Shared.Net.InventoryItemView("i2", "helmet_guerreiro_couro", "v07", -1, 1) },
            new[] { new CinzasDaMare.Shared.Net.InventoryItemView("i3", "pocao_vida", "comum", -1, 5) }, 150, 0, true, false));
        var showTrade = OS.GetEnvironment("VITRINE_TROCA") == "1";
        troca.Root.Visible = showTrade;
        mesa.Root.Visible = !showTrade;
        _uiTick = () => { chat.Update(model, 1.0); if (showTrade) troca.Update(model); else mesa.Update(); };
    }

    private Action? _uiTick;

    // modelo .glb de fora (inspeção): -- --vitrine glb --glb-files "a.glb;b.glb" [--glb-anim Walking]
    private void Glb(Camera3D camera)
    {
        var args = OS.GetCmdlineUserArgs();
        string Arg(string k, string d) { var i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : d; }
        var files = Arg("--glb-files", "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        var anim = Arg("--glb-anim", "");
        for (var i = 0; i < files.Length; i++)
        {
            var doc = new GltfDocument();
            var state = new GltfState();
            var err = doc.AppendFromFile(files[i], state);
            if (err != Error.Ok) { GD.PrintErr($"glb {files[i]}: {err}"); continue; }
            var scene = (Node3D)doc.GenerateScene(state);
            scene.Position = new Vector3(-1.2f * (files.Length - 1) + i * 2.4f, 0, 0);
            AddChild(scene);
            var player = FindPlayer(scene);
            if (player is not null)
            {
                GD.Print($"anims {files[i]}: {string.Join(",", player.GetAnimationList())}");
                var name = anim.Length > 0 && player.HasAnimation(anim) ? anim : player.GetAnimationList().FirstOrDefault();
                if (name is not null) { player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear; player.Play(name); }
            }
        }
        camera.Position = new Vector3(0, 1.4f, 4.2f);
        camera.LookAt(new Vector3(0, 0.9f, 0));
        camera.Fov = 50;
    }

    private static AnimationPlayer? FindPlayer(Node n)
    {
        if (n is AnimationPlayer p) return p;
        foreach (var c in n.GetChildren()) if (FindPlayer(c) is { } f) return f;
        return null;
    }

    // VITRINE_HAB=skill_guerreiro_giro troca a habilidade das poses da vitrine (para conferir a pose de cada uma)
    private static readonly string Hab = OS.GetEnvironment("VITRINE_HAB");

    // filme quadro a quadro (verificação de animação): VITRINE_PASSO=0.033 fixa o tempo de cada quadro;
    // VITRINE_FILME=pasta grava os quadros 40..(40+VITRINE_QUADROS) como png
    private static readonly float Passo = float.TryParse(OS.GetEnvironment("VITRINE_PASSO"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ps) ? ps : 0f;
    private static readonly string Filme = OS.GetEnvironment("VITRINE_FILME");
    private static readonly int Quadros = int.TryParse(OS.GetEnvironment("VITRINE_QUADROS"), out var qd) ? qd : 24;
    private int _frameNo;

    public override void _Process(double delta)
    {
        if (Passo > 0f) delta = Passo;
        _uiTick?.Invoke();
        _frameNo++;
        if (Filme.Length > 0 && _frameNo > 40 && _frameNo <= 40 + Quadros)
            GetViewport().GetTexture().GetImage().SavePng($"{Filme}/q{_frameNo - 40:000}.png");
        if (Filme.Length > 0 && _frameNo > 41 + Quadros) GetTree().Quit();
        _time += (float)delta;
        foreach (var (v, phase) in _actors)
        {
            var frame = new VisualFrame { Alive = true, Action = "" };
            var cycle = (_time + phase) % 3.2f;
            switch (_action)
            {
                case "windup": frame.Action = "windup:vitrine"; frame.ActionT = 0.75f; frame.WindupGlow = 0.8f; break;
                case "recover": frame.Action = "recover:vitrine"; frame.ActionT = 0.3f; break;
                case "golpe": frame.Action = "active:vitrine"; frame.ActionT = 0.5f; break;
                case "ciclo":
                    if (cycle < 1.0f) { frame.Action = "windup:vitrine"; frame.ActionT = cycle; frame.WindupGlow = cycle; }
                    else if (cycle < 1.25f) { frame.Action = "active:vitrine"; frame.ActionT = (cycle - 1f) / 0.25f; }
                    else if (cycle < 1.9f) { frame.Action = "recover:vitrine"; frame.ActionT = (cycle - 1.25f) / 0.65f; }
                    break;
                case "andar": frame.Speed = 1.6f; break;
                case "dormir": frame.Action = "sleep"; break;
                case "correr": frame.Speed = 5f; break;
                case "esquiva":
                    frame.Action = (_time % 0.8f) < 0.42f ? "dodge" : "";
                    frame.ActionT = (_time % 0.8f) / 0.42f;
                    frame.ActionSeq = (int)(_time / 0.8f);
                    frame.Speed = 6f;
                    break;
                case "combo":
                {
                    // ritmo do golpe básico do Guerreiro: preparação 0,18 s, recuperação 0,30 s, pausa curta
                    var c = (_time + 0.5f) % 0.62f;
                    frame.ActionSeq = (int)((_time + 0.5f) / 0.62f);
                    frame.Action = c < 0.18f ? "windup:vitrine" : c < 0.48f ? "recover:vitrine" : "";
                    frame.ActionT = c < 0.18f ? c / 0.18f : (c - 0.18f) / 0.3f;
                    break;
                }
                case "ataque": frame.Action = cycle < 1.5f ? "windup:x" : "active:x"; break;
                default: if (_action.StartsWith("pose_", StringComparison.Ordinal)) frame.Action = _action; break;
            }
            // 13/11: VITRINE_POSES="pose_aceno,pose_bracos,..." — uma pose por boneco (conferir as poses da tecla Z)
            if (OS.GetEnvironment("VITRINE_POSES") is { Length: > 0 } poses)
            {
                var lista = poses.Split(',');
                frame.Action = lista[_actors.FindIndex(a => a.Item1 == v) % lista.Length];
            }
            if (Hab.Length > 0) frame.Action = frame.Action.Replace(":vitrine", ":" + Hab);
            v.Update(frame, (float)delta);
        }
    }

    // 14/11 tarde: o menu do TAB com dados de exemplo (VITRINE_PAG = loja | objetivos | trilha | opcoes | teclas)
    private void MenuTab(Camera3D camera, string aba)
    {
        camera.Position = new Vector3(0, 3.2f, 6f); camera.LookAt(Vector3.Zero);
        var layer = new CanvasLayer();
        AddChild(layer);
        var model = new CinzasDaMare.Shared.Client.ClientModel();
        var trilha = new TrilhaPanel();
        layer.AddChild(trilha);
        var tipos = new[] { "mapa", "asas", "dungeon", "montaria", "poder", "capa", "classe", "presente" };
        var arts = new[] { "", "item:asas_fogo", "", "item:mount_corcel", "hab:skill_arqueiro_chuva", "item:capa_dragao", "", "item:caixa_cinzas_1" };
        trilha.Atualizar(new CinzasDaMare.Shared.Net.TrilhaMessage(12, Enumerable.Range(0, 24).Select(i => new CinzasDaMare.Shared.Net.TrilhaItem(2 + i / 2 * 3, tipos[i % 8], "Prêmio " + i, "detalhe", 2 + i / 2 * 3 <= 12) { Arte = arts[i % 8] }).ToList()), 1, model);
        var opc = new VBoxContainer();
        opc.AddChild(HudView.MakeLabel("Configurações de som", 16, new Color(0.95f, 0.78f, 0.30f)));
        opc.AddChild(new HSlider { CustomMinimumSize = new Vector2(300, 24), Value = 60 });
        var tec = new VBoxContainer();
        tec.AddChild(HudView.MakeLabel("Teclas", 18, new Color(0.95f, 0.78f, 0.30f)));
        var menu = new MenuTabPanel { Corpo = id => id switch { "trilha" => trilha.Corpo, "opcoes" => opc, "teclas" => tec, _ => null } };
        layer.AddChild(menu);
        var loja = new List<CinzasDaMare.Shared.Net.LojaCristalView>();
        foreach (var (it, n, p, g) in new[] { ("caixa_cinzas", "Caixa das Cinzas", 60, "caixas"), ("asas_fogo", "Asas de Fogo", 300, "asas"), ("asas_vazio", "Asas do Vazio", 550, "asas"),
                     ("asas_criacao", "Asas da Criação", 1200, "asas"), ("capa_dragao", "Capa do Dragão", 260, "capas"), ("capa_luz", "Capa da Luz", 180, "capas"), ("bracelete_lava", "Skin de Lava", 400, "skins") })
            loja.Add(new CinzasDaMare.Shared.Net.LojaCristalView(it, n, p, g, 20, it == "capa_luz"));
        var objs = new List<CinzasDaMare.Shared.Net.ObjetivoView>
        {
            new("o1", "dia", "Entre no jogo", 1, 1, 10, false), new("o2", "dia", "Mate 300 monstros", 120, 300, 25, false),
            new("o3", "semana", "Derrote 5 chefes", 5, 5, 60, true), new("o4", "conta", "Chegue ao nível 30", 12, 30, 200, false),
        };
        menu.Mostrar(model, aba);
        menu.Atualizar(new CinzasDaMare.Shared.Net.ObjetivosMessage(1340, objs, loja));
    }
}
