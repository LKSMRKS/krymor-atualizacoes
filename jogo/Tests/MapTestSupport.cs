using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Tests;

/// <summary>Servidor no modo mapa (E8) com banco temporário e transporte em memória.</summary>
internal sealed class MapHarness : IDisposable
{
    private readonly Dictionary<int, string> _characterIds = new();
    private readonly bool _ownsDb;

    public TempDb Temp { get; }
    public ContentBundle Content { get; }
    public BalanceConfig Balance { get; }
    public WorldContent World { get; }
    public InMemoryTransport Transport { get; } = new();
    public GameServer Server { get; }

    public static string ContentDir => Path.Combine(AppContext.BaseDirectory, "Content");
    public static WorldContent RealWorld() => WorldContentLoader.Load(ContentDir);

    /// <summary>Mapa de teste: quadrado aberto de 30 m, nascimento em (0,20) com zona segura de 3 m.</summary>
    public static WorldContent TestWorld(IEnumerable<MapSpawn>? spawns = null, IEnumerable<MapObstacle>? obstacles = null,
        IEnumerable<MapGate>? gates = null, IEnumerable<MapChest>? chests = null, double xpMultiplier = 1.0)
    {
        var real = RealWorld();
        var map = new MapLayout("teste", "Teste", 30, 0, 20, 3, xpMultiplier, Array.Empty<MapZone>(),
            (obstacles ?? Array.Empty<MapObstacle>()).ToList(), (gates ?? Array.Empty<MapGate>()).ToList(),
            (chests ?? Array.Empty<MapChest>()).ToList(), (spawns ?? Array.Empty<MapSpawn>()).ToList(), Array.Empty<MapProp>());
        return new WorldContent(map, real.Behaviors, real.LootTables);
    }

    public static MapSpawn Spawn(string id, string monster, double x, double y, int level = 4, string group = "g")
        => new(id, monster, x, y, level, group);

    public MapHarness(WorldContent? world = null, Func<double>? rng = null, TempDb? sharedDb = null)
    {
        Content = TestContent.Load();
        Balance = ContentLoader.LoadBalance(ContentDir);
        World = world ?? TestWorld();
        _ownsDb = sharedDb is null;
        Temp = sharedDb ?? new TempDb(Balance.InventoryCapacity);
        Server = new GameServer(Content, Temp.Db, Balance, Transport, rng ?? (() => 0.99), World);
    }

    public void Ticks(int n)
    {
        for (var i = 0; i < n; i++) Server.Tick();
    }

    /// <summary>Arma que os testes do E8 davam a todo personagem (o E9 tirou o kit inicial: personagem novo começa sem nada).</summary>
    public static string TestWeapon(string classId) => classId switch
    {
        "mago" => "staff_mago_aprendiz", "bruxa" => "talisma_bruxa_aprendiz", "arqueiro" => "arco_arqueiro_aprendiz",
        _ => "weapon_guerreiro_sword_p1",
    };

    /// <summary>
    /// Cria conta + personagem já posicionado no mapa e devolve o token. armed = true (padrão dos testes do E8): com a arma
    /// básica da classe equipada. armed = false: como um personagem novo do E9 (só as mãos).
    /// </summary>
    public string Precreate(string name, string classId = "guerreiro", double? x = null, double? y = null, int? level = null,
        bool armed = true, string? map = null)
    {
        var cls = Content.Classes[classId];
        var layout = World.Maps[map ?? World.Map.Id];
        var (accountId, token) = Temp.Db.ProvisionAccount(name);
        var id = Temp.Db.CreateCharacter(accountId, name, classId, level ?? Balance.StartingLevel, cls.StartingPrimary,
            cls.StartingVigor, layout.Id, x ?? layout.SpawnX, y ?? layout.SpawnY);
        _tokens[id] = token;
        foreach (var item in armed ? new[] { TestWeapon(classId) } : Array.Empty<string>())
        {
            var itemId = Temp.Db.CreateInventoryItem(id, item, "comum", 1, "starter_" + id + item)!;
            Temp.Db.TryEquip(id, itemId, GameServer.WeaponSlot);
        }
        Temp.Db.CreateInventoryItem(id, GameServer.StarterPotionDefinition, "comum", 3, "starter_" + id);
        return token;
    }

    public int Login(string token)
    {
        var id = Transport.Connect();
        Transport.ClientSends(id, new LoginRequest(token, Protocol.Version));
        Ticks(1);
        var result = Transport.Received<LoginResult>(id).LastOrDefault();
        if (result is { Ok: true, CharacterId: not null }) _characterIds[id] = result.CharacterId;
        return id;
    }

    public int Join(string name, string classId = "guerreiro", double? x = null, double? y = null, int? level = null,
        bool armed = true, string? map = null)
        => Login(Precreate(name, classId, x, y, level, armed, map));

    private readonly Dictionary<string, string> _tokens = new();

    /// <summary>E9: sai do jogo, grava o personagem em outro mapa/posição e entra de novo (devolve a nova conexão).</summary>
    public int Relocate(int clientId, string map, double x, double y)
    {
        var characterId = CharacterId(clientId);
        Transport.ClientDisconnects(clientId);
        Ticks(1);
        var r = Temp.Db.LoadCharacter(characterId)!;
        Temp.Db.SaveCharacterState(characterId, r.Level, r.Xp, map, x, y, r.Health);
        return Login(_tokens[characterId]);
    }

    /// <summary>14/11: marca as 4 provas de classe como feitas (a troca de classe passou a pedir as provas) e reconecta.</summary>
    public int ProvasDeClasseFeitas(int clientId, string letra)
    {
        var ch = CharacterId(clientId);
        for (var i = 1; i <= 4; i++)
        {
            var q = $"q_prova_{letra}_{i}";
            Temp.Db.TryAcceptQuest(ch, q, false);
            Temp.Db.TryCompleteQuest(ch, q, 0, null, 0, Array.Empty<GrantItem>(), 0);
        }
        var r = Temp.Db.LoadCharacter(ch)!;
        Transport.ClientDisconnects(clientId);
        Ticks(1);
        return Login(_tokens[ch]);
    }

    public T Last<T>(int id) where T : NetMessage => Transport.Received<T>(id).Last();

    public void Remember(int id, string characterId) => _characterIds[id] = characterId;
    public string CharacterId(int id) => _characterIds[id];

    /// <summary>13/11: o dono mexe nos NPCs pelo editor — testes vão até onde o NPC estiver (1 m ao sul dele).</summary>
    public (double X, double Y) PertoDoNpc(string npcId, string map = "porto_das_cinzas")
    {
        var n = World.Maps[map].Npcs.Single(x => x.Id == npcId);
        return (n.X, n.Y + 1.0);
    }

    /// <summary>13/11: idem para objetos de uso (máquina do caos, forja...).</summary>
    public (double X, double Y) PertoDoObjeto(string objId, string map = "porto_das_cinzas")
    {
        var o = World.Maps[map].Objects.Single(x => x.Id == objId);
        return (o.X, o.Y + 1.0);
    }

    /// <summary>12/11: a Guarda de Escudo pede escudo na mão esquerda — dá e veste um Escudo de Tábuas.</summary>
    public void EquipShield(int id)
    {
        var item = Temp.Db.CreateInventoryItem(CharacterId(id), "shield_guerreiro_tabuas", "comum", 1, "t:" + System.Guid.NewGuid())!;
        Transport.ClientSends(id, new EquipIntent(item));
        Ticks(2);
    }
    public Snapshot LastSnapshot(int id) => Transport.Received<Snapshot>(id).Last();
    public PlayerView Me(int id) => LastSnapshot(id).Players.First(p => p.CharacterId == _characterIds[id]);
    public MonsterView? Monster(int id, string monsterId) => LastSnapshot(id).Monsters.FirstOrDefault(m => m.Id == monsterId);

    public void Face(int id, double x, double y) { Transport.ClientSends(id, new FaceIntent(x, y)); Ticks(1); }

    public List<CombatEvent> Events(int id) => Transport.Received<CombatEvent>(id).ToList();

    /// <summary>Avança até a condição valer (no máximo maxTicks).</summary>
    public bool TickUntil(Func<bool> condition, int maxTicks = 400)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            if (condition()) return true;
            Server.Tick();
        }
        return condition();
    }

    public void Dispose()
    {
        if (_ownsDb) Temp.Dispose();
    }
}
