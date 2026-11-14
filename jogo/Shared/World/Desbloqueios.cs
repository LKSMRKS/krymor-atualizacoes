using System.Collections.Generic;
using System.Linq;

namespace CinzasDaMare.Shared.World;

/// <summary>
/// 12/11 (pedido do dono: "janela de próximos desbloqueios" e "o jogo inteiro é progressão de nível"): o que abre em cada nível.
/// O servidor avisa ao chegar; o mapa (M) mostra os próximos três. Fonte única (servidor e cliente).
/// </summary>
public static class Desbloqueios
{
    public static readonly IReadOnlyList<(int Nivel, string Texto)> Lista = new (int, string)[]
    {
        (5, "Galeria Funda (dungeon da Costa) e os prêmios de nível a cada 5"),
        (6, "Missões do acampamento goblin na Costa"),
        (7, "As páginas do livro no casarão da Costa"),
        (10, "Vale das Cinzas · montaria Corcel · Monte do Prefeito (ramificação do Vale)"),
        (14, "Crônica de KRYMOR (Inês, Biblioteca do Cais): cada região vencida vira um capítulo"),
        (11, "Provas de classe com o seu mentor (4 provas: caçada, troféu, memória e chefe)"),
        (15, "Evolução de classe (2ª classe, depois das 4 provas) · fim das missões do Prefeito · CASA própria com o Prefeito (15 mil)"),
        (17, "Mata dos Enforcados e a Cripta do Sino"),
        (20, "Missão secreta do Ezequiel (barqueiro do Porto): o caminho do Triângulo · Cânions dos Suplicantes · Floresta Oculta (Máscara)"),
        (25, "Montaria Lobo · companheiro Raposa"),
        (28, "Brejo da Viúva e o Mosteiro Afundado"),
        (34, "Véspera e o Mercado dos Mortos"),
        (35, "Reinos das transformações (Lua, Transilvânia, Inferno, Cidade Celestial)"),
        (38, "Ruínas dos Autômatos (ramificação da Véspera)"),
        (40, "Montaria Javali de Guerra · companheiro Coruja · Castelo da Véspera (selo)"),
        (42, "Coroa da Geada e o Salão da Geada"),
        (44, "Barco do Tibério até o Triângulo do Demônio"),
        (46, "Pico do Vento (topo da Coroa da Geada)"),
        (48, "Fundo do Triângulo (debaixo d'água) · Caminho da Serpente (só com ASAS) · Mar de Escória"),
        (54, "Ilha da Serpente Marinha (passagem secreta do Fundo)"),
        (50, "Asas da Serpente Celeste (chefe do Caminho da Serpente)"),
        (55, "Terceira classe · Acrópole · montaria Grifo · Dragão Ancião"),
        (60, "Kraken do Triângulo — chefe de mundo SEMANAL · Crônica, capítulo final"),
    };

    /// <summary>
    /// 14/11 ("item a cada poucos níveis pra sempre dar a sensação: vou jogar mais um pouco, logo libero tal coisa — casa,
    /// capa, asa, pet, armadura"): presentes que chegam sozinhos na mochila ao alcançar o nível (uma vez por personagem).
    /// Item = id de item/consumível (×quantidade); "mochila:" = +1 fileira na mochila.
    /// </summary>
    public static readonly IReadOnlyList<(int Nivel, string Item, int Qtd, string Texto)> Presentes = new (int, string, int, string)[]
    {
        (3, "consumable_c02_tonico_medio", 3, "3 Tônicos Médios"),
        (5, "capa_viajante", 1, "Capa de Viajante"),
        (7, "pet_grilo", 1, "Cri, o Grilo da Sorte (companheiro)"),
        (9, "mochila:presente9", 1, "+1 fileira na mochila"),
        (12, "capa_violeta", 1, "Capa Violeta do Arcano"),
        (14, "bracelete_chef", 1, "Bracelete do Cozinheiro (skin)"),
        (17, "consumable_c02_tonico_medio", 10, "10 Tônicos Médios"),
        (20, "mochila:presente20", 1, "+1 fileira na mochila"),
        (22, "capa_chamas", 1, "Capa de Chamas"),
        (26, "bracelete_pirata", 1, "Bracelete do Pirata (skin)"),
        (30, "pet_raposa", 1, "Fagulha, a Raposa (companheiro)"),
        (33, "mochila:presente33", 1, "+1 fileira na mochila"),
        (36, "capa_mare", 1, "Capa da Maré"),
        (40, "pet_coruja", 1, "Sábia, a Coruja (companheiro)"),
        (44, "capa_abismo", 1, "Capa do Abismo"),
        (48, "mochila:presente48", 1, "+1 fileira na mochila"),
        (52, "capa_brasa_viva", 1, "Capa de Brasa Viva"),
        (56, "bracelete_lata", 1, "Bracelete do Cavaleiro de Lata (skin)"),
    };

    private static IEnumerable<(int Nivel, string Texto)> Tudo()
        => Lista.Concat(Presentes.Select(p => (p.Nivel, "presente: " + p.Texto))).OrderBy(d => d.Nivel);

    public static IEnumerable<(int Nivel, string Texto)> Proximos(int nivel, int quantos = 3) => Tudo().Where(d => d.Nivel > nivel).Take(quantos);
    public static IEnumerable<string> No(int nivel) => Lista.Where(d => d.Nivel == nivel).Select(d => d.Texto);
}
