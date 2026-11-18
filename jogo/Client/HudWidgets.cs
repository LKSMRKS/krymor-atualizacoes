using System.Linq;
using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Pote de vidro com líquido (vida vermelha; mana azul / fôlego verde). O líquido balança de leve quando o valor
/// muda (perder ou ganhar sangue) e assenta sozinho. Desenhado à mão: não depende de textura.
/// </summary>
public sealed partial class GlassOrb : Control
{
    public Color Liquid { get; set; } = new(0.72f, 0.08f, 0.08f);
    public string Caption { get; set; } = "";
    /// <summary>Efeitos ativos ("veneno,lentidao"): o líquido muda de tom e aparece o nome em cima do frasco.</summary>
    public string Status { get; set; } = "";
    private double _value = 1, _max = 1, _shown = 1;
    private float _slosh, _phase, _tilt, _flash;

    public void SetValue(double value, double max)
    {
        max = Math.Max(1, max);
        var before = _value / _max;
        _value = Math.Clamp(value, 0, max);
        _max = max;
        var delta = (float)Math.Abs(_value / _max - before);
        if (delta > 0.002f)
        {
            _slosh = Math.Min(1f, _slosh + delta * 6f + 0.12f);
            _tilt += (float)(_value / _max < before ? 1 : -1) * Math.Min(0.25f, delta * 3f);
            if (_value / _max < before) _flash = Math.Min(1f, _flash + delta * 5f + 0.2f);
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _shown += (_value / _max - _shown) * Math.Min(1.0, dt * 8.0);
        _phase += dt * (3.2f + _slosh * 5f);
        _slosh *= MathF.Exp(-dt * 1.6f);
        _tilt *= MathF.Exp(-dt * 2.2f);
        _flash *= MathF.Exp(-dt * 4f);
        QueueRedraw();
    }

    // ---- frasco de vidro (pedido do usuário: "um frasco de vidro" no lugar da bola): bojo redondo, gargalo e rolha
    private float Rb => Size.X / 2f - 10f;                         // raio do bojo
    private Vector2 BodyCenter => new(Size.X / 2f, NeckTop + NeckLen + Rb * 0.92f);
    private float NeckTop => 18f;                                  // topo do gargalo (abaixo da rolha)
    private float NeckLen => Rb * 0.42f;
    private float NeckHalf => Rb * 0.30f;
    private float Bottom => BodyCenter.Y + Rb;
    /// <summary>Centro e raio do bojo (o berço de ferro da barra é desenhado atrás).</summary>
    public Vector2 RingCenter => BodyCenter;
    public float RingRadius => Rb + 6f;

    /// <summary>Meia-largura do interior do frasco na altura y (gargalo → ombro suave → bojo).</summary>
    private float HalfWidth(float y)
    {
        var c = BodyCenter;
        if (y < NeckTop || y > Bottom) return 0f;
        var body = MathF.Sqrt(Math.Max(0f, Rb * Rb - (y - c.Y) * (y - c.Y)));
        if (y <= NeckTop + NeckLen) return Math.Max(NeckHalf, body);
        // ombro: o bojo encontra o gargalo arredondado
        return Math.Max(NeckHalf, body);
    }

    public override void _Draw()
    {
        var cx = Size.X / 2f;
        var c = BodyCenter;
        // sombra e vidro (contorno de bronze fino, interior escuro): bojo = círculo, gargalo = retângulo
        void Shape(float grow, Color col)
        {
            DrawCircle(c, Rb + grow, col);
            DrawRect(new Rect2(cx - NeckHalf - grow, NeckTop - grow, 2f * (NeckHalf + grow), c.Y - NeckTop), col);
        }
        Shape(4f, new Color(0.06f, 0.05f, 0.04f, 0.85f));
        Shape(2f, new Color(0.30f, 0.29f, 0.27f, 0.95f));
        Shape(0f, new Color(0.05f, 0.045f, 0.05f, 0.94f));

        // líquido em colunas finas (cada uma um quadrilátero: nunca falha a triangulação)
        var span = Bottom - (NeckTop + 6f);
        var level = Bottom - (float)_shown * span;
        var amp = 1.0f + _slosh * 6f;
        var top = new List<Vector2>();
        var deep = Liquid.Darkened(0.5f);
        if (_shown > 0.003)
        {
            const float Col = 2f;
            for (var x = -Rb; x < Rb; x += Col)
            {
                var xm = x + Col / 2f;
                var circ = MathF.Sqrt(Math.Max(0f, Rb * Rb - xm * xm));
                var yBot = c.Y + circ;
                var inNeck = Math.Abs(xm) <= NeckHalf;
                var wallTop = inNeck ? NeckTop : c.Y - circ;            // até onde o vidro vai nesta coluna
                var wave = level + MathF.Sin(xm * 0.13f + _phase) * amp + MathF.Sin(xm * 0.27f - _phase * 1.3f) * amp * 0.35f + xm * _tilt;
                var yTop = Math.Max(wave, wallTop);
                if (yTop >= yBot - 0.5f) continue;
                top.Add(new Vector2(cx + xm, yTop));
                var side = Math.Abs(xm) / Rb;
                var cTop = Liquid.Lightened(0.08f).Lerp(deep, 0.25f + 0.25f * side);
                var cBot = deep.Lerp(Colors.Black, 0.15f + 0.2f * side);
                var q = new[] { new Vector2(cx + x, yTop), new Vector2(cx + x + Col + 0.6f, yTop), new Vector2(cx + x + Col + 0.6f, yBot), new Vector2(cx + x, yBot) };
                DrawPolygon(q, new[] { cTop, cTop, cBot, cBot });
            }
            if (top.Count >= 2) DrawPolyline(top.ToArray(), Liquid.Lightened(0.45f) with { A = 0.85f }, 1.5f, true);
            for (var bb = 0; bb < 5; bb++)
            {
                var bx = cx + MathF.Sin(_phase * 0.7f + bb * 2.1f) * Rb * 0.5f;
                var by = Bottom - ((_phase * 9f + bb * 37f) % ((Bottom - level) + 1f));
                if (by > level + 3 && by < Bottom - 3 && Math.Abs(bx - cx) < HalfWidth(by) - 3) DrawCircle(new Vector2(bx, by), 1.5f, Liquid.Lightened(0.5f) with { A = 0.5f });
            }
        }
        // reflexos do vidro: faixa curva no bojo, risco no gargalo, ponto de luz
        DrawArc(c + new Vector2(-Rb * 0.1f, -Rb * 0.08f), Rb * 0.78f, MathF.PI * 1.05f, MathF.PI * 1.5f, 24, new Color(1f, 1f, 1f, 0.30f), 3f, true);
        DrawArc(c, Rb - 1.5f, MathF.PI * 0.15f, MathF.PI * 0.6f, 18, new Color(1f, 1f, 1f, 0.10f), 2f, true);
        DrawLine(new Vector2(cx - NeckHalf + 4f, NeckTop + 4f), new Vector2(cx - NeckHalf + 4f, NeckTop + NeckLen), new Color(1f, 1f, 1f, 0.25f), 2f, true);
        DrawCircle(c + new Vector2(-Rb * 0.45f, -Rb * 0.42f), Rb * 0.08f, new Color(1f, 1f, 1f, 0.38f));
        // boca e rolha de cortiça
        DrawRect(new Rect2(cx - NeckHalf - 3f, NeckTop - 3f, 2f * NeckHalf + 6f, 5f), new Color(0.45f, 0.36f, 0.22f));
        var cork = new Rect2(cx - NeckHalf + 2f, NeckTop - 15f, 2f * NeckHalf - 4f, 14f);
        DrawRect(cork, new Color(0.52f, 0.36f, 0.22f));
        DrawRect(new Rect2(cork.Position.X, cork.Position.Y, cork.Size.X, 3f), new Color(0.62f, 0.45f, 0.28f));
        for (var k = 0; k < 4; k++) DrawCircle(cork.Position + new Vector2(4f + k * (cork.Size.X - 8f) / 3f, 8f + (k % 2) * 2f), 0.9f, new Color(0.30f, 0.2f, 0.12f));
        if (_flash > 0.02f) DrawArc(c, Rb + 1f, 0, MathF.Tau, 64, new Color(1f, 0.25f, 0.2f, _flash), 2.5f, true);
        // aro de ferro com friso de bronze em volta do bojo (ui_anel.png)
        DrawArc(c, Rb + 3.5f, 0, MathF.Tau, 72, new Color(0.03f, 0.03f, 0.03f, 0.95f), 4f, true);
        DrawArc(c, Rb + 6f, 0, MathF.Tau, 72, new Color(0.42f, 0.41f, 0.39f, 0.9f), 1.5f, true);
        // status: tarja colorida acima do frasco
        if (Status.Length > 0)
        {
            var sy = NeckTop - 22f;
            foreach (var st in Status.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var (label, col) = st switch
                {
                    "veneno" => ("VENENO", new Color(0.45f, 1f, 0.35f)), "lentidao" => ("LENTO", new Color(0.5f, 0.85f, 1f)),
                    "queimadura" => ("QUEIMANDO", new Color(1f, 0.55f, 0.2f)),
                    "sangramento" => ("SANGRANDO", new Color(1f, 0.2f, 0.2f)), "maldicao" => ("MALDITO", new Color(0.75f, 0.45f, 1f)), "atordoado" => ("ATORDOADO", new Color(1f, 0.9f, 0.4f)), _ => (st.ToUpperInvariant(), Colors.White),
                };
                var f2 = ThemeDB.FallbackFont;
                var sz = f2.GetStringSize(label, HorizontalAlignment.Left, -1, 11);
                var pulse = 0.7f + 0.3f * MathF.Sin(_phase * 2f);
                DrawRect(new Rect2(cx - sz.X / 2f - 5, sy - 12, sz.X + 10, 16), new Color(0, 0, 0, 0.7f));
                DrawString(f2, new Vector2(cx - sz.X / 2f, sy), label, HorizontalAlignment.Left, -1, 11, col with { A = pulse });
                sy -= 18f;
            }
        }
        // número (mesma dinâmica de antes)
        var font = HudView.TitleFont ?? ThemeDB.FallbackFont;
        var text = $"{_value:0}/{_max:0}";
        var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 14);
        DrawString(font, c + new Vector2(-size.X / 2f + 1, 6f), text, HorizontalAlignment.Left, -1, 14, new Color(0, 0, 0, 0.8f));
        DrawString(font, c + new Vector2(-size.X / 2f, 5f), text, HorizontalAlignment.Left, -1, 14, new Color(0.97f, 0.94f, 0.88f));
        if (Caption.Length > 0)
        {
            var cs = font.GetStringSize(Caption, HorizontalAlignment.Left, -1, 11);
            DrawString(font, new Vector2(cx - cs.X / 2f, Bottom + 15f), Caption, HorizontalAlignment.Left, -1, 12, new Color(0.80f, 0.78f, 0.72f));
        }
    }
}

/// <summary>Bússola curta no topo: O · N · L com marcas, girando com a câmera.</summary>
public sealed partial class CompassStrip : Control
{
    public float Yaw { get; set; }
    /// <summary>Hora do mundo embaixo da bússola ("21:40"); vazio = não mostra.</summary>
    public string Clock { get; set; } = "";
    /// <summary>10/11: pedrinhas numeradas das missões (graus da bússola, número, pronta para entregar).</summary>
    public List<(float Deg, int N, bool Ready)> Marks { get; set; } = new();

    public override void _Process(double delta) => QueueRedraw();

    /// <summary>Pedrinha de missão: pedra escura com aro dourado (verde = entregar) e o número no meio.</summary>
    public static void DrawQuestStone(CanvasItem c, Vector2 at, int n, bool ready, float r = 9f)
    {
        if (n == 0 && !ready) { DrawHorde(c, at, r * 1.35f); return; }
        if (n == -2) { DrawStarStone(c, at, r * 1.15f); return; }   // 17/11 noite: história principal = estrela
        var rim = n == 0 ? new Color(1f, 0.80f, 0.32f) : ready ? new Color(0.45f, 0.95f, 0.5f) : new Color(1f, 0.80f, 0.32f);
        c.DrawCircle(at + new Vector2(1, 1.5f), r + 1.5f, new Color(0, 0, 0, 0.55f));
        c.DrawCircle(at, r + 1.5f, rim);
        c.DrawCircle(at, r, new Color(0.16f, 0.14f, 0.13f));
        var font = HudView.TitleFont ?? ThemeDB.FallbackFont;
        var txt = n == 0 ? "!" : n < 0 ? "" : n.ToString();   // 0 = horda; -1 = só a pedrinha
        var size = (int)(r * 1.45f);
        var ts = font.GetStringSize(txt, HorizontalAlignment.Left, -1, size);
        c.DrawString(font, at + new Vector2(-ts.X / 2f, ts.Y * 0.32f), txt, HorizontalAlignment.Left, -1, size, rim);
    }

    /// <summary>17/11 noite (dono: "a missão História não mostra no mapa — com uma estrela, igual o ícone"): estrela laranja-dourada.</summary>
    public static void DrawStarStone(CanvasItem c, Vector2 at, float r)
    {
        var laranja = new Color(1f, 0.66f, 0.25f);
        var pulso = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 260f);
        c.DrawCircle(at, r + 2.5f + 1.5f * pulso, new Color(1f, 0.6f, 0.2f, 0.18f + 0.15f * pulso));
        var pts = new Vector2[10];
        for (var i = 0; i < 10; i++)
        {
            var ang = -Mathf.Pi / 2f + i * Mathf.Pi / 5f;
            var rr = i % 2 == 0 ? r * 1.25f : r * 0.52f;
            pts[i] = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
        }
        c.DrawColoredPolygon(pts.Select(q => q + new Vector2(1f, 1.5f)).ToArray(), new Color(0, 0, 0, 0.55f));
        c.DrawColoredPolygon(pts, laranja);
        c.DrawPolyline(pts.Append(pts[0]).ToArray(), new Color(1f, 0.92f, 0.6f), Mathf.Max(1f, r * 0.12f), true);
    }

    /// <summary>
    /// 13/11 ("a horda podia aparecer na bússola com um ícone vermelho"): disco vermelho que pulsa, com duas espadas
    /// cruzadas brancas — dá para ver de longe que é a horda, não uma missão.
    /// </summary>
    public static void DrawHorde(CanvasItem c, Vector2 at, float r)
    {
        var pulso = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 180f);
        c.DrawCircle(at, r + 3f + 2f * pulso, new Color(1f, 0.15f, 0.1f, 0.25f + 0.2f * pulso));
        c.DrawCircle(at + new Vector2(1, 1.5f), r + 1.5f, new Color(0, 0, 0, 0.55f));
        c.DrawCircle(at, r + 1.5f, new Color(1f, 0.55f, 0.45f));
        c.DrawCircle(at, r, new Color(0.78f, 0.08f, 0.06f));
        var k = r * 0.62f;
        var branco = new Color(1f, 0.95f, 0.9f);
        foreach (var s in new[] { 1f, -1f })
        {
            var a = at + new Vector2(-k * s, k); var b = at + new Vector2(k * s, -k);
            c.DrawLine(a, b, branco, Mathf.Max(1.5f, r * 0.2f), true);
            var g = a + (b - a) * 0.28f; var n = new Vector2(k * 0.35f, k * 0.35f * s);
            c.DrawLine(g - n, g + n, branco, Mathf.Max(1.2f, r * 0.16f), true);   // guarda da espada
        }
    }

    private static Texture2D? _strip;

    public override void _Draw()
    {
        // faixa de ferro com aro de aço e letras em serifa (prancha "Proposta visual"): sóbria, sem dourado
        var w = Size.X;
        var y = Size.Y / 2f;
        _strip ??= VisualAssets.Load("ui_bussola.png");
        var band = new Rect2(0, y - 15, w, 30);
        if (_strip is not null)
        {
            var tw = _strip.GetWidth(); var th = _strip.GetHeight();
            DrawTextureRectRegion(_strip, new Rect2(band.Position, new Vector2(40, band.Size.Y)), new Rect2(0, 0, 40, th));
            // 10/11: o miolo da arte tem um losango preto grande no meio; a faixa usa só as metades sem ele
            var half = (w - 80) / 2f;
            DrawTextureRectRegion(_strip, new Rect2(band.Position.X + 40, band.Position.Y, half, band.Size.Y), new Rect2(40, 0, tw / 2f - 58, th));
            DrawTextureRectRegion(_strip, new Rect2(band.Position.X + 40 + half, band.Position.Y, half, band.Size.Y), new Rect2(tw / 2f + 18, 0, tw / 2f - 58, th));
            DrawTextureRectRegion(_strip, new Rect2(w - 40, band.Position.Y, 40, band.Size.Y), new Rect2(tw - 40, 0, 40, th));
        }
        else DrawRect(band, new Color(0.05f, 0.05f, 0.05f, 0.85f));
        var font = HudView.TitleFont ?? ThemeDB.FallbackFont;
        var tick = new Color(0.62f, 0.60f, 0.56f);
        // heading em graus: câmera olha para -Z quando yaw = 0 (norte)
        var heading = Mathf.RadToDeg(-Yaw);
        for (var deg = 0; deg < 360; deg += 15)
        {
            var d = Mathf.Wrap(deg - heading, -180f, 180f);
            if (Math.Abs(d) > 75) continue;
            var x = w / 2f + d / 75f * (w / 2f - 30f);
            var a = 1f - Math.Abs(d) / 85f;
            var label = deg switch { 0 => "N", 90 => "L", 180 => "S", 270 => "O", _ => "" };
            if (label.Length == 0) continue;   // 11/11 noite (pedido): só as letras, sem os tracinhos cinza
            var col = label == "N" ? new Color(0.96f, 0.94f, 0.90f, a) : new Color(0.82f, 0.80f, 0.76f, a);
            var size = font.GetStringSize(label, HorizontalAlignment.Left, -1, 19);
            DrawString(font, new Vector2(x - size.X / 2f + 1, y + 7 + 1), label, HorizontalAlignment.Left, -1, 19, new Color(0, 0, 0, 0.8f * a));
            DrawString(font, new Vector2(x - size.X / 2f, y + 7), label, HorizontalAlignment.Left, -1, 19, col);
        }
        // missões: pedrinha numerada em cima da faixa, na direção dela (fora da vista: presa na ponta, mais apagada)
        foreach (var (deg, n, ready) in Marks)
        {
            var d = Mathf.Wrap(deg - heading, -180f, 180f);
            var edge = Math.Abs(d) > 75;
            var x = w / 2f + Mathf.Clamp(d, -75f, 75f) / 75f * (w / 2f - 30f);
            DrawQuestStone(this, new Vector2(x, y + 15f), n, ready, edge ? 4.5f : 5.5f);   // 11/11: menor e embaixo da faixa (em cima fica o losango)
        }
        // marca do centro: losango de aço
        var m = new Vector2(w / 2f, band.Position.Y);
        DrawColoredPolygon(new[] { m + new Vector2(0, -1), m + new Vector2(3.5f, 3), m + new Vector2(0, 7), m + new Vector2(-3.5f, 3) }, new Color(1f, 0.80f, 0.32f));
        if (Clock.Length > 0)
        {
            var cs = font.GetStringSize(Clock, HorizontalAlignment.Left, -1, 15);
            // logo à direita da faixa (embaixo dela fica a barra do alvo; dentro, as letras dos pontos cardeais)
            var at = new Vector2(w + 12f, y + 5);
            DrawString(font, at + new Vector2(1, 1), Clock, HorizontalAlignment.Left, -1, 15, new Color(0, 0, 0, 0.85f));
            DrawString(font, at, Clock, HorizontalAlignment.Left, -1, 15, new Color(0.80f, 0.78f, 0.74f, 0.9f));
        }
    }
}

/// <summary>Retrato do boneco (cabeça oval e ombros, com elmo/capuz conforme a classe) — no lugar da foto realista.</summary>
public sealed partial class PuppetPortrait : Control
{
    public string ClassId { get; set; } = "guerreiro";

    public override void _Draw()
    {
        var c = Size / 2f;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.13f, 0.12f, 0.14f));
        var body = new Color(0.29f, 0.22f, 0.18f);
        var joint = new Color(0.62f, 0.50f, 0.36f);
        // ombros e pescoço
        DrawColoredPolygon(new[] { new Vector2(c.X - Size.X * 0.42f, Size.Y), new Vector2(c.X - Size.X * 0.30f, Size.Y * 0.78f),
            new Vector2(c.X + Size.X * 0.30f, Size.Y * 0.78f), new Vector2(c.X + Size.X * 0.42f, Size.Y) }, body);
        DrawRect(new Rect2(c.X - 3, Size.Y * 0.62f, 6, Size.Y * 0.18f), body);
        DrawCircle(new Vector2(c.X - Size.X * 0.30f, Size.Y * 0.80f), 4, joint);
        DrawCircle(new Vector2(c.X + Size.X * 0.30f, Size.Y * 0.80f), 4, joint);
        // cabeça oval
        var head = new List<Vector2>();
        for (var i = 0; i < 24; i++)
        {
            var a = i / 24f * MathF.Tau;
            head.Add(new Vector2(c.X + MathF.Cos(a) * Size.X * 0.17f, Size.Y * 0.40f + MathF.Sin(a) * Size.Y * 0.23f));
        }
        if (ClassId == "bruxa")
        {
            // cabelo solto atrás da cabeça (caindo até os ombros)
            DrawColoredPolygon(new[] { new Vector2(c.X - Size.X * 0.21f, Size.Y * 0.30f), new Vector2(c.X, Size.Y * 0.13f), new Vector2(c.X + Size.X * 0.21f, Size.Y * 0.30f),
                new Vector2(c.X + Size.X * 0.25f, Size.Y * 0.82f), new Vector2(c.X - Size.X * 0.25f, Size.Y * 0.82f) }, new Color(0.30f, 0.14f, 0.09f));
        }
        DrawColoredPolygon(head.ToArray(), new Color(0.78f, 0.70f, 0.58f));
        if (ClassId == "bruxa")
        {
            // franja e diadema de ouro com gema esmeralda
            DrawColoredPolygon(new[] { new Vector2(c.X - Size.X * 0.18f, Size.Y * 0.30f), new Vector2(c.X, Size.Y * 0.16f), new Vector2(c.X + Size.X * 0.18f, Size.Y * 0.30f),
                new Vector2(c.X, Size.Y * 0.25f) }, new Color(0.30f, 0.14f, 0.09f));
            DrawLine(new Vector2(c.X - Size.X * 0.17f, Size.Y * 0.27f), new Vector2(c.X + Size.X * 0.17f, Size.Y * 0.27f), new Color(0.85f, 0.68f, 0.28f), 2f);
            DrawCircle(new Vector2(c.X, Size.Y * 0.26f), 3, new Color(0.25f, 0.85f, 0.55f));
        }
        else if (ClassId is "mago" or "arqueiro")
        {
            // capuz: violeta (Mago) ou verde-floresta (Arqueiro). Contorno sem cruzar (o antigo cruzava e o Godot recusava).
            var hood = ClassId == "mago" ? new Color(0.36f, 0.22f, 0.42f) : new Color(0.20f, 0.30f, 0.17f);
            DrawColoredPolygon(new[] { new Vector2(c.X, Size.Y * 0.06f), new Vector2(c.X + Size.X * 0.27f, Size.Y * 0.52f), new Vector2(c.X + Size.X * 0.20f, Size.Y * 0.70f),
                new Vector2(c.X + Size.X * 0.12f, Size.Y * 0.32f), new Vector2(c.X - Size.X * 0.12f, Size.Y * 0.32f), new Vector2(c.X - Size.X * 0.20f, Size.Y * 0.70f),
                new Vector2(c.X - Size.X * 0.27f, Size.Y * 0.52f) }, hood);
        }
        else
        {
            // elmo de ferro com fenda
            DrawColoredPolygon(new[] { new Vector2(c.X - Size.X * 0.21f, Size.Y * 0.56f), new Vector2(c.X - Size.X * 0.21f, Size.Y * 0.28f), new Vector2(c.X, Size.Y * 0.12f),
                new Vector2(c.X + Size.X * 0.21f, Size.Y * 0.28f), new Vector2(c.X + Size.X * 0.21f, Size.Y * 0.56f) }, new Color(0.35f, 0.37f, 0.41f));
            DrawRect(new Rect2(c.X - Size.X * 0.15f, Size.Y * 0.36f, Size.X * 0.30f, 3), new Color(0.06f, 0.06f, 0.07f));
            DrawLine(new Vector2(c.X, Size.Y * 0.14f), new Vector2(c.X, Size.Y * 0.34f), new Color(0.55f, 0.57f, 0.6f), 1.5f);
        }
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.55f, 0.42f, 0.22f), false, 1.5f);
    }
}


/// <summary>
/// Moldura da barra de baixo (referências MU/Diablo): faixa de pedra escura com friso dourado por trás dos atalhos,
/// "berços" de metal atrás dos frascos de vida e de recurso, e pontas ornamentadas.
/// </summary>
public sealed partial class HudBarFrame : PanelContainer
{
    private static Texture2D? _stone;

    public HudBarFrame()
    {
        var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
        box.ContentMarginLeft = box.ContentMarginRight = 18;
        box.ContentMarginTop = 2;
        box.ContentMarginBottom = 4;
        AddThemeStyleboxOverride("panel", box);
        _stone ??= VisualAssets.Load("parede_pedra.jpg");
    }

    public GlassOrb? Left { get; set; }
    public GlassOrb? Right { get; set; }
    private static Texture2D? _plate, _cradle;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        // prancha "Proposta visual" (09/11): placa de ferro forjado atrás dos atalhos, ligando os dois potes, e um berço de
        // ferro com garras atrás de cada pote. Sem ouro: aço gasto, rebites, friso de bronze apagado.
        _plate ??= VisualAssets.Load("ui_placa_barra.png");
        _cradle ??= VisualAssets.Load("ui_berco.png");
        var w = Size.X; var h = Size.Y;
        var top = h - 92f;
        if (_plate is not null)
        {
            var x0 = Left is null ? 0f : Left.Position.X + Left.Size.X * 0.5f + GetChild<Control>(0).Position.X;
            var x1 = Right is null ? w : Right.Position.X + Right.Size.X * 0.5f + GetChild<Control>(0).Position.X;
            NineH(_plate, new Rect2(x0, top, x1 - x0, h - top), 60f);
        }
        else DrawRect(new Rect2(0, top, w, h - top), new Color(0.06f, 0.06f, 0.06f, 0.9f));
        if (_cradle is not null)
            foreach (var orb in new[] { Left, Right })
            {
                if (orb is null) continue;
                var c = orb.Position + GetChild<Control>(0).Position + orb.RingCenter;
                var r = orb.RingRadius * 1.42f;
                DrawTextureRect(_cradle, new Rect2(c - new Vector2(r, r), new Vector2(2 * r, 2 * r)), false);
            }
    }

    /// <summary>Textura em 3 fatias na horizontal (pontas fixas, meio esticado).</summary>
    private void NineH(Texture2D tex, Rect2 r, float cap)
    {
        var tw = tex.GetWidth(); var th = tex.GetHeight();
        var capW = Mathf.Min(cap, r.Size.X / 2f);
        var src = cap * th / r.Size.Y;
        DrawTextureRectRegion(tex, new Rect2(r.Position, new Vector2(capW, r.Size.Y)), new Rect2(0, 0, src, th));
        DrawTextureRectRegion(tex, new Rect2(r.Position.X + capW, r.Position.Y, r.Size.X - 2 * capW, r.Size.Y), new Rect2(src, 0, tw - 2 * src, th));
        DrawTextureRectRegion(tex, new Rect2(r.End.X - capW, r.Position.Y, capW, r.Size.Y), new Rect2(tw - src, 0, src, th));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }
}

/// <summary>
/// 13/11 ("reforçar na tela o que está acontecendo"): bordas da tela tingidas pela cor do status, pulsando —
/// sangramento vermelho (pulsa como batimento), veneno verde, maldição roxa, queimadura laranja, lentidão azul.
/// </summary>
public sealed partial class StatusVinheta : Control
{
    private string _status = "";
    private float _t, _forca;
    public string Status { get => _status; set { _status = value; } }

    public StatusVinheta() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _forca = Mathf.MoveToward(_forca, _status.Length > 0 ? 1f : 0f, (float)delta * 2.5f);
        if (_forca > 0.001f || Visible) QueueRedraw();
    }

    public override void _Draw()
    {
        if (_forca <= 0.001f) return;
        var kind = _status.Split(',', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } arr ? arr[0] : _ultimo;
        _ultimo = kind;
        var cor = kind switch
        {
            "sangramento" => new Color(0.75f, 0.02f, 0.02f), "veneno" => new Color(0.35f, 0.8f, 0.15f), "maldicao" => new Color(0.5f, 0.15f, 0.8f),
            "queimadura" => new Color(1f, 0.45f, 0.08f), "lentidao" => new Color(0.4f, 0.75f, 1f), "atordoado" => new Color(1f, 0.9f, 0.4f), _ => new Color(0.6f, 0.6f, 0.6f),
        };
        // batimento (sangue) ou respiração lenta (o resto)
        var pulso = kind == "sangramento" ? 0.55f + 0.45f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_t * 7.5f)), 6f) : 0.7f + 0.3f * Mathf.Sin(_t * 2.2f);
        var s = Size;
        var faixa = Mathf.Min(s.X, s.Y) * 0.16f;
        const int passos = 10;
        for (var i = 0; i < passos; i++)
        {
            var k = i / (float)passos;
            var a = 0.42f * (1f - k) * (1f - k) * pulso * _forca;
            var c = cor with { A = a };
            var w = faixa / passos;
            var off = w * i;
            DrawRect(new Rect2(off, off, s.X - 2 * off, w), c);                 // cima
            DrawRect(new Rect2(off, s.Y - off - w, s.X - 2 * off, w), c);       // baixo
            DrawRect(new Rect2(off, off + w, w, s.Y - 2 * off - 2 * w), c);     // esquerda
            DrawRect(new Rect2(s.X - off - w, off + w, w, s.Y - 2 * off - 2 * w), c);   // direita
        }
    }

    private string _ultimo = "";
}
