using Godot;
using CinzasDaMare.Client;

namespace CinzasDaMare.Client.Ambiente;

/// <summary>
/// Cena da prévia "costa_depois" (lago, rochas, capim, arbustos, flores, coqueiros/pinheiros), convertida para o mapa.
/// Coordenadas RELATIVAS à origem; origem padrão = (-31.0, 49.0) no mapa da Costa, que põe o lago em cima do "lago" do map_costa (-20, 37).
/// Só visual: a colisão continua vindo dos obstáculos do mapa (ver FICHA, lista para o gerar_regioes.py).
/// Uso: CenaPrintCosta.Montar(mundo, new Vector2(-31.0f, 49.0f));   // mundo = Node3D raiz do mapa, P(x,y,h) = (x,h,y)
/// </summary>
public static class CenaPrintCosta
{
    // (arquivo, dx, dy do mapa, yaw graus Godot, escala x, y, z Godot)
    private static readonly (string f, float dx, float dy, float yaw, float sx, float sy, float sz)[] Pecas =
    {
        ("costa/costa_lago", 11.00f, -12.00f, 195.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_pinheiro_a", 18.00f, -22.00f, 44.2f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_pinheiro_b", -14.00f, -24.00f, 87.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_coqueiro_a", -7.00f, -15.00f, 106.3f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_coqueiro_b", 3.00f, -24.00f, 159.3f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_pinheiro_c", -22.00f, -14.00f, 86.4f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_coqueiro_a", 21.00f, -6.00f, 152.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_rocha_a", -3.00f, -9.00f, 190.4f, 1.80f, 1.90f, 1.80f),
        ("costa/costa_rocha_b", -15.00f, -6.00f, 347.6f, 2.20f, 1.50f, 2.20f),
        ("costa/costa_rocha_c", 4.00f, -4.00f, 159.6f, 1.10f, 1.00f, 1.10f),
        ("costa/costa_rocha_a", 20.00f, -16.00f, 53.6f, 1.40f, 1.30f, 1.40f),
        ("costa/costa_rocha_c", -19.00f, -20.00f, 144.3f, 1.60f, 1.40f, 1.60f),
        ("costa/costa_capim_c", -3.55f, -25.58f, 258.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 10.58f, 3.32f, 95.7f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", 16.34f, -0.49f, 230.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", -5.64f, -21.95f, 180.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -17.83f, -19.30f, 257.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -23.16f, 1.92f, 284.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", -22.15f, -8.74f, 64.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 14.17f, 4.11f, 68.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -22.40f, 2.60f, 287.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 6.66f, -20.69f, 232.5f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 20.87f, -16.36f, 37.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", 23.84f, -4.31f, 301.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 9.12f, 0.19f, 353.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -9.82f, -24.18f, 171.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 22.90f, -2.01f, 124.2f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 23.13f, -0.79f, 311.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -3.77f, -25.71f, 196.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 15.32f, 4.57f, 250.7f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -12.28f, 3.75f, 159.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 18.81f, -2.05f, 107.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -17.27f, -24.83f, 160.5f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 19.62f, -15.10f, 39.9f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -20.07f, -15.12f, 152.7f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -2.17f, -16.00f, 294.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 15.48f, -23.50f, 233.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -9.08f, 5.90f, 238.2f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", 21.67f, 5.57f, 12.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 4.52f, -18.41f, 33.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -15.66f, -11.42f, 331.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", 21.33f, 3.31f, 191.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 17.73f, -2.59f, 161.9f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -6.26f, -0.78f, 218.4f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 3.14f, -21.22f, 124.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -23.97f, 1.27f, 171.4f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 2.23f, -10.38f, 82.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 1.01f, -16.69f, 325.4f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", -23.44f, 4.71f, 45.7f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 0.03f, -15.17f, 212.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", 10.93f, -0.98f, 132.3f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 6.66f, -0.85f, 99.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -9.34f, -4.75f, 93.5f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", 6.56f, -3.46f, 281.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -9.16f, -16.60f, 160.4f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -7.19f, -7.42f, 184.2f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", -2.26f, -17.98f, 61.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -7.08f, -0.48f, 305.2f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", -6.92f, -2.39f, 118.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 7.20f, 0.97f, 133.2f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -9.04f, 5.24f, 164.3f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", -0.87f, -9.06f, 239.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -16.16f, 4.00f, 351.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -9.19f, -2.97f, 82.9f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 15.75f, -1.03f, 29.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -7.95f, -12.53f, 44.5f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -13.19f, -5.62f, 79.3f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 22.67f, -20.88f, 338.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", -7.21f, -18.99f, 66.9f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_b", -6.28f, -24.66f, 349.8f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_c", 13.14f, -24.27f, 228.1f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_capim_a", 8.77f, -20.19f, 249.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_a", 22.29f, -16.69f, 316.9f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_b", -5.37f, -14.20f, 265.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_c", -19.35f, -25.99f, 325.9f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_a", 10.63f, -17.80f, 221.4f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_b", -15.91f, -18.52f, 193.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_c", -5.45f, -24.10f, 16.3f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_a", 7.71f, -14.38f, 165.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_b", -15.29f, -17.62f, 112.7f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_c", -6.83f, -18.61f, 231.2f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_a", -4.61f, -14.72f, 164.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_b", -22.46f, -13.83f, 306.4f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_arbusto_c", -18.89f, -25.98f, 218.9f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_flores_a", -1.84f, -2.93f, 230.7f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_flores_b", -3.62f, 2.04f, 315.3f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_flores_a", 1.91f, -9.93f, 284.9f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_flores_b", -13.23f, -7.16f, 166.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_flores_a", -11.58f, -3.28f, 273.5f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_flores_b", -13.47f, -5.07f, 329.6f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_flores_a", 5.06f, 3.72f, 357.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_flores_b", 5.98f, -5.42f, 223.9f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_alga_a", -8.00f, 1.00f, 220.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_tronco_a", 7.00f, 0.50f, 250.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_seixos", -4.00f, 3.00f, 180.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_concha_grande", 2.50f, 3.00f, 30.0f, 1.00f, 1.00f, 1.00f),
    };

    // Paredão de penhasco do fundo do print: só use se não houver penhasco do mapa atrás (aqui cai em y = 19).
    private static readonly (string f, float dx, float dy, float yaw, float sx, float sy, float sz)[] Fundo =
    {
        ("costa/costa_penhasco_a", 40.00f, -30.00f, 180.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_penhasco_b", 30.00f, -30.00f, 180.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_penhasco_a", 20.00f, -30.00f, 180.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_penhasco_b", 10.00f, -30.00f, 180.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_penhasco_a", 0.00f, -30.00f, 180.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_penhasco_b", -10.00f, -30.00f, 180.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_penhasco_a", -20.00f, -30.00f, 180.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_penhasco_b", -30.00f, -30.00f, 180.0f, 1.00f, 1.00f, 1.00f),
        ("costa/costa_penhasco_a", -40.00f, -30.00f, 180.0f, 1.00f, 1.00f, 1.00f),
    };

    public static Node3D Montar(Node3D mundo, Vector2 origem, bool comParedao = false)
    {
        var raiz = new Node3D { Name = "CenaPrintCosta" };
        mundo.AddChild(raiz);
        void Por((string f, float dx, float dy, float yaw, float sx, float sy, float sz) p)
        {
            var n = new Node3D
            {
                Position = new Vector3(origem.X + p.dx, 0f, origem.Y + p.dy),
                RotationDegrees = new Vector3(0f, p.yaw, 0f),
                Scale = new Vector3(p.sx, p.sy, p.sz),
            };
            raiz.AddChild(n);
            if (!MapKit.Glb(n, p.f, Vector3.Zero, 0f, 1f)) n.QueueFree();
        }
        foreach (var p in Pecas) Por(p);
        if (comParedao) foreach (var p in Fundo) Por(p);
        return raiz;
    }

    /// <summary>
    /// Paredão de penhasco do print (só visual): costa_penhasco_a/b alternados a cada 10 m de "de" até "ate",
    /// com a face rochosa virada para "dentro" (ponto do mapa para onde a parede olha). Usar sobre os obstáculos
    /// "penhasco" do map_costa, que já têm colisão; aqui não entra colisão nenhuma.
    /// Ex.: borda norte  ParedaoVisual(raiz, new(-112, -109), new(35, -109), new(0, 0));
    /// </summary>
    public static Node3D ParedaoVisual(Node3D mundo, Vector2 de, Vector2 ate, Vector2 dentro, float altura = 1f)
    {
        var raiz = new Node3D { Name = "ParedaoVisual" };
        mundo.AddChild(raiz);
        var d = ate - de; var n = Mathf.Max(1, Mathf.CeilToInt(d.Length() / 10f)); var passo = d / n;
        var meio = de + d / 2f; var olhar = (dentro - meio).Normalized();
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-olhar.X, -olhar.Y));          // yaw 0 = face para -Z (norte); 180 = face para +Z
        for (var i = 0; i < n; i++)
        {
            var c = de + passo * (i + 0.5f);
            var no = new Node3D
            {
                Position = new Vector3(c.X, 0f, c.Y),
                RotationDegrees = new Vector3(0f, yaw, 0f),
                Scale = new Vector3(passo.Length() / 10f, altura, 1f),
            };
            raiz.AddChild(no);
            if (!MapKit.Glb(no, "costa/costa_penhasco_" + (i % 2 == 0 ? "a" : "b"), Vector3.Zero, 0f, 1f)) no.QueueFree();
        }
        return raiz;
    }
}
