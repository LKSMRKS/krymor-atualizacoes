using System;
using System.Linq;
using System.Collections.Generic;
using CinzasDaMare.Client.Visuals;
using CinzasDaMare.Shared.World;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Desenha o mapa a partir do MapLayout que o servidor envia (fonte única: a colisão do servidor usa os mesmos
/// números). Formas simples: chão por região, muralhas, penhascos, rochas, árvores, colunas, ruínas, portão com
/// alavanca, baús e enfeites (fogueira, tochas, tendas). Colisores estáticos só para a câmera não atravessar.
/// </summary>
public sealed class MapView
{
    private sealed class GateView
    {
        public MapGate Gate = null!;
        public Node3D Bars = null!;
        public Node3D Lever = null!;
        public float Open;
        /// <summary>E9: ponte (kind "ponte"): tábuas aparecem quando a marca existe; restos quebrados somem.</summary>
        public Node3D? Built;
        public Node3D? Broken;
        /// <summary>17/11: película verde (Véu da Maré) no vão do portão aberto — monstros e golpes não atravessam.</summary>
        public Node3D? Veu;
        /// <summary>10/11: rocha empurrável (kind "pedra"): posição fechada e para onde rola ao abrir.</summary>
        public Vector3 Home, Aside;
    }

    private sealed class ChestView
    {
        public MapChest Chest = null!;
        public Node3D Lid = null!;
        public float LidSign = -1f;               // baú do lote 2: a dobradiça gira para o outro lado
        public OmniLight3D? Glow;
        public float Open;
        public Node3D? Root;
        public float Sumindo = -1f;   // 17/11: segundos desde que abriu (o baú some para quem abriu)
        public bool Visto;
    }

    private readonly List<GateView> _gates = new();
    private readonly List<ChestView> _chests = new();
    private readonly List<(OmniLight3D Light, float Phase, float Base)> _flames = new();
    private readonly List<(string Trava, Label3D Nome, StandardMaterial3D Anel, string Texto)> _portasTrancadas = new();
    private readonly List<(string Trava, Node3D Portao)> _veusPorta = new();
    private readonly List<IEntityVisual> _presos = new();   // 13/11: prisioneiros (boneco amarrado)
    private readonly List<IEntityVisual> _mortos = new();   // 13/11: corpos de enfeite (boneco deitado)
    private readonly List<(Node3D Node, float Phase)> _flameMeshes = new();
    private readonly Random _rng = new(11);
    private readonly List<IEntityVisual> _npcs = new();
    private readonly List<NpcLife> _life = new();

    /// <summary>Posição do jogador (posta pela Main a cada quadro): NPCs viram para quem chega perto e acenam.</summary>
    public Vector3? PlayerPos { get; set; }

    private sealed class NpcLife
    {
        public MapNpc Npc = null!;
        public IEntityVisual Visual = null!;
        public float HomeYaw, Yaw, Timer, ActT, ActLen;
        public string Work = "", Action = "";
        public int Seq;
        public bool Greeted, Batendo;
    }

    /// <summary>
    /// NPC vivo: trabalha no ofício (ferreiro martela, agricultora mexe na terra, arcanista/curandeira conjura um brilho),
    /// olha em volta, vira para o jogador que chega perto, acena na chegada e gesticula enquanto ele está ali.
    /// </summary>
    private void UpdateNpcLife(NpcLife l, float delta)
    {
        if (l.Visual is PropVisual) return;
        var root = l.Visual.Root;
        var near = PlayerPos is { } p && root.Visible && new Vector2(p.X - root.Position.X, p.Z - root.Position.Z).Length() < 5.5f;
        var targetYaw = l.HomeYaw;
        if (near)
        {
            var d = PlayerPos!.Value - root.Position;
            targetYaw = Mathf.Atan2(d.X, d.Z);
            if (!l.Greeted) { l.Greeted = true; Start(l, Greets[(int)R(0, Greets.Length - 0.01f)], 1.5f); OnGreet?.Invoke(l.Npc, root.Position); }
        }
        else if (l.Greeted && PlayerPos is { } far && new Vector2(far.X - root.Position.X, far.Z - root.Position.Z).Length() > 9f) l.Greeted = false;

        l.Timer -= delta;
        if (l.ActLen > 0f)
        {
            l.ActT += delta;
            if (l.ActT >= l.ActLen) { l.ActLen = 0f; l.Action = ""; }
        }
        else if (l.Timer <= 0f)
        {
            if (near) { Start(l, Talks[(int)R(0, Talks.Length - 0.01f)], R(2.5f, 4f)); l.Timer = R(1f, 3f); }
            else switch (l.Work)
            {
                // 11/11 noite ("o ferreiro martelando atravessa a mão no corpo"): martelada curta na frente do corpo
                case "martelo": Start(l, "npc_martelar", R(3f, 5f)); l.Timer = R(0.3f, 0.8f); break;
                case "horta": Start(l, "gather", R(3f, 5f)); l.Timer = R(2f, 4f); break;
                case "magia": Start(l, "npc_magia", 1.6f); l.Timer = R(5f, 9f); break;
                // guarda: braços cruzados de plantão e, de vez em quando, olha ao longe com a mão na testa
                case "guarda": if (R(0, 1) < 0.35f) Start(l, "npc_olhar", 2.5f); else Start(l, "npc_bracos", R(5f, 9f)); l.Timer = R(0.5f, 2f); break;
                // pescador/capitã/batedora: olha o horizonte
                case "vigia": Start(l, "npc_olhar", R(2.5f, 4f)); l.Timer = R(3f, 7f); break;
                // bobo: malabares (braços alternando) e acenos
                case "bobo": Start(l, R(0, 1) < 0.7f ? "npc_malabar" : "npc_aceno", R(2f, 3.5f)); l.Timer = R(0.5f, 1.5f); break;
                case "danca": Start(l, "npc_danca", R(6f, 10f)); l.Timer = R(0.2f, 0.8f); break;   // 13/11: o dançarino não para
                default:
                    // olha para os lados de vez em quando
                    l.HomeYaw += R(-0.6f, 0.6f);
                    l.HomeYaw = Mathf.Lerp(l.HomeYaw, Mathf.DegToRad((float)l.Npc.Facing), 0.5f);
                    l.Timer = R(3f, 7f);
                    break;
            }
        }
        l.Yaw = Mathf.LerpAngle(l.Yaw, targetYaw, Mathf.Min(1f, delta * 4f));
        root.Rotation = new Vector3(0, l.Yaw, 0);

        // 12/11 ("faz uma faísca saindo quando ele bate"): a cada martelada (fundo do movimento do braço) solta faíscas na bigorna
        if (l.Action == "npc_martelar" && l.Visual is MannequinVisual mart && _bigornas.TryGetValue(l.Npc.Id, out var bigorna))
        {
            var golpe = Mathf.Sin(mart.AnimTime * 5.5f) < -0.97f;
            if (golpe && !l.Batendo) Sparks(root, bigorna);
            l.Batendo = golpe;
        }
        var frame = new VisualFrame { Alive = true, Action = "" };
        if (l.ActLen > 0f)
        {
            var t = l.ActT / l.ActLen;
            if (l.Action == "gather") { frame.Action = "gather"; frame.ActionT = Mathf.Min(1f, l.ActT); }
            else if (l.Action == "golpe")
            {
                // martelada: sobe 70% do tempo, desce rápido
                frame.Action = t < 0.7f ? "windup:npc_martelo" : "active:npc_martelo";
                frame.ActionT = t < 0.7f ? t / 0.7f : (t - 0.7f) / 0.3f;
                if (t >= 0.7f && t - delta / l.ActLen < 0.7f) Sparks(root);
            }
            else { frame.Action = (t < 0.25f ? "windup:" : t < 0.75f ? "active:" : "recover:") + l.Action; frame.ActionT = t < 0.25f ? t / 0.25f : t < 0.75f ? 0.5f : (t - 0.75f) / 0.25f; }
            frame.ActionSeq = l.Seq;
        }
        l.Visual.Update(frame, delta);
    }

    /// <summary>NPC cumprimenta quando o jogador chega perto (a Main toca a voz dele).</summary>
    public Action<MapNpc, Vector3>? OnGreet { get; set; }

    /// <summary>Reação do NPC numa conversa (aceno, gesticular): devolve onde ele está (para a voz sair dali).</summary>
    // 11/11 noite ("todos fazem os mesmos gestos, vamos variar"): cumprimentos e gestos de conversa sorteados
    private static readonly string[] Greets = { "npc_aceno", "npc_saudacao", "npc_aceno_cabeca", "npc_mao_alta", "npc_maos_quadril" };
    private static readonly string[] Talks = { "npc_conversa", "npc_explica", "npc_coca_cabeca", "npc_maos_quadril", "npc_conta_dedos", "npc_ombros" };

    public Vector3? NpcReact(string npcId, string action, float len)
    {
        var l = _life.Find(x => x.Npc.Id == npcId);
        if (l is null) return null;
        if (l.Visual is not PropVisual) { Start(l, action, len); l.Timer = len + R(0.5f, 1.5f); }
        return l.Visual.Root.Position;
    }

    private void Start(NpcLife l, string action, float len)
    {
        l.Action = action; l.ActT = 0f; l.ActLen = len; l.Seq++;
    }

    /// <summary>Faíscas na bigorna a cada martelada.</summary>
    private readonly Dictionary<string, Vector3> _bigornas = new();

    private void Sparks(Node3D npc, Vector3? onde = null)
    {
        if (!npc.Visible) return;
        var at = onde ?? npc.Position + npc.Basis * new Vector3(0.35f, 0.9f, 0.55f);
        var mat = VisualKit.Glow(new Color(1f, 0.6f, 0.2f), 4f);
        for (var i = 0; i < 6; i++)
        {
            var spark = VisualKit.Part(Root, VisualKit.Sphere(0.025f), mat, at);
            var dir = new Vector3(R(-1f, 1f), R(0.6f, 1.6f), R(-1f, 1f)) * 1.6f;
            var tw = spark.CreateTween();
            tw.TweenProperty(spark, "position", at + dir * 0.35f + Vector3.Down * 0.25f, 0.35f);
            tw.Parallel().TweenProperty(spark, "scale", Vector3.One * 0.1f, 0.35f);
            tw.TweenCallback(Callable.From(spark.QueueFree));
        }
    }
    private readonly List<(MapNpc Npc, Node3D Visual, Node3D Marker)> _npcNodes = new();
    private readonly List<(MapObject Obj, Node3D Node)> _objects = new();
    private readonly List<(Node3D Node, float Phase)> _portalGlows = new();
    /// <summary>Árvores que o vento balança (pedido: "a árvore não mexe, o jogo não tem vida").</summary>
    private readonly List<(Node3D Node, Vector3 Base, float Phase, float Amp)> _swaying = new();
    private readonly Dictionary<Node3D, Vector3> _posVento = new();
    private readonly List<(MapObject Obj, Node3D Tampa, List<StandardMaterial3D> Runas)> _placas = new();

    public Node3D Root { get; } = new() { Name = "Mapa" };
    private readonly VidaAmbiente _vida;
    private Vector3 _viewerPos;
    public MapLayout Map { get; }

    public MapView(MapLayout map) : this(map, false) { }

    /// <summary>16/11 (otimização item 3 — "mapa trava 6-7 s carregando"): com <paramref name="aosPoucos"/> o mapa é montado
    /// em etapas (<see cref="Avancar"/> a cada quadro, com limite de tempo) e a tela de carregamento continua viva, com barra
    /// de progresso. O resultado é exatamente o mesmo da montagem de uma vez (mesma ordem, mesmo sorteio).</summary>
    public MapView(MapLayout map, bool aosPoucos)
    {
        Map = map;
        // vida do pacote de arte (Cal Lote 2): fogueiras de verdade, fumaça de marco, farol, gaivotas e siris na praia
        _vida = new VidaAmbiente { Name = "VidaAmbiente", Metade = (float)map.HalfSize, Jogador = () => _viewerPos };
        _etapas = Etapas(map).GetEnumerator();
        if (!aosPoucos) while (_etapas.MoveNext()) { }
    }

    private readonly IEnumerator<int> _etapas;
    /// <summary>0..1 enquanto monta em etapas.</summary>
    public float Progresso { get; private set; }
    public bool Pronto { get; private set; }

    /// <summary>Monta mais um pedaço do mapa, até gastar <paramref name="ms"/> milissegundos. true = terminou.</summary>
    public bool Avancar(double ms)
    {
        if (Pronto) return true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < ms)
            if (!_etapas.MoveNext()) { Pronto = true; Progresso = 1f; return true; }
        return false;
    }

    private IEnumerable<int> Etapas(MapLayout map)
    {
        var total = 12 + map.Obstacles.Count + map.Gates.Count + map.Chests.Count + map.Props.Count + map.Portals.Count + map.Waypoints.Count + map.Npcs.Count + map.Objects.Count;
        var feito = 0;
        int Passo(int n = 1) { feito += n; Progresso = Math.Min(0.99f, (float)feito / total); return feito; }
        // 12/11 ("relevo: testar só no modo lab"): ondulação ligada só no Laboratório de Testes por enquanto
        Relevo.Configurar(map.Id == "laboratorio" ? "vale" : "cidade", (float)map.HalfSize, false);
        Root.AddChild(_vida);
        Visuals.EscadaCasa.Limpar();   // 11/11: alturas de assoalho/escada do mapa novo
        if (map.Id == "porto_das_cinzas") Ambiente.CaisDoPorto.RegistrarAlturas();   // 12/11: cais elevado do lado leste
        if (TerrainMaterialAvailable())   // 13/11: morros e platôs (só onde o chão é o TerrainShader, que sobe junto)
            foreach (var e in map.Elevations.Take(32)) Visuals.EscadaCasa.RegistrarElevacao((float)e.X, (float)e.Y, (float)e.W, (float)e.H, (float)e.Height, (float)e.Ramp);
        AddEnvironment();
        yield return Passo();
        AddGround();
        yield return Passo(2);
        foreach (var o in map.Obstacles) { AddObstacleGirado(o); yield return Passo(); }
        foreach (var g in map.Gates) { AddGate(g); yield return Passo(); }
        foreach (var c in map.Chests) { AddChest(c); yield return Passo(); }
        foreach (var p in map.Props) { AddProp(p); yield return Passo(); }
        foreach (var portal in map.Portals) { AddPortal(portal); yield return Passo(); }
        foreach (var wp in map.Waypoints) { AddWaypoint(wp); yield return Passo(); }
        foreach (var npc in map.Npcs) { AddNpc(npc); yield return Passo(); }
        foreach (var obj in map.Objects) { AddObject(obj); yield return Passo(); }
        AddSafeCircle();
        if (map.Theme == "cidade") AddVillageDressing();
        yield return Passo();
        // 11/11: praça do Porto do pacote de arte (fonte com a estátua do Guardião, bancos, feira, árvores, floreiras)
        if (map.Id == "porto_das_cinzas" && !map.Props.Any(p => p.Kind == "fonte_praca")) Ambiente.PracaDoPorto.Montar(Root);   // 13/11: virou peças editáveis
        if (map.Id == "porto_das_cinzas") Ambiente.CaisDoPorto.Montar(Root, (float)map.HalfSize);   // 12/11: cais, mar e arredores
        // 11/11 noite (complemento do pacote): a cena do lago da Costa (rochas, capim, arbustos, flores, coqueiros)
        if (map.Id == "costa_do_cal") Ambiente.CenaPrintCosta.Montar(Root, new Vector2(-31f, 49f));
        yield return Passo(2);
        BalancarNoVento(Root);
        TexturizeAll(Root);
        yield return Passo();
        // 16/11 (otimização item 2): junta as pecinhas paradas do cenário em poucos desenhos; o que o mapa guarda para mexer
        // depois (listas deste arquivo) fica de fora. Mede o tempo e deixa o resumo no log
        if (Ambiente.JuntarCenario.Ligado)
        {
            var swJ = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var prot = Ambiente.JuntarCenario.NosGuardados(this);
                Ambiente.JuntarCenario.Juntar(Root, prot);
                if (OS.GetEnvironment("VITRINE_MEDIR") == "1") GD.Print("JUNTAR " + Ambiente.JuntarCenario.UltimoResumo + $" ({swJ.ElapsedMilliseconds} ms) campos: " + string.Join(", ", Ambiente.JuntarCenario.PorCampo.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "=" + kv.Value)));
                CinzasDaMare.Shared.Diagnostics.GameLog.Info("cliente.mapa", $"{Ambiente.JuntarCenario.UltimoResumo} ({swJ.ElapsedMilliseconds} ms) em {map.Id}");
            }
            catch (Exception e) { CinzasDaMare.Shared.Diagnostics.GameLog.Info("cliente.mapa", $"juntar falhou em {map.Id}: {e.Message}"); }
        }
        yield return Passo(3);
        CullByDistance(Root);
        ApplyQuality(); // as chamas já existem: decide quais projetam sombra
        FadeLights(Root);
    }

    /// <summary>
    /// 13/11 ("dei tp para vários lugares e o jogo deu uma pesada"; log: 20–58 milhões de triângulos na Costa): luz pontual
    /// com sombra desenha a cena 6 vezes. Agora a sombra só existe perto da câmera (até ~22 m) e a luz some com
    /// esmaecimento depois de ~50 m — longe ela não aparece mesmo, e o custo vai embora.
    /// </summary>
    public static void FadeLights(Node n)
    {
        foreach (var ch in n.GetChildren())
        {
            if (ch is Light3D l and not DirectionalLight3D && !l.DistanceFadeEnabled)
            {
                l.DistanceFadeEnabled = true;
                l.DistanceFadeBegin = 48f;
                l.DistanceFadeLength = 10f;
                l.DistanceFadeShadow = 22f;
            }
            FadeLights(ch);
        }
    }

    private static readonly Dictionary<(Color, string), StandardMaterial3D> TexCache = new();

    /// <summary>
    /// Regra do usuário: "todo objeto com textura". Passa por tudo o que o mapa montou e, onde ficou cor lisa
    /// (material sem textura, sem brilho próprio, opaco), troca por um material com a textura gerada que combina com
    /// a cor: madeira (marrons), pedra/bloco (cinzas), reboco (claros), ardósia/telha (escuros avermelhados), terra.
    /// A cor original vira o tom (tint). Materiais iguais são reaproveitados (leve).
    /// </summary>
    /// <summary>
    /// Distância de desenho por tamanho (técnica padrão de jogo grande — "visibility range"): objeto pequeno some a ~45 m,
    /// médio a ~90 m, e o grande (paredes, penhascos, terreno) fica sempre. Some com esmaecimento, sem "estalo".
    /// </summary>
    private static void CullByDistance(Node n)
    {
        foreach (var ch in n.GetChildren())
        {
            if (ch is GeometryInstance3D g && g is not MultiMeshInstance3D && g.VisibilityRangeEnd <= 0f)
            {
                var size = g.GetAabb().Size * g.Transform.Basis.Scale;   // (fora da árvore ainda: escala local basta)
                var big = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
                if (big < 12f)
                {
                    g.VisibilityRangeEnd = big < 1.5f ? 45f : 95f;
                    g.VisibilityRangeEndMargin = 8f;
                    g.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
                }
            }
            CullByDistance(ch);
        }
    }

    private static void TexturizeAll(Node n)
    {
        if (n is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D sm && sm.AlbedoTexture is null && !sm.EmissionEnabled
            && sm.Transparency == BaseMaterial3D.TransparencyEnum.Disabled && sm.ShadingMode == BaseMaterial3D.ShadingModeEnum.PerPixel && sm.Metallic < 0.5f)
        {
            var c = sm.AlbedoColor;
            c.ToHsv(out var h, out var sat, out var v);
            var tex = sat < 0.18f ? (v > 0.6f ? "gen_reboco.png" : "gen_bloco.png")
                : h is > 0.02f and < 0.14f ? (sat > 0.45f && v < 0.45f ? "gen_madeira.png" : v > 0.55f ? "gen_reboco.png" : "gen_madeira.png")
                : h is > 0.14f and < 0.45f ? "gen_terra.png"
                : h > 0.9f || h < 0.02f ? "gen_ardosia.png" : "gen_bloco.png";
            var key = (c, tex);
            if (!TexCache.TryGetValue(key, out var m))
            {
                m = (StandardMaterial3D)sm.Duplicate();
                if (VisualAssets.Load(tex) is { } t)
                {
                    m.AlbedoTexture = t;
                    m.AlbedoColor = new Color(Mathf.Min(1f, c.R * 1.35f), Mathf.Min(1f, c.G * 1.35f), Mathf.Min(1f, c.B * 1.35f), c.A);
                    m.Uv1Triplanar = true;
                    m.Uv1WorldTriplanar = true;
                    m.Uv1Scale = Vector3.One * 0.5f;
                    VisualAssets.AddRelief(m, tex);
                }
                TexCache[key] = m;
            }
            mi.MaterialOverride = m;
        }
        foreach (var ch in n.GetChildren()) TexturizeAll(ch);
    }

    internal float R(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    // 13/11: tudo que é posto no mapa por P() acompanha os morros (árvores, peças, baús, portais, pedras de viagem)
    // 13/11 ("itens na beirada da elevação ficam voando, como a árvore seca"): peça parada no morro usa o ponto MAIS BAIXO
    // do chão em volta da base (raio 0,8 m) — na rampa ela afunda um dedo do lado de cima em vez de flutuar do lado de baixo
    private static Vector3 P(double x, double y, float h = 0f) => new((float)x, h + Visuals.EscadaCasa.ElevBase(x, y, 0.8), (float)y);

    // ------------------------------------------------------------ ambiente

    /// <summary>Clima de cada tema: céu, luz do sol (cor/altura), névoa e nuvens. Valores de aparência, ajustados por captura.</summary>
    private readonly record struct Look(
        Color SkyTop, Color Horizon, Color Ambient, float AmbientEnergy, Color Fog, float FogDensity,
        Color Sun, float SunEnergy, float SunPitch, float SunYaw, Color Fill, float FillEnergy,
        Color VolFog, float VolDensity, float HeightFog, float Clouds, float Stars, float Exposure);

    private static Look LookFor(string theme) => theme switch
    {
        // entardecer da prancha 01: violeta no alto, âmbar no horizonte, sol baixo e quente, contraluz frio
        "cidade" => new(new Color(0.16f, 0.14f, 0.26f), new Color(0.82f, 0.56f, 0.44f), new Color(0.46f, 0.47f, 0.62f), 0.45f,
            new Color(0.36f, 0.33f, 0.42f), 0.006f, new Color(1f, 0.80f, 0.58f), 1.45f, -26f, -40f, new Color(0.50f, 0.60f, 0.95f), 0.45f,
            new Color(0.62f, 0.60f, 0.72f), 0.010f, 0f, 0.55f, 0f, 1.12f),
        // Costa (direção 09/11: "menos banho marrom"): sol mais alto e mais branco, sombra azulada do mar, névoa fria —
        // a areia fica clara e neutra e os bichos (vermelho, cinza, laranja) se destacam pela cor
        "praia" => new(new Color(0.28f, 0.38f, 0.58f), new Color(0.80f, 0.72f, 0.64f), new Color(0.56f, 0.62f, 0.78f), 0.5f,
            new Color(0.58f, 0.64f, 0.76f), 0.0045f, new Color(1f, 0.92f, 0.80f), 1.75f, -42f, -35f, new Color(0.48f, 0.62f, 0.95f), 0.42f,
            new Color(0.72f, 0.76f, 0.84f), 0.006f, 0f, 0.45f, 0f, 1.0f),
        "vale" => new(new Color(0.20f, 0.18f, 0.28f), new Color(0.72f, 0.52f, 0.42f), new Color(0.52f, 0.52f, 0.62f), 0.45f,
            new Color(0.40f, 0.36f, 0.38f), 0.007f, new Color(1f, 0.74f, 0.52f), 1.6f, -28f, -30f, new Color(0.45f, 0.55f, 0.82f), 0.3f,
            new Color(0.70f, 0.62f, 0.62f), 0.012f, 0.35f, 0.6f, 0f, 1.0f),
        "mata" => new(new Color(0.15f, 0.17f, 0.22f), new Color(0.55f, 0.50f, 0.46f), new Color(0.46f, 0.52f, 0.56f), 0.42f,
            new Color(0.28f, 0.31f, 0.31f), 0.010f, new Color(1f, 0.80f, 0.60f), 1.35f, -34f, -50f, new Color(0.45f, 0.6f, 0.7f), 0.3f,
            new Color(0.55f, 0.62f, 0.60f), 0.022f, 0.8f, 0.75f, 0f, 1.05f),
        "canion" => new(new Color(0.24f, 0.20f, 0.30f), new Color(0.92f, 0.58f, 0.40f), new Color(0.62f, 0.52f, 0.52f), 0.45f,
            new Color(0.55f, 0.40f, 0.36f), 0.006f, new Color(1f, 0.68f, 0.42f), 1.9f, -22f, -60f, new Color(0.5f, 0.55f, 0.8f), 0.3f,
            new Color(0.85f, 0.62f, 0.50f), 0.010f, 0f, 0.35f, 0f, 1.0f),
        "brejo" => new(new Color(0.14f, 0.18f, 0.18f), new Color(0.48f, 0.52f, 0.44f), new Color(0.46f, 0.54f, 0.50f), 0.42f,
            new Color(0.26f, 0.32f, 0.28f), 0.012f, new Color(0.95f, 0.85f, 0.62f), 1.1f, -30f, -20f, new Color(0.4f, 0.6f, 0.55f), 0.3f,
            new Color(0.52f, 0.62f, 0.54f), 0.035f, 1.2f, 0.85f, 0f, 1.1f),
        "fantasma" => new(new Color(0.06f, 0.07f, 0.13f), new Color(0.24f, 0.30f, 0.42f), new Color(0.42f, 0.55f, 0.66f), 0.5f,
            new Color(0.16f, 0.20f, 0.28f), 0.012f, new Color(0.62f, 0.75f, 1f), 0.8f, -40f, 20f, new Color(0.40f, 0.65f, 0.70f), 0.45f,
            new Color(0.40f, 0.60f, 0.70f), 0.025f, 0.6f, 0.3f, 1f, 1.15f),
        "neve" => new(new Color(0.36f, 0.44f, 0.60f), new Color(0.85f, 0.78f, 0.78f), new Color(0.70f, 0.75f, 0.86f), 0.55f,
            new Color(0.66f, 0.70f, 0.78f), 0.012f, new Color(1f, 0.88f, 0.75f), 1.5f, -26f, -45f, new Color(0.55f, 0.7f, 1f), 0.4f,
            new Color(0.85f, 0.88f, 0.95f), 0.025f, 0.5f, 0.7f, 0f, 0.95f),
        "lava" => new(new Color(0.10f, 0.05f, 0.06f), new Color(0.55f, 0.20f, 0.10f), new Color(0.62f, 0.40f, 0.32f), 0.42f,
            new Color(0.32f, 0.12f, 0.08f), 0.010f, new Color(1f, 0.55f, 0.30f), 1.2f, -30f, -30f, new Color(0.9f, 0.3f, 0.1f), 0.35f,
            new Color(0.75f, 0.35f, 0.25f), 0.03f, 0.6f, 0.8f, 0f, 1.1f),
        "acropole" => new(new Color(0.20f, 0.16f, 0.26f), new Color(0.90f, 0.62f, 0.45f), new Color(0.62f, 0.55f, 0.55f), 0.48f,
            new Color(0.45f, 0.36f, 0.36f), 0.006f, new Color(1f, 0.74f, 0.48f), 1.8f, -20f, -35f, new Color(0.5f, 0.55f, 0.85f), 0.3f,
            new Color(0.80f, 0.65f, 0.60f), 0.010f, 0f, 0.5f, 0f, 1.0f),
        // 11/11: Santuário do Céu (teste do adm) — céu azul claro, sol alto e branco-dourado, nuvens fofas, névoa clara
        "ceu" => new(new Color(0.30f, 0.55f, 0.95f), new Color(0.95f, 0.92f, 0.85f), new Color(0.80f, 0.84f, 0.95f), 0.75f,
            new Color(0.88f, 0.90f, 0.98f), 0.004f, new Color(1f, 0.95f, 0.82f), 2.1f, -55f, -30f, new Color(0.7f, 0.8f, 1f), 0.5f,
            new Color(0.95f, 0.95f, 1f), 0.006f, 0f, 0.9f, 0f, 1.05f),
        // 12/11: Lua Vermelha (reino do lobisomem) — céu preto e estrelado, horizonte cor de sangue, luz fria e dura
        "lua" => new(new Color(0.0f, 0.0f, 0.02f), new Color(0.30f, 0.06f, 0.05f), new Color(0.55f, 0.55f, 0.66f), 0.55f,
            new Color(0.10f, 0.06f, 0.08f), 0.004f, new Color(0.92f, 0.92f, 1f), 1.7f, -18f, 60f, new Color(0.85f, 0.25f, 0.2f), 0.35f,
            new Color(0.35f, 0.25f, 0.30f), 0.006f, 0f, 0f, 1f, 1.05f),
        // 12/11: Inferno (reino do ceifador) — como a lava, mas com menos fumaça para enxergar a luta
        // 12/11: Fundo do Triângulo (debaixo d'água) — tudo azul-esverdeado, luz que desce em feixes, névoa densa
        "submerso" => new(new Color(0.02f, 0.14f, 0.24f), new Color(0.06f, 0.32f, 0.42f), new Color(0.30f, 0.55f, 0.66f), 0.7f,
            new Color(0.05f, 0.24f, 0.32f), 0.03f, new Color(0.62f, 0.90f, 1f), 1.1f, -70f, -20f, new Color(0.2f, 0.6f, 0.75f), 0.45f,
            new Color(0.10f, 0.40f, 0.50f), 0.045f, 0.6f, 0f, 0f, 1.15f),
        // 14/11: Coração dos Reinos (hub dos Reinos Ocultos, como a arte do mapa) — crepúsculo violeta, luz lilás, névoa leve
        "reinos" => new(new Color(0.07f, 0.03f, 0.14f), new Color(0.48f, 0.28f, 0.62f), new Color(0.55f, 0.45f, 0.70f), 0.55f,
            new Color(0.28f, 0.16f, 0.38f), 0.010f, new Color(0.85f, 0.75f, 1f), 1.2f, -35f, 25f, new Color(0.65f, 0.45f, 0.95f), 0.5f,
            new Color(0.55f, 0.40f, 0.75f), 0.02f, 0.4f, 0.3f, 1f, 1.1f),
        "inferno" => new(new Color(0.12f, 0.03f, 0.03f), new Color(0.75f, 0.28f, 0.10f), new Color(0.70f, 0.50f, 0.42f), 0.65f,
            new Color(0.30f, 0.10f, 0.06f), 0.006f, new Color(1f, 0.60f, 0.35f), 1.6f, -38f, -30f, new Color(1f, 0.35f, 0.1f), 0.45f,
            new Color(0.70f, 0.30f, 0.20f), 0.008f, 0.2f, 0.5f, 0f, 1.15f),
        // gruta/dungeon: sem céu; luz fria fraca vinda de cima e as chamas fazem o resto
        "gruta" => new(new Color(0.03f, 0.035f, 0.045f), new Color(0.03f, 0.035f, 0.045f), new Color(0.50f, 0.52f, 0.60f), 0.75f,
            new Color(0.05f, 0.06f, 0.08f), 0.018f, new Color(0.62f, 0.70f, 0.90f), 0.7f, -65f, -20f, new Color(0.85f, 0.6f, 0.4f), 0.35f,
            new Color(0.40f, 0.42f, 0.50f), 0.02f, 0f, 0f, 0f, 1.45f),
        _ => new(new Color(0.18f, 0.17f, 0.25f), new Color(0.75f, 0.55f, 0.45f), new Color(0.55f, 0.55f, 0.65f), 0.5f,
            new Color(0.36f, 0.33f, 0.36f), 0.007f, new Color(1f, 0.78f, 0.56f), 1.6f, -30f, -35f, new Color(0.45f, 0.55f, 0.85f), 0.3f,
            new Color(0.72f, 0.64f, 0.64f), 0.012f, 0f, 0.55f, 0f, 1.0f),
    };

    // céu: degradê, halo e disco do sol, nuvens fixas iluminadas pelo sol (sem TIME: o reflexo do céu não é recalculado a cada quadro)
    private const string SkyShader = @"
shader_type sky;
uniform vec3 top_color; uniform vec3 horizon_color; uniform vec3 ground_color;
uniform vec3 sun_color; uniform float clouds = 0.5; uniform vec3 cloud_color; uniform float stars = 0.0;
// cenário no horizonte (estilo skybox de CS 1.6): 3 camadas de serras/montanhas; 0 = sem, 1 = morros, 2 = montanhas,
// 3 = mar com ilhas ao longe, 4 = picos nevados
uniform int ridge_style = 1; uniform vec3 ridge_color = vec3(0.2); uniform vec3 sea_color = vec3(0.2, 0.3, 0.4);
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y); }
float fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int i = 0; i < 5; i++) { v += a * vnoise(p); p = p * 2.03 + 17.0; a *= 0.5; } return v; }
void sky() {
  vec3 d = EYEDIR; float h = d.y;
  vec3 col = mix(horizon_color, top_color, pow(clamp(h, 0.0, 1.0), 0.5));
  float sd = LIGHT0_ENABLED ? max(dot(d, LIGHT0_DIRECTION), 0.0) : 0.0;
  col += sun_color * (pow(sd, 6.0) * 0.35 + pow(sd, 48.0) * 0.6);
  col += sun_color * 6.0 * smoothstep(0.9993, 0.9997, sd);
  if (h > 0.0) {
    vec2 uv = d.xz / (h + 0.15);
    float c = fbm(uv * 1.3);
    c = smoothstep(1.0 - clouds, 1.0 - clouds + 0.32, c);
    float edge = pow(sd, 3.0);
    vec3 cc = mix(cloud_color, sun_color * 1.3, edge * 0.8);
    col = mix(col, cc, c * smoothstep(0.0, 0.2, h) * 0.9);
    float s = step(0.9975, hash(floor(uv * 90.0)));
    col += vec3(s) * stars * (1.0 - c) * smoothstep(0.15, 0.5, h);
  } else {
    col = mix(horizon_color * 0.8, ground_color, clamp(-h * 4.0, 0.0, 1.0));
  }
  if (ridge_style > 0) {
    float az = atan(d.x, d.z);
    float sea = ridge_style == 3 ? 1.0 : 0.0;
    if (ridge_style == 3 && h < 0.004) {
      // mar até o horizonte, com brilho do sol
      float glint = pow(max(dot(normalize(vec3(d.x, -d.y, d.z)), LIGHT0_ENABLED ? LIGHT0_DIRECTION : vec3(0.0, 1.0, 0.0)), 0.0), 40.0);
      col = mix(sea_color, horizon_color, 0.35) + sun_color * glint * 0.6 * (0.6 + 0.4 * vnoise(d.xz / max(-h, 0.01) * 6.0));
    }
    for (int k = 0; k < 3; k++) {
      float fk = float(k);
      float freq = (ridge_style == 2 || ridge_style == 4 ? 2.6 : 1.7) * (1.0 + fk * 0.9);
      float amp = (ridge_style == 2 || ridge_style == 4 ? 0.18 : 0.10) * (1.0 - fk * 0.25);
      float base = ridge_style == 3 ? -0.01 : 0.005;
      // ruído no círculo (cos, sen): sem emenda atrás da câmera
      float n = fbm(vec2(cos(az), sin(az)) * freq * 1.6 + vec2(fk * 13.7, fk * 3.1));
      if (ridge_style == 2 || ridge_style == 4) n = 1.0 - abs(n * 2.0 - 1.0);   // cristas pontudas
      float top = base + amp * n * (ridge_style == 3 ? smoothstep(0.55, 0.75, fbm(vec2(cos(az), sin(az)) * 2.0 + vec2(fk, 7.0))) : 1.0);
      if (h < top && (ridge_style != 3 || h > -0.012)) {
        float far = 1.0 - fk * 0.38;            // camada de trás mais clara (névoa)
        vec3 rc = mix(ridge_color, horizon_color, far * 0.55);
        if (ridge_style == 4) rc = mix(rc, vec3(0.92, 0.94, 0.98), smoothstep(top - 0.03, top, h) * 0.8);
        // contraluz: um filete claro no topo da serra
        rc += sun_color * 0.12 * smoothstep(top - 0.004, top, h);
        col = rc;
      }
    }
  }
  COLOR = col;
}";

    private Godot.Environment? _env;
    private DirectionalLight3D? _sun;
    /// <summary>Sol do mapa (a vitrine vira o sol para iluminar os bonecos de frente).</summary>
    public DirectionalLight3D? Sun => _sun;
    private Look _look;
    private Look _day;                       // o clima do tema (tarde); a noite e o entardecer saem dele
    private ShaderMaterial? _skyMat;
    private DirectionalLight3D? _fill;
    private float _shownHour = -100f;
    private float _flameBoost = 1f;
    private ShaderMaterial? _terrainMat;
    private MeshInstance3D? _cloudDome;
    private ShaderMaterial? _cloudMat;
    private readonly List<(Node3D Bird, Node3D WingL, Node3D WingR, Vector3 Center, float Radius, float Speed, float Phase, float Height)> _birds = new();
    private Node3D? _birdRoot;

    // nuvens que andam numa cúpula em volta da câmera (fora do céu: mexer o céu a cada quadro recalcularia o reflexo inteiro)
    private const string CloudShader = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_front, fog_disabled, shadows_disabled;
uniform vec3 lit = vec3(1.0); uniform vec3 shade = vec3(0.6); uniform float cover = 0.5; uniform float opacity = 0.85;
uniform vec2 wind = vec2(0.006, 0.0025); uniform vec3 sun_dir = vec3(0.0, 1.0, 0.0);
varying vec3 dir;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y); }
float fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int i = 0; i < 4; i++) { v += a * vnoise(p); p = p * 2.07 + 13.0; a *= 0.5; } return v; }
void vertex() { dir = normalize(VERTEX); }
void fragment() {
  vec3 d = normalize(dir);
  if (d.y < 0.0) discard;
  vec2 uv = d.xz / (d.y + 0.12) * 1.6;
  vec2 drift = wind * TIME;
  float n = fbm(uv + drift) * 0.75 + fbm(uv * 2.6 - drift * 1.7 + 4.0) * 0.25;   // duas camadas em ritmos diferentes
  float c = smoothstep(1.0 - cover, 1.0 - cover + 0.28, n);
  if (c < 0.01) discard;
  // luz: borda virada para o sol mais clara, miolo mais escuro (volume sem custo)
  float toward = clamp(dot(normalize(vec3(d.x, 0.0, d.z)), normalize(vec3(sun_dir.x, 0.0, sun_dir.z))) * 0.5 + 0.5, 0.0, 1.0);
  float thick = smoothstep(1.0 - cover, 1.0, n);
  vec3 col = mix(lit, shade, thick * 0.8) * (0.85 + 0.25 * toward);
  ALBEDO = col;
  ALPHA = c * opacity * smoothstep(0.0, 0.18, d.y);
}";

    /// <summary>0 = dia pleno, 1 = noite fechada (para quem quiser reagir: lanternas, sons).</summary>
    public float Night { get; private set; }

    // 15/11 (noite: bonecos viravam silhueta preta): luz fraca, fria e sem sombra que segue o herói só à noite —
    // deixa ver o próprio boneco e os monstros perto sem clarear o mapa todo.
    private OmniLight3D? _luzHeroi;
    public void LuzDoHeroi(Vector3 pos)
    {
        if (!Root.IsInsideTree()) return;
        if (_luzHeroi is null || !GodotObject.IsInstanceValid(_luzHeroi))
            Root.AddChild(_luzHeroi = new OmniLight3D { LightColor = new Color(0.75f, 0.82f, 1f), OmniRange = 8f, OmniAttenuation = 1.4f, ShadowEnabled = false });
        var e = Mathf.Clamp((Night - 0.25f) / 0.5f, 0f, 1f) * 1.3f;
        _luzHeroi.LightEnergy = e;
        _luzHeroi.Visible = e > 0.01f;
        _luzHeroi.Position = pos + new Vector3(0, 2.6f, 0);
    }

    /// <summary>Dia e noite (10/11): mesmo mapa, outra hora. Ao ar livre só; dungeon, gruta e casa ficam como estão.</summary>
    public bool HasDayCycle => Map.Theme != "gruta" && !Map.Instanced && !Map.Id.StartsWith("reino_", StringComparison.Ordinal);   // 12/11: reinos ocultos têm clima fixo

    private static Look NightOf(Look d) => d with
    {
        SkyTop = new Color(0.012f, 0.018f, 0.045f), Horizon = d.Horizon.Lerp(new Color(0.09f, 0.10f, 0.17f), 0.85f),
        Ambient = new Color(0.34f, 0.38f, 0.58f), AmbientEnergy = 0.55f,   // 15/11: 0,4 → 0,55 (de noite as pessoas viravam silhueta preta)
        Fog = new Color(0.07f, 0.08f, 0.12f), FogDensity = d.FogDensity * 1.2f,
        Sun = new Color(0.62f, 0.70f, 1f), SunEnergy = 0.55f, Fill = new Color(0.42f, 0.44f, 0.62f), FillEnergy = 0.4f,
        VolFog = new Color(0.30f, 0.34f, 0.46f), Clouds = d.Clouds * 0.7f, Stars = 1f, Exposure = d.Exposure * 1.08f,
    };

    private static Look DuskOf(Look d) => d with
    {
        SkyTop = new Color(0.17f, 0.12f, 0.26f), Horizon = new Color(0.96f, 0.50f, 0.30f),
        Ambient = d.Ambient.Lerp(new Color(0.60f, 0.46f, 0.50f), 0.6f), Fog = d.Fog.Lerp(new Color(0.55f, 0.36f, 0.34f), 0.5f),
        Sun = new Color(1f, 0.56f, 0.32f), SunEnergy = d.SunEnergy * 0.75f, VolFog = d.VolFog.Lerp(new Color(0.9f, 0.55f, 0.4f), 0.5f),
    };

    private static Look Mix(Look a, Look b, float t) => new(
        a.SkyTop.Lerp(b.SkyTop, t), a.Horizon.Lerp(b.Horizon, t), a.Ambient.Lerp(b.Ambient, t), Mathf.Lerp(a.AmbientEnergy, b.AmbientEnergy, t),
        a.Fog.Lerp(b.Fog, t), Mathf.Lerp(a.FogDensity, b.FogDensity, t), a.Sun.Lerp(b.Sun, t), Mathf.Lerp(a.SunEnergy, b.SunEnergy, t),
        a.SunPitch, a.SunYaw, a.Fill.Lerp(b.Fill, t), Mathf.Lerp(a.FillEnergy, b.FillEnergy, t), a.VolFog.Lerp(b.VolFog, t),
        Mathf.Lerp(a.VolDensity, b.VolDensity, t), Mathf.Lerp(a.HeightFog, b.HeightFog, t), Mathf.Lerp(a.Clouds, b.Clouds, t),
        Mathf.Lerp(a.Stars, b.Stars, t), Mathf.Lerp(a.Exposure, b.Exposure, t));

    /// <summary>
    /// Põe o mapa na hora pedida (0–24): o sol nasce às 6, se põe às 18 e anda pelo céu; à noite a lua assume a luz (fria,
    /// com sombra), aparecem estrelas e as chamas e lanternas ganham força. Barato: só recalcula o céu quando a hora muda.
    /// </summary>
    public void SetHour(float hour)
    {
        if (!HasDayCycle || _env is null || _sun is null || _skyMat is null) return;
        if (Mathf.Abs(hour - _shownHour) < 0.02f || Mathf.Abs(Mathf.Abs(hour - _shownHour) - 24f) < 0.02f) return;
        _shownHour = hour;
        var elev = Mathf.Sin(Mathf.Pi * (hour - 6f) / 12f);          // >0 de dia, <0 de noite
        var day = Mathf.SmoothStep(-0.26f, 0.26f, elev);            // 0 noite … 1 dia (crepúsculo de ~17h a ~19h)
        var dusk = Mathf.Clamp(4f * day * (1f - day), 0f, 1f);      // pico na virada
        Night = 1f - day;
        var look = Mix(NightOf(_day), _day, day);
        look = Mix(look, DuskOf(_day), dusk * 0.75f);
        _look = look;

        // sol de dia (anda de leste a oeste, mais alto ao meio-dia); lua de noite, alta e do outro lado
        if (day >= 0.5f)
        {
            var t = Mathf.Clamp((hour - 6f) / 12f, 0f, 1f);
            var pitch = Mathf.Lerp(-6f, Mathf.Min(-30f, _day.SunPitch * 1.4f), Mathf.Clamp(elev, 0f, 1f));
            _sun.RotationDegrees = new Vector3(pitch, _day.SunYaw - 70f + 140f * t, 0);
            _sun.LightEnergy = look.SunEnergy * 0.88f * Mathf.SmoothStep(0.5f, 0.75f, day);
        }
        else
        {
            var t = ((hour + 6f) % 24f) / 12f;                          // 18h → 0 … 6h → 1
            _sun.RotationDegrees = new Vector3(-Mathf.Lerp(20f, 48f, Mathf.Sin(Mathf.Pi * t)), _day.SunYaw + 110f + 80f * t, 0);
            _sun.LightEnergy = look.SunEnergy * Mathf.SmoothStep(0.5f, 0.25f, day);
        }
        // 15/11 (Porto de noite: gente virava silhueta preta): o contraluz fica sempre do lado oposto da luz principal (sol ou lua)
        if (_fill is not null) _fill.RotationDegrees = new Vector3(-30f, _sun.RotationDegrees.Y + 180f, 0);
        _sun.LightColor = look.Sun;
        if (_fill is not null) { _fill.LightColor = look.Fill; _fill.LightEnergy = look.FillEnergy; }

        _env.BackgroundColor = look.SkyTop;
        _env.AmbientLightColor = look.Ambient;
        _env.AmbientLightEnergy = look.AmbientEnergy * 1.25f;
        _env.AmbientLightSkyContribution = Mathf.Lerp(0.55f, 0.2f, Night);   // 15/11: de noite o céu é quase preto; a cor ambiente (azul do luar) pesa mais
        _env.TonemapExposure = look.Exposure * 0.84f;
        _env.FogLightColor = look.Fog;
        _env.VolumetricFogAlbedo = look.VolFog;
        _env.FogDensity = Profundidade.Ativo(Map.Theme) && !Map.Instanced ? Profundidade.Densidade(Map.Theme, Night) : look.FogDensity * (_env.VolumetricFogEnabled ? 1f : 1.6f);
        _skyMat.SetShaderParameter("top_color", look.SkyTop);
        _skyMat.SetShaderParameter("horizon_color", look.Horizon);
        _skyMat.SetShaderParameter("ground_color", look.SkyTop.Darkened(0.5f));
        _skyMat.SetShaderParameter("sun_color", day >= 0.5f ? look.Sun : look.Sun * 0.45f);
        _skyMat.SetShaderParameter("clouds", _cloudMat is null ? look.Clouds : look.Clouds * 0.35f);
        _skyMat.SetShaderParameter("cloud_color", look.SkyTop.Lerp(look.Horizon, 0.35f).Lightened(0.08f));
        _skyMat.SetShaderParameter("stars", look.Stars);
        _flameBoost = 1f + 0.7f * Night;
        CicloDoDia.Noite = Night;
        _ceuMat?.SetShaderParameter("night", Night);
        UpdateCloudColors(look, Night);
        _terrainMat?.SetShaderParameter("cloud_shadow", _cloudMat is null ? 0f : 0.16f * day);
    }
    private readonly List<(MeshInstance3D Mesh, StandardMaterial3D Mat)> _puddles = new();

    private void AddEnvironment()
    {
        _look = LookFor(Map.Id == "reino_inferno" ? "inferno" : Map.Id == "fundo_triangulo" ? "submerso" : Map.Theme);
        var cave = Map.Theme == "gruta";
        _day = _look;
        var skyMat = _skyMat = new ShaderMaterial { Shader = new Shader { Code = SkyShader } };
        skyMat.SetShaderParameter("top_color", _look.SkyTop);
        skyMat.SetShaderParameter("horizon_color", _look.Horizon);
        skyMat.SetShaderParameter("ground_color", _look.SkyTop.Darkened(0.5f));
        skyMat.SetShaderParameter("sun_color", _look.Sun);
        skyMat.SetShaderParameter("clouds", _look.Clouds);
        skyMat.SetShaderParameter("cloud_color", _look.SkyTop.Lerp(_look.Horizon, 0.35f).Lightened(0.08f));
        skyMat.SetShaderParameter("stars", _look.Stars);
        var (style, ridge) = Map.Theme switch
        {
            "praia" => (3, new Color(0.30f, 0.26f, 0.30f)),
            "cidade" => (3, new Color(0.22f, 0.18f, 0.26f)),
            "vale" => (1, new Color(0.30f, 0.26f, 0.28f)),
            "mata" => (1, new Color(0.12f, 0.16f, 0.15f)),
            "canion" => (2, new Color(0.45f, 0.28f, 0.22f)),
            "brejo" => (1, new Color(0.16f, 0.20f, 0.18f)),
            "fantasma" => (2, new Color(0.08f, 0.09f, 0.14f)),
            "neve" => (4, new Color(0.40f, 0.44f, 0.55f)),
            "lava" => (2, new Color(0.14f, 0.07f, 0.06f)),
            "acropole" => (2, new Color(0.40f, 0.32f, 0.28f)),
            _ => (0, Colors.Black),
        };
        skyMat.SetShaderParameter("ridge_style", style);
        skyMat.SetShaderParameter("ridge_color", ridge);
        skyMat.SetShaderParameter("sea_color", new Color(0.18f, 0.26f, 0.36f));
        // 13/11 ("o fundo do Triângulo parece mar mesmo, mas o céu tem que parecer a superfície, não um céu"): no fundo,
        // em cima é o espelho d'água visto por baixo — janela clara ondulando no alto, raios de luz e azul escuro em volta
        if (Map.Id == "fundo_triangulo") skyMat = _skyMat = new ShaderMaterial { Shader = new Shader { Code = SuperficieShader } };
        _env = new Godot.Environment
        {
            BackgroundMode = cave ? Godot.Environment.BGMode.Color : Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMat, RadianceSize = Sky.RadianceSizeEnum.Size128 },
            BackgroundColor = _look.SkyTop,
            // luz ambiente: metade do céu (tom quente/frio por direção), metade da cor do tema
            AmbientLightSource = cave ? Godot.Environment.AmbientSource.Color : Godot.Environment.AmbientSource.Sky,
            AmbientLightColor = _look.Ambient,
            AmbientLightSkyContribution = 0.55f,
            AmbientLightEnergy = _look.AmbientEnergy * (cave ? 1.6f : 1.25f),   // feedback: mundo estourado/claro demais
            ReflectedLightSource = cave ? Godot.Environment.ReflectionSource.Disabled : Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            TonemapExposure = _look.Exposure * (cave ? 1f : 0.84f),
            TonemapWhite = 6f,
            FogEnabled = true,
            FogMode = Godot.Environment.FogModeEnum.Exponential,
            FogLightColor = _look.Fog,
            FogDensity = _look.FogDensity,
            FogAerialPerspective = cave ? 0f : 0.7f, // ao longe a névoa pega a cor do céu (profundidade)
            FogSkyAffect = 0.15f,
            FogHeight = 0.6f,
            FogHeightDensity = _look.HeightFog * 0.25f,
            GlowEnabled = true,
            GlowIntensity = 0.55f,
            GlowStrength = 1.0f,
            GlowBloom = 0.04f,
            GlowHdrThreshold = 1.3f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight,
            AdjustmentEnabled = true,
            AdjustmentContrast = 1.06f,
            AdjustmentSaturation = 1.08f,
            AdjustmentBrightness = 1.0f,
        };
        _env.SetGlowLevel(0, 0f); _env.SetGlowLevel(1, 0.6f); _env.SetGlowLevel(2, 1f); _env.SetGlowLevel(3, 0.8f); _env.SetGlowLevel(4, 0.4f);
        var we = new WorldEnvironment { Environment = _env }; Root.AddChild(we);
        if (!Map.Instanced) Profundidade.Aplicar(_env, we, Map.Theme);   // 12/11: névoa de profundidade (pacote Vila)

        _sun = new DirectionalLight3D
        {
            LightColor = _look.Sun,
            LightEnergy = _look.SunEnergy * (cave ? 1f : 0.88f),
            ShadowEnabled = true,
            ShadowBlur = 1.5f,
            ShadowBias = 0.02f,
            ShadowNormalBias = 0.6f,   // menos "peter-panning": a sombra cola no pé (o boneco parecia flutuar)
            RotationDegrees = new Vector3(_look.SunPitch, _look.SunYaw, 0),
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowBlendSplits = true,
            DirectionalShadowMaxDistance = 70f,
            LightAngularDistance = 1.2f, // sombra mais dura perto do pé, suave longe (com filtro suave)
        };
        if (OS.GetEnvironment("CDM_SEM_SOMBRA") == "1") _sun.ShadowEnabled = false;   // 16/11: teste dos anéis no chão
        Root.AddChild(_sun);
        // contraluz frio sem sombra (lado oposto ao sol): separa silhuetas e faz o "quente x frio" da prancha
        Root.AddChild(_fill = new DirectionalLight3D
        {
            LightColor = _look.Fill, LightEnergy = _look.FillEnergy, ShadowEnabled = false, LightSpecular = 0.3f,
            RotationDegrees = new Vector3(-35f, _look.SunYaw + 180f, 0),
        });
        ApplyQuality();
        AddSkyLife();
        // 17/11 (dono: "pq tem esse quadrado verde aqui? é só no portão onde os bichos não passam e na biblioteca"):
        // a película em volta dos refúgios saiu; fica só no portão e nas entradas da biblioteca
        // AddVeuDaMare();
    }

    /// <summary>
    /// Céu vivo e leve (pedido 10/11): nuvens que andam numa cúpula presa à câmera (o céu de fundo continua parado, então
    /// o reflexo não é recalculado), a sombra delas passando no chão (no shader do terreno) e bandos de pássaros/gaivotas
    /// circulando alto. Nada disso tem colisão nem passa pelo servidor.
    /// </summary>
    private const string SuperficieShader = @"
shader_type sky;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vn(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
void sky() {
    vec3 d = EYEDIR;
    vec3 fundo = vec3(0.01, 0.06, 0.11);
    vec3 agua = vec3(0.03, 0.22, 0.32);
    float alto = clamp(d.y, 0.0, 1.0);
    vec3 cor = mix(fundo, agua, smoothstep(-0.2, 0.35, d.y));
    if (d.y > 0.0) {
        // a superfície: ondas projetadas no plano de cima (coordenada = direção / altura)
        vec2 p = d.xz / max(d.y, 0.05) * 3.0;
        float t = TIME * 0.25;
        float o = vn(p + vec2(t, t * 0.6)) * 0.6 + vn(p * 2.3 - vec2(t * 1.3, -t)) * 0.4;
        float caust = pow(1.0 - abs(o * 2.0 - 1.0), 6.0);
        float janela = smoothstep(0.55, 0.95, alto);           // janela de Snell: claro bem em cima
        cor += vec3(0.25, 0.55, 0.6) * janela * (0.45 + 0.55 * o);
        cor += vec3(0.5, 0.8, 0.85) * caust * smoothstep(0.25, 0.8, alto) * 0.35;
        // raios de luz descendo (faixas que balançam)
        float raio = pow(max(0.0, sin(atan(d.x, d.z) * 9.0 + TIME * 0.2 + vn(vec2(d.x * 4.0, TIME * 0.1)) * 2.0)), 18.0);
        cor += vec3(0.25, 0.45, 0.5) * raio * smoothstep(0.05, 0.5, alto) * 0.25;
    }
    COLOR = cor;
}";

    // 15/11 (dono: "o céu com coisas bonitas: aurora boreal de noite, luas, planetas — luas e planetas visíveis de dia também;
    // nada muito elaborado, só detalhe para ambientar"): cúpula atrás das nuvens, presa à câmera. Duas luas (a grande, clara e
    // com crateras; a pequena, avermelhada), dois planetas (um azulado com anel, um dourado) — de dia, apagadinhos — e, de
    // noite, a aurora: cortinas verdes que puxam para o roxo no alto, só no lado norte, ondulando devagar.
    private const string CeuEnfeitesShader = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_front, fog_disabled, shadows_disabled;
uniform float night = 0.0; uniform float aurora = 0.0;
uniform vec3 cor_a = vec3(0.15, 1.0, 0.55); uniform vec3 cor_b = vec3(0.62, 0.28, 0.95);
uniform float centro = 0.0; uniform float largura = 1.5;
varying vec3 dir;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y); }
float fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int i = 0; i < 4; i++) { v += a * vnoise(p); p = p * 2.03 + 7.0; a *= 0.5; } return v; }
void vertex() { dir = normalize(VERTEX); }
// 16/11 (dono: «coloque mais coisas no céu do sul, pra ficar daora de ver e bonito, uma galáxia, três marias»): estrela
// pontual com brilho em volta (a = ângulo até ela); usada nas Três Marias, em Órion e no Cruzeiro do Sul
float estrela(vec3 d, vec3 p, float r, float fase) {
  float c = dot(d, normalize(p));
  if (c < 0.995) return 0.0;
  float a2 = 2.0 * (1.0 - c);   // ≈ ângulo²
  float pisca = 0.82 + 0.18 * sin(TIME * (2.1 + fase) + fase * 7.0);
  return (exp(-a2 / (r * r)) + 0.35 * exp(-a2 / (r * r * 9.0))) * pisca;
}
vec4 astro(vec3 d, vec3 c, float raio, vec3 cor, float crateras, float anel) {
  if (dot(d, c) < 0.9) return vec4(0.0);   // só perto do astro (senão aparecia um disco no lado oposto do céu)
  vec3 t1 = normalize(cross(c, vec3(0.0, 1.0, 0.0))); vec3 t2 = cross(t1, c);
  vec2 uv = vec2(dot(d - c, t1), dot(d - c, t2)) / raio;
  float r = length(uv);
  vec4 o = vec4(0.0);
  if (anel > 0.0) {   // anel: elipse inclinada em volta do planeta
    vec2 e = vec2(uv.x * 0.55 + uv.y * 0.2, uv.y * 1.9 - uv.x * 0.5);
    float re = length(e);
    float a = smoothstep(1.25, 1.35, re) * (1.0 - smoothstep(1.75, 1.9, re));
    o = vec4(cor * 0.85, a * 0.8 * anel);
  }
  if (r > 1.25) return o;
  float z = sqrt(max(0.0, 1.0 - r * r));
  float luz = clamp(dot(normalize(vec3(uv, z)), normalize(vec3(-0.55, 0.45, 0.7))), 0.0, 1.0);
  float cr = (vnoise(uv * 5.0 + c.xz * 9.0) * 0.6 + vnoise(uv * 12.0) * 0.4) * crateras;
  vec3 col = cor * (0.3 + 0.8 * luz) * (1.0 - cr * 0.4);
  float corpo = 1.0 - smoothstep(0.94, 1.0, r);
  float halo = (1.0 - smoothstep(1.0, 1.25, r)) * 0.22;
  vec4 p = vec4(mix(cor, col, corpo), max(corpo, halo));
  return vec4(mix(o.rgb, p.rgb, p.a), max(o.a, p.a));
}
void fragment() {
  vec3 d = normalize(dir);
  if (d.y < -0.03) discard;
  vec4 acc = vec4(0.0);
  if (night > 0.05 && aurora > 0.0 && d.y > 0.03) {
    // 15/11 (dono: aurora na horizontal, meio ondulada — reta na vertical fica estranho): duas fitas deitadas ao longo do
    // horizonte norte, serpenteando; borda de baixo mais viva e véu subindo um pouco, com leve granulado de raios
    // 15/11 noite (dono: a ponta da aurora corta seco): as fitas ocupam um arco do céu (centro/largura por mapa) e somem
    // devagar nas duas pontas; o ruído usa o círculo (cos, sin) do ângulo — sem emenda em lugar nenhum
    float ang = atan(d.x, -d.z);
    float rel = ang - centro; rel = rel - 6.2831853 * floor((rel + 3.1415927) / 6.2831853);
    float pontas = 1.0 - smoothstep(largura * 0.35, largura, abs(rel));
    vec2 roda = vec2(cos(ang), sin(ang));
    float I = 0.0; vec3 c = vec3(0.0);
    for (int k = 0; k < 2; k++) {
      float fk = float(k);
      float y0 = 0.17 + fk * 0.11 + 0.05 * sin(ang * (2.0 + fk) + TIME * (0.05 + fk * 0.03)) + 0.04 * (fbm(roda * 2.4 + vec2(fk * 5.0, TIME * 0.03)) - 0.5);
      float dy = d.y - y0;
      float fita = dy < 0.0 ? exp(-dy * dy / 0.0006) : exp(-dy * dy / (0.006 + fk * 0.003));
      float liga = smoothstep(0.25, 0.65, fbm(roda * 1.8 + vec2(TIME * 0.02 + fk * 9.0, fk)));
      // 16/11 (dono: «ainda tem algumas linhas dentro da aurora»): o granulado de raios (seno de 70 voltas) desenhava riscos
      // verticais finos; agora a variação é só um ruído macio
      float grao = 0.88 + 0.12 * fbm(roda * 3.0 + vec2(TIME * 0.04, d.y * 4.0));
      float v = fita * liga * grao * (1.0 - fk * 0.35);
      I += v;
      c += v * mix(cor_a, cor_b, clamp(dy * 8.0 + fk * 0.4, 0.0, 1.0));
    }
    c = I > 0.001 ? c / I : c;
    float norte = pontas * smoothstep(0.03, 0.09, d.y);
    acc = vec4(c, clamp(I, 0.0, 1.0) * norte * night * aurora * 0.6);
  }
  // 16/11: CÉU DO SUL à noite — a Via Láctea (faixa de poeira estelar com núcleo dourado e fenda escura), as Três Marias
  // com o resto de Órion (Betelgeuse laranja, Rigel azul, a nebulosa rosada da espada) e o Cruzeiro do Sul perto do horizonte
  if (night > 0.05 && d.y > 0.0) {
    vec3 nucleo = normalize(vec3(-0.10, 0.20, 0.97));          // centro da galáxia: no sul, baixo (17/11: «pequena, lá no fundo»)
    vec3 outro = normalize(vec3(0.95, 0.12, 0.25));            // a faixa corre baixa pelo sudeste, perto do horizonte
    vec3 polo = normalize(cross(nucleo, outro));
    vec3 eixo2 = normalize(cross(polo, nucleo));
    float b = dot(d, polo);
    vec2 g = vec2(atan(dot(d, eixo2), dot(d, nucleo)), b);
    float faixa = exp(-b * b / 0.0045);
    float perto = exp(-(g.x * g.x) / 0.12);                   // mais forte em volta do núcleo
    faixa *= exp(-(g.x * g.x) / 0.9);                        // só um trecho da faixa (some antes de cruzar o céu)
    float nuvem = fbm(g * vec2(8.0, 45.0) + 3.0) * 0.75 + fbm(g * vec2(24.0, 120.0)) * 0.35;
    float fenda = smoothstep(0.45, 0.75, fbm(g * vec2(9.0, 60.0) + 11.0)) * exp(-b * b / 0.0006) * (0.4 + 0.6 * perto);
    float via = faixa * (0.25 + 0.75 * nuvem) * (0.55 + 1.3 * perto) * (1.0 - 0.75 * fenda);
    vec3 corVia = mix(vec3(0.58, 0.66, 0.95), vec3(1.0, 0.84, 0.66), clamp(perto * 1.2 - abs(b) * 4.0, 0.0, 1.0));
    // poeira de estrelinhas: mais densa dentro da faixa
    vec2 q = d.xz / (1.0 + d.y) * 260.0;
    vec2 cel = floor(q); vec2 f = fract(q) - 0.5;
    float hh = hash(cel);
    float limiar = 0.985 - faixa * 0.05;
    float pont = hh > limiar ? smoothstep(0.22, 0.0, length(f + (vec2(hash(cel + 3.1), hash(cel + 7.7)) - 0.5) * 0.5)) * (0.5 + 0.5 * sin(TIME * 1.7 + hh * 40.0)) : 0.0;
    float kVia = clamp(via * 0.30, 0.0, 0.32) * night * smoothstep(0.0, 0.12, d.y);
    acc = vec4(mix(acc.rgb, corVia, kVia), max(acc.a, kVia));
    float kP = pont * night * 0.9 * smoothstep(0.02, 0.1, d.y);
    acc = vec4(mix(acc.rgb, vec3(1.0, 0.97, 0.92), kP), max(acc.a, kP));

    // Órion e as Três Marias (sul, bem visível)
    vec3 cO = normalize(vec3(0.38, 0.62, 0.69));
    vec3 tO = normalize(cross(cO, vec3(0.0, 1.0, 0.0)));
    vec3 uO = normalize(cross(tO, cO));
    vec3 cinto = normalize(tO * 0.94 + uO * 0.34);              // o cinto levemente inclinado
    float tm = estrela(d, cO - cinto * 0.034, 0.0045, 0.1) + estrela(d, cO, 0.0047, 1.3) + estrela(d, cO + cinto * 0.033, 0.0044, 2.2);
    float azul = estrela(d, cO - uO * 0.115 + tO * 0.05, 0.0055, 3.0) + estrela(d, cO - uO * 0.11 - tO * 0.05, 0.0034, 4.1) * 0.7
      + estrela(d, cO + uO * 0.10 + tO * 0.055, 0.0036, 5.2) * 0.75;
    float laranja = estrela(d, cO + uO * 0.115 - tO * 0.052, 0.0058, 6.3);
    float neb = exp(-(2.0 * (1.0 - dot(d, normalize(cO - uO * 0.05 + tO * 0.006)))) / 0.00012) * (0.6 + 0.4 * fbm(d.xy * 90.0));
    vec3 cOri = vec3(0.0); float kO = 0.0;
    cOri += vec3(0.86, 0.92, 1.0) * tm; kO += tm;
    cOri += vec3(0.62, 0.78, 1.0) * azul; kO += azul;
    cOri += vec3(1.0, 0.58, 0.32) * laranja; kO += laranja;
    cOri += vec3(1.0, 0.45, 0.65) * neb * 0.6; kO += neb * 0.5;
    if (kO > 0.001) { float k = clamp(kO, 0.0, 1.0) * night; acc = vec4(mix(acc.rgb, cOri / kO, k), max(acc.a, k)); }

    // Cruzeiro do Sul (baixo, a sudoeste)
    vec3 cC = normalize(vec3(-0.52, 0.26, 0.81));
    vec3 tC = normalize(cross(cC, vec3(0.0, 1.0, 0.0)));
    vec3 uC = normalize(cross(tC, cC));
    float cruz = estrela(d, cC + uC * 0.05, 0.0042, 0.7) + estrela(d, cC - uC * 0.06, 0.0048, 1.9)
      + estrela(d, cC + tC * 0.035 + uC * 0.005, 0.0038, 2.8) + estrela(d, cC - tC * 0.03 - uC * 0.008, 0.0036, 3.6)
      + estrela(d, cC - tC * 0.012 - uC * 0.022, 0.0022, 4.4) * 0.7;
    if (cruz > 0.001) { float k = clamp(cruz, 0.0, 1.0) * night; acc = vec4(mix(acc.rgb, vec3(0.9, 0.94, 1.0), k), max(acc.a, k)); }
  }
  float vis = mix(0.32, 1.0, night);
  vec4 l1 = astro(d, normalize(vec3(-0.55, 0.52, -0.64)), 0.07, vec3(0.93, 0.91, 0.84), 1.0, 0.0);
  vec4 l2 = astro(d, normalize(vec3(0.72, 0.34, -0.28)), 0.032, vec3(0.95, 0.58, 0.42), 0.7, 0.0);
  vec4 p1 = astro(d, normalize(vec3(0.28, 0.6, 0.75)), 0.018, vec3(0.62, 0.78, 1.0), 0.15, 1.0);
  vec4 p2 = astro(d, normalize(vec3(-0.8, 0.26, 0.52)), 0.009, vec3(1.0, 0.86, 0.55), 0.0, 0.0);
  for (int i = 0; i < 4; i++) {
    vec4 a = i == 0 ? l1 : i == 1 ? l2 : i == 2 ? p1 : p2;
    float k = a.a * vis * (i >= 2 ? mix(0.7, 1.0, night) : 1.0);
    acc = vec4(mix(acc.rgb, a.rgb, k), max(acc.a, k));
  }
  if (acc.a < 0.003) discard;
  ALBEDO = acc.rgb;
  ALPHA = acc.a * smoothstep(-0.03, 0.06, d.y);
}";
    private MeshInstance3D? _ceuDome;
    private static readonly Dictionary<string, (float Forca, Color A, Color B, float Centro, float Largura)> Auroras = new()
    {
        // 15/11 noite (dono: "não era pra tirar da praça — praça é a primeira cidade"): Porto das Cinzas volta a ter
        ["porto_das_cinzas"] = (1.1f, new Color(0.15f, 1.0f, 0.55f), new Color(0.62f, 0.28f, 0.95f), 0.0f, 1.7f),
        ["coroa_da_geada"] = (1.4f, new Color(0.15f, 1.0f, 0.55f), new Color(0.62f, 0.28f, 0.95f), 0.0f, 1.9f),
        ["pico_vento"] = (1.1f, new Color(0.2f, 0.95f, 0.75f), new Color(0.3f, 0.55f, 1.0f), 0.9f, 1.4f),
        ["vespera"] = (0.85f, new Color(0.55f, 1.0f, 0.75f), new Color(0.35f, 0.75f, 0.85f), -0.7f, 1.2f),
        ["necropole_juramentos"] = (0.9f, new Color(0.95f, 0.3f, 0.35f), new Color(0.55f, 0.2f, 0.9f), 0.4f, 1.3f),
        ["charco_lanternas"] = (0.75f, new Color(0.75f, 1.0f, 0.3f), new Color(0.2f, 0.9f, 0.5f), -1.2f, 1.0f),
        ["caminho_serpente"] = (1.0f, new Color(1.0f, 0.45f, 0.75f), new Color(0.35f, 0.5f, 1.0f), 2.2f, 1.5f),
        ["mata_dos_enforcados"] = (0.6f, new Color(0.4f, 0.9f, 0.6f), new Color(0.45f, 0.3f, 0.7f), -2.0f, 0.9f),
    };
    private ShaderMaterial? _ceuMat;

    // 17/11 (dono: "na frente do portão o meu ataque não passa e os monstros não atravessam — tem tipo uma barreira; coloca uma
    // película com uma cor verde, mostrando que tem algo ali, e aproveita para criar uma lore: os bichos não podem sair"):
    // o VÉU DA MARÉ — em volta de cada refúgio (zona "seguro") dos mapas abertos, uma cortina verde translúcida, mais viva
    // embaixo, com ondas subindo devagar; de longe quase some, de perto aparece. Lore no Livro da Biblioteca do Cais.
    private const string VeuShader = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform vec3 cor = vec3(0.3, 1.0, 0.55);
varying vec3 mundo;
void vertex() { mundo = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    float y = UV.y;                       // 0 em cima, 1 embaixo
    float base = smoothstep(0.0, 1.0, y);
    float onda = 0.5 + 0.5 * sin((mundo.x + mundo.z) * 1.7 - TIME * 1.3 + y * 6.0);
    float fio = smoothstep(0.92, 1.0, y) * 0.8;
    float perto = 1.0 - smoothstep(6.0, 22.0, length(CAMERA_POSITION_WORLD - mundo));
    float a = (0.05 + 0.13 * base * onda + fio) * (0.25 + 0.75 * perto);
    ALBEDO = cor * a;
}";

    private void AddVeuDaMare()
    {
        if (Map.Instanced || Map.HalfSize < 30 || Map.Kind == "cidade") return;
        var mat = new ShaderMaterial { Shader = new Shader { Code = VeuShader }, RenderPriority = 2 };
        foreach (var z in Map.Zones)
        {
            if (z.Kind != "seguro") continue;
            var (x0, x1, y0, y1) = ((float)(z.X - z.W / 2), (float)(z.X + z.W / 2), (float)(z.Y - z.H / 2), (float)(z.Y + z.H / 2));
            foreach (var (a, b) in new[] { (new Vector2(x0, y0), new Vector2(x1, y0)), (new Vector2(x1, y0), new Vector2(x1, y1)),
                                           (new Vector2(x1, y1), new Vector2(x0, y1)), (new Vector2(x0, y1), new Vector2(x0, y0)) })
            {
                var len = (b - a).Length();
                var meio = (a + b) / 2f;
                var plano = new MeshInstance3D
                {
                    Mesh = new QuadMesh { Size = new Vector2(len, 3.2f), Material = mat },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(meio.X, 1.6f, meio.Y),
                    Rotation = new Vector3(0, -Mathf.Atan2(b.Y - a.Y, b.X - a.X), 0),
                };
                plano.SetMeta("nao_juntar", true);
                Root.AddChild(plano);
            }
        }
    }

    private void AddSkyLife()
    {
        if (Map.Id == "fundo_triangulo") { if (!Map.Instanced) VidaDoTema(); return; }   // 13/11: sem nuvens no fundo do mar
        if (!HasDayCycle || Map.Theme is "lava") return;
        _ceuMat = new ShaderMaterial { Shader = new Shader { Code = CeuEnfeitesShader }, RenderPriority = -12 };
        // 15/11 noite (dono: "aurora não em todos os mapas — só em alguns mais pra frente, variando, e só à noite; tira da Costa"):
        // cada mapa escolhido tem a sua (força, cores, onde fica no céu); nos outros não há aurora
        if (Auroras.TryGetValue(Map.Id, out var au))
        {
            _ceuMat.SetShaderParameter("aurora", au.Forca);
            _ceuMat.SetShaderParameter("cor_a", au.A); _ceuMat.SetShaderParameter("cor_b", au.B);
            _ceuMat.SetShaderParameter("centro", au.Centro); _ceuMat.SetShaderParameter("largura", au.Largura);
        }
        _ceuDome = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 245f, Height = 490f, RadialSegments = 96, Rings = 48, Material = _ceuMat },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Root.AddChild(_ceuDome);
        _cloudMat = new ShaderMaterial { Shader = new Shader { Code = CloudShader }, RenderPriority = -10 };
        _cloudMat.SetShaderParameter("cover", Mathf.Clamp(_day.Clouds * 0.9f, 0.3f, 0.75f));
        _cloudDome = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 230f, Height = 460f, RadialSegments = 32, Rings = 12, Material = _cloudMat },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Root.AddChild(_cloudDome);
        _skyMat?.SetShaderParameter("clouds", _day.Clouds * 0.35f);   // as nuvens "de verdade" agora são as da cúpula
        _terrainMat?.SetShaderParameter("cloud_shadow", 0.16f);
        UpdateCloudColors(_day, 0f);

        // 13/11 ("bichinhos só de cenário nos outros lugares também; algo que deixa mais vivo"): por tema
        if (!Map.Instanced && Map.Kind != "cidade") VidaDoTema();
        if (Map.Theme is "fantasma") return;
        if (Map.Theme == "praia" && Visuals.GlbCache.Instance("costa/vida_gaivota") is { } probe)
        {
            probe.QueueFree();
            _vida.Gaivotas(8); _vida.Siris(10); _vida.Vento();
            return;
        }
        var gull = Map.Theme is "praia" or "cidade";
        var bodyMat = new StandardMaterial3D { AlbedoColor = gull ? new Color(0.92f, 0.92f, 0.9f) : new Color(0.12f, 0.11f, 0.11f), Roughness = 0.9f };
        var wingMat = new StandardMaterial3D { AlbedoColor = gull ? new Color(0.78f, 0.80f, 0.82f) : new Color(0.10f, 0.09f, 0.09f), Roughness = 0.9f };
        var tipMat = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.1f), Roughness = 0.9f };
        _birdRoot = new Node3D();
        Root.AddChild(_birdRoot);
        var rng = new RandomNumberGenerator { Seed = (ulong)Map.Id.GetHashCode() };
        var half = (float)Map.HalfSize;
        for (var f = 0; f < 3; f++)
        {
            var center = new Vector3(rng.RandfRange(-half * 0.6f, half * 0.6f), 0, rng.RandfRange(-half * 0.6f, half * 0.6f));
            var radius = rng.RandfRange(14f, 30f);
            var speed = rng.RandfRange(0.10f, 0.18f) * (rng.Randf() < 0.5f ? 1f : -1f);
            var count = rng.RandiRange(3, 5);
            for (var i = 0; i < count; i++)
            {
                var bird = new Node3D { Scale = Vector3.One * (gull ? 1.3f : 1f) };
                bird.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.09f, Height = 0.55f, Material = bodyMat }, RotationDegrees = new Vector3(90, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                Node3D Wing(float side)
                {
                    var pivot = new Node3D { Position = new Vector3(side * 0.06f, 0, 0) };
                    pivot.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.62f, 0.02f, 0.2f), Material = wingMat }, Position = new Vector3(side * 0.31f, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                    if (gull) pivot.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.022f, 0.16f), Material = tipMat }, Position = new Vector3(side * 0.6f, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                    bird.AddChild(pivot);
                    return pivot;
                }
                var wl = Wing(-1f); var wr = Wing(1f);
                _birdRoot.AddChild(bird);
                _birds.Add((bird, wl, wr, center + new Vector3(rng.RandfRange(-3f, 3f), 0, rng.RandfRange(-3f, 3f)), radius + rng.RandfRange(-2f, 2f),
                    speed * rng.RandfRange(0.9f, 1.1f), i * 0.22f + rng.Randf() * 0.1f, rng.RandfRange(16f, 26f)));
            }
        }
    }

    private void UpdateCloudColors(Look look, float night)
    {
        if (_cloudMat is null) return;
        var lit = look.Horizon.Lerp(Colors.White, 0.55f).Lerp(look.Sun, 0.25f) * (1f - 0.8f * night);
        var shade = look.SkyTop.Lerp(look.Horizon, 0.4f).Lerp(new Color(0.55f, 0.58f, 0.66f), 0.5f) * (1f - 0.75f * night);
        _cloudMat.SetShaderParameter("lit", new Vector3(lit.R, lit.G, lit.B));
        _cloudMat.SetShaderParameter("shade", new Vector3(shade.R, shade.G, shade.B));
        _cloudMat.SetShaderParameter("opacity", Mathf.Lerp(0.88f, 0.6f, night));
        if (_sun is not null) _cloudMat.SetShaderParameter("sun_dir", _sun.Transform.Basis.Z);
    }

    /// <summary>Cúpula de nuvens presa à câmera e o voo dos pássaros (chamado por quadro).</summary>
    private void UpdateSkyLife(float time)
    {
        if (_cloudDome is not null && _cloudDome.GetViewport()?.GetCamera3D() is { } cam)
            _cloudDome.GlobalPosition = cam.GlobalPosition;
        if (_ceuDome is not null && _ceuDome.GetViewport()?.GetCamera3D() is { } cam2)
            _ceuDome.GlobalPosition = cam2.GlobalPosition;   // 15/11: luas, planetas e aurora
        if (_birdRoot is null) return;
        _birdRoot.Visible = Night < 0.6f;
        if (!_birdRoot.Visible) return;
        foreach (var (bird, wl, wr, c, r, sp, ph, h) in _birds)
        {
            var a = time * sp + ph;
            var wob = Mathf.Sin(time * 0.3f + ph * 5f);
            var pos = c + new Vector3(Mathf.Cos(a) * (r + wob * 3f), h + wob * 1.5f, Mathf.Sin(a) * (r + wob * 3f));
            var vel = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * Mathf.Sign(sp);
            bird.Position = pos;
            bird.LookAt(pos + vel, Vector3.Up);
            bird.RotateObjectLocal(Vector3.Forward, -0.25f * Mathf.Sign(sp));   // inclina na curva
            // bate as asas e plana: ciclos de batida com pausas
            var glide = Mathf.SmoothStep(0.2f, 0.6f, Mathf.Sin(time * 0.7f + ph * 3f));
            var flap = Mathf.Sin(time * 9f + ph * 7f) * 38f * (1f - glide) + 8f * glide;
            wl.RotationDegrees = new Vector3(0, 0, -flap);
            wr.RotationDegrees = new Vector3(0, 0, flap);
        }
    }

    /// <summary>Liga/desliga os efeitos caros conforme a qualidade escolhida (F10). Chamado ao montar o mapa e ao trocar.</summary>
    public void ApplyQuality()
    {
        if (_env is null || _sun is null) return;
        var q = GraphicsQuality.Current;
        var hi = q == GraphicsQuality.Level.Alto;
        var mid = q >= GraphicsQuality.Level.Medio;
        // sombra de contato: escurece onde as coisas se encostam (pés no chão, pé de parede, debaixo de barril)
        _env.SsaoEnabled = mid;
        _env.SsaoRadius = 1.4f;
        _env.SsaoIntensity = 2.2f;
        _env.SsaoPower = 1.6f;
        _env.SsaoDetail = 0.6f;
        _env.SsaoLightAffect = 0.25f;
        // luz rebatida: as chamas e o sol tingem as paredes próximas
        _env.SsilEnabled = hi;
        _env.SsilRadius = 4f;
        _env.SsilIntensity = 1.1f;
        // névoa com volume: raios de luz e halos nas lanternas
        _env.VolumetricFogEnabled = mid && _look.VolDensity > 0f;
        _env.VolumetricFogDensity = _look.VolDensity;
        _env.VolumetricFogAlbedo = _look.VolFog;
        _env.VolumetricFogAnisotropy = 0.55f;
        _env.VolumetricFogLength = 48f;
        _env.VolumetricFogDetailSpread = 2f;
        _env.VolumetricFogSkyAffect = 0.2f;
        _env.VolumetricFogAmbientInject = 0.4f;
        // reflexos nas poças e pedra molhada
        _env.SsrEnabled = hi;
        _env.SsrMaxSteps = 48;
        _env.GlowEnabled = true;
        // sem névoa com volume, a névoa comum fica um pouco mais densa para manter a profundidade
        _env.FogDensity = Profundidade.Ativo(Map.Theme) && !Map.Instanced ? Profundidade.Densidade(Map.Theme, Night) : _look.FogDensity * (_env.VolumetricFogEnabled ? 1f : 1.6f);
        _sun.DirectionalShadowMaxDistance = hi ? 60f : mid ? 45f : 35f;   // 13/11: menos faixa de sombra (desempenho)
        _sun.DirectionalShadowMode = q == GraphicsQuality.Level.Baixo ? DirectionalLight3D.ShadowMode.Parallel2Splits : DirectionalLight3D.ShadowMode.Parallel4Splits;
        foreach (var (light, _, _) in _flames)
            light.ShadowEnabled = hi && light.OmniRange >= 8f; // só as chamas grandes (forja, fogueira, braseiro, tocha) projetam sombra
    }

    /// <summary>
    /// 13/11 (pedido: "rastro de sangue, corpo morto de decoração, algo que deixa mais vivo"): cadaver = o NOSSO boneco
    /// deitado de costas (aventureiro que não teve sorte), com mancha embaixo; sangue = rastro de gotas e poças no chão,
    /// ao longo do eixo do yaw (Tools/gerar_vida_cenario.py).
    /// </summary>
    private void DecoracaoMorbida(Node3D node, MapProp p)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(Math.Abs(p.X * 131 + p.Y * 977) + 7) };
        // emissão quase zero: o TexturizeAll não põe tijolo em cima (ele só texturiza material liso sem emissão)
        StandardMaterial3D Sangue(Color c, float rough) { var m = VisualKit.Mat(c, rough); m.EmissionEnabled = true; m.Emission = c * 0.08f; return m; }
        var seco = Sangue(new Color(0.30f, 0.035f, 0.03f), 0.45f);
        var fresco = Sangue(new Color(0.48f, 0.03f, 0.03f), 0.08f);
        void Mancha(Vector3 at, float r, bool novo)
        {
            var m = VisualKit.Part(node, new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.01f, RadialSegments = 10 }, novo ? fresco : seco,
                at + new Vector3(0, 0.012f, 0));
            m.Scale = new Vector3(rng.RandfRange(0.7f, 1.3f), 1f, rng.RandfRange(0.6f, 1.2f));
            m.RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0);
            m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        if (p.Kind == "sangue")
        {
            // rastro: 6–9 m, gotas que ficam mais raras, com 1–2 poças
            var comp = rng.RandfRange(5f, 9f);
            for (var z = 0f; z < comp; z += rng.RandfRange(0.25f, 0.7f))
                Mancha(new Vector3(Mathf.Sin(z * 0.7f) * 0.35f + rng.RandfRange(-0.15f, 0.15f), 0, z), rng.RandfRange(0.05f, 0.14f), false);
            Mancha(Vector3.Zero, rng.RandfRange(0.45f, 0.7f), true);
            if (rng.Randf() < 0.6f) Mancha(new Vector3(0.3f, 0, comp * 0.6f), rng.RandfRange(0.25f, 0.4f), false);
            return;
        }
        var corpo = VisualFactory.NpcVisual(rng.Randf() < 0.5f ? "aldeao" : "guarda", "morto_" + (int)(p.X * 10) + "_" + (int)(p.Y * 10), "");
        var deitado = new Node3D { Position = new Vector3(0, 0.13f, 0), RotationDegrees = new Vector3(-90, 0, rng.RandfRange(-12f, 12f)) };
        node.AddChild(deitado);
        deitado.AddChild(corpo.Root);
        _mortos.Add(corpo);
        Mancha(new Vector3(0.1f, 0, -0.6f), 0.75f, false);
        Mancha(new Vector3(-0.3f, 0, 0.4f), 0.35f, true);
        // a arma que ele largou
        var lamina = VisualKit.Mat(new Color(0.55f, 0.55f, 0.58f), 0.35f, 0.8f);
        VisualKit.Part(node, VisualKit.Box(0.06f, 0.02f, 0.9f), lamina, new Vector3(0.85f, 0.02f, -0.3f), new Vector3(0, rng.RandfRange(20, 70), 0));
    }

    /// <summary>13/11: bichinhos de enfeite e partículas no ar por tema (ratos, insetos do lodo, vaga-lumes, brasas, neve, cinzas).</summary>
    private void VidaDoTema()
    {
        if (Map.Id == "fundo_triangulo")   // 13/11: fundo do mar — siris e bolhas subindo
        {
            _vida.Bichinhos("criaturas/caranguejinho", 0.25f, 6, 0.8f, lateral: true, tinta: new Color(0.6f, 0.25f, 0.2f));
            _vida.Luzinhas("bolha");
            return;
        }
        switch (Map.Theme)
        {
            case "mata": case "vale":
                _vida.Bichinhos("criaturas/rato_do_cais", 0.3f, 5, 1.6f, tinta: new Color(0.35f, 0.27f, 0.2f));
                _vida.Luzinhas(Map.Theme == "mata" ? "folha" : "borboleta");   // 13/11: mais vida (o de noite fica por último)
                _vida.Luzinhas(Map.Theme == "mata" ? "vagalume" : "folha");
                break;
            case "praia":
                _vida.Luzinhas("poeira");
                break;
            case "brejo":
                _vida.Bichinhos("criaturas/inseto_lodo", 0.35f, 6, 1.1f);
                _vida.Bichinhos("criaturas/rato_do_cais", 0.28f, 3, 1.6f, tinta: new Color(0.25f, 0.24f, 0.18f));
                _vida.Luzinhas("mosquito");
                _vida.Luzinhas("vagalume");
                break;
            case "fantasma":
                _vida.Bichinhos("criaturas/rato_do_cais", 0.3f, 7, 1.8f, tinta: new Color(0.2f, 0.2f, 0.22f));
                _vida.Luzinhas("cinza");
                break;
            case "canion": case "acropole":
                _vida.Bichinhos("criaturas/rato_do_cais", 0.28f, 4, 1.7f, tinta: new Color(0.55f, 0.45f, 0.32f));
                _vida.Bichinhos("criaturas/caranguejinho", 0.22f, 3, 0.8f, lateral: true, tinta: new Color(0.3f, 0.25f, 0.2f));   // escorpiãozinho de pedra
                _vida.Luzinhas("poeira");
                break;
            case "lava":
                _vida.Bichinhos("criaturas/caranguejinho", 0.25f, 5, 0.9f, lateral: true, tinta: new Color(0.25f, 0.1f, 0.06f));
                _vida.Luzinhas("brasa");
                break;
            case "neve":
                _vida.Luzinhas("neve");
                break;
        }
    }

    private void AddGround()
    {
        if (Map.Id == "taverna_andar2" && TavernaUpstairs()) return;
        if (Map.Id == "casa_pequena" && Casa6x6()) return;   // 16/11 (pendência u79_casa6x6)
        if (Map.Theme == "casarao" && CasaraoUpstairs()) return;
        if (Map.Theme == "biblioteca" && BibliotecaHall()) return;
        if (Map.Theme == "reinos") { IlhaDosReinos(); return; }
        if (Map.Theme == "ceu")
        {
            AddCloudFloor();
            if (Map.Id == "caminho_serpente") Serpente();   // 15/11 noite: o caminho é o dorso de uma serpente gigante
            if (Map.Id == "reino_celeste")   // 12/11: Cidade Celestial — praça de mármore branco flutuando nas nuvens
            {
                var lado = (float)Map.HalfSize * 2f;
                var marmore = TexMat("chao_pedra.jpg", new Color(1.35f, 1.32f, 1.25f), 0.16f);
                VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(lado, lado) }, marmore, new Vector3(0, 0.03f, 0)).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                VisualKit.Part(Root, new BoxMesh { Size = new Vector3(lado, 1.2f, lado) }, VisualKit.Mat(new Color(0.85f, 0.83f, 0.78f), 0.8f), new Vector3(0, -0.6f, 0));
            }
            return;
        }
        var size = (float)Map.HalfSize * 2f + 30f;
        var groundMat = VisualKit.Mat(Map.Theme switch
        {
            "praia" => new Color(0.50f, 0.44f, 0.33f),
            "gruta" => new Color(0.17f, 0.17f, 0.18f),
            _ => new Color(0.30f, 0.28f, 0.25f),
        }, 0.95f);
        var (texName, tile) = Map.Theme switch
        {
            "praia" => ("gen_areia.png", 7f),
            "cidade" => ("gen_calcamento.png", 3.2f),
            "mata" or "campo" or "brejo" => ("gen_grama.png", 7f),
            "neve" or "lua" => ("gen_areia.png", 9f),
            "lava" or "canion" => ("gen_terra.png", 7f),
            _ => ("chao_pedra.jpg", 6f),
        };
        var texture = VisualAssets.Load(texName);
        if (texture is not null)
        {
            groundMat.AlbedoTexture = texture;
            groundMat.AlbedoColor = Map.Theme switch
            {
                "gruta" => new Color(0.40f, 0.39f, 0.42f),
                "cidade" => new Color(0.72f, 0.70f, 0.74f),
                "praia" => new Color(0.84f, 0.82f, 0.80f),
                "neve" => new Color(1.6f, 1.65f, 1.75f),
                "lava" => new Color(0.45f, 0.32f, 0.28f),
                "lua" => new Color(0.62f, 0.62f, 0.68f),   // 12/11: poeira lunar cinza
                "brejo" => new Color(0.62f, 0.70f, 0.55f),
                "canion" => new Color(1.25f, 1.0f, 0.85f),
                _ => Colors.White,
            };
            groundMat.Uv1Scale = new Vector3(size / tile, size / tile, 1f);
            VisualAssets.AddRelief(groundMat, texName, texName == "gen_calcamento.png" ? 1.4f : 1f);
            if (texName == "chao_pedra.jpg" && VisualAssets.Load("chao_pedra_normal.png") is { } stoneN)
            {
                groundMat.NormalEnabled = true;
                groundMat.NormalTexture = stoneN;
            }
            // calçamento um pouco menos áspero: pega o brilho do céu e das lanternas como pedra úmida
            if (Map.Theme == "cidade") groundMat.Roughness = 0.72f;
        }
        if (Map.Theme == "cidade") AddPuddles(size);
        else if (Map.Theme != "gruta") { AddScatter(size); AddHorizon(size); }
        // 10/11 item 56 ("mar do lado certo da Costa; hoje parece lago"): o mar aberto fica a leste, do lado do farol e da
        // muralha do cais; a linha d'água passa logo depois do penhasco e vai até o horizonte (sem serras desse lado)
        if (Map.Theme == "praia") MarDaCosta.Construir(Root, (float)Map.HalfSize, "leste", recuo: -5f);
        AddRoads();
        if (TerrainMaterial() is { } terrain)
        {
            // terreno aberto: duas texturas detalhadas misturadas em manchas grandes (sem repetição visível), UV pelo
            // mundo — o chão de fora (até o horizonte) continua igual sem emenda
            if (Visuals.EscadaCasa.Elevacoes.Count > 0)
            {
                // 13/11: morros e platôs — o próprio chão sobe (malha subdividida + TerrainShader.elev_*), sem emenda
                var morros = (ShaderMaterial)terrain.Duplicate();
                var rs = new Vector4[32]; var hs = new Vector2[32];
                var n = Math.Min(32, Visuals.EscadaCasa.Elevacoes.Count);
                for (var i = 0; i < n; i++)
                {
                    var e = Visuals.EscadaCasa.Elevacoes[i];
                    rs[i] = new Vector4(e.X, e.Z, e.MeiaW, e.MeiaH); hs[i] = new Vector2(e.H, e.Rampa);
                }
                morros.SetShaderParameter("elev_r", rs);
                morros.SetShaderParameter("elev_h", hs);
                morros.SetShaderParameter("elev_n", n);
                var div = (int)Math.Min(300, size * 1.25f);
                VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(size, size), SubdivideWidth = div, SubdivideDepth = div }, morros, Vector3.Zero).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
            else if (Relevo.Amplitude > 0f)
            {
                // 12/11: relevo de teste (Laboratório): chão subdividido que ondula até 0,5 m; os pés acompanham (Relevo.Altura)
                var ondulado = (ShaderMaterial)terrain.Duplicate();
                ondulado.SetShaderParameter("relevo", Relevo.Amplitude);
                ondulado.SetShaderParameter("relevo_meia", Relevo.MeiaLargura);
                var div = (int)Math.Min(240, size * 2);
                VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(size, size), SubdivideWidth = div, SubdivideDepth = div }, ondulado, Vector3.Zero).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
            // 16/11 (dono: "ondulações que dá pra ver de longe" — anéis em volta da câmera na areia): era o próprio chão fazendo
            // sombra em si mesmo (acne de sombra); chão não precisa projetar sombra
            else VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(size, size) }, terrain, Vector3.Zero).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(2400f, 2400f) }, terrain, new Vector3(0, -0.06f, 0)).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        else VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(size, size) }, groundMat, Vector3.Zero).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        if (Map.Theme is not ("gruta" or "praia" or "vale" or "mata" or "brejo" or "canion" or "lava" or "neve" or "acropole" or "fantasma") && groundMat is StandardMaterial3D gm)
        {
            // chão continua além das bordas até o horizonte (sem "fim do mundo" atrás dos muros)
            var outer = (StandardMaterial3D)gm.Duplicate();
            const float far = 2400f;
            outer.Uv1Scale = gm.Uv1Scale * (far / size);
            outer.AlbedoColor = gm.AlbedoColor.Darkened(0.12f);
            VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(far, far) }, outer, new Vector3(0, -0.06f, 0)).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        // 13/11: o chão (os planos grandes) também fica na camada 11 — é onde as pinceladas do editor pintam
        foreach (var c in Root.GetChildren())
            if (c is MeshInstance3D { Mesh: PlaneMesh pm } chao && pm.Size.X >= size - 1f) chao.Layers |= ChaoLayer;
        // chão em camada própria: a câmera ignora o chão (tem trava de altura) e só desvia de paredes/colunas
        var body = new StaticBody3D { CollisionLayer = ThirdPersonCamera.FloorLayer };
        body.AddChild(new CollisionShape3D { Shape = new WorldBoundaryShape3D() });
        Root.AddChild(body);

        var dirt = VisualAssets.Load("gen_terra.png");
        foreach (var z in Map.Zones)
        {
            if (z.Kind == "caminho" && Map.Roads.Count > 0) continue;   // estradas de verdade já cobrem; o retângulo virava "quadrado escuro"
            if (z.Kind == "caminho" && dirt is not null)
            {
                // trilha de terra batida (caminho principal)
                var pathMat = VisualKit.Mat(Colors.White, 0.95f);
                pathMat.AlbedoTexture = dirt;
                pathMat.Uv1Scale = new Vector3((float)z.W / 5f, (float)z.H / 5f, 1f);
                VisualKit.Part(Root, new PlaneMesh { Size = new Vector2((float)z.W, (float)z.H) }, pathMat, P(z.X, z.Y, 0.011f));
                continue;
            }
            if (Map.Kind == "cidade" && z.Kind == "seguro") continue;
            // mapas abertos: sem retângulo pintado no chão (feedback: "quadrado escuro" sem naturalidade) — a área se
            // anuncia pelo nome na tela; a tinta fica só nas dungeons
            if (!Map.Instanced) continue;
            var color = z.Kind switch
            {
                "seguro" or "refugio" => new Color(0.55f, 0.42f, 0.25f, Map.Kind == "cidade" ? 0.12f : 0.30f),
                "fraco" => new Color(0.32f, 0.40f, 0.25f, 0.28f),
                "medio" => new Color(0.50f, 0.42f, 0.20f, 0.26f),
                "perigoso" => new Color(0.38f, 0.12f, 0.30f, 0.32f),
                "forte" => new Color(0.45f, 0.18f, 0.12f, 0.28f),
                "chefe" => new Color(0.08f, 0.05f, 0.05f, 0.50f),
                "segredo" => new Color(0.35f, 0.25f, 0.55f, 0.35f),
                _ => new Color(0.4f, 0.35f, 0.3f, 0.2f),
            };
            var layer = z.Kind switch { "seguro" => 0.012f, "fraco" => 0.014f, "forte" => 0.016f, "chefe" => 0.018f, _ => 0.02f };
            color.A *= 0.55f; // tinta discreta: o terreno aparece, a área ainda se lê
            // tinta iluminada (não brilha sozinha no escuro da dungeon nem sob a névoa)
            var tint = new StandardMaterial3D { AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1f };
            VisualKit.Part(Root, new PlaneMesh { Size = new Vector2((float)z.W, (float)z.H) }, tint, P(z.X, z.Y, layer));
        }
    }

    /// <summary>
    /// 11/11 noite (pedido, referência do "céu" de MU): chão de nuvens — névoa branco-azulada que corre devagar, meio
    /// transparente sobre um vazio azul lá embaixo, e colunas de luz rosada e branca descendo do alto.
    /// </summary>
    /// <summary>14/11: o Coração dos Reinos — uma ilha de pedra lilás flutuando sobre o abismo violeta, com ilhotas em volta.</summary>
    private void IlhaDosReinos()
    {
        var abismo = VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(2400f, 2400f) }, VisualKit.Unshaded(new Color(0.10f, 0.05f, 0.17f)), new Vector3(0, -45f, 0));
        abismo.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var pedra = TexMat("chao_pedra.jpg", new Color(0.68f, 0.60f, 0.78f), 0.12f);
        var rocha = VisualKit.Mat(new Color(0.22f, 0.17f, 0.27f), 0.95f);
        var raio = (float)Map.HalfSize * 1.48f;
        VisualKit.Part(Root, new CylinderMesh { TopRadius = raio, BottomRadius = raio * 0.92f, Height = 1.2f, RadialSegments = 48 }, pedra, new Vector3(0, -0.6f, 0));
        VisualKit.Part(Root, new CylinderMesh { TopRadius = raio * 0.92f, BottomRadius = raio * 0.25f, Height = 22f, RadialSegments = 32 }, rocha, new Vector3(0, -12.2f, 0));
        var rng = new Random(77);
        for (var i = 0; i < 9; i++)
        {
            var a = i * Mathf.Tau / 9f + (float)rng.NextDouble() * 0.4f;
            var d = raio + 25f + (float)rng.NextDouble() * 60f;
            var r = 4f + (float)rng.NextDouble() * 9f;
            var pos = new Vector3(Mathf.Cos(a) * d, -6f + (float)rng.NextDouble() * 14f, Mathf.Sin(a) * d);
            VisualKit.Part(Root, new CylinderMesh { TopRadius = r, BottomRadius = r * 0.2f, Height = r * 1.6f, RadialSegments = 12 }, rocha, pos);
            VisualKit.Part(Root, new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.4f, RadialSegments = 12 }, pedra, pos + new Vector3(0, r * 0.8f, 0));
        }
        // poeira lilás subindo do abismo
        Root.AddChild(new CpuParticles3D
        {
            Amount = 120, Lifetime = 9f, Preprocess = 9f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(raio, 2f, raio),
            Position = new Vector3(0, 1f, 0), Direction = Vector3.Up, Spread = 30f, Gravity = Vector3.Zero, InitialVelocityMin = 0.1f, InitialVelocityMax = 0.35f,
            Mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 4, Rings = 2, Material = VisualKit.Glow(new Color(0.8f, 0.6f, 1f), 3f) },
        });
        var body = new StaticBody3D { CollisionLayer = ThirdPersonCamera.FloorLayer };
        body.AddChild(new CollisionShape3D { Shape = new WorldBoundaryShape3D() });
        Root.AddChild(body);
    }

    // ------------------------------------------------------------------------------------------------------------------
    // 15/11 noite (pendência "Caminho da Serpente de verdade: corpo da serpente sobre nuvens alaranjadas, só com asas"): a
    // trilha da chegada ao covil é o DORSO de uma serpente colossal que serpenteia sobre o mar de nuvens — o topo do corpo
    // fica rente ao chão (anda-se em cima dela); a cauda some nas nuvens atrás da chegada, a cabeça ergue-se além do covil e
    // duas voltas do corpo emergem e mergulham nas nuvens dos lados. Só visual: as regras do mapa continuam as mesmas.
    private static Shader? _escamas;
    private const string EscamasShader = @"
shader_type spatial;
render_mode cull_disabled;
uniform vec3 dorso = vec3(0.12, 0.32, 0.30); uniform vec3 ventre = vec3(0.86, 0.72, 0.42); uniform vec3 brilho = vec3(1.0, 0.55, 0.2);
varying float lado;
void vertex() { lado = UV.y; }
void fragment() {
  vec2 e = vec2(UV.x * 140.0, UV.y * 18.0);
  e.x += step(1.0, mod(floor(e.y), 2.0)) * 0.5;
  vec2 f = fract(e) - 0.5;
  float esc = smoothstep(0.5, 0.18, length(f * vec2(1.0, 1.25)));
  float barriga = smoothstep(0.32, 0.2, abs(lado - 0.75));          // faixa da barriga (lado de baixo)
  float faixa = smoothstep(0.06, 0.0, abs(lado - 0.25)) * 0.8;      // listra dourada no meio do dorso
  vec3 c = mix(dorso * (0.55 + 0.45 * esc), ventre * (0.75 + 0.25 * esc), barriga);
  c = mix(c, vec3(0.95, 0.72, 0.28), faixa);
  ALBEDO = c;
  ROUGHNESS = mix(0.35, 0.7, barriga);
  METALLIC = 0.25 * (1.0 - barriga);
  RIM = 0.35; RIM_TINT = 0.6;
  EMISSION = brilho * faixa * 0.25;
}";

    private void Serpente()
    {
        // eixo do corpo (x, z do mundo = x, y do mapa) e altura do centro: rente ao chão sob a trilha, mergulhando nas pontas
        var pts = new List<Vector3>();
        for (var i = 0; i <= 64; i++)
        {
            var t = i / 64f;
            var y = Mathf.Lerp(78f, -66f, t);                                   // de trás da chegada até além do covil
            // sob a trilha (x = 0) ela só ondula de leve — quem anda pela trilha está sempre em cima do dorso; nas pontas, fora
            // do caminho, serpenteia forte
            var meio = Mathf.SmoothStep(0.08f, 0.2f, t) * Mathf.SmoothStep(0.86f, 0.74f, t);
            var x = Mathf.Lerp(9f, 2.2f, meio) * Mathf.Sin(t * Mathf.Tau * 2.2f);
            var alt = t < 0.13f ? Mathf.Lerp(-9f, -2.9f, t / 0.13f)            // cauda saindo das nuvens
                : t > 0.86f ? Mathf.Lerp(-2.9f, 5.5f, (t - 0.86f) / 0.14f)     // pescoço subindo
                : -2.9f;
            pts.Add(new Vector3(x, alt, y));
        }
        float RaioEm(float t) => t < 0.13f ? Mathf.Lerp(0.6f, 3.2f, Mathf.SmoothStep(0f, 1f, t / 0.13f)) : t > 0.86f ? Mathf.Lerp(3.2f, 2.5f, (t - 0.86f) / 0.14f) : 3.2f;
        _escamas ??= new Shader { Code = EscamasShader };
        var mat = new ShaderMaterial { Shader = _escamas };
        Root.AddChild(Tubo(pts, RaioEm, mat));
        // duas voltas do corpo aparecendo dos lados (arcos que saem e entram nas nuvens)
        foreach (var (cx, cz, r, giro) in new[] { (-44f, 6f, 13f, 0.3f), (46f, -20f, 11f, -0.5f) })
        {
            var arco = new List<Vector3>();
            for (var i = 0; i <= 28; i++)
            {
                var a = Mathf.Pi * i / 28f;
                arco.Add(new Vector3(cx + Mathf.Cos(a + giro) * r * 0.35f, Mathf.Sin(a) * r * 0.55f - 3.5f, cz + Mathf.Cos(a) * r));
            }
            Root.AddChild(Tubo(arco, _ => 2.6f, mat));
        }
        // cabeça além do covil: focinho, olhos acesos, chifres e a mandíbula aberta virada para a trilha
        var fim = pts[^1]; var antes = pts[^4];
        var cabeca = new Node3D { Position = fim };
        Root.AddChild(cabeca);
        cabeca.LookAt(fim + (fim - antes).Normalized() * 5f + new Vector3(0, -1.5f, 0), Vector3.Up);
        cabeca.RotateObjectLocal(Vector3.Up, Mathf.Pi);   // LookAt aponta -Z; o focinho é desenhado em +Z
        var pele = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.3f, 0.28f), Roughness = 0.4f, Metallic = 0.25f, RimEnabled = true, Rim = 0.3f };
        var osso = VisualKit.Mat(new Color(0.9f, 0.84f, 0.7f), 0.5f);
        VisualKit.Part(cabeca, VisualKit.Sphere(2.9f), pele, Vector3.Zero, null, new Vector3(1.05f, 0.82f, 1.25f));
        VisualKit.Part(cabeca, VisualKit.Sphere(2.2f), pele, new Vector3(0, -0.2f, 3.2f), null, new Vector3(0.9f, 0.55f, 1.25f));   // focinho
        var queixo = VisualKit.Part(cabeca, VisualKit.Sphere(1.9f), new StandardMaterial3D { AlbedoColor = new Color(0.82f, 0.68f, 0.4f), Roughness = 0.6f }, new Vector3(0, -1.9f, 2.6f), new Vector3(24, 0, 0), new Vector3(0.85f, 0.3f, 1.25f));
        foreach (var sd in new[] { -1f, 1f })
        {
            VisualKit.Part(cabeca, VisualKit.Sphere(0.42f), VisualKit.Glow(new Color(1f, 0.62f, 0.12f), 4f), new Vector3(sd * 1.55f, 0.9f, 2.0f), null, new Vector3(1f, 0.6f, 1f));
            var chifre = VisualKit.Part(cabeca, VisualKit.Cylinder(0.02f, 0.45f, 3.6f, 10), osso, new Vector3(sd * 1.3f, 2.2f, -0.8f), new Vector3(-55, 0, sd * -18));
            for (var k = 0; k < 4; k++)   // presas
                VisualKit.Part(cabeca, VisualKit.Cylinder(0.01f, 0.16f, 0.9f, 6), osso, new Vector3(sd * (0.4f + k * 0.35f), -0.85f, 4.4f - k * 0.45f), new Vector3(180, 0, 0));
        }
        cabeca.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.2f), LightEnergy = 2.5f, OmniRange = 14f, Position = new Vector3(0, 1f, 3f) });
        // barbatanas ao longo do dorso, fora da faixa onde se anda
        var fin = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.45f, 0.18f), Roughness = 0.5f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        for (var i = 2; i < pts.Count - 3; i += 2)
        {
            var d = (pts[i + 1] - pts[i - 1]).Normalized();
            var lado = d.Cross(Vector3.Up).Normalized();
            foreach (var sd in new[] { -1f, 1f })
            {
                var p0 = pts[i] + lado * sd * 2.9f + Vector3.Up * 1.6f;
                var f = VisualKit.Part(Root, new PrismMesh { Size = new Vector3(1.4f, 1.1f, 0.06f) }, fin, p0);
                f.LookAt(p0 + d, Vector3.Up);
                f.RotateObjectLocal(Vector3.Forward, sd * 0.9f);
                f.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
    }

    /// <summary>Tubo liso ao longo dos pontos (Catmull-Rom), raio por posição (0..1). UV.x = ao longo, UV.y = volta.</summary>
    private static MeshInstance3D Tubo(List<Vector3> pts, Func<float, float> raio, Material mat)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int Voltas = 28, Sub = 4;
        var n = (pts.Count - 1) * Sub;
        Vector3 P(int i) => pts[Math.Clamp(i, 0, pts.Count - 1)];
        Vector3 Ponto(float u)
        {
            var f = u * (pts.Count - 1); var i = Math.Min((int)f, pts.Count - 2); var t = f - i;
            Vector3 p0 = P(i - 1), p1 = P(i), p2 = P(i + 1), p3 = P(i + 2);
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
        }
        var aneis = new List<Vector3[]>(); var normais = new List<Vector3[]>();
        for (var k = 0; k <= n; k++)
        {
            var u = k / (float)n;
            var c = Ponto(u);
            var d = (Ponto(Math.Min(1f, u + 0.002f)) - Ponto(Math.Max(0f, u - 0.002f))).Normalized();
            var lado = d.Cross(Vector3.Up).Normalized(); if (lado.LengthSquared() < 0.01f) lado = Vector3.Right;
            var cima = lado.Cross(d).Normalized();
            var r = raio(u);
            var anel = new Vector3[Voltas + 1]; var nn = new Vector3[Voltas + 1];
            for (var j = 0; j <= Voltas; j++)
            {
                var a = j / (float)Voltas * Mathf.Tau;
                var dir = cima * Mathf.Cos(a) + lado * Mathf.Sin(a);   // j = 0: topo do dorso
                anel[j] = c + dir * r * (1f - 0.12f * Mathf.Pow(Mathf.Sin(a), 2));   // um pouco achatado dos lados
                nn[j] = dir;
            }
            aneis.Add(anel); normais.Add(nn);
        }
        for (var k = 0; k < n; k++)
            for (var j = 0; j < Voltas; j++)
            {
                void V(int kk, int jj) { st.SetNormal(normais[kk][jj]); st.SetUV(new Vector2(kk / (float)n, jj / (float)Voltas)); st.AddVertex(aneis[kk][jj]); }
                V(k, j); V(k + 1, j); V(k + 1, j + 1);
                V(k, j); V(k + 1, j + 1); V(k, j + 1);
            }
        var mi = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        return mi;
    }

    private void AddCloudFloor()
    {
        var deep = VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(2400f, 2400f) }, VisualKit.Unshaded(new Color(0.32f, 0.42f, 0.6f)), new Vector3(0, -30f, 0));
        deep.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var clouds = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_opaque;
uniform float camada = 0.0;
uniform vec3 azul = vec3(0.50, 0.60, 0.78); uniform vec3 branco = vec3(0.93, 0.95, 1.0);
varying vec3 wp;
float h(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float n(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(h(i), h(i + vec2(1, 0)), u.x), mix(h(i + vec2(0, 1)), h(i + vec2(1, 1)), u.x), u.y); }
float fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int k = 0; k < 5; k++) { v += a * n(p); p *= 2.03; a *= 0.5; } return v; }
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
  vec2 p = wp.xz * 0.045 + vec2(TIME * 0.012, TIME * 0.006) + camada * 7.0;
  float c = fbm(p) * 0.75 + fbm(p * 2.7 - TIME * 0.02) * 0.35;
  ALBEDO = mix(azul, branco, smoothstep(0.35, 0.85, c));
  ALPHA = camada > 0.5 ? smoothstep(0.45, 0.8, c) * 0.55 : 0.82 + 0.15 * c;
}" } };
        if (Map.Id == "caminho_serpente")   // 15/11 noite: mar de nuvens alaranjadas do pôr do sol
        {
            clouds.SetShaderParameter("azul", new Vector3(0.86f, 0.42f, 0.22f));
            clouds.SetShaderParameter("branco", new Vector3(1f, 0.82f, 0.55f));
            if (deep.MaterialOverride is StandardMaterial3D dm) dm.AlbedoColor = new Color(0.55f, 0.24f, 0.18f);
        }
        var floor = VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(2400f, 2400f) }, clouds, new Vector3(0, -0.02f, 0));
        floor.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var veil = (ShaderMaterial)clouds.Duplicate();
        veil.SetShaderParameter("camada", 1f);
        var top = VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(400f, 400f) }, veil, new Vector3(0, 0.25f, 0));
        top.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        // chão de verdade (colisão da câmera): igual aos outros mapas
        var body = new StaticBody3D { CollisionLayer = ThirdPersonCamera.FloorLayer };
        body.AddChild(new CollisionShape3D { Shape = new WorldBoundaryShape3D() });
        Root.AddChild(body);
        // colunas de luz
        var rng = new RandomNumberGenerator { Seed = 77 };
        for (var i = 0; i < 7; i++)
        {
            var pink = i % 2 == 0;
            var col = VisualKit.Unshaded(pink ? new Color(1f, 0.6f, 0.68f, 0.18f) : new Color(1f, 1f, 1f, 0.16f), true);
            var a = i / 7f * Mathf.Tau + rng.RandfRange(-0.3f, 0.3f);
            var r = (float)Map.HalfSize * rng.RandfRange(0.55f, 0.95f);
            var beam = VisualKit.Part(Root, VisualKit.Cylinder(rng.RandfRange(1.2f, 2.2f), rng.RandfRange(1.5f, 2.6f), 60f, 16), col,
                new Vector3(Mathf.Cos(a) * r, 25f, Mathf.Sin(a) * r), new Vector3(rng.RandfRange(-8, 8), 0, rng.RandfRange(-8, 8)));
            beam.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    private void AddSafeCircle()
    {
        if (Map.SafeRadius > 20) return; // cidade inteira é segura: sem anel
        // 13/11 ("faz o mesmo desenho do círculo do boneco nas zonas seguras dos TPs, levemente transparente nas bordas, girando"):
        // o anel amarelo virou o círculo de runas dourado, grande e suave
        var runas = new ShaderMaterial { Shader = new Shader { Code = Visuals.MannequinVisual.RunasShader }, RenderPriority = 3 };
        runas.SetShaderParameter("cor", new Vector3(1f, 0.78f, 0.36f));
        runas.SetShaderParameter("forca", 0.42f);
        runas.SetShaderParameter("suave", 1f);
        var d = (float)Map.SafeRadius * 2f;
        var ring = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(d, d) }, MaterialOverride = runas, Position = P(Map.SpawnX, Map.SpawnY, 0.05f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Name = "ZonaSegura" };
        Root.AddChild(ring);
    }

    // ------------------------------------------------------------ obstáculos

    internal void Collider(Vector3 position, Shape3D shape)
    {
        var body = new StaticBody3D { Position = position };
        body.AddChild(new CollisionShape3D { Shape = shape });
        Root.AddChild(body);
    }

    internal static StandardMaterial3D TexMat(string texture, Color tint, float scale)
    {
        var m = VisualKit.Mat(tint, 0.92f);
        if (VisualAssets.Load(texture) is { } tex)
        {
            m.AlbedoTexture = tex;
            m.Uv1Triplanar = true;
            m.Uv1Scale = new Vector3(scale, scale, scale);
            VisualAssets.AddRelief(m, texture);
        }
        return m;
    }

    internal StandardMaterial3D StoneMat(float tone, bool wallTexture)
    {
        var m = VisualKit.Mat(new Color(tone, tone * 0.97f, tone * 0.93f), 0.92f);
        if (wallTexture && VisualAssets.Load("gen_bloco.png") is { } tex)
        {
            m.AlbedoTexture = tex;
            m.AlbedoColor = new Color(tone * 1.6f, tone * 1.55f, tone * 1.6f);
            m.Uv1Triplanar = true;
            m.Uv1Scale = new Vector3(0.28f, 0.28f, 0.28f);
            VisualAssets.AddRelief(m, "gen_bloco.png");
        }
        return m;
    }

    /// <summary>13/11: obstáculo com giro ("rot"): desenha normal e pendura o que foi criado num pivô girado no centro dele.</summary>
    private void AddObstacleGirado(MapObstacle o)
    {
        if (Math.Abs(o.Rot) < 0.01) { AddObstacle(o); return; }
        var antes = Root.GetChildCount();
        AddObstacle(o);
        var pivo = new Node3D { Name = "Girado", Position = new Vector3((float)o.X, 0, (float)o.Y), RotationDegrees = new Vector3(0, (float)o.Rot, 0) };
        var novos = new List<Node>();
        for (var i = antes; i < Root.GetChildCount(); i++) novos.Add(Root.GetChild(i));
        Root.AddChild(pivo);
        var desloca = new Transform3D(Basis.Identity, -pivo.Position);   // só tira a posição: o pivô aplica o giro
        foreach (var n in novos)
        {
            if (n is Node3D n3)
            {
                var t = n3.Transform;
                Root.RemoveChild(n3);
                pivo.AddChild(n3);
                n3.Transform = desloca * t;
            }
        }
    }

    private void AddObstacle(MapObstacle o)
    {
        var h = (float)o.Height;
        // 12/11: muro/muralha/ruína/barricada/coluna de calcário com modelo (pacote Vila); sem o .glb, o desenho antigo
        if (!Map.Instanced && Map.Theme is "praia" or "cidade" or "vale" && Map.Id != "casa_jogador" && KitTapa.Obstaculo(this, Root, P(o.X, o.Y), o.Kind, (float)o.W, (float)o.H, h, (float)o.R, o.Solid)) return;
        if (Map.Theme == "biblioteca" && BibliotecaPeca(o)) return;
        switch (o.Kind)
        {
            case "muralha":
            case "muro":
            case "muralha_ilusoria":
            {
                var w = (float)o.W;
                var d = (float)o.H;
                var illusion = o.Kind == "muralha_ilusoria";
                // 16/11 ("mesmo padrão"): na gruta/mina a parede era muralha de castelo (tijolo com ameias); agora é rocha bruta
                var gruta = Map.Theme == "gruta";
                var mat = gruta ? TexMat("ter_pedra.png", new Color(0.92f, 0.86f, 0.80f), 0.16f) : StoneMat(illusion ? 0.52f : 0.48f, true);
                VisualKit.Part(Root, VisualKit.Box(w, h, d), mat, P(o.X, o.Y, h / 2f));
                var along = w >= d;
                var length = along ? w : d;
                if (gruta)
                {
                    // saliências de rocha na face e no topo (quebra a parede reta), mesma rocha
                    for (var s = -length / 2f + 0.8f; s < length / 2f - 0.4f; s += R(1.4f, 2.4f))
                    {
                        var off = along ? new Vector3(s, 0, 0) : new Vector3(0, 0, s);
                        var bh = h * R(0.35f, 0.95f);
                        foreach (var lado in new[] { -1f, 1f })
                        {
                            var face = along ? new Vector3(0, 0, lado * d * 0.5f) : new Vector3(lado * w * 0.5f, 0, 0);
                            VisualKit.Part(Root, VisualKit.Box(R(0.7f, 1.5f), bh, R(0.5f, 0.9f)), mat, P(o.X, o.Y, bh / 2f) + off + face,
                                new Vector3(R(-6, 6), R(0, 360), R(-6, 6)));
                        }
                        VisualKit.Part(Root, VisualKit.Box(R(0.8f, 1.6f), R(0.3f, 0.9f), R(0.8f, 1.4f)), mat, P(o.X, o.Y, h + 0.1f) + off,
                            new Vector3(R(-12, 12), R(0, 360), R(-12, 12)));
                    }
                    if (!illusion && o.Solid) Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3(w, h, d) });
                    break;
                }
                // ameias no topo
                for (var s = -length / 2f + 0.6f; s < length / 2f - 0.3f; s += 1.6f)
                {
                    var offset = along ? new Vector3(s, 0, 0) : new Vector3(0, 0, s);
                    VisualKit.Part(Root, VisualKit.Box(along ? 0.8f : w * 1.02f, 0.45f, along ? d * 1.02f : 0.8f), mat,
                        P(o.X, o.Y, h + 0.22f) + offset);
                }
                if (!illusion && o.Solid) Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3(w, h, d) });
                break;
            }
            case "penhasco" when (Map.Theme == "praia" || TintoRocha is not null) && CliffKit(o, h):   // 16/11: todos os temas ao ar livre
                break;
            case "penhasco":
            {
                // penhasco: degraus de rocha irregulares (caixas inclinadas), sem bolhas
                var w = (float)o.W;
                var d = (float)o.H;
                var mat = VisualKit.Mat(new Color(0.30f, 0.29f, 0.28f), 0.97f);
                var mat2 = VisualKit.Mat(new Color(0.36f, 0.34f, 0.31f), 0.97f);
                VisualKit.Part(Root, VisualKit.Box(w * 0.9f, h, d * 0.98f), mat, P(o.X, o.Y, h / 2f));
                var count = (int)(d / 1.8f) + 2;
                for (var i = 0; i < count; i++)
                {
                    var z = -d / 2f + d * (i + 0.5f) / count;
                    var bh = h * R(0.35f, 0.9f);
                    foreach (var side in new[] { -1f, 1f })
                        VisualKit.Part(Root, VisualKit.Box(R(0.6f, 1.1f), bh, d / count * R(0.8f, 1.15f)), i % 2 == 0 ? mat2 : mat,
                            P(o.X, o.Y, bh / 2f) + new Vector3(side * (w * 0.45f), 0, z), new Vector3(R(-4, 4), R(-8, 8), side * R(2, 8)));
                    var top = R(0.4f, 1.4f);
                    VisualKit.Part(Root, VisualKit.Box(w * R(0.55f, 0.9f), top, d / count * R(0.9f, 1.2f)), mat2,
                        P(o.X, o.Y, h + top / 2f - 0.3f) + new Vector3(R(-0.4f, 0.4f), 0, z), new Vector3(R(-5, 5), R(-12, 12), R(-5, 5)));
                }
                Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3(w, h, d) });
                break;
            }
            case "ruina":
            {
                var w = (float)o.W;
                var d = (float)o.H;
                var mat = StoneMat(0.55f, true);
                var along = w >= d;
                var length = along ? w : d;
                var pieces = Math.Max(2, (int)(length / 1.2f));
                for (var i = 0; i < pieces; i++)
                {
                    var s = -length / 2f + length * (i + 0.5f) / pieces;
                    var ph = h * R(0.5f, 1.25f);
                    VisualKit.Part(Root, VisualKit.Box(along ? length / pieces * 0.98f : w, ph, along ? d : length / pieces * 0.98f), mat,
                        P(o.X, o.Y, ph / 2f) + (along ? new Vector3(s, 0, 0) : new Vector3(0, 0, s)));
                }
                Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3(w, h, d) });
                break;
            }
            case "rocha":
            {
                // rochedo anguloso (prancha 03): blocos inclinados sobrepostos, não esferas
                var r = (float)o.R;
                var node = new Node3D { Position = P(o.X, o.Y), RotationDegrees = new Vector3(0, R(0, 360), 0) };
                Root.AddChild(node);
                // Costa: rocha do kit (cal em camadas, PBR); outros temas: pedra lascada gerada
                if ((Map.Theme == "praia" || TintoRocha is not null) && MapKit.Glb(node, "costa/costa_rocha_" + "abc"[(int)R(0, 2.99f)], Vector3.Zero, 0f, 1f))
                {
                    node.Scale = new Vector3(r, h, r);
                    if (TintoRocha is { } tr) Tingir(node, tr);   // 16/11: rocha do kit com a cor da região
                }
                else MapKit.Boulder(this, node, r, h);
                Collider(P(o.X, o.Y, h / 2f), new CylinderShape3D { Radius = r, Height = h });
                break;
            }
            case "arvore":
            {
                var node = new Node3D { Position = P(o.X, o.Y), RotationDegrees = new Vector3(0, R(0, 360), 0) };
                Root.AddChild(node);
                // 13/11: árvore do acervo da região (queimada no Vale, mangue no Brejo, árvore de neve na Geada)
                if (KitAcervo.Peca(this, node, "arvore")) { Collider(P(o.X, o.Y, 2f), new CylinderShape3D { Radius = 0.5f, Height = 4f }); break; }
                // regra: tudo com textura — casca de árvore com a textura de madeira gerada
                var bark = TexMat("gen_madeira.png", new Color(0.45f, 0.34f, 0.25f), 0.8f);
                // 17/11 (análise, acabamento marcado pelo dono: "câmera que não entra em tronco"): toda árvore tem o tronco
                // como colisor da câmera (antes só as do acervo) — a câmera para antes e não mostra a casca por dentro
                Collider(P(o.X, o.Y, 2f), new CylinderShape3D { Radius = Mathf.Clamp((float)o.R * 0.6f, 0.3f, 0.6f), Height = 4f });
                // cada região tem a sua árvore: copa redonda no Vale/Mata/Acrópole, pinheiro na Costa e na Geada,
                // árvore seca no Brejo, em Véspera e nos Cânions
                var pick = R(0, 1);
                switch (Map.Theme)
                {
                    // mais tipos de árvore por lugar (pendência): bétula no Vale/Geada, salgueiro no Brejo, acácia nos Cânions,
                    // cipreste na Acrópole e em Véspera
                    case "vale" or "neve" when pick > 0.75f:
                        // // MapKit.Birch(this, node, Math.Max(h, 5f));
                        break;
                    case "brejo" when pick < 0.45f:
                        // // MapKit.Willow(this, node, Math.Max(h, 5f), bark);
                        break;
                    case "canion" when pick < 0.4f:
                        // // MapKit.Acacia(this, node, Math.Max(h, 4.5f), bark);
                        break;
                    case "acropole" or "fantasma" when pick < 0.4f:
                        // // MapKit.Cypress(this, node, Math.Max(h, 6f) * 1.2f, bark);
                        break;
                    case "vale" or "acropole" or "mata" when Map.Theme != "mata" || pick < 0.55f:
                        MapKit.BroadTree(this, node, Math.Max(h, 5f), bark, Map.Theme == "mata");
                        break;
                    case "brejo" or "fantasma" or "canion" when pick < 0.8f:
                        MapKit.TryBuild(this, node, "arvore_seca", bark);
                        break;
                    // Costa: kit novo — coqueiros, pinheiros-mansos e arbustos com textura (lote 3)
                    case "praia" when CoastTree(node, pick, h):
                        break;
                    // Porto: pinheiro-manso com textura (kit da Costa) no lugar da árvore lisa
                    case "cidade" when CoastTree(node, 0.5f + pick * 0.3f, h):
                        break;
                    // Costa (praia): coqueiros na maior parte, alguns pinheiros-de-dunas e arbustos secos
                    case "praia" when pick < 0.72f:
                        MapKit.PalmTree(this, node, Math.Max(h, 5f) * R(0.95f, 1.25f));
                        break;
                    case "praia" when pick < 0.85f:
                        MapKit.DryBush(this, node, 1.4f);
                        break;
                    default:
                        MapKit.PineTree(this, node, Vector3.Zero, Math.Max(h, 5f), bark);
                        if (Map.Theme == "neve") MapKit.SnowCap(this, node, Math.Max(h, 5f));
                        break;
                }
                Collider(P(o.X, o.Y, h / 2f), new CylinderShape3D { Radius = (float)o.R, Height = h });
                // balança no vento (o tronco dobra na base; coqueiro e árvore alta mexem mais)
                foreach (var child in node.GetChildren())
                    if (child is Node3D t) _swaying.Add((t, t.RotationDegrees, R(0, 6.28f), Map.Theme == "praia" ? R(1.6f, 2.6f) : R(0.8f, 1.6f)));
                break;
            }
            case "cerca":
            {
                // cerca de pasto: mourões a cada ~1,6 m e duas travessas ao longo do lado comprido
                var along = o.W >= o.H;
                var length = (float)(along ? o.W : o.H);
                var wood = VisualKit.Mat(new Color(0.36f, 0.25f, 0.15f), 0.95f);
                var posts = Math.Max(2, (int)Mathf.Ceil(length / 1.6f) + 1);
                for (var i = 0; i < posts; i++)
                {
                    var t = -length / 2f + length * i / (posts - 1);
                    var at = P(o.X + (along ? t : 0), o.Y + (along ? 0 : t), h * 0.5f);
                    VisualKit.Part(Root, VisualKit.Box(0.13f, h, 0.13f), wood, at, new Vector3(R(-3, 3), R(0, 20), R(-3, 3)));
                }
                foreach (var y in new[] { h * 0.4f, h * 0.8f })
                    VisualKit.Part(Root, VisualKit.Box(along ? length : 0.06f, 0.08f, along ? 0.06f : length), wood, P(o.X, o.Y, y));
                Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3((float)o.W, h, (float)o.H) });
                break;
            }
            case "coluna":
            case "estatua":
            {
                var r = (float)o.R;
                var mat = StoneMat(0.62f, false);
                if (o.Kind == "coluna")
                {
                    VisualKit.Part(Root, VisualKit.Box(r * 2.4f, 0.3f, r * 2.4f), mat, P(o.X, o.Y, 0.15f));
                    VisualKit.Part(Root, VisualKit.Cylinder(r * 0.8f, r, h - 0.6f, 12), mat, P(o.X, o.Y, 0.3f + (h - 0.6f) / 2f));
                    VisualKit.Part(Root, VisualKit.Box(r * 2.2f, 0.3f, r * 2.2f), mat, P(o.X, o.Y, h - 0.15f));
                }
                else
                {
                    VisualKit.Part(Root, VisualKit.Box(r * 2f, 1.0f, r * 2f), mat, P(o.X, o.Y, 0.5f));
                    var statue = new HumanoidVisual(new HumanoidStyle { Skin = new Color(0.58f, 0.57f, 0.55f), Cloth = new Color(0.5f, 0.5f, 0.48f), Accent = new Color(0.5f, 0.5f, 0.48f), Scale = 1.3f, FixedWeapon = "sword_p1_placeholder" });
                    statue.Root.Position = P(o.X, o.Y, 1.0f);
                    statue.Root.RotationDegrees = new Vector3(0, 180, 0);
                    Root.AddChild(statue.Root);
                    statue.Update(new VisualFrame { Alive = true, Action = "windup:skill_guerreiro_golpe_de_cunha", ActionT = 1f }, 10f);
                }
                Collider(P(o.X, o.Y, h / 2f), new CylinderShape3D { Radius = r, Height = h });
                break;
            }
            case "casa":
            {
                // casa de enxaimel (prancha 03): térreo de pedra, andar de cima de reboco com vigas e balanço,
                // telhado de duas águas com empena na frente, chaminé, janelas âmbar e toldo bordô. Frente virada para o centro.
                var node = new Node3D { Position = P(o.X, o.Y) };
                Root.AddChild(node);
                var faceX = Math.Abs(o.X) > Math.Abs(o.Y) * 1.2;
                var yaw = faceX ? (o.X > 0 ? -90f : 90f) : (o.Y > 0 ? 180f : 0f);
                node.RotationDegrees = new Vector3(0, yaw, 0);
                var W = (float)(faceX ? o.H : o.W); // largura da fachada
                var D = (float)(faceX ? o.W : o.H); // profundidade
                BuildHouse(node, W, D, h, (int)(Math.Abs(o.X * 7 + o.Y * 3)) % 3);
                Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3((float)o.W, h, (float)o.H) });
                break;
            }
            case "barricada":
            {
                var w = (float)o.W;
                var d = (float)o.H;
                var wood = VisualKit.Mat(new Color(0.36f, 0.25f, 0.15f), 0.9f);
                var along = w >= d;
                var length = along ? w : d;
                for (var s2 = -length / 2f + 0.3f; s2 <= length / 2f - 0.2f; s2 += 0.55f)
                {
                    var offset = along ? new Vector3(s2, 0, 0) : new Vector3(0, 0, s2);
                    VisualKit.Part(Root, VisualKit.Cylinder(0f, 0.13f, h * R(0.9f, 1.2f), 6), wood, P(o.X, o.Y, h / 2f) + offset,
                        new Vector3(R(-12, 12), 0, R(-12, 12)));
                }
                VisualKit.Part(Root, VisualKit.Box(along ? length : 0.18f, 0.18f, along ? 0.18f : length), wood, P(o.X, o.Y, h * 0.45f));
                Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3(w, h, d) });
                break;
            }
            case "farol" when Visuals.GlbCache.Instance("cenario/cenario_farol") is { } lighthouse:
            {
                // farol do lote 3 (19,5 m, lanterna acesa) no mesmo lugar do obstáculo do servidor (círculo r 3)
                lighthouse.Position = P(o.X, o.Y);
                lighthouse.RotationDegrees = new Vector3(0, R(0, 360), 0);
                Root.AddChild(lighthouse);
                // a lanterna (vidro aceso) fica no eixo do modelo, entre 15,2 e 17,1 m: luz e facho saem do meio dela
                Root.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.85f, 0.5f), LightEnergy = 3f, OmniRange = 34f, Position = P(o.X, o.Y, 16.15f) });
                _vida.FeixeDoFarol(P(o.X, o.Y, 16.15f));
                Collider(P(o.X, o.Y, h / 2f), new CylinderShape3D { Radius = (float)o.R, Height = h });
                break;
            }
            case "ossada_gigante" when Visuals.GlbCache.Instance("costa/marco_ossada_gigante") is { } bones:
                // esqueleto de ~37 m meio enterrado na Enseada do Tirano (Cal Lote 2): passa-se por baixo das costelas
                // 17/11: o obstáculo agora é só o crânio (círculo); o desenho fica no centro de antes (80, -89,5)
                bones.Position = o.R > 0 ? P(80.0, -89.5) : P(o.X, o.Y);
                Root.AddChild(bones);
                if (o.R > 0) Collider(P(o.X, o.Y, h / 2f), new CylinderShape3D { Radius = (float)o.R, Height = h });
                else Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3((float)o.W, h, (float)o.H) });
                break;
            case "lago":
            {
                // lagoa do kit da Costa (água, margem molhada, juncos); não se atravessa
                var lake = new Node3D { Position = P(o.X, o.Y), RotationDegrees = new Vector3(0, R(0, 360), 0) };
                Root.AddChild(lake);
                if (!MapKit.Glb(lake, "costa/costa_lago", Vector3.Zero, 0f, (float)o.R / 5.5f))
                    VisualKit.Part(Root, VisualKit.Cylinder((float)o.R, (float)o.R, 0.05f, 32), VisualKit.Mat(new Color(0.2f, 0.35f, 0.4f), 0.1f), P(o.X, o.Y, 0.03f));
                Collider(P(o.X, o.Y, 0.5f), new CylinderShape3D { Radius = (float)o.R, Height = 1f });
                break;
            }
            case "farol":
            {
                // marco visual: avistado de longe (curiosidade), com luz no topo
                var r = (float)o.R;
                var stone = StoneMat(0.72f, true);
                var band = VisualKit.Mat(new Color(0.55f, 0.18f, 0.14f), 0.85f);
                VisualKit.Part(Root, VisualKit.Cylinder(r * 0.65f, r, h, 14), stone, P(o.X, o.Y, h / 2f));
                for (var i = 1; i <= 3; i++)
                    VisualKit.Part(Root, VisualKit.Cylinder(r * (1f - 0.11f * i) + 0.05f, r * (1f - 0.11f * i) + 0.05f, 0.9f, 14), band, P(o.X, o.Y, h * i / 4f));
                VisualKit.Part(Root, VisualKit.Cylinder(r * 0.75f, r * 0.75f, 0.4f, 14), StoneMat(0.4f, false), P(o.X, o.Y, h + 0.2f));
                VisualKit.Part(Root, VisualKit.Sphere(r * 0.45f), VisualKit.Glow(new Color(1f, 0.85f, 0.45f), 5f), P(o.X, o.Y, h + 1.0f));
                var beacon = new OmniLight3D { LightColor = new Color(1f, 0.85f, 0.5f), LightEnergy = 3f, OmniRange = 30f, Position = P(o.X, o.Y, h + 1.2f) };
                Root.AddChild(beacon);
                Collider(P(o.X, o.Y, h / 2f), new CylinderShape3D { Radius = r, Height = h });
                break;
            }
            case "invisivel":
                // bloqueio do servidor para peças do kit (torres do portão, forja, banca): o visual vem do prop
                break;
            case "ravina" when Map.Theme == "praia" && RavineKit(o):
                break;
            case "ravina":
            {
                // vão no chão: fundo escuro e bordas de pedra quebrada dos dois lados (não dá para pular)
                var w = (float)o.W;
                var d = (float)o.H;
                var along = w >= d;
                VisualKit.Part(Root, VisualKit.Box(w, 0.04f, d), VisualKit.Mat(new Color(0.03f, 0.025f, 0.02f), 1f), P(o.X, o.Y, 0.03f));
                VisualKit.Part(Root, VisualKit.Box(w * 0.96f, 0.05f, d * 0.6f), VisualKit.Mat(new Color(0f, 0f, 0f), 1f), P(o.X, o.Y, 0.05f));
                var lip = VisualKit.Mat(new Color(0.42f, 0.38f, 0.32f), 0.97f);
                var length = along ? w : d;
                foreach (var side in new[] { -1f, 1f })
                    for (var t = -length / 2f + 0.5f; t < length / 2f; t += R(0.8f, 1.5f))
                    {
                        var off = along ? new Vector3(t, 0, side * (d / 2f + R(-0.1f, 0.15f))) : new Vector3(side * (w / 2f + R(-0.1f, 0.15f)), 0, t);
                        VisualKit.Part(Root, VisualKit.Box(R(0.5f, 1.1f), R(0.15f, 0.4f), R(0.3f, 0.6f)), lip, P(o.X, o.Y, 0.1f) + off,
                            new Vector3(R(-8, 8), R(0, 90), R(-8, 8)));
                    }
                break;
            }
            default:
            {
                // peças do kit (árvore seca, colunas, nichos, rochas de lava...): visual do MapKit, colisão do servidor
                var kitNode = new Node3D { Position = P(o.X, o.Y), RotationDegrees = new Vector3(0, R(0, 360), 0) };
                Root.AddChild(kitNode);
                if (MapKit.TryBuild(this, kitNode, o.Kind, VisualKit.Mat(new Color(0.33f, 0.22f, 0.13f), 0.9f))) break;
                kitNode.QueueFree();
                var mat = VisualKit.Mat(new Color(0.4f, 0.4f, 0.4f));
                if (o.Shape == "circle")
                    VisualKit.Part(Root, VisualKit.Cylinder((float)o.R, (float)o.R, h), mat, P(o.X, o.Y, h / 2f));
                else
                    VisualKit.Part(Root, VisualKit.Box((float)o.W, h, (float)o.H), mat, P(o.X, o.Y, h / 2f));
                break;
            }
        }
    }

    // ------------------------------------------------------------ portão, baús e enfeites

    private void AddGate(MapGate g)
    {
        if (g.Kind == "ponte") { AddBridge(g); return; }
        if (g.Kind == "selo") { AddSeal(g); return; }
        if (g.Kind == "pedra") { AddBoulder(g); return; }
        if (g.Kind == "rachadura") { AddCrack(g); return; }
        if (g.Kind == "pedra_secreta") { AddPedraSecreta(g); return; }
        if (g.Kind is "corda" or "casa_ruina" or "porta") { AddDesafio(g); return; }
        var iron = VisualKit.Mat(new Color(0.22f, 0.22f, 0.24f), 0.5f, 0.8f);
        var wood = VisualKit.Mat(new Color(0.35f, 0.24f, 0.14f), 0.9f);
        var w = (float)g.W;
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(Root, VisualKit.Box(0.6f, 4.2f, 1.4f), StoneMat(0.45f, true), P(g.X, g.Y, 2.1f) + new Vector3(side * (w / 2f + 0.3f), 0, 0));
        VisualKit.Part(Root, VisualKit.Box(w + 1.2f, 0.5f, 1.4f), StoneMat(0.45f, true), P(g.X, g.Y, 4.0f));
        // colisor para a câmera (portão + pilares): a câmera não entra nas grades
        Collider(P(g.X, g.Y, 2.1f), new BoxShape3D { Size = new Vector3(w + 1.2f, 4.2f, 1.4f) });
        var bars = new Node3D { Position = P(g.X, g.Y, 0f) };
        Root.AddChild(bars);
        // 17/11 (dono: "na frente do portão o meu ataque não passa e os monstros não atravessam — coloca uma película verde"):
        // aberto, o vão fica com o Véu da Maré (só gente passa; ver o livro na Biblioteca do Cais)
        // (17/11, Biblioteca do Continente: "coloca a magia verde barrando as entradas" — lá o véu fica no vão FECHADO,
        // e a grade vai no comprido do portão, que é em pé no eixo y)
        var veuPortao = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(Mathf.Max(w, (float)g.H), Map.Theme == "biblioteca" ? 4.2f : 3.4f), Material = new ShaderMaterial { Shader = new Shader { Code = VeuShader }, RenderPriority = 2 } },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = P(g.X, g.Y, Map.Theme == "biblioteca" ? 2.1f : 1.7f), Visible = false,
            RotationDegrees = new Vector3(0, g.H > g.W ? 90f : 0f, 0),
        };
        veuPortao.SetMeta("nao_juntar", true);
        Root.AddChild(veuPortao);
        for (var x = -w / 2f + 0.25f; x <= w / 2f - 0.2f; x += 0.45f)
            VisualKit.Part(bars, VisualKit.Cylinder(0.05f, 0.05f, 3.6f, 6), iron, new Vector3(x, 1.8f, 0));
        for (var y = 0.6f; y < 3.6f; y += 1.0f)
            VisualKit.Part(bars, VisualKit.Box(w - 0.2f, 0.1f, 0.1f), iron, new Vector3(0, y, 0));

        var lever = new Node3D { Position = P(g.LeverX, g.LeverY, 0f) };
        Root.AddChild(lever);
        Node3D handle;
        // 16/11 (pendência u79_alavanca): alavanca de verdade do pacote (base de pedra + cabo que gira no eixo, a 0,55 m)
        if (Visuals.GlbCache.Instance("interior/puzzle_alavanca_base") is { } baseAlav && Visuals.GlbCache.Instance("interior/puzzle_alavanca_cabo") is { } cabo)
        {
            lever.AddChild(baseAlav);
            handle = VisualKit.Joint(lever, new Vector3(0, 0.55f, 0));
            cabo.RotationDegrees = new Vector3(0, 180, 0);   // o portão anima de +40 (fechado) a -40 (aberto): virado, é -35 → +35 do modelo
            handle.AddChild(cabo);
        }
        else
        {
            VisualKit.Part(lever, VisualKit.Box(0.5f, 0.6f, 0.4f), StoneMat(0.4f, false), new Vector3(0, 0.3f, 0));
            handle = VisualKit.Joint(lever, new Vector3(0, 0.6f, 0));
            VisualKit.Part(handle, VisualKit.Cylinder(0.04f, 0.04f, 0.9f, 6), wood, new Vector3(0, 0.45f, 0));
            VisualKit.Part(handle, VisualKit.Sphere(0.09f), VisualKit.Glow(new Color(1f, 0.75f, 0.3f), 1.5f), new Vector3(0, 0.92f, 0));
        }
        // 17/11 (dono: "coloca a alavanca com detalhes verdes"): na Biblioteca, a alavanca das alas é do Véu — anel de runas
        // verde na base, uma pedra verde acesa no cabo e uma luz fraca
        if (Map.Theme == "biblioteca")
        {
            var verde = new Color(0.3f, 1f, 0.55f);
            VisualKit.Part(lever, VisualKit.Ring(0.42f, 0.035f, 28), VisualKit.Glow(verde, 2.2f), new Vector3(0, 0.03f, 0));
            VisualKit.Part(lever, VisualKit.Ring(0.3f, 0.02f, 24), VisualKit.Glow(verde, 1.6f), new Vector3(0, 0.035f, 0));
            VisualKit.Part(handle, VisualKit.Sphere(0.075f), VisualKit.Glow(verde, 3f), new Vector3(0, 0.85f, 0));
            lever.AddChild(new OmniLight3D { LightColor = verde, LightEnergy = 0.7f, OmniRange = 2.4f, Position = new Vector3(0, 0.7f, 0), ShadowEnabled = false });
        }
        handle.RotationDegrees = new Vector3(40, 0, 0);
        _gates.Add(new GateView { Gate = g, Bars = bars, Lever = handle, Veu = veuPortao });
    }

    /// <summary>
    /// 10/11: trecho rachado do muro (missão da Nara): inteiro e cheio de fendas escuras até a explosão; depois vira
    /// um rombo com entulho dos dois lados — só para quem explodiu.
    /// </summary>
    private void AddCrack(MapGate g)
    {
        var w = (float)g.W;
        var h = 4.2f;
        var stone = StoneMat(0.48f, true);
        var wall = new Node3D { Position = P(g.X, g.Y) };
        Root.AddChild(wall);
        VisualKit.Part(wall, VisualKit.Box(w, h, (float)g.H), stone, new Vector3(0, h / 2f, 0));
        var crack = VisualKit.Mat(new Color(0.06f, 0.05f, 0.05f), 1f);
        for (var i = 0; i < 7; i++)
            VisualKit.Part(wall, VisualKit.Box(0.06f, R(0.6f, 1.6f), (float)g.H + 0.04f), crack,
                new Vector3(R(-w / 2f + 0.3f, w / 2f - 0.3f), R(0.8f, 3.4f), 0), new Vector3(0, 0, R(-35, 35)));
        // 17/11 (dono: "de longe tava marcando um nome, deixa sem" — pro jogo todo): sem nome flutuante; o aviso vem ao chegar perto (F)
        var rubble = new Node3D { Position = P(g.X, g.Y), Visible = false };
        Root.AddChild(rubble);
        foreach (var side in new[] { -1f, 1f })
            for (var i = 0; i < 6; i++)
                VisualKit.Part(rubble, VisualKit.Box(R(0.3f, 0.7f), R(0.2f, 0.5f), R(0.3f, 0.6f)), stone,
                    new Vector3(R(-w / 2f, w / 2f), 0.15f, side * R(0.8f, 2.2f)), new Vector3(R(-20, 20), R(0, 90), R(-20, 20)));
        foreach (var side in new[] { -1f, 1f })   // bordas quebradas do rombo
            VisualKit.Part(rubble, VisualKit.Box(0.4f, R(1.5f, 2.6f), (float)g.H), stone, new Vector3(side * (w / 2f - 0.15f), 1.0f, 0), new Vector3(0, 0, side * 8));
        _gates.Add(new GateView { Gate = g, Bars = new Node3D(), Lever = new Node3D(), Built = rubble, Broken = wall });
    }

    /// <summary>
    /// 14/11 tarde: rocha que se quebra (caverna secreta e iscas) — igual às rochas do mapa, sem placa nem destaque.
    /// Quebrada (marca do portão): vira um montinho de cascalho.
    /// </summary>
    private void AddPedraSecreta(MapGate g)
    {
        // 17/11 (dono: "as pedras com baús precisam esconder melhor os baús"): rocha da frente maior (tapa o vão)
        var r = (float)Math.Max(g.W, g.H) * 0.72f;
        // 17/11: suporte sem giro/escala (é ele que treme e some ao quebrar); a rocha e a marca ficam dentro dele
        var suporte = new Node3D { Position = P(g.X, g.Y) };
        Root.AddChild(suporte);
        var rock = new Node3D { RotationDegrees = new Vector3(0, R(0, 360), 0) };
        suporte.AddChild(rock);
        // igual às rochas do mapa (na Costa, a rocha de cal do kit) — ninguém distingue a oca das outras
        if (Map.Theme == "praia" && MapKit.Glb(rock, "costa/costa_rocha_" + "abc"[(int)R(0, 2.99f)], Vector3.Zero, 0f, 1f)) rock.Scale = new Vector3(r, 1.7f, r);
        else MapKit.Boulder(this, rock, r, 1.7f);
        // 17/11 (dono: "a pedra tem uma marca, um desenho às vezes em cima, às vezes embaixo na direita, às vezes na esquerda,
        // tipo um desenho pequeno rupestre na textura das pedras que precisam ser quebradas"): só a rocha CERTA (a da caverna)
        // ganha um petróglifo ocre pequeno, num canto que muda de rocha para rocha; as iscas ficam lisas
        var (fx, fy) = ((float)(g.LeverX - g.X), (float)(g.LeverY - g.Y));
        var fl = Mathf.Max(0.01f, Mathf.Sqrt(fx * fx + fy * fy));
        var marca = new Node3D();
        suporte.AddChild(marca);
        var cheia = g.Id.StartsWith("sala_secreta_racha", StringComparison.Ordinal);
        if (cheia)
        {
            var hh = 0; foreach (var ch in g.Id + Map.Id) hh = hh * 31 + ch;
            var lado = ((hh >> 3) & 1) == 0 ? -1f : 1f;
            var alto = ((hh >> 5) & 1) == 0;
            var nrm = new Vector3(fx / fl, 0.15f, fy / fl).Normalized();
            var dir = new Vector3(nrm.Z, 0, -nrm.X);
            var centro = P(g.X, g.Y, alto ? 1.15f : 0.45f) + new Vector3(fx / fl, 0, fy / fl) * r * 0.85f + dir * lado * r * 0.35f;
            var decal = new Decal
            {
                Size = new Vector3(0.42f, 1.4f, 0.42f), TextureAlbedo = Petroglifo(hh), Modulate = new Color(0.62f, 0.24f, 0.12f),
                AlbedoMix = 1f, UpperFade = 0.3f, LowerFade = 0.3f, CullMask = 1,
            };
            marca.AddChild(decal);
            marca.Position = centro - P(g.X, g.Y);
            decal.Basis = Basis.LookingAt(-nrm, Vector3.Up) * Basis.FromEuler(new Vector3(Mathf.Pi / 2f, 0, 0));
        }
        // a caverna de verdade: uma laje de pedra por cima do baú (de cima não se vê o que tem dentro)
        if (g.Id.StartsWith("sala_secreta_racha", StringComparison.Ordinal))
        {
            var tampa = new Node3D { Position = P(g.X, g.Y - 2.1, 1.25f), RotationDegrees = new Vector3(0, R(0, 360), 0) };
            Root.AddChild(tampa);
            if (Map.Theme == "praia" && MapKit.Glb(tampa, "costa/costa_rocha_" + "abc"[(int)R(0, 2.99f)], Vector3.Zero, 0f, 1f)) tampa.Scale = new Vector3(3.0f, 1.3f, 2.9f);
            else { MapKit.Boulder(this, tampa, 2.8f, 1.4f); }
            // 17/11: duas pedras nos cantos da frente fecham as frestas laterais (de lado não se vê mais o baú)
            foreach (var lado in new[] { -1f, 1f })
            {
                var canto = new Node3D { Position = P(g.X + lado * 1.35, g.Y - 0.6), RotationDegrees = new Vector3(0, R(0, 360), 0) };
                Root.AddChild(canto);
                if (Map.Theme == "praia" && MapKit.Glb(canto, "costa/costa_rocha_" + "abc"[(int)R(0, 2.99f)], Vector3.Zero, 0f, 1f)) canto.Scale = new Vector3(1.1f, 1.6f, 1.1f);
                else MapKit.Boulder(this, canto, 1.0f, 1.6f);
            }
        }
        var cascalho = new Node3D { Position = P(g.X, g.Y), Visible = false };
        Root.AddChild(cascalho);
        var stone = StoneMat(0.5f, true);
        for (var i = 0; i < 9; i++)
            VisualKit.Part(cascalho, VisualKit.Box(R(0.15f, 0.4f), R(0.1f, 0.25f), R(0.15f, 0.35f)), stone,
                new Vector3(R(-r, r), 0.08f, R(-r, r)), new Vector3(R(-20, 20), R(0, 90), R(-20, 20)));
        _gates.Add(new GateView { Gate = g, Bars = new Node3D(), Lever = new Node3D(), Built = cascalho, Broken = suporte });
    }

    /// <summary>17/11: golpe na rocha oca — a rocha mais perto do ponto dá uma tremidinha.</summary>
    public void TremerPedra(double x, double y)
    {
        var gv = _gates.Where(v => v.Gate.Kind == "pedra_secreta").OrderBy(v => (v.Gate.X - x) * (v.Gate.X - x) + (v.Gate.Y - y) * (v.Gate.Y - y)).FirstOrDefault();
        if (gv?.Broken is not Node3D rocha || !GodotObject.IsInstanceValid(rocha)) return;
        var casa = rocha.Position;
        var tw = rocha.CreateTween();
        for (var i = 0; i < 6; i++)
            tw.TweenProperty(rocha, "position", casa + new Vector3(R(-0.07f, 0.07f), R(0f, 0.03f), R(-0.07f, 0.07f)), 0.035);
        tw.TweenProperty(rocha, "position", casa, 0.05);
    }

    private static readonly Dictionary<int, ImageTexture> _petroglifos = new();

    /// <summary>17/11: desenho rupestre (caçador com lança, espiral ou mão) pintado por código numa textura 128×128.</summary>
    private static ImageTexture Petroglifo(int semente)
    {
        var tipo = Math.Abs(semente) % 3;
        if (_petroglifos.TryGetValue(tipo, out var pronto)) return pronto;
        const int N = 128;
        var img = Image.CreateEmpty(N, N, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        void Ponto(float x, float y, float raio)
        {
            for (var py = (int)(y - raio); py <= (int)(y + raio); py++)
                for (var px = (int)(x - raio); px <= (int)(x + raio); px++)
                    if (px >= 0 && py >= 0 && px < N && py < N && (px - x) * (px - x) + (py - y) * (py - y) <= raio * raio)
                        img.SetPixel(px, py, new Color(1, 1, 1, 0.9f));
        }
        void Linha(float x0, float y0, float x1, float y1, float esp = 4.5f)
        {
            var n = (int)Mathf.Max(2, Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)));
            for (var i = 0; i <= n; i++) Ponto(Mathf.Lerp(x0, x1, i / (float)n), Mathf.Lerp(y0, y1, i / (float)n), esp);
        }
        if (tipo == 0)   // caçador com lança
        {
            Ponto(64, 24, 9); Linha(64, 33, 64, 78); Linha(64, 45, 42, 62); Linha(64, 45, 88, 38);
            Linha(64, 78, 48, 108); Linha(64, 78, 82, 108); Linha(92, 14, 76, 112, 3f);
        }
        else if (tipo == 1)   // espiral
        {
            for (var t = 0f; t < 18.5f; t += 0.05f) Ponto(64 + Mathf.Cos(t) * t * 2.9f, 64 + Mathf.Sin(t) * t * 2.9f, 3.6f);
        }
        else   // mão
        {
            Linha(44, 110, 44, 70, 9); Linha(84, 110, 84, 70, 9); Linha(44, 110, 84, 110, 9); Ponto(64, 86, 18);
            Linha(46, 66, 38, 30); Linha(56, 62, 54, 18); Linha(68, 62, 72, 16); Linha(80, 66, 88, 26); Linha(86, 82, 108, 64);
        }
        var tex = ImageTexture.CreateFromImage(img);
        _petroglifos[tipo] = tex;
        return tex;
    }

    /// <summary>
    /// 10/11: rocha que tapa a passagem; quem tem a Força pedida empurra (F) e ela rola para o lado — só para ele.
    /// A plaquinha diz quanta Força precisa.
    /// </summary>
    private void AddBoulder(MapGate g)
    {
        var size = (float)Math.Max(g.W, g.H) * 1.15f;
        var home = P(g.X, g.Y, size * 0.42f);
        var rock = new Node3D { Position = home };
        Root.AddChild(rock);
        var mesh = new SphereMesh { Radius = size / 2f, Height = size * 0.92f, RadialSegments = 10, Rings = 6 };
        var body = VisualKit.Part(rock, mesh, StoneMat(0.42f, true), Vector3.Zero);
        body.Scale = new Vector3(1f, 0.9f, 1.08f);
        for (var i = 0; i < 5; i++)
            VisualKit.Part(rock, new SphereMesh { Radius = size * R(0.12f, 0.2f), Height = size * R(0.2f, 0.32f), RadialSegments = 7, Rings = 4 },
                StoneMat(0.36f, true), new Vector3(R(-0.35f, 0.35f), R(-0.1f, 0.35f), R(-0.35f, 0.35f)) * size);
        // 17/11 (dono: "de longe tava marcando um nome, deixa sem" — pro jogo todo): sem nome flutuante; o aviso vem ao chegar perto (F)
        // rola para o lado mais comprido da passagem (ao longo da parede)
        var along = g.H >= g.W ? new Vector3(0, 0, (float)g.H + size * 0.6f) : new Vector3((float)g.W + size * 0.6f, 0, 0);
        // e para o lado de quem empurra (não entra na parede)
        var toward = new Vector3((float)(g.LeverX - g.X), 0, (float)(g.LeverY - g.Y));
        toward = toward.LengthSquared() > 0.01f ? toward.Normalized() * size : Vector3.Zero;
        _gates.Add(new GateView { Gate = g, Bars = rock, Lever = new Node3D(), Home = home, Aside = home + along * 0.7f + toward });
    }

    /// <summary>
    /// 15/11 (mecânicas variadas espalhadas pelo mapa): paredão com argola (sobe com a Corda com Gancho — a corda aparece
    /// pendurada), casa em ruínas (vira casinha de pé depois de reconstruída) e porta trancada num arco de pedra (abre).
    /// Fechado = "Broken", aberto = "Built" (mesmo esquema da ponte).
    /// </summary>
    private void AddDesafio(MapGate g)
    {
        var raiz = new Node3D { Position = P(g.X, g.Y) };
        Root.AddChild(raiz);
        var fechado = new Node3D(); var aberto = new Node3D();
        raiz.AddChild(fechado); raiz.AddChild(aberto);
        var madeira = VisualKit.Mat(new Color(0.42f, 0.29f, 0.17f), 0.9f);
        var madeiraVelha = VisualKit.Mat(new Color(0.30f, 0.24f, 0.18f), 0.95f);
        var ferro = VisualKit.Mat(new Color(0.25f, 0.25f, 0.27f), 0.45f, 0.8f);
        var corda = VisualKit.Mat(new Color(0.62f, 0.5f, 0.3f), 0.95f);
        switch (g.Kind)
        {
            case "corda":
            {
                // rochedo alto e irregular (não coluna de tijolo): blocos de pedra empilhados e tortos
                var rocha = StoneMat(0.42f, true);
                VisualKit.Part(raiz, new SphereMesh { Radius = 2.1f, Height = 5.8f, RadialSegments = 9, Rings = 6 }, rocha, new Vector3(0, 2.6f, -0.3f), null, new Vector3(1f, 1f, 0.85f));
                for (var i = 0; i < 6; i++)
                    VisualKit.Part(raiz, new SphereMesh { Radius = R(0.8f, 1.3f), Height = R(1.4f, 2.4f), RadialSegments = 7, Rings = 4 }, StoneMat(0.36f, true),
                        new Vector3(R(-1.8f, 1.8f), R(0.4f, 4.6f), R(-1.3f, 0.6f)), new Vector3(R(-20, 20), R(0, 90), R(-20, 20)));
                VisualKit.Part(raiz, new SphereMesh { Radius = 1.5f, Height = 1.6f, RadialSegments = 8, Rings = 4 }, rocha, new Vector3(0, 5.5f, -0.2f));
                VisualKit.Part(raiz, VisualKit.Cylinder(0.16f, 0.16f, 0.06f, 10), ferro, new Vector3(0, 5.2f, 1.6f), new Vector3(90, 0, 0));
                // aberto: corda descendo da argola até o chão, com nós; e um bauzinho lá em cima
                for (var y = 0.3f; y < 5.2f; y += 0.35f)
                    VisualKit.Part(aberto, VisualKit.Cylinder(0.035f, 0.035f, 0.36f, 6), corda, new Vector3(0.02f * Mathf.Sin(y * 3f), y, 1.65f));
                for (var y = 0.8f; y < 5f; y += 0.9f)
                    VisualKit.Part(aberto, VisualKit.Sphere(0.07f), corda, new Vector3(0, y, 1.65f));
                // 18/11 ("todo baú deve sumir depois de pegar"): sem bauzinho — o prêmio já foi para a mochila
                break;
            }
            case "casa_ruina":
            {
                // fechado: paredes quebradas, viga caída e entulho
                VisualKit.Part(fechado, VisualKit.Box(4f, 1.1f, 0.3f), StoneMat(0.45f, true), new Vector3(0, 0.55f, -1.8f));
                VisualKit.Part(fechado, VisualKit.Box(0.3f, 1.8f, 2.4f), StoneMat(0.45f, true), new Vector3(-1.9f, 0.9f, -0.6f));
                VisualKit.Part(fechado, VisualKit.Box(0.3f, 0.7f, 1.6f), StoneMat(0.45f, true), new Vector3(1.9f, 0.35f, -1f));
                VisualKit.Part(fechado, VisualKit.Box(3.6f, 0.2f, 0.2f), madeiraVelha, new Vector3(0.2f, 0.5f, 0.3f), new Vector3(0, 25, 18));
                for (var i = 0; i < 6; i++)
                    VisualKit.Part(fechado, VisualKit.Box(R(0.3f, 0.7f), R(0.2f, 0.4f), R(0.3f, 0.6f)), StoneMat(0.4f, true), new Vector3(R(-1.5f, 1.5f), 0.15f, R(-1f, 1.2f)), new Vector3(0, R(0, 90), 0));
                // aberto: casinha de pé (paredes, telhado de duas águas, porta e janela)
                var parede = VisualKit.Mat(new Color(0.78f, 0.72f, 0.62f), 0.9f);
                VisualKit.Part(aberto, VisualKit.Box(4f, 2.6f, 3.8f), parede, new Vector3(0, 1.3f, 0));
                foreach (var lado in new[] { -1f, 1f })
                    VisualKit.Part(aberto, VisualKit.Box(4.4f, 0.15f, 2.6f), VisualKit.Mat(new Color(0.55f, 0.22f, 0.15f), 0.85f), new Vector3(0, 3.25f, lado * 0.95f), new Vector3(lado * 38f, 0, 0));
                VisualKit.Part(aberto, VisualKit.Box(1f, 1.7f, 0.08f), madeira, new Vector3(0, 0.85f, 1.92f));
                VisualKit.Part(aberto, VisualKit.Box(0.7f, 0.6f, 0.06f), VisualKit.Glow(new Color(1f, 0.75f, 0.35f), 1.2f), new Vector3(1.3f, 1.6f, 1.92f));
                break;
            }
            default:
            {
                foreach (var lado in new[] { -1f, 1f })
                    VisualKit.Part(raiz, VisualKit.Box(0.7f, 3.4f, 0.9f), StoneMat(0.45f, true), new Vector3(lado * 1.15f, 1.7f, 0));
                VisualKit.Part(raiz, VisualKit.Box(3f, 0.6f, 0.9f), StoneMat(0.45f, true), new Vector3(0, 3.5f, 0));
                // fechado: folha de madeira com ferragens e cadeado
                VisualKit.Part(fechado, VisualKit.Box(1.6f, 3.1f, 0.15f), madeira, new Vector3(0, 1.55f, 0));
                foreach (var y in new[] { 0.6f, 1.6f, 2.6f })
                    VisualKit.Part(fechado, VisualKit.Box(1.6f, 0.1f, 0.18f), ferro, new Vector3(0, y, 0));
                VisualKit.Part(fechado, VisualKit.Box(0.22f, 0.26f, 0.12f), VisualKit.Mat(new Color(0.7f, 0.55f, 0.2f), 0.35f, 0.85f), new Vector3(0.45f, 1.4f, 0.14f));
                // aberto: a folha girada para dentro e um baú atrás
                var folha = new Node3D { Position = new Vector3(-0.8f, 0, 0), RotationDegrees = new Vector3(0, -100, 0) };
                aberto.AddChild(folha);
                VisualKit.Part(folha, VisualKit.Box(1.6f, 3.1f, 0.15f), madeira, new Vector3(0.8f, 1.55f, 0));
                // 18/11 ("os das portas com chave não sumiram"): sem baú atrás da porta aberta — o prêmio já foi para a mochila
                break;
            }
        }
        // 17/11 (dono: "de longe tava marcando um nome, deixa sem" — pro jogo todo): sem nome flutuante; o aviso vem ao chegar perto (F)
        aberto.Visible = false;   // só aparece quando a marca chega (o laço dos portões liga/desliga)
        _gates.Add(new GateView { Gate = g, Bars = new Node3D(), Lever = new Node3D(), Built = aberto, Broken = fechado });
    }

    /// <summary>Selo de passagem: barreira violeta com runas; some para quem tem a marca (chefe vencido / rito / passo).</summary>
    private void AddSeal(MapGate g)
    {
        var w = (float)g.W;
        var stone = StoneMat(0.4f, true);
        foreach (var side in new[] { -1f, 1f })
        {
            VisualKit.Part(Root, VisualKit.Box(1.2f, 5f, 1.6f), stone, P(g.X, g.Y, 2.5f) + new Vector3(side * (w / 2f + 0.6f), 0, 0));
            VisualKit.Part(Root, VisualKit.Box(0.25f, 0.6f, 0.05f), VisualKit.Glow(MapKit.Violet, 2.5f), P(g.X, g.Y, 3.2f) + new Vector3(side * (w / 2f + 0.6f), 0, 0.82f));
        }
        var barrier = new Node3D { Position = P(g.X, g.Y) };
        Root.AddChild(barrier);
        VisualKit.Part(barrier, VisualKit.Box(w, 4.2f, 0.08f), VisualKit.Unshaded(new Color(0.56f, 0.41f, 0.75f, 0.28f), true), new Vector3(0, 2.1f, 0));
        for (var i = 0; i < 6; i++)
            VisualKit.Part(barrier, VisualKit.Box(0.08f, R(0.4f, 1.1f), 0.1f), VisualKit.Glow(MapKit.Violet, 2f),
                new Vector3(R(-w / 2f + 0.5f, w / 2f - 0.5f), R(0.8f, 3.4f), 0), new Vector3(0, 0, R(-30, 30)));
        barrier.AddChild(new OmniLight3D { LightColor = MapKit.Violet, LightEnergy = 1.4f, OmniRange = 7f, Position = new Vector3(0, 2f, 0.6f) });
        // 17/11 (dono: "de longe tava marcando um nome, deixa sem" — pro jogo todo): sem nome flutuante; o aviso vem ao chegar perto (F)
        _gates.Add(new GateView { Gate = g, Bars = new Node3D(), Lever = new Node3D(), Built = new Node3D(), Broken = barrier });
    }

    /// <summary>Ponte sobre a ravina: quebrada (estacas e tábuas soltas) até a missão gravar a marca; aí aparece inteira.</summary>
    private void AddBridge(MapGate g)
    {
        var wood = VisualKit.Mat(new Color(0.42f, 0.29f, 0.16f), 0.9f);
        var dark = VisualKit.Mat(new Color(0.28f, 0.19f, 0.11f), 0.9f);
        var rope = VisualKit.Mat(new Color(0.62f, 0.55f, 0.38f), 0.95f);
        var w = (float)g.W;
        var d = (float)g.H + 1.6f; // a ponte passa um pouco das bordas
        // o vão sob a ponte (mesmo fundo da ravina)
        if (Map.Theme != "praia")   // na Costa o rio da ravina passa por baixo
            VisualKit.Part(Root, VisualKit.Box(w, 0.04f, (float)g.H), VisualKit.Mat(new Color(0.03f, 0.025f, 0.02f), 1f), P(g.X, g.Y, 0.03f));
        // estacas nas quatro pontas (sempre visíveis)
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                VisualKit.Part(Root, VisualKit.Cylinder(0.12f, 0.14f, 1.3f, 6), dark, P(g.X, g.Y, 0.65f) + new Vector3(sx * (w / 2f - 0.4f), 0, sz * d / 2f));

        var built = new Node3D { Position = P(g.X, g.Y, 0f) };
        Root.AddChild(built);
        // ponte do kit da Costa (9,6 × 5 m), cruzando a ravina; se faltar, as tábuas antigas
        var kitBridge = Map.Theme == "praia" && MapKit.Glb(built, "costa/costa_ponte", Vector3.Zero, 0f, 1f);
        if (kitBridge) built.Scale = new Vector3(w / 9.6f, 1f, Mathf.Max(1f, ((float)g.H + 2.6f) / 5f));   // rio mais largo (10/11): a ponte estica junto
        // 12/11: o tabuleiro fica 0,34 m acima do chão — os pés sobem nele (rampa nas cabeceiras) em vez de atravessar a madeira
        var meia = kitBridge ? 3.1f * built.Scale.Z : d / 2f;
        Visuals.EscadaCasa.RegistrarPonte((float)g.X - w / 2f + 0.4f, (float)g.X + w / 2f - 0.4f, (float)g.Y - meia, (float)g.Y + meia, kitBridge ? 0.34f : 0.2f, 1.6f);
        if (!kitBridge)
        for (var z = -d / 2f + 0.2f; z <= d / 2f - 0.1f; z += 0.42f)
            VisualKit.Part(built, VisualKit.Box(w - 1.0f, 0.1f, 0.36f), (int)((z + 10) / 0.42f) % 2 == 0 ? wood : dark, new Vector3(R(-0.05f, 0.05f), 0.16f, z),
                new Vector3(0, R(-2, 2), 0));
        if (!kitBridge)
        foreach (var sx in new[] { -1f, 1f })
        {
            VisualKit.Part(built, VisualKit.Box(0.08f, 0.08f, d), rope, new Vector3(sx * (w / 2f - 0.4f), 1.1f, 0));
            VisualKit.Part(built, VisualKit.Box(0.16f, 0.14f, d), dark, new Vector3(sx * (w / 2f - 0.55f), 0.12f, 0));
        }

        var broken = new Node3D { Position = P(g.X, g.Y, 0f) };
        Root.AddChild(broken);
        // restos: duas tábuas em cada borda e uma pendurada; corda arrebentada
        foreach (var sz in new[] { -1f, 1f })
            for (var i = 0; i < 2; i++)
                VisualKit.Part(broken, VisualKit.Box(w - 1.4f, 0.1f, 0.36f), dark, new Vector3(R(-0.3f, 0.3f), 0.14f, sz * (d / 2f - 0.25f - i * 0.42f)),
                    new Vector3(R(-6, 6), R(-10, 10), R(-4, 4)));
        VisualKit.Part(broken, VisualKit.Box(1.6f, 0.08f, 0.32f), wood, new Vector3(1.0f, -0.25f, 0.2f), new Vector3(0, 20, -35));
        foreach (var sx in new[] { -1f, 1f })
            VisualKit.Part(broken, VisualKit.Box(0.06f, 0.06f, 0.9f), rope, new Vector3(sx * (w / 2f - 0.4f), 0.95f, -d / 2f + 0.45f), new Vector3(25, 0, 0));
        // 17/11: sem placa flutuante (o aviso vem ao chegar perto)
        _gates.Add(new GateView { Gate = g, Bars = new Node3D(), Lever = new Node3D(), Built = built, Broken = broken });
    }

    private void AddChest(MapChest c)
    {
        var root = new Node3D { Position = P(c.X, c.Y, 0f), RotationDegrees = new Vector3(0, (float)c.Facing, 0) };
        Root.AddChild(root);
        // baú esculpido do lote 2 (tampa com dobradiça); o secreto ganha um tom violeta
        if (Visuals.GlbCache.Instance("cenario/cenario_bau") is { } chest && chest.FindChild("cenario_bau_tampa", true, false) is Node3D glbLid)
        {
            chest.RotationDegrees = new Vector3(0, 180, 0);   // dobradiça para trás (frente do baú = +Z do mapa)
            chest.Scale = Vector3.One * 1.1f;
            root.AddChild(chest);
            // 14/11 tarde (dono: "o segredo está muito destacado"): o baú secreto é um baú comum, escondido entre as rochas,
            // com um brilho bem fraco (sem o véu roxo)
            var light = new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.35f), LightEnergy = 0.9f, OmniRange = 3.2f, Position = new Vector3(0, 0.9f, 0), Visible = !c.Secret };
            root.AddChild(light);
            Collider(P(c.X, c.Y, 0.4f), new BoxShape3D { Size = new Vector3(1f, 0.8f, 0.7f) });
            _chests.Add(new ChestView { Chest = c, Lid = glbLid, LidSign = 1f, Glow = light, Root = root });
            return;
        }
        var wood = VisualKit.Mat(new Color(0.40f, 0.26f, 0.13f), 0.85f);
        var trim = VisualKit.Mat(new Color(0.85f, 0.66f, 0.28f), 0.4f, 0.7f);
        VisualKit.Part(root, VisualKit.Box(1.0f, 0.55f, 0.65f), wood, new Vector3(0, 0.28f, 0));
        VisualKit.Part(root, VisualKit.Box(1.04f, 0.08f, 0.69f), trim, new Vector3(0, 0.08f, 0));
        var lid = VisualKit.Joint(root, new Vector3(0, 0.55f, -0.32f));
        VisualKit.Part(lid, VisualKit.Box(1.02f, 0.22f, 0.67f), wood, new Vector3(0, 0.11f, 0.32f));
        VisualKit.Part(lid, VisualKit.Box(1.06f, 0.06f, 0.08f), trim, new Vector3(0, 0.12f, 0.64f));
        VisualKit.Part(root, VisualKit.Box(0.14f, 0.18f, 0.06f), trim, new Vector3(0, 0.5f, 0.34f));
        var glow = new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.35f), LightEnergy = 0.9f, OmniRange = 3.2f, Position = new Vector3(0, 0.9f, 0), Visible = !c.Secret };
        root.AddChild(glow);
        Collider(P(c.X, c.Y, 0.4f), new BoxShape3D { Size = new Vector3(1f, 0.8f, 0.7f) });
        _chests.Add(new ChestView { Chest = c, Lid = lid, Glow = glow, Root = root });
    }

    /// <summary>Casa modular de enxaimel em coordenadas locais (frente = +Z). variant muda toldo/placa/chaminé.</summary>
    internal void BuildHouse(Node3D node, float W, float D, float h, int variant)
    {
        var stone = StoneMat(0.46f, true);
        var plaster = TexMat("gen_reboco.png", new Color(0.92f, 0.84f, 0.70f), 0.5f);
        var timber = VisualKit.Mat(new Color(0.17f, 0.12f, 0.09f), 0.9f);
        var roof = TexMat("gen_ardosia.png", new Color(0.85f, 0.85f, 0.95f), 0.5f);
        var glow = VisualKit.Glow(new Color(1f, 0.66f, 0.28f), 1.8f);
        var awning = VisualKit.Mat(MapKit.Bordo, 0.95f);
        var g = h * 0.45f;           // altura do térreo
        const float jut = 0.35f;     // balanço do andar de cima
        // térreo de pedra e andar de reboco em balanço
        VisualKit.Part(node, VisualKit.Box(W, g, D), stone, new Vector3(0, g / 2f, 0));
        VisualKit.Part(node, VisualKit.Box(W + jut, h - g, D + jut), plaster, new Vector3(0, g + (h - g) / 2f, 0));
        // vigas: base e topo do andar, montantes e mãos-francesas na frente e nas costas
        foreach (var z in new[] { 1f, -1f })
        {
            var fz = z * ((D + jut) / 2f + 0.03f);
            VisualKit.Part(node, VisualKit.Box(W + jut + 0.1f, 0.16f, 0.08f), timber, new Vector3(0, g + 0.08f, fz));
            VisualKit.Part(node, VisualKit.Box(W + jut + 0.1f, 0.16f, 0.08f), timber, new Vector3(0, h - 0.08f, fz));
            var posts = Math.Max(3, (int)(W / 1.3f));
            for (var i = 0; i <= posts; i++)
            {
                var x = -(W + jut) / 2f + i * (W + jut) / posts;
                VisualKit.Part(node, VisualKit.Box(0.13f, h - g, 0.08f), timber, new Vector3(x, g + (h - g) / 2f, fz));
                if (i < posts && i % 2 == 0)
                    VisualKit.Part(node, VisualKit.Box(0.09f, (h - g) * 1.15f, 0.07f), timber,
                        new Vector3(x + (W + jut) / posts / 2f, g + (h - g) / 2f, fz), new Vector3(0, 0, (i % 4 == 0 ? 1 : -1) * 32f));
            }
        }
        foreach (var x in new[] { 1f, -1f })
            for (var i = 0; i <= 2; i++)
                VisualKit.Part(node, VisualKit.Box(0.08f, h - g, 0.13f), timber, new Vector3(x * ((W + jut) / 2f + 0.03f), g + (h - g) / 2f, -(D + jut) / 2f + i * (D + jut) / 2f));
        // janelas do andar (com venezianas) e do térreo
        var windows = Math.Max(2, (int)(W / 2.6f));
        for (var i = 0; i < windows; i++)
        {
            var x = -W / 2f + (i + 0.5f) * W / windows;
            var wy = g + (h - g) * 0.52f;
            VisualKit.Part(node, VisualKit.Box(0.48f, 0.62f, 0.06f), glow, new Vector3(x, wy, (D + jut) / 2f + 0.05f));
            VisualKit.Part(node, VisualKit.Box(0.06f, 0.62f, 0.07f), timber, new Vector3(x, wy, (D + jut) / 2f + 0.06f));
            VisualKit.Part(node, VisualKit.Box(0.56f, 0.07f, 0.1f), timber, new Vector3(x, wy - 0.34f, (D + jut) / 2f + 0.07f));
        }
        // porta em arco no térreo + janela de loja com toldo
        var doorX = variant == 1 ? -W * 0.22f : W * 0.18f;
        VisualKit.Part(node, VisualKit.Box(1.05f, 1.9f, 0.1f), TexMat("gen_madeira.png", new Color(0.7f, 0.55f, 0.45f), 0.6f), new Vector3(doorX, 0.95f, D / 2f + 0.03f));
        VisualKit.Part(node, VisualKit.Cylinder(0.56f, 0.56f, 0.12f, 12), stone, new Vector3(doorX, 1.9f, D / 2f + 0.02f), new Vector3(90, 0, 0), new Vector3(1f, 1f, 0.55f));
        var shopX = variant == 1 ? W * 0.2f : -W * 0.22f;
        if (W > 5.5f)
        {
            VisualKit.Part(node, VisualKit.Box(1.4f, 0.8f, 0.06f), glow, new Vector3(shopX, 1.25f, D / 2f + 0.04f));
            VisualKit.Part(node, VisualKit.Box(1.6f, 0.1f, 0.16f), timber, new Vector3(shopX, 0.82f, D / 2f + 0.08f));
            if (variant != 2)
                VisualKit.Part(node, VisualKit.Box(1.9f, 0.06f, 0.9f), awning, new Vector3(shopX, g - 0.15f, D / 2f + 0.45f), new Vector3(-22, 0, 0));
        }
        // telhado de duas águas: empena triangular na frente e duas placas de ardósia
        const float k = 0.5f;                       // achatamento da empena (40° de inclinação)
        var r = W / Mathf.Sqrt(3f);
        var rise = 1.5f * r * k;
        var gable = VisualKit.Part(node, new CylinderMesh { TopRadius = r, BottomRadius = r, Height = D + jut, RadialSegments = 3, Rings = 1 }, plaster,
            new Vector3(0, h + r * 0.5f * k, 0), new Vector3(-90, 0, 0), new Vector3(1f, 1f, k));
        gable.Name = "Empena";
        var slope = Mathf.Atan2(rise, W / 2f);
        var slab = Mathf.Sqrt(rise * rise + W * W / 4f) + 0.45f;
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(node, VisualKit.Box(slab, 0.16f, D + jut + 0.7f), roof,
                new Vector3(side * W / 4f, h + rise / 2f + 0.1f, 0), new Vector3(0, 0, -side * Mathf.RadToDeg(slope)));
        VisualKit.Part(node, VisualKit.Box(0.22f, 0.22f, D + jut + 0.75f), timber, new Vector3(0, h + rise + 0.12f, 0));
        foreach (var z in new[] { 1f, -1f }) // tábuas de beiral na empena
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(node, VisualKit.Box(slab - 0.3f, 0.12f, 0.08f), timber,
                    new Vector3(side * W / 4f, h + rise / 2f, z * ((D + jut) / 2f + 0.05f)), new Vector3(0, 0, -side * Mathf.RadToDeg(slope)));
        VisualKit.Part(node, VisualKit.Box(0.45f, 0.6f, 0.06f), glow, new Vector3(0, h + rise * 0.35f, (D + jut) / 2f + 0.06f)); // janela da empena
        // chaminé de pedra
        var cx = variant == 2 ? -W * 0.25f : W * 0.25f;
        VisualKit.Part(node, VisualKit.Box(0.7f, rise + 1.2f, 0.7f), StoneMat(0.38f, true), new Vector3(cx, h + (rise + 1.2f) / 2f, -D * 0.2f));
        VisualKit.Part(node, VisualKit.Box(0.85f, 0.15f, 0.85f), StoneMat(0.3f, true), new Vector3(cx, h + rise + 1.2f, -D * 0.2f));
        // placa pendurada (loja) em algumas casas
        if (variant == 0)
        {
            var sx = doorX + 0.9f;
            VisualKit.Part(node, VisualKit.Box(0.08f, 0.08f, 0.8f), timber, new Vector3(sx, g + 0.3f, D / 2f + 0.4f));
            VisualKit.Part(node, VisualKit.Box(0.06f, 0.55f, 0.55f), VisualKit.Mat(MapKit.Bordo, 0.9f), new Vector3(sx, g - 0.05f, D / 2f + 0.65f));
        }
    }

    /// <summary>
    /// Vegetação e pedrisco (só visual, sem colisão; um MultiMesh por tipo): touceiras de capim em cartões cruzados com
    /// folhinhas de verdade (fol_capim / fol_capim_seco), moitas folhosas (fol_moita) e pedrinhas irregulares com a
    /// textura de pedra. O vento balança as pontas.
    /// </summary>
    /// <summary>
    /// Profundidade (feedback: "veja como o lobby fica bonito com profundidade"): fora dos limites do mapa, várias fileiras
    /// de silhuetas (árvores do tema, dunas, rochas) que somem na névoa — o mapa deixa de terminar num campo vazio.
    /// </summary>
    /// <summary>
    /// 16/11 ("deixar o jogo todo no mesmo padrão"): fora da Costa o penhasco era uma pilha de caixas com textura de tijolo.
    /// Agora todo tema ao ar livre usa o paredão e as rochas do kit da Costa (PBR), tingidos com a cor da região.
    /// null = tema sem tinta (Costa, que já é o original; gruta, cidade e interiores ficam como estavam).
    /// </summary>
    private Color? TintoRocha => Map.Theme switch
    {
        "vale" => new Color(0.56f, 0.53f, 0.56f),   // cinza de cinzas (o kit é cal quente)
        "mata" => new Color(0.56f, 0.62f, 0.50f),
        "canion" => new Color(0.95f, 0.60f, 0.44f),
        "brejo" => new Color(0.44f, 0.48f, 0.40f),
        "fantasma" => new Color(0.60f, 0.60f, 0.70f),
        "neve" => new Color(0.95f, 1.0f, 1.08f),
        "lava" => new Color(0.30f, 0.27f, 0.28f),
        "acropole" => new Color(1.0f, 0.95f, 0.88f),
        "lua" => new Color(0.62f, 0.62f, 0.68f),
        _ => null,
    };

    private readonly Dictionary<(Material, Color), Material> _tintas = new();

    /// <summary>Multiplica a cor de todos os materiais do modelo (uma cópia por material e cor, reaproveitada).</summary>
    private void Tingir(Node n, Color c)
    {
        if (n is MeshInstance3D mi && mi.Mesh is { } mesh)
            for (var i = 0; i < mesh.GetSurfaceCount(); i++)
            {
                if (mi.GetActiveMaterial(i) is not BaseMaterial3D bm) continue;
                if (!_tintas.TryGetValue((bm, c), out var t))
                {
                    var d = (BaseMaterial3D)bm.Duplicate();
                    d.AlbedoColor = new Color(bm.AlbedoColor.R * c.R, bm.AlbedoColor.G * c.G, bm.AlbedoColor.B * c.B, bm.AlbedoColor.A);
                    _tintas[(bm, c)] = t = d;
                }
                mi.SetSurfaceOverrideMaterial(i, t);
            }
        foreach (var ch in n.GetChildren()) Tingir(ch, c);
    }

    /// <summary>Paredão de cal em camadas (kit da Costa) a cada 10 m ao longo do penhasco, virado para o centro do mapa.</summary>
    private bool CliffKit(MapObstacle o, float h)
    {
        if (Visuals.GlbCache.Instance("costa/costa_penhasco_a") is null) return false;
        var w = (float)o.W; var d = (float)o.H;
        var along = w >= d; var len = along ? w : d; var depth = along ? d : w;
        var n = Math.Max(1, (int)Mathf.Ceil(len / 10f));
        var seg = len / n;
        // frente do trecho (+Z do modelo) para o centro do mapa: P(x, y) usa o y do mapa como Z
        var yaw = along ? (o.Y > 0 ? 180f : 0f) : (o.X > 0 ? -90f : 90f);
        for (var i = 0; i < n; i++)
        {
            var t = -len / 2f + seg * (i + 0.5f);
            var node = new Node3D
            {
                Position = P(o.X, o.Y) + (along ? new Vector3(t, 0, 0) : new Vector3(0, 0, t)),
                RotationDegrees = new Vector3(0, yaw, 0),
                Scale = new Vector3(seg / 10f, Math.Max(h, 3f) / 8f, Math.Max(1f, depth / 3f)),
            };
            Root.AddChild(node);
            MapKit.Glb(node, "costa/costa_penhasco_" + (i % 2 == 0 ? "a" : "b"), Vector3.Zero, 0f, 1f);
            if (TintoRocha is { } tr) Tingir(node, tr);
        }
        Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3(w, h, d) });
        return true;
    }

    /// <summary>Ravina com o trecho do kit da Costa (10 m) repetido ao longo do comprimento.</summary>
    private bool RavineKit(MapObstacle o)
    {
        if (Visuals.GlbCache.Instance("costa/costa_ravina_trecho") is null) return false;
        var w = (float)o.W; var d = (float)o.H;
        var along = w >= d; var len = along ? w : d; var depth = along ? d : w;
        var n = Math.Max(1, (int)Mathf.Ceil(len / 10f));
        var seg = len / n;
        for (var i = 0; i < n; i++)
        {
            var t = -len / 2f + seg * (i + 0.5f);
            var node = new Node3D
            {
                Position = P(o.X, o.Y) + (along ? new Vector3(t, 0, 0) : new Vector3(0, 0, t)),
                RotationDegrees = new Vector3(0, along ? 0f : 90f, 0),
                Scale = new Vector3(seg / 10f, 1f, depth / 2.4f),
            };
            Root.AddChild(node);
            MapKit.Glb(node, "costa/costa_ravina_trecho", Vector3.Zero, 0f, 1f);
        }
        // 10/11 ("o rio parece uma faixa preta; mais real e mais largo"): água correndo no fundo, com reflexo e espuma
        var water = new MeshInstance3D
        {
            // o fundo do trecho é raso (o chão do mapa é um plano só): a água cobre o fundo escuro, rente ao chão
            Mesh = new PlaneMesh { Size = along ? new Vector2(len, depth) : new Vector2(depth, len) },
            Position = P(o.X, o.Y, 0.06f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = RiverMaterial(len / 6f, along),
        };
        Root.AddChild(water);
        return true;
    }

    private static Shader? _riverShader;

    private static ShaderMaterial RiverMaterial(float repeat, bool along)
    {
        _riverShader ??= new Shader { Code = @"
shader_type spatial;
render_mode cull_disabled;
uniform sampler2D normals : hint_normal, repeat_enable;
uniform float repeat = 8.0;
uniform bool along = true;
void fragment() {
    vec2 uv = along ? vec2(UV.x * repeat, UV.y) : vec2(UV.y * repeat, UV.x);
    vec2 a = uv + vec2(TIME * 0.22, 0.0);
    vec2 b = uv * 1.7 + vec2(TIME * 0.31, 0.13);
    NORMAL_MAP = normalize(texture(normals, a).rgb + texture(normals, b).rgb - 1.0);
    float edge = 1.0 - smoothstep(0.0, 0.18, min(uv.y, 1.0 - uv.y));
    ALBEDO = mix(vec3(0.05, 0.17, 0.20), vec3(0.75, 0.82, 0.80), edge * 0.55);
    ROUGHNESS = mix(0.06, 0.6, edge);
    METALLIC = 0.15;
    SPECULAR = 0.6;
}" };
        var mat = new ShaderMaterial { Shader = _riverShader };
        mat.SetShaderParameter("repeat", repeat);
        mat.SetShaderParameter("along", along);
        if (VisualAssets.Load("fx_agua_n.png") is { } n) mat.SetShaderParameter("normals", n);
        return mat;
    }

    /// <summary>
    /// 13/11 ("coqueiros estáticos, árvores, barracas, arbustos — dá uma leve mexida, isso dá mais realismo"): todo modelo de
    /// planta ou de pano do mapa (props, cenas prontas, canteiros) entra no vento — planta dobra pela base, pano só treme.
    /// </summary>
    private void BalancarNoVento(Node root)
    {
        var ja = new HashSet<Node3D>(_swaying.Select(s => s.Node));
        var pilha = new Stack<Node>();
        pilha.Push(root);
        while (pilha.Count > 0)
        {
            var n = pilha.Pop();
            if (n is Node3D n3 && n3.HasMeta("glb") && !ja.Contains(n3))
            {
                var f = n3.GetMeta("glb").AsString();
                var amp = f.Contains("coqueiro") || f.Contains("palmeira") ? 2.2f
                    : f.Contains("capim") || f.Contains("moita") ? 3f
                    : f.Contains("flores") || f.Contains("arbusto") || f.Contains("erva") ? 1.8f
                    : f.Contains("pinheiro") || f.Contains("arvore") ? 1.0f
                    : f.Contains("tenda") || f.Contains("barraca") || f.Contains("redes") || f.Contains("estandarte") || f.Contains("lona") ? 0.45f : 0f;
                if (amp > 0f) { _swaying.Add((n3, n3.RotationDegrees, R(0, 6.28f), amp * R(0.8f, 1.2f))); continue; }
            }
            foreach (var c in n.GetChildren()) pilha.Push(c);
        }
    }

    /// <summary>Árvore da Costa (kit): coqueiro, pinheiro-manso ou arbusto, na altura pedida pelo obstáculo.</summary>
    private bool CoastTree(Node3D node, float pick, float h)
    {
        var (f, baseH) = pick < 0.45f ? ("costa_coqueiro_" + (pick < 0.25f ? "a" : "b"), pick < 0.25f ? 8.2f : 6.5f)
                       : pick < 0.85f ? ("costa_pinheiro_" + "abc"[(int)R(0, 2.99f)], 6.6f)
                       : ("costa_arbusto_" + "abc"[(int)R(0, 2.99f)], 1.2f);
        var sc = f.Contains("arbusto") ? R(1.0f, 1.5f) : Math.Max(h, 5f) / baseH * R(0.9f, 1.15f);
        return MapKit.Glb(node, "costa/" + f, Vector3.Zero, 0f, sc);
    }

    /// <summary>
    /// Vida no chão da Costa (kit): capim, flores, seixos, algas, arbustos, troncos e conchas. Um MultiMesh por peça
    /// (~260 peças de 1,5–4,5 mil triângulos viram poucas chamadas de desenho), fora das estradas e da área segura.
    /// </summary>
    private void CoastScatter(float size, float half, bool soCapim = false)
    {
        var groups = new Dictionary<string, List<Transform3D>>();
        var count = (int)((soCapim ? 150 : 260) * Mathf.Clamp(size * size / (220f * 220f), 0.6f, 1.6f) * GraphicsQuality.ScatterScale);
        float cx = 0, cz = 0; var resta = 0;
        for (var i = 0; i < count; i++)
        {
            float x, z;
            if (soCapim)
            {
                // em touceiras (3 a 8 juntas), longe das ruas — campo, não jardim
                if (resta <= 0) { cx = R(-half, half); cz = R(-half, half); resta = (int)R(3, 8); }
                x = cx + R(-2.6f, 2.6f); z = cz + R(-2.6f, 2.6f); resta--;
            }
            else { x = R(-half, half); z = R(-half, half); }
            if (!Clear(x, z, 0.8f) || OnRoad(x, z)) continue;
            var k = soCapim ? R(0, 0.5f) : R(0, 1);
            var f = k < 0.5f ? "costa_capim_" + "abc"[(int)R(0, 2.99f)] : k < 0.65f ? "costa_flores_" + (k < 0.58f ? "a" : "b")
                  : k < 0.78f ? "costa_seixos" : k < 0.88f ? "costa_alga_" + (k < 0.83f ? "a" : "b") : k < 0.95f ? "costa_arbusto_" + "abc"[(int)R(0, 2.99f)]
                  : k < 0.98f ? "costa_tronco_" + (k < 0.965f ? "a" : "b") : "costa_concha_grande";
            if (!groups.TryGetValue(f, out var list)) groups[f] = list = new List<Transform3D>();
            var sc = R(0.85f, 1.25f);
            list.Add(new Transform3D(new Basis(Vector3.Up, R(0, Mathf.Tau)).Scaled(Vector3.One * sc), new Vector3(x, Visuals.EscadaCasa.ElevBase(x, z, 0.6), z)));
        }
        foreach (var (file, list) in groups)
        {
            if (Visuals.GlbCache.Instance("costa/" + file) is not { } tmpl) continue;
            // cada malha do arquivo vira um MultiMesh (com a posição dela dentro do arquivo)
            void Walk(Node n, Transform3D acc)
            {
                foreach (var c in n.GetChildren())
                {
                    var t = c is Node3D n3 ? acc * n3.Transform : acc;
                    if (c is MeshInstance3D mi && mi.Mesh is not null)
                    {
                        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mi.Mesh, InstanceCount = list.Count };
                        for (var j = 0; j < list.Count; j++) mm.SetInstanceTransform(j, list[j] * t);
                        // 13/11: capim, flor, seixo, alga e concha não projetam sombra (eram desenhados mais 4 vezes, uma por faixa de sombra)
                        var sombra = file.Contains("arbusto") || file.Contains("tronco");
                        Root.AddChild(new MultiMeshInstance3D { Multimesh = mm, CastShadow = sombra ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off });
                    }
                    Walk(c, t);
                }
            }
            Walk(tmpl, Transform3D.Identity);
            tmpl.QueueFree();
        }
    }

    private void AddHorizon(float size)
    {
        // Costa: serras de cal ao longe (kit da Costa) no lugar das bolas de duna
        if (Map.Theme == "praia" && Visuals.GlbCache.Instance("costa/costa_horizonte_a") is not null)
        {
            var hh = size / 2f;
            for (var a = 0f; a < 360f; a += R(14f, 20f))
            {
                var dist = hh + R(45f, 95f);
                var pos = new Vector3(Mathf.Sin(Mathf.DegToRad(a)) * dist, -0.5f, Mathf.Cos(Mathf.DegToRad(a)) * dist);
                if (pos.X > hh * 0.55f) continue;   // lado do mar (leste): horizonte aberto
                var node = new Node3D { Position = pos, RotationDegrees = new Vector3(0, a + 180f, 0), Scale = Vector3.One * R(1.0f, 1.7f) };
                Root.AddChild(node);
                MapKit.Glb(node, "costa/costa_horizonte_" + (R(0, 1) < 0.5f ? "a" : "b"), Vector3.Zero, 0f, 1f);
            }
            return;
        }
        var half = size / 2f;
        var (col, kind) = Map.Theme switch
        {
            "praia" => (new Color(0.30f, 0.26f, 0.22f), "duna"),
            "canion" or "lava" or "acropole" => (new Color(0.28f, 0.20f, 0.17f), "rocha"),
            "brejo" or "fantasma" => (new Color(0.12f, 0.14f, 0.12f), "seca"),
            "neve" => (new Color(0.55f, 0.6f, 0.68f), "pinho"),
            "lua" => (new Color(0.30f, 0.30f, 0.34f), "rocha"),   // 12/11: crateras e serras cinzentas
            _ => (new Color(0.12f, 0.16f, 0.11f), "pinho"),
        };
        Mesh mesh = kind switch
        {
            "duna" => new SphereMesh { Radius = 1f, Height = 1f, RadialSegments = 12, Rings = 6 },
            "rocha" => new CylinderMesh { TopRadius = 0.35f, BottomRadius = 1f, Height = 1f, RadialSegments = 6 },
            _ => new CylinderMesh { TopRadius = 0f, BottomRadius = 1f, Height = 1f, RadialSegments = 7 },
        };
        var mat = new StandardMaterial3D { AlbedoColor = col, Roughness = 1f };
        var count = (int)(240 * Mathf.Clamp(size / 200f, 0.6f, 1.6f));
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = count };
        for (var i = 0; i < count; i++)
        {
            // em volta, entre 6 e 70 m além da borda, mais denso perto
            var band = Mathf.Pow(R(0, 1), 1.6f);
            var dist = 6f + band * 64f;
            var side = (int)R(0, 4);
            var t = R(-half - dist, half + dist);
            var pos = side switch { 0 => new Vector3(t, 0, -half - dist), 1 => new Vector3(t, 0, half + dist), 2 => new Vector3(-half - dist, 0, t), _ => new Vector3(half + dist, 0, t) };
            Vector3 scale = kind switch
            {
                "duna" => new Vector3(R(8, 18), R(2, 5), R(6, 14)),
                "rocha" => new Vector3(R(3, 7), R(5, 14), R(3, 7)),
                "seca" => new Vector3(R(0.4f, 0.8f), R(6, 11), R(0.4f, 0.8f)),
                _ => new Vector3(R(1.6f, 2.6f), R(6, 12), R(1.6f, 2.6f)),
            };
            var basis = new Basis(Vector3.Up, R(0, Mathf.Tau)).Scaled(scale);
            mm.SetInstanceTransform(i, new Transform3D(basis, pos + new Vector3(0, kind == "duna" ? -scale.Y * 0.3f : scale.Y / 2f, 0)));
        }
        Root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private void AddScatter(float size)
    {
        var (grassTex, grassTint, bushes) = Map.Theme switch
        {
            "vale" => ("fol_capim.png", new Color(1f, 1f, 0.9f), 1f),
            "mata" => ("fol_capim.png", new Color(0.8f, 0.92f, 0.8f), 1.2f),
            "brejo" => ("fol_capim.png", new Color(0.75f, 0.85f, 0.7f), 1f),
            "praia" => ("fol_capim_seco.png", new Color(1f, 0.97f, 0.9f), 0.15f),
            "canion" or "acropole" => ("fol_capim_seco.png", new Color(1.05f, 0.95f, 0.85f), 0.1f),
            "fantasma" => ("fol_capim_seco.png", new Color(0.7f, 0.75f, 0.8f), 0f),
            "neve" => ("fol_capim_seco.png", new Color(1.1f, 1.1f, 1.15f), 0f),
            _ => ((string?)null, Colors.White, 0f),
        };
        var half = size / 2f - 16f;
        var area = Mathf.Clamp(size * size / (90f * 90f), 1f, 6f) * GraphicsQuality.ScatterScale;
        int N(float baseCount) => (int)(baseCount * area);
        void Scatter(Mesh mesh, Material mat, int count, float minS, float maxS, float tilt, bool clump)
        {
            if (count <= 0) return;
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = count };
            var placed = 0;
            float cx = 0, cy = 0; var left = 0;
            for (var tries = 0; tries < count * 4 && placed < count; tries++)
            {
                float x, y;
                if (clump)
                {
                    // touceiras: várias juntas em manchas (natural), não espalhadas uma a uma
                    if (left <= 0) { cx = R(-half, half); cy = R(-half, half); left = (int)R(4, 14); }
                    x = cx + R(-2.2f, 2.2f); y = cy + R(-2.2f, 2.2f); left--;
                }
                else { x = R(-half, half); y = R(-half, half); }
                if (!Clear(x, y, 0.4f) || OnRoad(x, y)) continue;
                var sc = R(minS, maxS);
                var basis = new Basis(Vector3.Up, R(0, Mathf.Tau));
                if (tilt > 0) basis = basis * new Basis(Vector3.Right, R(-tilt, tilt));
                mm.SetInstanceTransform(placed++, new Transform3D(basis.Scaled(new Vector3(sc, sc * R(0.85f, 1.25f), sc)), new Vector3(x, Visuals.EscadaCasa.ElevBase(x, y, 0.6), y)));
            }
            mm.VisibleInstanceCount = placed;
            Root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        if (grassTex is { } gt && VisualAssets.Load(gt) is { } gtex)
        {
            // touceiras de folhas de verdade (geometria fina, cor que escurece na base e clareia na ponta, luz igual à do
            // chão) no lugar dos cartões com recorte — pedido: "o mato parece um papel encaixado"
            var dry = grassTex.Contains("seco");
            var (baseC, tipC) = Map.Theme switch
            {
                "praia" => (new Color(0.36f, 0.33f, 0.20f), new Color(0.80f, 0.74f, 0.50f)),
                "canion" or "acropole" => (new Color(0.38f, 0.30f, 0.20f), new Color(0.78f, 0.66f, 0.46f)),
                "fantasma" => (new Color(0.22f, 0.24f, 0.24f), new Color(0.55f, 0.58f, 0.58f)),
                "neve" => (new Color(0.40f, 0.42f, 0.44f), new Color(0.86f, 0.88f, 0.92f)),
                "mata" => (new Color(0.10f, 0.20f, 0.08f), new Color(0.42f, 0.58f, 0.24f)),
                "brejo" => (new Color(0.14f, 0.20f, 0.10f), new Color(0.46f, 0.52f, 0.26f)),
                _ => (new Color(0.16f, 0.26f, 0.10f), new Color(0.58f, 0.66f, 0.30f)),
            };
            Scatter(GrassTuft(dry ? 7 : 11, dry ? 0.42f : 0.5f), BladeMat(baseC, tipC, dry ? 0.07f : 0.1f), N(dry ? 900 : 1400),
                0.7f, 1.35f, 0.12f, true);
            // moitas: touceiras altas e cheias (mesma geometria, mais folhas), concentradas nas bordas das clareiras
            if (bushes > 0)
                Scatter(GrassTuft(22, 0.95f), BladeMat(baseC.Darkened(0.2f), tipC.Darkened(0.12f), 0.05f), N(110 * bushes), 0.8f, 1.5f, 0f, true);
        }
        if (Map.Theme == "praia") CoastScatter(size, half);
        // 13/11 ("usa mais o asset do capim alto"): touceiras do capim alto (modelo da Costa) em manchas pelo campo aberto
        else if (Map.Theme is "vale" or "mata" or "brejo" or "canion" && half > 24f)
            CoastScatter(size, half, soCapim: true);
        // pedrinhas irregulares (esfera de poucos lados achatada, com textura de pedra)
        var pebble = new SphereMesh { Radius = 0.12f, Height = 0.12f, RadialSegments = 6, Rings = 3 };
        var stone = TexMat("ter_pedra.png", Map.Theme == "neve" ? new Color(1.3f, 1.3f, 1.4f) : Map.Theme == "praia" ? new Color(1.25f, 1.18f, 1.05f) : new Color(1.0f, 0.96f, 0.9f), 2f);
        Scatter(pebble, stone, N(300), 0.5f, 1.8f, 0.4f, true);
    }

    /// <summary>Dois cartões cruzados (largura, altura) com a base no chão: touceira vista de qualquer lado.</summary>
    private static ArrayMesh CrossedCards(float width, float height)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var k = 0; k < 3; k++)
        {
            var a = k * Mathf.Pi / 3f;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * width / 2f;
            var n = new Vector3(-d.Z, 0, d.X).Normalized();
            var v = new[] { -d, d, d + Vector3.Up * height, -d + Vector3.Up * height };
            var uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            foreach (var i in new[] { 0, 1, 2, 0, 2, 3 })
            {
                st.SetNormal(Vector3.Up * 0.8f + n * 0.2f);
                st.SetUV(uv[i]);
                st.AddVertex(v[i]);
            }
        }
        return st.Commit();
    }

    /// <summary>
    /// Touceira de capim em geometria: N folhas finas e afinando, curvadas para fora, de alturas diferentes.
    /// UV.y = 0 na base e 1 na ponta (a cor e o balanço usam isso). Normais para cima: a luz casa com a do chão.
    /// </summary>
    private static ArrayMesh GrassTuft(int blades, float height)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var rng = new System.Random(blades * 31 + (int)(height * 100));
        float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        const int segs = 4;
        for (var b = 0; b < blades; b++)
        {
            var ang = b / (float)blades * Mathf.Tau + Rr(-0.3f, 0.3f);
            var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            var side = new Vector3(-dir.Z, 0, dir.X);
            var root = dir * Rr(0f, 0.09f);
            var h = height * Rr(0.55f, 1.1f);
            var lean = Rr(0.15f, 0.5f) * h;
            var w = Rr(0.022f, 0.036f);
            var pts = new Vector3[segs + 1];
            for (var i = 0; i <= segs; i++)
            {
                var t = i / (float)segs;
                pts[i] = root + Vector3.Up * (h * t) + dir * (lean * t * t);
            }
            for (var i = 0; i < segs; i++)
            {
                var t0 = i / (float)segs; var t1 = (i + 1) / (float)segs;
                var w0 = w * (1f - t0 * 0.92f); var w1 = w * (1f - t1 * 0.92f);
                var a = pts[i] - side * w0; var bb = pts[i] + side * w0;
                var c = pts[i + 1] + side * w1; var d = pts[i + 1] - side * w1;
                void V(Vector3 p, float u, float vv) { st.SetNormal((Vector3.Up * 0.85f + dir * 0.15f).Normalized()); st.SetUV(new Vector2(u, vv)); st.AddVertex(p); }
                V(a, 0, t0); V(bb, 1, t0); V(c, 1, t1);
                V(a, 0, t0); V(c, 1, t1); V(d, 0, t1);
            }
        }
        return st.Commit();
    }

    private static Shader? _bladeShader;

    /// <summary>Capim: base escura (sombra de contato), ponta clara, variação por touceira, vento só na ponta,
    /// e some aos poucos com a distância (pontilhado) para não virar "tapete" lá longe.</summary>
    private static ShaderMaterial BladeMat(Color baseC, Color tipC, float sway)
    {
        _bladeShader ??= new Shader { Code = @"
shader_type spatial;
render_mode cull_disabled;
uniform vec3 base_col = vec3(0.2, 0.3, 0.1);
uniform vec3 tip_col = vec3(0.6, 0.7, 0.3);
uniform float sway = 0.1;
varying float vari;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
void vertex() {
  vec3 o = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
  vari = hash(floor(o.xz * 3.0));
  float k = UV.y * UV.y;
  float g = sin(TIME * 1.6 + o.x * 0.25 + o.z * 0.18) * 0.7 + sin(TIME * 3.7 + o.x * 1.3) * 0.3;
  VERTEX.x += g * sway * k;
  VERTEX.z += cos(TIME * 1.2 + o.z * 0.3) * sway * 0.5 * k;
}
void fragment() {
  float d = length(VERTEX);
  float fade = 1.0 - smoothstep(38.0, 55.0, d);
  if (hash(FRAGCOORD.xy) > fade) discard;
  vec3 tip = tip_col * mix(0.85, 1.12, vari);
  vec3 c = mix(base_col, tip, smoothstep(0.0, 0.85, UV.y));
  ALBEDO = c;
  ROUGHNESS = 0.78;
  SPECULAR = 0.25;
  BACKLIGHT = tip * 0.35;
}" };
        var m = new ShaderMaterial { Shader = _bladeShader };
        m.SetShaderParameter("base_col", new Vector3(baseC.R, baseC.G, baseC.B));
        m.SetShaderParameter("tip_col", new Vector3(tipC.R, tipC.G, tipC.B));
        m.SetShaderParameter("sway", sway);
        return m;
    }

    private static Shader? _foliageShader;

    /// <summary>Folhagem com recorte por transparência, dos dois lados, balançando no vento (só as pontas).</summary>
    private static ShaderMaterial FoliageMat(Texture2D tex, Color tint, float sway)
    {
        _foliageShader ??= new Shader { Code = @"
shader_type spatial;
render_mode cull_disabled;
uniform sampler2D tex : source_color, filter_linear_mipmap, repeat_disable;
uniform vec3 tint = vec3(1.0);
uniform float sway = 0.1;
void vertex() {
  vec3 w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
  float top = VERTEX.y;
  VERTEX.x += sin(TIME * 1.7 + w.x * 0.3 + w.z * 0.2) * sway * top;
  VERTEX.z += cos(TIME * 1.3 + w.z * 0.3) * sway * 0.6 * top;
}
void fragment() {
  vec4 c = texture(tex, UV);
  if (c.a < 0.45) discard;
  ALBEDO = c.rgb * tint * (0.65 + 0.35 * (1.0 - UV.y));
  ROUGHNESS = 0.9;
  BACKLIGHT = vec3(0.25, 0.3, 0.15);
}" };
        var m = new ShaderMaterial { Shader = _foliageShader };
        m.SetShaderParameter("tex", tex);
        m.SetShaderParameter("tint", new Vector3(tint.R, tint.G, tint.B));
        m.SetShaderParameter("sway", sway);
        return m;
    }

    /// <summary>
    /// Terreno dos mapas abertos (refeito 09/11: "ondas iguais, riscos de encontro de blocos, cara de Paint"):
    /// - texturas sem repetição (amostragem com deslocamento por ruído: nenhum ladrilho se repete);
    /// - 3 tons de chão em manchas grandes + relevo macro de luz (dunas suaves, sem mexer na altura de quem anda);
    /// - areia: marcas de vento geradas por ruído, com direção e espaçamento que mudam pelo mapa e só em manchas;
    /// - máscara do mapa (ground_mask): R = estradas/trilhas pisadas (borda irregular, marcas de roda),
    ///   G = sombra de contato junto de árvores, rochas e muros, B = chão pisado em acampamentos e na área segura.
    /// </summary>
    private const string TerrainShader = @"
shader_type spatial;
render_mode cull_back;
uniform sampler2D tex_a : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D tex_b : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D nrm_a : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D nrm_b : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D ground_mask : filter_linear, repeat_disable;
uniform float mask_size = 256.0;
uniform float tile_a = 7.0; uniform float tile_b = 7.0;
uniform vec3 tint_a = vec3(1.0); uniform vec3 tint_b = vec3(1.0);
uniform vec3 tone_warm = vec3(1.0); uniform vec3 tone_cool = vec3(1.0); uniform vec3 tone_pale = vec3(1.0);
uniform vec3 path_col = vec3(0.55, 0.45, 0.35);
uniform float mix_amt = 0.4;
uniform float ripple_amt = 0.0;
uniform float tex_detail = 1.0;
uniform bool b_notile = true;   // 11/11: lajes de cal (rachaduras) sem a mistura que dobrava as linhas (quadrado dentro de quadrado)
uniform vec3 avg_a = vec3(0.6); uniform vec3 avg_b = vec3(0.5);
uniform float cloud_shadow = 0.0;          // sombra das nuvens passando (0 = desligada; o cliente sobe de dia)
uniform vec2 wind = vec2(1.6, 0.7);
varying vec3 wp;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y); }
// 13/11 (o chão ainda tinha quadrados trocando de tom de forma abrupta, textura reta e risquinhos de longe): o ruído
// gira a cada oitava (o de valor puro desenhava manchas alinhadas em quadrado) e a textura é a mistura de três leituras
// giradas/deslocadas com pesos macios (sem região com borda), que some para a cor média longe da câmera (sem moiré)
const mat2 ROT = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
float fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int i = 0; i < 5; i++) { v += a * vnoise(p); p = ROT * p * 2.03 + 17.0; a *= 0.5; } return v; }
float macio(vec2 p) { return vnoise(ROT * p) * 0.6 + vnoise(ROT * ROT * p * 1.9 + 4.7) * 0.4; }
vec4 notile(sampler2D t, vec2 uv) {
  vec2 dx = dFdx(uv); vec2 dy = dFdy(uv);
  mat2 r1 = mat2(vec2(0.62, 0.78), vec2(-0.78, 0.62));
  mat2 r2 = mat2(vec2(-0.29, 0.96), vec2(-0.96, -0.29));
  vec4 a = textureGrad(t, uv, dx, dy);
  vec4 b = textureGrad(t, r1 * uv * 1.13 + vec2(0.37, 0.71), r1 * dx * 1.13, r1 * dy * 1.13);
  vec4 c = textureGrad(t, r2 * uv * 0.91 + vec2(0.53, 0.19), r2 * dx * 0.91, r2 * dy * 0.91);
  float wb = smoothstep(0.3, 0.7, macio(uv * 0.21));
  float wc = smoothstep(0.35, 0.75, macio(uv * 0.17 + 9.3));
  vec4 m = mix(mix(a, b, wb), c, wc * 0.6);
  // mantém o contraste (a média de leituras desbota): puxa de volta pela variância perdida
  vec4 media = (a + b + c) / 3.0;
  return clamp(media + (m - media) * 1.35, 0.0, 1.0);
}
uniform float relevo = 0.0;   // 12/11: ondulação de teste (só no Laboratório) — mesma conta do Relevo.Altura (C#)
uniform float relevo_meia = 1000.0;
float relevo_h(vec2 p) {
  float f = clamp((relevo_meia - max(abs(p.x), abs(p.y))) / 20.0, 0.0, 1.0);
  return relevo * f * (0.55 * sin(p.x * 0.061 + 1.7) * sin(p.y * 0.047 + 0.4)
       + 0.30 * sin((p.x * 0.8 + p.y * 0.6) * 0.093 + 2.1)
       + 0.15 * sin((-0.5 * p.x + 0.87 * p.y) * 0.19 + 0.9));
}
uniform vec4 elev_r[32];   // 13/11: morros (centro x,z; meia largura, meia altura) — conta igual à EscadaCasa.Elev (C#)
uniform vec2 elev_h[32];   // (altura, rampa)
uniform int elev_n = 0;
float elev_hgt(vec2 p) {
  float best = 0.0;
  for (int i = 0; i < 32; i++) {
    if (i >= elev_n) break;
    vec2 d2 = max(abs(p - elev_r[i].xy) - elev_r[i].zw, vec2(0.0));
    float d = length(d2);
    best = max(best, elev_h[i].x * (1.0 - smoothstep(0.0, elev_h[i].y, d)));
  }
  return best;
}
void vertex() {
  if (elev_n > 0) {
    vec2 q = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    VERTEX.y += elev_hgt(q);
    float e = 0.4;
    NORMAL = normalize(vec3(elev_hgt(q - vec2(e, 0.0)) - elev_hgt(q + vec2(e, 0.0)), 2.0 * e, elev_hgt(q - vec2(0.0, e)) - elev_hgt(q + vec2(0.0, e))));
  }
  if (relevo > 0.0) {
    vec2 q = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    VERTEX.y += relevo_h(q);
    float e = 0.3;
    NORMAL = normalize(vec3(relevo_h(q - vec2(e, 0.0)) - relevo_h(q + vec2(e, 0.0)), 2.0 * e, relevo_h(q - vec2(0.0, e)) - relevo_h(q + vec2(0.0, e))));
  }
  wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
void fragment() {
  vec2 p = wp.xz;
  vec2 muv = p / mask_size + 0.5;
  float inside = step(0.0, muv.x) * step(muv.x, 1.0) * step(0.0, muv.y) * step(muv.y, 1.0);
  vec3 mk = texture(ground_mask, clamp(muv, 0.0, 1.0)).rgb * inside;

  // ---- tons grandes: três cores do chão em manchas de 20–80 m ----
  float t1 = fbm(p * 0.018); float t2 = fbm(p * 0.045 + 31.0);
  vec3 tone = mix(tone_warm, tone_cool, smoothstep(0.35, 0.65, t1));
  tone = mix(tone, tone_pale, smoothstep(0.55, 0.75, t2) * 0.7);

  // ---- texturas (sem repetição) ----
  vec3 a = notile(tex_a, p / tile_a).rgb;
  vec3 b = b_notile ? notile(tex_b, p / tile_b).rgb : texture(tex_b, p / tile_b).rgb;
  float la = dot(a, vec3(0.333)); float lb = dot(b, vec3(0.333));
  // na areia a textura entra só como grão (sem as listras e manchas dela); no resto, cor cheia
  vec3 avg = avg_a;   // cor média da textura (calculada no cliente)
  float lavg = max(dot(avg, vec3(0.333)), 0.05);
  vec3 ca = mix(avg, a, tex_detail) * mix(1.0, la / lavg, 0.35 * (1.0 - tex_detail)) * tint_a;
  vec3 cb = b * tint_b;
  float m = smoothstep(0.5 - 0.1, 0.5 + 0.1, fbm(p * 0.035 + 7.0) + (mix_amt - 0.5) * 0.7) * step(0.01, mix_amt);
  vec3 col = mix(ca * tone, cb * mix(vec3(1.0), tone, 0.5), m);
  // longe da câmera a textura fina vira a cor média (os grãos/linhas formavam riscos e moiré)
  // 16/11 (dono: «ondulações que dá pra ver de longe», com print da Costa): os grãos e o relevo do mapa normal viravam anéis
  // de moiré a 20-60 m. Agora a textura fina some mais cedo e por inteiro, e o mapa normal também se apaga com a distância
  float longe = smoothstep(10.0, 34.0, distance(CAMERA_POSITION_WORLD, wp));
  col = mix(col, mix(avg_a * tint_a * tone, avg_b * tint_b * mix(vec3(1.0), tone, 0.5), m) * 1.02, longe);
  vec3 nrm = mix(notile(nrm_a, p / tile_a).rgb, b_notile ? notile(nrm_b, p / tile_b).rgb : texture(nrm_b, p / tile_b).rgb, m);
  nrm = mix(vec3(0.5, 0.5, 1.0), nrm, mix(1.0, 0.45, ripple_amt) * (1.0 - longe));

  // ---- marcas de vento (areia): direção e espaçamento variam; só em manchas, somem longe (sem serrilhado) ----
  vec2 slope = vec2(0.0);
  if (ripple_amt > 0.0) {
    float ang = fbm(p * 0.008) * 3.5;
    vec2 dir = vec2(cos(ang), sin(ang));
    float freq = 6.2831 / mix(0.22, 0.4, vnoise(p * 0.03 + 5.0));
    float warp = fbm(p * 0.25) * 5.0;
    float ph = dot(p, dir) * freq + warp;
    float aa = clamp(1.0 - fwidth(ph) * 1.2, 0.0, 1.0) * (1.0 - smoothstep(8.0, 22.0, distance(CAMERA_POSITION_WORLD, wp)));
    float patch = smoothstep(0.52, 0.74, fbm(p * 0.02 + 3.0)) * (1.0 - mk.r) * (1.0 - mk.b * 0.8);
    float amp = ripple_amt * patch * aa;
    float w = sin(ph); float crest = pow(0.5 + 0.5 * w, 3.0);
    slope = dir * cos(ph) * amp * 0.16;
    col *= 1.0 + (crest - 0.35) * 0.04 * amp;
  }
  // ---- relevo macro (dunas suaves só na luz) ----
  float e = 0.8;
  float h0 = fbm(p * 0.03); float hx = fbm((p + vec2(e, 0.0)) * 0.03); float hz = fbm((p + vec2(0.0, e)) * 0.03);
  slope += vec2(hx - h0, hz - h0) / e * 6.0;

  // ---- trilhas pisadas: borda irregular, cor de terra batida, marcas de roda no meio ----
  float edge = (fbm(p * 0.5) - 0.5) * 0.3 + (vnoise(p * 2.3) - 0.5) * 0.08;
  float road = smoothstep(0.58, 0.72, mk.r + edge);
  vec3 dirt = path_col * clamp(0.8 + 0.4 * lb / max(dot(avg_b, vec3(0.333)), 0.05), 0.6, 1.3) * mix(vec3(1.0), tone, 0.3);
  col = mix(col, dirt, road * 0.92);
  float ruts = smoothstep(0.93, 1.0, mk.r) * (0.5 + 0.5 * sin(p.x * 3.0 + p.y * 2.0 + fbm(p) * 6.0));
  col *= 1.0 - ruts * road * 0.06;
  // chão pisado nos acampamentos e na área segura
  float trod = smoothstep(0.3, 0.7, mk.b + edge * 0.6);
  col = mix(col, dirt * 1.05, trod * 0.45);
  // sombra de contato junto de árvores, rochas e muros
  col *= 1.0 - mk.g * 0.22;
  // sombra de nuvem: manchas grandes e macias que atravessam o mapa com o vento (o céu ganha vida no chão)
  if (cloud_shadow > 0.0) {
    vec2 q = (p - wind * TIME) * 0.011;
    float cs = vnoise(q) * 0.65 + vnoise(q * 2.3 + 9.0) * 0.35;
    col *= 1.0 - smoothstep(0.5, 0.72, cs) * cloud_shadow;
  }

  ALBEDO = col;
  NORMAL_MAP = vec3(clamp(nrm.xy - slope * 0.5 * (1.0 - road * 0.7), 0.0, 1.0), nrm.z);
  NORMAL_MAP_DEPTH = 1.0;
  ROUGHNESS = 0.97;
  SPECULAR = mix(0.25, 0.08, road);
}";

    /// <summary>Cor média de uma textura (reduzida a 1 pixel) — base do chão sem as manchas da textura.</summary>
    private static Vector3 AverageColor(Texture2D t)
    {
        var img = t.GetImage();
        if (img is null) return new Vector3(0.6f, 0.55f, 0.45f);
        img = (Image)img.Duplicate();
        if (img.IsCompressed()) img.Decompress();
        // média de verdade (grade de 48×48 amostras): redimensionar para 1 pixel pega só alguns pontos
        double r = 0, g = 0, b = 0; var n = 0;
        int w = img.GetWidth(), h = img.GetHeight();
        for (var j = 0; j < 48; j++)
            for (var i = 0; i < 48; i++)
            {
                var c = img.GetPixel(i * w / 48, j * h / 48);
                r += c.R; g += c.G; b += c.B; n++;
            }
        return new Vector3((float)(r / n), (float)(g / n), (float)(b / n));
    }

    private bool TerrainMaterialAvailable() => Map.Theme is "praia" or "vale" or "mata" or "brejo" or "canion" or "lava" or "neve" or "acropole" or "fantasma";

    /// <summary>Terreno por região: (textura principal, secundária, tons e quanto da secundária aparece).</summary>
    private ShaderMaterial? TerrainMaterial()
    {
        (string A, Color TA, string B, Color TB, float Mix)? cfg = Map.Theme switch
        {
            // 12/11 ("o chão da praia está feio e quadriculado"): as lajes de cal (quadradas) saíram — areia com manchas de
            // pedra/cascalho sem ladrilho visível
            // (17:47: "ainda está cheio de formas pretas") — a pedra escura em manchas piorou; agora só areia, sem segunda
            // textura, e a sombra de contato só sai de pedra/árvore redonda (antes saía das caixas invisíveis e dos muros)
            "praia" => ("ter_areia.png", new Color(0.9f, 0.88f, 0.84f), "ter_costa_cal.png", new Color(0.86f, 0.84f, 0.8f), 0f),
            "vale" => ("ter_grama.png", new Color(1.05f, 1f, 0.9f), "ter_terra.png", Colors.White, 0.45f),
            "mata" => ("ter_grama.png", new Color(0.8f, 0.9f, 0.8f), "ter_terra.png", new Color(0.8f, 0.8f, 0.78f), 0.35f),
            "brejo" => ("ter_grama.png", new Color(0.7f, 0.8f, 0.68f), "ter_terra.png", new Color(0.62f, 0.62f, 0.6f), 0.55f),
            "canion" => ("ter_terra.png", new Color(1.25f, 0.98f, 0.82f), "ter_pedra.png", new Color(1.15f, 0.95f, 0.85f), 0.5f),
            "lava" => ("ter_pedra.png", new Color(0.55f, 0.45f, 0.42f), "ter_terra.png", new Color(0.45f, 0.32f, 0.28f), 0.4f),
            "neve" => ("ter_areia.png", new Color(1.45f, 1.5f, 1.62f), "ter_pedra.png", new Color(1.2f, 1.25f, 1.35f), 0.25f),
            "acropole" => ("ter_pedra.png", new Color(1.1f, 1.0f, 0.9f), "ter_terra.png", new Color(1.1f, 1.0f, 0.9f), 0.45f),
            "fantasma" => ("ter_terra.png", new Color(0.75f, 0.8f, 0.85f), "ter_pedra.png", new Color(0.75f, 0.8f, 0.88f), 0.4f),
            _ => null,
        };
        if (cfg is not { } c) return null;
        var ta = VisualAssets.Load(c.A); var tb = VisualAssets.Load(c.B);
        if (ta is null || tb is null) return null;
        var m = _terrainMat = new ShaderMaterial { Shader = new Shader { Code = TerrainShader } };
        m.SetShaderParameter("tex_a", ta); m.SetShaderParameter("tex_b", tb);
        if (VisualAssets.Normal(c.A) is { } na) m.SetShaderParameter("nrm_a", na);
        if (VisualAssets.Normal(c.B) is { } nb) m.SetShaderParameter("nrm_b", nb);
        m.SetShaderParameter("tint_a", new Vector3(c.TA.R, c.TA.G, c.TA.B));
        m.SetShaderParameter("tint_b", new Vector3(c.TB.R, c.TB.G, c.TB.B));
        m.SetShaderParameter("mix_amt", c.Mix);
        m.SetShaderParameter("avg_a", AverageColor(ta)); m.SetShaderParameter("avg_b", AverageColor(tb));
        var sand = Map.Theme is "praia" or "neve";
        // praia: a areia nova (kit da Costa) já tem ondulação e cascalho; lajes de cal maiores para não repetir
        var praia = Map.Theme == "praia";
        m.SetShaderParameter("tile_a", praia ? 6f : sand ? 3.5f : 6f); m.SetShaderParameter("tile_b", praia ? 12f : 6f);
        m.SetShaderParameter("b_notile", true);
        m.SetShaderParameter("ripple_amt", OS.GetEnvironment("CDM_SEM_ONDA") == "1" ? 0f : praia ? 0.08f : Map.Theme == "neve" ? 0.25f : 0f);
        m.SetShaderParameter("tex_detail", praia ? 1f : sand ? 0.35f : 1f);
        Vector3 V(float r, float g, float b) => new(r, g, b);
        var (warm, cool, pale, path) = Map.Theme switch
        {
            "praia" => (V(1.03f, 0.99f, 0.93f), V(0.93f, 0.93f, 0.94f), V(1.06f, 1.04f, 1.0f), V(0.42f, 0.36f, 0.29f)),
            "neve" => (V(1.0f, 1.0f, 1.02f), V(0.92f, 0.95f, 1.02f), V(1.05f, 1.05f, 1.06f), V(0.55f, 0.52f, 0.50f)),
            "canion" or "acropole" => (V(1.06f, 0.96f, 0.86f), V(0.92f, 0.88f, 0.86f), V(1.1f, 1.04f, 0.96f), V(0.50f, 0.40f, 0.32f)),
            "lava" => (V(1.05f, 0.92f, 0.88f), V(0.85f, 0.85f, 0.88f), V(1.0f, 0.96f, 0.94f), V(0.30f, 0.24f, 0.22f)),
            _ => (V(1.05f, 1.0f, 0.9f), V(0.88f, 0.94f, 0.92f), V(1.08f, 1.06f, 0.96f), V(0.42f, 0.34f, 0.26f)),
        };
        m.SetShaderParameter("tone_warm", warm); m.SetShaderParameter("tone_cool", cool);
        m.SetShaderParameter("tone_pale", pale); m.SetShaderParameter("path_col", path);
        var size = (float)Map.HalfSize * 2f + 30f;
        m.SetShaderParameter("mask_size", size);
        m.SetShaderParameter("ground_mask", BuildGroundMask(size));
        return m;
    }

    /// <summary>
    /// Máscara do chão (uma imagem do mapa visto de cima, ~0,5 m por pixel): R = estradas (rampa de 3 m para fora da borda,
    /// que o shader recorta com ruído = borda irregular), G = sombra de contato em volta de árvores/rochas/muros,
    /// B = chão pisado (acampamentos/refúgios e área segura).
    /// </summary>
    private ImageTexture BuildGroundMask(float size)
    {
        // 13/11 ("o chão ainda tem quadradinhos"): 3 pixels por metro (era 2, até 1024) — a borda das trilhas não serrilha mais
        var res = Mathf.Clamp((int)(size * 3f), 256, 2048);
        var px = size / res;
        var r = new float[res * res]; var g = new float[res * res]; var b = new float[res * res];
        int Ix(double w) => (int)((w / size + 0.5) * res);
        double Wx(int i) => (i + 0.5) / res * size - size / 2.0;
        void Splat(float[] ch, double x0, double y0, double x1, double y1, Func<double, double, float> val)
        {
            int ia = Math.Max(0, Ix(x0)), ib = Math.Min(res - 1, Ix(x1)), ja = Math.Max(0, Ix(y0)), jb = Math.Min(res - 1, Ix(y1));
            for (var j = ja; j <= jb; j++)
                for (var i = ia; i <= ib; i++)
                {
                    var v = val(Wx(i), Wx(j));
                    if (v > ch[j * res + i]) ch[j * res + i] = v;
                }
        }
        foreach (var road in Map.Roads)
        {
            var hw = road.Width / 2.0;
            for (var k = 0; k + 1 < road.Points.Count; k++)
            {
                double ax = road.Points[k][0], ay = road.Points[k][1], bx = road.Points[k + 1][0], by = road.Points[k + 1][1];
                var pad = hw + 3.5;
                Splat(r, Math.Min(ax, bx) - pad, Math.Min(ay, by) - pad, Math.Max(ax, bx) + pad, Math.Max(ay, by) + pad, (x, y) =>
                {
                    var dx = bx - ax; var dy = by - ay; var l2 = dx * dx + dy * dy;
                    var t = l2 < 1e-9 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / l2, 0, 1);
                    var d = Math.Sqrt(Math.Pow(x - ax - t * dx, 2) + Math.Pow(y - ay - t * dy, 2));
                    return (float)Math.Clamp(1.0 - (d - hw) / 3.0 * 0.4, 0, 1) * (d < hw ? 1f : (float)Math.Clamp(1.0 - (d - hw) / 3.0, 0, 1) * 0.7f + 0f);
                });
            }
        }
        foreach (var o in Map.Obstacles)
        {
            if (!o.Solid && o.Kind != "arvore") continue;
            // 12/11 ("o chão está cheio de formas pretas sobrepostas, quadrados e retângulos compridos"): a sombra de contato
            // saía também das caixas de colisão invisíveis (casas do kit, mesas, carroças) e dos muros/penhascos longos —
            // virava retângulo escuro no chão. Agora só pedra e árvore (redondas) fazem sombra, e mais suave.
            if (o.Shape != "circle" || o.Kind is "invisivel" or "lago" or "ravina") continue;
            if (o.Shape == "circle")
            {
                var rr = o.R + 1.6;
                Splat(g, o.X - rr, o.Y - rr, o.X + rr, o.Y + rr, (x, y) =>
                {
                    var d = Math.Sqrt((x - o.X) * (x - o.X) + (y - o.Y) * (y - o.Y));
                    return (float)Math.Clamp(1.0 - (d - o.R * 0.6) / 1.6, 0, 1) * (o.Kind == "arvore" ? 0.8f : 1f);
                });
            }
            else
            {
                double hx = o.W / 2, hy = o.H / 2;
                Splat(g, o.X - hx - 1.2, o.Y - hy - 1.2, o.X + hx + 1.2, o.Y + hy + 1.2, (x, y) =>
                {
                    var dx = Math.Max(0, Math.Abs(x - o.X) - hx); var dy = Math.Max(0, Math.Abs(y - o.Y) - hy);
                    return (float)Math.Clamp(1.0 - Math.Sqrt(dx * dx + dy * dy) / 1.2, 0, 1) * 0.85f;
                });
            }
        }
        foreach (var z in Map.Zones)
        {
            if (z.Kind != "refugio") continue;
            double hx = z.W / 2, hy = z.H / 2;
            Splat(b, z.X - hx, z.Y - hy, z.X + hx, z.Y + hy, (x, y) =>
            {
                var ex = Math.Abs(x - z.X) / hx; var ey = Math.Abs(y - z.Y) / hy;
                return (float)Math.Clamp(1.0 - Math.Max(ex * ex + ey * ey - 0.3, 0) * 1.6, 0, 1);
            });
        }
        if (Map.SafeRadius is > 0 and < 20)
        {
            var sr = Map.SafeRadius + 2;
            Splat(b, Map.SpawnX - sr, Map.SpawnY - sr, Map.SpawnX + sr, Map.SpawnY + sr, (x, y) =>
                (float)Math.Clamp(1.0 - Math.Sqrt((x - Map.SpawnX) * (x - Map.SpawnX) + (y - Map.SpawnY) * (y - Map.SpawnY)) / sr, 0, 1) * 0.9f);
        }
        var bytes = new byte[res * res * 3];
        for (var i = 0; i < res * res; i++)
        {
            bytes[i * 3] = (byte)(r[i] * 255); bytes[i * 3 + 1] = (byte)(g[i] * 255); bytes[i * 3 + 2] = (byte)(b[i] * 255);
        }
        _ = px;
        return ImageTexture.CreateFromImage(Image.CreateFromData(res, res, false, Image.Format.Rgb8, bytes));
    }

    /// <summary>Estradas de terra (Tools/povoar_mapas.py): faixa contínua pelo traçado, bordas mais claras e marcas de roda.</summary>
    private void AddRoads()
    {
        if (Map.Roads.Count == 0) return;
        // terreno aberto: a estrada é pintada no próprio chão (máscara do TerrainShader), com borda irregular
        if (TerrainMaterialAvailable()) return;
        var dirt = TexMat("ter_terra.png", new Color(0.95f, 0.88f, 0.78f), 0.25f);
        dirt.Roughness = 1f;
        VisualAssets.AddRelief(dirt, "ter_terra.png", 1.2f);
        var edge = TexMat("ter_terra.png", new Color(0.8f, 0.75f, 0.65f), 0.25f);
        edge.Roughness = 1f;
        foreach (var road in Map.Roads)
        {
            if (road.Points.Count < 2) continue;
            Strip(road, (float)road.Width, 0.03f, dirt);
            Strip(road, (float)road.Width + 0.7f, 0.022f, edge);
        }

        void Strip(MapRoad road, float width, float y, Material mat)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            var pts = road.Points.Select(p => new Vector2((float)p[0], (float)p[1])).ToList();
            var dist = 0f;
            for (var i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var dir = (b - a).Normalized();
                var n = new Vector2(-dir.Y, dir.X) * width / 2f;
                // emenda: usa a média das normais nos pontos do meio (sem fresta nas curvas)
                Vector2 Nrm(int k)
                {
                    var d0 = k > 0 ? (pts[k] - pts[k - 1]).Normalized() : dir;
                    var d1 = k < pts.Count - 1 ? (pts[k + 1] - pts[k]).Normalized() : dir;
                    var d = (d0 + d1).Normalized();
                    return new Vector2(-d.Y, d.X) * width / 2f;
                }
                var na = Nrm(i); var nb = Nrm(i + 1);
                var len = a.DistanceTo(b);
                var q = new[] { a + na, a - na, b + nb, b - nb };
                var uv = new[] { new Vector2(0, dist / 4f), new Vector2(width / 4f, dist / 4f), new Vector2(0, (dist + len) / 4f), new Vector2(width / 4f, (dist + len) / 4f) };
                foreach (var k in new[] { 0, 2, 1, 1, 2, 3 })
                {
                    st.SetNormal(Vector3.Up);
                    st.SetUV(uv[k]);
                    st.AddVertex(new Vector3(q[k].X, y, q[k].Y));
                }
                dist += len;
                _ = n;
            }
            var mi = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            Root.AddChild(mi);
        }
    }

    /// <summary>Poças rasas no calçamento (prancha 01): refletem céu e lanternas; reflexo de tela no gráfico alto.</summary>
    private void AddPuddles(float size)
    {
        var mat = new StandardMaterial3D
        {
            // película de água: deixa ver a pedra por baixo e só escurece/espelha
            AlbedoColor = new Color(0.05f, 0.05f, 0.07f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.03f,
            MetallicSpecular = 1f,
        };
        var half = size / 2f - 20f;
        var placed = 0;
        for (var tries = 0; tries < 400 && placed < 10; tries++)
        {
            var x = R(-half, half); var y = R(-half, half);
            if (!Clear(x, y, 1.6f)) continue;
            var puddle = new Node3D { Position = P(x, y, 0.012f), RotationDegrees = new Vector3(0, R(0, 360), 0) };
            Root.AddChild(puddle);
            // forma irregular: 3 discos achatados sobrepostos
            for (var i = 0; i < 3; i++)
                VisualKit.Part(puddle, new CylinderMesh { TopRadius = R(0.25f, 0.55f), BottomRadius = 0.55f, Height = 0.004f, RadialSegments = 14 }, mat,
                    new Vector3(R(-0.5f, 0.5f), 0.002f * i, R(-0.4f, 0.4f)), null, new Vector3(1f, 1f, R(0.5f, 0.9f)));
            placed++;
        }
    }

    /// <summary>Ponto livre de paredes/obstáculos/adereços (para enfeites que não podem atravessar nada).</summary>
    private bool OnRoad(float x, float y)
    {
        foreach (var road in Map.Roads)
            for (var i = 0; i < road.Points.Count - 1; i++)
            {
                var a = new Vector2((float)road.Points[i][0], (float)road.Points[i][1]);
                var b = new Vector2((float)road.Points[i + 1][0], (float)road.Points[i + 1][1]);
                var p = new Vector2(x, y);
                var ab = b - a;
                var t = ab.LengthSquared() < 1e-6f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
                if (p.DistanceTo(a + ab * t) < road.Width / 2f + 0.3f) return true;
            }
        return false;
    }

    private bool Clear(float x, float y, float r)
    {
        foreach (var o in Map.Obstacles)
        {
            var hw = o.Shape == "circle" ? o.R : o.W / 2; var hh = o.Shape == "circle" ? o.R : o.H / 2;
            if (x > o.X - hw - r && x < o.X + hw + r && y > o.Y - hh - r && y < o.Y + hh + r) return false;
        }
        foreach (var p in Map.Props)
            if ((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) < (r + 1.5) * (r + 1.5)) return false;
        foreach (var n in Map.Npcs)
            if ((n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y) < (r + 1.5) * (r + 1.5)) return false;
        return true;
    }

    private static ShaderMaterial? _flameMat;
    private const string FlameShader = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
instance uniform float fase = 0.0;
instance uniform float azul = 0.0;   // 13/11: chama azul da pedra de viagem
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vn(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
void vertex() {
    // sempre de frente para a câmera, só girando em Y (a chama não deita)
    vec3 cam = (INV_VIEW_MATRIX[3].xyz - MODEL_MATRIX[3].xyz);
    vec3 fw = normalize(vec3(cam.x, 0.0, cam.z));
    vec3 rt = normalize(cross(vec3(0, 1, 0), fw));
    vec3 w = MODEL_MATRIX[3].xyz + rt * VERTEX.x * length(MODEL_MATRIX[0].xyz) + vec3(0, 1, 0) * VERTEX.y * length(MODEL_MATRIX[1].xyz);
    POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(w, 1.0);
}
void fragment() {
    vec2 uv = vec2(UV.x - 0.5, 1.0 - UV.y);           // 0 na base, 1 na ponta
    float t = TIME * 1.7 + fase;
    float n = vn(vec2(uv.x * 4.0, uv.y * 3.0 - t * 2.2)) * 0.6 + vn(vec2(uv.x * 9.0 + 3.0, uv.y * 7.0 - t * 3.4)) * 0.4;
    float largura = 0.42 * (1.0 - uv.y) * (0.55 + 0.45 * smoothstep(0.0, 0.25, uv.y));
    float x = abs(uv.x + (n - 0.5) * 0.16 * uv.y);
    float corpo = smoothstep(largura, largura * 0.35, x) * smoothstep(1.0, 0.55, uv.y + (n - 0.5) * 0.35);
    float miolo = smoothstep(largura * 0.55, 0.0, x) * smoothstep(0.65, 0.1, uv.y);
    vec3 cor = mix(vec3(0.9, 0.18, 0.03), vec3(1.0, 0.62, 0.15), corpo);
    cor = mix(cor, vec3(1.0, 0.95, 0.75), miolo);
    vec3 corAzul = mix(mix(vec3(0.05, 0.22, 0.9), vec3(0.25, 0.65, 1.0), corpo), vec3(0.85, 0.95, 1.0), miolo);
    cor = mix(cor, corAzul, azul);
    ALBEDO = cor * 1.6;
    ALPHA = clamp(corpo * (0.75 + 0.25 * n), 0.0, 1.0);
}";

    internal void AddFlame(Node3D parent, Vector3 at, float size, float energy, float range) => AddFlame(parent, at, size, energy, range, false);

    internal void AddFlame(Node3D parent, Vector3 at, float size, float energy, float range, bool azul)
    {
        // 13/11 ("o fogo da tocha parece um cone com luz mexendo pra cima e pra baixo"): chama de verdade — um véu sempre de
        // frente para a câmera com fogo procedural (línguas subindo, miolo claro, ponta vermelha) + faíscas subindo
        var flame = VisualKit.Joint(parent, at);
        _flameMat ??= new ShaderMaterial { Shader = new Shader { Code = FlameShader } };
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(0.42f * size, 0.75f * size) }, MaterialOverride = _flameMat,
            Position = new Vector3(0, 0.3f * size, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        q.SetInstanceShaderParameter("fase", R(0, 10));
        q.SetInstanceShaderParameter("azul", azul ? 1f : 0f);
        flame.AddChild(q);
        if (size >= 1f)
            flame.AddChild(new CpuParticles3D
            {
                Amount = (int)(5 * size), Lifetime = 1.1f, Position = new Vector3(0, 0.35f * size, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
                EmissionSphereRadius = 0.06f * size, Direction = Vector3.Up, Spread = 18f, Gravity = new Vector3(0, 0.9f, 0),
                InitialVelocityMin = 0.25f, InitialVelocityMax = 0.6f, ScaleAmountMin = 0.012f, ScaleAmountMax = 0.03f, LocalCoords = false,
                Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 4, Rings = 2, Material = VisualKit.Glow(azul ? new Color(0.35f, 0.7f, 1f) : new Color(1f, 0.6f, 0.2f), 3f) },
            });
        var light = new OmniLight3D { LightColor = azul ? new Color(0.4f, 0.65f, 1f) : new Color(1f, 0.62f, 0.3f), LightEnergy = energy, OmniRange = range, LightVolumetricFogEnergy = 1.5f, Position = at + new Vector3(0, 0.5f, 0) };
        parent.AddChild(light);
        var phase = R(0, 10);
        _flames.Add((light, phase, energy));
    }

    // remendo de chão (trilha de lajes ou canteiro de grama) com borda irregular, por cima do calçamento
    private const string PatchShader = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back;
uniform sampler2D tex : source_color, filter_linear_mipmap, repeat_enable;
uniform vec3 tint = vec3(1.0); uniform float tile = 3.0; uniform float edge = 0.35; uniform float seed = 0.0; uniform bool round_shape = false;
varying vec3 wp;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y); }
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
  vec2 c = UV - 0.5;
  float d = round_shape ? length(c) * 2.0 : abs(c.y) * 2.0;           // 0 no meio, 1 na borda
  if (!round_shape) d = max(d, smoothstep(0.42, 0.5, abs(c.x)) );      // pontas da trilha se desfazem
  float n = vnoise(wp.xz * 1.3 + seed) * 0.6 + vnoise(wp.xz * 4.0 + seed * 3.0) * 0.4;
  float a = 1.0 - smoothstep(1.0 - edge, 1.0, d + (n - 0.5) * edge * 1.6);
  if (a < 0.02) discard;
  vec3 col = texture(tex, wp.xz / tile).rgb * tint * (0.88 + 0.24 * vnoise(wp.xz * 0.4 + seed));
  ALBEDO = col; ALPHA = a; ROUGHNESS = 0.95;
}";

    /// <summary>
    /// Vila mais viva (pedido 10/11: "caminhos na vila, plantas, canteiros ao redor de algumas casas, textura"): trilhas de
    /// lajes claras ligando o portão, a praça e as portas; manchas de grama junto dos muros e das casas; canteiros de flores
    /// com borda de madeira e vasos nas portas. Só visual (nada disso bloqueia passagem).
    /// </summary>
    private void AddVillageDressing()
    {
        var shader = new Shader { Code = PatchShader };
        ShaderMaterial Patch(string texture, Color tint, float tile, bool round, float seed)
        {
            var m = new ShaderMaterial { Shader = shader, RenderPriority = 1 };
            if (VisualAssets.Load(texture) is { } t) m.SetShaderParameter("tex", t);
            m.SetShaderParameter("tint", new Vector3(tint.R, tint.G, tint.B));
            m.SetShaderParameter("tile", tile); m.SetShaderParameter("round_shape", round); m.SetShaderParameter("seed", seed);
            m.SetShaderParameter("edge", round ? 0.45f : 0.3f);
            return m;
        }
        void Strip(Vector2 a, Vector2 b, float width, Material mat, float y)
        {
            var len = a.DistanceTo(b);
            var mid = (a + b) / 2f;
            var mi = new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(len + width, width), Material = mat },
                Position = new Vector3(mid.X, y, mid.Y), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            mi.Rotation = new Vector3(0, -Mathf.Atan2(b.Y - a.Y, b.X - a.X), 0);
            Root.AddChild(mi);
        }
        var path = Patch("gen_bloco.png", new Color(1.05f, 1.0f, 0.92f), 2.2f, false, 1f);
        var dirt = Patch("ter_terra.png", new Color(0.95f, 0.85f, 0.72f), 4f, false, 5f);
        var grass = Patch("ter_grama.png", new Color(0.85f, 1.0f, 0.75f), 5f, true, 9f);
        // trilhas principais: portão → praça → norte; praça → casas dos lados
        var trails = new (Vector2 A, Vector2 B, float W, bool Dirt)[]
        {
            (new(0, -26), new(0, -4), 4.2f, false), (new(0, 6), new(0, 18), 3.2f, false),
            (new(-3, -1.2f), new(-21, -1.2f), 2.6f, true), (new(3, 1), new(20, 1), 2.6f, false),
            (new(0, -10), new(-20, -11.6f), 2.4f, false), (new(0, -10), new(20, -8.2f), 2.4f, false),
            (new(-2, 10), new(-14, 14), 2f, true), (new(2, 10), new(13, 14), 2f, true),
            (new(20, 5), new(20, 12), 2f, true),
        };
        // 11/11 noite ("nem avenida é totalmente reta"): cada trilha vira uma curva suave (5 pedaços com desvio de lado)
        var bend = 0;
        foreach (var (a, b, w, d) in trails)
        {
            var dir = (b - a); var side = new Vector2(-dir.Y, dir.X).Normalized();
            var amp = Mathf.Min(1.4f, dir.Length() * 0.07f) * (bend++ % 2 == 0 ? 1f : -1f);
            var prev = a;
            for (var i = 1; i <= 5; i++)
            {
                var t = i / 5f;
                var next = a + dir * t + side * amp * Mathf.Sin(Mathf.Pi * t);
                Strip(prev, next, w, d ? dirt : path, d ? 0.02f : 0.03f);
                prev = next;
            }
        }
        // manchas de grama: junto da muralha e entre as casas (quebra o calçamento igual)
        var rng = new RandomNumberGenerator { Seed = 4711 };
        var spots = new List<Vector2>
        {
            new(-27, -8), new(-27, 8), new(27, -6), new(27, 9), new(-12, 25), new(12, 25), new(0, 25.5f), new(-27, 22),
            new(26, 21.5f), new(-9, -22), new(9, -22), new(-26, -26), new(26, -26), new(-17, 10), new(16, 10),
        };
        foreach (var at in spots)
        {
            var sz = rng.RandfRange(3.5f, 6.5f);
            Root.AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(sz, sz * rng.RandfRange(0.7f, 1.2f)), Material = grass },
                Position = new Vector3(at.X, 0.025f, at.Y), RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            // capim e uma flor ou outra na grama
            for (var k = 0; k < 3; k++)
                MapKit.Glb(Root, "costa/costa_capim_" + "abc"[rng.RandiRange(0, 2)], new Vector3(at.X + rng.RandfRange(-sz, sz) * 0.3f, 0, at.Y + rng.RandfRange(-sz, sz) * 0.3f), rng.RandfRange(0, 360), rng.RandfRange(0.6f, 0.9f));
            if (rng.Randf() < 0.6f)
                MapKit.Glb(Root, "costa/costa_flores_" + (rng.Randf() < 0.5f ? "a" : "b"), new Vector3(at.X + rng.RandfRange(-1, 1), 0, at.Y + rng.RandfRange(-1, 1)), rng.RandfRange(0, 360), 0.9f);
        }
        // canteiros de flores com borda de madeira na frente das casas e vasos ao lado das portas
        var wood = TexMat("gen_madeira.png", new Color(0.55f, 0.40f, 0.28f), 1f);
        var soil = TexMat("ter_terra.png", new Color(0.45f, 0.33f, 0.24f), 1.5f);
        var pot = TexMat("gen_bloco.png", new Color(0.78f, 0.45f, 0.30f), 0.6f);
        void Bed(Vector2 at, float yaw, float len)
        {
            var node = new Node3D { Position = new Vector3(at.X, 0, at.Y), RotationDegrees = new Vector3(0, yaw, 0) };
            Root.AddChild(node);
            VisualKit.Part(node, VisualKit.Box(len, 0.25f, 0.9f), wood, new Vector3(0, 0.12f, 0));
            VisualKit.Part(node, VisualKit.Box(len - 0.12f, 0.06f, 0.78f), soil, new Vector3(0, 0.26f, 0));
            for (var x = -len / 2f + 0.45f; x < len / 2f - 0.3f; x += 0.75f)
                MapKit.Glb(node, rng.Randf() < 0.7f ? "costa/costa_flores_" + (rng.Randf() < 0.5f ? "a" : "b") : "costa/costa_arbusto_" + "abc"[rng.RandiRange(0, 2)],
                    new Vector3(x + rng.RandfRange(-0.1f, 0.1f), 0.28f, rng.RandfRange(-0.15f, 0.15f)), rng.RandfRange(0, 360), rng.RandfRange(0.5f, 0.75f));
        }
        void Pot(Vector2 at)
        {
            var node = new Node3D { Position = new Vector3(at.X, 0, at.Y) };
            Root.AddChild(node);
            VisualKit.Part(node, VisualKit.Cylinder(0.32f, 0.22f, 0.5f, 10), pot, new Vector3(0, 0.25f, 0));
            MapKit.Glb(node, "costa/costa_arbusto_" + "abc"[rng.RandiRange(0, 2)], new Vector3(0, 0.45f, 0), rng.RandfRange(0, 360), 0.45f);
        }
        // 13/11 ("no editor não consigo pegar os canteiros, vasos, o chafariz"): no Porto eles viraram peças do mapa
        // ("canteiro_flores", "vaso_planta" — Tools/ajustes_logica.py); o desenho fixo só fica para mapa que não tem as peças
        if (Map.Props.Any(p => p.Kind is "canteiro_flores" or "vaso_planta")) return;
        // fachadas viradas para a praça (sobrados e casas pequenas ao sul, sobrado do oeste, taverna e casarão ao norte)
        Bed(new(-23.6f, 13.0f), 0, 2.6f); Bed(new(-12.5f, 14.7f), 0, 2.2f); Bed(new(17.6f, 12.6f), 0, 2.4f); Bed(new(11.5f, 14.7f), 0, 2.2f);
        Bed(new(-21.6f, 3.4f), 90, 2.4f); Bed(new(-14f, -11.0f), 0, 2.8f); Bed(new(26f, -8.0f), 0, 2.6f);
        foreach (var at in new Vector2[] { new(-19.2f, 13.2f), new(-16.2f, 14.9f), new(22.3f, 12.9f), new(15.6f, 14.9f), new(-8.9f, 18.4f), new(-5.1f, 18.4f),
                                           new(5.1f, 18.4f), new(8.9f, 18.4f), new(-21.5f, -2.6f), new(-17.3f, -11.4f), new(-22.7f, -11.4f), new(17.5f, -9.2f), new(22.5f, -9.2f) })
            Pot(at);
    }

    /// <summary>Prédio com interior: some o andar de cima e o telhado quando o jogador está dentro (dá para ver o térreo).</summary>
    private readonly List<(Rect2 Inside, List<Node3D> Upper)> _interiors = new();

    /// <summary>Prédios e peças do lote 2 no Porto (colisão: obstáculos "invisivel" do servidor, gerados pelo reorganizar_porto.py).</summary>
    private void LotTwoBuilding(Node3D node, MapProp p)
    {
        var (file, inner, upper, closed) = p.Kind switch
        {
            "taverna_grande" => ("cenario/edificio_taverna_grande", new Rect2(-6f, -4.5f, 12f, 9f), new[] { "edificio_taverna_grande_superior" }, Vector3.Zero),
            "casarao_cidade" => ("cenario/edificio_casarao_cidade", new Rect2(-8f, -6f, 16f, 13.5f), new[] { "edificio_casarao_cidade_andar2", "edificio_casarao_cidade_telhado" }, Vector3.Zero),
            "sobrado" => ("cenario/cenario_casa_sobrado", new Rect2(), Array.Empty<string>(), new Vector3(5.3f, 6f, 4.8f)),
            "casa_pequena" => ("cenario/cenario_casa_pequena", new Rect2(), Array.Empty<string>(), new Vector3(4.7f, 4.5f, 4.4f)),
            "portao_fortificado" => ("cenario/edificio_portao_fortificado", new Rect2(), Array.Empty<string>(), Vector3.Zero),
            _ => ("cenario/cenario_cerca", new Rect2(), Array.Empty<string>(), Vector3.Zero),
        };
        if (Visuals.GlbCache.Instance(file) is not { } model)
        {
            // sem o modelo (editor ainda não importou): caixa simples no lugar
            if (closed != Vector3.Zero) BuildHouse(node, closed.X, closed.Z, closed.Y * 0.7f, 0);
            return;
        }
        node.AddChild(model);
        if (p.Kind == "casarao_cidade") Visuals.EscadaCasa.RegistrarCasarao(node);
        if (p.Kind == "taverna_grande")
        {
            Visuals.EscadaCasa.TirarEscadaDoModelo(model);
            // escada só onde existe o 2º andar (passagem "_subir" perto); nas tavernas das cidades novas não tem andar de cima
            if (Map.Portals.Any(pt => pt.Id.EndsWith("_subir", StringComparison.Ordinal) && (pt.X - p.X) * (pt.X - p.X) + (pt.Y - p.Y) * (pt.Y - p.Y) < 100))
            { Visuals.EscadaCasa.RegistrarTaverna(node); Visuals.EscadaCasa.ConstruirEscadaTaverna(node); }
        }
        if (inner.Size != Vector2.Zero)
        {
            // 10/11 item 59: o calçamento (com relevo) atravessava o piso de dentro — o prédio sobe um dedo; e de noite o
            // interior virava um breu: lampiões quentes lá dentro (mais fortes à noite)
            model.Position += new Vector3(0, 0.06f, 0);
            AddInteriorLamps(node, inner);
        }
        if (closed != Vector3.Zero) Collider(node.Position + new Vector3(0, closed.Y / 2f, 0), new BoxShape3D { Size = (node.Basis * closed).Abs() });
        if (p.Kind == "portao_fortificado" && model.FindChild("edificio_portao_fortificado_grade", true, false) is Node3D grade)
            grade.Position += new Vector3(0, 4f, 0);   // grade levantada: a passagem fica aberta
        if (upper.Length == 0) return;
        var nodes = upper.Select(n => model.FindChild(n, true, false)).OfType<Node3D>().ToList();
        if (nodes.Count == 0) return;
        // retângulo de dentro em coordenadas do mapa (giro de 0/90/180/270)
        var a = node.Basis * new Vector3(inner.Position.X, 0, inner.Position.Y);
        var b = node.Basis * new Vector3(inner.End.X, 0, inner.End.Y);
        var min = new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Z, b.Z)) + new Vector2(node.Position.X, node.Position.Z);
        var max = new Vector2(Mathf.Max(a.X, b.X), Mathf.Max(a.Z, b.Z)) + new Vector2(node.Position.X, node.Position.Z);
        _interiors.Add((new Rect2(min, max - min), nodes));
    }

    /// <summary>Lampiões dentro de um prédio (até 4, espalhados pelo retângulo de dentro), sem sombra (leve).</summary>
    private void AddInteriorLamps(Node3D node, Rect2 inner, float height = 2.7f)
    {
        var c = inner.GetCenter();
        var dx = inner.Size.X * 0.27f; var dz = inner.Size.Y * 0.25f;
        var spots = inner.Size.X > 10f
            ? new[] { new Vector2(c.X - dx, c.Y - dz), new Vector2(c.X + dx, c.Y - dz), new Vector2(c.X - dx, c.Y + dz), new Vector2(c.X + dx, c.Y + dz) }
            : new[] { new Vector2(c.X, c.Y - dz), new Vector2(c.X, c.Y + dz) };
        var glass = VisualKit.Glow(new Color(1f, 0.7f, 0.35f), 2.5f);
        foreach (var at in spots)
        {
            var light = new OmniLight3D
            {
                LightColor = new Color(1f, 0.68f, 0.38f), LightEnergy = 1f, OmniRange = 6.5f, OmniAttenuation = 1.2f,
                Position = new Vector3(at.X, height, at.Y), ShadowEnabled = false,
            };
            node.AddChild(light);
            VisualKit.Part(node, VisualKit.Sphere(0.09f), glass, new Vector3(at.X, height + 0.05f, at.Y));
            _interiorLights.Add((light, R(0, 10)));
        }
    }

    private readonly List<(OmniLight3D Light, float Phase)> _interiorLights = new();

    /// <summary>
    /// 10/11 item 58: 2º andar do casarão (mapa próprio "casarao_andar2", entra pela escada). O mesmo modelo do casarão é
    /// posto 4,45 m para baixo — o piso de cima fica no chão do mapa —, sem o telhado (a câmera vê de cima). Coordenadas do
    /// mapa = coordenadas locais do modelo.
    /// </summary>
    /// <summary>13/11: 2º andar da taverna (mapa "taverna_andar2"): o modelo da taverna 4,7 m para baixo, sem o teto de cima;
    /// o vão da escada é um buraco escuro com guarda-corpo.</summary>
    private bool TavernaUpstairs()
    {
        // 13/11: o "superior" do modelo junta piso, paredes e telhado numa malha só (a câmera ficava dentro do telhado);
        // o quarto de cima é montado aqui: assoalho, quatro paredes de taipa com janelas acesas, vigas e o vão da escada
        var off = -Visuals.EscadaCasa.TOffset;
        var node = new Node3D { Name = "TavernaCima" };
        Root.AddChild(node);
        const float w = 6.6f, d = 4.85f, alto = 2.9f, piso = Visuals.EscadaCasa.TPiso2 - Visuals.EscadaCasa.TOffset;
        const float x0 = Visuals.EscadaCasa.TEscX0, x1 = Visuals.EscadaCasa.TEscX1, zt = Visuals.EscadaCasa.TEscZTopo, zb = Visuals.EscadaCasa.TEscZBase;
        var tabua = TexMat("gen_madeira.png", new Color(0.55f, 0.37f, 0.22f), 0.4f);
        var parede = TexMat("gen_reboco.png", new Color(0.78f, 0.7f, 0.58f), 0.6f);
        var viga = VisualKit.Mat(new Color(0.28f, 0.17f, 0.09f), 0.8f);
        // assoalho em três pedaços, deixando o vão da escada aberto
        VisualKit.Part(node, VisualKit.Box(x0 + w, 0.1f, 2 * d), tabua, new Vector3((x0 - w) / 2f, piso - 0.05f, 0));
        VisualKit.Part(node, VisualKit.Box(w - x1, 0.1f, 2 * d), tabua, new Vector3((x1 + w) / 2f, piso - 0.05f, 0));
        VisualKit.Part(node, VisualKit.Box(x1 - x0, 0.1f, zt + d), tabua, new Vector3((x0 + x1) / 2f, piso - 0.05f, (zt - d) / 2f));
        VisualKit.Part(node, VisualKit.Box(x1 - x0, 0.1f, d - zb), tabua, new Vector3((x0 + x1) / 2f, piso - 0.05f, (zb + d) / 2f));
        VisualKit.Part(node, VisualKit.Box(x1 - x0, 0.02f, zb - zt), VisualKit.Mat(new Color(0.03f, 0.025f, 0.02f), 1f), new Vector3((x0 + x1) / 2f, off + 0.3f, (zt + zb) / 2f));   // fundo do vão
        // paredes (a da frente mais baixa: a câmera vem dali) com janelas acesas
        var vidro = VisualKit.Glow(new Color(1f, 0.78f, 0.45f), 1.1f);
        foreach (var (cx, cz, sx, sz, h) in new[] { (0f, -d, 2 * w + 0.3f, 0.25f, alto), (0f, d, 2 * w + 0.3f, 0.25f, 0.9f), (-w, 0f, 0.25f, 2 * d, alto), (w, 0f, 0.25f, 2 * d, alto) })
            VisualKit.Part(node, VisualKit.Box(sx, h, sz), parede, new Vector3(cx, piso + h / 2f, cz));
        foreach (var x in new[] { -3.6f, 0f, 3.6f })
            VisualKit.Part(node, VisualKit.Box(1.0f, 0.8f, 0.05f), vidro, new Vector3(x, piso + 1.6f, -d + 0.14f));
        foreach (var z in new[] { -2f, 2f })
        {
            VisualKit.Part(node, VisualKit.Box(0.05f, 0.8f, 1.0f), vidro, new Vector3(-w + 0.14f, piso + 1.6f, z));
            VisualKit.Part(node, VisualKit.Box(0.05f, 0.8f, 1.0f), vidro, new Vector3(w - 0.14f, piso + 1.6f, z));
        }
        foreach (var x in new[] { -4.4f, -1.5f, 1.5f, 4.4f })
            VisualKit.Part(node, VisualKit.Box(0.22f, 0.22f, 2 * d), viga, new Vector3(x, piso + alto - 0.11f, 0));
        Visuals.EscadaCasa.RegistrarTavernaAndar2();
        Visuals.EscadaCasa.ConstruirEscadaTaverna(Root, off);
        var corr = VisualKit.Mat(new Color(0.3f, 0.19f, 0.1f), 0.8f);
        VisualKit.Part(Root, VisualKit.Box(0.08f, 0.08f, zb - zt), corr, new Vector3(x0 - 0.05f, piso + 1.0f, (zt + zb) / 2f));
        VisualKit.Part(Root, VisualKit.Box(x1 - x0 + 0.1f, 0.08f, 0.08f), corr, new Vector3((x0 + x1) / 2f, piso + 1.0f, zb + 0.05f));
        for (var i = 0; i <= 4; i++)
            VisualKit.Part(Root, VisualKit.Box(0.07f, 1f, 0.07f), corr, new Vector3(x0 - 0.05f, piso + 0.5f, zt + (zb - zt) * i / 4f));
        AddInteriorLamps(node, new Rect2(-6f, -4.3f, 12f, 8.6f), piso + 2.4f);
        VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(400f, 400f) }, VisualKit.Mat(new Color(0.08f, 0.07f, 0.06f), 1f), new Vector3(0, off - 0.05f, 0));
        return true;
    }

    private static Aabb SurfaceAabb(Mesh mesh, int surface)
    {
        var arr = mesh.SurfaceGetArrays(surface);
        var verts = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        if (verts.Length == 0) return new Aabb();
        var ab = new Aabb(verts[0], Vector3.Zero);
        foreach (var v in verts) ab = ab.Expand(v);
        return ab;
    }

    /// <summary>16/11 (pendência u79_casa6x6 — "interior 6×6 das casinhas do Porto: o modelo chegou no pacote; hoje usam o andar
    /// do casarão"): a Casa de Morador é a sala 6×6 do pacote (cama, baú, mesa, lareira e tapete já vêm no modelo).</summary>
    private bool Casa6x6()
    {
        if (Visuals.GlbCache.Instance("interior/casa_interior_6x6") is not { } model) return false;
        var node = new Node3D { Name = "Casa6x6" };
        Root.AddChild(node);
        node.AddChild(model);
        // lareira acesa e uma luz de vela na mesa; lá fora, escuro
        node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.3f), LightEnergy = 1.6f, OmniRange = 6f, Position = new Vector3(2.6f, 0.8f, -0.6f), ShadowEnabled = false });
        node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.55f), LightEnergy = 0.8f, OmniRange = 5f, Position = new Vector3(0.6f, 1.6f, -0.4f), ShadowEnabled = false });
        VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(200f, 200f) }, VisualKit.Mat(new Color(0.08f, 0.07f, 0.06f), 1f), new Vector3(0, -0.05f, 0)).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return true;
    }

    private bool CasaraoUpstairs()
    {
        if (Visuals.GlbCache.Instance("cenario/edificio_casarao_cidade") is not { } model) return false;
        var node = new Node3D { Name = "Casarao", Position = new Vector3(0, -4.45f, 0) };
        Root.AddChild(node);
        node.AddChild(model);
        if (model.FindChild("edificio_casarao_cidade_telhado", true, false) is Node3D roof) roof.Visible = false;
        AddInteriorLamps(node, new Rect2(-7.5f, -5.5f, 15f, 11f), 4.45f + 2.6f);
        // escuro lá fora (as janelas mostram a noite/o céu do tema), e um chão escuro bem abaixo para não ver o vazio
        VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(400f, 400f) }, VisualKit.Mat(new Color(0.08f, 0.07f, 0.06f), 1f), new Vector3(0, -4.5f, 0));
        Visuals.EscadaCasa.RegistrarAndar2();
        return true;
    }

    /// <summary>
    /// 12/11: Biblioteca do Continente (mapa "biblioteca_continente", tema "biblioteca"): o térreo do casarão, sem o andar de
    /// cima e sem telhado, com estantes, atris de livros e luz quente; lá fora é escuro (é escondida embaixo do Vale).
    /// </summary>
    private bool BibliotecaHall()
    {
        // 12/11 (referência do dono): Grande Salão — piso de madeira, paredes de bloco com pilares, teto com clarabóias e feixes
        var node = new Node3D { Name = "Biblioteca" };
        Root.AddChild(node);
        // 13/11: a Biblioteca do Cais (sala pequena) usa o mesmo salão, no tamanho do próprio mapa
        var pequena = Map.HalfSize <= 12;
        float x0 = pequena ? -(float)Map.HalfSize : -8f, x1 = -x0, y0 = pequena ? -(float)Map.HalfSize : -23f, y1 = pequena ? (float)Map.HalfSize : 7f;
        const float alto = 7f;
        var piso = TexMat("gen_madeira.png", new Color(0.55f, 0.36f, 0.22f), 0.35f);
        VisualKit.Part(node, VisualKit.Box(x1 - x0, 0.1f, y1 - y0), piso, new Vector3(0, -0.04f, (y0 + y1) / 2f));
        VisualKit.Part(Root, new PlaneMesh { Size = new Vector2(400f, 400f) }, VisualKit.Mat(new Color(0.05f, 0.045f, 0.04f), 1f), new Vector3(0, -0.06f, 0));
        // passadeira vermelha no corredor
        VisualKit.Part(node, VisualKit.Box(2.2f, 0.02f, y1 - y0 - 3f), VisualKit.Mat(new Color(0.42f, 0.07f, 0.08f), 0.95f), new Vector3(0, 0.02f, (y0 + y1) / 2f + 1f));
        // pilares (entre as estantes) e vigas do teto
        var pedra = TexMat("gen_bloco.png", new Color(0.78f, 0.72f, 0.64f), 0.5f);
        var madeiraEsc = VisualKit.Mat(new Color(0.24f, 0.14f, 0.08f), 0.7f);
        for (var y = y0 + 4f; y <= y1 - 2f; y += 6f)
            foreach (var x in new[] { x0 + 0.3f, x1 - 0.3f })
            {
                VisualKit.Part(node, VisualKit.Box(0.7f, alto, 0.7f), pedra, new Vector3(x, alto / 2f, y + 3f));
                VisualKit.Part(node, VisualKit.Box(x1 - x0, 0.35f, 0.35f), madeiraEsc, new Vector3(0, alto - 0.2f, y + 3f));
            }
        // teto: faixas de madeira com clarabóias de vidro entre as vigas (luz fria do céu) e feixes descendo
        var vidro = VisualKit.Glow(new Color(0.75f, 0.85f, 1f), 0.9f);
        // 13/11 ("as luzes do céu estão muito fortes, tipo sólidas; deixa bem transparente, só para parecer luz"): feixe com
        // borda esfumada (some nas laterais e embaixo) e bem fraco
        var feixe = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
varying vec3 lp;
void vertex() { lp = VERTEX; }
void fragment() {
    // 13/11 (2ª vez: luzes que saem do céu, mas sutis, como feixes de luz mesmo, mais difundidas no ar): cone aberto
    // embaixo, borda bem esfumada (fresnel ao quadrado), some antes de chegar no chão e tem poeira passando devagar
    float frente = abs(dot(normalize(NORMAL), VIEW));
    float miolo = frente * frente;
    float h = clamp(lp.y / 7.0 + 0.5, 0.0, 1.0);
    float desce = smoothstep(0.0, 0.55, h) * (0.55 + 0.45 * h);
    float poeira = 0.8 + 0.2 * sin(lp.y * 2.3 + lp.x * 1.7 + TIME * 0.5) * sin(lp.z * 2.9 - TIME * 0.35);
    ALBEDO = vec3(1.0, 0.94, 0.8);
    ALPHA = 0.055 * miolo * desce * poeira;
}" } };
        for (var y = y0 + 3f; y <= y1 - 3f; y += 6f)
        {
            VisualKit.Part(node, VisualKit.Box(x1 - x0, 0.2f, 1.2f), madeiraEsc, new Vector3(0, alto + 0.1f, y));
            VisualKit.Part(node, VisualKit.Box(4.5f, 0.05f, 3.6f), vidro, new Vector3(0, alto + 0.3f, y + 3f));
            var f = VisualKit.Part(node, new CylinderMesh { TopRadius = 1.3f, BottomRadius = 2.6f, Height = alto, RadialSegments = 24, Rings = 1, CapTop = false, CapBottom = false },
                feixe, new Vector3(0.8f, alto / 2f, y + 3f), new Vector3(0, 0, -10f));
            f.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            // poeira boiando dentro do feixe (difunde a luz no ar)
            node.AddChild(new CpuParticles3D
            {
                Amount = 26, Lifetime = 9f, Position = new Vector3(0.8f, alto * 0.45f, y + 3f), Mesh = new QuadMesh { Size = new Vector2(0.025f, 0.025f) },
                MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.92f, 0.75f, 0.35f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BlendMode = BaseMaterial3D.BlendModeEnum.Add },
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(1.4f, alto * 0.4f, 1.4f),
                Direction = new Vector3(0.2f, -1f, 0), Spread = 40f, Gravity = Vector3.Zero, InitialVelocityMin = 0.04f, InitialVelocityMax = 0.12f, Preprocess = 9f,
            });
            node.AddChild(new OmniLight3D { LightColor = new Color(0.85f, 0.9f, 1f), LightEnergy = 1.1f, OmniRange = 9f, Position = new Vector3(0, alto - 1f, y + 3f), ShadowEnabled = false });
        }
        // 17/11 (dono: "arruma direito a biblioteca do Porto"): a sala pequena ganha forro de madeira inteiro (antes via-se o céu)
        if (pequena) VisualKit.Part(node, VisualKit.Box(x1 - x0, 0.15f, y1 - y0), TexMat("gen_madeira.png", new Color(0.36f, 0.22f, 0.13f), 0.5f), new Vector3(0, alto + 0.45f, (y0 + y1) / 2f));
        // lâmpadas quentes nas alcovas (luz de leitura)
        foreach (var y in pequena ? new[] { -4f, 2f } : new[] { -19f, -13f, -7f, -1f })
            foreach (var x in new[] { x0 + 1.8f, x1 - 1.8f })
            {
                var lamp = new OmniLight3D { LightColor = new Color("#ffc074"), LightEnergy = 1.2f, OmniRange = 4.5f, Position = new Vector3(x, 2.4f, y), ShadowEnabled = false };
                node.AddChild(lamp);
                _interiorLights.Add((lamp, R(0, 10)));
            }
        // a runa azul da Krypta atrás da mesa do arquivista
        if (!pequena) node.AddChild(new OmniLight3D { LightColor = new Color(0.45f, 0.7f, 1f), LightEnergy = 1.4f, OmniRange = 7f, Position = new Vector3(0, 2.2f, -21.5f), ShadowEnabled = false });
        return true;
    }

    /// <summary>12/11: peças do Grande Salão desenhadas a partir dos obstáculos do servidor (parede, estante, vitrine).</summary>
    private bool BibliotecaPeca(MapObstacle o)
    {
        var w = (float)o.W; var d = (float)o.H; var h = (float)o.Height;
        var at = P(o.X, o.Y);
        switch (o.Kind)
        {
            case "parede_bib":
                VisualKit.Part(Root, VisualKit.Box(w, h, d), TexMat("gen_bloco.png", new Color(0.7f, 0.64f, 0.58f), 0.5f), at + new Vector3(0, h / 2f, 0));
                return true;
            case "estante":
            {
                var madeira = TexMat("gen_madeira.png", new Color(0.42f, 0.26f, 0.15f), 0.6f);
                var n = new Node3D { Position = at };
                Root.AddChild(n);
                var longo = w >= d;   // eixo das prateleiras
                var comp = longo ? w : d; var fundo = longo ? d : w;
                Vector3 V(float a, float y, float b) => longo ? new Vector3(a, y, b) : new Vector3(b, y, a);
                Vector3 S(float a, float y, float b) => longo ? new Vector3(a, y, b) : new Vector3(b, y, a);
                VisualKit.Part(n, new BoxMesh { Size = S(comp, h, 0.08f) }, madeira, V(0, h / 2f, 0));
                foreach (var lado in new[] { -1f, 1f }) VisualKit.Part(n, new BoxMesh { Size = S(0.1f, h, fundo) }, madeira, V(lado * comp / 2f, h / 2f, 0));
                var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = new BoxMesh { Size = new Vector3(1, 1, 1) } };
                var livros = new List<(Transform3D T, Color C)>();
                var rng = new RandomNumberGenerator { Seed = (ulong)(Math.Abs(o.X * 1000 + o.Y * 7) + 1) };
                var cores = new[] { new Color(0.45f, 0.08f, 0.08f), new Color(0.12f, 0.22f, 0.4f), new Color(0.15f, 0.32f, 0.16f), new Color(0.5f, 0.38f, 0.16f), new Color(0.3f, 0.18f, 0.1f), new Color(0.6f, 0.5f, 0.35f) };
                for (var p = 0; p < 6; p++)
                {
                    var py = 0.15f + p * (h - 0.3f) / 6f;
                    VisualKit.Part(n, new BoxMesh { Size = S(comp, 0.05f, fundo) }, madeira, V(0, py, 0));
                    foreach (var face in new[] { -1f, 1f })
                    {
                        var a = -comp / 2f + 0.12f;
                        while (a < comp / 2f - 0.15f)
                        {
                            var esp = rng.RandfRange(0.05f, 0.11f); var alt = rng.RandfRange(0.24f, 0.36f);
                            if (rng.Randf() < 0.06f) { a += 0.2f; continue; }   // um vão de vez em quando
                            var pos = V(a + esp / 2f, py + 0.03f + alt / 2f, face * fundo * 0.25f);
                            livros.Add((new Transform3D(Basis.Identity.Scaled(S(esp, alt, fundo * 0.42f)), pos), cores[rng.RandiRange(0, cores.Length - 1)].Darkened(rng.RandfRange(0f, 0.3f))));
                            a += esp + 0.008f;
                        }
                    }
                }
                mm.InstanceCount = livros.Count;
                for (var i = 0; i < livros.Count; i++) { mm.SetInstanceTransform(i, livros[i].T); mm.SetInstanceColor(i, livros[i].C); }
                var capa = VisualKit.Mat(Colors.White, 0.85f);
                capa.VertexColorUseAsAlbedo = true;
                n.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = capa });
                return true;
            }
            case "vitrine":
            {
                var n = new Node3D { Position = at };
                Root.AddChild(n);
                var madeira = VisualKit.Mat(new Color(0.3f, 0.17f, 0.09f), 0.5f);
                VisualKit.Part(n, VisualKit.Box(w, 0.8f, d), madeira, new Vector3(0, 0.4f, 0));
                var vidro = VisualKit.Mat(new Color(0.75f, 0.9f, 1f, 0.22f), 0.05f);
                vidro.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                VisualKit.Part(n, VisualKit.Box(w - 0.08f, 0.45f, d - 0.08f), vidro, new Vector3(0, 1.03f, 0));
                // relíquias expostas: um livro aberto, uma coroa e um cristal
                VisualKit.Part(n, VisualKit.Box(0.4f, 0.04f, 0.3f), VisualKit.Mat(new Color(0.92f, 0.88f, 0.75f), 0.8f), new Vector3(-0.3f, 0.83f, 0));
                VisualKit.Part(n, VisualKit.Cylinder(0.1f, 0.12f, 0.1f, 8), VisualKit.Mat(new Color(0.95f, 0.75f, 0.3f), 0.3f, 0.8f), new Vector3(0.15f, 0.86f, 0));
                VisualKit.Part(n, VisualKit.Sphere(0.06f), VisualKit.Glow(new Color(0.5f, 0.8f, 1f), 2f), new Vector3(0.38f, 0.88f, 0.1f));
                return true;
            }
        }
        return false;
    }

    /// <summary>Onde está o jogador (por quadro): dentro de um prédio, esconde andar de cima e telhado.</summary>
    public void SetViewer(double x, double y)
    {
        var at = new Vector2((float)x, (float)y);
        _viewerPos = new Vector3((float)x, 0, (float)y);
        foreach (var (inside, upper) in _interiors)
        {
            var show = !inside.Grow(-0.3f).HasPoint(at);
            // 13/11 (log do dono: ObjectDisposedException em SetViewer): pula nó que o editor já apagou
            foreach (var n in upper) if (GodotObject.IsInstanceValid(n) && n.Visible != show) n.Visible = show;
        }
    }

    /// <summary>Oficina (forja, bancada, armazém) com objeto de uso perto: quem desenha é o objeto (contorno e texto iguais).</summary>
    private bool StationOwnsProp(MapProp p)
    {
        var objKind = p.Kind == "bau_armazem" ? "armazem" : p.Kind;
        if (objKind is not ("forja" or "bancada" or "armazem" or "caldeirao" or "panela")) return false;
        return Map.Objects.Any(o => o.Kind == objKind && (p.X - o.X) * (p.X - o.X) + (p.Y - o.Y) * (p.Y - o.Y) < 12);
    }

    /// <summary>11/11 (editor v2, miniaturas da paleta): monta a peça fora do mapa e devolve o nó solto (null = não desenhou).</summary>
    public Node3D? PreviewProp(string kind)
    {
        var before = Root.GetChildCount();
        // 13/11 (log do dono: "ObjectDisposedException ... OmniLight3D" 12 mil vezes): a miniatura do editor monta a peça no
        // mapa de verdade e depois apaga — as luzes/vento/bonecos que ela registrou ficavam nas listas e quebravam o quadro
        var (nF, nS, nI, nM, nP, nO) = (_flames.Count, _swaying.Count, _interiorLights.Count, _flameMeshes.Count, _presos.Count, _mortos.Count);
        try { AddProp(new MapProp(kind, 0, 0, 0)); } catch (Exception) { }
        void Corta<T>(List<T> l, int n) { if (l.Count > n) l.RemoveRange(n, l.Count - n); }
        Corta(_flames, nF); Corta(_swaying, nS); Corta(_interiorLights, nI); Corta(_flameMeshes, nM); Corta(_presos, nP); Corta(_mortos, nO);
        if (Root.GetChildCount() <= before) return null;
        var node = Root.GetChild(before) as Node3D;   // a peça é o primeiro nó que AddProp põe
        // o que veio além do nó da peça (raro) também sai do mapa
        while (Root.GetChildCount() > before + 1) { var extra = Root.GetChild(Root.GetChildCount() - 1); Root.RemoveChild(extra); extra.QueueFree(); }
        if (node is null) return null;
        Root.RemoveChild(node);
        node.Position = Vector3.Zero;
        return node;
    }

    /// <summary>13/11: camada do chão (só ela recebe as pinceladas do editor; gente e peças não ficam manchadas).</summary>
    public const uint ChaoLayer = 1u << 10;

    /// <summary>Texturas do pincel de chão: nome curto → (arquivo, tom).</summary>
    public static readonly string[] PinturasDisponiveis = { "terra", "grama", "pedra", "areia", "areia_molhada", "cal", "calcada", "lama", "musgo" };
    public static (string Arquivo, Color Tom) PinturaArquivo(string nome) => nome switch
    {
        "terra" => ("ter_terra.png", Colors.White), "grama" => ("ter_grama.png", Colors.White), "pedra" => ("ter_pedra.png", Colors.White),
        "areia" => ("ter_areia.png", Colors.White), "areia_molhada" => ("ter_areia_molhada.png", Colors.White), "cal" => ("ter_costa_cal.png", Colors.White),
        "calcada" => ("chao_pedra.jpg", Colors.White), "lama" => ("ter_terra.png", new Color(0.5f, 0.42f, 0.33f)), "musgo" => ("ter_grama.png", new Color(0.55f, 0.72f, 0.45f)),
        _ => ("ter_terra.png", Colors.White),
    };
    private static readonly Dictionary<string, ImageTexture?> _pinturas = new();

    /// <summary>A textura com a borda já esfumada (alfa radial com ruído), uma vez por tipo.</summary>
    public static ImageTexture? PinturaTextura(string nome)
    {
        if (_pinturas.TryGetValue(nome, out var pronta)) return pronta;
        ImageTexture? tex = null;
        var (arq, tom) = PinturaArquivo(nome);
        if (VisualAssets.Load(arq) is { } fonte && fonte.GetImage() is { } img0)
        {
            var img = (Image)img0.Duplicate();
            if (img.IsCompressed()) img.Decompress();
            img.Convert(Image.Format.Rgba8);
            img.Resize(256, 256, Image.Interpolation.Bilinear);
            var rng = new RandomNumberGenerator { Seed = (ulong)nome.GetHashCode() & 0xffff };
            var ruido = new FastNoiseLite { Seed = (int)rng.Randi(), Frequency = 0.018f, FractalOctaves = 3 };
            for (var y = 0; y < 256; y++)
                for (var x = 0; x < 256; x++)
                {
                    var dx = (x - 127.5f) / 127.5f; var dy = (y - 127.5f) / 127.5f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy) + ruido.GetNoise2D(x, y) * 0.28f;
                    var a = 1f - Mathf.SmoothStep(0.45f, 0.98f, d);
                    var c = img.GetPixel(x, y);
                    img.SetPixel(x, y, new Color(c.R * tom.R, c.G * tom.G, c.B * tom.B, a));
                }
            img.GenerateMipmaps();
            tex = ImageTexture.CreateFromImage(img);
        }
        _pinturas[nome] = tex;
        return tex;
    }

    /// <summary>
    /// 13/11 ("coloca na fonte do meio água saindo, tipo o efeito do fogo"): quatro bicas no pedestal da estátua jogando
    /// arcos de água que caem na bacia, gotinhas e um borrifo claro onde batem, e a água da bacia tremendo.
    /// </summary>
    private static void FonteJorrando(Node3D node)
    {
        // 13/11 (2ª vez: "a água tem que sair da parte metálica da fonte, onde já tem uma água saindo só que estática"):
        // os 4 bicos do modelo (cenario_fonte_praca: jato0..3) ficam nas diagonais, a 1,34 m do centro e 0,83 m de altura,
        // e a água cai na bacia a 2,05 m / 0,49 m. As gotas saem exatamente da boca de cada bico e seguem o arco do jato.
        var agua = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.75f, 0.9f, 1f, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, MetallicSpecular = 1f,
            EmissionEnabled = true, Emission = new Color(0.35f, 0.6f, 0.9f), EmissionEnergyMultiplier = 0.35f, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        };
        var gota = new SphereMesh { Radius = 0.03f, Height = 0.08f, RadialSegments = 6, Rings = 3, Material = agua };
        const float boca = 1.34f, alturaBoca = 0.83f, queda = 2.05f, alturaAgua = 0.49f, tempo = 0.34f;
        var horiz = (queda - boca) / tempo;
        var vert = (alturaAgua - alturaBoca + 0.5f * 9.8f * tempo * tempo) / tempo;
        var vel = Mathf.Sqrt(horiz * horiz + vert * vert);
        for (var i = 0; i < 4; i++)
        {
            var a = Mathf.Pi / 4f + i * Mathf.Pi / 2f;
            var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            node.AddChild(new CpuParticles3D
            {
                Amount = 46, Lifetime = tempo + 0.02f, Position = dir * boca + new Vector3(0, alturaBoca, 0), Mesh = gota,
                Direction = (dir * horiz + Vector3.Up * vert).Normalized(), Spread = 2.5f, Gravity = new Vector3(0, -9.8f, 0),
                InitialVelocityMin = vel * 0.95f, InitialVelocityMax = vel * 1.05f, ScaleAmountMin = 0.7f, ScaleAmountMax = 1.2f,
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.015f,
            });
            // borrifo onde o jato cai na bacia
            node.AddChild(new CpuParticles3D
            {
                Amount = 14, Lifetime = 0.45f, Position = dir * queda + new Vector3(0, alturaAgua + 0.02f, 0), Mesh = new SphereMesh { Radius = 0.022f, Height = 0.045f, RadialSegments = 4, Rings = 2, Material = agua },
                Direction = Vector3.Up, Spread = 50f, Gravity = new Vector3(0, -6f, 0), InitialVelocityMin = 0.4f, InitialVelocityMax = 0.9f,
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.08f,
            });
        }
    }

    private void AddProp(MapProp p)
    {
        if (StationOwnsProp(p)) return;   // 10/11: a forja não tinha brilho nem texto (o desenho era do enfeite, não da oficina)
        var node = new Node3D { Position = P(p.X, p.Y), RotationDegrees = new Vector3(0, (float)p.Yaw, 0) };
        if (Math.Abs(p.Scale - 1) > 0.001) node.Scale = Vector3.One * (float)p.Scale;   // 11/11 noite: tamanho do editor
        if (Math.Abs(p.Altura) > 0.001) node.Position += new Vector3(0, (float)p.Altura, 0);   // 13/11: altura do editor
        Root.AddChild(node);
        var wood = VisualKit.Mat(new Color(0.33f, 0.22f, 0.13f), 0.9f);
        // 11/11 noite (editor v2): qualquer modelo da pasta Models vira peça de cenário ("glb:cenario/xxx")
        if (p.Kind.StartsWith("glb:", StringComparison.Ordinal)) { MapKit.Glb(node, p.Kind[4..], Vector3.Zero, 0f, 1f); return; }
        if (p.Kind is "cadaver" or "sangue") { DecoracaoMorbida(node, p); return; }
        if (Ambiente.PecasDaVila.Build(node, p.Kind)) return;   // 13/11: secador de peixe, açougue, biblioteca, placar...
        if (KitTapa.Prop(this, node, p.Kind)) return;   // 12/11: peças com modelo do pacote Vila (tocha, placa, tenda, redes, barco…)
        switch (p.Kind)
        {
            // 11/11: móveis e peças de puzzle do pacote da noite (Assets/Models/interior), luz como no _info.json
            case var k when (k.StartsWith("movel_") || k.StartsWith("puzzle_")) && Visuals.GlbCache.Instance("interior/" + k) is { } movel:
                node.AddChild(movel);
                if (k == "movel_lareira")
                {
                    var fogo = new OmniLight3D { LightColor = new Color("#ff8a3a"), LightEnergy = 1.6f, OmniRange = 6f, Position = new Vector3(0, 0.5f, 0.2f), ShadowEnabled = false };
                    node.AddChild(fogo);
                    _interiorLights.Add((fogo, R(0, 10)));
                    Sfx.Loop3D(node, "loop_fogueira", -8f, 2.5f, 12f);
                }
                else if (k == "movel_mesa")
                    node.AddChild(new OmniLight3D { LightColor = new Color("#ffb347"), LightEnergy = 0.8f, OmniRange = 2.5f, Position = new Vector3(0.1f, 0.92f, -0.25f), ShadowEnabled = false });
                else if (k == "puzzle_runa")
                    node.AddChild(new OmniLight3D { LightColor = new Color("#7affd8"), LightEnergy = 1f, OmniRange = 3f, Position = new Vector3(0, 1f, 0.5f), ShadowEnabled = false });
                break;
            // 12/11: mesa de Vinte-e-Um (2º andar do casarão): feltro verde, borda de madeira, fichas e cartas, luz baixa em cima
            case "mesa_vinteum":
            {
                var felt = VisualKit.Mat(new Color(0.06f, 0.3f, 0.16f), 0.95f);
                var rim = VisualKit.Mat(new Color(0.36f, 0.2f, 0.09f), 0.55f);
                VisualKit.Part(node, new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.35f, Height = 0.72f }, rim, new Vector3(0, 0.36f, 0));
                VisualKit.Part(node, new CylinderMesh { TopRadius = 1.05f, BottomRadius = 1.0f, Height = 0.08f, RadialSegments = 40 }, rim, new Vector3(0, 0.76f, 0));
                VisualKit.Part(node, new CylinderMesh { TopRadius = 0.93f, BottomRadius = 0.93f, Height = 0.02f, RadialSegments = 40 }, felt, new Vector3(0, 0.805f, 0));
                // fichas em pilhas (vermelha, azul, dourada) do lado do crupiê
                var chipCols = new[] { new Color(0.75f, 0.12f, 0.1f), new Color(0.15f, 0.3f, 0.8f), new Color(0.92f, 0.75f, 0.25f), new Color(0.1f, 0.1f, 0.1f) };
                for (var i = 0; i < 4; i++)
                    for (var j = 0; j < 3 + i; j++)
                        VisualKit.Part(node, new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.045f, Height = 0.014f }, VisualKit.Mat(chipCols[i], 0.4f),
                            new Vector3(-0.3f + i * 0.13f, 0.82f + j * 0.015f, -0.62f));
                // cartas abertas (brancas) na frente de cada banqueta e duas na frente do crupiê
                var card = VisualKit.Mat(new Color(0.96f, 0.94f, 0.88f), 0.6f);
                foreach (var (x, z, yaw) in new[] { (-0.55f, 0.35f, 0.6f), (-0.48f, 0.4f, 0.3f), (0f, 0.6f, 0.05f), (0.08f, 0.62f, -0.2f), (0.55f, 0.35f, -0.5f), (0f, -0.35f, 0.1f), (0.1f, -0.33f, -0.15f) })
                {
                    var c = VisualKit.Part(node, new BoxMesh { Size = new Vector3(0.09f, 0.004f, 0.13f) }, card, new Vector3(x, 0.818f, z));
                    c.RotationDegrees = new Vector3(0, Mathf.RadToDeg(yaw), 0);
                }
                // baralho (sapato) do lado do crupiê
                VisualKit.Part(node, new BoxMesh { Size = new Vector3(0.16f, 0.1f, 0.22f) }, VisualKit.Mat(new Color(0.45f, 0.1f, 0.12f), 0.6f), new Vector3(0.45f, 0.86f, -0.55f));
                var lamp = new OmniLight3D { LightColor = new Color("#ffcf7a"), LightEnergy = 1.4f, OmniRange = 3.2f, Position = new Vector3(0, 2.0f, 0), ShadowEnabled = false };
                node.AddChild(lamp);
                _interiorLights.Add((lamp, R(0, 10)));
                break;
            }
            // 11/11: gárgula de pedra no muro do portão (olhos vermelhos acesos)
            case "gargula_muro" when Visuals.GlbCache.Instance("cenario/cenario_gargula_muro") is { } garg:
                node.AddChild(garg);
                foreach (var side in new[] { -0.09f, 0.09f })
                    node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.15f, 0.1f), LightEnergy = 0.6f, OmniRange = 1.2f, Position = new Vector3(side, 4.25f, 0.25f), ShadowEnabled = false });
                break;
            case "cogumelo_gigante":
            {
                // 13/11: o cogumelo encantado da Dona Fungilda — tronco claro, chapéu roxo aceso com pintas, esporos subindo
                var tronco = VisualKit.Mat(new Color(0.9f, 0.86f, 0.76f), 0.85f);
                VisualKit.Part(node, VisualKit.Cylinder(0.55f, 0.85f, 3.2f, 16), tronco, new Vector3(0, 1.6f, 0));
                VisualKit.Part(node, VisualKit.Cylinder(0.95f, 0.65f, 0.25f, 16), tronco.Duplicate() as StandardMaterial3D ?? tronco, new Vector3(0, 2.3f, 0));
                var chapeu = VisualKit.Glow(new Color(0.5f, 0.16f, 0.72f), 0.55f);
                VisualKit.Part(node, VisualKit.Sphere(2.4f), chapeu, new Vector3(0, 3.4f, 0), null, new Vector3(1f, 0.45f, 1f));
                VisualKit.Part(node, VisualKit.Cylinder(2.35f, 2.4f, 0.08f, 28), VisualKit.Mat(new Color(0.25f, 0.08f, 0.32f), 0.9f), new Vector3(0, 3.05f, 0));
                var pinta = VisualKit.Glow(new Color(0.95f, 0.9f, 1f), 0.9f);
                for (var i = 0; i < 14; i++)
                {
                    var a = i * 2.39996f; var rr = 0.6f + 1.4f * ((i * 37 % 10) / 10f);
                    VisualKit.Part(node, VisualKit.Sphere(0.22f), pinta, new Vector3(Mathf.Sin(a) * rr, 3.4f + 1.05f * Mathf.Sqrt(Mathf.Max(0, 1 - rr * rr / 5.76f)), Mathf.Cos(a) * rr), null, new Vector3(1f, 0.35f, 1f));
                }
                node.AddChild(new OmniLight3D { LightColor = new Color(0.7f, 0.35f, 1f), LightEnergy = 2.2f, OmniRange = 11f, Position = new Vector3(0, 2.6f, 0), ShadowEnabled = false });
                var esporos = new CpuParticles3D { Amount = 40, Lifetime = 4f, Position = new Vector3(0, 3f, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
                    EmissionSphereRadius = 2.2f, Direction = Vector3.Up, Spread = 30f, InitialVelocityMin = 0.15f, InitialVelocityMax = 0.4f, Gravity = new Vector3(0, 0.05f, 0),
                    ScaleAmountMin = 0.5f, ScaleAmountMax = 1f, Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 } };
                esporos.MaterialOverride = VisualKit.Glow(new Color(0.8f, 0.45f, 1f), 3f);
                node.AddChild(esporos);
                break;
            }
            case "fogueira" when Visuals.GlbCache.Instance("costa/cenario_fogueira") is { } fire:
                fire.QueueFree();   // só confere se o modelo existe; a VidaAmbiente monta a fogueira completa
                _vida.Fogueira(P(p.X, p.Y));
                Sfx.Loop3D(node, "loop_fogueira", -6f, 3f, 14f);
                break;
            case "fogueira":
                for (var i = 0; i < 5; i++)
                    VisualKit.Part(node, VisualKit.Cylinder(0.08f, 0.1f, 1.1f, 6), wood, new Vector3(0, 0.12f, 0), new Vector3(80, i * 72, 0));
                for (var i = 0; i < 8; i++)
                    VisualKit.Part(node, VisualKit.Sphere(0.18f), VisualKit.Mat(new Color(0.4f, 0.38f, 0.36f)), new Vector3(Mathf.Cos(i * 0.785f) * 0.75f, 0.08f, Mathf.Sin(i * 0.785f) * 0.75f));
                AddFlame(node, new Vector3(0, 0.15f, 0), 2.2f, 4.5f, 12f);
                Sfx.Loop3D(node, "loop_fogueira", -6f, 3f, 14f);
                break;
            case "tocha":
                VisualKit.Part(node, VisualKit.Cylinder(0.05f, 0.07f, 1.8f, 6), wood, new Vector3(0, 0.9f, 0));
                VisualKit.Part(node, VisualKit.Cylinder(0.12f, 0.08f, 0.2f, 6), VisualKit.Mat(new Color(0.2f, 0.2f, 0.2f), 0.5f, 0.7f), new Vector3(0, 1.85f, 0));
                AddFlame(node, new Vector3(0, 1.92f, 0), 1f, 1.3f, 6f);   // 13/11: menos que o poste
                break;
            case var pk when pk.StartsWith("pintura_", StringComparison.Ordinal):
            {
                // 13/11 ("faltou mexer na textura do chão, não aquela mancha com profundidade"): pincelada de tinta — um decal
                // que pinta a textura escolhida direto no chão (só no chão: camada 11), borda macia e irregular, sem volume
                var tinta = PinturaTextura(pk[8..]);
                if (tinta is null) break;
                var dec = new Decal
                {
                    Size = new Vector3(2f, 1.6f, 2f), TextureAlbedo = tinta, Modulate = new Color(1, 1, 1, (float)Math.Clamp(p.Forca, 0.05, 1.0)),
                    AlbedoMix = 1f, CullMask = ChaoLayer, UpperFade = 0.2f, LowerFade = 0.2f, Position = new Vector3(0, 0.3f, 0),
                    SortingOffset = (float)(p.X * 0.0001 + p.Y * 0.00001),
                };
                if (VisualAssets.Normal(PinturaArquivo(pk[8..]).Arquivo) is { } nt) dec.TextureNormal = nt;
                node.AddChild(dec);
                break;
            }
            case "chao_terra" or "chao_grama" or "chao_pedra" or "chao_areia" or "chao_lama" or "chao_cal" or "chao_calcada":
            {
                // 13/11 ("não achei onde mexo no chão, pintura, textura"): pincel de chão do editor — mancha de 4 m com borda
                // irregular (o tamanho é a escala; girar muda o recorte), por cima do chão do mapa
                var (tex, tint) = p.Kind switch
                {
                    "chao_terra" => ("ter_terra.png", new Color(0.95f, 0.9f, 0.85f)), "chao_grama" => ("ter_grama.png", Colors.White),
                    "chao_pedra" => ("ter_pedra.png", Colors.White), "chao_areia" => ("ter_areia.png", Colors.White),
                    "chao_lama" => ("ter_terra.png", new Color(0.45f, 0.38f, 0.3f)), "chao_cal" => ("ter_costa_cal.png", Colors.White),
                    _ => ("gen_bloco.png", new Color(0.8f, 0.78f, 0.74f)),
                };
                var mat = TexMat(tex, tint, 0.5f);
                mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
                var rng = new RandomNumberGenerator { Seed = (ulong)(Math.Abs(p.X * 53 + p.Y * 7) + 3) };
                for (var i = 0; i < 5; i++)
                {
                    var r = i == 0 ? 2f : rng.RandfRange(0.9f, 1.5f);
                    var off = i == 0 ? Vector3.Zero : new Vector3(rng.RandfRange(-1.4f, 1.4f), 0, rng.RandfRange(-1.4f, 1.4f));
                    var disco = new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.01f, RadialSegments = 9 + i },
                        MaterialOverride = mat, Position = off + new Vector3(0, 0.02f + i * 0.002f, 0),
                        RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    };
                    node.AddChild(disco);
                }
                break;
            }
            case "canteiro_flores":
            {
                // 13/11: canteiro de flores com borda de madeira (peça editável; o tamanho é a escala do editor)
                var rng = new RandomNumberGenerator { Seed = (ulong)(Math.Abs(p.X * 131 + p.Y * 17) + 1) };
                var len = 2.4f;
                VisualKit.Part(node, VisualKit.Box(len, 0.25f, 0.9f), TexMat("gen_madeira.png", new Color(0.55f, 0.40f, 0.28f), 1f), new Vector3(0, 0.12f, 0));
                VisualKit.Part(node, VisualKit.Box(len - 0.12f, 0.06f, 0.78f), TexMat("ter_terra.png", new Color(0.45f, 0.33f, 0.24f), 1.5f), new Vector3(0, 0.26f, 0));
                for (var x = -len / 2f + 0.45f; x < len / 2f - 0.3f; x += 0.75f)
                    MapKit.Glb(node, rng.Randf() < 0.7f ? "costa/costa_flores_" + (rng.Randf() < 0.5f ? "a" : "b") : "costa/costa_arbusto_" + "abc"[rng.RandiRange(0, 2)],
                        new Vector3(x + rng.RandfRange(-0.1f, 0.1f), 0.28f, rng.RandfRange(-0.15f, 0.15f)), rng.RandfRange(0, 360), rng.RandfRange(0.5f, 0.75f));
                break;
            }
            case "vaso_planta":
            {
                var rng = new RandomNumberGenerator { Seed = (ulong)(Math.Abs(p.X * 97 + p.Y * 29) + 1) };
                VisualKit.Part(node, VisualKit.Cylinder(0.32f, 0.22f, 0.5f, 10), TexMat("gen_bloco.png", new Color(0.78f, 0.45f, 0.30f), 0.6f), new Vector3(0, 0.25f, 0));
                MapKit.Glb(node, "costa/costa_arbusto_" + "abc"[rng.RandiRange(0, 2)], new Vector3(0, 0.45f, 0), rng.RandfRange(0, 360), 0.45f);
                break;
            }
            case "anel_reinos":
            {
                // 14/11: o Anel do Coração dos Reinos (como na arte do mapa): aro de luz violeta em pé, girando devagar, e um fio
                // de luz descendo até a pedra; o círculo de runas no chão em volta
                var aro = new Node3D { Name = "Anel", Position = new Vector3(0, 5.2f, 0) };
                node.AddChild(aro);
                VisualKit.Part(aro, new TorusMesh { InnerRadius = 3.0f, OuterRadius = 3.25f, Rings = 48, RingSegments = 10 }, VisualKit.Glow(new Color(0.85f, 0.55f, 1f), 5f), Vector3.Zero, new Vector3(90, 0, 0));
                VisualKit.Part(aro, new TorusMesh { InnerRadius = 3.45f, OuterRadius = 3.52f, Rings = 48, RingSegments = 6 }, VisualKit.Glow(new Color(1f, 0.85f, 1f), 2f), Vector3.Zero, new Vector3(90, 0, 0));
                _orbitas.Add((aro, 0f));
                var fio = VisualKit.Part(node, new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.12f, Height = 5.2f, RadialSegments = 8 }, VisualKit.Glow(new Color(0.9f, 0.7f, 1f), 4f), new Vector3(0, 2.6f, 0));
                fio.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                var runas = new ShaderMaterial { Shader = new Shader { Code = Visuals.MannequinVisual.RunasShader } };
                runas.SetShaderParameter("cor", new Vector3(0.8f, 0.5f, 1f));
                runas.SetShaderParameter("forca", 0.7f);
                node.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(9f, 9f) }, MaterialOverride = runas, Position = new Vector3(0, 0.03f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                node.AddChild(new OmniLight3D { LightColor = new Color(0.8f, 0.55f, 1f), LightEnergy = 3f, OmniRange = 18f, Position = new Vector3(0, 5f, 0), ShadowEnabled = false });
                break;
            }
            case "cerejeira_reinos":
            {
                // árvore pálida em flor (a da arte): tronco retorcido e copa de pétalas claras, pétalas caindo devagar
                var casca = VisualKit.Mat(new Color(0.30f, 0.24f, 0.26f), 0.9f);
                var flor = VisualKit.Glow(new Color(1f, 0.86f, 0.94f), 0.6f);
                VisualKit.Part(node, VisualKit.Cylinder(0.25f, 0.5f, 3.2f, 8), casca, new Vector3(0, 1.6f, 0), new Vector3(0, 0, 8));
                VisualKit.Part(node, VisualKit.Cylinder(0.12f, 0.22f, 2.2f, 6), casca, new Vector3(0.9f, 3.4f, 0.2f), new Vector3(0, 0, -38));
                VisualKit.Part(node, VisualKit.Cylinder(0.1f, 0.2f, 2.0f, 6), casca, new Vector3(-0.8f, 3.3f, -0.3f), new Vector3(10, 0, 40));
                var rngC = new Random(5);
                for (var i = 0; i < 14; i++)
                {
                    var a = i * 2.4f; var d = 0.6f + (float)rngC.NextDouble() * 1.8f;
                    VisualKit.Part(node, VisualKit.Sphere(0.7f + (float)rngC.NextDouble() * 0.6f), flor, new Vector3(Mathf.Cos(a) * d, 4.0f + (float)rngC.NextDouble() * 1.4f, Mathf.Sin(a) * d));
                }
                node.AddChild(new CpuParticles3D
                {
                    Amount = 40, Lifetime = 7f, Preprocess = 7f, Position = new Vector3(0, 4.5f, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 2.2f,
                    Direction = new Vector3(0.3f, -1f, 0), Spread = 40f, Gravity = new Vector3(0.15f, -0.35f, 0), InitialVelocityMin = 0.1f, InitialVelocityMax = 0.4f,
                    Mesh = new QuadMesh { Size = new Vector2(0.07f, 0.05f), Material = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.85f, 0.93f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, CullMode = BaseMaterial3D.CullModeEnum.Disabled } },
                });
                node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.85f, 0.95f), LightEnergy = 0.8f, OmniRange = 7f, Position = new Vector3(0, 4f, 0), ShadowEnabled = false });
                break;
            }
            case "cascata_reinos":
            {
                // cascata caindo da borda da ilha para o abismo: cortina de água lilás que desce e spray em cima
                var agua = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.8f, 1f, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    EmissionEnabled = true, Emission = new Color(0.6f, 0.45f, 0.95f), EmissionEnergyMultiplier = 0.6f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles };
                node.AddChild(new CpuParticles3D
                {
                    Amount = 220, Lifetime = 2.6f, Preprocess = 3f, Position = new Vector3(0, 0.3f, 0), EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
                    EmissionBoxExtents = new Vector3(2.2f, 0.1f, 0.2f), Direction = Vector3.Down, Spread = 4f, Gravity = new Vector3(0, -9.8f, 0),
                    InitialVelocityMin = 0.5f, InitialVelocityMax = 1.2f, ScaleAmountMin = 0.8f, ScaleAmountMax = 1.6f,
                    Mesh = new QuadMesh { Size = new Vector2(0.18f, 0.6f), Material = agua },
                });
                node.AddChild(new OmniLight3D { LightColor = new Color(0.75f, 0.6f, 1f), LightEnergy = 1.2f, OmniRange = 8f, Position = new Vector3(0, -1f, 0), ShadowEnabled = false });
                break;
            }
            case "fonte_praca":
                // 13/11: a fonte da praça com a estátua do Guardião e a luz azul da água (peça editável)
                MapKit.Glb(node, "cenario_fonte_praca", Vector3.Zero, 0f, 1f);
                MapKit.Glb(node, "cenario_estatua_guardiao", Vector3.Zero, 180f, 1f);
                node.AddChild(new OmniLight3D { Position = new Vector3(0, 0.6f, 0), LightColor = new Color(0.55f, 0.85f, 1f), LightEnergy = 0.6f, OmniRange = 4.5f });
                FonteJorrando(node);
                break;
            case "tocha_parede":
            {
                // 13/11 ("faz uma tocha de parede"): suporte de ferro preso na parede (o fundo do prop é a parede, -Z),
                // braço inclinado para fora e a tocha com chama de verdade
                var ferro = VisualKit.Mat(new Color(0.18f, 0.17f, 0.17f), 0.5f, 0.8f);
                VisualKit.Part(node, VisualKit.Box(0.16f, 0.32f, 0.04f), ferro, new Vector3(0, 1.85f, -0.02f));
                foreach (var y in new[] { 1.74f, 1.96f })
                    VisualKit.Part(node, VisualKit.Cylinder(0.012f, 0.012f, 0.03f, 6), ferro, new Vector3(0, y, 0.01f), new Vector3(90, 0, 0));
                VisualKit.Part(node, VisualKit.Box(0.04f, 0.04f, 0.26f), ferro, new Vector3(0, 1.84f, 0.12f), new Vector3(-20, 0, 0));
                VisualKit.Part(node, new TorusMesh { InnerRadius = 0.045f, OuterRadius = 0.06f, Rings = 12, RingSegments = 6 }, ferro, new Vector3(0, 1.9f, 0.24f));
                var cabo = VisualKit.Joint(node, new Vector3(0, 1.9f, 0.25f));
                cabo.RotationDegrees = new Vector3(20, 0, 0);
                VisualKit.Part(cabo, VisualKit.Cylinder(0.035f, 0.025f, 0.5f, 6), wood, new Vector3(0, 0.05f, 0));
                VisualKit.Part(cabo, VisualKit.Cylinder(0.07f, 0.045f, 0.12f, 6), VisualKit.Mat(new Color(0.12f, 0.1f, 0.08f), 0.9f), new Vector3(0, 0.3f, 0));
                AddFlame(cabo, new Vector3(0, 0.34f, 0), 0.75f, 1.1f, 5.5f);
                break;
            }
            default:
                // 18/11 (dono: "não consigo usar essa panela no Monte do Prefeito, tá bugada"): panela/forja/bancada de
                // ENFEITE (vinheta de acampamento) não ganha o nome flutuante de oficina — só a de verdade (objeto) ganha
                MapKit.Enfeite = true;
                var montou = MapKit.TryBuild(this, node, p.Kind, wood);
                MapKit.Enfeite = false;
                if (montou)
                {
                    if (p.Kind == "pinheiros")
                        foreach (var child in node.GetChildren())
                            if (child is Node3D t && t.GetChildCount() > 2) _swaying.Add((t, t.RotationDegrees, R(0, 6.28f), R(0.8f, 1.5f)));
                    break;
                }
                break;
            case "torre_partida" or "vagonete" or "trilhos" or "carroca" or "blocos_cal_a" or "blocos_cal_b" or "blocos_cal_c"
                or "estandarte_tridente" or "tocha_poste" or "floreira" or "banco_praca" or "caixote" or "barril_v2" or "carrinho_feira":
            {
                // cenário dos estudos (atualização de arte 10/11); colisão: obstáculos "invisivel" do servidor
                var file = p.Kind switch
                {
                    "torre_partida" => "marco_torre_partida", "vagonete" => "cenario_vagonete", "trilhos" => "cenario_trilhos",
                    "carroca" => "cenario_carroca_quebrada", "estandarte_tridente" => "cenario_estandarte_tridente",
                    "tocha_poste" => "cenario_tocha_poste", _ => "cenario_" + p.Kind,
                };
                // 13/11 (regra do dono: "torre fechada, que não dá para ir, tem que ter um portão vermelho"): a torre partida
                // ganha um portão de pedra lacrado por um véu vermelho no pé dela, virado para o centro do mapa
                // 15/11: a da Costa virou dungeon (portal "Torre Partida" no pé dela) — torre com portal perto não ganha o véu
                if (p.Kind == "torre_partida" && !Map.Portals.Any(pt => Math.Abs(pt.X - p.X) < 8 && Math.Abs(pt.Y - p.Y) < 8))
                {
                    var paraCentro = new Vector2((float)-p.X, (float)-p.Y);
                    var dirT = paraCentro.LengthSquared() > 0.01f ? paraCentro.Normalized() : new Vector2(0, 1);
                    var esc = p.Scale > 0 ? (float)p.Scale : 1f;
                    PortaoVermelho(P(p.X + dirT.X * 3.0 * esc, p.Y + dirT.Y * 3.0 * esc), Mathf.Atan2(dirT.X, dirT.Y), 1.8f, 2.8f, "Torre lacrada: ainda não tem entrada (vem numa próxima versão)");
                }
                if (MapKit.Glb(node, file, Vector3.Zero, 0f, 1f) && p.Kind == "tocha_poste")
                {
                    // a chama já vem no modelo; aqui só a luz que tremula (e fica mais forte à noite)
                    var torch = new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.28f), LightEnergy = 1.5f, OmniRange = 7f, Position = new Vector3(0, 2.3f, 0) };
                    node.AddChild(torch);
                    _flames.Add((torch, R(0, 10), 1.5f));
                }
                break;
            }
            case "taverna_grande" or "casarao_cidade" or "sobrado" or "casa_pequena" or "portao_fortificado" or "cerca":
                LotTwoBuilding(node, p);
                break;
            case "tenda_goblin" or "palicada" or "totem_goblin" or "entrada_galeria":
            {
                // acampamento goblin e entrada da Galeria (lote 3); colisão vem do servidor (obstáculos "invisivel")
                var file = p.Kind switch
                {
                    "tenda_goblin" => "cenario/cenario_tenda_goblin", "palicada" => "cenario/cenario_palicada_trecho",
                    "totem_goblin" => "cenario/cenario_totem_goblin", _ => "cenario/edificio_entrada_galeria",
                };
                if (!MapKit.Glb(node, file, Vector3.Zero, 0f, 1f) && p.Kind == "tenda_goblin")
                    VisualKit.Part(node, VisualKit.Cylinder(0f, 1.9f, 2.4f, 4), VisualKit.Mat(new Color(0.45f, 0.30f, 0.20f), 0.95f), new Vector3(0, 1.2f, 0));
                if (p.Kind == "totem_goblin")
                {
                    node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.5f, 0.2f), LightEnergy = 1.2f, OmniRange = 6f, Position = new Vector3(0, 3.2f, 0.4f) });
                    _vida.ColunaDeFumaca(P(p.X - 3, p.Y - 3), grande: true);   // fumaça do acampamento, vista de longe
                }
                break;
            }
            // 11/11 noite (complemento do pacote): ruínas e estátua do guardião
            case "ruina_casarao" when MapKit.Glb(node, "cenario/ruina_casarao_abandonado", Vector3.Zero, 0f, 1f):
                break;
            case "ruina_salao" when MapKit.Glb(node, "cenario/ruina_salao_vazado", Vector3.Zero, 0f, 1f):
                break;
            case "estatua_guardiao" when MapKit.Glb(node, "cenario_estatua_guardiao", Vector3.Zero, 0f, 1f):
                break;
            case "monte_areia":
            {
                // 11/11 noite ("o chão sobe e desce, de forma sutil"): monte de areia baixo com capim; sólido no servidor
                var sand = TexMat("ter_areia.png", new Color(0.92f, 0.88f, 0.8f), 0.95f);
                // 11/11 noite: dá para subir (relevo, não parede) — o boneco anda por cima, na superfície
                var mound = VisualKit.Part(node, new SphereMesh { Radius = 2.2f, Height = 1.5f, RadialSegments = 24, Rings = 10 }, sand, new Vector3(0, -0.15f, 0), null, new Vector3(1f, 0.9f, 0.8f));
                mound.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                Visuals.EscadaCasa.RegistrarMonte(node, 2.2f, 1.76f, 0.675f, -0.15f);
                for (var i = 0; i < 3; i++)
                {
                    var at = new Vector3(R(-1.2f, 1.2f), 0, R(-0.9f, 0.9f));
                    var r = (at.X / 2.2f) * (at.X / 2.2f) + (at.Z / 1.76f) * (at.Z / 1.76f);
                    at.Y = r < 1f ? -0.15f + 0.675f * Mathf.Sqrt(1f - r) - 0.05f : 0f;
                    MapKit.Glb(node, "costa/costa_capim_" + "abc"[i], at, R(0, 360), R(0.7f, 1.1f));
                }
                break;
            }
            case "tenda" when MapKit.Glb(node, "cenario/cenario_tenda_goblin", Vector3.Zero, 0f, 1f):   // 11/11 noite: no lugar da pirâmide marrom
                break;
            case "tenda":
                VisualKit.Part(node, VisualKit.Cylinder(0f, 1.9f, 2.4f, 4), VisualKit.Mat(new Color(0.55f, 0.48f, 0.36f), 0.95f), new Vector3(0, 1.2f, 0));
                break;
            case "caixotes" when MapKit.Glb(node, "cenario_caixote", Vector3.Zero, R(0, 360), 1f):
                MapKit.Glb(node, "cenario_caixote", new Vector3(0.8f, 0, 0.2f), R(0, 360), 0.8f);
                MapKit.Glb(node, "cenario_caixote", new Vector3(0.1f, 0.71f, 0.05f), R(0, 360), 0.72f);
                break;
            case "caixotes":
                VisualKit.Part(node, VisualKit.Box(0.8f, 0.8f, 0.8f), wood, new Vector3(0, 0.4f, 0));
                VisualKit.Part(node, VisualKit.Box(0.6f, 0.6f, 0.6f), wood, new Vector3(0.75f, 0.3f, 0.2f), new Vector3(0, 25, 0));
                VisualKit.Part(node, VisualKit.Box(0.55f, 0.55f, 0.55f), wood, new Vector3(0.1f, 1.08f, 0.05f), new Vector3(0, -15, 0));
                break;
            case "cranios":
                for (var i = 0; i < 4; i++)
                    VisualKit.Part(node, VisualKit.Sphere(0.12f), VisualKit.Mat(new Color(0.85f, 0.82f, 0.72f)), new Vector3(R(-0.4f, 0.4f), 0.1f, R(-0.4f, 0.4f)), null, new Vector3(1f, 0.9f, 1.15f));
                break;
            case "rachadura":
                // fenda escura e brilho fraco: dica de que a parede não é o que parece
                for (var i = 0; i < 4; i++)
                    VisualKit.Part(node, VisualKit.Box(0.06f, R(0.6f, 1.2f), 0.08f), VisualKit.Glow(new Color(0.5f, 0.3f, 0.8f), 0.6f),
                        new Vector3(R(-1.5f, 1.5f), R(0.8f, 3f), 0), new Vector3(0, 0, R(-35, 35)));
                break;
            case "placa":
                VisualKit.Part(node, VisualKit.Cylinder(0.05f, 0.05f, 1.4f, 6), wood, new Vector3(0, 0.7f, 0));
                VisualKit.Part(node, VisualKit.Box(0.9f, 0.35f, 0.05f), wood, new Vector3(0, 1.25f, 0));
                if (!string.IsNullOrEmpty(p.Texto))
                {
                    // 11/11 noite: o texto da placa aparece letra por letra quando você chega perto (e some quando sai)
                    var lbl = new Label3D
                    {
                        Text = "", FontSize = 30, OutlineSize = 8, PixelSize = 0.008f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                        Modulate = new Color(1f, 0.93f, 0.75f), Position = new Vector3(0, 2.35f, 0), NoDepthTest = true,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                    };
                    node.AddChild(lbl);
                    _signs.Add((node, lbl, p.Texto!, 0f));
                }
                break;
        }
    }

    // ------------------------------------------------------------ E9: passagens, pontos de viagem e NPCs

    private readonly List<(Node3D Node, Label3D Label, string Text, float Shown)> _signs = new();

    /// <summary>Placas: letras aparecendo (≈ 45 por segundo) com você a até 7 m; somem devagar quando você se afasta.</summary>
    private void UpdateSigns(float dt)
    {
        if (PlayerPos is not { } pp) return;
        for (var i = 0; i < _signs.Count; i++)
        {
            var (node, lbl, text, shown) = _signs[i];
            var near = new Vector2(pp.X - node.Position.X, pp.Z - node.Position.Z).Length() < 7f;
            shown = near ? Mathf.Min(text.Length, shown + dt * 45f) : Mathf.Max(0f, shown - dt * 120f);
            var n = (int)shown;
            if (lbl.Text.Length != n) lbl.Text = text[..n];
            _signs[i] = (node, lbl, text, shown);
        }
    }

    private static ShaderMaterial? _vortexMat;
    // 13/11 (prints do dono, "mais trevoso"): túnel em espiral — centro preto, nuvens torcidas girando para dentro e borda
    // clara esfumada. vermelho = 1: passagem travada pelo nível (laranja/vermelho como o 2º print); 0 = roxo do 1º print.
    private const string VortexShader = @"
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;
uniform float vermelho = 0.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int k = 0; k < 4; k++) { v += a * vnoise(p); p *= 2.03; a *= 0.5; } return v; }
void fragment() {
    vec2 c = (UV - 0.5) * vec2(1.0, 2.0);
    float r = length(c) + 1e-4;
    float a = atan(c.y, c.x);
    // coordenadas do túnel: o ângulo torce com log(r) e tudo é puxado para o centro com o tempo
    float t = TIME * 0.35;
    // 13/11 (o portal tinha uma linha cortando a continuidade na esquerda): o ângulo pulava de +π para −π ali; agora o
    // ruído é lido na direção (cos, sin) do ângulo torcido, que dá a volta sem emenda
    float tw = a + log(r) * 3.4 - t * 1.2;
    float nuvem = fbm(vec2(cos(tw), sin(tw)) * 2.4 + vec2(log(r) * 3.1 + t * 1.96, log(r) * 1.2));
    float braco = 0.5 + 0.5 * sin(a * 2.0 - log(r) * 7.0 + TIME * 1.1 + nuvem * 3.0);
    float v = clamp(nuvem * 0.75 + braco * 0.45, 0.0, 1.0);
    float buraco = smoothstep(0.03, 0.32, r);            // centro preto (o fundo do túnel)
    float borda = smoothstep(0.42, 0.62, r);              // anel externo mais claro e esfumado
    vec3 fundo = mix(vec3(0.04, 0.01, 0.09), vec3(0.06, 0.0, 0.0), vermelho);
    vec3 meio = mix(vec3(0.30, 0.16, 0.62), vec3(0.72, 0.10, 0.04), vermelho);
    vec3 claro = mix(vec3(0.72, 0.62, 0.98), vec3(1.0, 0.52, 0.18), vermelho);
    vec3 col = mix(fundo, meio, v * buraco);
    col = mix(col, claro, pow(v, 2.2) * buraco * (0.35 + 0.65 * borda));
    col *= 0.25 + 0.75 * buraco;
    float edge = smoothstep(0.5, 0.44, abs(UV.x - 0.5)) * smoothstep(0.5, 0.46, abs(UV.y - 0.5));
    ALBEDO = col;
    ALPHA = 0.95 * edge;
}";

    private void AddPortal(MapPortal portal)
    {
        if (Visuals.EscadaCasa.EhPortalDeEscada(portal.X, portal.Y)) return;   // 11/11: a escada é a passagem (sem arco de pedra)
        if (portal.PorPorta) return;   // 13/11: passagem pela porta (F), sem arco
        // 11/11 noite ("o portal não preenche o portão, aparece o chão atrás"): portal logo atrás de um portão de cidade
        // vira um véu que fecha a boca do portão inteira (sem o arco de pedra)
        var gate = Map.Props.FirstOrDefault(pp => pp.Kind.Contains("portao") && new Vector2((float)(pp.X - portal.X), (float)(pp.Y - portal.Y)).Length() < 5f);
        if (gate is not null)
        {
            var dir = new Vector2((float)(portal.X - gate.X), (float)(portal.Y - gate.Y)).Normalized();
            var gnode = new Node3D { Position = P(gate.X + dir.X * 0.4, gate.Y + dir.Y * 0.4) };
            gnode.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Y), 0);
            Root.AddChild(gnode);
            var gveil = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(4.2f, 4.6f) }, MaterialOverride = VortexMat(false),
                Position = new Vector3(0, 2.3f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            gnode.AddChild(gveil);
            gnode.AddChild(new OmniLight3D { LightColor = new Color(0.7f, 0.45f, 1f), LightEnergy = 1.2f, OmniRange = 7f, Position = new Vector3(0, 2.2f, -1f), ShadowEnabled = false });
            var gl = new Label3D
            {
                Text = "→ " + portal.Label + (portal.MinLevel > 0 ? $"  (nível {portal.MinLevel}+)" : ""),
                Position = new Vector3(0, 5.4f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 56, OutlineSize = 12,
                Modulate = new Color(0.85f, 0.92f, 1f), PixelSize = 0.01f,
            };
            gnode.AddChild(gl);
            if (portal.LevelLock && portal.MinLevel > 0) _travados.Add((portal, gl, gveil));
            if (portal.RequiresFlag is { Length: > 0 }) _travadosMarca.Add((portal, gl, gveil));
            return;
        }
        // arco de pedra virado para o centro do mapa, com véu brilhante e o nome do destino
        var node = new Node3D { Position = P(portal.X, portal.Y) };
        var toCenter = new Vector2((float)-portal.X, (float)-portal.Y);
        node.Rotation = new Vector3(0, toCenter.LengthSquared() > 0.01f ? Mathf.Atan2(toCenter.X, toCenter.Y) : 0, 0);
        Root.AddChild(node);
        var stone = StoneMat(0.5f, true);
        var width = (float)portal.Radius * 1.6f;
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(node, VisualKit.Box(0.6f, 3.6f, 0.6f), stone, new Vector3(side * width / 2f, 1.8f, 0));
        VisualKit.Part(node, VisualKit.Box(width + 0.8f, 0.6f, 0.7f), stone, new Vector3(0, 3.8f, 0));
        // 11/11 noite (pedido: "parado, roxo, com um vórtex no meio, tipo o portal do fundo do Mortal Kombat, sem chamar
        // muita atenção"): véu fixo com espiral girando devagar por dentro
        var veil = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(width - 0.2f, 3.4f) }, MaterialOverride = VortexMat(false),
            Position = new Vector3(0, 1.7f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        node.AddChild(veil);
        node.AddChild(new OmniLight3D { LightColor = new Color(0.7f, 0.45f, 1f), LightEnergy = 0.9f, OmniRange = 5f, Position = new Vector3(0, 2f, 0.6f) });
        var lbl = new Label3D
        {
            Text = "→ " + portal.Label + (portal.MinLevel > 0 ? $"  (nível {portal.MinLevel}+)" : ""),
            Position = new Vector3(0, 4.6f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 56, OutlineSize = 12,
            Modulate = new Color(0.85f, 0.92f, 1f), PixelSize = 0.01f,
        };
        node.AddChild(lbl);
        if (portal.LevelLock && portal.MinLevel > 0) _travados.Add((portal, lbl, veil));
        if (portal.RequiresFlag is { Length: > 0 }) _travadosMarca.Add((portal, lbl, veil));
    }

    /// <summary>13/11 ("passagens com limitação são vermelhas"): nome em vermelho com cadeado enquanto o nível não chega.</summary>
    private readonly List<(MapPortal Portal, Label3D Label, MeshInstance3D Veu)> _travados = new();
    /// <summary>13/11 (regra: "passagem que não dá para acessar = portal vermelho"): passagem que pede uma marca fica vermelha até ter.</summary>
    private readonly List<(MapPortal Portal, Label3D Label, MeshInstance3D Veu)> _travadosMarca = new();
    private static ShaderMaterial? _vortexRed;
    private ShaderMaterial VortexMat(bool vermelho)
    {
        if (vermelho) { _vortexRed ??= new ShaderMaterial { Shader = _vortexMat?.Shader ?? new Shader { Code = VortexShader } }; _vortexRed.SetShaderParameter("vermelho", 1f); return _vortexRed; }
        return _vortexMat ??= new ShaderMaterial { Shader = _vortexRed?.Shader ?? new Shader { Code = VortexShader } };
    }
    /// <summary>13/11 (regra do dono): passagem que não dá para usar = portão vermelho. Arco de pedra com o véu do portal
    /// em vermelho e o aviso em cima. Devolve o véu (para trocar de cor quando abrir).</summary>
    private MeshInstance3D PortaoVermelho(Vector3 pos, float yaw, float largura, float altura, string aviso, bool arco = true)
    {
        var g = new Node3D { Position = pos, Rotation = new Vector3(0, yaw, 0) };
        Root.AddChild(g);
        if (arco)
        {
            var pedra = StoneMat(0.5f, true);
            foreach (var lado in new[] { -1f, 1f })
                VisualKit.Part(g, VisualKit.Box(0.35f, altura + 0.2f, 0.4f), pedra, new Vector3(lado * (largura / 2f + 0.15f), (altura + 0.2f) / 2f, 0));
            VisualKit.Part(g, VisualKit.Box(largura + 0.7f, 0.35f, 0.45f), pedra, new Vector3(0, altura + 0.3f, 0));
        }
        var veu = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(largura, altura) }, MaterialOverride = VortexMat(true),
            Position = new Vector3(0, altura / 2f, 0.05f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        g.AddChild(veu);
        g.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.3f, 0.22f), LightEnergy = 0.7f, OmniRange = 3.5f, Position = new Vector3(0, altura * 0.5f, 0.8f), ShadowEnabled = false });
        g.AddChild(new Label3D { Text = "Bloqueado · " + aviso, Position = new Vector3(0, altura + 0.9f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 40, OutlineSize = 10, PixelSize = 0.01f, Modulate = new Color(1f, 0.4f, 0.32f) });
        return veu;
    }
    public int NivelJogador { get; set; } = 99;
    private int _nivelPintado = -1;

    /// <summary>11/11 noite: marca que o jogador já tem (pedra de viagem descoberta acende).</summary>
    public Func<string, bool>? HasFlag { get; set; }
    private readonly List<(MapWaypoint Wp, StandardMaterial3D Crystal, OmniLight3D Light, MeshInstance3D Ring)> _stones = new();
    private readonly List<(MapWaypoint Wp, Node3D Chama)> _chamasPedra = new();
    private readonly HashSet<string> _pedrasVistas = new();

    private void AddWaypoint(MapWaypoint wp)
    {
        // pedra de viagem: obelisco baixo com cristal azul, ao lado da fogueira do acampamento.
        // 11/11 noite (pedido): sólida (não atravessa) e o cristal acende azul forte quando você já a descobriu (tocou);
        // antes disso fica azul apagado, meio transparente
        var node = new Node3D { Position = P(wp.X + 2.2, wp.Y - 0.6, 0.06f) };
        Root.AddChild(node);
        VisualKit.Part(node, VisualKit.Cylinder(0.25f, 0.4f, 1.6f, 5), StoneMat(0.6f, false), new Vector3(0, 0.8f, 0));
        VisualKit.Part(node, VisualKit.Cylinder(0.55f, 0.62f, 0.14f, 8), StoneMat(0.5f, false), new Vector3(0, 0.0f, 0));   // base acima das lajes
        var crystal = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.6f, 1f, 0.45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true, Emission = new Color(0.3f, 0.6f, 1f), EmissionEnergyMultiplier = 0.15f, Roughness = 0.15f, Metallic = 0.2f,
        };
        VisualKit.Part(node, VisualKit.Cylinder(0f, 0.16f, 0.42f, 6), crystal, new Vector3(0, 1.82f, 0));       // ponta do cristal
        VisualKit.Part(node, VisualKit.Cylinder(0.16f, 0f, 0.22f, 6), crystal, new Vector3(0, 1.5f, 0), new Vector3(180, 0, 0));
        VisualKit.Part(node, VisualKit.Box(0.08f, 0.5f, 0.42f), crystal, new Vector3(0, 0.95f, 0.02f));
        var light = new OmniLight3D { LightColor = new Color(0.45f, 0.7f, 1f), LightEnergy = 0f, OmniRange = 6f, Position = new Vector3(0, 1.8f, 0), ShadowEnabled = false };
        node.AddChild(light);
        // 13/11 ("a luz azul da pedra de TP está muito simples"): círculo de runas azul no chão (o mesmo desenho da aura
        // dourada), uma lasca de cristal flutuando e girando em cima do obelisco e um véu de luz bem fraco subindo
        var runas = new ShaderMaterial { Shader = new Shader { Code = Visuals.MannequinVisual.RunasShader } };
        runas.SetShaderParameter("cor", new Vector3(0.45f, 0.72f, 1f));
        runas.SetShaderParameter("forca", 0.7f);
        // 13/11 ("a marca azul da pedra da praça continua ABAIXO do chão de tijolos cinza"): o caminho de lajes é transparente
        // com prioridade 1 e era desenhado por cima; as runas passam a ser desenhadas depois dele
        runas.RenderPriority = 3;
        // 13/11 ("a marcação da pedra da praça está dentro do chão do caminho de pedra"): acima das lajes e do calçamento
        var ring = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(3.4f, 3.4f) }, MaterialOverride = runas, Position = P(wp.X, wp.Y, 0.13f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        Root.AddChild(ring);
        var lasca = new Node3D { Name = "Lasca", Position = new Vector3(0, 2.35f, 0) };
        node.AddChild(lasca);
        VisualKit.Part(lasca, VisualKit.Cylinder(0f, 0.09f, 0.22f, 5), crystal, new Vector3(0, 0.11f, 0));
        VisualKit.Part(lasca, VisualKit.Cylinder(0.09f, 0f, 0.16f, 5), crystal, new Vector3(0, -0.08f, 0));
        var veu = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.75f, 1f, 0.06f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        // 13/11 ("suaviza o cone de luz, bem sutil, quase transparente, igual uma luz; e o feixe que encosta no cristal está
        // mais grosso que a ponta da pirâmide"): o feixe nasce fininho NA ponta do cristal (y 2,03) e abre um pouco para
        // cima, some nas bordas e no alto — só um rastro de luz
        var veuSutil = new ShaderMaterial { Shader = _veuPedra ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
varying float h;
void vertex() { h = clamp(VERTEX.y / 2.6 + 0.5, 0.0, 1.0); }
void fragment() {
    float frente = pow(abs(dot(normalize(NORMAL), VIEW)), 2.5);
    float sobe = smoothstep(0.0, 0.08, h) * pow(1.0 - h, 1.8);
    ALBEDO = vec3(0.55, 0.78, 1.0);
    ALPHA = 0.09 * frente * sobe * (0.85 + 0.15 * sin(TIME * 1.8 - h * 6.0));
}" } };
        // 14/11 18h (dono, 2º print: "a parada preta continua em cima da pedra de viagem" — fica exatamente no alto do feixe,
        // 4,6 m): o véu com shader próprio virava uma mancha preta em algumas placas de vídeo na ponta aberta do cilindro;
        // agora é material padrão (aditivo, sem sombra), afinando até sumir — sem conta de shader que possa dar NaN
        _ = veuSutil;
        var feixeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.55f, 0.85f, 0.07f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableReceiveShadows = true, NoDepthTest = false,
        };
        var feixe = VisualKit.Part(node, new CylinderMesh { TopRadius = 0.004f, BottomRadius = 0.05f, Height = 2.2f, RadialSegments = 10, Rings = 1, CapTop = false, CapBottom = false }, feixeMat, new Vector3(0, 2.03f + 1.1f, 0));
        feixe.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _lascas.Add((lasca, ring, feixe, R(0, 6)));
        // 13/11 ("bota o efeito da pedra de viagem tipo o do fogo, só que azul"): chama azul de verdade no alto do obelisco
        // 14/11 ("ao destravar a pedra, o feixe de luz pro céu liga; antes fica desligada"): a chama azul também só acende
        // depois de descoberta (junto com o feixe, as runas e a lasca)
        var chama = new Node3D { Name = "ChamaAzul", Visible = false };
        node.AddChild(chama);
        AddFlame(chama, new Vector3(0, 2.02f, 0), 0.8f, 0.9f, 5f, azul: true);
        _chamasPedra.Add((wp, chama));
        _stones.Add((wp, crystal, light, ring));
        node.AddChild(new Label3D
        {
            Text = wp.Name, Position = new Vector3(0, 2.5f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40,
            OutlineSize = 10, Modulate = new Color(0.7f, 0.85f, 1f), PixelSize = 0.01f,
        });
    }

    /// <summary>Por quadro: cristal aceso (descoberta) ou apagado; pulsa devagar quando aceso.</summary>
    private readonly List<(Node3D Lasca, MeshInstance3D Runas, MeshInstance3D Feixe, float Fase)> _lascas = new();
    private static Shader? _veuPedra;
    private readonly List<(Node3D No, float Fase)> _orbitas = new();

    private void UpdateStones(float time)
    {
        // 15/11 noite (log do dono: ObjectDisposedException aqui depois de remontar o mapa): pula o que já foi liberado
        foreach (var (orb, fo) in _orbitas) if (GodotObject.IsInstanceValid(orb)) orb.Rotation = new Vector3(0, time * 1.3f + fo, 0);
        foreach (var (lasca, runas, feixe, fase) in _lascas)
        {
            if (!GodotObject.IsInstanceValid(lasca) || !GodotObject.IsInstanceValid(runas) || !GodotObject.IsInstanceValid(feixe)) continue;
            lasca.Rotation = new Vector3(0, time * 0.9f + fase, 0);
            lasca.Position = new Vector3(0, 2.35f + 0.08f * Mathf.Sin(time * 1.7f + fase), 0);
            lasca.Visible = feixe.Visible = runas.Visible;
        }
        foreach (var (wp, chama) in _chamasPedra)
        {
            var aceso = HasFlag?.Invoke("wp:" + wp.Id) == true;
            if (chama.Visible != aceso) { chama.Visible = aceso; if (aceso && _pedrasVistas.Count > 0 && !_pedrasVistas.Contains(wp.Id)) Sfx.Play("portal", -6f); }
            if (aceso) _pedrasVistas.Add(wp.Id);
        }
        if (_pedrasVistas.Count == 0) _pedrasVistas.Add("");   // 1º quadro: as já descobertas acendem sem som
        foreach (var (wp, crystal, light, ring) in _stones)
        {
            var on = HasFlag?.Invoke("wp:" + wp.Id) == true;
            var pulse = 0.85f + 0.15f * Mathf.Sin(time * 2f + (float)wp.X);
            crystal.AlbedoColor = on ? new Color(0.45f, 0.75f, 1f, 0.95f) : new Color(0.35f, 0.55f, 0.9f, 0.4f);
            crystal.EmissionEnergyMultiplier = on ? 2.6f * pulse : 0.12f;
            light.LightEnergy = on ? 1.4f * pulse : 0f;
            ring.Visible = on;
        }
    }

    private void AddNpc(MapNpc npc)
    {
        var visual = VisualFactory.NpcVisual(npc.Look, npc.Id, npc.Name);
        visual.Root.Position = new Vector3((float)npc.X, Visuals.EscadaCasa.Altura(npc.X, npc.Y), (float)npc.Y);
        visual.Root.RotationDegrees = new Vector3(0, (float)npc.Facing, 0);
        Root.AddChild(visual.Root);
        if (npc.Look == "ferreiro")
        {
            // 12/11 ("é uma caixa voando, não é bigorna"): bigorna de verdade, apoiada no chão em cima de um toco
            // (o servidor tem um sólido no mesmo lugar: MapGeometry.BigornaDe)
            var (ax, ay) = MapGeometry.BigornaDe(npc);
            var anvil = new Node3D { Name = "Bigorna", Position = P(ax, ay), Rotation = new Vector3(0, Mathf.DegToRad((float)npc.Facing), 0) };
            Root.AddChild(anvil);
            var toco = TexMat("gen_madeira.png", new Color(0.62f, 0.48f, 0.36f), 1.2f);
            var ferro = VisualKit.Mat(new Color(0.16f, 0.16f, 0.18f), 0.35f, 0.85f);
            VisualKit.Part(anvil, VisualKit.Cylinder(0.30f, 0.34f, 0.5f, 10), toco, new Vector3(0, 0.25f, 0));          // toco
            VisualKit.Part(anvil, VisualKit.Box(0.30f, 0.10f, 0.42f), ferro, new Vector3(0, 0.55f, 0));                   // pé
            VisualKit.Part(anvil, VisualKit.Box(0.16f, 0.14f, 0.26f), ferro, new Vector3(0, 0.67f, 0));                   // cintura
            VisualKit.Part(anvil, VisualKit.Box(0.26f, 0.13f, 0.56f), ferro, new Vector3(0, 0.80f, 0.02f));               // mesa
            var chifre = VisualKit.Part(anvil, VisualKit.Cylinder(0f, 0.11f, 0.30f, 8), ferro, new Vector3(0, 0.80f, -0.40f));
            chifre.RotationDegrees = new Vector3(-90, 0, 0);                                                              // ponta (bico)
            _bigornas[npc.Id] = anvil.Position + new Vector3(0, 0.88f, 0);
            // 10/11: o ferreiro trabalha debaixo de um telhadinho de tábuas (4 esteios e um teto inclinado)
            if (!npc.Name.Contains("Cozinh") && !npc.Name.Contains("Mineiro"))
            {
                var roofWood = TexMat("gen_madeira.png", new Color(0.75f, 0.62f, 0.5f), 0.85f);
                var post = VisualKit.Mat(new Color(0.30f, 0.20f, 0.12f), 0.9f);
                var c = P(npc.X + 0.6, npc.Y + 0.3);
                foreach (var dx in new[] { -1.8f, 1.8f })
                    foreach (var dz in new[] { -1.5f, 1.5f })
                        VisualKit.Part(Root, VisualKit.Cylinder(0.07f, 0.09f, 2.9f, 6), post, c + new Vector3(dx, 1.45f + (dz > 0 ? -0.15f : 0.15f), dz));
                VisualKit.Part(Root, VisualKit.Box(4.2f, 0.12f, 3.6f), roofWood, c + new Vector3(0, 2.95f, 0), new Vector3(-8, 0, 0));
                for (var i = 0; i < 5; i++)
                    VisualKit.Part(Root, VisualKit.Box(0.14f, 0.08f, 3.7f), post, c + new Vector3(-1.8f + i * 0.9f, 3.04f, 0), new Vector3(-8, 0, 0));
            }
        }
        if (npc.Id.StartsWith("oficio_dancarino", StringComparison.Ordinal))
            visual.Root.Position = P(npc.X, npc.Y, 0.32f);   // 13/11: o Zeca dança EM CIMA do tablado
        if (npc.Look == "bobo")
        {
            // 10/11: o bobo da corte se apresenta em cima de caixotes
            var crate = TexMat("gen_madeira.png", new Color(0.85f, 0.7f, 0.55f), 0.8f);
            VisualKit.Part(Root, VisualKit.Box(1.1f, 0.5f, 1.1f), crate, P(npc.X, npc.Y, 0.25f), new Vector3(0, 12, 0));
            VisualKit.Part(Root, VisualKit.Box(0.95f, 0.45f, 0.95f), crate, P(npc.X, npc.Y, 0.72f), new Vector3(0, -9, 0));
            VisualKit.Part(Root, VisualKit.Box(0.7f, 0.42f, 0.7f), crate, P(npc.X + 1.0, npc.Y + 0.3, 0.21f), new Vector3(0, 30, 0));
            visual.Root.Position = P(npc.X, npc.Y, 0.95f);
        }
        _npcs.Add(visual);
        _life.Add(new NpcLife { Npc = npc, Visual = visual, HomeYaw = Mathf.DegToRad((float)npc.Facing), Yaw = Mathf.DegToRad((float)npc.Facing),
            Timer = R(1f, 6f), Work = npc.Look == "ferreiro" && !npc.Name.Contains("Cozinh") && !npc.Name.Contains("Mineiro") ? "martelo"
                : npc.Name.Contains("Agricult") ? "horta" : npc.Look is "arcanista" or "curandeira" && !npc.Name.Contains("Cozinh") ? "magia"
                : npc.Look == "guarda" ? "guarda" : npc.Look == "bobo" ? "bobo" : npc.Id.StartsWith("oficio_dancarino", StringComparison.Ordinal) ? "danca"
                : npc.Look is "capita" or "batedora" || npc.Name.Contains("Pescador") ? "vigia" : "" });
        // marcador de missão (pedido 10/11: "mais brilho, girando, como se houvesse uma luz ali"): "!" (novo) ou "?"
        // (entregar) grande por cima, losango lapidado girando, halo que pulsa e uma luz de verdade iluminando a cabeça
        var marker = new Node3D { Position = new Vector3(0, 2.55f, 0), Name = "MarcadorMissao" };
        visual.Root.AddChild(marker);
        var gem = new Node3D { Name = "Gema" };
        marker.AddChild(gem);
        VisualKit.Part(gem, VisualKit.Cylinder(0f, 0.17f, 0.26f, 4), MarkerNew, new Vector3(0, 0.13f, 0));
        VisualKit.Part(gem, VisualKit.Cylinder(0.17f, 0f, 0.26f, 4), MarkerNew, new Vector3(0, -0.13f, 0));
        var sign = new Label3D
        {
            Name = "Sinal", Text = "!", FontSize = 150, OutlineSize = 26, PixelSize = 0.004f, Position = new Vector3(0, 0.62f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = false, Shaded = false,
            Modulate = new Color(1f, 0.82f, 0.25f), OutlineModulate = new Color(0.25f, 0.12f, 0f), Font = HudView.TitleFont,
        };
        marker.AddChild(sign);
        var halo = new MeshInstance3D
        {
            Name = "Halo", Mesh = new QuadMesh { Size = new Vector2(1.3f, 1.3f) }, Position = new Vector3(0, 0.3f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                AlbedoTexture = HaloTexture(), AlbedoColor = new Color(1f, 0.75f, 0.25f, 0.8f), DisableReceiveShadows = true,
            },
        };
        marker.AddChild(halo);
        var light = new OmniLight3D { Name = "Luz", LightColor = new Color(1f, 0.78f, 0.35f), LightEnergy = 1.4f, OmniRange = 3.8f, Position = new Vector3(0, 0.2f, 0) };
        marker.AddChild(light);
        _questMarks.Add((marker, gem, sign, halo, light, R(0, 6)));
        _npcNodes.Add((npc, visual.Root, marker));
    }

    /// <summary>Objeto de missão (relíquia, grimório): pedestal com brilho; só aparece com a missão ativa.</summary>
    private readonly Dictionary<string, Node3D> _plotPlants = new();

    /// <summary>Peça de ofício/horta/armazém (veio, canteiro, estações); falso = é relíquia de missão (desenho abaixo).</summary>
    private bool AddCraftObject(MapObject obj)
    {
        var wood = VisualKit.Mat(new Color(0.33f, 0.22f, 0.13f), 0.9f);
        switch (obj.Kind)
        {
            case "veio":
            {
                var node = new Node3D { Position = P(obj.X, obj.Y), RotationDegrees = new Vector3(0, R(0, 360), 0) };
                Root.AddChild(node);
                var y = obj.Yields ?? "";
                // 18/11 19h (dono: "você trocou o que dropa das pedras, mas não trocou a imagem da pedra que farmamos — faça isso
                // com todos"): a cor do veio/da moita é a do material que ele dá (a mesma do ícone); planta nunca vira pedra
                var corItem = ItemIconRenderer.CorDe(y);
                var planta = y.Contains("erva") || y.Contains("folha") || y.Contains("cogumelo") || y.Contains("flor") || y.Contains("alga") || y.Contains("cardo") || y.Contains("raiz");
                if (y.Contains("ferro") && KitTapa.Veio(node, y))
                {
                    if (y is not ("mat_ferro_eco" or "mat_ferro_raso"))
                    {
                        foreach (var l in node.GetChildren().OfType<OmniLight3D>()) l.LightColor = corItem;
                        for (var i = 0; i < 5; i++)
                            VisualKit.Part(node, VisualKit.Box(0.1f, 0.24f, 0.1f), VisualKit.Glow(corItem, 1.3f),
                                new Vector3(R(-0.45f, 0.45f), R(0.35f, 0.7f), R(-0.4f, 0.4f)), new Vector3(R(-30, 30), R(0, 90), R(-30, 30)));
                    }
                    return true;
                }
                if (y.Contains("erva") && KitTapa.Veio(node, y))
                {
                    if (y != "mat_erva_tonico")
                        for (var i = 0; i < 5; i++)
                            VisualKit.Part(node, VisualKit.Sphere(0.07f), VisualKit.Glow(corItem, 0.7f), new Vector3(R(-0.35f, 0.35f), R(0.4f, 0.6f), R(-0.35f, 0.35f)));
                    return true;
                }
                if (!planta && KitTapa.Veio(node, y)) return true;   // 12/11: árvore de tábua, moita de erva, pedra de cristais (pacote Vila)
                if (y == "mat_tabua")
                {
                    // árvore seca para tábuas: tronco nu, galhos tortos e um machado cravado no toco ao lado
                    var bark = VisualKit.Mat(new Color(0.30f, 0.24f, 0.18f), 0.95f);
                    VisualKit.Part(node, VisualKit.Cylinder(0.16f, 0.28f, 4.2f, 7), bark, new Vector3(0, 2.1f, 0), new Vector3(R(-4, 4), 0, R(-4, 4)));
                    for (var i = 0; i < 4; i++)
                        VisualKit.Part(node, VisualKit.Cylinder(0.03f, 0.08f, R(1.0f, 1.6f), 5), bark, new Vector3(0, R(2.4f, 3.8f), 0),
                            new Vector3(R(35, 65), i * 90 + R(-20, 20), 0)).Position += new Vector3(0, 0, 0);
                    VisualKit.Part(node, VisualKit.Cylinder(0.32f, 0.36f, 0.5f, 8), bark, new Vector3(1.0f, 0.25f, 0.4f));
                    VisualKit.Part(node, VisualKit.Box(0.05f, 0.55f, 0.05f), VisualKit.Mat(new Color(0.45f, 0.32f, 0.2f), 0.8f), new Vector3(1.0f, 0.7f, 0.4f), new Vector3(0, 0, 25));
                    VisualKit.Part(node, VisualKit.Box(0.2f, 0.14f, 0.03f), VisualKit.Mat(new Color(0.55f, 0.57f, 0.6f), 0.3f, 0.8f), new Vector3(1.08f, 0.95f, 0.4f));
                }
                else if (planta)
                {
                    // moita: tufos verdes com flores/brotos na cor do material (alga: tufos na água; cogumelo: chapéus)
                    var leaf = VisualKit.Mat(y.Contains("inverno") ? new Color(0.55f, 0.65f, 0.7f) : y.Contains("alga") ? new Color(0.12f, 0.35f, 0.3f) : new Color(0.22f, 0.36f, 0.20f), 0.95f);
                    for (var i = 0; i < 5; i++)
                        VisualKit.Part(node, VisualKit.Sphere(R(0.25f, 0.4f)), leaf, new Vector3(R(-0.4f, 0.4f), 0.2f, R(-0.4f, 0.4f)), null, new Vector3(1f, 0.7f, 1f));
                    var flower = corItem;
                    for (var i = 0; i < 4; i++)
                        VisualKit.Part(node, VisualKit.Sphere(0.06f), VisualKit.Glow(flower, 0.5f), new Vector3(R(-0.35f, 0.35f), 0.45f, R(-0.35f, 0.35f)));
                }
                else if (y.Contains("ferro") && (Visuals.GlbCache.Instance("itens/item_minerio_v2") ?? Visuals.GlbCache.Instance("itens/item_minerio")) is { } oreGlb)
                {
                    // 10/11 ("trocar a arte do minério e deixar menor, como a erva"): o minério 3D novo, baixo e no chão
                    node.AddChild(oreGlb);
                    oreGlb.Scale = Vector3.One * 2.8f;   // o modelo vem em tamanho real (~0,36 m): no chão fica ~1 m, como a moita
                    VisualKit.Part(node, VisualKit.Sphere(0.45f), StoneMat(0.42f, false), new Vector3(0, 0.02f, 0), null, new Vector3(1.4f, 0.35f, 1.2f));
                }
                else
                {
                    node.Scale = Vector3.One * 0.6f;   // 10/11: veios menores (do tamanho de uma moita)
                    var rock = VisualKit.Mat(new Color(0.26f, 0.25f, 0.26f), 0.9f);
                    var ore = y.Contains("basalto") ? new Color(1f, 0.35f, 0.1f) : corItem;
                    VisualKit.Part(node, VisualKit.Sphere(0.75f), rock, new Vector3(0, 0.35f, 0), null, new Vector3(1.3f, 0.75f, 1f));
                    VisualKit.Part(node, VisualKit.Sphere(0.45f), rock, new Vector3(0.6f, 0.25f, 0.3f), null, new Vector3(1f, 0.8f, 1f));
                    for (var i = 0; i < 5; i++)
                        VisualKit.Part(node, VisualKit.Box(0.12f, 0.22f, 0.12f), VisualKit.Glow(ore, 1.2f),
                            new Vector3(R(-0.6f, 0.6f), R(0.45f, 0.75f), R(-0.35f, 0.45f)), new Vector3(R(-30, 30), R(0, 90), R(-30, 30)));
                }
                return true;
            }
            case "barris_polvora":
            {
                // 11/11: pilha de barris da Nara (missão do muro): três no chão e um deitado em cima, com marca vermelha
                var node = new Node3D { Position = P(obj.X, obj.Y), RotationDegrees = new Vector3(0, R(0, 360), 0) };
                Root.AddChild(node);
                MapKit.TryBuild(this, node, "barril", wood);
                var top = new Node3D { Position = new Vector3(0, 0.95f, 0), RotationDegrees = new Vector3(0, 0, 90) };
                node.AddChild(top);
                MapKit.Glb(top, "cenario_barril", Vector3.Zero, 0f, 0.6f);
                VisualKit.Part(node, VisualKit.Box(0.5f, 0.5f, 0.02f), VisualKit.Glow(new Color(0.85f, 0.15f, 0.1f), 0.6f), new Vector3(0, 0.55f, 0.32f), new Vector3(0, 0, 45));
                return true;
            }
            case "canteiro":
            {
                var node = new Node3D { Position = P(obj.X, obj.Y) };
                Root.AddChild(node);
                MapKit.TryBuild(this, node, "canteiro", wood);
                var plant = new Node3D { Visible = false };
                node.AddChild(plant);
                var stem = VisualKit.Mat(new Color(0.30f, 0.50f, 0.25f), 0.9f);
                for (var i = 0; i < 12; i++)   // 3 mudas em cada uma das 4 leiras
                    VisualKit.Part(plant, VisualKit.Cylinder(0.01f, 0.04f, 0.5f, 5), stem, new Vector3(-1.2f + (i % 4) * 0.8f, 0.42f, -1.0f + (i / 4) * 1.0f));
                var bloom = VisualKit.Part(plant, VisualKit.Sphere(0.12f), VisualKit.Glow(new Color(0.9f, 0.8f, 0.35f), 0.8f), new Vector3(0, 0.66f, 0));
                bloom.Name = "Flor";
                _plotPlants[obj.Id] = plant;
                return true;
            }
            case "forja" or "bancada" or "caldeirao" or "panela" or "armazem":
            {
                var propKind = obj.Kind == "armazem" ? "bau_armazem" : obj.Kind;
                // o enfeite que já estava no mapa (e colide no servidor) passa a ser desenhado aqui, no mesmo lugar e giro
                var prop = Map.Props.FirstOrDefault(pr => pr.Kind == propKind && (pr.X - obj.X) * (pr.X - obj.X) + (pr.Y - obj.Y) * (pr.Y - obj.Y) < 12);
                var node = prop is not null
                    ? new Node3D { Position = P(prop.X, prop.Y), RotationDegrees = new Vector3(0, (float)prop.Yaw, 0) }
                    : new Node3D { Position = P(obj.X, obj.Y) };
                Root.AddChild(node);
                if (!KitTapa.Oficina(this, node, propKind)) MapKit.TryBuild(this, node, propKind, wood);   // 12/11: modelos do pacote Vila
                // som no lugar: forja martelando, caldeirão borbulhando (some com a distância)
                if (obj.Kind == "forja") Sfx.Loop3D(node, "loop_forja", -6f, 2.5f, 14f);   // 17/11 noite: só perto da forja
                else if (obj.Kind is "caldeirao" or "panela") Sfx.Loop3D(node, "loop_caldeirao", -6f, 2.5f, 12f);
                return true;
            }
            case "caos":
            {
                // 11/11: Máquina do Caos (MU) — pilar de runa do pacote da noite, maior, com brilho roxo girando
                var node = new Node3D { Position = P(obj.X, obj.Y) };
                Root.AddChild(node);
                if (Visuals.GlbCache.Instance("interior/puzzle_runa") is { } pilar) { pilar.Scale = Vector3.One * 1.35f; node.AddChild(pilar); }
                else VisualKit.Part(node, VisualKit.Box(0.9f, 2.2f, 0.9f), StoneMat(0.5f, false), new Vector3(0, 1.1f, 0));
                var orb = VisualKit.Part(node, VisualKit.Sphere(0.22f), VisualKit.Glow(new Color(0.75f, 0.35f, 1f), 5f), new Vector3(0, 2.9f, 0));
                _portalGlows.Add((orb, R(0, 6)));
                node.AddChild(new OmniLight3D { LightColor = new Color(0.75f, 0.4f, 1f), LightEnergy = 1.6f, OmniRange = 6f, Position = new Vector3(0, 2.6f, 0), ShadowEnabled = false });
                node.AddChild(new Label3D
                {
                    Text = obj.Label, FontSize = 40, OutlineSize = 8, PixelSize = 0.01f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    Modulate = new Color(0.85f, 0.65f, 1f), Position = new Vector3(0, 3.5f, 0),
                });
                return true;
            }
            case "altar":
            {
                // altar de oferenda: pedra baixa com cascas de cal empilhadas e uma chama verde-venenosa no meio
                var node = new Node3D { Position = P(obj.X, obj.Y) };
                Root.AddChild(node);
                var stone = MapView.TexMat("gen_bloco.png", new Color(0.78f, 0.74f, 0.66f), 0.9f);
                VisualKit.Part(node, VisualKit.Cylinder(1.0f, 1.2f, 0.6f, 10), stone, new Vector3(0, 0.3f, 0));
                VisualKit.Part(node, VisualKit.Cylinder(0.7f, 0.85f, 0.35f, 10), stone, new Vector3(0, 0.78f, 0));
                var shell = VisualKit.Mat(new Color(0.92f, 0.88f, 0.78f), 0.5f);
                for (var i = 0; i < 9; i++)
                {
                    var a = i * Mathf.Tau / 9f;
                    VisualKit.Part(node, VisualKit.Sphere(0.16f), shell, new Vector3(Mathf.Cos(a) * 0.95f, 0.66f, Mathf.Sin(a) * 0.95f),
                        new Vector3(R(-20, 20), R(0, 180), 0), new Vector3(1.2f, 0.45f, 1f));
                }
                foreach (var side in new[] { -1f, 1f })   // pinças de pedra como chifres
                    VisualKit.Part(node, VisualKit.Cylinder(0.03f, 0.14f, 1.1f, 6), stone, new Vector3(side * 0.62f, 1.35f, 0), new Vector3(0, 0, side * -22));
                AddFlame(node, new Vector3(0, 1.05f, 0), 0.9f, 2.6f, 6f);
                node.AddChild(new OmniLight3D { LightColor = new Color(0.5f, 1f, 0.4f), LightEnergy = 1.3f, OmniRange = 5f, Position = new Vector3(0, 1.4f, 0) });
                return true;
            }
            case "reforma_casa":   // 15/11 noite: placa de obra (reformar a casa) — cavalete com a planta da casa e um martelo
            {
                var node = new Node3D { Position = P(obj.X, obj.Y) };
                Root.AddChild(node);
                var madeira = MapView.TexMat("gen_madeira.png", new Color(0.8f, 0.62f, 0.4f), 0.6f);
                foreach (var sd in new[] { -1f, 1f }) VisualKit.Part(node, VisualKit.Box(0.08f, 1.5f, 0.08f), madeira, new Vector3(sd * 0.45f, 0.75f, 0), new Vector3(-8, 0, sd * -6));
                VisualKit.Part(node, VisualKit.Box(1.05f, 0.75f, 0.04f), VisualKit.Mat(new Color(0.88f, 0.84f, 0.72f), 0.8f), new Vector3(0, 1.15f, 0.04f), new Vector3(-8, 0, 0));
                var azul = VisualKit.Mat(new Color(0.15f, 0.3f, 0.7f), 0.6f);
                VisualKit.Part(node, VisualKit.Box(0.5f, 0.03f, 0.01f), azul, new Vector3(0, 0.95f, 0.08f), new Vector3(-8, 0, 0));
                VisualKit.Part(node, new PrismMesh { Size = new Vector3(0.6f, 0.28f, 0.01f) }, azul, new Vector3(0, 1.32f, 0.07f), new Vector3(-8, 0, 0));
                VisualKit.Part(node, VisualKit.Box(0.42f, 0.3f, 0.01f), azul, new Vector3(0, 1.1f, 0.075f), new Vector3(-8, 0, 0), new Vector3(1f, 1f, 0.5f));
                VisualKit.Part(node, VisualKit.Box(0.06f, 0.32f, 0.04f), madeira, new Vector3(0.62f, 0.2f, 0.2f), new Vector3(0, 0, 70));
                VisualKit.Part(node, VisualKit.Box(0.14f, 0.07f, 0.07f), VisualKit.Mat(new Color(0.4f, 0.4f, 0.42f), 0.3f, 0.9f), new Vector3(0.78f, 0.24f, 0.2f));
                return true;
            }
            case "bau_casa":
            {
                var node = new Node3D { Position = P(obj.X, obj.Y) };
                Root.AddChild(node);
                VisualKit.Part(node, VisualKit.Box(1.3f, 0.7f, 0.8f), MapView.TexMat("gen_madeira.png", Colors.White, 0.6f), new Vector3(0, 0.35f, 0));
                VisualKit.Part(node, VisualKit.Box(1.35f, 0.08f, 0.85f), VisualKit.Mat(new Color(0.62f, 0.45f, 0.24f), 0.4f, 0.7f), new Vector3(0, 0.72f, 0));
                return true;
            }
        }
        return false;
    }

    /// <summary>Plantas dos canteiros deste personagem: broto (plantado), média (crescendo), cheia com flor (pronta).</summary>
    public void UpdatePlots(IReadOnlyList<CinzasDaMare.Shared.Net.PlotView> plots, float time)
    {
        foreach (var (id, plant) in _plotPlants)
        {
            var p = plots.FirstOrDefault(x => x.PlotId == id);
            plant.Visible = p is not null;
            if (p is null) continue;
            var s = p.State switch { "planted" => 0.35f, "growing" => 0.7f, _ => 1.05f };
            plant.Scale = new Vector3(1f, s + 0.03f * Mathf.Sin(time * 2f), 1f);
            plant.GetNode<Node3D>("Flor").Visible = p.State == "ready";
        }
    }

    private void AddObject(MapObject obj)
    {
        // 13/11: o mar (só a fala ao apertar F na beira do cais) e a parede rachada (o desenho é o portão "rachadura")
        if (obj.Kind is "contemplar" or "parede_rachada" or "pedra_oca" or "cama") return;   // 14/11 tarde: pedra_oca = o portão "pedra_secreta"   // 14/11: cama = a cama do mapa (prop) já desenhada
        if (obj.Kind == "esfera")
        {
            // Esfera do Macaco: bola laranja brilhante com estrelinhas vermelhas, girando devagar sobre um toco de pedra
            var esf = new Node3D { Name = "Esfera_" + obj.Id, Position = P(obj.X, obj.Y) };
            Root.AddChild(esf);
            VisualKit.Part(esf, VisualKit.Cylinder(0.25f, 0.32f, 0.5f, 8), StoneMat(0.4f, false), new Vector3(0, 0.25f, 0));
            // 17/11 noite (dono): vermelha, com o número (1 a 5) dourado na frente
            var bola = new Node3D { Position = new Vector3(0, 0.8f, 0) };
            esf.AddChild(bola);
            var n = obj.Id.Length > 0 && char.IsDigit(obj.Id[^1]) ? obj.Id[^1] - '0' : 1;
            ItemIconRenderer.EsferaNumerada(bola, n, 0.22f);
            esf.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.25f, 0.15f), LightEnergy = 1.2f, OmniRange = 4f, Position = new Vector3(0, 0.9f, 0) });
            _interactables.Add((obj, esf));
            _esferas.Add((obj, esf));
            return;
        }
        var before = Root.GetChildCount();
        if (AddCraftObject(obj))
        {
            // o que dá para coletar/usar ganha contorno quando o jogador chega perto (ver UpdateOutlines)
            for (var i = before; i < Root.GetChildCount(); i++)
                if (Root.GetChild(i) is Node3D n && n is not MeshInstance3D) _interactables.Add((obj, n));
            return;
        }
        // 11/11 (dungeon estilo Skyrim): runa do quebra-cabeça (acende com a marca "runa:id") e placa de pressão
        if (obj.Kind is "runa" or "placa_pressao")
        {
            var pz = new Node3D { Position = P(obj.X, obj.Y), RotationDegrees = new Vector3(0, R(0, 360), 0) };
            Root.AddChild(pz);
            if (obj.Kind == "runa")
            {
                if (Visuals.GlbCache.Instance("interior/puzzle_runa_pedra") is { } pedra) pz.AddChild(pedra);
                else VisualKit.Part(pz, VisualKit.Box(0.8f, 1.8f, 0.7f), StoneMat(0.5f, false), new Vector3(0, 0.9f, 0));
                var brilho = Visuals.GlbCache.Instance("interior/puzzle_runa_brilho") ?? VisualKit.Part(pz, VisualKit.Box(0.4f, 0.9f, 0.04f), VisualKit.Glow(new Color("#7affd8"), 3f), new Vector3(0, 1f, 0.37f));
                if (brilho.GetParent() is null) { pz.AddChild(brilho); brilho.Position = new Vector3(0, 0.541f, 0.27f); }   // posição do _info.json
                var luz = new OmniLight3D { LightColor = new Color("#7affd8"), LightEnergy = 1.2f, OmniRange = 3.5f, Position = new Vector3(0, 1f, 0.5f), ShadowEnabled = false };
                pz.AddChild(luz);
                brilho.Visible = false; luz.Visible = false;
                _runes.Add((obj, brilho, luz));
                _interactables.Add((obj, pz));
            }
            // 16/11 (pendência u79_alavanca): placa em duas peças — a tampa afunda 6 cm quando alguém pisa e as runas acendem
            else if (Visuals.GlbCache.Instance("interior/puzzle_placa_base") is { } pbase && Visuals.GlbCache.Instance("interior/puzzle_placa_tampa") is { } tampa)
            {
                pz.AddChild(pbase);
                tampa.Position = new Vector3(0, 0.06f, 0);
                pz.AddChild(tampa);
                var mats = new List<StandardMaterial3D>();
                foreach (var mi in tampa.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                    for (var k = 0; k < (mi.Mesh?.GetSurfaceCount() ?? 0); k++)
                        if (mi.GetActiveMaterial(k) is StandardMaterial3D sm && sm.EmissionEnabled)
                        {
                            var copia = (StandardMaterial3D)sm.Duplicate();
                            copia.EmissionEnergyMultiplier = 0.3f;
                            mi.SetSurfaceOverrideMaterial(k, copia);
                            mats.Add(copia);
                        }
                _placas.Add((obj, tampa, mats));
            }
            else if (Visuals.GlbCache.Instance("interior/puzzle_placa") is { } placa) pz.AddChild(placa);
            else VisualKit.Part(pz, VisualKit.Box(1.2f, 0.06f, 1.2f), StoneMat(0.4f, false), new Vector3(0, 0.03f, 0));
            _objects.Add((obj, pz));
            return;
        }
        if (obj.Kind == "prisioneiro")
        {
            // 13/11 ("tem que mudar o sprite da mulher amarrada, tá muito feio"): agora é o NOSSO boneco (regra do dono),
            // sentado no chão encostado no poste, mãos amarradas para trás, corda no peito e um pano na boca
            var pr = new Node3D { Position = P(obj.X, obj.Y), RotationDegrees = new Vector3(0, R(0, 360), 0) };
            Root.AddChild(pr);
            var wood = TexMat("gen_madeira.png", new Color(0.55f, 0.40f, 0.28f), 0.9f);
            var rope = VisualKit.Mat(new Color(0.72f, 0.62f, 0.42f), 0.95f);
            VisualKit.Part(pr, VisualKit.Cylinder(0.09f, 0.11f, 2.1f, 8), wood, new Vector3(0, 1.05f, -0.32f));
            var nome = obj.Label.Split(',')[0].Trim() + (obj.Label.Contains("amarrada") ? ", a Presa" : ", o Preso");   // gênero do boneco pelo nome
            var preso = VisualFactory.NpcVisual("aldeao", obj.Id, nome);
            pr.AddChild(preso.Root);
            preso.Root.Position = new Vector3(0, 0, -0.05f);
            for (var i = 0; i < 2; i++)
                VisualKit.Part(pr, new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.235f, Rings = 16, RingSegments = 6 }, rope, new Vector3(0, 0.62f + i * 0.13f, -0.16f));
            VisualKit.Part(pr, VisualKit.Box(0.16f, 0.045f, 0.04f), VisualKit.Mat(new Color(0.88f, 0.86f, 0.8f), 0.9f), new Vector3(0, 1.13f, 0.08f));
            _presos.Add(preso);
            pr.AddChild(new Label3D
            {
                Text = obj.Label, FontSize = 36, OutlineSize = 8, PixelSize = 0.01f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color(1f, 0.85f, 0.5f), Position = new Vector3(0, 1.75f, 0),
            });
            _objects.Add((obj, pr));
            _interactables.Add((obj, pr));
            return;
        }
        if (obj.Kind == "porta")
        {
            // 12/11: porta de casa (a porta já está no modelo da casa): capacho redondo de palha na frente dela — mirar
            // no capacho (ou na porta) e apertar F bate na porta
            var pt = new Node3D { Position = P(obj.X, obj.Y) };
            Root.AddChild(pt);
            // 17/11 (dono: "dentro da casa tem que ter uma porta na parede pra sair, não aquela parada no chão — parece um
            // tapete"): o capacho só fica do lado de FORA; dentro, só a porta na parede (PortaDeSaida)
            var dentroDoComodo = Map.Instanced || Map.HalfSize <= 12;
            if (!dentroDoComodo)
            {
                VisualKit.Part(pt, VisualKit.Cylinder(0.42f, 0.44f, 0.03f, 20), TexMat("gen_madeira.png", new Color(0.85f, 0.72f, 0.45f), 2f), new Vector3(0, 0.015f, 0));
                VisualKit.Part(pt, VisualKit.Cylinder(0.3f, 0.3f, 0.035f, 20), VisualKit.Mat(new Color(0.55f, 0.32f, 0.2f), 0.95f), new Vector3(0, 0.02f, 0));
            }
            VisualKit.Part(pt, VisualKit.Box(0.8f, 1.9f, 0.6f), VisualKit.Mat(new Color(0, 0, 0, 0), 1f), new Vector3(0, 0.95f, 0)).Visible = false;
            // 13/11 22:26 ("a interação está no chão — mirando na porta não dá; joga para as portas; e dentro das casas não tem
            // porta, fica sem guia"): para onde fica a parede da porta — o prédio mais perto (rua) ou a parede do cômodo (dentro)
            var predio = Map.Props.Where(pp => pp.Kind.Contains("casa") || pp.Kind.Contains("sobrado") || pp.Kind.Contains("predio")
                    || pp.Kind.Contains("taverna") || pp.Kind.Contains("casarao") || pp.Kind.Contains("observatorio"))
                .OrderBy(pp => (pp.X - obj.X) * (pp.X - obj.X) + (pp.Y - obj.Y) * (pp.Y - obj.Y)).FirstOrDefault();
            // 17/11 (dono: "faltou uma porta na parede — coloca como padrão em todas as casas por dentro!"): cômodo pequeno
            // (casa, biblioteca, loja) é SEMPRE "dentro", mesmo com peça de casa por perto na decoração
            var dentroDeCasa = dentroDoComodo || predio is null || (predio.X - obj.X) * (predio.X - obj.X) + (predio.Y - obj.Y) * (predio.Y - obj.Y) > 100;
            var paraParede = dentroDeCasa ? new Vector2((float)obj.X, (float)obj.Y) : new Vector2((float)(predio!.X - obj.X), (float)(predio.Y - obj.Y));
            paraParede = paraParede.LengthSquared() > 0.01f ? paraParede.Normalized() : new Vector2(0, 1);
            _portas.Add((obj, P(obj.X + paraParede.X * 0.45, obj.Y + paraParede.Y * 0.45, 1.1f)));
            if (dentroDeCasa) PortaDeSaida(obj, paraParede);
            // 13/11 ("como não dá para ir ainda na casa, deixa vermelho; muda de cor quando for possível entrar"): a porta que
            // leva a uma passagem trancada por marca (sua casa) tem o nome e um anel no capacho — vermelho trancado, verde livre
            if (obj.GrantFlag is { } pg && pg.StartsWith("porta:portal:", StringComparison.Ordinal)
                && Map.Portals.FirstOrDefault(q => q.Id == pg[13..]) is { RequiresFlag: { } trava })
            {
                var nome = new Label3D { Text = obj.Label, FontSize = 38, OutlineSize = 8, PixelSize = 0.01f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, 2.2f, 0) };
                pt.AddChild(nome);
                var anel = (StandardMaterial3D)VisualKit.Unshaded(new Color(1f, 0.25f, 0.2f, 0.8f), true);
                VisualKit.Part(pt, VisualKit.Ring(0.5f, 0.06f, 32), anel, new Vector3(0, 0.05f, 0));
                _portasTrancadas.Add((trava, nome, anel, obj.Label));
                // 13/11 ("na porta da minha casa falta o portal vermelho"): o vão da porta fica fechado por um véu vermelho
                // enquanto não dá para entrar (some quando abrir). O vão fica entre o capacho e o centro da casa mais perto.
                var casa = Map.Props.Where(pp => pp.Kind.Contains("casa") || pp.Kind.Contains("sobrado") || pp.Kind.Contains("predio"))
                    .OrderBy(pp => (pp.X - obj.X) * (pp.X - obj.X) + (pp.Y - obj.Y) * (pp.Y - obj.Y)).FirstOrDefault();
                var dirC = casa is null ? new Vector2(0, -1) : new Vector2((float)(casa.X - obj.X), (float)(casa.Y - obj.Y));
                dirC = dirC.LengthSquared() > 0.01f ? dirC.Normalized() : new Vector2(0, -1);
                var veuPorta = PortaoVermelho(P(obj.X + dirC.X * 0.55, obj.Y + dirC.Y * 0.55), Mathf.Atan2(-dirC.X, -dirC.Y), 1.15f, 2.1f, obj.Label + " — fechada", arco: false);
                _veusPorta.Add((trava, (Node3D)veuPorta.GetParent()));
            }
            _objects.Add((obj, pt));
            _interactables.Add((obj, pt));
            return;
        }
        // 17/11 (dono: "faça a interação acontecer nas estantes de livros"): livro na frente de uma estante
        // (Tools/livros_nas_estantes.py) = um livro brilhando puxado para fora da prateleira, sem atril
        // (Biblioteca do Continente: as estantes são obstáculos "estante" de 0,9 m — o livro vai na face delas)
        var estantes = Map.Props.Where(e => e.Kind == "glb:dg/dg_estante_livros").Select(e => (e.X, e.Y, Face: 0.32))
            .Concat(Map.Obstacles.Where(o => o.Kind == "estante").Select(o => (o.X, o.Y, Face: Math.Min(o.W, o.H) / 2 + 0.05)));
        if (obj.Kind == "livro" && estantes.OrderBy(e => (e.X - obj.X) * (e.X - obj.X) + (e.Y - obj.Y) * (e.Y - obj.Y)).FirstOrDefault() is var est
            && est.Face > 0 && (est.X - obj.X) * (est.X - obj.X) + (est.Y - obj.Y) * (est.Y - obj.Y) < 1.7 * 1.7)
        {
            var frente = new Vector2((float)(obj.X - est.X), (float)(obj.Y - est.Y)).Normalized();
            var lb = new Node3D { Position = P(est.X + frente.X * est.Face, est.Y + frente.Y * est.Face) };   // o objeto (onde se aperta F) fica 1,45 m à frente
            lb.Rotation = new Vector3(0, Mathf.Atan2(frente.X, frente.Y), 0);
            Root.AddChild(lb);
            var hash = (uint)obj.Id.GetHashCode();
            var capa = Color.FromHsv((hash % 360) / 360f, 0.55f, 0.55f);
            var altura = 0.75f + (hash >> 8) % 3 * 0.42f;   // uma das prateleiras
            var livroMesh = VisualKit.Part(lb, VisualKit.Box(0.08f, 0.32f, 0.24f), VisualKit.Mat(capa, 0.7f), new Vector3(0, altura, 0.1f), new Vector3(0, 0, 6));
            VisualKit.Part(lb, VisualKit.Box(0.085f, 0.04f, 0.245f), VisualKit.Glow(new Color(1f, 0.82f, 0.4f), 2.2f), new Vector3(0, altura + 0.08f, 0.1f), new Vector3(0, 0, 6));
            var luzL = new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.45f), LightEnergy = 0.7f, OmniRange = 1.8f, Position = new Vector3(0, altura, 0.45f), ShadowEnabled = false };
            lb.AddChild(luzL);
            _interiorLights.Add((luzL, R(0, 10)));
            lb.AddChild(new Label3D
            {
                Text = obj.Label, FontSize = 30, OutlineSize = 8, PixelSize = 0.006f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color(1f, 0.88f, 0.6f), Position = new Vector3(0, 2.25f, 0.4f),
                VisibilityRangeEnd = 4.5f, VisibilityRangeEndMargin = 1f,
            });
            _objects.Add((obj, lb));
            _interactables.Add((obj, lb));
            return;
        }
        if (obj.Kind == "maquina_dg")
        {
            // 18/11 15h: Máquina de Chaves — bancada de ferro com engrenagens, fornalha acesa e uma chave grande pendurada
            var mq = new Node3D { Position = P(obj.X, obj.Y) };
            Root.AddChild(mq);
            var ferro = VisualKit.Mat(new Color(0.24f, 0.23f, 0.22f), 0.55f, 0.7f);
            var latao = VisualKit.Mat(new Color(0.72f, 0.55f, 0.25f), 0.35f, 0.85f);
            var madeira = VisualKit.Mat(new Color(0.36f, 0.24f, 0.14f), 0.85f);
            VisualKit.Part(mq, VisualKit.Box(1.8f, 0.9f, 1.0f), ferro, new Vector3(0, 0.45f, 0));
            VisualKit.Part(mq, VisualKit.Box(1.9f, 0.08f, 1.1f), madeira, new Vector3(0, 0.94f, 0));
            VisualKit.Part(mq, VisualKit.Box(0.5f, 0.35f, 0.06f), VisualKit.Glow(new Color(1f, 0.45f, 0.12f), 2.5f), new Vector3(-0.4f, 0.42f, 0.51f));
            foreach (var (gx, gy, gr) in new[] { (0.35f, 1.45f, 0.42f), (0.85f, 1.2f, 0.26f), (-0.2f, 1.25f, 0.22f) })
            {
                var eng = new Node3D { Position = new Vector3(gx, gy, 0.1f) };
                mq.AddChild(eng);
                VisualKit.Part(eng, VisualKit.Cylinder(gr, gr, 0.08f, 12), latao, Vector3.Zero, new Vector3(90, 0, 0));
                for (var d = 0; d < 8; d++)
                {
                    var a = d * Mathf.Tau / 8f;
                    VisualKit.Part(eng, VisualKit.Box(0.08f, 0.1f, 0.08f), latao, new Vector3(Mathf.Cos(a) * (gr + 0.04f), Mathf.Sin(a) * (gr + 0.04f), 0));
                }
                var tw = eng.CreateTween().SetLoops();
                tw.TweenProperty(eng, "rotation_degrees:z", gr > 0.3f ? 360f : -360f, 4f + gr * 6f).AsRelative();
            }
            // a chave grande pendurada no poste
            VisualKit.Part(mq, VisualKit.Cylinder(0.05f, 0.05f, 1.6f, 6), ferro, new Vector3(-0.85f, 1.7f, 0));
            var chave = new Node3D { Position = new Vector3(-0.85f, 2.1f, 0.12f) };
            mq.AddChild(chave);
            VisualKit.Part(chave, new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.16f, Rings = 16, RingSegments = 6 }, VisualKit.Glow(new Color(1f, 0.82f, 0.35f), 1.2f), Vector3.Zero, new Vector3(90, 0, 0));
            VisualKit.Part(chave, VisualKit.Box(0.05f, 0.45f, 0.05f), VisualKit.Glow(new Color(1f, 0.82f, 0.35f), 1.2f), new Vector3(0, -0.36f, 0));
            VisualKit.Part(chave, VisualKit.Box(0.14f, 0.05f, 0.05f), VisualKit.Glow(new Color(1f, 0.82f, 0.35f), 1.2f), new Vector3(0.06f, -0.52f, 0));
            mq.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.2f), LightEnergy = 1.2f, OmniRange = 3.5f, Position = new Vector3(-0.4f, 0.6f, 0.8f), ShadowEnabled = false });
            Collider(P(obj.X, obj.Y, 0.5f), new BoxShape3D { Size = new Vector3(1.8f, 1f, 1f) });
            mq.AddChild(new Label3D
            {
                Text = obj.Label, FontSize = 30, OutlineSize = 8, PixelSize = 0.007f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color(1f, 0.82f, 0.45f), Position = new Vector3(0, 2.7f, 0), VisibilityRangeEnd = 14f, VisibilityRangeEndMargin = 2f,
            });
            _interactables.Add((obj, mq));
            return;
        }
        if (obj.Kind == "mesclar_visual")
        {
            // 18/11: Máquina de Mesclar Visual (Astrália) — base de pedra com dois anéis de latão girando em volta de um
            // cristal violeta e dois pratos (um de cada lado: "este visual" → "naquela peça")
            var mq = new Node3D { Position = P(obj.X, obj.Y) };
            Root.AddChild(mq);
            var pedra = StoneMat(0.42f, true);
            var latao = VisualKit.Mat(new Color(0.78f, 0.6f, 0.28f), 0.35f, 0.85f);
            VisualKit.Part(mq, VisualKit.Cylinder(1.1f, 1.25f, 0.35f, 10), pedra, new Vector3(0, 0.17f, 0));
            VisualKit.Part(mq, VisualKit.Cylinder(0.55f, 0.75f, 0.9f, 8), pedra, new Vector3(0, 0.8f, 0));
            foreach (var lado in new[] { -1f, 1f })
            {
                VisualKit.Part(mq, VisualKit.Cylinder(0.06f, 0.06f, 1.0f, 6), latao, new Vector3(lado * 0.95f, 0.85f, 0));
                VisualKit.Part(mq, VisualKit.Cylinder(0.32f, 0.28f, 0.05f, 12), latao, new Vector3(lado * 0.95f, 1.37f, 0));
                VisualKit.Part(mq, VisualKit.Sphere(0.09f), VisualKit.Glow(lado < 0 ? new Color(0.55f, 0.85f, 1f) : new Color(1f, 0.75f, 0.35f), 3f), new Vector3(lado * 0.95f, 1.46f, 0));
            }
            VisualKit.Part(mq, new SphereMesh { Radius = 0.22f, Height = 0.7f, RadialSegments = 6, Rings = 2 }, VisualKit.Glow(new Color(0.7f, 0.4f, 1f), 3.5f), new Vector3(0, 1.75f, 0));
            var aneis = new Node3D { Position = new Vector3(0, 1.75f, 0) };
            mq.AddChild(aneis);
            foreach (var (rx, rz) in new[] { (60f, 0f), (-60f, 35f) })
            {
                var anel = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.48f, OuterRadius = 0.54f, Rings = 24, RingSegments = 6 }, MaterialOverride = latao, RotationDegrees = new Vector3(rx, 0, rz) };
                aneis.AddChild(anel);
            }
            var tw = mq.CreateTween().SetLoops();
            tw.TweenProperty(aneis, "rotation_degrees:y", 360f, 6f).AsRelative();
            var luz = new OmniLight3D { LightColor = new Color(0.75f, 0.5f, 1f), LightEnergy = 1.4f, OmniRange = 4.5f, Position = new Vector3(0, 1.8f, 0), ShadowEnabled = false };
            mq.AddChild(luz);
            Collider(P(obj.X, obj.Y, 0.6f), new CylinderShape3D { Radius = 1.0f, Height = 1.4f });
            mq.AddChild(new Label3D
            {
                Text = obj.Label, FontSize = 34, OutlineSize = 8, PixelSize = 0.007f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color(0.85f, 0.7f, 1f), Position = new Vector3(0, 2.6f, 0), VisibilityRangeEnd = 14f, VisibilityRangeEndMargin = 2f,
            });
            _interactables.Add((obj, mq));
            return;
        }
        if (obj.Kind is "livro" or "pesquisa")
        {
            // 12/11: atril de madeira com um livro aberto e uma vela (Biblioteca do Continente); o título flutua em cima
            var lv = new Node3D { Position = P(obj.X, obj.Y), RotationDegrees = new Vector3(0, R(-25, 25), 0) };
            Root.AddChild(lv);
            var madeira = VisualKit.Mat(new Color(0.30f, 0.19f, 0.10f), 0.7f);
            VisualKit.Part(lv, VisualKit.Box(0.5f, 0.06f, 0.4f), madeira, new Vector3(0, 0.03f, 0));
            VisualKit.Part(lv, VisualKit.Cylinder(0.05f, 0.06f, 1.0f, 6), madeira, new Vector3(0, 0.5f, 0));
            var tampo = VisualKit.Part(lv, VisualKit.Box(0.62f, 0.04f, 0.46f), madeira, new Vector3(0, 1.02f, 0.02f));
            tampo.RotationDegrees = new Vector3(-22, 0, 0);
            var papel = VisualKit.Mat(new Color(0.93f, 0.88f, 0.74f), 0.9f);
            foreach (var side in new[] { -1f, 1f })
            {
                var pg = VisualKit.Part(lv, VisualKit.Box(0.27f, 0.02f, 0.38f), papel, new Vector3(side * 0.14f, 1.06f, 0.03f));
                pg.RotationDegrees = new Vector3(-22, 0, side * -6f);
            }
            VisualKit.Part(lv, VisualKit.Cylinder(0.02f, 0.02f, 0.12f, 6), VisualKit.Mat(new Color(0.95f, 0.93f, 0.85f), 0.6f), new Vector3(0.26f, 1.12f, -0.12f));
            VisualKit.Part(lv, VisualKit.Sphere(0.025f), VisualKit.Glow(new Color(1f, 0.75f, 0.35f), 4f), new Vector3(0.26f, 1.2f, -0.12f));
            var vela = new OmniLight3D { LightColor = new Color(1f, 0.72f, 0.4f), LightEnergy = 0.9f, OmniRange = 2.6f, Position = new Vector3(0.2f, 1.3f, 0), ShadowEnabled = false };
            lv.AddChild(vela);
            _interiorLights.Add((vela, R(0, 10)));
            lv.AddChild(new Label3D
            {
                Text = obj.Label, FontSize = 30, OutlineSize = 8, PixelSize = 0.006f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color(1f, 0.88f, 0.6f), Position = new Vector3(0, 1.55f, 0),
                // 17/11 (dono: "arruma direito a biblioteca"): nome menor e só de perto — de longe os títulos se encavalavam
                VisibilityRangeEnd = 4.5f, VisibilityRangeEndMargin = 1f,
            });
            _objects.Add((obj, lv));
            _interactables.Add((obj, lv));
            return;
        }
        var root = new Node3D { Position = P(obj.X, obj.Y) };
        Root.AddChild(root);
        var glowColor = new Color(0.6f, 0.85f, 1f);
        // 13/11 ("o que são os marcos de divisa? deixa uma animação um pouco melhor"): achados de exploração ganham um
        // círculo de runas no chão girando devagar e uma fagulha que dá voltas; o marco de divisa é um marco de pedra de
        // verdade (pilar talhado com faixa de runas acesa), não um pedestal
        var runasAchado = new ShaderMaterial { Shader = new Shader { Code = Visuals.MannequinVisual.RunasShader } };
        runasAchado.SetShaderParameter("cor", new Vector3(glowColor.R, glowColor.G, glowColor.B));
        runasAchado.SetShaderParameter("forca", 0.45f);
        root.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(2.4f, 2.4f) }, MaterialOverride = runasAchado, Position = new Vector3(0, 0.03f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var orbita = new Node3D { Name = "Orbita" };
        root.AddChild(orbita);
        VisualKit.Part(orbita, VisualKit.Sphere(0.05f), VisualKit.Glow(glowColor, 4f), new Vector3(0.7f, 1.1f, 0));
        _orbitas.Add((orbita, R(0, 6)));
        if (obj.Label.Contains("Marco"))
        {
            VisualKit.Part(root, VisualKit.Box(0.42f, 1.3f, 0.32f), StoneMat(0.55f, false), new Vector3(0, 0.65f, 0), new Vector3(0, 0, R(-4, 4)));
            VisualKit.Part(root, VisualKit.Cylinder(0f, 0.3f, 0.25f, 4), StoneMat(0.55f, false), new Vector3(0, 1.42f, 0), new Vector3(0, 45, 0));
            VisualKit.Part(root, VisualKit.Box(0.44f, 0.08f, 0.34f), VisualKit.Glow(glowColor, 1.6f), new Vector3(0, 0.95f, 0));
            var luzM = new OmniLight3D { LightColor = glowColor, LightEnergy = 0.9f, OmniRange = 3.5f, Position = new Vector3(0, 1.2f, 0) };
            root.AddChild(luzM);
            var rotM = new Label3D
            {
                Text = obj.Label, FontSize = 36, OutlineSize = 8, PixelSize = 0.01f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = glowColor, Position = new Vector3(0, 2.0f, 0),
            };
            root.AddChild(rotM);
            _achados.Add((obj, runasAchado, rotM, luzM, orbita));
            _objects.Add((obj, root));
            _interactables.Add((obj, root));
            return;
        }
        VisualKit.Part(root, VisualKit.Cylinder(0.35f, 0.45f, 0.7f, 8), StoneMat(0.5f, false), new Vector3(0, 0.35f, 0));
        var item = obj.Kind switch
        {
            "elmo" => VisualKit.Part(root, VisualKit.Sphere(0.24f), VisualKit.Mat(new Color(0.62f, 0.6f, 0.55f), 0.35f, 0.85f), new Vector3(0, 0.92f, 0), null, new Vector3(1f, 0.85f, 1.1f)),
            _ => VisualKit.Part(root, VisualKit.Box(0.42f, 0.1f, 0.3f), VisualKit.Mat(new Color(0.25f, 0.18f, 0.32f), 0.8f), new Vector3(0, 0.78f, 0)),
        };
        VisualKit.Part(root, VisualKit.Sphere(0.08f), VisualKit.Glow(glowColor, 4f), new Vector3(0, 1.35f, 0));
        var luzA = new OmniLight3D { LightColor = glowColor, LightEnergy = 1.2f, OmniRange = 4f, Position = new Vector3(0, 1.2f, 0) };
        root.AddChild(luzA);
        var rotA = new Label3D
        {
            Text = obj.Label, FontSize = 36, OutlineSize = 8, PixelSize = 0.01f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = glowColor, Position = new Vector3(0, 1.8f, 0),
        };
        root.AddChild(rotA);
        _achados.Add((obj, runasAchado, rotA, luzA, orbita));
        _portalGlows.Add((item, R(0, 6)));
        _objects.Add((obj, root));
        _interactables.Add((obj, root));
    }

    private readonly List<(MapObject Obj, Node3D Node)> _interactables = new();
    /// <summary>17/11 (dono: "tá falando que eu já interagi — deixa mais destacado, pra saber que eu marquei"): achados de missão.</summary>
    private readonly List<(MapObject Obj, ShaderMaterial Runas, Label3D Rotulo, OmniLight3D Luz, Node3D Orbita)> _achados = new();
    /// <summary>13/11 22:26: o meio de cada porta (na parede, a 1,1 m do chão) — dá para mirar na porta, não só no capacho.</summary>
    private readonly List<(MapObject Obj, Vector3 Centro)> _portas = new();

    /// <summary>13/11 22:26 ("dentro das casas não tem porta, fica sem guia"): a porta de sair, de verdade, na parede do cômodo —
    /// batente, folha de madeira com maçaneta, uma lamparina em cima e a placa "Sair".</summary>
    private void PortaDeSaida(MapObject obj, Vector2 paraParede)
    {
        var g = new Node3D { Position = P(obj.X + paraParede.X * 0.75, obj.Y + paraParede.Y * 0.75), Rotation = new Vector3(0, Mathf.Atan2(-paraParede.X, -paraParede.Y), 0) };
        Root.AddChild(g);
        var madeira = TexMat("gen_madeira.png", new Color(0.62f, 0.42f, 0.26f), 0.9f);
        var batente = VisualKit.Mat(new Color(0.28f, 0.18f, 0.1f), 0.8f);
        foreach (var lado in new[] { -1f, 1f })
            VisualKit.Part(g, VisualKit.Box(0.14f, 2.25f, 0.2f), batente, new Vector3(lado * 0.62f, 1.12f, 0));
        VisualKit.Part(g, VisualKit.Box(1.38f, 0.16f, 0.22f), batente, new Vector3(0, 2.3f, 0));
        VisualKit.Part(g, VisualKit.Box(1.1f, 2.15f, 0.07f), madeira, new Vector3(0, 1.08f, 0.02f));
        for (var i = 0; i < 3; i++) VisualKit.Part(g, VisualKit.Box(1.1f, 0.06f, 0.09f), batente, new Vector3(0, 0.35f + i * 0.75f, 0.03f));
        VisualKit.Part(g, VisualKit.Sphere(0.045f), VisualKit.Mat(new Color(0.85f, 0.7f, 0.3f), 0.3f, 0.9f), new Vector3(0.4f, 1.0f, 0.1f));
        VisualKit.Part(g, VisualKit.Sphere(0.06f), VisualKit.Glow(new Color(1f, 0.75f, 0.4f), 3f), new Vector3(0, 2.55f, 0.15f));
        var luz = new OmniLight3D { LightColor = new Color(1f, 0.75f, 0.45f), LightEnergy = 0.9f, OmniRange = 3.2f, Position = new Vector3(0, 2.4f, 0.5f), ShadowEnabled = false };
        g.AddChild(luz);
        _interiorLights.Add((luz, R(0, 10)));
        g.AddChild(new Label3D { Text = "Sair ▸ " + (Map.Portals.FirstOrDefault(q => obj.GrantFlag == "porta:portal:" + q.Id)?.Label ?? "Rua"),
            FontSize = 30, OutlineSize = 8, PixelSize = 0.008f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, 2.85f, 0.1f),
            Modulate = new Color(1f, 0.88f, 0.6f) });
    }
    private readonly List<(MapObject Obj, Node3D Glow, OmniLight3D Light)> _runes = new();

    /// <summary>11/11: runas acesas por este personagem (marca "runa:id") brilham.</summary>
    private readonly List<(MapObject Obj, Node3D Node)> _esferas = new();

    public void SetRuneFlags(IReadOnlySet<string> flags)
    {
        // 14/11: esfera do macaco já pega some do chão (foi para a mochila)
        foreach (var (obj, node) in _esferas)
        {
            var pega = obj.GrantFlag is { } g && flags.Contains(g);
            if (node.Visible == pega) { node.Visible = !pega; if (pega) _interactables.RemoveAll(x => x.Obj == obj); }
        }
        // 17/11: achado já marcado — círculo e luz verdes, sem a fagulha, e o nome com ✓ "já examinado"
        foreach (var (obj, runas, rot, luz, orb) in _achados)
        {
            var feito = flags.Contains("marcado:" + obj.Id) || flags.Any(f => f.StartsWith("pego:", StringComparison.Ordinal) && f.EndsWith(":" + obj.Id, StringComparison.Ordinal));
            if (feito == orb.Visible)
            {
                var cor = feito ? new Color(0.35f, 1f, 0.45f) : new Color(0.6f, 0.85f, 1f);
                runas.SetShaderParameter("cor", new Vector3(cor.R, cor.G, cor.B));
                runas.SetShaderParameter("forca", feito ? 0.25f : 0.45f);
                luz.LightColor = cor; luz.LightEnergy = feito ? 0.5f : 1.1f;
                rot.Text = feito ? "✓ " + obj.Label + "  (já examinado)" : obj.Label;
                rot.Modulate = cor;
                orb.Visible = !feito;
            }
        }
        foreach (var (obj, glow, light) in _runes)
        {
            var lit = flags.Contains("runa:" + obj.Id);
            if (glow.Visible != lit) { glow.Visible = lit; light.Visible = lit; }
        }
    }
    private Node3D? _hoverBox;
    private Label3D? _hoverLabel;
    private string? _hoverId;

    /// <summary>
    /// 11/11 ("forja, caldeirão: um quadrado para interagir ao passar o mouse"): o objeto de uso (forja, bancada, caldeirão,
    /// baú, canteiro, coleta…) mais perto do ponto do chão sob o mouse (ou da mira) ganha um quadrado dourado em volta e o
    /// nome com a tecla. null = esconde.
    /// </summary>
    private Node3D? _hoverNode;
    private string? _hoverNpc;
    public string? HoveredNpc => _hoverNpc;
    public string? HoveredObjId { get; private set; }

    /// <summary>NPC sob o mouse: o corpo dele (dos pés à cabeça) projetado na tela, com uma folga que cresce de perto.</summary>
    public void HoverNpcOnScreen(Camera3D cam, Vector2 mouse)
    {
        _hoverNpc = null;
        var best = float.MaxValue;
        foreach (var (npc, visual, _) in _npcNodes)
        {
            if (!visual.Visible) continue;
            var feet = visual.GlobalPosition;
            if (cam.IsPositionBehind(feet)) continue;
            var a = cam.UnprojectPosition(feet + new Vector3(0, 0.15f, 0));
            var b = cam.UnprojectPosition(feet + new Vector3(0, 1.75f, 0));
            var halfW = Mathf.Max(14f, (a - b).Length() * 0.22f);
            // distância do mouse ao segmento pés–cabeça na tela
            var ab = b - a; var t = Mathf.Clamp((mouse - a).Dot(ab) / Mathf.Max(1f, ab.LengthSquared()), 0f, 1f);
            var d = (a + ab * t).DistanceTo(mouse);
            if (d < halfW && d < best) { best = d; _hoverNpc = npc.Id; }
        }
    }

    public void HoverAt(Vector3? ground, Camera3D? cam = null, Vector2? mira = null)
    {
        MapObject? best = null; Node3D? bestNode = null; var bestD = 2.2f;
        if (ground is { } g)
            foreach (var (obj, node) in _interactables)
            {
                // 17/11 noite (dono: "antes de eu achar a parede certa já apareceu o texto dando spoiler"): segredo e enigma
                // não ganham nome ao passar a mira — só o "F" quando você está colado
                if (obj.Kind is "runa" or "pedra_oca" or "pedra_oca_antiga" or "parede_rachada" or "esfera") continue;
                var d = new Vector2((float)obj.X - g.X, (float)obj.Y - g.Z).Length();
                if (d < bestD) { bestD = d; best = obj; bestNode = node; }
            }
        // 13/11 22:26: mirar NA PORTA (na parede) também vale — o meio da porta projetado na tela perto da mira
        if (cam is not null && mira is { } m2)
        {
            var bestTela = 70f;
            foreach (var (obj, centro) in _portas)
            {
                if (cam.IsPositionBehind(centro) || cam.GlobalPosition.DistanceTo(centro) > 16f) continue;
                var dt = cam.UnprojectPosition(centro).DistanceTo(m2);
                if (dt < bestTela) { bestTela = dt; best = obj; bestNode = _interactables.FirstOrDefault(i => i.Obj == obj).Node; }
            }
        }
        _hoverNode = bestNode;
        HoveredObjId = best?.Id;
        if (best is null) { if (_hoverBox is not null) _hoverBox.Visible = false; _hoverId = null; return; }
        if (_hoverBox is null)
        {
            _hoverBox = new Node3D { Name = "QuadradoUso" };
            // 11/11 noite: sem o quadrado em volta (pedido) — só o nome; o objeto ganha o contorno amarelo fino
            _hoverLabel = new Label3D { FontSize = 34, OutlineSize = 9, PixelSize = 0.0085f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color(1f, 0.9f, 0.6f), Position = new Vector3(0, 2.3f, 0), NoDepthTest = true };
            _hoverBox.AddChild(_hoverLabel);
            Root.AddChild(_hoverBox);
        }
        _hoverBox.Visible = true;
        if (_hoverId != best.Id)
        {
            _hoverId = best.Id;
            var size = bestNode is not null ? Mathf.Clamp(Footprint(bestNode), 1.0f, 3.2f) : 1.2f;
            _hoverBox.Scale = new Vector3(size * 0.6f, 1f, size * 0.6f);
            _hoverBox.Position = P(best.X, best.Y);
            if (_hoverLabel is not null)
            {
                _hoverLabel.Scale = new Vector3(1f / _hoverBox.Scale.X, 1f, 1f / _hoverBox.Scale.Z);
                _hoverLabel.Text = $"{best.Label}\n{HowTo(best.Kind)}";
            }
        }
    }

    private static float Footprint(Node3D node)
    {
        var first = true; var box = new Aabb();
        foreach (var mi in node.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (!mi.IsInsideTree()) continue;
            var b = mi.GlobalTransform * mi.GetAabb();
            box = first ? b : box.Merge(b); first = false;
        }
        return first ? 1.2f : Mathf.Max(box.Size.X, box.Size.Z) + 0.4f;
    }

    /// <summary>Uma linha do que o objeto faz (o "como funciona" do quadrado de uso).</summary>
    private static string HowTo(string kind) => kind switch
    {
        "porta" => "F: bater na porta",
        "pesquisa" => "F: pesquisar (item, monstro, lugar ou NPC)",
        "mesclar_visual" => "F: mesclar o visual de um item no outro",
        "maquina_dg" => "F: receita da chave da dungeon (F de novo faz)",
        "forja" => "chegue perto e aperte F: forjar e melhorar equipamento",
        "bancada" => "F: criar itens com materiais",
        "caldeirao" or "panela" => "F: preparar poções e comidas",
        "armazem" or "bau_casa" => "F: guardar e tirar itens (baú)",
        "reforma_casa" => "F: ver a próxima reforma da casa (F de novo para confirmar)",
        "canteiro" => "F: plantar sementes e colher",
        "prisioneiro" => "F: desamarrar",
        "cama" => "F: dormir (fique parado 8 s: vida cheia e o bônus Bem Dormido)",
        "barris_polvora" => "F: pegar um barril (missão da Nara) e levar até a parede rachada",
        "minerio" or "veio" => "F: minerar (precisa de picareta)",
        "arvore_corte" or "madeira" => "F: cortar lenha (precisa de machado)",
        _ => "chegue perto e aperte F",
    };
    /// <summary>Ponto de coleta esgotado para o jogador (vem do servidor).</summary>
    public Func<string, bool>? NodeDepleted { get; set; }
    private readonly HashSet<Node3D> _outlined = new();
    private static ShaderMaterial? _outlineMat;
    private readonly Dictionary<Node3D, ShaderMaterial?> _npcOutline = new();
    private static readonly ShaderMaterial?[] _npcOutlineMats = new ShaderMaterial?[3];

    /// <summary>Contorno de NPC: 0 = branco (sem missão), 1 = amarelo (missão), 2 = verde (entregar).</summary>
    private static ShaderMaterial NpcOutline(int kind)
    {
        if (_npcOutlineMats[kind] is { } ready) return ready;
        // 11/11: casca do pacote do trabalhador (contorno_npc.gdshader: mesma grossura na tela de perto e de longe, pulsa sozinha)
        if (ResourceLoader.Exists("res://Assets/Shaders/contorno_npc.gdshader"))
            return _npcOutlineMats[kind] = Visuals.NpcContorno.Material(kind switch { 2 => Visuals.NpcContorno.Estado.Entregar, 1 => Visuals.NpcContorno.Estado.Missao, _ => Visuals.NpcContorno.Estado.Nada });
        var col = kind switch { 2 => "vec3(0.35, 1.0, 0.45)", 1 => "vec3(1.0, 0.82, 0.25)", _ => "vec3(0.92, 0.92, 0.9)" };
        var mat = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, blend_add;
uniform float pulse = 1.0;
void vertex() { VERTEX += NORMAL * 0.025; }
void fragment() { ALBEDO = " + col + @" * (" + (kind == 0 ? "0.22 + 0.06" : "0.42 + 0.22") + @" * pulse); ALPHA = 0.7; }" } };
        _npcOutlineMats[kind] = mat;
        return mat;
    }

    private static bool EPlanta(MapObject o) => o.Kind is "veio" or "erva" or "planta"
        && o.Yields is { } y && (y.Contains("erva") || y.Contains("flor") || y.Contains("folha") || y.Contains("planta") || y.Contains("cogumelo") || y.Contains("raiz"));

    private static StandardMaterial3D? _anelMat;

    /// <summary>Anel dourado fino, meio transparente, deitado no chão em volta do objeto (liga/desliga).</summary>
    private static void AnelNoChao(Node3D node, bool on)
    {
        var anel = node.GetNodeOrNull<MeshInstance3D>("AnelDestaque");
        if (!on) { if (anel is not null) anel.Visible = false; return; }
        if (anel is null)
        {
            _anelMat ??= new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(1f, 0.85f, 0.4f, 0.55f), CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false,
            };
            var s = node.Scale.X <= 0.01f ? 1f : node.Scale.X;
            anel = new MeshInstance3D
            {
                Name = "AnelDestaque", Mesh = new TorusMesh { InnerRadius = 0.78f, OuterRadius = 0.84f, Rings = 48, RingSegments = 6 },
                MaterialOverride = _anelMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0, 0.04f, 0), Scale = new Vector3(1f / s, 0.15f / s, 1f / s),
            };
            node.AddChild(anel);
        }
        anel.Visible = true;
    }

    /// <summary>
    /// Contorno leve (sem trocar o desenho): casca dourada ao redor do objeto que dá para coletar/usar, só perto do jogador.
    /// </summary>
    private void UpdateOutlines(float time)
    {
        if (PlayerPos is not { } p) return;
        _outlineMat ??= new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, blend_add;
uniform float pulse = 1.0;
void vertex() { VERTEX += NORMAL * 0.035; }
void fragment() { ALBEDO = vec3(1.0, 0.82, 0.35) * (0.45 + 0.25 * pulse); ALPHA = 0.7; }" } };
        _outlineMat.SetShaderParameter("pulse", 0.5f + 0.5f * Mathf.Sin(time * 3f));
        foreach (var (obj, node) in _interactables)
        {
            // colhido: a moita encolhe (cortada) até voltar
            if (obj.Kind == "veio" && obj.Yields is { } yl && yl != "mat_tabua")
            {
                var target = NodeDepleted?.Invoke(obj.Id) == true ? 0.45f : 1f;
                var sc = Mathf.Lerp(node.Scale.Y, target, 0.1f);
                if (Mathf.Abs(sc - node.Scale.Y) > 0.002f) node.Scale = new Vector3(node.Scale.X / node.Scale.Y * sc, sc, node.Scale.Z / node.Scale.Y * sc);
            }
            // 11/11 noite: contorno só com o mouse em cima (antes: chegar perto já acendia)
            var near = node.Visible && ReferenceEquals(node, _hoverNode)
                       && (obj.Kind != "veio" || NodeDepleted?.Invoke(obj.Id) != true);   // já colhido: sem contorno
            if (near == _outlined.Contains(node)) continue;
            if (near) _outlined.Add(node); else _outlined.Remove(node);
            // 12/11 ("o contorno amarelo da moita de erva está feio, com vários quadrados bugados"): a moita é feita de folhas
            // planas; a casca inflada virava um quadrado por folha. Planta ganha só um anel fino e suave no chão.
            if (EPlanta(obj)) AnelNoChao(node, near);
            else SetOverlay(node, near ? NpcOutline(1) : null);
        }

        // 10/11: NPCs também ganham contorno perto de você — branco = só conversa, amarelo = tem missão, verde = entregar
        foreach (var (npc, visual, _) in _npcNodes)
        {
            var near = visual.Visible && npc.Id == _hoverNpc;   // 11/11 noite: só com o mouse mirando o NPC
            var kind = near ? (_markers is not null && _markers.TryGetValue(npc.Id, out var k) ? k : "") : null;
            var want = kind is null ? null : kind == "entregar" ? NpcOutline(2) : kind.Length > 0 ? NpcOutline(1) : NpcOutline(0);
            var had = _npcOutline.TryGetValue(visual, out var cur) ? cur : null;
            if (ReferenceEquals(had, want)) continue;
            _npcOutline[visual] = want;
            SetOverlay(visual, want);
        }
        foreach (var m in _npcOutlineMats) m?.SetShaderParameter("pulse", 0.5f + 0.5f * Mathf.Sin(time * 3f));

        static void SetOverlay(Node n, Material? m)
        {
            if (n.Name == "MarcadorMissao") return;   // o "!" e o halo de cima não ganham contorno (virava um quadrado)
            if (n is GeometryInstance3D g && n is not Label3D) g.MaterialOverlay = m;
            foreach (var c in n.GetChildren()) SetOverlay(c, m);
        }
    }

    private IReadOnlyDictionary<string, string>? _markers;
    private static readonly StandardMaterial3D MarkerNew = VisualKit.Glow(new Color(1f, 0.78f, 0.15f), 3.2f);
    private static readonly StandardMaterial3D MarkerDone = VisualKit.Glow(new Color(0.25f, 1f, 0.35f), 3.4f);
    private readonly List<(Node3D Marker, Node3D Gem, Label3D Sign, MeshInstance3D Halo, OmniLight3D Light, float Phase)> _questMarks = new();
    private static ImageTexture? _halo;

    /// <summary>Brilho redondo e macio (gerado uma vez) para o halo do marcador de missão.</summary>
    private static ImageTexture HaloTexture()
    {
        if (_halo is not null) return _halo;
        const int n = 64;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).Length() / (n / 2f);
                var a = Mathf.Clamp(1f - d, 0f, 1f);
                img.SetPixel(x, y, new Color(1, 1, 1, a * a * a));
            }
        return _halo = ImageTexture.CreateFromImage(img);
    }

    /// <summary>Marcadores vindos do servidor (por jogador).</summary>
    public void SetMarkers(IReadOnlyDictionary<string, string>? markers) => _markers = markers;

    /// <summary>"Mundo que muda" por personagem: NPCs que só chegam depois de uma missão e relíquias de missão ativa.</summary>
    public void UpdateVisibility(Func<MapNpc, bool> npcVisible, Func<MapObject, bool> objectVisible)
    {
        foreach (var (npc, visual, marker) in _npcNodes)
        {
            var v = npcVisible(npc);
            visual.Visible = v;
            // marcador: amarelo = algo novo, verde = missão pronta; sem nada novo, some (antes do 1º aviso do servidor: dourado)
            var kind = _markers is null ? "novo" : _markers.TryGetValue(npc.Id, out var k) ? k : "";
            marker.Visible = v && kind.Length > 0;
            if (kind.Length > 0)
            {
                var done = kind == "entregar";
                var main = kind == "principal";   // 13/11: caminho principal = estrela laranja-dourada
                var col = done ? new Color(0.35f, 1f, 0.45f) : main ? new Color(1f, 0.6f, 0.15f) : new Color(1f, 0.82f, 0.25f);
                if (marker.GetNodeOrNull<Node3D>("Gema") is { } gem)
                    foreach (var child in gem.GetChildren())
                        if (child is MeshInstance3D mi) mi.MaterialOverride = done ? MarkerDone : MarkerNew;
                if (marker.GetNodeOrNull<Label3D>("Sinal") is { } sign) { sign.Text = done ? "?" : "!"; sign.Modulate = col; }   // 14/11 tarde: "★" não existe na fonte do título (virava um quadrado preto em cima do NPC perto da pedra); o principal fica laranja
                if (marker.GetNodeOrNull<OmniLight3D>("Luz") is { } light) light.LightColor = col;
                if (marker.GetNodeOrNull<MeshInstance3D>("Halo")?.MaterialOverride is StandardMaterial3D hm) hm.AlbedoColor = col with { A = 0.8f };
            }
        }
        foreach (var (obj, node) in _objects) node.Visible = objectVisible(obj);
    }

    /// <summary>Anima portões, alavancas, tampas de baú (pelas marcas do personagem) e o fogo.</summary>
    private readonly HashSet<string> _decorFeito = new();
    private string? _decorSig;
    /// <summary>18/11: peças de decoração montadas (para o "F — Mover" e o modo de colocar).</summary>
    public readonly List<(string Id, Node3D No)> DecorNos = new();
    /// <summary>18/11: peça que está sendo posicionada agora (não desenha a fixa enquanto isso).</summary>
    public string? DecorEmPosicionamento { get => _decorPos; set { if (_decorPos != value) { _decorPos = value; _decorSig = null; } } }
    private string? _decorPos;

    public void Update(IReadOnlySet<string> flags, float time, float delta, IReadOnlySet<string>? decorDoDono = null)
    {
        // 12/11: decoração da casa (só na casa do jogador; peças novas aparecem assim que colocadas)
        // 13/11: visitando a casa de outro, a decoração é a dele
        // 18/11: cada peça no lugar que o dono escolheu (marca decorpos:), em qualquer cômodo da casa; sem lugar escolhido,
        // as antigas ficam no lugar fixo do cômodo grande. Mudou alguma marca de decoração → remonta.
        if (CinzasDaMare.Shared.World.AssentosCasa.TerrenoOuCasa(Map.Id))   // 18/11 15h: o terreno também (decoração de fora)
        {
            var fonte = decorDoDono ?? flags;
            var sig = string.Join(",", fonte.Where(f => f.StartsWith("decor", StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal));
            if (sig != _decorSig)
            {
                _decorSig = sig;
                foreach (var (_, no) in DecorNos) if (GodotObject.IsInstanceValid(no)) no.QueueFree();
                DecorNos.Clear();
                foreach (var f in fonte)
                {
                    if (!f.StartsWith("decor:", StringComparison.Ordinal)) continue;
                    var id = f[6..];
                    if (DecorEmPosicionamento == id) continue;
                    var onde = CasaDecor.Salvo(fonte, id, Map.Id);
                    if (onde is null && (Map.Id != "casa_jogador" || CinzasDaMare.Shared.World.AssentosCasa.DeFora(id))) continue;   // sem lugar escolhido: só no cômodo grande (lugar antigo)
                    var antes = Root.GetChildCount();
                    if (!CasaDecor.Build(Root, id, (x, y) => P(x, y), onde: onde)) continue;
                    if (Root.GetChild(Root.GetChildCount() - 1) is Node3D no && Root.GetChildCount() > antes) DecorNos.Add((id, no));
                }
            }
        }
        // vento: rajadas que passam pelo mapa (a fase anda com a posição), não todas as árvores juntas
        // 16/11 (otimização): ~1.500 plantas balançavam TODO quadro no mapa inteiro (cada uma avisa a árvore de nós toda).
        // Agora só até 80 m da câmera — mais longe a névoa e a distância já escondem um balanço de 1-2 graus — e a posição
        // de cada uma (fixa) é lida uma vez só
        foreach (var (node, rest, phase, amp) in _swaying)
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            if (!_posVento.TryGetValue(node, out var pv)) { if (!node.IsInsideTree()) continue; _posVento[node] = pv = node.GlobalPosition; }
            if (new Vector2(pv.X - _viewerPos.X, pv.Z - _viewerPos.Z).LengthSquared() > 80f * 80f) continue;
            if (!node.IsVisibleInTree()) continue;
            var gust = 0.6f + 0.4f * Mathf.Sin(time * 0.35f + pv.X * 0.02f);
            var sx = Mathf.Sin(time * 1.1f + phase) * amp * gust;
            var sz = Mathf.Sin(time * 0.8f + phase * 1.7f) * amp * 0.6f * gust;
            node.RotationDegrees = rest + new Vector3(sx, 0, sz);
        }
        foreach (var g in _gates)
        {
            if (g.Built is not null)
            {
                var built = flags.Contains(g.Gate.Flag);
                g.Built.Visible = built;
                g.Broken!.Visible = !built;
                continue;
            }
            var open = flags.Contains(g.Gate.Flag) ? 1f : 0f;
            g.Open = Mathf.MoveToward(g.Open, open, delta * 0.6f);
            if (g.Gate.Kind == "pedra")
            {
                var k = Mathf.SmoothStep(0f, 1f, g.Open);
                g.Bars.Position = g.Home.Lerp(g.Aside, k);
                g.Bars.Rotation = new Vector3(g.Aside.Z != g.Home.Z ? k * 3.2f : 0f, 0, g.Aside.X != g.Home.X ? -k * 3.2f : 0f);
                continue;
            }
            g.Bars.Position = new Vector3(g.Bars.Position.X, 3.3f * Mathf.SmoothStep(0f, 1f, g.Open), g.Bars.Position.Z);
            if (g.Veu is not null) g.Veu.Visible = Map.Theme == "biblioteca" ? g.Open < 0.4f : g.Open > 0.6f;
            g.Lever.RotationDegrees = new Vector3(Mathf.Lerp(40f, -40f, Mathf.Min(1f, g.Open * 3f)), 0, 0);
        }
        foreach (var (pobj, tampa, pmats) in _placas)
        {
            if (!GodotObject.IsInstanceValid(tampa)) continue;
            var pisada = new Vector2((float)pobj.X - _viewerPos.X, (float)pobj.Y - _viewerPos.Z).LengthSquared() < 0.75f * 0.75f;
            var alvo = pisada ? 0f : 0.06f;
            tampa.Position = new Vector3(0, Mathf.MoveToward(tampa.Position.Y, alvo, delta * 0.5f), 0);
            var e = Mathf.Lerp(3f, 0.3f, tampa.Position.Y / 0.06f);
            foreach (var m in pmats) m.EmissionEnergyMultiplier = e;
        }
        foreach (var c in _chests)
        {
            var open = flags.Contains("chest:" + c.Chest.Id) ? 1f : 0f;
            // 17/11 (dono: "baús, depois que abrem, somem pra sempre pra quem abriu"): já aberto ao chegar = nem aparece;
            // aberto agora = tampa abre, 1,5 s depois encolhe e some
            if (c.Root is { } cr && GodotObject.IsInstanceValid(cr))
            {
                if (!c.Visto) { c.Visto = true; if (open > 0) { cr.Visible = false; c.Sumindo = 99f; } }
                if (open > 0 && c.Sumindo < 0) c.Sumindo = 0f;
                if (open <= 0 && c.Sumindo >= 0) { c.Sumindo = -1f; cr.Visible = true; cr.Scale = Vector3.One; }
                if (c.Sumindo >= 0 && c.Sumindo < 99f)
                {
                    c.Sumindo += delta;
                    var k = Mathf.Clamp((c.Sumindo - 1.5f) / 0.5f, 0f, 1f);
                    cr.Scale = Vector3.One * Mathf.Max(0.01f, 1f - k);
                    if (k >= 1f) { cr.Visible = false; c.Sumindo = 99f; }
                }
            }
            c.Open = Mathf.MoveToward(c.Open, open, delta * 2f);
            c.Lid.RotationDegrees = new Vector3(100f * c.LidSign * c.Open, 0, 0);
            if (c.Glow is not null) c.Glow.LightEnergy = (1f - c.Open) * (0.7f + 0.3f * Mathf.Sin(time * 3f));
        }
        _flames.RemoveAll(fl => !GodotObject.IsInstanceValid(fl.Light));   // 13/11: luz apagada pelo editor (dava erro)
        foreach (var (pt, lb, veu) in _travadosMarca)
        {
            var trav = !Marcas.Tem(flags, pt.RequiresFlag) || NivelJogador < pt.MinLevel;
            veu.MaterialOverride = VortexMat(trav);
            lb.Modulate = trav ? new Color(1f, 0.35f, 0.3f) : new Color(0.85f, 0.92f, 1f);
            lb.Text = (trav ? "Trancado · " : "→ ") + pt.Label + (pt.MinLevel > 0 ? $"  (nível {pt.MinLevel}+)" : "");
        }
        foreach (var (trava, portao) in _veusPorta)
            if (GodotObject.IsInstanceValid(portao)) portao.Visible = !flags.Contains(trava);
        foreach (var (trava, nome, anel, texto) in _portasTrancadas)
        {
            var livre = flags.Contains(trava);
            var cor = livre ? new Color(0.45f, 1f, 0.5f) : new Color(1f, 0.32f, 0.26f);
            nome.Modulate = cor;
            nome.Text = livre ? texto : texto + " (trancada)";
            anel.AlbedoColor = cor with { A = 0.6f + 0.25f * Mathf.Sin(time * 3f) };
        }
        _flames.RemoveAll(f => !GodotObject.IsInstanceValid(f.Light));
        _interiorLights.RemoveAll(f => !GodotObject.IsInstanceValid(f.Light));
        foreach (var (light, phase, energy) in _flames)
            light.LightEnergy = energy * _flameBoost * (0.85f + 0.12f * Mathf.Sin(time * 9f + phase) + 0.06f * Mathf.Sin(time * 23f + phase * 2f));
        var indoor = Map.Theme is "casarao" or "biblioteca" ? 2.8f : 0.35f + 1.25f * Night;
        foreach (var (light, phase) in _interiorLights)
            light.LightEnergy = indoor * (0.92f + 0.06f * Mathf.Sin(time * 7f + phase) + 0.03f * Mathf.Sin(time * 19f + phase));
        foreach (var (node, phase) in _flameMeshes)
            node.Scale = new Vector3(1f, 0.85f + 0.2f * Mathf.Sin(time * 11f + phase), 1f);
        foreach (var life in _life) UpdateNpcLife(life, delta);
        if (_nivelPintado != NivelJogador)
        {
            _nivelPintado = NivelJogador;
            foreach (var (pt, lb, veu) in _travados)
            {
                var trav = NivelJogador < pt.MinLevel;
                veu.MaterialOverride = VortexMat(trav);   // 13/11: travada = vórtex vermelho
                lb.Modulate = trav ? new Color(1f, 0.35f, 0.3f) : new Color(0.85f, 0.92f, 1f);
                lb.Text = (trav ? "Trancado · " : "→ ") + pt.Label + $"  (nível {pt.MinLevel}+)";
            }
        }
        _presos.RemoveAll(v => !GodotObject.IsInstanceValid(v.Root)); _mortos.RemoveAll(v => !GodotObject.IsInstanceValid(v.Root));
        foreach (var preso in _presos) preso.Update(new VisualFrame { Action = "amarrado", Alive = true }, delta);
        foreach (var morto in _mortos) morto.Update(new VisualFrame { Action = "morto", Alive = true }, delta);
        UpdateOutlines(time);
        UpdateSkyLife(time);
        // marcador de missão: gema girando, sinal subindo e descendo, halo e luz pulsando
        foreach (var (marker, gem, sign, halo, light, ph) in _questMarks)
        {
            if (!marker.IsVisibleInTree()) continue;
            gem.Rotation = new Vector3(0, time * 2.2f + ph, 0);
            var bob = Mathf.Sin(time * 2.4f + ph);
            marker.Position = new Vector3(0, 2.55f + 0.07f * bob, 0);
            var pulse = 0.75f + 0.25f * Mathf.Sin(time * 3.6f + ph);
            halo.Scale = Vector3.One * (0.85f + 0.3f * pulse);
            light.LightEnergy = 1.1f + 0.7f * pulse;
            sign.Scale = Vector3.One * (1f + 0.06f * bob);
        }
        UpdateStones(time);
        UpdateSigns(1f / 60f);
        foreach (var (node, phase) in _portalGlows)
            node.Position = new Vector3(node.Position.X, node.Position.Y + 0.004f * Mathf.Sin(time * 2.5f + phase), node.Position.Z);
    }
}


