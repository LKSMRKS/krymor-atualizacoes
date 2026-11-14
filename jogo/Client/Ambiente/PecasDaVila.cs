using CinzasDaMare.Client.Visuals;
using Godot;

namespace CinzasDaMare.Client.Ambiente;

/// <summary>
/// 13/11 (pedido do dono: "coisas de trabalho perto dos NPCs — o pescador precisa de uma armação de madeira com peixes
/// pendurados"; "a biblioteca dentro de uma casa com aparência diferente"; "o Bartô no muro com um painel do placar"):
/// peças da vila feitas por código (madeira, pedra, pano), no mesmo estilo das outras. Tools/gerar_interiores.py as põe.
/// </summary>
public static class PecasDaVila
{
    private static readonly Color Madeira = new(0.42f, 0.28f, 0.16f), MadeiraEscura = new(0.24f, 0.16f, 0.09f), Corda = new(0.72f, 0.62f, 0.42f);

    public static bool Build(Node3D node, string kind)
    {
        var mad = VisualKit.Mat(Madeira, 0.9f);
        var esc = VisualKit.Mat(MadeiraEscura, 0.9f);
        switch (kind)
        {
            case "secador_peixe":
            {
                // armação de pescador: dois cavaletes em X, uma vara em cima e peixes pendurados por cordinhas
                foreach (var z in new[] { -1.1f, 1.1f })
                    foreach (var s in new[] { -1f, 1f })
                        VisualKit.Part(node, VisualKit.Box(0.08f, 1.9f, 0.08f), mad, new Vector3(s * 0.25f, 0.9f, z), new Vector3(0, 0, s * 14f));
                VisualKit.Part(node, VisualKit.Cylinder(0.04f, 0.04f, 2.4f, 6), esc, new Vector3(0, 1.75f, 0), new Vector3(90, 0, 0));
                var peixe = VisualKit.Mat(new Color(0.55f, 0.6f, 0.62f), 0.35f, 0.6f);
                var corda = VisualKit.Mat(Corda, 0.9f);
                for (var i = 0; i < 6; i++)
                {
                    var z = -0.95f + i * 0.38f;
                    VisualKit.Part(node, VisualKit.Cylinder(0.006f, 0.006f, 0.28f, 3), corda, new Vector3(0, 1.6f, z));
                    var p = VisualKit.Part(node, VisualKit.Sphere(0.09f), peixe, new Vector3(0, 1.32f - (i % 2) * 0.05f, z), new Vector3(0, 0, 90), new Vector3(0.55f, 1.9f, 0.35f));
                    VisualKit.Part(p, VisualKit.Box(0.16f, 0.12f, 0.01f), peixe, new Vector3(0, -0.1f, 0), null, new Vector3(1f, 1f, 1f));   // rabo
                }
                return true;
            }
            case "cesto_peixe":
            {
                var vime = VisualKit.Mat(new Color(0.66f, 0.52f, 0.3f), 1f);
                VisualKit.Part(node, VisualKit.Cylinder(0.32f, 0.26f, 0.38f, 12), vime, new Vector3(0, 0.19f, 0));
                var peixe = VisualKit.Mat(new Color(0.5f, 0.56f, 0.6f), 0.3f, 0.6f);
                for (var i = 0; i < 4; i++)
                    VisualKit.Part(node, VisualKit.Sphere(0.08f), peixe, new Vector3(-0.12f + i * 0.08f, 0.4f, (i % 2) * 0.08f - 0.04f), new Vector3(0, i * 40, 80), new Vector3(0.5f, 1.8f, 0.35f));
                // rede jogada do lado
                VisualKit.Part(node, VisualKit.Box(0.7f, 0.03f, 0.5f), VisualKit.Mat(new Color(0.35f, 0.4f, 0.38f), 1f), new Vector3(0.6f, 0.02f, 0.2f), new Vector3(0, 25, 4));
                return true;
            }
            case "carnes_penduradas":
            {
                VisualKit.Part(node, VisualKit.Box(2.2f, 0.08f, 0.08f), esc, new Vector3(0, 2.3f, 0));
                var carne = VisualKit.Mat(new Color(0.62f, 0.18f, 0.16f), 0.45f);
                var gordura = VisualKit.Mat(new Color(0.92f, 0.85f, 0.75f), 0.6f);
                var ferro = VisualKit.Mat(new Color(0.3f, 0.3f, 0.32f), 0.4f, 0.8f);
                for (var i = 0; i < 4; i++)
                {
                    var x = -0.8f + i * 0.53f;
                    VisualKit.Part(node, VisualKit.Cylinder(0.01f, 0.01f, 0.2f, 4), ferro, new Vector3(x, 2.15f, 0));   // gancho
                    var c = VisualKit.Part(node, VisualKit.Sphere(0.16f), carne, new Vector3(x, 1.78f, 0), null, new Vector3(0.9f, 1.9f, 0.6f));
                    VisualKit.Part(c, VisualKit.Sphere(0.08f), gordura, new Vector3(0.03f, 0.1f, 0.06f), null, new Vector3(1f, 0.6f, 0.5f));
                }
                return true;
            }
            case "cadeira_dentista":
            {
                var couro = VisualKit.Mat(new Color(0.2f, 0.45f, 0.5f), 0.5f);
                var metal = VisualKit.Mat(new Color(0.75f, 0.76f, 0.8f), 0.3f, 0.9f);
                VisualKit.Part(node, VisualKit.Cylinder(0.25f, 0.3f, 0.5f, 10), metal, new Vector3(0, 0.25f, 0));
                VisualKit.Part(node, VisualKit.Box(0.7f, 0.16f, 0.8f), couro, new Vector3(0, 0.58f, 0));
                VisualKit.Part(node, VisualKit.Box(0.7f, 0.9f, 0.14f), couro, new Vector3(0, 1.0f, -0.42f), new Vector3(-18, 0, 0));
                VisualKit.Part(node, VisualKit.Box(0.4f, 0.1f, 0.5f), couro, new Vector3(0, 0.5f, 0.6f), new Vector3(30, 0, 0));
                VisualKit.Part(node, VisualKit.Cylinder(0.02f, 0.02f, 1.4f, 6), metal, new Vector3(0.55f, 1.2f, -0.3f));
                VisualKit.Part(node, VisualKit.Sphere(0.12f), VisualKit.Glow(new Color(1f, 0.95f, 0.8f), 2f), new Vector3(0.4f, 1.9f, 0.0f));   // lâmpada
                // dente gigante de enfeite
                VisualKit.Part(node, VisualKit.Box(0.3f, 0.35f, 0.25f), VisualKit.Mat(new Color(0.97f, 0.96f, 0.92f), 0.3f), new Vector3(-0.9f, 0.2f, 0.3f));
                return true;
            }
            case "frascos_quimica":
            {
                var cores = new[] { new Color(0.3f, 1f, 0.5f), new Color(0.3f, 0.6f, 1f), new Color(1f, 0.5f, 0.2f), new Color(0.9f, 0.3f, 0.9f) };
                var vidro = VisualKit.Mat(new Color(0.85f, 0.9f, 0.92f), 0.05f);
                for (var i = 0; i < 4; i++)
                {
                    var p = new Vector3(-0.45f + i * 0.3f, 0.86f, (i % 2) * 0.12f);
                    VisualKit.Part(node, VisualKit.Sphere(0.07f), VisualKit.Glow(cores[i], 1.2f), p + new Vector3(0, 0.08f, 0));
                    VisualKit.Part(node, VisualKit.Cylinder(0.015f, 0.02f, 0.12f, 6), vidro, p + new Vector3(0, 0.2f, 0));
                }
                VisualKit.Part(node, VisualKit.Cylinder(0.08f, 0.1f, 0.06f, 10), VisualKit.Mat(new Color(0.2f, 0.2f, 0.22f), 0.4f, 0.8f), new Vector3(0.4f, 0.8f, -0.15f));
                return true;
            }
            case "tablado":
            {
                // palquinho do dançarino: tábuas, quatro estacas com bandeirolas e uma lanterna
                VisualKit.Part(node, VisualKit.Box(2.4f, 0.3f, 2.4f), mad, new Vector3(0, 0.15f, 0));
                for (var i = 0; i < 5; i++) VisualKit.Part(node, VisualKit.Box(2.42f, 0.02f, 0.05f), esc, new Vector3(0, 0.31f, -1f + i * 0.5f));
                var cores = new[] { new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.8f, 0.1f), new Color(0.2f, 0.5f, 1f), new Color(0.3f, 0.8f, 0.3f) };
                foreach (var (x, z) in new[] { (-1.1f, -1.1f), (1.1f, -1.1f), (1.1f, 1.1f), (-1.1f, 1.1f) })
                    VisualKit.Part(node, VisualKit.Box(0.08f, 2.2f, 0.08f), esc, new Vector3(x, 1.1f, z));
                for (var i = 0; i < 8; i++)
                    VisualKit.Part(node, VisualKit.Cylinder(0f, 0.09f, 0.18f, 3), VisualKit.Mat(cores[i % 4], 0.8f), new Vector3(-1f + i * 0.28f, 2.05f, -1.1f), new Vector3(180, 0, 0));
                return true;
            }
            case "painel_placar":
            {
                // painel do placar no muro: moldura, título e linhas de giz (o ranking de verdade é com o Bartô)
                VisualKit.Part(node, VisualKit.Box(0.12f, 2.4f, 2.6f), esc, new Vector3(0, 1.6f, 0));
                VisualKit.Part(node, VisualKit.Box(0.04f, 2.1f, 2.3f), VisualKit.Mat(new Color(0.12f, 0.16f, 0.13f), 0.95f), new Vector3(0.08f, 1.6f, 0));
                var giz = VisualKit.Mat(new Color(0.9f, 0.9f, 0.85f), 1f);
                for (var i = 0; i < 6; i++)
                    VisualKit.Part(node, VisualKit.Box(0.01f, 0.04f, 1.4f - (i % 3) * 0.25f), giz, new Vector3(0.11f, 2.2f - i * 0.28f, -0.2f + (i % 3) * 0.12f));
                node.AddChild(new Label3D
                {
                    Text = "PLACAR DOS HERÓIS", FontSize = 46, OutlineSize = 8, PixelSize = 0.006f, Modulate = new Color(1f, 0.85f, 0.4f),
                    Position = new Vector3(0.14f, 2.62f, 0), RotationDegrees = new Vector3(0, 90, 0),
                });
                return true;
            }
            case "predio_biblioteca":
            {
                // prédio diferente das casas: pedra cinza-azulada, colunas brancas na frente, frontão triangular, porta dupla
                // escura, janelas com vitral aceso e a placa "BIBLIOTECA DO CAIS" (a frente olha para +Z do nó)
                var pedra = VisualKit.Mat(new Color(0.55f, 0.58f, 0.62f), 0.85f);
                var marmore = VisualKit.Mat(new Color(0.9f, 0.88f, 0.84f), 0.5f);
                var telhado = VisualKit.Mat(new Color(0.2f, 0.28f, 0.42f), 0.6f);
                const float W = 8.6f, Dp = 6.2f, H = 4.6f;
                VisualKit.Part(node, VisualKit.Box(W + 0.6f, 0.4f, Dp + 1.8f), marmore, new Vector3(0, 0.2f, 0.5f));   // base/escadaria
                VisualKit.Part(node, VisualKit.Box(W, H, Dp), pedra, new Vector3(0, H / 2f + 0.4f, 0));
                // telhado de duas águas
                foreach (var s in new[] { -1f, 1f })
                    VisualKit.Part(node, VisualKit.Box(W + 0.8f, 0.25f, Dp / 2f + 0.9f), telhado, new Vector3(0, H + 1.2f, s * (Dp / 4f + 0.2f)), new Vector3(s * -28f, 0, 0));
                // frontão + colunas
                VisualKit.Part(node, VisualKit.Box(W + 0.4f, 0.35f, 1.4f), marmore, new Vector3(0, H + 0.55f, Dp / 2f + 0.7f));
                var front = VisualKit.Part(node, VisualKit.Cylinder(0f, 1f, 1f, 3), marmore, new Vector3(0, H + 1.2f, Dp / 2f + 0.8f), new Vector3(90, 0, 0), new Vector3(W * 0.55f, 0.3f, 1.2f));
                front.RotationDegrees = new Vector3(90, 0, 0);
                for (var i = 0; i < 4; i++)
                    VisualKit.Part(node, VisualKit.Cylinder(0.22f, 0.26f, H, 12), marmore, new Vector3(-3.0f + i * 2.0f, H / 2f + 0.4f, Dp / 2f + 1.1f));
                // porta dupla e janelas de vitral
                VisualKit.Part(node, VisualKit.Box(1.8f, 2.6f, 0.12f), VisualKit.Mat(new Color(0.18f, 0.1f, 0.06f), 0.8f), new Vector3(0, 1.7f, Dp / 2f + 0.02f));
                VisualKit.Part(node, VisualKit.Box(0.06f, 2.6f, 0.14f), VisualKit.Mat(new Color(0.75f, 0.6f, 0.3f), 0.3f, 0.9f), new Vector3(0, 1.7f, Dp / 2f + 0.05f));
                foreach (var x in new[] { -2.9f, 2.9f })
                {
                    VisualKit.Part(node, VisualKit.Box(1.1f, 1.6f, 0.08f), VisualKit.Glow(new Color(0.95f, 0.7f, 0.35f), 0.9f), new Vector3(x, 2.4f, Dp / 2f + 0.02f));
                    VisualKit.Part(node, VisualKit.Box(1.3f, 0.12f, 0.2f), marmore, new Vector3(x, 1.55f, Dp / 2f + 0.06f));
                }
                node.AddChild(new Label3D
                {
                    Text = "BIBLIOTECA DO CAIS", FontSize = 64, OutlineSize = 10, PixelSize = 0.008f, Modulate = new Color(0.95f, 0.85f, 0.55f),
                    Position = new Vector3(0, H + 0.55f, Dp / 2f + 1.42f),
                });
                node.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.75f, 0.4f), LightEnergy = 1.2f, OmniRange = 6f, Position = new Vector3(0, 3.4f, Dp / 2f + 1.8f) });
                return true;
            }
        }
        return false;
    }
}
