using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Armas, escudos e itens de chão feitos com formas básicas, escolhidos pelo ID de modelo do item
/// (model_asset_id em items.json). Empunhadura na origem, arma apontando para +Y.
/// Trocar por modelos de verdade = mudar só esta fábrica.
/// </summary>
public static class GearFactory
{
    private static readonly Color Steel = new(0.72f, 0.74f, 0.78f);
    private static readonly Color DarkSteel = new(0.32f, 0.33f, 0.36f);
    private static readonly Color Wood = new(0.42f, 0.27f, 0.14f);
    private static readonly Color Leather = new(0.30f, 0.19f, 0.11f);
    private static readonly Color Brass = new(0.78f, 0.60f, 0.25f);

    /// <summary>Arma corpo a corpo de duas mãos (12/11: inclui as do arsenal novo — espada longa, katana, lança, glaive,
    /// machados de guerra, martelos e montante — que antes ficavam com a mão esquerda solta). Arco e cajado têm pose própria.</summary>
    public static bool IsTwoHanded(string? model) => model is "axe_quarry" or "hammer_foreman" or "maul"
        || model is not null && (model.StartsWith("arma_espada_longa") || model.StartsWith("arma_katana") || model.StartsWith("arma_lanca")
            || model.StartsWith("arma_glaive") || model.StartsWith("arma_machado_guerra") || model.StartsWith("arma_machado_leao")
            || model.StartsWith("arma_machado_lobo") || model.StartsWith("arma_machado_presa") || model.StartsWith("arma_martelo")
            || model.StartsWith("arma_montante")
            // 14/11: armas lendárias do trabalhador (duas mãos)
            || model.StartsWith("arma_lend_machado_gigante") || model.StartsWith("arma_lend_foice") || model.StartsWith("arma_lend_garfo")
            || model.StartsWith("arma_lend_espada_dupla") || model.StartsWith("arma_lend_lanca"));
    public static bool IsStaff(string? model) => model is not null && (model.StartsWith("staff_") || model.StartsWith("arma_cajado") || model.StartsWith("arma_lend_cajado"));

    /// <summary>
    /// Armas e escudos esculpidos do lote 2 (Assets/Models/armas): mesmo encaixe dos feitos por código (empunhadura na
    /// origem, lâmina para +Y; escudo pela alça, face para +Z). Faixa pelo ID de modelo do item. Sem o arquivo: o de código.
    /// </summary>
    internal static string? LotTwoWeapon(string model) => model switch
    {
        "sword_p1_placeholder" => "arma_espada_t1_chapa",
        "sword_ornate" or "sword_claw" => "arma_espada_t2_bronze_ritual",
        "sword_vein" => "arma_espada_t3_brasa_bifurcada",
        "hatchet" => "arma_machado_t1_ferragem",
        "axe_quarry" => "arma_machado_t2_bronze_duplo",
        "axe_void" => "arma_machado_t3_crescente_violeta",
        "staff_twig" or "staff_apprentice" => "arma_cajado_t1_galho",
        "staff_crystal" or "staff_foreman" or "staff_shell" or "staff_coral" or "staff_twin" => "arma_cajado_t2_cristal_bronze",
        "staff_vein" or "staff_ember" => "arma_cajado_t3_halo_do_vazio",
        "shield_planks" => "arma_escudo_t1_tabuas",
        "shield_stone" => "arma_escudo_t2_pipa_ferro",
        "shield_ember" => "arma_escudo_t3_nucleo_de_brasa",
        // 11/11 (arsenal da noite do trabalhador): o que ainda era boneco de formas simples ganha modelo esculpido
        "club_driftwood" => "arma_clava_enferrujada",
        "spear_watch" => "arma_lanca_comum",
        "hammer_foreman" => "arma_maca_refinada",
        // qualquer item novo pode apontar direto para um arquivo do arsenal ("arma_katana_comum" etc.)
        _ when model.StartsWith("arma_") => model,
        _ => null,
    };

    /// <summary>Arco (desenhado ou esculpido do arsenal): vai na mão esquerda / nas costas e atira flecha.</summary>
    public static bool IsBow(string? model) => model is not null && (model.StartsWith("bow_") || model.StartsWith("arma_arco_") || model.StartsWith("arma_lend_arco_"));

    /// <summary>Ícone pintado de uma arma: o do lote 2 fica em Art/, o do arsenal em Art/icones/armas/.</summary>
    private static string WeaponIcon(string file)
    {
        var name = "ico_" + file["arma_".Length..] + ".png";
        return Godot.FileAccess.FileExists("res://Assets/Art/icones/armas/" + name) ? "icones/armas/" + name : name;
    }

    private static Node3D? LotTwo(string model, string name)
    {
        if (LotTwoWeapon(model) is not { } file || GlbCache.Instance("armas/" + file) is not { } glb) return null;
        var root = new Node3D { Name = name };
        root.AddChild(glb);
        return root;
    }

    /// <summary>Arma na mão (null se o ID não for conhecido: a mão fica vazia).</summary>
    public static Node3D? Weapon(string? model)
    {
        if (model is not null && GearFactory.IsBow(model)) return MannequinVisual.BowModel(model);
        if (model is null) return null;
        if (!model.StartsWith("shield_") && LotTwo(model, "Arma_" + model) is { } sculpted) return sculpted;
        var root = new Node3D { Name = "Arma_" + model };
        switch (model)
        {
            case "sword_p1_placeholder":
                Grip(root, 0.2f);
                VisualKit.Part(root, VisualKit.Box(0.30f, 0.05f, 0.07f), VisualKit.Mat(Brass, 0.4f, 0.6f), new Vector3(0, 0.12f, 0));
                VisualKit.Part(root, VisualKit.Box(0.075f, 0.82f, 0.02f), VisualKit.Mat(Steel, 0.25f, 0.85f), new Vector3(0, 0.55f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0f, 0.04f, 0.1f, 4), VisualKit.Mat(Steel, 0.25f, 0.85f), new Vector3(0, 1.01f, 0));
                break;
            case "axe_quarry":
                Grip(root, 1.05f, Wood, 0.03f, 0.35f);
                VisualKit.Part(root, VisualKit.Box(0.42f, 0.30f, 0.05f), VisualKit.Mat(DarkSteel, 0.35f, 0.8f), new Vector3(0.18f, 0.95f, 0));
                VisualKit.Part(root, VisualKit.Box(0.08f, 0.36f, 0.06f), VisualKit.Mat(Steel, 0.2f, 0.9f), new Vector3(0.40f, 0.95f, 0));
                VisualKit.Part(root, VisualKit.Box(0.12f, 0.12f, 0.08f), VisualKit.Mat(DarkSteel, 0.4f, 0.7f), new Vector3(-0.08f, 0.95f, 0));
                break;
            case "spear_watch":
                Grip(root, 1.7f, Wood, 0.025f, 0.55f);
                VisualKit.Part(root, VisualKit.Cylinder(0f, 0.06f, 0.36f, 4), VisualKit.Mat(Steel, 0.2f, 0.9f), new Vector3(0, 1.32f, 0));
                VisualKit.Part(root, VisualKit.Box(0.16f, 0.04f, 0.04f), VisualKit.Mat(Brass, 0.4f, 0.6f), new Vector3(0, 1.12f, 0));
                VisualKit.Part(root, VisualKit.Box(0.06f, 0.18f, 0.01f), VisualKit.Mat(new Color(0.6f, 0.1f, 0.1f)), new Vector3(0.05f, 1.02f, 0));
                break;
            case "mace_spiked":
            case "mace_morningstar":
            case "mace_flanged":
                CabecaDeMaca(root, model);
                break;
            case "hammer_foreman":
            case "maul":
                Grip(root, 1.15f, model == "maul" ? DarkSteel : Wood, 0.04f, 0.3f);
                // cabeça de marreta: cilindro atravessado no cabo, com anéis de latão nas pontas
                VisualKit.Part(root, VisualKit.Cylinder(0.19f, 0.19f, 0.62f, 10), VisualKit.Mat(model == "maul" ? new Color(0.22f, 0.2f, 0.2f) : DarkSteel, 0.5f, 0.7f),
                    new Vector3(0, 1.05f, 0), new Vector3(0, 0, 90));
                foreach (var end in new[] { -0.3f, 0.3f })
                    VisualKit.Part(root, VisualKit.Cylinder(0.215f, 0.215f, 0.07f, 10), VisualKit.Mat(Brass, 0.4f, 0.7f), new Vector3(end, 1.05f, 0), new Vector3(0, 0, 90));
                if (model == "hammer_foreman")
                    VisualKit.Part(root, VisualKit.Sphere(0.07f), VisualKit.Glow(new Color(1f, 0.45f, 0.1f), 3f), new Vector3(0, 1.05f, 0.18f));
                break;
            case "cleaver":
                Grip(root, 0.25f, Leather);
                VisualKit.Part(root, VisualKit.Box(0.26f, 0.70f, 0.03f), VisualKit.Mat(new Color(0.45f, 0.35f, 0.30f), 0.6f, 0.5f), new Vector3(0.06f, 0.55f, 0));
                VisualKit.Part(root, VisualKit.Box(0.05f, 0.70f, 0.04f), VisualKit.Mat(new Color(0.55f, 0.15f, 0.1f), 0.7f, 0.3f), new Vector3(0.19f, 0.55f, 0));
                break;
            case "staff_apprentice":
                Staff(root, Wood, new Color(0.75f, 0.85f, 1f), 0.06f, 1.0f);
                break;
            case "staff_ember":
                Staff(root, new Color(0.25f, 0.14f, 0.08f), new Color(1f, 0.45f, 0.1f), 0.1f, 3f);
                for (var i = 0; i < 3; i++)
                    VisualKit.Part(root, VisualKit.Cylinder(0.005f, 0.02f, 0.2f, 4), VisualKit.Mat(Brass, 0.4f, 0.7f),
                        new Vector3(Mathf.Cos(i * 2.1f) * 0.07f, 1.18f, Mathf.Sin(i * 2.1f) * 0.07f), new Vector3(0, 0, i * 20 - 20));
                break;
            case "staff_twin":
                Grip(root, 1.0f, new Color(0.3f, 0.3f, 0.35f), 0.025f, 0.45f);
                foreach (var side in new[] { -1f, 1f })
                {
                    VisualKit.Part(root, VisualKit.Cylinder(0.015f, 0.02f, 0.38f), VisualKit.Mat(new Color(0.3f, 0.3f, 0.35f)),
                        new Vector3(side * 0.08f, 1.12f, 0), new Vector3(0, 0, -side * 22f));
                    VisualKit.Part(root, VisualKit.Sphere(0.06f), VisualKit.Glow(new Color(0.35f, 0.7f, 1f), 3f), new Vector3(side * 0.16f, 1.3f, 0));
                }
                break;
            case "staff_foreman":
                Staff(root, new Color(0.16f, 0.14f, 0.14f), new Color(1f, 0.2f, 0.1f), 0.12f, 3.5f);
                VisualKit.Part(root, VisualKit.Box(0.32f, 0.06f, 0.06f), VisualKit.Mat(Brass, 0.4f, 0.7f), new Vector3(0, 1.08f, 0));
                break;
            // ---- E9 ----
            case "club_driftwood":
                Grip(root, 0.25f, Wood.Lightened(0.15f), 0.03f);
                VisualKit.Part(root, VisualKit.Cylinder(0.045f, 0.075f, 0.55f, 7), VisualKit.Mat(new Color(0.55f, 0.45f, 0.33f), 0.95f), new Vector3(0, 0.5f, 0), new Vector3(0, 0, 4));
                break;
            case "hatchet":
                Grip(root, 0.55f, Wood, 0.025f, 0.12f);
                VisualKit.Part(root, VisualKit.Box(0.26f, 0.18f, 0.04f), VisualKit.Mat(DarkSteel, 0.35f, 0.8f), new Vector3(0.11f, 0.52f, 0));
                VisualKit.Part(root, VisualKit.Box(0.05f, 0.22f, 0.05f), VisualKit.Mat(Steel, 0.2f, 0.9f), new Vector3(0.25f, 0.52f, 0));
                break;
            case "sword_claw":
                Grip(root, 0.22f, Leather);
                VisualKit.Part(root, VisualKit.Box(0.24f, 0.06f, 0.08f), VisualKit.Mat(new Color(0.75f, 0.35f, 0.25f), 0.5f), new Vector3(0, 0.13f, 0));
                VisualKit.Part(root, VisualKit.Box(0.11f, 0.9f, 0.04f), VisualKit.Mat(new Color(0.9f, 0.5f, 0.35f), 0.35f, 0.3f), new Vector3(0.02f, 0.6f, 0), new Vector3(0, 0, -4));
                VisualKit.Part(root, VisualKit.Cylinder(0f, 0.07f, 0.16f, 4), VisualKit.Mat(new Color(0.95f, 0.85f, 0.75f), 0.3f), new Vector3(0.05f, 1.1f, 0), new Vector3(0, 0, -12));
                break;
            case "sword_vein":
                Grip(root, 0.24f, new Color(0.15f, 0.12f, 0.12f));
                VisualKit.Part(root, VisualKit.Box(0.34f, 0.06f, 0.08f), VisualKit.Mat(new Color(0.3f, 0.32f, 0.36f), 0.3f, 0.8f), new Vector3(0, 0.13f, 0));
                VisualKit.Part(root, VisualKit.Box(0.085f, 0.95f, 0.025f), VisualKit.Mat(new Color(0.78f, 0.80f, 0.86f), 0.2f, 0.9f), new Vector3(0, 0.62f, 0));
                VisualKit.Part(root, VisualKit.Box(0.02f, 0.85f, 0.03f), VisualKit.Glow(new Color(0.4f, 0.85f, 1f), 3f), new Vector3(0, 0.6f, 0));
                break;
            case "staff_twig":
                Grip(root, 1.0f, new Color(0.5f, 0.4f, 0.3f), 0.022f, 0.45f);
                VisualKit.Part(root, VisualKit.Cylinder(0.008f, 0.015f, 0.25f, 4), VisualKit.Mat(new Color(0.5f, 0.4f, 0.3f)), new Vector3(0.06f, 1.0f, 0), new Vector3(0, 0, -30));
                break;
            case "staff_coral":
                Staff(root, new Color(0.85f, 0.45f, 0.4f), new Color(1f, 0.6f, 0.55f), 0.07f, 1.5f);
                break;
            case "staff_shell":
                Staff(root, new Color(0.35f, 0.3f, 0.28f), new Color(0.4f, 0.9f, 0.85f), 0.09f, 2.5f);
                VisualKit.Part(root, VisualKit.Sphere(0.13f), VisualKit.Mat(new Color(0.95f, 0.85f, 0.75f), 0.3f), new Vector3(0, 1.17f, -0.04f), null, new Vector3(1.2f, 0.8f, 0.5f));
                break;
            case "staff_vein":
                Staff(root, new Color(0.18f, 0.18f, 0.22f), new Color(0.4f, 0.85f, 1f), 0.11f, 3.5f);
                foreach (var side in new[] { -1f, 1f })
                    VisualKit.Part(root, VisualKit.Box(0.03f, 0.3f, 0.03f), VisualKit.Mat(new Color(0.3f, 0.32f, 0.36f), 0.3f, 0.8f), new Vector3(side * 0.08f, 1.2f, 0), new Vector3(0, 0, side * -18));
                break;
            // ---- vitrine do Porto (caros, de começo): mudam a cor e o formato ----
            case "sword_ornate":
                Grip(root, 0.24f, new Color(0.35f, 0.08f, 0.08f));
                VisualKit.Part(root, VisualKit.Box(0.40f, 0.06f, 0.09f), VisualKit.Mat(Brass, 0.25f, 0.9f), new Vector3(0, 0.13f, 0));
                foreach (var side in new[] { -1f, 1f })
                    VisualKit.Part(root, VisualKit.Sphere(0.04f), VisualKit.Mat(Brass, 0.25f, 0.9f), new Vector3(side * 0.21f, 0.13f, 0));
                VisualKit.Part(root, VisualKit.Box(0.09f, 0.92f, 0.022f), VisualKit.Mat(new Color(0.92f, 0.85f, 0.62f), 0.15f, 0.95f), new Vector3(0, 0.62f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0f, 0.045f, 0.12f, 4), VisualKit.Mat(new Color(0.92f, 0.85f, 0.62f), 0.15f, 0.95f), new Vector3(0, 1.14f, 0));
                VisualKit.Part(root, VisualKit.Sphere(0.035f), VisualKit.Glow(new Color(1f, 0.25f, 0.2f), 2f), new Vector3(0, 0.13f, 0.05f));
                break;
            case "staff_crystal":
                Staff(root, new Color(0.85f, 0.85f, 0.9f), new Color(0.35f, 0.6f, 1f), 0.1f, 3f);
                for (var i = 0; i < 4; i++)
                    VisualKit.Part(root, VisualKit.Box(0.04f, 0.2f, 0.04f), VisualKit.Glow(new Color(0.55f, 0.75f, 1f), 1.8f),
                        new Vector3(Mathf.Cos(i * 1.57f) * 0.1f, 1.25f, Mathf.Sin(i * 1.57f) * 0.1f), new Vector3(0, i * 45, 20));
                break;
            case "rifle_fallen":
            {
                // 12/11: rifle do Capitão Fallen — coronha de madeira escura (para trás do punho), corpo de ferro com gatilho,
                // cano comprido com bandas de latão e uma luneta curta
                var madeira = VisualKit.Mat(new Color(0.30f, 0.17f, 0.09f), 0.6f);
                var ferro = VisualKit.Mat(new Color(0.16f, 0.16f, 0.17f), 0.35f, 0.85f);
                var latao = VisualKit.Mat(new Color(0.72f, 0.55f, 0.25f), 0.3f, 0.9f);
                VisualKit.Part(root, VisualKit.Box(0.06f, 0.30f, 0.11f), madeira, new Vector3(0, -0.20f, -0.02f), new Vector3(-8, 0, 0));   // coronha
                VisualKit.Part(root, VisualKit.Box(0.05f, 0.12f, 0.06f), madeira, new Vector3(0, -0.02f, 0.01f));                          // punho
                VisualKit.Part(root, VisualKit.Box(0.05f, 0.22f, 0.07f), ferro, new Vector3(0, 0.12f, 0.03f));                             // corpo
                VisualKit.Part(root, VisualKit.Box(0.045f, 0.40f, 0.05f), madeira, new Vector3(0, 0.38f, 0.02f));                          // guarda-mão
                VisualKit.Part(root, VisualKit.Cylinder(0.014f, 0.016f, 0.62f, 8), ferro, new Vector3(0, 0.55f, 0.05f));                   // cano
                foreach (var y in new[] { 0.30f, 0.52f })
                    VisualKit.Part(root, VisualKit.Cylinder(0.03f, 0.03f, 0.025f, 10), latao, new Vector3(0, y, 0.03f));                   // bandas
                VisualKit.Part(root, VisualKit.Cylinder(0.018f, 0.018f, 0.16f, 10), ferro, new Vector3(0, 0.16f, 0.085f));                 // luneta
                VisualKit.Part(root, new TorusMesh { InnerRadius = 0.012f, OuterRadius = 0.02f, Rings = 12, RingSegments = 4 }, latao, new Vector3(0, 0.06f, 0.0f), new Vector3(0, 0, 90));   // gatilho
                break;
            }
            case "pick":
                Grip(root, 0.85f, Wood, 0.025f, 0.2f);
                VisualKit.Part(root, VisualKit.Box(0.6f, 0.06f, 0.05f), VisualKit.Mat(DarkSteel, 0.4f, 0.7f), new Vector3(0, 0.82f, 0), new Vector3(0, 0, 8));
                break;
            default:
                return null;
        }
        return root;
    }

    /// <summary>Peça de cabeça (E9) presa ao topo da cabeça do boneco; null = cabeça à mostra.</summary>
    public static Node3D? Helmet(string? model)
    {
        if (model is null) return null;
        var root = new Node3D { Name = "Elmo" };
        switch (model)
        {
            case "head_cap":
                VisualKit.Part(root, VisualKit.Sphere(0.135f), VisualKit.Mat(new Color(0.42f, 0.28f, 0.16f), 0.9f), new Vector3(0, 0.17f, -0.01f), null, new Vector3(1.0f, 0.62f, 1.05f));
                VisualKit.Part(root, VisualKit.Cylinder(0.14f, 0.14f, 0.03f, 12), VisualKit.Mat(new Color(0.32f, 0.2f, 0.1f)), new Vector3(0, 0.13f, 0));
                break;
            case "head_iron":
            case "head_guardian":
            {
                // elmo fechado (balde) com viseira: Ferro de Vigília; guardião = escuro com crista e viseira acesa
                var guardian = model == "head_guardian";
                var metal = VisualKit.Mat(guardian ? new Color(0.26f, 0.27f, 0.30f) : new Color(0.42f, 0.45f, 0.50f), 0.35f, 0.85f);
                var dark = VisualKit.Mat(new Color(0.08f, 0.08f, 0.09f), 0.6f);
                var trim = VisualKit.Mat(new Color(0.66f, 0.48f, 0.26f), 0.4f, 0.7f);
                VisualKit.Part(root, VisualKit.Cylinder(0.148f, 0.158f, 0.30f, 10), metal, new Vector3(0, 0.14f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.11f, 0.15f, 0.07f, 10), metal, new Vector3(0, 0.32f, 0));
                VisualKit.Part(root, VisualKit.Box(0.22f, 0.028f, 0.03f), guardian ? VisualKit.Glow(new Color(0.4f, 0.85f, 1f), 2.2f) : dark, new Vector3(0, 0.17f, 0.148f));
                VisualKit.Part(root, VisualKit.Box(0.028f, 0.16f, 0.03f), guardian ? trim : dark, new Vector3(0, 0.08f, 0.15f));
                VisualKit.Part(root, VisualKit.Box(0.03f, 0.05f, 0.30f), trim, new Vector3(0, 0.36f, 0));
                if (guardian)
                    foreach (var side in new[] { -1f, 1f })
                        VisualKit.Part(root, VisualKit.Box(0.03f, 0.16f, 0.12f), trim, new Vector3(side * 0.16f, 0.26f, -0.02f), new Vector3(0, 0, side * -25));
                break;
            }
            case "head_barbuta":
            case "head_horned":
            case "head_skull":
            case "head_spiked":
            case "head_demon":
            case "head_warlord":
                ElmoAmeacador(root, model);
                break;
            case "head_hood":
            {
                // capuz levantado como na referência do usuário: casca de tecido envolvendo alto, lados e nuca, abertura
                // larga na frente (rosto e barba livres) com debrum dourado; ponta leve para trás; embaixo emenda na capa
                var cloth = VisualKit.Mat(new Color(0.27f, 0.16f, 0.31f), 0.95f);
                cloth.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                VisualKit.Part(root, HoodShell(out var ring), cloth, Vector3.Zero);
                var trim = VisualKit.Mat(new Color(0.72f, 0.62f, 0.34f), 0.55f, 0.45f);
                for (var i = 0; i + 1 < ring.Count; i++)
                {
                    var p0 = ring[i]; var p1 = ring[i + 1];
                    var seg = VisualKit.Part(root, VisualKit.Box(0.03f, 0.012f, p0.DistanceTo(p1) + 0.006f), trim, (p0 + p1) / 2f);
                    seg.Basis = Basis.LookingAt((p1 - p0).Normalized(), Vector3.Forward);
                }
                break;
            }
            case "head_plume":
            {
                // elmo aberto polido com penacho vermelho
                var metal = VisualKit.Mat(new Color(0.75f, 0.72f, 0.62f), 0.25f, 0.9f);
                VisualKit.Part(root, VisualKit.Sphere(0.16f), metal, new Vector3(0, 0.16f, -0.01f), null, new Vector3(1.0f, 0.85f, 1.05f));
                VisualKit.Part(root, VisualKit.Cylinder(0.165f, 0.165f, 0.04f, 14), VisualKit.Mat(new Color(0.8f, 0.62f, 0.3f), 0.3f, 0.9f), new Vector3(0, 0.1f, 0));
                VisualKit.Part(root, VisualKit.Box(0.03f, 0.12f, 0.04f), metal, new Vector3(0, 0.07f, 0.16f));
                for (var i = 0; i < 6; i++)
                    VisualKit.Part(root, VisualKit.Box(0.05f, 0.05f, 0.09f), VisualKit.Mat(new Color(0.75f, 0.1f, 0.1f), 0.9f),
                        new Vector3(0, 0.32f + Mathf.Sin(i * 0.5f) * 0.04f, 0.1f - i * 0.06f), new Vector3(-20 + i * 10, 0, 0));
                break;
            }
            case "head_hood_travel":
            {
                var cloth = VisualKit.Mat(new Color(0.40f, 0.28f, 0.18f), 0.95f);
                VisualKit.Part(root, VisualKit.Sphere(0.17f), cloth, new Vector3(0, 0.15f, -0.06f), null, new Vector3(1.0f, 1.1f, 1.0f));
                VisualKit.Part(root, VisualKit.Sphere(0.11f), VisualKit.Mat(new Color(0.04f, 0.04f, 0.04f), 1f), new Vector3(0, 0.13f, 0.08f), null, new Vector3(0.95f, 1.25f, 0.75f));
                VisualKit.Part(root, VisualKit.Cylinder(0.17f, 0.26f, 0.12f, 10), VisualKit.Mat(new Color(0.55f, 0.15f, 0.12f), 0.9f), new Vector3(0, -0.06f, -0.01f));
                VisualKit.Part(root, VisualKit.Cylinder(0f, 0.08f, 0.22f, 6), cloth, new Vector3(0, 0.26f, -0.14f), new Vector3(-60, 0, 0));
                break;
            }
            default:
                return null;
        }
        return root;
    }

    /// <summary>
    /// 15/11 (pendência "cabeças de maça variadas"): três cabeças feitas por código — bola com cravos curtos (Cravejada),
    /// bola grande com espinhos longos e anel de reforço (Estrela-da-Manhã) e seis aletas de aço em volta do miolo (Flanges).
    /// Cabo de madeira com empunhadura de couro; mesma convenção das armas: empunhadura na origem, cabeça para +Y.
    /// </summary>
    private static void CabecaDeMaca(Node3D root, string model)
    {
        var aco = VisualKit.Mat(new Color(0.5f, 0.52f, 0.56f), 0.3f, 0.9f);
        var escuro = VisualKit.Mat(new Color(0.24f, 0.24f, 0.27f), 0.45f, 0.85f);
        var latao = VisualKit.Mat(new Color(0.7f, 0.52f, 0.25f), 0.35f, 0.8f);
        Grip(root, 0.62f, Wood, 0.024f, 0.22f);
        VisualKit.Part(root, VisualKit.Cylinder(0.04f, 0.035f, 0.05f, 10), latao, new Vector3(0, -0.04f, 0));   // pomo
        const float y = 0.66f;
        switch (model)
        {
            case "mace_spiked":
                VisualKit.Part(root, VisualKit.Sphere(0.085f), escuro, new Vector3(0, y, 0));
                for (var i = 0; i < 14; i++)
                {
                    var d = Fibonacci(i, 14);
                    var n = VisualKit.Part(root, VisualKit.Cylinder(0f, 0.022f, 0.06f, 5), aco, new Vector3(0, y, 0) + d * 0.1f);
                    n.Basis = Basis.LookingAt(d, Mathf.Abs(d.Y) > 0.9f ? Vector3.Forward : Vector3.Up) * Basis.FromEuler(new Vector3(-Mathf.Pi / 2, 0, 0));
                }
                break;
            case "mace_morningstar":
                VisualKit.Part(root, VisualKit.Sphere(0.11f), escuro, new Vector3(0, y + 0.03f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.115f, 0.115f, 0.03f, 14), latao, new Vector3(0, y + 0.03f, 0));
                for (var i = 0; i < 20; i++)
                {
                    var d = Fibonacci(i, 20);
                    var n = VisualKit.Part(root, VisualKit.Cylinder(0f, 0.024f, 0.11f, 5), aco, new Vector3(0, y + 0.03f, 0) + d * 0.15f);
                    n.Basis = Basis.LookingAt(d, Mathf.Abs(d.Y) > 0.9f ? Vector3.Forward : Vector3.Up) * Basis.FromEuler(new Vector3(-Mathf.Pi / 2, 0, 0));
                }
                break;
            default:   // mace_flanged
                VisualKit.Part(root, VisualKit.Cylinder(0.04f, 0.05f, 0.2f, 8), escuro, new Vector3(0, y, 0));
                for (var i = 0; i < 6; i++)
                {
                    var a = i / 6f * Mathf.Tau;
                    var aleta = VisualKit.Part(root, VisualKit.Box(0.012f, 0.2f, 0.09f), aco, new Vector3(Mathf.Sin(a) * 0.075f, y, Mathf.Cos(a) * 0.075f));
                    aleta.Rotation = new Vector3(0, a, 0);
                }
                VisualKit.Part(root, VisualKit.Cylinder(0f, 0.045f, 0.08f, 8), aco, new Vector3(0, y + 0.14f, 0));
                VisualKit.Part(root, VisualKit.Cylinder(0.06f, 0.06f, 0.025f, 10), latao, new Vector3(0, y - 0.11f, 0));
                break;
        }
    }

    /// <summary>Direção i de n espalhadas por igual numa esfera (espiral de Fibonacci).</summary>
    private static Vector3 Fibonacci(int i, int n)
    {
        var yy = 1f - 2f * (i + 0.5f) / n;
        var r = Mathf.Sqrt(1f - yy * yy);
        var t = i * 2.39996f;
        return new Vector3(Mathf.Cos(t) * r, yy, Mathf.Sin(t) * r);
    }

    /// <summary>
    /// 15/11 (pendência "elmos mais ameaçadores"): um elmo por região do midgame — barbuta de fenda em T (Cânions), chifres de
    /// carneiro (Brejo), caveira com mandíbula (Véspera), coroa de pontas de gelo (Geada), demônio de chifres curvos e olhos em
    /// brasa (Escória) e o senhor da guerra com asas e crista (Acrópole). Mesmo casco-base do elmo fechado.
    /// </summary>
    private static void ElmoAmeacador(Node3D root, string model)
    {
        var (cor, rough, metal) = model switch
        {
            "head_barbuta" => (new Color(0.48f, 0.47f, 0.45f), 0.4f, 0.85f),
            "head_horned" => (new Color(0.30f, 0.27f, 0.22f), 0.55f, 0.7f),
            "head_skull" => (new Color(0.78f, 0.74f, 0.64f), 0.7f, 0.15f),
            "head_spiked" => (new Color(0.55f, 0.66f, 0.76f), 0.25f, 0.9f),
            "head_demon" => (new Color(0.16f, 0.10f, 0.10f), 0.45f, 0.8f),
            _ => (new Color(0.20f, 0.20f, 0.23f), 0.3f, 0.9f),
        };
        var m = VisualKit.Mat(cor, rough, metal);
        var escuro = VisualKit.Mat(new Color(0.05f, 0.05f, 0.06f), 0.7f);
        var ouro = VisualKit.Mat(new Color(0.72f, 0.54f, 0.24f), 0.35f, 0.85f);
        // casco fechado, um pouco mais alto e com bochechas descendo (mais agressivo que o balde)
        VisualKit.Part(root, VisualKit.Cylinder(0.15f, 0.165f, 0.31f, 12), m, new Vector3(0, 0.14f, 0));
        VisualKit.Part(root, VisualKit.Sphere(0.152f), m, new Vector3(0, 0.29f, 0), null, new Vector3(1f, 0.55f, 1.05f));
        foreach (var lado in new[] { -1f, 1f })
            VisualKit.Part(root, VisualKit.Box(0.05f, 0.16f, 0.12f), m, new Vector3(lado * 0.12f, 0.02f, 0.1f), new Vector3(0, lado * 25, 0));
        Color brilho = model == "head_demon" ? new Color(1f, 0.3f, 0.05f) : model == "head_spiked" ? new Color(0.5f, 0.9f, 1f) : model == "head_warlord" ? new Color(1f, 0.75f, 0.3f) : new Color(0.9f, 0.15f, 0.1f);
        switch (model)
        {
            case "head_barbuta":
                // fenda em T: um corte horizontal nos olhos e um vertical até o queixo
                VisualKit.Part(root, VisualKit.Box(0.2f, 0.03f, 0.03f), escuro, new Vector3(0, 0.18f, 0.155f));
                VisualKit.Part(root, VisualKit.Box(0.035f, 0.17f, 0.03f), escuro, new Vector3(0, 0.08f, 0.158f));
                VisualKit.Part(root, VisualKit.Box(0.025f, 0.06f, 0.26f), m, new Vector3(0, 0.37f, -0.01f));
                break;
            case "head_horned":
                VisualKit.Part(root, VisualKit.Box(0.22f, 0.025f, 0.03f), escuro, new Vector3(0, 0.18f, 0.155f));
                foreach (var lado in new[] { -1f, 1f })
                    for (var i = 0; i < 6; i++)   // chifre de carneiro: espiral para trás e para baixo
                    {
                        var a = i * 0.55f;
                        VisualKit.Part(root, VisualKit.Sphere(0.05f - i * 0.004f), VisualKit.Mat(new Color(0.62f, 0.55f, 0.42f), 0.8f),
                            new Vector3(lado * (0.17f + 0.05f * Mathf.Sin(a)), 0.3f + 0.08f * Mathf.Cos(a), -0.02f - 0.07f * Mathf.Sin(a)));
                    }
                break;
            case "head_skull":
                foreach (var lado in new[] { -1f, 1f })
                {
                    VisualKit.Part(root, VisualKit.Sphere(0.04f), escuro, new Vector3(lado * 0.055f, 0.2f, 0.135f));
                    VisualKit.Part(root, VisualKit.Sphere(0.012f), VisualKit.Glow(brilho, 2.5f), new Vector3(lado * 0.055f, 0.2f, 0.165f));
                }
                VisualKit.Part(root, VisualKit.Box(0.03f, 0.05f, 0.03f), escuro, new Vector3(0, 0.13f, 0.158f), new Vector3(0, 0, 45));
                for (var i = -3; i <= 3; i++)   // dentes da mandíbula
                    VisualKit.Part(root, VisualKit.Box(0.022f, 0.04f, 0.02f), VisualKit.Mat(new Color(0.88f, 0.85f, 0.76f), 0.6f), new Vector3(i * 0.027f, 0.05f, 0.16f));
                break;
            case "head_spiked":
                VisualKit.Part(root, VisualKit.Box(0.22f, 0.025f, 0.03f), VisualKit.Glow(brilho, 1.8f), new Vector3(0, 0.18f, 0.155f));
                for (var i = 0; i < 9; i++)   // coroa de pontas de gelo
                {
                    var a = i / 9f * Mathf.Tau;
                    VisualKit.Part(root, VisualKit.Cylinder(0f, 0.025f, 0.13f + (i % 2) * 0.06f, 5), VisualKit.Mat(new Color(0.7f, 0.88f, 1f), 0.1f, 0.3f),
                        new Vector3(Mathf.Sin(a) * 0.13f, 0.38f + (i % 2) * 0.03f, Mathf.Cos(a) * 0.13f), new Vector3(Mathf.RadToDeg(Mathf.Cos(a)) * 0.35f, 0, -Mathf.RadToDeg(Mathf.Sin(a)) * 0.35f));
                }
                break;
            case "head_demon":
                foreach (var lado in new[] { -1f, 1f })
                {
                    VisualKit.Part(root, VisualKit.Box(0.07f, 0.022f, 0.03f), VisualKit.Glow(brilho, 3f), new Vector3(lado * 0.05f, 0.19f, 0.155f), new Vector3(0, 0, lado * -14));
                    for (var i = 0; i < 6; i++)   // chifre curvo para cima e para fora
                        VisualKit.Part(root, VisualKit.Cylinder(0.058f - i * 0.009f, 0.066f - i * 0.009f, 0.08f, 8), VisualKit.Mat(new Color(0.12f, 0.1f, 0.09f), 0.5f, 0.4f),
                            new Vector3(lado * (0.15f + i * 0.035f), 0.33f + i * 0.045f - i * i * 0.004f, -0.01f), new Vector3(0, 0, lado * (-55 + i * 12)));
                }
                VisualKit.Part(root, VisualKit.Box(0.03f, 0.12f, 0.03f), escuro, new Vector3(0, 0.08f, 0.158f));
                break;
            default:   // head_warlord
                VisualKit.Part(root, VisualKit.Box(0.22f, 0.025f, 0.03f), VisualKit.Glow(brilho, 1.6f), new Vector3(0, 0.18f, 0.155f));
                VisualKit.Part(root, VisualKit.Box(0.03f, 0.14f, 0.34f), ouro, new Vector3(0, 0.42f, -0.02f));   // crista
                foreach (var lado in new[] { -1f, 1f })
                    for (var i = 0; i < 4; i++)   // asas de metal nas laterais
                        VisualKit.Part(root, VisualKit.Box(0.02f, 0.05f, 0.16f - i * 0.025f), ouro,
                            new Vector3(lado * 0.17f, 0.26f + i * 0.045f, -0.06f - i * 0.02f), new Vector3(-25 - i * 8, lado * 15, 0));
                break;
        }
    }

    /// <summary>Casca do capuz (unidades do elmo: cabeça com centro ~(0; 0,14; 0) e raio ~0,14). <paramref name="ring"/> = borda da abertura.</summary>
    private static ArrayMesh HoodShell(out List<Vector3> ring)
    {
        var c = new Vector3(0, 0.155f, -0.035f);
        Vector3 P(Vector3 d)
        {
            // redondo (feedback: "formato redondo como na foto, sem ovalar"): raios iguais, só uma pontinha curta atrás
            var peak = 1f + 0.08f * Mathf.Max(0, d.Y) * Mathf.Max(0, -d.Z);
            return c + new Vector3(d.X * 0.2f, d.Y * 0.2f, d.Z * 0.205f) * peak;
        }
        const float open = 0.42f;
        bool Open(Vector3 d) => d.Z > open || (d.Y < -0.5f && d.Z > -0.15f) || d.Y < -0.82f;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int lat = 26, lon = 40;
        Vector3 Dir(int i, int j)
        {
            var th = Mathf.Pi * i / lat; var ph = Mathf.Tau * j / lon;
            return new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
        }
        for (var i = 0; i < lat; i++)
            for (var j = 0; j < lon; j++)
            {
                var d00 = Dir(i, j); var d01 = Dir(i, j + 1); var d10 = Dir(i + 1, j); var d11 = Dir(i + 1, j + 1);
                if (Open((d00 + d01 + d10 + d11).Normalized())) continue;
                foreach (var d in new[] { d00, d01, d10, d01, d11, d10 }) { st.SetNormal(d); st.AddVertex(P(d)); }
            }
        // borda: anel z = open, só da altura do queixo para cima
        ring = new List<Vector3>();
        var rr = Mathf.Sqrt(1 - open * open);
        var phi0 = Mathf.Asin(-0.5f / rr);
        for (var k = 0; k <= 40; k++)
        {
            var phi = Mathf.Lerp(phi0, Mathf.Pi - phi0, k / 40f);   // de baixo-direita, por cima, até baixo-esquerda
            var d = new Vector3(rr * Mathf.Cos(phi), rr * Mathf.Sin(phi), open);
            ring.Add(P(d) + new Vector3(0, 0, 0.004f));
        }
        var mesh = new ArrayMesh();
        st.Commit(mesh);
        return mesh;
    }

    public static Node3D? Shield(string? model)
    {
        if (model is not null && model.StartsWith("shield_") && LotTwo(model, "Escudo_" + model) is { } sculpted) return sculpted;
        return model switch
        {
            // escudo pipa (prancha 07): campo bordô com tridente de bronze e aro de ferro
            "shield_planks" => Kite(new Color(0.39f, 0.23f, 0.28f), new Color(0.22f, 0.23f, 0.26f), Brass, null),
            // pedra-cal / tiers do midgame: aço claro com veio azul
            "shield_stone" => Kite(new Color(0.55f, 0.57f, 0.60f), new Color(0.22f, 0.23f, 0.26f), Brass, new Color(0.4f, 0.85f, 1f)),
            _ => null,
        };
    }

    private static Node3D Kite(Color field, Color rim, Color emblem, Color? glow)
    {
        var root = new Node3D { Name = "Escudo" };
        var fieldM = VisualKit.Mat(field, glow is null ? 0.85f : 0.4f, glow is null ? 0f : 0.6f);
        var rimM = VisualKit.Mat(rim, 0.45f, 0.7f);
        // parte de cima (retângulo) + losango embaixo = pipa; o aro é a mesma forma maior, atrás
        VisualKit.Part(root, VisualKit.Box(0.60f, 0.40f, 0.06f), rimM, new Vector3(0, 0.14f, -0.012f));
        VisualKit.Part(root, VisualKit.Box(0.47f, 0.47f, 0.06f), rimM, new Vector3(0, -0.08f, -0.012f), new Vector3(0, 0, 45), new Vector3(0.93f, 1f, 1f));
        VisualKit.Part(root, VisualKit.Box(0.54f, 0.36f, 0.06f), fieldM, new Vector3(0, 0.14f, 0));
        VisualKit.Part(root, VisualKit.Box(0.42f, 0.42f, 0.06f), fieldM, new Vector3(0, -0.08f, 0), new Vector3(0, 0, 45), new Vector3(0.93f, 1f, 1f));
        var e = VisualKit.Mat(emblem, 0.4f, 0.7f);
        VisualKit.Part(root, VisualKit.Box(0.035f, 0.42f, 0.02f), e, new Vector3(0, -0.02f, 0.035f));
        VisualKit.Part(root, VisualKit.Box(0.22f, 0.03f, 0.02f), e, new Vector3(0, 0.14f, 0.035f));
        foreach (var dx in new[] { -0.1f, 0f, 0.1f })
            VisualKit.Part(root, VisualKit.Cylinder(0f, 0.03f, 0.12f, 4), e, new Vector3(dx, 0.23f, 0.035f));
        if (glow is { } g) VisualKit.Part(root, VisualKit.Box(0.02f, 0.30f, 0.02f), VisualKit.Glow(g, 1.6f), new Vector3(0.12f, 0.02f, 0.036f), new Vector3(0, 0, 10));
        return root;
    }

    /// <summary>Item no chão (girando). Equipamentos usam o próprio modelo; poções, um frasco.</summary>
    /// <summary>Itens de chão esculpidos do lote 2 (Assets/Models/itens), escolhidos pelo nome do item.</summary>
    /// <summary>Ícone pintado pelo artista (Art/ico_*.png, atualização de 10/11) para o item; null = fotografa o modelo 3D.</summary>
    /// <summary>14/11 noite: skins com o desenho recortado da prancha do dono.</summary>
    public static bool ArteDaPrancha(string id) => id is "bracelete_macaco_vermelho" or "bracelete_caveira" or "bracelete_alien" or "bracelete_tubarao" or "bracelete_fantasma" or "bracelete_lava";

    public static string? PaintedIcon(string? model, string definitionId)
    {
        definitionId = ItemIconRenderer.BaseDaVariante(definitionId);   // 17/11 noite: variantes usam a arte da base
        // 10/11: companheiros como item (sem arte própria ainda): vagalume = essência; dragão e fênix = joia de brasa
        if (definitionId.StartsWith("wings_") || definitionId.StartsWith("asas_")) return "ico_" + definitionId + ".png";   // 11/11: asas como item (14/11: + as 25 da prancha)
        if (ArteDaPrancha(definitionId)) return "ico_" + definitionId + ".png";   // 14/11 noite: skins com o desenho da prancha do dono
        if (model?.StartsWith("arm_") == true) return "ico_" + model.Replace(':', '_') + ".png";   // 11/11: peças de conjunto
        if (model?.StartsWith("armadura_") == true) return "ico_" + definitionId + ".png";          // 11/11: lendárias do Meshy
        if (definitionId.StartsWith("anel_") || definitionId.StartsWith("colar_") || definitionId.StartsWith("capa_")) return "ico_" + definitionId + ".png";
        // 13/11 ("a joia da brasa está com o mesmo desenho dos pets"): pet ganha a foto do próprio bicho (ItemIconRenderer)
        if (definitionId == "pet_vagalume") return "ico_essencia.png";
        if (model is not null && LotTwoWeapon(model) is { } w) return WeaponIcon(w);
        if (model is not null && MannequinVisual.ArsenalFile(model) is { } bt) return WeaponIcon(bt);
        if (LotTwoDrop(model, definitionId) is { } d) return "ico_" + d["item_".Length..] + ".png";
        return null;
    }

    internal static string? LotTwoDrop(string? model, string id)
    {
        // equipamento (arma, armadura, elmo, escudo) nunca usa os itens de chão
        if (id.StartsWith("weapon_") || id.StartsWith("armor_") || id.StartsWith("helmet_") || id.StartsWith("head_") || id.StartsWith("shield_")
            || id.StartsWith("offhand_") || id.StartsWith("body_") || (model is not null && (model.StartsWith("body_") || model.StartsWith("head_")
            || model.StartsWith("shield_") || GearFactory.IsBow(model) || model.StartsWith("staff_") || model.StartsWith("talisman_")
            || model.StartsWith("sword_") || model.StartsWith("axe_")))) return null;
        if (id.StartsWith("consumable_c08") || id.StartsWith("consumable_c09")) return null;   // 14/11: frasco de Foco usa o desenho próprio (laranja)
        // 16/11 (pendência p101_icones "ícones errados"): o nome contém a palavra mas não é o material — relíquia de forma
        // (colar da presa, foice de osso) virava presa/osso; chave de ferro virava minério; arma lendária virava presa
        if (id.StartsWith("reliquia_") || id.StartsWith("tomo_") || id.StartsWith("staff_") || id.StartsWith("lendaria_") || id.StartsWith("mat_chave_")) return null;
        if (id.StartsWith("consumable_c03") || id.StartsWith("consumable_c04")) return "item_pocao_mana";
        if (id.StartsWith("consumable_c0")) return "item_pocao_vida";
        if (id.Contains("joia") || id.Contains("gema"))
            return id.Contains("brasa") || id.Contains("fogo") ? "item_joia_brasa" : id.Contains("vazio") || id.Contains("sombra") ? "item_joia_vazio" : "item_joia_mare";
        if (id.Contains("pinca")) return "item_pinca_caranguejo";
        if (id.Contains("casca") || id.Contains("carapaca") || id.Contains("quitina") || model == "mat_shell") return "item_carapaca";
        if (id.Contains("presa") || id.Contains("ferrao") || id.Contains("dente")) return "item_presa";
        if (id.Contains("osso")) return "item_osso";
        if (id.Contains("erva") || id.Contains("folha") || id.Contains("flor") || id.Contains("raiz") || id.Contains("cogumelo")) return "item_erva";
        if (id.Contains("ferro") || id.Contains("bronze") || id.Contains("basalto") || id.Contains("liga") || id.Contains("minerio") || model == "mat_vein") return "item_minerio";
        if (id.Contains("seda") || id.Contains("couro") || id.Contains("membrana") || id.Contains("retalho") || model == "mat_cloth") return "item_seda";
        if (id.Contains("essencia") || id.Contains("cinza") || id.Contains("brasa") || id.Contains("nucleo") || model == "mat_ash") return "item_essencia";
        if (id.Contains("moeda") || id.Contains("ficha")) return "item_moedas";
        if (id.Contains("bolsa") || id.Contains("carta") || id.Contains("insignia")) return "item_bolsa";
        return null;
    }

    public static Node3D GroundItem(string? model, string definitionId)
    {
        var root = new Node3D();

        Node3D? shape = Weapon(model) ?? Shield(model) ?? Helmet(model);
        if (shape is not null)
        {
            shape.RotationDegrees = new Vector3(0, 0, 70);
            shape.Scale = Vector3.One * 0.75f;
            shape.Position = new Vector3(0.25f, 0.3f, 0);
            root.AddChild(shape);
            return root;
        }
        // 13/11 ("muda o tônico também quando cai no chão"): consumível com desenho próprio (tubo do tônico fraco, frascos
        // redondos etc.) cai no chão como ele mesmo, em pé e um pouco maior — o mesmo da mochila.
        // 13/11 (2ª vez: "trocar o 3D da poção fraca quando dropa"): vem ANTES do modelo 3D de lote, que mostrava o frasco grande
        // 14/11: vale para tudo que tem desenho próprio (poções, mapa do tesouro, esferas...), cada um no seu tamanho
        if (ItemIconRenderer.HasOwnIcon(definitionId) && !definitionId.StartsWith("bracelete_") && ItemIconRenderer.OwnIconNode(definitionId) is { } proprio)
        {
            var t = ItemIconRenderer.TamanhoNoChao(definitionId);
            proprio.Scale = Vector3.One * t;
            proprio.Position = new Vector3(0, 0.4f * t, 0);
            root.AddChild(proprio);
            return root;
        }
        // 10/11 ("os itens 3D novos não estão sendo usados? usa!"): a versão _v2 (complemento) tem preferência
        if (LotTwoDrop(model, definitionId) is { } drop && (GlbCache.Instance("itens/" + drop + "_v2") is { } v2 ? (v2, true) : (GlbCache.Instance("itens/" + drop), false)) is ({ } glb, var isV2))
        {
            glb.Scale = Vector3.One * (isV2 ? 2.6f : 1.3f);   // v2 vem em tamanho real (0,1–0,36 m): a ficha pede 2–3× no chão
            root.AddChild(glb);
            return root;
        }
        // 13/11 ("os itens quando dropam têm uma caixa no chão, e não o próprio item igual MU"): peça de conjunto 3D
        // ("arquivo:peça") aparece no chão como ela mesma — a malha da peça, deitada, girando devagar com brilho
        if (model is not null && model.Contains(':') && ArmorPieceOnGround(model) is { } peca)
        {
            root.AddChild(peca);
            return root;
        }
        if ((definitionId.StartsWith("caixa_cinzas_") || definitionId.StartsWith("caixa_pandora_")) && ItemIconRenderer.OwnIconNode(definitionId) is { } caixa)
        {
            caixa.Scale = Vector3.One * 0.7f;
            caixa.Position = new Vector3(0, 0.2f, 0);
            root.AddChild(caixa);
            return root;
        }
        if (definitionId.StartsWith("consumable_"))
        {
            VisualKit.Part(root, VisualKit.Sphere(0.14f), VisualKit.Glow(new Color(0.85f, 0.15f, 0.2f), 1.2f), new Vector3(0, 0.25f, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.04f, 0.05f, 0.14f), VisualKit.Mat(new Color(0.7f, 0.8f, 0.85f), 0.2f), new Vector3(0, 0.44f, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.05f, 0.05f, 0.04f), VisualKit.Mat(Leather), new Vector3(0, 0.53f, 0));
            return root;
        }
        if (model is not null && model.StartsWith("mat_"))
        {
            var (c, glow) = model switch
            {
                "mat_shell" => (new Color(0.9f, 0.88f, 0.8f), false),
                "mat_cloth" => (new Color(0.15f, 0.12f, 0.12f), false),
                "mat_ash" => (new Color(1f, 0.5f, 0.15f), true),
                _ => (new Color(0.4f, 0.85f, 1f), true),
            };
            VisualKit.Part(root, VisualKit.Sphere(0.12f), glow ? VisualKit.Glow(c, 1.5f) : VisualKit.Mat(c, 0.8f), new Vector3(0, 0.15f, 0), new Vector3(20, 30, 0), new Vector3(1.3f, 0.6f, 1f));
            return root;
        }
        // armadura: tronco de manequim com a peça
        var color = model switch
        {
            "body_mail" or "body_plate" => Steel,
            "body_robe" => new Color(0.32f, 0.22f, 0.45f),
            "body_robe_vein" => new Color(0.15f, 0.30f, 0.34f),
            "body_rags" => new Color(0.62f, 0.55f, 0.42f),
            _ => new Color(0.55f, 0.42f, 0.28f),
        };
        VisualKit.Part(root, VisualKit.Box(0.42f, 0.5f, 0.24f), VisualKit.Mat(color, model == "body_mail" ? 0.35f : 0.9f, model == "body_mail" ? 0.7f : 0f),
            new Vector3(0, 0.3f, 0));
        VisualKit.Part(root, VisualKit.Box(0.5f, 0.08f, 0.28f), VisualKit.Mat(Leather), new Vector3(0, 0.08f, 0));
        return root;
    }

    /// <summary>13/11: uma peça do pacote de armadura (malha com pele) vira malha solta, centrada e deitada no chão.</summary>
    private static Node3D? ArmorPieceOnGround(string model)
    {
        var cut = model.IndexOf(':');
        var file = model[..cut];
        var piece = model[(cut + 1)..].Split('+')[0];
        if (GlbCache.Instance("armaduras/" + file) is not { } pack) return null;
        try
        {
            if ((pack.FindChild(piece, true, false) ?? pack.FindChild(file + "_" + piece, true, false)) is not MeshInstance3D src || src.Mesh is null) return null;
            var mi = new MeshInstance3D { Mesh = src.Mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            for (var s = 0; s < src.Mesh.GetSurfaceCount(); s++)
                if (src.GetSurfaceOverrideMaterial(s) is { } m) mi.SetSurfaceOverrideMaterial(s, m);
            var box = src.Mesh.GetAabb();
            var big = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z));
            var scale = big > 0.01f ? Mathf.Clamp(0.55f / big, 0.4f, 1.6f) : 1f;   // ~55 cm no chão, como no MU
            var holder = new Node3D { Scale = Vector3.One * scale, RotationDegrees = new Vector3(piece is "calca" or "peitoral" ? -70 : 0, 30, 0) };
            mi.Position = -box.GetCenter();
            holder.AddChild(mi);
            var lift = new Node3D { Position = new Vector3(0, 0.3f, 0) };
            lift.AddChild(holder);
            return lift;
        }
        finally { pack.QueueFree(); }
    }

    private static void Grip(Node3D root, float length, Color? color = null, float radius = 0.028f, float below = 0.1f)
    {
        VisualKit.Part(root, VisualKit.Cylinder(radius, radius, length + below), VisualKit.Mat(color ?? Leather, 0.9f),
            new Vector3(0, (length - below) / 2f, 0));
    }

    private static void Staff(Node3D root, Color wood, Color gem, float gemRadius, float energy)
    {
        Grip(root, 1.15f, wood, 0.027f, 0.55f);
        VisualKit.Part(root, VisualKit.Cylinder(0.05f, 0.03f, 0.1f), VisualKit.Mat(wood.Darkened(0.2f)), new Vector3(0, 1.12f, 0));
        // cristal lapidado (dois cones) preso por três garras, como na prancha 07
        var y = 1.22f + gemRadius;
        var g = VisualKit.Glow(gem, Mathf.Min(energy, 1.8f));
        VisualKit.Part(root, VisualKit.Cylinder(0f, gemRadius, gemRadius * 1.6f, 5), g, new Vector3(0, y + gemRadius * 0.8f, 0));
        VisualKit.Part(root, VisualKit.Cylinder(gemRadius, 0f, gemRadius * 1.0f, 5), g, new Vector3(0, y - gemRadius * 0.5f, 0));
        for (var i = 0; i < 3; i++)
            VisualKit.Part(root, VisualKit.Cylinder(0.006f, 0.014f, gemRadius * 2.2f, 4), VisualKit.Mat(Brass, 0.4f, 0.7f),
                new Vector3(Mathf.Cos(i * 2.094f) * gemRadius * 0.8f, y, Mathf.Sin(i * 2.094f) * gemRadius * 0.8f), new Vector3(Mathf.Sin(i * 2.094f) * 18f, 0, -Mathf.Cos(i * 2.094f) * 18f));
    }
}
