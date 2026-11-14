using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Barra de túnica de tecido mole (Verlet), em anel fechado preso na cintura: cai RETA do corpo até perto do tornozelo,
/// abre um pouco embaixo, balança ao andar e as pernas empurram o pano (não atravessa). Pedido do usuário: "roupa do mago
/// descendo reta do peito, não justa" — sem o cone duro que parecia abajur. Malha refeita a cada quadro, em coordenadas do mundo.
/// </summary>
public sealed partial class ClothSkirt : MeshInstance3D
{
    private const int Cols = 18, Rows = 9;
    private readonly Vector3[] _p = new Vector3[Cols * Rows], _prev = new Vector3[Cols * Rows], _n = new Vector3[Cols * Rows];
    private readonly Node3D _hips;      // osso do quadril (posição)
    private readonly Node3D _facing;    // raiz do personagem (para onde ele olha)
    private readonly float _len, _topR, _botR, _waistUp;
    private readonly Material _mat, _trimMat;
    private readonly ArrayMesh _mesh = new();
    private bool _init;
    private Vector3 _last;
    private float _t;

    /// <summary>Cápsulas das pernas (início, fim, raio), em mundo.</summary>
    public Func<List<(Vector3 A, Vector3 B, float R)>>? Limbs { get; set; }
    private List<(Vector3 A, Vector3 B, float R)> _limbs = new();

    public ClothSkirt(Node3D hips, Node3D facing, Material cloth, Material trim, float length = 0.82f, float topRadius = 0.16f, float bottomRadius = 0.27f, float waistUp = 0.10f)
    {
        _hips = hips; _facing = facing;
        _len = length; _topR = topRadius; _botR = bottomRadius; _waistUp = waistUp;
        _mat = cloth; _trimMat = trim;
        if (cloth is BaseMaterial3D bm) bm.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        if (trim is BaseMaterial3D tm) tm.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        Mesh = _mesh;
        TopLevel = true;
        CastShadow = ShadowCastingSetting.On;
    }

    private (Vector3 Center, Vector3 Fwd, Vector3 Right) Frame()
    {
        var fwd = _facing.GlobalBasis.Z; fwd.Y = 0;
        fwd = fwd.LengthSquared() > 1e-4f ? fwd.Normalized() : Vector3.Back;
        var right = Vector3.Up.Cross(fwd).Normalized();
        return (_hips.GlobalPosition + Vector3.Up * _waistUp, fwd, right);
    }

    private Vector3 Ring(int c, float radius, Vector3 center, Vector3 fwd, Vector3 right, float sx = 1.12f)
    {
        var a = c / (float)Cols * Mathf.Tau;
        return center + right * (Mathf.Sin(a) * radius * sx) + fwd * (Mathf.Cos(a) * radius * 0.92f);
    }

    public void Step(float dt)
    {
        if (!IsInsideTree() || !_hips.IsInsideTree()) return;
        dt = Mathf.Clamp(dt, 0.001f, 0.05f);
        _t += dt;
        var (center, fwd, right) = Frame();
        if (_init && center.DistanceTo(_last) > 2.5f) _init = false;   // teleporte: recomeça pendurada
        _last = center;
        if (!_init)
        {
            for (var r = 0; r < Rows; r++)
                for (var c = 0; c < Cols; c++)
                {
                    var k = r / (float)(Rows - 1);
                    _p[r * Cols + c] = _prev[r * Cols + c] = Ring(c, Mathf.Lerp(_topR, _botR, k), center - Vector3.Up * (k * _len), fwd, right);
                }
            _init = true;
        }
        var g = new Vector3(0, -9.8f, 0) * dt * dt;
        for (var i = Cols; i < _p.Length; i++)
        {
            var v = (_p[i] - _prev[i]) * 0.955f;
            _prev[i] = _p[i];
            _p[i] += v + g;
        }
        for (var c = 0; c < Cols; c++) { _p[c] = Ring(c, _topR, center, fwd, right); _prev[c] = _p[c]; }

        _limbs = Limbs?.Invoke() ?? _limbs;
        var seg = _len / (Rows - 1);
        for (var it = 0; it < 7; it++)
        {
            for (var r = 0; r < Rows; r++)
            {
                var rad = Mathf.Lerp(_topR, _botR, r / (float)(Rows - 1));
                var h = Mathf.Tau * rad * 1.02f / Cols;
                for (var c = 0; c < Cols; c++)
                {
                    var i = r * Cols + c;
                    var right1 = r * Cols + (c + 1) % Cols;
                    Satisfy(i, right1, h, r == 0);
                    if (r < Rows - 1)
                    {
                        Satisfy(i, i + Cols, seg, r == 0);
                        Satisfy(i, (r + 1) * Cols + (c + 1) % Cols, Mathf.Sqrt(h * h + seg * seg), r == 0);
                    }
                }
            }
            // o pano não entra no corpo nem nas pernas: eixo do quadril e cápsulas das coxas/canelas
            for (var i = Cols; i < _p.Length; i++)
            {
                var row = i / Cols;
                var axis = center - Vector3.Up * (row * seg);
                var d = _p[i] - axis; d.Y = 0;
                var minR = _topR * 0.95f;
                if (d.Length() < minR) _p[i] = axis + (d.LengthSquared() > 1e-6f ? d.Normalized() : fwd) * minR + Vector3.Up * (_p[i].Y - axis.Y);
                foreach (var (la, lb, lr) in _limbs)
                {
                    var q = Closest(_p[i], la, lb);
                    var ld = _p[i] - q;
                    var ll = ld.Length();
                    var rr = lr + 0.035f;
                    if (ll < rr) _p[i] = q + (ll > 1e-4f ? ld / ll : fwd) * rr;
                }
                var floor = center.Y - _len - 0.25f;
                if (_p[i].Y < floor) _p[i].Y = floor;
            }
        }
        Rebuild();
    }

    private void Satisfy(int i, int j, float rest, bool pinnedI)
    {
        var d = _p[j] - _p[i];
        var len = d.Length();
        if (len < 1e-6f) return;
        var diff = (len - rest) / len;
        if (pinnedI) _p[j] -= d * diff;
        else { _p[i] += d * diff * 0.5f; _p[j] -= d * diff * 0.5f; }
    }

    private static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(1e-6f, ab.LengthSquared()), 0f, 1f);
        return a + ab * t;
    }

    private void Rebuild()
    {
        _mesh.ClearSurfaces();
        Array.Clear(_n);
        for (var r = 0; r < Rows - 1; r++)
            for (var c = 0; c < Cols; c++)
            {
                int i0 = r * Cols + c, i1 = r * Cols + (c + 1) % Cols, i2 = i0 + Cols, i3 = i1 + Cols;
                var n = (_p[i1] - _p[i0]).Cross(_p[i2] - _p[i0]);
                _n[i0] += n; _n[i1] += n; _n[i2] += n; _n[i3] += n;
            }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var r = 0; r < Rows - 1; r++)
            for (var c = 0; c < Cols; c++)
            {
                int i0 = r * Cols + c, i1 = r * Cols + (c + 1) % Cols, i2 = i0 + Cols, i3 = i1 + Cols;
                foreach (var i in new[] { i0, i2, i1, i1, i2, i3 })
                {
                    st.SetNormal(_n[i].Normalized());
                    st.SetUV(new Vector2((i % Cols) / (float)Cols * 3f, (i / Cols) / (float)(Rows - 1) * 2f));
                    st.AddVertex(_p[i]);
                }
            }
        st.SetMaterial(_mat);
        st.Commit(_mesh);
        // barra (faixa de acabamento) nos últimos 15% de cada coluna
        var hem = new SurfaceTool();
        hem.Begin(Mesh.PrimitiveType.Triangles);
        var last = (Rows - 1) * Cols;
        for (var c = 0; c < Cols; c++)
        {
            var a0 = _p[last + c]; var a1 = _p[last + (c + 1) % Cols];
            var b0 = _p[last - Cols + c].Lerp(a0, 0.8f); var b1 = _p[last - Cols + (c + 1) % Cols].Lerp(a1, 0.8f);
            var n = (a1 - a0).Cross(b0 - a0).Normalized();
            foreach (var v in new[] { b0, a0, b1, b1, a0, a1 }) { hem.SetNormal(n); hem.AddVertex(v); }
        }
        hem.SetMaterial(_trimMat);
        hem.Commit(_mesh);
    }
}
