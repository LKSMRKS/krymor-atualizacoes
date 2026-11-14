using Godot;
using System.Collections.Generic;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 11/11: altura VISUAL do chão dentro dos prédios com interior (casarão).
/// O servidor continua decidindo só X/Y; o cliente coloca os pés na altura certa:
///  - piso do térreo (0,53 m — antes o boneco afundava no assoalho);
///  - degraus da entrada (rampa suave);
///  - escada interna: o boneco SOBE de verdade, degrau por degrau, até o 2º andar,
///    e a troca de mapa (portal) acontece lá em cima, sem pulo — o 2º andar já é
///    desenhado no mesmo lugar (modelo 4,45 m para baixo).
///  - no mapa do 2º andar: piso a 0,12 m e o vão da escada descendo até o térreo.
/// Medidas tiradas do gerador do casarão (Tools/arte/build_houses.py: W 16, D 12, Z0 0,4, H1 4,0,
/// escada 14 degraus de 0,25 m (era 0,31; encurtada no modelo em 11/11 noite) em x -7,35..-5,65, do z +4,05 (base) ao z +0,61 (topo), em coordenadas do modelo no Godot).
/// </summary>
public static class EscadaCasa
{
    private const float Lift = 0.06f;             // MapView.LotTwoBuilding sobe o modelo 6 cm
    private const float PisoTerreo = 0.475f;      // topo das tábuas do térreo (no modelo)
    private const float PisoAndar2 = 4.57f;       // topo das tábuas do 2º andar (no modelo)
    private const float AndarOffset = 4.45f;      // CasaraoUpstairs desce o modelo 4,45 m
    private const float EscX0 = -7.35f, EscX1 = -5.65f, EscZBase = 4.05f   /* 11/11 noite: escada mais curta e firme, 1,4 m livres até a parede da frente */, EscZTopo = 0.61f;

    private readonly struct Predio
    {
        public readonly Transform3D ParaLocal;    // mapa (x,0,y) -> coordenadas do modelo
        public readonly bool Andar2, Taverna;
        public Predio(Transform3D paraLocal, bool andar2, bool taverna = false) { ParaLocal = paraLocal; Andar2 = andar2; Taverna = taverna; }
        public float? H(float x, float z) => Taverna ? (Andar2 ? AlturaTavernaAndar2(x, z) : AlturaTaverna(x, z)) : Andar2 ? AlturaAndar2(x, z) : AlturaTerreo(x, z);
    }

    // 13/11 ("olhando para a saída, a casa da esquerda ainda não tem escada pra subir"): escada da taverna grande, feita
    // por código (o modelo não tem), encostada na parede direita: 16 degraus de x 4,3..5,5, do z +3,9 (base) ao -0,2 (topo);
    // 2º andar a 4,8 m (piso do "superior" no modelo). O mapa "taverna_andar2" põe o modelo 4,7 m para baixo.
    // 13/11 23:39 ("a escada ainda está muito perto da parede, pouquíssimo espaço para subir"): 0,9 m da parede direita e 1 m da
    // parede da frente; o corrimão só na metade de cima — dá para entrar pela lateral, vindo do salão
    public const float TEscX0 = 3.35f, TEscX1 = 4.6f, TEscZBase = 3.0f, TEscZTopo = -0.8f,
         TPiso = 0.41f, TPiso2 = 4.8f, TOffset = 4.7f;
    private const int TDegraus = 16;

    private static float? NaEscadaTaverna(float x, float z)
    {
        if (x < TEscX0 || x > TEscX1 || z > TEscZBase + 0.15f || z < TEscZTopo - 0.05f) return null;
        var t = Mathf.Clamp((TEscZBase - z) / (TEscZBase - TEscZTopo), 0f, 1f);
        var degrau = t * TDegraus;
        var liso = Mathf.Floor(degrau) + Mathf.SmoothStep(0.35f, 0.65f, degrau - Mathf.Floor(degrau));
        return Mathf.Clamp(liso / TDegraus, 0f, 1f);
    }

    private static float? AlturaTavernaAndar2(float x, float z)
    {
        if (NaEscadaTaverna(x, z) is { } t) return Mathf.Lerp(TPiso, TPiso2, t) - TOffset;
        if (x < -6.6f || x > 6.6f || z < -4.85f || z > 4.85f) return null;
        return TPiso2 - TOffset;
    }

    /// <summary>
    /// 13/11 23:39 ("tem uma outra escada dentro da casa, qual a lógica? deixa só uma"): o modelo da taverna já vinha com uma
    /// escada encostada na parede esquerda, que não leva a lugar nenhum. Tira os triângulos dela do desenho (só nesta cópia).
    /// </summary>
    public static void TirarEscadaDoModelo(Node3D modelo)
    {
        var caixa = new Aabb(new Vector3(-5.45f, 0.6f, -0.3f), new Vector3(1.2f, 3.1f, 4.05f));
        foreach (var n in modelo.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            if (n is not MeshInstance3D mi || mi.Mesh is not ArrayMesh am) continue;
            var t = Transform3D.Identity;
            for (Node? p = mi; p is Node3D p3 && p != modelo; p = p.GetParent()) t = p3.Transform * t;
            var nova = new ArrayMesh();
            var tirou = false;
            for (var s = 0; s < am.GetSurfaceCount(); s++)
            {
                var arr = am.SurfaceGetArrays(s);
                var verts = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var idx = arr[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? null : arr[(int)Mesh.ArrayType.Index].AsInt32Array();
                var tri = idx ?? System.Linq.Enumerable.Range(0, verts.Length).ToArray();
                var fica = new List<int>(tri.Length);
                for (var i = 0; i + 2 < tri.Length; i += 3)
                {
                    if (caixa.HasPoint(t * verts[tri[i]]) && caixa.HasPoint(t * verts[tri[i + 1]]) && caixa.HasPoint(t * verts[tri[i + 2]])) { tirou = true; continue; }
                    fica.Add(tri[i]); fica.Add(tri[i + 1]); fica.Add(tri[i + 2]);
                }
                arr[(int)Mesh.ArrayType.Index] = fica.ToArray();
                nova.AddSurfaceFromArrays(am.SurfaceGetPrimitiveType(s), arr);
                nova.SurfaceSetMaterial(s, am.SurfaceGetMaterial(s));
            }
            if (tirou) mi.Mesh = nova;
        }
    }

    /// <summary>Degraus de madeira da taverna (no nó do prédio; yBase = deslocamento do modelo, 0 no térreo).</summary>
    public static void ConstruirEscadaTaverna(Node3D node, float yBase = 0f)
    {
        var madeira = VisualKit.Mat(new Color(0.42f, 0.27f, 0.15f), 0.85f);
        var escura = VisualKit.Mat(new Color(0.26f, 0.16f, 0.09f), 0.9f);
        var passo = (TEscZBase - TEscZTopo) / TDegraus;
        var cx = (TEscX0 + TEscX1) / 2f;
        for (var i = 0; i < TDegraus; i++)
        {
            var topo = Lift + TPiso + (TPiso2 - TPiso) * (i + 1) / TDegraus;
            var z = TEscZBase - passo * (i + 0.5f);
            VisualKit.Part(node, VisualKit.Box(TEscX1 - TEscX0, 0.06f, passo + 0.04f), madeira, new Vector3(cx, yBase + topo - 0.03f, z));   // pisada
            VisualKit.Part(node, VisualKit.Box(TEscX1 - TEscX0, (TPiso2 - TPiso) / TDegraus, 0.03f), escura, new Vector3(cx, yBase + topo - (TPiso2 - TPiso) / TDegraus / 2f, z + passo / 2f));   // espelho
        }
        // duas vigas (banzos) por baixo e o corrimão do lado de dentro
        var comp = Mathf.Sqrt((TEscZBase - TEscZTopo) * (TEscZBase - TEscZTopo) + (TPiso2 - TPiso) * (TPiso2 - TPiso));
        var ang = Mathf.RadToDeg(Mathf.Atan2(TPiso2 - TPiso, TEscZBase - TEscZTopo));
        var meio = new Vector3(0, yBase + Lift + (TPiso + TPiso2) / 2f - 0.15f, (TEscZBase + TEscZTopo) / 2f);
        foreach (var x in new[] { TEscX0 + 0.05f, TEscX1 - 0.05f })
            VisualKit.Part(node, VisualKit.Box(0.1f, 0.3f, comp), escura, meio + new Vector3(x, 0, 0), new Vector3(ang, 0, 0));
        // corrimão só na metade de cima (a de baixo fica aberta para subir pela lateral)
        var meioCima = new Vector3(0, yBase + Lift + TPiso + (TPiso2 - TPiso) * 0.775f - 0.15f, TEscZBase - (TEscZBase - TEscZTopo) * 0.775f);
        VisualKit.Part(node, VisualKit.Box(0.06f, 0.06f, comp * 0.45f), escura, meioCima + new Vector3(TEscX0 - 0.05f, 1.05f, 0), new Vector3(ang, 0, 0));
        for (var i = 2; i <= 4; i++)
        {
            var z = TEscZBase - (TEscZBase - TEscZTopo) * i / 4f;
            var h = Lift + TPiso + (TPiso2 - TPiso) * i / 4f;
            VisualKit.Part(node, VisualKit.Box(0.06f, 1f, 0.06f), escura, new Vector3(TEscX0 - 0.05f, yBase + h + 0.5f, z));
        }
    }

    private static readonly List<Predio> Predios = new();

    /// <summary>Chamar ao montar o mapa (antes de criar os prédios).</summary>
    public static void Limpar() { Predios.Clear(); Montes.Clear(); Plataformas.Clear(); Elevacoes.Clear(); }

    // 13/11 ("rampas e elevações pelos mapas"): morros/platôs do mapa (MapLayout.Elevations). MESMA conta do TerrainShader
    // (elev_hgt): topo plano, rampa suave (smoothstep) com cantos arredondados; a maior altura vence.
    public readonly record struct Elevacao(float X, float Z, float MeiaW, float MeiaH, float H, float Rampa);
    public static readonly List<Elevacao> Elevacoes = new();
    public static void RegistrarElevacao(float x, float z, float w, float h, float altura, float rampa)
        => Elevacoes.Add(new Elevacao(x, z, w / 2f, h / 2f, altura, Mathf.Max(0.5f, rampa)));

    /// <summary>Menor altura do morro num círculo de raio r em volta do ponto (base de peça parada: nunca flutua na rampa).</summary>
    public static float ElevBase(double x, double y, double r)
    {
        var h = Elev(x, y);
        if (h <= 0f) return h;
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8f;
            h = Mathf.Min(h, Elev(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r));
        }
        return h;
    }

    /// <summary>Altura dos morros no ponto (0 fora).</summary>
    public static float Elev(double mapX, double mapY)
    {
        var best = 0f;
        foreach (var e in Elevacoes)
        {
            var dx = Mathf.Max(Mathf.Abs((float)mapX - e.X) - e.MeiaW, 0f);
            var dz = Mathf.Max(Mathf.Abs((float)mapY - e.Z) - e.MeiaH, 0f);
            var d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d >= e.Rampa) continue;
            best = Mathf.Max(best, e.H * (1f - Mathf.SmoothStep(0f, e.Rampa, d)));
        }
        return best;
    }

    // 12/11 (cais do Porto): plataforma retangular elevada no mapa (x0..x1, z0..z1) com altura h; a altura sobe em rampa de
    // rampaX0 até rampaX1 (no eixo X do mapa). Só visual: o servidor continua plano.
    private readonly record struct Plataforma(float X0, float X1, float Z0, float Z1, float H, float RampaX0, float RampaX1, float RampaZ = 0f);
    private static readonly List<Plataforma> Plataformas = new();

    public static void RegistrarPlataforma(float x0, float x1, float z0, float z1, float h, float rampaX0, float rampaX1)
        => Plataformas.Add(new Plataforma(x0, x1, z0, z1, h, rampaX0, rampaX1));

    /// <summary>12/11 ("a ponte está sem física, atravessando o chão"): tabuleiro de ponte que atravessa no eixo Z do mapa,
    /// com rampa de <paramref name="rampa"/> m nas duas cabeceiras (o boneco sobe na ponte em vez de afundar nela).</summary>
    public static void RegistrarPonte(float x0, float x1, float z0, float z1, float h, float rampa)
        => Plataformas.Add(new Plataforma(x0, x1, z0, z1, h, 0f, 0f, rampa));

    // 11/11 noite ("fazer o boneco subir e descer conforme o chão, não atravessar"): montes de areia são relevo — o
    // servidor deixa passar por cima e o cliente põe os pés na superfície do monte (meio elipsoide).
    private readonly struct Monte
    {
        public readonly Transform3D ParaLocal;
        public readonly float Ax, Az, Ay, Cy;
        public Monte(Transform3D t, float ax, float az, float ay, float cy) { ParaLocal = t; Ax = ax; Az = az; Ay = ay; Cy = cy; }
    }

    private static readonly List<Monte> Montes = new();

    /// <summary>Monte de areia: elipsoide de semieixos ax (x), ay (altura), az (z), centro na altura cy, no nó do prop.</summary>
    public static void RegistrarMonte(Node3D node, float ax, float az, float ay, float cy) =>
        Montes.Add(new Monte(new Transform3D(node.Basis, node.Position).AffineInverse(), ax, az, ay, cy));

    /// <summary>Altura de um monte no ponto (0 fora). Também serve para pôr capim na superfície.</summary>
    public static float AlturaMonte(Transform3D paraLocal, float ax, float az, float ay, float cy, Vector3 mundo)
    {
        var l = paraLocal * mundo;
        var r = (l.X / ax) * (l.X / ax) + (l.Z / az) * (l.Z / az);
        if (r >= 1f) return 0f;
        var scaleY = paraLocal.Basis.Inverse().Y.Length();
        return Mathf.Max(0f, (cy + ay * Mathf.Sqrt(1f - r)) * scaleY);
    }

    /// <summary>Dentro de prédio com assoalho (som de passo de madeira).</summary>
    public static bool NoAssoalho(double mapX, double mapY)
    {
        foreach (var p in Predios)
        {
            var l = p.ParaLocal * new Vector3((float)mapX, 0, (float)mapY);
            var h = p.H(l.X, l.Z);
            if (h.HasValue) return true;
        }
        return false;
    }

    /// <summary>Casarão no mapa de fora (térreo). node = o Node3D do prop (posição + giro do mapa).</summary>
    public static void RegistrarCasarao(Node3D node) =>
        Predios.Add(new Predio(new Transform3D(node.Basis, node.Position).AffineInverse(), false));

    /// <summary>11/11 ("uma das casas ainda está bugado o andar lá dentro"): taverna grande — assoalho a 0,41 m
    /// (medido no modelo), porta na frente (+Z) com dois degraus até z 5,8.</summary>
    public static void RegistrarTaverna(Node3D node) =>
        Predios.Add(new Predio(new Transform3D(node.Basis, node.Position).AffineInverse(), false, true));

    private static float? AlturaTaverna(float x, float z)
    {
        if (NaEscadaTaverna(x, z) is { } te) return Lift + Mathf.Lerp(TPiso, TPiso2, te);
        if (x < -6.3f || x > 6.3f || z < -4.4f || z > 5.9f) return null;
        if (z <= 4.6f) return Lift + 0.41f;
        if (x < -1.7f || x > 1.7f) return null;              // fora da porta: é a calçada
        return Lift + 0.41f * Mathf.Clamp((5.9f - z) / 1.3f, 0f, 1f);
    }

    /// <summary>Mapa "taverna_andar2" (coordenadas do mapa = do modelo da taverna).</summary>
    public static void RegistrarTavernaAndar2() => Predios.Add(new Predio(Transform3D.Identity, true, true));

    /// <summary>Mapa "casarao_andar2" (coordenadas do mapa = do modelo).</summary>
    public static void RegistrarAndar2() => Predios.Add(new Predio(Transform3D.Identity, true));

    /// <summary>Altura do chão (Y do Godot) no ponto do mapa. 0 fora dos prédios.</summary>
    public static float Altura(double mapX, double mapY)
    {
        foreach (var p in Predios)
        {
            var l = p.ParaLocal * new Vector3((float)mapX, 0, (float)mapY);
            var h = p.H(l.X, l.Z);
            if (h.HasValue) return h.Value;
        }
        var best = Elev(mapX, mapY);
        foreach (var pl in Plataformas)
        {
            var x = (float)mapX; var z = (float)mapY;
            if (x < pl.X0 || x > pl.X1 || z < pl.Z0 || z > pl.Z1) continue;
            var k = pl.RampaX1 > pl.RampaX0 ? Mathf.Clamp((x - pl.RampaX0) / (pl.RampaX1 - pl.RampaX0), 0f, 1f) : 1f;
            if (pl.RampaZ > 0f) k = Mathf.Clamp(Mathf.Min(z - pl.Z0, pl.Z1 - z) / pl.RampaZ, 0f, 1f);
            best = Mathf.Max(best, pl.H * k);
        }
        foreach (var m in Montes)
            best = Mathf.Max(best, AlturaMonte(m.ParaLocal, m.Ax, m.Az, m.Ay, m.Cy, new Vector3((float)mapX, 0, (float)mapY)));
        return best;
    }

    /// <summary>0 = base da escada, 1 = topo (null fora da escada).</summary>
    private static float? NaEscada(float x, float z)
    {
        if (x < EscX0 || x > EscX1 || z > EscZBase + 0.15f || z < EscZTopo - 0.05f) return null;
        // degrau a degrau (suavizado para o boneco não "tremer"): 14 degraus
        var t = Mathf.Clamp((EscZBase - z) / (EscZBase - EscZTopo), 0f, 1f);
        var degrau = t * 14f;
        var liso = Mathf.Floor(degrau) + Mathf.SmoothStep(0.35f, 0.65f, degrau - Mathf.Floor(degrau));
        return Mathf.Clamp(liso / 14f, 0f, 1f);
    }

    private static float? AlturaTerreo(float x, float z)
    {
        if (NaEscada(x, z) is { } t) return Lift + Mathf.Lerp(PisoTerreo, PisoAndar2, t);
        if (x < -8f || x > 8f || z < -6f || z > 7.6f) return null;
        if (z <= 6f) return Lift + PisoTerreo;
        // degraus de pedra da entrada (z 6,0 -> 7,5): rampa até o chão de fora
        return Lift + PisoTerreo * Mathf.Clamp((7.5f - z) / 1.5f, 0f, 1f);
    }

    private static float? AlturaAndar2(float x, float z)
    {
        if (NaEscada(x, z) is { } t) return Mathf.Lerp(PisoTerreo, PisoAndar2, t) - AndarOffset;
        if (x < -8f || x > 8f || z < -6f || z > 6f) return null;
        return PisoAndar2 - AndarOffset;
    }

    /// <summary>13/11 ("no mapa mostra que tem um portal na escada; não deixa no mapa"): passagem de escada (subir/descer).</summary>
    public static bool PortalDeEscada(CinzasDaMare.Shared.World.MapPortal p)
        => p.PorPorta || p.Id.EndsWith("_subir", System.StringComparison.Ordinal) || p.Id.EndsWith("_descer", System.StringComparison.Ordinal) || EhPortalDeEscada(p.X, p.Y);

    /// <summary>Portal da escada (não desenhar o arco de pedra) — perto do topo ou da base da escada de algum casarão.</summary>
    public static bool EhPortalDeEscada(double mapX, double mapY)
    {
        foreach (var p in Predios)
        {
            var l = p.ParaLocal * new Vector3((float)mapX, 0, (float)mapY);
            if (p.Taverna ? l.X > TEscX0 - 1f && l.X < TEscX1 + 1f && l.Z > TEscZTopo - 1.5f && l.Z < TEscZBase + 1.5f
                          : l.X > EscX0 - 1f && l.X < EscX1 + 1f && l.Z > EscZTopo - 1.5f && l.Z < EscZBase + 1.5f) return true;
        }
        return false;
    }
}
