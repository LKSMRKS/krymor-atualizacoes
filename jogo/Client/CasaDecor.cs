using System.Collections.Generic;
using CinzasDaMare.Client.Visuals;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 12/11 (casa do jogador: "decoração, jukebox"): peças compradas nas lojas do Monte do Prefeito e colocadas em casa
/// (marca "decor:id") aparecem no lugar fixo de cada uma. A jukebox toca a música calma do Porto, só de perto.
/// </summary>
public static class CasaDecor
{
    private static readonly Dictionary<string, (float X, float Y, float Yaw)> Lugar = new()
    {
        ["decor_tapete_real"] = (2.5f, 4.5f, 90f),
        ["decor_quadro_mar"] = (-9.4f, 5.5f, 90f),
        ["decor_lustre"] = (0f, -3.5f, 0f),
        ["decor_aquario"] = (8.6f, -1.8f, -90f),
        ["decor_estatua_leao"] = (-8.2f, -8.2f, 45f),
        ["decor_jukebox"] = (-5f, 8.8f, 180f),
        ["decor_trofeu"] = (9.4f, -3.5f, -90f),
        ["decor_trono"] = (-2.6f, -8.3f, 0f),
        // 13/11 (análise — vila: "estante de troféus e mais decoração")
        ["decor_estante_trofeus"] = (9.3f, 3.5f, -90f),
        ["decor_lareira"] = (3.5f, 9.2f, 180f),
        ["decor_planta"] = (-9.0f, -1.5f, 90f),
        ["decor_bandeira"] = (4.5f, -9.3f, 0f),
        ["decor_sofa"] = (-6.5f, 6.5f, 180f),   // 18/11: sofá (dá para sentar: conforto ×1,5)
        // 18/11 15h: decoração de FORA (terreno da casa) — sempre no lugar escolhido pelo dono
        ["decor_terreno_boneco"] = (4f, 4f, 0f),
        ["decor_terreno_portal"] = (-4f, 4f, 0f),
        ["decor_terreno_mascate"] = (4f, -4f, 0f),
        ["decor_terreno_jukebox"] = (-4f, -4f, 0f),
        ["decor_terreno_cercado"] = (0f, 8f, 0f),
    };

    /// <summary>18/11 16h: linhas de decoração e de quintal das cidades (Vila das Mós, Astrália, Candeia, Brasaforte).</summary>
    private static readonly HashSet<string> Linhas = new()
    {
        "decor_rustico_tapete", "decor_rustico_estante", "decor_rustico_lampiao", "decor_terreno_poco", "decor_terreno_espantalho", "decor_terreno_portal_vila_das_mos",
        "decor_astro_globo", "decor_astro_telescopio", "decor_astro_luminaria", "decor_terreno_fonte", "decor_terreno_relogio_sol", "decor_terreno_portal_astralia",
        "decor_candeia_vitral", "decor_candeia_candelabro", "decor_candeia_bau", "decor_terreno_lanternas", "decor_terreno_caldeirao", "decor_terreno_portal_candeia",
        "decor_forja_armadura", "decor_forja_trofeu", "decor_forja_braseiro", "decor_terreno_forja", "decor_terreno_estatua", "decor_terreno_portal_brasaforte",
    };

    public static bool Conhece(string id) => Lugar.ContainsKey(id) || Linhas.Contains(id);

    /// <summary>
    /// 18/11 (dono: "cada objeto tem seu lugar, ou parede, ou chão, ou sobre algo"): onde a peça pode ir.
    /// parede = cola na parede mais perto, de frente para o cômodo; teto = pendurada (anda livre); sobre = em cima de
    /// mesa/prateleira/estante se tiver uma perto (senão no chão); chão = livre (a rodinha gira).
    /// </summary>
    public static string Categoria(string id) => id switch
    {
        "decor_quadro_mar" or "decor_bandeira" or "decor_trofeu" or "decor_candeia_vitral" or "decor_forja_trofeu" => "parede",
        "decor_lustre" or "decor_astro_luminaria" => "teto",
        "decor_aquario" or "decor_planta" or "decor_rustico_lampiao" => "sobre",
        _ => "chão",
    };

    /// <summary>Lugar salvo pelo jogador (marca "decorpos:id=mapa|x|y|giro|altura") ou null.</summary>
    public static (float X, float Y, float Yaw, float Altura)? Salvo(System.Collections.Generic.IEnumerable<string> flags, string id, string mapa)
    {
        foreach (var f in flags)
        {
            if (!f.StartsWith("decorpos:" + id + "=", System.StringComparison.Ordinal)) continue;
            var p = f[(f.IndexOf('=') + 1)..].Split('|');
            if (p.Length < 4 || p[0] != mapa) return null;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            float F(int i) => i < p.Length && float.TryParse(p[i], System.Globalization.NumberStyles.Float, ci, out var v) ? v : 0f;
            return (F(1), F(2), F(3), F(4));
        }
        return null;
    }

    /// <summary>Monta a peça (coordenadas do mapa → mundo pelo conversor do MapView). false = id desconhecido.</summary>
    public static bool Build(Node3D root, string id, System.Func<double, double, Vector3> at, bool icone = false, (float X, float Y, float Yaw, float Altura)? onde = null)
    {
        if (!Lugar.TryGetValue(id, out var l)) { if (!Linhas.Contains(id)) return false; l = (0f, 0f, 0f); }
        var (lx, ly, lyaw, alt) = onde ?? (l.X, l.Y, l.Yaw, 0f);
        var n = new Node3D { Name = id, Position = at(lx, ly) + new Vector3(0, alt, 0), RotationDegrees = new Vector3(0, lyaw, 0) };
        root.AddChild(n);
        if (Linhas.Contains(id)) { LinhaCidade(n, id, icone); return true; }   // 18/11 16h
        if (id.StartsWith("decor_terreno_", System.StringComparison.Ordinal) && id != "decor_terreno_jukebox") { DeFora(n, id, icone); return true; }
        if (id == "decor_terreno_jukebox") id = "decor_jukebox";   // a vitrola do quintal é a mesma jukebox (com a música)
        if (KitAcervo.Decor(n, id, icone)) return true;   // 13/11: decoração com modelo 3D (mantém luz e música)
        var madeira = VisualKit.Mat(new Color(0.36f, 0.2f, 0.1f), 0.6f);
        var ouro = VisualKit.Mat(new Color(0.95f, 0.75f, 0.3f), 0.3f, 0.8f);
        switch (id)
        {
            case "decor_tapete_real":
                VisualKit.Part(n, VisualKit.Box(3.2f, 0.02f, 2.0f), VisualKit.Mat(new Color(0.55f, 0.08f, 0.1f), 0.95f), new Vector3(0, 0.01f, 0));
                VisualKit.Part(n, VisualKit.Box(2.8f, 0.025f, 1.6f), VisualKit.Mat(new Color(0.85f, 0.65f, 0.25f), 0.9f), new Vector3(0, 0.012f, 0));
                VisualKit.Part(n, VisualKit.Box(2.5f, 0.03f, 1.3f), VisualKit.Mat(new Color(0.45f, 0.06f, 0.1f), 0.95f), new Vector3(0, 0.014f, 0));
                break;
            case "decor_quadro_mar":
                VisualKit.Part(n, VisualKit.Box(1.9f, 1.3f, 0.08f), ouro, new Vector3(0, 2.0f, 0));
                VisualKit.Part(n, VisualKit.Box(1.7f, 0.55f, 0.1f), VisualKit.Mat(new Color(0.12f, 0.3f, 0.5f), 0.7f), new Vector3(0, 1.75f, 0.01f));
                VisualKit.Part(n, VisualKit.Box(1.7f, 0.55f, 0.1f), VisualKit.Mat(new Color(0.85f, 0.55f, 0.35f), 0.7f), new Vector3(0, 2.3f, 0.01f));
                VisualKit.Part(n, VisualKit.Box(0.3f, 0.35f, 0.11f), VisualKit.Mat(new Color(0.15f, 0.1f, 0.08f), 0.7f), new Vector3(-0.2f, 1.95f, 0.02f));
                break;
            case "decor_lustre":
            {
                VisualKit.Part(n, VisualKit.Cylinder(0.02f, 0.02f, 0.8f), ouro, new Vector3(0, 3.4f, 0));
                VisualKit.Part(n, VisualKit.Cylinder(0.6f, 0.45f, 0.06f, 24), ouro, new Vector3(0, 3.0f, 0));
                var cristal = VisualKit.Glow(new Color(0.9f, 0.95f, 1f), 1.5f);
                for (var i = 0; i < 8; i++)
                {
                    var a = i * Mathf.Tau / 8;
                    VisualKit.Part(n, VisualKit.Sphere(0.06f), cristal, new Vector3(Mathf.Cos(a) * 0.55f, 2.9f, Mathf.Sin(a) * 0.55f));
                }
                n.AddChild(new OmniLight3D { LightColor = new Color("#ffe6b0"), LightEnergy = 1.6f, OmniRange = 9f, Position = new Vector3(0, 2.8f, 0), ShadowEnabled = false });
                break;
            }
            case "decor_aquario":
            {
                VisualKit.Part(n, VisualKit.Box(1.6f, 0.7f, 0.7f), madeira, new Vector3(0, 0.35f, 0));
                var vidro = VisualKit.Mat(new Color(0.4f, 0.75f, 0.85f, 0.35f), 0.05f);
                vidro.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                VisualKit.Part(n, VisualKit.Box(1.5f, 0.8f, 0.6f), vidro, new Vector3(0, 1.1f, 0));
                var peixe = VisualKit.Glow(new Color(1f, 0.6f, 0.2f), 2f);
                foreach (var (x, y) in new[] { (-0.4f, 1.0f), (0.3f, 1.2f), (0.05f, 0.9f) })
                    VisualKit.Part(n, VisualKit.Sphere(0.06f), peixe, new Vector3(x, y, 0), scale: new Vector3(1.8f, 1f, 0.6f));
                n.AddChild(new OmniLight3D { LightColor = new Color("#7fd8ff"), LightEnergy = 0.8f, OmniRange = 3f, Position = new Vector3(0, 1.1f, 0.5f), ShadowEnabled = false });
                break;
            }
            case "decor_estatua_leao":
                VisualKit.Part(n, VisualKit.Box(1.0f, 0.5f, 1.4f), VisualKit.Mat(new Color(0.5f, 0.48f, 0.45f), 0.8f), new Vector3(0, 0.25f, 0));
                VisualKit.Part(n, VisualKit.Box(0.6f, 0.55f, 1.1f), ouro, new Vector3(0, 0.8f, -0.05f));
                VisualKit.Part(n, VisualKit.Sphere(0.36f), ouro, new Vector3(0, 1.2f, 0.45f));
                VisualKit.Part(n, VisualKit.Sphere(0.22f), ouro, new Vector3(0, 1.18f, 0.72f));
                VisualKit.Part(n, VisualKit.Cylinder(0.04f, 0.07f, 0.6f), ouro, new Vector3(0, 0.9f, -0.75f), new Vector3(-50, 0, 0));
                break;
            case "decor_jukebox":
            {
                VisualKit.Part(n, VisualKit.Box(0.9f, 1.4f, 0.55f), madeira, new Vector3(0, 0.7f, 0));
                VisualKit.Part(n, VisualKit.Cylinder(0.45f, 0.45f, 0.55f, 20), madeira, new Vector3(0, 1.4f, 0), new Vector3(90, 0, 0));
                VisualKit.Part(n, VisualKit.Box(0.7f, 0.5f, 0.05f), VisualKit.Glow(new Color(1f, 0.55f, 0.85f), 1.2f), new Vector3(0, 1.05f, 0.27f));
                VisualKit.Part(n, VisualKit.Box(0.6f, 0.25f, 0.05f), VisualKit.Mat(new Color(0.1f, 0.08f, 0.06f), 0.9f), new Vector3(0, 0.45f, 0.27f));
                n.AddChild(new OmniLight3D { LightColor = new Color("#ff9ad8"), LightEnergy = 0.7f, OmniRange = 3f, Position = new Vector3(0, 1.1f, 0.6f), ShadowEnabled = false });
                if (!icone) Sfx.Loop3D(n, "musica_porto", -6f, 3f, 16f);   // no ícone, sem música
                break;
            }
            case "decor_trofeu":
                VisualKit.Part(n, VisualKit.Box(0.9f, 0.9f, 0.08f), madeira, new Vector3(0, 2.1f, 0));
                VisualKit.Part(n, VisualKit.Sphere(0.32f), VisualKit.Mat(new Color(0.35f, 0.3f, 0.25f), 0.8f), new Vector3(0, 2.1f, 0.25f));
                VisualKit.Part(n, VisualKit.Cylinder(0.02f, 0.07f, 0.5f), VisualKit.Mat(new Color(0.9f, 0.88f, 0.8f), 0.5f), new Vector3(-0.3f, 2.4f, 0.3f), new Vector3(0, 0, 40));
                VisualKit.Part(n, VisualKit.Cylinder(0.02f, 0.07f, 0.5f), VisualKit.Mat(new Color(0.9f, 0.88f, 0.8f), 0.5f), new Vector3(0.3f, 2.4f, 0.3f), new Vector3(0, 0, -40));
                VisualKit.Part(n, VisualKit.Sphere(0.05f), VisualKit.Glow(new Color(1f, 0.2f, 0.1f), 2f), new Vector3(-0.12f, 2.15f, 0.52f));
                VisualKit.Part(n, VisualKit.Sphere(0.05f), VisualKit.Glow(new Color(1f, 0.2f, 0.1f), 2f), new Vector3(0.12f, 2.15f, 0.52f));
                break;
            case "decor_trono":
                VisualKit.Part(n, VisualKit.Box(1.1f, 0.5f, 0.9f), madeira, new Vector3(0, 0.25f, 0));
                VisualKit.Part(n, VisualKit.Box(1.0f, 0.12f, 0.8f), VisualKit.Mat(new Color(0.55f, 0.08f, 0.12f), 0.9f), new Vector3(0, 0.56f, 0.02f));
                VisualKit.Part(n, VisualKit.Box(1.1f, 1.8f, 0.15f), madeira, new Vector3(0, 1.1f, -0.4f));
                VisualKit.Part(n, VisualKit.Box(0.15f, 0.4f, 0.8f), madeira, new Vector3(-0.5f, 0.7f, 0));
                VisualKit.Part(n, VisualKit.Box(0.15f, 0.4f, 0.8f), madeira, new Vector3(0.5f, 0.7f, 0));
                foreach (var x in new[] { -0.45f, 0f, 0.45f })
                    VisualKit.Part(n, VisualKit.Sphere(0.09f), ouro, new Vector3(x, 2.05f, -0.4f));
                break;
            case "decor_estante_trofeus":
            {
                // estante com 3 prateleiras; os troféus são as conquistas de quem mora (taças, escudo, crânio de chefe)
                foreach (var y in new[] { 0.05f, 0.75f, 1.45f, 2.15f })
                    VisualKit.Part(n, VisualKit.Box(1.8f, 0.06f, 0.45f), madeira, new Vector3(0, y, 0));
                foreach (var x in new[] { -0.9f, 0.9f })
                    VisualKit.Part(n, VisualKit.Box(0.06f, 2.2f, 0.45f), madeira, new Vector3(x, 1.1f, 0));
                for (var i = 0; i < 6; i++)
                {
                    var x = -0.6f + (i % 3) * 0.6f; var y = 0.1f + (i / 3) * 0.7f + 0.7f;
                    VisualKit.Part(n, VisualKit.Cylinder(0.08f, 0.04f, 0.18f, 10), ouro, new Vector3(x, y + 0.14f, 0));
                    VisualKit.Part(n, VisualKit.Cylinder(0.06f, 0.06f, 0.06f, 10), ouro, new Vector3(x, y + 0.03f, 0));
                }
                VisualKit.Part(n, VisualKit.Cylinder(0.3f, 0.3f, 0.05f, 16), VisualKit.Mat(new Color(0.55f, 0.1f, 0.12f), 0.6f, 0.4f), new Vector3(0, 0.45f, 0.1f), new Vector3(90, 0, 0));
                break;
            }
            case "decor_lareira":
            {
                var pedra = VisualKit.Mat(new Color(0.45f, 0.42f, 0.4f), 0.9f);
                VisualKit.Part(n, VisualKit.Box(2.0f, 1.4f, 0.6f), pedra, new Vector3(0, 0.7f, 0));
                VisualKit.Part(n, VisualKit.Box(1.1f, 0.8f, 0.5f), VisualKit.Mat(new Color(0.08f, 0.06f, 0.05f), 1f), new Vector3(0, 0.45f, 0.08f));
                VisualKit.Part(n, VisualKit.Box(2.3f, 0.12f, 0.75f), madeira, new Vector3(0, 1.45f, 0.05f));
                VisualKit.Part(n, VisualKit.Box(1.0f, 1.6f, 0.5f), pedra, new Vector3(0, 2.3f, -0.05f));
                VisualKit.Part(n, VisualKit.Sphere(0.25f), VisualKit.Glow(new Color(1f, 0.5f, 0.15f), 3f), new Vector3(0, 0.3f, 0.12f), null, new Vector3(1.6f, 0.9f, 0.6f));
                n.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.25f), LightEnergy = 1.6f, OmniRange = 6f, Position = new Vector3(0, 0.6f, 0.6f) });
                break;
            }
            case "decor_planta":
                VisualKit.Part(n, VisualKit.Cylinder(0.28f, 0.2f, 0.5f, 12), VisualKit.Mat(new Color(0.6f, 0.32f, 0.2f), 0.8f), new Vector3(0, 0.25f, 0));
                for (var i = 0; i < 7; i++)
                    VisualKit.Part(n, VisualKit.Sphere(0.22f), VisualKit.Mat(new Color(0.18f, 0.45f + 0.04f * i, 0.2f), 0.8f),
                        new Vector3(0.18f * Mathf.Cos(i * 0.9f), 0.75f + 0.12f * (i % 3), 0.18f * Mathf.Sin(i * 0.9f)), null, new Vector3(1f, 1.5f, 1f));
                break;
            case "decor_sofa":
            {
                var veludo = VisualKit.Mat(new Color(0.45f, 0.1f, 0.16f), 0.85f);
                VisualKit.Part(n, VisualKit.Box(2.2f, 0.45f, 0.9f), veludo, new Vector3(0, 0.35f, 0));          // assento
                VisualKit.Part(n, VisualKit.Box(2.2f, 0.7f, 0.22f), veludo, new Vector3(0, 0.8f, -0.36f));      // encosto
                foreach (var lado in new[] { -1f, 1f })
                    VisualKit.Part(n, VisualKit.Box(0.2f, 0.55f, 0.9f), veludo, new Vector3(lado * 1.1f, 0.55f, 0));   // braços
                for (var i = -1; i <= 1; i++)
                    VisualKit.Part(n, VisualKit.Box(0.66f, 0.12f, 0.7f), VisualKit.Mat(new Color(0.55f, 0.14f, 0.2f), 0.8f), new Vector3(i * 0.7f, 0.62f, 0.05f));
                foreach (var px in new[] { -0.95f, 0.95f })
                    foreach (var lz in new[] { -0.35f, 0.35f })
                        VisualKit.Part(n, VisualKit.Cylinder(0.04f, 0.03f, 0.14f, 6), madeira, new Vector3(px, 0.07f, lz));
                break;
            }
            case "decor_bandeira":
                VisualKit.Part(n, VisualKit.Cylinder(0.03f, 0.03f, 2.6f), ouro, new Vector3(0, 1.3f, 0));
                VisualKit.Part(n, VisualKit.Box(0.9f, 1.4f, 0.03f), VisualKit.Mat(new Color(0.5f, 0.08f, 0.1f), 0.9f), new Vector3(0.48f, 1.85f, 0));
                VisualKit.Part(n, VisualKit.Sphere(0.18f), ouro, new Vector3(0.48f, 1.9f, 0.03f), null, new Vector3(1f, 1f, 0.2f));
                break;
        }
        return true;
    }

    /// <summary>18/11 15h: peças de fora — tablado do boneco, portal de pedra, barraca do mascate e cercado de caça.</summary>
    private static void DeFora(Node3D n, string id, bool icone)
    {
        var madeira = VisualKit.Mat(new Color(0.42f, 0.27f, 0.15f), 0.85f);
        var escura = VisualKit.Mat(new Color(0.26f, 0.17f, 0.1f), 0.9f);
        var pedra = VisualKit.Mat(new Color(0.55f, 0.53f, 0.5f), 0.9f);
        switch (id)
        {
            case "decor_terreno_boneco":
                // tablado de treino: o boneco (monstro de treino de verdade) fica em cima
                VisualKit.Part(n, VisualKit.Cylinder(1.2f, 1.25f, 0.12f, 20), madeira, new Vector3(0, 0.06f, 0));
                VisualKit.Part(n, VisualKit.Ring(1.2f, 0.06f, 24), escura, new Vector3(0, 0.13f, 0));
                if (icone) VisualKit.Part(n, VisualKit.Cylinder(0.12f, 0.12f, 1.4f), madeira, new Vector3(0, 0.8f, 0));
                break;
            case "decor_terreno_portal":
            {
                VisualKit.Part(n, VisualKit.Cylinder(1.3f, 1.4f, 0.15f, 20), pedra, new Vector3(0, 0.07f, 0));
                VisualKit.Part(n, VisualKit.Box(0.4f, 2.8f, 0.4f), pedra, new Vector3(-1.0f, 1.4f, 0));
                VisualKit.Part(n, VisualKit.Box(0.4f, 2.8f, 0.4f), pedra, new Vector3(1.0f, 1.4f, 0));
                VisualKit.Part(n, VisualKit.Box(2.6f, 0.4f, 0.5f), pedra, new Vector3(0, 2.95f, 0));
                var luz = VisualKit.Glow(new Color(0.35f, 0.7f, 1f, 0.55f), 1.6f);
                luz.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                VisualKit.Part(n, VisualKit.Box(1.6f, 2.5f, 0.05f), luz, new Vector3(0, 1.4f, 0));
                n.AddChild(new OmniLight3D { LightColor = new Color("#7fc4ff"), LightEnergy = 1.0f, OmniRange = 5f, Position = new Vector3(0, 1.5f, 0.6f), ShadowEnabled = false });
                break;
            }
            case "decor_terreno_mascate":
            {
                // barraca: balcão, 4 esteios e um toldo listrado; o mascate (o nosso boneco) atrás do balcão
                VisualKit.Part(n, VisualKit.Box(2.4f, 0.9f, 0.7f), madeira, new Vector3(0, 0.45f, 0.4f));
                foreach (var (x, z) in new[] { (-1.15f, 0.7f), (1.15f, 0.7f), (-1.15f, -0.9f), (1.15f, -0.9f) })
                    VisualKit.Part(n, VisualKit.Cylinder(0.05f, 0.05f, 2.4f), escura, new Vector3(x, 1.2f, z));
                for (var i = 0; i < 6; i++)
                    VisualKit.Part(n, VisualKit.Box(0.42f, 0.05f, 1.9f), VisualKit.Mat(i % 2 == 0 ? new Color(0.75f, 0.15f, 0.12f) : new Color(0.93f, 0.88f, 0.75f), 0.9f),
                        new Vector3(-1.05f + i * 0.42f, 2.45f, -0.1f), new Vector3(12, 0, 0));
                foreach (var (x, c) in new[] { (-0.7f, new Color(0.9f, 0.3f, 0.3f)), (-0.2f, new Color(0.3f, 0.5f, 0.95f)), (0.4f, new Color(0.95f, 0.8f, 0.3f)) })
                    VisualKit.Part(n, VisualKit.Cylinder(0.08f, 0.1f, 0.25f), VisualKit.Mat(c, 0.3f), new Vector3(x, 1.03f, 0.4f));
                if (!icone)
                {
                    var v = Visuals.VisualFactory.NpcVisual("aldeao", "terreno_mascate", "Mascate do Quintal");
                    v.Root.Position = new Vector3(0, 0, -0.35f);
                    n.AddChild(v.Root);
                }
                break;
            }
            case "decor_terreno_cercado":
            {
                // cercado redondo de estacas com o ninho no meio
                const int estacas = 18; const float r = 3.2f;
                for (var i = 0; i < estacas; i++)
                {
                    var a = i * Mathf.Tau / estacas;
                    if (i == 0) continue;   // a entrada
                    VisualKit.Part(n, VisualKit.Cylinder(0.07f, 0.08f, 1.1f), madeira, new Vector3(Mathf.Cos(a) * r, 0.55f, Mathf.Sin(a) * r));
                    var b = (i + 0.5f) * Mathf.Tau / estacas;
                    if (i < estacas - 1)
                        VisualKit.Part(n, VisualKit.Box(0.08f, 0.08f, r * Mathf.Tau / estacas), escura, new Vector3(Mathf.Cos(b) * r, 0.85f, Mathf.Sin(b) * r), new Vector3(0, -Mathf.RadToDeg(b), 0));
                }
                VisualKit.Part(n, VisualKit.Cylinder(0.7f, 0.85f, 0.25f, 14), VisualKit.Mat(new Color(0.55f, 0.45f, 0.25f), 1f), new Vector3(0, 0.12f, 0));
                VisualKit.Part(n, VisualKit.Sphere(0.25f), VisualKit.Mat(new Color(0.92f, 0.88f, 0.75f), 0.6f), new Vector3(0, 0.45f, 0), scale: new Vector3(1f, 1.3f, 1f));
                break;
            }
        }
    }

    /// <summary>18/11 16h: peças das linhas de cada cidade (formas simples com material e luz próprios).</summary>
    private static void LinhaCidade(Node3D n, string id, bool icone)
    {
        StandardMaterial3D M(float r, float g, float b, float rough = 0.8f, float metal = 0f) => VisualKit.Mat(new Color(r, g, b), rough, metal);
        var carvalho = M(0.45f, 0.3f, 0.17f); var escuro = M(0.24f, 0.15f, 0.09f); var pedra = M(0.58f, 0.56f, 0.52f, 0.95f);
        var ferro = M(0.2f, 0.2f, 0.22f, 0.45f, 0.8f); var latao = M(0.85f, 0.65f, 0.3f, 0.3f, 0.9f); var bronze = M(0.65f, 0.42f, 0.22f, 0.35f, 0.85f);
        var ouro = M(0.95f, 0.78f, 0.3f, 0.25f, 0.9f); var marmore = M(0.9f, 0.88f, 0.84f, 0.5f);
        static TorusMesh Torus(float r, float t) => new() { InnerRadius = r - t, OuterRadius = r + t, Rings = 24, RingSegments = 8 };
        void P(Mesh m, Material mat, float x, float y, float z, float rx = 0, float ry = 0, float rz = 0) => VisualKit.Part(n, m, mat, new Vector3(x, y, z), new Vector3(rx, ry, rz));
        void Luz(string cor, float e, float r, float y, float z = 0) { if (!icone) n.AddChild(new OmniLight3D { LightColor = new Color(cor), LightEnergy = e, OmniRange = r, Position = new Vector3(0, y, z), ShadowEnabled = false }); }
        switch (id)
        {
            // ---------------- Vila das Mós (rústico)
            case "decor_rustico_tapete":
                P(VisualKit.Box(3.0f, 0.02f, 2.0f), M(0.62f, 0.45f, 0.28f, 1f), 0, 0.01f, 0);
                for (var i = 0; i < 5; i++) P(VisualKit.Box(2.8f, 0.025f, 0.12f), M(i % 2 == 0 ? 0.45f : 0.75f, i % 2 == 0 ? 0.2f : 0.6f, 0.15f, 1f), 0, 0.013f, -0.7f + i * 0.35f);
                break;
            case "decor_rustico_estante":
                P(VisualKit.Box(1.6f, 2.0f, 0.45f), escuro, 0, 1.0f, -0.05f);
                for (var i = 0; i < 4; i++) P(VisualKit.Box(1.5f, 0.06f, 0.42f), carvalho, 0, 0.15f + i * 0.55f, 0.02f);
                foreach (var (x, y, c) in new[] { (-0.5f, 0.35f, new Color(0.7f, 0.35f, 0.2f)), (0.1f, 0.35f, new Color(0.3f, 0.5f, 0.35f)), (0.45f, 0.9f, new Color(0.8f, 0.75f, 0.6f)), (-0.3f, 1.45f, new Color(0.5f, 0.3f, 0.6f)) })
                    P(VisualKit.Cylinder(0.12f, 0.14f, 0.32f), VisualKit.Mat(c, 0.6f), x, y, 0.05f);
                break;
            case "decor_rustico_lampiao":
                P(VisualKit.Cylinder(0.12f, 0.15f, 0.05f), ferro, 0, 0.03f, 0);
                P(VisualKit.Box(0.22f, 0.3f, 0.22f), VisualKit.Glow(new Color(1f, 0.75f, 0.4f), 1.6f), 0, 0.22f, 0);
                P(VisualKit.Cylinder(0.02f, 0.16f, 0.1f), ferro, 0, 0.42f, 0);
                P(Torus(0.06f, 0.012f), ferro, 0, 0.52f, 0, 90);
                Luz("#ffc070", 0.9f, 4f, 0.3f);
                break;
            case "decor_terreno_poco":
                P(VisualKit.Cylinder(0.85f, 0.9f, 0.85f, 16), pedra, 0, 0.42f, 0);
                P(VisualKit.Cylinder(0.68f, 0.68f, 0.05f, 16), M(0.1f, 0.2f, 0.3f, 0.1f), 0, 0.82f, 0);
                P(VisualKit.Box(0.12f, 1.5f, 0.12f), carvalho, -0.8f, 1.4f, 0);
                P(VisualKit.Box(0.12f, 1.5f, 0.12f), carvalho, 0.8f, 1.4f, 0);
                P(VisualKit.Cylinder(0.07f, 0.07f, 1.7f), escuro, 0, 1.85f, 0, 0, 0, 90);
                P(VisualKit.Box(2.0f, 0.06f, 1.2f), escuro, 0, 2.25f, 0, 18);
                P(VisualKit.Cylinder(0.14f, 0.11f, 0.22f), carvalho, 0.2f, 1.25f, 0);
                break;
            case "decor_terreno_espantalho":
                P(VisualKit.Cylinder(0.05f, 0.05f, 2.0f), carvalho, 0, 1.0f, 0);
                P(VisualKit.Cylinder(0.04f, 0.04f, 1.5f), carvalho, 0, 1.45f, 0, 0, 0, 90);
                P(VisualKit.Box(0.55f, 0.65f, 0.25f), M(0.55f, 0.25f, 0.2f, 1f), 0, 1.25f, 0);
                P(VisualKit.Sphere(0.2f), M(0.85f, 0.75f, 0.45f, 1f), 0, 1.78f, 0);
                P(VisualKit.Cylinder(0.05f, 0.32f, 0.22f), M(0.7f, 0.6f, 0.35f, 1f), 0, 1.98f, 0);
                break;
            // ---------------- Astrália (estrelas)
            case "decor_astro_globo":
                P(VisualKit.Cylinder(0.25f, 0.35f, 0.12f), bronze, 0, 0.06f, 0);
                P(VisualKit.Cylinder(0.04f, 0.04f, 0.8f), bronze, 0, 0.5f, 0);
                P(VisualKit.Sphere(0.38f), VisualKit.Mat(new Color(0.12f, 0.18f, 0.38f), 0.3f, 0.3f), 0, 1.15f, 0);
                P(Torus(0.44f, 0.02f), latao, 0, 1.15f, 0, 90, 0, 23);
                for (var i = 0; i < 9; i++) { var a = i * 0.7f; P(VisualKit.Sphere(0.025f), VisualKit.Glow(new Color(1f, 0.95f, 0.7f), 2f), Mathf.Cos(a) * 0.37f, 1.15f + Mathf.Sin(a * 1.7f) * 0.2f, Mathf.Sin(a) * 0.3f); }
                break;
            case "decor_astro_telescopio":
                foreach (var a in new[] { 0f, 120f, 240f }) P(VisualKit.Cylinder(0.03f, 0.03f, 1.2f), escuro, Mathf.Sin(Mathf.DegToRad(a)) * 0.25f, 0.55f, Mathf.Cos(Mathf.DegToRad(a)) * 0.25f, Mathf.Cos(Mathf.DegToRad(a)) * 15f, 0, -Mathf.Sin(Mathf.DegToRad(a)) * 15f);
                P(VisualKit.Cylinder(0.09f, 0.14f, 1.3f), latao, 0, 1.3f, 0.1f, 55);
                P(VisualKit.Cylinder(0.15f, 0.15f, 0.05f), VisualKit.Mat(new Color(0.5f, 0.7f, 0.9f, 1f), 0.05f), 0, 1.68f, 0.63f, 55);
                break;
            case "decor_astro_luminaria":
                P(VisualKit.Cylinder(0.015f, 0.015f, 0.7f), latao, 0, 3.35f, 0);
                P(VisualKit.Sphere(0.32f), VisualKit.Glow(new Color(0.6f, 0.75f, 1f), 1.4f), 0, 2.9f, 0);
                for (var i = 0; i < 6; i++) { var a = i * Mathf.Tau / 6; P(VisualKit.Sphere(0.05f), VisualKit.Glow(new Color(1f, 0.95f, 0.75f), 2.5f), Mathf.Cos(a) * 0.5f, 2.8f + 0.08f * (i % 2), Mathf.Sin(a) * 0.5f); }
                Luz("#a8c4ff", 1.4f, 8f, 2.7f);
                break;
            case "decor_terreno_fonte":
                P(VisualKit.Cylinder(1.6f, 1.7f, 0.45f, 24), marmore, 0, 0.22f, 0);
                P(VisualKit.Cylinder(1.45f, 1.45f, 0.05f, 24), VisualKit.Glow(new Color(0.35f, 0.7f, 0.95f), 0.6f), 0, 0.42f, 0);
                P(VisualKit.Cylinder(0.25f, 0.35f, 1.3f, 12), marmore, 0, 0.9f, 0);
                P(VisualKit.Cylinder(0.7f, 0.25f, 0.2f, 16), marmore, 0, 1.6f, 0);
                P(VisualKit.Sphere(0.18f), VisualKit.Glow(new Color(0.6f, 0.85f, 1f), 2.2f), 0, 1.85f, 0);
                Luz("#7fd0ff", 1.2f, 6f, 1.5f);
                break;
            case "decor_terreno_relogio_sol":
                P(VisualKit.Cylinder(0.35f, 0.45f, 1.0f, 12), marmore, 0, 0.5f, 0);
                P(VisualKit.Cylinder(0.7f, 0.7f, 0.08f, 24), marmore, 0, 1.04f, 0);
                P(VisualKit.Box(0.04f, 0.45f, 0.5f), bronze, 0, 1.25f, 0.05f, -30);
                for (var i = 0; i < 12; i++) { var a = i * Mathf.Tau / 12; P(VisualKit.Box(0.03f, 0.01f, 0.12f), bronze, Mathf.Cos(a) * 0.58f, 1.09f, Mathf.Sin(a) * 0.58f, 0, -Mathf.RadToDeg(a) + 90); }
                break;
            // ---------------- Candeia (lanternas, vitrais)
            case "decor_candeia_vitral":
            {
                P(VisualKit.Box(1.5f, 2.2f, 0.08f), escuro, 0, 1.9f, 0);
                var cores = new[] { new Color(0.9f, 0.25f, 0.2f), new Color(0.25f, 0.5f, 0.95f), new Color(0.95f, 0.8f, 0.25f), new Color(0.3f, 0.8f, 0.4f) };
                for (var r = 0; r < 4; r++) for (var c = 0; c < 3; c++)
                    P(VisualKit.Box(0.42f, 0.46f, 0.04f), VisualKit.Glow(cores[(r + c) % 4], 0.9f), -0.45f + c * 0.45f, 1.05f + r * 0.5f, 0.05f);
                Luz("#ffd9a0", 0.8f, 4f, 1.9f, 0.6f);
                break;
            }
            case "decor_candeia_candelabro":
                P(VisualKit.Cylinder(0.2f, 0.3f, 0.08f), ferro, 0, 0.04f, 0);
                P(VisualKit.Cylinder(0.04f, 0.05f, 1.5f), ferro, 0, 0.8f, 0);
                P(VisualKit.Box(1.1f, 0.04f, 0.04f), ferro, 0, 1.5f, 0);
                for (var i = 0; i < 7; i++)
                {
                    var x = -0.5f + i * (1f / 6f);
                    var h = 1.55f + (3 - Mathf.Abs(i - 3)) * 0.06f;
                    P(VisualKit.Cylinder(0.025f, 0.025f, 0.2f), marmore, x, h + 0.1f, 0);
                    P(VisualKit.Sphere(0.03f), VisualKit.Glow(new Color(1f, 0.7f, 0.3f), 3f), x, h + 0.24f, 0);
                }
                Luz("#ffb060", 1.2f, 6f, 1.9f);
                break;
            case "decor_candeia_bau":
                P(VisualKit.Box(1.2f, 0.6f, 0.7f), M(0.45f, 0.08f, 0.1f, 0.3f), 0, 0.3f, 0);
                P(VisualKit.Cylinder(0.35f, 0.35f, 1.2f, 16), M(0.45f, 0.08f, 0.1f, 0.3f), 0, 0.6f, 0, 0, 0, 90);
                foreach (var x in new[] { -0.6f, 0.6f }) P(VisualKit.Box(0.06f, 0.95f, 0.72f), ouro, x, 0.47f, 0);
                P(VisualKit.Box(0.16f, 0.16f, 0.05f), ouro, 0, 0.55f, 0.36f);
                break;
            case "decor_terreno_lanternas":
            {
                var papel = new[] { new Color(1f, 0.4f, 0.3f), new Color(1f, 0.8f, 0.35f), new Color(0.5f, 0.75f, 1f), new Color(0.9f, 0.5f, 0.9f) };
                for (var i = 0; i < 4; i++)
                {
                    var (x, z) = (i % 2 == 0 ? -1.3f : 1.3f, i < 2 ? -1.3f : 1.3f);
                    P(VisualKit.Cylinder(0.05f, 0.06f, 2.2f), escuro, x, 1.1f, z);
                    P(VisualKit.Box(0.4f, 0.04f, 0.04f), escuro, x * 0.88f, 2.15f, z * 0.88f, 0, i * 90 + 45);
                    P(VisualKit.Sphere(0.2f), VisualKit.Glow(papel[i], 1.8f), x * 0.78f, 1.85f, z * 0.78f);
                }
                Luz("#ffc080", 1.3f, 7f, 1.9f);
                break;
            }
            case "decor_terreno_caldeirao":
                foreach (var a in new[] { 0f, 120f, 240f }) P(VisualKit.Cylinder(0.04f, 0.04f, 1.6f), escuro, Mathf.Sin(Mathf.DegToRad(a)) * 0.75f, 0.8f, Mathf.Cos(Mathf.DegToRad(a)) * 0.75f, Mathf.Cos(Mathf.DegToRad(a)) * -22f, 0, Mathf.Sin(Mathf.DegToRad(a)) * 22f);
                P(VisualKit.Sphere(0.55f), ferro, 0, 0.7f, 0);
                P(VisualKit.Cylinder(0.48f, 0.48f, 0.04f, 20), VisualKit.Glow(new Color(0.4f, 0.95f, 0.45f), 1.2f), 0, 1.02f, 0);
                P(VisualKit.Sphere(0.35f), VisualKit.Glow(new Color(1f, 0.45f, 0.1f), 2.5f), 0, 0.1f, 0);
                Luz("#ff8a3a", 1.2f, 5f, 0.4f);
                break;
            // ---------------- Brasaforte (forja, bronze)
            case "decor_forja_armadura":
                P(VisualKit.Cylinder(0.3f, 0.35f, 0.1f), carvalho, 0, 0.05f, 0);
                P(VisualKit.Cylinder(0.04f, 0.04f, 1.4f), carvalho, 0, 0.75f, 0);
                P(VisualKit.Box(0.6f, 0.7f, 0.35f), ferro, 0, 1.25f, 0);
                foreach (var x in new[] { -0.4f, 0.4f }) P(VisualKit.Sphere(0.2f), bronze, x, 1.55f, 0);
                P(VisualKit.Sphere(0.2f), ferro, 0, 1.85f, 0);
                P(VisualKit.Box(0.5f, 0.06f, 0.06f), bronze, 0, 1.82f, 0.19f);
                P(VisualKit.Box(0.55f, 0.35f, 0.32f), ferro, 0, 0.82f, 0);
                break;
            case "decor_forja_trofeu":
                P(VisualKit.Box(1.0f, 1.0f, 0.08f), escuro, 0, 2.1f, 0);
                P(VisualKit.Sphere(0.34f), M(0.85f, 0.82f, 0.72f, 0.6f), 0, 2.1f, 0.28f);
                foreach (var s2 in new[] { -1f, 1f })
                    P(VisualKit.Cylinder(0.01f, 0.08f, 0.75f), M(0.25f, 0.2f, 0.18f, 0.5f), s2 * 0.42f, 2.45f, 0.3f, 0, 0, -s2 * 55);
                foreach (var x in new[] { -0.12f, 0.12f }) P(VisualKit.Sphere(0.05f), VisualKit.Glow(new Color(1f, 0.35f, 0.1f), 2.5f), x, 2.15f, 0.58f);
                break;
            case "decor_forja_braseiro":
                foreach (var a in new[] { 0f, 120f, 240f }) P(VisualKit.Cylinder(0.035f, 0.035f, 0.8f), bronze, Mathf.Sin(Mathf.DegToRad(a)) * 0.3f, 0.35f, Mathf.Cos(Mathf.DegToRad(a)) * 0.3f);
                P(VisualKit.Cylinder(0.55f, 0.3f, 0.3f, 16), bronze, 0, 0.85f, 0);
                P(VisualKit.Cylinder(0.48f, 0.48f, 0.05f, 16), VisualKit.Glow(new Color(1f, 0.35f, 0.08f), 2.8f), 0, 0.98f, 0);
                Luz("#ff7a30", 1.5f, 6f, 1.2f);
                break;
            case "decor_terreno_forja":
                P(VisualKit.Box(1.8f, 1.0f, 1.2f), pedra, 0, 0.5f, 0);
                P(VisualKit.Box(1.2f, 0.12f, 0.8f), VisualKit.Glow(new Color(1f, 0.4f, 0.08f), 3f), 0, 1.02f, 0);
                P(VisualKit.Box(0.6f, 2.2f, 0.6f), pedra, 0, 1.6f, -0.3f);
                P(VisualKit.Box(0.32f, 0.12f, 0.5f), ferro, 1.4f, 0.75f, 0.3f);
                P(VisualKit.Cylinder(0.25f, 0.3f, 0.65f, 10), carvalho, 1.4f, 0.33f, 0.3f);
                Luz("#ff7030", 1.8f, 7f, 1.3f, 0.5f);
                break;
            case "decor_terreno_estatua":
                P(VisualKit.Box(1.3f, 0.7f, 1.3f), pedra, 0, 0.35f, 0);
                P(VisualKit.Box(0.55f, 0.9f, 0.32f), bronze, 0, 1.5f, 0);
                P(VisualKit.Sphere(0.2f), bronze, 0, 2.15f, 0);
                foreach (var x in new[] { -0.15f, 0.15f }) P(VisualKit.Box(0.18f, 0.8f, 0.2f), bronze, x, 0.98f + 0.4f - 0.4f, 0);
                P(VisualKit.Box(0.14f, 0.7f, 0.14f), bronze, 0.38f, 2.0f, 0, 0, 0, -20);
                P(VisualKit.Box(0.06f, 1.2f, 0.12f), M(0.8f, 0.8f, 0.85f, 0.2f, 0.9f), 0.55f, 2.85f, 0);
                P(VisualKit.Box(0.14f, 0.7f, 0.14f), bronze, -0.36f, 1.55f, 0, 0, 0, 10);
                break;
            default:
                if (id.StartsWith("decor_terreno_portal_", System.StringComparison.Ordinal))
                {
                    var cor = id.EndsWith("vila_das_mos") ? new Color(0.55f, 0.9f, 0.45f) : id.EndsWith("astralia") ? new Color(0.65f, 0.6f, 1f)
                        : id.EndsWith("candeia") ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.4f, 0.2f);
                    P(VisualKit.Cylinder(1.3f, 1.4f, 0.15f, 20), pedra, 0, 0.07f, 0);
                    P(VisualKit.Box(0.4f, 2.8f, 0.4f), pedra, -1.0f, 1.4f, 0);
                    P(VisualKit.Box(0.4f, 2.8f, 0.4f), pedra, 1.0f, 1.4f, 0);
                    P(VisualKit.Box(2.6f, 0.4f, 0.5f), pedra, 0, 2.95f, 0);
                    P(VisualKit.Sphere(0.16f), VisualKit.Glow(cor, 2.5f), 0, 3.25f, 0);
                    var veu = VisualKit.Glow(cor with { A = 0.55f }, 1.6f);
                    veu.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    P(VisualKit.Box(1.6f, 2.5f, 0.05f), veu, 0, 1.4f, 0);
                    if (!icone) n.AddChild(new OmniLight3D { LightColor = cor, LightEnergy = 1.0f, OmniRange = 5f, Position = new Vector3(0, 1.5f, 0.6f), ShadowEnabled = false });
                }
                break;
        }
    }
}
