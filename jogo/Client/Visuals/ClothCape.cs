using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Capa de tecido mole (Verlet): grade de pontos presa nos ombros/nuca; o resto cai com a gravidade, fica para trás
/// quando o personagem anda/corre e não atravessa o corpo (empurrada para fora de uma cápsula do tronco e das pernas).
/// A malha é refeita a cada quadro. Só visual — não tem colisão com o mundo.
/// </summary>
public sealed partial class ClothCape : MeshInstance3D
{
    private const int Cols = 9, Rows = 11;
    private readonly Vector3[] _p = new Vector3[Cols * Rows], _prev = new Vector3[Cols * Rows], _n = new Vector3[Cols * Rows];
    private readonly Node3D _anchor;     // encaixe no peito (Spine2): eixos do personagem
    private readonly float _topW, _botW, _len;
    private bool _init;
    private readonly ArrayMesh _mesh = new();
    private readonly Material _mat, _trimMat;

    private float _t;
    /// <summary>Cápsulas extras (pernas e braços) para o pano não atravessar o corpo: (início, fim, raio), em mundo.</summary>
    public Func<List<(Vector3 A, Vector3 B, float R)>>? Limbs { get; set; }
    private List<(Vector3 A, Vector3 B, float R)> _limbs = new();
    private Vector3 _lastAnchor;

    /// <summary>14/11: capa com recorte (pontas, rasgos): sem a barra de baixo (o recorte é o acabamento).</summary>
    public bool SemBarra { get; set; }

    public ClothCape(Node3D anchor, Color cloth, Material trim, float length = 1.3f, float topWidth = 0.42f, float bottomWidth = 0.95f, Material? pano = null)
    {
        _anchor = anchor;
        _len = length; _topW = topWidth; _botW = bottomWidth;
        var m = pano ?? new StandardMaterial3D { AlbedoColor = cloth, Roughness = 0.92f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _mat = m;
        _trimMat = trim;
        if (trim is BaseMaterial3D bm) bm.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        Mesh = _mesh;
        TopLevel = true;   // posições em coordenadas do mundo
        CastShadow = ShadowCastingSetting.On;
    }

    /// <summary>Ponto preso (linha de cima): arco atrás dos ombros e da nuca, em coordenadas do encaixe.</summary>
    private Vector3 AnchorLocal(int c)
    {
        var t = c / (float)(Cols - 1) * 2f - 1f;
        // gola em arco: da nuca contorna os ombros e fecha na frente do pescoço (capa aberta na frente, como a referência)
        var th = t * Mathf.DegToRad(105f);
        // nasce rente ao pescoço (gola estreita) — feedback: a capa tem que sair do pescoço, não ser uma placa nas costas
        return new Vector3(Mathf.Sin(th) * 0.16f, 0.14f - 0.03f * Mathf.Abs(t), -Mathf.Cos(th) * 0.11f - 0.02f);
    }

    private Vector3 Rest(int r, int c)
    {
        var a = _anchor.GlobalTransform;
        var top = AnchorLocal(c);
        var k = r / (float)(Rows - 1);
        var local = new Vector3(top.X * (1f + 0.6f * k), top.Y - r * _len / (Rows - 1), top.Z * (1f + 0.6f * k) - 0.05f * k);
        return a * local;
    }

    public void Step(float dt)
    {
        if (!IsInsideTree()) return;
        dt = Mathf.Clamp(dt, 0.001f, 0.05f);
        var a = _anchor.GlobalTransform;
        _t += dt;
        // teleporte/troca de mapa: recomeça pendurada em vez de esticar pelo mapa
        if (_init && a.Origin.DistanceTo(_lastAnchor) > 2.5f) _init = false;
        _lastAnchor = a.Origin;
        if (!_init)
        {
            for (var r = 0; r < Rows; r++) for (var c = 0; c < Cols; c++) _p[r * Cols + c] = _prev[r * Cols + c] = Rest(r, c);
            _init = true;
        }
        // integra (Verlet) com amortecimento e gravidade
        var g = new Vector3(0, -9.8f, 0) * dt * dt;
        // brisa leve (a capa nunca fica 100% parada); mais forte na barra de baixo
        // vento só na horizontal (o eixo do osso do peito é inclinado ~25°: sem achatar, a brisa levantava a capa)
        var backH = -a.Basis.Z; backH.Y = 0; backH = backH.LengthSquared() > 1e-4f ? backH.Normalized() : Vector3.Back;
        var sideH = a.Basis.X; sideH.Y = 0; sideH = sideH.LengthSquared() > 1e-4f ? sideH.Normalized() : Vector3.Right;
        var wind = (backH * (0.55f + 0.45f * Mathf.Sin(_t * 1.3f)) + sideH * 0.35f * Mathf.Sin(_t * 0.7f)) * dt * dt;
        for (var i = Cols; i < _p.Length; i++)
        {
            var v = (_p[i] - _prev[i]) * 0.965f;
            _prev[i] = _p[i];
            var row = i / Cols / (float)(Rows - 1);
            var flutter = 0.6f * Mathf.Sin(_t * 3.1f + (i % Cols) * 0.9f + row * 4f) * row;
            _p[i] += v + g + wind * row * (1f + flutter);
        }
        for (var c = 0; c < Cols; c++) { _p[c] = a * AnchorLocal(c); _prev[c] = _p[c]; }

        _limbs = Limbs?.Invoke() ?? _limbs;
        var up = a.Basis.Y.Normalized();
        var back = -a.Basis.Z.Normalized();
        var spine = a.Origin;
        for (var it = 0; it < 8; it++)
        {
            for (var r = 0; r < Rows; r++)
            {
                var w = Mathf.Lerp(_topW, _botW, r / (float)(Rows - 1));
                var hRest = w / (Cols - 1);
                for (var c = 0; c < Cols; c++)
                {
                    var i = r * Cols + c;
                    if (c < Cols - 1) Satisfy(i, i + 1, hRest, r == 0);
                    if (r < Rows - 1) Satisfy(i, i + Cols, _len / (Rows - 1), r == 0);
                    if (r < Rows - 1 && c < Cols - 1) Satisfy(i, i + Cols + 1, Mathf.Sqrt(hRest * hRest + (_len / (Rows - 1)) * (_len / (Rows - 1))), r == 0);
                }
            }
            // não atravessa o corpo: cápsula do tronco (peito → quadril) e das pernas, com folga
            for (var i = Cols; i < _p.Length; i++)
            {
                // 15/11 (capa "voando" com o boneco parado): o eixo do osso do peito inclina ~25° e gira até 90° na animação
                // de espera (o tronco olha para os lados); a cápsula seguia esse eixo e a ponta de baixo ia meio metro para o
                // lado, empurrando o pano na horizontal. O tronco cai sempre na vertical do peito; as pernas têm cápsula própria.
                var s0 = spine + up * 0.15f; var s1 = spine - Vector3.Up * 1.25f;
                var q = ClosestOnSegment(_p[i], s0, s1);
                var d = _p[i] - q;
                var dist = d.Length();
                const float rad = 0.21f;
                if (dist < rad)
                {
                    var n = dist > 1e-4f ? d / dist : back;
                    if (n.Dot(backH) < -0.2f) n = (n + backH * 1.5f).Normalized();   // na dúvida, sai por trás (atrás na horizontal)
                    _p[i] = q + n * rad;
                }
                // braços e pernas também empurram o pano para fora (não atravessa)
                foreach (var (la, lb, lr) in _limbs)
                {
                    var lq = ClosestOnSegment(_p[i], la, lb);
                    var ld = _p[i] - lq;
                    var ll = ld.Length();
                    if (ll < lr) _p[i] = lq + (ll > 1e-4f ? ld / ll : back) * lr;
                }
                // nunca abaixo do chão
                var floor = spine.Y - 1.4f;
                if (_p[i].Y < floor) _p[i].Y = floor;
            }
        }
        Rebuild();
        if (_dbg && (_t % 1f) < dt) GD.Print($"CAPA dt={dt:0.000} anc={a.Origin} up={a.Basis.Y.Normalized()} back={(-a.Basis.Z).Normalized()} top={_p[Cols / 2]} bot={_p[_p.Length - 1 - Cols / 2]} limbs={_limbs.Count} l0={(_limbs.Count > 0 ? _limbs[0].A + "→" + _limbs[0].B + " r" + _limbs[0].R : "")}");
    }
    private static readonly bool _dbg = OS.GetEnvironment("CAPA_DEBUG") == "1";

    private void Satisfy(int i, int j, float rest, bool pinnedI)
    {
        var d = _p[j] - _p[i];
        var len = d.Length();
        if (len < 1e-6f) return;
        var diff = (len - rest) / len;
        if (pinnedI) _p[j] -= d * diff;
        else { _p[i] += d * diff * 0.5f; _p[j] -= d * diff * 0.5f; }
    }

    private static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return a + ab * t;
    }

    private void Rebuild()
    {
        _mesh.ClearSurfaces();
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        // normais suaves por vértice (média das faces vizinhas): tecido sem facetas
        System.Array.Clear(_n);
        for (var r = 0; r < Rows - 1; r++)
            for (var c = 0; c < Cols - 1; c++)
            {
                var i0 = r * Cols + c; var i1 = i0 + 1; var i2 = i0 + Cols; var i3 = i2 + 1;
                var n = (_p[i2] - _p[i0]).Cross(_p[i1] - _p[i0]);
                _n[i0] += n; _n[i1] += n; _n[i2] += n; _n[i3] += n;
            }
        for (var r = 0; r < Rows - 1; r++)
            for (var c = 0; c < Cols - 1; c++)
            {
                var i0 = r * Cols + c; var i1 = i0 + 1; var i2 = i0 + Cols; var i3 = i2 + 1;
                foreach (var i in new[] { i0, i1, i2, i1, i3, i2 })
                {
                    st.SetNormal(_n[i].Normalized());
                    st.SetUV(new Vector2((i % Cols) / (float)(Cols - 1), (i / Cols) / (float)(Rows - 1)));
                    st.AddVertex(_p[i]);
                }
            }
        st.SetMaterial(_mat);
        st.Commit(_mesh);
        if (SemBarra) return;
        // barra (borda de baixo) mais grossa, na cor do acabamento
        var hem = new SurfaceTool();
        hem.Begin(Mesh.PrimitiveType.Triangles);
        var lastRow = (Rows - 1) * Cols;
        for (var c = 0; c < Cols - 1; c++)
        {
            var a0 = _p[lastRow + c]; var a1 = _p[lastRow + c + 1];
            var b0 = _p[lastRow - Cols + c].Lerp(a0, 0.82f); var b1 = _p[lastRow - Cols + c + 1].Lerp(a1, 0.82f);
            var n = (a0 - b0).Cross(b1 - b0).Normalized();
            foreach (var v in new[] { b0, b1, a0, b1, a1, a0 }) { hem.SetNormal(n); hem.AddVertex(v); }
        }
        hem.SetMaterial(_trimMat);
        hem.Commit(_mesh);
    }
}
