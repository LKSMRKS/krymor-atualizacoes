using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Tests;

/// <summary>E8 — lógica do cliente sem Godot: movimento relativo à câmera, interação, regiões, catálogo.</summary>
public class ClientMapTests
{
    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, true)]
    public void CameraComYawZero_MoveIgualAoControleAntigo(bool up, bool down, bool left, bool right)
    {
        var (ex, ey) = MoveInput.Direction(up, down, left, right);
        var (x, y) = CameraMove.Direction(up, down, left, right, 0);
        Assert.Equal(ex, x, 9);
        Assert.Equal(ey, y, 9);
    }

    [Fact]
    public void FrenteSegueACamera_EYawForEInversa()
    {
        var yaw = Math.PI / 2; // câmera girada 90° para a esquerda: frente = -x
        var (x, y) = CameraMove.Direction(true, false, false, false, yaw);
        Assert.Equal(-1.0, x, 9);
        Assert.Equal(0.0, y, 9);
        var (rx, ry) = CameraMove.Direction(false, false, false, true, yaw); // direita = -y (norte)
        Assert.Equal(0.0, rx, 9);
        Assert.Equal(-1.0, ry, 9);
        Assert.Equal(yaw, CameraMove.YawFor(x, y), 9);
    }

    private static ClientModel ModelAt(double x, double y, params WorldItemView[] items)
    {
        var m = new ClientModel();
        m.Apply(new LoginResult(true, null, "me", "ana", 5));
        var map = new MapLayout("t", "T", 30, 0, 0, 3, 1,
            new[] { new MapZone("z1", "Pátio", "forte", 10, 10, 6, 6), new MapZone("z0", "Vale", "fraco", 0, 0, 60, 60) },
            Array.Empty<MapObstacle>(), new[] { new MapGate("g", "gate:g", 5, 0, 4, 1, 5, 2) },
            new[] { new MapChest("c", -3, 0, "x", 0) }, Array.Empty<MapSpawn>(), Array.Empty<MapProp>());
        m.Apply(new MapMessage(map));
        m.Apply(new Snapshot(1, new[] { new PlayerView("me", "ana", x, y, 300, 300, 100, true, false, 5, 1, 0) },
            Array.Empty<MonsterView>(), items));
        return m;
    }

    [Fact]
    public void Interacao_ApontaOQueEstaPerto_EsconeBauAbertoEPortaoAberto()
    {
        var m = ModelAt(-2, 0);
        Assert.Equal(("chest", "chest:c"), (m.NearestInteractable()!.Value.Kind, m.NearestInteractable()!.Value.Id));
        m.Apply(new FlagsMessage(new[] { "chest:c" }));
        Assert.Null(m.NearestInteractable());

        var atLever = ModelAt(5, 1.5);
        Assert.Equal("lever:g", atLever.NearestInteractable()!.Value.Id);
        atLever.Apply(new FlagsMessage(new[] { "gate:g" }));
        Assert.Null(atLever.NearestInteractable());

        var withItem = ModelAt(-2, 0, new WorldItemView("i1", "consumable_c01_tonico_fraco", -2.2, 0));
        Assert.Equal("item", withItem.NearestInteractable()!.Value.Kind);
    }

    [Fact]
    public void Regiao_MaisEspecificaVence()
    {
        var m = ModelAt(0, 0);
        Assert.Equal("Pátio", m.ZoneAt(10, 10)!.Name);
        Assert.Equal("Vale", m.ZoneAt(0, 0)!.Name);
        Assert.Null(m.ZoneAt(40, 40));
    }

    [Fact]
    public void Catalogo_DaNomesERecargasDasHabilidadesDaClasse()
    {
        var m = new ClientModel();
        m.Apply(new CatalogMessage(
            new[] { new ItemInfo("staff_x", "Cajado X", "weapon_main", new[] { "mago" }, "Poder 10.", "staff", 1, 2) },
            new[] { new SkillInfo("skill_x", "mago", "Raio", "skill1", 7000, 30, "ground", 12, 2.8, "") },
            "Mana", 100));
        Assert.Equal("Cajado X", ClientModel.DisplayName("staff_x"));
        Assert.Equal("Raio", ClientModel.SkillName("skill_x"));
        Assert.Equal("Mana", m.ResourceName);
        var cd = new CooldownTracker();
        cd.Register("skill_x", 7000);
        cd.StartSkill("skill_x", 0);
        Assert.Equal(3500, cd.RemainingMs("skill_x", 3500));
    }

    [Fact]
    public void AlvoNaDirecao_EscolheOMaisAlinhado()
    {
        var m = new ClientModel();
        m.Apply(new LoginResult(true, null, "me", "ana", 5));
        m.Apply(new Snapshot(1, new[] { new PlayerView("me", "ana", 0, 0, 300, 300, 100, true, false, 5, 1, 0) },
            new[]
            {
                new MonsterView("a", "A", 5, 0.2, 10, 10, true),
                new MonsterView("b", "B", 0, 5, 10, 10, true),
                new MonsterView("c", "C", 3, 0, 10, 10, false),
            }, Array.Empty<WorldItemView>()));
        Assert.Equal("a", m.BestMonsterInDirection(1, 0, 15)!.Id);
        Assert.Equal("b", m.BestMonsterInDirection(0, 1, 15)!.Id);
        Assert.Null(m.BestMonsterInDirection(-1, 0, 15));
    }
}
