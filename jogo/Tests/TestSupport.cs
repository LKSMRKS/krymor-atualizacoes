using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;
using Microsoft.Data.Sqlite;

namespace CinzasDaMare.Tests;

internal static class TestContent
{
    public const string Golpe = "skill_guerreiro_golpe_de_cunha";
    public const string Guarda = "skill_guerreiro_guarda_de_escudo";
    public const string Sword = "weapon_guerreiro_sword_p1";

    public static ContentBundle Load() => ContentLoader.Load(Path.Combine(AppContext.BaseDirectory, "Content"));

    // Atributos 10/10 são premissa provisória de teste (o GDD não define atributos iniciais).
    public static PlayerCombatState Warrior(ContentBundle b, int level = 1, int strength = 10, int vigor = 10)
        => new("p1", b.Classes["guerreiro"], level, strength, vigor, b.Items[Sword].BaseStats["weapon_power"]);

    public static MonsterCombatState Crab(ContentBundle b)
        => new("m1", b.Monsters["monster_r1_caranguejo_de_cal"]);
}

internal sealed class TempDb : IDisposable
{
    public string Dir { get; }
    public string DbPath { get; }
    public GameDatabase Db { get; }

    public TempDb(int inventoryCapacity = GameDatabase.DefaultInventoryCapacity)
    {
        Dir = Path.Combine(Path.GetTempPath(), "cdm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
        DbPath = Path.Combine(Dir, "game.db");
        Db = new GameDatabase(DbPath, inventoryCapacity);
        Db.Initialize();
    }

    public string NewCharacter(string name = "Heroi")
    {
        var account = Db.CreateAccount("conta_" + name, "hash_de_teste");
        return Db.CreateCharacter(account, name, "guerreiro", 1, 10, 10);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Dir, true); } catch { /* limpeza best-effort */ }
    }
}
