using System.Text.Json;
using CinzasDaMare.Server.Combat;
using Microsoft.Data.Sqlite;

namespace CinzasDaMare.Server.Persistence;

public sealed record CharacterRecord(
    string Id, string AccountId, string Name, string ClassId, int Level, long Xp,
    string MapId, double PosX, double PosY, double Health, int Strength, int Vigor, long Coins = 0, int StatPoints = 0);

public sealed record InventoryEntry(string ItemId, string DefinitionId, string Quality, int Slot, int Quantity, int Temper = 0);
public sealed record EquippedEntry(string Slot, string ItemId, string DefinitionId, string Quality, int Temper = 0);
public sealed record EquipResult(bool Success, string Reason, string? ReplacedItemId);
public sealed record ConsumeResult(bool Success, string Reason, string DefinitionId, int Remaining);
public sealed record ItemLocation(string Kind, string? OwnerCharacterId, string? Slot);
public sealed record ClaimResult(bool Success, string Reason, int Slot);
public sealed record CharacterListEntry(string Id, string Name, string ClassId, int Level);
public sealed record WorldItemEntry(string ItemId, string DefinitionId, string Quality, double X, double Y, int Quantity,
    string? MapId = null);
public sealed record QuestState(string QuestId, string State, int Progress, int Completions);
/// <summary>Resultado de compra/venda/missão: Coins = saldo depois da operação.</summary>
public sealed record TradeResult(bool Success, string Reason, long Coins);
public sealed record GrantItem(string DefinitionId, int Quantity, string Quality);
public sealed record ChestResult(bool Success, string Reason, IReadOnlyList<GrantItem> Items);

/// <summary>
/// Persistência SQLite (GDD 17.2). Operações de valor usam transação curta (BEGIN IMMEDIATE)
/// e ID de requisição idempotente. Uma instância de item ocupa uma única localização (PK em item_locations).
/// </summary>
public sealed partial class GameDatabase
{
    public const int CurrentSchemaVersion = 8;
    /// <summary>Pontos de atributo por nível (E9). Personagens antigos recebem os dos níveis que já tinham (migração v3).</summary>
    public const int StatPointsPerLevel = 3;
    /// <summary>Capacidade provisória de inventário (o GDD não define); o valor real vem de Content/balance.json.</summary>
    public const int DefaultInventoryCapacity = 40;

    private static readonly string[] Migrations = { MigrationV1, MigrationV2, MigrationV3, MigrationV4, MigrationV5, MigrationV6, MigrationV7, MigrationV8 };

    // 14/11 (menu do TAB: objetivos diários/semanais/da conta que dão CRISTAIS AZUIS, estilo Master Duel): contadores por
    // CONTA (vale para todos os personagens). Só ACRESCENTA a tabela; nada existente muda.
    private const string MigrationV8 = @"
CREATE TABLE IF NOT EXISTS conta_contadores (
    account_id TEXT NOT NULL,
    chave TEXT NOT NULL,
    valor INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (account_id, chave)
);";

    // Penalidade de morte (decisão do usuário, 09/11: "morrer perde level"): guarda o maior nível já alcançado para não
    // dar pontos de atributo de novo quando o personagem reconquista um nível perdido. Só ACRESCENTA a coluna.
    private const string MigrationV7 = @"
ALTER TABLE characters ADD COLUMN max_level INTEGER NOT NULL DEFAULT 0;
UPDATE characters SET max_level = level;";

    // Lobby estilo MU: excluir personagem = arquivar (só ACRESCENTA a coluna; nada é apagado do banco — o personagem
    // some da lista, o nome fica livre e itens/progresso continuam guardados).
    private const string MigrationV6 = @"
ALTER TABLE characters ADD COLUMN deleted_at TEXT;";

    // Rodada 3: atributos estilo MU para todos (só ACRESCENTA: pontos extras em Força/Agilidade/Energia, começando em 0;
    // o atributo principal da classe continua em "strength", a Vitalidade em "vigor").
    private const string MigrationV5 = @"
ALTER TABLE character_stats ADD COLUMN attr_forca INTEGER NOT NULL DEFAULT 0;
ALTER TABLE character_stats ADD COLUMN attr_agilidade INTEGER NOT NULL DEFAULT 0;
ALTER TABLE character_stats ADD COLUMN attr_energia INTEGER NOT NULL DEFAULT 0;";

    // E9: só ACRESCENTA. Mapa do item no chão (vários mapas), moedas, pontos de atributo (antigos ganham os dos níveis
    // que já têm: nada é retirado) e missões.
    private const string MigrationV3 = @"
ALTER TABLE item_locations ADD COLUMN world_map TEXT;
ALTER TABLE characters ADD COLUMN coins INTEGER NOT NULL DEFAULT 0;
ALTER TABLE character_stats ADD COLUMN stat_points INTEGER NOT NULL DEFAULT 0;
UPDATE character_stats SET stat_points = 3 * ((SELECT level FROM characters WHERE characters.id = character_stats.character_id) - 1);
CREATE TABLE character_quests (
  character_id TEXT NOT NULL REFERENCES characters(id),
  quest_id TEXT NOT NULL,
  state TEXT NOT NULL CHECK (state IN ('active','done')),
  progress INTEGER NOT NULL DEFAULT 0,
  completions INTEGER NOT NULL DEFAULT 0,
  updated_at TEXT NOT NULL,
  PRIMARY KEY (character_id, quest_id)
);";

    // E8: só ACRESCENTA (colunas e tabela novas). Nada existente é alterado ou apagado; o Initialize faz backup antes.
    private const string MigrationV2 = @"
ALTER TABLE item_locations ADD COLUMN world_x REAL;
ALTER TABLE item_locations ADD COLUMN world_y REAL;
CREATE TABLE character_flags (
  character_id TEXT NOT NULL REFERENCES characters(id),
  flag TEXT NOT NULL,
  created_at TEXT NOT NULL,
  PRIMARY KEY (character_id, flag)
);";

    /// <summary>
    /// Limite de pilha por definição (E8, modo mapa): ao coletar ou receber um consumível, soma na pilha existente.
    /// Vazio (padrão) = comportamento antigo, cada item ocupa um slot.
    /// </summary>
    public IReadOnlyDictionary<string, int> StackLimits { get; set; } = new Dictionary<string, int>();

    private const string MigrationV1 = @"
CREATE TABLE accounts (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL UNIQUE,
  token_hash TEXT NOT NULL,
  revoked INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL
);
CREATE TABLE characters (
  id TEXT PRIMARY KEY,
  account_id TEXT NOT NULL REFERENCES accounts(id),
  name TEXT NOT NULL UNIQUE,
  class_id TEXT NOT NULL,
  level INTEGER NOT NULL CHECK (level >= 1),
  xp INTEGER NOT NULL DEFAULT 0 CHECK (xp >= 0),
  map_id TEXT NOT NULL,
  pos_x REAL NOT NULL,
  pos_y REAL NOT NULL,
  health REAL NOT NULL CHECK (health >= 0),
  created_at TEXT NOT NULL
);
CREATE TABLE character_stats (
  character_id TEXT PRIMARY KEY REFERENCES characters(id),
  strength INTEGER NOT NULL,
  vigor INTEGER NOT NULL
);
CREATE TABLE item_instances (
  id TEXT PRIMARY KEY,
  definition_id TEXT NOT NULL,
  quality TEXT NOT NULL,
  temper_level INTEGER NOT NULL DEFAULT 0,
  quantity INTEGER NOT NULL DEFAULT 1 CHECK (quantity >= 1),
  version INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  origin_event_id TEXT NOT NULL
);
CREATE TABLE item_locations (
  item_id TEXT PRIMARY KEY REFERENCES item_instances(id),
  location_kind TEXT NOT NULL CHECK (location_kind IN ('world','inventory','equipment')),
  owner_character_id TEXT REFERENCES characters(id),
  slot TEXT,
  CHECK (
    (location_kind = 'world' AND owner_character_id IS NULL AND slot IS NULL) OR
    (location_kind <> 'world' AND owner_character_id IS NOT NULL AND slot IS NOT NULL)
  )
);
CREATE UNIQUE INDEX ux_item_location_slot
  ON item_locations(owner_character_id, location_kind, slot)
  WHERE owner_character_id IS NOT NULL;
CREATE TABLE processed_requests (
  request_id TEXT PRIMARY KEY,
  kind TEXT NOT NULL,
  result_json TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE TABLE transaction_log (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  kind TEXT NOT NULL,
  character_id TEXT,
  item_id TEXT,
  request_id TEXT,
  detail TEXT,
  created_at TEXT NOT NULL
);";

    private readonly string _connectionString;

    public string FilePath { get; }
    public int InventoryCapacity { get; }

    public GameDatabase(string filePath, int inventoryCapacity = DefaultInventoryCapacity)
    {
        FilePath = filePath;
        InventoryCapacity = inventoryCapacity;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();
    }

    // ---------- esquema ----------

    public void Initialize()
    {
        using (var c = Connect())
        {
            Exec(c, "PRAGMA journal_mode = WAL;");
            Exec(c, "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL);");
        }

        var current = GetSchemaVersion();
        if (current > CurrentSchemaVersion)
            throw new InvalidOperationException(
                $"Banco na versão {current}, mas este servidor só entende até {CurrentSchemaVersion}.");
        if (current == CurrentSchemaVersion) return;

        // Migração exige cópia prévia (GDD 17.2).
        if (current > 0) BackupTo($"{FilePath}.pre-v{current}.bak");

        for (var v = current + 1; v <= CurrentSchemaVersion; v++)
        {
            var sql = Migrations[v - 1];
            var version = v;
            Write(c =>
            {
                Exec(c, sql);
                Exec(c, "DELETE FROM schema_version;");
                using var cmd = Cmd(c, "INSERT INTO schema_version(version) VALUES(@v)", ("@v", version));
                cmd.ExecuteNonQuery();
            });
        }
    }

    public int GetSchemaVersion()
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT COALESCE(MAX(version), 0) FROM schema_version");
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Backup consistente via API de backup do SQLite (inclui dados ainda no WAL).</summary>
    public void BackupTo(string destinationPath)
    {
        using var source = Connect();
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        destination.Open();
        source.BackupDatabase(destination);
    }

    // ---------- contas e personagens ----------

    public string CreateAccount(string name, string tokenHash)
    {
        var id = Guid.NewGuid().ToString();
        Write(c =>
        {
            using var cmd = Cmd(c,
                "INSERT INTO accounts(id,name,token_hash,revoked,created_at) VALUES(@i,@n,@h,0,@t)",
                ("@i", id), ("@n", name), ("@h", tokenHash), ("@t", Now()));
            cmd.ExecuteNonQuery();
        });
        return id;
    }

    public string CreateCharacter(string accountId, string name, string classId, int level,
        int strength, int vigor, string mapId = "cidade", double x = 0, double y = 0)
    {
        var id = Guid.NewGuid().ToString();
        var health = CombatFormulas.MaxHealth(level, vigor);
        Write(c =>
        {
            using (var cmd = Cmd(c,
                "INSERT INTO characters(id,account_id,name,class_id,level,xp,map_id,pos_x,pos_y,health,created_at) " +
                "VALUES(@i,@a,@n,@cl,@lv,0,@m,@x,@y,@h,@t)",
                ("@i", id), ("@a", accountId), ("@n", name), ("@cl", classId), ("@lv", level),
                ("@m", mapId), ("@x", x), ("@y", y), ("@h", health), ("@t", Now())))
                cmd.ExecuteNonQuery();
            using (var cmd2 = Cmd(c,
                "INSERT INTO character_stats(character_id,strength,vigor) VALUES(@i,@s,@v)",
                ("@i", id), ("@s", strength), ("@v", vigor)))
                cmd2.ExecuteNonQuery();
        });
        return id;
    }

    public CharacterRecord? LoadCharacter(string id)
    {
        using var c = Connect();
        using var cmd = Cmd(c,
            "SELECT c.id,c.account_id,c.name,c.class_id,c.level,c.xp,c.map_id,c.pos_x,c.pos_y,c.health,s.strength,s.vigor,c.coins,s.stat_points " +
            "FROM characters c JOIN character_stats s ON s.character_id = c.id WHERE c.id = @i",
            ("@i", id));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new CharacterRecord(
            r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetInt64(5),
            r.GetString(6), r.GetDouble(7), r.GetDouble(8), r.GetDouble(9), r.GetInt32(10), r.GetInt32(11), r.GetInt64(12), r.GetInt32(13));
    }

    public bool SaveCharacterState(string id, int level, long xp, string mapId, double x, double y, double health)
    {
        return InTransaction(c =>
        {
            using var cmd = Cmd(c,
                "UPDATE characters SET level=@lv, xp=@xp, map_id=@m, pos_x=@x, pos_y=@y, health=@h WHERE id=@i",
                ("@lv", level), ("@xp", xp), ("@m", mapId), ("@x", x), ("@y", y), ("@h", health), ("@i", id));
            return cmd.ExecuteNonQuery() == 1;
        });
    }

    // ---------- autenticação por token (GDD 17.2) ----------

    /// <summary>Cria conta convidada e devolve o token em texto (mostrado uma única vez; só o hash é guardado).</summary>
    public (string AccountId, string Token) ProvisionAccount(string name)
    {
        var token = TokenHasher.NewToken();
        var id = CreateAccount(name, TokenHasher.Hash(token));
        return (id, token);
    }

    public string? AuthenticateToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 128) return null;
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT id FROM accounts WHERE token_hash=@h AND revoked=0", ("@h", TokenHasher.Hash(token)));
        return cmd.ExecuteScalar() as string;
    }

    public bool RevokeAccount(string name)
    {
        return InTransaction(c =>
        {
            using var cmd = Cmd(c, "UPDATE accounts SET revoked=1 WHERE name=@n", ("@n", name));
            return cmd.ExecuteNonQuery() == 1;
        });
    }

    public string? GetAccountName(string accountId)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT name FROM accounts WHERE id=@i", ("@i", accountId));
        return cmd.ExecuteScalar() as string;
    }

    public string? FindCharacterByAccount(string accountId)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT id FROM characters WHERE account_id=@a AND deleted_at IS NULL ORDER BY created_at LIMIT 1", ("@a", accountId));
        return cmd.ExecuteScalar() as string;
    }

    // ---------- itens ----------

    public string CreateWorldItem(string definitionId, string quality, string originEventId)
    {
        var id = Guid.NewGuid().ToString();
        Write(c => InsertWorldItem(c, id, definitionId, quality, originEventId));
        return id;
    }

    /// <summary>
    /// Item no chão com posição (E8: loot de monstro). O originEventId é único por queda: se o mesmo evento
    /// for processado de novo, devolve o item já criado em vez de duplicar.
    /// </summary>
    public string CreateWorldItemAt(string definitionId, string quality, string originEventId, double x, double y, int quantity = 1,
        string? mapId = null, int temper = 0)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT id FROM item_instances WHERE origin_event_id=@o LIMIT 1", ("@o", originEventId)))
                if (q.ExecuteScalar() is string existing) return existing;
            var id = Guid.NewGuid().ToString();
            using (var a = Cmd(c,
                "INSERT INTO item_instances(id,definition_id,quality,temper_level,quantity,version,created_at,origin_event_id) " +
                "VALUES(@i,@d,@q,@tp,@n,1,@t,@o)",
                ("@i", id), ("@d", definitionId), ("@q", quality), ("@tp", Math.Clamp(temper, 0, 15)), ("@n", Math.Max(1, quantity)), ("@t", Now()), ("@o", originEventId)))
                a.ExecuteNonQuery();
            using (var b = Cmd(c,
                "INSERT INTO item_locations(item_id,location_kind,owner_character_id,slot,world_x,world_y,world_map) VALUES(@i,'world',NULL,NULL,@x,@y,@m)",
                ("@i", id), ("@x", x), ("@y", y), ("@m", mapId)))
                b.ExecuteNonQuery();
            Log(c, "drop_item", null, id, originEventId, $"{definitionId} x{quantity}");
            return id;
        });
    }

    /// <summary>Itens no chão que têm posição gravada (loot de monstro ainda não coletado).</summary>
    public IReadOnlyList<WorldItemEntry> GetWorldItemsWithPosition()
    {
        using var c = Connect();
        using var cmd = Cmd(c,
            "SELECT i.id, i.definition_id, i.quality, l.world_x, l.world_y, i.quantity, l.world_map FROM item_locations l " +
            "JOIN item_instances i ON i.id = l.item_id WHERE l.location_kind='world' AND l.world_x IS NOT NULL");
        var list = new List<WorldItemEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new WorldItemEntry(r.GetString(0), r.GetString(1), r.GetString(2), r.GetDouble(3), r.GetDouble(4), r.GetInt32(5),
                r.IsDBNull(6) ? null : r.GetString(6)));
        return list;
    }

    /// <summary>Some com um item do chão (expirou). Só apaga se ele AINDA estiver no chão.</summary>
    public bool TryRemoveWorldItem(string itemId)
    {
        return InTransaction(c =>
        {
            using (var k = Cmd(c, "SELECT location_kind FROM item_locations WHERE item_id=@i", ("@i", itemId)))
                if (k.ExecuteScalar() as string != "world") return false;
            using (var d1 = Cmd(c, "DELETE FROM item_locations WHERE item_id=@i AND location_kind='world'", ("@i", itemId))) d1.ExecuteNonQuery();
            using (var d2 = Cmd(c, "DELETE FROM item_instances WHERE id=@i", ("@i", itemId))) d2.ExecuteNonQuery();
            Log(c, "expire_item", null, itemId, null, null);
            return true;
        });
    }

    // ---------- E8: lobby, marcas do personagem e baús ----------

    public IReadOnlyList<CharacterListEntry> ListCharacters(string accountId)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT id, name, class_id, level FROM characters WHERE account_id=@a AND deleted_at IS NULL ORDER BY created_at", ("@a", accountId));
        var list = new List<CharacterListEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new CharacterListEntry(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3)));
        return list;
    }

    /// <summary>Contas (nome) e seus personagens "Nome (classe, nível)" — para o atalho de inicialização. Sem tokens.</summary>
    public IReadOnlyList<(string Account, IReadOnlyList<string> Characters)> ListAccounts()
    {
        using var c = Connect();
        using var cmd = Cmd(c,
            "SELECT a.name, c.name, c.class_id, c.level FROM accounts a LEFT JOIN characters c ON c.account_id = a.id AND c.deleted_at IS NULL " +
            "WHERE a.revoked = 0 ORDER BY a.created_at, c.created_at");
        var map = new List<(string, List<string>)>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var account = r.GetString(0);
            if (map.Count == 0 || map[^1].Item1 != account) map.Add((account, new List<string>()));
            if (!r.IsDBNull(1)) map[^1].Item2.Add($"{r.GetString(1)} ({r.GetString(2)}, nível {r.GetInt32(3)})");
        }
        return map.Select(x => (x.Item1, (IReadOnlyList<string>)x.Item2)).ToList();
    }

    /// <summary>
    /// Exclui (arquiva) um personagem da conta: sai da lista e libera o nome. Não apaga linhas — itens, missões e
    /// registros ficam no banco (dá para restaurar à mão). Falso se não for da conta ou já estiver arquivado.
    /// </summary>
    public bool TryArchiveCharacter(string accountId, string characterId)
    {
        return InTransaction(c =>
        {
            using var u = Cmd(c,
                "UPDATE characters SET deleted_at=@t, name = name || '~' || substr(id, 1, 8) " +
                "WHERE id=@i AND account_id=@a AND deleted_at IS NULL",
                ("@t", Now()), ("@i", characterId), ("@a", accountId));
            if (u.ExecuteNonQuery() != 1) return false;
            Log(c, "archive_character", characterId, null, null, accountId);
            return true;
        });
    }

    public bool CharacterNameExists(string name)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT 1 FROM characters WHERE name=@n COLLATE NOCASE", ("@n", name));
        return cmd.ExecuteScalar() is not null;
    }

    public IReadOnlyList<string> GetFlags(string characterId)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT flag FROM character_flags WHERE character_id=@c ORDER BY flag", ("@c", characterId));
        var list = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>
    /// 11/11 (ranking da dungeon): melhores marcas "recorde:mapa:ms" de todos os personagens, as menores primeiro.
    /// Só lê a tabela de marcas que já existe (sem migração).
    /// </summary>
    public IReadOnlyList<(string Name, long Ms)> DungeonRecords(string mapId, int top)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT ch.name, f.flag FROM character_flags f JOIN characters ch ON ch.id = f.character_id " +
            "WHERE substr(f.flag,1,length(@p))=@p", ("@p", "recorde:" + mapId + ":"));
        var list = new List<(string, long)>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (long.TryParse(r.GetString(1).Split(':').Last(), out var ms)) list.Add((r.GetString(0), ms));
        return list.OrderBy(x => x.Item2).Take(top).ToList();
    }

    /// <summary>11/11 (reset estilo MU): volta ao nível 1 (XP zero) e ganha pontos de atributo de bônus. Itens e atributos ficam.</summary>
    public bool TryRebirth(string characterId, int minLevel, int bonusPoints)
    {
        return InTransaction(c =>
        {
            using (var u = Cmd(c, "UPDATE characters SET level = 1, xp = 0 WHERE id=@i AND level >= @l", ("@i", characterId), ("@l", minLevel)))
                if (u.ExecuteNonQuery() != 1) return false;
            using (var u = Cmd(c, "UPDATE character_stats SET stat_points = stat_points + @b WHERE character_id=@i", ("@b", bonusPoints), ("@i", characterId)))
                u.ExecuteNonQuery();
            Log(c, "reset", characterId, null, null, $"+{bonusPoints} pontos");
            return true;
        });
    }

    /// <summary>13/11: placar entre amigos — nível, renascimentos, maior refino vestido e conquistas de cada personagem.</summary>
    public IReadOnlyList<(string Nome, int Nivel, int Renasceu, int Refino, int Conquistas)> Placar()
        => PlacarCompleto().Select(x => (x.Nome, x.Nivel, x.Renasceu, x.Refino, x.Conquistas)).ToList();

    /// <summary>13/11 ("mais categorias no placar do Barto"): além do básico, golpe mais forte, chefes diferentes vencidos,
    /// duelos ganhos e moedas. Tudo vem de marcas e tabelas que já existem (nada novo no esquema).</summary>
    public IReadOnlyList<(string Nome, int Nivel, int Renasceu, int Refino, int Conquistas, int Golpe, int Chefes, int Duelos, int Moedas)> PlacarCompleto()
    {
        return InTransaction(c =>
        {
            var list = new List<(string, int, int, int, int, int, int, int, int)>();
            using var cmd = Cmd(c,
                "SELECT ch.name, ch.level, " +
                "(SELECT COUNT(*) FROM character_flags f WHERE f.character_id = ch.id AND f.flag LIKE 'renasceu:%'), " +
                "IFNULL((SELECT MAX(i.temper_level) FROM item_locations l JOIN item_instances i ON i.id = l.item_id WHERE l.owner_character_id = ch.id AND l.location_kind = 'equipment'), 0), " +
                "(SELECT COUNT(*) FROM character_flags f WHERE f.character_id = ch.id AND f.flag LIKE 'conquista:%'), " +
                "IFNULL((SELECT MAX(CAST(substr(f.flag, 11) AS INTEGER)) FROM character_flags f WHERE f.character_id = ch.id AND f.flag LIKE 'golpe_max:%'), 0), " +
                "(SELECT COUNT(*) FROM character_flags f WHERE f.character_id = ch.id AND f.flag LIKE 'chefe_vencido:%'), " +
                "(SELECT COUNT(*) FROM character_flags f WHERE f.character_id = ch.id AND f.flag LIKE 'x1v:%'), " +
                "IFNULL(ch.coins, 0) " +
                "FROM characters ch WHERE ch.deleted_at IS NULL");
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add((r.GetString(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6), r.GetInt32(7), r.GetInt32(8)));
            return (IReadOnlyList<(string, int, int, int, int, int, int, int, int)>)list;
        });
    }

    /// <summary>Apaga marcas com um prefixo (ex.: "petsel:" ao guardar o companheiro). Não mexe no esquema.</summary>
    public void RemoveFlags(string characterId, string prefix)
    {
        InTransaction(c =>
        {
            using var d = Cmd(c, "DELETE FROM character_flags WHERE character_id=@c AND substr(flag,1,length(@p))=@p", ("@c", characterId), ("@p", prefix));
            d.ExecuteNonQuery();
            return 0;
        });
    }

    /// <summary>Grava uma marca (alavanca puxada etc.). Falso se ela já existia.</summary>
    public bool TryAddFlag(string characterId, string flag)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT 1 FROM character_flags WHERE character_id=@c AND flag=@f", ("@c", characterId), ("@f", flag)))
                if (q.ExecuteScalar() is not null) return false;
            using (var i = Cmd(c, "INSERT INTO character_flags(character_id,flag,created_at) VALUES(@c,@f,@t)",
                ("@c", characterId), ("@f", flag), ("@t", Now())))
                i.ExecuteNonQuery();
            Log(c, "flag", characterId, null, null, flag);
            return true;
        });
    }

    /// <summary>
    /// Abre um baú para um personagem: grava a marca e entrega os itens na MESMA transação.
    /// Se a marca já existe (baú aberto antes, ou pedido repetido) nada é entregue. Mochila sem espaço: nada muda.
    /// </summary>
    public ChestResult TryOpenChest(string characterId, string flag, IReadOnlyList<GrantItem> items)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT 1 FROM character_flags WHERE character_id=@c AND flag=@f", ("@c", characterId), ("@f", flag)))
                if (q.ExecuteScalar() is not null) return new ChestResult(false, "already_opened", Array.Empty<GrantItem>());
            using (var i = Cmd(c, "INSERT INTO character_flags(character_id,flag,created_at) VALUES(@c,@f,@t)",
                ("@c", characterId), ("@f", flag), ("@t", Now())))
                i.ExecuteNonQuery();
            var index = 0;
            foreach (var item in items)
            {
                if (!GrantInto(c, characterId, item, $"{flag}:{characterId}:{index++}"))
                    throw new InventoryFullException();
            }
            Log(c, "open_chest", characterId, null, null, flag);
            return new ChestResult(true, "ok", items);
        }, onInventoryFull: () => new ChestResult(false, "inventory_full", Array.Empty<GrantItem>()));
    }

    private sealed class InventoryFullException : Exception { }

    /// <summary>Entrega um item na mochila (somando à pilha, se configurado). Falso = sem espaço.</summary>
    private bool GrantInto(SqliteConnection c, string characterId, GrantItem item, string originEventId)
    {
        if (TryMergeIntoStack(c, characterId, item.DefinitionId, item.Quantity)) return true;
        var slot = FindFreeInventorySlot(c, characterId, item.DefinitionId);
        if (slot < 0) return false;
        var id = Guid.NewGuid().ToString();
        using (var a = Cmd(c,
            "INSERT INTO item_instances(id,definition_id,quality,temper_level,quantity,version,created_at,origin_event_id) " +
            "VALUES(@i,@d,@q,0,@n,1,@t,@o)",
            ("@i", id), ("@d", item.DefinitionId), ("@q", item.Quality), ("@n", item.Quantity), ("@t", Now()), ("@o", originEventId)))
            a.ExecuteNonQuery();
        using (var b = Cmd(c,
            "INSERT INTO item_locations(item_id,location_kind,owner_character_id,slot) VALUES(@i,'inventory',@c,@s)",
            ("@i", id), ("@c", characterId), ("@s", slot.ToString())))
            b.ExecuteNonQuery();
        Log(c, "grant_item", characterId, id, null, $"{item.DefinitionId} x{item.Quantity} slot {slot}");
        return true;
    }

    /// <summary>
    /// 14/11 ("as poções ficam 20, 20, 20, 20 ocupando espaço"): junta pilhas do mesmo item da mochila até o limite atual
    /// (o limite subiu para 100). Devolve quantos espaços liberou. Nada some: a soma das quantidades fica igual.
    /// </summary>
    public int MergeInventoryStacks(string characterId)
    {
        if (StackLimits.Count == 0) return 0;
        return InTransaction(c =>
        {
            var stacks = new List<(string Id, string Def, int Qty)>();
            using (var q = Cmd(c,
                "SELECT i.id, i.definition_id, i.quantity FROM item_instances i JOIN item_locations l ON l.item_id = i.id " +
                "WHERE l.owner_character_id=@c AND l.location_kind='inventory' ORDER BY CAST(l.slot AS INTEGER)", ("@c", characterId)))
            using (var r = q.ExecuteReader())
                while (r.Read()) stacks.Add((r.GetString(0), r.GetString(1), r.GetInt32(2)));
            var freed = 0;
            foreach (var grp in stacks.GroupBy(x => x.Def))
            {
                if (!StackLimits.TryGetValue(grp.Key, out var limit) || limit <= 1 || grp.Count() < 2) continue;
                var list = grp.Select(x => (x.Id, Qty: x.Qty)).ToList();
                var total = list.Sum(x => x.Qty);
                // as primeiras pilhas (pela ordem dos espaços) ficam cheias; as que sobrarem vazias saem
                for (var i = 0; i < list.Count; i++)
                {
                    var q = Math.Min(limit, total);
                    total -= q;
                    if (q > 0)
                    {
                        if (q != list[i].Qty)
                            using (var u = Cmd(c, "UPDATE item_instances SET quantity=@n, version = version + 1 WHERE id=@i", ("@n", q), ("@i", list[i].Id)))
                                u.ExecuteNonQuery();
                    }
                    else
                    {
                        using (var d1 = Cmd(c, "DELETE FROM item_locations WHERE item_id=@i", ("@i", list[i].Id))) d1.ExecuteNonQuery();
                        using (var d2 = Cmd(c, "DELETE FROM item_instances WHERE id=@i", ("@i", list[i].Id))) d2.ExecuteNonQuery();
                        freed++;
                    }
                }
            }
            if (freed > 0) Log(c, "merge_stacks", characterId, null, null, $"{freed} espaço(s) liberado(s)");
            return freed;
        });
    }

    /// <summary>Soma a uma pilha existente do mesmo item, se houver limite configurado e espaço na pilha.</summary>
    private bool TryMergeIntoStack(SqliteConnection c, string characterId, string definitionId, int quantity)
    {
        if (!StackLimits.TryGetValue(definitionId, out var limit) || limit <= 1) return false;
        using var q = Cmd(c,
            "SELECT i.id FROM item_instances i JOIN item_locations l ON l.item_id = i.id " +
            "WHERE l.owner_character_id=@c AND l.location_kind='inventory' AND i.definition_id=@d AND i.quantity + @n <= @lim " +
            "ORDER BY CAST(l.slot AS INTEGER) LIMIT 1",
            ("@c", characterId), ("@d", definitionId), ("@n", quantity), ("@lim", limit));
        if (q.ExecuteScalar() is not string stackId) return false;
        using var u = Cmd(c, "UPDATE item_instances SET quantity = quantity + @n, version = version + 1 WHERE id=@i",
            ("@n", quantity), ("@i", stackId));
        u.ExecuteNonQuery();
        return true;
    }

    /// <summary>
    /// Garante um item "semente" por origem: cria uma vez; depois só devolve o ID se ele ainda estiver no chão
    /// (se já foi coletado, devolve null e não recria).
    /// </summary>
    public string? EnsureSeedItem(string originEventId, string definitionId, string quality)
    {
        return InTransaction<string?>(c =>
        {
            using (var q = Cmd(c,
                "SELECT i.id, l.location_kind FROM item_instances i JOIN item_locations l ON l.item_id = i.id " +
                "WHERE i.origin_event_id=@o LIMIT 1", ("@o", originEventId)))
            using (var r = q.ExecuteReader())
            {
                if (r.Read()) return r.GetString(1) == "world" ? r.GetString(0) : null;
            }
            var id = Guid.NewGuid().ToString();
            InsertWorldItem(c, id, definitionId, quality, originEventId);
            return id;
        });
    }

    private static void InsertWorldItem(SqliteConnection c, string id, string definitionId, string quality, string originEventId)
    {
        using (var a = Cmd(c,
            "INSERT INTO item_instances(id,definition_id,quality,temper_level,quantity,version,created_at,origin_event_id) " +
            "VALUES(@i,@d,@q,0,1,1,@t,@o)",
            ("@i", id), ("@d", definitionId), ("@q", quality), ("@t", Now()), ("@o", originEventId)))
            a.ExecuteNonQuery();
        using (var b = Cmd(c,
            "INSERT INTO item_locations(item_id,location_kind,owner_character_id,slot) VALUES(@i,'world',NULL,NULL)",
            ("@i", id)))
            b.ExecuteNonQuery();
    }

    /// <summary>
    /// Coleta exclusiva de item no chão. Idempotente por requestId: repetir o mesmo ID devolve o
    /// resultado já gravado, sem nova tentativa.
    /// </summary>
    public ClaimResult TryClaimWorldItem(string requestId, string itemId, string characterId)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT result_json FROM processed_requests WHERE request_id=@r", ("@r", requestId)))
            {
                if (q.ExecuteScalar() is string json)
                    return JsonSerializer.Deserialize<ClaimResult>(json)!;
            }

            var result = ResolveClaim(c, itemId, characterId);

            using (var ins = Cmd(c,
                "INSERT INTO processed_requests(request_id,kind,result_json,created_at) VALUES(@r,'claim_item',@j,@t)",
                ("@r", requestId), ("@j", JsonSerializer.Serialize(result)), ("@t", Now())))
                ins.ExecuteNonQuery();

            if (result.Success)
            {
                using var log = Cmd(c,
                    "INSERT INTO transaction_log(kind,character_id,item_id,request_id,detail,created_at) " +
                    "VALUES('claim_item',@c,@i,@r,@d,@t)",
                    ("@c", characterId), ("@i", itemId), ("@r", requestId), ("@d", $"slot {result.Slot}"), ("@t", Now()));
                log.ExecuteNonQuery();
            }
            return result;
        });
    }

    private ClaimResult ResolveClaim(SqliteConnection c, string itemId, string characterId)
    {
        using (var ch = Cmd(c, "SELECT 1 FROM characters WHERE id=@c", ("@c", characterId)))
            if (ch.ExecuteScalar() is null) return new ClaimResult(false, "unknown_character", -1);

        string? kind;
        using (var k = Cmd(c, "SELECT location_kind FROM item_locations WHERE item_id=@i", ("@i", itemId)))
            kind = k.ExecuteScalar() as string;
        if (kind is null) return new ClaimResult(false, "unknown_item", -1);
        if (kind != "world") return new ClaimResult(false, "already_taken", -1);

        if (StackLimits.Count > 0)
        {
            // E8: consumível coletado soma na pilha que já existe (o item do chão deixa de existir).
            string? definitionId = null;
            var quantity = 1;
            using (var dq = Cmd(c, "SELECT definition_id, quantity FROM item_instances WHERE id=@i", ("@i", itemId)))
            using (var dr = dq.ExecuteReader())
                if (dr.Read()) { definitionId = dr.GetString(0); quantity = dr.GetInt32(1); }
            if (definitionId is not null && TryMergeIntoStack(c, characterId, definitionId, quantity))
            {
                using (var d1 = Cmd(c, "DELETE FROM item_locations WHERE item_id=@i AND location_kind='world'", ("@i", itemId)))
                    if (d1.ExecuteNonQuery() != 1) return new ClaimResult(false, "already_taken", -1);
                using (var d2 = Cmd(c, "DELETE FROM item_instances WHERE id=@i", ("@i", itemId))) d2.ExecuteNonQuery();
                return new ClaimResult(true, "ok", 0);
            }
        }

        var slot = FindFreeInventorySlot(c, characterId, DefinitionOf(c, itemId) ?? "");
        if (slot < 0) return new ClaimResult(false, "inventory_full", -1);

        using (var u = Cmd(c,
            "UPDATE item_locations SET location_kind='inventory', owner_character_id=@c, slot=@s, world_x=NULL, world_y=NULL, world_map=NULL " +
            "WHERE item_id=@i AND location_kind='world'",
            ("@c", characterId), ("@s", slot.ToString()), ("@i", itemId)))
            if (u.ExecuteNonQuery() != 1) return new ClaimResult(false, "already_taken", -1);

        using (var v = Cmd(c, "UPDATE item_instances SET version = version + 1 WHERE id=@i", ("@i", itemId)))
            v.ExecuteNonQuery();

        return new ClaimResult(true, "ok", slot);
    }

    public IReadOnlyList<InventoryEntry> GetInventory(string characterId)
    {
        using var c = Connect();
        using var cmd = Cmd(c,
            "SELECT i.id, i.definition_id, i.quality, l.slot, i.quantity, i.temper_level FROM item_locations l " +
            "JOIN item_instances i ON i.id = l.item_id " +
            "WHERE l.owner_character_id=@c AND l.location_kind='inventory'",
            ("@c", characterId));
        var list = new List<InventoryEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new InventoryEntry(r.GetString(0), r.GetString(1), r.GetString(2), int.Parse(r.GetString(3)), r.GetInt32(4), r.GetInt32(5)));
        list.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        return list;
    }

    // ---------- E4: item no inventário, equipar, consumir ----------

    /// <summary>Cria um item (ou pilha) direto no inventário. Devolve null se o inventário estiver cheio.</summary>
    public string? CreateInventoryItem(string characterId, string definitionId, string quality,
        int quantity, string originEventId)
    {
        var id = Guid.NewGuid().ToString();
        return InTransaction<string?>(c =>
        {
            var slot = FindFreeInventorySlot(c, characterId, definitionId);
            if (slot < 0) return null;
            using (var a = Cmd(c,
                "INSERT INTO item_instances(id,definition_id,quality,temper_level,quantity,version,created_at,origin_event_id) " +
                "VALUES(@i,@d,@q,0,@n,1,@t,@o)",
                ("@i", id), ("@d", definitionId), ("@q", quality), ("@n", quantity), ("@t", Now()), ("@o", originEventId)))
                a.ExecuteNonQuery();
            using (var b = Cmd(c,
                "INSERT INTO item_locations(item_id,location_kind,owner_character_id,slot) VALUES(@i,'inventory',@c,@s)",
                ("@i", id), ("@c", characterId), ("@s", slot.ToString())))
                b.ExecuteNonQuery();
            Log(c, "grant_item", characterId, id, null, $"{definitionId} x{quantity} slot {slot}");
            return id;
        });
    }

    /// <summary>Consome uma unidade de um item do inventário do personagem (apaga a instância quando zera).</summary>
    public ConsumeResult TryConsumeOne(string characterId, string itemId)
    {
        return InTransaction(c =>
        {
            string? definitionId = null;
            var quantity = 0;
            using (var q = Cmd(c,
                "SELECT i.definition_id, i.quantity FROM item_instances i JOIN item_locations l ON l.item_id = i.id " +
                "WHERE i.id=@i AND l.owner_character_id=@c AND l.location_kind='inventory'",
                ("@i", itemId), ("@c", characterId)))
            using (var r = q.ExecuteReader())
            {
                if (r.Read()) { definitionId = r.GetString(0); quantity = r.GetInt32(1); }
            }
            if (definitionId is null) return new ConsumeResult(false, "not_in_inventory", "", 0);

            if (quantity > 1)
            {
                using var u = Cmd(c, "UPDATE item_instances SET quantity = quantity - 1, version = version + 1 WHERE id=@i", ("@i", itemId));
                u.ExecuteNonQuery();
            }
            else
            {
                using (var d1 = Cmd(c, "DELETE FROM item_locations WHERE item_id=@i", ("@i", itemId))) d1.ExecuteNonQuery();
                using (var d2 = Cmd(c, "DELETE FROM item_instances WHERE id=@i", ("@i", itemId))) d2.ExecuteNonQuery();
            }
            Log(c, "consume_item", characterId, itemId, null, definitionId);
            return new ConsumeResult(true, "ok", definitionId, quantity - 1);
        });
    }

    /// <summary>
    /// Move um item do inventário para o slot de equipamento. Se o slot estiver ocupado, troca: o item que
    /// estava equipado ocupa o lugar do novo no inventário (tudo numa transação).
    /// </summary>
    public EquipResult TryEquip(string characterId, string itemId, string slot)
    {
        return InTransaction(c =>
        {
            int? inventorySlot = null;
            using (var q = Cmd(c,
                "SELECT slot FROM item_locations WHERE item_id=@i AND owner_character_id=@c AND location_kind='inventory'",
                ("@i", itemId), ("@c", characterId)))
                if (q.ExecuteScalar() is string text) inventorySlot = int.Parse(text);
            if (inventorySlot is null) return new EquipResult(false, "not_in_inventory", null);

            string? replaced;
            using (var q2 = Cmd(c,
                "SELECT item_id FROM item_locations WHERE owner_character_id=@c AND location_kind='equipment' AND slot=@s",
                ("@c", characterId), ("@s", slot)))
                replaced = q2.ExecuteScalar() as string;

            if (replaced is not null)
                using (var u0 = Cmd(c,
                    "UPDATE item_locations SET location_kind='inventory', slot='__swap' WHERE item_id=@i", ("@i", replaced)))
                    u0.ExecuteNonQuery();
            using (var u1 = Cmd(c,
                "UPDATE item_locations SET location_kind='equipment', slot=@s WHERE item_id=@i", ("@s", slot), ("@i", itemId)))
                u1.ExecuteNonQuery();
            if (replaced is not null)
            {
                // grade: o que sai do corpo vai para o lugar do que entrou, se couber; senão, para a primeira casa livre
                var target = inventorySlot.Value;
                if (GridOn)
                {
                    var occ = Occupied(c, characterId, replaced);
                    var size = SizeOf(DefinitionOf(c, replaced) ?? "");
                    if (!FitsAt(occ, target, size)) target = FirstFit(occ, size);
                    if (target < 0) throw new InventoryFullException();
                }
                using (var u2 = Cmd(c,
                    "UPDATE item_locations SET slot=@s WHERE item_id=@i", ("@s", target.ToString()), ("@i", replaced)))
                    u2.ExecuteNonQuery();
            }

            using (var v = Cmd(c, "UPDATE item_instances SET version = version + 1 WHERE id IN (@a, @b)",
                ("@a", itemId), ("@b", replaced ?? itemId)))
                v.ExecuteNonQuery();
            Log(c, "equip", characterId, itemId, null, $"{slot} replaced={replaced ?? "-"}");
            return new EquipResult(true, "ok", replaced);
        }, onInventoryFull: () => new EquipResult(false, "inventory_full", null));
    }

    public EquipResult TryUnequip(string characterId, string slot)
    {
        return InTransaction(c =>
        {
            string? itemId;
            using (var q = Cmd(c,
                "SELECT item_id FROM item_locations WHERE owner_character_id=@c AND location_kind='equipment' AND slot=@s",
                ("@c", characterId), ("@s", slot)))
                itemId = q.ExecuteScalar() as string;
            if (itemId is null) return new EquipResult(false, "nothing_equipped", null);

            var free = FindFreeInventorySlot(c, characterId, DefinitionOf(c, itemId) ?? "");
            if (free < 0) return new EquipResult(false, "inventory_full", null);

            using (var u = Cmd(c,
                "UPDATE item_locations SET location_kind='inventory', slot=@s WHERE item_id=@i",
                ("@s", free.ToString()), ("@i", itemId)))
                u.ExecuteNonQuery();
            using (var v = Cmd(c, "UPDATE item_instances SET version = version + 1 WHERE id=@i", ("@i", itemId)))
                v.ExecuteNonQuery();
            Log(c, "unequip", characterId, itemId, null, slot);
            return new EquipResult(true, "ok", itemId);
        });
    }

    public IReadOnlyList<EquippedEntry> GetEquipment(string characterId)
    {
        using var c = Connect();
        using var cmd = Cmd(c,
            "SELECT l.slot, i.id, i.definition_id, i.quality, i.temper_level FROM item_locations l " +
            "JOIN item_instances i ON i.id = l.item_id " +
            "WHERE l.owner_character_id=@c AND l.location_kind='equipment' ORDER BY l.slot",
            ("@c", characterId));
        var list = new List<EquippedEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new EquippedEntry(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4)));
        return list;
    }

    /// <summary>Casa livre de 1×1 (itens sem definição conhecida).</summary>
    private int FindFreeInventorySlot(SqliteConnection c, string characterId)
        => FirstFit(Occupied(c, characterId), (1, 1));

    private static void Log(SqliteConnection c, string kind, string? characterId, string? itemId, string? requestId, string? detail)
    {
        using var cmd = Cmd(c,
            "INSERT INTO transaction_log(kind,character_id,item_id,request_id,detail,created_at) VALUES(@k,@c,@i,@r,@d,@t)",
            ("@k", kind), ("@c", characterId), ("@i", itemId), ("@r", requestId), ("@d", detail), ("@t", Now()));
        cmd.ExecuteNonQuery();
    }

    public ItemLocation? GetItemLocation(string itemId)
    {
        using var c = Connect();
        using var cmd = Cmd(c,
            "SELECT location_kind, owner_character_id, slot FROM item_locations WHERE item_id=@i", ("@i", itemId));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new ItemLocation(r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2));
    }

    public int CountTransactionLog(string kind)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT COUNT(*) FROM transaction_log WHERE kind=@k", ("@k", kind));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ---------- E9: moedas, loja, atributos, missões ----------

    public long GetCoins(string characterId)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT coins FROM characters WHERE id=@i", ("@i", characterId));
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
    }

    /// <summary>Soma moedas (abate, recompensa). Idempotente por origem: repetir a mesma origem não paga de novo.</summary>
    public long AddCoins(string characterId, long amount, string originEventId)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT 1 FROM processed_requests WHERE request_id=@r", ("@r", originEventId)))
                if (q.ExecuteScalar() is not null) return ReadCoins(c, characterId);
            using (var u = Cmd(c, "UPDATE characters SET coins = coins + @n WHERE id=@i", ("@n", Math.Max(0, amount)), ("@i", characterId)))
                u.ExecuteNonQuery();
            MarkProcessed(c, originEventId, "coins", amount.ToString());
            Log(c, "coins", characterId, null, originEventId, $"+{amount}");
            return ReadCoins(c, characterId);
        });
    }

    /// <summary>Compra: debita e entrega na mesma transação. Idempotente por requestId (repetir devolve o resultado gravado).</summary>
    public TradeResult TryBuy(string requestId, string characterId, string definitionId, int quantity, long totalPrice, string quality = "comum")
    {
        return InTransaction(c =>
        {
            if (Processed(c, requestId) is { } done) return done;
            TradeResult result;
            var coins = ReadCoins(c, characterId);
            if (coins < totalPrice) result = new TradeResult(false, "not_enough_coins", coins);
            else if (!GrantInto(c, characterId, new GrantItem(definitionId, quantity, quality), "buy:" + requestId))
                result = new TradeResult(false, "inventory_full", coins);
            else
            {
                using (var u = Cmd(c, "UPDATE characters SET coins = coins - @p WHERE id=@i", ("@p", totalPrice), ("@i", characterId)))
                    u.ExecuteNonQuery();
                Log(c, "buy", characterId, null, requestId, $"{definitionId} x{quantity} por {totalPrice}");
                result = new TradeResult(true, "ok", coins - totalPrice);
            }
            MarkProcessed(c, requestId, "buy", JsonSerializer.Serialize(result));
            return result;
        });
    }

    /// <summary>Venda de um item da MOCHILA (equipado não): apaga o item e paga, na mesma transação. Idempotente.</summary>
    /// <summary>Jogar no chão (arrastar para fora da mochila): o item sai da mochila e vira item do mundo naquele ponto.</summary>
    public bool TryDropToWorld(string characterId, string itemId, double x, double y, string? mapId, bool doCorpo = false)
    {
        return InTransaction(c =>
        {
            // 13/11 ("arrastar o item da mão direto para o chão, mesmo com a mochila cheia"): doCorpo = sai do equipamento
            using (var u = Cmd(c,
                "UPDATE item_locations SET location_kind='world', owner_character_id=NULL, slot=NULL, world_x=@x, world_y=@y, world_map=@m " +
                "WHERE item_id=@i AND owner_character_id=@c AND location_kind=" + (doCorpo ? "'equipment'" : "'inventory'"),
                ("@x", x), ("@y", y), ("@m", mapId), ("@i", itemId), ("@c", characterId)))
                if (u.ExecuteNonQuery() != 1) return false;
            Log(c, "discard_item", characterId, itemId, null, $"{x:0.0};{y:0.0}");
            return true;
        });
    }

    /// <summary>Arrastar dentro da mochila: põe o item na casa pedida; se tiver outro lá, os dois trocam de lugar.</summary>
    public bool TryMoveInventorySlot(string characterId, string itemId, int toSlot)
    {
        if (toSlot < 0 || toSlot >= InventoryCapacity) return false;
        if (GridOn) return TryMoveInGrid(characterId, itemId, toSlot);
        return InTransaction(c =>
        {
            string? from;
            using (var q = Cmd(c, "SELECT slot FROM item_locations WHERE item_id=@i AND owner_character_id=@c AND location_kind='inventory'",
                ("@i", itemId), ("@c", characterId)))
                from = q.ExecuteScalar() as string;
            if (from is null) return false;
            var to = toSlot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (from == to) return true;
            string? other;
            using (var q = Cmd(c, "SELECT item_id FROM item_locations WHERE owner_character_id=@c AND location_kind='inventory' AND slot=@s",
                ("@c", characterId), ("@s", to)))
                other = q.ExecuteScalar() as string;
            void Set(string id, string slot)
            {
                using var u = Cmd(c, "UPDATE item_locations SET slot=@s WHERE item_id=@i", ("@s", slot), ("@i", id));
                u.ExecuteNonQuery();
            }
            if (other is not null) Set(other, "tmp_" + other);
            Set(itemId, to);
            if (other is not null) Set(other, from);
            return true;
        });
    }

    public TradeResult TrySell(string requestId, string characterId, string itemId, long unitPrice)
    {
        return InTransaction(c =>
        {
            if (Processed(c, requestId) is { } done) return done;
            TradeResult result;
            int quantity = 0;
            string? definitionId = null;
            using (var q = Cmd(c,
                "SELECT i.definition_id, i.quantity FROM item_instances i JOIN item_locations l ON l.item_id = i.id " +
                "WHERE i.id=@i AND l.owner_character_id=@c AND l.location_kind='inventory'", ("@i", itemId), ("@c", characterId)))
            using (var r = q.ExecuteReader())
                if (r.Read()) { definitionId = r.GetString(0); quantity = r.GetInt32(1); }
            if (definitionId is null) result = new TradeResult(false, "not_in_inventory", ReadCoins(c, characterId));
            else
            {
                using (var d1 = Cmd(c, "DELETE FROM item_locations WHERE item_id=@i", ("@i", itemId))) d1.ExecuteNonQuery();
                using (var d2 = Cmd(c, "DELETE FROM item_instances WHERE id=@i", ("@i", itemId))) d2.ExecuteNonQuery();
                var total = unitPrice * quantity;
                using (var u = Cmd(c, "UPDATE characters SET coins = coins + @p WHERE id=@i", ("@p", total), ("@i", characterId)))
                    u.ExecuteNonQuery();
                Log(c, "sell", characterId, itemId, requestId, $"{definitionId} x{quantity} por {total}");
                result = new TradeResult(true, "ok", ReadCoins(c, characterId));
            }
            MarkProcessed(c, requestId, "sell", JsonSerializer.Serialize(result));
            return result;
        });
    }

    /// <summary>Paga um serviço (viagem). Idempotente por requestId. Falha sem moedas suficientes, sem mudar nada.</summary>
    public TradeResult TrySpendCoins(string characterId, long amount, string requestId)
    {
        return InTransaction(c =>
        {
            if (Processed(c, requestId) is { } done) return done;
            var coins = ReadCoins(c, characterId);
            var result = coins < amount ? new TradeResult(false, "not_enough_coins", coins) : new TradeResult(true, "ok", coins - amount);
            if (result.Success)
            {
                using var u = Cmd(c, "UPDATE characters SET coins = coins - @p WHERE id=@i", ("@p", amount), ("@i", characterId));
                u.ExecuteNonQuery();
                Log(c, "spend_coins", characterId, null, requestId, amount.ToString());
            }
            MarkProcessed(c, requestId, "spend", JsonSerializer.Serialize(result));
            return result;
        });
    }

    /// <summary>Gasta 1 ponto em "primary" (Força/Sintonia) ou "vigor". Falso sem ponto livre.</summary>
    public bool TrySpendStatPoint(string characterId, string stat)
    {
        var column = stat switch
        {
            "primary" => "strength", "vigor" => "vigor",
            "forca" => "attr_forca", "agilidade" => "attr_agilidade", "energia" => "attr_energia",
            _ => null,
        };
        if (column is null) return false;
        return InTransaction(c =>
        {
            using var u = Cmd(c, $"UPDATE character_stats SET stat_points = stat_points - 1, {column} = {column} + 1 " +
                                 "WHERE character_id=@i AND stat_points > 0", ("@i", characterId));
            if (u.ExecuteNonQuery() != 1) return false;
            Log(c, "stat_point", characterId, null, null, stat);
            return true;
        });
    }

    /// <summary>
    /// Refino estilo MU (11/11): consome 1 joia da mochila e muda o +N do item (sobe no sucesso, desce no fracasso).
    /// Tudo numa transação: falta de joia ou item fora da mochila/equipamento do dono = nada muda (false).
    /// </summary>
    /// <summary>13/11 (reforjar): gasta 1 joia e troca a qualidade (variação V) do item — numa transação.</summary>
    public bool TryReforge(string characterId, string jewelItemId, string targetItemId, string newQuality, long coins)
    {
        if (!TryRefineCore(characterId, jewelItemId, targetItemId, coins, out _)) return false;
        return InTransaction(c =>
        {
            using var u = Cmd(c, "UPDATE item_instances SET quality=@q, version=version+1 WHERE id=@t", ("@q", newQuality), ("@t", targetItemId));
            u.ExecuteNonQuery();
            Log(c, "reforge", characterId, targetItemId, null, newQuality);
            return true;
        });
    }

    private bool TryRefineCore(string characterId, string jewelItemId, string targetItemId, long coins, out int temper)
    {
        temper = 0;
        var t = 0;
        var ok = InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT i.temper_level FROM item_instances i JOIN item_locations l ON l.item_id=i.id WHERE i.id=@t AND l.owner_character_id=@c AND l.location_kind IN ('inventory','equipment')",
                       ("@t", targetItemId), ("@c", characterId)))
            {
                var v = q.ExecuteScalar();
                if (v is null) return false;
                t = Convert.ToInt32(v);
            }
            return true;
        });
        temper = t;
        return ok && TryRefine(characterId, jewelItemId, targetItemId, t, coins);   // refino "para o mesmo +": só gasta a joia e as moedas
    }

    public bool TryRefine(string characterId, string jewelItemId, string targetItemId, int newLevel, long coins)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT COUNT(*) FROM item_locations WHERE item_id=@t AND owner_character_id=@c AND location_kind IN ('inventory','equipment')",
                       ("@t", targetItemId), ("@c", characterId)))
                if (Convert.ToInt32(q.ExecuteScalar()) == 0) return false;
            if (coins > 0 && ReadCoins(c, characterId) < coins) return false;
            using (var q = Cmd(c, "SELECT i.quantity FROM item_instances i JOIN item_locations l ON l.item_id=i.id WHERE i.id=@j AND l.owner_character_id=@c AND l.location_kind='inventory'",
                       ("@j", jewelItemId), ("@c", characterId)))
            {
                var qty = q.ExecuteScalar();
                if (qty is null) return false;
                if (Convert.ToInt32(qty) > 1)
                {
                    using var u = Cmd(c, "UPDATE item_instances SET quantity=quantity-1, version=version+1 WHERE id=@j", ("@j", jewelItemId));
                    u.ExecuteNonQuery();
                }
                else
                {
                    using var d1 = Cmd(c, "DELETE FROM item_locations WHERE item_id=@j", ("@j", jewelItemId)); d1.ExecuteNonQuery();
                    using var d2 = Cmd(c, "DELETE FROM item_instances WHERE id=@j", ("@j", jewelItemId)); d2.ExecuteNonQuery();
                }
            }
            if (coins > 0)
            {
                using var u = Cmd(c, "UPDATE characters SET coins = coins - @n WHERE id=@i", ("@n", coins), ("@i", characterId)); u.ExecuteNonQuery();
            }
            using (var u = Cmd(c, "UPDATE item_instances SET temper_level=@l, version=version+1 WHERE id=@t", ("@l", Math.Clamp(newLevel, 0, 15)), ("@t", targetItemId)))
                u.ExecuteNonQuery();
            Log(c, "refine", characterId, targetItemId, null, $"+{newLevel}");
            return true;
        });
    }

    /// <summary>
    /// Reinicia os pontos gastos (11/11, Sibila no Porto): devolve como pontos livres tudo o que foi gasto com
    /// <see cref="TrySpendStatPoint"/> desde o último reinício (os presentes da 2ª classe e os iniciais ficam) e cobra
    /// <paramref name="cost"/> moedas. -1 = moedas insuficientes; 0 = nada para devolver (não cobra).
    /// </summary>
    public int TryResetStats(string characterId, long cost)
    {
        return InTransaction(c =>
        {
            long since = 0;
            using (var q = Cmd(c, "SELECT COALESCE(MAX(id),0) FROM transaction_log WHERE kind='stat_reset' AND character_id=@i", ("@i", characterId)))
                since = Convert.ToInt64(q.ExecuteScalar());
            var spent = new Dictionary<string, int>();
            using (var q = Cmd(c, "SELECT detail, COUNT(*) FROM transaction_log WHERE kind='stat_point' AND character_id=@i AND id>@s GROUP BY detail",
                       ("@i", characterId), ("@s", since)))
            using (var r = q.ExecuteReader())
                while (r.Read()) spent[r.IsDBNull(0) ? "" : r.GetString(0)] = r.GetInt32(1);
            if (spent.Values.Sum() == 0) return 0;
            if (cost > 0 && ReadCoins(c, characterId) < cost) return -1;
            var total = 0;
            foreach (var (stat, n) in spent)
            {
                var column = stat switch
                {
                    "primary" => "strength", "vigor" => "vigor",
                    "forca" => "attr_forca", "agilidade" => "attr_agilidade", "energia" => "attr_energia",
                    _ => null,
                };
                if (column is null || n <= 0) continue;
                using var cur = Cmd(c, $"SELECT {column} FROM character_stats WHERE character_id=@i", ("@i", characterId));
                var have = Convert.ToInt32(cur.ExecuteScalar() ?? 0);
                var back = Math.Min(n, have);
                if (back <= 0) continue;
                using var u = Cmd(c, $"UPDATE character_stats SET {column} = {column} - @n, stat_points = stat_points + @n WHERE character_id=@i",
                    ("@n", back), ("@i", characterId));
                u.ExecuteNonQuery();
                total += back;
            }
            if (total == 0) return 0;
            if (cost > 0)
            {
                using var u = Cmd(c, "UPDATE characters SET coins = coins - @n WHERE id=@i", ("@n", cost), ("@i", characterId));
                u.ExecuteNonQuery();
            }
            Log(c, "stat_reset", characterId, null, null, $"{total} pontos, {cost} moedas");
            return total;
        });
    }

    /// <summary>
    /// Atributos dados de presente (11/11: bônus da 2ª classe), sem gastar pontos. Chaves como em <see cref="TrySpendStatPoint"/>
    /// ("primary", "vigor", "forca", "agilidade", "energia"); "points" soma pontos livres. Tudo numa transação.
    /// </summary>
    public void GrantAttributes(string characterId, IReadOnlyDictionary<string, int> bonus, string reason)
    {
        Write(c =>
        {
            foreach (var (stat, amount) in bonus)
            {
                var column = stat switch
                {
                    "primary" => "strength", "vigor" => "vigor", "points" => "stat_points",
                    "forca" => "attr_forca", "agilidade" => "attr_agilidade", "energia" => "attr_energia",
                    _ => null,
                };
                if (column is null || amount <= 0) continue;
                using var u = Cmd(c, $"UPDATE character_stats SET {column} = {column} + @n WHERE character_id=@i", ("@i", characterId), ("@n", amount));
                u.ExecuteNonQuery();
            }
            Log(c, "stat_gift", characterId, null, null, reason);
        });
    }

    /// <summary>Maior nível já alcançado (0 = nunca gravado).</summary>
    public int GetMaxLevel(string characterId)
    {
        using var c = Connect();
        using var q = Cmd(c, "SELECT max_level FROM characters WHERE id=@i", ("@i", characterId));
        return q.ExecuteScalar() is { } v ? Convert.ToInt32(v) : 0;
    }

    public void RaiseMaxLevel(string characterId, int level)
        => Write(c => { using var u = Cmd(c, "UPDATE characters SET max_level = @l WHERE id=@i AND max_level < @l", ("@i", characterId), ("@l", level)); u.ExecuteNonQuery(); });

    public void AddStatPoints(string characterId, int points)
    {
        Write(c =>
        {
            using var u = Cmd(c, "UPDATE character_stats SET stat_points = stat_points + @n WHERE character_id=@i",
                ("@n", Math.Max(0, points)), ("@i", characterId));
            u.ExecuteNonQuery();
        });
    }

    /// <summary>Pontos extras nos atributos que não são o principal da classe.</summary>
    public (int Forca, int Agilidade, int Energia) GetExtraAttributes(string characterId)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT attr_forca, attr_agilidade, attr_energia FROM character_stats WHERE character_id=@i", ("@i", characterId));
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2)) : (0, 0, 0);
    }

    public (int Primary, int Vigor, int Points) GetStats(string characterId)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT strength, vigor, stat_points FROM character_stats WHERE character_id=@i", ("@i", characterId));
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2)) : (0, 0, 0);
    }

    public IReadOnlyList<QuestState> GetQuests(string characterId)
    {
        using var c = Connect();
        using var cmd = Cmd(c, "SELECT quest_id, state, progress, completions FROM character_quests WHERE character_id=@c ORDER BY updated_at",
            ("@c", characterId));
        var list = new List<QuestState>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new QuestState(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3)));
        return list;
    }

    /// <summary>12/11 (modo teste): esquece a missão (como se nunca tivesse sido aceita) — para refazer e testar.</summary>
    public void ResetQuest(string characterId, string questId)
    {
        InTransaction(c =>
        {
            using (var d = Cmd(c, "DELETE FROM character_quests WHERE character_id=@c AND quest_id=@q", ("@c", characterId), ("@q", questId)))
                d.ExecuteNonQuery();
            Log(c, "quest_reset", characterId, null, null, questId);
            return true;
        });
    }

    /// <summary>13/11: missão "sem morrer" — o progresso volta a zero (continua ativa).</summary>
    public void ResetQuestProgress(string characterId, string questId)
    {
        InTransaction(c =>
        {
            using (var u = Cmd(c, "UPDATE character_quests SET progress=0, updated_at=@t WHERE character_id=@c AND quest_id=@q AND state='active'",
                ("@t", Now()), ("@c", characterId), ("@q", questId)))
                u.ExecuteNonQuery();
            return true;
        });
    }

    /// <summary>Aceita (ou reaceita uma repetível já concluída). Falso se já está ativa ou concluída sem repetição.</summary>
    public bool TryAcceptQuest(string characterId, string questId, bool repeatable)
    {
        return InTransaction(c =>
        {
            string? state;
            using (var q = Cmd(c, "SELECT state FROM character_quests WHERE character_id=@c AND quest_id=@q", ("@c", characterId), ("@q", questId)))
                state = q.ExecuteScalar() as string;
            if (state == "active" || (state == "done" && !repeatable)) return false;
            using (var u = Cmd(c,
                "INSERT INTO character_quests(character_id,quest_id,state,progress,completions,updated_at) VALUES(@c,@q,'active',0,0,@t) " +
                "ON CONFLICT(character_id,quest_id) DO UPDATE SET state='active', progress=0, updated_at=@t",
                ("@c", characterId), ("@q", questId), ("@t", Now())))
                u.ExecuteNonQuery();
            Log(c, "quest_accept", characterId, null, null, questId);
            return true;
        });
    }

    /// <summary>Avança uma missão ativa (até o máximo). Devolve o novo progresso, ou -1 se ela não está ativa.</summary>
    public int AdvanceQuest(string characterId, string questId, int amount, int max)
    {
        return InTransaction(c =>
        {
            using (var u = Cmd(c,
                "UPDATE character_quests SET progress = MIN(@max, progress + @n), updated_at=@t WHERE character_id=@c AND quest_id=@q AND state='active'",
                ("@max", max), ("@n", amount), ("@t", Now()), ("@c", characterId), ("@q", questId)))
                if (u.ExecuteNonQuery() != 1) return -1;
            using var q = Cmd(c, "SELECT progress FROM character_quests WHERE character_id=@c AND quest_id=@q", ("@c", characterId), ("@q", questId));
            return Convert.ToInt32(q.ExecuteScalar());
        });
    }

    /// <summary>
    /// Conclui uma missão ativa: confere o progresso (ou retira os itens a entregar), entrega recompensas e marca
    /// concluída — tudo numa transação. Pedido repetido não paga duas vezes (a missão já não está ativa).
    /// </summary>
    public TradeResult TryCompleteQuest(string characterId, string questId, int requiredProgress,
        string? deliverDefinition, int deliverCount, IReadOnlyList<GrantItem> rewards, long rewardCoins)
    {
        return InTransaction(c =>
        {
            int progress;
            using (var q = Cmd(c, "SELECT progress FROM character_quests WHERE character_id=@c AND quest_id=@q AND state='active'",
                ("@c", characterId), ("@q", questId)))
            {
                if (q.ExecuteScalar() is not { } p) return new TradeResult(false, "not_active", ReadCoins(c, characterId));
                progress = Convert.ToInt32(p);
            }
            if (deliverDefinition is not null)
            {
                if (!RemoveFromInventory(c, characterId, deliverDefinition, deliverCount))
                    return new TradeResult(false, "missing_items", ReadCoins(c, characterId));
            }
            else if (progress < requiredProgress) return new TradeResult(false, "not_done", ReadCoins(c, characterId));

            var index = 0;
            foreach (var item in rewards)
                if (!GrantInto(c, characterId, item, $"quest:{questId}:{characterId}:{Now()}:{index++}"))
                    throw new InventoryFullException();
            using (var u = Cmd(c, "UPDATE characters SET coins = coins + @n WHERE id=@i", ("@n", rewardCoins), ("@i", characterId)))
                u.ExecuteNonQuery();
            using (var u2 = Cmd(c,
                "UPDATE character_quests SET state='done', completions = completions + 1, updated_at=@t WHERE character_id=@c AND quest_id=@q",
                ("@t", Now()), ("@c", characterId), ("@q", questId)))
                u2.ExecuteNonQuery();
            Log(c, "quest_complete", characterId, null, null, questId);
            return new TradeResult(true, "ok", ReadCoins(c, characterId));
        }, onInventoryFull: () => new TradeResult(false, "inventory_full", GetCoins(characterId)));
    }

    /// <summary>Gasta N unidades de um item da mochila (altar de oferenda). Tudo ou nada; registra no histórico.</summary>
    public bool TryConsumeDefinition(string characterId, string definitionId, int count, string reason)
    {
        return InTransaction(c =>
        {
            if (!RemoveFromInventory(c, characterId, definitionId, count)) return false;
            Log(c, "offering", characterId, null, null, $"{definitionId} x{count} ({reason})");
            return true;
        });
    }

    /// <summary>Quantidade total de um item na mochila (somando pilhas).</summary>
    public int CountInInventory(string characterId, string definitionId)
    {
        using var c = Connect();
        using var cmd = Cmd(c,
            "SELECT COALESCE(SUM(i.quantity),0) FROM item_instances i JOIN item_locations l ON l.item_id = i.id " +
            "WHERE l.owner_character_id=@c AND l.location_kind='inventory' AND i.definition_id=@d", ("@c", characterId), ("@d", definitionId));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private bool RemoveFromInventory(SqliteConnection c, string characterId, string definitionId, int count)
    {
        var stacks = new List<(string Id, int Qty)>();
        using (var q = Cmd(c,
            "SELECT i.id, i.quantity FROM item_instances i JOIN item_locations l ON l.item_id = i.id " +
            "WHERE l.owner_character_id=@c AND l.location_kind='inventory' AND i.definition_id=@d ORDER BY i.quantity",
            ("@c", characterId), ("@d", definitionId)))
        using (var r = q.ExecuteReader())
            while (r.Read()) stacks.Add((r.GetString(0), r.GetInt32(1)));
        if (stacks.Sum(x => x.Qty) < count) return false;
        var left = count;
        foreach (var (id, qty) in stacks)
        {
            if (left <= 0) break;
            if (qty <= left)
            {
                using (var d1 = Cmd(c, "DELETE FROM item_locations WHERE item_id=@i", ("@i", id))) d1.ExecuteNonQuery();
                using (var d2 = Cmd(c, "DELETE FROM item_instances WHERE id=@i", ("@i", id))) d2.ExecuteNonQuery();
                left -= qty;
            }
            else
            {
                using (var u = Cmd(c, "UPDATE item_instances SET quantity = quantity - @n, version = version + 1 WHERE id=@i", ("@n", left), ("@i", id)))
                    u.ExecuteNonQuery();
                left = 0;
            }
        }
        Log(c, "deliver_items", characterId, null, null, $"{definitionId} x{count}");
        return true;
    }

    private static long ReadCoins(SqliteConnection c, string characterId)
    {
        using var q = Cmd(c, "SELECT coins FROM characters WHERE id=@i", ("@i", characterId));
        return Convert.ToInt64(q.ExecuteScalar() ?? 0L);
    }

    private static TradeResult? Processed(SqliteConnection c, string requestId)
    {
        using var q = Cmd(c, "SELECT result_json FROM processed_requests WHERE request_id=@r", ("@r", requestId));
        return q.ExecuteScalar() is string json ? JsonSerializer.Deserialize<TradeResult>(json) : null;
    }

    private static void MarkProcessed(SqliteConnection c, string requestId, string kind, string json)
    {
        using var ins = Cmd(c, "INSERT INTO processed_requests(request_id,kind,result_json,created_at) VALUES(@r,@k,@j,@t)",
            ("@r", requestId), ("@k", kind), ("@j", json), ("@t", Now()));
        ins.ExecuteNonQuery();
    }

    // ---------- infraestrutura ----------

    private SqliteConnection Connect()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        Exec(c, "PRAGMA busy_timeout = 5000;");
        return c;
    }

    private T InTransaction<T>(Func<SqliteConnection, T> work)
    {
        using var c = Connect();
        Exec(c, "BEGIN IMMEDIATE;");
        try
        {
            var result = work(c);
            Exec(c, "COMMIT;");
            return result;
        }
        catch
        {
            try { Exec(c, "ROLLBACK;"); } catch { /* conexão já sem transação */ }
            throw;
        }
    }

    private T InTransaction<T>(Func<SqliteConnection, T> work, Func<T> onInventoryFull)
    {
        try { return InTransaction(work); }
        catch (InventoryFullException) { return onInventoryFull(); }
    }

    private void Write(Action<SqliteConnection> work)
    {
        InTransaction<bool>(c => { work(c); return true; });
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static SqliteCommand Cmd(SqliteConnection c, string sql, params (string Name, object? Value)[] ps)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in ps)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private static string Now() => DateTime.UtcNow.ToString("O");
}
