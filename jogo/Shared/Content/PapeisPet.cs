using System.Collections.Generic;

namespace CinzasDaMare.Shared.Content;

/// <summary>18/11 17h: papel e poder ativo de cada pet (servidor: GameServer.Pets.cs; cliente: ficha do item).</summary>
public sealed record PapelPet(string Papel, string Poder, string Texto, int RecargaMs, double Forca);

public static class PapeisPet
{
    public static readonly Dictionary<string, PapelPet> Tabela = new()
    {
        ["grilo"] = new("ataque", "Salto do Cri", "pula no inimigo", 1600, 0.05),
        ["tartaruga"] = new("escudo", "Casco Protetor", "escudo que absorve dano", 9000, 0.06),
        ["vagalume"] = new("cura", "Luz que Cura", "cura você aos poucos", 5000, 0.03),
        ["macaco"] = new("lentidao", "Casca de Banana", "deixa o inimigo lento", 4000, 0.6),
        ["aguia"] = new("ataque", "Mergulho da Rajada", "mergulha no inimigo", 1800, 0.06),
        ["coelho"] = new("pressa", "Pique do Pipoca", "você corre mais rápido por instantes", 8000, 0.25),
        ["morcego"] = new("recurso", "Gole de Energia", "devolve mana/fúria/fé/foco", 5000, 0.06),
        ["lobinho"] = new("ataque", "Mordida do Uivo", "morde forte", 1700, 0.07),
        ["fenix"] = new("cura", "Brasa que Renasce", "cura forte", 5000, 0.05),
        ["sapinho"] = new("lentidao", "Língua Grudenta", "gruda e deixa lento", 4000, 0.6),
        ["raposa"] = new("quebra", "Mordida no Escudo", "quebra a armadura do inimigo", 5000, 0.25),
        ["dragao"] = new("ataque", "Sopro do Draco", "sopra fogo", 1600, 0.07),
        ["corvo"] = new("quebra", "Bicada na Armadura", "fura a armadura e marca o inimigo", 5000, 0.3),
        ["coruja"] = new("recurso", "Sabedoria da Coruja", "devolve recurso", 4500, 0.08),
        ["dragao_anciao"] = new("ataque", "Fogo Ancestral", "fogo de dragão antigo", 1500, 0.08),
    };


    public static string Texto(string kind)
        => Tabela.TryGetValue(kind, out var p)
            ? $"{NomePapel(p.Papel)}: {p.Poder} — {p.Texto} (a cada {p.RecargaMs / 1000.0:0.#} s em combate)"
            : "";

    public static string NomePapel(string papel) => papel switch
    {
        "ataque" => "Ataque", "cura" => "Cura", "escudo" => "Proteção", "recurso" => "Recurso", "pressa" => "Velocidade",
        "lentidao" => "Lentidão", "quebra" => "Quebra-escudo", _ => papel,
    };
}
