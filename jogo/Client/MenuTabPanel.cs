using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 14/11 ("usar o TAB como menu, onde reúne as mecânicas — trilha de níveis, mapa... igual Skyrim/Diablo; e uma lojinha com
/// missões: mate mil monstros, login diário... tipo Master Duel, que dão cristais azuis para trocar por coisas picas").
/// 14/11 tarde (dono: "visual AAA; no TAB não é pra abrir coisas por cima, é pra trocar de aba ao clicar, mantendo tudo
/// ali dentro; Teclas dentro de Opções; Loja primeiro, depois Objetivos; cristal azul no lugar do diamante").
/// Abas: Loja · Objetivos · Trilha · Mapa · Opções (Geral | Teclas). Trilha, Mapa e Opções são EMPRESTADOS das janelas
/// próprias (<see cref="Corpo"/>): o conteúdo muda de pai para dentro do TAB e volta quando troca de aba ou fecha.
/// </summary>
public sealed partial class MenuTabPanel : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.78f, 0.30f), Dim = new(0.72f, 0.69f, 0.62f), Azul = new(0.55f, 0.8f, 1f), Good = new(0.5f, 0.95f, 0.55f);
    public event Action<NetMessage>? Send;
    /// <summary>O Main entrega o conteúdo da janela própria: "trilha", "mapa", "opcoes", "teclas".</summary>
    public Func<string, Control?>? Corpo;
    /// <summary>Avisa o Main que um conteúdo entrou (true) ou saiu (false) do TAB (para atualizar/pedir dados).</summary>
    public event Action<string, bool>? Embutido;

    public static Texture2D? Cristal { get; } = VisualAssets.Load("ui_cristal_azul.png");

    private readonly Label _saldo = HudView.MakeLabel("0", 20, Azul);
    private readonly VBoxContainer _conteudo = new();
    private readonly ScrollContainer _scroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly VBoxContainer _hospede = new() { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly HBoxContainer _subAbas = new() { Visible = false };
    private readonly HBoxContainer _abas = new();
    private readonly Label _rodape = HudView.MakeLabel("", 11, Dim);
    private readonly Dictionary<string, Button> _botoes = new();
    private readonly Dictionary<string, Button> _botoesSub = new();
    private string _aba = "loja", _sub = "opcoes";
    private ObjetivosMessage? _dados;
    private ClientModel? _m;
    // conteúdo emprestado de outra janela
    private Control? _emprestado; private Node? _donoOrig; private int _idxOrig; private string? _idEmprestado;

    public MenuTabPanel()
    {
        Visible = false;
        var fundo = new StyleBoxFlat
        {
            BgColor = new Color(0.045f, 0.05f, 0.07f, 0.97f), BorderColor = new Color(0.62f, 0.5f, 0.24f, 0.95f),
            ShadowColor = new Color(0, 0, 0, 0.6f), ShadowSize = 18,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12,
        };
        fundo.SetBorderWidthAll(2); fundo.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", fundo);
        CustomMinimumSize = new Vector2(1000, 660);
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.Both;
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        AddChild(col);

        // topo: título · saldo de cristais
        var topo = new HBoxContainer();
        topo.AddThemeConstantOverride("separation", 10);
        var t = HudView.MakeLabel("KRYMOR", 26, Gold, 5);
        if (HudView.TitleFont is not null) t.AddThemeFontOverride("font", HudView.TitleFont);
        topo.AddChild(t);
        var sub = HudView.MakeLabel("·  menu do herói", 15, Dim);
        sub.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        topo.AddChild(sub);
        topo.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var saldoBox = new PanelContainer();
        var sb = new StyleBoxFlat { BgColor = new Color(0.06f, 0.12f, 0.22f, 0.9f), BorderColor = new Color(0.35f, 0.62f, 1f, 0.8f),
            ContentMarginLeft = 10, ContentMarginRight = 14, ContentMarginTop = 2, ContentMarginBottom = 2 };
        sb.SetBorderWidthAll(1); sb.SetCornerRadiusAll(14);
        saldoBox.AddThemeStyleboxOverride("panel", sb);
        var saldoRow = new HBoxContainer();
        saldoRow.AddThemeConstantOverride("separation", 6);
        saldoRow.AddChild(Icone(30));
        _saldo.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        saldoRow.AddChild(_saldo);
        var cr = HudView.MakeLabel("cristais", 12, Dim);
        cr.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        saldoRow.AddChild(cr);
        saldoBox.AddChild(saldoRow);
        topo.AddChild(saldoBox);
        topo.AddChild(new Control { CustomMinimumSize = new Vector2(16, 0) });   // espaço do X
        col.AddChild(topo);

        // faixa das abas
        _abas.AddThemeConstantOverride("separation", 2);
        col.AddChild(_abas);
        foreach (var (id, nome) in new[] { ("loja", "Loja de Cristais"), ("objetivos", "Objetivos"), ("trilha", "Trilha"), ("mapa", "Mapa"), ("notas", "Notas"), ("opcoes", "Opções") })
        {
            var b = Aba(nome, 17, new Vector2(150, 40));
            if (id is "loja" or "objetivos" && Cristal is not null) { b.Icon = Cristal; b.ExpandIcon = false; b.AddThemeConstantOverride("icon_max_width", 22); }
            b.Pressed += () => { Sfx.Play("ui_clique"); Ir(id); };
            _botoes[id] = b;
            _abas.AddChild(b);
        }
        var linha = new ColorRect { Color = new Color(0.62f, 0.5f, 0.24f, 0.55f), CustomMinimumSize = new Vector2(0, 1) };
        col.AddChild(linha);

        // sub-abas (Opções: Geral | Teclas)
        _subAbas.AddThemeConstantOverride("separation", 2);
        foreach (var (id, nome) in new[] { ("opcoes", "Geral"), ("teclas", "Teclas") })
        {
            var b = Aba(nome, 14, new Vector2(110, 30));
            b.Pressed += () => { Sfx.Play("ui_clique"); _sub = id; Montar(); };
            _botoesSub[id] = b;
            _subAbas.AddChild(b);
        }
        col.AddChild(_subAbas);

        // corpo
        var corpo = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        var cf = new StyleBoxFlat { BgColor = new Color(0.02f, 0.025f, 0.035f, 0.75f), BorderColor = new Color(0.3f, 0.26f, 0.18f, 0.5f),
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10 };
        cf.SetBorderWidthAll(1); cf.SetCornerRadiusAll(4);
        corpo.AddThemeStyleboxOverride("panel", cf);
        var pilha = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        corpo.AddChild(pilha);
        _conteudo.AddThemeConstantOverride("separation", 6);
        _conteudo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(_conteudo);
        pilha.AddChild(_scroll);
        pilha.AddChild(_hospede);
        col.AddChild(corpo);
        col.AddChild(_rodape);
        VisibilityChanged += () => { if (!Visible) Devolver(); };
    }

    public override void _Ready() => Arrastador.Ligar(this, "menutab", 46f, fechar: () => Visible = false);

    /// <summary>Ícone do cristal azul (arte do dono).</summary>
    public static Control Icone(float lado) => new TextureRect
    {
        Texture = Cristal, CustomMinimumSize = new Vector2(lado, lado), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, SizeFlagsVertical = SizeFlags.ShrinkCenter,
    };

    private static Button Aba(string texto, int fonte, Vector2 tam)
    {
        var b = new Button { Text = texto, ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = tam };
        StyleBoxFlat Estilo(Color fundo, Color borda, int baixo)
        {
            var s = new StyleBoxFlat { BgColor = fundo, BorderColor = borda, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 4, ContentMarginBottom = 4 };
            s.BorderWidthBottom = baixo; s.CornerRadiusTopLeft = 4; s.CornerRadiusTopRight = 4;
            return s;
        }
        b.AddThemeStyleboxOverride("normal", Estilo(new Color(0.08f, 0.08f, 0.1f, 0.6f), new Color(0.3f, 0.26f, 0.18f, 0.6f), 1));
        b.AddThemeStyleboxOverride("hover", Estilo(new Color(0.16f, 0.13f, 0.08f, 0.85f), new Color(0.8f, 0.64f, 0.3f, 0.8f), 2));
        b.AddThemeStyleboxOverride("pressed", Estilo(new Color(0.24f, 0.18f, 0.07f, 0.95f), Gold, 3));
        b.AddThemeStyleboxOverride("hover_pressed", Estilo(new Color(0.28f, 0.21f, 0.08f, 0.95f), Gold, 3));
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        b.AddThemeColorOverride("font_color", Dim);
        b.AddThemeColorOverride("font_hover_color", new Color(1f, 0.92f, 0.75f));
        b.AddThemeColorOverride("font_pressed_color", new Color(1f, 0.88f, 0.5f));
        b.AddThemeColorOverride("font_hover_pressed_color", new Color(1f, 0.9f, 0.6f));
        b.AddThemeFontSizeOverride("font_size", fonte);
        if (HudView.TitleFont is not null) b.AddThemeFontOverride("font", HudView.TitleFont);
        return b;
    }

    public void Mostrar(ClientModel m, string? aba = null)
    {
        _m = m;
        if (aba is "teclas") { _aba = "opcoes"; _sub = "teclas"; }
        else if (aba is "loja" or "objetivos" or "trilha" or "mapa" or "notas" or "opcoes") _aba = aba;
        Visible = true;
        Send?.Invoke(new ObjetivosIntent("abrir"));
        Montar();
    }

    /// <summary>Troca de aba por fora (ex.: o botão "Trocar teclas" das Opções).</summary>
    public void Ir(string aba)
    {
        if (aba == "teclas") { _aba = "opcoes"; _sub = "teclas"; }
        else _aba = aba;
        Montar();
    }

    public void Atualizar(ObjetivosMessage? d)
    {
        if (d is null || ReferenceEquals(d, _dados)) return;
        _dados = d;
        if (Visible && _aba is "loja" or "objetivos") Montar();
        else _saldo.Text = $"{_dados.Cristais:N0}".Replace(",", ".");
    }

    private void Montar()
    {
        _saldo.Text = $"{_dados?.Cristais ?? 0:N0}".Replace(",", ".");
        foreach (var (id, b) in _botoes) b.SetPressedNoSignal(id == _aba);
        foreach (var (id, b) in _botoesSub) b.SetPressedNoSignal(id == _sub);
        _subAbas.Visible = _aba == "opcoes";
        foreach (var c in _conteudo.GetChildren()) { _conteudo.RemoveChild(c); c.QueueFree(); }
        var proprio = _aba is "loja" or "objetivos" or "notas";
        _scroll.Visible = proprio;
        _hospede.Visible = !proprio;
        _rodape.Text = _aba switch
        {
            "loja" or "objetivos" => "Os cristais são da CONTA: valem para todos os seus personagens. Diárias zeram à meia-noite; semanais, na segunda.",
            "trilha" => "O que abre em cada nível: regiões, dungeons, viagens, montarias, asas, poderes, classe e história.",
            "mapa" => "Rodinha: zoom · arraste para mover · clique numa pedra de viagem descoberta para viajar.",
            "notas" => "O que mudou em cada atualização (a mais nova em cima).",
            _ => "TAB fecha o menu.",
        };
        if (!proprio) { Emprestar(_aba == "opcoes" ? _sub : _aba); return; }
        Devolver();
        if (_aba == "notas") { MontarNotas(); return; }
        if (_dados is null) { _conteudo.AddChild(HudView.MakeLabel("carregando...", 14, Dim)); return; }
        if (_aba == "objetivos") MontarObjetivos(); else MontarLoja();
    }

    /// <summary>18/11 (dono: "coloca uma aba no TAB de NOTAS — as atualizações explicando o que teve"): uma carta por versão,
    /// a mais nova em cima; cada novidade numa linha com marcador (Shared/NotasAtualizacao.cs, gerado a cada entrega).</summary>
    private void MontarNotas()
    {
        var primeira = true;
        foreach (var (versao, texto) in CinzasDaMare.Shared.NotasAtualizacao.Todas)
        {
            var card = new PanelContainer();
            card.AddThemeStyleboxOverride("panel", HudView.CardStyle(primeira));
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 3);
            card.AddChild(col);
            col.AddChild(HudView.MakeLabel((primeira ? "★ " : "") + versao + (primeira ? "  (a sua versão)" : ""), 16, primeira ? new Color(1f, 0.78f, 0.35f) : new Color(0.92f, 0.85f, 0.65f)));
            foreach (var item in texto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var l = HudView.MakeLabel("•  " + char.ToUpper(item[0]) + item[1..], 13, new Color(0.9f, 0.88f, 0.82f));
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                l.CustomMinimumSize = new Vector2(900, 0);
                col.AddChild(l);
            }
            _conteudo.AddChild(card);
            primeira = false;
        }
    }

    private void Emprestar(string id)
    {
        if (_idEmprestado == id && _emprestado is not null && GodotObject.IsInstanceValid(_emprestado)) return;
        Devolver();
        var c = Corpo?.Invoke(id);
        if (c is null) { _hospede.AddChild(HudView.MakeLabel("indisponível", 14, Dim)); return; }
        _donoOrig = c.GetParent(); _idxOrig = c.GetIndex();
        _donoOrig?.RemoveChild(c);
        c.SizeFlagsHorizontal = SizeFlags.ExpandFill; c.SizeFlagsVertical = SizeFlags.ExpandFill;
        _hospede.AddChild(c);
        _emprestado = c; _idEmprestado = id;
        Embutido?.Invoke(id, true);
    }

    /// <summary>Devolve o conteúdo emprestado para a janela de origem.</summary>
    public void Devolver()
    {
        foreach (var c in _hospede.GetChildren()) if (!ReferenceEquals(c, _emprestado)) { _hospede.RemoveChild(c); c.QueueFree(); }
        if (_emprestado is null) return;
        var c2 = _emprestado; var id = _idEmprestado!;
        _emprestado = null; _idEmprestado = null;
        if (!GodotObject.IsInstanceValid(c2)) return;
        _hospede.RemoveChild(c2);
        if (_donoOrig is not null && GodotObject.IsInstanceValid(_donoOrig))
        {
            _donoOrig.AddChild(c2);
            _donoOrig.MoveChild(c2, Math.Min(_idxOrig, _donoOrig.GetChildCount() - 1));
        }
        Embutido?.Invoke(id, false);
    }

    private static PanelContainer Cartao(Color borda, float alpha = 0.85f)
    {
        var p = new PanelContainer();
        var s = new StyleBoxFlat { BgColor = new Color(0.07f, 0.075f, 0.095f, alpha), BorderColor = borda,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6 };
        s.SetBorderWidthAll(1); s.SetCornerRadiusAll(5);
        p.AddThemeStyleboxOverride("panel", s);
        return p;
    }

    private static Label Titulo(string texto)
    {
        var l = HudView.MakeLabel(texto, 19, Gold);
        if (HudView.TitleFont is not null) l.AddThemeFontOverride("font", HudView.TitleFont);
        return l;
    }

    private static HBoxContainer Preco(int n, int fonte = 14)
    {
        var h = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        h.AddThemeConstantOverride("separation", 3);
        h.AddChild(Icone(fonte + 6));
        h.AddChild(HudView.MakeLabel($"{n:N0}".Replace(",", "."), fonte, Azul));
        return h;
    }

    private void MontarObjetivos()
    {
        // 14/11 tarde (dono: "coloca o ícone do cristal no objetivo, pra mostrar que é pra ganhar cristal — deixar claro")
        var faixa = Cartao(new Color(0.35f, 0.62f, 1f, 0.9f));
        var fr = new HBoxContainer();
        fr.AddThemeConstantOverride("separation", 12);
        faixa.AddChild(fr);
        fr.AddChild(Icone(46));
        var txt = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        txt.AddChild(HudView.MakeLabel("Cumpra objetivos e ganhe CRISTAIS AZUIS", 18, Azul));
        txt.AddChild(HudView.MakeLabel("Troque os cristais na Loja de Cristais por asas, capas, skins, caixas, montarias e armas lendárias.", 12, Dim));
        fr.AddChild(txt);
        _conteudo.AddChild(faixa);
        // 15/11 noite (dono: "tira as missões já feitas; uma aba ao lado — já feitas — e aí clica pra ver")
        var nFeitas = _dados!.Lista.Count(o => o.Resgatado);
        var abas = new HBoxContainer();
        abas.AddThemeConstantOverride("separation", 6);
        foreach (var (feitas, rotulo) in new[] { (false, "A fazer"), (true, $"Já feitas ({nFeitas})") })
        {
            var ab = Aba(rotulo, 14, new Vector2(150, 32));
            ab.SetPressedNoSignal(_verFeitas == feitas);
            ab.Pressed += () => { _verFeitas = feitas; Montar(); };
            abas.AddChild(ab);
        }
        _conteudo.AddChild(abas);
        var algum = false;
        foreach (var (tipo, titulo) in new[] { ("dia", "Diárias"), ("semana", "Semanais"), ("conta", "Da conta (uma vez só)") })
        {
            var lista = _dados!.Lista.Where(o => o.Tipo == tipo && o.Resgatado == _verFeitas).OrderByDescending(o => o.Progresso >= o.Meta).ToList();
            if (lista.Count == 0) continue;
            algum = true;
            _conteudo.AddChild(Titulo(titulo));
            foreach (var o in lista)
            {
                var feito = o.Progresso >= o.Meta;
                var card = Cartao(o.Resgatado ? new Color(0.25f, 0.25f, 0.25f, 0.6f) : feito ? Good : new Color(0.35f, 0.3f, 0.2f, 0.7f), o.Resgatado ? 0.5f : 0.85f);
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 10);
                card.AddChild(row);
                var nome = HudView.MakeLabel((o.Resgatado ? "✓ " : "") + o.Titulo, 14, o.Resgatado ? Dim : Colors.White);
                nome.CustomMinimumSize = new Vector2(330, 0); nome.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                row.AddChild(nome);
                var barra = new ProgressBar { MinValue = 0, MaxValue = o.Meta, Value = o.Progresso, CustomMinimumSize = new Vector2(190, 14), ShowPercentage = false, SizeFlagsVertical = SizeFlags.ShrinkCenter };
                var bf = new StyleBoxFlat { BgColor = feito ? Good : new Color(0.35f, 0.62f, 1f) }; bf.SetCornerRadiusAll(3);
                var bb = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.12f, 0.9f) }; bb.SetCornerRadiusAll(3);
                barra.AddThemeStyleboxOverride("fill", bf); barra.AddThemeStyleboxOverride("background", bb);
                row.AddChild(barra);
                var pr = HudView.MakeLabel($"{o.Progresso:N0}/{o.Meta:N0}".Replace(",", "."), 12, Dim);
                pr.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                row.AddChild(pr);
                row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
                var premio = Preco(o.Cristais, 16);
                premio.GetChild<Label>(1).Text = "+" + premio.GetChild<Label>(1).Text;
                premio.AddChild(HudView.MakeLabel("cristais", 11, Dim));
                premio.CustomMinimumSize = new Vector2(120, 0);
                row.AddChild(premio);
                var b = new Button { Text = o.Resgatado ? "resgatado" : feito ? "RESGATAR" : "—", Disabled = o.Resgatado || !feito, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 34) };
                if (feito && !o.Resgatado) BotaoAzul(b);   // 15/11 noite (dono: "o botão resgatar mais destacado, azul")
                var id = o.Id;
                b.Pressed += () => { Sfx.Play("moeda"); Send?.Invoke(new ObjetivosIntent("resgatar", id)); };
                row.AddChild(b);
                _conteudo.AddChild(card);
            }
            _conteudo.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }
        if (!algum) _conteudo.AddChild(HudView.MakeLabel(_verFeitas ? "Nenhum objetivo resgatado ainda." : "Tudo feito por agora! Diárias voltam à meia-noite; semanais, na segunda.", 14, Dim));
    }

    private bool _verFeitas;

    /// <summary>Botão azul brilhante (Resgatar): fundo azul cristal, borda clara, texto branco forte.</summary>
    private static void BotaoAzul(Button b)
    {
        StyleBoxFlat Caixa(Color bg, Color borda)
        {
            var s = new StyleBoxFlat { BgColor = bg, BorderColor = borda, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 4, ContentMarginBottom = 4,
                ShadowColor = new Color(0.25f, 0.55f, 1f, 0.55f), ShadowSize = 6 };
            s.SetBorderWidthAll(2); s.SetCornerRadiusAll(6);
            return s;
        }
        b.AddThemeStyleboxOverride("normal", Caixa(new Color(0.12f, 0.42f, 0.95f), new Color(0.6f, 0.82f, 1f)));
        b.AddThemeStyleboxOverride("hover", Caixa(new Color(0.2f, 0.55f, 1f), new Color(0.8f, 0.92f, 1f)));
        b.AddThemeStyleboxOverride("pressed", Caixa(new Color(0.08f, 0.32f, 0.8f), new Color(0.6f, 0.82f, 1f)));
        b.AddThemeColorOverride("font_color", Colors.White);
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeFontSizeOverride("font_size", 15);
    }

    private static readonly Dictionary<string, string> NomeGrupo = new()
    {
        ["caixas"] = "Caixas das Cinzas", ["conforto"] = "Conforto", ["asas"] = "Asas (raras, épicas e lendárias)", ["capas"] = "Capas", ["skins"] = "Skins (bracelete)", ["pets"] = "Companheiros",
        ["montarias"] = "Montarias", ["lendarias"] = "Armas lendárias",
    };

    /// <summary>Moldura pela faixa de preço (o que custa mais brilha mais).</summary>
    private static Color CorPreco(long p) => p >= 900 ? new Color(1f, 0.62f, 0.2f) : p >= 450 ? new Color(0.75f, 0.45f, 1f)
        : p >= 150 ? new Color(0.4f, 0.65f, 1f) : new Color(0.4f, 0.36f, 0.28f, 0.8f);

    private void MontarLoja()
    {
        var saldo = _dados!.Cristais;
        foreach (var g in _dados.Loja.GroupBy(l => l.Grupo))
        {
            _conteudo.AddChild(Titulo(NomeGrupo.GetValueOrDefault(g.Key, g.Key)));
            var grade = new GridContainer { Columns = 5 };
            grade.AddThemeConstantOverride("h_separation", 8); grade.AddThemeConstantOverride("v_separation", 8);
            foreach (var l in g.OrderBy(x => x.Preco))
            {
                var info = _m?.ItemInfoFor(l.Item);
                var card = Cartao(CorPreco(l.Preco));
                card.CustomMinimumSize = new Vector2(176, 0);
                var v = new VBoxContainer();
                v.AddThemeConstantOverride("separation", 3);
                card.AddChild(v);
                var icon = new TextureRect
                {
                    Texture = l.Item.StartsWith("mochila:", StringComparison.Ordinal) ? null : ItemIconRenderer.Get(info, l.Item), CustomMinimumSize = new Vector2(72, 72),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                };
                v.AddChild(icon);
                var nome = HudView.MakeLabel(l.Nome, 13, Colors.White, 2);
                nome.AutowrapMode = TextServer.AutowrapMode.WordSmart; nome.HorizontalAlignment = HorizontalAlignment.Center;
                v.AddChild(nome);
                if (l.Nivel > 1) { var nv = HudView.MakeLabel($"nível {l.Nivel}", 10, (_m?.Me?.Level ?? 1) >= l.Nivel ? Dim : new Color(0.95f, 0.5f, 0.42f)); nv.HorizontalAlignment = HorizontalAlignment.Center; v.AddChild(nv); }
                var b = new Button { Text = l.JaTem ? "já tem" : $"{l.Preco:N0}".Replace(",", "."), Disabled = l.JaTem || saldo < l.Preco, FocusMode = FocusModeEnum.None };
                if (!l.JaTem && Cristal is not null) { b.Icon = Cristal; b.AddThemeConstantOverride("icon_max_width", 18); }
                var item = l.Item;
                b.Pressed += () => { Sfx.Play("moeda"); Send?.Invoke(new ObjetivosIntent("comprar", item)); };
                v.AddChild(b);
                grade.AddChild(card);
            }
            _conteudo.AddChild(grade);
            _conteudo.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        }
    }
}
