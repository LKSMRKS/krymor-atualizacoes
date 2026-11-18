using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 13/11 ("uma progressão para acompanhar o que você ganha em cada nível — tipo um passe de batalha, numa tecla").
/// 14/11 21h (dono: "a trilha na horizontal, com as recompensas em arte, igual o passe de batalha do Master Duel";
/// "separar o que você pode USAR e o que você pode GANHAR"): uma faixa que rola para o lado, uma coluna por nível com
/// prêmio. Em cima, o que ABRE para usar (regiões, dungeons, viagens, poderes, classe, casa, história); embaixo, o que
/// você GANHA (montarias, asas, capas, companheiros, skins, presentes). Cartão com a arte do prêmio (ícone do item ou do
/// poder); o que você já tem fica aceso com ✓, o que falta fica apagado com o nível. Abre já no seu nível.
/// </summary>
public sealed partial class TrilhaPanel : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.78f, 0.30f);
    private static readonly Color Dim = new(0.72f, 0.69f, 0.62f);
    private static readonly HashSet<string> Ganhar = new() { "montaria", "asas", "capa", "pet", "skin", "presente" };
    private readonly HBoxContainer _colunas = new();
    private readonly Label _proximo = HudView.MakeLabel("", 14, new Color(0.6f, 0.9f, 1f));
    private readonly ScrollContainer _scroll = new() { VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(940, 470), SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private int _seq = -1;
    private ClientModel? _m;
    public bool Open => Visible;
    /// <summary>O miolo (o TAB o pega emprestado na aba Trilha).</summary>
    public VBoxContainer Corpo { get; }

    public TrilhaPanel()
    {
        Visible = false;
        AddThemeStyleboxOverride("panel", HudView.PanelStyle(10f, 0.95f));
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.Both;
        var col = Corpo = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        AddChild(col);
        var t = HudView.MakeLabel("⚜ Trilha do Herói", 22, Gold);
        if (HudView.TitleFont is not null) t.AddThemeFontOverride("font", HudView.TitleFont);
        col.AddChild(t);
        _proximo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_proximo);
        col.AddChild(_scroll);
        _colunas.AddThemeConstantOverride("separation", 6);
        _scroll.AddChild(_colunas);
        // 18/11 ("fala que GANHA, mas eu ganho? tá meio confuso"): legenda do que cada marca quer dizer
        col.AddChild(HudView.MakeLabel("Em cima: o que ABRE para você (lugares, dungeons, poderes). Embaixo: PRÊMIOS — ✓ já é seu · ◆ laranja = o nível já deixa, mas você ainda precisa pegar (passe o mouse: diz onde) · apagado = nível ainda não chegou. Presentes chegam sozinhos na mochila.", 11, Dim));
    }

    public override void _Ready() => Arrastador.Ligar(this, "trilha", 40f, fechar: () => Visible = false);

    /// <summary>Atualiza quando chega a trilha nova do servidor.</summary>
    public void Atualizar(TrilhaMessage? t, int seq, ClientModel? m = null)
    {
        if (m is not null) _m = m;
        if (t is null || seq == _seq) return;
        _seq = seq;
        foreach (var c in _colunas.GetChildren()) { _colunas.RemoveChild(c); c.QueueFree(); }
        var prox = t.Itens.Where(i => !i.Tem && i.Nivel >= t.Nivel).Take(3).ToList();
        _proximo.Text = prox.Count == 0 ? "Você já abriu tudo da trilha. Renascer (nível 60) recomeça mais forte." :
            "Próximo: " + string.Join("   ·   ", prox.Select(p => $"Nv {p.Nivel} — {p.Titulo}"));
        Control? atual = null;
        foreach (var g in t.Itens.GroupBy(i => i.Nivel).OrderBy(g => g.Key))
        {
            var chegou = g.Key <= t.Nivel;
            var coluna = new PanelContainer();
            var fundo = new StyleBoxFlat
            {
                BgColor = chegou ? new Color(0.17f, 0.13f, 0.06f, 0.75f) : new Color(0.06f, 0.06f, 0.08f, 0.7f),
                BorderColor = chegou ? new Color(0.75f, 0.58f, 0.24f, 0.8f) : new Color(0.25f, 0.24f, 0.22f, 0.6f),
                ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 6, ContentMarginBottom = 6,
            };
            fundo.SetBorderWidthAll(1); fundo.SetCornerRadiusAll(6);
            coluna.AddThemeStyleboxOverride("panel", fundo);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 4);
            coluna.AddChild(v);
            // cabeçalho do nível (medalha)
            var nv = HudView.MakeLabel($"Nv {g.Key}", 18, chegou ? Gold : Dim, 3);
            nv.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(nv);
            var faixa = new ColorRect { Color = chegou ? Gold : new Color(0.3f, 0.3f, 0.3f), CustomMinimumSize = new Vector2(0, 3) };
            v.AddChild(faixa);
            // USAR (em cima) e GANHAR (embaixo)
            var usar = g.Where(i => !Ganhar.Contains(i.Tipo)).Take(3).ToList();
            var ganhar = g.Where(i => Ganhar.Contains(i.Tipo)).Take(3).ToList();
            v.AddChild(Faixa("ABRE", usar, chegou, new Color(0.45f, 0.75f, 1f)));
            v.AddChild(new ColorRect { Color = new Color(1, 1, 1, 0.08f), CustomMinimumSize = new Vector2(0, 1) });
            v.AddChild(Faixa("PRÊMIOS", ganhar, chegou, new Color(0.55f, 0.95f, 0.5f)));
            var resto = g.Count() - usar.Count - ganhar.Count;
            if (resto > 0) { var r = HudView.MakeLabel($"+{resto} no mesmo nível", 10, Dim); r.HorizontalAlignment = HorizontalAlignment.Center; v.AddChild(r); }
            _colunas.AddChild(coluna);
            if (chegou) atual = coluna;
        }
        _atual = atual;
        Focar();
    }

    private Control? _atual;

    /// <summary>18/11 ("no tab, trilha, sempre que abrir, já mostrar ali de acordo com seu nível"): rola até o seu nível
    /// (com uma coluna de folga à esquerda). Chamado ao chegar a trilha e toda vez que a aba/janela abre.</summary>
    public void Focar()
    {
        var alvo = _atual;
        if (alvo is null) return;
        void Rolar()
        {
            if (!IsInstanceValid(alvo) || !IsInstanceValid(_scroll)) return;
            _scroll.ScrollHorizontal = (int)Math.Max(0, alvo.Position.X - 110);
        }
        Callable.From(Rolar).CallDeferred();
        // o layout do TAB só fecha no quadro seguinte: tenta de novo um pouco depois
        if (IsInsideTree()) GetTree().CreateTimer(0.08).Timeout += Rolar;
    }

    private Control Faixa(string titulo, List<TrilhaItem> itens, bool chegou, Color cor)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(0, 205) };
        box.AddThemeConstantOverride("separation", 3);
        var l = HudView.MakeLabel(titulo, 10, cor, 2);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(l);
        var linha = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        linha.AddThemeConstantOverride("separation", 4);
        foreach (var i in itens) linha.AddChild(Cartao(i));
        if (itens.Count == 0) { var vazio = HudView.MakeLabel("—", 14, new Color(0.4f, 0.4f, 0.4f)); vazio.CustomMinimumSize = new Vector2(96, 0); vazio.HorizontalAlignment = HorizontalAlignment.Center; linha.AddChild(vazio); }
        box.AddChild(linha);
        return box;
    }

    private static Color CorDoTipo(string tipo) => tipo switch
    {
        "mapa" => new Color(0.25f, 0.5f, 0.3f), "dungeon" => new Color(0.5f, 0.2f, 0.18f), "viagem" => new Color(0.2f, 0.42f, 0.6f),
        "poder" => new Color(0.45f, 0.25f, 0.6f), "classe" => new Color(0.65f, 0.45f, 0.12f), "historia" => new Color(0.45f, 0.35f, 0.25f),
        "merlin" => new Color(0.3f, 0.3f, 0.65f), "casa" => new Color(0.5f, 0.38f, 0.2f), _ => new Color(0.3f, 0.3f, 0.32f),
    };

    private static string NomeDoTipo(string tipo) => tipo switch
    {
        "mapa" => "REGIÃO", "dungeon" => "DUNGEON", "viagem" => "VIAGEM", "poder" => "PODER", "classe" => "CLASSE", "historia" => "HISTÓRIA",
        "merlin" => "MERLIN", "casa" => "CASA", "montaria" => "MONTARIA", "asas" => "ASAS", "capa" => "CAPA", "pet" => "COMPANHEIRO",
        "skin" => "SKIN", "presente" => "PRESENTE", _ => tipo.ToUpperInvariant(),
    };

    private Control Cartao(TrilhaItem i)
    {
        var laranja = new Color(1f, 0.6f, 0.2f);
        var dica = i.Tem ? "✓ Já é seu." : i.Disponivel ? "◆ Você já tem o nível, mas ainda não pegou." : "";
        var card = new PanelContainer { CustomMinimumSize = new Vector2(96, 0), TooltipText = $"{i.Titulo}\n{i.Detalhe}" + (dica.Length > 0 ? "\n" + dica : "") };
        var st = new StyleBoxFlat { BgColor = i.Tem ? new Color(0.12f, 0.1f, 0.05f, 0.95f) : i.Disponivel ? new Color(0.13f, 0.07f, 0.03f, 0.95f) : new Color(0.05f, 0.05f, 0.06f, 0.95f),
            BorderColor = i.Tem ? Gold : i.Disponivel ? laranja : new Color(0.3f, 0.29f, 0.27f), ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4 };
        st.SetBorderWidthAll(i.Tem || i.Disponivel ? 2 : 1); st.SetCornerRadiusAll(5);
        card.AddThemeStyleboxOverride("panel", st);
        var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", 2);
        card.AddChild(v);
        // arte: ícone do item / do poder; sem arte, um cartão na cor do tipo
        Texture2D? tex = null;
        if (i.Arte.StartsWith("item:", StringComparison.Ordinal)) { var id = i.Arte[5..]; tex = ItemIconRenderer.Get(_m?.ItemInfoFor(id), id); }
        else if (i.Arte.StartsWith("hab:", StringComparison.Ordinal)) tex = VisualAssets.Load("hab_" + i.Arte[4..] + ".png");
        Control arte;
        if (tex is not null)
            arte = new TextureRect { Texture = tex, CustomMinimumSize = new Vector2(84, 84), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        else
        {
            var tile = new PanelContainer { CustomMinimumSize = new Vector2(84, 84), MouseFilter = MouseFilterEnum.Ignore };
            var ts = new StyleBoxFlat { BgColor = CorDoTipo(i.Tipo), BorderColor = CorDoTipo(i.Tipo).Lightened(0.35f) };
            ts.SetBorderWidthAll(1); ts.SetCornerRadiusAll(4);
            tile.AddThemeStyleboxOverride("panel", ts);
            var nome = HudView.MakeLabel(NomeDoTipo(i.Tipo), 12, new Color(1f, 0.95f, 0.85f), 3);
            nome.HorizontalAlignment = HorizontalAlignment.Center; nome.VerticalAlignment = VerticalAlignment.Center;
            tile.AddChild(nome);
            arte = tile;
        }
        if (!i.Tem && !i.Disponivel) arte.Modulate = new Color(0.55f, 0.55f, 0.55f, 0.85f);
        v.AddChild(arte);
        var tipo = HudView.MakeLabel(i.Disponivel ? "◆ PEGAR (veja onde)" : NomeDoTipo(i.Tipo), 9, i.Tem ? Gold : i.Disponivel ? laranja : Dim, 2);
        tipo.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(tipo);
        var titulo = HudView.MakeLabel((i.Tem ? "✓ " : "") + i.Titulo, 11, i.Tem ? new Color(1f, 0.92f, 0.7f) : new Color(0.75f, 0.73f, 0.68f), 2);
        titulo.AutowrapMode = TextServer.AutowrapMode.WordSmart; titulo.HorizontalAlignment = HorizontalAlignment.Center;
        titulo.CustomMinimumSize = new Vector2(88, 0); titulo.MaxLinesVisible = 2;
        v.AddChild(titulo);
        return card;
    }
}
