using System;
using System.Linq;
using CinzasDaMare.Shared.Client;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Minimapa redondo no canto de cima à direita (11/11, fase 1 do roteiro AAA). Gira com a câmera (o alto do círculo é
/// para onde você olha), raio de 45 m. Mostra: estrada e trilhas, muros e rochas grandes, você (seta dourada), amigos
/// (azul), monstros (vermelho; chefe maior, raro roxo), NPC (amarelo; "!" se tem missão), pedras de viagem, portais e o
/// objetivo da missão fixada (losango; se estiver longe, fica na borda apontando a direção). Tecla N liga/desliga.
/// </summary>
public sealed partial class MiniMapa : Control
{
    public ClientModel? Model { get; set; }
    public float Yaw { get; set; }
    private const float RangeM = 45f;
    private static readonly Color Bg = new(0.06f, 0.055f, 0.05f, 0.78f);
    private static readonly Color Rim = new(0.62f, 0.5f, 0.3f);
    private double _t;

    public MiniMapa()
    {
        CustomMinimumSize = new Vector2(190, 190);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _t += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var m = Model;
        var me = m?.Me;
        var r = Size.X / 2f;
        var c = new Vector2(r, r);
        DrawCircle(c, r, Bg);
        if (m?.Map is not { } map || me is null) { DrawArc(c, r - 1, 0, Mathf.Tau, 64, Rim, 2.5f, true); return; }
        var scale = (r - 8) / RangeM;
        // a câmera olha para "f" (no chão): f fica para cima no círculo, a direita da câmera para a direita
        var (fxd, fyd) = CameraMove.Forward(Yaw);
        var f = new Vector2((float)fxd, (float)fyd);
        var rgt = new Vector2(-f.Y, f.X);
        Vector2 ToScreen(Vector2 d) => new(d.Dot(rgt), -d.Dot(f));
        Vector2 P(double wx, double wy) => c + ToScreen(new Vector2((float)(wx - me.X), (float)(wy - me.Y))) * scale;
        bool Inside(Vector2 p, float pad = 4) => p.DistanceTo(c) <= r - pad;

        // estrada e trilhas
        foreach (var road in map.Roads ?? Array.Empty<CinzasDaMare.Shared.World.MapRoad>())
            for (var i = 0; i < road.Points.Count - 1; i++)
            {
                var a = P(road.Points[i][0], road.Points[i][1]); var b = P(road.Points[i + 1][0], road.Points[i + 1][1]);
                if (!Inside(a, -20) && !Inside(b, -20)) continue;
                DrawLine(ClampIn(a, c, r - 4), ClampIn(b, c, r - 4), new Color(0.55f, 0.45f, 0.3f, 0.7f), Mathf.Max(1.5f, (float)road.Width * scale), true);
            }
        // muros e rochas (obstáculos grandes)
        foreach (var o in map.Obstacles)
        {
            if (o.Kind == "invisivel") continue;
            var p = P(o.X, o.Y);
            if (!Inside(p, -30)) continue;
            if (o.Shape == "circle") { if (Inside(p)) DrawCircle(p, Mathf.Max(1.5f, (float)o.R * scale), new Color(0.38f, 0.36f, 0.33f, 0.8f)); }
            else
            {
                var hw = (float)o.W / 2; var hh = (float)o.H / 2;
                var pts = new[] { P(o.X - hw, o.Y - hh), P(o.X + hw, o.Y - hh), P(o.X + hw, o.Y + hh), P(o.X - hw, o.Y + hh) };
                if (pts.All(q => !Inside(q, -2))) continue;
                for (var i = 0; i < 4; i++) DrawLine(ClampIn(pts[i], c, r - 3), ClampIn(pts[(i + 1) % 4], c, r - 3), new Color(0.5f, 0.47f, 0.42f, 0.85f), 2f);
            }
        }
        foreach (var w in map.Waypoints) { var p = P(w.X, w.Y); if (Inside(p)) DrawCircle(p, 4.5f, new Color(0.35f, 0.75f, 1f)); }
        foreach (var pt in map.Portals) { if (Visuals.EscadaCasa.PortalDeEscada(pt)) continue; var p = P(pt.X, pt.Y); if (Inside(p)) DrawArc(p, 5f, 0, Mathf.Tau, 16, new Color(0.7f, 0.5f, 1f), 2f); }
        foreach (var n in map.Npcs)
        {
            var p = P(n.X, n.Y);
            if (!Inside(p)) continue;
            // 14/11 19h (dono: "deixa amarelo só os que têm missão"): com missão = amarelo (verde = entregar); o resto, pontinho cinza
            var marca = m.NpcMarkers is { } nm && nm.TryGetValue(n.Id, out var k) ? k : "";
            if (marca.Length > 0) DrawCircle(p, 3.6f, marca == "entregar" ? new Color(0.4f, 1f, 0.45f) : new Color(1f, 0.85f, 0.35f));
            else DrawCircle(p, 2f, new Color(0.75f, 0.73f, 0.68f, 0.55f));
        }
        if (m.Snapshot is { } snap)
        {
            foreach (var mon in snap.Monsters)
            {
                if (!mon.Alive) continue;
                var p = P(mon.X, mon.Y);
                if (!Inside(p)) continue;
                var col = mon.Boss ? new Color(1f, 0.25f, 0.15f) : mon.Rare ? new Color(0.8f, 0.4f, 1f) : mon.Elite ? new Color(1f, 0.55f, 0.2f) : new Color(0.9f, 0.25f, 0.2f);
                DrawCircle(p, mon.Boss ? 5.5f : mon.Elite ? 3.5f : 2.4f, col);
            }
            foreach (var pl in snap.Players)
            {
                if (pl.CharacterId == me.CharacterId) continue;
                var p = P(pl.X, pl.Y);
                if (Inside(p)) DrawCircle(p, 3.6f, new Color(0.4f, 0.75f, 1f));
            }
        }
        // objetivo das missões (na borda se estiver longe)
        var pulse = 0.6f + 0.4f * Mathf.Sin((float)_t * 4f);
        foreach (var (_, q) in m.QuestMarkers)
        {
            var p = P(q.X, q.Y);
            var far = !Inside(p, 10);
            p = ClampIn(p, c, r - 10);
            var s = far ? 5f : 6.5f;
            DrawColoredPolygon(new[] { p + new Vector2(0, -s), p + new Vector2(s, 0), p + new Vector2(0, s), p + new Vector2(-s, 0) },
                new Color(1f, 0.85f, 0.25f, far ? 0.7f : pulse));
        }
        // você: seta dourada apontando para onde o personagem olha (o alto é a câmera)
        var dir = ToScreen(new Vector2((float)me.FacingX, (float)me.FacingY));
        dir = dir.LengthSquared() < 1e-6f ? new Vector2(0, -1) : dir.Normalized();
        var side = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon(new[] { c + dir * 8f, c - dir * 5f + side * 5f, c - dir * 2.5f, c - dir * 5f - side * 5f }, new Color(1f, 0.85f, 0.35f));
        DrawArc(c, r - 1, 0, Mathf.Tau, 64, Rim, 2.5f, true);
        // N na borda
        var north = ClampIn(c + ToScreen(new Vector2(0, -1)) * 1000f, c, r - 9);
        var font = HudView.BodyFont ?? ThemeDB.FallbackFont;
        DrawString(font, north + new Vector2(-4, 5), "N", HorizontalAlignment.Left, -1, 13, new Color(1f, 0.9f, 0.6f));
    }

    private static Vector2 ClampIn(Vector2 p, Vector2 c, float r)
    {
        var d = p - c;
        return d.Length() <= r ? p : c + d.Normalized() * r;
    }
}
