using System;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 13/11 (pedido do dono: "ao apertar Z abre um círculo com as animações"): roda de poses no meio da tela. O mouse aponta a
/// fatia (ela acende), clique escolhe; Z, ESC ou clique no meio fecham sem escolher.
/// </summary>
public sealed partial class RodaPoses : Control
{
    public static readonly (string Rotulo, string Pose)[] Opcoes =
    {
        ("Dar tchau", "aceno"), ("Braços cruzados", "bracos"), ("De boa", "deboa"), ("Sentar no chão", "chao"), ("Dançar", "danca"),
    };
    public event Action<string>? Escolheu;
    private int _sobre = -1;
    private const float RFora = 170f, RDentro = 55f;

    public RodaPoses()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private Vector2 Centro => Size / 2f;

    private int FatiaEm(Vector2 p)
    {
        var d = p - Centro;
        if (d.Length() < RDentro || d.Length() > RFora + 40) return -1;
        var ang = Mathf.PosMod(Mathf.Atan2(d.Y, d.X) + Mathf.Pi / 2f + Mathf.Pi / Opcoes.Length, Mathf.Tau);
        return (int)(ang / (Mathf.Tau / Opcoes.Length)) % Opcoes.Length;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm) { var f = FatiaEm(mm.Position); if (f != _sobre) { _sobre = f; QueueRedraw(); } }
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            var f = FatiaEm(mb.Position);
            Visible = false;
            if (f >= 0) Escolheu?.Invoke(Opcoes[f].Pose);
            else Escolheu?.Invoke("");
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var c = Centro;
        DrawCircle(c, RFora + 6, new Color(0, 0, 0, 0.35f));
        var n = Opcoes.Length;
        var passo = Mathf.Tau / n;
        var fonte = ThemeDB.FallbackFont;
        for (var i = 0; i < n; i++)
        {
            var a0 = -Mathf.Pi / 2f - passo / 2f + i * passo;
            var a1 = a0 + passo;
            var cor = i == _sobre ? new Color(0.95f, 0.75f, 0.3f, 0.9f) : new Color(0.18f, 0.14f, 0.1f, 0.85f);
            var pts = new System.Collections.Generic.List<Vector2>();
            for (var k = 0; k <= 16; k++) pts.Add(c + Vector2.FromAngle(a0 + (a1 - a0) * k / 16f) * RFora);
            for (var k = 16; k >= 0; k--) pts.Add(c + Vector2.FromAngle(a0 + (a1 - a0) * k / 16f) * RDentro);
            DrawColoredPolygon(pts.ToArray(), cor);
            DrawLine(c + Vector2.FromAngle(a0) * RDentro, c + Vector2.FromAngle(a0) * RFora, new Color(0.95f, 0.8f, 0.45f, 0.6f), 2f);
            var meio = c + Vector2.FromAngle((a0 + a1) / 2f) * ((RFora + RDentro) / 2f);
            var texto = Opcoes[i].Rotulo;
            var tam = fonte.GetStringSize(texto, HorizontalAlignment.Left, -1, 15);
            DrawString(fonte, meio - new Vector2(tam.X / 2f, -5f), texto, HorizontalAlignment.Left, -1, 15, i == _sobre ? Colors.Black : new Color(0.95f, 0.9f, 0.8f));
        }
        DrawCircle(c, RDentro - 4, new Color(0.1f, 0.08f, 0.06f, 0.9f));
        var t = "Poses";
        var ts = fonte.GetStringSize(t, HorizontalAlignment.Left, -1, 14);
        DrawString(fonte, c - new Vector2(ts.X / 2f, -5f), t, HorizontalAlignment.Left, -1, 14, new Color(0.95f, 0.78f, 0.3f));
    }
}
