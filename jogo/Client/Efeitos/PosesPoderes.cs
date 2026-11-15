using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 14/11 — poses que combinam com o nome da habilidade (pedido do dono). Duas partes, ligadas ao MannequinVisual por
/// 3 linhas (ver relatório): FamiliaPoder() troca a família de pose de habilidades que caíam numa pose errada
/// (ex.: Prisão de Gelo empurrava a mão para a frente; agora aponta o chão), e PosePoder() desenha as famílias novas.
/// VFX_ANTIGO=1 desliga (volta às poses antigas). Mesmas ferramentas da pose original: Pose(osso, eixo, ângulo, peso)
/// e AimArm(lado, braço, antebraço, peso) com X = para fora do lado do braço, Y = para cima, Z = para a frente.
/// </summary>
public sealed partial class MannequinVisual
{
    /// <summary>Família de pose nova (ou existente mais adequada) para o id; null = regra antiga do PoseKind.</summary>
    internal static string? FamiliaPoder(string skill)
    {
        if (!Client.Efeitos.PoderesVfx.Ligado || !skill.StartsWith("skill_", System.StringComparison.Ordinal)) return null;
        return skill switch
        {
            "skill_mago_prisao_gelo" => "apontar_chao",
            "skill_mago_anel_chamas" => "explode_volta",
            "skill_guerreiro_chuva_espadas" => "lancar_ceu",
            "skill_guerreiro_laminas_sangue" => "x_cruzado",
            "skill_bruxa_vinhas" => "chicote",
            "skill_leao_rugido_supremo" => "rugido",
            "skill_arqueiro_serpente" => "onda_mar",
            "skill_bruxa_tronco" => "erguer_bater",
            // famílias que já existiam, mas não eram escolhidas para estes ids
            "skill_guerreiro_estocada" => "investida",
            "skill_mago_esfera_protecao" => "barreira",
            "skill_guerreiro_furia_colosso" or "skill_arma_onda" => "cunha",
            "skill_mascara_furacao" or "skill_macaco_onda" => "giro",
            "skill_macaco_avanco" => "investida",
            "skill_leao_lanca_solar" => "arco",
            "skill_bruxa_raizes" or "skill_matriarca_espinhos_raiz" or "skill_leao_jardim_garras" => "chao",
            _ => null,
        };
    }

    /// <summary>Desenha as famílias novas. true = tratou (o switch antigo não roda).</summary>
    private bool PosePoder(string kind, string phase, float w, bool strike)
    {
        switch (kind)
        {
            case "apontar_chao":
                // Prisão de Gelo: ergue o braço (junta o frio) e crava a mão/cajado apontando o chão marcado
                if (!strike && phase == "windup")
                {
                    AimArm("Right", new(0.35f, 0.85f, 0.25f), new(0.15f, 0.95f, 0.3f), w);
                    AimArm("Left", new(0.3f, -0.4f, 0.8f), new(-0.6f, 0.3f, 0.7f), w);
                    Pose("Spine1", AxL, -0.15f, w);
                }
                else
                {
                    AimArm("Right", new(0.15f, -0.5f, 0.85f), new(0.05f, -0.8f, 0.6f), w);
                    AimArm("Left", new(0.2f, -0.55f, 0.8f), new(0.05f, -0.8f, 0.6f), w);
                    Pose("Spine1", AxL, 0.4f, w);
                    Pose("Neck", AxL, 0.25f, w);
                    _attackCrouch = Mathf.Max(_attackCrouch, 0.1f * w);
                }
                return true;
            case "explode_volta":
                // Anel de Chamas: encolhe com os braços cruzados no peito e explode abrindo os braços para os lados
                if (!strike && phase == "windup")
                {
                    AimArm("Left", new(0.25f, -0.45f, 0.85f), new(-0.85f, 0.25f, 0.45f), w);
                    AimArm("Right", new(0.25f, -0.45f, 0.85f), new(-0.85f, 0.3f, 0.4f), w);
                    Pose("Spine1", AxL, 0.3f, w);
                    Pose("Neck", AxL, 0.2f, w);
                    _attackCrouch = Mathf.Max(_attackCrouch, 0.14f * w);
                }
                else
                {
                    AimArm("Left", new(0.95f, -0.2f, 0.15f), new(0.95f, -0.05f, 0.2f), w);
                    AimArm("Right", new(0.95f, -0.2f, 0.15f), new(0.95f, -0.05f, 0.2f), w);
                    Pose("Spine1", AxL, -0.22f, w);
                    Pose("Neck", AxL, -0.2f, w);
                }
                return true;
            case "lancar_ceu":
                // Chuva de Espadas: agacha com a espada embaixo e atira para o alto, olhando para cima
                if (!strike && phase == "windup")
                {
                    AimArm("Right", new(0.35f, -0.65f, -0.55f), new(0.2f, -0.35f, -0.9f), w);
                    Pose("Spine1", AxL, 0.25f, w);
                    _attackCrouch = Mathf.Max(_attackCrouch, 0.15f * w);
                }
                else
                {
                    AimArm("Right", new(0.1f, 1f, 0.08f), new(0.05f, 1f, 0.12f), w);
                    AimArm("Left", new(0.6f, -0.5f, 0.4f), new(0.5f, -0.3f, 0.7f), w * 0.7f);
                    Pose("Spine1", AxL, -0.28f, w);
                    Pose("Neck", AxL, -0.45f, w);
                }
                return true;
            case "x_cruzado":
                // Lâminas de Sangue: braços cruzados no alto e os dois descem em diagonal, soltando o X
                if (!strike && phase == "windup")
                {
                    AimArm("Left", new(0.35f, 0.9f, -0.05f), new(-0.65f, 0.7f, 0.1f), w);
                    AimArm("Right", new(0.35f, 0.9f, -0.05f), new(-0.65f, 0.7f, 0.1f), w);
                    Pose("Spine1", AxL, -0.2f, w);
                }
                else
                {
                    AimArm("Left", new(0.6f, -0.35f, 0.72f), new(0.7f, -0.5f, 0.5f), w);
                    AimArm("Right", new(0.6f, -0.35f, 0.72f), new(0.7f, -0.5f, 0.5f), w);
                    Pose("Spine1", AxL, 0.38f, w);
                    _attackCrouch = Mathf.Max(_attackCrouch, 0.08f * w);
                }
                return true;
            case "chicote":
            {
                // Vinhas Chicote: mão da névoa (esquerda na Bruxa) sobe atrás da cabeça e chicoteia para a frente e para baixo
                var mao = _classId == "bruxa" ? "Left" : "Right";
                var lado = mao == "Left" ? -1f : 1f;
                if (!strike && phase == "windup")
                {
                    AimArm(mao, new(0.3f, 0.85f, -0.4f), new(0.1f, 0.55f, -0.85f), w);
                    Pose("Spine1", AxUp, -0.35f * lado, w);
                }
                else
                {
                    AimArm(mao, new(0.1f, -0.1f, 1f), new(0.0f, -0.4f, 0.92f), w);
                    Pose("Spine1", AxUp, 0.35f * lado, w);
                    Pose("Spine1", AxL, 0.2f, w);
                }
                return true;
            }
            case "rugido":
                // Rugido Supremo: enche o peito com os braços para trás e solta o rugido, cabeça para a frente, braços abertos
                if (!strike && phase == "windup")
                {
                    AimArm("Left", new(0.5f, -0.6f, -0.6f), new(0.4f, -0.5f, -0.75f), w);
                    AimArm("Right", new(0.5f, -0.6f, -0.6f), new(0.4f, -0.5f, -0.75f), w);
                    Pose("Spine1", AxL, 0.25f, w);
                    _attackCrouch = Mathf.Max(_attackCrouch, 0.12f * w);
                }
                else
                {
                    AimArm("Left", new(0.9f, 0.05f, -0.4f), new(0.9f, 0.2f, -0.35f), w);
                    AimArm("Right", new(0.9f, 0.05f, -0.4f), new(0.9f, 0.2f, -0.35f), w);
                    Pose("Spine1", AxL, -0.3f, w);
                    Pose("Neck", AxL, 0.3f, w);
                }
                return true;
            case "onda_mar":
                // Serpente do Mar: as duas mãos varrem do chão para o alto, erguendo a onda
                if (!strike && phase == "windup")
                {
                    AimArm("Left", new(0.25f, -0.85f, 0.45f), new(0.1f, -0.7f, 0.7f), w);
                    AimArm("Right", new(0.25f, -0.85f, 0.45f), new(0.1f, -0.7f, 0.7f), w);
                    Pose("Spine", AxL, 0.3f, w);
                    _attackCrouch = Mathf.Max(_attackCrouch, 0.15f * w);
                }
                else
                {
                    AimArm("Left", new(0.2f, 0.75f, 0.62f), new(0.1f, 0.9f, 0.4f), w);
                    AimArm("Right", new(0.2f, 0.75f, 0.62f), new(0.1f, 0.9f, 0.4f), w);
                    Pose("Spine1", AxL, -0.22f, w);
                }
                return true;
            case "erguer_bater":
                // Tronco Esmagador: puxa o tronco do chão com as mãos para o alto e bate com ele para a frente
                if (!strike && phase == "windup")
                {
                    AimArm("Left", new(0.4f, 0.88f, 0.15f), new(0.2f, 1f, 0.2f), w);
                    AimArm("Right", new(0.4f, 0.88f, 0.15f), new(0.2f, 1f, 0.2f), w);
                    Pose("Spine1", AxL, -0.2f, w);
                }
                else
                {
                    AimArm("Left", new(0.1f, -0.45f, 0.88f), new(0.0f, -0.8f, 0.6f), w);
                    AimArm("Right", new(0.1f, -0.45f, 0.88f), new(0.0f, -0.8f, 0.6f), w);
                    Pose("Spine1", AxL, 0.5f, w);
                    Pose("Spine", AxL, 0.15f, w);
                }
                return true;
        }
        return false;
    }
}
