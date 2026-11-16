namespace CinzasDaMare.Shared.World;

/// <summary>
/// Colisão 2D do mapa (C# puro, testável): círculos (personagens) contra caixas alinhadas aos eixos e círculos.
/// O servidor é a autoridade; o cliente usa a mesma geometria só para desenhar e para a câmera.
/// </summary>
public sealed class MapGeometry
{
    private readonly struct Solid
    {
        public readonly bool IsBox;
        public readonly double X, Y, Hw, Hh, R;
        public readonly string? GateFlag;
        /// <summary>11/11 (voo com asas): bloqueia também quem voa (bordas do mapa, muralhas da cidade, portões fechados).</summary>
        public readonly bool BlocksFlight;
        public Solid(bool box, double x, double y, double hw, double hh, double r, string? gate, bool blocksFlight = false)
        { IsBox = box; X = x; Y = y; Hw = hw; Hh = hh; R = r; GateFlag = gate; BlocksFlight = blocksFlight || gate is not null; }
    }

    private readonly List<Solid> _solids = new();
    private readonly List<(double X, double Y)> _npcs = new();
    /// <summary>13/11 ("faz a colisão dos itens e NPCs padrão: nada atravessa"): peças e objetos pequenos que não eram sólidos
    /// (tocha, placa, fogueira, caldeirão, bancada, altar...) — sólidos no andar (Move), como os NPCs; não mexem em
    /// nascimento, portais e validação (Resolve/IsFree).</summary>
    private readonly List<(double X, double Y, double R)> _corpos = new();

    public static double CorpoDePeca(string kind) => kind switch
    {
        "tocha" or "tocha_poste" or "estandarte" or "placa" or "poste" or "estandarte_tridente" => 0.15,
        "fogueira" => 0.45, "acampamento_abandonado" => 0.8, "barco_encalhado" => 1.6, "redes" => 0.4,
        "movel_cadeira" or "movel_banqueta" => 0.22, "cadeira_dentista" => 0.5, "frascos_quimica" => 0.4, "cesto_peixe" => 0.35,
        "carnes_penduradas" or "painel_placar" => 0.3, "monte_areia" => 0.6,
        "dg_candelabro" or "dg_lanterna_mina" => 0.2, "dg_pilha_livros" or "dg_ferramentas_mina" => 0.35, "dg_lapides" or "dg_casulos_larva" or "dg_cristais_cal" => 0.5,
        "dg_urnas" => 0.4, "dg_entulho" or "dg_mesa_estudo" => 0.6, "dg_atril" => 0.3,
        // 13/11 ("biblioteca do cais está sem física"): móveis dos interiores
        "dg_astrolabio" or "dg_sino_rachado" or "dg_estatua_sabio" => 0.5, "dg_sarcofago" or "dg_altar_cripta" => 0.7, "dg_coluna_gotica" => 0.45,
        "movel_mesa" or "movel_lareira" => 0.55, "movel_bau" or "movel_caixas" => 0.4,
        _ => 0,
    };

    public static double CorpoDeObjeto(MapObject o) => o.Kind switch
    {
        "caldeirao" => 0.6, "panela" => 0.45, "forja" => 0.9, "bancada" => 0.7, "altar" => 0.8, "caos" => 0.9, "armazem" => 0.7,
        "barris_polvora" => 0.6, "bau_casa" => 0.45, "livro" => 0.3, "runa" => 0.35,
        "veio" when !PedraSolida(o) => 0.3,
        _ => 0,
    };
    private const double NpcBody = 0.42;
    public double HalfSize { get; }
    public double SafeX { get; }
    public double SafeY { get; }
    public double SafeRadius { get; }

    public MapGeometry(MapLayout map)
    {
        HalfSize = map.HalfSize;
        SafeX = map.SpawnX;
        SafeY = map.SpawnY;
        SafeRadius = map.SafeRadius;
        // E9: refúgios (acampamentos no meio do campo) também são seguros: monstros não entram, vida volta rápido.
        foreach (var z in map.Zones)
            if (z.Kind == "refugio") _refuges.Add((z.X - z.W / 2, z.Y - z.H / 2, z.X + z.W / 2, z.Y + z.H / 2));
        foreach (var o in map.Obstacles)
        {
            if (!o.Solid) continue;
            if (o.Shape != "circle" && Math.Abs(o.Rot % 180) > 0.5)
            {
                // caixa girada: preenchida por bolinhas (raio = meia espessura) ao longo do comprimento, no giro dela
                var a = o.Rot * Math.PI / 180.0;
                var hw = o.W / 2; var hh = o.H / 2;
                var r = Math.Max(0.06, Math.Min(hw, hh));
                for (var lx = -hw + r; lx <= hw - r + 1e-6 || lx == -hw + r; lx += Math.Max(0.1, r))
                    for (var ly = -hh + r; ly <= hh - r + 1e-6 || ly == -hh + r; ly += Math.Max(0.1, r))
                    {
                        _solids.Add(new Solid(false, o.X + lx * Math.Cos(a) + ly * Math.Sin(a), o.Y - lx * Math.Sin(a) + ly * Math.Cos(a), 0, 0, r, null, FlightBlocker(o.Kind)));
                        if (hh - r < 1e-6) break;
                    }
                continue;
            }
            _solids.Add(o.Shape == "circle"
                ? new Solid(false, o.X, o.Y, 0, 0, o.R, null, FlightBlocker(o.Kind))
                : o.Kind == "cerca"   // 13/11 ("a colisão das cercas é maior que o 3D"): só a espessura das travessas (12 cm)
                    ? new Solid(true, o.X, o.Y, o.W >= o.H ? o.W / 2 : Math.Min(o.W / 2, 0.06), o.W >= o.H ? Math.Min(o.H / 2, 0.06) : o.H / 2, 0, null)
                    : new Solid(true, o.X, o.Y, o.W / 2, o.H / 2, 0, null, FlightBlocker(o.Kind)));
        }
        foreach (var g in map.Gates)
            _solids.Add(new Solid(true, g.X, g.Y, g.W / 2, g.H / 2, 0, g.Flag));
        // objetos de cenário grandes (árvores, barracas, caixotes, colunas): também bloqueiam (antes eram só visuais)
        foreach (var p in map.Props)
        {
            var pr = p.Solid > 0 ? p.Solid : PropRadius(p.Kind) * p.Scale;
            if (pr > 0) _solids.Add(new Solid(false, p.X, p.Y, 0, 0, pr, null));
            // 12/11 ("a segunda casa não tem física"): o casarão em ruínas da Costa era só um círculo no meio (atravessava as
            // paredes e travava no salão). Agora as quatro paredes, com a porta da frente aberta, viram fileiras de bolinhas
            // que giram com o yaw (o mesmo contorno do casarão da cidade, que é o mesmo modelo).
            if (p.Kind == "ruina_casarao")
                foreach (var (ax, ay, bx, by) in RuinaCasaraoParedes)
                {
                    var a = p.Yaw * Math.PI / 180.0;
                    var len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                    var n = Math.Max(1, (int)Math.Ceiling(len / 0.5));
                    for (var i = 0; i <= n; i++)
                    {
                        var lx = ax + (bx - ax) * i / n; var ly = ay + (by - ay) * i / n;
                        _solids.Add(new Solid(false, p.X + lx * Math.Cos(a) + ly * Math.Sin(a), p.Y - lx * Math.Sin(a) + ly * Math.Cos(a), 0, 0, 0.32, null));
                    }
                }
            // 11/11 noite ("cerca na região do cal não tem física"): cerca vira uma fileira de bolinhas ao longo dela (gira com o yaw)
            // 12/11: paliçada goblin (trecho de 4,1 m) também vira fileira
            if (p.Kind == "palicada")
            {
                var a = p.Yaw * Math.PI / 180.0;
                for (var t = -1.8; t <= 1.81; t += 0.3)   // 13/11: bolinhas menores (colisão dentro do 3D)
                    _solids.Add(new Solid(false, p.X + Math.Cos(a) * t * p.Scale, p.Y - Math.Sin(a) * t * p.Scale, 0, 0, 0.2, null));
            }
            // 12/11 ("a casa no meio da Costa está sem física"): salão vazado com paredes de verdade (vãos onde a parede caiu)
            if (p.Kind == "ruina_salao") AddParedes(p, RuinaSalaoParedes);
            // 13/11 ("a biblioteca ainda está sem colisão"): o prédio leva as paredes junto (mover/girar no editor não as deixa para trás)
            if (p.Kind == "predio_biblioteca") AddParedes(p, BibliotecaParedes);
            if (p.Kind is "cerca" or "cerca_quebrada")
            {
                var a = p.Yaw * Math.PI / 180.0;
                for (var t = -1.2; t <= 1.21; t += 0.24)   // 13/11: travessas finas — bolinhas de 0,1 m bem juntas
                    _solids.Add(new Solid(false, p.X + Math.Cos(a) * t, p.Y - Math.Sin(a) * t, 0, 0, 0.1, null));
            }
        }
        // 11/11 noite ("meu boneco atravessa o NPC"): NPC é sólido (corpo de 0,4 m)
        // 11/11 noite: o pilar da pedra de viagem é sólido (fica 2,2 m a leste e 0,6 m ao norte do ponto)
        foreach (var w in map.Waypoints) _solids.Add(new Solid(false, w.X + 2.2, w.Y - 0.6, 0, 0, 0.45, null));
        // (só no andar — Move —, não em Resolve/IsFree: nascimento, portais e validação não mudam)
        foreach (var n in map.Npcs) _npcs.Add((n.X, n.Y));
        // 13/11 ("a caixa do bobo da corte dá para atravessar"): os caixotes onde o bobo se apresenta (MapView: caixote de 1,1 m
        // embaixo dele e um menor a 1 m ao lado) são sólidos
        foreach (var n in map.Npcs)
            if (n.Look == "bobo") { _corpos.Add((n.X, n.Y, 0.72)); _corpos.Add((n.X + 1.0, n.Y + 0.3, 0.45)); }
        foreach (var p in map.Props)
        {
            if (p.Solid > 0 || PropRadius(p.Kind) > 0) continue;
            var k = p.Kind.StartsWith("glb:", StringComparison.Ordinal) ? p.Kind[(p.Kind.LastIndexOf('/') + 1)..] : p.Kind;
            var r = CorpoDePeca(k) * (p.Scale > 0 ? p.Scale : 1);
            if (r > 0) _corpos.Add((p.X, p.Y, r));
            // 13/11: móvel comprido (estante, balcão, prateleira, cama) — fileira de corpos ao longo da largura (gira com o yaw)
            var (largura, raio) = k switch
            {
                "dg_estante_livros" => (2.4, 0.4), "movel_balcao" => (2.4, 0.45), "movel_prateleira" => (1.6, 0.3), "movel_cama" => (1.0, 0.6),
                "dg_parede_ossuario" => (3.0, 0.4), _ => (0.0, 0.0),
            };
            if (largura > 0)
            {
                var sc = p.Scale > 0 ? p.Scale : 1;
                var a = p.Yaw * Math.PI / 180.0;
                for (var t = -largura / 2; t <= largura / 2 + 1e-6; t += raio)
                    _corpos.Add((p.X + Math.Cos(a) * t * sc, p.Y - Math.Sin(a) * t * sc, raio * sc));
            }
        }
        foreach (var o in map.Objects)
            if (CorpoDeObjeto(o) is > 0 and var ro) _corpos.Add((o.X, o.Y, ro));
        // 13/11 22:26 (regra do dono: "nada atravessa, a não ser fantasma ou água — quadro de recados, bancada..."): cada peça
        // ganha o contorno do PRÓPRIO modelo (PecasMedidas, medido dos .glb), girado e escalado com ela
        foreach (var p in map.Props)
            if (p.Solid <= 0) AddPe(p.Kind, p.X, p.Y, p.Yaw, p.Scale > 0 ? p.Scale : 1);
        foreach (var o in map.Objects)
        {
            // oficinas: o cliente desenha no lugar do enfeite do mesmo tipo que estiver perto (o editor move o enfeite)
            if (o.Kind is not ("forja" or "bancada" or "caldeirao" or "panela" or "armazem")) continue;
            var pk = o.Kind == "armazem" ? "bau_armazem" : o.Kind;
            var pr = map.Props.FirstOrDefault(q => q.Kind == pk && (q.X - o.X) * (q.X - o.X) + (q.Y - o.Y) * (q.Y - o.Y) < 12);
            if (pr is null) AddPe(pk, o.X, o.Y, 0, 1);
        }
        foreach (var n in map.Npcs)
            if (n.Look == "quadro")   // quadro de recados: 1,9 m de largura, virado para onde o "facing" manda
                AddLinha(n.X, n.Y, n.Facing, -0.95, 0.95, 0.18);
        // 12/11 ("algumas pedras do Cal dá para atravessar"): a pedra de minério é sólida (minera-se de pé ao lado dela)
        foreach (var o in map.Objects)
            if (PedraSolida(o)) _solids.Add(new Solid(false, o.X, o.Y, 0, 0, 0.55, null));
        // 12/11 ("a bigorna não tem física, dá para atravessar"): a bigorna do ferreiro é sólida
        foreach (var n in map.Npcs)
            if (n.Look == "ferreiro" && BigornaDe(n) is var (bx, by) && Math.Sqrt((bx - SafeX) * (bx - SafeX) + (by - SafeY) * (by - SafeY)) > 2.5)
                _solids.Add(new Solid(false, bx, by, 0, 0, 0.42, null));   // (no ponto de chegada não: ali ninguém pode ficar preso)
    }

    /// <summary>Peças que NÃO bloqueiam: chão, água, fantasma, coisas penduradas, e prédios/árvores (esses já têm a colisão própria).</summary>
    private static readonly string[] NaoBloqueia =
    {
        "pintura_", "tapete", "sangue", "mancha", "poca", "agua", "lago", "capim", "grama", "flores", "folha", "trilha", "estrada",
        "cadaver", "ossada", "rachadura", "fantasma", "nevoa", "luz", "ponte", "cais", "escada", "rampa", "piso", "tablado", "trilhos",
        "entrada_galeria", "ruina_", "predio_", "casa", "sobrado", "casarao", "taverna", "edificio", "portao", "muralha", "torre",
        "arvore", "pinheir", "palmeira", "coqueiro", "mangue", "moita", "arbusto", "canteiro", "gargula_muro", "correntes", "estalactite",
        "observatorio", "moinho", "horta", "lustre", "quadro_mar", "cerca", "palafita", "farol", "elevador", "ninho",
    };

    private void AddPe(string kind, double x, double y, double yaw, double esc)
    {
        if (!PecasMedidas.Pe.TryGetValue(kind, out var pe)) return;
        var k = kind.StartsWith("glb:", StringComparison.Ordinal) ? kind[(kind.LastIndexOf('/') + 1)..] : kind;
        if (k.StartsWith("glb:", StringComparison.Ordinal)) k = k[4..];
        foreach (var n in NaoBloqueia) if (k.Contains(n, StringComparison.Ordinal)) return;
        double w = (pe.X1 - pe.X0) * esc, d = (pe.Z1 - pe.Z0) * esc;
        if (Math.Max(w, d) > 9 || Math.Max(w, d) < 0.12) return;
        double cx = (pe.X0 + pe.X1) / 2 * esc, cz = (pe.Z0 + pe.Z1) / 2 * esc;
        var a = yaw * Math.PI / 180.0;
        double Mx(double lx, double lz) => x + lx * Math.Cos(a) + lz * Math.Sin(a);
        double My(double lx, double lz) => y - lx * Math.Sin(a) + lz * Math.Cos(a);
        var r = Math.Clamp(Math.Min(w, d) / 2, 0.12, 1.3);
        if (w >= d)
            for (var t = -w / 2 + r; t <= w / 2 - r + 1e-6 || t == -w / 2 + r; t += Math.Max(0.15, r))
            { _corpos.Add((Mx(cx + t, cz), My(cx + t, cz), r)); if (w / 2 - r < 1e-6) break; }
        else
            for (var t = -d / 2 + r; t <= d / 2 - r + 1e-6 || t == -d / 2 + r; t += Math.Max(0.15, r))
            { _corpos.Add((Mx(cx, cz + t), My(cx, cz + t), r)); if (d / 2 - r < 1e-6) break; }
    }

    /// <summary>Fileira de corpos ao longo da largura (eixo X do modelo) girada pelo yaw.</summary>
    private void AddLinha(double x, double y, double yaw, double x0, double x1, double r)
    {
        var a = yaw * Math.PI / 180.0;
        for (var t = x0 + r; t <= x1 - r + 1e-6; t += r)
            _corpos.Add((x + t * Math.Cos(a), y - t * Math.Sin(a), r));
    }

    /// <summary>Objeto que é uma pedra no chão (veio de minério): bloqueia a passagem.</summary>
    public static bool PedraSolida(MapObject o) => o.Kind == "veio" && o.Yields is { } y && (y.Contains("ferro") || y.Contains("minerio") || y.Contains("pedra"));

    /// <summary>Onde fica a bigorna do ferreiro: 0,85 m à frente dele (o "facing" é o giro do modelo, frente = +Z girado).</summary>
    public static (double X, double Y) BigornaDe(MapNpc n)
    {
        var a = n.Facing * Math.PI / 180.0;
        return (n.X + Math.Sin(a) * 0.85, n.Y + Math.Cos(a) * 0.85);
    }

    /// <summary>Quem voa passa por cima de rocha, árvore, ruína, cerca, muro baixo e ravina; não passa da borda do mapa nem da muralha.</summary>
    public static bool FlightBlocker(string kind) => kind is "penhasco" or "muralha" or "barricada" or "portao" or "portao_cidade";

    /// <summary>Raio de colisão de cada objeto de cenário (0 = não bloqueia: tocha, poste, placa, estandarte...).</summary>
    public static double PropRadius(string kind) => kind switch
    {
        // 16/11: cenário novo da Transilvânia e da Floresta Oculta (morcegos, vaga-lumes, flores e círculo não bloqueiam)
        "caixao" => 0.55, "candelabro" => 0.25, "cripta" => 1.9, "vitral" => 0.9, "grade_gotica" => 0.4,
        "cogumelos_gigantes" => 0.9, "tronco_musgo" => 0.8, "pedra_musgo" => 1.1,
        "pinheiros" => 1.1, "tenda" or "barraca" => 1.8, "caixotes" => 0.7, "barril" => 0.4, "coluna_quebrada" => 0.7,
        "estacas" => 0.5, "lapide" => 0.45, "banca" => 1.1, "forja" => 1.3, "sino" => 0.9, "bau_armazem" => 0.7,
        // 11/11: enfeites novos da cidade (floreira, banco, caixote, carrinho da feira)
        "floreira" => 0.6, "banco_praca" => 0.5, "caixote" => 0.45, "barril_v2" => 0.45, "carrinho_feira" => 1.0,
        // 11/11: móveis do pacote da noite (cadeira, banqueta e tapete não bloqueiam)
        "movel_mesa" => 0.65, "movel_cama" => 0.8, "movel_lareira" => 0.85, "movel_balcao" => 1.1, "movel_prateleira" => 0.45,
        "ruina_casarao" => 0, "ruina_salao" => 0, "estatua_guardiao" => 1.0,
        "movel_bau" => 0.45, "movel_caixas" => 0.8, "puzzle_runa" => 0.45, "puzzle_alavanca" => 0.3,
        "mesa_vinteum" => 1.0, "mesa_cartas" => 0.85, "arvore_serpente" => 0.8,   // 12/11: mesa de Vinte-e-Um do 2º andar do casarão
        // 12/11 ("os itens na região do chefe goblin não têm física"; "pedras do Cal dá para atravessar"): acampamento goblin,
        // mina e enfeites que eram só desenho (medidas dos modelos)
        "tenda_goblin" => 1.8, "totem_goblin" => 0.55, "vagonete" => 0.7, "carroca" => 1.5,
        "blocos_cal_a" => 1.0, "blocos_cal_b" => 1.1, "blocos_cal_c" => 0.75, "feno" => 0.7, "espantalho" => 0.3,
        "bonecos_treino" => 0.35, "boneco_treino" => 0.35,
        _ => 0,
    };

    /// <summary>Paredes do salão vazado (coordenadas do modelo, medidas do .glb): porta na frente (+Z) e dois rombos (norte e oeste).</summary>
    /// <summary>Biblioteca do Cais (PecasDaVila: corpo de 8,6 × 6,2 m, frente para +Z do modelo).</summary>
    private static readonly (double Ax, double Ay, double Bx, double By)[] BibliotecaParedes =
    {
        (-4.3, -3.1, 4.3, -3.1), (-4.3, -3.1, -4.3, 3.1), (4.3, -3.1, 4.3, 3.1), (-4.3, 3.1, 4.3, 3.1),
    };

    private static readonly (double Ax, double Ay, double Bx, double By)[] RuinaSalaoParedes =
    {
        (-5.45, -4.0, -4.1, -4.0), (-2.5, -4.0, 5.35, -4.0),   // fundo, com o rombo
        (5.35, -4.0, 5.35, 4.0),                               // leste
        (-5.45, -4.0, -5.45, -1.9), (-5.45, 0.6, -5.45, 4.0),   // oeste, com o rombo
        (-5.45, 4.0, -1.35, 4.0), (1.05, 4.0, 5.35, 4.0),       // frente, com a porta
    };

    private void AddParedes(MapProp p, (double Ax, double Ay, double Bx, double By)[] paredes)
    {
        var a = p.Yaw * Math.PI / 180.0;
        foreach (var (ax, ay, bx, by) in paredes)
        {
            var len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            var n = Math.Max(1, (int)Math.Ceiling(len / 0.5));
            for (var i = 0; i <= n; i++)
            {
                var lx = (ax + (bx - ax) * i / n) * p.Scale; var ly = (ay + (by - ay) * i / n) * p.Scale;
                _solids.Add(new Solid(false, p.X + lx * Math.Cos(a) + ly * Math.Sin(a), p.Y - lx * Math.Sin(a) + ly * Math.Cos(a), 0, 0, 0.32, null));
            }
        }
    }

    /// <summary>Paredes do casarão (coordenadas do modelo): fundo, laterais e frente com a porta de 3,2 m no meio.</summary>
    private static readonly (double Ax, double Ay, double Bx, double By)[] RuinaCasaraoParedes =
    {
        (-7.9, -5.72, 7.9, -5.72),     // fundo
        (-7.72, -5.6, -7.72, 5.6),     // lado esquerdo
        (7.72, -5.6, 7.72, 5.6),       // lado direito
        (-7.9, 5.72, -1.75, 5.72),     // frente, até a porta
        (1.75, 5.72, 7.9, 5.72),       // frente, depois da porta
    };

    /// <summary>Arena quadrada sem obstáculos (modo antigo).</summary>
    public MapGeometry(double halfSize)
    {
        HalfSize = halfSize;
    }

    private readonly List<(double X0, double Y0, double X1, double Y1)> _refuges = new();

    public bool InSafeZone(double x, double y)
        => (SafeRadius > 0 && Dist(x, y, SafeX, SafeY) <= SafeRadius)
           || _refuges.Any(r => x >= r.X0 && x <= r.X1 && y >= r.Y0 && y <= r.Y1);

    /// <summary>
    /// Move um círculo de (x,y) por (dx,dy), deslizando em paredes. Portões cuja marca está em openFlags não bloqueiam.
    /// Passos curtos (até 0,25 m) evitam atravessar paredes finas.
    /// </summary>
    public (double X, double Y) Move(double x, double y, double dx, double dy, double radius,
        IReadOnlySet<string>? openFlags = null, bool blockSafeZone = false, bool flying = false)
    {
        var length = Math.Sqrt(dx * dx + dy * dy);
        var steps = Math.Max(1, (int)Math.Ceiling(length / 0.25));
        var sx = dx / steps;
        var sy = dy / steps;
        for (var i = 0; i < steps; i++)
        {
            var ox = x; var oy = y;   // onde estava de verdade antes deste passo
            x += sx;
            y += sy;
            (x, y) = Resolve(x, y, radius, openFlags, blockSafeZone, flying);
            if (flying) continue;
            foreach (var (nx, ny, rr) in _npcs.Select(n => (n.X, n.Y, NpcBody)).Concat(_corpos))
            {
                var ddx = x - nx; var ddy = y - ny;
                var d = Math.Sqrt(ddx * ddx + ddy * ddy);
                var min = rr + radius;
                if (d >= min || d < 1e-9) continue;
                // 13/11 ("dá para atravessar todos os NPCs"): quem já estava dentro (chegou por portal, o NPC foi movido) pode
                // SAIR, mas nunca chegar mais perto. Antes a conta usava a posição "de antes" estimada (x - passo), e um empurrão
                // de lado (bigorna, parede) fazia ela cair dentro e liberar atravessar o NPC inteiro.
                var pd = Math.Sqrt((ox - nx) * (ox - nx) + (oy - ny) * (oy - ny));
                var lim = pd < min ? pd : min;
                if (d >= lim) continue;
                x = nx + ddx / d * lim; y = ny + ddy / d * lim;
            }
        }
        return (x, y);
    }

    /// <summary>Empurra o círculo para fora de tudo que for sólido e o mantém dentro do mapa.</summary>
    public (double X, double Y) Resolve(double x, double y, double radius, IReadOnlySet<string>? openFlags = null,
        bool blockSafeZone = false, bool flying = false)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var s in _solids)
            {
                if (s.GateFlag is not null && openFlags is not null && openFlags.Contains(s.GateFlag)) continue;
                if (flying && !s.BlocksFlight) continue;
                if (s.IsBox)
                {
                    var cx = Math.Clamp(x, s.X - s.Hw, s.X + s.Hw);
                    var cy = Math.Clamp(y, s.Y - s.Hh, s.Y + s.Hh);
                    var ddx = x - cx;
                    var ddy = y - cy;
                    var d2 = ddx * ddx + ddy * ddy;
                    if (d2 >= radius * radius) continue;
                    if (d2 > 1e-12)
                    {
                        var d = Math.Sqrt(d2);
                        x = cx + ddx / d * radius;
                        y = cy + ddy / d * radius;
                    }
                    else
                    {
                        // centro dentro da caixa: sai pelo lado mais próximo
                        var left = x - (s.X - s.Hw);
                        var right = s.X + s.Hw - x;
                        var top = y - (s.Y - s.Hh);
                        var bottom = s.Y + s.Hh - y;
                        var min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
                        if (min == left) x = s.X - s.Hw - radius;
                        else if (min == right) x = s.X + s.Hw + radius;
                        else if (min == top) y = s.Y - s.Hh - radius;
                        else y = s.Y + s.Hh + radius;
                    }
                }
                else
                {
                    var ddx = x - s.X;
                    var ddy = y - s.Y;
                    var d = Math.Sqrt(ddx * ddx + ddy * ddy);
                    var min = s.R + radius;
                    if (d >= min) continue;
                    if (d < 1e-9) { ddx = 1; ddy = 0; d = 1; }
                    x = s.X + ddx / d * min;
                    y = s.Y + ddy / d * min;
                }
            }
            if (blockSafeZone && SafeRadius > 0)
            {
                var ddx = x - SafeX;
                var ddy = y - SafeY;
                var d = Math.Sqrt(ddx * ddx + ddy * ddy);
                var min = SafeRadius + radius;
                if (d < min)
                {
                    if (d < 1e-9) { ddx = 0; ddy = -1; d = 1; }
                    x = SafeX + ddx / d * min;
                    y = SafeY + ddy / d * min;
                }
            }
            if (blockSafeZone)
                foreach (var r in _refuges)
                {
                    // empurra para a borda mais próxima do refúgio (com o raio do monstro)
                    if (x < r.X0 - radius || x > r.X1 + radius || y < r.Y0 - radius || y > r.Y1 + radius) continue;
                    var left = x - (r.X0 - radius);
                    var right = (r.X1 + radius) - x;
                    var top = y - (r.Y0 - radius);
                    var bottom = (r.Y1 + radius) - y;
                    var m = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
                    if (m == left) x = r.X0 - radius;
                    else if (m == right) x = r.X1 + radius;
                    else if (m == top) y = r.Y0 - radius;
                    else y = r.Y1 + radius;
                }
            x = Math.Clamp(x, -HalfSize + radius, HalfSize - radius);
            y = Math.Clamp(y, -HalfSize + radius, HalfSize - radius);
        }
        return (x, y);
    }

    /// <summary>Verdadeiro se o ponto (com raio) está livre de sólidos.</summary>
    public bool IsFree(double x, double y, double radius, IReadOnlySet<string>? openFlags = null)
    {
        var (rx, ry) = Resolve(x, y, radius, openFlags);
        return Math.Abs(rx - x) < 1e-6 && Math.Abs(ry - y) < 1e-6;
    }

    /// <summary>
    /// Fração (0..1) do segmento até o primeiro sólido (1 = livre). Usada para projéteis e linha de visão.
    /// Portões contam como sólidos (projéteis não atravessam o portão fechado de ninguém).
    /// </summary>
    public double Raycast(double x0, double y0, double x1, double y1)
    {
        var best = 1.0;
        var dx = x1 - x0;
        var dy = y1 - y0;
        foreach (var s in _solids)
        {
            double t;
            if (s.IsBox) t = SegmentBox(x0, y0, dx, dy, s.X - s.Hw, s.Y - s.Hh, s.X + s.Hw, s.Y + s.Hh);
            else t = SegmentCircle(x0, y0, dx, dy, s.X, s.Y, s.R);
            if (t >= 0 && t < best) best = t;
        }
        return best;
    }

    public bool HasLineOfSight(double x0, double y0, double x1, double y1) => Raycast(x0, y0, x1, y1) >= 0.999;

    private static double SegmentBox(double x0, double y0, double dx, double dy, double minX, double minY, double maxX, double maxY)
    {
        var tmin = 0.0;
        var tmax = 1.0;
        if (!Slab(x0, dx, minX, maxX, ref tmin, ref tmax)) return -1;
        if (!Slab(y0, dy, minY, maxY, ref tmin, ref tmax)) return -1;
        return tmin;
    }

    private static bool Slab(double o, double d, double min, double max, ref double tmin, ref double tmax)
    {
        if (Math.Abs(d) < 1e-12) return o >= min && o <= max;
        var t1 = (min - o) / d;
        var t2 = (max - o) / d;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tmin = Math.Max(tmin, t1);
        tmax = Math.Min(tmax, t2);
        return tmin <= tmax;
    }

    private static double SegmentCircle(double x0, double y0, double dx, double dy, double cx, double cy, double r)
    {
        var fx = x0 - cx;
        var fy = y0 - cy;
        var a = dx * dx + dy * dy;
        if (a < 1e-12) return fx * fx + fy * fy <= r * r ? 0 : -1;
        var b = 2 * (fx * dx + fy * dy);
        var c = fx * fx + fy * fy - r * r;
        if (c <= 0) return 0;
        var disc = b * b - 4 * a * c;
        if (disc < 0) return -1;
        var t = (-b - Math.Sqrt(disc)) / (2 * a);
        return t is >= 0 and <= 1 ? t : -1;
    }

    /// <summary>Distância do ponto (px,py) ao segmento (ax,ay)-(bx,by).</summary>
    public static double DistToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var len2 = dx * dx + dy * dy;
        var t = len2 < 1e-12 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / len2, 0, 1);
        return Dist(px, py, ax + dx * t, ay + dy * t);
    }

    public static double Dist(double x0, double y0, double x1, double y1)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Verdadeiro se (px,py) está no cone de abertura arcDegrees centrado em (fx,fy) a partir de (ox,oy), até range.</summary>
    public static bool InCone(double ox, double oy, double fx, double fy, double px, double py, double range, double arcDegrees,
        double targetRadius = 0)
    {
        var dx = px - ox;
        var dy = py - oy;
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d > range + targetRadius) return false;
        if (d < 0.3 + targetRadius) return true; // colado: acerta
        var fl = Math.Sqrt(fx * fx + fy * fy);
        if (fl < 1e-9) return true;
        var cos = (fx * dx + fy * dy) / (fl * d);
        // o raio do alvo alarga um pouco o ângulo aceito
        var half = arcDegrees * 0.5 * Math.PI / 180.0 + Math.Atan2(targetRadius, Math.Max(d, 0.01));
        return cos >= Math.Cos(Math.Min(Math.PI, half));
    }
}
