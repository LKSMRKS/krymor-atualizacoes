using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 15/11 (pendência do pacote de movimento: "monstro de manequim andando de lado ou recuando ainda toca a animação de
/// andar para a frente"). Mede para onde o corpo anda de verdade (posição quadro a quadro) em relação para onde ele olha:
/// - recuando (mais de 115° para trás): o clipe de andar toca AO CONTRÁRIO (passo para trás);
/// - de lado: o quadril e as pernas viram para a direção do passo (até 60°) e o tronco volta, olhando para o alvo.
/// Só aparência: o servidor continua decidindo posição e direção. Desligar: PASSO_LATERAL=0.
/// </summary>
public sealed class PassoLateral
{
    private static readonly bool Desligado = OS.GetEnvironment("PASSO_LATERAL") == "0";
    private Vector3 _ultima;
    private bool _tem;
    private Vector3 _vel;
    private float _giro, _sentido = 1f;

    /// <summary>Devolve (sentido do clipe: 1 frente / -1 trás, giro do quadril em radianos).</summary>
    public (float Sentido, float Giro) Medir(Node3D raiz, float velocidade, bool andandoNoChao, float delta)
    {
        if (Desligado || !raiz.IsInsideTree() || delta <= 0f) return (1f, 0f);
        var p = raiz.GlobalPosition;
        if (!_tem) { _ultima = p; _tem = true; }
        var d = p - _ultima; d.Y = 0;
        _ultima = p;
        // teleporte / troca de mapa: não conta como passo
        if (d.Length() > 3f) d = Vector3.Zero;
        _vel = _vel.Lerp(d / delta, Mathf.Clamp(delta * 8f, 0f, 1f));
        var alvoGiro = 0f; var alvoSentido = 1f;
        if (andandoNoChao && velocidade > 0.35f && _vel.Length() > 0.3f)
        {
            var frente = raiz.GlobalBasis.Z; frente.Y = 0;
            if (frente.LengthSquared() > 1e-4f)
            {
                frente = frente.Normalized();
                var v = _vel.Normalized();
                var ang = Mathf.Atan2(frente.Cross(v).Y, frente.Dot(v));   // + = para a esquerda do boneco (giro em +Y)
                if (Mathf.Abs(ang) > Mathf.DegToRad(115f))
                {
                    alvoSentido = -1f;
                    var resto = ang > 0 ? ang - Mathf.Pi : ang + Mathf.Pi;   // quanto o "para trás" está torto
                    alvoGiro = Mathf.Clamp(resto, -0.8f, 0.8f);
                }
                else alvoGiro = Mathf.Clamp(ang, -1.05f, 1.05f);
            }
        }
        _giro = Mathf.MoveToward(_giro, alvoGiro, delta * 4f);
        // troca de sentido só quando o giro já voltou perto do zero (sem tremer no limite dos 115°)
        if (alvoSentido != _sentido && Mathf.Abs(_giro) < 0.35f) _sentido = alvoSentido;
        return (_sentido, _giro);
    }
}
