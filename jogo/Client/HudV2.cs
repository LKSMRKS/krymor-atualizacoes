using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Barra de baixo da HUD v2 (arte de 10/11, feita a partir das referências do dono): placa de ferro (ui_barra_inferior,
/// 1800×220) com os dois orbes nas pontas (x 380 / 1420, y 112), a moldura da XP por cima da placa e os quadros de
/// habilidade dentro dela. Tudo em coordenadas da arte, multiplicadas pela escala da tela.
/// </summary>
public sealed partial class BarraInferior : Control
{
    public const float ArtW = 1800f, ArtH = 220f;
    public float Escala { get; private set; } = 0.75f;
    public OrbeHud Vida { get; } = new() { MouseFilter = MouseFilterEnum.Ignore, Caption = "Vida" };
    public OrbeHud Recurso { get; } = new() { MouseFilter = MouseFilterEnum.Ignore, Caption = "Mana", Liquid = new Color(0.16f, 0.32f, 0.85f) };
    public Control Habilidades { get; }
    public ProgressBar Xp { get; }
    public ProgressBar Folego { get; }

    private readonly TextureRect _placa, _xpMoldura;
    private Vector2 _lastViewport;
    /// <summary>Chamado quando a escala muda (a HUD refaz os quadros de habilidade no tamanho novo).</summary>
    public event Action<float>? Reescalou;

    /// <summary>11/11: barra da gárgula e do dragão (potes de líquido) quando a arte e o shader estão no jogo.</summary>
    public static bool GargulaDisponivel =>
        ResourceLoader.Exists("res://Assets/Shaders/pote_liquido.gdshader") && VisualAssets.Load("ui_barra_gargula.png") is not null;

    private readonly BarraGargula? _gargula;
    private readonly Label? _vidaTexto, _manaTexto;
    private float W => _gargula is null ? ArtW : BarraGargula.W;
    private float H => _gargula is null ? ArtH : BarraGargula.H;

    public BarraInferior(Control hotbar, ProgressBar xp, ProgressBar stamina)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        if (hotbar is SlotsGargula)
        {
            // gárgula (pote de vida) e dragão (pote de mana): uma arte só; os orbes ficam escondidos só para guardar os valores
            _gargula = new BarraGargula();
            AddChild(_gargula);
            _placa = new TextureRect(); _xpMoldura = new TextureRect();
            Xp = xp; AddChild(xp);
            Folego = stamina; AddChild(stamina);
            Habilidades = hotbar; AddChild(hotbar);
            Vida.Visible = false; Recurso.Visible = false;
            AddChild(Vida); AddChild(Recurso);
            _vidaTexto = HudView.MakeLabel("", 15, new Color(1f, 0.92f, 0.86f), 4);
            _manaTexto = HudView.MakeLabel("", 15, new Color(0.88f, 0.92f, 1f), 4);
            foreach (var l in new[] { _vidaTexto, _manaTexto }) { l.HorizontalAlignment = HorizontalAlignment.Center; AddChild(l); }
            return;
        }
        _placa = new TextureRect { Texture = VisualAssets.Load("ui_barra_inferior.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_placa);
        // moldura da XP atrás, a barra por dentro dela (o miolo da moldura é escuro)
        _xpMoldura = new TextureRect { Texture = VisualAssets.Load("ui_xp_moldura.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_xpMoldura);
        Xp = xp; AddChild(xp);
        Folego = stamina; AddChild(stamina);
        Habilidades = hotbar; AddChild(hotbar);
        AddChild(Vida); AddChild(Recurso);
    }

    public override void _Process(double delta)
    {
        var vp = GetViewportRect().Size;
        // sempre centrada na base da tela (fora dos contêineres da HUD)
        Position = new Vector2((vp.X - W * Escala) / 2f, vp.Y - H * Escala - 4f).Floor();
        if (_gargula is not null) { ProcessGargula(vp); return; }
        if (vp == _lastViewport) return;
        _lastViewport = vp;
        // a barra ocupa ~62% da largura (deixa as colunas de registro dos lados), sem ficar pequena demais nem alta demais
        var s = Mathf.Clamp(vp.X * 0.62f / ArtW, 0.42f, 0.85f);
        s = Mathf.Min(s, vp.Y * 0.21f / ArtH);
        Escala = s;
        CustomMinimumSize = new Vector2(ArtW * s, ArtH * s);
        Size = CustomMinimumSize;
        _placa.Position = Vector2.Zero; _placa.Size = new Vector2(ArtW, ArtH) * s;
        // XP sobre a placa, com a moldura de ferro por cima; fôlego fininho logo acima
        _xpMoldura.Position = new Vector2(480, 40) * s; _xpMoldura.Size = new Vector2(840, 44) * s;
        var xpPos = new Vector2(506, 52) * s; var xpSize = new Vector2(788, 20) * s;
        Xp.Position = xpPos; Xp.Size = xpSize; Xp.CustomMinimumSize = xpSize;
        Folego.Position = new Vector2(600, 30) * s; Folego.Size = new Vector2(600, 6) * s; Folego.CustomMinimumSize = Folego.Size;
        // orbes: o vidro tem 196/640 de raio; aro inteiro de 330 px de arte
        var d = 330f * s;
        Vida.Size = Vida.CustomMinimumSize = new Vector2(d, d + 18f);
        Recurso.Size = Recurso.CustomMinimumSize = new Vector2(d, d + 18f);
        Vida.Position = new Vector2(380, 112) * s - new Vector2(d / 2f, d / 2f);
        Recurso.Position = new Vector2(1420, 112) * s - new Vector2(d / 2f, d / 2f);
        Reescalou?.Invoke(s);
        Habilidades.Position = new Vector2(552, 96) * s;
    }

    private void ProcessGargula(Vector2 vp)
    {
        var g = _gargula!;
        g.Vida = Vida.Max > 0 ? Vida.Value / Vida.Max : 0f;
        g.Mana = Recurso.Max > 0 ? Recurso.Value / Recurso.Max : 0f;
        g.CorMana(Recurso.Liquid);
        _vidaTexto!.Text = $"{Vida.Value:0}/{Vida.Max:0}";
        _manaTexto!.Text = $"{Recurso.Value:0}/{Recurso.Max:0}";
        if (vp == _lastViewport) return;
        _lastViewport = vp;
        // ~60% da largura, sem passar de 26% da altura da tela
        var s = Mathf.Clamp(vp.X * 0.60f / BarraGargula.W, 0.32f, 0.8f);
        s = Mathf.Min(s, vp.Y * 0.26f / BarraGargula.H);
        Escala = s;
        CustomMinimumSize = new Vector2(BarraGargula.W * s, BarraGargula.H * s);
        Size = CustomMinimumSize;
        g.Escala = s;
        var xr = g.XpRect;
        Xp.Position = xr.Position * s; Xp.Size = Xp.CustomMinimumSize = xr.Size * s;
        Folego.Position = new Vector2(xr.Position.X + xr.Size.X * 0.15f, xr.Position.Y - 9f) * s;
        Folego.Size = Folego.CustomMinimumSize = new Vector2(xr.Size.X * 0.7f, 5f) * s;
        if (Habilidades is SlotsGargula slots) { slots.Escala = s; slots.Rects = Enumerable.Range(0, BarraGargula.Slots).Select(i => g.SlotRect(i)).ToArray(); }
        Habilidades.Position = Vector2.Zero; Habilidades.Size = Size;
        Reescalou?.Invoke(s * 90f / 80f);   // o quadro da gárgula tem 90 px de arte (o da placa antiga, 80)
        foreach (var (l, c) in new[] { (_vidaTexto!, BarraGargula.CentroVida), (_manaTexto!, BarraGargula.CentroMana) })
        {
            l.Size = new Vector2(160, 24);
            l.Position = c * s + new Vector2(-80, -14f * s);   // 11/11 noite: mais alto, no meio do pote (longe da animação da base)
        }
    }
}

/// <summary>Quadros de habilidade da barra da gárgula: cada filho vai para o retângulo da arte (8 quadros, o 5º maior).</summary>
public sealed partial class SlotsGargula : Container
{
    public float Escala { get; set; } = 0.5f;
    public Rect2[] Rects { get; set; } = Array.Empty<Rect2>();

    public SlotsGargula() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren) return;
        var i = 0;
        foreach (var child in GetChildren())
        {
            if (child is not Control c || i >= Rects.Length) continue;
            var r = Rects[i++];
            FitChildInRect(c, new Rect2(r.Position * Escala, r.Size * Escala));
        }
    }

    public override void _Process(double delta) => QueueSort();
}

/// <summary>Barra do alvo/chefe (ui_barra_alvo / ui_barra_chefe): nome em serifa por cima, vida vermelha dentro da moldura.</summary>
public sealed partial class BarraAlvo : Control
{
    public string Nome { get; set; } = "";
    public double Fracao { get; set; } = 1;
    public bool Chefe { get; set; }
    public bool Missao { get; set; }
    private double _shown = 1;

    public override void _Process(double delta)
    {
        _shown = Mathf.Lerp((float)_shown, (float)Fracao, 1f - Mathf.Exp(-8f * (float)delta));
        QueueRedraw();
    }

    public override void _Draw()
    {
        var w = Size.X;
        var font = HudView.TitleFont ?? ThemeDB.FallbackFont;
        var fs = Chefe ? 19 : 16;
        var ts = font.GetStringSize(Nome, HorizontalAlignment.Left, -1, fs);
        var ty = fs + 2f;
        DrawString(font, new Vector2(w / 2f - ts.X / 2f + 1.5f, ty + 1.5f), Nome, HorizontalAlignment.Left, -1, fs, new Color(0, 0, 0, 0.9f));
        DrawString(font, new Vector2(w / 2f - ts.X / 2f, ty), Nome, HorizontalAlignment.Left, -1, fs, new Color(0.95f, 0.91f, 0.84f));
        if (Missao) CompassStrip.DrawQuestStone(this, new Vector2(w / 2f - ts.X / 2f - 12f, ty - fs * 0.35f), -1, false, 6f);   // alvo de missão
        var tex = VisualAssets.Load(Chefe ? "ui_barra_chefe.png" : "ui_barra_alvo.png");
        var barH = Chefe ? 30f : 18f;
        var bar = new Rect2(0, ty + 8f, w, barH);
        // miolo: fundo escuro e a vida (vermelho com brilho na beirada de cima)
        var inner = bar.Grow(-barH * 0.28f);
        inner.Position += new Vector2(w * (Chefe ? 0.03f : 0.02f), 0); inner.Size -= new Vector2(w * (Chefe ? 0.06f : 0.04f), 0);
        // 11/11 ("a barra está vazia"): a moldura tem o miolo opaco e cobria a vida — moldura primeiro, vida por cima
        if (tex is not null) DrawTextureRect(tex, bar, false);
        else DrawRect(bar, new Color(0.4f, 0.38f, 0.35f), false, 1.5f);
        DrawRect(inner, new Color(0.05f, 0.01f, 0.01f, 0.92f));
        var fill = new Rect2(inner.Position, new Vector2(inner.Size.X * (float)Math.Clamp(_shown, 0, 1), inner.Size.Y));
        DrawRect(fill, new Color(0.62f, 0.07f, 0.06f));
        DrawRect(new Rect2(fill.Position, new Vector2(fill.Size.X, Mathf.Max(1f, fill.Size.Y * 0.3f))), new Color(0.95f, 0.30f, 0.22f, 0.6f));
    }
}

/// <summary>
/// Grade estilo MU/Diablo (mochila e baú): casas quadradas, cada item desenhado ocupando o seu tamanho (espada 1×3,
/// armadura 2×3…). Arrastar mostra onde o item vai cair (verde = cabe, vermelho = não cabe). Só pede: o servidor decide.
/// </summary>
public sealed partial class GradeItens : Control
{
    public int Colunas { get; set; } = 8;
    public int Linhas { get; set; } = 8;
    public float Casa { get; set; } = 40f;
    /// <summary>"bag" (mochila) ou "store:&lt;container&gt;" (baú/armazém): vai no pacote do arrastar.</summary>
    public string Tipo { get; set; } = "bag";
    /// <summary>14/11 (bolsinha): definição de um item que está na OUTRA grade (joia da bolsinha solta numa arma da mochila).</summary>
    public Func<string, string?>? DefDe { get; set; }
    public Func<string, ItemInfo?>? Info { get; set; }
    /// <summary>Itens (id, definição, qualidade, quantidade, casa do canto, largura, altura).</summary>
    public List<(string Id, string Def, string Quality, int Qty, int Slot, int W, int H)> Itens { get; } = new();
    public string? Selecionado { get; set; }

    public event Action<string, string, int>? Soltou;          // (id, origem "bag"/"store:x"/"equip:slot", casa de destino)
    public event Action<string, bool, bool>? Clicou;            // (id, duplo, direito)
    public event Action<string>? SoltouFora;                    // arrastou da mochila para fora: largar no chão
    /// <summary>11/11: joia solta em cima de outro item = refinar (joia, alvo).</summary>
    public event Action<string, string>? Refinar;
    /// <summary>+N de refino de cada item (desenhado no canto).</summary>
    public Func<string, int>? Plus { get; set; }

    private int _hoverSlot = -1;
    private (int W, int H) _dragSize = (1, 1);
    private string? _dragOver;
    private static readonly Color CasaFundo = new(0.055f, 0.05f, 0.048f, 0.95f);
    private static readonly Color CasaBorda = new(0.30f, 0.27f, 0.22f);

    public GradeItens()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ItemIconRenderer.IconReady += OnIcon;
        TreeExiting += () => ItemIconRenderer.IconReady -= OnIcon;
    }

    private void OnIcon() => QueueRedraw();

    public void Ajustar()
    {
        CustomMinimumSize = new Vector2(Colunas * Casa + 2, Linhas * Casa + 2);
        QueueRedraw();
    }

    private int SlotAt(Vector2 p)
    {
        var x = (int)(p.X / Casa); var y = (int)(p.Y / Casa);
        if (x < 0 || y < 0 || x >= Colunas || y >= Linhas) return -1;
        return y * Colunas + x;
    }

    private (string Id, string Def, string Quality, int Qty, int Slot, int W, int H)? ItemAt(int slot)
    {
        if (slot < 0) return null;
        var (cx, cy) = (slot % Colunas, slot / Colunas);
        foreach (var it in Itens)
        {
            var (x0, y0) = (it.Slot % Colunas, it.Slot / Colunas);
            if (cx >= x0 && cx < x0 + it.W && cy >= y0 && cy < y0 + it.H) return it;
        }
        return null;
    }

    private bool Cabe(int slot, (int W, int H) size, string? ignorar)
    {
        if (slot < 0) return false;
        var (x0, y0) = (slot % Colunas, slot / Colunas);
        if (x0 + size.W > Colunas || y0 + size.H > Linhas) return false;
        foreach (var it in Itens)
        {
            if (it.Id == ignorar) continue;
            var (ix, iy) = (it.Slot % Colunas, it.Slot / Colunas);
            if (x0 < ix + it.W && ix < x0 + size.W && y0 < iy + it.H && iy < y0 + size.H) return false;
        }
        return true;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm)
        {
            var s = SlotAt(mm.Position);
            if (s != _hoverSlot) { _hoverSlot = s; QueueRedraw(); }
            var it = ItemAt(s);
            TooltipText = it is { } i ? "item:" + i.Id + ":" + s : "";   // 11/11: texto único por casa — o Godot só troca o quadro quando o texto muda
        }
        if (e is InputEventMouseButton { Pressed: true } mb && (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right))
            if (ItemAt(SlotAt(mb.Position)) is { } it)
            {
                Selecionado = it.Id;
                Clicou?.Invoke(it.Id, mb.DoubleClick, mb.ButtonIndex == MouseButton.Right);
                QueueRedraw();
            }
    }

    public override string _GetTooltip(Vector2 atPosition)
        => ItemAt(SlotAt(atPosition)) is { } i ? "item:" + i.Id + ":" + SlotAt(atPosition) : "";

    public override Control? _MakeCustomTooltip(string forText)
    {
        var slot = _hoverSlot;
        var parts = forText.Split(':');
        if (parts.Length >= 3 && int.TryParse(parts[^1], out var ps)) slot = ps;
        if (ItemAt(slot) is not { } it) return null;
        return ItemTooltip.Build(Info?.Invoke(it.Def), it.Def, it.Quality, it.Qty, null, it.Id);   // sem preço de compra: mostra "Vende por"
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit) { _hoverSlot = -1; _dragOver = null; QueueRedraw(); }
        if (what == NotificationDragEnd)
        {
            if (_dragging is { } id && !GetViewport().GuiIsDragSuccessful() && Tipo == "bag") SoltouFora?.Invoke(id);
            _dragging = null; _dragOver = null; QueueRedraw();
        }
    }

    private string? _dragging;

    public override Variant _GetDragData(Vector2 at)
    {
        if (ItemAt(SlotAt(at)) is not { } it) return default;
        _dragging = it.Id;
        var holder = new Control();
        var icon = new TextureRect
        {
            Texture = ItemIconRenderer.Get(Info?.Invoke(it.Def), it.Def), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Size = new Vector2(it.W, it.H) * Casa,
            Position = -new Vector2(it.W, it.H) * Casa / 2f, Modulate = new Color(1, 1, 1, 0.85f),
        };
        holder.AddChild(icon);
        SetDragPreview(holder);
        Sfx.Play("ui_clique", -8f);
        return $"item|{Tipo}|{it.Id}|{it.W}|{it.H}";
    }

    public override bool _CanDropData(Vector2 at, Variant data)
    {
        var p = data.AsString().Split('|');
        if (p.Length < 3 || p[0] != "item") return false;
        _dragSize = p.Length >= 5 ? (int.Parse(p[3]), int.Parse(p[4])) : (1, 1);
        // arrastando, o item "pega" pelo meio: a casa do canto é a do mouse menos metade do tamanho
        _hoverSlot = CornerFor(at, _dragSize);
        _dragOver = p[2];
        QueueRedraw();
        return true;
    }

    private int CornerFor(Vector2 at, (int W, int H) size)
    {
        var x = (int)Mathf.Floor(at.X / Casa - (size.W - 1) / 2f + 0.0001f);
        var y = (int)Mathf.Floor(at.Y / Casa - (size.H - 1) / 2f + 0.0001f);
        x = Math.Clamp(x, 0, Math.Max(0, Colunas - size.W)); y = Math.Clamp(y, 0, Math.Max(0, Linhas - size.H));
        return y * Colunas + x;
    }

    public override void _DropData(Vector2 at, Variant data)
    {
        var p = data.AsString().Split('|');
        var size = p.Length >= 5 ? (int.Parse(p[3]), int.Parse(p[4])) : (1, 1);
        var slot = CornerFor(at, size);
        _dragOver = null;
        // 11/11: joia em cima de arma/armadura = refino (estilo MU)
        if (p[1] == Tipo && ItemAt(SlotAt(at)) is { } under && under.Id != p[2] && (Itens.FirstOrDefault(i => i.Id == p[2]).Def ?? DefDe?.Invoke(p[2])) is "joia_mare" or "joia_brasa" or "joia_vazio" && Refinar is not null)
        {
            Refinar(p[2], under.Id);
            QueueRedraw();
            return;
        }
        Soltou?.Invoke(p[2], p[1], slot);
        QueueRedraw();
    }

    public override void _Draw()
    {
        // casas: pedra escura com borda de bronze apagado (como as da prancha)
        for (var y = 0; y < Linhas; y++)
            for (var x = 0; x < Colunas; x++)
            {
                var r = new Rect2(x * Casa + 1, y * Casa + 1, Casa - 2, Casa - 2);
                DrawRect(r, CasaFundo);
                DrawRect(r, CasaBorda, false, 1f);
                DrawLine(r.Position + new Vector2(1, 1), r.Position + new Vector2(r.Size.X - 1, 1), new Color(0, 0, 0, 0.6f), 1f);
            }
        // onde vai cair o que está sendo arrastado
        if (_dragOver is not null && _hoverSlot >= 0)
        {
            var ok = Cabe(_hoverSlot, _dragSize, _dragOver);
            var (hx, hy) = (_hoverSlot % Colunas, _hoverSlot / Colunas);
            DrawRect(new Rect2(hx * Casa + 1, hy * Casa + 1, _dragSize.W * Casa - 2, _dragSize.H * Casa - 2),
                ok ? new Color(0.25f, 0.8f, 0.3f, 0.28f) : new Color(0.9f, 0.2f, 0.15f, 0.3f));
        }
        var font = HudView.BodyFont ?? ThemeDB.FallbackFont;
        foreach (var it in Itens)
        {
            var (x0, y0) = (it.Slot % Colunas, it.Slot / Colunas);
            var r = new Rect2(x0 * Casa + 2, y0 * Casa + 2, it.W * Casa - 4, it.H * Casa - 4);
            var tint = QualityColor(it.Quality);
            DrawRect(r, new Color(tint.R, tint.G, tint.B, 0.10f));
            var hover = ItemAt(_hoverSlot)?.Id == it.Id && _dragOver is null;
            if (ItemIconRenderer.Get(Info?.Invoke(it.Def), it.Def) is { } tex)
            {
                // ícone inteiro dentro do retângulo, sem esticar
                var ts = tex.GetSize(); var k = Mathf.Min(r.Size.X / ts.X, r.Size.Y / ts.Y) * 0.92f;
                var sz = ts * k;
                DrawTextureRect(tex, new Rect2(r.Position + (r.Size - sz) / 2f, sz), false, it.Id == _dragging ? new Color(1, 1, 1, 0.35f) : Colors.White);
            }
            DrawRect(r, it.Id == Selecionado ? new Color(1f, 0.82f, 0.4f) : hover ? new Color(0.85f, 0.8f, 0.7f) : tint with { A = 0.75f }, false, it.Id == Selecionado ? 2f : 1.2f);
            if (Plus?.Invoke(it.Id) is > 0 and var plus)
            {
                var ptxt = "+" + plus;
                var pcol = plus >= 13 ? new Color(1f, 0.45f, 0.2f) : plus >= 9 ? new Color(1f, 0.85f, 0.3f) : plus >= 7 ? new Color(0.5f, 0.85f, 1f) : new Color(0.9f, 0.9f, 0.85f);
                DrawString(font, r.Position + new Vector2(4, 15), ptxt, HorizontalAlignment.Left, -1, 13, Colors.Black);
                DrawString(font, r.Position + new Vector2(3, 14), ptxt, HorizontalAlignment.Left, -1, 13, pcol);
            }
            if (it.Qty > 1)
            {
                var q = it.Qty.ToString();
                var qs = font.GetStringSize(q, HorizontalAlignment.Left, -1, 13);
                var qp = r.End - new Vector2(qs.X + 3, 3);
                DrawString(font, qp + new Vector2(1, 1), q, HorizontalAlignment.Left, -1, 13, Colors.Black);
                DrawString(font, qp, q, HorizontalAlignment.Left, -1, 13, new Color(1f, 0.95f, 0.85f));
            }
        }
    }

    public static Color QualityColor(string q) => ItemTooltip.VarColor(q) ?? q switch
    {
        "incomum" => new Color(0.4f, 0.85f, 0.4f),
        "excelente" => new Color(0.25f, 1f, 0.6f),
        "raro" => new Color(0.4f, 0.65f, 1f),
        "epico" => new Color(0.75f, 0.4f, 1f),
        "chefe" or "lendario" => new Color(1f, 0.6f, 0.15f),
        _ => new Color(0.55f, 0.52f, 0.46f),
    };

    /// <summary>Arruma itens sem posição (depósito) na grade, do maior para o menor (posições só do desenho).</summary>
    public static List<(string Id, string Def, string Quality, int Qty, int Slot, int W, int H)> Empacotar(
        IEnumerable<(string Id, string Def, string Quality, int Qty, int W, int H)> itens, int colunas, int linhas)
    {
        var occ = new bool[colunas * linhas];
        var list = new List<(string, string, string, int, int, int, int)>();
        foreach (var it in itens.OrderByDescending(i => i.W * i.H))
        {
            for (var s = 0; s < occ.Length; s++)
            {
                var (x0, y0) = (s % colunas, s / colunas);
                if (x0 + it.W > colunas || y0 + it.H > linhas) continue;
                var free = true;
                for (var y = y0; y < y0 + it.H && free; y++)
                    for (var x = x0; x < x0 + it.W; x++)
                        if (occ[y * colunas + x]) { free = false; break; }
                if (!free) continue;
                for (var y = y0; y < y0 + it.H; y++)
                    for (var x = x0; x < x0 + it.W; x++) occ[y * colunas + x] = true;
                list.Add((it.Id, it.Def, it.Quality, it.Qty, s, it.W, it.H));
                break;
            }
        }
        return list;
    }
}

/// <summary>Janela da HUD v2: moldura de ferro (ui_moldura) com a aba de título (ui_aba) no alto, como na prancha.</summary>
public sealed partial class JanelaV2 : PanelContainer
{
    public string Titulo { get; set; } = "";
    public JanelaV2(string titulo, float margem = 18f)
    {
        Titulo = titulo;
        var tex = VisualAssets.Load("ui_moldura.png");
        if (tex is not null)
        {
            var box = new StyleBoxTexture { Texture = tex };
            box.SetTextureMarginAll(26); box.SetContentMarginAll(margem);
            box.ContentMarginTop = margem + 26f;
            AddThemeStyleboxOverride("panel", box);
        }
        else AddThemeStyleboxOverride("panel", HudView.WindowStyle());
    }

    public override void _Draw()
    {
        if (Titulo.Length == 0) return;
        var tab = VisualAssets.Load("ui_aba.png");
        var font = HudView.TitleFont ?? ThemeDB.FallbackFont;
        var ts = font.GetStringSize(Titulo, HorizontalAlignment.Left, -1, 19);
        var w = Mathf.Max(ts.X + 80f, 180f);
        var r = new Rect2(14, -14, w, 40);
        if (tex(tab, r)) { }
        DrawString(font, new Vector2(r.Position.X + 26, r.Position.Y + 27), Titulo, HorizontalAlignment.Left, -1, 19, new Color(0.93f, 0.88f, 0.78f));
        bool tex(Texture2D? t, Rect2 rr)
        {
            if (t is null) { DrawRect(rr, new Color(0.08f, 0.075f, 0.07f)); return false; }
            // 3 fatias: pontas de 48 px, miolo esticado
            var tw = t.GetWidth(); var th = t.GetHeight(); var cap = 48f * rr.Size.Y / th;
            DrawTextureRectRegion(t, new Rect2(rr.Position, new Vector2(cap, rr.Size.Y)), new Rect2(0, 0, 48, th));
            DrawTextureRectRegion(t, new Rect2(rr.Position.X + cap, rr.Position.Y, rr.Size.X - 2 * cap, rr.Size.Y), new Rect2(48, 0, tw - 96, th));
            DrawTextureRectRegion(t, new Rect2(rr.End.X - cap, rr.Position.Y, cap, rr.Size.Y), new Rect2(tw - 48, 0, 48, th));
            return true;
        }
    }
}

/// <summary>Placa da área (ui_placa_zona, 3 fatias com ponta de 64 px) com o ícone (âncora no porto/costa, picareta na mina).</summary>
public sealed partial class PlacaZona : Control
{
    public string Texto { get; set; } = "";
    public string Icone { get; set; } = "ico_zona_ancora.png";
    private string _shown = "";

    public override void _Process(double delta)
    {
        if (_shown != Texto) { _shown = Texto; QueueRedraw(); }
    }

    public override void _Draw()
    {
        if (Texto.Length == 0) return;
        var font = HudView.TitleFont ?? ThemeDB.FallbackFont;
        var h = Size.Y;
        var ts = font.GetStringSize(Texto, HorizontalAlignment.Left, -1, 17);
        var w = Mathf.Min(Size.X, ts.X + h * 2.6f);
        if (VisualAssets.Load("ui_placa_zona.png") is { } t)
        {
            var tw = t.GetWidth(); var th = t.GetHeight(); var cap = 64f * h / th;
            DrawTextureRectRegion(t, new Rect2(0, 0, cap, h), new Rect2(0, 0, 64, th));
            DrawTextureRectRegion(t, new Rect2(cap, 0, Mathf.Max(0, w - 2 * cap), h), new Rect2(64, 0, tw - 128, th));
            DrawTextureRectRegion(t, new Rect2(w - cap, 0, cap, h), new Rect2(tw - 64, 0, 64, th));
        }
        else DrawRect(new Rect2(0, 0, w, h), new Color(0.06f, 0.055f, 0.05f, 0.9f));
        if (VisualAssets.Load(Icone) is { } ic) DrawTextureRect(ic, new Rect2(h * 0.28f, h * 0.18f, h * 0.64f, h * 0.64f), false);
        DrawString(font, new Vector2(h * 1.1f + 1, h / 2f + 7f), Texto, HorizontalAlignment.Left, -1, 17, new Color(0, 0, 0, 0.8f));
        DrawString(font, new Vector2(h * 1.1f, h / 2f + 6f), Texto, HorizontalAlignment.Left, -1, 17, new Color(0.93f, 0.89f, 0.80f));
    }
}
