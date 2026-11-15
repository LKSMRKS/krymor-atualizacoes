using System.Runtime.CompilerServices;

namespace CinzasDaMare.Shared.World;

/// <summary>
/// 14/11 (movimento dos inimigos): grade de caminhos sobre a geometria do mapa, montada aos poucos (cada célula só é
/// testada na primeira vez que o A* passa por ela). Serve para o monstro contornar parede/pedra em vez de ficar
/// raspando nela. Só o servidor usa; a geometria do mapa é fixa (o editor cria outra MapGeometry, e a grade junto).
/// </summary>
public sealed class NavGrid
{
    public const double Cell = 0.75;
    private static readonly ConditionalWeakTable<MapGeometry, NavGrid> Grades = new();

    private readonly MapGeometry _geo;
    private readonly int _n;
    private readonly double _origin;
    private readonly Dictionary<int, sbyte[]> _livre = new();   // por raio (em quartos de metro): 0 = não testada, 1 = livre, 2 = bloqueada

    private NavGrid(MapGeometry geo)
    {
        _geo = geo;
        _n = Math.Max(2, (int)Math.Ceiling(2 * geo.HalfSize / Cell));
        _origin = -geo.HalfSize;
    }

    public static NavGrid For(MapGeometry geo) => Grades.GetValue(geo, g => new NavGrid(g));

    public int Size => _n;

    private int CellOf(double v) => Math.Clamp((int)Math.Floor((v - _origin) / Cell), 0, _n - 1);
    private double CenterOf(int c) => _origin + (c + 0.5) * Cell;

    /// <summary>Célula livre para um corpo deste raio (fora de sólidos e da zona segura, onde monstro não entra).</summary>
    public bool Livre(int cx, int cy, double radius)
    {
        if (cx < 0 || cy < 0 || cx >= _n || cy >= _n) return false;
        var key = (int)Math.Ceiling(radius * 4);
        if (!_livre.TryGetValue(key, out var arr)) _livre[key] = arr = new sbyte[_n * _n];
        ref var v = ref arr[cy * _n + cx];
        if (v == 0)
        {
            var x = CenterOf(cx); var y = CenterOf(cy);
            v = _geo.IsFree(x, y, key / 4.0) && !_geo.InSafeZone(x, y) ? (sbyte)1 : (sbyte)2;
        }
        return v == 1;
    }

    /// <summary>
    /// Caminho de (x0,y0) até (x1,y1) para um corpo de raio radius: lista de pontos (o último é o destino ou a célula
    /// livre mais perto dele). null = não achou dentro do limite de nós (o chamador volta a andar reto).
    /// </summary>
    public List<(double X, double Y)>? FindPath(double x0, double y0, double x1, double y1, double radius, int maxNodes = 4000)
    {
        int sx = CellOf(x0), sy = CellOf(y0);
        int gx = CellOf(x1), gy = CellOf(y1);
        if (!Livre(gx, gy, radius) && !MaisPerto(ref gx, ref gy, radius)) return null;
        var start = sy * _n + sx;
        var goal = gy * _n + gx;
        if (start == goal) return new List<(double, double)> { (x1, y1) };
        var open = new PriorityQueue<int, double>();
        var g = new Dictionary<int, double> { [start] = 0 };
        var from = new Dictionary<int, int>();
        var closed = new HashSet<int>();
        open.Enqueue(start, H(sx, sy, gx, gy));
        var expanded = 0;
        while (open.Count > 0)
        {
            var cur = open.Dequeue();
            if (cur == goal) return Montar(from, cur, start, x1, y1, radius);
            if (!closed.Add(cur)) continue;
            if (++expanded > maxNodes) return null;
            int cx = cur % _n, cy = cur / _n;
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= _n || ny >= _n) continue;
                    var ni = ny * _n + nx;
                    if (closed.Contains(ni)) continue;
                    // a célula de partida pode estar "bloqueada" (monstro encostado na parede): sai dela mesmo assim
                    if (!Livre(nx, ny, radius) && ni != goal) continue;
                    if (dx != 0 && dy != 0 && (!Livre(cx + dx, cy, radius) || !Livre(cx, cy + dy, radius))) continue;   // sem cortar quina
                    var cost = g[cur] + (dx != 0 && dy != 0 ? 1.4142 : 1.0);
                    if (g.TryGetValue(ni, out var old) && old <= cost) continue;
                    g[ni] = cost;
                    from[ni] = cur;
                    open.Enqueue(ni, cost + H(nx, ny, gx, gy));
                }
        }
        return null;
    }

    private static double H(int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0); var dy = Math.Abs(y1 - y0);
        return Math.Max(dx, dy) + 0.4142 * Math.Min(dx, dy);
    }

    private bool MaisPerto(ref int gx, ref int gy, double radius)
    {
        for (var r = 1; r <= 4; r++)
            for (var dy = -r; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    if (!Livre(gx + dx, gy + dy, radius)) continue;
                    gx += dx; gy += dy;
                    return true;
                }
        return false;
    }

    private List<(double X, double Y)> Montar(Dictionary<int, int> from, int cur, int start, double x1, double y1, double radius)
    {
        var cells = new List<int> { cur };
        while (from.TryGetValue(cur, out var p) && cur != start) { cur = p; cells.Add(cur); }
        cells.Reverse();
        // tira os pontos em linha reta (só ficam as curvas) e termina no destino exato se ele for livre
        var pts = new List<(double X, double Y)>();
        for (var i = 1; i < cells.Count; i++)
        {
            if (i < cells.Count - 1)
            {
                int ax = cells[i - 1] % _n, ay = cells[i - 1] / _n, bx = cells[i] % _n, by = cells[i] / _n, cx = cells[i + 1] % _n, cy = cells[i + 1] / _n;
                if (bx - ax == cx - bx && by - ay == cy - by) continue;
            }
            pts.Add((CenterOf(cells[i] % _n), CenterOf(cells[i] / _n)));
        }
        if (_geo.IsFree(x1, y1, radius)) { if (pts.Count > 0) pts[^1] = (x1, y1); else pts.Add((x1, y1)); }
        return pts;
    }
}
