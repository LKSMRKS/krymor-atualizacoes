using CinzasDaMare.Shared.Content;
using System.Collections.Concurrent;
using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// Servidor autoritativo. Dois modos com o MESMO código de login, inventário, equipamento, coleta e persistência:
/// <list type="bullet">
/// <item>Modo arena (sem <see cref="WorldContent"/>): uma arena, um monstro, um item semente — o Marco A validado (testes E2–E7).</item>
/// <item>Modo mapa (E8): mapa com obstáculos, vários monstros com preparação visível, loot por morte, baús, portão-atalho,
/// lobby com Guerreiro/Mago, esquiva e habilidades por classe.</item>
/// </list>
/// Todo o estado de jogo é tocado apenas dentro de Tick(); os eventos de rede só enfileiram trabalho.
/// O relógio é por tick (50 ms), independente do relógio de parede: testes rodam deterministicamente.
/// </summary>
public sealed partial class GameServer
{
    public const int TickMs = 50;
    /// <summary>Id especial do <see cref="MoveSlotIntent"/> para o botão Organizar da mochila.</summary>
    public const string OrganizeItemId = "@organizar";
    public const string MapId = "arena";
    public const string SeedItemOrigin = "seed_arena_sword";
    public const string SeedItemDefinition = "weapon_guerreiro_sword_p1";
    public const string StarterPotionDefinition = "consumable_c01_tonico_fraco";
    public const string WeaponSlot = "weapon_main";
    public const string OffhandSlot = "off_hand";
    public const string BodySlot = "body";
    /// <summary>10/11: companheiro como item (vagalume, dragão, fênix): equipado aqui, ele vem junto.</summary>
    public const string PetSlot = "pet";
    /// <summary>11/11 item 51: asas são item (espaço "asas"): +ataque, +velocidade, +defesa e o voo curto (impulso).</summary>
    public const string WingsSlot = "asas";
    /// <summary>14/11: cliente de outra versão não entra no mundo (o ServerHost liga; testes ficam sem).</summary>
    public bool ExigirVersao { get; set; }
    public const int MinPilha = 100;   // 14/11: pilha mínima de quem empilha
    public const string BraceleteSlot = "bracelete";
    public const string SkinOcultaFlag = "skin_oculta";   // 14/11: skin do bracelete escondida (o bônus continua)   // 13/11: bracelete de skin (aparência por cima da armadura + um bônus pequeno)
    /// <summary>11/11: armadura em peças (luva, bota) e joias (2 anéis, colar). Slots novos = só aditivos no banco.</summary>
    public const string GlovesSlot = "luvas", BootsSlot = "botas", PantsSlot = "calca", Ring1Slot = "anel1", Ring2Slot = "anel2", AmuletSlot = "colar";
    public const string MonsterId = "monster_1";
    public const int MaxCharactersPerAccount = 8;   // 14/11 tarde: 8 vagas (classes novas a caminho); eram 5, como no MU

    private const int SnapshotEveryTicks = 2;
    private const int AutosaveEveryTicks = 600;
    private const int MaxMessagesPerSecond = 120;
    private const int MaxStrikes = 5;
    private const int MaxFailedLogins = 3;
    private const long XpShareWindowMs = 30_000; // GDD 6.2: participação nos últimos 30 s
    private const double XpShareRangeM = 30.0;   // GDD 6.2: até 30 m do encontro

    private sealed record PendingCast(string SkillId, string? TargetId, long ResolveAtMs)
    {
        public double FaceX { get; init; } = 1;
        public double FaceY { get; init; }
        public double AimX { get; init; }
        public double AimY { get; init; }
    }

    /// <summary>Soma dos equipamentos (E8). Só números: a aparência é do cliente.</summary>
    private sealed record GearStats(double ReachM, double MoveSpeedPct, int ProjectileCount, double ProjectileSpeedPct,
        double AoeRadiusBonus, double AttackSpeedFactor)
    {
        public static readonly GearStats None = new(0, 0, 1, 0, 0, 1.0);
        /// <summary>E9: estilo da arma no ataque básico — machado interrompe a preparação; lança tem leque estreito.</summary>
        public bool BasicStagger { get; init; }
        public double BasicArc { get; init; }
        public string? WeaponSkill { get; init; }
        /// <summary>12/11 ("ataques por tipo de arma"): subtipo da arma (sword, axe, club, hammer, dagger, spear, bow...).</summary>
        public string WeaponKind { get; init; } = "";
        public string WeaponId { get; init; } = "";
        public double PowerMult { get; init; } = 1.0;
        /// <summary>11/11 (conjuntos estilo MU): chance extra de crítico e de dano dobrado.</summary>
        public double CritPct { get; init; }
        public double DoublePct { get; init; }
        /// <summary>11/11: atributos exclusivos das peças (botas esquiva, peitoral espinhos/redução, capacete recarga/XP, calça fôlego).</summary>
        public double DodgePct { get; init; }
        public double TenacityPct { get; init; }
        public double HitPct { get; init; }
        public double StunResistPct { get; init; }
        public double MagicResistPct { get; init; }
        public double ThornsPct { get; init; }
        public double DamageReductionPct { get; init; }
        public double CdrPct { get; init; }
        public double XpPct { get; init; }
        public double StaminaRegenPct { get; init; }
        /// <summary>13/11 (dono: "lvl 60 de set full sem recuperação de vida; não SUPREMO, mas mais forte"): vida por segundo,
        /// em fração da vida máxima, também em combate (atributo hp_regen_pct + conjunto completo + nível 20+).</summary>
        public double HpRegenPct { get; init; }
    }

    private sealed partial class Session
    {
        public int ClientId;
        public string? AccountId;
        public bool LobbyAuthed;
        public string? CharacterId;
        public string? Name;
        public string ClassId = "guerreiro";
        public bool HordeMarked;   // 10/11: já recebeu o marcador da horda que o caça
        public PlayerCombatState? Combat;
        public int Level;
        public long Xp;
        public double X, Y, DirX, DirY;
        public double VelX, VelY;                 // velocidade atual (modo mapa): acelera e freia com peso, não muda na hora
        public double FaceX = 1, FaceY = 0; // orientação (vetor unitário); padrão: olhando para +x
        public long LastCombatMs = long.MinValue / 2;
        public long RespawnAtMs;
        /// <summary>Efeitos de status ativos (veneno, lentidão, queimadura, atordoado) → até quando (ms do servidor).</summary>
        public readonly Dictionary<string, long> StatusUntil = new();
        public double DotCarry;
        public long WindowStartMs;
        public long LastChatMs = long.MinValue / 2;
        public readonly CinzasDaMare.Server.Combat.EstadoForma Forma = new();   // 12/11: transformação ativa
        public double FormBaseHealth, FormDrain;
        public long MaskReadyMs;   // 12/11: recarga da Máscara do Pandemônio
        public int MsgCount;
        public int Strikes;
        public int FailedLogins;
        public string? AutoTargetId;
        public long NextBasicAttackAtMs;
        public long BusyUntilMs;
        public PendingCast? Pending;
        // ---- E8 (modo mapa) ----
        public HashSet<string> Flags = new();
        public long UpdWindowMs; public int UpdCount;   // 14/11 20h: cota dos pedidos da atualização automática
        public GearStats Gear = GearStats.None;
        public long AvisoCondicaoAteMs;
        public string? MontariaEscolhida;
        public long DesmontouMs;
        public bool SetCompleto;
        public string? Skin;
        public string? MontariaItem;   // 13/11: bicho do item no espaço Montaria
        public string VersaoCliente = "";   // 13/11: versão do pacote do cliente (aviso de versão diferente)
        public int PetPlus;    // 13/11: refino do pet (+N): dano dele +10% por passo   // 13/11: skin do bracelete (coelho, palhaco, bobo...)
        public string? LugarAtual;   // 13/11: zona com nome em que está (anuncia ao entrar)
        public (string Map, double X, double Y)? VoltaPorta;   // 13/11: de qual porta entrou na casa pequena   // 13/11: montaria escolhida (laboratório); senão a melhor que tiver
        /// <summary>13/11: onde o NPC do duelo (q_duelo_*) espera: ponto real e centro da área que a bússola mostra.</summary>
        public readonly Dictionary<string, (double X, double Y, double AreaX, double AreaY)> DueloPonto = new();   // 13/11: aviso "esse abate não contou" (missão com condição)
        public string? WeaponDef, OffhandDef, BodyDef;
        public string Action = "";
        public bool Sitting;
        public long SentouEm;   // 13/11: descansar no banco dá o "Descansado"
        public bool DescansoDado, NaCama;   // 14/11: o buff sai quando a bolinha enche (banco 5 s, cama 8 s)
        public string? Pose;   // 13/11: pose do jogador (pose_aceno, pose_bracos, pose_deboa, pose_chao, pose_danca)   // 11/11 noite: sentado num banco (qualquer movimento levanta)
        public long ActionStartMs, ActionEndMs;
        public int ActionSeq;
        public double ActionAimX, ActionAimY;
        /// <summary>Raio real da área mirada (habilidade + bônus do equipamento); 0 se não for área.</summary>
        public double ActionAimRadius;
        public double ActionMoveMult = 1.0;
        public long DodgeUntilMs;
        public double DodgeDirX, DodgeDirY;
        public long InvulnerableUntilMs;
        public string? RiposteTarget;             // 11/11: aparou este monstro — o próximo golpe nele é contra-ataque
        public long RiposteUntilMs;
        public long NextDodgeAtMs;
        // ---- E9 ----
        /// <summary>Mapa (ou instância de dungeon) onde o personagem está. Só nulo fora do mundo.</summary>
        public Zone? Zone;
        public long Coins;
        public int StatPoints;
        public Dictionary<string, QuestState> Quests = new();
        public long PortalLockUntilMs;
        public long PortalWarnAtMs;
        public string? HeadDef;
        public bool Sprint;
        public bool Walk;
        public string CollectSig = "";            // última contagem das missões de "trazer X" enviada
        // atributos estilo MU: pontos extras nos que não são o principal da classe; sobrecarga pela armadura
        public int ExtraForca, ExtraAgilidade, ExtraEnergia;
        public string? GlovesDef, BootsDef, PantsDef;
        public int WeaponPlus;
        public int GearVar;   // 12/11: variação média da armadura vestida (brilho dourado no boneco)
        public bool WeaponExcellent;
        public string? WingsCache; public long WingsCacheTick = -1;   // asas vestidas (11/11), lidas uma vez por tick
        public double Weight, Overload;                      // andando (Shift desligado no cliente): passo de caminhada
        public bool LoggedIn => Combat is not null;
    }

    /// <summary>
    /// Um mapa em execução (E9): monstros, itens no chão, projéteis e jogadores DESTE mapa. Os mapas fixos existem desde
    /// o início; uma dungeon tem uma zona por grupo (instância), criada ao entrar e desfeita quando fica vazia.
    /// </summary>
    private sealed partial class Zone
    {
        public string Id = "";
        public MapLayout? Layout;                 // null = arena do Marco A
        public MapGeometry Geo = null!;
        public readonly List<MonsterInstance> Monsters = new();
        public readonly Dictionary<string, WorldItemView> Items = new();
        public readonly Dictionary<string, long> ItemExpiry = new();
        public readonly List<Projectile> Projectiles = new();
        public readonly List<Session> Players = new();
        public readonly List<MonsterInstance> PendingAdds = new();   // ajudantes chamados neste tick (entram depois do laço)
        public bool Instanced;
        /// <summary>12/11: dungeon no modo DIFÍCIL (40+): monstros +5 níveis, mais vida e dano; mais XP e um sorteio extra de loot.</summary>
        public bool Hard;
        public string? OwnerKey;                  // instância: chave do grupo (ou do personagem sozinho)
        public long EmptySinceMs = -1;
        public string LayoutId => Layout?.Id ?? MapId;
        /// <summary>Dungeon: checkpoint alcançado (renasce aqui).</summary>
        public MapCheckpoint? Checkpoint;
        public readonly List<Burn> Burns = new();
        public long NextHordeMs;                  // próxima horda caçadora neste mapa (0 = ainda não sorteada)
    }

    private enum MonsterPhase { Idle, Chase, Windup, Active, Recover, Return, Flee }

    private sealed partial class MonsterInstance
    {
        public string? Tipo;                      // 12/11: natureza (vantagem por classe), calculada na 1ª pancada
        public bool GodSpawned;                   // chamado pelo modo deus: não renasce
        public long ChillUntilMs, FreezeUntilMs;  // 12/11: Estaca de Gelo — congela um instante e deixa lento
        public double ChillMult = 1.0;
        public bool Perched, Diving;              // 11/11 noite: gárgula no poleiro / mergulhando
        public long DiveStartMs, DiveEndMs;
        public double DiveFromX, DiveFromY, DiveToX, DiveToY;
        public string Id = "";
        public MonsterDefinition Def = null!;
        public MonsterCombatState State = null!;
        public double X, Y, SpawnX, SpawnY;
        public Session? Aggro;
        public long RespawnAtMs;
        public int BossWarned;
        // 11/11 (movimento mais realista): arranca e freia (não sai a toda de uma vez), vira com peso, cerca pelos lados
        // e, esperando o golpe recarregar, anda de lado em volta do alvo em vez de ficar parado
        public double MoveSpeedCur;
        public long LastStepMs, StrafeUntilMs;
        public int StrafeDir;                    // 11/11: avisos de "chefe de campo volta em X" já dados nesta espera (bits)
        public readonly Dictionary<Session, long> Damagers = new();
        /// <summary>11/11: ameaça (dano somado) de cada jogador — quem bate mais vira o alvo.</summary>
        public readonly Dictionary<Session, double> Threat = new();
        // ---- E8 (modo mapa) ----
        public MapSpawn? Spawn;
        public MonsterBehaviorDef? Behavior;
        public int Level;
        public double FaceX = 0, FaceY = 1;
        public MonsterPhase Phase = MonsterPhase.Idle;
        public MonsterAttackDef? Attack;
        public long PhaseStartMs, PhaseEndMs;
        public int Seq;
        public double AimX, AimY;
        public readonly HashSet<Session> LungeHits = new();
        public readonly Dictionary<string, long> AttackReadyAt = new();
        public int DeathCount;
        public bool Present = true;   // monstro raro: false quando não apareceu neste ciclo
        public long ThinkAtMs;
        public Zone Zone = null!;
        /// <summary>Quem participou do último abate (dono do loot por alguns segundos).</summary>
        public HashSet<string> LootOwners = new();
        public bool Enraged;                      // E9: 2ª fase do chefe
        public string Affix = "";                 // elite/raro estilo Diablo: rapido | vampiro | blindado | furioso
        public long WeakenUntilMs;                // Aura Primal (Bruxa): bate mais fraco até este instante
        public double WeakenMult = 1.0;
        public double FugaX, FugaY; public long FugaAte;   // 14/11: fujão encurralado escolhe um rumo e corre por ele
        public long MarkedUntilMs;                // Olhar do Caçador (Arqueiro): marcado, leva mais dano
        public MonsterInstance? Master;           // ajudante chamado por um chefe (some quando morre ou o chefe cai)
        public long NextSummonMs;
        public bool Gone;                         // ajudante a retirar da zona no fim do tick
        public bool Hunter;                       // horda caçadora: some ao morrer ou ao desistir
        public string? QuestOwner;                // bicho de missão (caçada/defesa/escolta): só do acontecimento deste personagem
        public int HitIndex;                      // golpe atual da sequência (Hits)
        public long NextHitMs;
        public long WindupLenMs;                  // preparação sem o atraso aleatório (a barra enche e segura)
        // ---- monstros vivos (rodada 3): nascem espalhados, andam à toa, alguns dormem ----
        public double HomeX, HomeY;               // ponto do mapa (centro da região onde nasce)
        public double WanderX, WanderY;
        public bool Wandering, Asleep;
        public long NextWanderMs, WakeAtMs;
    }

    /// <summary>Área que queima no chão (Explosão do Arcanista): dano periódico de quem lançou.</summary>
    private sealed class Burn
    {
        public string Id = "";
        public string Kind = "";
        public double X, Y, Radius, Ratio;
        public long StartMs, EndMs, NextTickMs;
        public Session Owner = null!;
        public SkillDefinition Skill = null!;
    }

    private sealed class Projectile
    {
        public bool HitDuel;                       // 12/11: x1 — já acertou o adversário
        public string Id = "";
        public string Kind = "";
        public double X, Y, DirX, DirY, Speed, Remaining, Radius;
        public bool Hostile;
        public bool Pierce;                       // E9: atravessa inimigos (acerta cada um uma vez)
        public double ExplodeRadius;              // E9: explode ao acertar
        public double Mult = 1.0;                 // Tiro de Emboscada: bônus de quem atirou escondido
        public readonly HashSet<MonsterInstance> HitMonsters = new();
        public Session? Owner;
        public SkillDefinition? Skill;
        public MonsterInstance? Source;
        public MonsterAttackDef? Attack;
        /// <summary>Marcado para sair da lista no fim de UpdateProjectiles (nunca remover no meio do laço).</summary>
        public bool Dead;
    }

    private readonly ContentBundle _content;
    private readonly GameDatabase _db;
    private readonly BalanceConfig _bal;
    private readonly IServerTransport _transport;
    private readonly Func<double> _rng;
    private readonly SkillExecutor _skills;
    private readonly WorldContent? _world;
    private readonly ConcurrentQueue<Action> _inbox = new();
    private readonly Dictionary<int, Session> _sessions = new();
    private readonly Dictionary<string, Zone> _zones = new();
    /// <summary>Zona onde nasce o personagem novo (arena no modo arena; mapa inicial no modo mapa).</summary>
    private readonly Zone _startZone;
    private long _tick;
    private int _nextProjectile;

    /// <summary>11/11: atualizador automático (null = desligado; o ServerHost liga com a pasta do projeto).</summary>
    public ClientUpdates? Updates { get; set; }

    public GameServer(ContentBundle content, GameDatabase db, BalanceConfig balance,
        IServerTransport transport, Func<double>? rng = null, WorldContent? world = null)
    {
        _content = content;
        _db = db;
        _bal = balance;
        _transport = transport;
        _rng = rng ?? Random.Shared.NextDouble;
        _skills = new SkillExecutor(content, _rng);
        _world = world;

        if (world is null)
        {
            // Modo arena (Marco A): exatamente como validado.
            var arena = new Zone { Id = MapId, Geo = new MapGeometry(balance.ArenaHalfSize) };
            _zones[arena.Id] = arena;
            _startZone = arena;
            var def = content.Monsters.TryGetValue("monster_r1_caranguejo_de_cal", out var crab) ? crab : content.Monsters.Values.First();
            arena.Monsters.Add(new MonsterInstance
            {
                Id = MonsterId,
                Def = def,
                State = new MonsterCombatState(MonsterId, def),
                X = balance.MonsterSpawnX, Y = balance.MonsterSpawnY,
                SpawnX = balance.MonsterSpawnX, SpawnY = balance.MonsterSpawnY,
                Zone = arena,
            });

            var seed = db.EnsureSeedItem(SeedItemOrigin, SeedItemDefinition, "comum");
            if (seed is not null)
                arena.Items[seed] = new WorldItemView(seed, SeedItemDefinition, balance.SeedItemX, balance.SeedItemY);
        }
        else
        {
            // 14/11 ("aumenta o que cabe por slot: 20 poções → 100"): tudo que empilha cabe pelo menos 100 por espaço
            static int Pilha(int n) => n > 1 ? Math.Max(n, MinPilha) : n;
            db.StackLimits = content.Consumables.Values.Select(c => (c.Id, Pilha(c.StackLimit)))
                .Concat(content.Items.Values.Where(i => i.StackLimit > 1).Select(i => (i.Id, Pilha(i.StackLimit))))
                .ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var layout in world.Maps.Values.Where(l => !l.Instanced))
                _zones[layout.Id] = CreateZone(layout, layout.Id);
            _startZone = _zones[world.StartMap];
            foreach (var item in db.GetWorldItemsWithPosition())
            {
                // Item sem mapa gravado (E8, mapa único): é do mapa do Vale/inicial. Mapa que sumiu ou instância antiga: some.
                var zone = item.MapId is null ? (_zones.GetValueOrDefault(LegacyItemMap) ?? _startZone) : _zones.GetValueOrDefault(item.MapId);
                if (zone is null) { db.TryRemoveWorldItem(item.ItemId); continue; }
                zone.Items[item.ItemId] = new WorldItemView(item.ItemId, item.DefinitionId, item.X, item.Y, item.Quality);
                zone.ItemExpiry[item.ItemId] = DropLifetimeMs;
            }
        }

        transport.ClientConnected += id => _inbox.Enqueue(() =>
        {
            _sessions[id] = new Session { ClientId = id, WindowStartMs = NowMs };
            GameLog.Info("servidor.rede", $"conexão #{id} aberta");
        });
        transport.ClientDisconnected += id => _inbox.Enqueue(() => RemoveSession(id));
        transport.MessageReceived += (id, frame) => _inbox.Enqueue(() => OnFrame(id, frame));
    }

    public long CurrentTick => _tick;
    /// <summary>Tamanho do último snapshot e o maior já enviado (bytes), para o log de saúde.</summary>
    public int LastSnapshotBytes { get; private set; }
    public int MaxSnapshotBytes { get; private set; }
    public int AliveMonsters => _zones.Values.Sum(z => z.Monsters.Count(m => m.Present && m.State.Alive));
    public int WorldItemCount => _zones.Values.Sum(z => z.Items.Count);
    public int ZoneCount => _zones.Count;

    private static string Who(Session s) => $"{s.Name ?? "?"}#{s.ClientId}";
    private long NowMs => _tick * TickMs;
    public int ConnectedPlayers => _sessions.Values.Count(s => s.LoggedIn);
    private bool MapMode => _world is not null;
    /// <summary>Mapa do E8 (mapa único): itens no chão gravados sem mapa pertencem a ele.</summary>
    private const string LegacyItemMap = "vale_das_cinzas";

    /// <summary>Um passo da simulação. Deve ser chamado sempre pela mesma thread.</summary>
    public void Tick()
    {
        _tick++;
        while (_inbox.TryDequeue(out var work)) work();

        var now = NowMs;
        var dt = TickMs / 1000.0;
        foreach (var s in _sessions.Values.ToList())
            if (s.LoggedIn) UpdatePlayer(s, now, dt);
        foreach (var z in _zones.Values.ToList())
        {
            // 12/11 (editor: "os monstros não arrastam / a marcação fica em lugar aleatório"): com alguém editando o mapa,
            // os monstros dos pontos ficam parados em cima do próprio ponto — clicar no bicho = clicar no ponto
            var editing = MapMode && z.Players.Any(p => p.Editing);
            foreach (var m in z.Monsters)
            {
                if (editing && m.Spawn is { } hold && m.Master is null && !m.GodSpawned)
                {
                    m.X = hold.X; m.Y = hold.Y; m.Aggro = null; m.Phase = MonsterPhase.Idle; m.Attack = null;
                    continue;
                }
                if (m.Behavior is null) UpdateMonster(m, now, dt);
                else UpdateMapMonster(m, now, dt);
            }
            if (z.PendingAdds.Count > 0) { z.Monsters.AddRange(z.PendingAdds); z.PendingAdds.Clear(); }
            if (_tick % 10 == 0) z.Monsters.RemoveAll(m => m.Gone);
            if (MapMode)
            {
                UpdateProjectiles(z, now, dt);
                SeparateMonsters(z);
                if (_tick % 20 == 0) ExpireDrops(z, now);
                if (_tick % 20 == 0) UpdateHorde(z, now);
                if (_tick % 5 == 0) UpdateQuestEvents(z, now);
            }
        }
        if (MapMode) UpdateWorld(now);

        if (_tick % SnapshotEveryTicks == 0)
            foreach (var z in _zones.Values)
                if (z.Players.Count > 0) Broadcast(z, BuildSnapshot(z));
        if (_tick % AutosaveEveryTicks == 0)
            foreach (var s in _sessions.Values)
                if (s.LoggedIn) Save(s);
    }

    /// <summary>Salva todos os personagens conectados (chamar ao desligar).</summary>
    public void Shutdown()
    {
        foreach (var s in _sessions.Values)
            if (s.LoggedIn) Save(s);
    }

    // ---------------- entrada de mensagens ----------------

    private void OnFrame(int clientId, byte[] frame)
    {
        if (!_sessions.TryGetValue(clientId, out var s)) return;
        // 14/11 20h (amigo derrubado no meio da atualização automática: o atualizador pede muitos arquivos pequenos
        // e passava das 120 mensagens/s do anti-robô): pedidos de arquivo da atualização têm a cota própria (600/s)
        if (frame.AsSpan().IndexOf("update_"u8) >= 0)
        {
            var agora = NowMs;
            if (agora - s.UpdWindowMs >= 1000) { s.UpdWindowMs = agora; s.UpdCount = 0; }
            if (++s.UpdCount > 600) return;   // passou da cota: só ignora (o cliente pede de novo), não derruba
        }
        else if (!RateOk(s))
        {
            GameLog.Warn("servidor.rede", $"{Who(s)} passou de {MaxMessagesPerSecond} mensagens/s: desconectado");
            Auditar("spam", $"{s.Name ?? "conexão " + clientId} mandou mais de {MaxMessagesPerSecond} mensagens por segundo e foi desconectado (robô/macro?).", true);
            Disconnect(clientId);
            return;
        }

        var message = NetCodec.Decode(frame);
        if (message is null) { Strike(s, "malformed"); return; }

        switch (message)
        {
            case LoginRequest login:
                HandleLogin(s, login);
                break;
            case LobbyRequest lobby:
                HandleLobby(s, lobby);
                break;
            case CreateCharacterIntent create:
                HandleCreateCharacter(s, create);
                break;
            case DeleteCharacterIntent del:
                HandleDeleteCharacter(s, del);
                break;
            case EnterWorldIntent enter:
                HandleEnterWorld(s, enter);
                break;
            case MoveIntent move:
                if (RequireLogin(s)) HandleMove(s, move);
                break;
            case FaceIntent face:
                if (RequireLogin(s)) HandleFace(s, face);
                break;
            case CastIntent cast:
                if (RequireLogin(s)) HandleCast(s, cast);
                break;
            case ClaimItemIntent claim:
                if (RequireLogin(s)) HandleClaim(s, claim);
                break;
            case AutoAttackIntent auto:
                if (RequireLogin(s)) HandleAutoAttack(s, auto);
                break;
            case EquipIntent equip:
                if (RequireLogin(s)) HandleEquip(s, equip);
                break;
            case UnequipIntent unequip:
                if (RequireLogin(s)) HandleUnequip(s, unequip);
                break;
            case UseItemIntent use:
                if (RequireLogin(s)) HandleUseItem(s, use);
                break;
            case DodgeIntent dodge:
                if (RequireLogin(s)) HandleDodge(s, dodge);
                break;
            case InteractIntent interact:
                if (RequireLogin(s)) HandleInteract(s, interact);
                break;
            case BuybackIntent bb:
                if (RequireLogin(s)) HandleBuyback(s, bb);
                break;
            case BuyIntent buy:
                if (RequireLogin(s)) HandleBuy(s, buy);
                break;
            case SellIntent sell:
                if (RequireLogin(s)) HandleSell(s, sell);
                break;
            case SalvageIntent salvage:
                if (RequireLogin(s)) HandleSalvage(s, salvage);
                break;
            case TradeIntent trade:
                if (RequireLogin(s)) HandleTrade(s, trade);
                break;
            case DropItemIntent drop:
                if (RequireLogin(s)) HandleDropItem(s, drop);
                break;
            case MoveSlotIntent move:
                if (RequireLogin(s))
                {
                    // "@organizar" (10/11 item 53): botão Organizar da mochila — o servidor reempacota tudo
                    if (move.ItemId == OrganizeItemId) { _db.MergeInventoryStacks(s.CharacterId!); _db.OrganizeInventory(s.CharacterId!); }   // 14/11: junta as pilhas antes
                    else _db.TryMoveInventorySlot(s.CharacterId!, move.ItemId ?? "", move.Slot);
                    SendInventory(s);
                }
                break;
            case QuestIntent quest:
                if (RequireLogin(s)) HandleQuest(s, quest);
                break;
            case SpendStatIntent stat:
                if (RequireLogin(s)) HandleSpendStat(s, stat);
                break;
            case TravelIntent travel:
                if (RequireLogin(s)) HandleTravel(s, travel);
                break;
            case ObjetivosIntent obj:
                if (RequireLogin(s)) HandleObjetivos(s, obj);
                break;
            case SkinVisivelIntent skinVis:
                if (RequireLogin(s)) HandleSkinVisivel(s, skinVis);
                break;
            case PartyIntent party:
                if (RequireLogin(s)) HandleParty(s, party);
                break;
            case PaidTravelIntent paid:
                if (RequireLogin(s)) HandlePaidTravel(s, paid);
                break;
            case CraftIntent craft:
                if (RequireLogin(s)) HandleCraft(s, craft);
                break;
            case StorageIntent storage:
                if (RequireLogin(s)) HandleStorage(s, storage);
                break;
            case MountIntent mount:
                if (RequireLogin(s)) HandleMount(s, mount);
                break;
            case WingDashIntent wing:
                if (RequireLogin(s)) HandleWingDash(s, wing);
                break;
            case UpdateCheckIntent uc:
                if (s.LobbyAuthed || RequireLogin(s))   // 14/11: quem foi barrado pela versão também baixa a atualização (do lobby)
                {
                    if (Updates is null) break;
                    Updates.Warm();
                    if (Updates.Page(uc.Page) is { } page) SendTo(s, page);
                    else SendTo(s, new UpdateManifestMessage(0, 0, Array.Empty<UpdateFileInfo>()));   // ainda montando: o cliente tenta de novo
                }
                break;
            case UpdateFileRequest uf:
                if (RequireLogin(s) && Updates is not null) SendTo(s, Updates.Chunk(uf.Path, uf.Offset));
                break;
            case RefineIntent refine:
                if (RequireLogin(s) && MapMode) HandleRefine(s, refine);
                break;
            case FlyIntent fly:
                if (RequireLogin(s) && MapMode) HandleFly(s, fly);
                break;
            case PetIntent pet:
                if (RequireLogin(s)) HandlePet(s, pet);
                break;
            case GodIntent god:
                if (RequireLogin(s)) HandleGod(s, god);
                break;
            case ChatIntent chat:
                if (RequireLogin(s)) HandleChat(s, chat);
                break;
            case TrilhaIntent:
                if (RequireLogin(s) && MapMode) SendTrilha(s);
                break;
            case BestiaryIntent:
                if (RequireLogin(s) && MapMode) SendBestiary(s);
                break;
            case VinteUmIntent vu:
                if (RequireLogin(s) && MapMode) HandleVinteUm(s, vu);
                break;
            default:
                Strike(s, "unexpected_message"); // mensagens de servidor enviadas por cliente
                break;
        }
    }

    private bool RateOk(Session s)
    {
        var now = NowMs;
        if (now - s.WindowStartMs >= 1000) { s.WindowStartMs = now; s.MsgCount = 0; }
        s.MsgCount++;
        // 13/11 (avaliação: o dono foi desconectado por "spam" usando o modo deus/editor): conta de administrador tem
        // folga de 4× (o editor manda várias ordens juntas: seleção múltipla, lotes, miniaturas)
        return s.MsgCount <= MaxMessagesPerSecond || (s.MsgCount <= MaxMessagesPerSecond * 4 && s.LoggedIn && GodAllowed(s));
    }

    private bool RequireLogin(Session s)
    {
        if (s.LoggedIn) return true;
        Strike(s, "not_logged_in");
        return false;
    }

    private void Strike(Session s, string reason)
    {
        s.Strikes++;
        GameLog.Warn("servidor.mensagem", $"{Who(s)} mensagem recusada: {reason} (advertência {s.Strikes}/{MaxStrikes})");
        SendTo(s, new ErrorMessage(reason));
        if (s.Strikes >= MaxStrikes)
        {
            Auditar("mensagem", $"{s.Name ?? "conexão " + s.ClientId} mandou {MaxStrikes} mensagens inválidas ({reason}) e foi desconectado (cliente modificado?).", true);
            Disconnect(s.ClientId);
        }
    }

    // ---------------- login (direto, compatível com o cliente antigo) ----------------

    private void HandleLogin(Session s, LoginRequest m)
    {
        if (s.LoggedIn || s.LobbyAuthed) { Strike(s, "already_in_session"); return; }
        if (m.ProtocolVersion != Protocol.Version)
        {
            SendTo(s, new LoginResult(false, "bad_version", null, null, 0));
            Disconnect(s.ClientId);
            return;
        }

        var accountId = _db.AuthenticateToken(m.Token ?? "");
        if (accountId is null)
        {
            SendTo(s, new LoginResult(false, "invalid_token", null, null, 0));
            GameLog.Warn("servidor.login", $"conexão #{s.ClientId}: token inválido (tentativa {s.FailedLogins + 1})");
            if (++s.FailedLogins >= MaxFailedLogins) Disconnect(s.ClientId);
            return;
        }
        if (_sessions.Values.Any(x => x.AccountId == accountId))
        {
            GameLog.Warn("servidor.login", $"conexão #{s.ClientId}: conta já está no jogo");
            SendTo(s, new LoginResult(false, "already_logged_in", null, null, 0));
            return;
        }

        var characterId = _db.FindCharacterByAccount(accountId);
        var record = characterId is null ? null : _db.LoadCharacter(characterId);
        if (record is null)
        {
            var name = _db.GetAccountName(accountId) ?? accountId;
            characterId = _db.CreateCharacter(accountId, name, "guerreiro", _bal.StartingLevel,
                _bal.StartingStrength, _bal.StartingVigor, MapId, _bal.SpawnX, _bal.SpawnY);
            record = _db.LoadCharacter(characterId)!;
            // Pacote inicial (provisório): poções, só na criação do personagem.
            if (_bal.StartingPotions > 0 && _content.Consumables.ContainsKey(StarterPotionDefinition))
                _db.CreateInventoryItem(record.Id, StarterPotionDefinition, "comum", _bal.StartingPotions,
                    "starter_" + record.Id);
        }
        GrandfatherSkills(record.Id, record.ClassId);
        EnterWorld(s, accountId, record);
    }

    /// <summary>Coloca o personagem no mundo e manda o estado inicial (comum ao login direto e ao lobby).</summary>
    private void EnterWorld(Session s, string accountId, CharacterRecord record)
    {
        if (!_content.Classes.TryGetValue(record.ClassId, out var cls))
        {
            GameLog.Error("servidor.login", $"personagem {record.Name} tem classe desconhecida '{record.ClassId}'");
            SendTo(s, new LoginResult(false, "unknown_class", null, null, 0));
            Disconnect(s.ClientId);
            return;
        }

        var combat = new PlayerCombatState(record.Id, cls, record.Level, record.Strength, record.Vigor,
            _bal.UnarmedWeaponDamage);
        if (record.Health > 0) combat.SetHealth(record.Health);

        s.AccountId = accountId;
        s.CharacterId = record.Id;
        s.Name = record.Name;
        s.ClassId = record.ClassId;
        s.Combat = combat;
        s.Level = record.Level;
        s.Xp = record.Xp;
        s.Coins = record.Coins;
        s.StatPoints = record.StatPoints;
        (s.ExtraForca, s.ExtraAgilidade, s.ExtraEnergia) = _db.GetExtraAttributes(record.Id);
        if (MapMode) s.Quests = _db.GetQuests(record.Id).ToDictionary(q => q.QuestId);
        Zone zone;
        if (MapMode)
        {
            s.Flags = new HashSet<string>(_db.GetFlags(record.Id));
            var (z, x, y) = ResolveEntry(s, record);
            zone = z;
            s.X = x;
            s.Y = y;
            s.FaceX = 0;
            s.FaceY = -1;
        }
        else
        {
            zone = _startZone;
            // Posição salva fora da arena (ex.: a arena ficou menor depois) é trazida para dentro dos limites.
            s.X = Math.Clamp(record.PosX, -_bal.ArenaHalfSize, _bal.ArenaHalfSize);
            s.Y = Math.Clamp(record.PosY, -_bal.ArenaHalfSize, _bal.ArenaHalfSize);
        }
        JoinZone(s, zone);
        if (MapMode)
        {
            LoadBuff(s);
            ConvertPetFlagsToItems(s);
            ConvertMountFlagsToItems(s);   // 13/11: montaria como item
            ConvertWingFlagsToItems(s);   // 11/11: asas viraram item (espaço "asas")
            if (Evolved(s)) GrantSecondClassSkill(s);
            if (s.Flags.Contains(Class3Flag)) GrantThirdClassSkill(s);   // quem já trocou de classe antes de 11/11 também ganha o golpe novo   // 10/11: companheiro virou item (o equipado no espaço "pet" vem junto)
        }
        ApplyEquipment(s);
        if (MapMode) ApplyUpgrades(s);
        GameLog.Info("servidor.login", $"{Who(s)} entrou: {record.ClassId} nível {record.Level}, xp {record.Xp}, vida {s.Combat.Health:0}/{s.Combat.MaxHealth:0}, " +
                                       $"posição ({s.X:0.0}; {s.Y:0.0}) mapa {zone.Id}, salvo em '{record.MapId}'");

        SendTo(s, new LoginResult(true, null, record.Id, record.Name, record.Level));
        if (MapMode)
        {
            SendTo(s, BuildCatalog(cls));
            SendMapState(s);
            SendWorldExtras(s);
            SendTo(s, new NoticeMessage(zone.Layout!.DisplayName, "area"));
            // 13/11 ("meu amigo vê os pets e montarias de outro jeito"): cliente de outra versão desenha coisas erradas — avisa
            if (s.VersaoCliente.Length > 0 && s.VersaoCliente != VersaoJogo.Atual)
            {
                SendTo(s, new NoticeMessage($"ATENÇÃO: seu jogo está na versão {s.VersaoCliente} e o servidor na {VersaoJogo.Atual}. Aplique o atualizacao_pendente.zip mais novo — senão pets, montarias e itens aparecem errados.", "warn"));
                foreach (var host in _sessions.Values.Where(x => x.LoggedIn && !ReferenceEquals(x, s) && GodAllowed(x)))
                    SendTo(host, new NoticeMessage($"{s.Name} entrou com o jogo na versão {s.VersaoCliente} (o servidor está na {VersaoJogo.Atual}).", "warn"));
                GameLog.Warn("servidor.login", $"{Who(s)} entrou com cliente {s.VersaoCliente} (servidor {VersaoJogo.Atual})");
            }
        }
        else
        {
            SendTo(s, new WorldInfoMessage(_bal.ArenaHalfSize, _bal.GuardArcDegrees, MapId));
        }
        // mochila em grade: arruma quem ficou fora do lugar (mochila antiga); o que não couber vai para o depósito
        if (MapMode && _db.MergeInventoryStacks(s.CharacterId!) is > 0 and var juntou)   // 14/11: pilhas de 100
            SendTo(s, new NoticeMessage($"Pilhas agora cabem 100: juntei suas poções e materiais ({juntou} espaço(s) livre(s) na mochila).", "info"));
        if (_db.NormalizeInventory(s.CharacterId!) is > 0 and var stored)
            SendTo(s, new NoticeMessage($"Mochila arrumada na grade nova: {stored} item(ns) foram para o seu depósito no armazém.", "warn"));
        SendInventory(s);
        SendEquipment(s);
        if (MapMode) { EsferasRetroativas(s); PresentesPendentes(s); LoginDoDia(s); }   // 14/11: esfera do macaco virou item; presentes de nível que não couberam
        SendProgress(s, 0);
        SendTo(s, BuildSnapshot(zone));
        OnSessionBack(s);
    }

    // ---------------- movimento ----------------

    private void HandleMove(Session s, MoveIntent m)
    {
        if (!double.IsFinite(m.DirX) || !double.IsFinite(m.DirY)) { Strike(s, "bad_move"); return; }
        var len = Math.Sqrt(m.DirX * m.DirX + m.DirY * m.DirY);
        if (len > 1.0) { s.DirX = m.DirX / len; s.DirY = m.DirY / len; } // cliente não acelera o personagem
        else { s.DirX = m.DirX; s.DirY = m.DirY; }
        s.Sprint = m.Sprint;
        s.Walk = m.Walk;
        if (s.Sitting && (Math.Abs(s.DirX) > 0.05 || Math.Abs(s.DirY) > 0.05)) { s.Sitting = false; s.Action = ""; s.NaCama = false; }
        if (s.Pose is not null && (Math.Abs(s.DirX) > 0.05 || Math.Abs(s.DirY) > 0.05 || s.Action.Length > 0)) s.Pose = null;   // 13/11: pose some ao andar/agir
    }

    private void HandleFace(Session s, FaceIntent m)
    {
        if (!double.IsFinite(m.DirX) || !double.IsFinite(m.DirY)) { Strike(s, "bad_face"); return; }
        var len = Math.Sqrt(m.DirX * m.DirX + m.DirY * m.DirY);
        if (len < 1e-6) return; // vetor nulo: mantém a orientação atual
        // Modo mapa: durante a preparação de um golpe a direção fica travada (não dá para girar e acertar tudo).
        if (MapMode && (s.Pending is not null || NowMs < s.DodgeUntilMs)) return;
        // 13/11 ("chegando de frente no banco, senta virado para o banco"): sentado, quem manda é o banco (costas no encosto)
        if (MapMode && s.Sitting) return;
        s.FaceX = m.DirX / len;
        s.FaceY = m.DirY / len;
    }

    // ---------------- habilidades (com tempo de conjuração) ----------------

    private bool IsBusy(Session s) => s.Pending is not null || NowMs < s.BusyUntilMs;

    private void HandleCast(Session s, CastIntent m)
    {
        if (MapMode) { HandleMapCast(s, m); return; }

        var skillId = m.SkillId ?? "";
        var combat = s.Combat!;
        var now = NowMs;
        var target = FindMonster(s.Zone, m.TargetId);

        // Durante conjuração e recuperação nenhuma outra ação começa (golpes não são cancelados).
        if (IsBusy(s))
        {
            SendTo(s, new CastResultMessage(skillId, false, "Busy", 0, false, m.TargetId, false));
            return;
        }

        var distance = target is null ? 0.0 : Distance(s.X, s.Y, target.X, target.Y);
        var failure = _skills.Validate(combat, skillId, target?.State, distance, now);
        if (failure != CastFailure.None)
        {
            SendTo(s, new CastResultMessage(skillId, false, failure.ToString(), 0, false, target?.Id, false));
            return;
        }

        var skill = _content.Skills[skillId];
        s.BusyUntilMs = now + skill.CastMs + skill.RecoveryMs;
        if (skill.CastMs <= 0)
        {
            ResolveCast(s, skillId, target);
            return;
        }
        // Efeito, custo e recarga só valem no fim da conjuração, com tudo revalidado.
        s.Pending = new PendingCast(skillId, target?.Id, now + skill.CastMs);
        SendTo(s, new CastStartedMessage(skillId, skill.CastMs, skill.RecoveryMs));
    }

    private void ResolveCast(Session s, string skillId, MonsterInstance? target)
    {
        var distance = target is null ? 0.0 : Distance(s.X, s.Y, target.X, target.Y);
        var result = _skills.TryCast(s.Combat!, skillId, target?.State, distance, NowMs);
        SendTo(s, new CastResultMessage(skillId, result.Success, result.Failure.ToString(),
            result.Damage, result.Crit, target?.Id, result.TargetDied));
        if (!result.Success) return;

        s.LastCombatMs = NowMs;
        if (target is null) return; // habilidade de si mesmo (Guarda): o estado aparece no snapshot
        RegisterHit(s, target, skillId, result.Damage, result.Crit, result.TargetDied);
    }

    /// <summary>Efeitos comuns a qualquer golpe do jogador no monstro (habilidade ou ataque básico).</summary>
    private void RegisterHit(Session s, MonsterInstance target, string skillId, int damage, bool crit, bool died,
        bool exposed = false, bool staggered = false)
    {
        Broadcast(target.Zone, new CombatEvent(s.CharacterId!, target.Id, skillId, damage, crit, died, false, exposed, staggered));
        target.Damagers[s] = NowMs;
        target.Threat[s] = target.Threat.GetValueOrDefault(s) + Math.Max(1, damage);
        if (MapMode) FormaOnHit(s, damage, NowMs);   // 12/11: roubo de vida (vampiro)
        if (MapMode && target.Def.DamageMult > 0) RecordeDeGolpe(s, damage);   // 13/11: placar do Barto (boneco de treino não conta)
        if (MapMode && died && target.Def.Boss && s.CharacterId is { } cid && s.Flags.Add("chefe_vencido:" + target.Def.Id)) _db.TryAddFlag(cid, "chefe_vencido:" + target.Def.Id);
        if (!died)
        {
            if (target.Behavior is null) target.Aggro = s;
            else OnMonsterProvoked(target, s);
            return;
        }
        target.Aggro = null;
        if (target.Behavior is null)
        {
            target.RespawnAtMs = NowMs + _bal.MonsterRespawnSeconds * 1000L;
            AwardXp(target);
        }
        else
        {
            OnMapMonsterDied(target);
        }
    }

    /// <summary>13/11 (placar do Barto): guarda o golpe mais forte do personagem numa marca "golpe_max:N" (troca a antiga).</summary>
    private void RecordeDeGolpe(Session s, int damage)
    {
        if (s.CharacterId is not { } cid || damage <= 0) return;
        var atual = s.Flags.Where(f => f.StartsWith("golpe_max:", StringComparison.Ordinal))
            .Select(f => int.TryParse(f.AsSpan(10), out var n) ? n : 0).DefaultIfEmpty(0).Max();
        if (damage <= atual) return;
        s.Flags.RemoveWhere(f => f.StartsWith("golpe_max:", StringComparison.Ordinal));
        s.Flags.Add("golpe_max:" + damage);
        _db.RemoveFlags(cid, "golpe_max:");
        _db.TryAddFlag(cid, "golpe_max:" + damage);
    }

    // ---------------- ataque básico ----------------

    private void HandleAutoAttack(Session s, AutoAttackIntent m)
    {
        if (string.IsNullOrEmpty(m.TargetId))
        {
            s.AutoTargetId = null;
            SendTo(s, new ActionResultMessage("auto_attack", true, "stopped"));
            return;
        }
        var target = FindMonster(s.Zone, m.TargetId);
        if (target is null) { SendTo(s, new ActionResultMessage("auto_attack", false, "no_target")); return; }
        if (!target.State.Alive) { SendTo(s, new ActionResultMessage("auto_attack", false, "target_dead")); return; }
        s.AutoTargetId = target.Id;
        SendTo(s, new ActionResultMessage("auto_attack", true, "started"));
    }

    private void UpdateAutoAttack(Session s, long now)
    {
        if (s.AutoTargetId is null) return;
        var target = FindMonster(s.Zone, s.AutoTargetId);
        if (target is null || !target.State.Alive) { s.AutoTargetId = null; return; }
        if (IsBusy(s) || now < s.NextBasicAttackAtMs) return;
        if (Distance(s.X, s.Y, target.X, target.Y) > _bal.BasicAttackRangeM) return; // não persegue sozinho

        var c = s.Combat!;
        var crit = _rng() < CombatFormulas.BaseCritChance;
        var reduction = CombatFormulas.ArmorReduction(target.State.EffectiveArmor(now), c.Level);
        var damage = CombatFormulas.FinalDamage(c.PhysicalPower, _bal.BasicAttackCoefficient, reduction,
            crit ? CombatFormulas.CritMultiplier : 1.0);
        target.State.TakeDamage(damage);
        s.NextBasicAttackAtMs = now + c.AttackIntervalMs;
        s.LastCombatMs = now;
        RegisterHit(s, target, "basic_attack", damage, crit, !target.State.Alive);
    }

    // ---------------- XP e níveis ----------------

    private void AwardXp(MonsterInstance target, double multiplier = 1.0)
    {
        var now = NowMs;
        var eligible = EligibleParticipants(target, now);
        target.Damagers.Clear();
        target.Threat.Clear();
        foreach (var player in eligible)
            GrantXp(player, (long)Math.Round((MapMode
                ? Progression.MapKillXp(target.State.Level, player.Level, eligible.Count)
                : Progression.KillXp(target.State.Level, player.Level, eligible.Count)) * multiplier * (1 + player.Gear.XpPct)
                * (MapMode && target.Zone is not null ? RotinaXp(target, eligible.Count) : 1.0)));   // 11/11: XP do capacete; 13/11: caçada, dungeon em grupo, eventos
    }

    private List<Session> EligibleParticipants(MonsterInstance target, long now)
    {
        var direct = target.Damagers
            .Where(kv => now - kv.Value <= XpShareWindowMs
                         && IsActive(kv.Key)
                         && kv.Key.Combat!.Alive
                         && ReferenceEquals(kv.Key.Zone, target.Zone)
                         && Distance(kv.Key.X, kv.Key.Y, target.X, target.Y) <= XpShareRangeM)
            .Select(kv => kv.Key)
            .ToList();
        if (!MapMode) return direct;
        // E9: membros do grupo de quem participou, no mesmo mapa e perto, também contam (sem duplicar ninguém).
        var all = new List<Session>(direct);
        foreach (var s in direct)
            foreach (var mate in PartyMatesNear(s, target, now))
                if (!all.Contains(mate)) all.Add(mate);
        return all;
    }

    private void GrantXp(Session s, long gained)
    {
        var (level, xp, levelsGained) = Progression.AddXp(s.Level, s.Xp, gained, MapMode);
        s.Level = level;
        s.Xp = xp;
        if (levelsGained > 0)
        {
            s.Combat!.SetLevel(level);
            GameLog.Info("servidor.progresso", $"{Who(s)} subiu para o nível {level}");
            OnLevelUp(s, levelsGained);
        }
        SendProgress(s, gained);
        Save(s); // persiste na hora: uma queda do servidor não deve perder o abate
    }

    private bool IsActive(Session s)
        => _sessions.TryGetValue(s.ClientId, out var current) && ReferenceEquals(current, s) && s.LoggedIn;

    // ---------------- equipamento ----------------

    private static string? SlotFor(ItemDefinition def) => def.Category switch
    {
        "weapon" => WeaponSlot,
        "offhand" => OffhandSlot,
        "armor" => BodySlot,
        "helmet" => HeadSlot,
        "pet" => PetSlot,
        "wings" or "cape" => WingsSlot,   // 11/11: asa ou capa (o mesmo espaço)
        "gloves" => GlovesSlot,
        "boots" => BootsSlot,
        "pants" => PantsSlot,   // 11/11: armadura em 5 partes, como no MU (calça = coxas)
        "ring" => Ring1Slot,
        "amulet" => AmuletSlot,
        "bracelet" => BraceleteSlot,
        "mount" => MountSlot,   // 13/11: montaria como item
        _ => null,
    };

    private void HandleEquip(Session s, EquipIntent m)
    {
        var itemId = m.ItemId ?? "";
        var entry = _db.GetInventory(s.CharacterId!).FirstOrDefault(e => e.ItemId == itemId);
        if (entry is null) { Reply(s, "equip", false, "not_in_inventory"); return; }
        if (!_content.Items.TryGetValue(entry.DefinitionId, out var def)) { Reply(s, "equip", false, "not_equippable"); return; }
        var slot = SlotFor(def);
        if (slot is null) { Reply(s, "equip", false, "not_equippable"); return; }
        // Estilo MU (rodada 3): armadura, elmo e escudo servem a qualquer classe — o que pesa é a Força (sobrecarga);
        // arma continua da classe (ela define os golpes) e pede o atributo principal.
        var weaponSlot = slot == WeaponSlot;
        if (weaponSlot && !def.AllowedClasses.Contains(s.Combat!.Class.Id)) { Reply(s, "equip", false, "class_not_allowed"); return; }
        // 11/11: conjuntos (capacete, peitoral, luvas e botas do mesmo set) são de uma classe só
        if (def.Set is not null && def.Category is "armor" or "helmet" or "gloves" or "boots" or "pants" && def.AllowedClasses.Count > 0
            && !def.AllowedClasses.Contains(s.Combat!.Class.Id))
        {
            SendTo(s, new NoticeMessage($"{def.DisplayName}: é de outra classe.", "warn"));
            Reply(s, "equip", false, "class_not_allowed"); return;
        }
        if (s.Level < def.MinLevel)
        {
            SendTo(s, new NoticeMessage($"{def.DisplayName}: precisa do nível {def.MinLevel} (você está no {s.Level}).", "warn"));
            Reply(s, "equip", false, "level_too_low"); return;
        }
        if (weaponSlot && s.Combat!.Strength < def.ReqPrimary)
        {
            // 11/11 (amigo: "aparece sintonia necessária"): diz quanto falta e onde pôr os pontos
            var attr = s.Combat.Class.PrimaryName;
            var falta = def.ReqPrimary - s.Combat.Strength;
            SendTo(s, new NoticeMessage($"{def.DisplayName}: precisa de {attr} {def.ReqPrimary} (você tem {s.Combat.Strength}). " +
                (s.StatPoints >= falta ? $"Abra os Atributos (C) e ponha {falta} ponto(s) em {attr}." : $"Faltam {falta}: cada nível dá {GameDatabase.StatPointsPerLevel} pontos para pôr em {attr} (tecla C)."), "warn"));
            Reply(s, "equip", false, "attribute_too_low"); return;
        }
        if (IsBusy(s)) { Reply(s, "equip", false, "busy"); return; }

        // Duas mãos x escudo: arma de duas mãos guarda o escudo na mochila; escudo não entra com arma de duas mãos.
        var equipped = _db.GetEquipment(s.CharacterId!);
        // dois anéis: o segundo vai para o espaço livre
        if (slot == Ring1Slot && equipped.Any(e => e.Slot == Ring1Slot) && equipped.All(e => e.Slot != Ring2Slot)) slot = Ring2Slot;
        if (slot == OffhandSlot)
        {
            var weapon = equipped.FirstOrDefault(e => e.Slot == WeaponSlot);
            if (weapon is not null && _content.Items.TryGetValue(weapon.DefinitionId, out var wd) && wd.Handedness == 2)
            {
                Reply(s, "equip", false, "two_handed");
                return;
            }
        }
        if (slot == WeaponSlot && def.Handedness == 2 && equipped.Any(e => e.Slot == OffhandSlot))
        {
            var off = _db.TryUnequip(s.CharacterId!, OffhandSlot);
            if (!off.Success) { Reply(s, "equip", false, off.Reason); return; }
        }

        var result = _db.TryEquip(s.CharacterId!, itemId, slot);
        if (!result.Success) { Reply(s, "equip", false, result.Reason); return; }
        GameLog.Info("servidor.itens", $"{Who(s)} equipou {def.Id} em {slot}");

        ApplyEquipment(s);
        Reply(s, "equip", true, "ok");
        SendInventory(s);
        SendEquipment(s);
        SendStats(s);   // peso da armadura muda a sobrecarga
    }

    private void HandleUnequip(Session s, UnequipIntent m)
    {
        if (IsBusy(s)) { Reply(s, "unequip", false, "busy"); return; }
        var result = _db.TryUnequip(s.CharacterId!, m.Slot ?? "");
        if (!result.Success) { Reply(s, "unequip", false, result.Reason); return; }

        ApplyEquipment(s);
        Reply(s, "unequip", true, "ok");
        SendInventory(s);
        SendEquipment(s);
        SendStats(s);   // peso da armadura muda a sobrecarga
    }

    /// <summary>Recalcula a arma e os demais equipamentos efetivos a partir do que o banco diz estar equipado.</summary>
    private void ApplyEquipment(Session s)
    {
        var equipped = _db.GetEquipment(s.CharacterId!);
        var weapon = equipped.FirstOrDefault(e => e.Slot == WeaponSlot);
        // 14/11: opções estilo MU do exemplar (Sorte, Opção +N, Excelentes) — somam como atributo de equipamento
        var muExtra = new Dictionary<string, double>();
        foreach (var e in equipped)
            if (_content.Items.TryGetValue(e.DefinitionId, out var md))
                foreach (var a in OpcoesMu.Rolar(e.ItemId, e.Quality, md.Category, md.MinLevel).Linhas)
                    muExtra[a.Stat] = muExtra.GetValueOrDefault(a.Stat) + a.Value;
        var interval = _bal.UnarmedAttackIntervalMs;
        if (weapon is not null && _content.Items.TryGetValue(weapon.DefinitionId, out var def))
        {
            var power = def.BaseStats.TryGetValue("weapon_power", out var p) ? p : _bal.UnarmedWeaponDamage;
            interval = def.BaseStats.TryGetValue("attack_interval_ms", out var iv) ? (int)iv : _bal.UnarmedAttackIntervalMs;
            // 11/11: velocidade de ataque das luvas (e afixos) encurta o intervalo, no máximo 30%
            var asp = Math.Clamp(equipped.Sum(e => _content.Items.TryGetValue(e.DefinitionId, out var d) ? d.BaseStats.GetValueOrDefault("attack_speed_pct") : 0) + muExtra.GetValueOrDefault("attack_speed_pct"), 0, 0.3);
            interval = (int)Math.Round(interval / (1 + asp) * FormaStats(s).IntervalMult * (s.Flags.Contains(Class3Flag) ? Class3Interval : 1));
            s.Combat!.SetWeapon(power, interval);
        }
        else
        {
            s.Combat!.SetWeapon(_bal.UnarmedWeaponDamage, _bal.UnarmedAttackIntervalMs);
        }

        double Stat(string key) => muExtra.GetValueOrDefault(key) + equipped.Sum(e =>
            _content.Items.TryGetValue(e.DefinitionId, out var d) && d.BaseStats.TryGetValue(key, out var v) ? v : 0.0);
        s.GearHealOnKill = Math.Min(0.15, Stat("heal_on_kill_pct"));
        s.GearResourceOnKill = Math.Min(0.25, Stat("resource_on_kill_pct"));
        s.GearCoinPct = Math.Min(0.8, Stat("coin_pct"));

        s.WeaponDef = weapon?.DefinitionId;
        s.OffhandDef = equipped.FirstOrDefault(e => e.Slot == OffhandSlot)?.DefinitionId;
        s.BodyDef = equipped.FirstOrDefault(e => e.Slot == BodySlot)?.DefinitionId;
        s.HeadDef = equipped.FirstOrDefault(e => e.Slot == HeadSlot)?.DefinitionId;
        s.GlovesDef = equipped.FirstOrDefault(e => e.Slot == GlovesSlot)?.DefinitionId;
        s.BootsDef = equipped.FirstOrDefault(e => e.Slot == BootsSlot)?.DefinitionId;
        s.PantsDef = equipped.FirstOrDefault(e => e.Slot == PantsSlot)?.DefinitionId;
        if (MapMode) ApplyPetItem(s, equipped.FirstOrDefault(e => e.Slot == PetSlot)?.DefinitionId);
        var count = (int)Math.Round(Stat("projectile_count"));
        s.Gear = new GearStats(Stat("reach_m"), Stat("move_speed_pct"), Math.Max(1, count), Stat("projectile_speed_pct"),
            Stat("aoe_radius_bonus"), Math.Clamp(interval / 1000.0, 0.5, 2.0))
        {
            BasicStagger = weapon is not null && _content.Items.TryGetValue(weapon.DefinitionId, out var wd1) && wd1.BaseStats.GetValueOrDefault("basic_stagger") > 0,
            BasicArc = weapon is not null && _content.Items.TryGetValue(weapon.DefinitionId, out var wd2) ? wd2.BaseStats.GetValueOrDefault("basic_arc") : 0,
            WeaponSkill = weapon is not null && _content.Items.TryGetValue(weapon.DefinitionId, out var wd3) ? wd3.WeaponSkill : null,
            WeaponKind = weapon is not null && _content.Items.TryGetValue(weapon.DefinitionId, out var wd4) ? wd4.Subtype ?? "" : "",
            WeaponId = weapon?.DefinitionId ?? "",
        };
        // refeição ativa (um efeito por vez) e especialização (evolução 50) entram aqui
        var buff = MapMode ? ActiveBuff(s) : null;
        var armor = Stat("armor") + (buff?.BuffArmor ?? 0);
        var health = Stat("max_health") + (buff?.BuffMaxHealth ?? 0);
        // conjuntos (set): conta as peças vestidas de cada conjunto e soma os bônus que a quantidade liberou
        double setArmor = 0, setHealth = 0, setPower = Stat("power_pct"), setMove = 0, setRegen = 0, setCrit = 0, setDouble = 0, setDodge = 0, setReduce = 0;   // power_pct: asas (11/11)
        s.MontariaItem = equipped.FirstOrDefault(e => e.Slot == MountSlot) is { } mt && _content.Items.TryGetValue(mt.DefinitionId, out var mtd) ? mtd.Subtype : null;
        if (s.MontariaItem is null && s.Mounted is not null && MapMode) Dismount(s, "Você guardou a montaria.");
        s.Skin = !s.Flags.Contains(SkinOcultaFlag) && equipped.FirstOrDefault(e => e.Slot == BraceleteSlot) is { } br && _content.Items.TryGetValue(br.DefinitionId, out var brd) ? brd.Subtype : null;   // 14/11: olhinho esconde a skin
        s.SetCompleto = equipped.Select(e => _content.Items.GetValueOrDefault(e.DefinitionId)).Where(d => d?.Set is not null && d.Category is "helmet" or "armor" or "gloves" or "pants" or "boots")
            .GroupBy(d => d!.Set).Any(g => g.Count() >= 5);   // 13/11: conjunto completo = brilho
        foreach (var grp in equipped.Select(e => _content.Items.GetValueOrDefault(e.DefinitionId)).Where(d => d?.Set is not null).GroupBy(d => d!.Set))
        {
            var bonus = grp.First()!.SetBonus;
            if (bonus is null) continue;
            var n = grp.Count();
            foreach (var (need, stats) in bonus)
                if (int.TryParse(need, out var k) && n >= k)
                {
                    setArmor += stats.GetValueOrDefault("armor"); setHealth += stats.GetValueOrDefault("max_health");
                    setPower += stats.GetValueOrDefault("power_pct"); setMove += stats.GetValueOrDefault("move_speed_pct");
                    setRegen += stats.GetValueOrDefault("resource_regen_pct");
                    setCrit += stats.GetValueOrDefault("crit_pct"); setDouble += stats.GetValueOrDefault("double_pct");
                }
        }
        // afixos sorteados do exemplar (determinísticos pelo id; ver ItemAffixes)
        foreach (var e in equipped)
        {
            if (!_content.Items.TryGetValue(e.DefinitionId, out var ad)) continue;
            foreach (var a in ItemAffixes.Roll(e.ItemId, e.Quality, ad.AffixPool, ad.MinLevel).Concat(ItemAffixes.Feitio(e.ItemId, ad.Category, ad.MinLevel).Linhas))   // 13/11: + feitio
                switch (a.Stat)
                {
                    case "armor": setArmor += a.Value; break;
                    case "max_health": setHealth += a.Value; break;
                    case "power_pct": setPower += a.Value; break;
                    case "move_speed_pct": setMove += a.Value; break;
                    case "resource_regen_pct": setRegen += a.Value; break;
                    case "crit_pct": setCrit += a.Value; break;            // 12/11: linhas das variações de conjunto
                    case "dodge_pct": setDodge += a.Value; break;
                    case "damage_reduction_pct": setReduce += a.Value; break;
                }
        }
        // 11/11: refino +N (estilo MU): armadura/peça +6% da armadura-base por +, arma +3% de poder por +; de +10 em diante, mais
        foreach (var e in equipped.Where(e => e.Temper > 0))
        {
            if (!_content.Items.TryGetValue(e.DefinitionId, out var rd)) continue;
            var steps = Refine.Steps(e.Temper);
            if (e.Slot == WeaponSlot) setPower += 0.03 * steps;
            else if (e.Slot == PetSlot)
            {
                // 13/11 (pedido do Barto: "pet + com a joia — fica mais forte e vai ficando com o mesmo efeito do item"):
                // cada + do pet aumenta em 8% o que ele já dá (poder, vida, armadura, velocidade) e o dano dele (PetPlus)
                var k = 0.08 * steps;
                setPower += rd.BaseStats.GetValueOrDefault("power_pct") * k; setHealth += rd.BaseStats.GetValueOrDefault("max_health") * k;
                setArmor += rd.BaseStats.GetValueOrDefault("armor") * k; setMove += rd.BaseStats.GetValueOrDefault("move_speed_pct") * k;
            }
            else { setArmor += rd.BaseStats.GetValueOrDefault("armor") * 0.06 * steps; setHealth += rd.BaseStats.GetValueOrDefault("max_health") * 0.04 * steps; }
        }
        // 12/11: brilho dourado do boneco = média das variações das 5 peças de armadura (peça sem variação conta 0)
        s.PetPlus = equipped.FirstOrDefault(e => e.Slot == PetSlot)?.Temper ?? 0;
        var armorSlots = new[] { BodySlot, HeadSlot, GlovesSlot, BootsSlot, PantsSlot };
        s.GearVar = (int)Math.Floor(armorSlots.Sum(slot => Variacao.Of(equipped.FirstOrDefault(e => e.Slot == slot)?.Quality)) / 5.0);
        var wEntry = equipped.FirstOrDefault(e => e.Slot == WeaponSlot);
        s.WeaponPlus = wEntry?.Temper ?? 0;
        s.WeaponExcellent = wEntry?.Quality == "excelente" || Variacao.IsExcellent(wEntry?.Quality);
        if (s.WeaponExcellent) Achieve(s, "excelente");
        armor += setArmor; health += setHealth;
        health *= 1 + Math.Min(0.2, Stat("max_health_pct"));   // 14/11: opção Excelente de armadura (MU)
        var forma = FormaStats(s);   // 12/11: transformação ativa
        if (s.Flags.Contains(Class3Flag)) { setPower += Class3Power; armor *= Class3Armor; }   // 12/11: 3ª classe (leão)
        armor *= forma.ArmorMult; health += forma.ExtraHealth; setPower += forma.Power; setMove += forma.Move;
        if (s.Flags.Contains("spec:vanguarda")) { armor *= 1.25; health += 60; }
        s.Gear = s.Gear with
        {
            PowerMult = 1.0 + (buff?.BuffPowerPct ?? 0) + setPower, MoveSpeedPct = s.Gear.MoveSpeedPct + setMove,
            CritPct = setCrit + Stat("crit_pct"), DoublePct = setDouble + Stat("double_pct"),
            DodgePct = Math.Min(0.25, Stat("dodge_pct") + setDodge), ThornsPct = Math.Min(0.3, Stat("thorns_pct") + (s.SetCompleto ? 0.08 : 0)),
            HpRegenPct = Math.Min(0.01, Stat("hp_regen_pct") + (s.SetCompleto ? 0.0015 : 0) + 0.0025 * Math.Clamp((s.Level - 20) / 40.0, 0, 1)), DamageReductionPct = Math.Min(0.4, Stat("damage_reduction_pct") + setReduce),
            CdrPct = Math.Min(0.3, Stat("cdr_pct")), XpPct = Stat("xp_pct"), StaminaRegenPct = Stat("stamina_regen_pct"),
            // 11/11 noite: os 6 que faltavam
            TenacityPct = Math.Min(0.5, Stat("tenacity_pct")), HitPct = Math.Min(0.3, Stat("hit_pct")), StunResistPct = Math.Min(0.5, Stat("stun_resist_pct")),
            MagicResistPct = Math.Min(0.4, Stat("magic_resist_pct") + forma.MagicRes),
        };
        s.Combat!.HealReceivedMult = (1.0 + Math.Min(0.5, Stat("healing_received_pct"))) * forma.HealMult;
        s.Combat!.CostMult = 1.0 - Math.Min(0.3, Stat("mana_cost_pct"));
        s.Combat!.GearCooldownMult = 1.0 - s.Gear.CdrPct;
        // peso: armadura/elmo/escudo pedem Força; sem ela a esquiva demora e o passo fica pesado
        s.Weight = equipped.Sum(e => _content.Items.TryGetValue(e.DefinitionId, out var d) ? Atributos.Weight(e.Slot, d.BaseStats) : 0);
        s.Overload = Atributos.Overload(s.Weight, Attr(s, Atributos.Forca));
        // Energia que não é a principal: +1% de regeneração do recurso por ponto acima da base
        var energyRegen = Atributos.PrimaryKey(s.Combat!.Class.PrimaryAttribute) == Atributos.Energia ? 0 : Math.Max(0, Attr(s, Atributos.Energia) - Atributos.BaseEnergia) * 0.01;
        s.Combat!.HasShield = equipped.Any(e => e.Slot == OffhandSlot && _content.Items.TryGetValue(e.DefinitionId, out var od) && od.Subtype == "shield");
        s.Combat!.SetGear(armor, health, Stat("guard_bonus"), 1.0 + Stat("resource_regen_pct") + (buff?.BuffRegenPct ?? 0) + energyRegen + setRegen);
        s.Combat.SetResourceBonus(Math.Max(0, Attr(s, Atributos.Energia) - Atributos.BaseEnergia) * 1.0);   // 13/11: +1 de recurso máximo por ponto de Energia
    }

    /// <summary>Valor do atributo: o principal da classe vem da coluna antiga; os outros são base + pontos extras.</summary>
    private static int Attr(Session s, string key)
    {
        var c = s.Combat!;
        if (Atributos.PrimaryKey(c.Class.PrimaryAttribute) == key) return c.Strength;
        return key switch
        {
            Atributos.Forca => Atributos.BaseForca + s.ExtraForca,
            Atributos.Agilidade => Atributos.BaseAgilidade + s.ExtraAgilidade,
            Atributos.Energia => Atributos.BaseEnergia + s.ExtraEnergia,
            _ => c.Vigor,
        };
    }

    // ---------------- consumíveis ----------------

    private void HandleUseItem(Session s, UseItemIntent m)
    {
        var itemId = m.ItemId ?? "";
        var entry = _db.GetInventory(s.CharacterId!).FirstOrDefault(e => e.ItemId == itemId);
        if (entry is null) { Reply(s, "use_item", false, "not_in_inventory"); return; }
        if (!_content.Consumables.TryGetValue(entry.DefinitionId, out var consumable))
        {
            Reply(s, "use_item", false, "not_consumable");
            return;
        }

        // Checa recarga/vida ANTES de gastar a unidade; só então consome (transação) e aplica o efeito.
        if (consumable.Transform is not null && MapMode) { UseRelic(s, itemId, consumable); return; }
        if (FormaRestrito(s, "sem_pocoes") && (consumable.HealPercentMaxHealth > 0 || consumable.ResourcePercent > 0))
        {
            SendTo(s, new NoticeMessage($"Na forma de {s.Forma.Ativa!.DisplayName} você não usa poções.", "warn"));
            Reply(s, "use_item", false, "transformed");
            return;
        }
        if (consumable.UpgradeSkill is { } up) { UseUpgradeTome(s, itemId, consumable, up); return; }
        if (consumable.TeachSkill is { } teach) { UseTome(s, itemId, consumable, teach); return; }
        if (MapMode && TryUseDecor(s, itemId, consumable)) return;   // 12/11: decoração da casa
        if (MapMode && TryOpenCaixa(s, itemId, consumable)) return;  // 13/11: Caixa das Cinzas +1..+5
        if (consumable.Id.StartsWith("caixa_pandora_"))   // 14/11: a Pandora abre jogando no chão
        {
            SendTo(s, new NoticeMessage("A Caixa de Pandora não abre na mão: JOGUE NO CHÃO (arraste para fora da mochila) e ela estoura.", "info"));
            Reply(s, "use_item", false, "drop_it");
            return;
        }
        if (MapMode && TryUseMapaTesouro(s, itemId, consumable)) return;   // 13/11: mapa do tesouro
        if (MapMode && consumable.Montaria is { } mont)   // 13/11: apito da vendedora de montarias
        {
            if (s.Flags.Contains("mount:" + mont)) { SendTo(s, new NoticeMessage("Você já tem essa montaria (V monta; toque duas vezes para trocar).", "info")); return; }
            if (s.Level < consumable.MinLevel) { SendTo(s, new NoticeMessage($"Essa montaria pede nível {consumable.MinLevel}.", "warn")); return; }
            if (!_db.TryConsumeOne(s.CharacterId!, itemId).Success) { Reply(s, "use_item", false, "not_in_inventory"); return; }
            GrantFlags(s, new[] { "mount:" + mont });
            EscolherMontaria(s, mont);
            SendInventory(s);
            SendTo(s, new NoticeMessage($"Montaria nova: {consumable.DisplayName.Replace("Apito: ", "")}! Aperte V para montar.", "reward"));
            return;
        }
        var check = ConsumableSystem.CanUse(s.Combat!, consumable, NowMs);
        if (check != UseFailure.None)
        {
            Reply(s, "use_item", false, check switch { UseFailure.Dead => "dead", UseFailure.WrongClass => "class_not_allowed", _ => "on_cooldown" });
            return;
        }
        var consumed = _db.TryConsumeOne(s.CharacterId!, itemId);
        if (!consumed.Success) { Reply(s, "use_item", false, consumed.Reason); return; }

        ConsumableSystem.TryUse(s.Combat!, consumable, NowMs);
        foreach (var cure in consumable.Cures ?? (consumable.Id.Contains("antidoto") ? new[] { "veneno" } : consumable.Id.Contains("bandagem") || consumable.Id.Contains("atadura") ? new[] { "sangramento" }
                 : consumable.Id.Contains("agua_benta") ? new[] { "maldicao" } : consumable.Id.Contains("panaceia") ? new[] { "veneno", "sangramento", "maldicao", "queimadura", "lentidao" } : Array.Empty<string>()))
            if (s.StatusUntil.Remove(cure)) SendTo(s, new NoticeMessage($"Curado: {StatusName(cure)}.", "info"));
        if (consumable.Buff is not null && MapMode) ApplyFood(s, consumable);
        Reply(s, "use_item", true, "ok");
        SendInventory(s);
    }

    // ---------------- coleta ----------------

    private void HandleClaim(Session s, ClaimItemIntent m)
    {
        var requestId = m.RequestId ?? "";
        var itemId = m.ItemId ?? "";
        if (requestId.Length == 0 || requestId.Length > 64 || itemId.Length == 0 || itemId.Length > 64)
        {
            Strike(s, "bad_claim");
            return;
        }

        // Só rejeita por distância enquanto o item ainda está no chão. Reenvios de uma coleta já feita
        // chegam ao banco para receberem o resultado gravado (idempotência).
        var zone = s.Zone!;
        if (zone.Items.TryGetValue(itemId, out var onGround)
            && Distance(s.X, s.Y, onGround.X, onGround.Y) > _bal.PickupRangeM)
        {
            SendTo(s, new ClaimResultMessage(requestId, false, "out_of_range", -1));
            return;
        }
        if (onGround is not null && LootBlocked(s, itemId))
        {
            SendTo(s, new ClaimResultMessage(requestId, false, "not_yours_yet", -1));
            return;
        }

        // O ID de requisição é escopado ao personagem: um jogador não consegue reaproveitar o ID de outro.
        var result = _db.TryClaimWorldItem($"{s.CharacterId}:{requestId}", itemId, s.CharacterId!);
        if (result.Success || result.Reason is "already_taken" or "unknown_item")
        {
            zone.Items.Remove(itemId);
            zone.ItemExpiry.Remove(itemId);
        }

        SendTo(s, new ClaimResultMessage(requestId, result.Success, result.Reason, result.Slot));
        if (result.Success) SendInventory(s);
        if (result.Success && MapMode && onGround is not null) ShareQuestItem(s, onGround.DefinitionId, requestId);
        GameLog.Write(result.Success ? LogLevel.Info : LogLevel.Debug, "servidor.itens",
            $"{Who(s)} coleta {(onGround?.DefinitionId ?? itemId)}: {(result.Success ? "ok" : result.Reason)}");
    }

    // ---------------- simulação ----------------

    private void UpdatePlayer(Session s, long now, double dt)
    {
        if (MapMode) { UpdateMapPlayer(s, now, dt); return; }

        var c = s.Combat!;
        if (!c.Alive)
        {
            if (now >= s.RespawnAtMs) Respawn(s);
            return;
        }

        if (s.Pending is { } pending && now >= pending.ResolveAtMs)
        {
            s.Pending = null;
            ResolveCast(s, pending.SkillId, FindMonster(s.Zone, pending.TargetId));
        }
        UpdateAutoAttack(s, now);

        var inCombat = now - s.LastCombatMs < _bal.CombatTimeoutSeconds * 1000L;
        c.TickResource(dt, inCombat, now);
        c.AdvanceHeal(now);
        if (!inCombat) c.RegenOutOfCombat(dt);

        if (s.DirX != 0 || s.DirY != 0)
        {
            var step = _bal.MoveSpeedMps * c.MoveSpeedMultiplier(now) * dt;
            s.X = Math.Clamp(s.X + s.DirX * step, -_bal.ArenaHalfSize, _bal.ArenaHalfSize);
            s.Y = Math.Clamp(s.Y + s.DirY * step, -_bal.ArenaHalfSize, _bal.ArenaHalfSize);
        }
    }

    private void Respawn(Session s)
    {
        var (deadX, deadY) = (s.X, s.Y);
        s.Combat!.Revive();
        s.Stamina = StaminaMax; s.Winded = false;
        var layout = s.Zone?.Layout;
        s.X = s.Zone?.Checkpoint?.X ?? layout?.SpawnX ?? _bal.SpawnX;   // dungeon: renasce no último checkpoint
        s.Y = s.Zone?.Checkpoint?.Y ?? layout?.SpawnY ?? _bal.SpawnY;
        s.DirX = 0;
        s.DirY = 0;
        s.FaceX = MapMode ? 0 : 1;
        s.FaceY = MapMode ? -1 : 0;
        s.LastCombatMs = long.MinValue / 2;
        s.Action = "";
        // 13/11 (dono: "se cair de nível, sou jogado para fora da região, para o ponto permitido mais perto"): mapa travado
        // por nível acima do seu → renasce do lado de fora da passagem que leva até ele
        if (MapMode && s.Zone is { Instanced: false } zt && EntradaTravada(zt.LayoutId, s.Level) is { } fora && _zones.TryGetValue(fora.Map, out var zf))
        {
            MoveToZone(s, zf, fora.X, fora.Y);
            SendTo(s, new NoticeMessage($"Seu nível não basta mais para {zt.Layout!.DisplayName}: você acordou do lado de fora. Volte no nível {fora.Nivel}.", "warn"));
            return;
        }
        // 11/11: fora de dungeon, renasce na última pedra de viagem (mesmo se for em outro mapa)
        // 12/11: no mapa onde morreu, o ponto de renascer mais perto: pedra de viagem OU portal dentro de área segura
        // (ex.: Costa do Cal → portão do Porto, que não tem pedra). Sem nenhum no mapa, volta à última pedra (outro mapa).
        if (MapMode && s.Zone is { Instanced: false, Checkpoint: null } && NearestRespawnPoint(s.Zone, deadX, deadY) is { } near)
        {
            (s.X, s.Y) = s.Zone.Geo.Resolve(near.X, near.Y, PlayerRadius, s.Flags);
            return;
        }
        if (MapMode && s.Zone is { Instanced: false, Checkpoint: null } && LastWaypoint(s) is { } wp)
        {
            if (wp.Layout.Id == s.Zone.Id) (s.X, s.Y) = s.Zone.Geo.Resolve(wp.X, wp.Y, PlayerRadius, s.Flags);
            else if (_zones.TryGetValue(wp.Layout.Id, out var z)) MoveToZone(s, z, wp.X, wp.Y);
        }
    }

    /// <summary>
    /// Pedras de viagem do mapa e portais que ficam em área segura; o portal devolve um ponto um pouco para dentro do
    /// mapa (renascer em cima do portal faria viajar na hora).
    /// </summary>
    private static (double X, double Y)? NearestRespawnPoint(Zone zone, double x, double y)
    {
        if (zone.Layout is not { } map) return null;
        (double X, double Y)? best = null;
        var bestD = double.MaxValue;
        void Consider(double px, double py)
        {
            var d = (px - x) * (px - x) + (py - y) * (py - y);
            if (d < bestD) { bestD = d; best = (px, py); }
        }
        foreach (var w in map.Waypoints) Consider(w.X + 1.2, w.Y + 1.6);   // ao lado da pedra, não dentro
        foreach (var p in map.Portals)
        {
            var safe = zone.Geo.InSafeZone(p.X, p.Y)
                       || (map.SafeRadius > 0 && Math.Sqrt((p.X - map.SpawnX) * (p.X - map.SpawnX) + (p.Y - map.SpawnY) * (p.Y - map.SpawnY)) <= map.SafeRadius + p.Radius + 4)
                       || map.Zones.Any(z => z.Kind is "seguro" or "refugio" && Math.Abs(p.X - z.X) <= z.W / 2 && Math.Abs(p.Y - z.Y) <= z.H / 2);
            if (!safe) continue;
            var len = Math.Sqrt(p.X * p.X + p.Y * p.Y);
            var step = p.Radius + 3.0;
            Consider(len > 1e-6 ? p.X - p.X / len * step : p.X, len > 1e-6 ? p.Y - p.Y / len * step : p.Y + step);
        }
        return best;
    }

    private void KillPlayerState(Session s, long now)
    {
        GameLog.Info("servidor.combate", $"{Who(s)} morreu em ({s.X:0.0}; {s.Y:0.0})");
        s.RespawnAtMs = now + _bal.PlayerRespawnSeconds * 1000L;
        s.DirX = 0;
        s.DirY = 0;
        s.Pending = null;          // morte cancela a conjuração
        s.Gather = null;
        s.Mounted = null;
        s.Carry = null;
        if (s.Forma.Ativa is not null) { s.Forma.Encerrar(); s.FormBaseHealth = 0; }
        s.Flying = false;
        if (s.PetKind is not null) s.PetDownUntilMs = now + PetDownMs;
        s.AutoTargetId = null;
        s.BusyUntilMs = 0;
        s.Action = "";
        s.DodgeUntilMs = 0;
        foreach (var m in s.Zone!.Monsters)
            if (ReferenceEquals(m.Aggro, s)) m.Aggro = null;
        s.StatusUntil.Clear();
        DeathPenalty(s);
        CondicaoMorte(s);
    }

    /// <summary>
    /// Penalidade de morte (10/11, decisão do usuário): 20% da experiência do nível atual, sem cair de nível.
    /// </summary>
    internal const int DeathHeavyLevel = 20;
    internal const double DeathXpPct = 0.20;
    /// <summary>13/11: a partir deste nível a morte pode derrubar um nível.</summary>
    internal const int DeathLevelDownFrom = 30;

    private double DeathPct(Session s) => s.Flags.Contains(Class3Flag) ? 0.30 : Evolved(s) ? 0.25 : DeathXpPct;

    /// <summary>Passagem com trava de nível (acima do seu) que leva a este mapa: ponto 3 m para dentro do mapa de origem.</summary>
    private (string Map, double X, double Y, int Nivel)? EntradaTravada(string mapId, int nivel)
    {
        foreach (var m in _world!.Maps.Values)
            foreach (var p in m.Portals)
            {
                if (p.ToMap != mapId || !p.LevelLock || p.MinLevel <= nivel) continue;
                var len = Math.Sqrt(p.X * p.X + p.Y * p.Y);
                var (x, y) = len > 1e-6 ? (p.X - p.X / len * (p.Radius + 3), p.Y - p.Y / len * (p.Radius + 3)) : (p.X, p.Y + 3);
                return (m.Id, x, y, p.MinLevel);
            }
        return null;
    }

    /// <summary>13/11: caiu de nível — o que pede nível maior sai do corpo para a mochila até você subir de novo.</summary>
    private void TirarAcimaDoNivel(Session s)
    {
        var tirou = new List<string>();
        foreach (var e in _db.GetEquipment(s.CharacterId!))
        {
            if (!_content.Items.TryGetValue(e.DefinitionId, out var d) || d.MinLevel <= s.Level) continue;
            if (_db.TryUnequip(s.CharacterId!, e.Slot).Success) tirou.Add(d.DisplayName);
            else SendTo(s, new NoticeMessage($"{d.DisplayName} pede nível {d.MinLevel}: abra espaço na mochila para tirá-lo.", "warn"));
        }
        if (tirou.Count == 0) return;
        ApplyEquipment(s);
        SendInventory(s);
        SendEquipment(s);
        SendTo(s, new NoticeMessage($"Nível baixo demais para: {string.Join(", ", tirou)} — foi para a mochila até você subir de novo.", "warn"));
    }

    private void DeathPenalty(Session s)
    {
        if (!MapMode || _world?.LivelyMonsters != true || s.CharacterId is null) return;
        // 10/11 (escolha do usuário na lista de prioridades: "20% FIXO"): morrer tira 20% da experiência que você
        // juntou no nível atual — nunca zera de uma vez e nunca cai de nível, em qualquer nível
        var from = s.Level;
        _db.RaiseMaxLevel(s.CharacterId, from);
        // 12/11 ("na classe 2 sobe de 20 para 25%, na classe 3 de 25 para 30%"): quem evoluiu arrisca mais
        var pct = DeathPct(s);
        long lostXp;
        if (s.Level >= DeathLevelDownFrom)
        {
            // 13/11 (dono: "cair de nível só depois do midgame, nível 30+"): do 30 em diante a perda é a fração da barra
            // INTEIRA do nível; se não houver XP para pagar, cai um nível (nunca abaixo do 30). Pontos de atributo e prêmios
            // não voltam a ser dados ao reconquistar (maior nível já alcançado fica gravado).
            lostXp = (long)Math.Round(Progression.XpToNext(s.Level, MapMode) * pct);
            if (s.Xp >= lostXp || s.Level <= DeathLevelDownFrom) { s.Xp = Math.Max(0, s.Xp - lostXp); }
            else
            {
                var falta = lostXp - s.Xp;
                s.Level -= 1;
                s.Xp = Math.Max(0, Progression.XpToNext(s.Level, MapMode) - falta);
                s.Combat!.SetLevel(s.Level);
                TirarAcimaDoNivel(s);
                SendStats(s);
            }
        }
        else
        {
            lostXp = (long)Math.Round(s.Xp * pct);
            s.Xp -= lostXp;
        }
        SendTo(s, new NoticeMessage(s.Level < from ? $"Você morreu, perdeu {lostXp:N0} de experiência e caiu para o nível {s.Level}."
            : lostXp > 0 ? $"Você morreu e perdeu {lostXp:N0} de experiência ({pct * 100:0}% {(from >= DeathLevelDownFrom ? "da barra do nível" : "do que tinha no nível")})."
            : "Você morreu (sem experiência para perder neste nível).", "info"));
        GameLog.Info("servidor.progresso", $"{Who(s)} morreu: nível {from} → {s.Level}, perdeu {lostXp} xp");
        SendProgress(s, 0);
        Save(s);
        foreach (var k in _memberInfo.Keys.Where(k => k == s.CharacterId).ToList()) _memberInfo[k] = (s.Name!, s.ClassId, s.Level);
    }

    /// <summary>IA do Marco A (modo arena): ataque instantâneo ao alcance. Mantida sem mudança.</summary>
    private void UpdateMonster(MonsterInstance m, long now, double dt)
    {
        if (!m.State.Alive)
        {
            if (m.RespawnAtMs > 0 && now >= m.RespawnAtMs)
            {
                m.State = new MonsterCombatState(m.Id, m.Def);
                m.X = m.SpawnX;
                m.Y = m.SpawnY;
                m.Aggro = null;
                m.RespawnAtMs = 0;
                m.Damagers.Clear();
            }
            return;
        }

        var target = m.Aggro;
        if (target is null || !target.LoggedIn || !target.Combat!.Alive || !_sessions.ContainsKey(target.ClientId))
        {
            target = null;
            var best = _bal.MonsterAggroRadiusM;
            foreach (var s in m.Zone.Players)
            {
                if (!s.LoggedIn || !s.Combat!.Alive) continue;
                var d = Distance(s.X, s.Y, m.X, m.Y);
                if (d <= best) { best = d; target = s; }
            }
            m.Aggro = target;
        }

        if (target is null)
        {
            MoveToward(m, m.SpawnX, m.SpawnY, dt);
            return;
        }

        if (Distance(target.X, target.Y, m.X, m.Y) > _bal.MonsterAttackRangeM)
        {
            MoveToward(m, target.X, target.Y, dt);
            return;
        }

        // Golpe frontal = o monstro está dentro do arco que o jogador encara (orientação enviada pelo cliente).
        var frontal = CombatGeometry.IsFrontal(target.FaceX, target.FaceY, target.X, target.Y, m.X, m.Y, _bal.GuardArcDegrees);
        var hit = MonsterCombat.TryAttack(m.State, target.Combat!, frontal, now);
        if (!hit.Hit) return;
        target.LastCombatMs = now;
        Broadcast(m.Zone, new CombatEvent(m.Id, target.CharacterId!, "monster_basic", hit.Damage, false, hit.PlayerDied));
        if (hit.PlayerDied)
        {
            KillPlayerState(target, now);
            m.Aggro = null;
        }
    }

    private void MoveToward(MonsterInstance m, double tx, double ty, double dt)
    {
        var dx = tx - m.X;
        var dy = ty - m.Y;
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 0.01) return;
        var step = Math.Min(d, _bal.MonsterMoveSpeedMps * dt);
        m.X += dx / d * step;
        m.Y += dy / d * step;
    }

    private static MonsterInstance? FindMonster(Zone? zone, string? id)
        => string.IsNullOrEmpty(id) || zone is null ? null : zone.Monsters.FirstOrDefault(x => x.Id == id);

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ---------------- saída ----------------

    private Snapshot BuildSnapshot(Zone z)
    {
        var now = NowMs;
        var players = z.Players.Where(s => s.LoggedIn).Select(s => BuildPlayerView(s, now)).ToList();
        var monsters = z.Monsters.Where(m => m.Present).Select(m => BuildMonsterView(m, now)).ToList();
        if (!MapMode) return new Snapshot(_tick, players, monsters, z.Items.Values.ToList());
        var projectiles = z.Projectiles.Where(p => !p.Dead).Select(p => new ProjectileView(p.Id, p.Kind, p.X, p.Y, p.DirX, p.DirY, p.Speed, p.Hostile)).ToList();
        var effects = z.Burns.Count == 0 && z.Kegs.Count == 0 ? null : z.Burns.Select(b => new GroundEffectView(b.Id, b.Kind, b.X, b.Y, b.Radius,
            Math.Clamp((now - b.StartMs) / (double)Math.Max(1, b.EndMs - b.StartMs), 0, 1))).Concat(KegViews(z, now)).Concat(TrapViews(z, now)).ToList();
        return new Snapshot(_tick, players, monsters, z.Items.Values.ToList(), projectiles, effects, BuildPets(z, now));
    }

    private PlayerView BuildPlayerView(Session s, long now)
    {
        var c = s.Combat!;
        if (!MapMode)
            return new PlayerView(s.CharacterId!, s.Name!, s.X, s.Y, c.Health, c.MaxHealth,
                c.Resource, c.Alive, c.IsGuarding(now), s.Level, s.FaceX, s.FaceY);
        var (action, t) = PlayerAction(s, now);
        return new PlayerView(s.CharacterId!, s.Name!, s.X, s.Y, c.Health, c.MaxHealth,
            c.Resource, c.Alive, c.IsGuarding(now), s.Level, s.FaceX, s.FaceY,
            s.ClassId, action, t, s.ActionSeq, s.WeaponDef, s.OffhandDef, s.BodyDef, c.MaxResource,
            s.ActionAimX, s.ActionAimY, s.ActionAimRadius, s.HeadDef,
            _partyOf.TryGetValue(s.CharacterId!, out var party) ? party.Id : null,
            s.Mounted is not null && now >= s.MountReadyAtMs ? s.Mounted : null, OwnedWings(s), Spec(s),
            s.WeaponDef is not null && _content.Items.TryGetValue(s.WeaponDef, out var wdef) && wdef.Quality == "lendario",
            now < s.ShieldUntilMs ? s.Shield : 0, now < s.StealthUntilMs,
            string.Join(",", s.StatusUntil.Where(kv => kv.Value > now).Select(kv => kv.Key)), Evolved(s), Math.Round(s.Stamina, 1), s.Carry, s.GlovesDef, s.BootsDef, s.Flying, s.WeaponPlus, s.WeaponExcellent, TitleOf(s), s.PantsDef, s.GearVar, FormVisual(s, now), s.Flags.Contains(Class3Flag), s.SetCompleto, s.Skin);
    }

    private MonsterView BuildMonsterView(MonsterInstance m, long now)
    {
        if (m.Behavior is null)
            return new MonsterView(m.Id, m.Def.SpeciesName, m.X, m.Y, m.State.Health, m.State.Profile.Health, m.State.Alive);
        var (action, t) = MonsterActionText(m, now);
        var lift = PerchLift(m, now);
        if (m.Perched) (action, t) = ("perch", 0);
        else if (m.Diving) (action, t) = ("dive", Math.Clamp((now - m.DiveStartMs) / (double)Math.Max(1, m.DiveEndMs - m.DiveStartMs), 0, 1));
        double aimX = 0, aimY = 0, aimRadius = 0, aimArc = 0;
        if (m.Attack is { } a && m.Phase is MonsterPhase.Windup or MonsterPhase.Active)
        {
            switch (a.Shape)
            {
                case "circle": aimX = m.AimX; aimY = m.AimY; aimRadius = a.Radius; aimArc = 0; break;
                // A marca é exatamente a área de acerto (mesmas funções de BeginActive/bote): quem está com o corpo
                // fora dela não é atingido (auditoria E8, item 2).
                case "cone": aimX = m.X; aimY = m.Y; aimRadius = ConeReach(m, a); aimArc = a.Arc; break;
                case "lunge": aimX = m.X; aimY = m.Y; aimRadius = a.LungeM + LungeHalfWidth(m, a); aimArc = -2 * LungeHalfWidth(m, a); break;
                case "beam": aimX = m.X; aimY = m.Y; aimRadius = BeamLength(m, a); aimArc = -2 * a.Radius; break;
                // projéteis em leque: marca curta mostrando a abertura (avisa que vem areia/flechas)
                // 10/11 ("3 pedras = 3 riscos, com o alcance certo"): um risco por projétil, do tamanho do voo real
                case "projectile": aimX = m.X; aimY = m.Y; aimRadius = a.Range; aimArc = a.Count > 1 ? a.SpreadDeg : 0; break;
            }
        }
        return new MonsterView(m.Id, m.Def.SpeciesName, m.X, m.Y, m.State.Health, m.State.Profile.Health, m.State.Alive,
            m.Def.Id, m.Level, action, t, m.Seq, m.FaceX, m.FaceY, aimX, aimY, aimRadius, m.Def.Rare, m.Def.Boss, aimArc,
            m.Def.VisualAssetId, m.Def.Elite, m.Enraged, now < m.WeakenUntilMs, now < m.MarkedUntilMs,
            m.Attack is { Shape: "beam" } && m.Phase is MonsterPhase.Windup or MonsterPhase.Active ? "beam"
                // 12/11: sopro de fogo (gárgula): o cliente desenha chamas no cone em vez de só a marca
                : m.Attack is { Shape: "cone" } fa && fa.Id.Contains("fogo") && m.Phase is MonsterPhase.Windup or MonsterPhase.Active ? "fogo"
                : m.Attack is { Shape: "projectile" } pa && m.Phase is MonsterPhase.Windup or MonsterPhase.Active ? $"raios{Math.Max(1, pa.Count)}:{pa.Radius:0.00}"
                // 13/11 ("o ataque de 3 golpes: em vez de uma marcação, uma dividida em 3, na mesma reta"): golpes em
                // sequência que avançam — o cliente desenha uma marca por golpe que falta, uma à frente da outra
                : m.Attack is { Shape: "cone" or "lunge", Hits: > 1, StepM: > 0 } ga && m.Phase is MonsterPhase.Windup or MonsterPhase.Active
                    ? FormattableString.Invariant($"golpes{ga.Hits}:{ga.StepM:0.00}:{(m.Phase == MonsterPhase.Active ? Math.Max(0, m.HitIndex - 1) : 0)}") : "", m.Affix,
            MonsterSize(m), lift, now < m.ChillUntilMs);
    }

    /// <summary>
    /// Variante pelo nível (pedido do usuário): a mesma espécie num ponto mais longe e de nível maior nasce um pouco maior
    /// — +4% de tamanho por nível acima do nível-base da espécie (até +40%), −4% por nível abaixo (até −10%).
    /// Só aparência; força e vida já vêm do nível.
    /// </summary>
    private static double MonsterSize(MonsterInstance m)
        => m.Def.Boss ? 1.0 : Math.Clamp(1.0 + 0.04 * (m.Level - m.Def.Level), 0.9, 1.4);

    private void SendInventory(Session s)
    {
        var items = _db.GetInventory(s.CharacterId!)
            .Select(e => new InventoryItemView(e.ItemId, e.DefinitionId, e.Quality, e.Slot, e.Quantity, e.Temper)).ToList();
        SendTo(s, new InventoryMessage(items, _db.GridOn ? _db.GridColumns : 0, _db.GridOn ? _db.RowsFor(s.CharacterId!) : 0));
        // missões de "trazer X": o contador vem da mochila — sem isso a tela ficava parada até matar algo (feedback)
        if (MapMode && s.Quests.Any(kv => kv.Value.State == "active" && _world!.Quests.TryGetValue(kv.Key, out var q) && q.Kind == "collect"))
        {
            var sig = string.Join(",", s.Quests.Where(kv => kv.Value.State == "active" && _world!.Quests[kv.Key].Kind == "collect")
                .Select(kv => kv.Key + ":" + _db.CountInInventory(s.CharacterId!, _world!.Quests[kv.Key].Targets[0])));
            if (sig != s.CollectSig) { s.CollectSig = sig; SendQuestLog(s); }
        }
    }

    private void SendEquipment(Session s)
    {
        var items = _db.GetEquipment(s.CharacterId!)
            .Select(e => new EquippedItemView(e.Slot, e.ItemId, e.DefinitionId, e.Quality, e.Temper)).ToList();
        SendTo(s, new EquipmentMessage(items));
    }

    private void SendProgress(Session s, long gained)
        => SendTo(s, new ProgressMessage(s.Level, s.Xp, Progression.XpToNext(s.Level, MapMode), gained));

    private void Reply(Session s, string action, bool success, string reason)
        => SendTo(s, new ActionResultMessage(action, success, reason));

    private void SendTo(Session s, NetMessage message) => _transport.Send(s.ClientId, NetCodec.Encode(message));

    /// <summary>Envia a quem está na zona (snapshot, golpes): jogadores de outro mapa não recebem.</summary>
    private void Broadcast(Zone zone, NetMessage message)
    {
        var bytes = NetCodec.Encode(message);
        if (message is Snapshot)
        {
            LastSnapshotBytes = bytes.Length;
            if (bytes.Length > MaxSnapshotBytes) MaxSnapshotBytes = bytes.Length;
            if (bytes.Length > Framing.MaxFrameBytes * 3 / 4)
                GameLog.Warn("servidor.rede", $"snapshot com {bytes.Length} bytes (limite {Framing.MaxFrameBytes})");
        }
        foreach (var s in zone.Players)
            if (s.LoggedIn) _transport.Send(s.ClientId, bytes);
    }

    // ---------------- sessões ----------------

    private void Disconnect(int clientId)
    {
        _transport.Disconnect(clientId);
        RemoveSession(clientId);
    }

    private void RemoveSession(int clientId)
    {
        if (!_sessions.Remove(clientId, out var s)) return;
        if (s.LoggedIn) { Save(s); PurgeBuyback(s, send: false); FlushConta(s); }
        GameLog.Info("servidor.rede", $"{Who(s)} saiu" + (s.LoggedIn ? $" (salvo: nível {s.Level}, vida {s.Combat!.Health:0}, ({s.X:0.0}; {s.Y:0.0}))" : ""));
        DuelAbandon(s);
        LeaveZone(s);
        if (s.CharacterId is not null) _mesa?.Abandonar(s.CharacterId, HojeBr());
        OnSessionGone(s);
    }

    private void Save(Session s)
        => _db.SaveCharacterState(s.CharacterId!, s.Level, s.Xp, s.Zone?.LayoutId ?? MapId, s.X, s.Y, s.Combat!.Health);
}
