using System.Text.Json;
using System.Text.Json.Serialization;

namespace CinzasDaMare.Shared.Net;

public static class Protocol
{
    public const int Version = 1; // mensagens novas são aditivas: clientes antigos continuam válidos
}

/// <summary>
/// Mensagens da rede. Cliente -> servidor carrega apenas INTENÇÕES (nunca dano, posição final, XP ou itens).
/// O discriminador "t" identifica o tipo no JSON.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "t")]
[JsonDerivedType(typeof(LoginRequest), "login")]
[JsonDerivedType(typeof(MoveIntent), "move")]
[JsonDerivedType(typeof(FaceIntent), "face")]
[JsonDerivedType(typeof(CastIntent), "cast")]
[JsonDerivedType(typeof(ClaimItemIntent), "claim")]
[JsonDerivedType(typeof(AutoAttackIntent), "auto_attack")]
[JsonDerivedType(typeof(EquipIntent), "equip")]
[JsonDerivedType(typeof(UnequipIntent), "unequip")]
[JsonDerivedType(typeof(UseItemIntent), "use_item")]
[JsonDerivedType(typeof(LoginResult), "login_result")]
[JsonDerivedType(typeof(Snapshot), "snapshot")]
[JsonDerivedType(typeof(CastResultMessage), "cast_result")]
[JsonDerivedType(typeof(CombatEvent), "combat_event")]
[JsonDerivedType(typeof(ClaimResultMessage), "claim_result")]
[JsonDerivedType(typeof(InventoryMessage), "inventory")]
[JsonDerivedType(typeof(ErrorMessage), "error")]
[JsonDerivedType(typeof(ProgressMessage), "progress")]
[JsonDerivedType(typeof(EquipmentMessage), "equipment")]
[JsonDerivedType(typeof(ActionResultMessage), "action_result")]
[JsonDerivedType(typeof(CastStartedMessage), "cast_started")]
[JsonDerivedType(typeof(WorldInfoMessage), "world_info")]
[JsonDerivedType(typeof(LobbyRequest), "lobby")]
[JsonDerivedType(typeof(CreateCharacterIntent), "create_character")]
[JsonDerivedType(typeof(EnterWorldIntent), "enter_world")]
[JsonDerivedType(typeof(DodgeIntent), "dodge")]
[JsonDerivedType(typeof(InteractIntent), "interact")]
[JsonDerivedType(typeof(LobbyMessage), "lobby_info")]
[JsonDerivedType(typeof(MapMessage), "map")]
[JsonDerivedType(typeof(CatalogMessage), "catalog")]
[JsonDerivedType(typeof(FlagsMessage), "flags")]
[JsonDerivedType(typeof(LootNoticeMessage), "loot_notice")]
[JsonDerivedType(typeof(BuyIntent), "buy")]
[JsonDerivedType(typeof(SellIntent), "sell")]
[JsonDerivedType(typeof(SalvageIntent), "salvage")]
[JsonDerivedType(typeof(BuybackIntent), "buyback")]
[JsonDerivedType(typeof(BuybackMessage), "buyback_list")]
[JsonDerivedType(typeof(DeleteCharacterIntent), "delete_character")]
[JsonDerivedType(typeof(DropItemIntent), "drop_item")]
[JsonDerivedType(typeof(TradeIntent), "trade")]
[JsonDerivedType(typeof(TradeStateMessage), "trade_state")]
[JsonDerivedType(typeof(MoveSlotIntent), "move_slot")]
[JsonDerivedType(typeof(QuestIntent), "quest")]
[JsonDerivedType(typeof(SpendStatIntent), "spend_stat")]
[JsonDerivedType(typeof(TravelIntent), "travel")]
[JsonDerivedType(typeof(SkinVisivelIntent), "skin_visivel")]
[JsonDerivedType(typeof(ObjetivosIntent), "objetivos")]
[JsonDerivedType(typeof(ObjetivosMessage), "objetivos_lista")]
[JsonDerivedType(typeof(PartyIntent), "party")]
[JsonDerivedType(typeof(NpcDialogMessage), "npc_dialog")]
[JsonDerivedType(typeof(CoinsMessage), "coins")]
[JsonDerivedType(typeof(StatsMessage), "stats")]
[JsonDerivedType(typeof(QuestLogMessage), "quest_log")]
[JsonDerivedType(typeof(WorldMapMessage), "world_map")]
[JsonDerivedType(typeof(PartyMessage), "party_info")]
[JsonDerivedType(typeof(PartyInviteMessage), "party_invite")]
[JsonDerivedType(typeof(NoticeMessage), "notice")]
[JsonDerivedType(typeof(NpcMarkersMessage), "npc_markers")]
[JsonDerivedType(typeof(PaidTravelIntent), "paid_travel")]
[JsonDerivedType(typeof(StationMessage), "station")]
[JsonDerivedType(typeof(CraftIntent), "craft")]
[JsonDerivedType(typeof(StorageMessage), "storage")]
[JsonDerivedType(typeof(StorageIntent), "storage_move")]
[JsonDerivedType(typeof(PlotsMessage), "plots")]
[JsonDerivedType(typeof(NodeTimersMessage), "node_timers")]
[JsonDerivedType(typeof(BuffMessage), "buff")]
[JsonDerivedType(typeof(MountIntent), "mount")]
[JsonDerivedType(typeof(WingDashIntent), "wing_dash")]
[JsonDerivedType(typeof(FlyIntent), "fly")]
[JsonDerivedType(typeof(RefineIntent), "refine")]
[JsonDerivedType(typeof(UpdateCheckIntent), "update_check")]
[JsonDerivedType(typeof(UpdateManifestMessage), "update_manifest")]
[JsonDerivedType(typeof(UpdateFileRequest), "update_file")]
[JsonDerivedType(typeof(UpdateChunkMessage), "update_chunk")]
[JsonDerivedType(typeof(PetIntent), "pet")]
[JsonDerivedType(typeof(CompanionsMessage), "companions")]
[JsonDerivedType(typeof(QuestMarkerMessage), "quest_marker")]
[JsonDerivedType(typeof(GodIntent), "god")]
[JsonDerivedType(typeof(ChatIntent), "chat_say")]
[JsonDerivedType(typeof(ChatMessage), "chat")]
[JsonDerivedType(typeof(BookMessage), "book")]
[JsonDerivedType(typeof(BestiaryIntent), "bestiary_ask")]
[JsonDerivedType(typeof(BestiaryMessage), "bestiary")]
[JsonDerivedType(typeof(VinteUmIntent), "vinteum")]
[JsonDerivedType(typeof(VinteUmStateMessage), "vinteum_state")]
[JsonDerivedType(typeof(GodCatalogMessage), "god_catalog")]
[JsonDerivedType(typeof(WorldTimeMessage), "world_time")]
[JsonDerivedType(typeof(EditorStateMessage), "editor_state")]
[JsonDerivedType(typeof(ContentFileMessage), "content_file")]
[JsonDerivedType(typeof(TrilhaIntent), "trilha_ask")]
[JsonDerivedType(typeof(TrilhaMessage), "trilha")]
public abstract record NetMessage;

/// <summary>13/11 ("quero poder mexer em tudo pelo editor, sem falar com você"): arquivos do jogo (Content/*.json) — a lista,
/// e o texto de um deles para editar. Info = resultado de salvar.</summary>
/// <summary>13/11 ("progressão para acompanhar o que você ganha em cada nível — montaria, TP... tipo um passe"): pede a trilha.</summary>
public sealed record TrilhaIntent() : NetMessage;
/// <summary>Um marco da trilha do herói: nível, tipo (mapa, montaria, viagem, classe, poder, história, asas, extra), texto e se já tem.</summary>
public sealed record TrilhaItem(int Nivel, string Tipo, string Titulo, string Detalhe, bool Tem)
{
    /// <summary>14/11 (passe de batalha): arte do prêmio — "item:&lt;id&gt;", "hab:&lt;skill&gt;" ou vazio (cartão do tipo).</summary>
    public string Arte { get; init; } = "";
}
public sealed record TrilhaMessage(int Nivel, IReadOnlyList<TrilhaItem> Itens) : NetMessage;

public sealed record ContentFileMessage(IReadOnlyList<string> Files, string? Name = null, string? Text = null, string? Info = null) : NetMessage;

/// <summary>11/11 noite (editor v2): rascunho (mudanças não salvas) e o objeto selecionado (lista, posição, campos em JSON).</summary>
public sealed record EditorStateMessage(int Unsaved, bool Testing, string? SelList, double SelX, double SelY, string? SelJson, string? Info = null) : NetMessage;

// ---- expansão até o midgame: ofícios, armazém, horta, refeição, montaria, asas, companheiro ----
public sealed record IngredientView(string Item, string Name, int Need, int Have);
public sealed record RecipeView(string Id, string Name, string Output, string OutputName, int OutputQty,
    IReadOnlyList<IngredientView> Inputs, bool Known, bool CanCraft, string Note);
/// <summary>Estação aberta (forja, bancada, caldeirão, panela): receitas com o que falta.</summary>
public sealed record StationMessage(string StationId, string Kind, string Name, IReadOnlyList<RecipeView> Recipes) : NetMessage;
public sealed record CraftIntent(string RequestId, string StationId, string RecipeId) : NetMessage;
/// <summary>Armazém comunitário (conta pessoal) ou baú da casa.</summary>
public sealed record StorageMessage(string Container, string Name, int Capacity, IReadOnlyList<InventoryItemView> Items) : NetMessage;
/// <summary>Action: deposit (mochila → depósito) ou withdraw (depósito → mochila).</summary>
/// <summary>Slot (opcional) = casa da mochila onde soltar o item tirado do depósito (arrastar).</summary>
public sealed record StorageIntent(string RequestId, string Action, string Container, string ItemId, int Slot = -1) : NetMessage;
/// <summary>Canteiros do personagem (horta comunitária e da casa). State: empty | planted | growing | ready.</summary>
public sealed record PlotView(string PlotId, string Seed, string CropName, string State, long ReadyInMs);
public sealed record PlotsMessage(IReadOnlyList<PlotView> Plots) : NetMessage;

/// <summary>Pontos de coleta esgotados para este personagem: id → segundos até voltar (o contorno some enquanto isso).</summary>
public sealed record NodeTimersMessage(IReadOnlyDictionary<string, long> SecondsLeft) : NetMessage;
public sealed record BuffMessage(string Name, long RemainingMs, string Summary) : NetMessage;
public sealed record MountIntent(bool Mount) : NetMessage;
public sealed record WingDashIntent(double DirX, double DirY) : NetMessage;
/// <summary>11/11: voar (asas) ou pousar.</summary>
public sealed record FlyIntent(bool On) : NetMessage;
/// <summary>11/11: refino estilo MU — arrastar a joia (JewelItemId) em cima do item (TargetItemId).</summary>
public sealed record RefineIntent(string JewelItemId, string TargetItemId) : NetMessage;

// ---- atualizador automático (11/11): o cliente do amigo baixa do servidor o que mudou ----
/// <summary>Pede a lista de arquivos do jogo (página Page).</summary>
public sealed record UpdateCheckIntent(int Page = 0) : NetMessage;
/// <summary>Arquivo do jogo: caminho relativo à raiz (com '/'), tamanho e SHA-256 (hex).</summary>
public sealed record UpdateFileInfo(string Path, long Size, string Sha256);
/// <summary>Uma página da lista (até 400 arquivos por página, para caber no quadro de 256 KB).</summary>
public sealed record UpdateManifestMessage(int Page, int Pages, IReadOnlyList<UpdateFileInfo> Files, string Version = "") : NetMessage;
public sealed record UpdateFileRequest(string Path, long Offset) : NetMessage;
/// <summary>Pedaço de arquivo (Data em base64, até 128 KB de bytes). Error preenchido = não serve esse arquivo.</summary>
public sealed record UpdateChunkMessage(string Path, long Offset, long Total, string Data, string Error = "") : NetMessage;
/// <summary>Action: summon (Kind) | dismiss | passive | aggressive.</summary>
public sealed record PetIntent(string Action, string? Kind = null) : NetMessage;

// ---- relógio do mundo e modo deus (10/11) ----
/// <summary>Hora do mundo (0–24) agora e quantas horas do jogo passam por segundo real; o cliente avança sozinho entre avisos.</summary>
public sealed record WorldTimeMessage(double Hour, double HoursPerSecond) : NetMessage;
/// <summary>Modo deus (F1, só com o servidor aberto com --deus): Action = open | level | xp | coins | points | item | spawn |
/// kill | clear | heal | god | speed | cooldowns | time | time_speed | travel | unlock | evolve.</summary>
public sealed record GodIntent(string Action, string? Arg = null, long Amount = 0, int Count = 1, string? Extra = null,
    double X = 0, double Y = 0, double Yaw = 0) : NetMessage;   // X/Y/Yaw: editor no jogo (11/11)
public sealed record GodEntry(string Id, string Name, string Kind, int Level, string Visual = "");
public sealed record GodCatalogMessage(bool Enabled, IReadOnlyList<GodEntry> Items, IReadOnlyList<GodEntry> Monsters,
    IReadOnlyList<GodEntry> Maps, bool Invulnerable, double Speed, double TimeSpeed) : NetMessage
{
    /// <summary>11/11 (editor no jogo): peças de cenário que dá para pôr (todas as usadas em algum mapa).</summary>
    public IReadOnlyList<string> PropKinds { get; init; } = Array.Empty<string>();
    /// <summary>11/11 noite: jogadores online e o alvo das ações do painel (null = você).</summary>
    public IReadOnlyList<string> Players { get; init; } = Array.Empty<string>();
    public string? Target { get; init; }
    /// <summary>Quantas versões anteriores do mapa atual existem (botão "Voltar versão").</summary>
    public int MapVersions { get; init; }
}
/// <summary>O que o personagem já conquistou: montarias, asas e companheiros (para o painel e as teclas).</summary>
public sealed record CompanionsMessage(IReadOnlyList<string> Mounts, IReadOnlyList<string> Wings, IReadOnlyList<string> Pets,
    string? ActivePet, bool PetPassive, long WingReadyInMs) : NetMessage;
public sealed record PetView(string OwnerId, string Kind, double X, double Y, double FacingX, double FacingY, string Action, double ActionT,
    bool Down);

// ---- cliente -> servidor ----
public sealed record LoginRequest(string Token, int ProtocolVersion) : NetMessage;
/// <summary>Direção desejada; o servidor normaliza (comprimento máx. 1) e aplica velocidade e limites.</summary>
/// <summary>Walk = andar (mais devagar, animação de caminhada); sem Walk = correr (padrão dos clientes antigos).
/// Sprint = corrida longa fora de combate.</summary>
public sealed record MoveIntent(double DirX, double DirY, bool Sprint = false, bool Walk = false) : NetMessage;
/// <summary>Direção para onde o jogador olha (vetor no plano; o servidor normaliza). Vetor nulo é ignorado.</summary>
public sealed record FaceIntent(double DirX, double DirY) : NetMessage;
/// <summary>AimX/AimY (opcionais): ponto do chão mirado (habilidades de área). O servidor limita ao alcance.</summary>
public sealed record CastIntent(string SkillId, string? TargetId, double? AimX = null, double? AimY = null) : NetMessage;
public sealed record ClaimItemIntent(string RequestId, string ItemId) : NetMessage;
/// <summary>Liga o ataque básico automático contra o alvo; TargetId vazio/nulo desliga.</summary>
public sealed record AutoAttackIntent(string? TargetId) : NetMessage;
public sealed record EquipIntent(string ItemId) : NetMessage;
public sealed record UnequipIntent(string Slot) : NetMessage;
public sealed record UseItemIntent(string ItemId) : NetMessage;
/// <summary>Autentica e pede a lista de personagens (sem entrar no mundo).</summary>
public sealed record LobbyRequest(string Token, int ProtocolVersion) : NetMessage;
public sealed record CreateCharacterIntent(string Name, string ClassId) : NetMessage;
/// <summary>Versao (13/11, opcional): versão do pacote do cliente — o servidor avisa se for diferente da dele.</summary>
public sealed record EnterWorldIntent(string CharacterId, string? Versao = null) : NetMessage;
/// <summary>Lobby: excluir personagem (pede o nome digitado de novo, para confirmar).</summary>
public sealed record DeleteCharacterIntent(string CharacterId, string ConfirmName) : NetMessage;
/// <summary>Esquiva na direção pedida (vetor nulo = para trás da orientação). Invulnerável no começo.</summary>
public sealed record DodgeIntent(double DirX, double DirY) : NetMessage;
/// <summary>Abrir baú / puxar alavanca. O servidor confere distância e marcas do personagem.</summary>
public sealed record InteractIntent(string ObjectId) : NetMessage;

// ---- servidor -> cliente ----
public sealed record LoginResult(bool Ok, string? Error, string? CharacterId, string? Name, int Level) : NetMessage;

/// <summary>
/// Action = "fase:id" ("windup:skill_x", "recover:skill_x", "dodge", "" parado). ActionT = progresso 0..1 da fase.
/// ActionSeq muda a cada ação nova (para o cliente reiniciar a animação mesmo com a mesma habilidade).
/// Weapon/Offhand/Body = definição equipada (o cliente escolhe a forma pelo ID; não afeta regras).
/// </summary>
public sealed record PlayerView(
    string CharacterId, string Name, double X, double Y,
    double Health, double MaxHealth, double Resource, bool Alive, bool Guarding, int Level,
    double FacingX, double FacingY,
    string ClassId = "guerreiro", string Action = "", double ActionT = 0, int ActionSeq = 0,
    string? Weapon = null, string? Offhand = null, string? Body = null, double ResourceMax = 100,
    double AimX = 0, double AimY = 0, double AimRadius = 0, string? Head = null, string? PartyId = null,
    string? Mount = null, string? Wings = null, string? Spec = null, bool Legendary = false,
    double Shield = 0, bool Stealth = false, string Status = "", bool Evolved = false, double Stamina = 100,
    string? Carry = null, string? Gloves = null, string? Boots = null, bool Flying = false, int WeaponPlus = 0, bool WeaponExcellent = false, string? Title = null, string? Pants = null,
    int GearVar = 0,    // 12/11: variação média (V1–V15) da armadura vestida — o boneco fica mais dourado
    string? Form = null,    // 12/11: transformação ativa (forma_lobisomem, forma_vampiro...) — o cliente troca o visual
    bool Class3 = false,   // 12/11: 3ª classe (nível 55+): leão, maior e mais forte
    bool SetCompleto = false,   // 13/11: vestindo as 5 peças do mesmo conjunto — brilho em volta do corpo
    string? Skin = null);       // 13/11: skin do bracelete (coelho, cartola, gravata, palhaco, bobo) — só aparência

/// <summary>
/// Action: "idle", "chase", "windup:ataque", "active:ataque", "recover:ataque" (vulnerável), "flee", "return".
/// AimX/AimY/AimRadius/AimArc: marca no chão do ataque em preparação. AimArc &gt; 0: cone (graus) a partir de AimX/AimY
/// na direção Facing; AimArc = 0: círculo em AimX/AimY; AimArc &lt; 0: faixa reta de largura |AimArc| (bote).
/// </summary>
public sealed record MonsterView(
    string Id, string SpeciesName, double X, double Y, double Health, double MaxHealth, bool Alive,
    string DefinitionId = "", int Level = 0, string Action = "", double ActionT = 0, int ActionSeq = 0,
    double FacingX = 1, double FacingY = 0, double AimX = 0, double AimY = 0, double AimRadius = 0,
    bool Rare = false, bool Boss = false, double AimArc = 0, string Visual = "", bool Elite = false, bool Enraged = false,
    bool Weakened = false, bool Marked = false, string AimKind = "", string Affix = "", double Size = 1.0, double Lift = 0,
    bool Chilled = false);   // 12/11: lento pela Estaca de Gelo

/// <summary>Projétil em voo (seta do Mago, bola do Acólito). Hostile = vem de monstro.</summary>
public sealed record ProjectileView(string Id, string Kind, double X, double Y, double DirX, double DirY, double Speed, bool Hostile);

public sealed record WorldItemView(string ItemId, string DefinitionId, double X, double Y, string Quality = "comum");

public sealed record InventoryItemView(string ItemId, string DefinitionId, string Quality, int Slot, int Quantity, int Plus = 0);

/// <summary>11/11: troca entre jogadores. Action: pedir (Target = personagem), aceitar, item, tirar, moedas, confirmar, cancelar.</summary>
public sealed record TradeIntent(string Action, string? Target = null, string? ItemId = null, long Coins = 0) : NetMessage;

/// <summary>11/11: estado da troca. State: "pedido" (Partner quer trocar com você), "aberta", "fechada" (Reason diz por quê).</summary>
public sealed record TradeStateMessage(string State, string Partner, IReadOnlyList<InventoryItemView> Mine, IReadOnlyList<InventoryItemView> Theirs,
    long MyCoins, long TheirCoins, bool MyOk, bool TheirOk, string Reason = "", string PartnerId = "") : NetMessage;

public sealed record EquippedItemView(string Slot, string ItemId, string DefinitionId, string Quality, int Plus = 0);

public sealed record Snapshot(
    long Tick,
    IReadOnlyList<PlayerView> Players,
    IReadOnlyList<MonsterView> Monsters,
    IReadOnlyList<WorldItemView> Items,
    IReadOnlyList<ProjectileView>? Projectiles = null, IReadOnlyList<GroundEffectView>? Effects = null,
    IReadOnlyList<PetView>? Pets = null) : NetMessage;

public sealed record CastResultMessage(
    string SkillId, bool Success, string Failure, int Damage, bool Crit, string? TargetId, bool TargetDied) : NetMessage;

/// <summary>Evaded = acertaria, mas o alvo estava invulnerável (esquiva). Exposed = acerto no monstro em recuperação (bônus).</summary>
public sealed record CombatEvent(
    string AttackerId, string TargetId, string SkillId, int Damage, bool Crit, bool TargetDied,
    bool Evaded = false, bool Exposed = false, bool Staggered = false, bool Heal = false, int Absorbed = 0) : NetMessage;

public sealed record ClaimResultMessage(string RequestId, bool Success, string Reason, int Slot) : NetMessage;

/// <summary>Mochila. Columns/Rows &gt; 0 = grade estilo MU (Slot = casa do canto de cima à esquerda; tamanho no ItemInfo).</summary>
public sealed record InventoryMessage(IReadOnlyList<InventoryItemView> Items, int Columns = 0, int Rows = 0) : NetMessage;

public sealed record ErrorMessage(string Reason) : NetMessage;

public sealed record ProgressMessage(int Level, long Xp, long XpToNext, long XpGained) : NetMessage;

public sealed record EquipmentMessage(IReadOnlyList<EquippedItemView> Items) : NetMessage;

/// <summary>Resultado de ações simples: "auto_attack", "equip", "unequip", "use_item". Reason "ok" em caso de sucesso.</summary>
public sealed record ActionResultMessage(string Action, bool Success, string Reason) : NetMessage;

public sealed record CastStartedMessage(string SkillId, int CastMs, int RecoveryMs) : NetMessage;

/// <summary>Dados do mundo que o cliente precisa para desenhar a arena e o arco da Guarda (fonte única: o servidor).</summary>
public sealed record WorldInfoMessage(double ArenaHalfSize, double GuardArcDegrees, string MapId,
    string ClassId = "guerreiro") : NetMessage;

// ---- E8: lobby, mapa, catálogo, marcas e avisos de loot ----
public sealed record CharacterSummary(string Id, string Name, string ClassId, int Level)
{
    /// <summary>Lobby 3D (estilo MU): modelos do que está vestido (ids de modelo, não de item), para o boneco aparecer equipado.</summary>
    public string? Weapon { get; init; }
    public string? Offhand { get; init; }
    public string? Body { get; init; }
    public string? Head { get; init; }
    // 14/11 tarde (dono: "no lobby os bonecos não vêm condizentes com o jogo"): o resto do visual do jogo
    public string? Gloves { get; init; }
    public string? Boots { get; init; }
    public string? Pants { get; init; }
    public string? Wings { get; init; }
    public string? Skin { get; init; }
    public bool Evolved { get; init; }
    public bool Class3 { get; init; }
    public int GearVar { get; init; }
    public int WeaponPlus { get; init; }
    public bool WeaponExcellent { get; init; }
}
public sealed record ClassSummary(string Id, string DisplayName, string Description);
public sealed record LobbyMessage(bool Ok, string? Error, string? AccountName,
    IReadOnlyList<CharacterSummary> Characters, IReadOnlyList<ClassSummary> Classes) : NetMessage;

/// <summary>Mapa completo (fonte única: o servidor). O cliente desenha a partir disto.</summary>
public sealed record MapMessage(CinzasDaMare.Shared.World.MapLayout Map) : NetMessage
{
    /// <summary>13/11 ("visita na sua casa ver a SUA decoração"): na casa de outro jogador, as peças dele (marcas "decor:id"). null = a sua.</summary>
    public IReadOnlyList<string>? Decor { get; init; }
}

/// <summary>Resumo legível de itens e habilidades (nomes, slot, efeitos) para o HUD; regras continuam no servidor.</summary>
public sealed record ItemInfo(string Id, string DisplayName, string Slot, IReadOnlyList<string> Classes,
    string Summary, string Model, int MinLevel, int Handedness, double AoeRadiusBonus = 0,
    IReadOnlyDictionary<string, double>? Stats = null, int ReqPrimary = 0, long Price = 0, long SellPrice = 0,
    string Source = "", string Category = "", string? WeaponSkill = null, string Set = "", string SetText = "", string AffixPool = "",
    int GridW = 1, int GridH = 1, string Subtype = "");
public sealed record SkillInfo(string Id, string ClassId, string DisplayName, string Slot, int CooldownMs,
    double ResourceCost, string TargetMode, double Range, double Radius, string Description, int UnlockLevel = 1,
    string? Tome = null, int TomeLevel = 0);
public sealed record CatalogMessage(IReadOnlyList<ItemInfo> Items, IReadOnlyList<SkillInfo> Skills,
    string ResourceName, double ResourceMax) : NetMessage;

/// <summary>Marcas do personagem: baús abertos ("chest:id"), portões abertos ("gate:id").</summary>
public sealed record FlagsMessage(IReadOnlyList<string> Flags) : NetMessage;

/// <summary>Itens recebidos fora da coleta (baú). Source: "chest:id".</summary>
public sealed record LootNoticeMessage(string Source, IReadOnlyList<string> DefinitionIds) : NetMessage;

// ---- E9: loja, missões, atributos, mapa do mundo, viagem, grupo, avisos ----

/// <summary>Compra na loja do NPC (precisa estar perto dele). RequestId torna o pedido idempotente.</summary>
public sealed record BuyIntent(string RequestId, string NpcId, string DefinitionId, int Quantity = 1) : NetMessage;
/// <summary>Venda de um item da mochila para o NPC (perto dele). Paga um quarto do preço.</summary>
public sealed record SellIntent(string RequestId, string NpcId, string ItemId) : NetMessage;
/// <summary>13/11 (análise do jogo — itens): desmanchar equipamento na forja (bigorna perto) — vira material e, nas variações altas, joia.</summary>
public sealed record SalvageIntent(string ItemId) : NetMessage;
/// <summary>10/11: comprar de volta um item vendido a este NPC (só enquanto a loja está aberta).</summary>
public sealed record BuybackIntent(string RequestId, string NpcId, string ItemId) : NetMessage;
public sealed record BuybackEntry(string ItemId, string DefinitionId, string Quality, int Quantity, long Price);
/// <summary>Balcão de recompra: o que você vendeu a este NPC agora há pouco (lista vazia = balcão limpo).</summary>
public sealed record BuybackMessage(string NpcId, IReadOnlyList<BuybackEntry> Items) : NetMessage;

/// <summary>Arrastar item da mochila para fora das janelas: joga no chão aos pés do personagem.</summary>
public sealed record DropItemIntent(string ItemId) : NetMessage;

/// <summary>Arrastar item para outra casa da mochila (troca de lugar se a casa estiver ocupada).</summary>
public sealed record MoveSlotIntent(string ItemId, int Slot) : NetMessage;
/// <summary>Action: accept | complete | abandon. Aceitar e concluir exigem estar perto de quem dá a missão.</summary>
public sealed record QuestIntent(string QuestId, string Action) : NetMessage;
/// <summary>Stat: primary (Força/Sintonia) | vigor.</summary>
public sealed record SpendStatIntent(string Stat) : NetMessage;
/// <summary>Viagem rápida: só parado num ponto seguro descoberto, fora de combate, para outro ponto descoberto.</summary>
public sealed record TravelIntent(string WaypointId) : NetMessage;
/// <summary>14/11: olhinho do bracelete — mostra ou esconde a skin (o bônus do bracelete continua).</summary>
public sealed record SkinVisivelIntent(bool Visivel) : NetMessage;
/// <summary>14/11: menu do TAB — "abrir", "resgatar" (Id = objetivo) ou "comprar" (Id = item da Loja de Cristais).</summary>
public sealed record ObjetivosIntent(string Acao, string? Id = null) : NetMessage;
public sealed record ObjetivoView(string Id, string Tipo, string Titulo, long Progresso, long Meta, int Cristais, bool Resgatado);
public sealed record LojaCristalView(string Item, string Nome, int Preco, string Grupo, int Nivel, bool JaTem);
/// <summary>14/11: objetivos (diários, semanais, da conta) e a Loja de Cristais, com o saldo de cristais azuis da conta.</summary>
public sealed record ObjetivosMessage(long Cristais, IReadOnlyList<ObjetivoView> Lista, IReadOnlyList<LojaCristalView> Loja) : NetMessage;
/// <summary>Action: invite (Target = nome) | accept | decline | leave | kick (Target = id do personagem).</summary>
public sealed record PartyIntent(string Action, string? Target = null) : NetMessage;

public sealed record ShopOffer(string DefinitionId, long Price);
/// <summary>State: available | active | ready (pode concluir) | done | locked.</summary>
public sealed record QuestOffer(string Id, string Name, string Description, string State, int Progress, int Count,
    string Rewards);
public sealed record TravelOffer(string Id, string Label, long Price);
public sealed record NpcDialogMessage(string NpcId, string Name, string Role, string Greeting,
    IReadOnlyList<ShopOffer> Shop, IReadOnlyList<QuestOffer> Quests, IReadOnlyList<TravelOffer>? Travel = null,
    IReadOnlyList<LibraryEntry>? Library = null) : NetMessage;
/// <summary>12/11 (Biblioteca do Continente): um monstro de uma região e o que ele pode soltar.</summary>
public sealed record LibraryEntry(string Region, string Levels, string Monster, string MonsterLevels, string Tag,
    IReadOnlyList<string> Drops, int MaxVariation = 0, string Type = "", string MonsterId = "");
/// <summary>12/11: Livro dos Monstros pessoal (tecla L, do nível 10): pede as criaturas que você já derrotou.</summary>
public sealed record BestiaryIntent() : NetMessage;
public sealed record BestiaryMessage(IReadOnlyList<LibraryEntry> Entries, int Seen, int Total, int UnlockLevel, bool Locked) : NetMessage;
/// <summary>Viagem paga com um NPC (barqueiro): perto dele, paga e vai.</summary>
public sealed record PaidTravelIntent(string NpcId, string TravelId) : NetMessage;
/// <summary>Áreas que queimam no chão (Explosão do Arcanista): só para desenhar; o servidor aplica o dano.</summary>
public sealed record GroundEffectView(string Id, string Kind, double X, double Y, double Radius, double T);
public sealed record CoinsMessage(long Coins, long Gained) : NetMessage;
/// <summary>Atributos e o efeito de cada um (números já calculados pelo servidor).</summary>
public sealed record StatsMessage(int Primary, int Vigor, int Points, string PrimaryName, double Power, double MaxHealth,
    double Armor, double ArmorReductionPct, double PowerPerPoint, double HealthPerPoint) : NetMessage
{
    /// <summary>Rodada 3 (atributos estilo MU): qual é o principal ("forca"/"agilidade"/"energia") e os quatro valores.</summary>
    public string PrimaryKey { get; init; } = "";
    public int Forca { get; init; }
    public int Agilidade { get; init; }
    public int Energia { get; init; }
    /// <summary>Peso do que está vestido (pede Força) e quanto passa da Força (0–100%).</summary>
    public double Weight { get; init; }
    public double OverloadPct { get; init; }
    public int DodgeCooldownMs { get; init; }
}
public sealed record QuestEntry(string Id, string Name, string Hint, string State, int Progress, int Count, string Giver,
    string Where, string MapId = "", double X = 0, double Y = 0)   // 10/11: onde fazer (vazio = sem lugar marcado)
{
    /// <summary>11/11 noite: monstros que contam para a missão (pedrinha amarela no nome do alvo).</summary>
    public IReadOnlyList<string> Targets { get; init; } = Array.Empty<string>();
    /// <summary>12/11: recompensa (para o diário mostrar).</summary>
    public long RewardXp { get; init; }
    public long RewardCoins { get; init; }
    /// <summary>13/11: nível que trava a entrada do lugar da missão (0 = sem trava). Abaixo dele a missão aparece em vermelho.</summary>
    public int NivelLugar { get; init; }
}
public sealed record QuestLogMessage(IReadOnlyList<QuestEntry> Quests) : NetMessage;

/// <summary>State: visited | known (vista/conectada, ainda não visitada) | unknown | future (não implementada).</summary>
public sealed record WorldRegionView(string Id, string Name, string Kind, double X, double Y, string Levels,
    string Description, string State, bool Here, IReadOnlyList<string>? Quests = null);
public sealed record WaypointView(string Id, string Name, string MapId, string MapName, double X, double Y, string Region = "");
public sealed record WorldMapMessage(IReadOnlyList<WorldRegionView> Regions, IReadOnlyList<string[]> Links,
    IReadOnlyList<WaypointView> Waypoints, string CurrentMap, string CurrentMapName, string CurrentRegion) : NetMessage;

public sealed record PartyMemberView(string CharacterId, string Name, string ClassId, int Level, double Health,
    double MaxHealth, string MapName, bool Online, bool Leader);
public sealed record PartyMessage(string? PartyId, IReadOnlyList<PartyMemberView> Members) : NetMessage;
public sealed record PartyInviteMessage(string FromId, string FromName, string Kind = "") : NetMessage;   // 12/11: Kind "duelo" = desafio x1
/// <summary>Aviso no centro da tela. Kind: area (nome da região), info, warn, reward.</summary>
public sealed record NoticeMessage(string Text, string Kind = "info") : NetMessage;
/// <summary>12/11: chat. Texto normal = global (todos os mapas); "/nome texto" ou "@nome texto" = privado; "/g texto" = grupo.</summary>
public sealed record ChatIntent(string Text) : NetMessage;
/// <summary>12/11: livro aberto (Biblioteca do Continente). Discovery = texto de "você descobriu algo" (null = nada novo).</summary>
public sealed record BookMessage(string Title, string Text, string? Discovery = null) : NetMessage;
/// <summary>Channel: global | privado | grupo | sistema. To = destinatário (só no privado, para quem mandou ver "para X").</summary>
/// <summary>12/11: mesa de Vinte-e-Um (blackjack) do 2º andar do casarão. Action: abrir | apostar | pedir | parar | dobrar | separar | sair.</summary>
public sealed record VinteUmIntent(string NpcId, string Action, int Bet = 0) : NetMessage;
public sealed record VinteUmHand(IReadOnlyList<string> Cards, int Total, int Bet, string Result);
/// <summary>Estado da mesa para quem joga. Open = false fecha a tela. Cartas por código ("AE" = Ás de espadas, "??" = escondida).</summary>
public sealed record VinteUmStateMessage(bool Open, string NpcId, IReadOnlyList<VinteUmHand> Hands, IReadOnlyList<string> Dealer, int? DealerTotal,
    int Current, bool Finished, IReadOnlyList<string> Actions, int Profit, long Coins, int MinBet, int MaxBet, string? Error = null) : NetMessage;
public sealed record ChatMessage(string From, string Text, string Channel, string? To = null) : NetMessage;
/// <summary>Marcador móvel de missão (escolta: posição da carroça). Kind vazio = remover.</summary>
public sealed record QuestMarkerMessage(string QuestId, double X, double Y, string Kind) : NetMessage;
/// <summary>Marcador sobre a cabeça dos NPCs do mapa, por jogador: "novo" (amarelo: missão ou conversa nova) ou
/// "entregar" (verde: missão pronta). NPC fora do dicionário = nada novo (sem marcador).</summary>
public sealed record NpcMarkersMessage(IReadOnlyDictionary<string, string> Markers) : NetMessage;

public static class NetCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static byte[] Encode(NetMessage message)
        => JsonSerializer.SerializeToUtf8Bytes<NetMessage>(message, Options);

    /// <summary>Devolve null para payload inválido, vazio ou de tipo desconhecido.</summary>
    public static NetMessage? Decode(ReadOnlySpan<byte> payload)
    {
        try
        {
            return JsonSerializer.Deserialize<NetMessage>(payload, Options);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }
}
