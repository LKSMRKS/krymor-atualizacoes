using System;
using CinzasDaMare.Client.Visuals;
using Godot;

namespace CinzasDaMare.Client.Ambiente;

/// <summary>
/// 12/11 (pedido: "um dos lados da ilha do Porto ser literalmente um porto, o horizonte vira mar infinito, umas pedras e um
/// NPC só para conversar"). Lado LESTE do map_porto, pela abertura da muralha (x = 30):
///  - cais de pedra elevado (0,9 m acima da água) com rampa na saída da muralha e dois piers de madeira;
///  - mar até o horizonte (MarDaCosta), rochas no mar, barcos amarrados, barris, caixotes, redes e lanternas;
///  - Ezequiel, o Barqueiro (map_porto.json), na ponta do pier sul — futuramente leva ao Triângulo do Demônio.
/// Limites (servidor): paredes invisíveis no map_porto (borda d'água com as duas aberturas dos piers).
/// Também monta os ARREDORES de fora da muralha norte e oeste (árvores, rochas, acampamento) — só visual, ninguém passa.
/// Coordenadas = do mapa (Godot x = x, z = y).
/// </summary>
public static class CaisDoPorto
{
    public const float Altura = 0.9f;                      // topo do cais e dos piers (a água fica em 0)
    private const float X0 = 30.4f, XRampa = 33f, XBorda = 44.3f, Meia = 16.3f;
    private static readonly (float X0, float X1, float Z)[] Piers = { (44.3f, 52.3f, -9f), (44.3f, 50.3f, 8f) };

    /// <summary>Chamar logo depois do EscadaCasa.Limpar (antes dos NPCs), para pés e NPCs ficarem em cima do cais.</summary>
    public static void RegistrarAlturas()
    {
        EscadaCasa.RegistrarPlataforma(X0, XBorda + 0.2f, -Meia, Meia, Altura, X0, XRampa);
        foreach (var p in Piers) EscadaCasa.RegistrarPlataforma(p.X0, p.X1, p.Z - 1.6f, p.Z + 1.6f, Altura, 0f, 0f);
    }

    public static void Montar(Node3D mundo, float metadeDoMapa)
    {
        var raiz = new Node3D { Name = "CaisDoPorto" };
        mundo.AddChild(raiz);
        // mar: linha d'água na borda do cais (x = 44,3), sem a faixa de areia molhada (é cais, não praia)
        MarDaCosta.Construir(raiz, metadeDoMapa, "leste", recuo: metadeDoMapa - XBorda, faixa: 1f);

        var pedra = Tex("gen_calcamento.png", new Color(0.78f, 0.76f, 0.72f), 0.9f, 0.25f);
        var borda = Tex("gen_calcamento.png", new Color(0.62f, 0.6f, 0.58f), 0.85f, 1f);
        var madeira = Tex("gen_madeira.png", new Color(0.55f, 0.42f, 0.3f), 0.9f, 1f);
        // bloco do cais (topo em 0,9; desce 2,4 m para dentro d'água) + rampa saindo da muralha
        var comp = XBorda - XRampa;
        VisualKit.Part(raiz, VisualKit.Box(comp, 2.4f, Meia * 2f), pedra, new Vector3(XRampa + comp / 2f, Altura - 1.2f, 0));
        var rampaLen = XRampa - X0;
        var ang = Mathf.Atan2(Altura, rampaLen);
        VisualKit.Part(raiz, VisualKit.Box(rampaLen / Mathf.Cos(ang) + 0.1f, 0.3f, 8.6f), pedra,
            new Vector3(X0 + rampaLen / 2f, Altura / 2f - 0.15f, 0), new Vector3(0, 0, Mathf.RadToDeg(ang)));
        // pedras de arremate na beira d'água e nas laterais
        VisualKit.Part(raiz, VisualKit.Box(0.7f, 0.25f, Meia * 2f), borda, new Vector3(XBorda - 0.35f, Altura + 0.1f, 0));
        foreach (var z in new[] { -Meia + 0.35f, Meia - 0.35f })
            VisualKit.Part(raiz, VisualKit.Box(comp, 0.45f, 0.7f), borda, new Vector3(XRampa + comp / 2f, Altura + 0.2f, z));

        // piers: módulo cenario_cais (8 m, tábua a 1,2 m no modelo) baixado para a tábua ficar em 0,9
        foreach (var p in Piers)
        {
            var len = p.X1 - p.X0;
            var n = new Node3D { Position = new Vector3((p.X0 + p.X1) / 2f, Altura - 1.2f, p.Z), Scale = new Vector3(len / 8f, 1f, 1f) };
            raiz.AddChild(n);
            if (!MapKit.Glb(n, "cenario_cais", Vector3.Zero, 0f, 1f))
            {
                VisualKit.Part(raiz, VisualKit.Box(len, 0.2f, 3.2f), madeira, new Vector3((p.X0 + p.X1) / 2f, Altura - 0.1f, p.Z));
                for (var x = p.X0 + 1f; x < p.X1; x += 2f)
                    foreach (var dz in new[] { -1.4f, 1.4f })
                        VisualKit.Part(raiz, VisualKit.Cylinder(0.12f, 0.14f, 3f, 6), madeira, new Vector3(x, Altura - 1.5f, p.Z + dz));
            }
        }

        // cabeços de amarração e lanternas ao longo da beira
        var ferro = VisualKit.Mat(new Color(0.16f, 0.15f, 0.15f), 0.5f, 0.6f);
        for (var z = -13f; z <= 13f; z += 6.5f)
        {
            VisualKit.Part(raiz, VisualKit.Cylinder(0.16f, 0.2f, 0.55f, 10), ferro, new Vector3(XBorda - 0.9f, Altura + 0.27f, z));
            VisualKit.Part(raiz, VisualKit.Sphere(0.2f), ferro, new Vector3(XBorda - 0.9f, Altura + 0.58f, z));
        }
        foreach (var z in new[] { -14.5f, -3.5f, 3.5f, 14.5f })
        {
            MapKit.Glb(raiz, "cenario_lanterna_rua", new Vector3(XBorda - 1.6f, Altura, z), -90f, 1f);
            raiz.AddChild(new OmniLight3D { Position = new Vector3(XBorda - 1.6f, Altura + 3f, z), LightColor = new Color(1f, 0.75f, 0.4f), LightEnergy = 1.2f, OmniRange = 9f, ShadowEnabled = false });
        }

        // carga no cais: barris, caixotes, redes
        void Por(string f, float x, float z, float yaw, float esc = 1f) => MapKit.Glb(raiz, f, new Vector3(x, Altura, z), yaw, esc);
        Por("cenario_barril", 36.2f, -13.6f, 0f); Por("cenario_barril", 37.1f, -14.2f, 40f); Por("cenario_barril_v2", 36.6f, -12.7f, 10f);
        Por("cenario_caixote", 39.5f, -14.1f, 15f); Por("cenario_caixote", 40.4f, -14.0f, -10f); Por("cenario_caixote", 39.9f, -14.0f, 5f, 0.9f);
        Por("cenario/cenario_redes", 40.5f, 12.8f, 180f); Por("cenario/cenario_redes", 36.5f, 13.2f, 170f);
        Por("cenario_barril", 34.2f, 12.9f, 0f); Por("cenario_caixote", 42.6f, -5.2f, 30f);
        Por("cenario_caixote", 51.0f, 6.9f, 0f, 0.8f); Por("cenario_barril", 49.6f, 9.1f, 0f, 0.9f);

        // barcos amarrados ao lado dos piers (balançam na água)
        var balanca = new Node3D { Name = "Barcos" };
        balanca.SetMeta("nao_juntar", true);   // 16/11: os barcos balançam (tween); fora do JuntarCenario
        raiz.AddChild(balanca);
        foreach (var (x, z, yaw) in new[] { (48.5f, -12.6f, 90f), (47.2f, 11.7f, 95f), (55.5f, 1.5f, 80f) })
        {
            var b = new Node3D { Position = new Vector3(x, -0.15f, z), RotationDegrees = new Vector3(0, yaw, 0) };
            balanca.AddChild(b);
            MapKit.Glb(b, "cenario/cenario_barco_encalhado", Vector3.Zero, 0f, 1.25f);
            var tw = b.CreateTween().SetLoops();
            var fase = (x + z) * 0.1f;
            tw.TweenProperty(b, "rotation_degrees", new Vector3(2.5f, yaw, 1.5f + fase % 1f), 2.6).SetTrans(Tween.TransitionType.Sine);
            tw.TweenProperty(b, "rotation_degrees", new Vector3(-2.5f, yaw, -1.5f), 2.6).SetTrans(Tween.TransitionType.Sine);
        }

        // rochas no mar (perto e longe) — o horizonte do céu já é mar com ilhas
        var rng = new RandomNumberGenerator { Seed = 7331 };
        foreach (var (x, z, esc) in new[] { (60f, -22f, 3.2f), (66f, 18f, 4.5f), (78f, -6f, 6f), (92f, 30f, 8f), (104f, -34f, 9f), (130f, 8f, 14f), (58f, 34f, 2.6f) })
            MapKit.Glb(raiz, "costa/costa_rocha_" + "abc"[rng.RandiRange(0, 2)], new Vector3(x, -0.4f, z), rng.RandfRange(0, 360), esc);

        MontarArredores(raiz, metadeDoMapa);
        return;
    }

    /// <summary>
    /// 12/11 ("colocar mais coisas nas outras 2 saídas"): de fora das muralhas norte e oeste — pinheiros, arbustos, rochas, um
    /// acampamento de pescadores com tenda e fogueira, cerca quebrada e barcos virados. Só visual (ninguém sai por ali).
    /// </summary>
    private static void MontarArredores(Node3D raiz, float metade)
    {
        var arr = new Node3D { Name = "Arredores" };
        raiz.AddChild(arr);
        var rng = new RandomNumberGenerator { Seed = 4242 };
        void Por(string f, float x, float z, float esc = 1f) => MapKit.Glb(arr, f, new Vector3(x, 0f, z), rng.RandfRange(0, 360), esc);
        // faixa norte (y 31..52) e oeste (x -52..-31): mata baixa e rochas, mais densa longe do muro
        for (var i = 0; i < 46; i++)
        {
            var norte = i % 2 == 0;
            var along = rng.RandfRange(-50f, 50f);
            var fora = rng.RandfRange(33f, metade - 1f);
            var (x, z) = norte ? (along, fora) : (-fora, along);
            if (!norte && z < -27f && z > -36f) continue;   // não tampa o portão sul
            var r = rng.Randf();
            if (r < 0.45f) Por("costa/costa_pinheiro_" + "abc"[rng.RandiRange(0, 2)], x, z, rng.RandfRange(0.9f, 1.3f));
            else if (r < 0.75f) Por("costa/costa_arbusto_" + "abc"[rng.RandiRange(0, 2)], x, z, rng.RandfRange(1f, 1.6f));
            else Por("costa/costa_rocha_" + "abc"[rng.RandiRange(0, 2)], x, z, rng.RandfRange(1f, 2.4f));
        }
        // acampamento de pescadores (norte) e barcos virados/cerca (oeste)
        MapKit.Glb(arr, "cenario/cenario_tenda_lona", new Vector3(-6f, 0, 37f), 200f, 1f);
        MapKit.Glb(arr, "cenario/cenario_acampamento_abandonado", new Vector3(4f, 0, 38.5f), 160f, 1f);
        MapKit.Glb(arr, "cenario_fogueira", new Vector3(-1f, 0, 36f), 0f, 1f);
        arr.AddChild(new OmniLight3D { Position = new Vector3(-1f, 1.2f, 36f), LightColor = new Color(1f, 0.6f, 0.25f), LightEnergy = 1.6f, OmniRange = 10f, ShadowEnabled = false });
        MapKit.Glb(arr, "cenario/cenario_redes", new Vector3(9f, 0, 35.5f), 190f, 1f);
        MapKit.Glb(arr, "cenario/cenario_barco_encalhado", new Vector3(-38f, 0, -12f), 70f, 1.2f);
        MapKit.Glb(arr, "cenario/cenario_barco_encalhado", new Vector3(-41f, 0, 6f), 120f, 1.1f);
        MapKit.Glb(arr, "cenario/cenario_cerca_quebrada", new Vector3(-35f, 0, 18f), 90f, 1f);
        MapKit.Glb(arr, "cenario/cenario_cerca_quebrada", new Vector3(-35f, 0, 24f), 95f, 1f);
        MapKit.Glb(arr, "cenario/cenario_espantalho", new Vector3(-40f, 0, 21f), 80f, 1f);
        MapKit.Glb(arr, "cenario/cenario_feno", new Vector3(-38.5f, 0, 26f), 10f, 1f);
    }

    private static StandardMaterial3D Tex(string tex, Color tint, float rough, float uv)
    {
        var m = VisualKit.Mat(tint, rough);
        if (VisualAssets.Load(tex) is { } t) { m.AlbedoTexture = t; m.Uv1Triplanar = true; m.Uv1Scale = Vector3.One * uv; }
        return m;
    }
}
