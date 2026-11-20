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
        // 19/11 (dono: "nas dungeons fica sem minimapa"): a dungeon não tem estrada nem ilustração — as salas (zonas) viram
        // chão claro e os muros ficam grossos, para dar para ver o desenho dos corredores
        if (map.Instanced || map.Kind == "dungeon")
            foreach (var z in map.Zones)
            {
                if (z.Kind is "secreto" or "segredo" && !CinzasDaMare.Shared.World.SegredoNoMapa.Achado(map, z, Model?.Flags)) continue;
                var hz = (float)z.W / 2; var vz = (float)z.H / 2;
                var cz = new[] { P(z.X - hz, z.Y - vz), P(z.X + hz, z.Y - vz), P(z.X + hz, z.Y + vz), P(z.X - hz, z.Y + vz) };
                if (cz.All(q => !Inside(q, -30))) continue;
                DrawColoredPolygon(cz.Select(q => ClampIn(q, c, r - 3)).ToArray(), z.Kind == "seguro" ? new Color(0.3f, 0.42f, 0.3f, 0.45f) : new Color(0.32f, 0.29f, 0.25f, 0.55f));
            }
        // muros e rochas (obstáculos grandes)
        var escondido = CinzasDaMare.Shared.World.SegredoNoMapa.Escondidos(map, Model?.Flags);
        foreach (var o in map.Obstacles)
        {
            if (o.Kind == "invisivel") continue;
            if (CinzasDaMare.Shared.World.SegredoNoMapa.Dentro(escondido, o.X, o.Y)) continue;   // 18/11 15h: segredo não achado
            var p = P(o.X, o.Y);
            if (!Inside(p, -30)) continue;
            if (o.Shape == "circle") { if (Inside(p)) DrawCircle(p, Mathf.Max(1.5f, (float)o.R * scale), new Color(0.38f, 0.36f, 0.33f, 0.8f)); }
            else
            {
                var hw = (float)o.W / 2; var hh = (float)o.H / 2;
                var pts = new[] { P(o.X - hw, o.Y - hh), P(o.X + hw, o.Y - hh), P(o.X + hw, o.Y + hh), P(o.X - hw, o.Y + hh) };
                if (pts.All(q => !Inside(q, -2))) continue;
                var grosso = map.Instanced ? Mathf.Max(2.5f, Mathf.Min(hw, hh) * 2f * scale) : 2f;
                for (var i = 0; i < 4; i++) DrawLine(ClampIn(pts[i], c, r - 3), ClampIn(pts[(i + 1) % 4], c, r - 3), new Color(0.62f, 0.58f, 0.5f, 0.95f), grosso);
            }
        }
        foreach (var w in map.Waypoints) { var p = P(w.X, w.Y); if (Inside(p)) DrawCircle(p, 4.5f, Model?.Flags.Contains("wp:" + w.Id) != false ? new Color(0.35f, 0.75f, 1f) : new Color(0.5f, 0.52f, 0.56f, 0.7f)); }
        var meuNivel = m.Progress?.Level ?? me.Level;
        foreach (var pt in map.Portals)
        {
            if (Visuals.EscadaCasa.PortalDeEscada(pt) || pt.Oculto) continue;   // 17/11: passagem secreta não aparece
            var p = P(pt.X, pt.Y);
            if (!Inside(p)) continue;
            // 14/11 23h: trancado (nível ou marca que falta) = vermelho, igual ao portal vermelho do mapa 3D
            var trancado = (pt.LevelLock && pt.MinLevel > meuNivel) || !CinzasDaMare.Shared.World.Marcas.Tem(m.Flags, pt.RequiresFlag) || m.FaltaChaveDg(pt);   // 19/11: dungeon sem chave
            DrawArc(p, 5f, 0, Mathf.Tau, 16, trancado ? new Color(1f, 0.25f, 0.2f) : new Color(0.7f, 0.5f, 1f), 2f);
        }
        foreach (var n in map.Npcs)
        {
            var p = P(n.X, n.Y);
            if (!Inside(p)) continue;
            // 14/11 19h (dono: "deixa amarelo só os que têm missão"): com missão = amarelo (verde = entregar); o resto, pontinho cinza
            var marca = m.NpcMarkers is { } nm && nm.TryGetValue(n.Id, out var k) ? k : "";
            if (marca.Length > 0) DrawCircle(p, 3.6f, marca == "entregar" ? new Color(0.4f, 1f, 0.45f) : marca == "destaque" ? new Color(0.82f, 0.84f, 0.9f) : new Color(1f, 0.85f, 0.35f));
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
            if (q.Kind == "tesouro")
            {
                // 17/11: X vermelho do mapa do tesouro — só aparece quando já está dentro do minimapa (nada na borda)
                var px = P(q.X, q.Y);
                if (!Inside(px, 6)) continue;
                var xc = new Color(0.85f, 0.12f, 0.08f);
                DrawLine(px + new Vector2(-5, -5), px + new Vector2(5, 5), xc, 3f, true);
                DrawLine(px + new Vector2(-5, 5), px + new Vector2(5, -5), xc, 3f, true);
                continue;
            }
            if (q.Kind == "horda")
            {
                // 17/11 noite (dono: "a horda quando vem caçar, mostra no mapa — pra eu saber, ir até ela ou fugir"):
                // disco vermelho pulsando com espadas (o mesmo da bússola), preso na borda quando está longe
                var ph = P(q.X, q.Y);
                CompassStrip.DrawHorde(this, ClampIn(ph, c, r - 11), Inside(ph, 11) ? 7.5f : 6f);
                continue;
            }
            var p = P(q.X, q.Y);
            var far = !Inside(p, 10);
            p = ClampIn(p, c, r - 10);
            var s = far ? 5f : 6.5f;
            DrawColoredPolygon(new[] { p + new Vector2(0, -s), p + new Vector2(s, 0), p + new Vector2(0, s), p + new Vector2(-s, 0) },
                new Color(1f, 0.85f, 0.25f, far ? 0.7f : pulse));
        }
        // 19/11 (dono: "mostra as missões ativadas no minimapa também"): as marcadas no J (até 15), com o mesmo número do
        // canto da tela e do mapa (M); longe = presa na borda, apontando o caminho (passagem, se for em outro mapa)
        if (m.Map is not null)
            for (var i = HudView.NoMapa.Count - 1; i >= 0; i--)
            {
                var q = HudView.NoMapa[i];
                if (q.MapId != m.Map.Id || (q.X == 0 && q.Y == 0)) continue;
                var pq = P(q.X, q.Y);
                var longeQ = !Inside(pq, 10);
                CompassStrip.DrawQuestStone(this, ClampIn(pq, c, r - 10), i + 1, q.State == "ready", longeQ ? 6f : 7.5f);
            }
        if (m.ProximaHistoria is { } hq && m.IsPinned(hq.Id) && m.Map is not null && hq.MapId == m.Map.Id && (hq.X != 0 || hq.Y != 0))
        {
            var ph = P(hq.X, hq.Y);
            var longe = !Inside(ph, 10);
            CompassStrip.DrawStarStone(this, ClampIn(ph, c, r - 10), longe ? 5f : 6.5f);
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
