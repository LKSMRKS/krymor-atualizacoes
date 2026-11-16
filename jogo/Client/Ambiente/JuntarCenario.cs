using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace CinzasDaMare.Client.Ambiente;

/// <summary>
/// 16/11 (otimização, item 2 — "desenhar menos coisas separadas"): o cenário é montado com milhares de pecinhas (cada
/// caixa, bola e cilindro de um poste, cerca, lápide…) e cada uma era UMA chamada de desenho para a placa de vídeo. Aqui,
/// depois que o mapa é montado, as pecinhas PARADAS são juntadas por pedaço do mapa e por material:
///  • malha repetida (o mesmo modelo usado várias vezes, ex.: GLB de lápide, árvore, caixote) vira um MultiMesh (um
///    desenho para todas as cópias do pedaço);
///  • pecinhas únicas com o mesmo material viram uma malha só (os vértices já vão no lugar certo).
/// Nada muda no visual: mesma malha, mesmo material (o mesmo objeto de material), mesma sombra, mesma camada. A distância
/// em que as coisas pequenas somem fica igual ou um pouco MAIOR (nunca some antes).
/// Não mexe em nada que se move ou muda depois: tudo que o MapView guarda em listas (portas, baús, NPCs, portais, pedras de
/// viagem, telhados que somem…), nós com script próprio, animações, peças balançando no vento, material transparente,
/// shader, "billboard", textura em coordenada local, malha com esqueleto. Para desligar: variável KRYMOR_SEM_JUNTAR=1.
/// </summary>
public static class JuntarCenario
{
    public static bool Ligado = OS.GetEnvironment("KRYMOR_SEM_JUNTAR") != "1";
    public static string UltimoResumo = "";
    /// <summary>Por que cada malha ficou de fora (diagnóstico, vai no resumo).</summary>
    public static readonly Dictionary<string, int> Motivos = new();
    private static void Motivo(string m) => Motivos[m] = Motivos.GetValueOrDefault(m) + 1;

    private readonly record struct Peca(MeshInstance3D Mi, Transform3D Mundo, Material?[] Mats, float Classe, bool SoMulti = false);

    public static void Juntar(Node3D raiz, HashSet<Node> protegidos)
    {
        if (!Ligado) return;
        Motivos.Clear();
        var antes = 0; var pecas = new List<Peca>();
        var matsProtegidos = new HashSet<Material>();
        foreach (var p in protegidos.ToList())
            if (GodotObject.IsInstanceValid(p))
                foreach (var m in Malhas(p)) foreach (var mat in Materiais(m)) if (mat is not null) matsProtegidos.Add(mat);
        Coletar(raiz, Transform3D.Identity, protegidos, pecas, ref antes, raiz);
        if (pecas.Count < 8) { UltimoResumo = $"juntar: {pecas.Count} peças, nada a fazer"; return; }

        // materiais iguais em tudo (objetos diferentes criados com os mesmos valores) viram o mesmo objeto
        var canon = new Dictionary<string, Material>();
        var porRef = new Dictionary<Material, Material>();
        Material? Canon(Material? m)
        {
            if (m is null || matsProtegidos.Contains(m)) return m;
            if (porRef.TryGetValue(m, out var c)) return c;
            var sig = Assinatura(m);
            if (!canon.TryGetValue(sig, out c)) canon[sig] = c = m;
            porRef[m] = c;
            return c;
        }
        for (var i = 0; i < pecas.Count; i++) pecas[i] = pecas[i] with { Mats = pecas[i].Mats.Select(Canon).ToArray() };

        // malha usada 3+ vezes → MultiMesh (por pedaço); o resto → malha juntada por material. Peça só entra no "juntar" se
        // TODAS as superfícies dela podem ir (senão ficaria metade desenhada duas vezes)
        var usos = pecas.GroupBy(p => p.Mi.Mesh).ToDictionary(g => g.Key, g => g.Count());
        string Chave0(Peca p)
        {
            var cel = Celula(p.Classe); var o = p.Mundo.Origin;
            return $"{Mathf.FloorToInt(o.X / cel)}|{Mathf.FloorToInt(o.Z / cel)}|{p.Classe}|{(int)p.Mi.CastShadow}|{p.Mi.Layers}|{(int)p.Mi.GIMode}";
        }
        var multi = new Dictionary<string, List<Peca>>();
        var resto = new List<Peca>();
        foreach (var p in pecas)
        {
            if (usos[p.Mi.Mesh] >= 3 && (p.Mi.MaterialOverride is not null || SemTrocaPorSuperficie(p.Mi)))
            {
                var k = Chave0(p) + "|M" + p.Mi.Mesh.GetInstanceId() + "|" + (Canon(p.Mi.MaterialOverride)?.GetInstanceId() ?? 0);
                if (!multi.TryGetValue(k, out var l)) multi[k] = l = new();
                l.Add(p);
            }
            else if (!p.SoMulti) resto.Add(p);
            else Motivo("triplanar local sem cópias");
        }
        foreach (var l in multi.Values.Where(l => l.Count < 2).ToList()) resto.AddRange(l.Where(p => !p.SoMulti));

        var removidas = 0; var depois = 0;
        void Tirar(MeshInstance3D mi)
        {
            if (mi.GetChildCount() == 0) { mi.GetParent()?.RemoveChild(mi); mi.Free(); }
            else mi.Mesh = null;
            removidas++;
        }
        foreach (var l in multi.Values.Where(l => l.Count >= 2))
        {
            var centro = Centro(l.Select(p => p.Mundo.Origin));
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = l[0].Mi.Mesh, InstanceCount = l.Count };
            for (var i = 0; i < l.Count; i++) mm.SetInstanceTransform(i, new Transform3D(l[i].Mundo.Basis, l[i].Mundo.Origin - centro));
            var no = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = Canon(l[0].Mi.MaterialOverride), Position = centro, Name = "Juntado" };
            Copiar(l[0].Mi, no, l[0].Classe);
            raiz.AddChild(no);
            depois++;
            foreach (var p in l) Tirar(p.Mi);
        }

        var juntar = new Dictionary<string, List<(Peca P, int Sup)>>();
        var matDoGrupo = new Dictionary<string, Material?>();
        var entram = new List<Peca>();
        foreach (var p in resto)
        {
            var n = p.Mi.Mesh.GetSurfaceCount();
            var fmts = new string?[n];
            for (var sIdx = 0; sIdx < n; sIdx++) fmts[sIdx] = Formato(p.Mi.Mesh, sIdx);
            if (fmts.Any(f => f is null)) continue;
            entram.Add(p);
            for (var sIdx = 0; sIdx < n; sIdx++)
            {
                var k = Chave0(p) + "|J" + (p.Mats[sIdx]?.GetInstanceId() ?? 0) + "|" + fmts[sIdx];
                if (!juntar.TryGetValue(k, out var l)) { juntar[k] = l = new(); matDoGrupo[k] = p.Mats[sIdx]; }
                l.Add((p, sIdx));
            }
        }
        var falhou = new HashSet<MeshInstance3D>();
        var feitos = new List<(MeshInstance3D No, List<(Peca P, int Sup)> Itens)>();
        foreach (var (k, l) in juntar)
        {
            var centro = Centro(l.Select(x => x.P.Mundo.Origin));
            var malha = Mesclar(l, centro);
            if (malha is null) { foreach (var x in l) falhou.Add(x.P.Mi); continue; }
            var no = new MeshInstance3D { Mesh = malha, MaterialOverride = matDoGrupo[k], Position = centro, Name = "Juntado" };
            Copiar(l[0].P.Mi, no, l[0].P.Classe);
            feitos.Add((no, l));
        }
        // se alguma superfície de uma peça falhou, a peça inteira fica como era e sai dos grupos montados
        foreach (var (no, l) in feitos)
        {
            if (l.Any(x => falhou.Contains(x.P.Mi)))
            {
                var ok = l.Where(x => !falhou.Contains(x.P.Mi)).ToList();
                if (ok.Count == 0) { no.Free(); continue; }
                var m2 = Mesclar(ok, no.Position);
                if (m2 is null) { no.Free(); foreach (var x in ok) falhou.Add(x.P.Mi); continue; }
                no.Mesh = m2;
            }
            raiz.AddChild(no);
            depois++;
        }
        foreach (var p in entram) if (!falhou.Contains(p.Mi)) Tirar(p.Mi);
        UltimoResumo = $"juntar: {antes} malhas no cenário, {pecas.Count} paradas; {removidas} juntadas em {depois} desenhos; fora: " + string.Join(", ", Motivos.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key} {kv.Value}"));
    }

    private static float Celula(float classe) => classe switch { 45f => 8f, 95f => 16f, _ => 24f };

    private static Vector3 Centro(IEnumerable<Vector3> pts)
    {
        var l = pts.ToList();
        var c = l.Aggregate(Vector3.Zero, (a, b) => a + b) / l.Count;
        return new Vector3(c.X, 0, c.Z);
    }

    private static void Copiar(GeometryInstance3D de, GeometryInstance3D para, float classe)
    {
        para.CastShadow = de.CastShadow;
        para.Layers = de.Layers;
        para.GIMode = de.GIMode;
        if (classe > 0)
        {
            // pedaço de mapa: soma meia diagonal da célula — nada some ANTES do que sumia
            para.VisibilityRangeEnd = classe + Celula(classe) * 0.75f;
            para.VisibilityRangeEndMargin = 8f;
            para.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        }
    }

    // ---------------------------------------------------------------- coleta

    private static void Coletar(Node n, Transform3D pai, HashSet<Node> protegidos, List<Peca> saida, ref int total, Node3D raiz)
    {
        if (protegidos.Contains(n)) { var q = Malhas(n).Count(); total += q; if (q > 0) Motivos["guardado pelo mapa"] = Motivos.GetValueOrDefault("guardado pelo mapa") + q; return; }
        if (n != raiz && n.GetType().Assembly != typeof(Node).Assembly) { var q = Malhas(n).Count(); total += q; if (q > 0) Motivos["script " + n.GetType().Name] = Motivos.GetValueOrDefault("script " + n.GetType().Name) + q; return; }   // nó com script próprio (gira, bate asa…)
        if (n.HasMeta("nao_juntar")) return;
        if (n is AnimationPlayer or AnimationTree) return;
        var t = pai;
        if (n is Node3D n3)
        {
            if (!n3.Visible) { Motivo("escondido"); return; }
            if (n != raiz) t = pai * n3.Transform;
        }
        // GLB animado: o pai da AnimationPlayer inteiro fica de fora
        foreach (var ch in n.GetChildren()) if (ch is AnimationPlayer or AnimationTree) { Motivo("animado"); return; }
        if (n is MeshInstance3D mi)
        {
            total++;
            if (Serve(mi, false)) saida.Add(new Peca(mi, t, Materiais(mi), ClasseDistancia(mi)));
            // textura em coordenada local: só pode virar MultiMesh (cada cópia continua com o próprio espaço local)
            else if (Serve(mi, true)) saida.Add(new Peca(mi, t, Materiais(mi), ClasseDistancia(mi), SoMulti: true));
            else Motivo(PorQueNao(mi));
        }
        foreach (var ch in n.GetChildren()) Coletar(ch, t, protegidos, saida, ref total, raiz);
    }

    private static bool Serve(MeshInstance3D mi, bool localOk)
    {
        if (mi.Mesh is null || mi.Skin is not null || mi.Transparency > 0f || mi.MaterialOverlay is not null) return false;
        if (!mi.Skeleton.IsEmpty && mi.Skeleton.ToString() != "..") return false;
        if (mi.ExtraCullMargin > 0f || mi.IgnoreOcclusionCulling) return false;
        if (mi.VisibilityRangeBegin > 0f) return false;
        if (mi.Mesh.GetSurfaceCount() == 0) return false;
        for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
        {
            var mat = mi.GetActiveMaterial(s);
            if (mat is not null && !MaterialSimples(mat, localOk)) return false;
        }
        return true;
    }

    private static string PorQueNao(MeshInstance3D mi)
    {
        if (mi.Mesh is null) return "sem malha";
        if (mi.Transparency > 0f || mi.MaterialOverlay is not null) return "transparência/overlay no nó";
        if (mi.VisibilityRangeBegin > 0f) return "range begin";
        for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
        {
            var mat = mi.GetActiveMaterial(s);
            if (mat is null || MaterialSimples(mat)) continue;
            if (mat is not BaseMaterial3D b) return "shader " + (mat is ShaderMaterial sm ? sm.Shader?.ResourcePath ?? "código" : mat.GetClass());
            if (b.Transparency != BaseMaterial3D.TransparencyEnum.Disabled) return "material transparente";
            if (b.BillboardMode != BaseMaterial3D.BillboardModeEnum.Disabled) return "billboard";
            if (b.Uv1Triplanar && !b.Uv1WorldTriplanar) return "triplanar local";
            return "material especial";
        }
        return "outro (esqueleto/cull)";
    }

    private static bool MaterialSimples(Material m, bool localOk = false)
    {
        if (m.NextPass is not null) return false;
        if (m is not BaseMaterial3D b) return false;   // shader próprio: pode depender da posição do objeto
        if (b.Transparency != BaseMaterial3D.TransparencyEnum.Disabled) return false;
        if (b.BillboardMode != BaseMaterial3D.BillboardModeEnum.Disabled) return false;
        if (b.Uv1Triplanar && !b.Uv1WorldTriplanar && !localOk) return false;
        if (b.Grow || b.NoDepthTest || b.FixedSize || b.UsePointSize) return false;
        if (b.ProximityFadeEnabled || b.DistanceFadeMode != BaseMaterial3D.DistanceFadeModeEnum.Disabled) return false;
        return true;
    }

    private static bool SemTrocaPorSuperficie(MeshInstance3D mi)
    {
        for (var s = 0; s < mi.GetSurfaceOverrideMaterialCount(); s++) if (mi.GetSurfaceOverrideMaterial(s) is not null) return false;
        return true;
    }

    private static Material?[] Materiais(MeshInstance3D mi)
    {
        if (mi.Mesh is null) return Array.Empty<Material?>();
        var r = new Material?[mi.Mesh.GetSurfaceCount()];
        for (var s = 0; s < r.Length; s++) r[s] = mi.GetActiveMaterial(s);
        return r;
    }

    private static IEnumerable<MeshInstance3D> Malhas(Node n)
    {
        if (n is MeshInstance3D mi) yield return mi;
        foreach (var ch in n.GetChildren()) foreach (var m in Malhas(ch)) yield return m;
    }

    /// <summary>Igual ao CullByDistance do MapView (que roda depois e não mexe no que já tem distância).</summary>
    private static float ClasseDistancia(MeshInstance3D g)
    {
        if (g.VisibilityRangeEnd > 0f) return g.VisibilityRangeEnd;
        var size = g.GetAabb().Size * g.Transform.Basis.Scale;
        var big = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        return big < 1.5f ? 45f : big < 12f ? 95f : 0f;
    }

    private static readonly HashSet<string> SemComparar = new() { "resource_name", "resource_path", "resource_local_to_scene", "script", "resource_scene_unique_id" };

    private static string Assinatura(Material m)
    {
        var sb = new System.Text.StringBuilder(m.GetClass());
        foreach (var d in m.GetPropertyList())
        {
            var usage = (PropertyUsageFlags)(int)d["usage"];
            if ((usage & PropertyUsageFlags.Storage) == 0) continue;
            var nome = (string)d["name"];
            if (SemComparar.Contains(nome)) continue;
            var v = m.Get(nome);
            sb.Append('|').Append(nome).Append('=');
            sb.Append(v.VariantType == Variant.Type.Object ? (v.AsGodotObject()?.GetInstanceId() ?? 0).ToString() : v.ToString());
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- malha juntada

    private static string? Formato(Mesh mesh, int s)
    {
        if (mesh is ArrayMesh am)
        {
            if (am.SurfaceGetPrimitiveType(s) != Mesh.PrimitiveType.Triangles) return null;
            if (am.GetBlendShapeCount() > 0) return null;
            // malha grande (modelo GLB com níveis de detalhe): juntada perderia o LOD — fica como está
            var idx = am.SurfaceGetArrayIndexLen(s);
            if ((idx > 0 ? idx : am.SurfaceGetArrayLen(s)) / 3 > 1500) return null;
        }
        else if (mesh is not PrimitiveMesh) return null;
        var arr = mesh.SurfaceGetArrays(s);
        if (arr.Count == 0 || arr[(int)Mesh.ArrayType.Vertex].VariantType == Variant.Type.Nil) return null;
        if (arr[(int)Mesh.ArrayType.Bones].VariantType != Variant.Type.Nil) return null;
        var f = "";
        foreach (var (tipo, letra) in new[] { (Mesh.ArrayType.Normal, "n"), (Mesh.ArrayType.Tangent, "t"), (Mesh.ArrayType.Color, "c"), (Mesh.ArrayType.TexUV, "u"), (Mesh.ArrayType.TexUV2, "v") })
            if (arr[(int)tipo].VariantType != Variant.Type.Nil) f += letra;
        for (var c = (int)Mesh.ArrayType.Custom0; c <= (int)Mesh.ArrayType.Custom3; c++)
            if (arr[c].VariantType != Variant.Type.Nil) return null;
        return f;
    }

    private static ArrayMesh? Mesclar(List<(Peca P, int Sup)> itens, Vector3 centro)
    {
        var vs = new List<Vector3>(); var ns = new List<Vector3>(); var ts = new List<float>(); var cs = new List<Color>();
        var us = new List<Vector2>(); var u2 = new List<Vector2>(); var idx = new List<int>();
        bool temN = false, temT = false, temC = false, temU = false, temU2 = false;
        foreach (var (p, s) in itens)
        {
            var arr = p.Mi.Mesh.SurfaceGetArrays(s);
            var v = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var basis = p.Mundo.Basis;
            var t = new Transform3D(basis, p.Mundo.Origin - centro);
            var nb = basis.Inverse().Transposed();
            var espelho = basis.Determinant() < 0;
            var baseI = vs.Count;
            foreach (var x in v) vs.Add(t * x);
            if (arr[(int)Mesh.ArrayType.Normal].VariantType != Variant.Type.Nil)
            {
                temN = true;
                foreach (var n in arr[(int)Mesh.ArrayType.Normal].AsVector3Array()) ns.Add((nb * n).Normalized());
            }
            if (arr[(int)Mesh.ArrayType.Tangent].VariantType != Variant.Type.Nil)
            {
                temT = true;
                var tg = arr[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
                for (var i = 0; i + 3 < tg.Length; i += 4)
                {
                    var tv = (basis * new Vector3(tg[i], tg[i + 1], tg[i + 2])).Normalized();
                    ts.Add(tv.X); ts.Add(tv.Y); ts.Add(tv.Z); ts.Add(espelho ? -tg[i + 3] : tg[i + 3]);
                }
            }
            if (arr[(int)Mesh.ArrayType.Color].VariantType != Variant.Type.Nil) { temC = true; cs.AddRange(arr[(int)Mesh.ArrayType.Color].AsColorArray()); }
            if (arr[(int)Mesh.ArrayType.TexUV].VariantType != Variant.Type.Nil) { temU = true; us.AddRange(arr[(int)Mesh.ArrayType.TexUV].AsVector2Array()); }
            if (arr[(int)Mesh.ArrayType.TexUV2].VariantType != Variant.Type.Nil) { temU2 = true; u2.AddRange(arr[(int)Mesh.ArrayType.TexUV2].AsVector2Array()); }
            int[] ind = arr[(int)Mesh.ArrayType.Index].VariantType != Variant.Type.Nil ? arr[(int)Mesh.ArrayType.Index].AsInt32Array() : Enumerable.Range(0, v.Length).ToArray();
            for (var i = 0; i + 2 < ind.Length; i += 3)
            {
                // espelhado (escala negativa) inverte o sentido do triângulo: troca a ordem para a face continuar virada para fora
                if (espelho) { idx.Add(baseI + ind[i]); idx.Add(baseI + ind[i + 2]); idx.Add(baseI + ind[i + 1]); }
                else { idx.Add(baseI + ind[i]); idx.Add(baseI + ind[i + 1]); idx.Add(baseI + ind[i + 2]); }
            }
        }
        if (vs.Count == 0) return null;
        var a = new Godot.Collections.Array();
        a.Resize((int)Mesh.ArrayType.Max);
        a[(int)Mesh.ArrayType.Vertex] = vs.ToArray();
        if (temN && ns.Count == vs.Count) a[(int)Mesh.ArrayType.Normal] = ns.ToArray();
        if (temT && ts.Count == vs.Count * 4) a[(int)Mesh.ArrayType.Tangent] = ts.ToArray();
        if (temC && cs.Count == vs.Count) a[(int)Mesh.ArrayType.Color] = cs.ToArray();
        if (temU && us.Count == vs.Count) a[(int)Mesh.ArrayType.TexUV] = us.ToArray();
        if (temU2 && u2.Count == vs.Count) a[(int)Mesh.ArrayType.TexUV2] = u2.ToArray();
        a[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var m = new ArrayMesh();
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, a);
        return m;
    }

    // ---------------------------------------------------------------- o que NÃO pode ser juntado

    /// <summary>Tudo o que um objeto guarda em campos (listas, tuplas, objetos de apoio) e é nó da cena — com 3 níveis de
    /// profundidade. Usado com o MapView: tudo que ele guarda é porque mexe depois (abre, acende, some, balança).</summary>
    public static readonly Dictionary<string, int> PorCampo = new();
    public static HashSet<Node> NosGuardados(object dono)
    {
        var r = new HashSet<Node>();
        PorCampo.Clear();
        var vistos = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Olhar(object? o, int nivel)
        {
            if (o is null || nivel > 4) return;
            if (o is Node n) { r.Add(n); return; }
            if (o is string || o.GetType().IsPrimitive || o is Resource) return;
            if (!o.GetType().IsValueType && !vistos.Add(o)) return;
            if (o is Delegate || o is IEnumerator) return;
            if (o is IDictionary d) { foreach (DictionaryEntry e in d) { Olhar(e.Key, nivel + 1); Olhar(e.Value, nivel + 1); } return; }
            // só coleções de verdade (lista, conjunto, vetor): enumerar um "yield" executaria código (a montagem do mapa!)
            if (o is ICollection col) { foreach (var x in col) Olhar(x, nivel + 1); return; }
            if (o is IEnumerable && o.GetType().IsGenericType && o.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)))
            { foreach (var x in (IEnumerable)o) Olhar(x, nivel + 1); return; }
            if (o is IEnumerable) return;
            if (o is System.Runtime.CompilerServices.ITuple tu) { for (var i = 0; i < tu.Length; i++) Olhar(tu[i], nivel + 1); return; }
            var tipo = o.GetType();
            if (tipo.Namespace is null || !tipo.Namespace.StartsWith("CinzasDaMare") || tipo.Namespace.StartsWith("CinzasDaMare.Shared")) return;
            foreach (var f in tipo.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Olhar(f.GetValue(o), nivel + 1);
            foreach (var p in tipo.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                if (p.GetIndexParameters().Length == 0 && (typeof(Node).IsAssignableFrom(p.PropertyType) || p.PropertyType.IsInterface))
                    try { Olhar(p.GetValue(o), nivel + 1); } catch { }
        }
        foreach (var f in dono.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.Name.Contains("<Root>")) continue;   // a raiz do mapa em si
            var v = f.GetValue(dono);
            if (v is Node3D raiz && f.FieldType == typeof(Node3D) && f.Name == "Root") continue;
            var antes = r.Count;
            Olhar(v, 0);
            if (r.Count > antes) PorCampo[f.Name] = r.Count - antes;
        }
        return r;
    }
}
