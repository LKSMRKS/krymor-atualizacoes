using System;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Braço não atravessa o corpo (pedido do dono 14/11: "o braço às vezes enfia pra dentro dele... tem que ser uma hitbox
/// e não pode atravessar a barriga"). Roda DEPOIS das animações e das poses de golpe (é o último modificador do
/// esqueleto): o corpo vira cápsulas invisíveis (peito, barriga, quadril, cabeça, coxas); se o cotovelo, o punho ou a
/// ponta da mão cair dentro de uma delas, o osso de cima (braço ou antebraço) gira o mínimo para pôr o ponto do lado de
/// fora. O tamanho do braço nunca muda (só rotação). Custa ~40 contas por braço por quadro.
/// Gancho (uma linha, logo depois de <c>_skel.AddChild(new ManequimPoser { Manequim = this });</c> no MannequinVisual):
/// <c>LimiteCorpo.Ligar(_skel);</c>
/// </summary>
public sealed partial class LimiteCorpo : SkeletonModifier3D
{
    /// <summary>Folga entre a mão e a pele, em fração da largura dos ombros (0,05 ≈ 1,5 cm num adulto).</summary>
    public static float Folga = 0.06f;
    /// <summary>Quantas correções por segundo (só para conferir: CDM_LIMITE_LOG=1 imprime a cada 2 s).</summary>
    public int Correcoes;
    /// <summary>Medição (vitrine de teste): maior entrada no corpo antes e depois da correção, em cm, por rótulo.</summary>
    public static string Rotulo = "";
    public static readonly System.Collections.Generic.Dictionary<string, (float Antes, float Depois)> Medida = new();

    private int _hips = -1, _spine = -1, _spine1 = -1, _spine2 = -1, _neck = -1, _head = -1;
    private readonly int[] _arm = { -1, -1 }, _fore = { -1, -1 }, _hand = { -1, -1 }, _upLeg = { -1, -1 }, _leg = { -1, -1 };
    private float _escala = 1f;   // largura dos ombros em unidades do esqueleto / 0,30 m
    private bool _pronto, _log;
    private double _t;

    public static LimiteCorpo? Ligar(Skeleton3D skel)
    {
        if (OS.GetEnvironment("CDM_SEM_LIMITE") == "1") return null;   // para comparar antes/depois
        var l = new LimiteCorpo { Name = "LimiteCorpo" };
        skel.AddChild(l);   // depois do ManequimPoser: roda por último
        return l;
    }

    private int Osso(Skeleton3D s, string nome)
    {
        for (var i = 0; i < s.GetBoneCount(); i++)
        {
            var n = s.GetBoneName(i);
            var cut = n.LastIndexOfAny(new[] { ':', '_' });
            var curto = n.StartsWith("mixamorig", StringComparison.Ordinal) && cut >= 0 ? n[(cut + 1)..] : n.StartsWith("mixamorig", StringComparison.Ordinal) ? n[9..] : n;
            if (curto == nome) return i;
        }
        return -1;
    }

    private bool Preparar(Skeleton3D s)
    {
        _hips = Osso(s, "Hips"); _spine = Osso(s, "Spine"); _spine1 = Osso(s, "Spine1"); _spine2 = Osso(s, "Spine2");
        _neck = Osso(s, "Neck"); _head = Osso(s, "Head");
        string[] lado = { "Left", "Right" };
        for (var k = 0; k < 2; k++)
        {
            _arm[k] = Osso(s, lado[k] + "Arm"); _fore[k] = Osso(s, lado[k] + "ForeArm"); _hand[k] = Osso(s, lado[k] + "Hand");
            _upLeg[k] = Osso(s, lado[k] + "UpLeg"); _leg[k] = Osso(s, lado[k] + "Leg");
        }
        if (_hips < 0 || _spine2 < 0 || _arm[0] < 0 || _arm[1] < 0 || _fore[0] < 0 || _hand[0] < 0) return false;
        var a = s.GetBoneGlobalRest(_arm[0]).Origin; var b = s.GetBoneGlobalRest(_arm[1]).Origin;
        _escala = Mathf.Max(1e-4f, a.DistanceTo(b) / 0.30f);
        _log = OS.GetEnvironment("CDM_LIMITE_LOG") == "1";
        return true;
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var s = GetSkeleton();
        if (s is null) return;
        if (!_pronto) { if (!Preparar(s)) return; _pronto = true; }
        var e = _escala;
        // cápsulas do corpo (pontos em espaço do esqueleto, raios em metros * escala)
        Vector3 P(int i) => i >= 0 ? s.GetBoneGlobalPose(i).Origin : Vector3.Zero;
        var hips = P(_hips); var sp = P(_spine >= 0 ? _spine : _hips); var sp1 = P(_spine1 >= 0 ? _spine1 : _spine2);
        var sp2 = P(_spine2); var neck = P(_neck >= 0 ? _neck : _spine2); var head = P(_head >= 0 ? _head : _neck);
        var caps = new (Vector3 A, Vector3 B, float R)[]
        {
            (sp1, sp2.Lerp(neck, 0.4f), 0.115f * e),        // peito
            (hips, sp1, 0.105f * e),                         // barriga
            (hips + (hips - sp) * 0.4f, hips, 0.11f * e),    // quadril
            (head, head + (head - neck).Normalized() * 0.12f * e, 0.095f * e),   // cabeça
            (P(_upLeg[0]), P(_leg[0]), 0.07f * e),
            (P(_upLeg[1]), P(_leg[1]), 0.07f * e),
        };
        var folga = Folga * 0.30f * e;
        var medir = Rotulo.Length > 0;
        var antes = medir ? Entrada(s, caps) : 0f;
        for (var k = 0; k < 2; k++)
        {
            if (_arm[k] < 0 || _fore[k] < 0 || _hand[k] < 0) continue;
            for (var passo = 0; passo < 2; passo++)
            {
                // meio do braço e cotovelo: gira o braço em volta do ombro
                Empurra(s, _arm[k], P(_arm[k]).Lerp(P(_fore[k]), 0.6f), caps, folga);
                Empurra(s, _arm[k], P(_fore[k]), caps, folga);
                // meio do antebraço
                Empurra(s, _fore[k], P(_fore[k]).Lerp(P(_hand[k]), 0.5f), caps, folga);
                // punho e ponta da mão: gira o antebraço em volta do cotovelo
                Empurra(s, _fore[k], P(_hand[k]), caps, folga);
                var mao = s.GetBoneGlobalPose(_hand[k]);
                var ponta = mao.Origin + mao.Basis.Y.Normalized() * 0.09f * e;   // eixo Y do osso aponta para os dedos (Mixamo)
                Empurra(s, _fore[k], ponta, caps, folga);
            }
        }
        if (medir)
        {
            var depois = Entrada(s, caps);
            Medida.TryGetValue(Rotulo, out var m);
            Medida[Rotulo] = (Mathf.Max(m.Antes, antes / e * 100f), Mathf.Max(m.Depois, depois / e * 100f));
        }
        if (_log && (_t += delta) > 2) { GD.Print($"LimiteCorpo: {Correcoes} correções em {_t:0.0}s"); _t = 0; Correcoes = 0; }
    }

    /// <summary>Quanto o cotovelo, o punho ou a ponta da mão estão DENTRO das cápsulas (sem folga), em unidades do esqueleto.</summary>
    private float Entrada(Skeleton3D s, (Vector3 A, Vector3 B, float R)[] caps)
    {
        var pior = 0f;
        for (var k = 0; k < 2; k++)
        {
            if (_arm[k] < 0 || _fore[k] < 0 || _hand[k] < 0) continue;
            var mao = s.GetBoneGlobalPose(_hand[k]);
            var om = s.GetBoneGlobalPose(_arm[k]).Origin; var co = s.GetBoneGlobalPose(_fore[k]).Origin;
            foreach (var pt in new[] { om.Lerp(co, 0.6f), co, co.Lerp(mao.Origin, 0.5f), mao.Origin, mao.Origin + mao.Basis.Y.Normalized() * 0.09f * _escala })
                foreach (var (a, b, r) in caps)
                {
                    var ab = b - a; var len2 = ab.LengthSquared();
                    var t = len2 > 1e-8f ? Mathf.Clamp((pt - a).Dot(ab) / len2, 0f, 1f) : 0f;
                    pior = Mathf.Max(pior, r - pt.DistanceTo(a + ab * t));
                }
        }
        return pior;
    }

    private void Empurra(Skeleton3D s, int osso, Vector3 ponto, (Vector3 A, Vector3 B, float R)[] caps, float folga)
    {
        foreach (var (a, b, r) in caps)
        {
            var ab = b - a; var len2 = ab.LengthSquared();
            var t = len2 > 1e-8f ? Mathf.Clamp((ponto - a).Dot(ab) / len2, 0f, 1f) : 0f;
            var c = a + ab * t; var d = ponto - c; var dist = d.Length();
            var lim = r + folga;
            if (dist >= lim) continue;
            var normal = dist > 1e-5f ? d / dist : Vector3.Up;
            var alvo = c + normal * lim;
            var g = s.GetBoneGlobalPose(osso);
            var de = ponto - g.Origin; var para = alvo - g.Origin;
            if (de.LengthSquared() < 1e-8f || para.LengthSquared() < 1e-8f) continue;
            var giro = new Quaternion(de.Normalized(), para.Normalized());
            if (!giro.IsFinite()) continue;
            var novo = new Transform3D(new Basis(giro) * g.Basis, g.Origin);
            var pai = s.GetBoneParent(osso);
            var paiG = pai >= 0 ? s.GetBoneGlobalPose(pai) : Transform3D.Identity;
            var local = paiG.AffineInverse() * novo;
            s.SetBonePoseRotation(osso, local.Basis.GetRotationQuaternion());
            Correcoes++;
            return;   // um empurrão por chamada; o próximo passo confere de novo
        }
    }
}
