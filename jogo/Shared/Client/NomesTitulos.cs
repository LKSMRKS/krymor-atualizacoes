namespace CinzasDaMare.Shared.Client;

/// <summary>
/// 15/11 (bug visto no teste: a caixa "Parabéns!" mostrava "Você ganhou um título: primeiro_sangue."): nome legível de cada título,
/// para o cliente. Cópia das tabelas do servidor (GameServer.Achievements e TitulosDeMissao); o teste NomesTitulosTests
/// falha se as duas ficarem diferentes.
/// </summary>
public static class NomesTitulos
{
    public static readonly System.Collections.Generic.Dictionary<string, (string Conquista, string Titulo)> Todos = new()
    {
        ["primeiro_sangue"] = ("Primeiro Sangue", "Novato da Costa"),
        ["cacador_chefes"] = ("Caçador de Chefes", "Caçador de Chefes"),
        ["dungeon"] = ("Desbravador", "Desbravador"),
        ["noite"] = ("Notívago", "Notívago"),
        ["runas"] = ("Mestre das Runas", "Mestre das Runas"),
        ["refino7"] = ("Mãos de Ferreiro", "Ferreiro Lendário"),
        ["excelente"] = ("Brilho Excelente", "Afortunado"),
        ["caos"] = ("Alquimista do Caos", "Alquimista"),
        ["segredo"] = ("Quebra-Paredes", "Quebra-Paredes"),
        ["voo"] = ("Asas ao Vento", "Alado"),
        ["ranking"] = ("Contra o Relógio", "Veloz"),
        ["troca"] = ("Bom de Negócio", "Mercador"),
        ["renascer"] = ("Renascido das Cinzas", "Renascido"),
        ["nivel10"] = ("Pé na Estrada", "Viajante"),
        ["nivel20"] = ("Calejado", "Calejado"),
        ["nivel30"] = ("Veterano", "Veterano"),
        ["nivel40"] = ("Temido", "Temido"),
        ["nivel50"] = ("Herói do Continente", "Herói"),
        ["nivel60"] = ("Lenda de KRYMOR", "Lenda de KRYMOR"),
        ["missoes25"] = ("Prestativo", "Prestativo"),
        ["missoes100"] = ("Braço Direito", "Braço Direito"),
        ["missoes200"] = ("Todo Mundo te Conhece", "Famoso"),
        ["mapas10"] = ("Andarilho", "Andarilho"),
        ["mapas25"] = ("Cartógrafo", "Cartógrafo"),
        ["cronica"] = ("Cronista", "Cronista de KRYMOR"),
        ["casa"] = ("Dono de Casa", "Proprietário"),
        ["anfitriao"] = ("Anfitrião", "Anfitrião"),
        ["montaria"] = ("Cavaleiro", "Cavaleiro"),
        ["evolucao"] = ("Segunda Pele", "Evoluído"),
        ["classe3"] = ("Além do Limite", "Transcendido"),
        ["x1"] = ("Duelista", "Duelista"),
        ["x1_10"] = ("Campeão da Arena", "Campeão da Arena"),
        ["escolha"] = ("Decidido", "Decidido"),
        ["forma"] = ("Metamorfo", "Metamorfo"),
        ["refino10"] = ("Mestre Ferreiro", "Mestre Ferreiro"),
        ["refino13"] = ("Mão de Deus", "Mão de Deus"),
        ["sorte"] = ("Sorte Grande", "Sortudo"),
        ["titulos"] = ("Colecionador", "Colecionador"),
        ["q_guardiao_caravana"] = ("", "Guarda de Caravana"),
        ["q_detetive"] = ("", "Detetive das Cinzas"),
        ["q_amigo_mata"] = ("", "Amigo da Mata"),
        ["q_braco_roca"] = ("", "Braço da Roça"),
        ["q_voz_mortos"] = ("", "Voz dos Mortos"),
        ["q_juiz_vespera"] = ("", "Juiz de Véspera"),
        ["q_forja_livre"] = ("", "Forja Livre"),
        ["q_forja_guilda"] = ("", "Martelo da Guilda"),
        ["q_noturno"] = ("", "Caçador Noturno"),
        ["q_intocavel"] = ("", "Intocável"),
        ["q_mensageiro"] = ("", "Mensageiro do Continente"),
    };

    /// <summary>Texto da caixa: "Você ganhou o título «Novato da Costa» (conquista Primeiro Sangue)." Id desconhecido: o id sem "_".</summary>
    public static string Texto(string id)
    {
        if (!Todos.TryGetValue(id, out var t)) return $"Você ganhou um título: {id.Replace('_', ' ')}.";
        return t.Conquista.Length > 0 ? $"Conquista: {t.Conquista}!\nNovo título: «{t.Titulo}»." : $"Você ganhou o título «{t.Titulo}».";
    }
}
