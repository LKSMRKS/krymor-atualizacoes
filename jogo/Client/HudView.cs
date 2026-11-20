using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Client;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// HUD compacto no estilo do lote 7: cartão do jogador (retrato, nome, nível e barras) no canto superior esquerdo, alvo no topo,
/// barra de habilidades (com ícones) embaixo e registro de combate curto no canto. Mais nameplates e números flutuantes.
/// Só desenha o que o ClientModel contém. Os ícones de golpe/guarda/ataque/poção vêm do mockup do lote 7 (provisórios).
/// </summary>
public sealed class HudView
{
    private static float SlotSize = 50f;   // muda com a escala da barra de baixo (HUD v2)
    private const float PlateWidth = 130f;
    private const float CardWidth = 300f;
    private const float SideColumnWidth = 250f;
    private const int LogLines = 4;
    private Control _logPanel = null!;
    private long _logChangedMs;

    private static readonly Color Ink = new(0.92f, 0.90f, 0.84f);
    // paleta séria (prancha "Proposta visual", 09/11): ferro e aço gastos; o "dourado" virou marfim apagado
    private static readonly Color Bronze = new(0.36f, 0.34f, 0.31f);
    private static readonly Color Gold = new(0.88f, 0.82f, 0.68f);

    private sealed class Slot
    {
        public StyleBoxFlat Style = null!;
        public ColorRect Cooldown = null!;
        public Control Icon = null!;
        public Label Glyph = null!;
        public TextureRect? Frame;
        public Label Count = null!;
        public Control? Painel;   // 18/11 15h: o quadrado (dica com o nome do poder)
    }

    private sealed class Plate
    {
        public Control Root = null!;
        public Label Name = null!;
        public ProgressBar Bar = null!;
        public Control BarHolder = null!;
        public bool Seen;
    }

    private sealed class Floater
    {
        public Label Label = null!;
        public Vector3 World;
        public double Age;
    }

    public Control Root { get; }

    private const float OrbSize = 112f;
    private readonly Label _name, _levelText, _classText, _buffText, _xpText, _coinsText, _targetName, _prompt, _notice, _zone, _bossName, _hint;
    private readonly ProgressBar _xp, _targetBar, _bossBar, _stamina;
    private double _staminaShown = 100, _staminaLow;
    private readonly OrbeHud _hpOrb, _resOrb;
    private readonly BarraAlvo _alvo;
    private readonly PlacaZona _placaZona;
    private VBoxContainer? _leftCol;
    private BarraInferior _barra = null!;
    private Control _barraVaga = null!;
    private readonly CompassStrip _compass;
    private readonly VBoxContainer _questBox, _feedBox;
    private readonly Control _questPanel;
    private readonly ColorRect _vignette;
    private readonly List<(Control Node, double Age)> _feed = new();
    private string _questSig = "";
    private double _vignetteAlpha;
    private string? _introBoss;
    private double _introAge = 99;
    private readonly VBoxContainer _bossIntro = new() { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
    private readonly Label _bossIntroName = MakeLabel("", 46, new Color(0.95f, 0.78f, 0.35f));
    private readonly Label _bossIntroSub = MakeLabel("", 18, new Color(0.85f, 0.8f, 0.7f));
    public float CameraYaw { set { _compass.Yaw = value; MiniMap.Yaw = value; } }
    public readonly MiniMapa MiniMap = new();
    public Action? AbrirBusca;
    private readonly VBoxContainer _grupoBox = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
    /// <summary>14/11: botão x1 da lista do grupo (o Main manda o desafio).</summary>
    public event Action<string>? Desafiar;
    private string _grupoSig = "";

    private void UpdateGrupo(CinzasDaMare.Shared.Client.ClientModel m)
    {
        var membros = m.Party?.Members.Where(x => x.CharacterId != m.CharacterId).ToList() ?? new();
        var sig = string.Join("|", membros.Select(x => $"{x.Name}{x.Level}{(int)(x.Health / Math.Max(1, x.MaxHealth) * 20)}{x.MapName}{x.Online}"));
        if (sig == _grupoSig) return;
        _grupoSig = sig;
        foreach (var c in _grupoBox.GetChildren()) { _grupoBox.RemoveChild(c); c.QueueFree(); }
        foreach (var x in membros)
        {
            var pct = x.MaxHealth > 0 ? x.Health / x.MaxHealth : 0;
            var cor = !x.Online ? new Color(0.55f, 0.55f, 0.55f) : pct < 0.3 ? new Color(1f, 0.4f, 0.35f) : new Color(0.55f, 0.95f, 0.6f);
            // 14/11 19h (dono: "dois nomes dos jogadores — fica só o de barra, embaixo do minimapa"): nome + barra de vida
            // 14/11 21h (dono: "a vida do parceiro ficou muito grande — até o limite do minimapa"): largura do minimapa
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(LarguraGrupo, 0), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
            box.AddThemeConstantOverride("separation", 1);
            // 14/11 22h (dono: "embaixo do minimapa só o nome, o nível e a barra de vida de quem está no grupo")
            var linha = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            var nm = MakeLabel($"{x.Name} · nv {x.Level}" + (!x.Online ? " (fora)" : x.MapName != (m.Map?.DisplayName ?? "") ? $" · {x.MapName}" : ""), 12,
                x.Online ? new Color(0.55f, 0.75f, 1f) : new Color(0.55f, 0.55f, 0.55f), 3);
            nm.ClipText = true; nm.CustomMinimumSize = new Vector2(LarguraGrupo - 36, 0);
            linha.AddChild(nm);
            box.AddChild(linha);
            var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(LarguraGrupo, 5), Value = pct, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
            bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = cor });
            bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.06f, 0.85f) });
            box.AddChild(bar);
            _grupoBox.AddChild(box);
        }
    }

    private const float LarguraGrupo = 180f;   // = largura do minimapa (190) menos a margem
    /// <summary>14/11 21h: convite de grupo pela lista de quem está por perto.</summary>
    public event Action<string>? ConvidarGrupo;

    private static Button BotaoMini(string texto, Color fundo, Color borda, string dica, Action clique)
    {
        var b = new Button { Text = texto, TooltipText = dica, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(texto.Length > 2 ? 40 : 26, 18), MouseFilter = Control.MouseFilterEnum.Stop };
        b.AddThemeFontSizeOverride("font_size", 11);
        var bx = new StyleBoxFlat { BgColor = fundo, BorderColor = borda };
        bx.SetBorderWidthAll(1); bx.SetCornerRadiusAll(3); bx.ContentMarginLeft = bx.ContentMarginRight = 3;
        b.AddThemeStyleboxOverride("normal", bx);
        b.Pressed += () => { Sfx.Play("ui_clique"); clique(); };
        return b;
    }
    // 14/11 ("quando sentar, coloca tipo uma bolinha carregando para totalizar o buff"): anel que enche em cima da barra
    private AnelDescanso? _anelDescanso;
    public void Descanso(float? t, bool cama, Vector2? pes = null)
    {
        if (_anelDescanso is null)
        {
            // 18/11 19h (dono: "o descansando ficou bem em cima do boneco — coloca menor, embaixo do pé, um pouco
            // transparente pra ficar clean"): bolinha pequena (40 px) seguindo os pés na tela, 70% visível
            _anelDescanso = new AnelDescanso { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(40, 40), Size = new Vector2(40, 40), Modulate = new Color(1, 1, 1, 0.72f) };
            Root.AddChild(_anelDescanso);
        }
        _anelDescanso.Visible = t is not null && pes is not null;
        if (pes is { } p) _anelDescanso.Position = p + new Vector2(-20f, 14f);
        if (t is { } v) { _anelDescanso.T = v; _anelDescanso.Cama = cama; _anelDescanso.QueueRedraw(); }
    }

    // 14/11 ("o N, antes de tirar o mapa, muda o tamanho: 3 tamanhos, esse de agora no meio, um maior e um menor"):
    // médio → grande → pequeno → desligado → médio
    private int _minimapaPasso;
    private static readonly float[] MinimapaTamanhos = { 190f, 280f, 130f };
    public void ToggleMinimap()
    {
        _minimapaPasso = (_minimapaPasso + 1) % 4;
        MiniMap.Visible = _minimapaPasso < 3;
        if (_minimapaPasso < 3)
        {
            var t = MinimapaTamanhos[_minimapaPasso];
            MiniMap.CustomMinimumSize = new Vector2(t, t);
            MiniMap.Size = new Vector2(t, t);
        }
    }
    /// <summary>Missões no canto, na ordem dos números (o mapa M usa a mesma numeração).</summary>
    public static List<CinzasDaMare.Shared.Net.QuestEntry> Tracked { get; private set; } = new();
    public static List<CinzasDaMare.Shared.Net.QuestEntry> NoMapa { get; private set; } = new();
    public string ClockText { set => _compass.Clock = value; }
    private CenterContainer _promptRow = null!;
    private void PosicionarAviso()
    {
        var h = _promptPanel.GetCombinedMinimumSize().Y;
        var topo = _barra.Visible ? _barra.GetGlobalRect().Position.Y : Root.Size.Y - 40f;
        _promptRow.Size = new Vector2(Root.Size.X, h);
        _promptRow.Position = new Vector2(0, Mathf.Round(topo - h - 14f));
    }
    private readonly Control _targetPanel, _promptPanel, _death, _crosshair, _bossPanel;
    private readonly Label _promptItem;
    private readonly PanelContainer _promptItemPanel;
    private readonly Label _promptItemSub;
    private readonly StyleBoxFlat _promptItemEstilo;
    /// <summary>13/11: cor da raridade do item do chão em foco (nome e borda da plaquinha).</summary>
    public Color PromptCor { get; set; } = new(0.93f, 0.9f, 0.82f);
    /// <summary>13/11: posição na tela do item no chão (o quadro dele fica ao lado do item, não no meio da tela).</summary>
    public Vector2? PromptAncora { get; set; }
    private readonly Container _hotbar;
    /// <summary>11/11: HUD com a barra da gárgula (os quadros já vêm desenhados na arte).</summary>
    public static bool GargulaMode { get; private set; }
    private readonly Control _portraitHolder;
    private string _slotsClass = "";
    private double _noticeAge = 99, _zoneAge = 99;
    private readonly List<Slot> _slots = new();
    private readonly Dictionary<string, Plate> _plates = new();
    private readonly List<Floater> _floaters = new();

    // ------------------------------------------------------------ estilo (também usado pela tela de login)

    public static StyleBoxFlat PanelFlat(float margin = 8f, float alpha = 0.84f)
    {
        // carvão #24262D com borda de bronze (pacote visual v1)
        var style = new StyleBoxFlat { BgColor = new Color(0.09f, 0.09f, 0.11f, alpha), BorderColor = Bronze };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        style.SetContentMarginAll(margin);
        style.ShadowColor = new Color(0, 0, 0, 0.35f * alpha);
        style.ShadowSize = 4;
        return style;
    }

    /// <summary>Janela grande (menus): borda dupla de bronze e sombra.</summary>
    public static StyleBoxFlat WindowFlat(float margin = 14f)
    {
        var style = new StyleBoxFlat { BgColor = new Color(0.10f, 0.10f, 0.12f, 1f), BorderColor = new Color(0.62f, 0.47f, 0.24f) };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(margin);
        style.ShadowColor = new Color(0, 0, 0, 0.55f);
        style.ShadowSize = 10;
        return style;
    }

    /// <summary>Cartão dentro de uma janela (missão, item de loja).</summary>
    public static StyleBoxFlat CardFlat(bool highlight = false)
    {
        var style = new StyleBoxFlat
        {
            BgColor = highlight ? new Color(0.18f, 0.15f, 0.10f, 0.95f) : new Color(0.14f, 0.14f, 0.16f, 0.92f),
            BorderColor = highlight ? Gold : new Color(0.36f, 0.30f, 0.22f),
        };
        style.SetBorderWidthAll(highlight ? 2 : 1);
        style.SetCornerRadiusAll(3);
        style.SetContentMarginAll(8);
        return style;
    }

    // ---- molduras com textura (Tools/gerar_moldura.py): ferro escuro, friso dourado, esmeraldas nos cantos (MU/Diablo) ----

    private static StyleBoxTexture? Nine(string file, int margin, float content, float alpha, Color? tint = null)
    {
        if (VisualAssets.Load(file) is not { } tex) return null;
        var sb = new StyleBoxTexture { Texture = tex, ModulateColor = (tint ?? Colors.White) with { A = alpha } };
        sb.TextureMarginLeft = sb.TextureMarginRight = sb.TextureMarginTop = sb.TextureMarginBottom = margin;
        sb.SetContentMarginAll(content);
        return sb;
    }

    /// <summary>Painel da HUD (missões, registro, alvo): moldura fina de ferro e ouro sobre couro escuro.</summary>
    public static StyleBox PanelStyle(float margin = 8f, float alpha = 0.84f)
        => alpha < 0.05f ? PanelFlat(margin, alpha) : (StyleBox?)Nine("ui_moldura_fina.png", 10, Mathf.Max(margin, 9f), Mathf.Min(1f, alpha + 0.1f)) ?? PanelFlat(margin, alpha);

    /// <summary>Janela grande (menus): moldura grossa com cantos de esmeralda.</summary>
    public static StyleBox WindowStyle(float margin = 14f)
        => (StyleBox?)Nine("ui_moldura.png", 26, Mathf.Max(margin, 22f), 1f) ?? WindowFlat(margin);

    /// <summary>Cartão dentro de uma janela (missão, item de loja); destacado = borda dourada mais quente.</summary>
    public static StyleBox CardStyle(bool highlight = false)
        => (StyleBox?)Nine("ui_moldura_fina.png", 10, 10f, 1f, highlight ? new Color(1.25f, 1.1f, 0.85f) : null) ?? CardFlat(highlight);

    // ---- fontes (OFL, Assets/Fonts): Cinzel nos títulos e botões (cara de MU), Alegreya Sans no texto ----
    // serifa clássica (EB Garamond) nos títulos, nomes e números grandes — mais sóbria que a Cinzel
    public static FontFile? TitleFont { get; } = LoadFont("eb-garamond-latin-600-normal.woff");
    public static FontFile? BodyFont { get; } = LoadFont("alegreya-sans-latin-500-normal.woff");

    private static FontFile? LoadFont(string file)
    {
        var path = "res://Assets/Fonts/" + file;
        try
        {
            if (!Godot.FileAccess.FileExists(path)) return null;
            var bytes = Godot.FileAccess.GetFileAsBytes(path);
            return bytes.Length == 0 ? null : new FontFile { Data = bytes, Hinting = TextServer.Hinting.Light, MultichannelSignedDistanceField = false };
        }
        catch { return null; }
    }

    /// <summary>Tema global: botões escuros com borda de bronze (dourado ao passar o mouse), barras de rolagem discretas.</summary>
    public static Theme BuildTheme()
    {
        var theme = new Theme();
        StyleBoxFlat Btn(Color bg, Color border, int width)
        {
            var b = new StyleBoxFlat { BgColor = bg, BorderColor = border };
            b.SetBorderWidthAll(width);
            b.SetCornerRadiusAll(3);
            b.ContentMarginLeft = 12; b.ContentMarginRight = 12; b.ContentMarginTop = 5; b.ContentMarginBottom = 5;
            return b;
        }
        theme.SetStylebox("normal", "Button", (StyleBox?)Nine("ui_botao.png", 8, 10f, 1f) ?? Btn(new Color(0.16f, 0.14f, 0.13f), new Color(0.50f, 0.38f, 0.20f), 1));
        theme.SetStylebox("hover", "Button", (StyleBox?)Nine("ui_botao_hover.png", 8, 10f, 1f) ?? Btn(new Color(0.24f, 0.19f, 0.12f), Gold, 2));
        theme.SetStylebox("pressed", "Button", (StyleBox?)Nine("ui_botao_press.png", 8, 10f, 1f) ?? Btn(new Color(0.34f, 0.25f, 0.12f), Gold, 2));
        theme.SetStylebox("hover_pressed", "Button", (StyleBox?)Nine("ui_botao_press.png", 8, 10f, 1f) ?? Btn(new Color(0.34f, 0.25f, 0.12f), Gold, 2));
        if (BodyFont is not null) { theme.DefaultFont = BodyFont; theme.DefaultFontSize = 15; }
        if (TitleFont is not null) theme.SetFont("font", "Button", TitleFont);
        theme.SetColor("font_outline_color", "Button", Colors.Black);
        theme.SetConstant("outline_size", "Button", 3);
        theme.SetStylebox("panel", "PanelContainer", PanelStyle(8f, 0.84f));
        theme.SetStylebox("panel", "TooltipPanel", PanelStyle(8f, 0.95f));
        theme.SetStylebox("focus", "Button", Btn(new Color(0, 0, 0, 0), new Color(0.95f, 0.78f, 0.30f, 0.5f), 1));
        theme.SetStylebox("disabled", "Button", Btn(new Color(0.11f, 0.11f, 0.12f), new Color(0.25f, 0.23f, 0.21f), 1));
        theme.SetColor("font_color", "Button", new Color(0.95f, 0.90f, 0.78f));
        theme.SetColor("font_hover_color", "Button", new Color(1f, 0.88f, 0.55f));
        theme.SetColor("font_disabled_color", "Button", new Color(0.45f, 0.43f, 0.40f));
        theme.SetFontSize("font_size", "Button", 14);
        var line = new StyleBoxFlat { BgColor = new Color(0.07f, 0.07f, 0.08f), BorderColor = new Color(0.45f, 0.35f, 0.20f) };
        line.SetBorderWidthAll(1); line.SetCornerRadiusAll(3); line.SetContentMarginAll(6);
        theme.SetStylebox("normal", "LineEdit", line);
        var sep = new StyleBoxLine { Color = new Color(0.45f, 0.35f, 0.20f, 0.6f), Thickness = 1 };
        theme.SetStylebox("separator", "HSeparator", sep);
        var grab = new StyleBoxFlat { BgColor = new Color(0.45f, 0.35f, 0.20f) };
        grab.SetCornerRadiusAll(3);
        theme.SetStylebox("grabber", "VScrollBar", grab);
        theme.SetStylebox("grabber_highlight", "VScrollBar", grab);
        theme.SetStylebox("scroll", "VScrollBar", new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.06f, 0.6f) });
        // 13/11 ("uma linguagem única de interface"): o mesmo desenho para TODO tipo de controle — o que não tinha estilo
        // caía no cinza padrão do Godot (abas, listas, caixas de marcar, deslizadores, menus, campos de texto grandes)
        var ouro = new Color(0.95f, 0.78f, 0.30f); var bronze = new Color(0.50f, 0.38f, 0.20f);
        var fundo = new Color(0.10f, 0.09f, 0.08f, 0.96f); var texto = new Color(0.93f, 0.89f, 0.80f); var apagado = new Color(0.66f, 0.62f, 0.55f);
        StyleBoxFlat Caixa(Color bg, Color borda, int w = 1, int raio = 4, float m = 6)
        {
            var b = new StyleBoxFlat { BgColor = bg, BorderColor = borda };
            b.SetBorderWidthAll(w); b.SetCornerRadiusAll(raio); b.SetContentMarginAll(m);
            return b;
        }
        theme.SetColor("font_color", "Label", texto);
        theme.SetColor("font_color", "LineEdit", texto);
        theme.SetColor("font_placeholder_color", "LineEdit", apagado);
        theme.SetStylebox("focus", "LineEdit", Caixa(new Color(0, 0, 0, 0), ouro));
        foreach (var tipo in new[] { "TextEdit", "CodeEdit" })
        {
            theme.SetStylebox("normal", tipo, Caixa(new Color(0.06f, 0.06f, 0.07f), bronze));
            theme.SetStylebox("focus", tipo, Caixa(new Color(0, 0, 0, 0), ouro));
            theme.SetColor("font_color", tipo, texto);
        }
        theme.SetStylebox("panel", "ItemList", Caixa(new Color(0.06f, 0.06f, 0.07f), bronze));
        theme.SetStylebox("selected", "ItemList", Caixa(new Color(0.34f, 0.25f, 0.12f), ouro, 1, 3, 2));
        theme.SetStylebox("selected_focus", "ItemList", Caixa(new Color(0.34f, 0.25f, 0.12f), ouro, 1, 3, 2));
        theme.SetStylebox("hovered", "ItemList", Caixa(new Color(0.20f, 0.16f, 0.10f), bronze, 0, 3, 2));
        theme.SetColor("font_color", "ItemList", texto);
        theme.SetColor("font_selected_color", "ItemList", new Color(1f, 0.92f, 0.65f));
        // abas
        theme.SetStylebox("tab_selected", "TabBar", Caixa(new Color(0.24f, 0.19f, 0.12f), ouro, 1, 4, 8));
        theme.SetStylebox("tab_unselected", "TabBar", Caixa(new Color(0.12f, 0.11f, 0.10f), bronze, 1, 4, 8));
        theme.SetStylebox("tab_hovered", "TabBar", Caixa(new Color(0.18f, 0.15f, 0.11f), ouro, 1, 4, 8));
        theme.SetStylebox("tab_selected", "TabContainer", Caixa(new Color(0.24f, 0.19f, 0.12f), ouro, 1, 4, 8));
        theme.SetStylebox("tab_unselected", "TabContainer", Caixa(new Color(0.12f, 0.11f, 0.10f), bronze, 1, 4, 8));
        theme.SetStylebox("tab_hovered", "TabContainer", Caixa(new Color(0.18f, 0.15f, 0.11f), ouro, 1, 4, 8));
        theme.SetStylebox("panel", "TabContainer", PanelStyle(8f, 0.9f));
        foreach (var tipo in new[] { "TabBar", "TabContainer" })
        {
            theme.SetColor("font_selected_color", tipo, new Color(1f, 0.9f, 0.6f));
            theme.SetColor("font_unselected_color", tipo, apagado);
            theme.SetColor("font_hovered_color", tipo, texto);
        }
        // menus e listas suspensas
        theme.SetStylebox("panel", "PopupMenu", Caixa(fundo, bronze, 1, 4, 4));
        theme.SetStylebox("hover", "PopupMenu", Caixa(new Color(0.30f, 0.22f, 0.11f), ouro, 0, 3, 2));
        theme.SetColor("font_color", "PopupMenu", texto);
        theme.SetColor("font_hover_color", "PopupMenu", new Color(1f, 0.9f, 0.6f));
        theme.SetStylebox("normal", "OptionButton", Btn(new Color(0.16f, 0.14f, 0.13f), bronze, 1));
        theme.SetStylebox("hover", "OptionButton", Btn(new Color(0.24f, 0.19f, 0.12f), ouro, 1));
        theme.SetStylebox("pressed", "OptionButton", Btn(new Color(0.34f, 0.25f, 0.12f), ouro, 1));
        theme.SetColor("font_color", "OptionButton", texto);
        // caixas de marcar e deslizadores
        theme.SetColor("font_color", "CheckBox", texto);
        theme.SetColor("font_hover_color", "CheckBox", new Color(1f, 0.9f, 0.6f));
        theme.SetColor("font_pressed_color", "CheckBox", new Color(1f, 0.9f, 0.6f));
        foreach (var est in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" }) theme.SetStylebox(est, "CheckBox", new StyleBoxEmpty());
        var trilho = Caixa(new Color(0.06f, 0.06f, 0.07f), bronze, 1, 3, 2); trilho.ContentMarginTop = trilho.ContentMarginBottom = 3;
        theme.SetStylebox("slider", "HSlider", trilho);
        theme.SetStylebox("grabber_area", "HSlider", Caixa(new Color(0.55f, 0.42f, 0.20f), ouro, 0, 3, 2));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Caixa(new Color(0.75f, 0.58f, 0.25f), ouro, 0, 3, 2));
        theme.SetStylebox("grabber", "HScrollBar", grab);
        theme.SetStylebox("grabber_highlight", "HScrollBar", grab);
        theme.SetStylebox("scroll", "HScrollBar", new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.06f, 0.6f) });
        // barra de progresso padrão (as da HUD têm a sua)
        theme.SetStylebox("background", "ProgressBar", Caixa(new Color(0.05f, 0.05f, 0.06f, 0.92f), new Color(0.30f, 0.25f, 0.18f), 1, 2, 0));
        theme.SetStylebox("fill", "ProgressBar", Caixa(new Color(0.75f, 0.58f, 0.25f), new Color(0, 0, 0, 0), 0, 2, 0));
        theme.SetColor("font_color", "TooltipLabel", texto);
        theme.SetStylebox("panel", "PopupPanel", PanelStyle(8f, 0.95f));
        theme.SetStylebox("panel", "Panel", PanelStyle(8f, 0.9f));
        return theme;
    }

    public static Label MakeLabel(string text, int size = 15, Color? color = null, int outline = 4)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", outline);
        if (size >= 18 && TitleFont is not null) label.AddThemeFontOverride("font", TitleFont);
        return label;
    }

    private static ProgressBar MakeBar(Color fill, Vector2 size)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            CustomMinimumSize = size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var background = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.06f, 0.92f), BorderColor = new Color(0.30f, 0.25f, 0.18f) };
        background.SetBorderWidthAll(1);
        background.SetCornerRadiusAll(2);
        var fillStyle = new StyleBoxFlat { BgColor = fill };
        fillStyle.SetCornerRadiusAll(2);
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", fillStyle);
        return bar;
    }

    /// <summary>Barra com o texto "valor / máximo" desenhado dentro dela.</summary>
    private static Control BarWithText(Color fill, Vector2 size, out ProgressBar bar, out Label text, int fontSize = 11)
    {
        bar = MakeBar(fill, size);
        text = MakeLabel("", fontSize, null, 3);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(text);
        text.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return bar;
    }

    private static Control Spacer()
        => new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

    private static PanelContainer Framed(Control content, float margin = 6f, float alpha = 0.84f)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(margin, alpha));
        panel.AddChild(content);
        return panel;
    }

    // ------------------------------------------------------------ construção

    private readonly StatusVinheta _vinheta;

    // 13/11: caixa "Parabéns, você ganhou..." — pequena, no meio, arrastável, com OK e o X de todas as janelas
    private PanelContainer? _ganhou;
    private Label? _ganhouTitulo, _ganhouTexto;

    /// <summary>13/11 ("apareceu o Parabéns mas o mouse não está na tela"): a caixa aberta solta o mouse; fechada, avisa o Main.</summary>
    public bool GanhouAberto => _ganhou is { Visible: true };
    public event Action? GanhouFechou;

    private void FecharGanhou()
    {
        if (_ganhou is null) return;
        _ganhou.Visible = false;
        if (_ganhosPendentes) return;   // tem outro na fila: abre já, com o mouse solto
        GanhouFechou?.Invoke();
    }

    private bool _ganhosPendentes;
    public void FecharGanhouPorFora() { FecharGanhou(); Sfx.Play("ui_fechar"); }

    private void AtualizarGanhou(ClientModel m)
    {
        _ganhosPendentes = m.Ganhos.Count > 0;
        if (_ganhou is { Visible: true } || m.Ganhos.Count == 0) return;
        if (_death.Visible) return;   // 15/11: morto, a caixa espera o renascer (não aparece por cima do "Você foi derrotado")
        if (_ganhou is null)
        {
            _ganhou = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(420, 0) };
            _ganhou.AddThemeStyleboxOverride("panel", PanelStyle(16f, 0.95f));
            var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            col.AddThemeConstantOverride("separation", 10);
            _ganhou.AddChild(col);
            _ganhouTitulo = MakeLabel("", 22, new Color(0.98f, 0.82f, 0.35f), 3);
            _ganhouTitulo.HorizontalAlignment = HorizontalAlignment.Center;
            if (TitleFont is not null) _ganhouTitulo.AddThemeFontOverride("font", TitleFont);
            col.AddChild(_ganhouTitulo);
            _ganhouTexto = MakeLabel("", 15, new Color(0.92f, 0.9f, 0.85f), 2);
            _ganhouTexto.HorizontalAlignment = HorizontalAlignment.Center;
            _ganhouTexto.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _ganhouTexto.CustomMinimumSize = new Vector2(380, 0);
            col.AddChild(_ganhouTexto);
            var ok = new Button { Text = "OK", CustomMinimumSize = new Vector2(120, 34), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, FocusMode = Control.FocusModeEnum.None };
            ok.Pressed += () => { FecharGanhou(); Sfx.Play("ui_clique"); };
            col.AddChild(ok);
            Root.AddChild(_ganhou);
            Arrastador.Ligar(_ganhou, "ganhou", 40f, fechar: FecharGanhou);
        }
        var (titulo, texto) = m.Ganhos.Dequeue();
        _ganhosPendentes = m.Ganhos.Count > 0;
        _ganhouTitulo!.Text = titulo;
        _ganhouTexto!.Text = texto;
        _ganhou.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;   // mouse na tela para clicar no OK
        _ganhou.ResetSize();
        var view = Root.GetViewportRect().Size;
        _ganhou.Position = new Vector2((view.X - Mathf.Max(420f, _ganhou.Size.X)) / 2f, view.Y * 0.32f) + Arrastador.Desvio("ganhou");
        Sfx.Play("nivel", -6f);
    }
    private readonly Control _placas;

    /// <summary>13/11: missões fixadas escondidas pelo jogador (J → Minimizar); guardado em user://hud.cfg.</summary>
    public static bool MissoesMinimizadas
    {
        get => _mini ??= Godot.FileAccess.FileExists("user://hud_missoes_min") ;
        set
        {
            _mini = value;
            try
            {
                if (value) { using var f = Godot.FileAccess.Open("user://hud_missoes_min", Godot.FileAccess.ModeFlags.Write); f?.StoreString("1"); }
                else if (Godot.FileAccess.FileExists("user://hud_missoes_min")) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://hud_missoes_min"));
            }
            catch (Exception) { }
        }
    }
    private static bool? _mini;

    public HudView()
    {
        Root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _vinheta = new StatusVinheta();
        Root.AddChild(_vinheta);
        _vinheta.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // 13/11 ("o nome dos NPCs fica na frente do quadro de missões fixadas; coloca sempre atrás dos quadros"): as placas
        // de nome ficam numa camada própria logo no começo — todo quadro do HUD é desenhado por cima delas
        _placas = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Name = "Placas" };
        Root.AddChild(_placas);
        _placas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 12);
        Root.AddChild(margin);

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddChild(column);

        // ---- linha de cima: cartão do jogador | bússola + alvo | missões acompanhadas ----
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(top);

        _portraitHolder = new Control { CustomMinimumSize = new Vector2(58f, 58f), MouseFilter = Control.MouseFilterEnum.Ignore };
        var info = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(CardWidth - 58f - 8f - 14f, 0f) };
        info.AddThemeConstantOverride("separation", 1);
        var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _name = MakeLabel("", 17, Gold);
        _name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _levelText = MakeLabel("", 14);
        header.AddChild(_name);
        header.AddChild(_levelText);
        info.AddChild(header);
        _classText = MakeLabel("", 12, new Color(0.78f, 0.72f, 0.60f), 3);
        info.AddChild(_classText);
        _buffText = MakeLabel("", 11, new Color(0.55f, 0.85f, 0.55f), 3);
        info.AddChild(_buffText);
        var cardRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        cardRow.AddThemeConstantOverride("separation", 8);
        cardRow.AddChild(_portraitHolder);
        cardRow.AddChild(info);
        var card = Framed(cardRow, 6f);
        card.CustomMinimumSize = new Vector2(CardWidth, 0f);
        card.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        // placa da área embaixo do cartão (HUD v2): "COSTA DO CAL" com a âncora
        var leftCol = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        leftCol.AddThemeConstantOverride("separation", 6);
        // 11/11 noite (pedido): sem o cartão do personagem no alto — fica a placa da região e, embaixo dela, o minimapa
        // (as missões fixadas sobem para o canto direito)
        card.Visible = false;
        leftCol.AddChild(card);
        _placaZona = new PlacaZona { CustomMinimumSize = new Vector2(CardWidth, 38f), MouseFilter = Control.MouseFilterEnum.Ignore };
        leftCol.AddChild(_placaZona);
        leftCol.AddChild(_casaSub);   // 18/11 19h: conforto da casa embaixo do nome do lugar
        _casaSub.Visible = false;
        _leftCol = leftCol;
        top.AddChild(leftCol);

        top.AddChild(Spacer());

        var centerTop = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centerTop.AddThemeConstantOverride("separation", 14);
        _compass = new CompassStrip { CustomMinimumSize = new Vector2(380f, 34f), MouseFilter = Control.MouseFilterEnum.Ignore };
        centerTop.AddChild(_compass);
        // alvo (HUD v2): nome em serifa e a barra de ferro com a vida dentro (ui_barra_alvo)
        _targetName = MakeLabel("", 14);
        _targetBar = MakeBar(new Color(0.78f, 0.15f, 0.12f), new Vector2(300f, 9f));
        _alvo = new BarraAlvo { CustomMinimumSize = new Vector2(440f, 52f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _targetPanel = _alvo;
        centerTop.AddChild(_targetPanel);
        // 17/11 (dono: "bússola e o sangue grande quando mira no monstro não estão centralizados"): antes ficavam entre a
        // coluna da esquerda e a da direita (larguras diferentes) e escorregavam para a direita; agora presos no meio da tela
        Root.AddChild(centerTop);
        centerTop.AnchorLeft = 0.5f; centerTop.AnchorRight = 0.5f; centerTop.AnchorTop = 0f; centerTop.AnchorBottom = 0f;
        centerTop.OffsetLeft = -220f; centerTop.OffsetRight = 220f; centerTop.OffsetTop = 12f; centerTop.OffsetBottom = 112f;
        centerTop.GrowHorizontal = Control.GrowDirection.Both;
        centerTop.Alignment = BoxContainer.AlignmentMode.Begin;
        _compass.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _targetPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        top.AddChild(Spacer());

        _questBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _questBox.AddThemeConstantOverride("separation", 2);
        _questPanel = Framed(_questBox, 8f, 0.72f);
        _questPanel.CustomMinimumSize = new Vector2(CardWidth, 0f);
        _questPanel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        // 11/11: minimapa em cima das missões (tecla N liga/desliga)
        var rightCol = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        rightCol.AddThemeConstantOverride("separation", 6);
        var mapRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin };
        mapRow.AddChild(MiniMap);
        _leftCol!.AddChild(mapRow);
        // 13/11 ("coloca o nome do amigo da party embaixo do minimapa"): grupo com vida e onde está
        _grupoBox.AddThemeConstantOverride("separation", 1);
        _leftCol.AddChild(_grupoBox);
        rightCol.AddChild(_questPanel);
        top.AddChild(rightCol);

        // ---- meio: espaço livre para o jogo ----
        column.AddChild(new Control { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // ---- aviso de interação ----
        var promptRow = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _prompt = MakeLabel("", 15, Gold);
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _promptPanel = Framed(_prompt, 7f, 0.55f);   // 13/11: caixa mais sutil (o texto continua forte)
        promptRow.AddChild(_promptPanel);
        _promptRow = promptRow;   // 14/11 18h: fora da coluna (ver abaixo, depois da barra)
        // 13/11 ("a informação do item no chão aparece no meio da tela, em cima do boneco — joga para a diagonal do item"):
        // quadro flutuante pequeno ao lado do item (em cima e à direita dele; à esquerda se não couber)
        // 13/11 (2ª vez: "ainda é um quadradão feio"): plaquinha arredondada e escura, borda fina na cor da raridade, tecla F
        _promptItem = MakeLabel("", 14, Gold, 4);
        _promptItemSub = MakeLabel("", 11, new Color(0.85f, 0.82f, 0.74f), 3);
        var tecla = MakeLabel("F", 12, new Color(0.1f, 0.08f, 0.05f));
        tecla.HorizontalAlignment = HorizontalAlignment.Center; tecla.VerticalAlignment = VerticalAlignment.Center;
        tecla.CustomMinimumSize = new Vector2(18, 18);
        var teclaFundo = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var tf = new StyleBoxFlat { BgColor = new Color(0.95f, 0.8f, 0.4f, 0.95f) }; tf.SetCornerRadiusAll(4);
        teclaFundo.AddThemeStyleboxOverride("panel", tf);
        teclaFundo.AddChild(tecla);
        var linhas = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        linhas.AddThemeConstantOverride("separation", 0);
        linhas.AddChild(_promptItem); linhas.AddChild(_promptItemSub);
        var fila = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        fila.AddThemeConstantOverride("separation", 7);
        fila.AddChild(teclaFundo); fila.AddChild(linhas);
        _promptItemPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _promptItemEstilo = new StyleBoxFlat { BgColor = new Color(0.035f, 0.03f, 0.025f, 0.78f), ContentMarginLeft = 7, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 5 };
        _promptItemEstilo.SetCornerRadiusAll(7); _promptItemEstilo.SetBorderWidthAll(1);
        _promptItemPanel.AddThemeStyleboxOverride("panel", _promptItemEstilo);
        _promptItemPanel.AddChild(fila);
        _promptItemPanel.Visible = false;
        _promptItemPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        Root.AddChild(_promptItemPanel);

        // ---- linha de baixo: dicas | pote de vida · habilidades + XP · pote de mana/fôlego | moedas, ganhos e registro ----
        var bottom = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(bottom);

        var hintBox = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(SideColumnWidth, 0f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
        };
        // HUD compacta: as teclas ficam recolhidas numa linha; H mostra a lista toda
        _hint = MakeLabel(HintShort, 11, new Color(0.80f, 0.77f, 0.70f, 0.75f), 3);
        hintBox.AddChild(_hint);
        bottom.AddChild(hintBox);

        bottom.AddChild(Spacer());

        // ---- barra de baixo da HUD v2 (prancha do dono): placa de ferro, orbes de vida e recurso nas pontas, XP por cima
        // e os quadros de habilidade dentro da placa (BarraInferior, em HudV2.cs)
        // fôlego (correr, esquivar e atacar gastam): barra verde fina sobre a XP, como no Elden Ring
        _stamina = MakeBar(new Color(0.30f, 0.78f, 0.36f), new Vector2(0f, 7f));
        if (_stamina.GetThemeStylebox("background") is StyleBoxFlat stBg) { stBg.BgColor = new Color(0.02f, 0.03f, 0.02f, 0.9f); stBg.BorderColor = new Color(0.30f, 0.29f, 0.27f); stBg.SetCornerRadiusAll(0); }
        if (_stamina.GetThemeStylebox("fill") is StyleBoxFlat stFill) { stFill.BorderColor = new Color(0.55f, 0.75f, 0.5f); stFill.BorderWidthTop = 1; stFill.SetCornerRadiusAll(0); }
        _stamina.TooltipText = "Fôlego";
        GargulaMode = BarraInferior.GargulaDisponivel;
        _hotbar = GargulaMode ? new SlotsGargula() : new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _hotbar.AddThemeConstantOverride("separation", 4);
        _xp = MakeBar(new Color(0.62f, 0.42f, 0.88f), new Vector2(0f, 12f));
        if (_xp.GetThemeStylebox("background") is StyleBoxFlat xpBg) { xpBg.BgColor = new Color(0.02f, 0.015f, 0.03f, 0.95f); xpBg.SetBorderWidthAll(0); xpBg.SetCornerRadiusAll(0); }
        if (_xp.GetThemeStylebox("fill") is StyleBoxFlat xpFill) { xpFill.BgColor = new Color(0.40f, 0.28f, 0.62f); xpFill.BorderColor = new Color(0.70f, 0.60f, 0.85f); xpFill.BorderWidthTop = 1; xpFill.SetCornerRadiusAll(0); }
        _xpText = MakeLabel("", 11, null, 3);
        _xpText.HorizontalAlignment = HorizontalAlignment.Center;
        _xpText.VerticalAlignment = VerticalAlignment.Center;
        _xp.AddChild(_xpText);
        _xpText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _barra = new BarraInferior(_hotbar, _xp, _stamina) { SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        _hpOrb = _barra.Vida;
        _resOrb = _barra.Recurso;
        _barra.Reescalou += scale =>
        {
            // 8 quadros dentro da placa (552..1248 da arte): refaz no tamanho novo
            var size = Mathf.Floor(80f * scale);
            if (Math.Abs(size - SlotSize) < 0.5f) return;
            SlotSize = size;
            _hotbar.AddThemeConstantOverride("separation", (int)Mathf.Max(2f, 7f * scale));
            var cls = _slotsClass; _slotsClass = ""; ConfigureSlots(cls.Length > 0 ? cls : "guerreiro");
        };
        // a barra fica fora da linha de baixo (centrada sozinha na base da tela); no lugar, um espaço do mesmo tamanho
        Root.AddChild(_barra);
        // 14/11 18h (dono: "o 'F — Viajar' ficou atrás da barra, e a altura muda conforme você olha — deixa fixo"): o aviso
        // de interação fica por cima da barra, sempre logo acima dela (posição fixa; ver PosicionarAviso)
        Root.AddChild(_promptRow);
        _barraVaga = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        bottom.AddChild(_barraVaga);
        ConfigureSlots("guerreiro");

        bottom.AddChild(Spacer());

        var right = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(SideColumnWidth, 0f), SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        right.AddThemeConstantOverride("separation", 4);
        _feedBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _feedBox.AddThemeConstantOverride("separation", 3);
        right.AddChild(_feedBox);
        _coinsText = MakeLabel("", 15, Gold);
        _coinsText.HorizontalAlignment = HorizontalAlignment.Right;
        right.AddChild(_coinsText);
        // 18/11 19h (dono: "histórico: morreu/perdeu/gastou em vermelho, entrou em lugar em amarelo"): texto com cores
        _log = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _log.AddThemeFontSizeOverride("normal_font_size", 12);
        _log.AddThemeColorOverride("default_color", new Color(0.92f, 0.9f, 0.84f));
        _log.AddThemeConstantOverride("outline_size", 3);
        _log.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        if (BodyFont is not null) _log.AddThemeFontOverride("normal_font", BodyFont);
        // quebra a linha dentro da moldura (antes a frase comprida passava da borda e saía da tela)
        _log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _log.CustomMinimumSize = new Vector2(SideColumnWidth - 24f, 0);
        _logPanel = Framed(_log, 6f, 0.5f);
        right.AddChild(_logPanel);
        bottom.AddChild(right);

        // ---- mira, avisos, região e chefe ----
        _crosshair = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _crosshair.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        foreach (var (pos, size) in new[] { (new Vector2(-9, -1), new Vector2(6, 2)), (new Vector2(3, -1), new Vector2(6, 2)),
                     (new Vector2(-1, -9), new Vector2(2, 6)), (new Vector2(-1, 3), new Vector2(2, 6)) })
            _crosshair.AddChild(new ColorRect { Color = new Color(1f, 0.95f, 0.85f, 0.8f), Position = pos, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore });
        Root.AddChild(_crosshair);

        _notice = MakeLabel("", 22, Gold);
        _notice.HorizontalAlignment = HorizontalAlignment.Center;
        _notice.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        // 13/11 ("quando o texto for muito grande, divide em duas linhas, centralizado e com margem"): quebra por palavra
        // numa caixa de 720 px, centralizada; cresce para baixo
        _notice.Position = new Vector2(-310, 150);
        _notice.Size = new Vector2(620, 40);
        _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _notice.VerticalAlignment = VerticalAlignment.Top;
        Root.AddChild(_notice);
        _zone = MakeLabel("", 30, new Color(0.92f, 0.88f, 0.78f), 6);
        _zone.HorizontalAlignment = HorizontalAlignment.Center;
        _zone.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _zone.Position = new Vector2(-400, 100);
        _zone.Size = new Vector2(800, 44);
        Root.AddChild(_zone);

        var bossBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _bossName = MakeLabel("", 16, new Color(1f, 0.75f, 0.6f));
        _bossName.HorizontalAlignment = HorizontalAlignment.Center;
        // 11/11 (roteiro AAA): barra do chefe grande, no alto da tela, como nos jogos grandes
        _bossBar = MakeBar(new Color(0.7f, 0.12f, 0.08f), new Vector2(760f, 18f));
        bossBox.AddChild(_bossName);
        bossBox.AddChild(_bossBar);
        _bossPanel = Framed(bossBox, 6f, 0.7f);
        _bossPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _bossPanel.Position = new Vector2(-392, 128);
        _bossPanel.Visible = false;
        Root.AddChild(_bossPanel);
        // entrada do chefe: nome grande no meio
        _bossIntro.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _bossIntro.Position = new Vector2(-400, -160);
        _bossIntro.Size = new Vector2(800, 100);
        _bossIntro.PivotOffset = new Vector2(400, 50);
        _bossIntroName.HorizontalAlignment = HorizontalAlignment.Center;
        _bossIntroName.AddThemeConstantOverride("outline_size", 10);
        _bossIntroName.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _bossIntroSub.HorizontalAlignment = HorizontalAlignment.Center;
        _bossIntro.AddChild(_bossIntroName);
        _bossIntro.AddChild(_bossIntroSub);
        Root.AddChild(_bossIntro);

        // ---- tela avermelhada rápida ao levar dano ----
        _vignette = new ColorRect { Color = new Color(0.6f, 0.0f, 0.0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _vignette.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Root.AddChild(_vignette);

        // ---- sobreposição de morte ----
        _death = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _death.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var tint = new ColorRect { Color = new Color(0.30f, 0.0f, 0.0f, 0.38f), MouseFilter = Control.MouseFilterEnum.Ignore };
        tint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _death.AddChild(tint);
        var deathText = MakeLabel("Você foi derrotado\nRenascendo em instantes…", 34, new Color(0.95f, 0.75f, 0.70f));
        deathText.HorizontalAlignment = HorizontalAlignment.Center;
        deathText.VerticalAlignment = VerticalAlignment.Center;
        deathText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _death.AddChild(deathText);
        Root.AddChild(_death);
    }

    /// <summary>Barra de habilidades da classe: [clique] básico, [Q] e [E] habilidades, [Espaço] esquiva, [1] poção.</summary>
    public void ConfigureSlots(string classId)
    {
        if (classId == _slotsClass) return;
        _slotsClass = classId;
        foreach (var child in _hotbar.GetChildren()) { _hotbar.RemoveChild(child); child.QueueFree(); }
        _slots.Clear();
        var warrior = classId == "guerreiro";
        var mage = classId is "mago" or "bruxa";   // usa frasco de mana/éter na tecla 2
        var (g0, g1, g2) = classId switch
        {
            "mago" => ("Seta", "Expl.", "Passo"),
            "bruxa" => ("Lamp.", "Cura", "Barr."),
            "arqueiro" => ("Disp.", "Emb.", "Fant."),
            _ => ("", "GC", "GE"),
        };
        _hotbar.AddChild(MakeSlot("Clique", g0, new Color(0.55f, 0.38f, 0.12f), warrior ? "icone_auto.png" : ""));
        _hotbar.AddChild(MakeSlot("Q", g1, classId == "bruxa" ? new Color(0.14f, 0.48f, 0.30f) : new Color(0.55f, 0.15f, 0.12f), warrior ? "icone_golpe.png" : ""));
        _hotbar.AddChild(MakeSlot("E", g2, new Color(0.18f, 0.36f, 0.62f), warrior ? "icone_guarda.png" : ""));
        _hotbar.AddChild(MakeSlot("Espaço", "Esq.", new Color(0.30f, 0.30f, 0.33f), ""));
        var s1 = MakeSlot("1", "PV", new Color(0.62f, 0.12f, 0.18f), "icone_pocao.png");   // arte do usuário (o frasco pedido era o da vida, não o da poção)
        // 13/11 ("arrastar um tônico pro slot 1 pra escolher qual poção a tecla 1 usa"): o quadro 1 aceita item da mochila
        var alvo = new SlotPocaoDrop { MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = "Tecla 1 — arraste um tônico da mochila para cá para escolher qual ela usa" };
        s1.AddChild(alvo);
        alvo.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _hotbar.AddChild(s1);
        // E9: [R] habilidade da arma, [T] habilidade da evolução — depois de E na barra (índices 5 e 6)
        var weapon = MakeSlot("R", "—", new Color(0.42f, 0.30f, 0.14f), "");
        _hotbar.AddChild(weapon);
        _hotbar.MoveChild(weapon, 3);
        var evo = MakeSlot("T", "—", new Color(0.36f, 0.22f, 0.40f), "");
        _hotbar.AddChild(evo);
        _hotbar.MoveChild(evo, 4);
        // [7] tecla 2: Frasco de Mana (Mago) ou refeição (Guerreiro)
        var s2 = MakeSlot("2", mage ? "PM" : "Com.", mage ? new Color(0.15f, 0.25f, 0.62f) : new Color(0.45f, 0.32f, 0.14f), "");
        // 14/11: o quadro 2 aceita qualquer item de usar arrastado da mochila (antes caía no chão)
        var alvo2 = new SlotTecla2Drop { MouseFilter = Control.MouseFilterEnum.Pass,
            TooltipText = (mage ? "Tecla 2 — frasco de mana." : classId == "arqueiro" ? "Tecla 2 — frasco de Foco (laranja); sem ele, comida." : "Tecla 2 — comida (recupera vida devagar).") + "\nArraste qualquer poção, frasco ou comida da mochila para cá para escolher o que ela usa." };
        s2.AddChild(alvo2);
        alvo2.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _hotbar.AddChild(s2);
        // 17/11: [8] botão direito do mouse (poder ligado à tecla "Dir" no Livro de Poderes), logo depois do Clique
        var dir = MakeSlot("Dir", "—", new Color(0.30f, 0.26f, 0.40f), "");
        dir.TooltipText = "Botão direito do mouse — ligue um poder a ele no Livro de Poderes (K).";
        _hotbar.AddChild(dir);
        _hotbar.MoveChild(dir, 1);

        foreach (var child in _portraitHolder.GetChildren()) child.QueueFree();
        var portrait = new PuppetPortrait { ClassId = classId, MouseFilter = Control.MouseFilterEnum.Ignore };
        _portraitHolder.AddChild(portrait);
        portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    // ------------------------------------------------------------------ 18/11 15h: efeitos ativos
    // dono: "o buff deve aparecer no C e também carregar ali em cima as passivas ativadas — quadradinhos com um tempo
    // girando dentro, que mostra quando está acabando" (print: em cima da barra, à direita do meio)
    public sealed record Efeito(string Id, string Simbolo, string Nome, string Resumo, TimeSpan? Restante, double Fracao, bool Ruim, Color Cor);
    public static bool NoiteXp;

    public static List<Efeito> EfeitosAtivos(ClientModel m, CinzasDaMare.Shared.Net.PlayerView? me)
    {
        var l = new List<Efeito>();
        if (m.BuffAtivo is { } b)
        {
            var conforto = b.Nome.Contains("onforto", StringComparison.OrdinalIgnoreCase);
            l.Add(new Efeito("buff", conforto ? "⌂" : "♨", b.Nome, b.Resumo, b.Falta, b.Fracao, false, conforto ? new Color(0.95f, 0.75f, 0.45f) : new Color(0.55f, 0.95f, 0.5f)));
        }
        // 18/11 15h: disco na vitrola do quintal (+XP por 30 min) — marca "jukebox_ate=<ms>|<pct>"
        foreach (var f in m.Flags)
        {
            if (!f.StartsWith("jukebox_ate=", StringComparison.Ordinal)) continue;
            var p = f[12..].Split('|');
            if (p.Length == 2 && long.TryParse(p[0], out var ate) && double.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pct))
            {
                var falta = DateTimeOffset.FromUnixTimeMilliseconds(ate) - DateTimeOffset.UtcNow;
                if (falta > TimeSpan.Zero) l.Add(new Efeito("vitrola", "♪", "Vitrola do Quintal", $"+{pct * 100:0}% de experiência", falta, Math.Clamp(falta.TotalMinutes / 30, 0, 1), false, new Color(1f, 0.6f, 0.85f)));
            }
        }
        if (NoiteXp) l.Add(new Efeito("noite", "☾", "Noite", "+25% de experiência (monstros 20% mais fortes)", null, 1, false, new Color(0.6f, 0.65f, 1f)));
        if (me is { Shield: > 0.5 }) l.Add(new Efeito("escudo", "◈", "Escudo", $"absorve {me.Shield:0} de dano", null, 1, false, new Color(0.55f, 0.85f, 1f)));
        if (me is { SetCompleto: true }) l.Add(new Efeito("conjunto", "✦", "Conjunto completo", "bônus do conjunto ativo", null, 1, false, Gold));
        foreach (var st in (me?.Status ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            // 18/11 19h: efeitos BONS que vêm como status (Campeão)
            if (st == "furor") { l.Add(new Efeito("furor", "⚔", "Furor do Campeão", "+20% de dano e de velocidade", null, 1, false, new Color(1f, 0.55f, 0.25f))); continue; }
            if (st.StartsWith("espadas", StringComparison.Ordinal)) { if (st.Length == 8) l.Add(new Efeito("espadas", "†", "Espadas Voadoras", $"{st[7]} espada(s) girando", null, 1, false, new Color(0.7f, 0.9f, 1f))); continue; }
            var (nome, cor, simb) = st switch
            {
                "veneno" => ("Veneno", new Color(0.45f, 1f, 0.35f), "☠"), "lentidao" => ("Lento", new Color(0.5f, 0.85f, 1f), "❄"),
                "queimadura" => ("Queimando", new Color(1f, 0.55f, 0.2f), "🔥"), "sangramento" => ("Sangrando", new Color(1f, 0.25f, 0.25f), "✚"),
                "maldicao" => ("Maldição", new Color(0.75f, 0.45f, 1f), "☠"), "atordoado" => ("Atordoado", new Color(1f, 0.9f, 0.4f), "✶"),
                "cansaco" => ("Cansaço", new Color(0.8f, 0.75f, 0.55f), "≈"),
                _ => (st, Colors.White, "!"),
            };
            l.Add(new Efeito("st_" + st, simb, nome, st == "cansaco" ? "o fôlego não volta enquanto durar" : "efeito ruim — passa sozinho", null, 1, true, cor));
        }
        return l;
    }

    private HBoxContainer? _efeitosRow;
    private Label? _casaLabel;
    private ProgressBar? _casaBarra;
    private readonly Label _casaSub = MakeLabel("", 11, new Color(0.85f, 0.8f, 0.68f), 3);

    private ProgressBar NovaBarraCasa()
    {
        var b = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, Size = new Vector2(120, 5), CustomMinimumSize = new Vector2(120, 5), MouseFilter = Control.MouseFilterEnum.Ignore };
        var fundo = new StyleBoxFlat { BgColor = new Color(0.11f, 0.1f, 0.08f, 0.9f), BorderColor = new Color(0.35f, 0.27f, 0.13f) };
        fundo.SetBorderWidthAll(1); fundo.SetCornerRadiusAll(3);
        var cheio = new StyleBoxFlat { BgColor = new Color(0.79f, 0.63f, 0.32f) };
        cheio.SetCornerRadiusAll(3);
        b.AddThemeStyleboxOverride("background", fundo); b.AddThemeStyleboxOverride("fill", cheio);
        b.TooltipText = "Conforto da casa: enche com decoração, reformas e peças do quintal";
        Root.AddChild(b);
        return b;
    }
    private string _efeitosSig = "";

    private void AtualizarEfeitos(ClientModel m, CinzasDaMare.Shared.Net.PlayerView? me)
    {
        if (_efeitosRow is null)
        {
            // 18/11 19h (dono: "os efeitos entram da direita pra esquerda e menores"): o primeiro fica colado à direita
            _efeitosRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, LayoutDirection = Control.LayoutDirectionEnum.Rtl };
            _efeitosRow.AddThemeConstantOverride("separation", 3);
            Root.AddChild(_efeitosRow);
        }
        var efs = EfeitosAtivos(m, me);
        var sig = string.Join(",", efs.Select(e => e.Id));
        if (sig != _efeitosSig)
        {
            _efeitosSig = sig;
            foreach (var c in _efeitosRow.GetChildren()) c.QueueFree();
            foreach (var e in efs) _efeitosRow.AddChild(new IconeEfeito { CustomMinimumSize = new Vector2(24, 24) });
        }
        var i = 0;
        foreach (var n in _efeitosRow.GetChildren())
        {
            if (n is not IconeEfeito ic || ic.IsQueuedForDeletion() || i >= efs.Count) continue;
            var e = efs[i++];
            ic.Efeito = e;
            var dica = e.Nome + (e.Restante is { } r ? $" — {(r.TotalMinutes >= 1 ? $"{Math.Ceiling(r.TotalMinutes):0} min" : $"{r.Seconds} s")}" : "") + (e.Resumo.Length > 0 ? "\n" + e.Resumo : "");
            if (ic.TooltipText != dica) ic.TooltipText = dica;
            ic.QueueRedraw();
        }
        // 18/11 15h: nível da casa — em cima da barra, à esquerda do meio (só na sua casa/terreno)
        if (_casaLabel is null)
        {
            _casaLabel = MakeLabel("", 10, new Color(0.98f, 0.85f, 0.55f), 2);   // 19/11: menor (dono: "tá muito grande")
            Root.AddChild(_casaLabel);
        }
        // 18/11 19h: "casa|nível|conforto|pontos|fração" — em cima da barra só "SUA CASA · NÍVEL N" e a barrinha (estilo grupo);
        // conforto e Ctrl+F vão embaixo do nome do lugar
        var ci = m.CasaInfo.Split('|');
        var naCasa = ci.Length >= 5 && ci[0] == "casa";
        _casaLabel.Text = naCasa ? $"SUA CASA · NÍVEL {ci[1]}" : "";
        _casaLabel.Visible = naCasa;
        _casaBarra ??= NovaBarraCasa();
        _casaBarra.Visible = naCasa;
        if (naCasa && double.TryParse(ci[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fr)) _casaBarra.Value = fr;
        _casaSub.Text = naCasa ? $"Conforto {ci[2]}/10  ·  Ctrl+F: editar decoração" : "";
        _casaSub.Visible = naCasa;
        var xp = _barra.Xp;
        if (xp.IsInsideTree() && naCasa)
        {
            _casaLabel.Position = new Vector2(xp.GlobalPosition.X + 4f, xp.GlobalPosition.Y - 26f);
            _casaBarra.Position = new Vector2(xp.GlobalPosition.X + 4f, xp.GlobalPosition.Y - 11f);
        }
        if (xp.IsInsideTree())
        {
            var topo = xp.GlobalPosition.Y - 38f;
            _efeitosRow.Size = _efeitosRow.GetCombinedMinimumSize();
            _efeitosRow.Position = new Vector2(xp.GlobalPosition.X + xp.Size.X - 4f - _efeitosRow.Size.X, topo);
        }
        _efeitosRow.Visible = efs.Count > 0;
    }

    // ------------------------------------------------------------------ 18/11: cartão de nível
    // dono: "fala que GANHA, mas eu ganho? deveria mostrar o que dá pra fazer agora que chegou no 20, e o que ganha todo
    // nível". Texto do servidor: "Nível N|ganhou;...|pode;...|conseguir;...". Fica 14 s (clique fecha).
    private PanelContainer? _nivelCard;
    private double _nivelIdade;

    public void MostrarNivel(string texto)
    {
        var p = texto.Split('|');
        if (p.Length < 4) return;
        _nivelCard?.QueueFree();
        var card = _nivelCard = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(540, 0) };
        var st = new StyleBoxFlat { BgColor = new Color(0.07f, 0.05f, 0.03f, 0.93f), BorderColor = new Color(0.95f, 0.75f, 0.28f) };
        st.SetBorderWidthAll(2); st.SetCornerRadiusAll(8); st.SetContentMarginAll(14);
        st.ShadowColor = new Color(0, 0, 0, 0.55f); st.ShadowSize = 12;
        card.AddThemeStyleboxOverride("panel", st);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 4);
        card.AddChild(col);
        var t = MakeLabel("⚜  " + p[0].ToUpperInvariant() + "  ⚜", 26, Gold, 3);
        if (TitleFont is not null) t.AddThemeFontOverride("font", TitleFont);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(t);
        // 19/11 (dono: "tira esse quadrado de lvl, só deixa o NÍVEL 23 e os ícones do lado"): só o título com os ⚜, sem
        // moldura; o que ganhou/liberou vai para o histórico (O) e para a Trilha (Tab)
        foreach (var (titulo, lista) in new[] { ("Ganhou", p[1]), ("Agora pode", p[2]), ("Dá para conseguir", p[3]) })
            foreach (var it in lista.Split(';', StringSplitOptions.RemoveEmptyEntries)) AvisoLocal?.Invoke($"{p[0]} — {titulo}: {it}");
        t.AddThemeFontSizeOverride("font_size", 34);
        st.BgColor = new Color(0, 0, 0, 0); st.BorderColor = new Color(0, 0, 0, 0); st.ShadowSize = 0;
        card.CustomMinimumSize = new Vector2(420, 0);
        card.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) { card.QueueFree(); if (_nivelCard == card) _nivelCard = null; } };
        Root.AddChild(card);
        card.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        card.Position = new Vector2(-210, 170);
        card.MouseFilter = Control.MouseFilterEnum.Ignore;
        Callable.From(() => { if (GodotObject.IsInstanceValid(card)) card.Position = new Vector2((Root.Size.X - card.Size.X) / 2f, 170); }).CallDeferred();
        _nivelIdade = 0;
    }

    private void TickNivel(double dt)
    {
        if (_nivelCard is not { } c || !GodotObject.IsInstanceValid(c)) return;
        _nivelIdade += dt;
        if (_nivelIdade > 5) { c.QueueFree(); _nivelCard = null; return; }
        c.Modulate = new Color(1, 1, 1, (float)Math.Clamp(5 - _nivelIdade, 0, 1));
    }

    /// <summary>14/11 19h: avisos locais (atualizador etc.) também vão para o histórico (O).</summary>
    public static event Action<string>? AvisoLocal;
    public void ShowNotice(string text)
    {
        AvisoLocal?.Invoke(text);
        _notice.Text = text;
        // 13/11 ("divide em duas linhas os eventos e dicas — numa linha só fica na frente das coisas e até sai da tela"):
        // texto longo fica menor e quebra em duas linhas dentro de 620 px no meio da tela
        _notice.AddThemeFontSizeOverride("font_size", text.Length > 110 ? 17 : text.Length > 60 ? 19 : 22);
        _noticeAge = text.Length > 80 ? -4 : 0;   // 13/11: dica longa fica mais tempo
    }

    public void ShowZone(string name)
    {
        _zone.Text = name;
        _zoneAge = 0;
    }

    private Control MakeSlot(string key, string glyph, Color iconColor, string iconFile)
    {
        // quadrado de habilidade "AAA": ícone pintado (Tools/gerar_ui.py: hab_<habilidade>.png) dentro de uma moldura de
        // bronze com relevo (ui_slot.png); recarga escurece de cima para baixo; tecla no canto
        // estilo Diablo IV: quadrado escuro, borda fina de bronze apagado, sombra; nada de moldura grossa
        var style = PanelFlat(0f, 0.0f);
        style.BgColor = new Color(0.025f, 0.022f, 0.025f, 0.92f);
        style.BorderColor = new Color(0.26f, 0.25f, 0.24f);
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(0);
        style.ShadowColor = new Color(0, 0, 0, 0.6f);
        style.ShadowSize = 3;
        var panel = new Panel { CustomMinimumSize = new Vector2(SlotSize, SlotSize), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", style);

        var icon = new TextureRect
        {
            Texture = iconFile.Length > 0 ? VisualAssets.Load(iconFile) : null,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddChild(icon);
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 3);

        var g = MakeLabel(icon.Texture is null ? glyph : "", glyph.Length > 4 ? 12 : 16);
        g.HorizontalAlignment = HorizontalAlignment.Center;
        g.VerticalAlignment = VerticalAlignment.Center;
        panel.AddChild(g);
        g.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var cooldown = new ColorRect { Color = new Color(0f, 0f, 0f, 0.66f), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(cooldown);

        var frame = new TextureRect { Texture = VisualAssets.Load("ui_slot_hab.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, -2);

        if (frame.Texture is not null) { style.BorderColor = style.BorderColor with { A = 0f }; style.ShadowSize = 0; }
        if (GargulaMode) { frame.Visible = false; style.BgColor = style.BgColor with { A = 0.35f }; style.BorderColor = style.BorderColor with { A = 0f }; style.ShadowSize = 0; }
        var keyLabel = MakeLabel(key, key.Length > 2 ? 10 : 12, new Color(0.93f, 0.88f, 0.78f), 3);
        keyLabel.Position = new Vector2(SlotSize * 0.12f, SlotSize * 0.06f);
        panel.AddChild(keyLabel);

        var count = MakeLabel("", 13, null, 3);
        count.Position = new Vector2(SlotSize - 20f, SlotSize - 21f);
        panel.AddChild(count);

        // 18/11 15h ("ao passar o mouse nos poderes, mostrar o nome dos poderes"): o quadrado recebe o mouse (só para a dica)
        panel.MouseFilter = Control.MouseFilterEnum.Pass;
        _slots.Add(new Slot { Style = style, Cooldown = cooldown, Count = count, Icon = icon, Glyph = g, Frame = frame, Painel = panel });
        return panel;
    }

    /// <summary>Ícone pintado da habilidade (hab_&lt;id&gt;.png), com cache; null = sem ícone (fica o texto).</summary>
    private static readonly Dictionary<string, Texture2D?> SkillIcons = new();
    private static Texture2D? SkillIcon(string? id)
    {
        if (id is null) return null;
        if (!SkillIcons.TryGetValue(id, out var t)) SkillIcons[id] = t = VisualAssets.Load("hab_" + id + ".png");
        return t;
    }

    private Texture2D? _pocaoPadrao;

    private void SetSlotIcon(int idx, Texture2D? tex, string glyph)
    {
        var slot = _slots[idx];
        if (slot.Icon is TextureRect tr && tr.Texture != tex) tr.Texture = tex;
        slot.Icon.Visible = tex is not null;
        slot.Glyph.Text = tex is null ? glyph : "";
    }

    // ------------------------------------------------------------ atualização por quadro

    public void Update(ClientModel m, CooldownTracker cooldowns, long nowMs)
        => Update(m, cooldowns, nowMs, m.NearestAliveMonster(), null, null, false, 0.016);

    private PanelContainer? _channel;
    private ProgressBar? _channelBar;
    private Label? _channelText;

    /// <summary>Barra de coleta no meio da tela ("Colhendo… / Cortando…"): enche com o tempo que o servidor deu.</summary>
    private void UpdateChannel(ClientModel m)
    {
        if (_channel is null)
        {
            _channel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            _channel.AddThemeStyleboxOverride("panel", PanelStyle(5f, 0.8f));
            _channel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            _channel.Position += new Vector2(-95, -165);   // 19/11 noite (dono: "em cima do aviso do F não dá — e pode diminuir"): pequena, acima da cabeça
            var box = new VBoxContainer();
            _channel.AddChild(box);
            _channelText = MakeLabel("", 12, new Color(0.95f, 0.85f, 0.55f));
            _channelText.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(_channelText);
            _channelBar = MakeBar(new Color(0.55f, 0.80f, 0.35f), new Vector2(190, 8));
            box.AddChild(_channelBar);
            Root.AddChild(_channel);
        }
        var me = m.Me;
        var gathering = me is not null && me.Action == "gather";
        _channel.Visible = gathering;
        if (!gathering) return;
        var near = m.NearestObjectLabel ?? "";
        _channelText!.Text = near.Contains("rvore") ? "Cortando tábuas… (parado)" : near.Contains("eio") ? "Minerando… (parado)" : "Colhendo… (parado)";
        _channelBar!.Value = Math.Clamp(me!.ActionT, 0.0, 1.0);
    }

    private Control? _lootCard;
    private double _lootAge = 99;

    /// <summary>Baú aberto / recompensa: quadro com o desenho de cada item, que sobe e some (estilo "achei!").</summary>
    private void UpdateLootPopup(ClientModel m, double delta)
    {
        _lootAge += delta;
        if (_lootCard is not null)
        {
            var t = (float)_lootAge;
            _lootCard.Modulate = new Color(1, 1, 1, Mathf.Clamp(4.2f - t, 0f, 1f) * Mathf.Clamp(t * 4f, 0f, 1f));
            _lootCard.Position = new Vector2(_lootCard.Position.X, 140 - Mathf.Min(t, 0.4f) * 60f - Mathf.Max(0f, t - 3f) * 30f);
            if (t > 4.3f) { _lootCard.QueueFree(); _lootCard = null; }
            return;
        }
        if (m.LootPopups.Count == 0) return;
        var loot = m.LootPopups.Dequeue();
        var card = new StonePanel(14f) { MouseFilter = Control.MouseFilterEnum.Ignore };
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 6);
        card.AddChild(col);
        var title = loot.Source.StartsWith("quest:", StringComparison.Ordinal) ? "Recompensa da missão" : loot.Source.StartsWith("chest", StringComparison.Ordinal) || loot.Source.StartsWith("bau", StringComparison.Ordinal) ? "Baú aberto!" : "Você obteve";
        var head = MakeLabel(title, 20, new Color(1f, 0.85f, 0.35f));
        head.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(head);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        foreach (var def in loot.DefinitionIds.Take(6))
        {
            var item = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            var slot = new ItemSlot(72f, null) { MouseFilter = Control.MouseFilterEnum.Ignore };
            slot.Show(m.ItemInfoFor(def), def, 1, "comum", true, false);
            item.AddChild(slot);
            var name = MakeLabel(ClientModel.DisplayName(def), 13, new Color(0.92f, 0.9f, 0.85f));
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.CustomMinimumSize = new Vector2(96, 0);
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            item.AddChild(name);
            row.AddChild(item);
        }
        col.AddChild(row);
        Root.AddChild(card);
        card.ResetSize();
        card.Position = new Vector2((Root.Size.X - Math.Max(card.Size.X, 240)) / 2f, 140);
        _lootCard = card;
        _lootAge = 0;
        Sfx.Play("bau");
    }

    public void Update(ClientModel m, CooldownTracker cooldowns, long nowMs, CinzasDaMare.Shared.Net.MonsterView? target,
        string? prompt, CinzasDaMare.Shared.Net.MonsterView? boss, bool crosshair, double delta, bool locked = false, long dodgeReadyInMs = 0)
    {
        _barraVaga.CustomMinimumSize = new Vector2(Mathf.Max(0f, _barra.Size.X - 260f * _barra.Escala * 2f), 4f);
        AtualizarGanhou(m);
        ConfigureSlots(m.ClassId);
        UpdateChannel(m);
        UpdateLootPopup(m, delta);
        _crosshair.Visible = crosshair;
        _noticeAge += delta;
        TickNivel(delta);
        _zoneAge += delta;
        _notice.Modulate = new Color(1f, 1f, 1f, (float)Math.Clamp(4.0 - _noticeAge, 0.0, 1.0));
        _zone.Modulate = new Color(1f, 1f, 1f, (float)Math.Clamp(3.0 - _zoneAge, 0.0, 1.0));
        _bossPanel.Visible = boss is not null;
        UpdateGrupo(m);
        // 11/11: entrada do chefe — nome grande no meio da tela + rugido, na 1ª vez que ele aparece
        if (boss is { Boss: true } && boss.Id != _introBoss)
        {
            _introBoss = boss.Id;
            _introAge = 0;
            _bossIntroName.Text = boss.SpeciesName.ToUpperInvariant();
            _bossIntroSub.Text = boss.Boss ? $"Chefe · nível {boss.Level}" : $"Elite · nível {boss.Level}";
            Sfx.Play("rugido");
        }
        if (boss is null && _introAge > 6) _introBoss = null;
        _introAge += delta;
        var ia = (float)(_introAge < 0.4 ? _introAge / 0.4 : Math.Clamp(3.6 - _introAge, 0.0, 1.0));
        _bossIntro.Visible = ia > 0.01f && _introBoss is not null;
        _bossIntro.Modulate = new Color(1, 1, 1, ia);
        _bossIntro.Scale = Vector2.One * (1.15f - 0.15f * Mathf.Min(1f, (float)_introAge / 0.4f));
        if (boss is not null)
        {
            _bossName.Text = $"{boss.SpeciesName}  ·  Nv {boss.Level}{(boss.Enraged ? "  ·  ENFURECIDO" : "")}";
            _bossBar.Value = boss.MaxHealth > 0 ? Math.Clamp(boss.Health / boss.MaxHealth, 0.0, 1.0) : 0.0;
        }
        var me = m.Me;
        var progress = m.Progress;

        _name.Text = me?.Name ?? m.Name ?? "";
        var level = progress?.Level ?? me?.Level ?? 0;
        var percent = progress is { XpToNext: > 0 } ? 100.0 * progress.Xp / progress.XpToNext : 0.0;
        _levelText.Text = $"Nv {level}";
        _classText.Text = m.Me?.Title is { Length: > 0 } title ? $"{m.ClassTitle} · «{title}»" : m.ClassTitle;   // 11/11: título da conquista
        _buffText.Text = m.BuffText;
        AtualizarEfeitos(m, me);   // 18/11 15h: quadradinhos de efeito em cima da barra
        _coinsText.Text = m.LoggedIn ? $"◉ {m.Coins:N0} moedas".Replace(",", ".") : "";

        _resOrb.Liquid = m.ClassId switch
        {
            "mago" => new Color(0.16f, 0.32f, 0.85f),
            "bruxa" => new Color(0.18f, 0.72f, 0.48f),     // éter esmeralda
            "arqueiro" => new Color(0.85f, 0.62f, 0.18f),  // foco âmbar
            _ => new Color(0.86f, 0.30f, 0.10f),            // fúria do Guerreiro (o verde agora é o fôlego)
        };
        _resOrb.Caption = m.ResourceName;
        if (me is not null)
        {
            _hpOrb.SetValue(me.Health, me.MaxHealth);
            _hpOrb.Status = me.Status ?? "";
            _vinheta.Status = me.Alive ? me.Status ?? "" : "";
            _resOrb.SetValue(me.Resource, me.ResourceMax > 0 ? me.ResourceMax : m.ResourceMax);
            _staminaShown += (me.Stamina - _staminaShown) * Math.Min(1.0, delta * 14);
            _stamina.Value = Math.Clamp(_staminaShown / 100.0, 0, 1);
            // quase sem fôlego: a barra pisca em vermelho para avisar antes de travar a corrida/esquiva
            _staminaLow = me.Stamina < 20 ? _staminaLow + delta : 0;
            if (_stamina.GetThemeStylebox("fill") is StyleBoxFlat sf)
                sf.BgColor = me.Stamina < 20 && Math.Sin(_staminaLow * 14) > 0 ? new Color(0.85f, 0.25f, 0.18f) : new Color(0.30f, 0.78f, 0.36f);
        }
        if (progress is not null)
        {
            SetBar(_xp, null, progress.Xp, progress.XpToNext);
            _xpText.Text = $"Nível {level}  ·  {progress.Xp:N0} / {progress.XpToNext:N0} XP  ({percent:0.0}%)".Replace(",", ".");
        }
        UpdateQuests(m);
        LimitarQuests();
        UpdateFeed(delta);
        _vignetteAlpha = Math.Max(0, _vignetteAlpha - delta * 1.8);
        _vignette.Color = new Color(0.6f, 0f, 0f, (float)_vignetteAlpha);

        if (m.Map is { } zoneMap)
        {
            _placaZona.Texto = zoneMap.DisplayName.ToUpperInvariant();
            _placaZona.Icone = zoneMap.Instanced ? "ico_zona_picareta.png" : "ico_zona_ancora.png";
        }
        _targetPanel.Visible = target is not null && (boss is null || target.Id != boss.Id)   // 13/11: o chefe já tem a barra grande
            && !target.DefinitionId.Contains("boneco");   // 18/11 ("o boneco de treino aparece uma vida ali em cima, tira isso")
        if (target is not null)
        {
            var exposed = target.Action.StartsWith("recover", StringComparison.Ordinal) ? "  · EXPOSTO" : "";
            // 11/11 noite: "Nome - Nv x" e pedrinha amarela à esquerda quando o bicho conta para uma missão ativa
            var questTarget = m.Quests.Any(q => q.State == "active" && q.Progress < q.Count && m.IsPinned(q.Id) && q.Targets.Contains(target.DefinitionId));
            // 13/11 23:39 ("no boneco de nível 1 dou 51, no de 10 dou 13, no de 20 dou 12 — qual a lógica?"): monstro de nível
            // MAIOR que o seu toma −12% por nível de diferença (mínimo 30%) e tem mais armadura. Agora a barra mostra isso.
            var acima = me is not null && target.Level > me.Level + 2 && !target.DefinitionId.Contains("boneco") ? target.Level - me.Level : 0;
            var corte = acima > 0 ? $"  · seu dano {Math.Max(50, 100 - 5 * (acima - 2))}% (nível acima)" : "";
            _targetName.Text = $"{(locked ? "◆ " : "")}{target.SpeciesName}{(target.Level > 0 ? $" - Nv {target.Level}" : "")}{exposed}{corte}";
            _alvo.Missao = questTarget;
            _targetBar.Value = target.MaxHealth > 0 ? Math.Clamp(target.Health / target.MaxHealth, 0.0, 1.0) : 0.0;
            _alvo.Nome = _targetName.Text.ToUpperInvariant();
            _alvo.Fracao = _targetBar.Value;
            _alvo.Chefe = target.Boss;
        }

        // interação: item, baú ou alavanca ao alcance
        if (prompt is null && m.Map is null)
        {
            var item = m.NearestItem();
            if (me is not null && item is not null && ClientModel.Distance(me.X, me.Y, item.X, item.Y) <= 3.0)
                prompt = "F — pegar " + ClientModel.DisplayName(item.DefinitionId);
        }
        var flutua = prompt is not null && PromptAncora is not null;
        _promptPanel.Visible = prompt is not null && !flutua;
        PosicionarAviso();
        _promptItemPanel.Visible = flutua;
        if (flutua)
        {
            var partes = prompt!.Split('\n', 2);
            _promptItem.Text = partes[0];
            _promptItem.AddThemeColorOverride("font_color", PromptCor);
            _promptItemSub.Text = partes.Length > 1 ? partes[1] : "";
            _promptItemSub.Visible = partes.Length > 1;
            var up2 = _promptItemSub.Text.Contains('▲'); var down2 = _promptItemSub.Text.Contains('▼');
            _promptItemSub.AddThemeColorOverride("font_color", up2 && !down2 ? new Color(0.55f, 0.95f, 0.45f) : down2 && !up2 ? new Color(1f, 0.5f, 0.42f) : new Color(0.85f, 0.82f, 0.74f));
            _promptItemEstilo.BorderColor = PromptCor with { A = 0.75f };
            _promptItemPanel.ResetSize();
            var tam = _promptItemPanel.Size;
            var tela = Root.GetViewportRect().Size;
            var a = PromptAncora!.Value;
            var pos = a + new Vector2(22, -tam.Y - 20);
            if (pos.X + tam.X > tela.X - 10) pos.X = a.X - tam.X - 22;
            pos.Y = Mathf.Clamp(pos.Y, 10, tela.Y - tam.Y - 10);
            pos.X = Mathf.Clamp(pos.X, 10, tela.X - tam.X - 10);
            _promptItemPanel.Position = pos;
        }
        else if (prompt is not null)
        {
            _prompt.Text = prompt;
            // item melhor que o equipado = verde; pior = vermelho; misto/sem comparação = dourado
            var up = prompt.Contains('▲'); var down = prompt.Contains('▼');
            _prompt.AddThemeColorOverride("font_color", up && !down ? new Color(0.55f, 0.95f, 0.45f) : down && !up ? new Color(1f, 0.5f, 0.42f) : Gold);
        }

        UpdateSlots(m, cooldowns, nowMs, me, dodgeReadyInMs);

        var logText = string.Join("\n", m.Log.Skip(Math.Max(0, m.Log.Count - LogLines)));
        if (logText != _logRaw) { _logRaw = logText; _log.Text = string.Join("\n", logText.Split('\n').Select(CorDoLog)); _logChangedMs = nowMs; }
        // registro some sozinho depois de uns segundos sem novidade (HUD compacta)
        var age = (nowMs - _logChangedMs) / 1000.0;
        _logPanel.Modulate = new Color(1, 1, 1, (float)Math.Clamp(1.0 - (age - 6.0) / 2.0, 0.0, 1.0));
        _death.Visible = me is not null && !me.Alive;
    }

    private readonly RichTextLabel _log;
    private string _logRaw = "";

    /// <summary>18/11 19h: vermelho = morreu/perdeu/gastou/pagou; amarelo = entrou num lugar; o resto, creme.</summary>
    private static string CorDoLog(string linha)
    {
        var t = linha.Replace("[", "[lb]");
        var b = linha.ToLowerInvariant();
        if (b.Contains("morreu") || b.Contains("morte") || b.Contains("perdeu") || b.Contains("gastou") || b.Contains("pagou") || b.Contains("caiu") || b.Contains("quebrou") || b.Contains("derrotado"))
            return $"[color=#ff6b5b]{t}[/color]";
        if (b.Contains("entrou") || b.Contains("chegou") || b.Contains("bem-vindo") || b.StartsWith("→") || b.Contains("viajou"))
            return $"[color=#f2d15a]{t}[/color]";
        return t;
    }

    /// <summary>18/11 19h (dono: "a caixa das missões cresce e empurra o histórico de baixo — cada um com seu limite"): o quadro
    /// de missões tem altura máxima (~1/3 da tela); o que não cabe some e vira "+N no diário (J)".</summary>
    private void LimitarQuests()
    {
        if (!_questPanel.Visible) return;
        var max = Math.Max(220f, Root.GetViewportRect().Size.Y * 0.46f);   // 19/11: 34% → 46% da tela
        var filhos = _questBox.GetChildren().OfType<Control>().ToList();
        if (filhos.Count < 2) return;
        if (_questPanel.GetCombinedMinimumSize().Y <= max) return;
        // esconde de baixo para cima, sempre em pares (nome + dica), até caber
        var vis = filhos.Where(f => f.Visible && f.Name != "mais").ToList();
        if (vis.Count <= 2) return;
        vis[^1].Visible = false; vis[^2].Visible = false;
        var mais = _questBox.GetNodeOrNull<Label>("mais");
        if (mais is null) { mais = MakeLabel("", 11, new Color(0.7f, 0.66f, 0.58f), 3); mais.Name = "mais"; _questBox.AddChild(mais); }
        var escondidas = filhos.Count(f => !f.Visible && f.Name != "mais") / 2;
        mais.Text = $"+{escondidas} no diário (J)";
    }

    /// <summary>Até três missões no canto: prontas primeiro (◆ dourado), depois em andamento com progresso.</summary>
    private void UpdateQuests(ClientModel m)
    {
        // fixadas no diário (J) primeiro; sem nenhuma fixada, as 3 mais urgentes
        var open = m.Quests.Where(q => q.State is "active" or "ready").ToList();
        // 18/11 (dono: "quando entra em dungeon as missões do J crescem e ficam na tela, ocupando muito espaço"): na dungeon
        // o quadro mostra só o objetivo + 2 missões (as do próprio lugar primeiro); fora, até 5
        var naDg = m.Map?.Instanced == true;
        var tracked = open.Where(q => m.IsPinned(q.Id))
            .OrderBy(q => naDg && q.MapId == m.Map?.Id ? 0 : 1).ThenBy(q => q.State == "ready" ? 0 : 1).Take(naDg ? 3 : 7).ToList();   // 19/11 (dono: "agora mostra pouca missão, aumenta um pouco")
        // 10/11: número de cada missão (o mesmo da pedrinha na bússola e no mapa M)
        Tracked = tracked;
        // 19/11 (dono: "o que está marcado no J mostra no mapa até 10 ou 15"): no mapa (M) vão até 15 marcadas — as do canto
        // primeiro (mesmo número), depois as outras marcadas
        NoMapa = tracked.Concat(open.Where(q => m.IsPinned(q.Id) && !tracked.Contains(q))).Take(15).ToList();
        var me = m.Me;
        var marks = new List<(float Deg, int N, bool Ready)>();
        if (me is not null && m.Map is not null)
            for (var i = 0; i < tracked.Count; i++)
            {
                var q = tracked[i];
                if (q.MapId != m.Map.Id || (q.X == 0 && q.Y == 0)) continue;
                var dx = q.X - me.X; var dy = q.Y - me.Y;
                if (dx * dx + dy * dy < 9) continue;   // já está lá
                marks.Add((Mathf.RadToDeg(Mathf.Atan2((float)dx, (float)-dy)), i + 1, q.State == "ready"));
            }
        // 17/11 noite: o próximo passo da história = estrela na bússola (e no minimapa e no mapa M)
        if (me is not null && m.Map is not null && m.ProximaHistoria is { } hq && m.IsPinned(hq.Id) && hq.MapId == m.Map.Id && (hq.X != 0 || hq.Y != 0)
            && (hq.X - me.X) * (hq.X - me.X) + (hq.Y - me.Y) * (hq.Y - me.Y) >= 9)
            marks.Add((Mathf.RadToDeg(Mathf.Atan2((float)(hq.X - me.X), (float)-(hq.Y - me.Y))), -2, false));
        // horda caçando você: pedrinha vermelha "!" na bússola e uma linha vermelha no topo das missões
        var horde = m.QuestMarkers.TryGetValue("horda", out var hm) && hm.Kind == "horda" ? hm : ((double X, double Y, string Kind)?)null;
        if (horde is { } hz && me is not null)
            marks.Add((Mathf.RadToDeg(Mathf.Atan2((float)(hz.X - me.X), (float)-(hz.Y - me.Y))), 0, false));
        // 13/11: objetivo da dungeon ("pra onde vou? o que faço?") — pedrinha dourada na bússola e linha no topo
        var objetivo = m.QuestMarkers.FirstOrDefault(kv => kv.Value.Kind == "objetivo");
        if (objetivo.Key is not null && me is not null)
            marks.Add((Mathf.RadToDeg(Mathf.Atan2((float)(objetivo.Value.X - me.X), (float)-(objetivo.Value.Y - me.Y))), 0, true));
        _compass.Marks = marks;
        var hordeLine = horde is { } hh && me is not null ? $"⚔ Horda vindo te caçar — {Math.Sqrt((hh.X - me.X) * (hh.X - me.X) + (hh.Y - me.Y) * (hh.Y - me.Y)):0} m (vença e ganhe um prêmio)" : "";
        if (objetivo.Key is not null && me is not null)
            hordeLine = (hordeLine.Length > 0 ? hordeLine + "\n" : "") + $"{objetivo.Key[(objetivo.Key.IndexOf('|') + 1)..]} — {(int)(Math.Sqrt((objetivo.Value.X - me.X) * (objetivo.Value.X - me.X) + (objetivo.Value.Y - me.Y) * (objetivo.Value.Y - me.Y)) / 5) * 5} m";
        // 17/11: o próximo passo da história sempre no topo do quadro (laranja-dourado)
        // 19/11 (dono: "a história principal tem que marcar e desmarcar também"): desmarcada no J, sai do quadro, da bússola e dos mapas
        var hist = m.ProximaHistoria is { } ph0 && m.IsPinned(ph0.Id) ? ph0 : null;
        var sig = hordeLine + (hist is null ? "" : hist.Name + hist.Hint) + string.Join("|", tracked.Select(q => q.Id + q.State + q.Progress + q.Count));
        _questPanel.Visible = (tracked.Count > 0 || hordeLine.Length > 0 || hist is not null) && !MissoesMinimizadas;
        if (sig == _questSig) return;
        _questSig = sig;
        foreach (var child in _questBox.GetChildren()) { _questBox.RemoveChild(child); child.QueueFree(); }
        // 18/11: tudo quebra linha na largura do quadro (a linha do objetivo da dungeon esticava o quadro na tela inteira)
        Label Quebra(Label l) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.CustomMinimumSize = new Vector2(CardWidth - 20f, 0); return l; }
        if (hordeLine.Length > 0) _questBox.AddChild(Quebra(MakeLabel(hordeLine, 14, hordeLine.StartsWith("⚔") ? new Color(1f, 0.35f, 0.3f) : Gold, 3)));
        if (hist is not null)
        {
            _questBox.AddChild(Quebra(MakeLabel(hist.Name, 15, new Color(1f, 0.66f, 0.25f))));
            var hl = MakeLabel(hist.Hint, 12, new Color(0.95f, 0.85f, 0.7f), 3);
            hl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hl.CustomMinimumSize = new Vector2(CardWidth - 20f, 0);
            _questBox.AddChild(hl);
        }
        var num = 0;
        foreach (var q in tracked)
        {
            num++;
            var done = q.State == "ready" || q.Progress >= q.Count;
            var trav = !done && q.NivelLugar > (m.Me?.Level ?? 99);   // 13/11: lugar trancado pelo nível
            _questBox.AddChild(Quebra(MakeLabel($"[{num}] " + (trav ? "🔒 " : "") + q.Name + (trav ? $" (nível {q.NivelLugar})" : ""), 15,
                done ? new Color(0.55f, 0.9f, 0.5f) : trav ? new Color(1f, 0.45f, 0.4f) : Gold)));
            var line = done ? $"Volte a {q.Giver}" + (q.Where.Length > 0 ? $" ({q.Where})" : "") : $"{q.Hint}  {q.Progress}/{q.Count}";
            var l = MakeLabel(line, 12, new Color(0.88f, 0.85f, 0.78f), 3);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(CardWidth - 20f, 0);
            _questBox.AddChild(l);
        }
    }

    /// <summary>Ganhos recentes no canto inferior direito ("Obteve: Casca de Cal ×1").</summary>
    public void Feed(string text, Color? color = null)
    {
        var label = MakeLabel(text, 13, color ?? new Color(0.95f, 0.88f, 0.70f), 3);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(SideColumnWidth - 24f, 0);
        var panel = Framed(label, 5f, 0.7f);
        _feedBox.AddChild(panel);
        _feed.Add((panel, 0));
        while (_feed.Count > 5) { _feed[0].Node.QueueFree(); _feed.RemoveAt(0); }
    }

    private Label? _coinLabel;
    private Control? _coinPanel;
    private long _coinSum;

    /// <summary>Moedas ganhas em sequência viram UMA linha que vai somando (antes: 5 caixas "+3 moedas" empilhadas).</summary>
    public void FeedCoins(long gained)
    {
        var idx = _coinPanel is null ? -1 : _feed.FindIndex(x => x.Node == _coinPanel);
        if (idx >= 0 && _feed[idx].Age < 4.0 && _coinLabel is not null)
        {
            _coinSum += gained;
            _coinLabel.Text = $"+{_coinSum} moedas";
            _feed[idx] = (_feed[idx].Node, 0);
            return;
        }
        _coinSum = gained;
        Feed($"+{gained} moedas", new Color(1f, 0.85f, 0.35f));
        _coinPanel = _feed[^1].Node;
        _coinLabel = FindLabel(_coinPanel);
    }

    private static Label? FindLabel(Node n)
    {
        if (n is Label l) return l;
        foreach (var c in n.GetChildren()) if (FindLabel(c) is { } x) return x;
        return null;
    }

    private void UpdateFeed(double delta)
    {
        for (var i = _feed.Count - 1; i >= 0; i--)
        {
            var (node, age) = _feed[i];
            age += delta;
            _feed[i] = (node, age);
            node.Modulate = new Color(1, 1, 1, (float)Math.Clamp(6.0 - age, 0, 1));
            if (age > 6.0) { node.QueueFree(); _feed.RemoveAt(i); }
        }
    }

    public void FlashDamage(int damage) => _vignetteAlpha = Math.Min(0.35, _vignetteAlpha + 0.12 + damage * 0.0008);

    private const string HintShort = "H — ver teclas · F11 tela cheia";
    private const string HintFull = "WASD andar · Shift corre (fica correndo) · rodinha zoom (travado: troca de alvo)\n" +
        "Clique atacar · Q E R T poderes · K livro de poderes\nEspaço esquivar · Botão do meio trava/destrava alvo · Tab ou rodinha trocam · F falar/pegar\nGuarda na hora certa = aparar (deixa o inimigo aberto)\n" +
        "1 vida · 2 mana/comida · I mochila · J missões\nC atributos · M mapa · P grupo · U trocar · H recolher teclas\n" +
        "V montaria · G voar (asas) · N minimapa · B companheiro · F3 trilha do herói · F10 gráficos · F11 tela cheia";

    public void ToggleHints() => _hint.Text = _hint.Text == HintShort ? HintFull : HintShort;

    private static void SetBar(ProgressBar bar, Label? text, double value, double max)
    {
        bar.Value = max > 0 ? Math.Clamp(value / max, 0.0, 1.0) : 0.0;
        if (text is not null) text.Text = $"{value:0} / {max:0}";
    }

    private Texture2D? _tecla2Padrao;

    private void UpdateSlots(ClientModel m, CooldownTracker cd, long now, CinzasDaMare.Shared.Net.PlayerView? me, long dodgeReadyInMs)
    {
        AvisoDaMascara(m, me);
        if (_slots.Count < 9) return;
        var busy = me?.Action.StartsWith("windup", StringComparison.Ordinal) == true;
        var resource = me?.Resource ?? 0;

        // [0] básico  [1] Q  [2] E  [3] esquiva  [4] poção  [5] R  [6] T  [7] tecla 2
        SetSlotIcon(0, SkillIcon(m.BasicSkill?.Id), m.BasicSkill is { } bs ? Short(bs.DisplayName) : "");
        if (_slots[3].Icon is TextureRect { Texture: null }) SetSlotIcon(3, SkillIcon("esquiva"), "Esq.");
        if (_slots[7].Icon is TextureRect { Texture: null }) SetSlotIcon(7, SkillIcon(m.ClassId is "mago" or "bruxa" ? "mana" : "comida"), "Com.");
        StyleSlot(0, 0.0, busy);
        StyleSlot(3, Math.Clamp(dodgeReadyInMs / 850.0, 0.0, 1.0), me?.Action == "dodge");
        StyleSlot(4, cd.Fraction(CooldownTracker.Potion, now), false);
        // 13/11 ("quando coloca no número 1, faz a troca completa"): o quadro 1 mostra o tônico escolhido — o desenho dele
        // e quantos desse ainda tem; sem escolha (ou acabou) volta ao frasco padrão e à soma dos tônicos
        _pocaoPadrao ??= (_slots[4].Icon as TextureRect)?.Texture;
        var escolhido = Main.Pocao1();
        var qtd = escolhido is null ? 0 : m.CountInInventory(escolhido);
        if (escolhido is not null && qtd > 0)
        {
            SetSlotIcon(4, ItemIconRenderer.Get(m.ItemInfoFor(escolhido), escolhido) ?? _pocaoPadrao, "PV");
            _slots[4].Count.Text = qtd.ToString();
        }
        else if (new[] { "consumable_c01", "consumable_c02", "consumable_c05" }
                     .Select(pre => m.Inventory.FirstOrDefault(i => i.DefinitionId.StartsWith(pre, StringComparison.Ordinal) && i.Quantity > 0))
                     .FirstOrDefault(i => i is not null) is { } usa)
        {
            // 13/11 (2ª vez: "o ícone da poção fraca no 1 tem que ser igual à prévia do item"): sem escolha, o quadro 1 mostra
            // o desenho do tônico que a tecla 1 vai usar de fato (o primeiro: fraco, médio, depois o resto)
            SetSlotIcon(4, ItemIconRenderer.Get(m.ItemInfoFor(usa.DefinitionId), usa.DefinitionId) ?? _pocaoPadrao, "PV");
            _slots[4].Count.Text = m.CountInInventory(usa.DefinitionId).ToString();
        }
        else
        {
            if (_pocaoPadrao is not null) SetSlotIcon(4, _pocaoPadrao, "PV");
            _slots[4].Count.Text = (m.CountInInventory("consumable_c01_tonico_fraco") + m.CountInInventory("consumable_c02_tonico_medio")).ToString();
        }
        var mageSlot = m.ClassId is "mago" or "bruxa";
        StyleSlot(7, cd.Fraction(mageSlot ? CooldownTracker.Mana : CooldownTracker.Food, now), false);
        _tecla2Padrao ??= (_slots[7].Icon as TextureRect)?.Texture;
        if (Main.Tecla2() is { } t2 && m.CountInInventory(t2) > 0)
        {
            SetSlotIcon(7, ItemIconRenderer.Get(m.ItemInfoFor(t2), t2) ?? _tecla2Padrao, "");
            _slots[7].Count.Text = m.CountInInventory(t2).ToString();
        }
        else
        {
            if (_tecla2Padrao is not null) SetSlotIcon(7, _tecla2Padrao, "");
            var focos = m.CountInInventory("consumable_c08_frasco_foco") + m.CountInInventory("consumable_c09_frasco_foco_grande");
            if (!mageSlot && m.ClassId == "arqueiro" && focos > 0)
            {
                // 14/11: Arqueiro com frasco de Foco — a tecla 2 usa ele (laranja) antes da comida
                var f = m.CountInInventory("consumable_c08_frasco_foco") > 0 ? "consumable_c08_frasco_foco" : "consumable_c09_frasco_foco_grande";
                SetSlotIcon(7, ItemIconRenderer.Get(m.ItemInfoFor(f), f) ?? _tecla2Padrao, "PF");
            }
            _slots[7].Count.Text = !mageSlot && m.ClassId == "arqueiro" && focos > 0 ? focos.ToString() : mageSlot
            ? (m.CountInInventory("consumable_c03_frasco_mana") + m.CountInInventory("consumable_c04_frasco_mana_grande")).ToString()
            : m.Inventory.Where(i => i.DefinitionId.StartsWith("food_", StringComparison.Ordinal)).Sum(i => i.Quantity).ToString();
            if (_slots[7].Count.Text == "0") _slots[7].Count.Text = "";   // 14/11: "frigideira com número 0" — sem nada, sem número
        }

        // poderes: o que estiver em cada tecla (Livro de Poderes, K). Tecla sem poder = quadrado vazio.
        void Dica(int i, string t) { if (_slots[i].Painel is { } p && p.TooltipText != t) p.TooltipText = t; }
        Dica(0, m.BasicSkill is { } b0 ? $"{b0.DisplayName} (clique esquerdo)" : "Ataque básico (clique esquerdo)");
        Dica(3, "Esquiva (Espaço)");
        Dica(4, "Tônico de vida (tecla 1)" + (escolhido is not null ? $": {ClientModel.DisplayName(escolhido)}" : ""));
        Dica(7, (Main.Tecla2() is { } t2d ? ClientModel.DisplayName(t2d) : mageSlot ? "Frasco de mana" : "Comida") + " (tecla 2)");
        foreach (var (idx, key) in new[] { (1, "Q"), (2, "E"), (5, "R"), (6, "T"), (8, "Dir") })
        {
            var sk = m.SkillForKey(key);
            Dica(idx, sk is null ? $"Tecla {key}: vazia (escolha um poder no Livro de Poderes, K)" : $"{sk.DisplayName} ({key}) · custo {sk.ResourceCost:0}" + (sk.Description is { Length: > 0 } dd ? "\n" + dd : ""));
            SetSlotIcon(idx, SkillIcon(sk?.Id), sk is null ? "" : Short(sk.DisplayName));
            _slots[idx].Count.Text = sk is not null && sk.ResourceCost > resource ? "!" : "";
            StyleSlot(idx, sk is null ? 0 : cd.Fraction(sk.Id, now), idx == 2 && me?.Guarding == true && sk?.Id == ClientModel.SkillGuarda);
        }
    }

    private Label? _avisoMascara;
    /// <summary>17/11 (dono: "algo em cima da barra quando usa a Máscara do Pandemônio, dizendo que X aciona o poder"):
    /// com a máscara vestida, um aviso verde em cima da barra — o que o X faz agora.</summary>
    private void AvisoDaMascara(ClientModel m, CinzasDaMare.Shared.Net.PlayerView? me)
    {
        var veste = m.Equipment.Any(e => e.DefinitionId == "mascara_pandemonio");
        if (!veste) { if (_avisoMascara is not null) _avisoMascara.Visible = false; return; }
        if (_avisoMascara is null)
        {
            _avisoMascara = MakeLabel("", 16, new Color(0.55f, 1f, 0.4f), 4);
            _avisoMascara.HorizontalAlignment = HorizontalAlignment.Center;
            _barra.AddChild(_avisoMascara);
            _avisoMascara.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
            _avisoMascara.GrowHorizontal = Control.GrowDirection.Both;
            _avisoMascara.GrowVertical = Control.GrowDirection.Begin;
            _avisoMascara.OffsetTop = -6; _avisoMascara.OffsetBottom = -6;
        }
        var forma = me?.Form == "forma_pandemonio";
        var girando = me?.Action.StartsWith("active:skill_mascara_furacao", StringComparison.Ordinal) == true;
        _avisoMascara.Text = girando ? "[X] para o Furacão" : forma ? "[X] Furacão do Pandemônio — gira até você apertar X de novo" : "[X] Máscara do Pandemônio — libera a fúria";
        _avisoMascara.Visible = true;
    }

    /// <summary>Rótulo curto para o quadrado da barra (iniciais das palavras grandes: "Corte que Avança" → "CA").</summary>
    private static string Short(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToArray();
        return words.Length == 0 ? name[..Math.Min(3, name.Length)] : string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
    }

    private void StyleSlot(int index, double cooldownFraction, bool active)
    {
        var slot = _slots[index];
        slot.Style.BorderColor = active ? new Color(0.95f, 0.75f, 0.35f) : new Color(0.36f, 0.28f, 0.17f);
        slot.Cooldown.Position = Vector2.Zero;
        slot.Cooldown.Size = new Vector2(SlotSize, SlotSize * (float)Math.Clamp(cooldownFraction, 0.0, 1.0));
    }

    // ------------------------------------------------------------ nameplates e números flutuantes

    public void BeginPlates()
    {
        foreach (var plate in _plates.Values) plate.Seen = false;
    }

    public void UpdatePlate(string id, string text, double fraction, bool showBar, Color barColor,
        Vector3 world, Camera3D camera, bool visible)
    {
        if (!_plates.TryGetValue(id, out var plate))
        {
            plate = CreatePlate(barColor);
            _plates[id] = plate;
            _placas.AddChild(plate.Root);
        }
        plate.Seen = true;
        var show = visible && !camera.IsPositionBehind(world);
        plate.Root.Visible = show;
        if (!show) return;

        plate.Name.Text = text;
        plate.BarHolder.Visible = showBar;
        // cor da barra acompanha a chamada (antes ficava a da criação: placa criada escondida saía PRETA)
        if (plate.Bar.GetThemeStylebox("fill") is StyleBoxFlat fs && fs.BgColor != barColor && barColor.A > 0.01f) fs.BgColor = barColor;
        plate.Bar.Value = Math.Clamp(fraction, 0.0, 1.0);
        var screen = camera.UnprojectPosition(world);
        plate.Root.Position = screen - new Vector2(PlateWidth / 2f, 30f);
    }

    public void EndPlates()
    {
        foreach (var id in _plates.Where(p => !p.Value.Seen).Select(p => p.Key).ToList())
        {
            _plates[id].Root.QueueFree();
            _plates.Remove(id);
        }
    }

    private static Plate CreatePlate(Color barColor)
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(PlateWidth, 0f) };
        box.AddThemeConstantOverride("separation", 1);
        var name = MakeLabel("", 15, new Color(0.94f, 0.92f, 0.88f), 4);
        if (TitleFont is not null) name.AddThemeFontOverride("font", TitleFont);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        var bar = MakeBar(barColor, new Vector2(PlateWidth - 24f, 7f));
        // barra séria: fundo quase preto com aro de ferro (como a prancha)
        if (bar.GetThemeStylebox("background") is StyleBoxFlat pb) { pb.BgColor = new Color(0.03f, 0.03f, 0.03f, 0.9f); pb.BorderColor = new Color(0.30f, 0.29f, 0.27f); pb.SetCornerRadiusAll(0); }
        if (bar.GetThemeStylebox("fill") is StyleBoxFlat pf) pf.SetCornerRadiusAll(0);
        var holder = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.AddChild(bar);
        box.AddChild(name);
        box.AddChild(holder);
        return new Plate { Root = box, Name = name, Bar = bar, BarHolder = holder };
    }

    public void SpawnFloater(Vector3 world, string text, Color color, int fontSize = 22)
    {
        var label = MakeLabel(text, fontSize, color);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        Root.AddChild(label);
        _floaters.Add(new Floater { Label = label, World = world });
    }

    public void UpdateFloaters(Camera3D camera, double delta)
    {
        for (var i = _floaters.Count - 1; i >= 0; i--)
        {
            var f = _floaters[i];
            f.Age += delta;
            if (f.Age > 1.1)
            {
                f.Label.QueueFree();
                _floaters.RemoveAt(i);
                continue;
            }
            var screen = camera.UnprojectPosition(f.World + new Vector3(0f, (float)(f.Age * 1.3), 0f));
            f.Label.Position = screen - new Vector2(20f, 14f);
            f.Label.Modulate = new Color(1f, 1f, 1f, (float)Math.Clamp(1.25 - f.Age, 0.0, 1.0));
        }
    }

    public void ClearTransient()
    {
        foreach (var plate in _plates.Values) plate.Root.QueueFree();
        _plates.Clear();
        foreach (var floater in _floaters) floater.Label.QueueFree();
        _floaters.Clear();
    }
}


/// <summary>14/11: alvo de soltar do quadro da tecla 2 (escolhe o consumível que a tecla usa).</summary>
public partial class SlotTecla2Drop : Control
{
    public static event System.Action<string>? Escolheu;
    public override bool _CanDropData(Vector2 at, Variant data)
        => data.VariantType == Variant.Type.String && data.AsString().StartsWith("item|bag|");
    public override void _DropData(Vector2 at, Variant data)
    {
        var p = data.AsString().Split('|');
        if (p.Length >= 3) Escolheu?.Invoke(p[2]);
    }
}

/// <summary>13/11: alvo de soltar do quadro da tecla 1 (escolhe o tônico que a tecla usa).</summary>
public partial class SlotPocaoDrop : Control
{
    public static event System.Action<string>? Escolheu;
    public override bool _CanDropData(Vector2 at, Variant data)
        => data.VariantType == Variant.Type.String && data.AsString().StartsWith("item|bag|");
    public override void _DropData(Vector2 at, Variant data)
    {
        var p = data.AsString().Split('|');
        if (p.Length >= 3) Escolheu?.Invoke(p[2]);
    }
}

/// <summary>14/11: bolinha do descanso — anel dourado que enche; cheio = "✓" (o buff entrou).</summary>
public sealed partial class AnelDescanso : Control
{
    public float T;
    public bool Cama;

    public override void _Draw()
    {
        var c = Size / 2f;
        var r = Mathf.Min(Size.X, Size.Y) / 2f - 4f;
        DrawCircle(c, r + 3f, new Color(0.05f, 0.04f, 0.03f, 0.75f));
        DrawArc(c, r, 0, Mathf.Tau, 48, new Color(0.35f, 0.3f, 0.22f, 0.9f), 3f, true);
        var cheio = T >= 1f;
        var cor = cheio ? new Color(0.45f, 0.95f, 0.55f) : Cama ? new Color(0.55f, 0.75f, 1f) : new Color(1f, 0.82f, 0.35f);
        if (T > 0) DrawArc(c, r, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Mathf.Clamp(T, 0, 1), 48, cor, 3f, true);
        var font = HudView.BodyFont ?? ThemeDB.FallbackFont;
        var txt = cheio ? "✓" : Cama ? "Zz" : $"{Mathf.Clamp(T, 0, 1) * 100:0}%";
        var tam = cheio ? 16 : 10;
        var w = font.GetStringSize(txt, HorizontalAlignment.Left, -1, tam).X;
        DrawString(font, c + new Vector2(-w / 2f, tam * 0.35f), txt, HorizontalAlignment.Left, -1, tam, cor);
        var sub = cheio ? (Cama ? "Bem Dormido!" : "Descansado!") : Cama ? "dormindo..." : "descansando...";
        var ws = font.GetStringSize(sub, HorizontalAlignment.Left, -1, 10).X;
        DrawString(font, c + new Vector2(-ws / 2f, r + 15f), sub, HorizontalAlignment.Left, -1, 10, new Color(0.9f, 0.85f, 0.7f));
    }
}

/// <summary>18/11: botãozinho da lupa (desenhado, sem depender de fonte com emoji).</summary>
public sealed partial class BotaoLupa : Control
{
    public Action? Clicou;
    private bool _sobre;
    public BotaoLupa() { MouseFilter = MouseFilterEnum.Stop; MouseDefaultCursorShape = CursorShape.PointingHand; }
    public override void _Notification(int what)
    {
        if (what == NotificationMouseEnter) { _sobre = true; QueueRedraw(); }
        else if (what == NotificationMouseExit) { _sobre = false; QueueRedraw(); }
    }
    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { Clicou?.Invoke(); AcceptEvent(); }
    }
    public override void _Draw()
    {
        var c = Size / 2f;
        var r = Mathf.Min(Size.X, Size.Y) / 2f - 1f;
        DrawCircle(c, r, new Color(0.08f, 0.06f, 0.05f, _sobre ? 0.95f : 0.8f));
        DrawArc(c, r, 0, Mathf.Tau, 32, new Color(0.75f, 0.58f, 0.24f), 1.6f, true);
        var ouro = _sobre ? new Color(1f, 0.88f, 0.5f) : new Color(0.95f, 0.78f, 0.3f);
        var lente = c + new Vector2(-2.5f, -2.5f);
        DrawArc(lente, r * 0.38f, 0, Mathf.Tau, 24, ouro, 2.4f, true);
        DrawLine(lente + new Vector2(r * 0.28f, r * 0.28f), lente + new Vector2(r * 0.62f, r * 0.62f), ouro, 3.2f, true);
    }
}

/// <summary>18/11 15h: quadradinho de efeito (buff, noite, escudo, veneno...) com o tempo girando por dentro.</summary>
public sealed partial class IconeEfeito : Control
{
    public HudView.Efeito? Efeito;
    public IconeEfeito() { MouseFilter = MouseFilterEnum.Stop; }

    public override void _Draw()
    {
        if (Efeito is not { } e) return;
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, new Color(0.04f, 0.035f, 0.035f, 0.92f));
        // tempo: a parte que já passou escurece girando (sentido horário, de cima)
        if (e.Restante is not null && e.Fracao < 0.999)
        {
            var c = Size / 2f; var raio = Size.Length() / 2f;
            var pts = new List<Vector2> { c };
            var ini = -Mathf.Pi / 2f; var fim = ini + Mathf.Tau * (float)(1 - e.Fracao);
            for (var k = 0; k <= 24; k++) { var a = ini + (fim - ini) * k / 24f; pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * raio); }
            var tinta = new Color(e.Cor.R * 0.35f, e.Cor.G * 0.35f, e.Cor.B * 0.35f, 0.9f);
            DrawRect(r, e.Cor with { A = 0.28f });
            Vector2 Cl(Vector2 p) => new(Mathf.Clamp(p.X, 0, Size.X), Mathf.Clamp(p.Y, 0, Size.Y));
            var cores = new[] { tinta, tinta, tinta };
            for (var k = 1; k < pts.Count - 1; k++)
                DrawPrimitive(new[] { c, Cl(pts[k]), Cl(pts[k + 1]) }, cores, new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero });
        }
        else DrawRect(r, e.Cor with { A = 0.28f });
        DrawRect(r, e.Ruim ? new Color(1f, 0.35f, 0.3f) : e.Cor, false, 1.5f);
        var f = HudView.BodyFont ?? ThemeDB.FallbackFont;
        var sz = f.GetStringSize(e.Simbolo, HorizontalAlignment.Left, -1, 16);
        DrawString(f, new Vector2((Size.X - sz.X) / 2f, Size.Y / 2f + 6f), e.Simbolo, HorizontalAlignment.Left, -1, 16, Colors.White);
    }
}
