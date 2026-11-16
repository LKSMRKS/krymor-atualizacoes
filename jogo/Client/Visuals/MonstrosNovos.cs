using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Catálogo dos monstros refeitos SEM EMENDA (pedido do dono 14/11: "goblin parecendo de balão, braço com emenda").
/// Lido de <c>res://Assets/Models/monstros/catalogo.json</c>; cada entrada troca a aparência de um <c>visual_asset_id</c>
/// por um .glb pronto, sem mexer em regra nenhuma. Um monstro novo entra só com o .glb + uma linha no catálogo.
/// <code>
/// { "goblin": { "arquivo": "monstros/goblin", "altura": 1.23, "tipo": "humanoide",
///               "variantes": ["monstros/goblin_sangue", "monstros/goblin_orelha"], "variacao_altura": 0.05 },
///   "wolf":   { "arquivo": "monstros/wolf", "altura": 1.0, "tipo": "criatura", "escala": 1.0, "chefe": 1.4 } }
/// </code>
/// tipo "humanoide" = esqueleto do manequim (usa as poses de ataque/guarda/morte dele);
/// tipo "criatura" = .glb com clipes próprios (idle, walk, attack_windup/strike/recover, hit, death).
/// "variantes" (14/11, dono: "uns diferentes, um com sangue, outro com orelha cortada, coisas sutis"): cada monstro
/// escolhe pela sua semente um dos arquivos (o "arquivo" conta como um deles) e ganha ±"variacao_altura" de tamanho —
/// o mesmo monstro sempre do mesmo jeito; o chefe usa sempre o "arquivo". Falta o arquivo = volta ao visual antigo.
/// Gancho (uma linha, no começo de <c>VisualFactory.Monster</c>):
/// <c>if (MonstrosNovos.Criar(visual, boss, seed) is { } novo) return novo;</c>
/// </summary>
public static class MonstrosNovos
{
    private const string Caminho = "res://Assets/Models/monstros/catalogo.json";

    private sealed record Entrada(string Arquivo, float Altura, string Tipo, float Escala, float Chefe, string[] Variantes, float VariacaoAltura,
        float ManterAntigo, bool ChefeAntigo, string[] AntigoPara, string ArquivoChefe, Dictionary<string, string> PorDefinicao);

    private static Dictionary<string, Entrada>? _catalogo;

    public static IEntityVisual? Criar(string visual, bool boss, string seed = "", string definitionId = "")
    {
        if (string.IsNullOrEmpty(visual)) return null;
        var cat = _catalogo ??= Ler();
        if (!cat.TryGetValue(visual, out var e)) return null;
        // 15/11: "por_definicao": {"id_do_monstro": "outra_entrada"} — o mesmo visual_asset_id com modelo próprio para uma
        // definição (ex.: coruja e gavião usavam o "bat"), sem mexer no conteúdo gerado. Falta o arquivo = segue no original.
        if (definitionId.Length > 0 && e.PorDefinicao.TryGetValue(definitionId, out var outra) && cat.TryGetValue(outra, out var e2)
            && Godot.FileAccess.FileExists("res://Assets/Models/" + e2.Arquivo + ".glb")) e = e2;
        var h = Hash(seed.Length > 0 ? seed : visual);
        // 15/11 (dono: "pode manter os 2, como variações"): onde já havia modelo do dono, parte dos monstros fica com ele —
        // "manter_antigo" = fração que usa o modelo antigo; chefe e "antigo_para" (ids) sempre o antigo. Retornar null = caminho antigo.
        if (boss && e.ChefeAntigo) return null;
        if (definitionId.Length > 0 && Array.IndexOf(e.AntigoPara, definitionId) >= 0) return null;
        if (e.ManterAntigo > 0f && (Misturar(h) % 1000u) / 1000f < e.ManterAntigo) return null;
        var arquivo = e.Arquivo;
        // 15/11: "arquivo_chefe" = modelo próprio do chefe (ex.: Carvalho Enforcado usa "root" e ganha o root_boss)
        if (boss && e.ArquivoChefe.Length > 0 && Godot.FileAccess.FileExists("res://Assets/Models/" + e.ArquivoChefe + ".glb")) arquivo = e.ArquivoChefe;
        if (!boss && e.Variantes.Length > 0)
        {
            var i = (int)(h % (uint)(e.Variantes.Length + 1));
            if (i > 0 && Godot.FileAccess.FileExists("res://Assets/Models/" + e.Variantes[i - 1] + ".glb")) arquivo = e.Variantes[i - 1];
        }
        if (!Godot.FileAccess.FileExists("res://Assets/Models/" + arquivo + ".glb")) return null;
        var mult = boss ? e.Chefe : 1f + e.VariacaoAltura * (((h >> 8) % 1000u) / 500f - 1f);
        try
        {
            return e.Tipo == "humanoide"
                ? MannequinVisual.MonsterBody(arquivo, e.Altura * mult)
                : new GlbCreatureVisual(arquivo, e.Altura, e.Escala * mult);
        }
        catch (Exception ex)
        {
            GD.PrintErr("monstro novo " + visual + ": " + ex.Message);
            return null;
        }
    }

    /// <summary>Espalha os bits do hash (ids em sequência davam metades tortas, ex.: 31% em vez de 50%).</summary>
    private static uint Misturar(uint x)
    {
        unchecked { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16; }
        return x;
    }

    private static uint Hash(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) { h ^= c; h *= 16777619u; }
        return h;
    }

    private static Dictionary<string, Entrada> Ler()
    {
        var d = new Dictionary<string, Entrada>(StringComparer.Ordinal);
        if (!Godot.FileAccess.FileExists(Caminho)) return d;
        try
        {
            using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(Caminho));
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var o = p.Value;
                string S(string k, string padrao) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : padrao;
                float F(string k, float padrao) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : padrao;
                var arq = S("arquivo", "");
                if (arq.Length == 0) continue;
                var vars = new List<string>();
                if (o.TryGetProperty("variantes", out var va) && va.ValueKind == JsonValueKind.Array)
                    foreach (var x in va.EnumerateArray()) if (x.ValueKind == JsonValueKind.String) vars.Add(x.GetString()!);
                var antigos = new List<string>();
                if (o.TryGetProperty("antigo_para", out var ap) && ap.ValueKind == JsonValueKind.Array)
                    foreach (var x in ap.EnumerateArray()) if (x.ValueKind == JsonValueKind.String) antigos.Add(x.GetString()!);
                var manter = F("manter_antigo", 0f);
                var porDef = new Dictionary<string, string>(StringComparer.Ordinal);
                if (o.TryGetProperty("por_definicao", out var pd) && pd.ValueKind == JsonValueKind.Object)
                    foreach (var x in pd.EnumerateObject()) if (x.Value.ValueKind == JsonValueKind.String) porDef[x.Name] = x.Value.GetString()!;
                var chefeAntigo = o.TryGetProperty("chefe_antigo", out var ca) ? ca.ValueKind == JsonValueKind.True : manter > 0f;
                d[p.Name] = new Entrada(arq, F("altura", 1.6f), S("tipo", "criatura"), F("escala", 1f), F("chefe", 1.4f), vars.ToArray(), F("variacao_altura", 0.04f),
                    manter, chefeAntigo, antigos.ToArray(), S("arquivo_chefe", ""), porDef);
            }
        }
        catch (Exception ex) { GD.PrintErr("catalogo de monstros: " + ex.Message); }
        return d;
    }
}
