using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Shared.Client;

/// <summary>
/// Estado do cliente construído só a partir das mensagens do servidor (C# puro, sem Godot, testável).
/// O cliente nunca "decide" resultado: apenas guarda e exibe o que o servidor mandou.
/// Convenção de coordenadas: (x, y) do servidor = (X, Z) no Godot; "para cima" na tela = y diminuindo.
/// </summary>
public sealed class ClientModel
{
    public const string SkillGolpe = "skill_guerreiro_golpe_de_cunha";
    public const string SkillGuarda = "skill_guerreiro_guarda_de_escudo";
    public const string WeaponSlot = "weapon_main";
    private const int MaxLogLines = 8;

    private readonly List<string> _log = new();

    public bool LoggedIn { get; private set; }
    public string? LoginError { get; private set; }
    public string? CharacterId { get; private set; }
    public string? Name { get; private set; }
    public Snapshot? Snapshot { get; private set; }
    public ProgressMessage? Progress { get; private set; }
    public bool AutoAttackOn { get; private set; }
    /// <summary>Tamanho da arena e arco da Guarda vêm do servidor (WorldInfoMessage); os padrões valem até a mensagem chegar.</summary>
    public double ArenaHalfSize { get; private set; } = 20;
    public double GuardArcDegrees { get; private set; } = 120;
    public string MapId { get; private set; } = "";
    public IReadOnlyList<InventoryItemView> Inventory { get; private set; } = Array.Empty<InventoryItemView>();
    public IReadOnlyList<EquippedItemView> Equipment { get; private set; } = Array.Empty<EquippedItemView>();
    public IReadOnlyList<string> Log => _log;

    // ---- E8: lobby, mapa, catálogo e marcas ----
    public LobbyMessage? Lobby { get; private set; }
    public MapLayout? Map { get; private set; }
    public CatalogMessage? Catalog { get; private set; }
    public string ClassId { get; private set; } = "guerreiro";
    public IReadOnlySet<string> Flags => _flags;
    public string ResourceName => Catalog?.ResourceName ?? "Fôlego";
    public double ResourceMax => Catalog?.ResourceMax ?? 100;
    /// <summary>Aviso curto e recente (baú aberto, item novo) para o HUD; o Main limpa depois de mostrar.</summary>
    public string? Notice { get; set; }
    private readonly HashSet<string> _flags = new();
    private static readonly Dictionary<string, ItemInfo> KnownItems = new();
    private static readonly Dictionary<string, string> KnownSkills = new();

    // ---- E9: moedas, atributos, missões, mapa do mundo, grupo, NPC e avisos ----
    public long Coins { get; private set; }
    public StatsMessage? Stats { get; private set; }
    public IReadOnlyList<QuestEntry> Quests { get; private set; } = Array.Empty<QuestEntry>();
    /// <summary>17/11: próximo passo da história principal (diário, sempre visível).</summary>
    public QuestEntry? ProximaHistoria { get; private set; }
    /// <summary>Marcadores móveis de missão (escolta: carroça), por id da missão.</summary>
    public Dictionary<string, (double X, double Y, string Kind)> QuestMarkers { get; } = new();
    public WorldMapMessage? WorldMap { get; private set; }
    public PartyMessage? Party { get; private set; }
    public PartyInviteMessage? PendingInvite { get; set; }
    /// <summary>13/11: decoração da casa que você está visitando (null = está na sua).</summary>
    public HashSet<string>? MapDecor { get; set; }
    /// <summary>Conversa aberta com um NPC (loja/missões). O Main fecha ao se afastar.</summary>
    public NpcDialogMessage? Dialog { get; set; }
    /// <summary>10/11: balcão de recompra do NPC da loja aberta (o que você vendeu agora há pouco).</summary>
    public BuybackMessage? Buyback { get; set; }
    /// <summary>Baú/recompensa recém-ganhos: a HUD mostra o desenho dos itens subindo na tela.</summary>
    public Queue<LootNoticeMessage> LootPopups { get; } = new();
    /// <summary>Marcadores sobre os NPCs (servidor): "novo" amarelo, "entregar" verde; null = ainda não chegou.</summary>
    public IReadOnlyDictionary<string, string>? NpcMarkers { get; private set; }
    private readonly Queue<NoticeMessage> _notices = new();
    /// <summary>Próximo aviso de centro de tela (nome da área, recompensa, aviso). Null se não houver.</summary>
    public NoticeMessage? NextNotice() => _notices.Count > 0 ? _notices.Dequeue() : null;

    private bool IsBasic(string skillId) => Catalog?.Skills.Any(s => s.Id == skillId && s.Slot is "basic" or "basic_unarmed") == true;

    /// <summary>Clique: o ataque da arma; sem arma, o gratuito (soco/faísca). O servidor confirma pelo equipamento.</summary>
    public SkillInfo? BasicSkill
        => Equipment.Any(e => e.Slot == WeaponSlot) ? SkillFor("basic") : SkillFor("basic_unarmed") ?? SkillFor("basic");

    /// <summary>Habilidade própria da arma equipada (tecla R), ou null se a arma não tiver.</summary>
    public SkillInfo? WeaponSkill
    {
        get
        {
            var weapon = Equipment.FirstOrDefault(e => e.Slot == WeaponSlot);
            var id = weapon is null ? null : ItemInfoFor(weapon.DefinitionId)?.WeaponSkill;
            return id is null ? null : Catalog?.Skills.FirstOrDefault(s => s.Id == id);
        }
    }

    /// <summary>Habilidade da evolução de classe (tecla T). Bloqueada até a marca "evo:*".</summary>
    public SkillInfo? EvolutionSkill => SkillFor("skill3");
    public bool Evolved => _flags.Any(f => f.StartsWith("evo:", StringComparison.Ordinal));

    /// <summary>Classe como aparece no cartão: "Guerreiro" ou a evolução ("Campeão · Guerreiro").</summary>
    public string ClassTitle
    {
        get
        {
            var baseName = ClassName(ClassId);
            var spec = _flags.FirstOrDefault(f => f.StartsWith("spec:", StringComparison.Ordinal));
            if (spec is not null) return SpecName(spec[5..]) + " · " + baseName;
            if (_flags.Contains("evo:campeao")) return "Campeão · " + baseName;
            if (_flags.Contains("evo:arcanista")) return "Arcanista · " + baseName;
            if (_flags.Contains("evo:matriarca")) return "Matriarca do Éter · " + baseName;
            if (_flags.Contains("evo:batedor")) return "Batedor das Sombras · " + baseName;
            return baseName;
        }
    }

    /// <summary>14/11 tarde (dono: "mostrar no C ou no I a classe em que ele está: classe 1, 2..."): 1, 2 ou 3.</summary>
    public int ClassStage => _flags.Contains("classe3") ? 3 : Evolved ? 2 : 1;

    /// <summary>Ex.: "Classe 2 · Campeão · Guerreiro" / "Classe 3 · Leão · Guerreiro".</summary>
    public string ClassStageText => ClassStage switch
    {
        3 => "Classe 3 · " + (ClassId switch { "guerreiro" => "Leão", "mago" => "Corvo", "arqueiro" => "Serpente", _ => "Gata" }) + " · " + ClassName(ClassId),
        2 => "Classe 2 · " + ClassTitle,
        _ => "Classe 1 · " + ClassTitle,
    };

    public static string SpecName(string id) => id switch
    {
        "vanguarda" => "Vanguarda", "quebra_selos" => "Quebra-Selos", "piromante" => "Piromante", "tecelao" => "Tecelão Espectral",
        "guardia" => "Guardiã", "maldicao" => "Maldição", "atirador" => "Atirador", "sombra" => "Sombra", _ => id,
    };

    public static string ClassName(string classId) => classId switch
    {
        "mago" => "Mago", "bruxa" => "Bruxa", "arqueiro" => "Arqueiro", _ => "Guerreiro",
    };

    /// <summary>Classes que atiram de longe (mira à distância, frasco de recurso etc.).</summary>
    public static bool IsRanged(string classId) => classId is "mago" or "bruxa" or "arqueiro";

    /// <summary>Efeito de refeição ativo (vem do servidor), mostrado no cartão.</summary>
    public string BuffText => Buff is { Name.Length: > 0 } b && BuffUntilLocal > DateTime.UtcNow
        ? $"{b.Name}: {(int)(BuffUntilLocal - DateTime.UtcNow).TotalMinutes + 1} min" : "";
    public BuffMessage? Buff { get; private set; }
    private DateTime BuffUntilLocal;
    /// <summary>18/11 15h: buff ativo (nome, quanto falta, fração restante 0..1) para a barra de efeitos e o C.</summary>
    public long BuffTotalMs { get; private set; } = 1;
    /// <summary>18/11 15h: "⌂ Mansão · casa nível 3/3 · Conforto 6/10..." enquanto você está na sua casa/terreno.</summary>
    public string CasaInfo { get; private set; } = "";
    public (string Nome, string Resumo, TimeSpan Falta, double Fracao)? BuffAtivo
        => Buff is { Name.Length: > 0 } b && BuffUntilLocal > DateTime.UtcNow
            ? (b.Name, b.Summary, BuffUntilLocal - DateTime.UtcNow, Math.Clamp((BuffUntilLocal - DateTime.UtcNow).TotalMilliseconds / BuffTotalMs, 0, 1)) : null;
    /// <summary>Estação de ofício aberta (forja, bancada, caldeirão, panela) — painel de receitas.</summary>
    public StationMessage? Station { get; set; }
    /// <summary>Armazém comunitário ou baú da casa aberto.</summary>
    public StorageMessage? Storage { get; set; }
    /// <summary>17/11 noite: chefe de campo morto perto de você — nome, segundos para voltar e quando chegou (some sem atualização).</summary>
    public (string Nome, int Segundos, DateTime Em)? ChefeVolta { get; set; }
    public IReadOnlyList<PlotView> Plots { get; private set; } = Array.Empty<PlotView>();
    public CompanionsMessage? Companions { get; private set; }
    /// <summary>Mochila em grade estilo MU (0 = servidor antigo, sem grade).</summary>
    public int InventoryColumns { get; private set; }
    public int InventoryRows { get; private set; }
    /// <summary>Relógio do mundo: hora recebida, ritmo e quando chegou (o cliente avança sozinho entre avisos).</summary>
    public double ClockHour { get; private set; } = 15;
    public double ClockHoursPerSecond { get; private set; }
    public DateTime ClockAtUtc { get; private set; } = DateTime.UtcNow;
    public bool HasClock { get; private set; }
    public double HourNow => ((ClockHour + (DateTime.UtcNow - ClockAtUtc).TotalSeconds * ClockHoursPerSecond) % 24 + 24) % 24;
    /// <summary>Modo deus (F1): listas e estado vindos do servidor; null = ainda não perguntou.</summary>
    public GodCatalogMessage? God { get; private set; }
    /// <summary>12/11: últimas mensagens do chat (global, privado, grupo, sistema). ChatSeq muda a cada nova.</summary>
    public List<ChatMessage> Chat { get; } = new();
    /// <summary>13/11 (pedido do Barto: "o histórico já existe, mas faz ele aparecer numa tela maior apertando uma tecla"):
    /// tudo o que passou no chat e nos avisos (até 400 linhas), com a hora — a tecla O abre.</summary>
    public List<(string Hora, string Canal, string Texto)> Historico { get; } = new();
    private bool Historicar2(NoticeMessage n) { Historicar("aviso:" + n.Kind, n.Text); return true; }
    /// <summary>Registro feito pelo cliente (conexão, atualização, lobby, erros locais) — aparece no histórico (O).</summary>
    public void Registrar(string texto, string canal = "registro")
    {
        if (string.IsNullOrWhiteSpace(texto)) return;
        if (Historico.Count > 0 && Historico[^1].Texto == texto) return;   // não repete a mesma linha seguida
        Historicar(canal, texto);
    }

    private void Historicar(string canal, string texto)
    {
        Historico.Add((DateTime.Now.ToString("HH:mm"), canal, texto));
        if (Historico.Count > 400) Historico.RemoveAt(0);
    }
    public int ChatSeq { get; private set; }
    /// <summary>11/11: estado do editor (rascunho não salvo, selecionado). EditorSeq muda a cada mensagem.</summary>
    public EditorStateMessage? Editor { get; private set; }
    public int EditorSeq { get; private set; }
    /// <summary>13/11: arquivos do jogo para o editor (lista e o aberto). ArquivoSeq muda a cada mensagem.</summary>
    public ContentFileMessage? Arquivo { get; private set; }
    /// <summary>13/11: Trilha do Herói (o que abre em cada nível).</summary>
    public TrilhaMessage? Trilha { get; private set; }
    public int TrilhaSeq { get; private set; }
    public int ArquivoSeq { get; private set; }

    public PlotView? PlotFor(string plotId) => Plots.FirstOrDefault(p => p.PlotId == plotId);

    /// <summary>NPC visível para este personagem (alguns só aparecem depois de uma missão: "mundo que muda").</summary>
    public bool NpcVisible(MapNpc npc) => (npc.RequiresFlag is null || _flags.Contains(npc.RequiresFlag)) && (npc.ForbidFlag is null || !_flags.Contains(npc.ForbidFlag));

    /// <summary>Objeto de missão (relíquia) aparece só com a missão ativa e ainda não feita.</summary>
    public bool ObjectVisible(MapObject obj)
        => obj.Quest is null || Quests.Any(q => q.Id == obj.Quest && q.State == "active" && q.Progress < q.Count);

    public bool InParty(string characterId) => Party?.Members.Any(m => m.CharacterId == characterId) == true;

    public ItemInfo? ItemInfoFor(string definitionId) => KnownItems.TryGetValue(definitionId, out var i) ? i : null;
    /// <summary>
    /// O personagem já pode usar este poder: básico sempre; arma pela arma; evolução evoluído; o resto só com o tomo lido
    /// (marca "tomo:id" vinda do servidor). O servidor confere de novo — isto só desenha a barra.
    /// </summary>
    public bool Knows(SkillInfo skill, int level)
    {
        if (skill.Slot is "basic" or "basic_unarmed" or "weapon") return true;
        if (skill.Slot == "skill3") return Evolved;
        if (skill.Tome is null) return level >= skill.UnlockLevel;
        return _flags.Contains("tomo:" + skill.Id) && level >= skill.UnlockLevel;
    }

    public bool Knows(SkillInfo skill) => Knows(skill, Progress?.Level ?? Me?.Level ?? 1);

    /// <summary>Teclas de poder (Q, E, R, T) → habilidade escolhida no Livro de Poderes. Vazio = padrão da classe.</summary>
    public Dictionary<string, string> Binds { get; } = new();
    /// <summary>Missões fixadas na tela pelo diário (J). Vazio = a tela mostra as 3 mais urgentes.</summary>
    /// <summary>11/11: toda missão aberta já vem marcada (no canto da tela); aqui ficam só as que o jogador DESMARCOU no J.</summary>
    public HashSet<string> UnpinnedQuests { get; } = new();
    public bool IsPinned(string questId) => !UnpinnedQuests.Contains(questId);
    /// <summary>Muda quando o jogador fixa/desafixa (a Main grava em user://).</summary>
    public int PinsVersion { get; set; }

    private readonly Dictionary<string, long> _nodeReady = new();

    /// <summary>Segundos até o ponto de coleta voltar para este personagem (0 = disponível).</summary>
    public long NodeSecondsLeft(string nodeId) =>
        _nodeReady.TryGetValue(nodeId, out var t) && t > Environment.TickCount64 ? (t - Environment.TickCount64) / 1000 + 1 : 0;

    public SkillInfo? SkillById(string id) => Catalog?.Skills.FirstOrDefault(s => s.Id == id);

    /// <summary>Poder na tecla: o escolhido (se ainda souber) ou o padrão (Q = 1º da classe, E = 2º, R = arma/inato, T = evolução).</summary>
    public SkillInfo? SkillForKey(string key)
    {
        if (Binds.TryGetValue(key, out var id) && SkillById(id) is { } chosen && Knows(chosen)) return chosen;
        if (Binds.ContainsKey(key)) return null;   // escolheu deixar vazia
        var fallback = key switch
        {
            "Q" => SkillFor("skill1"),
            "E" => SkillFor("skill2"),
            "R" => WeaponSkill ?? SkillFor("innate"),
            "T" => SkillFor("skill3"),
            _ => null,
        };
        if (fallback is null || !Knows(fallback)) return null;
        // o padrão não repete um poder que já está em outra tecla escolhida
        return Binds.Values.Contains(fallback.Id) ? null : fallback;
    }

    /// <summary>Muda quando um poder novo foi para uma tecla sozinho (a Main grava as teclas).</summary>
    public int BindsVersion { get; set; }

    /// <summary>
    /// 10/11 ("usei a Guarda de Escudo e não apareceu na barra"): poder recém-aprendido vai sozinho para a primeira tecla
    /// vazia (Q, E, R, T). Se todas estiverem ocupadas, fica no Livro de Poderes (K) para escolher.
    /// </summary>
    /// <summary>
    /// 13/11 (pedido do dono: "ao ganhar coisas novas — o primeiro pet, poder, montaria — pra não passar batido, uma caixa no
    /// meio: Parabéns, você ganhou um pet — Grilo... e um OK embaixo"): fila de (título, texto) que o HUD mostra uma por vez.
    /// Vale para pet, montaria, asas e bracelete (a 1ª vez que cada um chega na mochila ou no corpo) e poder aprendido.
    /// </summary>
    public Queue<(string Titulo, string Texto)> Ganhos { get; } = new();

    /// <summary>13/11 22:26 ("se eu já vi a mensagem de parabéns, não precisa aparecer de novo"): o cliente guarda, por
    /// personagem, o que já foi parabenizado (Main liga estes dois ao arquivo user://parabens_&lt;nome&gt;).</summary>
    public System.Func<string, bool>? JaParabenizado { get; set; }
    public System.Action<string>? MarcarParabenizado { get; set; }

    private bool Novidade(string chave)
    {
        if (JaParabenizado?.Invoke(chave) == true) return false;
        MarcarParabenizado?.Invoke(chave);
        return true;
    }
    private HashSet<string>? _jaTive;

    private void GanhouItens(IReadOnlyList<InventoryItemView> novos)
    {
        var agora = novos.Select(i => i.DefinitionId).Concat(Equipment.Select(e => e.DefinitionId)).ToHashSet();
        if (_jaTive is null) { _jaTive = agora; return; }   // primeira mochila ao entrar: nada é "novo"
        foreach (var d in agora)
        {
            if (!_jaTive.Add(d)) continue;
            var cat = ItemInfoFor(d)?.Category ?? "";
            var tipo = cat switch { "pet" => "um pet", "mount" => "uma montaria", "wings" => "asas", "bracelet" => "uma skin (bracelete)", _ => null };
            if (tipo is null) continue;
            var dica = cat switch
            {
                "pet" => "Vista no espaço Pet da mochila (I) para ele te seguir.",
                "mount" => "Coloque no espaço Montaria da mochila (I) e aperte V para montar.",
                "wings" => "Vista no espaço Asas; G voa.",
                _ => "Coloque no espaço Skin da mochila (I).",
            };
            if (Novidade("item:" + d)) Ganhos.Enqueue(("Parabéns!", $"Você ganhou {tipo}: {DisplayName(d)}.\n{dica}"));
        }
    }

    /// <summary>18/11: decoração nova colocada na casa — o cliente abre o modo de escolher o lugar.</summary>
    public readonly Queue<string> DecorNovo = new();

    private void GanhouFlag(string f)
    {
        if (f.StartsWith("decor:", StringComparison.Ordinal)) DecorNovo.Enqueue(f[6..]);
        if (f.StartsWith("tomo:", StringComparison.Ordinal) && SkillById(f[5..]) is { } sk && Novidade(f))
            Ganhos.Enqueue(("Parabéns!", $"Você aprendeu um poder: {sk.DisplayName}.\nEscolha a tecla no Livro de Poderes (K)."));
        else if (f.StartsWith("titulo:", StringComparison.Ordinal) && Novidade(f))
            Ganhos.Enqueue(("Título novo", NomesTitulos.Texto(f[7..])));   // 15/11: nome legível («Novato da Costa»), não o id
    }

    public void AutoBind(IEnumerable<string> newFlags)
    {
        foreach (var f in newFlags)
        {
            if (!f.StartsWith("tomo:", StringComparison.Ordinal) || SkillById(f[5..]) is not { } sk) continue;
            var keys = new[] { "Q", "E", "R", "T", "Dir" };
            if (keys.Any(k => SkillForKey(k)?.Id == sk.Id)) continue;
            var free = new[] { "Q", "E", "R", "T" }.FirstOrDefault(k => SkillForKey(k) is null && !Binds.ContainsKey(k));
            if (free is null) continue;
            Binds[free] = sk.Id;
            BindsVersion++;
        }
    }

    public SkillInfo? SkillFor(string slot) => Catalog?.Skills.FirstOrDefault(s => s.Slot == slot);

    /// <summary>Raio da área desta habilidade com o bônus do equipamento atual (só para desenhar; o servidor decide o acerto).</summary>
    public double GroundRadius(SkillInfo skill)
        => skill.Radius + Equipment.Sum(e => Catalog?.Items.FirstOrDefault(i => i.Id == e.DefinitionId)?.AoeRadiusBonus ?? 0);

    public PlayerView? Me => Snapshot?.Players.FirstOrDefault(p => p.CharacterId == CharacterId);

    public void Reset()
    {
        LoggedIn = false;
        LoginError = null;
        CharacterId = null;
        Name = null;
        Snapshot = null;
        Progress = null;
        AutoAttackOn = false;
        ArenaHalfSize = 20;
        GuardArcDegrees = 120;
        MapId = "";
        Inventory = Array.Empty<InventoryItemView>();
        Equipment = Array.Empty<EquippedItemView>();
        _log.Clear();
        Lobby = null;
        Map = null;
        Catalog = null;
        ClassId = "guerreiro";
        _flags.Clear();
        Notice = null;
        Coins = 0;
        Stats = null;
        Quests = Array.Empty<QuestEntry>();
        WorldMap = null;
        Party = null;
        PendingInvite = null;
        Dialog = null;
        _notices.Clear();
    }

    /// <summary>Volta ao lobby (mantém a conexão autenticada e a lista de personagens).</summary>
    public void LeaveWorld()
    {
        var lobby = Lobby;
        Reset();
        Lobby = lobby;
    }

    public void Apply(NetMessage message)
    {
        switch (message)
        {
            case LoginResult login:
                LoggedIn = login.Ok;
                LoginError = login.Ok ? null : login.Error;
                CharacterId = login.CharacterId;
                Name = login.Name;
                if (login.Ok) AddLog($"Entrou como {login.Name}.");
                break;
            case Snapshot snapshot:
                Snapshot = snapshot;
                break;
            case StationMessage station:
                Station = station;
                break;
            case StorageMessage storage:
                Storage = storage;
                break;
            case PlotsMessage plots:
                Plots = plots.Plots;
                break;
            case BuffMessage buff:
                Buff = buff;
                BuffUntilLocal = DateTime.UtcNow.AddMilliseconds(buff.RemainingMs);
                BuffTotalMs = Math.Max(1, buff.RemainingMs);
                break;
            case CompanionsMessage companions:
                Companions = companions;
                break;
            case WorldTimeMessage time:
                ClockHour = time.Hour; ClockHoursPerSecond = time.HoursPerSecond; ClockAtUtc = DateTime.UtcNow; HasClock = true;
                break;
            case ObjetivosMessage obj:   // 14/11: menu do TAB
                Objetivos = obj;
                break;
            case TrilhaMessage tr:
                Trilha = tr;
                TrilhaSeq++;
                break;
            case ContentFileMessage cf:
                Arquivo = cf;
                ArquivoSeq++;
                break;
            case EditorStateMessage ed:
                Editor = ed;
                EditorSeq++;
                break;
            case GodCatalogMessage god:
                // a resposta "leve" (só estado) mantém as listas já recebidas
                God = god.Items.Count == 0 && God is { } old && god.Enabled
                    ? god with { Items = old.Items, Monsters = old.Monsters, Maps = old.Maps } : god;
                break;
            case WorldInfoMessage world:
                ArenaHalfSize = world.ArenaHalfSize;
                GuardArcDegrees = world.GuardArcDegrees;
                MapId = world.MapId;
                ClassId = world.ClassId;
                break;
            case LobbyMessage lobby:
                Lobby = lobby;
                if (!lobby.Ok) LoginError = lobby.Error;
                break;
            case MapMessage map:
                if (Map is not null && Map.Id != map.Map.Id) { Dialog = null; Station = null; Storage = null; QuestMarkers.Clear(); } // trocou de mapa: fecha painéis
                Map = map.Map;
                MapDecor = map.Decor is null ? null : new HashSet<string>(map.Decor);
                break;
            case BuybackMessage bb:
                Buyback = bb;
                break;
            case CoinsMessage coins:
                Coins = coins.Coins;
                if (coins.Gained > 0) AddLog($"+{coins.Gained} moedas.");
                break;
            case StatsMessage stats:
                Stats = stats;
                break;
            case QuestLogMessage log:
            {
                // 10/11: missão aceita já entra fixada no canto da tela (o diário, J, desafixa)
                if (Quests.Count > 0)
                {
                    var before = Quests.ToDictionary(q => q.Id, q => q.State);
                    foreach (var q in log.Quests)
                        if (q.State == "active" && (!before.TryGetValue(q.Id, out var prevState) || prevState is not ("active" or "ready")) && UnpinnedQuests.Remove(q.Id))
                            PinsVersion++;
                }
                // 17/11: a entrada "historia" é o próximo passo da história (fica fora da lista de missões)
                ProximaHistoria = log.Quests.FirstOrDefault(q => q.State == "historia");
                Quests = log.Quests.Where(q => q.State != "historia").ToList();
                break;
            }
            case QuestMarkerMessage qm:
                if (qm.Kind.Length == 0) QuestMarkers.Remove(qm.QuestId);
                else QuestMarkers[qm.QuestId] = (qm.X, qm.Y, qm.Kind);
                break;
            case WorldMapMessage world:
                WorldMap = world;
                break;
            case PartyMessage party:
                Party = party.PartyId is null ? null : party;
                break;
            case PartyInviteMessage invite:
                PendingInvite = invite;
                AddLog($"{invite.FromName} convidou você para o grupo (Y aceita, N recusa).");
                break;
            case NpcDialogMessage dialog:
                Dialog = dialog;
                break;
            case NodeTimersMessage nodes:
                _nodeReady.Clear();
                foreach (var (id, sec) in nodes.SecondsLeft) _nodeReady[id] = Environment.TickCount64 + sec * 1000;
                break;
            case NpcMarkersMessage markers:
                NpcMarkers = markers.Markers;
                break;
            case ChatMessage chat:
                Chat.Add(chat);
                if (Chat.Count > 60) Chat.RemoveAt(0);
                Historicar(chat.Channel, chat.Channel == "sistema" ? chat.Text : $"{chat.From}{(chat.To is { } para ? " → " + para : "")}: {chat.Text}");
                ChatSeq++;
                break;
            case NoticeMessage { Kind: "chefe_volta" } relogio:
            {
                // 17/11 noite: relógio do chefe de campo morto (só chega para quem está perto do ponto dele)
                var partes = relogio.Text.Split('|');
                ChefeVolta = (partes[0], partes.Length > 1 && int.TryParse(partes[1], out var seg) ? seg : 0, DateTime.UtcNow);
                break;
            }
            case NoticeMessage { Kind: "casa_info" } ci:   // 18/11 15h: nível da casa em cima da barra
                CasaInfo = ci.Text;
                break;
            case NoticeMessage { Kind: "nivel" } nv:
            {
                // 18/11: cartão de nível (o HUD desenha); no histórico vira texto corrido
                var pt = nv.Text.Split('|');
                AddLog(pt[0] + (pt.Length > 1 && pt[1].Length > 0 ? " — ganhou: " + pt[1].Replace(";", ", ") : "")
                       + (pt.Length > 2 && pt[2].Length > 0 ? " — agora pode: " + pt[2].Replace(";", ", ") : ""));
                break;
            }
            case NoticeMessage notice when Historicar2(notice):
                _notices.Enqueue(notice);
                if (notice.Kind != "area" && (notice.Kind == "pedra" ? notice.Text.Split('|')[0] : notice.Text) is { Length: > 0 } linha) AddLog(linha);   // 17/11: "texto|x|y"
                break;
            case CatalogMessage catalog:
                Catalog = catalog;
                foreach (var item in catalog.Items) KnownItems[item.Id] = item;
                foreach (var skill in catalog.Skills) KnownSkills[skill.Id] = skill.DisplayName;
                break;
            case FlagsMessage flags:
            {
                var first = _flags.Count == 0;
                var added = flags.Flags.Where(f => !_flags.Contains(f)).ToList();
                _flags.Clear();
                foreach (var f in flags.Flags) _flags.Add(f);
                if (!first) { AutoBind(added); foreach (var f in added) GanhouFlag(f); }
                break;
            }
            case LootNoticeMessage loot:
                var names = string.Join(", ", loot.DefinitionIds.Select(DisplayName));
                AddLog(loot.Source.StartsWith("quest:", StringComparison.Ordinal) ? $"Recompensa: {names}." : $"Baú: {names}.");
                Notice = "Você obteve: " + names;
                if (!loot.Source.StartsWith("node:", StringComparison.Ordinal)) LootPopups.Enqueue(loot);
                break;
            case InventoryMessage inventory:
                GanhouItens(inventory.Items);
                Inventory = inventory.Items;
                InventoryColumns = inventory.Columns; InventoryRows = inventory.Rows;
                break;
            case EquipmentMessage equipment:
                Equipment = equipment.Items;
                break;
            case ProgressMessage progress:
                var previousLevel = Progress?.Level ?? progress.Level;
                Progress = progress;
                if (progress.XpGained > 0) AddLog($"+{progress.XpGained} XP.");
                if (progress.Level > previousLevel) AddLog($"Subiu para o nível {progress.Level}!");
                break;
            case CastStartedMessage started:
                if (!IsBasic(started.SkillId)) AddLog($"Conjurando {SkillName(started.SkillId)}…");
                break;
            case CastResultMessage cast:
                // ataques básicos e "ocupado" (botão segurado) não poluem o registro
                if (cast.Success && IsBasic(cast.SkillId)) break;
                if (!cast.Success && cast.Failure == "Busy" && Map is not null) break;
                AddLog(cast.Success
                    ? (cast.Damage > 0 || cast.TargetId is not null
                        ? $"{SkillName(cast.SkillId)}: {cast.Damage} de dano{(cast.Crit ? " (crítico)" : "")}."
                        : $"{SkillName(cast.SkillId)}.")
                    : $"{SkillName(cast.SkillId)} falhou: {FailureText(cast.Failure)}.");
                break;
            case CombatEvent combat:
                ApplyCombatEvent(combat);
                break;
            case ClaimResultMessage claim:
                // 18/11: item que já sumiu do chão (outro pegou, ou o 2º clique) não é erro para mostrar
                if (claim.Success || claim.Reason is not ("unknown_item" or "already_taken"))
                    AddLog(claim.Success ? "Item coletado." : $"Coleta falhou: {ReasonText(claim.Reason)}.");
                break;
            case ActionResultMessage action:
                ApplyAction(action);
                break;
            case ErrorMessage error:
                AddLog($"Erro do servidor: {error.Reason}.");
                break;
        }
    }

    private void ApplyCombatEvent(CombatEvent e)
    {
        if (e.AttackerId == CharacterId)
        {
            if (e.SkillId == "basic_attack") AddLog($"Ataque básico: {e.Damage}.");
            if (e.Staggered) AddLog("Quebrou a preparação do inimigo!");
            if (e.TargetDied)
            {
                AutoAttackOn = false;
                var name = Snapshot?.Monsters.FirstOrDefault(m => m.Id == e.TargetId)?.SpeciesName;
                AddLog(name is null ? "Monstro derrotado!" : $"{name} derrotado!");
            }
        }
        else if (e.TargetId == CharacterId)
        {
            if (e.Evaded) AddLog("Esquivou!");
            else if (e.Damage > 0) AddLog($"Você sofreu {e.Damage} de dano.");
        }
    }

    /// <summary>14/11: o último "sentar" foi numa cama (8 s) ou num banco (5 s) — para a bolinha do descanso.</summary>
    public bool DescansoNaCama { get; private set; }
    public double DescansoSegundos { get; private set; } = 5;
    /// <summary>14/11: objetivos e Loja de Cristais (menu do TAB).</summary>
    public ObjetivosMessage? Objetivos { get; private set; }

    private void ApplyAction(ActionResultMessage a)
    {
        if (a.Action == "interact" && a.Success && a.Reason is "sit" or "cama" or "sit_casa" or "cama_casa")
        {
            DescansoNaCama = a.Reason is "cama" or "cama_casa";
            DescansoSegundos = a.Reason switch { "cama_casa" => 60, "sit_casa" => 30, "cama" => 8, _ => 5 };   // 18/11: casa
        }
        if (a.Action == "dodge") return; // esquiva recusada não polui o registro
        if (a.Action == "interact" && a.Success && a.Reason == "npc") return;
        if (a.Action is "buy" or "sell" or "quest" or "party" or "spend_stat" or "travel" && a.Success) return;
        // montaria, companheiro, asas, ofício, coleta, depósito: o efeito já aparece no mundo/painel; sem "ok" cru no registro
        if (a.Success && a.Action is "pet" or "wing_dash" or "gather" or "craft" or "storage" or "plot") return;
        if (a.Success && a.Action == "mount") { AddLog(a.Reason == "dismounted" ? "Você desmontou." : "Você montou."); return; }
        if (a.Action == "interact" && a.Success)
        {
            if (a.Reason is "station" or "storage" or "gather" or "plot") return; // o painel/barra já mostram
            AddLog(a.Reason == "gate_opened" ? "O portão do atalho se abriu." : "Baú aberto.");
            if (a.Reason == "gate_opened") Notice = "Atalho aberto: o portão leva direto ao acampamento.";
            return;
        }
        if (a.Action == "auto_attack" && a.Success) AutoAttackOn = a.Reason == "started";
        if (a.Action == "auto_attack" && !a.Success) AutoAttackOn = false;
        if (a.Action == "auto_attack" && a.Success) return; // sem ruído no registro
        AddLog(a.Success ? SuccessText(a.Action) : $"{ActionName(a.Action)} falhou: {ReasonText(a.Reason)}.");
    }

    // ---- consultas usadas pelas teclas ----

    public MonsterView? NearestAliveMonster()
    {
        var me = Me;
        if (me is null || Snapshot is null) return null;
        return Snapshot.Monsters.Where(m => m.Alive).OrderBy(m => Distance(me.X, me.Y, m.X, m.Y)).FirstOrDefault();
    }

    public WorldItemView? NearestItem()
    {
        var me = Me;
        if (me is null || Snapshot is null) return null;
        return Snapshot.Items.OrderBy(i => Distance(me.X, me.Y, i.X, i.Y)).FirstOrDefault();
    }

    /// <summary>Monstro vivo mais bem alinhado à direção (fx,fy), até maxRange (alvo para travar a mira).</summary>
    public MonsterView? BestMonsterInDirection(double fx, double fy, double maxRange, double maxAngleDegrees = 50)
    {
        var me = Me;
        if (me is null || Snapshot is null) return null;
        MonsterView? best = null;
        var bestScore = double.MaxValue;
        var fl = Math.Sqrt(fx * fx + fy * fy);
        if (fl < 1e-9) return null;
        foreach (var m in Snapshot.Monsters)
        {
            if (!m.Alive) continue;
            var dx = m.X - me.X;
            var dy = m.Y - me.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d > maxRange || d < 1e-6) continue;
            var angle = Math.Acos(Math.Clamp((dx * fx + dy * fy) / (d * fl), -1, 1)) * 180 / Math.PI;
            if (angle > maxAngleDegrees) continue;
            var score = angle * 0.15 + d;
            if (score < bestScore) { bestScore = score; best = m; }
        }
        return best;
    }

    /// <summary>O que dá para usar com a tecla de interação aqui: item no chão, baú fechado, alavanca ou NPC.</summary>
    /// <summary>
    /// 10/11 ("interage com o NPC/a rosa que está nas costas"): o que fica atrás de você conta como mais longe — o que
    /// está na frente ganha a vez, mesmo um pouco mais distante.
    /// </summary>
    private static double Behind(PlayerView me, double x, double y)
    {
        var dx = x - me.X; var dy = y - me.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1.3) return 0;   // 17/11 noite: colado no objeto, tanto faz para onde olha (o F sumia de frente pra porta)
        var dot = (dx * me.FacingX + dy * me.FacingY) / len;
        return dot < -0.2 ? 1.6 : dot < 0.3 ? 0.5 : 0;
    }

    /// <summary>11/11 noite (pedido): "F falar" só com o mouse/mira no NPC. O cliente liga e informa qual NPC está mirado.</summary>
    public bool RequireNpcAim { get; set; }
    public string? AimedNpcId { get; set; }

    /// <summary>13/11 22:26: objeto de uso sob a mira (o MapView acha também mirando na porta, na parede).</summary>
    public string? AimedObjId { get; set; }

    public (string Kind, string Id, string Label)? NearestInteractable(double range = 2.6)
    {
        var me = Me;
        if (me is null) return null;
        (string, string, string)? best = null;
        var bestD = range;
        // 13/11 ("a interação com os NPCs está bem ruim voando de asa"): no ar (ou montado) não conta para onde o boneco olha
        // e o alcance para falar é um pouco maior (o servidor aceita até 3,4 m)
        var solto = me.Flying || me.Mount is not null;
        if (Map is not null)
            foreach (var npc in Map.Npcs)
            {
                if (!NpcVisible(npc)) continue;
                if (RequireNpcAim && npc.Id != AimedNpcId) continue;   // 11/11 noite: só o NPC que você mira
                var d = Distance(me.X, me.Y, npc.X, npc.Y) - (solto ? 0.75 : 0.4) + (solto ? 0 : Behind(me, npc.X, npc.Y)); // perto do boneco (feedback: falava de longe demais)
                if (d <= bestD) { bestD = d; best = ("npc", "npc:" + npc.Id, "Falar com " + npc.Name); }
            }
        if (Snapshot is not null)
            foreach (var item in Snapshot.Items)
            {
                var d = Distance(me.X, me.Y, item.X, item.Y);
                if (d <= bestD) { bestD = d; best = ("item", item.ItemId, "Pegar " + DisplayName(item.DefinitionId)); }
            }
        // 18/11 ("o X do mapa tem que chegar em cima e apertar F"): em cima do X do tesouro, F cava
        if (QuestMarkers.TryGetValue("tesouro", out var tx) && tx.Kind == "tesouro")
        {
            var d = Distance(me.X, me.Y, tx.X, tx.Y) - 0.6;
            if (d <= bestD && d <= 1.4) { bestD = d - 5; best = ("tesouro", "tesouro", "Cavar o tesouro"); }
        }
        if (Map is not null)
            foreach (var wp in Map.Waypoints)   // 14/11: pedra de viagem ainda apagada — interagir ativa
            {
                if (_flags.Contains("wp:" + wp.Id)) continue;
                var d = Distance(me.X, me.Y, wp.X + 2.2, wp.Y - 0.6) - 0.6;
                if (d <= bestD) { bestD = d; best = ("waypoint", "wp:" + wp.Id, "Ativar a pedra de viagem"); }
            }
        if (Map is not null)
        {
            foreach (var chest in Map.Chests)
            {
                if (_flags.Contains("chest:" + chest.Id)) continue;
                var d = Distance(me.X, me.Y, chest.X, chest.Y);
                if (d <= bestD) { bestD = d; best = ("chest", "chest:" + chest.Id, chest.Secret ? "Abrir o baú escondido" : "Abrir o baú"); }
            }
            foreach (var obj in Map.Objects)
            {
                if (!ObjectVisible(obj)) continue;
                if (obj.RequiresFlag is not null && obj.Kind == "canteiro" && !_flags.Contains(obj.RequiresFlag)) continue;
                if (obj.Kind is "placa_pressao" or "pedra_oca") continue;   // 17/11: rocha oca é segredo — sem "Examinar", só no golpe
                if (obj.Kind == "esfera" && obj.GrantFlag is { } eg && _flags.Contains(eg)) continue;   // 14/11: já pegou — sumiu   // 11/11: armadilha — pisa, não aperta F
                var d = Distance(me.X, me.Y, obj.X, obj.Y) - (obj.Kind is "forja" or "bancada" or "armazem" ? 0.6 : 0) + Behind(me, obj.X, obj.Y)
                        - (obj.Id == AimedObjId && Distance(me.X, me.Y, obj.X, obj.Y) <= 2.75 ? 1.0 : 0);   // 13/11 22:26: o que você mira (porta na parede) ganha preferência (até onde o servidor aceita)
                if (d <= bestD) { bestD = d; best = ("object", "obj:" + obj.Id, ObjectPrompt(obj)); }
            }
            // 18/11 15h: peças de fora no SEU terreno (portal, mascate, vitrola, cercado)
            if (CinzasDaMare.Shared.World.AssentosCasa.Lote(Map.Id) && _flags.Contains("casa:propria"))
                foreach (var f in _flags)
                {
                    if (!f.StartsWith("decorpos:decor_terreno_", StringComparison.Ordinal)) continue;
                    var eq = f.IndexOf('=');
                    var id = f[9..eq];
                    var p = f[(eq + 1)..].Split('|');
                    if (p.Length < 3 || p[0] != Map.Id || !_flags.Contains("decor:" + id)) continue;
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    if (!double.TryParse(p[1], System.Globalization.NumberStyles.Float, ci, out var px) || !double.TryParse(p[2], System.Globalization.NumberStyles.Float, ci, out var py)) continue;
                    var (rotulo, folga) = id switch
                    {
                        "decor_terreno_portal" => ("Portal do Quintal (ir para o Porto)", 0.8),
                        "decor_terreno_mascate" => ("Comprar com o Mascate do Quintal", 0.8),
                        "decor_terreno_jukebox" => ("Vitrola: pôr um disco (+XP por 30 min)", 0.5),
                        "decor_terreno_cercado" => ("Cercado de Caça (fazer ovos de monstro)", 1.4),
                        "decor_terreno_forja" => ("Usar: Forja do Quintal", 0.8),
                        "decor_terreno_caldeirao" => ("Usar: Caldeirão do Quintal", 0.6),
                        _ when id.StartsWith("decor_terreno_portal_", StringComparison.Ordinal) => ("Portal do Quintal (ir para " + (id[21..] switch { "vila_das_mos" => "a Vila das Mós", "astralia" => "Astrália", "candeia" => "Candeia", "brasaforte" => "Brasaforte", var o => o }) + ")", 0.8),
                        _ => ("", 0.0),
                    };
                    if (rotulo.Length == 0) continue;
                    var d = Distance(me.X, me.Y, px, py) - folga;
                    if (d <= bestD) { bestD = d; best = ("object", id == "decor_terreno_mascate" ? "npc:terreno_mascate" : "terreno:" + id, rotulo); }
                }
            // 11/11 noite: pedra de viagem descoberta — F abre o mapa com a viagem
            foreach (var wp in Map.Waypoints)
            {
                if (!_flags.Contains("wp:" + wp.Id)) continue;
                var d = Distance(me.X, me.Y, wp.X + 2.2, wp.Y - 0.6) - 0.5;
                if (d <= bestD) { bestD = d; best = ("waypoint", "wp:" + wp.Id, "Viajar (pedra de viagem)"); }
            }
            // 11/11 noite ("dá pra sentar nos bancos da praça"): perto de um banco, F senta (andar levanta)
            if (me.Action != "sit")
                foreach (var assento in CinzasDaMare.Shared.World.AssentosCasa.Lista(Map, _flags))
                {
                    // 18/11: cadeiras, trono e sofá da casa também — na sua casa, sentar carrega o Conforto do Lar ×1,5
                    var d = Distance(me.X, me.Y, assento.X, assento.Y) + 0.3;
                    if (d <= bestD) { bestD = d; best = ("bench", "banco", assento.Casa && _flags.Contains("casa:propria") ? "Sentar (Conforto do Lar ×1,5 em 30 s)" : "Sentar no banco"); }
                }
            foreach (var gate in Map.Gates)
            {
                if (_flags.Contains(gate.Flag) || !gate.Lever) continue;
                var d = Distance(me.X, me.Y, gate.LeverX, gate.LeverY);
                if (d <= bestD) { bestD = d; best = ("lever", "lever:" + gate.Id, gate.Kind == "pedra" ? $"Empurrar a rocha (Força {gate.Strength})" : gate.Kind == "ponte" ? "Construir a ponte (6 tábuas)" : gate.Kind == "rachadura" ? CrackPrompt(me, gate)
                    : gate.Kind == "corda" ? "Subir com a Corda com Gancho" : gate.Kind == "casa_ruina" ? "Reconstruir a casa (" + string.Join(" + ", MapGate.CasaRuinaPede(Map.Objects).Select(p => $"{p.Qty} {DisplayName(p.Item)}")) + $" + {gate.Coins:N0} moedas)"
                    : gate.Kind == "porta" ? "Abrir a porta (Chave de Ferro)" : "Puxar a alavanca"); }
            }
        }
        return best;
    }

    /// <summary>11/11: parede rachada — o que fazer agora com o barril de pólvora.</summary>
    private string CrackPrompt(PlayerView me, MapGate gate)
    {
        if (me.Carry == "barril") return "Pôr o barril no pé da parede";
        var keg = Snapshot?.Effects?.FirstOrDefault(e => e.Kind is "barril" or "barril_aceso" && Distance(e.X, e.Y, gate.LeverX, gate.LeverY) < 3);
        if (keg?.Kind == "barril") return "Acender o pavio (e correr!)";
        if (keg?.Kind == "barril_aceso") return "O pavio está queimando — CORRA!";
        return _flags.Contains("explosivo:parede") ? "Parede rachada: traga um barril de pólvora da pilha da Nara" : "Parede rachada (fale com a Nara, a mineira)";
    }

    /// <summary>Rótulo do objeto de coleta mais perto (para a barra "Colhendo/Cortando/Minerando").</summary>
    public string? NearestObjectLabel
    {
        get
        {
            if (Me is not { } me || Map is null) return null;
            return Map.Objects.Where(o => o.Kind == "veio").OrderBy(o => Distance(me.X, me.Y, o.X, o.Y)).FirstOrDefault()?.Label;
        }
    }

    /// <summary>Texto do aviso "F — ..." para cada tipo de objeto (ofício, horta, armazém, relíquia).</summary>
    public string ObjectPrompt(MapObject obj)
    {
        switch (obj.Kind)
        {
            case "mesclar_visual": return "Usar a Máquina de Mesclar Visual";
            case "maquina_dg": return "Máquina de Chaves (ver a receita / fazer a chave da dungeon)";
            case "veio":
                var herb = obj.Yields is { } y && (y.Contains("erva") || y.Contains("folha") || y.Contains("cogumelo"));
                var label = System.Text.RegularExpressions.Regex.Replace(obj.Label, @"\s*\((machado|picareta|foice)\)", "");
                var tool = obj.RequiresFlag is { } rf && rf.StartsWith("tool:") ? rf[5..] : obj.Yields == "mat_tabua" ? "machado" : "picareta";
                if (obj.RequiresFlag is not null && !_flags.Contains(obj.RequiresFlag)) return tool == "machado" ? $"{label} — precisa de machado (missão da batedora Lia)" : $"{label} — precisa de {tool}";
                if (NodeSecondsLeft(obj.Id) is var left and > 0)
                    return $"{label} — esgotado (volta em {(left >= 120 ? $"{left / 60} min" : $"{left} s")})";
                return (herb ? "Colher: " : obj.Yields == "mat_tabua" ? "Cortar: " : "Minerar: ") + label;
            case "prisioneiro":
                return "Desamarrar " + obj.Label.Replace(", amarrado", "");
            case "runa":
                return _flags.Contains("runa:" + obj.Id) ? "Runa acesa" : "Tocar a runa antiga";
            case "barris_polvora":
                return Me?.Carry is not null ? "Barris de pólvora (suas mãos estão ocupadas)" : "Pegar um barril de pólvora";
            case "canteiro":
                var plot = PlotFor(obj.Id);
                return plot is null ? "Plantar (precisa de semente)" : plot.State switch
                {
                    "planted" => $"Regar {plot.CropName}",
                    "growing" => $"{plot.CropName} crescendo ({plot.ReadyInMs / 60000 + 1} min)",
                    _ => $"Colher {plot.CropName}",
                };
            case "forja" or "bancada" or "caldeirao" or "panela" or "caos": return "Usar: " + obj.Label;
            // 17/11 noite: a porta diz o que faz (antes "Examinar: Porta (sair)")
            case "porta":
                var porPortal = obj.GrantFlag is { } pg && pg.StartsWith("porta:portal:", StringComparison.Ordinal) ? Map?.Portals.FirstOrDefault(q => q.Id == pg[13..]) : null;
                return porPortal is not null
                    ? porPortal.Label.StartsWith("Entrar", StringComparison.Ordinal) ? porPortal.Label : "Sair pela porta" + (porPortal.Label.Length > 0 ? " — " + porPortal.Label : "")
                    : obj.GrantFlag is "porta:entra" || (obj.GrantFlag?.StartsWith("porta:para:", StringComparison.Ordinal) ?? false) ? "Entrar: " + obj.Label
                    : "Bater na porta" + (obj.Label is { Length: > 0 } l && l != "Porta" ? ": " + l : "");
            case "armazem": return "Abrir seu depósito no armazém";
            case "bau_casa": return "Abrir o baú da casa";
            case "altar": return $"Oferecer {obj.ConsumeQty} {DisplayName(obj.Consumes ?? "")} ao {obj.Label}";
            default: return "Examinar: " + obj.Label;
        }
    }

    /// <summary>Nome da região do mapa onde (x,y) está, ou null.</summary>
    public MapZone? ZoneAt(double x, double y)
        => Map?.Zones.Where(z => Math.Abs(x - z.X) <= z.W / 2 && Math.Abs(y - z.Y) <= z.H / 2)
            .OrderBy(z => z.W * z.H).FirstOrDefault();

    public int CountInInventory(string definitionId) => Inventory.Where(i => i.DefinitionId == definitionId).Sum(i => i.Quantity);

    public InventoryItemView? FirstInventoryItemWithPrefix(string prefix)
        => Inventory.OrderBy(i => i.Slot).FirstOrDefault(i => i.DefinitionId.StartsWith(prefix, StringComparison.Ordinal));

    public static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ---- textos ----

    public static string DisplayName(string definitionId)
        => KnownItems.TryGetValue(definitionId, out var info) ? info.DisplayName : DefaultName(definitionId);

    private static string DefaultName(string definitionId) => definitionId switch
    {
        "weapon_guerreiro_sword_p1" => "Espada de Chapa",
        "consumable_c01_tonico_fraco" => "Tônico Fraco",
        _ => definitionId,
    };

    public static string SkillName(string skillId) => skillId switch
    {
        SkillGolpe => "Golpe de Cunha",
        SkillGuarda => "Guarda de Escudo",
        _ => KnownSkills.TryGetValue(skillId, out var name) ? name : skillId,
    };

    public static string FailureText(string failure) => failure switch
    {
        "OutOfRange" => "alvo longe demais",
        "OnCooldown" => "em recarga",
        "NotEnoughResource" => "Fúria/mana insuficiente",
        "NoStamina" => "Sem fôlego",
        "Busy" => "você está ocupado",
        "NoTarget" => "sem alvo",
        "TargetDead" => "alvo já morreu",
        "NotUnlocked" => "habilidade bloqueada",
        "CasterDead" => "você está morto",
        "WrongClass" => "não é da sua classe",
        "NoWeaponSkill" => "a arma equipada não tem essa habilidade",
        "NotEvolved" => "só depois da evolução de classe",
        "NeedsShield" => "precisa de um escudo na mão esquerda",
        "NotLearned" => "você não tem essa habilidade (a tecla X é a do item vestido, ex.: Máscara do Pandemônio)",
        _ => failure,
    };

    private static string ReasonText(string reason) => reason switch
    {
        "out_of_range" => "longe demais",
        "already_taken" => "já foi pego",
        "inventory_full" => "mochila cheia",
        "on_cooldown" => "em recarga",
        "no_stamina" => "sem fôlego",
        "not_in_inventory" => "item não está na mochila",
        "not_equippable" => "não pode ser equipado",
        "not_consumable" => "não é consumível",
        "level_too_low" => "nível insuficiente",
        "busy" => "você está ocupado",
        "no_target" => "sem alvo",
        "target_dead" => "alvo já morreu",
        "dead" => "você está morto",
        "two_handed" => "a arma é de duas mãos",
        "class_not_allowed" => "não é da sua classe",
        "already_opened" => "já foi aberto",
        "unknown_object" => "nada aqui",
        "name_taken" => "nome já usado",
        "bad_name" => "nome inválido (3 a 16 letras/números)",
        "too_many" => "limite de personagens",
        "unknown_class" => "classe indisponível",
        "nothing_equipped" => "nada equipado",
        "attribute_too_low" => "atributo principal insuficiente",
        "not_enough_coins" => "moedas insuficientes",
        "unknown_destination" => "destino ainda desconhecido",
        "need_tool" => "precisa de ferramenta (picareta: missão do Brás; machado de ofício: missão da ponte, com a Lia)",
        "node_empty" => "esgotado por enquanto",
        "no_seed" => "sem semente",
        "plot_busy" => "canteiro ocupado",
        "not_ready" => "ainda não está pronto",
        "storage_full" => "depósito cheio",
        "not_stored" => "não está no depósito",
        "recipe_unknown" => "receita desconhecida",
        "unknown_recipe" => "receita inválida",
        "no_mount" => "você ainda não tem montaria",
        "no_mount_here" => "sem montaria aqui",
        "mounted" => "desmonte primeiro",
        "no_wings" => "você ainda não tem asas",
        "no_pet" => "você ainda não tem companheiro",
        "not_yours" => "não é seu",
        "nothing_to_do" => "nada a fazer aqui agora",
        "not_sold_here" => "não vende isso",
        "no_value" => "não vale moedas",
        "not_yours_yet" => "ainda é de quem lutou (alguns segundos)",
        "missing_items" => "faltam itens",
        "not_done" => "ainda não terminou",
        "not_active" => "missão não está ativa",
        "requirements" => "requisito não cumprido",
        "already" => "já aceita",
        "in_combat" => "em combate",
        "not_at_waypoint" => "chegue mais perto da pedra de viagem",
        "in_dungeon" => "dentro da dungeon não dá para viajar — saia pela porta (ou termine a dungeon) primeiro",
        "unknown_waypoint" => "ponto não descoberto",
        "not_online" => "não está no jogo",
        "already_in_party" => "já está num grupo",
        "party_full" => "grupo cheio",
        "no_invite" => "sem convite",
        "no_party" => "sem grupo",
        "not_leader" => "só o líder",
        "no_points" => "sem pontos",
        "self" => "não dá para convidar a si mesmo",
        _ => reason,
    };

    /// <summary>Frase de sucesso legível (antes saía "Criar personagem: ok.").</summary>
    private static string SuccessText(string action) => action switch
    {
        "create_character" => "Personagem criado.",
        "equip" => "Equipado.",
        "unequip" => "Guardado na mochila.",
        "spend_stat" => "Ponto de atributo aplicado.",
        _ => $"{ActionName(action)}: feito.",
    };

    private static string ActionName(string action) => action switch
    {
        "equip" => "Equipar",
        "unequip" => "Desequipar",
        "use_item" => "Usar item",
        "auto_attack" => "Ataque automático",
        "interact" => "Interagir",
        "create_character" => "Criar personagem",
        "buy" => "Comprar",
        "sell" => "Vender",
        "quest" => "Missão",
        "travel" => "Viagem",
        "party" => "Grupo",
        "spend_stat" => "Atributo",
        _ => action,
    };

    private void AddLog(string line)
    {
        // 14/11 19h (dono: "tudo que acontece no jogo deve ir pro O — fez, não fez, deu erro, por que não abriu"):
        // cada linha do registro também vai para o histórico (canal "registro")
        Historicar("registro", line);
        _log.Add(line);
        while (_log.Count > MaxLogLines) _log.RemoveAt(0);
    }
}
