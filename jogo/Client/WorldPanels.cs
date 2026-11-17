using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Telas do E9 (todas só mostram o que o servidor mandou e enviam intenções; o servidor valida tudo):
/// conversa com NPC (missões e loja), diário (J), atributos (C), mapa do mundo (M), grupo (P), quadros do grupo,
/// convite e avisos no centro da tela.
/// </summary>
public sealed class WorldPanels
{
    private static readonly Color Gold = new(0.95f, 0.78f, 0.30f);
    private static readonly Color Dim = new(0.70f, 0.68f, 0.62f);
    private static readonly Color Good = new(0.55f, 0.85f, 0.5f);

    public Control Root { get; }
    public event Action<NetMessage>? Send;
    /// <summary>12/11: x da borda esquerda da mochila aberta (o baú abre colado nela, sem ficar por baixo).</summary>
    public Func<float?>? InventarioEsquerda { get; set; }

    private readonly PanelContainer _dialog, _journal, _stats, _map, _party, _craft, _storage, _powers;
    private VBoxContainer? _mapSide;
    private VBoxContainer _proximos = new();
    private int _proximosNivel = -1;
    private readonly VBoxContainer _powersBox;
    private string _powersSig = "";
    /// <summary>Livro de Poderes: o jogador pôs (ou tirou, skillId vazio) um poder numa tecla.</summary>
    public event Action<string, string>? BindRequested;
    private readonly VBoxContainer _dialogBox, _journalBox, _statsBox, _partyBox, _frames, _waypoints, _craftBox, _storageBox;
    private readonly GradeItens _storageGrid;
    private readonly Label _storageInfo;
    private readonly Button _storageClose;
    private ClientModel? _storageModel;
    private string _craftSig = "", _storageSig = "";
    // 11/11: ícones da forja que ainda estavam sendo fotografados — trocam sozinhos quando ficam prontos
    private readonly List<(TextureRect Rect, ItemInfo? Info, string Def)> _craftIcons = new();
    /// <summary>Último pedido de fabricação (para o som certo de cada estação).</summary>
    public string LastStationKind { get; private set; } = "";
    private readonly WorldMapCanvas _canvas;
    private readonly Label _banner, _subBanner, _invite, _coins;
    private readonly LineEdit _inviteName;
    private double _bannerAge = 99, _subAge = 99;
    private string _dialogSig = "", _journalSig = "", _statsSig = "", _mapSig = "", _partySig = "", _framesSig = "";

    public WorldPanels()
    {
        Root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ItemIconRenderer.IconReady += RefreshCraftIcons;
        Root.TreeExiting += () => ItemIconRenderer.IconReady -= RefreshCraftIcons;

        _dialog = Window("", new Vector2(520, 520), Control.LayoutPreset.CenterLeft, new Vector2(40, -260), out _dialogBox, "dialogo");
        _journal = Window("Diário de missões  (J fecha)", new Vector2(460, 460), Control.LayoutPreset.CenterLeft, new Vector2(40, -230), out _journalBox);
        _stats = Window("Atributos  (C fecha)", new Vector2(430, 470), Control.LayoutPreset.CenterLeft, new Vector2(40, -165), out _statsBox);
        _party = Window("Grupo  (P fecha)", new Vector2(420, 420), Control.LayoutPreset.CenterLeft, new Vector2(40, -210), out _partyBox);
        _powers = Window("Livro de Poderes  (K fecha)", new Vector2(560, 560), Control.LayoutPreset.CenterLeft, new Vector2(40, -280), out _powersBox);
        _craft = Window("", new Vector2(580, 600), Control.LayoutPreset.CenterLeft, new Vector2(40, -280), out _craftBox);
        // baú / armazém estilo MU (10/11): grade de casas à esquerda, a mochila abre à direita; arrasta entre as duas
        _storage = new JanelaV2("BAÚ", 16f) { Visible = false };
        _storageBox = new VBoxContainer();
        _storageBox.AddThemeConstantOverride("separation", 6);
        _storage.AddChild(_storageBox);
        _storageInfo = HudView.MakeLabel("", 13, Dim, 2);
        _storageInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _storageInfo.CustomMinimumSize = new Vector2(320, 0);
        _storageBox.AddChild(_storageInfo);
        _storageGrid = new GradeItens { Colunas = 8, Linhas = 8, Casa = 40f, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _storageGrid.Ajustar();
        _storageBox.AddChild(_storageGrid);
        var closeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _storageClose = new Button { Text = "Fechar (Esc)", FocusMode = Control.FocusModeEnum.None };
        closeRow.AddChild(_storageClose);
        _storageBox.AddChild(closeRow);
        Root.AddChild(_storage);
        Arrastador.Ligar(_storage, "bau", 46f, porDesvio: true);

        // mapa do mundo: desenho + lista de pontos de viagem
        // 12/11: abas "Mapa local" / "Mapa geral", zoom (+ e −, rodinha) e a lateral rola (antes crescia e cortava embaixo)
        _map = new StonePanel(16f) { Visible = false };
        _map.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _map.CustomMinimumSize = new Vector2(900, 580);
        var mapRow = MapaCorpo = new HBoxContainer();
        mapRow.AddThemeConstantOverride("separation", 12);
        _map.AddChild(mapRow);
        _canvas = new WorldMapCanvas { CustomMinimumSize = new Vector2(580, 550) };
        _canvas.ViajarPara += id => Send?.Invoke(new TravelIntent(id));
        mapRow.AddChild(_canvas);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        side.AddThemeConstantOverride("separation", 6);
        side.AddChild(HudView.MakeLabel("Mapa  (M fecha)", 18, Gold));
        var abas = new HBoxContainer();
        abas.AddThemeConstantOverride("separation", 4);
        var abaLocal = new Button { Text = "Mapa local", ToggleMode = true, ButtonPressed = true, FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var abaGeral = new Button { Text = "VER MAPA GERAL", ToggleMode = true, FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        void Aba(bool local)
        {
            _canvas.AreaMode = local;
            abaLocal.SetPressedNoSignal(local);
            abaGeral.SetPressedNoSignal(!local);
            _canvas.QueueRedraw();
        }
        abaLocal.Pressed += () => Aba(true);
        abaGeral.Pressed += () => Aba(false);
        abas.AddChild(abaLocal);
        abas.AddChild(abaGeral);
        side.AddChild(abas);
        var zoomRow = new HBoxContainer();
        zoomRow.AddThemeConstantOverride("separation", 4);
        zoomRow.AddChild(HudView.MakeLabel("Zoom:", 13, Dim));
        var menos = new Button { Text = "  −  ", FocusMode = Control.FocusModeEnum.None };
        var mais = new Button { Text = "  +  ", FocusMode = Control.FocusModeEnum.None };
        var centro = new Button { Text = "Centralizar", FocusMode = Control.FocusModeEnum.None };
        menos.Pressed += () => _canvas.Zoom(1f / 1.3f);
        mais.Pressed += () => _canvas.Zoom(1.3f);
        centro.Pressed += () => _canvas.ResetView();
        zoomRow.AddChild(menos); zoomRow.AddChild(mais); zoomRow.AddChild(centro);
        side.AddChild(zoomRow);
        side.AddChild(Wrap("Rodinha do mouse: zoom. Arraste para mover.", 11, Dim, 270));
        var rolagem = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(280, 0) };
        var lista = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        lista.AddThemeConstantOverride("separation", 6);
        rolagem.AddChild(lista);
        side.AddChild(rolagem);
        lista.AddChild(Wrap("Viagem rápida: fique parado num ponto de viagem, fora de combate, e escolha o destino.", 12, Dim, 260));
        _waypoints = new VBoxContainer();
        _waypoints.AddThemeConstantOverride("separation", 4);
        lista.AddChild(_waypoints);
        lista.AddChild(new HSeparator());
        lista.AddChild(HudView.MakeLabel("Próximos desbloqueios", 14, Gold));
        _proximos = new VBoxContainer();
        _proximos.AddThemeConstantOverride("separation", 3);
        lista.AddChild(_proximos);
        lista.AddChild(new HSeparator());
        // 13/11 ("passar o mouse no ícone mostra a legenda dele, em vez da legenda embaixo"): o próprio mapa explica cada ícone
        lista.AddChild(Wrap("Passe o mouse num ícone do mapa para ver o que é (missão, NPC, passagem, pedra de viagem, horda).", 12, Dim, 260));
        mapRow.AddChild(side);
        _mapSide = side;
        Root.AddChild(_map);
        Arrastador.Ligar(_map, "mapa", 46f, porDesvio: true);   // 12/11: toda janela arrasta e fica dentro da tela

        // grupo: quadros sempre visíveis à esquerda, abaixo do cartão do jogador
        _frames = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(14, 104) };
        _frames.AddThemeConstantOverride("separation", 3);
        Root.AddChild(_frames);

        // convite de grupo
        _invite = HudView.MakeLabel("", 16, Gold);
        var invitePanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        invitePanel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(10f, 0.92f));
        invitePanel.AddChild(_invite);
        invitePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        invitePanel.Position = new Vector2(-220, 120);
        invitePanel.CustomMinimumSize = new Vector2(440, 0);
        Root.AddChild(invitePanel);

        // avisos: nome da área (grande) e recompensas/alertas (menor)
        _banner = HudView.MakeLabel("", 34, Gold, 6);
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _banner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _banner.Position = new Vector2(-400, 215);
        _banner.Size = new Vector2(800, 50);
        Root.AddChild(_banner);
        _subBanner = HudView.MakeLabel("", 18, new Color(0.92f, 0.90f, 0.84f), 5);
        _subBanner.HorizontalAlignment = HorizontalAlignment.Center;
        _subBanner.AutowrapMode = TextServer.AutowrapMode.WordSmart;   // 13/11: dica longa vira duas linhas, não sai da tela
        _subBanner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _subBanner.Position = new Vector2(-400, 266);
        _subBanner.Size = new Vector2(800, 30);
        Root.AddChild(_subBanner);

        // moedas (canto inferior direito, acima do registro)
        _coins = HudView.MakeLabel("", 15, Gold);
        _coins.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _coins.Position = new Vector2(300, -40);
        _coins.Visible = false; // moedas agora ficam no HUD (canto inferior direito)
        Root.AddChild(_coins);

        _inviteName = new LineEdit { PlaceholderText = "nome do personagem", CustomMinimumSize = new Vector2(200, 0) };
    }

    private PanelContainer Window(string title, Vector2 size, Control.LayoutPreset preset, Vector2 offset, out VBoxContainer box, string? nome = null)
    {
        var panel = new StonePanel(16f) { Visible = false, CustomMinimumSize = size };   // moldura de pedra e bronze (estilo MU)
        panel.SetAnchorsAndOffsetsPreset(preset);
        panel.Position = offset;
        var scroll = new ScrollContainer { CustomMinimumSize = size - new Vector2(24, 24), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(scroll);
        box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(box);
        if (title.Length > 0) box.AddChild(HudView.MakeLabel(title, 18, Gold));
        Root.AddChild(panel);
        Arrastador.Ligar(panel, nome ?? (title.Length > 0 ? title : "oficina"));   // 12/11: arrasta pelo título
        return panel;
    }

    // ------------------------------------------------------------ abrir/fechar

    /// <summary>14/11 tarde: miolo do mapa (o TAB o pega emprestado na aba Mapa) e se ele está lá dentro agora.</summary>
    public HBoxContainer MapaCorpo { get; private set; } = null!;
    private bool _mapaNoMenu;
    public bool MapaNoMenu { get => _mapaNoMenu; set { if (value && !_mapaNoMenu) _canvas.ResetView(); _mapaNoMenu = value; } }

    public bool AnyOpen => _dialog.Visible || _journal.Visible || _stats.Visible || _map.Visible || _party.Visible || _craft.Visible || _storage.Visible || _powers.Visible;
    public bool DialogOpen => _dialog.Visible;

    public void CloseAll()
    {
        _dialog.Visible = _journal.Visible = _stats.Visible = _map.Visible = _party.Visible = _craft.Visible = _storage.Visible = _powers.Visible = false;
    }

    public bool IsOpen(string which) => which switch
    {
        "journal" => _journal.Visible, "stats" => _stats.Visible, "map" => _map.Visible, "party" => _party.Visible, "poderes" => _powers.Visible, _ => false,
    };

    public void Toggle(string which)
    {
        var target = which switch { "journal" => _journal, "stats" => _stats, "map" => _map, "party" => _party, "poderes" => _powers, _ => null };
        if (target is null) return;
        var show = !target.Visible;
        CloseAll();
        target.Visible = show;
        if (show && target == _map) _canvas.ResetView();
        _journalSig = _statsSig = _mapSig = _partySig = _powersSig = "";
    }

    public bool MouseOverDialog => _dialog.Visible && _dialog.GetGlobalRect().HasPoint(_dialog.GetGlobalMousePosition());
    public void ShowDialog() { CloseAll(); _dialog.Visible = true; _dialogSig = ""; }

    // ------------------------------------------------------------ atualização por quadro

    public void Update(ClientModel m, double delta)
    {
        // avisos
        while (m.NextNotice() is { } n)
        {
            // 16/11: anoiteceu/amanheceu — anúncio de tela própria (lua, título vermelho, o que mudou)
            if (n.Kind is "noite" or "amanhecer") { AvisoNoite.Pedir(n.Kind == "noite"); continue; }
            if (n.Kind == "pesquisa_abrir") continue;   // 17/11: abre a caixa do Catálogo (Main)
            if (n.Kind == "pedra") n = n with { Text = n.Text.Split('|')[0], Kind = "info" };   // 17/11: golpe na rocha oca
            if (n.Kind == "regiao")
            {
                // 13/11: primeira vez numa região — cartão grande de "capítulo novo"
                _banner.Text = "✦ " + n.Text + " ✦";
                _bannerAge = -2.5;
                _subBanner.Text = "Região nova descoberta — siga o ★ do caminho principal";
                _subAge = -2.5;
                continue;
            }
            if (n.Kind == "area")
            {
                var region = m.WorldMap?.Regions.FirstOrDefault(r => r.Here);
                _banner.Text = n.Text;
                _bannerAge = 0;
                _subBanner.Text = region is not null && region.Levels != "cidade" ? $"{region.Name} · nível {region.Levels}" : "";
                _subAge = 0;
            }
            else
            {
                _subBanner.Text = n.Text;
                _subBanner.AddThemeColorOverride("font_color", n.Kind switch
                {
                    "warn" => new Color(1f, 0.55f, 0.4f),
                    "legend" => new Color(0.3f, 1f, 0.6f),
                    "reward" => Gold,
                    _ => new Color(0.92f, 0.90f, 0.84f),
                });
                _subAge = 0;
            }
        }
        _bannerAge += delta;
        _subAge += delta;
        _banner.Modulate = new Color(1, 1, 1, (float)Math.Clamp(3.5 - _bannerAge, 0, 1));
        _subBanner.Modulate = new Color(1, 1, 1, (float)Math.Clamp(3.0 - _subAge, 0, 1));

        _coins.Text = "";
        var invitePanel = (PanelContainer)_invite.GetParent();
        invitePanel.Visible = m.PendingInvite is not null;
        if (m.PendingInvite is { } inv) _invite.Text = inv.Kind == "duelo" ? $"{inv.FromName} te desafiou para um x1 (aqui mesmo)!   Y aceitar · N recusar" : $"{inv.FromName} convidou você para o grupo.   Y aceitar · N recusar";

        UpdateFrames(m);
        if (m.Dialog is null && _dialog.Visible) _dialog.Visible = false;
        // 13/11 ("as Próximas missões ficam sempre abertas em todo NPC; fecha quando fechar a conversa"): diálogo fechado =
        // a abinha volta a ficar recolhida
        if (!_dialog.Visible) _showLocked = false;
        if (_dialog.Visible) UpdateDialog(m);
        _craft.Visible = m.Station is not null;
        if (_craft.Visible) UpdateCraft(m);
        _storage.Visible = m.Storage is not null;
        if (_storage.Visible) UpdateStorage(m);
        if (_journal.Visible) UpdateJournal(m);
        if (_stats.Visible) UpdateStats(m);
        else if (PendingTotal > 0) CommitStats();   // 12/11: fechou/minimizou a ficha = pontos valem
        if (_map.Visible || MapaNoMenu)
        {
            UpdateMap(m);
            _canvas.Layout = m.Map;
            _canvas.Markers = m.QuestMarkers;
            _canvas.NpcMarks = m.NpcMarkers;
            _canvas.NpcShown ??= m.NpcVisible;
            _canvas.Flags = m.Flags;
            if (m.Me is { } me) { _canvas.MyLevel = me.Level; _canvas.Me = new Vector2((float)me.X, (float)me.Y); _canvas.MeFacing = new Vector2((float)me.FacingX, (float)me.FacingY); }
            if (_canvas.AreaMode) _canvas.QueueRedraw();
            // 11/11 noite ("o mapa abriu para baixo, metade fora da tela"): sempre centralizado na tela
            // 12/11 ("em tela cheia ainda corta embaixo"): o painel cabe na tela (o desenho cresce com ela, a lateral rola)
            var vp = _map.GetViewportRect().Size;
            if (MapaNoMenu)
            {
                // dentro do TAB: tamanho fixo que cabe no corpo do menu (1000×660)
                if (Mathf.Abs(_canvas.CustomMinimumSize.X - 500f) > 2f) { _canvas.CustomMinimumSize = new Vector2(500, 500); if (_mapSide is not null) _mapSide.CustomMinimumSize = new Vector2(280, 500); }
                goto fimMapa;
            }
            var alto = Mathf.Clamp(vp.Y - 40f, 420f, 900f);
            var lado = Mathf.Clamp(Mathf.Min(alto - 32f, vp.X - 360f), 380f, 900f);
            if (Mathf.Abs(_canvas.CustomMinimumSize.X - lado) > 2f)
            {
                _canvas.CustomMinimumSize = new Vector2(lado, lado);
                _map.CustomMinimumSize = new Vector2(lado + 312f, lado + 32f);
                if (_mapSide is not null) _mapSide.CustomMinimumSize = new Vector2(280, lado);
                _map.Size = _map.CustomMinimumSize;
            }
            _map.Position = (new Vector2(Mathf.Max(8f, (vp.X - _map.Size.X) / 2f), Mathf.Max(8f, (vp.Y - _map.Size.Y) / 2f)) + Arrastador.Desvio("mapa")).Floor();
            fimMapa:;
        }
        if (_party.Visible) UpdateParty(m);
        if (_powers.Visible) UpdatePowers(m);
    }

    /// <summary>Função do NPC como texto de gente ("missoes" → "Missões").</summary>
    private static string RoleName(string role) => role switch
    {
        "missoes" => "Missões", "mentor" => "Mentor", "ferreiro" => "Ferreiro", "curandeira" => "Curandeira",
        "mercador" => "Mercador", "guarda" => "Guarda", "biblioteca" => "Biblioteca", "biblioteca_continente" => "Biblioteca do Continente", "" => "",
        _ => char.ToUpperInvariant(role[0]) + role[1..],
    };

    /// <summary>Qual retrato de boneco combina com o NPC (mentores das classes novas pelo nome).</summary>
    private static string PortraitClass(string role, string name)
    {
        if (name.Contains("Bruxa", StringComparison.OrdinalIgnoreCase)) return "bruxa";
        if (name.Contains("Batedor", StringComparison.OrdinalIgnoreCase) || name.Contains("Caçad", StringComparison.OrdinalIgnoreCase)) return "arqueiro";
        return role.Contains("Arcan", StringComparison.OrdinalIgnoreCase) || role.Contains("Ervas", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Arcanista", StringComparison.OrdinalIgnoreCase) ? "mago" : "guerreiro";
    }

    private bool _showLocked;
    private string _libOpen = "";

    /// <summary>12/11: Biblioteca do Continente — uma aba por região; aberta, lista cada monstro e o grupo de drop dele.</summary>
    private void AddLibrary(IReadOnlyList<LibraryEntry> lib)
    {
        _dialogBox.AddChild(Section("Bestiário e drops por região"));
        _dialogBox.AddChild(HudView.MakeLabel("Peças de conjunto: V1 é a mais comum; cada variação acima é mais rara. Chefes e dungeons vão mais alto.", 11, Dim));
        foreach (var g in lib.GroupBy(e => e.Region))
        {
            var first = g.First();
            var open = _libOpen == g.Key;
            var tab = new Button { Text = (open ? "▾ " : "▸ ") + $"{g.Key}  (nível {first.Levels}, {g.Count()} criaturas)", Flat = true,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, FocusMode = Control.FocusModeEnum.None };
            var key = g.Key;
            tab.Pressed += () => { _libOpen = _libOpen == key ? "" : key; _dialogSig = ""; };
            _dialogBox.AddChild(tab);
            if (!open) continue;
            foreach (var e in g)
            {
                var card = new PanelContainer();
                card.AddThemeStyleboxOverride("panel", HudView.CardStyle(e.Tag.Length > 0));
                var col = new VBoxContainer();
                col.AddThemeConstantOverride("separation", 2);
                card.AddChild(col);
                var tag = e.Tag.Length > 0 ? $"  · {e.Tag}" : "";
                col.AddChild(HudView.MakeLabel($"{e.Monster}  —  nível {e.MonsterLevels}{tag}", 14, e.Tag.Length > 0 ? Gold : new Color(0.9f, 0.88f, 0.82f)));
                if (e.Type.Length > 0) col.AddChild(HudView.MakeLabel("Natureza: " + e.Type, 11, new Color(0.6f, 0.8f, 0.95f)));
                foreach (var dr in e.Drops)
                {
                    var c = dr.StartsWith("Peça de conjunto") && e.MaxVariation > 0
                        ? ItemTooltip.VarColor(Variacao.Quality(e.MaxVariation)) ?? Dim
                        : Dim;
                    col.AddChild(Wrap("• " + dr, 12, c, 440));
                }
                _dialogBox.AddChild(card);
            }
        }
    }

    private void UpdateDialog(ClientModel m)
    {
        var d = m.Dialog!;
        var sig = $"{_showLocked}|{_libOpen}|{d.NpcId}|{string.Join(",", d.Quests.Select(q => q.Id + q.State + q.Progress))}|{m.Coins}|{m.Inventory.Count}|{(m.Buyback is { } bb ? bb.NpcId + bb.Items.Count : "")}|{d.RepairCost}";
        if (sig == _dialogSig) return;
        _dialogSig = sig;
        Clear(_dialogBox);
        // cabeçalho: retrato do boneco, nome e função
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(new PuppetPortrait { ClassId = PortraitClass(d.Role, d.Name), CustomMinimumSize = new Vector2(64, 64) });
        var names = new VBoxContainer();
        names.AddChild(HudView.MakeLabel(d.Name, 22, Gold));
        names.AddChild(HudView.MakeLabel(RoleName(d.Role), 13, Dim));
        head.AddChild(names);
        _dialogBox.AddChild(head);
        var greet = new PanelContainer();
        greet.AddThemeStyleboxOverride("panel", HudView.CardStyle());
        greet.AddChild(Wrap($"“{d.Greeting}”", 14, new Color(0.88f, 0.85f, 0.78f), 450));
        _dialogBox.AddChild(greet);
        // 16/11 (dono: "os itens vão quebrando, tem que ir no ferreiro arrumar"): morrer gasta o equipamento
        if (d.RepairCost >= 0)
        {
            _dialogBox.AddChild(Section($"Consertar equipamento — você tem {m.Coins} moedas"));
            var gastos = m.Equipment.Where(e => e.Durability < 100).OrderBy(e => e.Durability).ToList();
            var row = new HBoxContainer();
            var txt = d.RepairCost == 0 ? "Está tudo inteiro."
                : $"Consertar tudo — {d.RepairCost} moedas" + (gastos.Count > 0 ? $" (pior peça: {gastos[0].Durability}%)" : "");
            var label = HudView.MakeLabel(txt, 14, d.RepairCost > m.Coins ? new Color(1f, 0.45f, 0.4f) : null);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            if (d.RepairCost > 0)
            {
                var npc = d.NpcId;
                var go = new Button { Text = "Consertar", Disabled = d.RepairCost > m.Coins };
                go.Pressed += () => { Send?.Invoke(new RepairIntent(npc)); Sfx.Play("forja"); };
                row.AddChild(go);
            }
            _dialogBox.AddChild(row);
            if (gastos.Any(e => e.Durability <= 0))
                _dialogBox.AddChild(Wrap("Peça quebrada (0%) não dá atributos até ser consertada.", 12, new Color(1f, 0.55f, 0.5f), 440));
        }
        if (d.Library is { Count: > 0 } lib) AddLibrary(lib);
        if (d.Quests.Count > 0)
        {
            _dialogBox.AddChild(Section("Missões"));
            var doneCount = d.Quests.Count(q => q.State == "done");
            if (doneCount > 0) _dialogBox.AddChild(HudView.MakeLabel($"{doneCount} missão(ões) já concluída(s) com este NPC (no diário, J).", 11, Dim));
            // 11/11 noite ("só mostra as liberadas para pegar; as bloqueadas numa abinha Próximas Missões")
            var open = d.Quests.Where(q => q.State is "ready" or "available" or "active").OrderBy(q => q.State switch { "ready" => 0, "available" => 1, _ => 2 }).ToList();
            if (open.Count == 0) _dialogBox.AddChild(HudView.MakeLabel("Nada para pegar agora.", 12, Dim));
            foreach (var q in open)
            {
                var card = new PanelContainer();
                card.AddThemeStyleboxOverride("panel", HudView.CardStyle(q.State is "ready" or "available"));
                var col = new VBoxContainer();
                col.AddThemeConstantOverride("separation", 4);
                card.AddChild(col);
                var state = q.State switch
                {
                    "available" => "nova",
                    "active" => $"{q.Progress}/{q.Count}",
                    "ready" => "pronta para entregar!",
                    "done" => "concluída",
                    _ => "bloqueada",
                };
                var row = new HBoxContainer();
                var label = HudView.MakeLabel($"◆ {q.Name}", 16, q.State == "ready" ? Good : q.State == "done" ? Dim : Gold);
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(label);
                row.AddChild(HudView.MakeLabel(state, 12, q.State == "ready" ? Good : Dim));
                col.AddChild(row);
                col.AddChild(Wrap(q.Description, 13, new Color(0.85f, 0.82f, 0.75f), 440));
                if (q.State == "active" && q.Count > 0)
                {
                    var bar = new ProgressBar { MinValue = 0, MaxValue = q.Count, Value = q.Progress, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6) };
                    col.AddChild(bar);
                }
                if (q.Rewards.Length > 0) col.AddChild(Wrap("Recompensa: " + q.Rewards, 12, Gold, 440));
                if (q.State is "available" or "ready")
                {
                    var b = new Button { Text = q.State == "available" ? (q.Id.StartsWith("q_troca_") ? "Trocar de classe" : "Aceitar") : "Entregar e receber", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
                    var id = q.Id;
                    var action = q.State == "available" ? "accept" : "complete";
                    b.Pressed += () => Send?.Invoke(new QuestIntent(id, action));
                    col.AddChild(b);
                }
                _dialogBox.AddChild(card);
            }
        }
        if (d.Shop.Count > 0)
        {
            // loja estilo MU: vitrine com o desenho de cada item, preço embaixo; passar o mouse mostra tudo; duplo clique compra
            _dialogBox.AddChild(Section($"Loja — você tem {m.Coins:N0} moedas".Replace(",", ".")));
            _dialogBox.AddChild(Wrap("Duplo clique (ou botão direito) compra. Para vender: arraste o item da mochila para cá.", 12, Dim));
            var shopNpc = d.NpcId;
            ((StonePanel)_dialog).OnItemDrop = (id, kind) => { if (kind == "bag" || kind.StartsWith("equip:")) { Send?.Invoke(new SellIntent(Guid.NewGuid().ToString("N"), shopNpc, id)); Sfx.Play("vender"); } };
            var grid = new GridContainer { Columns = 7 };
            grid.AddThemeConstantOverride("h_separation", 4);
            grid.AddThemeConstantOverride("v_separation", 4);
            foreach (var offer in d.Shop)
            {
                var info = m.ItemInfoFor(offer.DefinitionId);
                var cell = new ItemSlot(62f, null) { Price = offer.Price };
                var def = offer.DefinitionId;
                var npc = d.NpcId;
                var price = offer.Price;
                cell.Show(info, def, 1, "comum", price <= m.Coins, false);   // 12/11: a loja vende o item cru (Cinza, sem atributo), como o servidor entrega
                cell.DroppedOn += (id, kind) => { if (kind == "bag" || kind.StartsWith("equip:")) { Send?.Invoke(new SellIntent(Guid.NewGuid().ToString("N"), npc, id)); Sfx.Play("vender"); } };
                cell.Clicked += (dbl, right) =>
                {
                    if (!(dbl || right)) return;
                    if (price > m.Coins) { Sfx.Play("ui_erro"); return; }
                    Send?.Invoke(new BuyIntent(Guid.NewGuid().ToString("N"), npc, def));
                };
                grid.AddChild(cell);
            }
            _dialogBox.AddChild(grid);
            // 10/11: balcão de recompra — o que você vendeu agora há pouco; some quando você se afasta ou depois de um tempo
            if (m.Buyback is { } back && back.NpcId == d.NpcId && back.Items.Count > 0)
            {
                _dialogBox.AddChild(Section("Recomprar — vendidos agora há pouco"));
                _dialogBox.AddChild(Wrap("Vendeu sem querer? Duplo clique (ou arraste para a mochila) compra de volta pelo mesmo preço. Ao sair da loja o vendedor fica com eles.", 12, Dim));
                var backGrid = new GridContainer { Columns = 7 };
                backGrid.AddThemeConstantOverride("h_separation", 4);
                backGrid.AddThemeConstantOverride("v_separation", 4);
                foreach (var it in back.Items)
                {
                    var info = m.ItemInfoFor(it.DefinitionId);
                    var cell = new ItemSlot(62f, null) { Price = it.Price, DragItemId = it.ItemId, DragKind = "recompra:" + d.NpcId };
                    cell.Show(info, it.DefinitionId, it.Quantity, it.Quality, it.Price <= m.Coins, false);
                    var npc = d.NpcId;
                    var itemId = it.ItemId;
                    var price = it.Price;
                    cell.Clicked += (dbl, right) =>
                    {
                        if (!(dbl || right)) return;
                        if (price > m.Coins) { Sfx.Play("ui_erro"); return; }
                        Send?.Invoke(new BuybackIntent(Guid.NewGuid().ToString("N"), npc, itemId));
                    };
                    backGrid.AddChild(cell);
                }
                _dialogBox.AddChild(backGrid);
            }
        }
        if (d.Travel is { Count: > 0 })
        {
            var isReset = d.Role == "atributos";
            _dialogBox.AddChild(Section(isReset ? $"Reinício de atributos — você tem {m.Coins} moedas" : $"Viagens — você tem {m.Coins} moedas"));
            foreach (var t in d.Travel)
            {
                var row = new HBoxContainer();
                var trancado = t.Label.Contains("trancado");   // 13/11: viagem com trava de nível = vermelha
                var label = HudView.MakeLabel((trancado ? "🔒 " : "") + $"{t.Label} — {t.Price} moedas", 14,
                    trancado ? new Color(1f, 0.45f, 0.4f) : t.Price > m.Coins ? new Color(0.55f, 0.53f, 0.5f) : null);
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(label);
                var reset = t.Id == "reset_atributos";
                var go = new Button { Text = reset ? "Pagar e reiniciar" : "Pagar e ir", Disabled = t.Price > m.Coins || trancado };
                var npc = d.NpcId;
                var id = t.Id;
                go.Pressed += () => { Send?.Invoke(new PaidTravelIntent(npc, id)); if (!reset) { m.Dialog = null; _dialog.Visible = false; } };
                row.AddChild(go);
                _dialogBox.AddChild(row);
            }
            _dialogBox.AddChild(Wrap(isReset ? "Os pontos gastos voltam como pontos livres (tecla C). Os ganhos da 2ª classe ficam." : "Novos destinos aparecem quando você os descobre a pé.", 12, Dim));
        }
        // 13/11 ("Próximas missões no final de cada NPC: missões, loja e tudo vêm antes")
        var locked = d.Quests.Where(q => q.State is not ("done" or "ready" or "available" or "active")).ToList();
        // 13/11 ("PRÓXIMAS MISSÕES sempre abaixo das de aceitar e entregar; só as 5 próximas"; "ícone vermelho quando o
        // nível está muito abaixo"): as trancadas ficam embaixo, em vermelho, só as 5 mais perto do seu nível
        if (locked.Count > 0)
        {
            var prox = locked.Take(5).ToList();
            var tab = new Button { Text = (_showLocked ? "▾ " : "▸ ") + $"Próximas missões ({prox.Count}{(locked.Count > prox.Count ? $" de {locked.Count}" : "")})", Flat = true,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, FocusMode = Control.FocusModeEnum.None };
            tab.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.45f));
            tab.Pressed += () => { _showLocked = !_showLocked; _dialogSig = ""; };
            _dialogBox.AddChild(tab);
            if (_showLocked)
                foreach (var q in prox)
                    _dialogBox.AddChild(Wrap($"🔒 {q.Name} — {q.Description}", 12, new Color(1f, 0.55f, 0.5f), 440));
        }
        var close = new Button { Text = "Fechar (Esc)" };
        close.Pressed += () => { m.Dialog = null; _dialog.Visible = false; };
        _dialogBox.AddChild(close);
    }

    private void UpdateJournal(ClientModel m)
    {
        var sig = string.Join(",", m.Quests.Select(q => q.Id + q.State + q.Progress)) + "#" + m.PinsVersion + "#" + HudView.MissoesMinimizadas + "#" + m.ProximaHistoria?.Name + m.ProximaHistoria?.Hint;
        if (sig == _journalSig) return;
        _journalSig = sig;
        Clear(_journalBox, keepTitle: true);
        // 13/11 ("dentro do J, uma aba minimizar missões: mesmo deixando fixado, tira as missões ali do lado")
        var mini = new Button { Text = HudView.MissoesMinimizadas ? "▸ Mostrar as missões fixadas na tela" : "▾ Minimizar as missões fixadas (some o quadro da direita)",
            Flat = true, FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
        mini.AddThemeColorOverride("font_color", Gold);
        mini.Pressed += () => { HudView.MissoesMinimizadas = !HudView.MissoesMinimizadas; _journalSig = ""; };
        _journalBox.AddChild(mini);
        // 17/11: história principal — o próximo passo, sempre no topo do diário
        if (m.ProximaHistoria is { } hist)
        {
            var hc = new PanelContainer();
            hc.AddThemeStyleboxOverride("panel", HudView.CardStyle(true));
            var hv = new VBoxContainer();
            hc.AddChild(hv);
            hv.AddChild(HudView.MakeLabel(hist.Name, 16, new Color(1f, 0.66f, 0.25f)));
            hv.AddChild(Wrap(hist.Hint, 13, new Color(0.95f, 0.88f, 0.72f)));
            _journalBox.AddChild(hc);
        }
        if (m.Quests.Count == 0)
        {
            _journalBox.AddChild(HudView.MakeLabel("Nenhuma missão ativa.", 16, Gold));
            _journalBox.AddChild(Wrap("Fale com a Capitã Ondina, perto do portão norte do Porto, ou procure NPCs com losango amarelo sobre a cabeça. Missões ajudam, não obrigam: dá para só sair e caçar.", 13, Dim));
            return;
        }
        void Group(string title, IEnumerable<QuestEntry> list, Color color)
        {
            // 13/11 ("separador no J para separar as missões por lugar, senão fica tudo bagunçado"): dentro de cada grupo,
            // as missões vêm juntas por lugar (onde está quem deu), com um separador
            var items = list.OrderBy(q => q.Where.Length == 0 ? "~" : q.Where, StringComparer.CurrentCulture).ToList();
            if (items.Count == 0) return;
            _journalBox.AddChild(Section($"{title} ({items.Count})"));
            string? lugar = null;
            foreach (var q in items)
            {
                var aqui = q.Where.Length > 0 ? q.Where : "Outros lugares";
                if (aqui != lugar)
                {
                    lugar = aqui;
                    var sep = new HBoxContainer();
                    sep.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
                    sep.AddChild(HudView.MakeLabel($"  {aqui}  ({items.Count(x => (x.Where.Length > 0 ? x.Where : "Outros lugares") == aqui)})  ", 13, new Color(0.75f, 0.68f, 0.5f)));
                    sep.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
                    _journalBox.AddChild(sep);
                }
                var card = new PanelContainer();
                card.AddThemeStyleboxOverride("panel", HudView.CardStyle(q.State == "ready"));
                var col = new VBoxContainer();
                card.AddChild(col);
                var head = new HBoxContainer();
                var travado = q.State != "done" && q.NivelLugar > (m.Me?.Level ?? 99);   // 13/11: lugar trancado pelo nível = vermelho
                var qTitle = HudView.MakeLabel((travado ? "🔒 " : m.IsPinned(q.Id) ? "▶ " : "◆ ") + q.Name + (travado ? $"  (nível {q.NivelLugar} para entrar)" : ""), 15,
                    travado ? new Color(1f, 0.45f, 0.4f) : color);
                qTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                head.AddChild(qTitle);
                // 12/11: recompensa à vista (XP e moedas)
                if (q.State != "done" && (q.RewardXp > 0 || q.RewardCoins > 0))
                    head.AddChild(HudView.MakeLabel((q.RewardXp > 0 ? $"+{q.RewardXp:N0} XP" : "") + (q.RewardXp > 0 && q.RewardCoins > 0 ? "  ·  " : "") +
                        (q.RewardCoins > 0 ? $"{q.RewardCoins:N0} moedas" : ""), 13, new Color(0.55f, 0.85f, 1f)));
                if (q.State != "done")
                {
                    var pinned = m.IsPinned(q.Id);
                    var pin = new Button { Text = pinned ? "Desmarcar" : "Marcar na tela", TooltipText = "Mostra esta missão no canto da tela (todas vêm marcadas)" };
                    var qid = q.Id;
                    pin.Pressed += () =>
                    {
                        if (!m.UnpinnedQuests.Remove(qid)) m.UnpinnedQuests.Add(qid);
                        m.PinsVersion++;
                        Sfx.Play("ui_clique");
                    };
                    head.AddChild(pin);
                }
                col.AddChild(head);
                if (q.State != "done")
                {
                    var ready = q.State == "ready" || q.Progress >= q.Count;
                    col.AddChild(Wrap(ready ? $"Pronta! Volte a {q.Giver}{(q.Where.Length > 0 ? $" ({q.Where})" : "")}." : $"{q.Hint}  —  {q.Progress}/{q.Count}", 12, ready ? Good : Dim, 400));
                    if (!ready && q.Count > 1)
                        col.AddChild(new ProgressBar { MinValue = 0, MaxValue = q.Count, Value = q.Progress, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 5) });
                }
                _journalBox.AddChild(card);
            }
        }
        Group("Prontas para entregar", m.Quests.Where(q => q.State == "ready" || (q.State == "active" && q.Progress >= q.Count)), Good);
        Group("Em andamento", m.Quests.Where(q => q.State == "active" && q.Progress < q.Count), Gold);
        Group("Concluídas", m.Quests.Where(q => q.State == "done"), Dim);
    }

    // 11/11 ("errei o ponto, tem como voltar?") + 12/11: os pontos ficam marcados aqui e vão para o servidor ao fechar a
    // ficha (sem botão de confirmar); antes disso o − tira. Depois de confirmar, só resetando com a Sibila (Porto), paga.
    private readonly Dictionary<string, int> _pendingStats = new();
    private int PendingTotal => _pendingStats.Values.Sum();
    private int _pendingVersion;

    /// <summary>Manda ao servidor os pontos marcados (ao fechar a ficha).</summary>
    private void CommitStats()
    {
        foreach (var (stat, n) in _pendingStats) for (var i = 0; i < n; i++) Send?.Invoke(new SpendStatIntent(stat));
        _pendingStats.Clear(); _pendingVersion++;
        Sfx.Play("nivel", -6f);
    }

    private void UpdateStats(ClientModel m)
    {
        var s = m.Stats;
        var level = m.Progress?.Level ?? m.Me?.Level ?? 1;
        if (s is not null && PendingTotal > s.Points) _pendingStats.Clear();   // os pontos mudaram por fora (subiu de nível, reset)
        var sig = s is null ? "" : $"{m.ClassStage}|{s.Primary}|{s.Vigor}|{s.Points}|{s.Power:0.0}|{s.MaxHealth:0}|{s.Armor:0}|{s.Forca}|{s.Agilidade}|{s.Energia}|{s.Weight:0.0}|{level}|{_pendingVersion}";
        if (sig == _statsSig) return;
        _statsSig = sig;
        Clear(_statsBox, keepTitle: true);
        if (s is null) return;
        // janela estilo MU: cabeçalho com classe e nível, faixa de pontos, linhas de atributo em placas com o "+"
        _statsBox.AddChild(HudView.MakeLabel($"{m.Me?.Name ?? ""} — {ClientModel.ClassName(m.ClassId)} nível {level}", 15, new Color(0.92f, 0.86f, 0.70f), 1));
        _statsBox.AddChild(HudView.MakeLabel(m.ClassStageText, 14, m.ClassStage == 3 ? new Color(1f, 0.6f, 0.25f) : m.ClassStage == 2 ? Gold : new Color(0.72f, 0.69f, 0.62f), 1));
        var free = s.Points - PendingTotal;
        var pts = Plate(s.Points > 0 ? new Color(0.20f, 0.16f, 0.05f, 0.95f) : new Color(0.03f, 0.03f, 0.035f, 0.9f), s.Points > 0 ? Gold : new Color(0.36f, 0.28f, 0.17f));
        pts.AddChild(HudView.MakeLabel(s.Points > 0 ? $"✦  {free} pontos para distribuir" + (PendingTotal > 0 ? $"  ({PendingTotal} marcados)" : "") : "Sem pontos livres — suba de nível", 15, s.Points > 0 ? Gold : Dim, 1));
        _statsBox.AddChild(pts);
        if (PendingTotal > 0)
            _statsBox.AddChild(Wrap("Marcados em verde: valem ao fechar a ficha (C). Antes disso, o − tira. Depois, só a Sibila (Porto) reseta, e cobra.", 11, Dim));
        if (s.PrimaryKey.Length == 0)
        {
            AddStatRow("⚔", s.PrimaryName, s.Primary, "primary", free > 0, $"+{s.PowerPerPoint:0.#} de poder por ponto", true);
            AddStatRow("♥", "Vitalidade", s.Vigor, "vigor", free > 0, $"+{s.HealthPerPoint:0} de vida e +1,2% de fôlego (estamina) por ponto", false);
        }
        else
        {
            // 14/11: cada classe tem 2 atributos padrão (★ principal, ☆ 2º); o resto é "um pouco de cada"
            var sec = CinzasDaMare.Shared.Content.Atributos.Secundario(m.ClassId);
            string Main(string key) => s.PrimaryKey == key ? $"  ·  ★ PRINCIPAL: +{s.PowerPerPoint:0.#} de poder por ponto" : sec == key ? "  ·  ☆ 2º ATRIBUTO da classe" : "";
            string N(string key, string nome) => s.PrimaryKey == key ? nome + "  ★" : sec == key ? nome + "  ☆" : nome;
            AddStatRow("⚒", N("forca", "Força"), s.Forca, "forca", free > 0, "carrega armadura pesada sem perder esquiva" + Main("forca"), s.PrimaryKey == "forca");
            AddStatRow("➶", N("agilidade", "Destreza"), s.Agilidade, "agilidade", free > 0, "esquiva volta mais rápido" + Main("agilidade"), s.PrimaryKey == "agilidade");
            AddStatRow("♥", N("vigor", "Vitalidade"), s.Vigor, "vigor", free > 0, $"+{s.HealthPerPoint:0} de vida máxima e +1,2% de fôlego (estamina) por ponto" + Main("vigor"), false);
            AddStatRow("✦", N("energia", "Energia"), s.Energia, "energia", free > 0, "+5 de mana/éter/foco máximo por ponto e o recurso regenera mais" + Main("energia"), s.PrimaryKey == "energia");
            _statsBox.AddChild(Wrap($"Para {ClientModel.ClassName(m.ClassId)}: suba {CinzasDaMare.Shared.Content.Atributos.Nome(s.PrimaryKey)} ★ e {CinzasDaMare.Shared.Content.Atributos.Nome(sec)} ☆. Os outros, um pouco de cada.", 12, Gold));
        }
        // valores calculados em duas colunas, como a ficha do MU
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 4);
        void Cell(string name, string value, Color c)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(185, 0) };
            var n = HudView.MakeLabel(name, 13, Dim); n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(n);
            row.AddChild(HudView.MakeLabel(value, 14, c));
            grid.AddChild(row);
        }
        Cell("Poder", $"{s.Power:0.#}", Gold);
        Cell("Vida máxima", $"{s.MaxHealth:0}", new Color(1f, 0.55f, 0.5f));
        Cell("Armadura", $"{s.Armor:0}", new Color(0.75f, 0.82f, 0.95f));
        Cell("Reduz dano", $"{s.ArmorReductionPct:0}%", new Color(0.75f, 0.82f, 0.95f));
        if (s.PrimaryKey.Length > 0)
        {
            var heavy = s.OverloadPct > 0.5;
            Cell("Peso / Força", $"{s.Weight:0.#} / {s.Forca}", heavy ? new Color(1f, 0.45f, 0.35f) : Good);
            Cell("Sobrecarga", heavy ? $"{s.OverloadPct:0}%" : "leve", heavy ? new Color(1f, 0.45f, 0.35f) : Good);
        }
        var gp = Plate(new Color(0.03f, 0.03f, 0.035f, 0.9f), new Color(0.36f, 0.28f, 0.17f));
        gp.AddChild(grid);
        _statsBox.AddChild(gp);
        _statsBox.AddChild(Wrap("Cada nível dá +3 pontos. Armas pedem " + s.PrimaryName + "; armadura pesada pede Força (sem ela: esquiva lenta e passo pesado).", 11, Dim));
    }

    /// <summary>Placa escura com borda de bronze (mesmo estilo dos espaços da barra).</summary>
    private static PanelContainer Plate(Color bg, Color border)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = bg, BorderColor = border, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        return p;
    }

    private void AddStatRow(string glyph, string name, int value, string stat, bool canSpend, string effect, bool primary)
    {
        var plate = Plate(primary ? new Color(0.09f, 0.06f, 0.03f, 0.95f) : new Color(0.03f, 0.03f, 0.035f, 0.9f), primary ? Gold : new Color(0.36f, 0.28f, 0.17f));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 1);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(HudView.MakeLabel(glyph, 18, primary ? Gold : new Color(0.8f, 0.72f, 0.55f)));
        var label = HudView.MakeLabel(name, 16, primary ? Gold : new Color(0.92f, 0.88f, 0.8f));
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        var pend = _pendingStats.GetValueOrDefault(stat);
        row.AddChild(HudView.MakeLabel(pend > 0 ? $"{value} +{pend}" : value.ToString(), 20, pend > 0 ? new Color(0.5f, 1f, 0.5f) : Colors.White));
        if (pend > 0)
        {
            var minus = new Button { Text = "−", CustomMinimumSize = new Vector2(30, 28), TooltipText = $"Tirar 1 ponto marcado de {name}" };
            minus.Pressed += () => { if (--_pendingStats[stat] <= 0) _pendingStats.Remove(stat); _pendingVersion++; };
            row.AddChild(minus);
        }
        var plus = new Button { Text = "+", Disabled = !canSpend, CustomMinimumSize = new Vector2(30, 28), TooltipText = $"Pôr 1 ponto em {name}" };
        plus.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0.22f, 0.16f, 0.06f), BorderColor = Gold, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1 });
        plus.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(0.40f, 0.28f, 0.08f), BorderColor = Gold, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1 });
        plus.AddThemeStyleboxOverride("disabled", new StyleBoxFlat { BgColor = new Color(0.06f, 0.05f, 0.05f), BorderColor = new Color(0.25f, 0.2f, 0.14f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1 });
        plus.AddThemeColorOverride("font_color", Gold);
        plus.Pressed += () => { _pendingStats[stat] = _pendingStats.GetValueOrDefault(stat) + 1; _pendingVersion++; Sfx.Play("ui_clique"); };
        row.AddChild(plus);
        col.AddChild(row);
        col.AddChild(Wrap(effect, 11, Dim));
        plate.AddChild(col);
        _statsBox.AddChild(plate);
    }

    private void UpdateMap(ClientModel m)
    {
        var nv = m.Snapshot?.Players.FirstOrDefault(p => p.CharacterId == m.CharacterId)?.Level ?? 0;
        if (nv > 0 && nv != _proximosNivel)
        {
            _proximosNivel = nv;
            Clear(_proximos);
            foreach (var (l, txt) in CinzasDaMare.Shared.World.Desbloqueios.Proximos(nv))
                _proximos.AddChild(Wrap($"Nível {l}: {txt}", 12, l - nv <= 2 ? Gold : Dim, 260));
            if (_proximos.GetChildCount() == 0) _proximos.AddChild(Wrap("Tudo liberado. Agora é farmar o melhor equipamento.", 12, Dim, 260));
        }
        var w = m.WorldMap;
        var sig = w is null ? "" : $"{w.CurrentMap}|{string.Join(",", w.Regions.Select(r => r.Id + r.State + string.Join("/", r.Quests ?? Array.Empty<string>())))}|{w.Waypoints.Count}|{m.CountInInventory("portal_viagem")}";
        if (sig == _mapSig) return;
        _mapSig = sig;
        _canvas.Data = w;
        _canvas.QueueRedraw();
        Clear(_waypoints);
        if (w is null) return;
        _waypoints.AddChild(HudView.MakeLabel($"Você está em: {w.CurrentMapName}", 14, Gold));
        if (w.Waypoints.Count == 0) _waypoints.AddChild(Wrap("Nenhum ponto de viagem descoberto. Chegue perto de uma fogueira de acampamento.", 12, Dim, 270));
        else _waypoints.AddChild(Wrap(m.CountInInventory("portal_viagem") > 0 && (m.Me?.Level ?? 0) >= 7
            ? "Pedra de Portal: viaje daqui mesmo (fora de combate e de dungeon)." : "Para viajar, fique perto de uma pedra de viagem — ou compre a Pedra de Portal do Téo (nível 7).", 12, Dim, 270));
        foreach (var wp in w.Waypoints)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(270, 0) };
            var label = HudView.MakeLabel($"{wp.Name}\n{wp.MapName}", 12);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            var go = new Button { Text = "Viajar" };
            var id = wp.Id;
            go.Pressed += () => Send?.Invoke(new TravelIntent(id));
            row.AddChild(go);
            _waypoints.AddChild(row);
        }
        var withQuests = w.Regions.Where(r => r.Quests is { Count: > 0 }).ToList();
        if (withQuests.Count > 0)
        {
            _waypoints.AddChild(new HSeparator());
            _waypoints.AddChild(HudView.MakeLabel("Missões (!)", 14, Gold));
            foreach (var r in withQuests)
                _waypoints.AddChild(Wrap($"{r.Name}: {string.Join("; ", r.Quests!)}", 12, null, 270));
        }
    }

    /// <summary>
    /// Livro de Poderes (tecla K): todos os poderes da classe, quais já foram aprendidos e onde comprar/ganhar o tomo dos
    /// outros. Poder aprendido vai para qualquer tecla (Q, E, R, T) — a arma muda a força, não o conjunto de golpes.
    /// </summary>
    private void UpdatePowers(ClientModel m)
    {
        var level = m.Progress?.Level ?? m.Me?.Level ?? 1;
        var skills = m.Catalog?.Skills.Where(s => s.Slot is not ("basic" or "basic_unarmed")).GroupBy(s => s.Id).Select(g => g.First()).ToList()
                     ?? new List<SkillInfo>();
        var keys = new[] { "Q", "E", "R", "T", "Dir" };   // "Dir" = botão direito do mouse (10/11)
        var sig = $"{level}|{string.Join(",", m.Flags)}|{string.Join(",", keys.Select(k => m.SkillForKey(k)?.Id))}|{skills.Count}";
        if (sig == _powersSig) return;
        _powersSig = sig;
        Clear(_powersBox, keepTitle: true);
        _powersBox.AddChild(Wrap("Você começa só com o ataque básico. Cada poder vem de um tomo: o primeiro é a prova do seu mentor no Porto; " +
                                 "os outros ele vende (caros) ou caem de chefes. Leia o tomo na mochila e escolha a tecla aqui.", 12, Dim));
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 10);
        foreach (var k in keys)
            bar.AddChild(HudView.MakeLabel($"[{k}] {m.SkillForKey(k)?.DisplayName ?? "vazio"}", 13, Gold));
        _powersBox.AddChild(bar);
        _powersBox.AddChild(new HSeparator());
        // 13/11 ("no K só tem que ter poderes desbloqueados, nada a mais — começando do zero, não vai ter nada")
        var conhecidos = skills.Where(s => m.Knows(s, level)).ToList();
        if (conhecidos.Count == 0)
            _powersBox.AddChild(Wrap("Nenhum poder aprendido ainda.", 14, Dim));
        foreach (var s in conhecidos.OrderBy(s => s.Slot == "weapon" ? 2 : s.Slot == "skill3" ? 1 : 0).ThenBy(s => s.TomeLevel))
        {
            var known = true;
            var card = new PanelContainer();
            card.AddThemeStyleboxOverride("panel", HudView.CardStyle());
            var col = new VBoxContainer();
            card.AddChild(col);
            var head = new HBoxContainer();
            var name = HudView.MakeLabel(s.DisplayName, 16, known ? Gold : Dim);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            head.AddChild(name);
            var where = s.Slot switch
            {
                "weapon" => "vem da arma",
                "skill3" => known ? "evolução" : "evolução (nível 20 + mentor)",
                _ => known ? "aprendido" : s.TomeLevel > 0 ? $"tomo · nível {s.TomeLevel} · mentor" : $"nível {s.UnlockLevel}",
            };
            head.AddChild(HudView.MakeLabel(where, 12, known ? new Color(0.5f, 0.9f, 0.5f) : Dim));
            col.AddChild(head);
            col.AddChild(Wrap($"{s.Description}  Custo {s.ResourceCost:0} · recarga {s.CooldownMs / 1000.0:0.#} s", 12, new Color(0.85f, 0.82f, 0.76f), 500));
            if (known && s.Slot != "weapon")
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);
                row.AddChild(HudView.MakeLabel("Pôr na tecla:", 12, Dim));
                foreach (var k in keys)
                {
                    var b = new Button { Text = k, ToggleMode = true, ButtonPressed = m.SkillForKey(k)?.Id == s.Id, CustomMinimumSize = new Vector2(34, 0) };
                    var key = k;
                    var id = s.Id;
                    b.Pressed += () => BindRequested?.Invoke(key, b.ButtonPressed ? id : "");
                    row.AddChild(b);
                }
                col.AddChild(row);
            }
            _powersBox.AddChild(card);
        }
    }

    private void UpdateParty(ClientModel m)
    {
        var others = m.Snapshot?.Players.Where(p => p.CharacterId != m.CharacterId).Select(p => p.Name).OrderBy(n => n).ToList() ?? new List<string>();
        var sig = $"{m.Flags.Contains("casa:propria")}|{m.Party?.PartyId}|{string.Join(",", m.Party?.Members.Select(x => x.Name + x.Online) ?? Array.Empty<string>())}|{string.Join(",", others)}";
        if (sig == _partySig) return;
        _partySig = sig;
        Clear(_partyBox, keepTitle: true);
        _partyBox.AddChild(Wrap("Até 5. Quem estiver perto (30 m, mesmo mapa) divide a XP do abate com bônus de grupo; o loot fica 45 s reservado a quem lutou; missões de abate contam para todos.", 12, Dim));
        if (m.Party is { } party)
        {
            _partyBox.AddChild(HudView.MakeLabel("Membros", 15, Gold));
            var leader = party.Members.FirstOrDefault(x => x.Leader)?.CharacterId;
            foreach (var mem in party.Members)
            {
                var row = new HBoxContainer();
                var label = HudView.MakeLabel($"{(mem.Leader ? "★ " : "")}{mem.Name} · nv {mem.Level}{(mem.Online ? $" · {mem.MapName}" : " · fora do jogo")}", 14,
                    mem.Online ? null : Dim);
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(label);
                if (mem.CharacterId != m.CharacterId && mem.Online)
                {
                    // 12/11 ("desafiar o amigo para um x1 dentro do P"): os dois vão para a Arena e lutam até 10% de vida
                    var x1 = new Button { Text = "Desafiar x1" };
                    var alvo = mem.Name;
                    x1.Pressed += () => Send?.Invoke(new PartyIntent("duelo", alvo));
                    row.AddChild(x1);
                }
                if (leader == m.CharacterId && mem.CharacterId != m.CharacterId)
                {
                    var kick = new Button { Text = "Remover" };
                    var id = mem.CharacterId;
                    kick.Pressed += () => Send?.Invoke(new PartyIntent("kick", id));
                    row.AddChild(kick);
                }
                _partyBox.AddChild(row);
            }
            // 12/11 ("convidar pelo P para a casa"): quem tem casa chama o grupo (10 min para entrar pela porta do Porto)
            if (m.Flags.Contains("casa:propria"))
            {
                var casa = new Button { Text = "Convidar o grupo para a minha casa", TooltipText = "Por 10 minutos, quem está no grupo entra pela porta da sua casa no Porto e cai na SUA casa." };
                casa.Pressed += () => Send?.Invoke(new PartyIntent("casa"));
                _partyBox.AddChild(casa);
            }
            var leave = new Button { Text = "Sair do grupo" };
            leave.Pressed += () => Send?.Invoke(new PartyIntent("leave"));
            _partyBox.AddChild(leave);
        }
        _partyBox.AddChild(new HSeparator());
        _partyBox.AddChild(HudView.MakeLabel("Convidar", 15, Gold));
        var inviteRow = new HBoxContainer();
        if (_inviteName.GetParent() is Node old) old.RemoveChild(_inviteName);
        inviteRow.AddChild(_inviteName);
        var send = new Button { Text = "Convidar" };
        send.Pressed += () => { if (_inviteName.Text.Trim().Length > 0) Send?.Invoke(new PartyIntent("invite", _inviteName.Text.Trim())); };
        inviteRow.AddChild(send);
        _partyBox.AddChild(inviteRow);
        foreach (var name in others.Where(n => m.Party?.Members.All(x => x.Name != n) ?? true))
        {
            var row = new HBoxContainer();
            var label = HudView.MakeLabel(name + " (perto)", 13);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            // 14/11 22h (dono: "no P, menor: um botão x1 e um botão grupo"; não precisa estar em grupo para o x1)
            var b = new Button { Text = "grupo", TooltipText = "Convidar para o grupo", CustomMinimumSize = new Vector2(52, 22) };
            b.AddThemeFontSizeOverride("font_size", 12);
            var n = name;
            b.Pressed += () => Send?.Invoke(new PartyIntent("invite", n));
            row.AddChild(b);
            var x1 = new Button { Text = "x1", TooltipText = "Desafiar para um x1 (aceita com Y)", CustomMinimumSize = new Vector2(34, 22) };
            x1.AddThemeFontSizeOverride("font_size", 12);
            x1.Pressed += () => Send?.Invoke(new PartyIntent("duelo", n));
            row.AddChild(x1);
            _partyBox.AddChild(row);
        }
    }

    private void UpdateFrames(ClientModel m)
    {
        var party = m.Party;
        var sig = party is null ? "" : string.Join(",", party.Members.Select(x => $"{x.Name}{x.Online}{(int)x.Health}/{(int)x.MaxHealth}{x.MapName}"));
        if (sig == _framesSig) return;
        _framesSig = sig;
        Clear(_frames);
        if (party is null || true) return;   // 14/11 19h: a lista do grupo agora é uma só (HudView, embaixo do minimapa)
        foreach (var mem in party.Members.Where(x => x.CharacterId != m.CharacterId))
        {
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(200, 0) };
            box.AddChild(HudView.MakeLabel($"{mem.Name} · nv {mem.Level}{(mem.Online ? "" : " (fora)")}", 12, mem.Online ? new Color(0.55f, 0.75f, 1f) : Dim, 3));
            var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(200, 7),
                Value = mem.MaxHealth > 0 ? mem.Health / mem.MaxHealth : 0, MouseFilter = Control.MouseFilterEnum.Ignore };
            var fill = new StyleBoxFlat { BgColor = new Color(0.3f, 0.75f, 0.35f) };
            var bg = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.06f, 0.85f) };
            bar.AddThemeStyleboxOverride("fill", fill);
            bar.AddThemeStyleboxOverride("background", bg);
            box.AddChild(bar);
            if (mem.Online && mem.MapName.Length > 0 && mem.MapName != m.WorldMap?.CurrentMapName)
                box.AddChild(HudView.MakeLabel("   em " + mem.MapName, 11, Dim, 2));
            _frames.AddChild(box);
        }
    }

    // ------------------------------------------------------------ utilitários

    private static void Clear(Node box, bool keepTitle = false)
    {
        var children = box.GetChildren();
        for (var i = keepTitle ? 1 : 0; i < children.Count; i++) children[i].QueueFree();
    }

    // ------------------------------------------------------------ ofício (forja, bancada, caldeirão, panela)

    private void RefreshCraftIcons()
    {
        for (var i = _craftIcons.Count - 1; i >= 0; i--)
        {
            var (rect, info, def) = _craftIcons[i];
            if (!GodotObject.IsInstanceValid(rect)) { _craftIcons.RemoveAt(i); continue; }
            if (ItemIconRenderer.Get(info, def) is { } tex) { rect.Texture = tex; _craftIcons.RemoveAt(i); }
        }
    }

    private void UpdateCraft(ClientModel m)
    {
        var st = m.Station!;
        var sig = $"{st.StationId}|{_craftTab}|{string.Join(",", st.Recipes.Select(r => r.Id + r.CanCraft + string.Join("/", r.Inputs.Select(i => i.Have))))}";
        if (sig == _craftSig) return;
        _craftSig = sig;
        Clear(_craftBox);
        _craftIcons.Clear();
        _craftBox.AddChild(HudView.MakeLabel(st.Name, 22, Gold));
        _craftBox.AddChild(Wrap(st.Kind switch
        {
            "forja" => "Metal: rebites, placas, ligas. O resultado é exato — sem sorte.",
            "bancada" => "Couro, seda e peças: selas, ataduras, insígnias.",
            "caldeirao" => "Poções e preparos. Frascos vendem na Ilma e com mascates.",
            _ => "Refeições: um efeito ativo por vez (a nova substitui a anterior).",
        }, 12, Dim, 520));
        // 10/11 ("bancada bonita, com imagem do que vai sair; passar o mouse mostra as informações"): grade de cartões —
        // ícone grande do resultado, nome, ingredientes como mini-ícones com tem/precisa; clique faz (se der)
        // 13/11 ("na máquina do caos coloca categorias e abas, tipo menu R1 R2: Asas, Armas e o que tiver"): abas pela
        // categoria do que sai; "Tudo" mostra todas. Só aparecem as abas que têm receita.
        if (_craftStation != st.StationId) { _craftStation = st.StationId; _craftTab = "Tudo"; }
        var cats = st.Recipes.Select(r => CraftCategoria(m.ItemInfoFor(r.Output), r.Output)).Distinct().ToList();
        if (cats.Count > 1)
        {
            var abas = new HBoxContainer();
            abas.AddThemeConstantOverride("separation", 4);
            foreach (var c in new[] { "Tudo" }.Concat(CraftOrdem.Where(cats.Contains)))
            {
                var n = c == "Tudo" ? st.Recipes.Count : st.Recipes.Count(r => CraftCategoria(m.ItemInfoFor(r.Output), r.Output) == c);
                var b = new Button { Text = $"{c} ({n})", ToggleMode = true, ButtonPressed = _craftTab == c, FocusMode = Control.FocusModeEnum.None };
                var cc = c;
                b.Pressed += () => { _craftTab = cc; _craftSig = ""; Sfx.Play("ui_clique", -12f); };
                abas.AddChild(b);
            }
            _craftBox.AddChild(abas);
        }
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        foreach (var r in st.Recipes.Where(r => _craftTab == "Tudo" || CraftCategoria(m.ItemInfoFor(r.Output), r.Output) == _craftTab))
        {
            // Flat = false: com Flat o Godot não desenha o quadro de "hover" (só o zoom aparecia)
            var card = new Button { CustomMinimumSize = new Vector2(190, 228), FocusMode = Control.FocusModeEnum.None };
            card.AddThemeStyleboxOverride("normal", HudView.CardStyle(r.CanCraft));
            // 13/11 ("ao passar o mouse nada acontece, só aparece uma caixa de texto"): quadro dourado em volta e o cartão cresce um pouco
            var destaque = new StyleBoxFlat { BgColor = new Color(0.22f, 0.17f, 0.09f, 0.95f), BorderColor = new Color(1f, 0.82f, 0.35f) };
            destaque.SetBorderWidthAll(3); destaque.SetCornerRadiusAll(8);
            card.AddThemeStyleboxOverride("hover", destaque);
            card.AddThemeStyleboxOverride("pressed", HudView.CardStyle(true));
            var outInfo = m.ItemInfoFor(r.Output);
            var outDef = r.Output;
            var rr = r;
            var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            col.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            col.OffsetLeft = 8; col.OffsetRight = -8; col.OffsetTop = 6; col.OffsetBottom = -6;
            col.AddThemeConstantOverride("separation", 4);
            card.AddChild(col);
            var icon = new TextureRect
            {
                Texture = ItemIconRenderer.Get(outInfo, outDef), CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = r.Known ? (r.CanCraft || r.Note.Length == 0 ? Colors.White : new Color(0.7f, 0.7f, 0.7f)) : new Color(0.04f, 0.04f, 0.05f, 0.9f),   // 14/11: a descobrir = silhueta
            };
            col.AddChild(icon);
            if (icon.Texture is null) _craftIcons.Add((icon, outInfo, outDef));
            var name = HudView.MakeLabel(r.OutputName + (r.OutputQty > 1 ? $" ×{r.OutputQty}" : ""), 14, r.Known ? Gold : Dim, 2);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            col.AddChild(name);
            var ing = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            ing.AddThemeConstantOverride("separation", 4);
            foreach (var i in r.Inputs)
            {
                var cell = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                var inInfo = m.ItemInfoFor(i.Item);
                var mini = new TextureRect
                {
                    Texture = ItemIconRenderer.Get(inInfo, i.Item), CustomMinimumSize = new Vector2(30, 30),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                if (mini.Texture is null) _craftIcons.Add((mini, inInfo, i.Item));
                cell.AddChild(mini);
                var have = HudView.MakeLabel($"{i.Have}/{i.Need}", 11, i.Have >= i.Need ? Good : new Color(0.95f, 0.5f, 0.42f), 2);
                have.HorizontalAlignment = HorizontalAlignment.Center;
                have.MouseFilter = Control.MouseFilterEnum.Ignore;
                cell.AddChild(have);
                // 13/11 ("na máquina do caos não consigo ver o nome dos itens que pede"): nome curto embaixo do ícone
                var nomeIng = HudView.MakeLabel(i.Name.Length > 14 ? i.Name[..13] + "…" : i.Name, 9, Dim, 1);
                nomeIng.HorizontalAlignment = HorizontalAlignment.Center;
                nomeIng.MouseFilter = Control.MouseFilterEnum.Ignore;
                nomeIng.CustomMinimumSize = new Vector2(42, 0); nomeIng.ClipText = true;
                cell.AddChild(nomeIng);
                ing.AddChild(cell);
            }
            col.AddChild(ing);
            if (r.Known && r.Note.Contains("Chance"))
            {
                var chance = HudView.MakeLabel(r.Note[r.Note.IndexOf("Chance", StringComparison.Ordinal)..].Split(" —")[0] + " · falhou = perde", 11, new Color(1f, 0.7f, 0.35f), 2);
                chance.HorizontalAlignment = HorizontalAlignment.Center;
                chance.MouseFilter = Control.MouseFilterEnum.Ignore;
                col.AddChild(chance);
            }
            // 14/11 ("tenho os materiais, tentei criar a sela e não consegui"): diz o PORQUÊ — receita a aprender (com quem),
            // nível, outra bancada... e só "faltam ingredientes" quando é isso mesmo
            var motivo = !r.Known || (!r.CanCraft && r.Note.Length > 0 && !r.Note.StartsWith("Chance", StringComparison.Ordinal)) ? r.Note.Split(" · ")[0] : "";
            var state = HudView.MakeLabel(motivo.Length > 0 ? motivo : r.CanCraft ? "clique para fazer" : "faltam ingredientes", 11,
                !r.Known ? new Color(0.75f, 0.7f, 0.55f) : r.CanCraft ? Good : new Color(0.9f, 0.6f, 0.5f), 2);
            state.HorizontalAlignment = HorizontalAlignment.Center;
            state.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            state.MouseFilter = Control.MouseFilterEnum.Ignore;
            col.AddChild(state);
            var station = st.StationId;
            var kind = st.Kind;
            card.Pressed += () =>
            {
                if (!rr.Known || !rr.CanCraft) { Sfx.Play("ui_erro"); return; }
                LastStationKind = kind;
                Send?.Invoke(new CraftIntent(Guid.NewGuid().ToString("N"), station, rr.Id));
            };
            card.PivotOffset = new Vector2(95, 114);
            card.MouseEntered += () => { ShowCraftTip(card, outInfo, outDef, rr); card.Scale = new Vector2(1.04f, 1.04f); Sfx.Play("ui_clique", -18f); };
            card.MouseExited += () => { HideCraftTip(); card.Scale = Vector2.One; };
            grid.AddChild(card);
        }
        _craftBox.AddChild(grid);
        var close = new Button { Text = "Fechar (Esc)" };
        close.Pressed += () => { m.Station = null; _craft.Visible = false; };
        _craftBox.AddChild(close);
    }

    private Control? _craftTip;
    private string _craftTab = "Tudo", _craftStation = "";
    private static readonly string[] CraftOrdem = { "Asas", "Armas", "Armaduras", "Joias", "Montarias e pets", "Poções e comida", "Materiais", "Outros" };

    private static string CraftCategoria(ItemInfo? info, string def)
    {
        var cat = info?.Category ?? "";
        if (def.StartsWith("wings_") || cat == "wings") return "Asas";
        if (cat == "weapon" || cat == "offhand") return "Armas";
        if (cat is "armor" or "helmet" or "gloves" or "boots" or "pants" or "bracelet") return "Armaduras";
        if (cat is "ring" or "necklace" or "amulet" || def.StartsWith("anel_") || def.StartsWith("colar_") || def.Contains("joia")) return "Joias";
        if (cat is "pet" or "mount" || def.StartsWith("pet_") || def.StartsWith("montaria_") || def.StartsWith("apito_")) return "Montarias e pets";
        if (def.StartsWith("consumable_") || def.StartsWith("food_") || cat == "consumable") return "Poções e comida";
        if (def.StartsWith("mat_") || cat == "material") return "Materiais";
        return "Outros";
    }

    /// <summary>Dica do cartão da receita: a mesma ficha do item (atributos, comparação) + ingredientes e nota.</summary>
    private void ShowCraftTip(Control card, ItemInfo? info, string def, RecipeView r)
    {
        HideCraftTip();
        var tip = r.Known ? ItemTooltip.Build(info, def, "comum", r.OutputQty) : ItemTooltip.Build(null, "Receita a descobrir", "comum", 1);   // 14/11: a descobrir não revela o item
        if (tip.GetChild(0) is VBoxContainer col)
        {
            // 13/11: o que precisa, com o nome de cada ingrediente (tem/precisa)
            col.AddChild(new HSeparator());
            col.AddChild(HudView.MakeLabel("Precisa:", 13, Gold));
            foreach (var i in r.Inputs)
                col.AddChild(HudView.MakeLabel($"  {(i.Have >= i.Need ? "✔" : "✘")} {i.Name}  {i.Have}/{i.Need}", 13, i.Have >= i.Need ? Good : new Color(0.95f, 0.5f, 0.42f)));
            if (r.Note.Length > 0) col.AddChild(Wrap(r.Note, 11, Dim, 280));
        }
        tip.TopLevel = true;
        tip.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(tip);
        var at = card.GetGlobalRect();
        tip.Position = new Vector2(at.End.X + 8, at.Position.Y);
        var view = card.GetViewportRect().Size;
        if (tip.Position.X + 300 > view.X) tip.Position = new Vector2(at.Position.X - 300, at.Position.Y);
        _craftTip = tip;
    }

    private void HideCraftTip()
    {
        if (_craftTip is not null && GodotObject.IsInstanceValid(_craftTip)) _craftTip.QueueFree();
        _craftTip = null;
    }

    // ------------------------------------------------------------ armazém comunitário / baú da casa

    private void UpdateStorage(ClientModel m)
    {
        var st = m.Storage!;
        // posição: à esquerda da mochila (que abre encostada à direita), centrado na altura
        var view = _storage.GetViewportRect().Size;
        var k = Mathf.Min(1f, (view.Y - 16f) / Mathf.Max(1f, _storage.Size.Y));
        _storage.Scale = new Vector2(k, k);
        // 12/11 ("olha como fica quando abro baú e inventário"): encosta na borda esquerda REAL da mochila (ela é larga)
        var invLeft = InventarioEsquerda?.Invoke() ?? view.X - 460f * k - 12f;
        _storage.Position = (new Vector2(Mathf.Max(8f, invLeft - _storage.Size.X * k - 12f), Mathf.Max(8f, (view.Y - _storage.Size.Y * k) / 2f)) + Arrastador.Desvio("bau")).Floor();
        var sig = $"{st.Container}|{string.Join(",", st.Items.Select(i => i.ItemId + i.Quantity))}|{m.InventoryColumns}";
        if (sig == _storageSig) return;
        _storageSig = sig;
        if (_storageModel != m)
        {
            _storageModel = m;
            _storageGrid.Info = def => m.ItemInfoFor(def);
            _storageGrid.Soltou += (id, from, slot) =>
            {
                if (from == "bag" && m.Storage is { } box) Send?.Invoke(new StorageIntent(Guid.NewGuid().ToString("N"), "deposit", box.Container, id));
            };
            _storageGrid.Clicou += (id, dbl, right) =>
            {
                if ((dbl || right) && m.Storage is { } box) Send?.Invoke(new StorageIntent(Guid.NewGuid().ToString("N"), "withdraw", box.Container, id));
            };
            _storageClose.Pressed += () => { m.Storage = null; _storage.Visible = false; };
        }
        var grid = m.InventoryColumns > 0;
        if (_storage is JanelaV2 jw) jw.Titulo = st.Container == "casa" ? "BAÚ" : "ARMAZÉM";
        _storage.QueueRedraw();
        _storageGrid.Tipo = "store:" + st.Container;
        _storageGrid.Colunas = 8;
        _storageGrid.Linhas = grid ? Math.Max(1, st.Capacity / 8) : Math.Max(2, (st.Capacity + 7) / 8);
        _storageGrid.Casa = _storageGrid.Linhas > 10 ? 34f : 40f;
        _storageGrid.Ajustar();
        _storageGrid.Itens.Clear();
        var used = 0;
        var sized = st.Items.Select(i =>
        {
            var info = m.ItemInfoFor(i.DefinitionId);
            var (w, h) = grid ? (Math.Max(1, info?.GridW ?? 1), Math.Max(1, info?.GridH ?? 1)) : (1, 1);
            used += w * h;
            return (i.ItemId, i.DefinitionId, i.Quality, i.Quantity, w, h);
        }).ToList();
        _storageGrid.Itens.AddRange(GradeItens.Empacotar(sized, _storageGrid.Colunas, _storageGrid.Linhas));
        _storageGrid.QueueRedraw();
        _storageInfo.Text = $"{st.Name} · {used}/{st.Capacity} casas\nArraste da mochila para guardar · clique direito para tirar";
    }

    private static Control StorageRow(string name, int qty, string action, Action onPress, bool enabled)
    {
        var row = new HBoxContainer();
        var label = HudView.MakeLabel(qty > 1 ? $"{name} x{qty}" : name, 13);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        var b = new Button { Text = action, Disabled = !enabled };
        b.Pressed += onPress;
        row.AddChild(b);
        return row;
    }

    /// <summary>Título de seção com linha de bronze (como no pacote visual).</summary>
    private static Control Section(string title)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        box.AddChild(HudView.MakeLabel("◇ " + title, 17, Gold));
        box.AddChild(new HSeparator());
        return box;
    }

    private static Label Wrap(string text, int size, Color? color = null, float width = 380)
    {
        var label = HudView.MakeLabel(text, size, color, 2);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }
}

/// <summary>Desenho do mapa do mundo: regiões nas posições do world.json, ligações, estado de cada uma e "você está aqui".</summary>
public sealed partial class WorldMapCanvas : Control
{
    /// <summary>13/11: nível de quem olha (passagem trancada aparece em vermelho).</summary>
    public int MyLevel { get; set; } = 99;
    public IReadOnlySet<string> Flags { get; set; } = new HashSet<string>();
    public WorldMapMessage? Data { get; set; }
    private string? _hover;
    /// <summary>13/11 ("no mapa geral consigo dar TP só clicando nas bolinhas, nas que já fui"): clique numa região visitada
    /// com pedra de viagem descoberta → viaja para a primeira pedra dela.</summary>
    public event Action<string>? ViajarPara;
    private readonly List<(Vector2 P, string Id)> _wpTela = new();   // 17/11: losangos das pedras desenhados (duplo clique)
    /// <summary>Mapa da área: ilustração Art/mapa_&lt;id do mapa&gt;.png (cobre −130..130 em x e y, norte = y negativo no topo).</summary>
    public bool AreaMode { get; set; } = true;
    public CinzasDaMare.Shared.World.MapLayout? Layout { get; set; }
    public Vector2 Me { get; set; }
    /// <summary>Marcadores de missão do servidor (horda caçando, carroça).</summary>
    public System.Collections.Generic.IReadOnlyDictionary<string, (double X, double Y, string Kind)>? Markers { get; set; }
    public System.Collections.Generic.IReadOnlyDictionary<string, string>? NpcMarks { get; set; }
    public Func<CinzasDaMare.Shared.World.MapNpc, bool>? NpcShown { get; set; }

    /// <summary>Para onde o personagem olha (seta do "você está aqui").</summary>
    public Vector2 MeFacing { get; set; } = new(0, -1);

    private bool DrawArea()
    {
        if (!AreaMode || Layout is null) return false;
        // 12/11 ("na praça não tem mapa; coloca que todos têm mapa local"): sem ilustração, o mapa é desenhado do próprio
        // layout (zonas, estradas, muros, prédios, NPCs, portais) — todo lugar tem mapa local
        var tex = VisualAssets.Load("mapa_" + Layout.Id + ".png");
        // 13/11: a arte da Costa foi feita cobrindo −130..130; as novas (Porto e os outros) cobrem a borda do mapa + 2 m
        var ext = tex is not null ? (Layout.Id == "costa_do_cal" ? 130.0 : Layout.HalfSize + 2) : Layout.HalfSize + 4;
        // 11/11 noite: rodinha dá zoom (no ponto do mouse), arrastar move; abre centralizado
        var side = Mathf.Min(Size.X, Size.Y) * _zoom;
        var origin = (Size - new Vector2(side, side)) / 2f + _pan;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.08f, 0.07f, 0.06f));
        Vector2 At(double x, double y) => origin + new Vector2((float)((x + ext) / (2 * ext) * side), (float)((y + ext) / (2 * ext) * side));
        var k = side / (float)(2 * ext);   // pixels por metro
        if (tex is not null) DrawTextureRect(tex, new Rect2(origin, new Vector2(side, side)), false);
        else DrawGenerated(At, k, origin, side);
        // 16/11 (pendência u79_mapa_costa): névoa no que você ainda não andou (mapas de campo com ilustração)
        if (tex is not null && Layout.Kind == "campo" && VisualAssets.Load("mapa_nevoa_textura.png") is { } nevoa)
        {
            var c = Explorado.Cel;
            var n0 = Mathf.FloorToInt((float)-ext / c); var n1 = Mathf.CeilToInt((float)ext / c);
            var tw = nevoa.GetWidth(); var th = nevoa.GetHeight();
            for (var cx = n0; cx < n1; cx++)
                for (var cy = n0; cy < n1; cy++)
                {
                    if (Explorado.Visto(Layout.Id, cx, cy)) continue;
                    var a = At(cx * c, cy * c); var b = At((cx + 1) * c, (cy + 1) * c);
                    // a textura é "esticada" pelo mundo (cada célula pega o seu pedaço): as nuvens continuam de uma célula na outra
                    var src = new Rect2(Mathf.PosMod(cx * 37f, tw - 64), Mathf.PosMod(cy * 37f, th - 64), 64, 64);
                    var borda = 0.18f * (b.X - a.X);
                    DrawTextureRectRegion(nevoa, new Rect2(a - new Vector2(borda, borda), b - a + new Vector2(borda * 2, borda * 2)), src, new Color(1, 1, 1, 0.93f));
                }
        }
        var font = HudView.TitleFont ?? ThemeDB.FallbackFont;
        _alvos.Clear();
        foreach (var portal in Layout.Portals)
        {
            if (Visuals.EscadaCasa.PortalDeEscada(portal) || portal.Oculto) continue;   // 13/11: a escada é mecânica, não aparece no mapa; 17/11: passagem secreta também não
            var p = At(portal.X, portal.Y);
            // 13/11 ("passagens com trava são vermelhas"): trancada pelo nível = vermelha, com o nível ao lado
            // 14/11 23h (dono: "os TPs bloqueados, com o portal vermelho, em vermelho no minimapa e no mapa"): mesma regra do portal 3D
            var trancada = (portal.LevelLock && portal.MinLevel > MyLevel) || !CinzasDaMare.Shared.World.Marcas.Tem(Flags, portal.RequiresFlag);
            DrawCircle(p, 7, new Color(0.15f, 0.08f, 0.25f, 0.9f));
            DrawCircle(p, 5, trancada ? new Color(1f, 0.25f, 0.2f) : new Color(0.72f, 0.45f, 1f));
            DrawString(font, p + new Vector2(9, 5), portal.Label + (trancada ? (portal.MinLevel > MyLevel ? $"  (trancado · nível {portal.MinLevel})" : "  (trancado)") : ""), HorizontalAlignment.Left, -1, 14,
                trancada ? new Color(1f, 0.55f, 0.5f) : new Color(1f, 0.95f, 0.85f));
            _alvos.Add((p, 9f, new[] { "Passagem: " + portal.Label,
                trancada ? (portal.MinLevel > MyLevel ? $"trancada até o nível {portal.MinLevel}" : "trancada: falta uma missão/chave") : portal.MinLevel > 0 ? $"recomendado nível {portal.MinLevel}+" : "caminho livre" }, trancada ? new Color(1f, 0.5f, 0.45f) : new Color(0.8f, 0.6f, 1f)));
        }
        // 11/11 noite: NPC que ainda não apareceu = apagado; branco = só conversa; amarelo = tem missão; verde = entregar
        foreach (var npc in Layout.Npcs)
        {
            var at = At(npc.X, npc.Y);
            if (NpcShown is not null && !NpcShown(npc)) { DrawCircle(at, 3.5f, new Color(0.35f, 0.33f, 0.3f, 0.55f)); _alvos.Add((at, 6f, new[] { "Alguém que você ainda não conheceu" }, new Color(0.7f, 0.68f, 0.62f))); continue; }
            var mk = NpcMarks is not null && NpcMarks.TryGetValue(npc.Id, out var mkv) ? mkv : "";
            // 14/11 ("círculo preto bugado em cima da pedra de viagem"): NPC sem missão colado na pedra não desenha bolinha
            if (mk.Length == 0 && Layout.Waypoints.Any(w => Math.Abs(w.X - npc.X) < 4 && Math.Abs(w.Y - npc.Y) < 4)) { _alvos.Add((at, 6f, new[] { npc.Name, npc.Shop is not null ? "loja" : "conversa" }, new Color(0.96f, 0.96f, 0.94f))); continue; }
            var col = mk == "entregar" ? new Color(0.4f, 1f, 0.45f) : mk.Length > 0 ? new Color(1f, 0.82f, 0.25f) : new Color(0.96f, 0.96f, 0.94f);
            DrawCircle(at, 5f, new Color(0.08f, 0.06f, 0.05f, 0.9f));
            DrawCircle(at, 3.8f, col);
            if (_zoom >= 1.8f) DrawString(font, at + new Vector2(7, 4), npc.Name, HorizontalAlignment.Left, -1, 11, new Color(0.15f, 0.1f, 0.05f));
            _alvos.Add((at, 7f, new[] { npc.Name, mk == "entregar" ? "missão pronta para entregar aqui" : mk.Length > 0 ? "tem missão para você" : npc.Shop is not null ? "loja" : "conversa" }, col));
        }
        // 14/11 ("a pedra de viagem em azul"): por cima dos NPCs, losango azul com borda clara
        _wpTela.Clear();
        foreach (var wp in Layout.Waypoints)
        {
            var p = At(wp.X, wp.Y);
            _wpTela.Add((p, wp.Id));
            var los = new[] { p + new Vector2(0, -8.5f), p + new Vector2(7, 0), p + new Vector2(0, 8.5f), p + new Vector2(-7, 0) };
            DrawColoredPolygon(los.Select(q => q + new Vector2(1.2f, 1.6f)).ToArray(), new Color(0, 0, 0, 0.45f));
            DrawColoredPolygon(los, new Color(0.3f, 0.65f, 1f));
            DrawPolyline(los.Append(los[0]).ToArray(), new Color(0.85f, 0.95f, 1f), 1.4f, true);
            _alvos.Add((p, 10f, new[] { "Pedra de viagem: " + wp.Name, "duplo clique para viajar (perto de uma pedra, fora de combate)" }, new Color(0.45f, 0.75f, 1f)));
        }
        // 10/11: missões do canto da tela, com o mesmo número
        for (var i = 0; i < HudView.Tracked.Count; i++)
        {
            var q = HudView.Tracked[i];
            if (q.MapId == Layout.Id && (q.X != 0 || q.Y != 0))
            {
                CompassStrip.DrawQuestStone(this, At(q.X, q.Y), i + 1, q.State == "ready", 11f);
                var linhas = new List<string> { $"Missão {i + 1}: {q.Name}" };
                if (q.Hint.Length > 0) linhas.Add(q.Hint);
                linhas.Add(q.State == "ready" ? $"pronta — entregue para {q.Giver}" : q.Count > 1 ? $"progresso {q.Progress}/{q.Count}" : "em andamento");
                _alvos.Add((At(q.X, q.Y), 13f, linhas.ToArray(), q.State == "ready" ? new Color(0.45f, 0.95f, 0.5f) : new Color(1f, 0.8f, 0.32f)));
            }
        }
        if (Markers is not null && Markers.TryGetValue("horda", out var hz) && hz.Kind == "horda")
        {
            CompassStrip.DrawQuestStone(this, At(hz.X, hz.Y), 0, false, 11f);
            _alvos.Add((At(hz.X, hz.Y), 16f, new[] { "Horda caçando você", "vença o bando inteiro e ganhe um prêmio" }, new Color(1f, 0.35f, 0.3f)));
        }
        // 12/11 ("coloca o ícone do jogador no mapa"): seta dourada grande, apontando para onde você olha, com halo e "Você"
        var me = At(Me.X, Me.Y);
        var pulse = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 250f);
        DrawCircle(me, 14 + 6 * pulse, new Color(1f, 0.8f, 0.25f, 0.22f));
        var dir = MeFacing.LengthSquared() < 1e-6f ? new Vector2(0, -1) : MeFacing.Normalized();
        var lado = new Vector2(-dir.Y, dir.X);
        var seta = new[] { me + dir * 13f, me - dir * 8f + lado * 8.5f, me - dir * 3.5f, me - dir * 8f - lado * 8.5f };
        var sombra = seta.Select(q => q + new Vector2(1.5f, 2f)).ToArray();
        DrawColoredPolygon(sombra, new Color(0, 0, 0, 0.55f));
        DrawColoredPolygon(seta, new Color(1f, 0.82f, 0.28f));
        DrawPolyline(seta.Append(seta[0]).ToArray(), new Color(0.25f, 0.12f, 0.02f), 1.6f, true);
        DrawString(font, me + new Vector2(-14, -18), "Você", HorizontalAlignment.Left, -1, 13, new Color(1f, 0.92f, 0.6f));
        DrawString(font, new Vector2(origin.X + 14, origin.Y + 28), Layout.DisplayName, HorizontalAlignment.Left, -1, 22, tex is not null ? new Color(0.15f, 0.08f, 0.03f) : new Color(0.95f, 0.85f, 0.6f));
        _alvos.Add((me, 12f, new[] { "Você está aqui" }, new Color(1f, 0.85f, 0.3f)));
        DesenharDica();
        return true;
    }

    // 13/11: ícones do mapa local sob o mouse (o mais perto ganha) — caixinha com o que é
    private readonly List<(Vector2 P, float R, string[] Linhas, Color Cor)> _alvos = new();
    private Vector2 _mouse = new(-9999, -9999);

    private void DesenharDica()
    {
        var alvo = _alvos.Where(a => a.P.DistanceTo(_mouse) <= a.R).OrderBy(a => a.P.DistanceTo(_mouse)).FirstOrDefault();
        if (alvo.Linhas is null) return;
        var font = HudView.BodyFont ?? ThemeDB.FallbackFont;
        const float w = 260f;
        var hs = alvo.Linhas.Select((l, i) => font.GetMultilineStringSize(l, HorizontalAlignment.Left, w - 16, i == 0 ? 15 : 12).Y + 3f).ToArray();
        var hgt = 12f + hs.Sum();
        var at = alvo.P + new Vector2(16, -hgt / 2);
        if (at.X + w > Size.X - 6) at.X = alvo.P.X - w - 16;
        at.Y = Mathf.Clamp(at.Y, 6, Size.Y - hgt - 6);
        var box = new Rect2(at, new Vector2(w, hgt));
        DrawRect(box, new Color(0.07f, 0.06f, 0.05f, 0.95f));
        DrawRect(box, alvo.Cor, false, 1.5f);
        var y = at.Y + 6;
        for (var i = 0; i < alvo.Linhas.Length; i++)
        {
            var sz = i == 0 ? 15 : 12;
            DrawMultilineString(font, new Vector2(at.X + 8, y + sz), alvo.Linhas[i], HorizontalAlignment.Left, w - 16, sz, -1,
                i == 0 ? alvo.Cor : new Color(0.86f, 0.83f, 0.76f));
            y += hs[i];
        }
    }

    /// <summary>Mapa local desenhado a partir do layout (quando não há ilustração da área).</summary>
    private void DrawGenerated(Func<double, double, Vector2> At, float k, Vector2 origin, float side)
    {
        var L = Layout!;
        var chao = L.Theme switch
        {
            "cidade" => new Color(0.42f, 0.38f, 0.33f), "praia" => new Color(0.62f, 0.55f, 0.40f), "neve" => new Color(0.72f, 0.74f, 0.78f),
            "lava" => new Color(0.30f, 0.16f, 0.12f), "fantasma" => new Color(0.22f, 0.25f, 0.30f), "ceu" => new Color(0.70f, 0.78f, 0.90f),
            "mata" or "brejo" => new Color(0.28f, 0.36f, 0.24f), "lua" => new Color(0.35f, 0.35f, 0.38f), _ => new Color(0.40f, 0.38f, 0.30f),
        };
        DrawRect(new Rect2(At(-L.HalfSize, -L.HalfSize), new Vector2((float)(2 * L.HalfSize) * k, (float)(2 * L.HalfSize) * k)), chao);
        var font = HudView.BodyFont ?? ThemeDB.FallbackFont;
        foreach (var z in L.Zones)
        {
            var a = At(z.X - z.W / 2, z.Y - z.H / 2);
            var r = new Rect2(a, new Vector2((float)z.W * k, (float)z.H * k));
            var c = z.Kind switch { "seguro" or "refugio" => new Color(0.5f, 0.75f, 0.45f, 0.18f), "chefe" => new Color(0.9f, 0.3f, 0.2f, 0.16f), "caminho" => new Color(0, 0, 0, 0), _ => new Color(1, 1, 1, 0.05f) };
            if (c.A > 0) DrawRect(r, c);
        }
        foreach (var road in L.Roads ?? Array.Empty<CinzasDaMare.Shared.World.MapRoad>())
            for (var i = 0; i < road.Points.Count - 1; i++)
                DrawLine(At(road.Points[i][0], road.Points[i][1]), At(road.Points[i + 1][0], road.Points[i + 1][1]), new Color(0.62f, 0.52f, 0.36f, 0.85f), Mathf.Max(2f, (float)road.Width * k), true);
        foreach (var o in L.Obstacles)
        {
            if (o.Kind == "invisivel" && o.Shape == "circle") continue;
            var col = o.Kind switch
            {
                "invisivel" => new Color(0.55f, 0.42f, 0.32f, 0.95f),           // prédios da cidade (caixas de colisão)
                "arvore" => new Color(0.16f, 0.30f, 0.14f, 0.9f), "lago" or "ravina" => new Color(0.18f, 0.32f, 0.48f, 0.9f),
                "lava" => new Color(0.85f, 0.35f, 0.1f, 0.9f), _ => new Color(0.22f, 0.20f, 0.18f, 0.9f),
            };
            if (o.Shape == "circle") DrawCircle(At(o.X, o.Y), Mathf.Max(1.5f, (float)o.R * k), col);
            else DrawRect(new Rect2(At(o.X - o.W / 2, o.Y - o.H / 2), new Vector2(Mathf.Max(1.5f, (float)o.W * k), Mathf.Max(1.5f, (float)o.H * k))), col);
        }
        foreach (var z in L.Zones)
            if (z.Kind != "caminho" && z.Kind is not ("secreto" or "segredo") && z.Name.Length > 0 && z.W * k > 60)
                DrawString(font, At(z.X, z.Y) + new Vector2(-60, 0), z.Name, HorizontalAlignment.Center, 120, 12, new Color(1f, 0.95f, 0.8f, 0.75f));
        DrawRect(new Rect2(At(-L.HalfSize, -L.HalfSize), new Vector2((float)(2 * L.HalfSize) * k, (float)(2 * L.HalfSize) * k)), new Color(0.72f, 0.55f, 0.26f), false, 2f);
    }

    public WorldMapCanvas() { MouseFilter = MouseFilterEnum.Stop; ClipContents = true; }

    private float _zoom = 1f;
    private Vector2 _pan;
    /// <summary>12/11 ("dar zoom e tirar, com área mínima e máxima"): de 0,6× (mapa inteiro com folga) a 5× (rua por rua).</summary>
    public const float ZoomMin = 0.6f, ZoomMax = 5f;

    /// <summary>Botões + e −: aproximam/afastam pelo centro.</summary>
    public void Zoom(float fator)
    {
        var old = _zoom;
        _zoom = Mathf.Clamp(_zoom * fator, ZoomMin, ZoomMax);
        _pan *= _zoom / old;
        QueueRedraw();
    }

    /// <summary>Volta para o enquadramento inteiro e centralizado (ao abrir o mapa).</summary>
    public void ResetView() { _zoom = 1f; _pan = Vector2.Zero; QueueRedraw(); }

    // mapa de pergaminho: regiões são pontos (sem caixas sobrepostas); passar o mouse mostra o quadro da região
    private Vector2 P(double x, double y)
    {
        // 13/11 ("quero fazer a arte do mapa geral bonita e interativa"): com a arte Art/mapa_mundo.png, o mundo é o quadrado
        // de −100 a +100 (x e y, norte em cima) desenhado inteiro no meio do quadro, mesma escala nos dois eixos
        if (ArteMundo is not null)
        {
            var lado = Mathf.Min(Size.X, Size.Y);
            var o = (Size - new Vector2(lado, lado)) / 2f;
            return o + new Vector2((float)((x + 100) / 200 * lado), (float)((y + 100) / 200 * lado));
        }
        return new((float)(Size.X / 2 + x * (Size.X - 80) / 150.0), (float)(Size.Y / 2 + y * (Size.Y - 80) / 160.0));
    }

    private static Texture2D? ArteMundo => VisualAssets.Load("mapa_mundo.png");

    /// <summary>13/11 (arte definitiva do mapa geral, "mapa_definitivo_KRYMOR"): onde cada região está desenhada na arte
    /// (coordenadas −100..+100 do quadrado da imagem; pino em cima do desenho, não do nome). Sem a arte vale o world.json.</summary>
    private static readonly Dictionary<string, (double X, double Y)> NaArte = new()
    {
        // 14/11: arte nova do mapa (com as 4 cidades e os Reinos Ocultos no canto) — posições tiradas de cada desenho na arte
        ["porto"] = (-11, 70), ["costa"] = (-23, 43), ["vila_das_mos"] = (-10, 38), ["vale"] = (27, 25), ["monte"] = (65, 39),
        ["mata"] = (81, -2), ["astralia"] = (60, -20), ["canions"] = (39, -15), ["brejo"] = (3.5, -11), ["candeia"] = (-31, -15),
        ["vespera"] = (-62, -18), ["automatos"] = (-77, 10), ["geada"] = (-74, -48), ["ceus"] = (-85, -73), ["brasaforte"] = (-37, -51),
        ["escoria"] = (-10, -67), ["acropole"] = (39, -80), ["reinos"] = (-78, 71), ["mar"] = (58, 74),
    };
    private (double X, double Y) Onde(WorldRegionView r) => ArteMundo is not null && NaArte.TryGetValue(r.Id, out var a) ? a : (r.X, r.Y);

    private IEnumerable<WorldRegionView> Shown => Data?.Regions.Where(r => r.State != "future") ?? Enumerable.Empty<WorldRegionView>();

    public override void _GuiInput(InputEvent @event)
    {
        if (AreaMode && Layout is not null)
        {
            if (@event is InputEventMouseButton { Pressed: true } wb && wb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var old = _zoom;
                _zoom = Mathf.Clamp(_zoom * (wb.ButtonIndex == MouseButton.WheelUp ? 1.2f : 1f / 1.2f), ZoomMin, ZoomMax);
                // mantém o ponto sob o mouse no lugar
                var c = Size / 2f + _pan;
                _pan += (wb.Position - c) * (1f - _zoom / old);
                if (_zoom <= 1.001f) _pan *= 0.5f;
                QueueRedraw();
                AcceptEvent();
                return;
            }
            if (@event is InputEventMouseMotion solto && (solto.ButtonMask & MouseButtonMask.Left) == 0) { _mouse = solto.Position; QueueRedraw(); }
            if (@event is InputEventMouseMotion drag && (drag.ButtonMask & MouseButtonMask.Left) != 0)
            {
                var lim = Mathf.Min(Size.X, Size.Y) * _zoom / 2f;
                _pan = new Vector2(Mathf.Clamp(_pan.X + drag.Relative.X, -lim, lim), Mathf.Clamp(_pan.Y + drag.Relative.Y, -lim, lim));
                QueueRedraw();
                return;
            }
        }
        // 17/11 (dono: "clicar duas vezes na pedra de viagem e ir nela"): no mapa da área, duplo clique no losango azul viaja
        // (o servidor confere se você está perto de uma pedra e fora de combate e explica se não der)
        if (AreaMode && Layout is not null && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, DoubleClick: true } dbl)
        {
            var wpAlvo = _wpTela.Select(w => (w.Id, d: w.P.DistanceTo(dbl.Position))).Where(t => t.d < 14f).OrderBy(t => t.d).Select(t => t.Id).FirstOrDefault();
            if (wpAlvo is not null) { ViajarPara?.Invoke(wpAlvo); Sfx.Play("ui_clique"); AcceptEvent(); return; }
        }
        if (!AreaMode && Data is not null && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } clk)
        {
            var alvo = Shown.Select(r => (r, d: P(Onde(r).X, Onde(r).Y).DistanceTo(clk.Position))).Where(t => t.d < (ArteMundo is null ? 18 : 34)).OrderBy(t => t.d).Select(t => t.r).FirstOrDefault();
            if (alvo is not null)
            {
                var wp = Data.Waypoints.FirstOrDefault(w => w.Region == alvo.Id);
                if (wp is not null && !alvo.Here) { ViajarPara?.Invoke(wp.Id); Sfx.Play("ui_clique"); }
                else Sfx.Play("ui_erro");
                AcceptEvent();
                return;
            }
        }
        if (@event is not InputEventMouseMotion mm || Data is null) return;
        var hit = Shown.Select(r => (r, d: P(Onde(r).X, Onde(r).Y).DistanceTo(mm.Position))).Where(t => t.d < (ArteMundo is null ? 18 : 34)).OrderBy(t => t.d).Select(t => t.r.Id).FirstOrDefault();
        if (hit != _hover) { _hover = hit; QueueRedraw(); }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit) { _mouse = new Vector2(-9999, -9999); if (_hover is not null) _hover = null; QueueRedraw(); }
    }

    public override void _Draw()
    {
        if (DrawArea()) return;
        var size = Size;
        var rect = new Rect2(Vector2.Zero, size);
        if (ArteMundo is { } arte)
        {
            DrawRect(rect, new Color(0.08f, 0.07f, 0.06f));
            var lado = Mathf.Min(size.X, size.Y);
            DrawTextureRect(arte, new Rect2((size - new Vector2(lado, lado)) / 2f, new Vector2(lado, lado)), false);
        }
        // pergaminho: fundo creme queimado, manchas e borda escura
        else DrawRect(rect, new Color(0.62f, 0.52f, 0.36f));
        if (ArteMundo is null)
        {
            var rng = new System.Random(7);
            for (var i = 0; i < 60; i++)
                DrawCircle(new Vector2((float)rng.NextDouble() * size.X, (float)rng.NextDouble() * size.Y), 20 + (float)rng.NextDouble() * 60,
                    new Color(0.45f, 0.35f, 0.22f, 0.05f));
            for (var k = 0; k < 10; k++) DrawRect(rect.Grow(-k * 2.5f), new Color(0.25f, 0.16f, 0.08f, 0.10f), false, 2.5f);
            DrawRect(rect.Grow(-4), new Color(0.35f, 0.22f, 0.1f), false, 2f);
            Compass(new Vector2(size.X - 46, size.Y - 46));
        }
        if (Data is null) return;
        var font = ThemeDB.FallbackFont;
        var byId = Shown.ToDictionary(r => r.Id);
        var ink = new Color(0.25f, 0.15f, 0.07f);
        foreach (var link in Data.Links)
        {
            if (!byId.TryGetValue(link[0], out var a) || !byId.TryGetValue(link[1], out var b)) continue;
            if (ArteMundo is not null) break;   // a arte já tem as estradas
            var known = a.State == "visited" && b.State == "visited";
            DrawDashedLine(P(a.X, a.Y), P(b.X, b.Y), new Color(ink, known ? 0.8f : 0.35f), known ? 2.5f : 1.5f, known ? 10f : 5f);
        }
        foreach (var r in Shown)
        {
            var c = P(Onde(r).X, Onde(r).Y);
            var hover = r.Id == _hover;
            var visited = r.State == "visited";
            var known = visited || r.State == "known";
            var rad = (r.Kind == "cidade" ? 9f : 7f) * (hover ? 1.35f : 1f) * (ArteMundo is null ? 1f : 0.8f);
            if (r.Here) DrawCircle(c, rad + 7, new Color(1f, 0.85f, 0.3f, 0.35f + 0.15f * Mathf.Sin(Time.GetTicksMsec() / 300f)));
            DrawCircle(c, rad + 2, ink);
            DrawCircle(c, rad, visited ? new Color(0.85f, 0.32f, 0.16f) : known ? new Color(0.55f, 0.48f, 0.36f) : new Color(0.3f, 0.26f, 0.2f));
            if (r.Kind == "cidade") DrawCircle(c, rad * 0.4f, new Color(0.95f, 0.85f, 0.5f));
            var name = known ? r.Name : "?";
            if (ArteMundo is null) DrawString(font, c + new Vector2(-70, rad + 16), name, HorizontalAlignment.Center, 140, hover ? 13 : 11, ink);   // a arte já tem os nomes
            if (r.Quests is { Count: > 0 } quests)
            {
                var badge = c + new Vector2(rad + 4, -rad - 4);
                DrawCircle(badge, 8, new Color(0.95f, 0.72f, 0.15f));
                DrawString(font, badge + new Vector2(-4, 5), quests.Count > 1 ? quests.Count.ToString() : "!", HorizontalAlignment.Left, 20, 12, new Color(0.12f, 0.08f, 0.02f));
            }
        }
        // quadro da região sob o mouse
        if (_hover is not null && byId.TryGetValue(_hover, out var h))
        {
            var known = h.State is "visited" or "known";
            var lines = new List<(string T, int S, Color C)> { (known ? h.Name : "Região desconhecida", 15, new Color(0.95f, 0.8f, 0.4f)) };
            if (known) lines.Add((h.Kind == "cidade" ? "cidade segura" : "nível " + h.Levels, 12, new Color(0.85f, 0.82f, 0.75f)));
            if (known && h.Description.Length > 0) lines.Add((h.Description, 11, new Color(0.75f, 0.72f, 0.65f)));
            if (h.Quests is { Count: > 0 } q) lines.Add(($"{q.Count} missão(ões) aqui", 11, new Color(0.95f, 0.72f, 0.15f)));
            if (h.Here) lines.Add(("você está aqui", 11, new Color(1f, 0.85f, 0.3f)));
            else if (Data.Waypoints.Any(w => w.Region == h.Id)) lines.Add(("clique para viajar (pedra de viagem)", 11, new Color(0.55f, 0.85f, 1f)));
            const float w = 240;
            // 13/11 ("o quadrado de info tem informações sobrepostas"): mede a altura real de cada linha quebrada (antes chutava 2 linhas)
            float Alt((string T, int S, Color C) l) => font.GetMultilineStringSize(l.T, HorizontalAlignment.Left, w - 16, l.S).Y + 5f;
            var hgt = 12f + lines.Sum(Alt);
            var at = P(Onde(h).X, Onde(h).Y) + new Vector2(14, -hgt / 2);
            at.X = Mathf.Min(at.X, size.X - w - 8); at.Y = Mathf.Clamp(at.Y, 8, size.Y - hgt - 8);
            var box = new Rect2(at, new Vector2(w, hgt));
            DrawRect(box, new Color(0.08f, 0.07f, 0.06f, 0.94f));
            DrawRect(box, new Color(0.72f, 0.55f, 0.26f), false, 1.5f);
            var y = at.Y + 6;
            foreach (var l in lines)
            {
                DrawMultilineString(font, new Vector2(at.X + 8, y + font.GetAscent(l.S)), l.T, HorizontalAlignment.Left, w - 16, l.S, -1, l.C);
                y += Alt(l);
            }
        }
    }

    private void Compass(Vector2 c)
    {
        var ink = new Color(0.3f, 0.18f, 0.08f, 0.8f);
        DrawCircle(c, 26, new Color(0.55f, 0.45f, 0.3f, 0.6f));
        DrawArc(c, 26, 0, Mathf.Tau, 32, ink, 1.5f);
        DrawColoredPolygon(new[] { c + new Vector2(0, -24), c + new Vector2(5, 0), c + new Vector2(0, 24), c + new Vector2(-5, 0) }, ink);
        DrawColoredPolygon(new[] { c + new Vector2(-24, 0), c + new Vector2(0, 4), c + new Vector2(24, 0), c + new Vector2(0, -4) }, new Color(ink, 0.5f));
        DrawString(ThemeDB.FallbackFont, c + new Vector2(-4, -28), "N", HorizontalAlignment.Left, 20, 12, ink);
    }
}
