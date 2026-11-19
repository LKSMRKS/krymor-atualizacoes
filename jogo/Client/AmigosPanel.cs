using System;
using System.Linq;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 18/11 (lista do dono, item 5): lista de amigos (F4). Campo para pedir amizade pelo nome do personagem; pedidos
/// recebidos com Aceitar/Recusar; amigos com bolinha verde (online) ou cinza, nível, classe e o mapa onde está.
/// Arrastável (regra: toda janela arrasta e fica dentro da tela).
/// </summary>
public sealed partial class AmigosPanel : PanelContainer
{
    private static readonly Color Gold = new(0.95f, 0.78f, 0.30f);
    private static readonly Color Dim = new(0.72f, 0.69f, 0.62f);
    private static readonly Color Verde = new(0.45f, 0.95f, 0.45f);
    private readonly VBoxContainer _lista = new();
    private readonly LineEdit _nome = new() { PlaceholderText = "Nome do personagem", CustomMinimumSize = new Vector2(250, 30) };
    private readonly Label _vazio = HudView.MakeLabel("Ninguém na lista ainda. Escreva o nome de um personagem e peça amizade.", 13, Dim);
    private int _seq = -1;
    public event Action<NetMessage>? Send;

    public AmigosPanel()
    {
        Visible = false;
        AddThemeStyleboxOverride("panel", HudView.PanelStyle(10f, 0.95f));
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.Both;
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        AddChild(col);
        var t = HudView.MakeLabel("🤝 Amigos", 22, Gold);
        if (HudView.TitleFont is not null) t.AddThemeFontOverride("font", HudView.TitleFont);
        col.AddChild(t);
        col.AddChild(HudView.MakeLabel("Quem está online, onde e em que nível. (F4 fecha; no chat: /amigo Nome)", 12, Dim));
        var add = new HBoxContainer();
        add.AddThemeConstantOverride("separation", 6);
        add.AddChild(_nome);
        var btn = Botao("Pedir amizade", () => Pedir());
        add.AddChild(btn);
        _nome.TextSubmitted += _ => Pedir();
        col.AddChild(add);
        _vazio.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _vazio.CustomMinimumSize = new Vector2(420, 0);
        col.AddChild(_vazio);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(440, 300) };
        _lista.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _lista.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_lista);
        col.AddChild(scroll);
    }

    public override void _Ready() => Arrastador.Ligar(this, "amigos", 40f, fechar: () => Visible = false);

    private void Pedir()
    {
        var n = _nome.Text.Trim();
        if (n.Length == 0) return;
        Send?.Invoke(new AmigosIntent("adicionar", n));
        _nome.Text = "";
        Sfx.Play("ui_clique");
    }

    private static Button Botao(string texto, Action acao)
    {
        var b = new Button { Text = texto, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 28) };
        b.AddThemeFontSizeOverride("font_size", 13);
        b.Pressed += acao;
        return b;
    }

    /// <summary>Atualiza quando chega lista nova do servidor.</summary>
    public void Atualizar(AmigosMessage? m, int seq)
    {
        if (m is null || seq == _seq) return;
        _seq = seq;
        foreach (var c in _lista.GetChildren()) c.QueueFree();
        _vazio.Visible = m.Amigos.Count == 0;
        // pedidos recebidos primeiro, depois amigos online, offline e por fim os pedidos que você mandou
        foreach (var a in m.Amigos.OrderBy(a => a.Estado == "recebido" ? 0 : a.Estado == "ok" ? (a.Online ? 1 : 2) : 3).ThenBy(a => a.Nome))
        {
            var linha = new PanelContainer();
            linha.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = a.Estado == "recebido" ? new Color(0.25f, 0.2f, 0.06f, 0.7f) : new Color(0.08f, 0.08f, 0.1f, 0.55f),
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4,
            });
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            linha.AddChild(row);
            row.AddChild(HudView.MakeLabel(a.Estado != "ok" ? "•" : a.Online ? "●" : "○", 16, a.Online && a.Estado == "ok" ? Verde : Dim));
            var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            info.AddThemeConstantOverride("separation", 0);
            info.AddChild(HudView.MakeLabel(a.Nome, 15, a.Estado == "ok" && a.Online ? Colors.White : Dim));
            var sub = a.Estado switch
            {
                "recebido" => "quer ser seu amigo",
                "enviado" => "pedido enviado — esperando aceitar",
                _ => $"Nv {a.Nivel} · {Classe(a.Classe)}" + (a.Online ? $" · {a.Mapa}" : " · offline"),
            };
            info.AddChild(HudView.MakeLabel(sub, 12, a.Estado == "recebido" ? Gold : Dim));
            row.AddChild(info);
            var nome = a.Nome;
            if (a.Estado == "recebido")
            {
                row.AddChild(Botao("Aceitar", () => Send?.Invoke(new AmigosIntent("aceitar", nome))));
                row.AddChild(Botao("Recusar", () => Send?.Invoke(new AmigosIntent("recusar", nome))));
            }
            else row.AddChild(Botao(a.Estado == "enviado" ? "Cancelar" : "Remover", () => Send?.Invoke(new AmigosIntent("remover", nome))));
            _lista.AddChild(linha);
        }
    }

    private static string Classe(string id) => id switch
    {
        "guerreiro" => "Guerreiro", "mago" => "Mago", "bruxa" => "Bruxa", "arqueiro" => "Arqueiro",
        _ => id.Length > 0 ? char.ToUpper(id[0]) + id[1..] : id,
    };
}
