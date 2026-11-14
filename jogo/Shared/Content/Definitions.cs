namespace CinzasDaMare.Shared;

// Definições imutáveis de conteúdo (GDD 17.4). Nomes JSON em snake_case.
// Desvios do contrato do GDD estão registrados em Docs/STATUS.md.

public sealed record ClassDefinition(
    string Id,
    string DisplayName,
    string PrimaryAttribute,
    string Resource,
    double ResourceMax,
    double ResourceRegenCombat,
    double ResourceRegenIdle)
{
    // E8 (opcionais): texto do lobby, atributos e itens iniciais para personagens criados no lobby.
    public string Description { get; init; } = "";
    public string ResourceName { get; init; } = "Fôlego";
    public int StartingPrimary { get; init; } = 10;
    public int StartingVigor { get; init; } = 10;
    public IReadOnlyList<string> StarterEquipment { get; init; } = Array.Empty<string>();
    /// <summary>Habilidades por botão: basic (clique), skill1 (Q), skill2 (E).</summary>
    public IReadOnlyDictionary<string, string> Loadout { get; init; } = new Dictionary<string, string>();
    // E9
    /// <summary>Ataque básico sem arma (soco, faísca): gratuito e fraco. O servidor troca o clique por ele sem arma.</summary>
    public string? UnarmedBasic { get; init; }
    /// <summary>Nome do atributo principal no HUD (Força, Sintonia).</summary>
    public string PrimaryName { get; init; } = "Força";
    /// <summary>Nível de quem é criado no lobby (0 = o do balance.json, usado também pelo login antigo).</summary>
    public int StartingLevel { get; init; }
    /// <summary>Evolução (E9): nome novo, marca que a libera (dada pela missão do mentor) e habilidade nova (tecla T).</summary>
    public string? EvolvedName { get; init; }
    public string? EvolutionFlag { get; init; }
    public string? EvolutionSkill { get; init; }
    /// <summary>Habilidade própria da classe na tecla R quando a arma equipada não traz uma (Bruxa: Aura Primal; Arqueiro: Olhar do Caçador).</summary>
    public string? InnateSkill { get; init; }
    /// <summary>Corpo do boneco no cliente: "masc" ou "fem" (só aparência).</summary>
    public string Body { get; init; } = "masc";
}

public sealed record SkillDefinition(
    string Id,
    string ClassId,
    string DisplayName,
    int UnlockLevel,
    string TargetMode,
    double Range,
    double Radius,
    int CooldownMs,
    double ResourceCost,
    double ResourceGenerated,
    double Coefficient,
    IReadOnlyDictionary<string, double> Effects,
    int CastMs,
    int RecoveryMs)
{
    /// <summary>Texto do poder (Livro de Poderes); vazio = descrição automática pelo modo de alvo.</summary>
    public string Description { get; init; } = "";
}

public sealed record ItemDefinition(
    string Id,
    string DisplayName,
    string Category,
    string Subtype,
    int MinLevel,
    IReadOnlyList<string> AllowedClasses,
    int Handedness,
    IReadOnlyDictionary<string, double> BaseStats,
    string AffixPool,
    bool Temperable,
    string TradeRule,
    int StackLimit,
    string IconAssetId,
    string ModelAssetId)
{
    /// <summary>Texto curto do efeito para o HUD (as regras usam base_stats).</summary>
    public string Summary { get; init; } = "";
    // E9
    /// <summary>Preço de compra na loja (0 = não vale moedas). Venda paga um quarto (mínimo 1).</summary>
    public long Price { get; init; }
    /// <summary>Atributo principal mínimo para equipar (Força do Guerreiro, Sintonia do Mago); 0 = sem exigência.</summary>
    public int ReqPrimary { get; init; }
    /// <summary>Onde costuma ser encontrado (dica para o jogador, não regra).</summary>
    public string Source { get; init; } = "";
    /// <summary>Habilidade própria da arma (tecla R), ou null.</summary>
    public string? WeaponSkill { get; init; }
    /// <summary>12/11: habilidade que o item dá enquanto vestido (qualquer classe; tecla X). Ex.: Máscara do Pandemônio → Furacão.</summary>
    public string? ItemSkill { get; init; }
    /// <summary>comum | incomum | raro | epico | lendario (texto e cor na mochila; lendário mostra brilho no corpo).</summary>
    public string Quality { get; init; } = "comum";
    /// <summary>Lendário: a cada BaseStats["proc_every"] golpes básicos que acertam, dispara esta habilidade em volta do alvo.</summary>
    public string? ProcSkill { get; init; }
    /// <summary>Texto do efeito especial (lendário), mostrado na mochila.</summary>
    public string Effect { get; init; } = "";
    /// <summary>Conjunto (set): peças com o mesmo id somam bônus por quantidade vestida ("2", "3" → atributos).</summary>
    public string? Set { get; init; }
    public string SetName { get; init; } = "";
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? SetBonus { get; init; }
}

public sealed record MonsterDefinition(
    string Id,
    string SpeciesName,
    string Archetype,
    int Level,
    IReadOnlyDictionary<string, double> Resistances,
    string BehaviorId,
    string? LootTableId,
    string VisualAssetId)
{
    // E8 (opcionais; só valem no modo mapa): ajuste do protótipo, sem mexer nas fórmulas do GDD.
    public double HealthMult { get; init; } = 1.0;
    public double DamageMult { get; init; } = 1.0;
    public double XpMult { get; init; } = 1.0;
    public double Radius { get; init; } = 0.6;
    public bool Rare { get; init; }
    public bool Boss { get; init; }
    // E9
    public bool Elite { get; init; }
    /// <summary>Moedas por abate (sorteadas entre min e max, divididas entre quem participou).</summary>
    public int CoinsMin { get; init; }
    public int CoinsMax { get; init; }
}

public sealed record ConsumableDefinition(
    string Id,
    string DisplayName,
    double HealPercentMaxHealth,
    int HealDurationMs,
    string CooldownGroup,
    int CooldownMs,
    int StackLimit)
{
    public long Price { get; init; }
    /// <summary>Recupera esta fração do recurso máximo na hora (Frasco de Mana).</summary>
    public double ResourcePercent { get; init; }
    /// <summary>Só estas classes usam (null = todas). Também filtra o drop com match_class.</summary>
    public IReadOnlyList<string>? Classes { get; init; }
    /// <summary>Refeição: efeito temporário (um ativo por personagem; outra refeição substitui).</summary>
    public string? Buff { get; init; }
    public int BuffMs { get; init; }
    public double BuffPowerPct { get; init; }
    public double BuffArmor { get; init; }
    public double BuffMaxHealth { get; init; }
    public double BuffRegenPct { get; init; }
    /// <summary>Texto curto do efeito (mostrado na mochila).</summary>
    public string Summary { get; init; } = "";
    /// <summary>Efeitos de status que este item cura ao usar ("veneno", "queimadura"…). null = deduz (antídoto cura veneno).</summary>
    public IReadOnlyList<string>? Cures { get; init; }
    /// <summary>Tomo: ao usar, ensina esta habilidade (marca "tomo:id"). Só a classe da habilidade usa.</summary>
    public string? TeachSkill { get; init; }
    /// <summary>Esfera de nível 2: fortalece esta habilidade já aprendida (marca "up:id"; só classe evoluída).</summary>
    public string? UpgradeSkill { get; init; }
    /// <summary>12/11 (formas): relíquia que transforma (id em transformacoes.json); reutilizável não some ao usar.</summary>
    public string? Transform { get; init; }
    public bool Reusable { get; init; }
    public bool Soulbound { get; init; }
    /// <summary>Nível mínimo para usar (tomos).</summary>
    public int MinLevel { get; init; }
    /// <summary>12/11: peça de decoração da casa (pontos de conforto); usar dentro da própria casa a coloca lá.</summary>
    public int DecorPoints { get; init; }
    /// <summary>13/11 (pedido do dono: "sistema de baú tipo as box +1 até +5"): Caixa das Cinzas +N — abrir sorteia um prêmio da faixa.</summary>
    public int Caixa { get; init; }
    /// <summary>13/11 (vendedora de montarias): apito que ensina a montaria (marca mount:&lt;id&gt;) ao ser usado.</summary>
    public string? Montaria { get; init; }
}
