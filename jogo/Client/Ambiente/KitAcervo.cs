using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 13/11 — liga o acervo do pedido do trabalhador de mapas (Assets/Models/cenario/&lt;região&gt;_*.glb, interior/deco_*.glb)
/// aos tipos que os mapas já usam ("arvore", "lapide", "rocha_lava", "coluna_quebrada"...). Cada região troca o desenho
/// de formas pelo modelo da região. Se o .glb faltar, devolve false e o desenho antigo continua (nada quebra).
/// A colisão continua vindo do servidor / do código que já existe em cada caso.
/// </summary>
public static class KitAcervo
{
    private static string[] L(string pre, string letras) { var a = new string[letras.Length]; for (var i = 0; i < letras.Length; i++) a[i] = pre + letras[i]; return a; }

    // (tema do mapa, tipo da peça) → (arquivos sorteados, chance de trocar, escala)
    private static readonly Dictionary<(string Tema, string Tipo), (string[] Arquivos, float Chance, float Escala)> Troca = new()
    {
        // Vale das Cinzas
        [("vale", "arvore")] = (L("cenario/vale_arvore_queimada_", "abc"), 0.35f, 1f),
        // Mata dos Enforcados (e Floresta Oculta / Ilha, tema "mata")
        [("mata", "lapide")] = (L("cenario/vespera_lapide_", "abcd"), 1f, 1f),
        // Cânions dos Suplicantes
        [("canion", "vagonete")] = (new[] { "cenario/canion_vagonete_tombado" }, 0.5f, 1f),
        // Brejo da Viúva
        [("brejo", "arvore")] = (new[] { "cenario/brejo_mangue" }, 0.35f, 1f),
        [("brejo", "estacas")] = (new[] { "cenario/brejo_palafita" }, 1f, 0.8f),
        // Véspera (e Castelo / Transilvânia, tema "fantasma")
        [("fantasma", "lapide")] = (L("cenario/vespera_lapide_", "abcd"), 1f, 1f),
        [("fantasma", "poste")] = (new[] { "cenario/vespera_poste_apagado" }, 1f, 1f),
        // Coroa da Geada (e Pico do Vento / Fortaleza, tema "neve")
        [("neve", "arvore")] = (L("cenario/geada_arvore_neve_", "abc"), 1f, 1f),
        [("neve", "pinheiros")] = (L("cenario/geada_arvore_neve_", "abc"), 1f, 1f),
        [("neve", "acampamento_abandonado")] = (new[] { "cenario/geada_tenda_peles" }, 1f, 1f),
        [("neve", "cristal_gelo")] = (new[] { "cenario/geada_runa_gelo" }, 0.15f, 1f),
        // Mar de Escória (e Reino do Inferno / Cidadela, tema "lava")
        [("lava", "rocha_lava")] = (L("cenario/escoria_basalto_brasa_", "abc"), 1f, 1f),
        // Acrópole das Cinzas (e Céu: Caminho da Serpente, Reino Celeste)
        [("acropole", "coluna_quebrada")] = (L("cenario/acropole_coluna_", "ab"), 1f, 1f),
        [("acropole", "estatua_bronze")] = (new[] { "cenario/acropole_estatua_juramento" }, 0.6f, 0.6f),
        [("ceu", "coluna_quebrada")] = (L("cenario/acropole_coluna_", "ab"), 1f, 1f),
        [("ceu", "estatua_bronze")] = (new[] { "cenario/acropole_estatua_juramento" }, 0.6f, 0.6f),
        // 19/11 ("verifica tudo que está fora do padrão"): as peças que ainda eram de caixas/cilindros em algum tema
        [("mata", "arvore_seca")] = (new[] { "cenario/mata_arvore_forca_a", "cenario/mata_arvore_forca_b", "cenario/vale_arvore_queimada_c" }, 1f, 1f),
        [("brejo", "arvore_seca")] = (new[] { "cenario/brejo_mangue", "cenario/mata_arvore_forca_b" }, 1f, 1f),
        [("fantasma", "arvore_seca")] = (L("cenario/vale_arvore_queimada_", "abc"), 1f, 0.9f),
        [("vale", "arvore_seca")] = (L("cenario/vale_arvore_queimada_", "abc"), 1f, 1f),
        [("canion", "arvore_seca")] = (L("cenario/vale_arvore_queimada_", "bc"), 1f, 1f),
        [("lava", "arvore_seca")] = (L("cenario/vale_arvore_queimada_", "abc"), 1f, 1f),
        [("neve", "arvore_seca")] = (L("cenario/geada_arvore_neve_", "abc"), 1f, 0.9f),
        [("lua", "arvore_seca")] = (new[] { "cenario/mata_arvore_forca_a", "cenario/vale_arvore_queimada_c" }, 1f, 1f),
        [("lua", "coluna_quebrada")] = (L("cenario/acropole_coluna_", "ab"), 1f, 1f),
        [("lua", "lapide")] = (L("cenario/vespera_lapide_", "abcd"), 1f, 1f),
        [("brejo", "lapide")] = (L("cenario/vespera_lapide_", "abcd"), 1f, 1f),
    };

    /// <summary>19/11: qualquer tema — a peça de formas antiga vira o modelo esculpido genérico.</summary>
    private static readonly Dictionary<string, (string[] Arquivos, float Escala)> TrocaGeral = new()
    {
        ["arvore_seca"] = (new[] { "cenario/cenario_arvore_seca" }, 1f),
        ["coluna_quebrada"] = (new[] { "cenario/cenario_coluna", "cenario/acropole_coluna_b" }, 1f),
        ["lapide"] = (L("cenario/vespera_lapide_", "abcd"), 1f),
        ["panela"] = (new[] { "cenario/cenario_panela" }, 1f),
        ["bancada"] = (new[] { "cenario/cenario_bancada" }, 1f),
        ["rocha_lava"] = (L("cenario/escoria_basalto_brasa_", "abc"), 1f),
    };

    /// <summary>Troca a peça pelo modelo da região. true = modelo colocado (pule o desenho antigo, mantenha a colisão).</summary>
    public static bool Peca(MapView? v, Node3D node, string tipo)
    {
        // 14/11 (u81 travava o desenho): peça montada fora de um mapa (o boneco de treino usa o mesmo construtor) não tem tema
        if (v?.Map is null) return false;
        if (!Troca.TryGetValue((v.Map.Theme, tipo), out var t))
        {
            if (v.Map.Theme is "cidade" or "biblioteca" or "casarao" || !TrocaGeral.TryGetValue(tipo, out var g)) return false;
            t = (g.Arquivos, 1f, g.Escala);
        }
        if (t.Chance < 1f && v.R(0f, 1f) > t.Chance) return false;
        var arq = t.Arquivos[System.Math.Clamp((int)v.R(0f, t.Arquivos.Length), 0, t.Arquivos.Length - 1)];
        if (!MapKit.Glb(node, arq, Vector3.Zero, 0f, t.Escala * v.R(0.9f, 1.1f))) return false;
        // a mesma colisão que o desenho antigo punha (o MapKit.TryBuild devolve logo depois desta troca)
        switch (tipo)
        {
            case "coluna_quebrada": v.Collider(node.Position + new Vector3(0, 1.5f, 0), new CylinderShape3D { Radius = 0.5f, Height = 3f }); break;
            case "rocha_lava": v.Collider(node.Position + new Vector3(0, 0.5f, 0), new SphereShape3D { Radius = 1.2f }); break;
            case "estatua_bronze": v.Collider(node.Position + new Vector3(0, 1.5f, 0), new BoxShape3D { Size = new Vector3(1.6f, 3f, 1.6f) }); break;
        }
        return true;
    }

    // decoração da casa: id do item → modelo e altura da origem (lustre pendura do teto; quadro no meio da parede)
    private static readonly Dictionary<string, (string Arquivo, float Y)> DecorGlb = new()
    {
        ["decor_tapete_real"] = ("interior/deco_tapete_real", 0f),
        ["decor_quadro_mar"] = ("interior/deco_quadro_mar", 0.4f),
        ["decor_lustre"] = ("interior/deco_lustre_cristal", 3.8f),
        ["decor_aquario"] = ("interior/deco_aquario", 0f),
        ["decor_estatua_leao"] = ("interior/deco_estatua_leao", 0f),
        ["decor_jukebox"] = ("interior/deco_jukebox", 0f),
        ["decor_trofeu"] = ("interior/deco_trofeu_chefe", 0.35f),
        ["decor_trono"] = ("interior/deco_trono", 0f),
        ["decor_estante_trofeus"] = ("interior/deco_estante_trofeus", 0f),
        ["decor_lareira"] = ("interior/movel_lareira", 0f),   // a lareira do interior (o item pede "Lareira de Pedra")
        ["decor_planta"] = ("interior/deco_vaso_samambaia", 0f),
        ["decor_bandeira"] = ("interior/deco_estandarte", 0f),
    };

    /// <summary>Decoração com modelo 3D (com a mesma luz/música da versão de formas). false = sem modelo: o CasaDecor desenha a de formas.</summary>
    public static bool Decor(Node3D n, string id, bool icone)
    {
        if (!DecorGlb.TryGetValue(id, out var d) || !MapKit.Glb(n, d.Arquivo, new Vector3(0, d.Y, 0), 0f, 1f)) return false;
        switch (id)
        {
            case "decor_lustre":
                n.AddChild(new OmniLight3D { LightColor = new Color("#ffe6b0"), LightEnergy = 1.6f, OmniRange = 9f, Position = new Vector3(0, 2.8f, 0), ShadowEnabled = false });
                break;
            case "decor_aquario":
                n.AddChild(new OmniLight3D { LightColor = new Color("#7fd8ff"), LightEnergy = 0.8f, OmniRange = 3f, Position = new Vector3(0, 1.1f, 0.5f), ShadowEnabled = false });
                break;
            case "decor_jukebox":
                n.AddChild(new OmniLight3D { LightColor = new Color("#ff9ad8"), LightEnergy = 0.7f, OmniRange = 3f, Position = new Vector3(0, 1.1f, 0.6f), ShadowEnabled = false });
                if (!icone) Sfx.Loop3D(n, "musica_porto", -6f, 3f, 16f);
                break;
            case "decor_lareira":
                n.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.25f), LightEnergy = 1.6f, OmniRange = 6f, Position = new Vector3(0, 0.6f, 0.6f) });
                break;
        }
        return true;
    }
}
