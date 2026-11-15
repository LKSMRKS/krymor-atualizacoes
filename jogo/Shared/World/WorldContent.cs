using System.Text.Json;

namespace CinzasDaMare.Shared.World;

// Conteúdo do mundo do protótipo jogável (E8): mapa, comportamentos de monstro e tabelas de loot.
// É DADO, não geometria visual: o servidor usa para colisão/IA/recompensas e envia o mapa ao cliente,
// que desenha o que quiser em cima (hoje formas simples; depois modelos). Coordenadas em metros,
// (x, y) do servidor = (X, Z) do Godot; "norte" = y negativo.

/// <summary>Obstáculo do mapa. Shape "box" (x,y = centro; w,h = largura em x e em y) ou "circle" (r).</summary>
public sealed record MapObstacle(string Kind, string Shape, double X, double Y, double W, double H, double R, double Height)
{
    /// <summary>13/11 ("os invisíveis com colisão eu não consigo girar"): giro da caixa em graus (mesmo sentido do yaw das peças).</summary>
    public double Rot { get; init; }
    /// <summary>Falso = só visual (ex.: parede ilusória do segredo): não bloqueia nada no servidor.</summary>
    public bool Solid { get; init; } = true;
}

/// <summary>Região nomeada (piso, tom e nome exibido ao entrar). Kind: seguro, fraco, forte, chefe, segredo, caminho.</summary>
public sealed record MapZone(string Id, string Name, string Kind, double X, double Y, double W, double H);

/// <summary>Portão do atalho: sólido até o personagem ter a marca Flag (puxar a alavanca grava a marca).</summary>
public sealed record MapGate(string Id, string Flag, double X, double Y, double W, double H, double LeverX, double LeverY)
{
    /// <summary>E9: "portao" (grades) ou "ponte" (vão intransponível até a ponte ser reconstruída).</summary>
    public string Kind { get; init; } = "portao";
    /// <summary>Falso: não tem alavanca; a marca vem de uma missão (mudança no mundo por personagem).</summary>
    public bool Lever { get; init; } = true;
    /// <summary>10/11: kind "pedra" — rocha que só sai do caminho com esta Força (atributo do personagem, sem itens).</summary>
    public int Strength { get; init; }
    /// <summary>12/11 ("a alavanca no final não ativa; só abrir ao matar todos os monstros"): alavanca trava até limpar o mapa.</summary>
    public bool RequiresClear { get; init; }
    /// <summary>
    /// 15/11 (dono: "corda com gancho, ponte que constrói, casa que destruiu, algo pra abrir uma porta — mecânicas variadas
    /// espalhadas pelo mapa"): kinds "corda" (subir no paredão com a Corda com Gancho — não gasta), "casa_ruina" (reconstruir
    /// pagando o carpinteiro e com retalhos) e "porta" (a Chave de Ferro abre e se gasta). Ao abrir: recompensa da tabela.
    /// </summary>
    public string? RequiresItem { get; init; }
    public int ItemQty { get; init; } = 1;
    public bool Consume { get; init; }
    public int Coins { get; init; }
    public string? RewardLoot { get; init; }
    public int RewardXp { get; init; }
}

/// <summary>Baú: cada personagem abre uma vez (marca "chest:id"); o conteúdo vai direto para a mochila.</summary>
public sealed record MapChest(string Id, double X, double Y, string LootTable, double Facing)
{
    public bool Secret { get; init; }
    /// <summary>12/11: só abre com TODOS os monstros do mapa (a cópia da dungeon) derrotados.</summary>
    public bool RequiresClear { get; init; }
}

/// <summary>Ponto de monstro. Kind referencia monsters.json; Group liga monstros que entram em combate juntos.</summary>
public sealed record MapSpawn(string Id, string Monster, double X, double Y, int Level, string Group)
{
    /// <summary>Para o monstro raro: chance (0..1) de aparecer a cada ciclo, em um dos pontos alternativos.</summary>
    public double Chance { get; init; } = 1.0;
    public IReadOnlyList<double[]>? AltPoints { get; init; }
    public int RespawnSeconds { get; init; } = 30;
    /// <summary>11/11 (mundo vivo): só aparece de noite (19h às 5h do relógio do jogo); some ao amanhecer se não estiver lutando.</summary>
    public bool Night { get; init; }
    /// <summary>10/11: monstro de acampamento — fica sentado de frente para a fogueira mais perto até alguém chegar.</summary>
    public bool Camp { get; init; }
    /// <summary>11/11 noite (gárgula do muro): altura do poleiro (m). Fica sentada lá em cima até alguém chegar perto;
    /// aí mergulha em quem chegou e depois luta no chão. Volta ao poleiro quando desiste.</summary>
    public double Perch { get; init; }
    /// <summary>Chefe de passagem: ao derrotá-lo, quem participou ganha esta marca (selo pessoal; abre o portão mesmo que ele volte).</summary>
    public string? GrantFlag { get; init; }
    /// <summary>Multiplicadores de vida/dano para chefes de passagem (força fixa por encontro).</summary>
    public double HealthMult { get; init; } = 1;
    public double DamageMult { get; init; } = 1;
}

/// <summary>Enfeite sem colisão (tocha, estandarte, crânios). O cliente decide como desenhar pelo Kind.</summary>
public sealed record MapProp(string Kind, double X, double Y, double Yaw)
{
    /// <summary>11/11 noite (editor v2): tamanho (1 = normal) e raio de colisão escolhido no editor (0 = o padrão da peça).</summary>
    public double Scale { get; init; } = 1;
    public double Solid { get; init; }
    /// <summary>13/11 ("no editor, mexer na altura vertical também"): quanto a peça sobe (m) acima do chão — só desenho.</summary>
    public double Altura { get; init; }
    /// <summary>13/11 (pincel de chão do editor, "pintura_*"): força da tinta (0..1, 1 = cobre o chão de baixo).</summary>
    public double Forca { get; init; } = 1;
    /// <summary>11/11 noite: placa com texto (aparece letra por letra quando você chega perto).</summary>
    public string? Texto { get; init; }
}

// ---------------- E9: mundo de mapas conectados ----------------

/// <summary>
/// Passagem para outro mapa: entrar no círculo (X, Y, Radius) leva a (ToX, ToY) do mapa ToMap.
/// Para uma dungeon (mapa instanciado), leva à instância do grupo (ou cria uma).
/// </summary>
public sealed record MapPortal(string Id, double X, double Y, double Radius, string ToMap, double ToX, double ToY, string Label)
{
    /// <summary>Nível RECOMENDADO (0 = sem). Abaixo dele: aviso, mas passa (pode se aventurar cedo e voltar depois).</summary>
    public int MinLevel { get; init; }
    /// <summary>13/11 ("em casa não usa portal, usa uma porta: só interage no F"): a passagem não é desenhada nem dispara
    /// por encostar; quem usa é a porta (objeto "porta" com grant_flag "porta:portal:&lt;id deste portal&gt;").</summary>
    public bool PorPorta { get; init; }
    /// <summary>Marca exigida para passar (marco: evolução de classe etc.). Sem ela, fica e recebe o aviso.</summary>
    public string? RequiresFlag { get; init; }
    /// <summary>12/11 (castelos do fim de jogo): item que a passagem CONSOME ao entrar (selo caro, vendido por um arauto).</summary>
    public string? RequiresItem { get; init; }
    /// <summary>12/11 (ramificações: "não chegar num lugar antes da hora"): true = o nível vira TRAVA (abaixo dele não passa).</summary>
    public bool LevelLock { get; init; }
    /// <summary>12/11: entrada da dungeon no modo DIFÍCIL (outra instância, monstros mais fortes, mais XP e loot).</summary>
    public bool Hard { get; init; }
}

/// <summary>Viagem paga oferecida por um NPC (barqueiro, caravana). Só aparece com a marca exigida (se houver).</summary>
public sealed record MapTravel(string Id, string Label, string ToMap, double ToX, double ToY, long Price)
{
    public string? RequiresFlag { get; init; }
    /// <summary>12/11: nível mínimo (trava). Aparece 5 níveis antes, marcado "trancado".</summary>
    public int MinLevel { get; init; }
}

/// <summary>Objeto com que se interage (F) para missões "interagir": altar, relíquia, destroços. Some depois de usado só para quem usou.</summary>
public sealed record MapObject(string Id, string Kind, double X, double Y, string Label)
{
    /// <summary>Veio de minério: item que dá, quantos por coleta e recarga pessoal (segundos). Requer a ferramenta (marca).</summary>
    public string? Yields { get; init; }
    /// <summary>12/11 (livros da Biblioteca do Continente): texto lido ao interagir e marca gravada na primeira leitura.</summary>
    public string? Text { get; init; }
    public string? GrantFlag { get; init; }
    public int YieldMin { get; init; } = 1;
    public int YieldMax { get; init; } = 2;
    public double RespawnSeconds { get; init; } = 90;
    public string? RequiresFlag { get; init; }
    /// <summary>Armazém/baú: capacidade (pessoal).</summary>
    public int Capacity { get; init; }
    /// <summary>Só aparece/funciona com esta missão ativa.</summary>
    public string? Quest { get; init; }
    /// <summary>Altar de oferenda: gasta Consumes × ConsumeQty da mochila e chama o monstro Summons (nível SummonLevel).</summary>
    public string? Consumes { get; init; }
    public int ConsumeQty { get; init; } = 1;
    public string? Summons { get; init; }
    public int SummonLevel { get; init; } = 1;
}

/// <summary>Checkpoint de dungeon: ao passar, quem morrer renasce aqui (vale para a cópia toda).</summary>
public sealed record MapCheckpoint(string Id, double X, double Y, double Radius, string Label);

/// <summary>Ponto seguro de viagem rápida: descoberto ao chegar perto (marca "wp:id").</summary>
public sealed record MapWaypoint(string Id, string Name, double X, double Y);

/// <summary>13/11 ("rampas e elevações como a do cais, pelos mapas"): morro/platô suave (só visual; o servidor segue plano).
/// Topo plano W×H na altura Height, descendo em rampa de Ramp metros para todos os lados (cantos arredondados).</summary>
public sealed record MapElevation(double X, double Y, double W, double H, double Height)
{
    public double Ramp { get; init; } = 4;
}

/// <summary>Estrada de terra (só visual; gerada por Tools/povoar_mapas.py): pontos do traçado e largura.</summary>
public sealed record MapRoad(IReadOnlyList<double[]> Points, double Width);

/// <summary>Personagem com quem se fala (F). Role: loja, missoes, ferreiro, curandeira, guarda.</summary>
public sealed record MapNpc(string Id, string Name, string Role, double X, double Y, double Facing)
{
    public string? Shop { get; init; }
    public string Greeting { get; init; } = "";
    /// <summary>Só aparece para quem tem esta marca (ex.: comerciante que volta depois de uma missão).</summary>
    public string? RequiresFlag { get; init; }
    /// <summary>13/11: some para quem tem esta marca (ex.: o bobo que se revelou e foi para o picadeiro).</summary>
    public string? ForbidFlag { get; init; }
    public IReadOnlyList<MapTravel> Travel { get; init; } = Array.Empty<MapTravel>();
    /// <summary>Aparência (o cliente escolhe): ferreiro, curandeira, guarda, capita, aldeao.</summary>
    public string Look { get; init; } = "aldeao";
}

/// <summary>Região do mapa do mundo (pode ter vários mapas). Implemented = falso: lugar reservado ("em breve").</summary>
public sealed record WorldRegion(string Id, string Name, string Kind, double X, double Y, string Levels, string Description)
{
    public IReadOnlyList<string> Maps { get; init; } = Array.Empty<string>();
    public bool Implemented { get; init; } = true;
}

public sealed record WorldLink(string From, string To);

public sealed record WorldDefinition(string StartMap, IReadOnlyList<string> MapFiles, IReadOnlyList<WorldRegion> Regions,
    IReadOnlyList<WorldLink> Links);

/// <summary>Loja: vende suprimentos e opções básicas (os itens interessantes vêm de farm, exploração e dungeons).</summary>
public sealed record ShopDef(string Id, string Name, IReadOnlyList<string> Items);

/// <summary>
/// Missão. Kind: kill (Targets = monstros), collect (entregar Count do item Targets[0]; os itens saem da mochila),
/// visit (chegar perto de X/Y no mapa Map), boss (derrotar Targets); hunt (caçar a presa alfa de Targets[0] perto de X/Y),
/// defend (segurar Count ondas de Targets no ponto X/Y), escort (levar a carroça de X/Y até ToX/ToY em Count trechos, com emboscadas). Recompensas entregues ao concluir no NPC Giver.
/// </summary>
public sealed record QuestDef(string Id, string Name, string Giver, string Kind, IReadOnlyList<string> Targets, int Count,
    string Description)
{
    public string? Requires { get; init; }
    public int MinLevel { get; init; } = 1;
    /// <summary>13/11 (bobos da corte em faixas): acima deste nível a missão some (0 = sem teto) — a variação seguinte assume.</summary>
    public int MaxLevel { get; init; }
    /// <summary>13/11 (pedido do dono: "lobisomem só na classe 1 — foi pra classe 2, já não faz mais"): depois da 2ª classe a
    /// missão some, não conclui e o NPC avisa. Cria histórias diferentes por personagem.</summary>
    public bool OnlyFirstClass { get; init; }
    public string? Map { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Radius { get; init; } = 4;
    /// <summary>Escolta: destino da carroça (sai de X, Y).</summary>
    public double ToX { get; init; }
    public double ToY { get; init; }
    public long RewardXp { get; init; }
    public long RewardCoins { get; init; }
    /// <summary>Itens de recompensa. "@classe:id_g|id_m|id_b|id_a": escolhe pela classe (Guerreiro|Mago|Bruxa|Arqueiro); faltando, usa o último.</summary>
    public IReadOnlyList<string> RewardItems { get; init; } = Array.Empty<string>();
    public bool Repeatable { get; init; }
    /// <summary>Diária (Bobo da praça): repetível, mas uma vez por dia (horário de Brasília).</summary>
    public bool Daily { get; init; }
    /// <summary>Texto curto do diário enquanto está ativa (ex.: "Abata caranguejinhos no Areal da Maré").</summary>
    public string Hint { get; init; } = "";
    /// <summary>Marcas gravadas ao concluir: mudam o mundo SÓ para este personagem (ponte pronta, comerciante de volta, evolução).</summary>
    public IReadOnlyList<string> RewardFlags { get; init; } = Array.Empty<string>();
    /// <summary>Classe que pode fazer (evolução); null = todas.</summary>
    public string? Class { get; init; }
    // ---- expansão até o midgame (opcionais) ----
    /// <summary>Só aparece com esta marca (ex.: "selo:mata" ou outra cadeia) / some se o personagem tiver esta (ramos exclusivos).</summary>
    public string? RequiresFlag { get; init; }
    public string? ForbidFlag { get; init; }
    /// <summary>historia | regiao | oficio | casa | companheiro | contrato (filtro do diário).</summary>
    public string Category { get; init; } = "regiao";
    /// <summary>Itens entregues ao ACEITAR (ferramenta de missão, sementes) e marcas gravadas ao aceitar (ex.: "tool:picareta").</summary>
    public IReadOnlyList<string> AcceptItems { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AcceptFlags { get; init; } = Array.Empty<string>();
    /// <summary>Preço cobrado ao concluir (ex.: compra da casa). 0 = nada.</summary>
    public long CostCoins { get; init; }
    /// <summary>
    /// 10/11: rodízio de repetíveis ("Encomenda do Cais" varia): das missões do mesmo grupo que você pode fazer, o NPC
    /// oferece uma por vez, na ordem, avançando a cada entrega.
    /// </summary>
    public string? Rotation { get; init; }
    /// <summary>13/11: quadro de contratos do Porto ("contratos"): o NPC do quadro oferece só os 3 contratos sorteados do dia.</summary>
    public string? Board { get; init; }
    /// <summary>13/11 (missões com condição): "noite" | "zona:x,y,r" | "sem_morrer" | "forma" | "arma:tipo" — o abate só conta assim.</summary>
    public string? Condition { get; init; }
}

/// <summary>Receita: consome ingredientes e cria o resultado numa transação (o resultado é exato, sem sorte).</summary>
public sealed record RecipeDef(string Id, string Name, string Station, IReadOnlyList<RecipeInput> Inputs, string Output, int OutputQty = 1)
{
    /// <summary>Marca necessária para conhecer a receita (ex.: "receita:rebites"); null = conhecida desde o início.</summary>
    public string? RequiresFlag { get; init; }
    public int MinLevel { get; init; } = 1;
    /// <summary>11/11 (Máquina do Caos, MU): chance de dar certo (1 = sempre). Falhou: os ingredientes se perdem.</summary>
    public double Chance { get; init; } = 1.0;
}

public sealed record RecipeInput(string Item, int Qty);

/// <summary>Planta: semente → (plantar, regar) → espera → colheita. Tempo em minutos de relógio do servidor.</summary>
public sealed record CropDef(string Seed, string Name, string Crop, double GrowMinutes, int MinQty = 2, int MaxQty = 3);

public sealed record MapLayout(
    string Id,
    string DisplayName,
    double HalfSize,
    double SpawnX,
    double SpawnY,
    double SafeRadius,
    double XpMultiplier,
    IReadOnlyList<MapZone> Zones,
    IReadOnlyList<MapObstacle> Obstacles,
    IReadOnlyList<MapGate> Gates,
    IReadOnlyList<MapChest> Chests,
    IReadOnlyList<MapSpawn> Spawns,
    IReadOnlyList<MapProp> Props)
{
    // ---- E9 (opcionais; mapas antigos continuam válidos) ----
    /// <summary>cidade | campo | dungeon.</summary>
    public string Kind { get; init; } = "campo";
    public string Region { get; init; } = "";
    public IReadOnlyList<MapPortal> Portals { get; init; } = Array.Empty<MapPortal>();
    public IReadOnlyList<MapWaypoint> Waypoints { get; init; } = Array.Empty<MapWaypoint>();
    public IReadOnlyList<MapNpc> Npcs { get; init; } = Array.Empty<MapNpc>();
    /// <summary>Dungeon: cada grupo (ou jogador sozinho) tem sua cópia; monstros não reaparecem dentro dela.</summary>
    public bool Instanced { get; init; }
    /// <summary>Para onde volta quem sai/reconecta sem a instância existir (dungeon).</summary>
    public string? ExitMap { get; init; }
    public double ExitX { get; init; }
    public double ExitY { get; init; }
    /// <summary>Tom do chão/céu para o cliente (praia, vale, cidade, gruta).</summary>
    public string Theme { get; init; } = "vale";
    public IReadOnlyList<MapObject> Objects { get; init; } = Array.Empty<MapObject>();
    public IReadOnlyList<MapCheckpoint> Checkpoints { get; init; } = Array.Empty<MapCheckpoint>();
    /// <summary>Dungeon: recompensa garantida na 1ª vez que o personagem derrota FirstClearBoss (marca "clear:id").</summary>
    public string? FirstClearLoot { get; init; }
    public string? FirstClearBoss { get; init; }
    /// <summary>"party" (padrão: cópia por grupo) ou "personal" (casa: só o dono entra na sua cópia).</summary>
    public string InstanceScope { get; init; } = "party";
    /// <summary>Montaria proibida aqui (dungeons, casa, interiores).</summary>
    public bool NoMount { get; init; }
    /// <summary>Estradas de terra entre acampamento, pedras de viagem e portais (só desenho).</summary>
    public IReadOnlyList<MapRoad> Roads { get; init; } = Array.Empty<MapRoad>();
    /// <summary>13/11: morros e platôs (Tools/gerar_elevacoes.py); o cliente sobe o chão e põe pés, bichos e peças em cima.</summary>
    public IReadOnlyList<MapElevation> Elevations { get; init; } = Array.Empty<MapElevation>();
}

/// <summary>Um ataque de monstro: preparação visível (windup), golpe (active) e abertura para contra-ataque (recovery).</summary>
public sealed record MonsterAttackDef(
    string Id,
    string Shape,          // "cone" | "lunge" | "circle" | "projectile" | "beam" (raio em linha reta)
    double Range,          // distância para começar o ataque
    int WindupMs,
    int ActiveMs,
    int RecoveryMs,
    int CooldownMs,
    double DamageMult)
{
    public double Arc { get; init; } = 90;           // cone
    public double Radius { get; init; } = 1.0;       // círculo / largura do bote / raio do projétil
    public double Offset { get; init; }              // círculo: distância à frente do monstro
    public double LungeM { get; init; }              // bote: quanto avança
    public double ProjectileSpeed { get; init; } = 10;
    /// <summary>E9 (elites): golpe que emenda logo depois deste (combinação curta); a brecha vem depois do último.</summary>
    public string? Followup { get; init; }
    public double MinRange { get; init; }            // não usa o ataque se o alvo estiver mais perto que isso
    /// <summary>Fração da preparação em que o monstro ainda acompanha o alvo; depois trava a direção (janela de esquiva).</summary>
    public double TrackFraction { get; init; } = 0.5;

    // ---- ataques com identidade (rodada de feedback 2) ----
    /// <summary>Golpes em sequência dentro da mesma ação (estocadas rápidas, pancadas que marcham). 1 = golpe único.</summary>
    public int Hits { get; init; } = 1;
    /// <summary>Intervalo entre os golpes da sequência (0 = ActiveMs dividido por Hits). Mínimo prático 260 ms.</summary>
    public int HitIntervalMs { get; init; }
    /// <summary>Avanço por golpe: leque/bote/raio andam para a frente durante a sequência; no círculo a área marcha à frente.</summary>
    public double StepM { get; init; }
    /// <summary>Golpe "atrasado" (souls): segura a arma no alto por um tempo aleatório de 0 até isto antes de soltar.</summary>
    public int WindupJitterMs { get; init; }
    /// <summary>Bate e volta: recua esta distância logo depois do golpe.</summary>
    public double BackstepM { get; init; }
    /// <summary>Projéteis por disparo (leque de areia, três flechas) e a abertura total do leque em graus.</summary>
    public int Count { get; init; } = 1;
    public double SpreadDeg { get; init; }
    /// <summary>Círculo cai sobre a posição do alvo (morteiro), não na frente do monstro.</summary>
    public bool AtTarget { get; init; }
    /// <summary>Efeito de status no acerto: "veneno", "lentidao", "queimadura", "atordoado" (null = deduz pelo nome do golpe).</summary>
    public string? Status { get; init; }
    public int StatusMs { get; init; }

    // ---- família de caranguejos do lote 2 (09/11) ----
    /// <summary>Dano recebido durante a preparação e o golpe deste ataque (Pedra fechado na carapaça: 0,1; Ermitão no barril: 0,15).</summary>
    public double SelfGuardMult { get; init; } = 1.0;
    /// <summary>A defesa acima só vale para golpes vindos da frente (barril do Ermitão); de costas apanha normal.</summary>
    public bool GuardFrontal { get; init; }
    /// <summary>Escavador: na preparação some no chão e anda por baixo até o alvo; para de seguir em TrackFraction (a marca
    /// no chão fica parada = janela de esquiva) e sai do chão com o golpe em círculo onde está.</summary>
    public bool Burrow { get; init; }
    public double BurrowSpeed { get; init; } = 7.0;
}

public sealed record MonsterBehaviorDef(string Id, double MoveSpeed, double AggroRadius, double LeashRadius,
    IReadOnlyList<MonsterAttackDef> Attacks)
{
    public double KeepDistance { get; init; }   // conjurador: distância preferida
    public double FleeDistance { get; init; }   // conjurador: recua se o jogador chegar mais perto que isso
    public bool Flees { get; init; }            // raro: foge sempre
    /// <summary>Multiplicador de dano recebido durante a recuperação (oportunidade de contra-ataque).</summary>
    public double ExposedMult { get; init; } = 1.3;
    /// <summary>Golpes com "stagger" interrompem a preparação deste monstro (chefe: false).</summary>
    public bool Staggerable { get; init; } = true;
    /// <summary>E9 (chefes): abaixo desta fração da vida entra na 2ª fase (0 = não tem).</summary>
    public double PhaseTwoAt { get; init; }
    /// <summary>Ataques que só aparecem na 2ª fase.</summary>
    public IReadOnlyList<string> PhaseTwoAttacks { get; init; } = Array.Empty<string>();
    public double PhaseTwoSpeedMult { get; init; } = 1.2;
    public double PhaseTwoCooldownMult { get; init; } = 0.7;
    /// <summary>Chefe que chama ajudantes durante a luta: id do monstro, quantos por vez e de quanto em quanto tempo
    /// (na 2ª fase o intervalo cai pela metade). Máximo de ajudantes vivos: 2 × SummonCount.</summary>
    public string? Summon { get; init; }
    public int SummonCount { get; init; }
    public int SummonEveryMs { get; init; } = 15000;

    // ---- 10/11 item 47: caranguejo-geodo ----
    /// <summary>Dano recebido por golpes vindos da frente/lados (carapaça de geodo). Pelas costas (o buraco) apanha cheio.</summary>
    public double FrontTakenMult { get; init; } = 1.0;
    /// <summary>Quantos graus por segundo consegue virar (0 = vira na hora). Bicho lento = dá para rodear e pegar as costas.</summary>
    public double TurnDegPerSec { get; init; }
    /// <summary>Sem alvo fica parado no lugar, encolhido como pedra (dorme), até alguém chegar perto. Depois volta e encolhe de novo.</summary>
    public bool StoneRest { get; init; }
}

/// <summary>Entrada de loot. Item fixo ou Pick (sorteia um; com MatchClass prefere a classe de quem participou).</summary>
public sealed record LootEntry(double Chance)
{
    public string? Item { get; init; }
    public IReadOnlyList<string>? Pick { get; init; }
    public bool MatchClass { get; init; }
    public int MinQty { get; init; } = 1;
    public int MaxQty { get; init; } = 1;
    public string Quality { get; init; } = "comum";
    /// <summary>E9: só cai de monstro deste nível para cima (o mesmo bicho rende itens melhores numa região mais forte).</summary>
    public int MinMonsterLevel { get; init; }
}

public sealed record LootTable(string Id, IReadOnlyList<LootEntry> Entries);

public sealed class WorldContent
{
    /// <summary>Mapa inicial (onde nasce o personagem novo). Mantido para o código/testes de mapa único.</summary>
    public MapLayout Map => Maps[StartMap];
    public string StartMap { get; }
    public IReadOnlyDictionary<string, MapLayout> Maps { get; }
    public IReadOnlyList<WorldRegion> Regions { get; }
    public IReadOnlyList<WorldLink> Links { get; }
    public IReadOnlyDictionary<string, MonsterBehaviorDef> Behaviors { get; }
    public IReadOnlyDictionary<string, LootTable> LootTables { get; }
    public IReadOnlyDictionary<string, ShopDef> Shops { get; }
    public IReadOnlyDictionary<string, QuestDef> Quests { get; }
    /// <summary>Monstros "vivos": nascem num ponto aleatório da região, andam à toa, alguns dormem; só o mundo real
    /// (world.json) liga — os testes de mapa montado à mão continuam com monstro parado no ponto exato.</summary>
    public bool LivelyMonsters { get; set; }

    public WorldContent(MapLayout map, IReadOnlyDictionary<string, MonsterBehaviorDef> behaviors,
        IReadOnlyDictionary<string, LootTable> lootTables)
        : this(new[] { map }, map.Id, behaviors, lootTables) { }

    public WorldContent(IEnumerable<MapLayout> maps, string startMap, IReadOnlyDictionary<string, MonsterBehaviorDef> behaviors,
        IReadOnlyDictionary<string, LootTable> lootTables, IReadOnlyList<WorldRegion>? regions = null,
        IReadOnlyList<WorldLink>? links = null, IReadOnlyDictionary<string, ShopDef>? shops = null,
        IReadOnlyDictionary<string, QuestDef>? quests = null)
    {
        Maps = maps.ToDictionary(m => m.Id);
        if (!Maps.ContainsKey(startMap)) throw new InvalidDataException($"mapa inicial '{startMap}' não existe");
        StartMap = startMap;
        Behaviors = behaviors;
        LootTables = lootTables;
        Regions = regions ?? Array.Empty<WorldRegion>();
        Links = links ?? Array.Empty<WorldLink>();
        Shops = shops ?? new Dictionary<string, ShopDef>();
        Quests = quests ?? new Dictionary<string, QuestDef>();
    }

    /// <summary>11/11 (editor no jogo): troca um mapa já carregado pela versão nova (mesmo id).</summary>
    public void ReplaceMap(MapLayout map) => ((Dictionary<string, MapLayout>)Maps)[map.Id] = map;

    /// <summary>Receitas de ofício (forja, bancada, caldeirão, panela).</summary>
    public IReadOnlyDictionary<string, RecipeDef> Recipes { get; set; } = new Dictionary<string, RecipeDef>();
    /// <summary>Plantas da horta, pela semente.</summary>
    public IReadOnlyDictionary<string, CropDef> Crops { get; set; } = new Dictionary<string, CropDef>();
}

public static class WorldContentLoader
{
    public const string MapFile = "map_vale.json";
    public const string BehaviorsFile = "behaviors.json";
    public const string LootFile = "loot.json";
    public const string WorldFile = "world.json";
    public const string ShopsFile = "shops.json";
    public const string QuestsFile = "quests.json";

    public static bool Exists(string directory)
        => File.Exists(Path.Combine(directory, WorldFile)) || File.Exists(Path.Combine(directory, MapFile));

    public static WorldContent Load(string directory)
    {
        var behaviors = ReadList<MonsterBehaviorDef>(directory, BehaviorsFile).ToDictionary(b => b.Id);
        var loot = ReadList<LootTable>(directory, LootFile).ToDictionary(t => t.Id);
        if (!File.Exists(Path.Combine(directory, WorldFile)))
            return new WorldContent(Read<MapLayout>(directory, MapFile), behaviors, loot); // E8: mapa único

        var world = Read<WorldDefinition>(directory, WorldFile);
        var maps = world.MapFiles.Select(f => Read<MapLayout>(directory, f)).ToList();
        var shops = File.Exists(Path.Combine(directory, ShopsFile))
            ? ReadList<ShopDef>(directory, ShopsFile).ToDictionary(x => x.Id) : null;
        var quests = File.Exists(Path.Combine(directory, QuestsFile))
            ? ReadList<QuestDef>(directory, QuestsFile).ToDictionary(x => x.Id) : null;
        var content = new WorldContent(maps, world.StartMap, behaviors, loot, world.Regions, world.Links, shops, quests) { LivelyMonsters = true };
        if (File.Exists(Path.Combine(directory, RecipesFile)))
        {
            var receitas = ReadList<RecipeDef>(directory, RecipesFile).ToDictionary(x => x.Id);
            // 13/11: receitas extras em recipes_*.json (Máquina do Caos: armas e lendárias; Joia da Forja; Máscara)
            foreach (var extra in Directory.GetFiles(directory, "recipes_*.json").OrderBy(f => f, StringComparer.Ordinal))
                foreach (var r in ReadList<RecipeDef>(directory, Path.GetFileName(extra)))
                    receitas[r.Id] = r;
            content.Recipes = receitas;
        }
        if (File.Exists(Path.Combine(directory, CropsFile)))
            content.Crops = ReadList<CropDef>(directory, CropsFile).ToDictionary(x => x.Seed);
        return content;
    }

    /// <summary>Lê um mapa (texto JSON) com as mesmas regras do carregamento (editor no jogo).</summary>
    public static MapLayout ParseMap(string json)
        => JsonSerializer.Deserialize<MapLayout>(json, ContentLoader.JsonOptions) ?? throw new InvalidDataException("mapa vazio ou inválido");

    public const string RecipesFile = "recipes.json";
    public const string CropsFile = "crops.json";

    private static List<T> ReadList<T>(string directory, string file)
    {
        var list = Read<List<T>>(directory, file);
        list.AddRange(ContentLoader.ReadExtras<T>(directory, file));
        return list;
    }

    private static T Read<T>(string directory, string file)
    {
        var path = Path.Combine(directory, file);
        if (!File.Exists(path)) throw new FileNotFoundException($"Arquivo de conteúdo ausente: {path}");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ContentLoader.JsonOptions)
               ?? throw new InvalidDataException($"{file}: vazio ou inválido.");
    }
}

/// <summary>14/11: marca exigida que aceita alternativas — "livro:lobisomem|merlin:reinos" = basta ter uma delas.</summary>
public static class Marcas
{
    public static bool Tem(System.Collections.Generic.IEnumerable<string> marcas, string? exigida)
    {
        if (string.IsNullOrEmpty(exigida)) return true;
        var set = marcas as System.Collections.Generic.ISet<string> ?? new System.Collections.Generic.HashSet<string>(marcas);
        foreach (var m in exigida.Split('|')) if (set.Contains(m)) return true;
        return false;
    }
}
