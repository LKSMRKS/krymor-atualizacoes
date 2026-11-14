using System;

namespace CinzasDaMare.Shared.Content;

/// <summary>
/// 12/11 (pedido do dono: "todos os monstros tenham tipo — aquático, pedra, fantasma — para vantagens e desvantagens depois"):
/// natureza de cada monstro, deduzida do visual/nome (sem mexer nos JSON). 12/11: vale no dano — cada classe tem duas
/// naturezas fortes (+15%) e uma fraca (−15%); ver <see cref="Vantagem"/> e as bibliotecas.
/// </summary>
public static class TipoMonstro
{
    public static readonly string[] Todos = { "Aquático", "Pedra", "Fantasma", "Morto-vivo", "Fera", "Humanoide", "Inseto", "Planta", "Fogo", "Gelo", "Demônio", "Celeste", "Arcano" };

    public static string Of(MonsterDefinition m) => Of(m.Id, m.VisualAssetId, m.SpeciesName);

    public static string Of(string id, string visual, string nome)
    {
        var v = (visual + " " + id + " " + nome).ToLowerInvariant();
        bool Has(params string[] k) { foreach (var x in k) if (v.Contains(x, StringComparison.Ordinal)) return true; return false; }
        if (Has("fantasma", "ghost", "espectr", "sineiro")) return "Fantasma";
        if (Has("geode", "geodo", "stone", "golem", "gargul", "sentinel", "bronze", "de_pedra", "de pedra", "estalagm", "gesso", "chimera", "quimera")) return "Pedra";
        if (Has("crab", "carangue", "frog", "sapo", "serpent", "leech", "sanguess", "drowned", "afogado", "basilisk", "maré")) return "Aquático";
        if (Has("skeleton", "esquelet", "zombie", "zumbi", "mummy", "múmia", "ghoul", "carni", "necro", "draugr", "knight", "cavaleiro", "cobrador", "oco")) return "Morto-vivo";
        if (Has("charred", "carboniz", "elemental", "brasa", "lizard", "salamandra", "dragon", "dragão", "escória")) return "Fogo";
        if (Has("spider", "aranha", "tecel", "scorpion", "escorpi", "beetle", "besouro", "worm", "verme", "larva", "inseto", "slime", "meleca", "rastejante")) return "Inseto";
        if (Has("ice", "gelo", "neve", "giant", "jotunn", "wendigo", "geada")) return "Gelo";
        if (Has("demon", "demônio", "death", "a_morte", "vampir", "chupa", "cerberus", "três cabeças")) return "Demônio";
        if (Has("angel", "anjo", "harpy", "harpia", "centaur", "minotaur", "genie", "gênio")) return "Celeste";
        if (Has("plant", "planta", "root", "raiz", "boca-de-lodo", "espantalho")) return "Planta";
        if (Has("wolf", "lobo", "boar", "javali", "rat", "rato", "bat", "morcego", "dog", "cão", "werewolf", "lobisomem", "troll", "ogre", "ogro")) return "Fera";
        if (Has("eye", "olho", "acolyte", "acólito", "acolito", "saci", "alien", "visitante")) return "Arcano";
        return "Humanoide";
    }

    /// <summary>
    /// 12/11 ("tipos de monstro passam a valer"): multiplicador de dano da classe contra a natureza.
    /// Guerreiro (corte e pancada): forte contra Fera e Humanoide, fraco contra Fantasma (a lâmina atravessa).
    /// Mago (fogo e arcano): forte contra Gelo e Planta, fraco contra Fogo.
    /// Bruxa (ervas, maldição e luz): forte contra Morto-vivo e Demônio, fraca contra Arcano.
    /// Arqueiro (flecha): forte contra Inseto e Celeste (voadores), fraco contra Pedra.
    /// </summary>
    public static double Vantagem(string? classe, string tipo) => (classe, tipo) switch
    {
        ("guerreiro", "Fera" or "Humanoide") => 1.15, ("guerreiro", "Fantasma") => 0.85,
        ("mago", "Gelo" or "Planta") => 1.15, ("mago", "Fogo") => 0.85,
        ("bruxa", "Morto-vivo" or "Demônio") => 1.15, ("bruxa", "Arcano") => 0.85,
        ("arqueiro", "Inseto" or "Celeste") => 1.15, ("arqueiro", "Pedra") => 0.85,
        _ => 1.0,
    };

    /// <summary>Texto curto para as bibliotecas/livro dos monstros.</summary>
    public static string Dica(string tipo) => tipo switch
    {
        "Fera" or "Humanoide" => "Guerreiro +15%", "Fantasma" => "Guerreiro −15%",
        "Gelo" => "Mago +15%", "Planta" => "Mago +15%", "Fogo" => "Mago −15%",
        "Morto-vivo" or "Demônio" => "Bruxa +15%", "Arcano" => "Bruxa −15%",
        "Inseto" or "Celeste" => "Arqueiro +15%", "Pedra" => "Arqueiro −15%",
        _ => "",
    };
}
