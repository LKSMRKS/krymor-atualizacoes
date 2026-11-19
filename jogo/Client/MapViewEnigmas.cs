using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Client.Visuals;
using CinzasDaMare.Shared.World;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 18/11 21h (dono: "o enigma dos pilares em todo lugar — varia, mais criativo"): as travas novas do cofre do enigma
/// (servidor: GameServer.Enigmas.cs; gerador: Tools/gerar_enigmas.py). Alavancas que descem, sinos de três tamanhos que
/// balançam e soam, guardiões de pedra que giram um quarto de volta, espelhos que giram 45° com o raio do braseiro pulando
/// de um para o outro até o selo. Tudo por marca do personagem ("runa:id" = feita, "giro:id:k" = posição).
/// </summary>
public sealed partial class MapView
{
    private sealed class PecaEnigma
    {
        public MapObject Obj = null!;
        public Node3D Raiz = null!;
        public Node3D? Gira;          // estátua/espelho: gira em volta do eixo vertical
        public Node3D? Cabo;          // alavanca
        public Node3D? Pendulo;       // sino
        public OmniLight3D? Luz;
        public float AlvoGraus, Graus = float.NaN, Balanco;
        public bool Feita;
        public int Estado = -1;
    }

    private readonly List<PecaEnigma> _enigma = new();
    private readonly List<(string Grupo, MeshInstance3D Raio, int Precisa)> _raios = new();
    private bool _raiosMontados;
    private static readonly Color Bronze = new(0.72f, 0.5f, 0.24f);

    private static int Voltas(MapObject o) => o.Kind == "espelho" ? 8 : 4;

    private static int EstadoDe(IReadOnlySet<string> flags, MapObject o)
    {
        var pre = "giro:" + o.Id + ":";
        var v = Voltas(o);
        foreach (var f in flags)
            if (f.StartsWith(pre, StringComparison.Ordinal) && int.TryParse(f[pre.Length..], out var k)) return ((k % v) + v) % v;
        return ((o.Inicio % v) + v) % v;
    }

    private static bool Certo(IReadOnlySet<string> flags, MapObject o) => EstadoDe(flags, o) == ((o.Ordem % Voltas(o)) + Voltas(o)) % Voltas(o);

    private bool AddEnigma(MapObject obj)
    {
        if (obj.Kind is not ("alavanca_enig" or "sino" or "estatua_giro" or "espelho" or "fonte_luz")) return false;
        var raiz = new Node3D { Position = P(obj.X, obj.Y) };
        Root.AddChild(raiz);
        var peca = new PecaEnigma { Obj = obj, Raiz = raiz };
        var bronze = VisualKit.Mat(Bronze, 0.35f, 0.8f);
        var madeira = VisualKit.Mat(new Color(0.36f, 0.24f, 0.14f), 0.85f);
        switch (obj.Kind)
        {
            case "alavanca_enig":
            {
                raiz.RotationDegrees = new Vector3(0, R(0, 360), 0);
                if (GlbCache.Instance("interior/puzzle_alavanca_base") is { } b && GlbCache.Instance("interior/puzzle_alavanca_cabo") is { } c)
                {
                    raiz.AddChild(b);
                    peca.Cabo = VisualKit.Joint(raiz, new Vector3(0, 0.55f, 0));
                    c.RotationDegrees = new Vector3(0, 180, 0);
                    peca.Cabo.AddChild(c);
                }
                else
                {
                    VisualKit.Part(raiz, VisualKit.Box(0.5f, 0.6f, 0.4f), StoneMat(0.4f, false), new Vector3(0, 0.3f, 0));
                    peca.Cabo = VisualKit.Joint(raiz, new Vector3(0, 0.6f, 0));
                    VisualKit.Part(peca.Cabo, VisualKit.Cylinder(0.04f, 0.04f, 0.9f, 6), madeira, new Vector3(0, 0.45f, 0));
                }
                // corrente que some no chão (vai até o selo) e a marca acesa do lado
                for (var i = 0; i < 6; i++)
                    VisualKit.Part(raiz, new TorusMesh { InnerRadius = 0.025f, OuterRadius = 0.045f, Rings = 8, RingSegments = 6 }, VisualKit.Mat(new Color(0.3f, 0.3f, 0.32f), 0.5f, 0.8f),
                        new Vector3(0, 0.05f + i * 0.004f, -0.3f - i * 0.07f), new Vector3(90, i % 2 == 0 ? 0 : 90, 0));
                peca.Luz = new OmniLight3D { LightColor = new Color(1f, 0.75f, 0.35f), LightEnergy = 0f, OmniRange = 2.6f, Position = new Vector3(0, 1f, 0), ShadowEnabled = false };
                raiz.AddChild(peca.Luz);
                peca.Cabo!.RotationDegrees = new Vector3(40, 0, 0);
                break;
            }
            case "sino":
            {
                raiz.RotationDegrees = new Vector3(0, R(0, 360), 0);
                var tam = obj.Label.Contains("grande") ? 1.35f : obj.Label.Contains("pequeno") ? 0.7f : 1f;
                foreach (var sd in new[] { -1f, 1f })
                    VisualKit.Part(raiz, VisualKit.Cylinder(0.07f, 0.09f, 2.3f, 8), madeira, new Vector3(sd * 0.75f, 1.15f, 0));
                VisualKit.Part(raiz, VisualKit.Box(1.75f, 0.16f, 0.18f), madeira, new Vector3(0, 2.3f, 0));
                peca.Pendulo = VisualKit.Joint(raiz, new Vector3(0, 2.22f, 0));
                var sino = VisualKit.Joint(peca.Pendulo, Vector3.Zero);
                sino.Scale = Vector3.One * tam;
                VisualKit.Part(sino, VisualKit.Cylinder(0.12f, 0.27f, 0.42f, 18), bronze, new Vector3(0, -0.3f, 0));
                VisualKit.Part(sino, VisualKit.Sphere(0.12f), bronze, new Vector3(0, -0.1f, 0), null, new Vector3(1f, 0.6f, 1f));
                VisualKit.Part(sino, new TorusMesh { InnerRadius = 0.25f, OuterRadius = 0.3f, Rings = 18, RingSegments = 6 }, bronze, new Vector3(0, -0.5f, 0));
                VisualKit.Part(sino, VisualKit.Sphere(0.05f), VisualKit.Mat(new Color(0.2f, 0.18f, 0.16f), 0.5f, 0.6f), new Vector3(0, -0.5f, 0));   // badalo
                peca.Luz = new OmniLight3D { LightColor = new Color(1f, 0.82f, 0.45f), LightEnergy = 0f, OmniRange = 3f, Position = new Vector3(0, 1.6f, 0.3f), ShadowEnabled = false };
                raiz.AddChild(peca.Luz);
                break;
            }
            case "estatua_giro":
            {
                peca.Gira = VisualKit.Joint(raiz, Vector3.Zero);
                VisualKit.Part(raiz, VisualKit.Cylinder(0.62f, 0.7f, 0.3f, 20), StoneMat(0.45f, false), new Vector3(0, 0.15f, 0));   // pedestal
                VisualKit.Part(raiz, VisualKit.Ring(0.66f, 0.05f, 32), VisualKit.Mat(new Color(0.25f, 0.22f, 0.2f), 0.9f), new Vector3(0, 0.305f, 0));
                if (!MapKit.Glb(peca.Gira, "cenario_estatua_guardiao", new Vector3(0, 0.3f, 0), 0f, 0.62f))
                {
                    VisualKit.Part(peca.Gira, VisualKit.Capsule(0.28f, 1.6f), StoneMat(0.55f, false), new Vector3(0, 1.1f, 0));
                    VisualKit.Part(peca.Gira, VisualKit.Box(0.5f, 0.12f, 0.3f), StoneMat(0.5f, false), new Vector3(0, 1.5f, 0.2f));   // braços à frente
                }
                // seta gasta no pedestal giratório: mostra para onde ele olha
                VisualKit.Part(peca.Gira, VisualKit.Cylinder(0f, 0.12f, 0.2f, 3), VisualKit.Mat(new Color(0.85f, 0.78f, 0.6f), 0.6f), new Vector3(0, 0.33f, 0.55f), new Vector3(90, 0, 0));
                peca.Luz = new OmniLight3D { LightColor = new Color(0.55f, 0.85f, 1f), LightEnergy = 0f, OmniRange = 3f, Position = new Vector3(0, 2f, 0.4f), ShadowEnabled = false };
                raiz.AddChild(peca.Luz);
                break;
            }
            case "espelho":
            {
                VisualKit.Part(raiz, VisualKit.Cylinder(0.3f, 0.36f, 0.16f, 16), StoneMat(0.45f, false), new Vector3(0, 0.08f, 0));
                VisualKit.Part(raiz, VisualKit.Cylinder(0.05f, 0.06f, 1f, 8), bronze, new Vector3(0, 0.6f, 0));
                peca.Gira = VisualKit.Joint(raiz, new Vector3(0, 1.2f, 0));
                VisualKit.Part(peca.Gira, new TorusMesh { InnerRadius = 0.36f, OuterRadius = 0.43f, Rings = 28, RingSegments = 8 }, bronze, Vector3.Zero, new Vector3(90, 0, 0));
                var vidro = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.88f, 0.95f), Metallic = 1f, Roughness = 0.05f };
                VisualKit.Part(peca.Gira, VisualKit.Cylinder(0.37f, 0.37f, 0.03f, 28), vidro, Vector3.Zero, new Vector3(90, 0, 0));
                VisualKit.Part(peca.Gira, VisualKit.Box(0.7f, 0.7f, 0.04f), VisualKit.Mat(new Color(0.3f, 0.2f, 0.12f), 0.8f), new Vector3(0, 0, -0.04f), null, new Vector3(1f, 1f, 1f));   // costas
                peca.Luz = new OmniLight3D { LightColor = new Color(1f, 0.85f, 0.5f), LightEnergy = 0f, OmniRange = 2.5f, Position = new Vector3(0, 1.2f, 0), ShadowEnabled = false };
                raiz.AddChild(peca.Luz);
                break;
            }
            default:   // fonte_luz: braseiro que não apaga
            {
                if (!MapKit.Glb(raiz, "cenario/acropole_braseiro", Vector3.Zero, 0f, 0.8f))
                {
                    VisualKit.Part(raiz, VisualKit.Cylinder(0.12f, 0.2f, 0.9f, 10), StoneMat(0.4f, false), new Vector3(0, 0.45f, 0));
                    VisualKit.Part(raiz, VisualKit.Cylinder(0.42f, 0.22f, 0.25f, 16), bronze, new Vector3(0, 1f, 0));
                }
                VisualKit.Part(raiz, VisualKit.Sphere(0.22f), VisualKit.Glow(new Color(1f, 0.85f, 0.45f), 4f), new Vector3(0, 1.2f, 0), null, new Vector3(1f, 1.5f, 1f));
                raiz.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.45f), LightEnergy = 1.6f, OmniRange = 6f, Position = new Vector3(0, 1.5f, 0), ShadowEnabled = false });
                Sfx.Loop3D(raiz, "loop_fogueira", -8f, 2.5f, 12f);
                break;
            }
        }
        _enigma.Add(peca);
        _interactables.Add((obj, raiz));
        _objects.Add((obj, raiz));
        return true;
    }

    /// <summary>Marcas mudaram: alavanca desce, sino soa, estátua/espelho gira, raio acende.</summary>
    private void EnigmaFlags(IReadOnlySet<string> flags)
    {
        if (_enigma.Count == 0) return;
        if (!_raiosMontados) MontarRaios();
        foreach (var p in _enigma)
        {
            var o = p.Obj;
            var aberto = o.RequiresFlag is { } g && flags.Contains(g);
            switch (o.Kind)
            {
                case "alavanca_enig" or "sino":
                {
                    var feita = flags.Contains("runa:" + o.Id) || aberto;
                    if (feita && !p.Feita && p.Estado >= 0)
                    {
                        if (o.Kind == "sino") { p.Balanco = 1f; Sfx.PlayAt(o.Label.Contains("grande") ? "sino_grave" : o.Label.Contains("pequeno") ? "sino_agudo" : "sino_medio", p.Raiz.GlobalPosition, 0f, 0.01f); }
                        else Sfx.PlayAt("alavanca", p.Raiz.GlobalPosition, -2f);
                    }
                    p.Feita = feita;
                    p.Estado = feita ? 1 : 0;
                    if (p.Luz is not null) p.Luz.LightEnergy = feita ? 0.9f : 0f;
                    break;
                }
                case "estatua_giro" or "espelho":
                {
                    var k = EstadoDe(flags, o);
                    var passo = o.Kind == "espelho" ? 45f : 90f;
                    if (p.Estado >= 0 && k != p.Estado) Sfx.PlayAt("pedra_girando", p.Raiz.GlobalPosition, -3f);
                    p.Estado = k;
                    p.AlvoGraus = k * passo;
                    if (float.IsNaN(p.Graus)) { p.Graus = p.AlvoGraus; p.Gira?.Set("rotation_degrees", new Vector3(0, p.Graus, 0)); }
                    if (p.Luz is not null) p.Luz.LightEnergy = aberto ? 0.9f : 0f;
                    break;
                }
            }
        }
        foreach (var (grupo, raio, precisa) in _raios)
        {
            if (!GodotObject.IsInstanceValid(raio)) continue;
            var aberto = flags.Contains(grupo);
            var espelhos = _enigma.Where(p => p.Obj.Kind == "espelho" && p.Obj.RequiresFlag == grupo).OrderBy(p => p.Obj.Id, StringComparer.Ordinal).ToList();
            var certos = 0;
            foreach (var e in espelhos) { if (Certo(flags, e.Obj)) certos++; else break; }
            raio.Visible = aberto || certos >= precisa;
        }
    }

    /// <summary>Raio do braseiro: braseiro → espelho 1 → espelho 2 → selo (cada trecho só com os espelhos de antes certos).</summary>
    private void MontarRaios()
    {
        _raiosMontados = true;
        foreach (var fonte in _enigma.Where(p => p.Obj.Kind == "fonte_luz"))
        {
            var grupo = fonte.Obj.RequiresFlag;
            if (grupo is null) continue;
            var pts = new List<Vector3> { P(fonte.Obj.X, fonte.Obj.Y, 1.2f) };
            pts.AddRange(_enigma.Where(p => p.Obj.Kind == "espelho" && p.Obj.RequiresFlag == grupo).OrderBy(p => p.Obj.Id, StringComparer.Ordinal).Select(p => P(p.Obj.X, p.Obj.Y, 1.2f)));
            if (Map.Gates.FirstOrDefault(gt => gt.Flag == grupo) is { } selo) pts.Add(P(selo.X, selo.Y, 1.2f));
            var mat = VisualKit.Unshaded(new Color(1f, 0.85f, 0.45f, 0.85f), additive: true);
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var d = b - a;
                var mi = new MeshInstance3D { Mesh = VisualKit.Cylinder(0.05f, 0.05f, d.Length(), 8), MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                Root.AddChild(mi);
                var yv = d.Normalized();
                var xv = yv.Cross(Mathf.Abs(yv.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
                mi.GlobalTransform = new Transform3D(new Basis(xv, yv, xv.Cross(yv).Normalized()), (a + b) * 0.5f);
                mi.Visible = false;
                _raios.Add((grupo, mi, i));
            }
        }
    }

    /// <summary>A cada quadro: giro suave, sino balançando, cabo da alavanca descendo.</summary>
    private void AnimarEnigmas(float delta)
    {
        foreach (var p in _enigma)
        {
            if (!GodotObject.IsInstanceValid(p.Raiz)) continue;
            if (p.Gira is not null && !float.IsNaN(p.Graus))
            {
                var dif = Mathf.Wrap(p.AlvoGraus - p.Graus, -180f, 180f);
                p.Graus += Mathf.Clamp(dif, -120f * delta, 120f * delta);
                p.Gira.RotationDegrees = new Vector3(0, p.Graus, 0);
            }
            if (p.Cabo is not null)
            {
                var alvo = p.Feita ? -40f : 40f;
                var x = Mathf.MoveToward(p.Cabo.RotationDegrees.X, alvo, 240f * delta);
                p.Cabo.RotationDegrees = new Vector3(x, 0, 0);
            }
            if (p.Pendulo is not null)
            {
                p.Balanco = Mathf.MoveToward(p.Balanco, 0f, delta * 0.18f);
                var t = (float)Time.GetTicksMsec() / 1000f;
                p.Pendulo.RotationDegrees = new Vector3(Mathf.Sin(t * 5.5f) * 18f * p.Balanco, 0, 0);
            }
        }
    }
}
