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
    };

    public static bool Conhece(string id) => Lugar.ContainsKey(id);

    /// <summary>
    /// 18/11 (dono: "cada objeto tem seu lugar, ou parede, ou chão, ou sobre algo"): onde a peça pode ir.
    /// parede = cola na parede mais perto, de frente para o cômodo; teto = pendurada (anda livre); sobre = em cima de
    /// mesa/prateleira/estante se tiver uma perto (senão no chão); chão = livre (a rodinha gira).
    /// </summary>
    public static string Categoria(string id) => id switch
    {
        "decor_quadro_mar" or "decor_bandeira" or "decor_trofeu" => "parede",
        "decor_lustre" => "teto",
        "decor_aquario" or "decor_planta" => "sobre",
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
        if (!Lugar.TryGetValue(id, out var l)) return false;
        var (lx, ly, lyaw, alt) = onde ?? (l.X, l.Y, l.Yaw, 0f);
        var n = new Node3D { Name = id, Position = at(lx, ly) + new Vector3(0, alt, 0), RotationDegrees = new Vector3(0, lyaw, 0) };
        root.AddChild(n);
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
}
