using System;
using System.Linq;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 13/11 ("uma progressão para acompanhar o que você ganha em cada nível — montaria, TP... tipo um passe de batalha, numa
/// tecla; deixa o game bem mais imersivo"): a Trilha do Herói (F3). Uma linha do tempo do nível 1 ao 60: cada nível com o
/// que abre nele (região, dungeon, viagem, montaria, asas, poder, troca de classe, capítulo, Merlin). O que você já tem fica
/// dourado com ✓; o seu nível tem a faixa acesa; os próximos marcos aparecem em destaque em cima ("Próximo: ...").
/// </summary>
public sealed partial class TrilhaPanel : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.78f, 0.30f);
    private static readonly Color Dim = new(0.72f, 0.69f, 0.62f);
    private readonly VBoxContainer _lista = new();
    private readonly Label _proximo = HudView.MakeLabel("", 14, new Color(0.6f, 0.9f, 1f));
    private readonly ScrollContainer _scroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(560, 470) };
    private int _seq = -1;
    public bool Open => Visible;
    /// <summary>14/11 tarde: o miolo (o TAB o pega emprestado na aba Trilha).</summary>
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
        col.AddChild(HudView.MakeLabel("O que abre em cada nível: regiões, dungeons, viagens, montarias, asas, poderes, classe e história. (F3 fecha)", 12, Dim));
        _proximo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _proximo.CustomMinimumSize = new Vector2(560, 0);
        col.AddChild(_proximo);
        col.AddChild(_scroll);
        _lista.AddThemeConstantOverride("separation", 2);
        _lista.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(_lista);
    }

    public override void _Ready() => Arrastador.Ligar(this, "trilha", 40f, fechar: () => Visible = false);

    private static string Icone(string tipo) => tipo switch
    {
        "mapa" => "🗺", "dungeon" => "🏰", "viagem" => "⛵", "montaria" => "🐎", "asas" => "🪽", "poder" => "✦",
        "classe" => "⚔", "historia" => "📜", "merlin" => "🧙", "capa" => "🧣", "pet" => "🐾", "skin" => "🎭", "presente" => "🎁", "casa" => "🏠", _ => "★",
    };

    /// <summary>Atualiza quando chega a trilha nova do servidor.</summary>
    public void Atualizar(TrilhaMessage? t, int seq)
    {
        if (t is null || seq == _seq) return;
        _seq = seq;
        foreach (var c in _lista.GetChildren()) c.QueueFree();
        var prox = t.Itens.Where(i => !i.Tem && i.Nivel >= t.Nivel).Take(3).ToList();
        _proximo.Text = prox.Count == 0 ? "Você já abriu tudo da trilha. Renascer (nível 60) recomeça mais forte." :
            "Próximo: " + string.Join("   ·   ", prox.Select(p => $"Nv {p.Nivel} {Icone(p.Tipo)} {p.Titulo}"));
        ScrollContainer? alvo = null; Control? linhaAtual = null;
        foreach (var g in t.Itens.GroupBy(i => i.Nivel).OrderBy(g => g.Key))
        {
            var atual = g.Key <= t.Nivel && (t.Itens.Where(i => i.Nivel > g.Key).Select(i => i.Nivel).DefaultIfEmpty(99).Min() > t.Nivel);
            var bloco = new PanelContainer();
            var fundo = new StyleBoxFlat { BgColor = g.Key <= t.Nivel ? new Color(0.22f, 0.17f, 0.08f, 0.55f) : new Color(0.08f, 0.08f, 0.1f, 0.5f),
                BorderColor = atual ? Gold : new Color(0.3f, 0.26f, 0.2f, 0.6f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 };
            fundo.BorderWidthLeft = atual ? 3 : 1; fundo.BorderWidthTop = fundo.BorderWidthBottom = fundo.BorderWidthRight = atual ? 2 : 1;
            bloco.AddThemeStyleboxOverride("panel", fundo);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            bloco.AddChild(row);
            var nv = HudView.MakeLabel($"Nv {g.Key}", 16, g.Key <= t.Nivel ? Gold : Dim);
            nv.CustomMinimumSize = new Vector2(58, 0);
            row.AddChild(nv);
            var itens = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(itens);
            foreach (var i in g)
            {
                var l = HudView.MakeLabel($"{(i.Tem ? "✓" : "○")} {Icone(i.Tipo)} {i.Titulo}  —  {i.Detalhe}", 12,
                    i.Tem ? new Color(0.95f, 0.88f, 0.62f) : g.Key <= t.Nivel ? new Color(0.85f, 0.75f, 0.55f) : new Color(0.66f, 0.64f, 0.6f));
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                l.CustomMinimumSize = new Vector2(450, 0);
                itens.AddChild(l);
            }
            _lista.AddChild(bloco);
            if (atual) { linhaAtual = bloco; alvo = _scroll; }
        }
        if (alvo is not null && linhaAtual is not null)
            Callable.From(() => { if (IsInstanceValid(linhaAtual)) _scroll.EnsureControlVisible(linhaAtual); }).CallDeferred();
    }
}
