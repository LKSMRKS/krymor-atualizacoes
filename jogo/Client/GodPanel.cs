using CinzasDaMare.Client.Sandbox.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Modo deus (F1; pedido 10/11): painel de teste — nível, XP, moedas, pontos, itens, monstros, hora do dia, viagem.
/// Só pede (GodIntent); quem faz é o servidor, e só se ele foi aberto com --deus.
/// </summary>
public sealed class GodPanel
{
    private static readonly Color Gold = new(0.88f, 0.82f, 0.68f);
    private static readonly Color Dim = new(0.70f, 0.68f, 0.62f);

    public Control Root { get; }
    public bool Visible => _panel.Visible;
    public event Action<NetMessage>? Send;
    /// <summary>Botão "EDITAR AGORA" (Main abre o editor no jogo).</summary>
    public event Action? EditRequested;
    /// <summary>13/11: testes do Laboratório que rodam só no cliente (foto e tela de carregamento, orbes).</summary>
    public event Action<string>? Lab;

    private readonly StonePanel _panel;
    private readonly VBoxContainer[] _pages = new VBoxContainer[7];
    private readonly Button[] _tabs = new Button[7];
    private readonly Label _status;
    private readonly Button _godBtn;
    private readonly SpinBox _level, _coins, _itemQty, _itemCopies, _monLevel, _monCount, _hour;
    private readonly LineEdit _itemSearch, _monSearch;
    private readonly OptionButton _itemKind, _quality;
    private readonly ItemList _items, _mons, _maps;
    private readonly Button[] _speedBtns = new Button[4];
    private readonly Button[] _timeBtns = new Button[4];
    private readonly Label _clock;
    private GodCatalogMessage? _shown;
    private readonly OptionButton _target;
    private string _targetSig = "";
    private int _curLevel = 1;
    private List<GodEntry> _itemView = new(), _monView = new();

    private static readonly (string Label, string Kind)[] ItemKinds =
    {
        ("Todos", ""), ("Armas", "weapon"), ("Armaduras", "armor"), ("Elmos", "helmet"), ("Escudos/mão esq.", "offhand"),
        ("Consumíveis", "consumable"), ("Materiais", "material"),
    };
    private static readonly string[] Qualities = { "comum", "incomum", "raro", "epico", "lendario" };
    private static readonly double[] Speeds = { 100, 150, 200, 300 };
    private static readonly double[] TimeSpeeds = { 0, 1, 10, 60 };

    private static string Sem(string t)
    {
        var d = t.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string(d.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static IEnumerable<Node> Todos(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            yield return c;
            foreach (var d in Todos(c)) yield return d;
        }
    }

    public GodPanel()
    {
        Root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel = new StonePanel(16f) { Visible = false, CustomMinimumSize = new Vector2(560, 620) };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterRight);
        _panel.Position = new Vector2(-520, -310);
        Root.AddChild(_panel);
        Arrastador.Ligar(_panel, "deus", 46f);   // 12/11: arrastável e sempre dentro da tela (encolhe se não couber)

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(col);
        col.AddChild(HudView.MakeLabel("Modo deus  (F1 fecha)", 20, Gold));
        _status = Wrap("", 12, Dim);
        col.AddChild(_status);
        // 11/11: editor no jogo (estilo Mario Maker)
        var edit = Btn("✎  EDITAR AGORA  (pôr/tirar árvores, cenário e monstros)", () => EditRequested?.Invoke());
        edit.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.45f));
        col.AddChild(edit);
        // 11/11 noite: para quem vão as ações (item, nível, XP, moedas, viagem...): você ou outro jogador online
        _target = new OptionButton { CustomMinimumSize = new Vector2(180, 0), FocusMode = Control.FocusModeEnum.None };
        _target.ItemSelected += i => Send?.Invoke(new GodIntent("target", Extra: i == 0 ? "" : _target.GetItemText((int)i)));
        col.AddChild(Row(HudView.MakeLabel("Para quem:", 13, Dim), _target,
            Btn("Ir até ele", () => God("goto")), Btn("Trazer aqui", () => God("bring"))));

        // 13/11 ("no F1 coloca uma caixa de pesquisa, pra eu achar o que quero sem ficar caçando"): procura em TODOS os
        // botões das abas (inclusive as subabas do Laboratório) e mostra os achados aqui; clicar faz a mesma coisa
        var busca = new LineEdit { PlaceholderText = "🔍 Procurar no F1 (ex.: asas, nível, esfera, boneco, biblioteca...)", ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddChild(busca);
        var achados = new FlowContainer { Visible = false };
        achados.AddThemeConstantOverride("h_separation", 4);
        achados.AddThemeConstantOverride("v_separation", 4);
        col.AddChild(achados);
        busca.TextChanged += t =>
        {
            foreach (var c in achados.GetChildren()) c.QueueFree();
            var q = Sem(t.Trim());
            achados.Visible = q.Length >= 2;
            if (!achados.Visible) return;
            var n = 0;
            foreach (var b in Todos(_panel).OfType<Button>())
            {
                if (b.GetParent() == achados || b.Text.Length == 0 || b.ToggleMode || !Sem(b.Text + " " + b.TooltipText).Contains(q)) continue;
                var alvo = b;
                var copia = new Button { Text = b.Text, TooltipText = b.TooltipText, FocusMode = Control.FocusModeEnum.None };
                copia.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.45f));
                copia.Pressed += () => alvo.EmitSignal(BaseButton.SignalName.Pressed);
                achados.AddChild(copia);
                if (++n >= 24) break;
            }
            if (n == 0) achados.AddChild(HudView.MakeLabel("Nada com esse nome.", 12, Dim));
        };

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 4);
        col.AddChild(tabs);
        var names = new[] { "Personagem", "Itens", "Monstros", "Mundo", "Combate (sandbox)", "LABORATÓRIO", "EVENTOS" };
        for (var i = 0; i < names.Length; i++)
        {
            var idx = i;
            _tabs[i] = Btn(names[i], () => ShowPage(idx));
            _tabs[i].ToggleMode = true;
            _tabs[i].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            tabs.AddChild(_tabs[i]);
            _pages[i] = new VBoxContainer { Visible = i == 0, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            _pages[i].AddThemeConstantOverride("separation", 6);
            col.AddChild(_pages[i]);
        }
        _tabs[0].ButtonPressed = true;

        // ---------------- personagem
        var p = _pages[0];
        p.AddChild(Title("Nível"));
        _level = Spin(1, 120, 1, 10);
        p.AddChild(Row(_level, Btn("Definir nível", () => God("level", amount: (long)_level.Value)),
            Btn("+1", () => God("level", amount: _curLevel + 1)), Btn("+10", () => God("level", amount: _curLevel + 10))));
        p.AddChild(Row(Btn("+1.000 XP", () => God("xp", amount: 1_000)), Btn("+100.000 XP", () => God("xp", amount: 100_000)),
            Btn("+10 pontos", () => God("points", amount: 10))));
        p.AddChild(Title("Moedas"));
        _coins = Spin(-1_000_000, 10_000_000, 100, 10_000);
        p.AddChild(Row(_coins, Btn("Dar", () => God("coins", amount: (long)_coins.Value)), Btn("+1.000", () => God("coins", amount: 1_000)),
            Btn("+100.000", () => God("coins", amount: 100_000))));
        p.AddChild(Title("Corpo"));
        _godBtn = Btn("Invulnerável: desligado", () => God("god"));
        p.AddChild(Row(Btn("Curar tudo", () => God("heal")), _godBtn, Btn("Zerar recargas", () => God("cooldowns"))));
        var speedRow = new HBoxContainer();
        speedRow.AddThemeConstantOverride("separation", 4);
        speedRow.AddChild(HudView.MakeLabel("Velocidade", 13, Dim));
        for (var i = 0; i < Speeds.Length; i++)
        {
            var v = Speeds[i];
            _speedBtns[i] = Btn($"{v / 100:0.#}×", () => God("speed", amount: (long)v));
            _speedBtns[i].ToggleMode = true;
            speedRow.AddChild(_speedBtns[i]);
        }
        p.AddChild(speedRow);
        p.AddChild(Title("Liberar"));
        p.AddChild(Row(Btn("Viagens, montarias, asas e companheiros", () => God("unlock")), Btn("Tirar asas", () => God("unwings"))));
        p.AddChild(Row(Btn("Evoluir a classe (mentor do nível 20)", () => God("evolve"))));

        // ---------------- itens
        p = _pages[1];
        _itemSearch = new LineEdit { PlaceholderText = "procurar item (nome ou id)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _itemSearch.TextChanged += _ => FillItems();
        _itemKind = new OptionButton();
        foreach (var (label, _) in ItemKinds) _itemKind.AddItem(label);
        _itemKind.ItemSelected += _ => FillItems();
        p.AddChild(Row(_itemSearch, _itemKind));
        _items = List(300);
        _items.ItemActivated += _ => GiveItem();
        p.AddChild(_items);
        _quality = new OptionButton();
        foreach (var q in Qualities) _quality.AddItem(q);
        _itemQty = Spin(1, 99, 1, 1);
        _itemCopies = Spin(1, 10, 1, 1);
        p.AddChild(Row(HudView.MakeLabel("Qualidade", 13, Dim), _quality, HudView.MakeLabel("Qtd", 13, Dim), _itemQty,
            HudView.MakeLabel("Cópias", 13, Dim), _itemCopies));
        p.AddChild(Row(Btn("Dar item (ou duplo clique)", GiveItem)));

        // ---------------- monstros
        p = _pages[2];
        _monSearch = new LineEdit { PlaceholderText = "procurar monstro", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _monSearch.TextChanged += _ => FillMonsters();
        p.AddChild(_monSearch);
        _mons = List(300);
        _mons.ItemActivated += _ => SpawnMonster();
        p.AddChild(_mons);
        _monLevel = Spin(0, 120, 1, 0);
        _monCount = Spin(1, 10, 1, 1);
        p.AddChild(Row(HudView.MakeLabel("Nível (0 = o dele)", 13, Dim), _monLevel, HudView.MakeLabel("Quantos", 13, Dim), _monCount));
        p.AddChild(Row(Btn("Chamar na minha frente", SpawnMonster)));
        p.AddChild(Row(Btn("Derrotar os de perto (com XP e saque)", () => God("kill")), Btn("Sumir com os que chamei", () => God("clear"))));

        // ---------------- mundo
        p = _pages[3];
        _clock = HudView.MakeLabel("", 15, Gold);
        p.AddChild(_clock);
        p.AddChild(Row(Btn("Amanhecer", () => God("time", amount: 600)), Btn("Meio-dia", () => God("time", amount: 1200)),
            Btn("Entardecer", () => God("time", amount: 1800)), Btn("Noite", () => God("time", amount: 2200))));
        _hour = Spin(0, 23.75, 0.25, 21);
        p.AddChild(Row(HudView.MakeLabel("Hora", 13, Dim), _hour, Btn("Pôr nesta hora", () => God("time", amount: (long)Math.Round(_hour.Value * 100)))));
        var tRow = new HBoxContainer();
        tRow.AddThemeConstantOverride("separation", 4);
        tRow.AddChild(HudView.MakeLabel("Passar do tempo", 13, Dim));
        for (var i = 0; i < TimeSpeeds.Length; i++)
        {
            var v = TimeSpeeds[i];
            _timeBtns[i] = Btn(v == 0 ? "Parado" : $"{v}×", () => God("time_speed", amount: (long)v));
            _timeBtns[i].ToggleMode = true;
            tRow.AddChild(_timeBtns[i]);
        }
        p.AddChild(tRow);
        p.AddChild(Wrap($"Um dia inteiro dura {40} minutos reais em 1×. Vale para todos no servidor.", 12, Dim));
        p.AddChild(Title("Viajar para"));
        _maps = List(220);
        _maps.ItemActivated += _ => Travel();
        p.AddChild(_maps);
        p.AddChild(Row(Btn("Ir (ou duplo clique)", Travel)));

        // ---------------- eventos (13/11: "o ADM inicia um evento das opções disponíveis quando quiser")
        p = _pages[6];
        p.AddChild(Title("Eventos do mundo"));
        p.AddChild(Wrap("Começa agora, para todo mundo online. Sozinhos, eles também acontecem no horário (Brasília): Invasão sábado 20h · Meteoros quarta 21h · Hora dourada domingo 19h · Chefe errante sexta 21h.", 12, Dim));
        foreach (var (id, nome, desc) in new[]
        {
            ("invasao", "⚔ Invasão às portas do Porto", "3 ondas de saqueadores no Portão do Porto (Costa), no nível de quem está online; chefe na última. Vencer dá 10 min de hora dourada."),
            ("meteoros", "☄ Chuva de meteoros", "10 fragmentos ardentes (elites) espalhados no Mar de Escória; cada um solta joias."),
            ("hora_dourada", "✦ Hora dourada", "XP em dobro no mundo todo por 30 minutos."),
            ("chefe_errante", "☠ Chefe errante", "Um chefe de região perto do nível do grupo sai do covil e vaga pelo mapa dele."),
            ("invasao_dourada", "✦ Invasão Dourada", "Monstros de ouro (+3 níveis, aguentam o dobro) cercam a entrada da cidade da faixa do grupo por 7 min; soltam Caixas de Pandora (Capitão: sempre)."),
        })
        {
            var ev = id;
            p.AddChild(Row(Btn(nome, () => God("evento", ev))));
            p.AddChild(Wrap(desc, 11, Dim));
        }
        p.AddChild(Row(Btn("Ver o que está acontecendo", () => God("evento_status"))));

        // ---------------- testes (12/11 20:18: "todas as funções de teste organizadas em abas, categorias — quanto mais opções melhor")
        p = _pages[5];
        p.AddChild(Row(Btn("IR AO LABORATÓRIO DE TESTES", () => God("travel", "laboratorio")), Btn("Arena (monstros 1–60)", () => God("travel", "santuario_arena")),
            Btn("Invulnerável", () => God("god")), Btn("Curar", () => God("heal")), Btn("Zerar recargas", () => God("cooldowns"))));
        p.AddChild(Wrap("Use num personagem de TESTE: nível, itens e marcas mudam de verdade. Com alguém em \"Para quem\", vale para ele.", 12, Dim));
        var sub = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 430) };
        p.AddChild(sub);
        VBoxContainer Aba(string nome)
        {
            var sc = new ScrollContainer { Name = nome, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            v.AddThemeConstantOverride("separation", 5);
            sc.AddChild(v);
            sub.AddChild(sc);
            return v;
        }
        void Grade(VBoxContainer v, int porLinha, params (string Label, Action Acao)[] bs)
        {
            for (var i = 0; i < bs.Length; i += porLinha)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);
                for (var k = i; k < Math.Min(i + porLinha, bs.Length); k++)
                {
                    var acao = bs[k].Acao;
                    var bt = Btn(bs[k].Label, () => acao());
                    bt.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    row.AddChild(bt);
                }
                v.AddChild(row);
            }
        }
        (string, Action) Pre(string id, string label) => (label, () => God("preset", id));
        (string, Action) Vai(string mapa, string label) => (label, () => God("travel", mapa));

        // 1) preparos
        var a1 = Aba("Preparos");
        a1.AddChild(Wrap("Um clique: nível exato, conjunto + arma da classe vestidos, 20 tônicos, viagens/montarias/pets/asas, classe 2 (15+) e 3 (55+), e leva ao acampamento.", 12, Dim));
        a1.AddChild(Title("Regiões principais"));
        Grade(a1, 3, Pre("costa5", "Costa · 5"), Pre("costa9", "Costa · 9"), Pre("vale13", "Vale · 13"), Pre("mata20", "Mata · 20"),
            Pre("floresta20", "Floresta Oculta · 20"), Pre("canions28", "Cânions · 28"), Pre("reinos35", "Reinos (formas) · 35"), Pre("brejo36", "Brejo · 36"),
            Pre("vespera44", "Véspera · 44"), Pre("geada50", "Geada · 50"), Pre("escoria56", "Escória · 56"), Pre("acropole60", "Acrópole · 60"));
        a1.AddChild(Title("Ramificações novas (u47)"));
        Grade(a1, 3, Pre("monte12", "Monte do Prefeito · 12"), Pre("casa20", "Casa própria · 20"), Pre("automatos40", "Autômatos · 40"),
            Pre("triangulo46", "Triângulo · 46"), Pre("pico47", "Pico do Vento · 47"), Pre("fundo50", "Fundo do mar · 50"),
            Pre("serpente50", "Caminho da Serpente · 50"), Pre("ilha56", "Ilha da Serpente · 56"));

        // 2) viagem direta (sem mudar nível: bom para testar as TRAVAS de nível das passagens)
        var a2 = Aba("Viagem");
        a2.AddChild(Wrap("Vai direto, sem mudar nível nem equipamento. Para testar as travas, ande até a passagem com nível baixo.", 12, Dim));
        a2.AddChild(Title("Cidade e começo"));
        Grade(a2, 3, Vai("porto_das_cinzas", "Porto"), Vai("casa_jogador", "Minha casa"), Vai("casarao_andar2", "Casarão 2º andar"),
            Vai("costa_do_cal", "Costa"), Vai("galeria_funda", "Galeria Funda"), Vai("vale_das_cinzas", "Vale"), Vai("biblioteca_continente", "Biblioteca"),
            Vai("monte_prefeito", "Monte do Prefeito"), Vai("santuario_arena", "Arena"), Vai("santuario_ceu", "Santuário do Céu"));
        a2.AddChild(Title("Meio do jogo"));
        Grade(a2, 3, Vai("mata_dos_enforcados", "Mata"), Vai("cripta_do_sino", "Cripta do Sino"), Vai("canions_suplicantes", "Cânions"),
            Vai("galerias_piedade", "Galerias da Piedade"), Vai("brejo_da_viuva", "Brejo"), Vai("mosteiro_afundado", "Mosteiro"),
            Vai("vespera", "Véspera"), Vai("mercado_mortos", "Mercado dos Mortos"), Vai("castelo_vespera", "Castelo da Véspera"), Vai("ruinas_automatos", "Ruínas dos Autômatos"));
        a2.AddChild(Title("Fim do jogo"));
        Grade(a2, 3, Vai("coroa_da_geada", "Coroa da Geada"), Vai("salao_geada", "Salão da Geada"), Vai("fortaleza_geada", "Fortaleza da Geada"),
            Vai("pico_vento", "Pico do Vento"), Vai("caminho_serpente", "Caminho da Serpente"), Vai("mar_de_escoria", "Mar de Escória"),
            Vai("fornalha", "Fornalha"), Vai("cidadela_escoria", "Cidadela"), Vai("acropole_das_cinzas", "Acrópole"), Vai("labirinto_juramento", "Labirinto"));
        a2.AddChild(Title("Dungeons no modo DIFÍCIL (40+)"));
        (string, Action) Dif(string mapa, string label) => (label, () => Send?.Invoke(new GodIntent("travel", mapa, Extra: "dificil")));
        Grade(a2, 3, Dif("mosteiro_afundado", "Mosteiro (difícil)"), Dif("mercado_mortos", "Mercado dos Mortos (difícil)"), Dif("salao_geada", "Salão da Geada (difícil)"),
            Dif("fornalha", "Fornalha (difícil)"), Dif("labirinto_juramento", "Labirinto (difícil)"));
        a2.AddChild(Title("Mar e reinos"));
        Grade(a2, 3, Vai("triangulo_demonio", "Triângulo do Demônio"), Vai("fundo_triangulo", "Fundo do Triângulo"), Vai("ilha_serpente", "Ilha da Serpente"),
            Vai("floresta_oculta", "Floresta Oculta"), Vai("reino_lua", "Lua Vermelha"), Vai("reino_transilvania", "Transilvânia"),
            Vai("reino_inferno", "Inferno"), Vai("reino_celeste", "Cidade Celestial"));

        // 3) personagem
        var a3 = Aba("Personagem");
        a3.AddChild(Title("Nível exato"));
        Grade(a3, 6, Enumerable.Range(0, 13).Select(i => { var n = i == 0 ? 1 : i * 5; return ($"{n}", (Action)(() => God("level", amount: n))); }).ToArray());
        a3.AddChild(Title("Classe"));
        a3.AddChild(Title("Conjunto vestido por variação (o boneco fica mais dourado)"));
        Grade(a3, 6, new[] { 1, 3, 6, 9, 12, 15 }.Select(v => ($"V{v}", (Action)(() => God("setvar", amount: v)))).ToArray());
        a3.AddChild(Title("Conjuntos novos (13/11) — as 5 peças em V12; vista pela mochila"));
        Grade(a3, 3, new (string Id, string Nome)[]
            {
                ("runico", "Rúnico · Mago 18"), ("lendario", "Lendário · Mago 25"), ("selvagem", "Invocadora · Bruxa 25"),
                ("grao_mestre", "Grão-Mestre · Mago 32"), ("quitina", "Quitina · Bruxa 32"), ("dragonico", "Dracônica · Guerreiro 40"),
                ("musa", "Musa de Vidro · Arqueiro 40"), ("mestre_almas", "Mestre das Almas · Mago 50"), ("rainha_sangue", "Rainha de Sangue · Bruxa 50"),
            }.Select(c => (c.Nome, (Action)(() => { foreach (var pc in new[] { "capacete", "peitoral", "luvas", "calca", "botas" }) God("item", $"arm_{c.Id}_{pc}", extra: "v12"); }))).ToArray());
        a3.AddChild(Title("Poderes"));
        Grade(a3, 2, ("Todos os tomos da classe (8 poderes da 1ª classe)", () => God("tomes")), ("Todos os tomos II (fortalecer, 15+)", () => God("tomes", "ii")));
        Grade(a3, 3, ("Evoluir (2ª classe)", () => God("evolve")), ("Terceira classe", () => God("class3")), ("+10 pontos de atributo", () => God("points", amount: 10)));
        // 14/11 tarde: põe a classe exatamente em 1, 2 ou 3 (sobe ou volta) — para testar
        Grade(a3, 3, ("Ficar na classe 1", () => God("classe_n", amount: 1)), ("Ficar na classe 2", () => God("classe_n", amount: 2)), ("Ficar na classe 3", () => God("classe_n", amount: 3)));
        a3.AddChild(Title("Recursos"));
        Grade(a3, 4, ("+100 mil XP", () => God("xp", amount: 100_000)), ("+5 milhões XP", () => God("xp", amount: 5_000_000)),
            ("+10 mil moedas", () => God("coins", amount: 10_000)), ("+1 milhão moedas", () => God("coins", amount: 1_000_000)));
        // 15/11 noite: cristais azuis da conta (Loja de Cristais, TAB)
        Grade(a3, 4, ("+100 cristais", () => God("cristais", amount: 100)), ("+1.000 cristais", () => God("cristais", amount: 1000)),
            ("+10.000 cristais", () => God("cristais", amount: 10_000)), ("Zerar cristais", () => God("cristais", amount: -1_000_000)));
        a3.AddChild(Title("Caixas das Cinzas (13/11) — abra na mochila"));
        Grade(a3, 5, Enumerable.Range(1, 5).Select(t => ($"5× Caixa +{t}", (Action)(() => God("item", $"caixa_cinzas_{t}", amount: 5)))).ToArray());
        a3.AddChild(Title("Novidades u64 — lendárias, reforja, casa, dungeon"));
        Grade(a3, 3, ("Lendária: Cajado Cobra Verde (V8)", () => God("item", "lendaria_cajado_cobra_verde", extra: "v08")),
            ("Lendária: Montante Dragão Rubro (V8)", () => God("item", "lendaria_montante_dragao_rubro", extra: "v08")),
            ("Lendária: Lança Valquíria (V8)", () => God("item", "lendaria_lanca_valquiria", extra: "v08")),
            ("5× Joia da Forja (reforjar V)", () => God("item", "joia_forja", amount: 5)),
            ("Entrar na casa pequena", () => God("travel", "casa_pequena")),
            ("Quadro de recados (Porto)", () => God("travel", "porto_das_cinzas")));
        a3.AddChild(Title("Skins (bracelete — espaço Skin no inventário)"));
        Grade(a3, 5, ("Coelho", () => God("item", "bracelete_coelho")), ("Executivo", () => God("item", "bracelete_gravata")),
            ("Cavalheiro", () => God("item", "bracelete_cartola")), ("Palhaço", () => God("item", "bracelete_palhaco")), ("Bobo da Corte", () => God("item", "bracelete_bobo")));
        a3.AddChild(Title("u67 — montarias, pet +, migrante"));
        Grade(a3, 3, ("Apito: Lince (nv 20)", () => God("item", "apito_lince")), ("Apito: Condor (nv 55)", () => God("item", "apito_condor")),
            ("Joias da Maré ×5 (refinar pet)", () => God("item", "joia_mare", amount: 5)), ("Soltar/mudar o migrante agora", () => God("migrante")),
            ("Ir ao Porto (NPCs novos)", () => God("travel", "porto_das_cinzas")), ("Ir à Prefeitura do Monte", () => God("travel", "monte_prefeito")));
        a3.AddChild(Title("u70 — taverna, bonecos, biblioteca gradeada, segredos"));
        Grade(a3, 3, ("Grande Martelo do Dorval", () => God("item", "weapon_guerreiro_martelo_dorval")), ("Bracelete do Bobo (raro)", () => God("item", "bracelete_bobo")),
            ("Mapa do tesouro", () => God("item", "mapa_tesouro")), ("Ir à Biblioteca do Continente", () => God("travel", "biblioteca_continente")),
            ("Abrir as 4 alas da biblioteca", () => { foreach (var a in new[] { "biblio:ala1", "biblio:ala2", "biblio:ala3", "biblio:ala4" }) God("flag", a); }),
            ("Ir ao 2º andar da taverna", () => God("travel", "taverna_andar2")), ("Ir à Biblioteca do Cais", () => God("travel", "biblioteca_cais")),
            ("5 esferas do Macaco", () => { for (var i = 1; i <= 5; i++) God("flag", "esfera:" + i); }), ("Nível 31 (testar queda ao morrer)", () => God("level", amount: 31)),
            ("Morrer agora", () => God("die")));
        a3.AddChild(Title("u68 — montaria como item, poses (Z)"));
        Grade(a3, 5, ("Cavalo", () => God("item", "montaria_corcel")), ("Lobo", () => God("item", "montaria_lobo")), ("Urso", () => God("item", "montaria_urso")),
            ("Grifo", () => God("item", "montaria_grifo")), ("Condor", () => God("item", "montaria_condor")));
        a3.AddChild(Title("Segurança (anti-cheat)"));
        Grade(a3, 1, ("Auditoria: ver alertas dos últimos 10 min (tudo fica em auditoria.log)", () => God("auditoria")));
        a3.AddChild(Title("u65 — efeitos negativos e inimigas novas"));
        Grade(a3, 3, ("5× Bandagem (sangramento)", () => God("item", "consumable_bandagem", amount: 5)),
            ("5× Água Benta (maldição)", () => God("item", "consumable_agua_benta", amount: 5)),
            ("3× Panaceia (cura tudo)", () => God("item", "consumable_panaceia", amount: 3)),
            ("Chamar Harpia", () => God("spawn", "monster_harpia")), ("Chamar Medusa", () => God("spawn", "monster_medusa")),
            ("Chamar Vampira", () => God("spawn", "monster_vampira")), ("Chamar Esfinge (elite)", () => God("spawn", "monster_esfinge")));
        a3.AddChild(Title("Variação desta cópia de dungeon (entre numa dungeon antes)"));
        Grade(a3, 5, ("Calma", () => God("variacao", "calma")), ("Elites errantes", () => God("variacao", "elites")),
            ("Ladrão do tesouro", () => God("variacao", "ladrao")), ("Furiosa", () => God("variacao", "furiosa")), ("Emboscada", () => God("variacao", "emboscada")));
        a3.AddChild(Title("Morte"));
        Grade(a3, 2, ("Morrer agora (testa a penalidade)", () => God("die")), ("Invulnerável liga/desliga", () => God("god")));

        // 4) missões e marcas
        var a4 = Aba("Missões");
        a4.AddChild(Title("Refazer"));
        var qid = new LineEdit { PlaceholderText = "id da missão (ex.: q_pref_1)", CustomMinimumSize = new Vector2(240, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        a4.AddChild(Row(qid, Btn("Refazer esta", () => { if (qid.Text.Trim().Length > 0) God("quest_reset", qid.Text.Trim()); }),
            Btn("Concluir esta (pula)", () => { if (qid.Text.Trim().Length > 0) God("quest_done", qid.Text.Trim()); })));
        a4.AddChild(Row(Btn("Refazer TODAS as missões deste mapa", () => God("quest_reset", "*mapa"))));
        a4.AddChild(Title("Marcas (destravam coisas do mundo)"));
        var flag = new LineEdit { PlaceholderText = "marca (ex.: casa:propria, livro:anjo, mount:grifo)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        a4.AddChild(Row(flag, Btn("Dar", () => { if (flag.Text.Trim().Length > 0) God("flag", flag.Text.Trim()); }),
            Btn("Tirar (começo)", () => { if (flag.Text.Trim().Length > 2) God("unflag", flag.Text.Trim()); })));
        a4.AddChild(Title("Atalhos"));
        Grade(a4, 3, ("Livros das formas (todos)", () => { foreach (var f in new[] { "livro:lobisomem", "livro:vampiro", "livro:ceifador", "livro:anjo", "livro:mascara" }) God("flag", f); }),
            ("Fallen: capítulo 5", () => { for (var i = 1; i <= 5; i++) God("flag", "fallen:" + i); }), ("Vacho: capítulo 4", () => { for (var i = 1; i <= 4; i++) God("flag", "vacho:" + i); }),
            ("Crônica: refazer os 8 capítulos", () => { for (var i = 1; i <= 8; i++) God("quest_reset", "q_cronica_" + i); God("unflag", "cronica:"); }),
            ("Crônica: pular para o capítulo 5", () => { for (var i = 1; i <= 4; i++) God("quest_done", "q_cronica_" + i); }),
            ("Ezequiel: pular para o Ozório (44)", () => { God("quest_done", "q_ezequiel_1"); God("quest_done", "q_ezequiel_2"); }),
            ("Ir à Sibila (reinos 35+)", () => God("travel", "porto_das_cinzas")),
            ("Prefeito: pular para a Escritura", () => { foreach (var id in new[] { "q_pref_1", "q_pref_2", "q_pref_3", "q_pref_4" }) God("quest_done", id); }),
            ("Montarias: liberar as missões", () => God("unlock")),
            ("Ir à Inês (Crônica, Porto)", () => God("travel", "porto_das_cinzas")),
            ("Esquecer Fallen", () => God("unflag", "fallen:")), ("Esquecer Vacho", () => God("unflag", "vacho:")), ("Esquecer livros", () => God("unflag", "livro:")));

        // 5) casa, companheiros, montarias, asas
        var a5 = Aba("Casa e companheiros");
        a5.AddChild(Title("Casa"));
        // 15/11 noite: tamanhos da casa (terreno e sobrado) para testar sem pagar a reforma
        Grade(a5, 3, ("Casa com terreno (nível 2)", () => God("flag", "casa:nivel2")), ("Sobrado (nível 3)", () => God("flag", "casa:nivel3")),
            ("Ir ao terreno", () => God("travel", "casa_jogador_media")));
        Grade(a5, 3, ("Dar a casa + decorações", () => God("house")), ("Ir para a minha casa", () => God("travel", "casa_jogador")),
            ("Esvaziar a decoração", () => God("unflag", "decor:")), ("Tirar a casa", () => God("unflag", "casa:propria")));
        a5.AddChild(Wrap("Decorações: use cada peça DENTRO da casa. Saindo, ganha o Conforto do Lar (mais pontos, mais forte). Convite: P → Convidar o grupo para a minha casa.", 12, Dim));
        a5.AddChild(Title("Montarias (melhor que tiver é a usada)"));
        Grade(a5, 4, ("Corcel (10)", () => God("flag", "mount:corcel")), ("Lobo (25)", () => God("flag", "mount:lobo")),
            ("Javali (40)", () => God("flag", "mount:javali")), ("Grifo (55)", () => God("flag", "mount:grifo")), ("Tirar todas", () => God("unflag", "mount:")));
        a5.AddChild(Title("Montarias 3D novas (escolhe e monta com a tecla de montar)"));
        Grade(a5, 4, CinzasDaMare.Client.Visuals.VisualFactory.Montarias3D.Select(kv => (kv.Value.Nome, (Action)(() => God("montaria", kv.Key)))).ToArray());
        a5.AddChild(Title("Companheiros (item no espaço de pet)"));
        Grade(a5, 4, ("Raposa (25)", () => God("item", "pet_raposa")), ("Coruja (40)", () => God("item", "pet_coruja")),
            ("Dragão Ancião (55)", () => God("item", "pet_dragao_anciao")), ("Tartaruga", () => God("item", "pet_tartaruga")));
        a5.AddChild(Title("Asas"));
        Grade(a5, 3, ("Asas da Serpente Celeste (50)", () => God("item", "wings_serpente")), ("Asas de Neve (45)", () => God("item", "wings_neve")), ("Asas do Dragão (35)", () => God("item", "wings_dragao")));
        Grade(a5, 3, ("Vestir asas de ferro", () => God("wings")), ("Tirar asas", () => God("unwings")), ("Liberar tudo (viagens, montarias, pets, asas)", () => God("unlock")));

        // 6) combate e monstros
        var a6 = Aba("Combate e mobs");
        a6.AddChild(Wrap("Chama na sua frente (nível da espécie). Natureza: o Guerreiro bate +15% em fera, o Mago em morto-vivo... (passe o mouse na Biblioteca).", 12, Dim));
        (string, Action) Mob(string id, string label, int qtd = 1) => (label, () => God("spawn", id, 0, qtd));
        a6.AddChild(Title("Chefes das ramificações"));
        Grade(a6, 3, Mob("monster_monte_urso", "Troll das Pedras (16)"), Mob("monster_auto_centuriao", "Centurião a Vapor (47)"), Mob("monster_tri_capitao", "Capitão Fantasma (51)"),
            Mob("monster_pico_gigante", "Gigante do Pico (50)"), Mob("monster_fundo_leviata", "Leviatã (55)"), Mob("monster_serp_serpente", "Serpente Celeste (56)"),
            Mob("monster_ilha_serpente", "Serpente Marinha (61)"));
        a6.AddChild(Title("Grupos de 5 (testar área, puxar, esquiva)"));
        Grade(a6, 3, Mob("monster_monte_cabra", "5 Cabras (11)", 5), Mob("monster_auto_aranha", "5 Aranhas de Engrenagem (41)", 5), Mob("monster_tri_homem_peixe", "5 Homens-Peixe (47)", 5),
            Mob("monster_fundo_tritao", "5 Tritões (50)", 5), Mob("monster_serp_anjo", "5 Anjos Caídos (51)", 5), Mob("monster_ilha_guerreiro", "5 Guerreiros da Ilha (55)", 5));
        a6.AddChild(Title("Monstros com modelo novo (13/11)"));
        Grade(a6, 4, Mob("monster_bst_cao_sarnento", "Cão Sarnento (3)"), Mob("monster_bst_meleca", "Meleca (5)"),
            Mob("monster_r2_olho_alado", "Olho Alado (11)"), Mob("monster_r1_tirano_da_mare", "Tirano da Maré (12)"));
        // 13/11 (pedido 17:33): tudo do conteúdo novo testável daqui
        a6.AddChild(Title("Era Uma Vez e arredores (u72)"));
        Grade(a6, 3, ("Ir ao reino Era Uma Vez", () => God("travel", "reino_fabula")),
            Mob("monster_fab_pai_pancudo", "Aquela Família: o pai"), Mob("monster_fab_mae_cabeleira", "a mãe"),
            Mob("monster_fab_filho_estilingue", "o filho (estilingue)"), Mob("monster_fab_filha_rabeca", "a filha (rabeca)"), Mob("monster_fab_bebe_birra", "o bebê (chocalho)"),
            Mob("monster_fab_gnomo_enfeiticado", "5 Gnomos enfeitiçados", 5), Mob("monster_fab_fungilda", "Dona Fungilda (chefe)"),
            Mob("monster_fab_soldadinho", "3 Soldadinhos", 3), Mob("monster_fab_malaquias", "Malaquias (elite)"), Mob("monster_fab_grao_bobo", "Grão-Bobo (chefe)"),
            Mob("monster_fab_macaco_coqueiro", "Macaco-Coqueiro"), Mob("monster_fab_dentuco", "Dentuço"), Mob("monster_fab_dentuco_pai", "Dentuço-Pai (elite)"),
            Mob("monster_fab_dama_urtiga", "Dama Urtiga (chefe)"), Mob("monster_fab_barao_vlakor", "Barão Vlakor (elite)"), Mob("monster_fab_baronesa_vlakor", "Baronesa Vlakor"),
            Mob("monster_fab_gemeo_vlakor_a", "Dimitri Vlakor"), Mob("monster_fab_gemeo_vlakor_b", "Doroteia Vlakor"), Mob("monster_fab_tritao_corrompido", "Tritão Corrompido (chefe)"),
            ("Ir à Mata (macacos)", () => God("travel", "mata_dos_enforcados")), ("Ir aos Cânions (dentuços)", () => God("travel", "canions_suplicantes")),
            ("Ir à Floresta Oculta (Urtiga)", () => God("travel", "floresta_oculta")), ("Ir à Transilvânia (Vlakor)", () => God("travel", "reino_transilvania")),
            ("Ir ao Fundo do Triângulo (tritão)", () => God("travel", "fundo_triangulo")),
            ("Refazer as missões do Tritão (escolha)", () => { foreach (var q in new[] { "q_fab_tritao_1", "q_fab_tritao_salvar", "q_fab_tritao_acabar" }) God("quest_reset", q); God("unflag", "fab:tritao_"); }),
            ("Refazer missões dos bobos", () => { foreach (var q in new[] { "q_fab_malaquias_1", "q_fab_malaquias_2", "q_fab_malaquias_3", "q_fab_grao_bobo" }) God("quest_reset", q); God("unflag", "fab:malaquias"); }),
            ("Refazer missões dos gnomos", () => { foreach (var q in new[] { "q_fab_gnomos_1", "q_fab_gnomos_2", "q_fab_gnomos_3", "q_fab_fungilda" }) God("quest_reset", q); God("unflag", "fab:gnomos"); }),
            ("5× Esporo Lunar", () => God("item", "mat_esporo_lunar", amount: 5)), ("6× Guizo do Castelo", () => God("item", "mat_guizo_castelo", amount: 6)));
        a6.AddChild(Title("Leva da noite 13/11 (u76)"));
        Grade(a6, 3, Mob("monster_r1_goblin_saqueador", "Goblin: 3 estocadas (3 marcas)"), Mob("monster_r1_gnomo_do_areal", "Gnomo do Areal"),
            ("Tirar foto para o carregamento", () => Lab?.Invoke("foto_carga")), ("Ver a tela de carregamento (3 s)", () => Lab?.Invoke("ver_carga")),
            ("Sacudir os orbes (respingo)", () => Lab?.Invoke("sacudir")), ("Tônico fraco ×5 (ícone no chão/tecla 1)", () => God("item", "consumable_c01_tonico_fraco", amount: 5)));
        a6.AddChild(Title("Rotina e começo (13/11)"));
        Grade(a6, 3, Mob("monster_costa_caranguejo_mae", "Caranguejo-Mãe (mini-chefe 5)"), ("Ir ao quadro de contratos (Porto)", () => God("travel", "porto_das_cinzas")),
            ("Eventos: aba EVENTOS", () => ShowPage(6)));
        a6.AddChild(Title("Conferir itens marcados (geodo, giro, baú)"));
        Grade(a6, 3, Mob("monster_r1_caranguejo_geodo", "Caranguejo-Geodo (drop e IA)"), ("Tomo do Giro de Lâmina", () => God("item", "tomo_guerreiro_giro")),
            ("Ir ao baú da casa", () => God("travel", "casa_jogador")));
        Grade(a6, 2, Mob("monster_mundo_kraken", "Kraken (chefe SEMANAL, 60)"), Mob("monster_ilha_harpia", "5 Harpias da Ilha (57)", 5));
        a6.AddChild(Title("Armas por tipo: o golpe é o mesmo; mudam alcance, velocidade e poder (veja na ficha do item)"));
        Grade(a6, 3, ("Espada", () => God("item", "arsenal_espada_curta_refinada")), ("Machado (forte, lento)", () => God("item", "arsenal_machado_mao_refinada")),
            ("Clava (pesada)", () => God("item", "arsenal_clava_refinada")), ("Maça (mais pesada)", () => God("item", "arsenal_maca_refinada")),
            ("Faca (rápida, fraca)", () => God("item", "arsenal_faca_refinada")), ("Lança (alcance longo)", () => God("item", "arsenal_lanca_refinada")));
        Grade(a6, 2, ("Matar tudo por perto", () => God("kill")), ("Sumir com os que eu chamei", () => God("clear")));
        a6.AddChild(Title("Movimento v2 contra mobs"));
        Grade(a6, 1, ("Abrir 'Combate (sandbox)': ligar o v2 e transformar os bonecos no monstro escolhido", () => ShowPage(4)));
        a6.AddChild(Wrap("x1: monte grupo no P e aperte 'Desafiar x1' ao lado do amigo.", 12, Dim));
    }

    public void Toggle()
    {
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible) Send?.Invoke(new GodIntent("open"));
    }

    public void Hide() => _panel.Visible = false;
    public void Page(int idx) => ShowPage(Math.Clamp(idx, 0, _pages.Length - 1));

    private ulong _playersAsk;

    public void Update(ClientModel m)
    {
        if (!_panel.Visible) return;
        // 12/11 ("só aparece eu, mesmo com outro player online"): a lista de jogadores se atualiza sozinha com o painel aberto
        var nowMs = Time.GetTicksMsec();
        if (nowMs >= _playersAsk) { _playersAsk = nowMs + 3000; Send?.Invoke(new GodIntent("players")); }
        if (_lab is not null && _labStatus is not null && _pages[4].Visible) _labStatus.Text = _lab.Status;
        _curLevel = m.Progress?.Level ?? _curLevel;
        _curMap = m.Map?.Id ?? _curMap;
        var g = m.God;
        if (g is not null && !ReferenceEquals(g, _shown))
        {
            var listsChanged = _shown is null || !ReferenceEquals(_shown.Items, g.Items);
            _shown = g;
            if (listsChanged)
            {
                FillItems();
                FillMonsters();
                _maps.Clear();
                foreach (var e in g.Maps) _maps.AddItem($"{e.Name}{e.Kind switch { "dungeon" => "  (dungeon)", "casa" => "  (sua casa — cópia só sua)", "cidade" => "  (cidade/interior)", _ => "" }}");   // 13/11: a casa não é dungeon
            }
            var sig = string.Join("|", g.Players) + "#" + g.Target;
            if (sig != _targetSig)
            {
                _targetSig = sig;
                _target.Clear();
                _target.AddItem("Eu mesmo");
                foreach (var pl in g.Players.Where(p => p != m.Name)) _target.AddItem(pl);
                _target.Selected = 0;
                for (var i = 1; i < _target.ItemCount; i++) if (_target.GetItemText(i) == g.Target) _target.Selected = i;
            }
            _godBtn.Text = g.Invulnerable ? "Invulnerável: LIGADO" : "Invulnerável: desligado";
            for (var i = 0; i < Speeds.Length; i++) _speedBtns[i].SetPressedNoSignal(Math.Abs(Speeds[i] / 100 - g.Speed) < 0.01);
            for (var i = 0; i < TimeSpeeds.Length; i++) _timeBtns[i].SetPressedNoSignal(Math.Abs(TimeSpeeds[i] - g.TimeSpeed) < 0.01);
        }
        _status.Text = g is null ? "Perguntando ao servidor…"
            : !g.Enabled ? "O servidor não está no modo deus. Feche o servidor (Jogar.cmd -Parar) e abra de novo com Jogar.cmd (sem -SemDeus)."
            : $"{m.Name} · nível {m.Progress?.Level ?? 0} · {m.Coins:N0} moedas · {m.Map?.DisplayName}";
        if (m.HasClock)
        {
            var h = m.HourNow;
            _clock.Text = $"Agora: {(int)h:00}:{(int)(h % 1 * 60):00}";
        }
    }

    // ------------------------------------------------------------ ações

    private void God(string action, string? arg = null, long amount = 0, int count = 1, string? extra = null)
    {
        Send?.Invoke(new GodIntent(action, arg, amount, count, extra));
    }

    private void GiveItem()
    {
        var sel = _items.GetSelectedItems();
        if (sel.Length == 0 || sel[0] >= _itemView.Count) return;
        God("item", _itemView[sel[0]].Id, (long)_itemQty.Value, (int)_itemCopies.Value, Qualities[Math.Max(0, _quality.Selected)]);
    }

    private void SpawnMonster()
    {
        var sel = _mons.GetSelectedItems();
        if (sel.Length == 0 || sel[0] >= _monView.Count) return;
        God("spawn", _monView[sel[0]].Id, (long)_monLevel.Value, (int)_monCount.Value);
    }

    private void Travel()
    {
        var sel = _maps.GetSelectedItems();
        if (_shown is null || sel.Length == 0 || sel[0] >= _shown.Maps.Count) return;
        God("travel", _shown.Maps[sel[0]].Id);
    }

    private void FillItems()
    {
        if (_shown is null) return;
        var q = _itemSearch.Text.Trim();
        var kind = ItemKinds[Math.Max(0, _itemKind.Selected)].Kind;
        _itemView = _shown.Items.Where(e => (kind.Length == 0 || e.Kind == kind || (kind == "material" && e.Kind is not ("weapon" or "armor" or "helmet" or "offhand" or "consumable")))
                                            && (q.Length == 0 || e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Id.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Take(400).ToList();
        _items.Clear();
        foreach (var e in _itemView) _items.AddItem($"{e.Name}   ·  nv {e.Level}  ·  {KindName(e.Kind)}");
    }

    private void FillMonsters()
    {
        if (_shown is null) return;
        var q = _monSearch.Text.Trim();
        _monView = _shown.Monsters.Where(e => q.Length == 0 || e.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Id.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        _mons.Clear();
        foreach (var e in _monView) _mons.AddItem($"{e.Name}   ·  nv {e.Level}{(e.Kind == "comum" ? "" : "  ·  " + e.Kind)}");
    }

    private static string KindName(string k) => k switch
    {
        "weapon" => "arma", "armor" => "armadura", "helmet" => "elmo", "offhand" => "mão esquerda", "consumable" => "consumível", _ => k,
    };

    private string _curMap = "";

    private void ShowPage(int idx)
    {
        for (var i = 0; i < _pages.Length; i++) { _pages[i].Visible = i == idx; _tabs[i].SetPressedNoSignal(i == idx); }
        // 12/11 ("ao clicar no modo lab, já leva para uma área DIFERENTE, não abre o lab onde você está"): as abas de teste
        // levam primeiro ao mapa Laboratório de Testes
        if (idx is 4 or 5 && _curMap != "laboratorio") Send?.Invoke(new GodIntent("travel", "laboratorio"));
    }

    // ------------------------------------------------------------ Laboratório (Sandbox)

    private ISandboxLab? _lab;
    private Label? _labStatus;

    /// <summary>
    /// Aba "Laboratório (Sandbox)": liga/desliga os protótipos isolados de <c>Client/Sandbox/</c> (troca a quente, sem
    /// reiniciar) e calibra os números ao vivo. Nada aqui muda o jogo oficial nem o servidor.
    /// </summary>
    public void AttachLab(ISandboxLab lab)
    {
        _lab = lab;
        var p = _pages[4];
        p.AddChild(Title("Laboratório (Sandbox)"));
        p.AddChild(Wrap("Protótipos isolados, só no seu cliente. Ligar troca o controle na hora; desligar volta ao jogo oficial " +
                        "no mesmo instante (o boneco volta para onde o servidor diz). Dica: ligue o Invulnerável — os monstros do " +
                        "servidor continuam vendo você parado onde ligou.", 12, Dim));
        var combat = new CheckButton { Text = "Combate & Movimentação v2", FocusMode = Control.FocusModeEnum.None };
        combat.AddThemeFontSizeOverride("font_size", 14);
        combat.Toggled += on => { lab.CombatV2 = on; combat.SetPressedNoSignal(lab.CombatV2); };
        var env = new CheckButton { Text = "Ambientação & Atmosfera v2", FocusMode = Control.FocusModeEnum.None };
        env.AddThemeFontSizeOverride("font_size", 14);
        env.Toggled += on => { lab.EnvironmentV2 = on; env.SetPressedNoSignal(lab.EnvironmentV2); };
        p.AddChild(combat);
        p.AddChild(env);
        var st = lab.Settings;
        p.AddChild(Title("Calibragem ao vivo"));
        p.AddChild(Slider("Input Buffer Window (ms)", 0, 400, 10, st.InputBufferMs, v => st.InputBufferMs = v, "0"));
        p.AddChild(Slider("Dash Velocity (m/s)", 6, 30, 0.5, st.DashVelocity, v => st.DashVelocity = v, "0.0"));
        p.AddChild(Slider("Hitstop Scale (escala do tempo)", 0.01, 1, 0.01, st.HitstopScale, v => st.HitstopScale = v, "0.00"));
        p.AddChild(Slider("Hitstop (ms)", 0, 200, 5, st.HitstopMs, v => st.HitstopMs = v, "0"));
        // 12/11 ("movimento AAA: mais lento, esquiva com preparação")
        p.AddChild(Slider("Velocidade de andar/correr (×)", 0.5, 1.3, 0.05, st.MoveScale, v => st.MoveScale = v, "0.00"));
        p.AddChild(Slider("Aceleração (× — menor = mais peso)", 0.2, 1.5, 0.05, st.AccelScale, v => st.AccelScale = v, "0.00"));
        p.AddChild(Slider("Preparação da esquiva (ms)", 0, 300, 10, st.DodgeWindupMs, v => st.DodgeWindupMs = v, "0"));
        p.AddChild(Row(Btn("Predefinição AAA pesado (0,8× · 0,45× · 120 ms)", () => { st.MoveScale = 0.8; st.AccelScale = 0.45; st.DodgeWindupMs = 120; st.NotifyChanged(); }),
            Btn("Padrão", () => { st.MoveScale = 1; st.AccelScale = 1; st.DodgeWindupMs = 0; st.NotifyChanged(); })));
        var dummies = new CheckBox { Text = "Bonecos de treino atacam", ButtonPressed = st.DummiesAttack, FocusMode = Control.FocusModeEnum.None };
        dummies.AddThemeFontSizeOverride("font_size", 13);
        dummies.Toggled += on => { st.DummiesAttack = on; st.NotifyChanged(); };
        p.AddChild(dummies);
        // 12/11 ("dá para testar o v2 contra os mobs que já existem?"): os bonecos viram a espécie escolhida na aba Monstros
        p.AddChild(Title("Mobs do jogo no v2"));
        p.AddChild(Wrap("Escolha um monstro na aba Monstros (clique uma vez) e aperte o botão: os 3 bonecos viram essa espécie — " +
                        "com o visual dela, perseguindo, preparando (anel) e atacando como mob v2. Chefe bate mais e aguenta mais.", 12, Dim));
        var usar = new Button { Text = "Usar o monstro selecionado", FocusMode = Control.FocusModeEnum.None };
        var madeira = new Button { Text = "Voltar ao boneco de madeira", FocusMode = Control.FocusModeEnum.None };
        var qual = Wrap("Agora: boneco de madeira", 12, Gold);
        usar.Pressed += () =>
        {
            var sel = _mons.GetSelectedItems();
            if (sel.Length == 0 || sel[0] >= _monView.Count) { qual.Text = "Selecione um monstro na aba Monstros primeiro."; return; }
            var e = _monView[sel[0]];
            st.DummySpecies = e.Id; st.DummyName = e.Name; st.DummyVisual = e.Visual; st.DummyBoss = e.Kind == "chefe";
            qual.Text = $"Agora: {e.Name} (nv {e.Level}{(st.DummyBoss ? ", chefe" : "")})" + (lab.CombatV2 ? "" : " — ligue o Combate v2");
            lab.RespawnDummies();
        };
        madeira.Pressed += () => { st.DummySpecies = ""; qual.Text = "Agora: boneco de madeira"; lab.RespawnDummies(); };
        var linha = new HBoxContainer();
        linha.AddChild(usar); linha.AddChild(madeira);
        p.AddChild(linha);
        p.AddChild(qual);
        p.AddChild(Title("Estado"));
        _labStatus = Wrap(lab.Status, 12, Gold);
        p.AddChild(_labStatus);
        p.AddChild(Wrap("Controles no v2: WASD anda · Shift corre · clique esquerdo = combo de 3 golpes (segure ou repita) · " +
                        "Espaço/Ctrl = esquiva (cancela preparação e recuperação). Q/E/R/T ficam desligados durante o teste.", 12, Dim));
    }

    private static Control Slider(string label, double min, double max, double step, double value, Action<double> set, string fmt)
    {
        var box = new VBoxContainer();
        var head = HudView.MakeLabel($"{label}: {value.ToString(fmt)}", 13, Dim);
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, FocusMode = Control.FocusModeEnum.None,
                              CustomMinimumSize = new Vector2(500, 18) };
        s.ValueChanged += v => { set(v); head.Text = $"{label}: {v.ToString(fmt)}"; };
        box.AddChild(head);
        box.AddChild(s);
        return box;
    }

    // ------------------------------------------------------------ peças

    private static Button Btn(string text, Action onPress)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", 13);
        b.Pressed += onPress;
        return b;
    }

    private static SpinBox Spin(double min, double max, double step, double value)
        => new() { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(96, 0), SelectAllOnFocus = true };

    private static ItemList List(float height)
    {
        var l = new ItemList { CustomMinimumSize = new Vector2(0, height), SizeFlagsVertical = Control.SizeFlags.ExpandFill, SelectMode = ItemList.SelectModeEnum.Single };
        l.AddThemeFontSizeOverride("font_size", 13);
        return l;
    }

    private static HBoxContainer Row(params Control[] items)
    {
        var r = new HBoxContainer();
        r.AddThemeConstantOverride("separation", 6);
        foreach (var c in items) r.AddChild(c);
        return r;
    }

    private static Label Title(string t) => HudView.MakeLabel(t, 15, Gold);

    private static Label Wrap(string t, int size, Color c)
    {
        var l = HudView.MakeLabel(t, size, c);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(440, 0);
        return l;
    }
}
